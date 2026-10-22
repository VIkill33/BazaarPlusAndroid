using BazaarGameShared.Domain.Core.Types;
using TheBazaar.Tooltips;

namespace BazaarPlusPlus.Android;

internal static class AndroidItemDescription
{
    internal static string Build(CardTooltipData tooltip) =>
        WithAttributes(tooltip, tooltip.BuildCombinedEffectDescription());

    internal static string WithAttributes(CardTooltipData tooltip, string effects)
    {
        var lines = new List<string>();
        if (tooltip.CardInstance?.Type == ECardType.Item)
        {
            Add(ECardAttributeType.CooldownMax, NativeUi.Text("Cooldown", "冷却时间"),
                NativeUi.Text(" s", " 秒"));
            Add(ECardAttributeType.AmmoMax, NativeUi.Text("Ammo", "弹药数量"));
        }
        if (!string.IsNullOrWhiteSpace(effects)) lines.Add(effects);
        return string.Join("\n", lines);

        void Add(ECardAttributeType attribute, string label, string unit = "")
        {
            // The native tooltip resolves tier/enchantment/runtime values and formats
            // values (including cooldown in seconds) independently of effect text.
            var value = NativeNullableValue.Invoke<float, ECardAttributeType>(
                tooltip, nameof(CardTooltipData.GetCurrentAttributeValue), attribute);
            if (!value.HasValue || value.Value <= 0) return;
            var rendered = tooltip.RenderAttributeValueWithIcon(attribute, value.Value).Item1;
            lines.Add($"{label}：{rendered}{unit}");
        }
    }
}
