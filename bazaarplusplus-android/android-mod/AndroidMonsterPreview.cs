using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Combat;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Players;
using BazaarGameShared.Domain.Spawning.SpawningContexts;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Localization;
using TheBazaar.Tooltips;
using TheBazaar.Utilities;

namespace BazaarPlusPlus.Android;

internal static class AndroidMonsterPreview
{
    internal static string Build(CombatEncounterCard card, CardTooltipData tooltip)
    {
        if (Data.IsInCombat) return string.Empty;
        var encounter = card.Template?.TryCast<TCardEncounterCombat>();
        if (encounter == null) return string.Empty;
        var monster = GetMonster(card, tooltip);
        var sections = new List<string>();
        var title = tooltip.GetTitle();
        if (!string.IsNullOrWhiteSpace(title)) sections.Add($"<b>{title}</b>");
        if (monster?.Player != null)
        {
            var player = monster.Player;
            if (player.Attributes != null && player.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out var health))
                sections.Add($"{NativeUi.Text("Health", "生命值")}：{health}");
            var itemCount = player.Hand?.Items?.Count ?? 0;
            if (itemCount > 0)
                sections.Add($"<b>{NativeUi.Text("Monster items", "野怪物品")} · {itemCount}</b>");
            var skills = new List<string>();
            if (player.Skills != null)
                foreach (var skill in player.Skills)
                    skills.Add(CardName(skill.TemplateId, skill.Tier));
            if (skills.Count > 0)
                sections.Add($"<b>{NativeUi.Text("Monster skills", "野怪技能")}</b>\n" +
                    string.Join("\n", skills.OrderBy(name => name).Select(name => "• " + name)));
        }
        var combatRewards = new List<string>();
        if (encounter.RewardCombatGold > 0)
            combatRewards.Add($"{NativeUi.Text("Gold", "金币")} +{encounter.RewardCombatGold}");
        if (encounter.RewardCombatXp > 0)
            combatRewards.Add($"{NativeUi.Text("Experience", "经验")} +{encounter.RewardCombatXp}");
        if (combatRewards.Count > 0)
            sections.Add($"<b>{NativeUi.Text("Combat rewards", "战斗奖励")}</b>\n" + string.Join("\n", combatRewards));
        AppendRewards(sections, encounter.RewardVictory, NativeUi.Text("Victory rewards", "获胜奖励"));
        AppendRewards(sections, encounter.RewardDefeat, NativeUi.Text("Defeat rewards", "战败奖励"));
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_preview.rendered template_id={card.TemplateId} monster={monster?.InternalName} sections={sections.Count} combat_gold={encounter.RewardCombatGold} combat_xp={encounter.RewardCombatXp} victory_gold={encounter.RewardVictory?.GoldReward} victory_xp={encounter.RewardVictory?.ExperienceReward} victory_query={encounter.RewardVictory?.SpawnContext?.TryCast<TSpawnContextQuery>() != null}");
        return string.Join("\n\n", sections);
    }

    internal static TMonster? GetMonster(CombatEncounterCard card, CardTooltipData tooltip) =>
        tooltip.Monster ?? DataExtensions.GetMonsterTemplate(card);

    private static void AppendRewards(List<string> sections, TCombatRewards? rewards, string heading)
    {
        if (rewards == null) return;
        var lines = new List<string>();
        if (rewards.GoldReward > 0) lines.Add($"{NativeUi.Text("Gold", "金币")} +{rewards.GoldReward}");
        if (rewards.ExperienceReward > 0) lines.Add($"{NativeUi.Text("Experience", "经验")} +{rewards.ExperienceReward}");
        var query = rewards.SpawnContext?.TryCast<TSpawnContextQuery>();
        var choices = AndroidEventPreview.BuildQuery(query,
            NativeUi.Text("Possible item rewards", "可能出现的物品奖励"), offerChoices: true);
        if (!string.IsNullOrWhiteSpace(choices)) lines.Add(choices);
        if (lines.Count > 0) sections.Add($"<b>{heading}</b>\n" + string.Join("\n", lines));
    }

    private static string CardName(Il2CppSystem.Guid id, ETier tier)
    {
        var template = Data.GetStatic()?.GetCardById(id)?.TryCast<TCardBase>();
        var title = template?.Localization?.Title;
        var name = title == null ? template?.InternalName : TooltipExtensions.GetLocalizedText(title);
        if (string.IsNullOrWhiteSpace(name)) name = NativeUi.Text("Unknown card", "未知卡牌");
        var tierName = new LocalizableText(tier.ToString()).GetLocalizedText();
        return $"{name} · {tierName}";
    }
}
