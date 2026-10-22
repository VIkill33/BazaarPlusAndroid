using BazaarGameShared.Domain.Cards;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;

namespace BazaarPlusPlus.Android;

// Small native card previews inside the selectable candidate buttons.
internal sealed class AndroidBuildCandidates
{
    private sealed class Request
    {
        internal required Button Button;
        internal bool FillCell;
        internal required Guid Id;
        internal required RectTransform Host;
        internal NativePreviewTask? Creation;
        internal Il2CppSystem.Threading.Tasks.Task? Setup;
        internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
        internal TCardInstance? Instance;
        internal CardPreviewBase? Card;
        internal bool Ready;
        internal int FitAttempts;
        internal int NextFitFrame;
    }

    private readonly Dictionary<Button, Request> _requests = new();

    internal void Show(Button button, Guid id, bool fillCell = false, float captionHeight = 0)
    {
        if (_requests.TryGetValue(button, out var existing) && existing.Id == id)
        {
            if (existing.Card != null && existing.Ready) { existing.FitAttempts = 3; existing.NextFitFrame = Time.frameCount + 1; }
            return;
        }
        Remove(button);
        var host = NativeUi.Rect("BPP.CandidateCard", button.transform,
            fillCell || captionHeight > 0 ? new Vector2(.05f, .05f + captionHeight) : new Vector2(.02f, .08f),
            fillCell || captionHeight > 0 ? new Vector2(.95f, .95f) : new Vector2(.30f, .92f));
        _requests.Add(button, new Request { Button = button, Id = id, Host = host, FillCell = fillCell });
        // An invisible graphic keeps taps on the card routed to the parent candidate button.
        var hit = NativeUi.Rect("BPP.CandidateHit", button.transform, Vector2.zero, Vector2.one);
        var graphic = hit.gameObject.AddComponent<Image>();
        graphic.color = new Color(0, 0, 0, .001f);
        graphic.raycastTarget = true;
    }

    internal void Remove(Button button)
    {
        if (!_requests.Remove(button, out var request)) return;
        NativePreviewCleanup.Retire(request.Creation, request.Setup, request.Card, request.Cancellation, request.Host.gameObject);
        var hit = button.transform.Find("BPP.CandidateHit");
        if (hit != null) UnityEngine.Object.Destroy(hit.gameObject);
    }

    internal void Hide()
    {
        foreach (var button in _requests.Keys.ToArray()) Remove(button);
    }

    internal void Tick()
    {
        if (_requests.Count == 0 || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var request in _requests.Values)
        {
            if (request.Ready)
            {
                if (request.FitAttempts > 0 && Time.frameCount >= request.NextFitFrame)
                {
                    Fit(request);
                    request.FitAttempts--;
                    request.NextFitFrame = Time.frameCount + 2;
                }
                continue;
            }
            try
            {
                var template = Data.GetStatic()?.GetCardById(
                    Il2CppSystem.Guid.Parse(request.Id.ToString()))?.TryCast<TCardBase>();
                if (template == null) { request.Ready = true; continue; }
                if (request.Creation == null)
                {
                    request.Cancellation = new Il2CppSystem.Threading.CancellationTokenSource();
                    request.Creation = loader.GenerateUICard(request.Host, request.Cancellation.Token, template);
                    continue;
                }
                if (request.Card == null && request.Creation.IsCompleted)
                {
                    if (request.Creation.IsFaulted || request.Creation.IsCanceled)
                    {
                        Fail(request, "create", request.Creation.Exception);
                        continue;
                    }
                    request.Card = request.Creation.Result;
                    if (request.Card == null) { Fail(request, "create_null", null); continue; }
                    var rect = request.Card.GetComponent<RectTransform>();
                    rect.SetParent(request.Host, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    request.Card.gameObject.SetActive(true);
                    PrepareLayoutSize(request.Card);
                    request.Card.Resize();
                    request.Instance = AndroidCardVariants.CreateInstance(template, template.StartingTier, null);
                    request.Setup = request.Card.SetUp(template, false, request.Instance,
                        request.Cancellation!.Token);
                }
                if (request.Setup is { IsCompleted: true } && !request.Ready)
                {
                    if (request.Setup.IsFaulted || request.Setup.IsCanceled)
                    {
                        Fail(request, "setup", request.Setup.Exception);
                        continue;
                    }
                    request.Ready = true;
                    request.Card!.Resize();
                    request.Card.Show(true);
                    Fit(request);
                    request.FitAttempts = 3;
                    request.NextFitFrame = Time.frameCount + 2;
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_candidates.card_ready template_id={request.Id}");
                }
            }
            catch (Exception error)
            {
                Fail(request, "exception", error);
            }
        }
    }

    private static void Fail(Request request, string phase, object? error)
    {
        request.Ready = true;
        if (request.Card != null) request.Card.gameObject.SetActive(false);
        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.build_candidates.card_failed phase={phase} template_id={request.Id} exception={error}");
    }

    private static void Fit(Request request)
    {
        var card = request.Card!;
        var rect = card.GetComponent<RectTransform>();
        PrepareLayoutSize(card);
        card.Resize();
        Canvas.ForceUpdateCanvases();
        // Collection cells follow body spans. Measure the artwork, because a large card's
        // native frame rectangle contains empty margins that would shrink only large cards.
        var frame = request.FillCell && card._cardImage != null ? card._cardImage.rectTransform :
            card._currentFrame?.GetComponent<RectTransform>() ?? card._frameContainer?.GetComponent<RectTransform>() ?? rect;
        if (frame.rect.width <= 1 || frame.rect.height <= 1) return;
        var bounds = request.Host.rect;
        var scale = Mathf.Min(bounds.width / frame.rect.width, bounds.height / frame.rect.height);
        rect.localScale = new Vector3(scale, scale, scale);
        var center = request.Host.InverseTransformPoint(frame.TransformPoint(frame.rect.center));
        rect.anchoredPosition -= new Vector2(center.x, center.y);
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
