using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Android;
using BazaarPlusPlus.GameInterop.DayTiers;

static void Check(bool value, string description)
{
    if (!value) throw new InvalidOperationException(description);
}

var day = GameDataDayTierTable.FromWeights(60, 30, 10, 0)!;
var empty = new HashSet<ETier>();
var regular = AndroidEncounterTierDistribution.Resolve([], empty, true, day)!;
Check(regular.Count == 3 && regular[0].Percent == 60 && regular[1].Percent == 30 && regular[2].Percent == 10,
    "Regular shops must preserve the current GameData distribution.");
var fixedGold = AndroidEncounterTierDistribution.Resolve([ETier.Gold], empty, false, day)!;
Check(fixedGold.Count == 1 && fixedGold[0].Tier == ETier.Gold && fixedGold[0].Percent == 100,
    "Fixed quality must override the day distribution.");
Check(AndroidEncounterTierDistribution.Resolve([ETier.Gold, ETier.Diamond], empty, false, day) == null,
    "A multi-tier set does not imply equal odds.");
Check(AndroidEncounterTierDistribution.Resolve([], empty, false, day) == null,
    "Ignored or inherited quality rules cannot use ordinary day odds.");
Check(AndroidEncounterTierDistribution.Resolve([], new HashSet<ETier> { ETier.Bronze }, true, day) == null,
    "Exclusions must not silently renormalize day odds.");
Check(AndroidEncounterTierDistribution.Resolve([ETier.Gold], new HashSet<ETier> { ETier.Gold }, true, day) == null,
    "Contradictory fixed/excluded tiers must not fall back to day odds.");
Check(AndroidEncounterTierDistribution.Resolve([], empty, true, null) == null,
    "Missing day data must remain unknown.");
var bronzeOnly = GameDataDayTierTable.FromWeights(100, 0, 0, 0)!;
Check(AndroidEncounterTierDistribution.Resolve([ETier.Diamond], empty, true, bronzeOnly)![0].Tier == ETier.Diamond,
    "Fixed diamond must bypass an early-day bronze maximum.");
Check(AndroidEncounterTierDistribution.Resolve([ETier.Gold, ETier.Gold], empty, false, null)![0].Percent == 100,
    "Duplicate fixed tiers must not create artificial alternatives.");
Console.WriteLine("Encounter preview quality probability scenarios passed.");
