using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Event;
using BazaarGameShared.Domain.Spawning;
using BazaarGameShared.Domain.Spawning.SpawnFilters;
using BazaarGameShared.Domain.Spawning.SpawningContexts;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;

namespace BazaarPlusPlus.Android;

internal static class AndroidEventPreview
{
    internal static string Build(EventEncounterCard source)
    {
        if (Data.IsInCombat) return string.Empty;
        var template = source.Template?.TryCast<TCardEncounterEvent>();
        var query = template?.SelectionContext?.SpawnContext?.TryCast<TSpawnContextQuery>();
        var sections = new List<string>();
        var outcomes = BuildQuery(query, NativeUi.Text("Possible outcomes", "可能结果"));
        if (!string.IsNullOrWhiteSpace(outcomes)) sections.Add(outcomes);
        var next = new List<string>();
        var transitions = template?.NextEncounterOnSelection;
        if (transitions != null)
        {
            for (var index = 0; index < transitions.Count; index++)
            {
                var name = ResolveCardName(transitions[index].Id);
                if (!string.IsNullOrWhiteSpace(name)) next.Add(name);
            }
        }
        if (next.Count > 0)
            sections.Add($"<b>{NativeUi.Text("Following encounters", "后续遭遇")}</b>\n" +
                string.Join("、", next.Distinct()));
        return string.Join("\n\n", sections);
    }

    internal static string BuildLevelRewards(int currentLevel)
    {
        if (Data.IsInCombat || currentLevel < 0) return string.Empty;
        var level = Data.GetStatic()?.GetLevelUpById(currentLevel);
        if (level == null) return string.Empty;
        var sections = new List<string>();
        if (level.HealthIncrease > 0)
            sections.Add($"{NativeUi.Text("Maximum health", "生命上限")} +{level.HealthIncrease}");
        if (currentLevel < 4)
            sections.Add($"{NativeUi.Text("Board slots", "场上格数")} +2");
        var query = level.Rewards?.TryCast<TSpawnContextQuery>();
        var rewards = BuildQuery(query, NativeUi.Text("Next level rewards", "下一级奖励"));
        if (!string.IsNullOrWhiteSpace(rewards)) sections.Add(rewards);
        return string.Join("\n", sections);
    }

    private static string BuildQuery(TSpawnContextQuery? query, string heading)
    {
        var gameData = Data.GetStatic();
        if (query?.Groups == null || gameData == null) return string.Empty;

        var groups = new List<(string Text, uint Weight, bool Conditional)>();
        for (var groupIndex = 0; groupIndex < query.Groups.Count; groupIndex++)
        {
            var group = query.Groups[groupIndex];
            if (group == null) continue;
            if (group.Filters == null)
            {
                groups.Add((NativeUi.Text("other outcome", "其他结果"), group.RandomWeight, true));
                continue;
            }
            var cards = new List<string>();
            var dynamicPools = 0;
            var hasUnsupportedFilter = false;
            for (var filterIndex = 0; filterIndex < group.Filters.Count; filterIndex++)
            {
                var filter = group.Filters[filterIndex];
                if (filter?.TryCast<TSpawnFilterQuery>() != null) dynamicPools++;
                var ids = filter?.TryCast<TSpawnFilterIdList>()?.Ids;
                if (ids == null)
                {
                    if (filter != null && filter.TryCast<TSpawnFilterQuery>() == null)
                        hasUnsupportedFilter = true;
                    continue;
                }
                for (var cardIndex = 0; cardIndex < ids.Count; cardIndex++)
                {
                    var name = ResolveCardName(ids[cardIndex]);
                    if (!string.IsNullOrWhiteSpace(name)) cards.Add(name);
                }
            }
            if (dynamicPools > 0)
                cards.Add(NativeUi.Text("card pool matching conditions", "按条件抽取的卡牌池") +
                    (dynamicPools > 1 ? $" ×{dynamicPools}" : string.Empty));
            if (hasUnsupportedFilter)
                cards.Add(NativeUi.Text("other conditional outcomes", "其他条件结果"));
            if (cards.Count == 0)
            {
                groups.Add((NativeUi.Text("other outcome", "其他结果"), group.RandomWeight, true));
                continue;
            }
            var text = string.Join("、", cards.Distinct());
            if (group.SelectionMethod == ESpawnSelectionMethod.Random && cards.Count > 1)
                text = NativeUi.Text("Random from: ", "随机取自：") + text;
            groups.Add((text, group.RandomWeight,
                group.Prerequisites?.Count > 0 || hasUnsupportedFilter));
        }

        if (groups.Count == 0) return string.Empty;
        var outerRandom = query.SelectionMethod == ESpawnSelectionMethod.Random;
        var choice = outerRandom
            ? NativeUi.Text(" (random selection)", "（随机选取）") : string.Empty;
        var showPercent = outerRandom && groups.All(group => !group.Conditional)
            && groups.All(group => group.Weight > 0);
        var totalWeight = showPercent ? groups.Aggregate(0UL, (sum, group) => sum + group.Weight) : 0UL;
        var lines = groups.Select(group =>
        {
            var chance = showPercent
                ? $"{Math.Round(group.Weight * 100d / totalWeight, 1):0.#}% " : string.Empty;
            return "• " + chance + group.Text;
        });
        return $"<b>{heading}{choice}</b>\n" + string.Join("\n", lines);
    }

    private static string? ResolveCardName(Il2CppSystem.Guid id)
    {
        var reward = Data.GetStatic()?.GetCardById(id)?.TryCast<TCardBase>();
        if (reward == null) return null;
        var title = reward.Localization?.Title;
        return title == null ? reward.InternalName : TooltipExtensions.GetLocalizedText(title);
    }
}
