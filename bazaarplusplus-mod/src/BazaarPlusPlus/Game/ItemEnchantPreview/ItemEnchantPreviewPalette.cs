#nullable enable
using BazaarGameShared.Domain.Core.Types;

namespace BazaarPlusPlus.Game.ItemEnchantPreview;

public static class ItemEnchantPreviewPalette
{
    public static string GetEnchantmentColorHex(EEnchantmentType enchantmentType)
    {
        return enchantmentType switch
        {
            EEnchantmentType.Heavy => "CB9F6E",
            EEnchantmentType.Golden => "FFCD19",
            EEnchantmentType.Icy => "3FC8F7",
            EEnchantmentType.Turbo => "00ECC3",
            EEnchantmentType.Shielded => "F4CF20",
            EEnchantmentType.Restorative => "8EEA31",
            EEnchantmentType.Toxic => "0EBE4F",
            EEnchantmentType.Fiery => "FF9F45",
            EEnchantmentType.Shiny => "98A8FE",
            EEnchantmentType.Deadly => "F5503D",
            EEnchantmentType.Radiant => "98A8FE",
            EEnchantmentType.Obsidian => "9D4A6F",
            _ => "FFFFFF",
        };
    }
}
