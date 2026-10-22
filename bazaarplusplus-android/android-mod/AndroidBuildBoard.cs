using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;

namespace BazaarPlusPlus.Android;

// Renders one recommended ten-slot board at a time with the game's own card renderer.
internal sealed class AndroidBuildBoard
{
    private sealed class Request
    {
        internal required RectTransform Host;
        internal required AndroidBuildItem Item;
        internal NativePreviewTask? Creation;
        internal Il2CppSystem.Threading.Tasks.Task? Setup;
        internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
        internal TCardInstance? Instance;
        internal CardPreviewBase? Card;
        internal bool Ready;
        internal int FitAttempts;
        internal int NextFitFrame;
    }

    private readonly RectTransform _root;
    private readonly Action<string> _onSelect;
    private readonly List<Request> _requests = new();
    private AndroidBuild? _shownBuild;

    internal AndroidBuildBoard(RectTransform root, Action<string> onSelect)
    {
        _root = root;
        _onSelect = onSelect;
    }

    internal void Show(AndroidBuild? build)
    {
        if (build == null) { Hide(); return; }
        if (ReferenceEquals(_shownBuild, build)) return;
        Hide();
        _shownBuild = build;
        for (var slot = 0; slot < 10; slot++)
        {
            var cell = NativeUi.Rect("BPP.BuildSlot", _root,
                new Vector2(slot / 10f, 0), new Vector2((slot + 1) / 10f, 1),
                new Vector2(2, 3), new Vector2(-2, -3));
            var background = cell.gameObject.AddComponent<Image>();
            background.color = new Color(.12f, .15f, .18f, .8f);
            background.raycastTarget = false;
        }
        foreach (var item in build.Items)
        {
            var host = NativeUi.Rect("BPP.BuildCard", _root,
                new Vector2(item.Slot / 10f, 0),
                new Vector2((item.Slot + item.Size) / 10f, 1),
                new Vector2(3, 4), new Vector2(-3, -4));
            var request = new Request { Host = host, Item = item };
            var hitArea = host.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, .01f);
            hitArea.raycastTarget = true;
            var button = host.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(() => Select(request)));
            _requests.Add(request);
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_board.started build={build.Id} items={build.Items.Count}");
    }

    internal void Tick()
    {
        if (_requests.Count == 0 || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var request in _requests)
        {
            if (request.Ready)
            {
                if (request.FitAttempts > 0 && Time.frameCount >= request.NextFitFrame)
                {
                    Fit(request);
                    request.NextFitFrame = Time.frameCount + 2;
                    request.FitAttempts--;
                    if (request.FitAttempts == 0)
                    {
                        var art = request.Card!._cardImage;
                        var rect = request.Card.GetComponent<RectTransform>();
                        var frame = request.Card._currentFrame?.GetComponent<RectTransform>();
                        var frameWidth = frame != null ? frame.rect.width : 0;
                        var frameHeight = frame != null ? frame.rect.height : 0;
                        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_board.card_fit template_id={request.Item.TemplateId} host={request.Host.rect.width:0.#}x{request.Host.rect.height:0.#} root={rect.rect.width:0.#}x{rect.rect.height:0.#} frame={frameWidth:0.#}x{frameHeight:0.#} art={art.rectTransform.rect.width:0.#}x{art.rectTransform.rect.height:0.#} texture={art.texture != null} material={request.Card._cardMaterial != null} active={art.gameObject.activeInHierarchy} alpha={art.color.a:0.##} scale={rect.localScale.x:0.###} position={rect.anchoredPosition}");
                    }
                }
                continue;
            }
            try
            {
                var template = Data.GetStatic()?.GetCardById(
                    Il2CppSystem.Guid.Parse(request.Item.TemplateId.ToString()))?.TryCast<TCardBase>();
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
                    var enchantment = Enum.TryParse<EEnchantmentType>(request.Item.Enchantment, true, out var parsed)
                        ? parsed : (EEnchantmentType?)null;
                    // The analyzer stores tiers as 1..5; native ETier uses 0..4.
                    request.Instance = AndroidCardVariants.CreateInstance(template,
                        (ETier)(request.Item.Tier - 1), enchantment);
                    request.Setup = request.Card.SetUp(template, false, request.Instance, request.Cancellation!.Token);
                }
                if (request.Setup is { IsCompleted: true } && !request.Ready)
                {
                    request.Ready = true;
                    if (request.Setup.IsFaulted || request.Setup.IsCanceled)
                    {
                        Fail(request, "setup", request.Setup.Exception);
                        continue;
                    }
                    request.Card!.Resize();
                    request.Card.Show(true);
                    Fit(request);
                    request.FitAttempts = 3;
                    request.NextFitFrame = Time.frameCount + 2;
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_board.card_ready template_id={request.Item.TemplateId} tier={request.Instance?.Tier}");
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
        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.build_board.card_failed phase={phase} template_id={request.Item.TemplateId} exception={error}");
    }

    private void Select(Request request)
    {
        if (!request.Ready || request.Card?._tooltipData == null) return;
        var tooltip = request.Card._tooltipData;
        var detail = tooltip.BuildCombinedEffectDescription();
        if (string.IsNullOrWhiteSpace(detail))
            detail = NativeUi.Text("No item description available.", "暂无物品描述。");
        _onSelect($"<b>{tooltip.GetTitle()}</b>\n{detail}");
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_board.card_selected template_id={request.Item.TemplateId}");
    }

    private static void Fit(Request request)
    {
        var card = request.Card!;
        var rect = card.GetComponent<RectTransform>();
        PrepareLayoutSize(card);
        card.Resize();
        Canvas.ForceUpdateCanvases();
        var frame = card._currentFrame?.GetComponent<RectTransform>()
            ?? card._frameContainer?.GetComponent<RectTransform>() ?? rect;
        var width = frame.rect.width;
        var height = frame.rect.height;
        if (width <= 1 || height <= 1) return;
        var bounds = request.Host.rect;
        var scale = Mathf.Min(bounds.width / width, bounds.height / height);
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

    internal void Hide()
    {
        _shownBuild = null;
        foreach (var request in _requests)
        {
            NativePreviewCleanup.Retire(request.Creation, request.Setup, request.Card, request.Cancellation);
            UnityEngine.Object.Destroy(request.Host.gameObject);
        }
        _requests.Clear();
        for (var index = _root.childCount - 1; index >= 0; index--)
            UnityEngine.Object.Destroy(_root.GetChild(index).gameObject);
    }
}
