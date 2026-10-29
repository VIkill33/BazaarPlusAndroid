using System.Text.Json;
using TheBazaar;
using UnityEngine;

namespace BazaarPlusPlus.Android;

public sealed class AndroidRunObserver(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static AndroidRunObserver? Instance;
    internal readonly HistorySessionCore Session = new(Guid.NewGuid().ToString("D"));
    private readonly HistoryRunContinuity _continuity = new();
    private sealed record Write(long Order, object Dto, Action<JsonElement> Complete, Task<JsonElement> Task);
    private readonly Queue<Write> _pendingWrites = new();
    private readonly SortedDictionary<long, Action> _retry = new();
    private long _writeOrder;
    private readonly Dictionary<string, Ticket> _tickets = new();
    private Observation? _last;
    private long _sequence;
    private string? _localRun;
    private HistoryScope? _boundScope;
    private float _next;
    private bool _installed;
    private readonly Queue<(HistoryScope Scope, Dictionary<string, object?> Request)> _purchases = new();
    private bool _purchaseWriting;
    private float _nextRetry;

    private sealed record Observation(HistoryScope Scope, HistoryPhase Phase, string? Checkpoint,
        uint Day, uint Hour, uint Victories, uint Losses, string? Hero, string? Mode, bool ProgressAvailable);
    internal sealed class Ticket(HistoryWindow window, string runId)
    {
        internal readonly HistoryWindow Window = window;
        internal readonly string RunId = runId;
        internal bool BeforeCommitted;
        internal Dictionary<string, object?>? Result;
        internal bool ResultQueued;
    }

    public void Update()
    {
        Instance = this;
        DrainWrites();
        if (!_installed)
        {
            _installed = true;
            try { AndroidHistoryNativeCapture.Install(); }
            catch (Exception error) { Failure(error); }
            try { AndroidPurchaseCapture.Install(); }
            catch (Exception error) { Failure(error); }
        }
        if (_retry.Count > 0 && _pendingWrites.Count == 0 && Time.realtimeSinceStartup >= _nextRetry)
        {
            _nextRetry = Time.realtimeSinceStartup + 5;
            Retry();
        }
        FlushPurchases();
        try { AndroidHistoryNativeCapture.Tick(); } catch (Exception error) { Failure(error); }
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + .5f;
        try { Observe(); AndroidBattleCapture.WarmOne(); }
        catch (Exception error) { Failure(error); }
    }

    internal void Purchase(PurchaseFact fact)
    {
        Observe();
        if (CurrentPhase() is not (HistoryPhase.Choice or HistoryPhase.Encounter or HistoryPhase.Loot or HistoryPhase.LevelUp)
            || Session.Scope is not { Confirmed: true } scope)
        {
            Failure(new InvalidOperationException("购牌身份未确认，未归入其他账号"));
            return;
        }
        if (_purchases.Count >= 512)
        {
            Failure(new InvalidOperationException("购牌待保存队列已满"));
            return;
        }
        var request = Request("recordPurchase", scope);
        request["purchaseId"] = Guid.NewGuid().ToString("D");
        request["card"] = fact; request["day"] = _last?.Day ?? 0; request["hour"] = _last?.Hour ?? 0;
        _purchases.Enqueue((scope, request));
        FlushPurchases();
    }

    private void FlushPurchases()
    {
        if (_purchaseWriting || _pendingWrites.Count != 0 || _retry.Count != 0) return;
        while (_purchases.TryPeek(out var pending))
        {
            if (Session.Scope != pending.Scope)
            {
                _purchases.Dequeue();
                Failure(new InvalidOperationException("购牌保存前账号或对局已改变，未归入新账号"));
                continue;
            }
            if (_boundScope != pending.Scope || _localRun == null) return;
            pending.Request["runId"] = _localRun;
            _purchaseWriting = true;
            Submit(pending.Request, response =>
            {
                var state = response.GetProperty("state").GetString();
                if (state != "recorded") Failure(new InvalidOperationException("购牌未保存：" + state));
                _purchases.Dequeue();
                _purchaseWriting = false;
            });
            return;
        }
    }

    internal static HistoryPhase CurrentPhase() => AppState.CurrentState?.GetIl2CppType().Name switch
    {
        "ChoiceState" => HistoryPhase.Choice, "EncounterState" => HistoryPhase.Encounter,
        "LootState" => HistoryPhase.Loot, "LevelUpState" => HistoryPhase.LevelUp,
        "CombatState" => HistoryPhase.Combat, "PVPCombatState" => HistoryPhase.PvpCombat,
        "ReplayState" => HistoryPhase.Replay, "EndRunVictoryState" => HistoryPhase.Victory,
        "EndRunDefeatState" => HistoryPhase.Defeat, _ => HistoryPhase.Unknown,
    };

    internal void Observe()
    {
        var phase = CurrentPhase();
        var run = Data.Run;
        var account = ClientCache.Profile?.HasData == true ? ClientCache.Profile.Value?.AccountId.ToString() : null;
        var hero = run?.Player?.Hero.ToString();
        if (hero == "Common") hero = null;
        var identity = _continuity.Resolve(account, AppState._cachedRunInitialized?.RunId, phase,
            run == null ? null : new HistoryProgress(run.Day, run.Hour, run.Victories, run.Losses, hero, run.PlayMode.ToString()));
        var previousScope = Session.Scope;
        var scope = Session.Observe(identity.AccountId, identity.NativeRunId, phase);
        if (previousScope != scope) _tickets.Clear();
        var progress = identity.Progress;
        var observed = new Observation(scope, phase, Session.CheckpointId, progress?.Day ?? 0, progress?.Hour ?? 0,
            progress?.Victories ?? 0, progress?.Losses ?? 0, progress?.Hero, progress?.Mode, identity.ProgressAvailable);
        if (observed == _last) return;
        _last = observed;
        var sequence = ++_sequence;
        var request = Request("observeRun", scope);
        request["observationSequence"] = sequence; request["accountId"] = scope.AccountId;
        request["nativeRunId"] = scope.NativeRunId; request["checkpointId"] = observed.Checkpoint;
        request["phase"] = PhaseName(phase); request["hero"] = observed.Hero; request["playMode"] = observed.Mode;
        request["day"] = observed.Day; request["hour"] = observed.Hour;
        request["victories"] = observed.Victories; request["losses"] = observed.Losses;
        request["progressAvailable"] = observed.ProgressAvailable;
        request["gameVersion"] = Application.version; request["pluginVersion"] = Plugin.Version;
        request["timezoneOffset"] = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        Submit(request, response =>
        {
            if (Session.Scope != scope || sequence != _sequence) return;
            var state = response.GetProperty("state").GetString();
            if (state != "bound") return;
            _localRun = response.GetProperty("runId").GetString();
            if (_boundScope != scope)
            {
                var evidence = response.GetProperty("evidence").EnumerateArray().Select(value => new HistoryOpeningEvidence(
                    value.GetProperty("accountId").GetString()!, value.GetProperty("nativeRunId").GetString()!,
                    value.GetProperty("sessionId").GetString()!, value.GetProperty("checkpointId").GetString()!,
                    value.GetProperty("messageId").GetString()!, value.GetProperty("windowId").GetString()!)).ToArray();
                Session.RestoreEvidence(evidence);
                if (!Session.ConfirmBinding(scope, response.GetProperty("allowFirstPartial").GetBoolean())) return;
                _boundScope = scope;
                Observe(); // Persist the newly allocated explicit checkpoint before any opening request.
            }
        });
    }

    internal Ticket? Begin(string? messageId, HistoryPhase phase, object opening, object snapshots, uint day, uint hour, bool completeSnapshot = true)
    {
        var decision = Session.Begin(messageId, phase, day, hour);
        if (decision.Decision is HistoryCaptureDecision.Replay or HistoryCaptureDecision.Ignored) return null;
        if (decision.Decision == HistoryCaptureDecision.Duplicate)
            return decision.Window != null && _tickets.TryGetValue(decision.Window.Id, out var existing) ? existing : null;
        if (decision.Window == null || _localRun == null || _boundScope != decision.Window.Scope)
        {
            if (Session.Scope is { Confirmed: true } scope && _localRun != null && _boundScope == scope)
            {
                var diagnostic = Request("quarantine", scope);
                diagnostic["runId"] = _localRun; diagnostic["reason"] = decision.Reason;
                diagnostic["opening"] = opening; diagnostic["snapshots"] = snapshots;
                Submit(diagnostic, _ => { });
            }
            return null;
        }
        var ticket = new Ticket(decision.Window, _localRun);
        _tickets.Add(ticket.Window.Id, ticket);
        var request = Request("beginBattle", ticket.Window.Scope);
        request["runId"] = ticket.RunId; request["windowId"] = ticket.Window.Id;
        request["checkpointId"] = ticket.Window.CheckpointId; request["messageId"] = ticket.Window.MessageId;
        request["kind"] = "pvp";
        request["partialOpening"] = ticket.Window.PartialOpening;
        request["day"] = day; request["hour"] = hour; request["opening"] = opening; request["snapshots"] = snapshots;
        SaveOpening(ticket, request);
        return ticket;
    }

    private void SaveOpening(Ticket ticket, Dictionary<string, object?> request, long? originalOrder = null)
    {
        var order = originalOrder ?? ++_writeOrder;
        Submit(request, response =>
        {
            Observe();
            if (Session.Scope != ticket.Window.Scope) return;
            if (response.GetProperty("state").GetString() != "recorded")
            {
                var reason = response.TryGetProperty("reason", out var detail) ? detail.GetString() : null;
                if (reason == "checkpoint_not_proven" || response.GetProperty("state").GetString() == "stale")
                    RetainRetry(order, () => SaveOpening(ticket, request, order));
                Failure(new InvalidOperationException("战斗前记录未保存，原快照已保留待重试"));
                return;
            }
            ticket.BeforeCommitted = true;
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=history.opening.saved window={ticket.Window.Id}");
            QueueResult(ticket);
        }, order);
    }

    internal void Complete(Ticket ticket, string winner, string loser, bool successful)
    {
        // Read native identity now: a task may finish between the periodic observations.
        Observe();
        if (!Session.CanCommitResult(ticket.Window)) return;
        if (!successful) { Failure(new InvalidOperationException("战斗任务未完成，结果未记录")); return; }
        var request = Request("resolveBattle", ticket.Window.Scope);
        request["runId"] = ticket.RunId; request["windowId"] = ticket.Window.Id;
        request["winner"] = winner; request["loser"] = loser;
        request["outcome"] = winner == "Player" ? "win" : winner == "Opponent" ? "loss"
            : winner == "Draw" ? "draw" : "unknown";
        Progress(request); ticket.Result = request; QueueResult(ticket);
    }

    private void QueueResult(Ticket ticket)
    {
        if (!ticket.BeforeCommitted || ticket.Result == null || ticket.ResultQueued) return;
        ticket.ResultQueued = true;
        Submit(ticket.Result, response =>
        {
            Observe();
            if (Session.Scope != ticket.Window.Scope) return;
            if (response.GetProperty("state").GetString() != "resolved") { Failure(new InvalidOperationException("结果未保存，原记录已保留")); return; }
            Session.MarkResultCommitted(ticket.Window);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=history.result.saved window={ticket.Window.Id}");
        });
    }

    private void Submit(object dto, Action<JsonElement> complete, long? originalOrder = null)
    {
        var order = originalOrder ?? ++_writeOrder;
        _pendingWrites.Enqueue(new(order, dto, complete, AndroidHistoryBridge.WriteAsync(dto)));
    }

    private void DrainWrites()
    {
        // Observe task completions on the game thread, in submission order. An asynchronous
        // continuation's scheduling cannot reorder failures or hide a pending retry item.
        while (_pendingWrites.TryPeek(out var write) && write.Task.IsCompleted)
        {
            _pendingWrites.Dequeue();
            try { write.Complete(write.Task.GetAwaiter().GetResult()); }
            catch (Exception error)
            {
                Failure(error);
                RetainRetry(write.Order, () => Submit(write.Dto, write.Complete, write.Order));
            }
        }
    }

    private void RetainRetry(long order, Action retry)
    {
        if (_retry.Count == 0) _nextRetry = Time.realtimeSinceStartup + 5;
        if (_retry.ContainsKey(order) || _retry.Count < HistoryLimits.QueueCapacity) _retry[order] = retry;
    }

    internal void Failure(Exception error)
    {
        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=history.failed exception={error}");
    }
    internal void Retry()
    {
        DrainWrites();
        if (_pendingWrites.Count != 0) return;
        try { AndroidHistoryBridge.ResumeAfterFailure(); }
        catch (Exception error) { Failure(error); return; }
        var work = _retry.Values.ToArray(); _retry.Clear();
        foreach (var retry in work) retry();
        _last = null; Observe();
    }
    internal static Dictionary<string, object?> Request(string operation, HistoryScope scope) => new()
    {
        ["op"] = operation, ["sessionId"] = scope.SessionId, ["epoch"] = scope.Epoch,
        ["observedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    };
    private void Progress(Dictionary<string, object?> request)
    {
        // Complete called Observe immediately before this. Reuse that identity-checked managed
        // projection; a cleared or replaced native Run must not supply zero or another run's totals.
        request["day"] = _last?.Day ?? 0; request["hour"] = _last?.Hour ?? 0;
        request["victories"] = _last?.Victories ?? 0; request["losses"] = _last?.Losses ?? 0;
        request["progressAvailable"] = _last?.ProgressAvailable == true;
    }
    private static string PhaseName(HistoryPhase phase) => phase switch
    {
        HistoryPhase.Choice => "choice", HistoryPhase.Encounter => "encounter", HistoryPhase.Loot => "loot",
        HistoryPhase.LevelUp => "level_up", HistoryPhase.Combat => "combat", HistoryPhase.PvpCombat => "pvp",
        HistoryPhase.Replay => "replay", HistoryPhase.Victory => "victory", HistoryPhase.Defeat => "defeat", _ => "unknown",
    };
}
