using System.Diagnostics;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Domain.Values;
using BazaarPlusPlus.Game.ItemEnchantPreview;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.Utilities;

namespace BazaarPlusPlus.Android;

internal static class AndroidEnchantPreview
{
    internal static string Build(ItemCard source)
    {
        var section = NativeNullableValue.Read<EInventorySection>(source, "get_Section");
        var current = NativeNullableValue.Read<EEnchantmentType>(source, "get_Enchantment");
        if (Data.IsInCombat) return string.Empty;
        var ordinary = ItemEnchantPreviewSectionPolicy.IsEligible(source.Type, section, false);
        var opponent = source.Owner == null || (source.Owner.Pointer == Data.Run?.Opponent?.Pointer && section == EInventorySection.Hand);
        if (!ordinary && !(source.Type == ECardType.Item && opponent)) return string.Empty;
        var enchantments = source.GetEnchantments();
        if (enchantments == null || enchantments.Count == 0) return string.Empty;
        var keys = new List<EEnchantmentType>();
        foreach (var kind in Enum.GetValues<EEnchantmentType>())
            if (enchantments.ContainsKey(kind)) keys.Add(kind);
        var candidates = ItemEnchantPreviewCandidateSelector.SelectCandidates(current, keys);
        var before = AttributeDigest(source);
        var clock = Stopwatch.StartNew();
        var lines = new List<string>();
        foreach (var kind in candidates)
        {
            var enchantment = enchantments[kind];
            if (enchantment.Localization?.Tooltips == null) continue;
            var clone = Clone(source, kind, section);
            if (enchantment.Attributes != null)
                foreach (var pair in enchantment.Attributes) clone.Attributes[pair.Key] = pair.Value;
            var tooltip = new CardTooltipData(clone, clone.Template, null);
            var context = new TooltipContext(clone, clone.Template,
                new ValueContext(Data.Run?.Cast<IRun>(), clone.Cast<ICard>(), null));
            var effects = new List<string>();
            foreach (var part in enchantment.Localization.Tooltips)
            {
                if (part?.Content == null) continue;
                var localized = TooltipExtensions.GetLocalizedText(part.Content);
                var rendered = tooltip.RenderTooltip(TooltipBuilder.Create(context, localized));
                if (!string.IsNullOrWhiteSpace(rendered)) effects.Add(rendered.Trim());
            }
            if (effects.Count == 0) continue;
            var label = new LocalizableText(kind.ToString()).GetLocalizedText();
            var color = ItemEnchantPreviewPalette.GetEnchantmentColorHex(kind);
            lines.Add($"<color=#{color}><b>{label}</b></color>: {string.Join(" ", effects)}");
        }
        var unchanged = before == AttributeDigest(source)
            && current == NativeNullableValue.Read<EEnchantmentType>(source, "get_Enchantment");
        if (!unchanged) throw new InvalidOperationException("Enchant preview changed the source item.");
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.enchant_preview.rendered template_id={source.TemplateId} candidates={candidates.Count} lines={lines.Count} elapsed_ms={clock.ElapsedMilliseconds} source_unchanged={unchanged}");
        return lines.Count == 0 ? string.Empty : "<b>BazaarPlusPlus</b>\n" + string.Join("\n", lines);
    }

    internal static ItemCard Clone(ItemCard source, EEnchantmentType? kind, EInventorySection? section)
    {
        // This is the desktop clone factory's explicit value-context whitelist.
        // Owner/template are read-only context; every mutable collection is copied.
        var attributes = new Il2CppSystem.Collections.Generic.Dictionary<ECardAttributeType, int>();
        foreach (var pair in source.Attributes) attributes.Add(pair.Key, pair.Value);
        return new ItemCard
        {
            InstanceId = source.InstanceId, TemplateId = source.TemplateId, Attributes = attributes,
            Heroes = Copy(source.Heroes), HiddenTags = Copy(source.HiddenTags), Tags = Copy(source.Tags),
            Size = source.Size, Tier = source.Tier, Type = source.Type, Owner = source.Owner,
            LeftSocketId = NativeNullableValue.Box(NativeNullableValue.Read<EContainerSocketId>(source, "get_LeftSocketId")),
            Section = NativeNullableValue.Box(section), State = source.State, Template = source.Template,
            Enchantment = NativeNullableValue.Box(kind),
        };
    }

    private static Il2CppSystem.Collections.Generic.HashSet<T> Copy<T>(Il2CppSystem.Collections.Generic.HashSet<T>? source)
    {
        var copy = new Il2CppSystem.Collections.Generic.HashSet<T>();
        if (source != null) foreach (var value in source) copy.Add(value);
        return copy;
    }

    internal static int AttributeDigest(ItemCard card)
    {
        var digest = 0;
        foreach (var pair in card.Attributes) digest ^= HashCode.Combine(pair.Key, pair.Value);
        return digest;
    }
}
