using System.Text.Json;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;

namespace BazaarPlusPlus.Android;

internal sealed record AndroidBuildItem(Guid TemplateId, int Slot, int Tier, string? Enchantment, int Size);
internal sealed record AndroidBuild(int Id, IReadOnlyList<AndroidBuildItem> Items, int TenWinRuns,
    int TenWinRateBps, int? P75FinalDay, long Score)
{
    internal IReadOnlySet<Guid> TemplateIds { get; } = Items.Select(item => item.TemplateId).ToHashSet();
}
internal sealed record AndroidBuildMatch(AndroidBuild Build, int MatchedCards, int LiveStateScore);

internal sealed class AndroidBuildCorpus
{
    private readonly Dictionary<string, IReadOnlyList<AndroidBuild>> _heroes;
    private readonly Dictionary<string, Dictionary<Guid, HashSet<int>>> _cardIndices;

    private AndroidBuildCorpus(Dictionary<string, IReadOnlyList<AndroidBuild>> heroes, DateTimeOffset generatedAt)
    {
        _heroes = heroes;
        _cardIndices = heroes.ToDictionary(hero => hero.Key, hero => hero.Value
            .SelectMany(build => build.TemplateIds.Select(card => (card, build.Id)))
            .GroupBy(entry => entry.card)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Id).ToHashSet()),
            StringComparer.Ordinal);
        GeneratedAt = generatedAt;
    }

    internal DateTimeOffset GeneratedAt { get; }
    internal int BuildCount => _heroes.Values.Sum(builds => builds.Count);
    internal IReadOnlyDictionary<string, int> HeroBuildCounts =>
        _heroes.ToDictionary(hero => hero.Key, hero => hero.Value.Count, StringComparer.Ordinal);
    internal int CountForHero(string hero) =>
        _heroes.TryGetValue(CanonicalHero(hero), out var builds) ? builds.Count : 0;

    internal static AndroidBuildCorpus? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schema_version").GetInt32() != 2
                || root.GetProperty("kind").GetString() != "ten_win_builds") return null;
            var schemas = root.GetProperty("schemas");
            if (!HasSchema(schemas.GetProperty("build"), "card_refs", "layout", "stats")
                || !HasSchema(schemas.GetProperty("layout"), "card_ref", "slot", "tier", "enchant_ref", "size")
                || !HasSchema(schemas.GetProperty("stats"), "completed_run_count", "ten_win_run_count",
                    "ten_win_rate_bps", "p75_ten_win_final_day", "score")) return null;
            var generatedAt = root.GetProperty("generated_at").GetDateTimeOffset();
            var window = root.GetProperty("window");
            var start = DateOnly.ParseExact(window.GetProperty("start").GetString()!, "yyyy-MM-dd");
            var end = DateOnly.ParseExact(window.GetProperty("end").GetString()!, "yyyy-MM-dd");
            var days = window.GetProperty("days").GetInt32();
            if (days is < 1 or > 7 || end.DayNumber - start.DayNumber + 1 != days) return null;
            var cards = root.GetProperty("cards").EnumerateArray()
                .Select(value => Guid.Parse(value.GetString()!)).ToArray();
            if (cards.Any(id => id == Guid.Empty) || cards.Distinct().Count() != cards.Length) return null;
            var enchantments = root.GetProperty("enchantments").EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()).ToArray();
            if (enchantments.Length == 0 || enchantments[0] != null
                || enchantments.Skip(1).Any(string.IsNullOrWhiteSpace)
                || enchantments.Skip(1).Distinct(StringComparer.Ordinal).Count() != enchantments.Length - 1)
                return null;

            var heroes = new Dictionary<string, IReadOnlyList<AndroidBuild>>(StringComparer.Ordinal);
            foreach (var hero in root.GetProperty("heroes").EnumerateObject()
                .OrderBy(entry => CanonicalHero(entry.Name), StringComparer.Ordinal)
                .ThenBy(entry => entry.Name == "TheDragons" ? 0 : 1))
            {
                var builds = new List<AndroidBuild>();
                var refsByBuild = new List<int[]>();
                foreach (var row in hero.Value.GetProperty("builds").EnumerateArray())
                {
                    if (row.GetArrayLength() != 3) return null;
                    var references = row[0].EnumerateArray().Select(value => value.GetInt32()).ToArray();
                    if (references.Any(index => index < 0 || index >= cards.Length)
                        || !references.SequenceEqual(references.OrderBy(index => index))) return null;
                    refsByBuild.Add(references);
                    var occupied = new bool[10];
                    var items = new List<AndroidBuildItem>();
                    foreach (var item in row[1].EnumerateArray())
                    {
                        if (item.GetArrayLength() != 5) return null;
                        var cardRef = item[0].GetInt32();
                        var slot = item[1].GetInt32();
                        var tier = item[2].GetInt32();
                        var enchantRef = item[3].GetInt32();
                        var size = item[4].GetInt32();
                        if (cardRef < 0 || cardRef >= cards.Length || slot < 0 || size is < 1 or > 3
                            || slot + size > occupied.Length || tier is < 1 or > 5
                            || enchantRef < 0 || enchantRef >= enchantments.Length) return null;
                        for (var position = slot; position < slot + size; position++)
                        {
                            if (occupied[position]) return null;
                            occupied[position] = true;
                        }
                        items.Add(new AndroidBuildItem(cards[cardRef], slot, tier, enchantments[enchantRef], size));
                    }
                    if (!occupied.All(value => value)
                        || !references.OrderBy(value => value).SequenceEqual(items
                            .Select(item => Array.IndexOf(cards, item.TemplateId)).OrderBy(value => value))) return null;

                    var stats = row[2];
                    if (stats.GetArrayLength() != 5) return null;
                    var completed = stats[0].GetInt32();
                    var tenWin = stats[1].GetInt32();
                    var rate = stats[2].GetInt32();
                    var day = stats[3].ValueKind == JsonValueKind.Null ? (int?)null : stats[3].GetInt32();
                    var score = stats[4].GetInt64();
                    if (completed < 0 || tenWin < 0 || tenWin > completed || rate is < 0 or > 10000
                        || day < 0 || score is < 0 or > 1000000) return null;
                    builds.Add(new AndroidBuild(builds.Count, items, tenWin, rate, day, score));
                }
                if (!ValidCardIndex(hero.Value.GetProperty("card_index"), cards.Length, refsByBuild))
                    return null;
                var canonical = CanonicalHero(hero.Name);
                if (heroes.TryGetValue(canonical, out var preferred))
                    heroes[canonical] = preferred.Concat(builds.Where(candidate =>
                        preferred.All(existing => existing.Id != candidate.Id)))
                        .OrderBy(build => build.Id).ToArray();
                else heroes.Add(canonical, builds);
            }
            return new AndroidBuildCorpus(heroes, generatedAt);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    internal IReadOnlyList<AndroidBuildMatch> Find(string hero, IReadOnlyCollection<Guid> selected,
        BuildLiveState? liveState = null)
    {
        if (!_heroes.TryGetValue(CanonicalHero(hero), out var builds) || selected.Count == 0)
            return Array.Empty<AndroidBuildMatch>();
        var selectedSet = selected.Where(id => id != Guid.Empty).ToHashSet();
        if (selectedSet.Count == 0) return Array.Empty<AndroidBuildMatch>();
        var index = _cardIndices[CanonicalHero(hero)];
        var covered = new List<HashSet<int>>();
        var uncovered = false;
        foreach (var card in selectedSet)
        {
            if (index.TryGetValue(card, out var buildIds) && buildIds.Count > 0)
                covered.Add(buildIds);
            else uncovered = true;
        }
        if (covered.Count == 0) return Array.Empty<AndroidBuildMatch>();
        var candidateIds = new HashSet<int>(covered[0]);
        if (!uncovered)
            foreach (var matches in covered.Skip(1)) candidateIds.IntersectWith(matches);
        if (uncovered || candidateIds.Count == 0)
        {
            candidateIds.Clear();
            foreach (var matches in covered) candidateIds.UnionWith(matches);
        }
        var state = liveState ?? BuildLiveState.Empty;
        return builds.Where(build => candidateIds.Contains(build.Id))
            .Select(build => new AndroidBuildMatch(build,
                build.TemplateIds.Count(selectedSet.Contains),
                build.TemplateIds.Sum(state.WeightFor)))
            .OrderByDescending(match => match.MatchedCards)
            .ThenByDescending(match => match.LiveStateScore)
            .ThenByDescending(match => match.Build.Score)
            .ThenBy(match => match.Build.Id)
            .ToArray();
    }

    private static bool HasSchema(JsonElement actual, params string[] expected) =>
        actual.ValueKind == JsonValueKind.Array && actual.GetArrayLength() == expected.Length
        && actual.EnumerateArray().Select(value => value.GetString()).SequenceEqual(expected);

    private static bool ValidCardIndex(JsonElement rows, int cardCount, IReadOnlyList<int[]> refsByBuild)
    {
        var observed = new Dictionary<int, int[]>();
        var previous = -1;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetArrayLength() != 2) return false;
            var cardRef = row[0].GetInt32();
            if (cardRef <= previous || cardRef >= cardCount) return false;
            previous = cardRef;
            observed[cardRef] = row[1].EnumerateArray().Select(value => value.GetInt32()).ToArray();
        }
        for (var cardRef = 0; cardRef < cardCount; cardRef++)
        {
            var expected = Enumerable.Range(0, refsByBuild.Count)
                .Where(buildId => refsByBuild[buildId].Contains(cardRef)).ToArray();
            if (!observed.TryGetValue(cardRef, out var actual))
            {
                if (expected.Length != 0) return false;
                continue;
            }
            if (!actual.SequenceEqual(expected)) return false;
        }
        return true;
    }

    private static string CanonicalHero(string hero) =>
        hero.Trim().Equals("Hero8", StringComparison.OrdinalIgnoreCase)
        || hero.Trim().Equals("TheDragons", StringComparison.OrdinalIgnoreCase)
            ? "TheDragons" : hero;
}
