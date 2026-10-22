using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Android;
using BazaarPlusPlus.Game.CollectionPanel.Data;
using BazaarPlusPlus.Game.CollectionPanel.Sources;
using BazaarPlusPlus.GameInterop.DayTiers;
using BazaarPlusPlus.Game.CollectionPanel.Grid;

var sources = AndroidCollectionSources.Load();
Check(sources.Count == 74, "All embedded sources parsed on current Android enums");
Check(sources.Select(source => source.SourceKey).Distinct().Count() == sources.Count, "Unique source identities");
var hero = EHero.Pygmalien;
var cards = new List<CollectionCardVm>();
CollectionCardVm Card(EHero owner, ETier tier, params ECardTag[] tags)
{
    var card = new CollectionCardVm { Id = Guid.NewGuid(), Type = ECardType.Item,
        StartingTier = tier, Heroes = new[] { owner }, Tags = tags };
    cards.Add(card);
    return card;
}
var bronzeWeapon = Card(hero, ETier.Bronze, ECardTag.Weapon);
var goldWeapon = Card(hero, ETier.Gold, ECardTag.Weapon);
var neutral = Card(EHero.Common, ETier.Bronze);
var other = Card(EHero.Vanessa, ETier.Bronze, ECardTag.Weapon);
var tool = Card(hero, ETier.Bronze, ECardTag.Tool);
CollectionSourceEntry Source(string name) => sources.Single(source => source.Name == name && source.AppliesToHero(hero));
HashSet<Guid> Pool(string name) => CollectionSourceOfferPoolResolver.Resolve(Source(name), hero, cards).OfferedCardIds.ToHashSet();
Check(Pool("Jay Jay").SetEquals(new[] { bronzeWeapon.Id, goldWeapon.Id, tool.Id }), "Selected-hero pool excludes neutral and other heroes");
Check(Pool("Aila").SetEquals(new[] { bronzeWeapon.Id, goldWeapon.Id }), "Weapon merchant excludes tools and other heroes");
Check(Pool("Curio").SetEquals(new[] { neutral.Id }), "Neutral-only source survives hero scope");
Check(Source("Goldie").SuppressDayGate, "Pinned-tier source bypasses day ceiling");
Check(!Source("Jay Jay").SuppressDayGate, "General merchant follows day ceiling");
Check(GameDataDayTierTable.FromWeights(100, 0, 0, 0)?.MaximumTier == ETier.Bronze, "Day table follows game weights");
Check(GameDataDayTierTable.FromWeights(0, 0, 0, 0) == null, "Missing spawn weights fail open");
Check(GameDataDayTierTable.FromWeights(float.NaN, 0, 0, 0) == null, "Invalid weights fail open");
var filter = new CollectionFilterState();
filter.ToggleHero(hero);
var dayContext = new CollectionFilterContext
{
    OfferedCardIds = Pool("Aila"), ApplyHeroFilter = false,
    DayTiers = GameDataDayTierTable.FromWeights(100, 0, 0, 0),
};
Check(CollectionFilterEngine.Apply(cards, filter, dayContext).Select(card => card.Id).SequenceEqual(new[] { bronzeWeapon.Id }),
    "Merchant pool AND current day ceiling");
filter.Tiers.Add(ETier.Gold);
Check(CollectionFilterEngine.Apply(cards, filter, dayContext).Count == 0, "Manual tier remains AND day gate");
var fixedContext = new CollectionFilterContext
{
    OfferedCardIds = Pool("Goldie"), ApplyHeroFilter = false,
    DayTiers = dayContext.DayTiers, SuppressDayGate = Source("Goldie").SuppressDayGate,
};
Check(CollectionFilterEngine.Apply(cards, filter, fixedContext).Single().Id == goldWeapon.Id,
    "Fixed-tier merchant suppresses only day gate");
filter.Tiers.Clear();
Check(CollectionFilterEngine.Apply(cards, filter, new CollectionFilterContext { DayTiers = null }).Count == 3,
    "Unavailable day data fails open without dropping hero scope");
var mixed = Enumerable.Range(0, 18).Select(index => new CollectionCardVm
{
    Id = Guid.NewGuid(), Type = ECardType.Item,
    Size = new[] { ECardSize.Small, ECardSize.Medium, ECardSize.Large }[index % 3],
}).ToArray();
var layout = CollectionGridLayout.Build(mixed, CollectionTabKind.Items);
for (var index = 0; index < mixed.Length; index++)
{
    var cell = layout.ContentRectFor(index, 50, 8, 0, 0);
    var span = index % 3 + 1;
    Check(cell.Width == span * 50 + (span - 1) * 8 && cell.Height == 108, "Mixed size spans preserve equal height");
    Check(cell.X >= 0 && cell.X + cell.Width <= 572, "Mixed shelves stay within ten columns");
    if (index > 0)
    {
        var previous = layout.ContentRectFor(index - 1, 50, 8, 0, 0);
        Check(cell.Y > previous.Y || cell.X >= previous.X + previous.Width, "Cells never overlap");
    }
}
Check(layout.ShelfAt(layout.ShelfCount - 1).LastIndex == mixed.Length - 1, "Final shelf retains last item");
var skills = CollectionGridLayout.Build(mixed, CollectionTabKind.Skills);
Check(skills.Columns == 7 && skills.ShelfHeightUnits == 1, "Skills remain a square grid");
Console.WriteLine("Android source parsing, merchant/day filtering, and mixed-size grid scenarios passed.");
static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
}

// Display-name lookup belongs to the live game, outside these pure catalog tests.
namespace TheBazaar.Utilities
{
    internal static class LocalizableShared
    {
        internal static string GetLocalizedHero(EHero hero) => hero.ToString();
    }
}
