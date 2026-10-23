using BazaarGameShared.Domain.Core.Types;

namespace BazaarPlusPlus.Android;

internal static class AndroidUpgradeTierPolicy
{
    internal static IEnumerable<ETier> TargetTiers(ETier sourceTier) =>
        Enum.GetValues<ETier>()
            .Distinct()
            .Where(tier => tier > sourceTier && tier <= ETier.Diamond)
            .OrderBy(tier => (int)tier);
}
