using BazaarGameClient.Domain.Models.Cards;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Domain.Values;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Localization;
using TheBazaar.Tooltips;
using TheBazaar.Utilities;

namespace BazaarPlusPlus.Android;

internal static class AndroidUpgradePreview
{
    internal static string Build(ItemCard source)
    {
        if (Data.IsInCombat)
            return NativeUi.Text("Upgrade previews are unavailable during combat.", "战斗中暂不提供升级预览。");
        if (!source.CanCardUpgrade())
            return NativeUi.Text("This item cannot be upgraded.", "此物品无法升级。");
        var template = source.Template?.TryCast<TCardItem>();
        if (template == null)
            return NativeUi.Text("Upgrade data is unavailable for this item.", "此物品没有可用的升级数据。");
        var before = AndroidEnchantPreview.AttributeDigest(source);
        var sourceTier = source.Tier;
        var section = NativeNullableValue.Read<EInventorySection>(source, "get_Section");
        var enchantment = NativeNullableValue.Read<EEnchantmentType>(source, "get_Enchantment");
        var previews = new List<string>();
        foreach (var tier in Enum.GetValues<ETier>().Distinct().OrderBy(value => (int)value))
        {
            if ((int)tier <= (int)sourceTier) continue;
            var description = BuildTier(source, template, tier, section, enchantment);
            if (string.IsNullOrWhiteSpace(description)) continue;
            var label = new LocalizableText(tier.ToString()).GetLocalizedText();
            previews.Add($"<b>{NativeUi.Text("Upgrade to", "升级至")} · {label}</b>\n{description}");
        }
        if (source.Tier != sourceTier || AndroidEnchantPreview.AttributeDigest(source) != before)
            throw new InvalidOperationException("Upgrade preview changed the source item.");
        if (previews.Count == 0)
            return NativeUi.Text("No higher tier description is available.", "暂时无法生成更高品质的描述。");
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.upgrade_preview.rendered template_id={source.TemplateId} current_tier={sourceTier} tiers={previews.Count} characters={previews.Sum(value => value.Length)} source_unchanged=True");
        return string.Join("\n\n", previews);
    }

    private static string BuildTier(ItemCard source, TCardItem template,
        ETier targetTier, EInventorySection? section, EEnchantmentType? enchantment)
    {
        if (!template.TryGetTierTemplate(targetTier, out var target) || target == null)
            return string.Empty;
        var projection = AndroidEnchantPreview.Clone(source, enchantment, section);
        projection.Tier = targetTier;
        foreach (var attribute in Enum.GetValues<ECardAttributeType>())
        {
            if (!source.Attributes.ContainsKey(attribute) && !target.Attributes.ContainsKey(attribute)) continue;
            var currentBase = BaseValue(template, attribute, source.Tier);
            var nextBase = BaseValue(template, attribute, targetTier);
            if (!nextBase.HasValue) continue;
            projection.Attributes[attribute] = source.Attributes.ContainsKey(attribute) && currentBase.HasValue
                ? nextBase.Value + source.Attributes[attribute] - currentBase.Value
                : nextBase.Value;
        }
        ApplyRuntimeMultipliers(source, projection, template, targetTier);

        var tooltip = new CardTooltipData(projection, projection.Template, null);
        var context = new TooltipContext(projection, projection.Template,
            new ValueContext(Data.Run?.Cast<IRun>(), projection.Cast<ICard>(), null));
        var lines = new List<string>();
        if (template.Localization?.Tooltips != null)
        {
            foreach (var part in template.Localization.Tooltips)
            {
                if (part?.Content == null) continue;
                var localized = TooltipExtensions.GetLocalizedText(part.Content);
                var rendered = tooltip.RenderTooltip(TooltipBuilder.Create(context, localized));
                if (!string.IsNullOrWhiteSpace(rendered)) lines.Add(rendered.Trim());
            }
        }
        var description = string.Join("\n", lines);
        return description;
    }

    private static void ApplyRuntimeMultipliers(ItemCard source, ItemCard projected, TCardItem template, ETier nextTier)
    {
        var auraMultipliers = AndroidUpgradeAuraResolver.Resolve(source, projected);
        foreach (var attribute in Enum.GetValues<ECardAttributeType>())
        {
            if (!projected.Attributes.ContainsKey(attribute)) continue;
            var currentBase = BaseValue(template, attribute, source.Tier);
            var nextBase = BaseValue(template, attribute, nextTier);
            if (!nextBase.HasValue) continue;
            var currentMultiplier = 1f;
            var projectedMultiplier = 1f;
            if (attribute == ECardAttributeType.CooldownMax)
            {
                currentMultiplier *= CooldownMultiplier(source);
                projectedMultiplier *= CooldownMultiplier(projected);
            }
            if (auraMultipliers.TryGetValue(attribute, out var aura))
            {
                currentMultiplier *= aura.Current;
                projectedMultiplier *= aura.Projected;
            }
            if (MathF.Abs(currentMultiplier - 1f) < 0.0001f &&
                MathF.Abs(projectedMultiplier - 1f) < 0.0001f) continue;
            if (currentMultiplier <= 0 || projectedMultiplier < 0) continue;
            var runtimeDelta = currentBase.HasValue && source.Attributes.ContainsKey(attribute)
                ? source.Attributes[attribute] / currentMultiplier - currentBase.Value : 0f;
            projected.Attributes[attribute] = Math.Max(0, (int)Math.Round(
                (nextBase.Value + runtimeDelta) * projectedMultiplier, MidpointRounding.AwayFromZero));
        }
    }

    private static float CooldownMultiplier(ItemCard card)
    {
        var reduction = card.Attributes.ContainsKey(ECardAttributeType.PercentCooldownReduction)
            ? card.Attributes[ECardAttributeType.PercentCooldownReduction] : 0;
        return Math.Max(0, 100 - reduction) / 100f;
    }

    private static int? BaseValue(TCardItem template, ECardAttributeType attribute, ETier tier) =>
        NativeNullableValue.Invoke<int, ECardAttributeType, ETier>(
            template, "GetAttributeBaseValueAtTier", attribute, tier);
}
