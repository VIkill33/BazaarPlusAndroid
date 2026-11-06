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

var sharedShop = AndroidPreviewText.Grouped("Shop", [("Day 1: Bronze 100%", "Items"),
    ("Day 1: Bronze 100%", "Vanessa items"), ("Day 1: Bronze 100%", "Items")]);
Check(Count(sharedShop, "Day 1: Bronze 100%") == 1 && Count(sharedShop, "Items") == 1
    && sharedShop.Contains("Vanessa items"),
    "A shared shop quality is shown once and every distinct pool remains.");
var mixedShop = AndroidPreviewText.Grouped("Shop", [("Day 3: Silver 100%", "A"),
    ("Fixed Gold 100%", "B"), ("Day 3: Silver 100%", "C")]);
Check(Count(mixedShop, "Day 3: Silver 100%") == 1 && Count(mixedShop, "Fixed Gold 100%") == 1
    && mixedShop.IndexOf("C", StringComparison.Ordinal) < mixedShop.IndexOf("Fixed Gold", StringComparison.Ordinal),
    "Distinct qualities each head the pools they govern, in first-seen order.");
var unknownShop = AndroidPreviewText.Grouped("Shop", [("", "A"), ("", "B")]);
Check(unknownShop.Contains("A") && unknownShop.Contains("B"),
    "Pools without a quality line are still listed.");
Check(AndroidPreviewText.Shared(["x", "x"]) == "x" && AndroidPreviewText.Shared(["x"]) == null
    && AndroidPreviewText.Shared(["x", "y"]) == null && AndroidPreviewText.Shared(["x", null]) == null,
    "Only a value repeated by every entry is hoisted.");
var nested = AndroidPreviewText.Bullet("outer\n" + AndroidPreviewText.Bullet("inner") + "\ntail");
Check(nested == "•<indent=0.9em>outer\n•<indent=1.8em>inner<indent=0.9em>\ntail</indent>",
    "Nested bullets indent one level deeper and restore the outer hanging indent.");
Check(AndroidPreviewText.Muted(" ") == string.Empty && AndroidPreviewText.Lines("a", null, " ", "b") == "a\nb",
    "Empty fragments do not create blank preview lines.");
Check(AndroidPreviewText.Unwrap("(Vanessa AND Item)") == "Vanessa AND Item"
    && AndroidPreviewText.Unwrap("(A) OR (B)") == "(A) OR (B)" && AndroidPreviewText.Unwrap("Item") == "Item",
    "Only parentheses enclosing the whole pool are removed.");
Console.WriteLine("Preview text grouping scenarios passed.");

static int Count(string text, string value)
{
    var count = 0;
    for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0;
        index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal)) count++;
    return count;
}
