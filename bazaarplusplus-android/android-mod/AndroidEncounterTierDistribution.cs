using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.GameInterop.DayTiers;

namespace BazaarPlusPlus.Android;

internal static class AndroidEncounterTierDistribution
{
    // An inclusive multi-tier rule names a set, not a uniform distribution.
    // Likewise, excluding tiers does not prove that failed rolls are retried.
    internal static IReadOnlyList<GameDataDayTierProbability>? Resolve(
        IReadOnlyList<ETier> fixedTiers, IReadOnlySet<ETier> excluded,
        bool usesDayDistribution, GameDataDayTierTable? dayTable)
    {
        var allowed = fixedTiers.Where(tier => !excluded.Contains(tier)).Distinct().ToArray();
        if (allowed.Length == 1) return [new(allowed[0], 100)];
        if (fixedTiers.Count > 0 || excluded.Count > 0 || !usesDayDistribution) return null;
        return dayTable?.Entries;
    }
}
