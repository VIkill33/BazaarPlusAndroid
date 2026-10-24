namespace BazaarPlusPlus.Android;

// These are observed app phases, not guesses made from day/hour or the presence of cards.
internal enum HistoryPhase { Unknown, Choice, Encounter, Loot, LevelUp, Combat, PvpCombat, Replay, Victory, Defeat }
internal enum HistoryCaptureDecision { Begin, Duplicate, Replay, AwaitingIdentity, AwaitingStore, Quarantine }
internal sealed record HistoryScope(string SessionId, long Epoch, string? AccountId, string? NativeRunId)
{
    internal bool Confirmed => AccountId != null && NativeRunId != null;
}
internal sealed record HistoryWindow(string Id, HistoryScope Scope, string CheckpointId,
    string MessageId, HistoryPhase Kind, uint Day, uint Hour, bool PartialOpening);
internal sealed record HistoryOpeningEvidence(string AccountId, string NativeRunId, string SessionId,
    string CheckpointId, string MessageId, string WindowId);
internal sealed record HistoryBeginResult(HistoryCaptureDecision Decision, HistoryWindow? Window, string? Reason = null);

/// <summary>Pure observation rules. It never accesses Unity, JNI, files, clocks or database rows.</summary>
internal sealed class HistorySessionCore
{
    private readonly string _sessionId;
    private readonly Dictionary<string, HistoryOpeningEvidence> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HistoryWindow> _windows = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private HistoryScope? _scope;
    private HistoryPhase _lastPhase;
    private string? _checkpoint;
    private bool _consumed;
    private bool _bound;
    private bool _allowPartial;
    private bool _runEnded;

    internal HistorySessionCore(string sessionId)
    {
        _sessionId = NormalizeId(sessionId) ?? throw new ArgumentException("A nonzero session UUID is required", nameof(sessionId));
    }

    internal HistoryScope? Scope => _scope;
    internal string? CheckpointId => _checkpoint;

    internal HistoryScope Observe(string? accountId, string? nativeRunId, HistoryPhase phase)
    {
        var account = NormalizeId(accountId);
        var run = NormalizeId(nativeRunId);
        if (_scope == null || _scope.AccountId != account || _scope.NativeRunId != run)
        {
            _scope = new HistoryScope(_sessionId, (_scope?.Epoch ?? 0) + 1, account, run);
            _checkpoint = null;
            _consumed = false;
            _bound = false;
            _allowPartial = false;
            _runEnded = false;
            _lastPhase = HistoryPhase.Unknown;
        }
        if (phase is HistoryPhase.Unknown or HistoryPhase.Victory or HistoryPhase.Defeat)
        {
            _checkpoint = null;
            _consumed = true;
            _allowPartial = false;
            if (phase is HistoryPhase.Victory or HistoryPhase.Defeat) _runEnded = true;
        }
        if (_scope.Confirmed && _bound && IsCheckpoint(phase)
            && (phase != _lastPhase || (_checkpoint == null && !_consumed)))
        {
            _checkpoint = Guid.NewGuid().ToString("D");
            _consumed = false;
        }
        _lastPhase = phase;
        return _scope;
    }

    // The caller must replay its latest observation after this asynchronous acknowledgement.
    internal bool ConfirmBinding(HistoryScope scope, bool allowFirstPartial)
    {
        if (scope != _scope || !scope.Confirmed) return false;
        _bound = true;
        _allowPartial = allowFirstPartial;
        return true;
    }

    internal void RestoreEvidence(IEnumerable<HistoryOpeningEvidence> evidence)
    {
        foreach (var item in evidence)
        {
            if (NormalizeId(item.AccountId) is not { } account || NormalizeId(item.NativeRunId) is not { } run
                || string.IsNullOrWhiteSpace(item.MessageId) || item.MessageId.Length > 128)
                throw new ArgumentException("Invalid persisted opening evidence", nameof(evidence));
            // A conflicting short ID is still evidence of ambiguity, never permission to replace a row.
            _messages.TryAdd(Key(account, run, item.MessageId), item);
        }
    }

    internal HistoryBeginResult Begin(string? messageId, HistoryPhase phase, uint day, uint hour)
    {
        if (phase == HistoryPhase.Replay) return new(HistoryCaptureDecision.Replay, null);
        if (phase is not (HistoryPhase.Combat or HistoryPhase.PvpCombat))
            return new(HistoryCaptureDecision.Quarantine, null, "not_an_original_combat_phase");
        if (_scope?.Confirmed != true) return new(HistoryCaptureDecision.AwaitingIdentity, null);
        if (!_bound) return new(HistoryCaptureDecision.AwaitingStore, null);
        if (_runEnded) return new(HistoryCaptureDecision.Quarantine, null, "run_already_ended");
        if (string.IsNullOrWhiteSpace(messageId) || messageId.Length > 128)
            return new(HistoryCaptureDecision.Quarantine, null, "missing_message_evidence");
        var key = Key(_scope.AccountId!, _scope.NativeRunId!, messageId);
        if (_messages.TryGetValue(key, out var previous))
        {
            if (previous.SessionId == _sessionId && previous.CheckpointId == _checkpoint
                && _windows.TryGetValue(previous.WindowId, out var existing))
                return new(HistoryCaptureDecision.Duplicate, existing);
            return new(HistoryCaptureDecision.Quarantine, null, "message_from_another_window_or_process");
        }
        var partial = _checkpoint == null;
        if (_consumed || (partial && !_allowPartial))
            return new(HistoryCaptureDecision.Quarantine, null, "opening_not_proven");
        _checkpoint ??= Guid.NewGuid().ToString("D");
        _consumed = true;
        _allowPartial = false;
        _lastPhase = phase;
        var window = new HistoryWindow(Guid.NewGuid().ToString("D"), _scope, _checkpoint,
            messageId, phase, day, hour, partial);
        _windows.Add(window.Id, window);
        _messages.Add(key, new(_scope.AccountId!, _scope.NativeRunId!, _sessionId,
            _checkpoint, messageId, window.Id));
        return new(HistoryCaptureDecision.Begin, window);
    }

    internal bool CanCommitResult(HistoryWindow window) => window.Scope == _scope && _bound
        && _windows.TryGetValue(window.Id, out var owned) && owned == window && !_completed.Contains(window.Id);

    internal bool MarkResultCommitted(HistoryWindow window)
    {
        if (!CanCommitResult(window)) return false;
        _completed.Add(window.Id);
        return true;
    }

    internal static bool IsCheckpoint(HistoryPhase phase) => phase is HistoryPhase.Choice
        or HistoryPhase.Encounter or HistoryPhase.Loot or HistoryPhase.LevelUp;

    internal static string? NormalizeId(string? value) => Guid.TryParse(value, out var id) && id != Guid.Empty
        ? id.ToString("D") : null;

    private static string Key(string account, string run, string message) => account + "/" + run + "/" + message;
}
