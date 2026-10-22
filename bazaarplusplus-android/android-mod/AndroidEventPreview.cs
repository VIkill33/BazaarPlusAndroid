using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Event;
using BazaarGameShared.Domain.Cards.Encounter.Step;
using BazaarGameShared.Domain.Cards.Encounter;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Domain.Values;
using BazaarGameShared.Domain.Spawning.SpawnBehaviors;
using BazaarPlusPlus.GameInterop.DayTiers;
using BazaarGameShared.Domain.Spawning;
using BazaarGameShared.Domain.Spawning.SpawnFilters;
using BazaarGameShared.Domain.Spawning.SpawningContexts;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;
using BazaarGameClient.Domain.Tooltips;

namespace BazaarPlusPlus.Android;

internal static class AndroidEventPreview
{
    internal static string Build(EventEncounterCard source)
    {
        if (Data.IsInCombat) return string.Empty;
        var template = source.Template?.TryCast<TCardEncounterEvent>();
        var query = template?.SelectionContext?.SpawnContext?.TryCast<TSpawnContextQuery>();
        var sections = new List<string>();
        var context = new ValueContext(Data.Run?.Cast<IRun>(), source.Cast<ICard>(), null);
        var merchant = template?.Tags?.Contains(ECardTag.Merchant) == true;
        var outcomes = merchant ? BuildMerchant(query, context)
            : BuildQuery(query, NativeUi.Text("Options and rewards", "选项、条件与奖励"),
                offerChoices: false, context: context);
        if (!string.IsNullOrWhiteSpace(outcomes)) sections.Add(outcomes);
        if (template?.SelectionRequirements != null)
            foreach (var requirement in template.SelectionRequirements)
            {
                var condition = AndroidEncounterConditions.Prerequisites(requirement.Prerequisites, context);
                var text = requirement.Tooltip?.Content;
                var rendered = text == null ? string.Empty : RenderText(template, TooltipExtensions.GetLocalizedText(text));
                if (!string.IsNullOrWhiteSpace(condition) || !string.IsNullOrWhiteSpace(rendered))
                    sections.Add(NativeUi.Text("Selection requirement: ", "选择条件：") + string.Join(" · ",
                        new[] { condition, rendered }.Where(value => !string.IsNullOrWhiteSpace(value))));
            }
        var next = new List<string>();
        var transitions = template?.NextEncounterOnSelection;
        if (transitions != null)
        {
            for (var index = 0; index < transitions.Count; index++)
            {
                var transition = transitions[index];
                var detail = DescribeCard(transition.Id, context, new HashSet<string>(), 0);
                var condition = AndroidEncounterConditions.Prerequisites(transition.Prerequisites, context);
                if (!string.IsNullOrWhiteSpace(detail)) next.Add(detail + (string.IsNullOrEmpty(condition) ? string.Empty : "\n" + condition));
            }
        }
        if (next.Count > 0)
            sections.Add($"<b>{NativeUi.Text("Following encounters", "后续遭遇")}</b>\n" +
                string.Join("\n", next.Distinct()));
        var result = string.Join("\n\n", sections);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.event_preview.rendered template_id={source.TemplateId} merchant={merchant} characters={result.Length}");
        return result;
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
        var rewards = BuildQuery(query,
            NativeUi.Text("Possible next level reward choices", "下一级可能出现的奖励选项"),
            offerChoices: true);
        if (!string.IsNullOrWhiteSpace(rewards)) sections.Add(rewards);
        return string.Join("\n", sections);
    }

    internal static string BuildQuery(TSpawnContextQuery? query, string heading, bool offerChoices,
        ValueContext? context = null, HashSet<string>? path = null, int depth = 0)
    {
        var gameData = Data.GetStatic();
        if (query?.Groups == null || gameData == null) return string.Empty;
        context ??= new ValueContext(Data.Run?.Cast<IRun>(), null, null);
        path ??= new HashSet<string>();

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
            var hasUnsupportedFilter = false;
            for (var filterIndex = 0; filterIndex < group.Filters.Count; filterIndex++)
            {
                var filter = group.Filters[filterIndex];
                if (filter?.TryCast<TSpawnFilterQuery>() is { } pool)
                {
                    cards.Add(NativeUi.Text("Random reward: ", "随机奖励：") + AndroidEncounterConditions.Pool(pool.Constraints));
                    continue;
                }
                var ids = filter?.TryCast<TSpawnFilterIdList>()?.Ids;
                if (ids == null)
                {
                    if (filter != null && filter.TryCast<TSpawnFilterQuery>() == null)
                        hasUnsupportedFilter = true;
                    continue;
                }
                for (var cardIndex = 0; cardIndex < ids.Count; cardIndex++)
                {
                    var name = DescribeCard(ids[cardIndex], context, path, depth);
                    if (!string.IsNullOrWhiteSpace(name)) cards.Add(name);
                }
            }
            if (hasUnsupportedFilter)
                cards.Add(NativeUi.Text("other conditional outcomes", "其他条件结果"));
            if (cards.Count == 0)
            {
                groups.Add((NativeUi.Text("other outcome", "其他结果"), group.RandomWeight, true));
                continue;
            }
            var text = string.Join("\n", cards.Distinct());
            if (group.SelectionMethod == ESpawnSelectionMethod.Random && cards.Count > 1)
                text = NativeUi.Text("Random from: ", "随机取自：") + text;
            var conditions = AndroidEncounterConditions.Prerequisites(group.Prerequisites, context);
            if (!string.IsNullOrWhiteSpace(conditions))
                text += "\n" + NativeUi.Text("Requires: ", "条件：") + conditions;
            if (group.Limit != null)
                text += "\n" + NativeUi.Text("Quantity: ", "数量：") + AndroidEncounterConditions.Number(group.Limit, context);
            var quality = Quality(query.Behaviors, group.Behaviors);
            if (AndroidEncounterConditions.Read(group.Filters).Any(filter => filter.TryCast<TSpawnFilterQuery>() != null) && !string.IsNullOrWhiteSpace(quality))
                text += "\n" + quality;
            groups.Add((text, group.RandomWeight,
                group.Prerequisites?.Count > 0 || hasUnsupportedFilter));
        }

        if (groups.Count == 0) return string.Empty;
        var outerRandom = query.SelectionMethod == ESpawnSelectionMethod.Random;
        var choice = outerRandom && !offerChoices
            ? NativeUi.Text(" (random selection)", "（随机选取）") : string.Empty;
        var showPercent = !offerChoices && outerRandom && groups.All(group => !group.Conditional)
            && groups.All(group => group.Weight > 0);
        var totalWeight = showPercent ? groups.Aggregate(0UL, (sum, group) => sum + group.Weight) : 0UL;
        var lines = groups.Select(group =>
        {
            var chance = showPercent
                ? $"{Math.Round(group.Weight * 100d / totalWeight, 1):0.#}% " : string.Empty;
            var condition = group.Conditional
                ? NativeUi.Text(" (conditional)", "（有条件）") : string.Empty;
            return "• " + chance + group.Text + condition;
        });
        return $"<b>{heading}{choice}</b>\n" + string.Join("\n", lines);
    }

    internal static string? ResolveCardName(Il2CppSystem.Guid id)
    {
        var reward = Data.GetStatic()?.GetCardById(id)?.TryCast<TCardBase>();
        if (reward == null) return null;
        var title = reward.Localization?.Title;
        return title == null ? reward.InternalName : TooltipExtensions.GetLocalizedText(title);
    }

    private static string BuildMerchant(TSpawnContextQuery? query, ValueContext context)
    {
        if (query?.Groups == null) return string.Empty;
        var lines = new List<string>();
        foreach (var group in query.Groups)
        {
            var quality = Quality(query.Behaviors, group.Behaviors);
            var condition = AndroidEncounterConditions.Prerequisites(group.Prerequisites, context);
            var pools = new List<string>();
            if (group.Filters != null)
                foreach (var filter in group.Filters)
                    if (filter.TryCast<TSpawnFilterQuery>() is { } pool)
                        pools.Add(AndroidEncounterConditions.Pool(pool.Constraints));
            var quantity = group.Limit == null ? string.Empty :
                NativeUi.Text("Offers: ", "供货数量：") + AndroidEncounterConditions.Number(group.Limit, context);
            lines.Add(string.Join("\n", new[] { quality, quantity, string.Join("\n", pools),
                string.IsNullOrEmpty(condition) ? string.Empty : NativeUi.Text("Requires: ", "条件：") + condition }
                .Where(value => !string.IsNullOrWhiteSpace(value))));
        }
        return "<b>" + NativeUi.Text("Shop quality probabilities", "商店品质概率") + "</b>\n" +
            string.Join("\n\n", lines.Distinct());
    }

    private static string Quality(Il2CppSystem.Collections.Generic.List<ITSpawnBehavior>? parent,
        Il2CppSystem.Collections.Generic.List<ITSpawnBehavior>? group)
    {
        var fixedTiers = new List<ETier>();
        var usesDay = true;
        var excluded = new HashSet<ETier>();
        void Read(Il2CppSystem.Collections.Generic.List<ITSpawnBehavior>? behaviors)
        {
            if (behaviors == null) return;
            foreach (var behavior in behaviors)
            {
                if (behavior.TryCast<TSpawnBehaviorTier>() is { } tier)
                {
                    if (tier.IsNot) { foreach (var value in tier.Tiers) excluded.Add(value); }
                    else { fixedTiers.Clear(); foreach (var value in tier.Tiers) fixedTiers.Add(value); }
                }
                if (behavior.TryCast<TSpawnBehaviorIgnoreTierTable>() is { IgnoreTierTable: true }
                    || behavior.TryCast<TSpawnBehaviorInheritTier>() is { Inherits: true }) usesDay = false;
                // DownShiftTier changes availability after the quality roll. Like
                // desktop EncounterStructuredParser, retain the day distribution.
            }
        }
        Read(parent);
        Read(group);
        var (day, table) = CurrentDayTiers();
        var probabilities = AndroidEncounterTierDistribution.Resolve(fixedTiers, excluded, usesDay, table);
        var tiers = fixedTiers.Where(tier => !excluded.Contains(tier)).Distinct().ToArray();
        if (probabilities != null)
            return (fixedTiers.Count > 0 ? string.Empty : NativeUi.Text($"Day {day}: ", $"第 {day} 天：")) +
                string.Join(" · ", probabilities.Select(entry =>
                    $"{AndroidEncounterConditions.Label(entry.Tier)} {entry.Percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%"));
        if (tiers.Length > 1) return NativeUi.Text("Fixed tier pool: ", "指定品质池：") +
            string.Join(" / ", tiers.Select(AndroidEncounterConditions.Label));
        if (fixedTiers.Count > 0 || !usesDay)
            return NativeUi.Text("Special tier rule; probabilities unavailable", "特殊品质规则，暂无法计算概率");
        if (table == null) return NativeUi.Text("Current day quality probabilities unavailable", "当前天数品质概率暂不可用");
        // Keep the GameData probabilities; exclusions do not imply a reroll or
        // renormalization unless the game's spawn behavior explicitly says so.
        if (excluded.Count > 0) return NativeUi.Text("Tier exclusions; probabilities unavailable: ", "排除品质，概率暂不可用：") +
            string.Join(" / ", excluded.Select(AndroidEncounterConditions.Label));
        return string.Empty;
    }

    private static (int? Day, GameDataDayTierTable? Table) CurrentDayTiers()
    {
        var run = Data.Run;
        if (!TheBazaar.DataExtensions.IsInRun(AppState.CurrentState) || run == null || run.Day == 0)
            return (null, null);
        var weights = Data.GetStatic()?.GetGameModeById(run.GameModeId)?.ItemSkillSpawnTierPercantagesByDay;
        if (weights == null || !weights.TryGetValue(run.Day, out var day) || day == null)
            return (checked((int)run.Day), null);
        return (checked((int)run.Day), GameDataDayTierTable.FromWeights(day.Bronze, day.Silver, day.Gold, day.Diamond));
    }

    private static string DescribeCard(Il2CppSystem.Guid id, ValueContext context, HashSet<string> path, int depth)
    {
        var template = Data.GetStatic()?.GetCardById(id)?.TryCast<TCardBase>();
        if (template == null) return string.Empty;
        var name = ResolveCardName(id) ?? template.InternalName;
        if (depth >= 4 || !path.Add(id.ToString())) return name + NativeUi.Text(" (further encounter)", "（后续遭遇）");
        try
        {
            var description = Description(template);
            var text = $"<b>{name}</b>" + (string.IsNullOrWhiteSpace(description) ? string.Empty : "\n" + description);
            if (template.TryCast<TCardEncounter>() is { ExperienceAwardUponSelection: > 0 } encounter)
                text += "\n" + NativeUi.Text("Experience +", "经验 +") + encounter.ExperienceAwardUponSelection;
            if (template.TryCast<TCardEncounterEvent>() is { } nextEvent)
            {
                var rewards = BuildQuery(nextEvent.SelectionContext?.SpawnContext?.TryCast<TSpawnContextQuery>(),
                    NativeUi.Text("Rewards", "奖励"), false, context, path, depth + 1);
                if (!string.IsNullOrEmpty(rewards)) text += "\n" + rewards;
            }
            var transitions = template.TryCast<TCardEncounterStep>()?.NextEncounterOnSelection
                ?? template.TryCast<TCardEncounterEvent>()?.NextEncounterOnSelection;
            if (transitions != null)
                foreach (var next in transitions)
                {
                    var result = DescribeCard(next.Id, context, path, depth + 1);
                    var condition = AndroidEncounterConditions.Prerequisites(next.Prerequisites, context);
                    text += "\n→ " + result + (string.IsNullOrEmpty(condition) ? string.Empty : "\n" + condition);
                }
            return text;
        }
        finally { path.Remove(id.ToString()); }
    }

    private static string Description(TCardBase template)
    {
        var lines = new List<string>();
        if (template.Localization?.Description is { } description)
            lines.Add(RenderText(template, TooltipExtensions.GetLocalizedText(description)));
        if (template.Localization?.Tooltips != null)
            foreach (var tooltip in template.Localization.Tooltips)
                if (tooltip?.Content != null)
                    lines.Add(RenderText(template, TooltipExtensions.GetLocalizedText(tooltip.Content)));
        return string.Join("\n", lines.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
    }

    private static string RenderText(TCardBase template, string localized)
    {
        // DTOUtils creates a detached client card. It never selects an encounter,
        // inserts the preview into the run, or executes an ability.
        var card = DTOUtils.CreateCard("bpp-event-preview-" + Guid.NewGuid().ToString("N"), template.Id.ToString(), template.Type);
        var tooltip = new CardTooltipData(card, template.Cast<ITCard>(), null);
        var context = new TooltipContext(card, template.Cast<ITCard>(), new ValueContext(Data.Run?.Cast<IRun>(), card.Cast<ICard>(), null));
        return tooltip.RenderTooltip(TooltipBuilder.Create(context, localized))?.Trim() ?? string.Empty;
    }
}
