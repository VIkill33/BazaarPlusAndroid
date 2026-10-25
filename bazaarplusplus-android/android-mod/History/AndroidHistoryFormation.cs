using System.Text.Json;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NativeCreation = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;
using NativeTask = Il2CppSystem.Threading.Tasks.Task;

namespace BazaarPlusPlus.Android;

// Owns only synthetic previews made from saved DTOs. Never receives a live card or controller.
internal sealed class AndroidHistoryFormation
{
    private sealed class Preview
    {
        internal required JsonElement Saved;
        internal required RectTransform Box;
        internal required TextMeshProUGUI Placeholder;
        internal float Top, Height;
        internal GameObject? NativeRoot;
        internal NativeCreation? Creation;
        internal NativeTask? Setup;
        internal CardPreviewBase? Card;
        internal TCardInstance? Instance;
        internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
        internal bool Started, Ready;
        internal int FitAttempts;
    }
    private readonly RectTransform _root, _content, _viewport, _detail;
    private readonly ScrollRect _scroll, _detailScroll;
    private readonly TextMeshProUGUI _detailText;
    private readonly RectTransform _detailContent;
    private readonly TMP_FontAsset _font;
    private readonly List<Preview> _previews = new();
    private readonly List<GameObject> _owned = new();
    private Preview? _pressed;
    private int? _pointer;
    private float _pressedAt, _height;
    private bool _active;

    internal AndroidHistoryFormation(RectTransform panel, TMP_FontAsset font)
    {
        _font = font;
        _root = NativeUi.Rect("BPP.HistoryFormation", panel, new(.03f,.16f), new(.97f,.80f));
        _viewport = NativeUi.Rect("Viewport", _root, Vector2.zero, Vector2.one);
        _viewport.gameObject.AddComponent<RectMask2D>(); _viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.02f);
        _content = NativeUi.Rect("Content", _viewport, new(0,1), Vector2.one); _content.pivot = new(.5f,1);
        _scroll = _root.gameObject.AddComponent<ScrollRect>(); _scroll.viewport = _viewport; _scroll.content = _content;
        _scroll.horizontal = false; _scroll.movementType = ScrollRect.MovementType.Clamped;
        _detail = NativeUi.Rect("BPP.HistoryCardDetail", panel, Vector2.zero, Vector2.one);
        var background = NativeUi.Button(_detail, font, "", Vector2.zero, Vector2.one, () => CloseDetail());
        background.GetComponent<Image>().color = new Color(0,0,0,.85f);
        var sheet = NativeUi.Rect("DetailSheet", _detail, new(.18f,.12f), new(.82f,.88f));
        sheet.gameObject.AddComponent<Image>().color = new Color(.09f,.12f,.15f,1);
        (_detailText, _detailContent, _detailScroll) = NativeUi.ScrollText(sheet, font, new(.02f,.10f), new(.98f,.98f));
        _detailText.richText = false; _detailText.fontSize = 25;
        NativeUi.Button(sheet, font, "关闭", new(.76f,.01f), new(.98f,.09f), () => CloseDetail());
        _detail.gameObject.SetActive(false); _root.gameObject.SetActive(false);
    }
    private RectTransform Area(string name, float left, float right, float top, float height)
    {
        var rect = NativeUi.Rect(name, _content, new(left,1), new(right,1), new(4,-top-height), new(-4,-top));
        _owned.Add(rect.gameObject); return rect;
    }
    private void Text(string value, float left, float right, float top, float height, float size = 23)
    {
        var label = NativeUi.Label(Area("Label",left,right,top,height),_font,value,size);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.richText = false;
    }
    internal void Show(JsonElement battle)
    {
        Hide(); _active = true; _root.gameObject.SetActive(true);
        var opening = battle.GetProperty("opening");
        var version = HistoryViewText.Value(opening,"gameVersion");
        Text(HistoryViewText.Battle(battle) + $"\n保存版本：{version}" + (version != Application.version ? " · 与当前版本不同，卡面仅供参考" : " · 原生卡面仅供参考"),0,1,0,110);
        Text("我方：" + HistoryViewText.Value(opening,"playerHero"),0,.5f,110,42,27);
        Text("对方：" + (HistoryViewText.Value(battle,"kind") == "pve" ? "野怪（名称未记录）" : HistoryViewText.Value(opening,"opponentHero")),.5f,1,110,42,27);
        var snapshots = battle.GetProperty("snapshots");
        var left = Group(snapshots.GetProperty("playerHand"),"物品",0,.5f,152,false);
        left = Group(snapshots.GetProperty("playerSkills"),"技能",0,.5f,left,true);
        var right = Group(snapshots.GetProperty("opponentHand"),"物品",.5f,1,152,false);
        right = Group(snapshots.GetProperty("opponentSkills"),"技能",.5f,1,right,true);
        _height = Math.Max(left,right) + 12; _content.sizeDelta = new(0,_height); _scroll.verticalNormalizedPosition = 1;
    }
    private float Group(JsonElement group, string name, float left, float right, float top, bool skills)
    {
        Text(name + " · " + HistoryViewText.Group(group),left,right,top,44); top += 44;
        var cards = group.GetProperty("cards");
        if (cards.GetArrayLength() == 0) return top + 16;
        var next = 0; var rows = 1; var columns = skills ? 5 : 10; var height = skills ? 174 : 248;
        foreach (var card in cards.EnumerateArray())
        {
            var size = skills ? 1 : HistoryViewText.Value(card,"size") switch { "Medium" => 2, "Large" => 3, _ => 1 };
            var socket = HistoryViewText.Value(card,"socket");
            var slot = !skills && int.TryParse(socket[(socket.LastIndexOf('_') + 1)..],out var stored) && stored > 0 ? Math.Min(127,stored - 1) : next;
            var row = slot / columns; var column = slot % columns;
            var x = left + (right-left) * column / columns; var end = left + (right-left) * Math.Min(columns,column+size) / columns;
            var box = Area("SavedCard",x,end,top + row * height,height - 8);
            var button = NativeUi.Button(box,_font,"",Vector2.zero,Vector2.one,() => { if (PlayerPreferences.Data?.AlternativeInput != true) ShowDetail(card); });
            box.gameObject.AddComponent<Image>().color = new Color(.13f,.17f,.20f,1);
            button.GetComponent<Image>().color = new Color(0,0,0,.01f);
            var placeholder = NativeUi.Label(box,_font,HistoryViewText.Value(card,"name") + "\n" + HistoryViewText.Value(card,"tier"),17);
            placeholder.richText = false; placeholder.overflowMode = TextOverflowModes.Ellipsis;
            var caption = NativeUi.Label(NativeUi.Rect("SavedName",box,Vector2.zero,new(1,.15f)),_font,HistoryViewText.Value(card,"name"),16);
            caption.richText = false; caption.overflowMode = TextOverflowModes.Ellipsis;
            var preview = new Preview { Saved=card,Box=box,Placeholder=placeholder,Top=top+row*height,Height=height };
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            Event(trigger,EventTriggerType.PointerDown,data => { if (_pressed == null) { _pressed=preview; _pointer=data.pointerId; _pressedAt=Time.realtimeSinceStartup; } });
            Event(trigger,EventTriggerType.PointerUp,data => { if (_pointer == data.pointerId) ReleaseGesture(); });
            Event(trigger,EventTriggerType.PointerExit,data => { if (_pointer == data.pointerId && !_detail.gameObject.activeSelf) ReleaseGesture(); });
            Event(trigger,EventTriggerType.InitializePotentialDrag,data => _scroll.OnInitializePotentialDrag(data));
            Event(trigger,EventTriggerType.BeginDrag,data => { ReleaseGesture(); data.eligibleForClick=false; _scroll.OnBeginDrag(data); });
            Event(trigger,EventTriggerType.Drag,data => _scroll.OnDrag(data));
            Event(trigger,EventTriggerType.EndDrag,data => _scroll.OnEndDrag(data));
            _previews.Add(preview); next = Math.Max(next,slot+size); rows = Math.Max(rows,row+1);
        }
        return top + rows * height + 12;
    }
    private static void Event(EventTrigger trigger, EventTriggerType type, Action<PointerEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID=type };
        entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((BaseEventData data) => action(data.Cast<PointerEventData>()))); trigger.triggers.Add(entry);
    }
    private void ReleaseGesture()
    {
        _pressed = null; _pointer = null;
        if (PlayerPreferences.Data?.AlternativeInput == true) CloseDetail();
    }
    private void ShowDetail(JsonElement card)
    {
        _detail.gameObject.SetActive(true); _detail.SetAsLastSibling();
        _detailText.text = "历史卡牌 · 只读\n" + HistoryViewText.Fields(card);
        Canvas.ForceUpdateCanvases();
        var preferred = _detailText.GetPreferredValues(_detailText.text,Math.Max(1,_detailContent.rect.width - 24),float.PositiveInfinity);
        _detailContent.sizeDelta = new(0,Math.Max(80,preferred.y+24)); _detailScroll.verticalNormalizedPosition = 1;
    }
    internal bool CloseDetail()
    {
        _pressed = null; _pointer = null;
        if (!_detail.gameObject.activeSelf) return false;
        _detail.gameObject.SetActive(false); return true;
    }
    internal void Tick()
    {
        if (!_active) return;
        if (_pressed != null && Time.realtimeSinceStartup - _pressedAt >= .35f && !_detail.gameObject.activeSelf) ShowDetail(_pressed.Saved);
        var visibleTop = Math.Max(0,_height - _viewport.rect.height) * (1 - _scroll.verticalNormalizedPosition);
        var started = false;
        foreach (var request in _previews)
        {
            try
            {
                if (request.Creation != null && request.Creation.IsCompleted)
                {
                    var creation = request.Creation; request.Creation = null;
                    if (creation.IsFaulted || creation.IsCanceled) throw new InvalidOperationException("Native card image unavailable");
                    request.Card = creation.Result;
                    if (request.Card == null) throw new InvalidOperationException("Native card image missing");
                    NativePreviewTooltips.Suppress(request.Card);
                    request.Setup = request.Card.SetUp(Data.GetStatic().GetCardById(new Il2CppSystem.Guid(HistoryViewText.Value(request.Saved,"templateId"))).TryCast<TCardBase>(),false,request.Instance,request.Cancellation!.Token);
                }
                if (request.Setup != null && request.Setup.IsCompleted)
                {
                    var setup = request.Setup; request.Setup = null;
                    if (setup.IsFaulted || setup.IsCanceled) throw new InvalidOperationException("Native card setup unavailable");
                    // SetUp rebuilds its synthetic client card from tier defaults. Restore
                    // saved values on that owned preview after setup, before drawing gems.
                    var client = request.Card!._clientCard
                        ?? throw new InvalidOperationException("Historical preview instance missing");
                    var attributes = new Il2CppSystem.Collections.Generic.Dictionary<ECardAttributeType,int>();
                    foreach (var attribute in request.Saved.GetProperty("attributes").EnumerateObject())
                        attributes[Enum.Parse<ECardAttributeType>(attribute.Name)] = attribute.Value.GetInt32();
                    client.Attributes = attributes;
                    var item = request.Card.TryCast<CardPreviewItem>();
                    if (item?._gemGroupController != null)
                        item._gemGroupController.Initialize(client,
                            new Il2CppSystem.Collections.Generic.List<ECardAttributeType>()
                                .Cast<Il2CppSystem.Collections.Generic.IEnumerable<ECardAttributeType>>(),true);
                    NativePreviewTooltips.Suppress(request.Card!); request.Card!.Resize(); request.Card.Show(true);
                    request.Ready = true; request.FitAttempts = 4; request.Placeholder.text = "";
                }
                if (request.Ready && request.FitAttempts-- > 0) Fit(request);
                if (!request.Started && !started && request.Top + request.Height >= visibleTop && request.Top <= visibleTop + _viewport.rect.height)
                { started = true; Start(request); }
            }
            catch (Exception error)
            {
                Retire(request); request.Started = true;
                request.Placeholder.text = HistoryViewText.Value(request.Saved,"name") + "\n" + HistoryViewText.Value(request.Saved,"tier") + "\n无可用卡面，点击查看保存字段";
                Plugin.PluginLog?.LogDebug($"[BPP][Android] event=history.preview.fallback reason={error.Message}");
            }
        }
    }
    private static void Start(Preview request)
    {
        if (!Data.IsManagerCreated() || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        request.Started = true;
        var template = Data.GetStatic().GetCardById(new Il2CppSystem.Guid(HistoryViewText.Value(request.Saved,"templateId"))).TryCast<TCardBase>()
            ?? throw new InvalidOperationException("Historical template no longer exists");
        if (!Enum.TryParse<ETier>(HistoryViewText.Value(request.Saved,"tier"),out var tier)
            || !Enum.TryParse<ECardType>(HistoryViewText.Value(request.Saved,"type"),out var type) || type != template.Type)
            throw new InvalidOperationException("Historical card type or tier unavailable");
        EEnchantmentType? enchantment = null;
        if (request.Saved.TryGetProperty("enchantment",out var savedEnchantment) && savedEnchantment.ValueKind != JsonValueKind.Null)
        {
            if (!Enum.TryParse<EEnchantmentType>(savedEnchantment.GetString(),out var parsed)) throw new InvalidOperationException("Historical enchantment unavailable");
            enchantment = parsed;
        }
        request.Instance = AndroidCardVariants.CreateInstance(template,tier,enchantment);
        foreach (var attribute in request.Saved.GetProperty("attributes").EnumerateObject())
        {
            if (!Enum.TryParse<ECardAttributeType>(attribute.Name,out var key)) throw new InvalidOperationException("Historical attribute unavailable");
            request.Instance.Attributes[key] = attribute.Value.GetInt32();
        }
        var host = NativeUi.Rect("OwnedHistoricalPreview",request.Box,new(0,.15f),Vector2.one);
        request.NativeRoot = host.gameObject; request.NativeRoot.transform.SetAsFirstSibling();
        request.Cancellation = new(); request.Creation = loader.GenerateUICard(host,request.Cancellation.Token,template);
    }
    private static void Fit(Preview request)
    {
        var card = request.Card!; var rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new(.5f,.5f); rect.anchoredPosition = Vector2.zero; rect.localRotation = Quaternion.identity;
        var fitter = card.GetComponent<AspectRatioFitter>();
        var ratio = fitter != null && fitter.aspectRatio > .01f ? fitter.aspectRatio : .8f;
        if (rect.rect.width <= 1 || rect.rect.height <= 1)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,500);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,500 * ratio);
        }
        card.Resize();
        Canvas.ForceUpdateCanvases();
        var frame = card._currentFrame?.GetComponent<RectTransform>()
            ?? card._frameContainer?.GetComponent<RectTransform>() ?? rect;
        if (frame.rect.width <= 1 || frame.rect.height <= 1) return;
        var parent = request.NativeRoot!.GetComponent<RectTransform>(); var bounds = parent.rect;
        var scale = Math.Min(bounds.width / frame.rect.width,bounds.height / frame.rect.height) * .86f;
        rect.localScale = new(scale,scale,scale);
        var center = parent.InverseTransformPoint(frame.TransformPoint(frame.rect.center));
        rect.anchoredPosition -= new Vector2(center.x,center.y);
    }
    private static void Retire(Preview request)
    {
        NativePreviewTooltips.Release(request.Card);
        NativePreviewCleanup.Retire(request.Creation,request.Setup,request.Card,request.Cancellation,request.NativeRoot);
        request.Creation = null; request.Setup = null; request.Card = null; request.Cancellation = null; request.NativeRoot = null; request.Instance = null; request.Ready = false;
    }
    internal void Hide()
    {
        _active = false; _pressed = null; CloseDetail();
        foreach (var request in _previews) Retire(request);
        _previews.Clear(); foreach (var owned in _owned) UnityEngine.Object.Destroy(owned); _owned.Clear(); _root.gameObject.SetActive(false);
    }
}
