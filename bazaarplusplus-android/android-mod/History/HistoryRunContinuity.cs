namespace BazaarPlusPlus.Android;

internal sealed record HistoryProgress(uint Day, uint Hour, uint Victories, uint Losses, string? Hero, string? Mode);
internal sealed record HistoryRunObservation(string? AccountId, string? NativeRunId, HistoryPhase Phase,
    HistoryProgress? Progress, bool ProgressAvailable);

// Only an explicit ending phase can end a run. Missing native objects are never an ending signal.
internal sealed class HistoryRunContinuity
{
    private HistoryRunObservation? _lastReliable;

    internal HistoryRunObservation Resolve(string? accountId, string? cachedRunId, HistoryPhase phase, HistoryProgress? nativeProgress)
    {
        var account = HistorySessionCore.NormalizeId(accountId);
        var cachedRun = HistorySessionCore.NormalizeId(cachedRunId);
        if (_lastReliable != null && (account != _lastReliable.AccountId
            || (cachedRun != null && cachedRun != _lastReliable.NativeRunId) || phase == HistoryPhase.Unknown))
            _lastReliable = null;

        if (nativeProgress != null && cachedRun != null && phase != HistoryPhase.Unknown)
        {
            var observed = new HistoryRunObservation(account, cachedRun, phase, nativeProgress, true);
            if (account != null) _lastReliable = observed;
            return observed;
        }
        // Replay cannot open a battle. It may retain an already proven scope long enough for its
        // original task to finish; the explicit ending phase may then commit that same run's end.
        if (_lastReliable != null && phase is HistoryPhase.Replay or HistoryPhase.Victory or HistoryPhase.Defeat)
            return _lastReliable with { Phase = phase, ProgressAvailable = false };

        _lastReliable = null;
        return new(account, null, phase, nativeProgress, nativeProgress != null);
    }
}
