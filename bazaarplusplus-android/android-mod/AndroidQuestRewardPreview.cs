using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Tooltips;
using Il2CppInterop.Runtime;
using TheBazaar.Tooltips;

namespace BazaarPlusPlus.Android;

internal static class AndroidQuestRewardPreview
{
    internal static bool HasRewards(ItemCard item)
    {
        var groups = item.Template?.TryCast<TCardItem>()?.Quests;
        if (groups == null) return false;
        foreach (var group in groups)
            foreach (var entry in group.Entries)
                if (entry.Reward?.Localization?.Tooltips?.Count > 0) return true;
        return false;
    }

    internal static string Build(ItemCard item)
    {
        var groups = item.Template?.TryCast<TCardItem>()?.Quests;
        if (groups == null) return string.Empty;
        var tooltip = new CardTooltipData(item, item.Template, null);
        var lines = new List<string>();
        foreach (var group in groups)
        {
            foreach (var entry in group.Entries)
            {
                var reward = entry.Reward?.Localization?.Tooltips;
                if (reward == null || reward.Count == 0) continue;
                var passive = tooltip.RenderQuestTooltips(ref reward, ETooltipType.Passive)?.Trim();
                var active = tooltip.RenderQuestTooltips(ref reward, ETooltipType.Active)?.Trim();
                var rendered = new[] { passive, active }.Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct().ToArray();
                if (rendered.Length == 0) continue;
                lines.Add($"<b>{NativeUi.Text("Quest reward", "任务奖励")} {lines.Count + 1}</b>\n" +
                    string.Join("\n", rendered));
            }
        }
        return string.Join("\n\n", lines);
    }
}
