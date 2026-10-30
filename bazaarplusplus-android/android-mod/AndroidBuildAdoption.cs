namespace BazaarPlusPlus.Android;

internal static class AndroidBuildAdoption
{
    internal const int MinimumRemoteBytes = 51200;
    internal const int MinimumBuildCount = 100;

    internal static string? RejectionReason(AndroidBuildCorpus? retained,
        AndroidBuildCorpus candidate, long bytes)
    {
        if (bytes < MinimumRemoteBytes || candidate.BuildCount < MinimumBuildCount)
            return "truncated";
        if (retained == null) return null;
        if (candidate.WindowEnd < retained.WindowEnd) return "older_window";
        if (candidate.WindowEnd > retained.WindowEnd) return null;
        if (candidate.GeneratedAt <= retained.GeneratedAt) return "older_window";
        if (candidate.BuildCount < retained.BuildCount / 4 ||
            retained.HeroBuildCounts.Any(hero => hero.Value > 0 &&
                candidate.CountForHero(hero.Key) < Math.Max(1, hero.Value / 10)))
            return "same_window_shrink";
        return null;
    }
}
