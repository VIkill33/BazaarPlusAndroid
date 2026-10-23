using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Android;

var cases = new (ETier Source, ETier[] Expected)[]
{
    (ETier.Bronze, [ETier.Silver, ETier.Gold, ETier.Diamond]),
    (ETier.Silver, [ETier.Gold, ETier.Diamond]),
    (ETier.Gold, [ETier.Diamond]),
    (ETier.Diamond, []),
    (ETier.Legendary, []),
};
foreach (var (source, expected) in cases)
{
    var actual = AndroidUpgradeTierPolicy.TargetTiers(source).ToArray();
    if (!actual.SequenceEqual(expected))
        throw new InvalidOperationException($"Unexpected upgrade tiers for {source}: {string.Join(", ", actual)}");
}
Console.WriteLine("Upgrade tier policy: all five source tiers passed; legendary is never an upgrade target.");
