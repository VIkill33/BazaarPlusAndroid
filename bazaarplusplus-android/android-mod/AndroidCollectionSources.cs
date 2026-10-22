using System.Text.Json;
using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.CollectionPanel.Sources;

namespace BazaarPlusPlus.Android;

// JSON/native collection boundaries only; offer membership and day gates use desktop rules.
internal static class AndroidCollectionSources
{
    internal static IReadOnlyList<CollectionSourceEntry> Load()
    {
        using var stream = typeof(AndroidCollectionSources).Assembly.GetManifestResourceStream(
            "BazaarPlusPlus.Android.collection-sources.json")!;
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 4)
            throw new InvalidOperationException("Unsupported collection source schema.");
        var groups = Strings(root, "groups");
        var entries = new List<CollectionSourceEntry>();
        foreach (var entry in root.GetProperty("entries").EnumerateArray())
        {
            var heroResults = Strings(entry, "availableHeroes").Select(CollectionSourceHeroParser.Parse).ToArray();
            if (heroResults.Any(hero => hero.Status == CollectionSourceHeroParseStatus.Invalid))
                throw new InvalidOperationException("Invalid source hero.");
            if (heroResults.Length > 0 && heroResults.All(hero => !hero.Hero.HasValue)) continue;
            var heroes = heroResults.Where(hero => hero.Hero.HasValue).Select(hero => hero.Hero!.Value).ToArray();
            var segments = new List<CollectionSourceOfferSegment>();
            var unavailable = false;
            foreach (var segment in entry.GetProperty("offerSegments").EnumerateArray())
            {
                var rule = segment.GetProperty("rule");
                var mode = Parse<CollectionSourceHeroMode>(rule, "heroMode");
                EHero? hero = null;
                if (rule.TryGetProperty("hero", out var heroValue))
                {
                    var parsed = CollectionSourceHeroParser.Parse(heroValue.GetString());
                    if (parsed.Status == CollectionSourceHeroParseStatus.Invalid)
                        throw new InvalidOperationException("Invalid fixed source hero.");
                    if (!parsed.Hero.HasValue) { unavailable = true; break; }
                    hero = parsed.Hero;
                }
                if (mode == CollectionSourceHeroMode.FixedHero && !hero.HasValue)
                    throw new InvalidOperationException("Missing fixed source hero.");
                var tier = rule.TryGetProperty("startingTier", out var tierRule)
                    ? new CollectionSourceStartingTierRule(Parse<CollectionSourceStartingTierMode>(tierRule, "mode"), Parse<ETier>(tierRule, "tier")) : null;
                var hidden = Values<EHiddenTag>(rule, "hiddenTagsAny").Concat(
                    CollectionHiddenTagGroups.Expand(Strings(rule, "hiddenTagGroupsAny"), "hiddenTagGroupsAny")).Distinct().ToArray();
                segments.Add(new CollectionSourceOfferSegment(segment.GetProperty("key").GetString()!,
                    Parse<CollectionSourceOfferSegmentKind>(segment, "kind"),
                    segment.TryGetProperty("rarityLabel", out var rarity) ? rarity.GetString() ?? string.Empty : string.Empty,
                    new CollectionSourceOfferRule(mode, hero, tier, Values<ECardSize>(rule, "sizesAny"),
                        Values<ECardTag>(rule, "tagsAny"), Values<ECardTag>(rule, "tagsNone"), hidden,
                        rule.TryGetProperty("enchantableOnly", out var enchantable) && enchantable.GetBoolean(),
                        Values<EEnchantmentType>(rule, "enchantmentTypesAny"), Values<ECardTag>(rule, "enchantmentTagsAny"),
                        Values<EHiddenTag>(rule, "enchantmentHiddenTagsAny"))));
            }
            if (unavailable) continue;
            if (segments.Count == 0 || (segments.Any(s => s.Rule.StartingTier != null) && segments.Any(s => s.Rule.StartingTier == null)))
                throw new InvalidOperationException("Invalid source offer segments.");
            var ids = Strings(entry, "sourceTemplateIds").Select(Guid.Parse).ToArray();
            // Template identities distinguish same-name merchants without persisting Android-only keys.
            var key = ids[0].ToString();
            var group = entry.GetProperty("group").GetString()!;
            entries.Add(new CollectionSourceEntry(key, Parse<CollectionSourceKind>(entry, "kind"),
                entry.GetProperty("name").GetString()!, heroes, entry.GetProperty("description").GetString()!,
                Guid.Parse(entry.GetProperty("portraitTemplateId").GetString()!), ids, segments, group,
                entry.GetProperty("order").GetInt32(), Array.IndexOf(groups, group)));
        }
        return entries.OrderBy(entry => entry.GroupDisplayIndex).ThenBy(entry => entry.Order).ThenBy(entry => entry.Name).ToArray();
    }

    private static string[] Strings(JsonElement element, string name) => element.TryGetProperty(name, out var array)
        ? array.EnumerateArray().Select(value => value.GetString()!).ToArray() : Array.Empty<string>();
    private static T[] Values<T>(JsonElement element, string name) where T : struct, Enum =>
        Strings(element, name).Select(value => Enum.Parse<T>(value, true)).ToArray();
    private static T Parse<T>(JsonElement element, string name) where T : struct, Enum =>
        Enum.Parse<T>(element.GetProperty(name).GetString()!, true);
}
