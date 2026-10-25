using BazaarPlusPlus.Android;

var account = Guid.NewGuid().ToString("D");
var otherAccount = Guid.NewGuid().ToString("D");
var run = Guid.NewGuid().ToString("D");
var core = new HistorySessionCore(Guid.NewGuid().ToString("D"));
var pending = core.Observe(Guid.Empty.ToString(), run, HistoryPhase.Choice);
Expect(!pending.Confirmed && !core.ConfirmBinding(pending, false), "All-zero account must remain unconfirmed");
Expect(core.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.AwaitingIdentity,
    "Unknown identity cannot create a battle window");
var binding = core.Observe(account, run, HistoryPhase.Choice);
Expect(binding.Epoch > pending.Epoch, "Identity arriving later invalidates the old epoch");
Expect(core.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.AwaitingStore,
    "Capture must wait for durable parent binding");
Expect(core.ConfirmBinding(binding, false), "Confirmed account can bind");
core.Observe(account, run, HistoryPhase.Choice);
var beforeMonster = core.CheckpointId;
Expect(core.Begin("monster", HistoryPhase.Combat, 1, 3).Decision == HistoryCaptureDecision.Ignored
    && core.CheckpointId == beforeMonster, "PvE does not allocate a window or consume a PvP checkpoint");
var first = Began(core.Begin("one", HistoryPhase.PvpCombat, 1, 3));
Expect(!first.PartialOpening && first.Scope == binding, "Window keeps opening identity");
Expect(core.Begin("one", HistoryPhase.PvpCombat, 1, 3).Window == first, "Repeated original uses same window");
Expect(core.Begin("another", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "A consumed checkpoint cannot create a second battle from a different message");
Expect(core.Begin("one", HistoryPhase.Replay, 2, 0).Decision == HistoryCaptureDecision.Replay,
    "Replay after day advance does not create or change a battle");
Expect(core.MarkResultCommitted(first) && !core.MarkResultCommitted(first), "Results commit once");
Expect(core.Begin("one", HistoryPhase.PvpCombat, 2, 0).Decision == HistoryCaptureDecision.Duplicate,
    "Completed evidence also prevents duplicate creation");

core.Observe(account, run, HistoryPhase.Loot);
var second = Began(core.Begin("two", HistoryPhase.PvpCombat, 1, 3));
Expect(second.Id != first.Id && second.CheckpointId != first.CheckpointId,
    "Two battles at exactly the same day and hour have distinct observed windows");
core.Observe(account, run, HistoryPhase.Choice);
Expect(core.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "Old original after completion and a fresh checkpoint cannot create another row");
var third = Began(core.Begin("three", HistoryPhase.PvpCombat, 1, 6));
Expect(core.CanCommitResult(second) && core.CanCommitResult(third),
    "Late result keeps its old UUID instead of selecting the current window");
Expect(core.MarkResultCommitted(second), "Late result for same scope can resolve its own window");

var switched = core.Observe(otherAccount, run, HistoryPhase.Choice);
Expect(!core.CanCommitResult(third) && !core.ConfirmBinding(binding, false),
    "Account switch rejects old callbacks and a late parent acknowledgement");
Expect(core.ConfirmBinding(switched, false), "Second account binds independently");
core.Observe(otherAccount, run, HistoryPhase.Choice);
var fourth = Began(core.Begin("one", HistoryPhase.PvpCombat, 1, 3));
Expect(fourth.Scope.AccountId == otherAccount, "Message IDs are scoped by account");
core.Observe(account, Guid.NewGuid().ToString(), HistoryPhase.Unknown);
Expect(!core.CanCommitResult(fourth), "New run invalidates old window callbacks");

var restored = new HistorySessionCore(Guid.NewGuid().ToString());
var restoredScope = restored.Observe(account, run, HistoryPhase.PvpCombat);
restored.ConfirmBinding(restoredScope, false);
Expect(restored.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "Restart directly in combat cannot guess a pending identity");
restored.RestoreEvidence([new(account, run, first.Scope.SessionId, first.CheckpointId, first.MessageId, first.Id)]);
restored.Observe(account, run, HistoryPhase.Choice);
Expect(restored.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "Persisted completed or pending evidence cannot be mistaken for a new original after restart");
Began(restored.Begin("new_after_restart", HistoryPhase.PvpCombat, 1, 3));

foreach (var phase in new[] { HistoryPhase.Unknown, HistoryPhase.Replay, HistoryPhase.Victory, HistoryPhase.Defeat })
{
    var paused = new HistorySessionCore(Guid.NewGuid().ToString());
    var scope = paused.Observe(account, run, phase);
    paused.ConfirmBinding(scope, false);
    paused.Observe(account, run, phase);
    Expect(paused.Begin("one", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
        $"{phase} cannot open a checkpoint");
}
var partial = new HistorySessionCore(Guid.NewGuid().ToString());
var partialScope = partial.Observe(account, run, HistoryPhase.PvpCombat);
partial.ConfirmBinding(partialScope, true); // Storage must atomically prove no corresponding prior partial.
var partialWindow = Began(partial.Begin("first_partial", HistoryPhase.PvpCombat, 5, 3));
Expect(partialWindow.PartialOpening, "First enable mid-run must preserve partial provenance");
Expect(partial.Begin("next_partial", HistoryPhase.PvpCombat, 5, 3).Decision == HistoryCaptureDecision.Quarantine,
    "Permission for first partial is consumed once");
Expect(HistorySessionCore.NormalizeId("invalid") == null && HistorySessionCore.NormalizeId(account.ToUpperInvariant()) == account,
    "Scope identity is validated and normalized");
var disconnected = new HistorySessionCore(Guid.NewGuid().ToString());
var disconnectedScope = disconnected.Observe(account, run, HistoryPhase.Choice);
disconnected.ConfirmBinding(disconnectedScope, false);
disconnected.Observe(account, run, HistoryPhase.Choice);
disconnected.Observe(account, run, HistoryPhase.Unknown);
disconnected.Observe(account, run, HistoryPhase.PvpCombat);
Expect(disconnected.Begin("after_disconnect", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "Loading or disconnect invalidates an unused checkpoint");
disconnected.Observe(account, run, HistoryPhase.Choice);
Began(disconnected.Begin("confirmed_again", HistoryPhase.PvpCombat, 1, 3));
disconnected.Observe(account, run, HistoryPhase.Defeat);
disconnected.Observe(account, run, HistoryPhase.Choice);
Expect(disconnected.Begin("late_after_run_end", HistoryPhase.PvpCombat, 1, 3).Decision == HistoryCaptureDecision.Quarantine,
    "A completed run cannot reopen from a stale checkpoint callback");
Console.WriteLine("History identity, opening windows, replay, duplicate results, late callbacks and conservative recovery passed.");

static HistoryWindow Began(HistoryBeginResult result)
{
    Expect(result.Decision == HistoryCaptureDecision.Begin && result.Window != null, "Expected a new proven window");
    return result.Window!;
}
static void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
