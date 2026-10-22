using TheBazaar;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

internal static class NativePreviewTooltips
{
    // Collection owns its description and variant controls. Native hover/select
    // handlers must not open a second tooltip outside that panel's lifetime.
    internal static void Suppress(CardPreviewBase card)
    {
        card._showTooltips = false;
        foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    internal static void Release(CardPreviewBase? card)
    {
        if (card == null) return;
        Suppress(card);
        var data = card._tooltipData;
        var parent = Data.TooltipParentComponent;
        if (data == null || parent == null) return;
        // Compare native identity: separate IL2CPP wrappers can refer to the same data.
        var primary = parent.CardTooltipController;
        if (primary != null && primary.CurrentTooltipData?.Pointer == data.Pointer)
        {
            parent.UnlockCardTooltipController();
            parent.HideCardTooltipController();
        }
        var secondary = parent.SecondaryCardTooltipController;
        if (secondary != null && secondary.CurrentTooltipData?.Pointer == data.Pointer)
        {
            secondary.Unlock();
            parent.HideSecondaryCardTooltipController();
        }
    }
}
