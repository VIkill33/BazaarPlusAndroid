using BazaarGameShared.Domain.Core.Types;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Places CardPreviewBase renders into slot hosts the way the game board sizes its items.
internal static class NativeCardFit
{
    // One native size unit is a 260-wide card root under a 518.5-high frame; the
    // small extra width leaves the hairline gap seen between cards on the game board.
    internal const float UnitAspect = .52f;

    internal static int SizeInSlots(ECardSize size) => size switch
    {
        ECardSize.Medium => 2,
        ECardSize.Large => 3,
        _ => 1,
    };

    internal static void Attach(CardPreviewBase card, RectTransform host)
    {
        var rect = card.GetComponent<RectTransform>();
        rect.SetParent(host, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        card.gameObject.SetActive(true);
        PrepareLayoutSize(card);
        card.Resize();
    }

    // Item frames carry size-dependent empty margins, widest on Large, so the root's
    // width is the true size span; fitting the frame would shrink larger items.
    internal static void FitItem(CardPreviewBase card, RectTransform host) => Fit(card, host, true);

    internal static void FitFrame(CardPreviewBase card, RectTransform host) => Fit(card, host, false);

    private static void Fit(CardPreviewBase card, RectTransform host, bool rootWidth)
    {
        var rect = card.GetComponent<RectTransform>();
        PrepareLayoutSize(card);
        card.Resize();
        Canvas.ForceUpdateCanvases();
        var frame = card._currentFrame?.GetComponent<RectTransform>()
            ?? card._frameContainer?.GetComponent<RectTransform>() ?? rect;
        var width = rootWidth ? rect.rect.width : frame.rect.width;
        var height = frame.rect.height;
        if (width <= 1 || height <= 1) return;
        var bounds = host.rect;
        var scale = Mathf.Min(bounds.width / width, bounds.height / height);
        rect.localScale = new Vector3(scale, scale, scale);
        var center = host.InverseTransformPoint(frame.TransformPoint(frame.rect.center));
        rect.anchoredPosition += bounds.center - new Vector2(center.x, center.y);
    }

    private static void PrepareLayoutSize(CardPreviewBase card)
    {
        var rect = card.GetComponent<RectTransform>();
        if (rect.rect.width > 1 && rect.rect.height > 1) return;
        var fitter = card.GetComponent<AspectRatioFitter>();
        var ratio = fitter != null && fitter.aspectRatio > .01f ? fitter.aspectRatio : .8f;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500 * ratio);
    }
}
