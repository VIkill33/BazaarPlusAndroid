using BazaarPlusPlus.Android;
using TheBazaar;
using TheBazaar.UI;

var card = new CardPreviewBase { _tooltipData = new TooltipData(1) };
var parent = new TooltipParent();
Data.TooltipParentComponent = parent;
parent.CardTooltipController.CurrentTooltipData = new TooltipData(1);
parent.CardTooltipController.Locked = true;
parent.SecondaryCardTooltipController.CurrentTooltipData = new TooltipData(2);
NativePreviewTooltips.Release(card);
Require(!card._showTooltips && card.Graphics.All(g => !g.raycastTarget), "Preview still accepts native hover input");
Require(!parent.CardTooltipController.Visible && !parent.CardTooltipController.Locked, "Owned locked tooltip survived release");
Require(parent.SecondaryCardTooltipController.Visible, "Unrelated secondary tooltip was hidden");

parent.CardTooltipController.Visible = true;
parent.CardTooltipController.CurrentTooltipData = new TooltipData(3);
parent.SecondaryCardTooltipController.CurrentTooltipData = new TooltipData(1);
parent.SecondaryCardTooltipController.Locked = true;
NativePreviewTooltips.Release(card);
Require(parent.CardTooltipController.Visible, "Unrelated gameplay tooltip was hidden");
Require(!parent.SecondaryCardTooltipController.Visible && !parent.SecondaryCardTooltipController.Locked,
    "Owned secondary tooltip survived release");
NativePreviewTooltips.Release(card);
Require(parent.CardTooltipController.Visible, "Repeated cleanup changed an unrelated tooltip");

Data.TooltipParentComponent = null;
NativePreviewTooltips.Release(new CardPreviewBase());
NativePreviewTooltips.Release(null);
Console.WriteLine("Preview input suppression and owned primary/secondary tooltip cleanup passed.");

static void Require(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
}

namespace UnityEngine { }
namespace UnityEngine.UI
{
    public class Graphic { public bool raycastTarget = true; }
}
namespace TheBazaar.UI
{
    public sealed record TooltipData(int Id) { public IntPtr Pointer => new(Id); }
    public sealed class CardPreviewBase
    {
        public bool _showTooltips = true;
        public TooltipData? _tooltipData;
        public UnityEngine.UI.Graphic[] Graphics = [new(), new()];
        public T[] GetComponentsInChildren<T>(bool includeInactive) => (T[])(object)Graphics;
    }
    public sealed class TooltipController
    {
        public TooltipData? CurrentTooltipData;
        public bool Visible = true;
        public bool Locked;
        public void Unlock() => Locked = false;
    }
    public sealed class TooltipParent
    {
        public TooltipController CardTooltipController = new();
        public TooltipController SecondaryCardTooltipController = new();
        public void UnlockCardTooltipController() => CardTooltipController.Unlock();
        public void HideCardTooltipController() => CardTooltipController.Visible = false;
        public void HideSecondaryCardTooltipController() => SecondaryCardTooltipController.Visible = false;
    }
}
namespace TheBazaar
{
    public static class Data { public static TooltipParent? TooltipParentComponent; }
}
