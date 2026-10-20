using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Skill;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar.Tooltips;
using TheBazaar.UI.Tooltips.LockMode;

namespace BazaarPlusPlus.Android;

internal sealed record VariantChoice<T>(T Value, string Label);

internal static class AndroidCardVariants
{
    internal static TCardInstance CreateInstance(TCardBase template, ETier tier, EEnchantmentType? enchantment)
    {
        TCardInstance instance;
        if (template.Type == ECardType.Skill)
            instance = new TCardInstanceSkill();
        else if (template.Type == ECardType.Item)
            instance = new TCardInstanceItem
            {
                SocketId = new Il2CppSystem.Nullable<EContainerSocketId>((EContainerSocketId)0),
                EnchantmentType = enchantment.HasValue
                    ? new Il2CppSystem.Nullable<EEnchantmentType>(enchantment.Value)
                    : new Il2CppSystem.Nullable<EEnchantmentType>(),
            };
        else throw new ArgumentException("Only item and skill previews are supported.", nameof(template));
        instance.TemplateId = template.Id;
        instance.TemplateVersion = template.Version ?? string.Empty;
        instance.InstanceId = "bpp-android-preview-" + Guid.NewGuid().ToString("N");
        instance.Tier = tier;
        instance.Attributes = new Il2CppSystem.Collections.Generic.Dictionary<ECardAttributeType, int>();
        return instance;
    }

    internal static (List<VariantChoice<ETier>> Tiers, List<VariantChoice<EEnchantmentType?>> Enchantments)
        ReadOptions(CardTooltipData tooltip)
    {
        // Keep the game's lock-mode policy and localized labels as the single owner
        // of available variants, instead of inferring upgrades from enum values.
        var options = tooltip.BuildLockVariantOptions();
        var tiers = new List<VariantChoice<ETier>>();
        var enchantments = new List<VariantChoice<EEnchantmentType?>>();
        var nativeTiers = options.TierOptions;
        var tierCount = nativeTiers.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<CardTierOption>>().Count;
        for (var index = 0; index < tierCount; index++)
        {
            var option = nativeTiers[index];
            tiers.Add(new VariantChoice<ETier>(option.Tier, option.LabelSource.GetLocalizedText()));
        }
        var nativeEnchantments = options.EnchantmentOptions;
        var enchantmentCount = nativeEnchantments.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<CardEnchantmentOption>>().Count;
        for (var index = 0; index < enchantmentCount; index++)
        {
            var option = nativeEnchantments[index];
            var value = ReadEnchantment(option);
            enchantments.Add(new VariantChoice<EEnchantmentType?>(value, option.LabelSource.GetLocalizedText()));
        }
        return (tiers, enchantments);
    }

    private static EEnchantmentType? ReadEnchantment(CardEnchantmentOption option) =>
        NativeNullableValue.Read<EEnchantmentType>(option, "get_EnchantmentType");
}
