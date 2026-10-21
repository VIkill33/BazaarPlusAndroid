using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;
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
    }

    private readonly RectTransform _root;
    private readonly List<Request> _requests = new();
    private AndroidBuild? _shownBuild;

    internal AndroidBuildBoard(RectTransform root) => _root = root;

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
            _requests.Add(new Request { Host = host, Item = item });
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.build_board.started build={build.Id} items={build.Items.Count}");
    }

    internal void Tick()
    {
        if (_requests.Count == 0 || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var request in _requests)
        {
            if (request.Ready) continue;
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
                    if (request.Creation.IsFaulted || request.Creation.IsCanceled) { request.Ready = true; continue; }
                    request.Card = request.Creation.Result;
                    if (request.Card == null) { request.Ready = true; continue; }
                    var rect = request.Card.GetComponent<RectTransform>();
                    rect.SetParent(request.Host, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                    request.Card.gameObject.SetActive(true);
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
                    if (request.Setup.IsFaulted || request.Setup.IsCanceled) continue;
                    request.Card!.Resize();
                    request.Card.Show(true);
                    Fit(request);
                }
            }
            catch (Exception error)
            {
                request.Ready = true;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.build_board.card_failed template_id={request.Item.TemplateId} exception={error}");
            }
        }
    }

    private static void Fit(Request request)
    {
        var card = request.Card!;
        var rect = card.GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        var art = card._cardImage.rectTransform.rect;
        var width = art.width > 1 ? art.width : rect.rect.width;
        var height = art.height > 1 ? art.height : rect.rect.height;
        if (width <= 1 || height <= 1) return;
        var bounds = request.Host.rect;
        var scale = Mathf.Min(bounds.width / width, bounds.height / height);
        rect.localScale = new Vector3(scale, scale, scale);
        var center = request.Host.InverseTransformPoint(card._cardImage.rectTransform.TransformPoint(
            new Vector3(art.center.x, art.center.y, 0)));
        rect.anchoredPosition -= new Vector2(center.x, center.y);
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
