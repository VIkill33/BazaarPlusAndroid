using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Players;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;

namespace BazaarPlusPlus.Android;

// CardPreviewBase is the same native card renderer used by the Android collection.
// Every rental belongs to one observed tooltip and is retired when that tooltip closes.
internal sealed class AndroidMonsterCardGallery
{
    private sealed class Request
    {
        internal required RectTransform Host;
        internal required TCardInstanceItem Instance;
        internal NativePreviewTask? Creation;
        internal Il2CppSystem.Threading.Tasks.Task? Setup;
        internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
        internal CardPreviewBase? Card;
        internal bool Ready;
    }

    private readonly RectTransform _root;
    private readonly RectTransform _panel;
    private readonly Action<string> _onSelect;
    private readonly List<Request> _requests = new();
    private string? _monsterId;

    internal AndroidMonsterCardGallery(RectTransform panel, Action<string> onSelect)
    {
        _panel = panel;
        _onSelect = onSelect;
        _root = NativeUi.Rect("BPP.MonsterCards", panel, Vector2.zero, Vector2.one,
            new Vector2(16, 12), new Vector2(-16, -36));
        var background = _root.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .07f, .05f, .35f);
        background.raycastTarget = false;
        _root.gameObject.SetActive(false);
    }

    internal void Show(TMonster? monster)
    {
        var id = monster?.Id.ToString();
        if (_monsterId == id && _root.gameObject.activeSelf) return;
        Hide();
        if (monster?.Player?.Hand?.Items == null) return;
        _monsterId = id;
        var items = monster.Player.Hand.Items;
        var count = items.Count;
        if (count == 0) return;
        var canvas = _panel.GetComponentInParent<Canvas>();
        var scale = canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
        var cardWidth = Screen.width / 26f / scale;
        var cardHeight = cardWidth * 2.05f;
        var columnWidth = cardWidth + 10;
        var rowHeight = cardHeight + 12;
        var columns = Math.Min(count, Math.Max(1, Mathf.FloorToInt(
            (Screen.width / scale - 500) / columnWidth)));
        var rows = (count + columns - 1) / columns;
        var panelWidth = columns * columnWidth + 42;
        var panelHeight = rows * rowHeight + 44;
        _panel.offsetMin = new Vector2(-550 - panelWidth, 520 - panelHeight / 2);
        _panel.offsetMax = new Vector2(-550, 520 + panelHeight / 2);
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.right;
        _root.pivot = new Vector2(.5f, 0);
        _root.offsetMin = new Vector2(16, 12);
        _root.offsetMax = new Vector2(-16, panelHeight - 36);
        _root.gameObject.SetActive(true);
        for (var index = 0; index < count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var host = NativeUi.Rect("BPP.MonsterCard", _root, Vector2.up, Vector2.up);
            host.pivot = Vector2.up;
            host.sizeDelta = new Vector2(cardWidth, cardHeight);
            host.anchoredPosition = new Vector2(column * columnWidth + 4, -row * rowHeight - 4);
            var request = new Request { Host = host, Instance = items[index] };
            var hitArea = host.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, .01f);
            hitArea.raycastTarget = true;
            var button = host.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(() => Select(request)));
            _requests.Add(request);
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_gallery.started monster={monster.InternalName} items={count} rows={rows} card_width_px={cardWidth * scale:0.#} screen_width={Screen.width}");
    }

    private void Select(Request request)
    {
        if (!request.Ready || request.Card?._tooltipData == null) return;
        var tooltip = request.Card._tooltipData;
        var detail = tooltip.BuildCombinedEffectDescription();
        if (string.IsNullOrWhiteSpace(detail))
            detail = NativeUi.Text("No item description available.", "暂无物品描述。");
        _onSelect($"<b>{tooltip.GetTitle()}</b>\n{detail}");
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_gallery.card_selected template_id={request.Instance.TemplateId}");
    }

    internal void Tick()
    {
        if (!_root.gameObject.activeSelf || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var request in _requests)
        {
            if (request.Ready) continue;
            try
            {
                if (request.Creation == null)
                {
                    var template = Data.GetStatic()?.GetCardById(request.Instance.TemplateId)?.TryCast<TCardBase>();
                    if (template == null) { request.Ready = true; continue; }
                    request.Cancellation = new Il2CppSystem.Threading.CancellationTokenSource();
                    request.Creation = loader.GenerateUICard(request.Host, request.Cancellation.Token, template);
                    continue;
                }
                if (request.Card == null && request.Creation.IsCompleted)
                {
                    if (request.Creation.IsFaulted || request.Creation.IsCanceled) { request.Ready = true; continue; }
                    request.Card = request.Creation.Result;
                    var template = Data.GetStatic()?.GetCardById(request.Instance.TemplateId)?.TryCast<TCardBase>();
                    if (request.Card == null || template == null) { request.Ready = true; continue; }
                    var rect = request.Card.GetComponent<RectTransform>();
                    rect.SetParent(request.Host, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    request.Card.gameObject.SetActive(true);
                    request.Setup = request.Card.SetUp(template, false, request.Instance, request.Cancellation!.Token);
                }
                if (request.Setup is { IsCompleted: true } && !request.Ready)
                {
                    request.Ready = true;
                    if (request.Setup.IsFaulted || request.Setup.IsCanceled) continue;
                    request.Card!.Resize();
                    request.Card.Show(true);
                    Fit(request);
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_gallery.card_ready template_id={request.Instance.TemplateId}");
                }
            }
            catch (Exception error)
            {
                request.Ready = true;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.monster_gallery.card_failed template_id={request.Instance.TemplateId} exception={error}");
            }
        }
    }

    private static void Fit(Request request)
    {
        var card = request.Card!;
        var rect = card.GetComponent<RectTransform>();
        var fitter = card.GetComponent<AspectRatioFitter>();
        var ratio = fitter != null && fitter.aspectRatio > .01f ? fitter.aspectRatio : .8f;
        if (rect.rect.width <= 1 || rect.rect.height <= 1)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500 * ratio);
        }
        Canvas.ForceUpdateCanvases();
        var art = card._cardImage.rectTransform.rect;
        var bounds = request.Host.rect;
        var width = art.width > 1 ? art.width : rect.rect.width;
        var scale = bounds.width / Mathf.Max(1, width);
        rect.localScale = new Vector3(scale, scale, scale);
        var center = request.Host.InverseTransformPoint(card._cardImage.rectTransform.TransformPoint(
            new Vector3(art.center.x, art.center.y, 0)));
        rect.anchoredPosition -= new Vector2(center.x, center.y);
    }

    internal void Hide()
    {
        _root.gameObject.SetActive(false);
        _monsterId = null;
        foreach (var request in _requests)
        {
            NativePreviewCleanup.Retire(request.Creation, request.Setup, request.Card, request.Cancellation);
            UnityEngine.Object.Destroy(request.Host.gameObject);
        }
        _requests.Clear();
    }
}
