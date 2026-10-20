#nullable enable
using BazaarGameShared.Domain.Core.Types;

namespace BazaarPlusPlus.Game.ItemEnchantPreview;

public static class ItemEnchantPreviewSectionPolicy
{
    public static bool IsEligible(ECardType cardType, EInventorySection? section, bool isInCombat)
    {
        if (isInCombat || cardType != ECardType.Item)
            return false;
        return section == EInventorySection.Hand || section == EInventorySection.Stash;
    }
}
