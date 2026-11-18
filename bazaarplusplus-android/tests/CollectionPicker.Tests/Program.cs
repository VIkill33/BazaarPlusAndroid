using BazaarPlusPlus.Android;
using BazaarPlusPlus.Game.CollectionPanel.Data;
using BazaarGameShared.Domain.Core.Types;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var memory = new AndroidCollectionPickerState();
var first = memory.Open(EHero.Vanessa);
Check(!first.Filter.AllHeroesSelected && first.Filter.EffectiveHero == EHero.Vanessa, "First picker selects current hero");
Check(!first.Filter.UseRunDayFilter && !first.Follow && first.SourceKey == null, "First picker has no shop or day gate");
first.Filter.ToggleAllHeroes();
first.Filter.SearchQuery = "test";
first.Filter.Sizes.Add(ECardSize.Large);
first.Filter.Tiers.Add(ETier.Gold);
first.Filter.UseRunDayFilter = true;
memory.Remember(first.Filter, "merchant:test", true);
first.Filter.SearchQuery = "mutated";
first.Filter.Sizes.Clear();
var next = memory.Open(EHero.Pygmalien);
Check(next.Filter.AllHeroesSelected && next.Filter.SearchQuery == "test" && next.Filter.Sizes.Contains(ECardSize.Large), "Successful selection keeps independent snapshot");
Check(next.Filter.Tiers.Contains(ETier.Gold) && next.Filter.UseRunDayFilter && next.Follow && next.SourceKey == "merchant:test", "All active filters restored");
next.Filter.ResetFacets();
var cancelled = memory.Open(EHero.Pygmalien);
Check(cancelled.Filter.SearchQuery == "test" && cancelled.Filter.AllHeroesSelected, "Cancel does not replace last successful filters");
var noHero = new AndroidCollectionPickerState().Open(null);
Check(noHero.Filter.AllHeroesSelected, "Without a current hero show all heroes");
Console.WriteLine($"Collection picker: {checks} checks passed.");
