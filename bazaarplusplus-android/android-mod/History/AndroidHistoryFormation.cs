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
        internal bool Skill;
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
    private const float Pad = 20, Gap = 16, Caption = 28, RowGap = 6;
    private const int SkillColumns = 10;
    private static readonly Color Board = new(0,0,0,.28f);
    private static readonly Color Rule = new(.85f,.68f,.38f,.35f);

    internal AndroidHistoryFormation(RectTransform panel, TMP_FontAsset font)
    {
        _font = font;
        _root = NativeUi.Rect("BPP.HistoryFormation", panel, new(.03f,.07f), new(.97f,.875f));
        _viewport = NativeUi.Rect("Viewport", _root, Vector2.zero, Vector2.one);
        _viewport.gameObject.AddComponent<RectMask2D>(); _viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.02f);
        _content = NativeUi.Rect("Content", _viewport, new(0,1), Vector2.one); _content.pivot = new(.5f,1);
        _scroll = _root.gameObject.AddComponent<ScrollRect>(); _scroll.viewport = _viewport; _scroll.content = _content;
        _scroll.horizontal = false; _scroll.movementType = ScrollRect.MovementType.Clamped;
        _detail = NativeUi.Rect("BPP.HistoryCardDetail", panel, Vector2.zero, Vector2.one);
        var background = NativeUi.Button(_detail, font, "", Vector2.zero, Vector2.one, () => CloseDetail());
        background.GetComponent<Image>().color = new Color(0,0,0,.85f);
        var sheet = NativeUi.Rect("DetailSheet", _detail, new(.18f,.12f), new(.82f,.88f));
        var sheetImage = sheet.gameObject.AddComponent<Image>(); sheetImage.color = AndroidMarketPanelTheme.Surface;
        AndroidHistoryPanel.AddFrame(sheetImage, AndroidHistoryPanel.Frame);
        (_detailText, _detailContent, _detailScroll) = NativeUi.ScrollText(sheet, font, new(.03f,.11f), new(.97f,.97f));
        _detailText.richText = false; _detailText.fontSize = 26; _detailText.color = AndroidHistoryPanel.Body;
        AndroidPanelButtonSkin.Register(NativeUi.Button(sheet, font, "关闭", new(.76f,.015f), new(.97f,.095f), () => CloseDetail()));
        _detail.gameObject.SetActive(false); _root.gameObject.SetActive(false);
    }
    // Content coordinates are canvas units from the top-left of the scrolling content.
    private RectTransform Area(string name, float x, float top, float width, float height)
    {
        var rect = NativeUi.Rect(name, _content, new(0,1), new(0,1), new(x,-top-height), new(x+width,-top));
        _owned.Add(rect.gameObject); return rect;
    }
    private RectTransform Surface(string name, float x, float top, float width, float height, Color color, bool frame)
    {
        var rect = Area(name,x,top,width,height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        if (frame) AndroidHistoryPanel.AddFrame(image, AndroidHistoryPanel.Frame);
        return rect;
    }
    private static void Resize(RectTransform rect, float height) => rect.offsetMin = new(rect.offsetMin.x, rect.offsetMax.y - height);
    private TextMeshProUGUI Text(string value, float x, float top, float width, float height, float size, Color color, bool bold = false)
    {
        var label = NativeUi.Label(Area("Label",x,top,width,height),_font,value,size);
        // Ellipsis also drops a line that is taller than its rect, so the label keeps the full height.
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.richText = false; label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        if (bold) label.fontStyle = FontStyles.Bold;
        return label;
    }
    internal void Show(JsonElement battle)
    {
        Hide(); _active = true; _root.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var width = Math.Max(400,_content.rect.width);
        var opening = battle.GetProperty("opening");
        var version = HistoryViewText.Value(opening,"gameVersion");
        Surface("BattleInfo",0,0,width,96,AndroidMarketPanelTheme.Surface,true);
        Text(HistoryViewText.BattleWhen(battle),Pad,6,width - 2 * Pad - 160,48,30,AndroidHistoryPanel.Heading,true);
        var result = Text(HistoryViewText.Result(battle),width - Pad - 160,6,160,48,32,
            HistoryViewText.Value(battle,"outcome") switch { "win" => AndroidHistoryPanel.Success, "loss" => AndroidHistoryPanel.Loss, _ => AndroidHistoryPanel.Muted },true);
        result.alignment = TextAlignmentOptions.MidlineRight;
        Text($"对手：{HistoryViewText.OpponentName(battle)} · {HistoryViewText.Value(opening,"opponentHero")} · 保存版本 {version}"
            + (version != Application.version ? " · 与当前版本不同，卡面仅供参考" : ""),Pad,52,width - 2 * Pad,38,23,AndroidHistoryPanel.Muted);
        var snapshots = battle.GetProperty("snapshots");
        const float sidesTop = 96 + Gap;
        var half = (width - Gap) / 2;
        var leftPanel = Surface("Side",0,sidesTop,half,1,AndroidMarketPanelTheme.Surface,true);
        var rightPanel = Surface("Side",half + Gap,sidesTop,half,1,AndroidMarketPanelTheme.Surface,true);
        var columns = BoardColumns(snapshots);
        var left = Side("我方",HistoryViewText.Value(opening,"playerHero"),snapshots.GetProperty("playerHand"),snapshots.GetProperty("playerSkills"),0,sidesTop,half,columns);
        var right = Side("对方",HistoryViewText.Value(opening,"opponentHero"),snapshots.GetProperty("opponentHand"),snapshots.GetProperty("opponentSkills"),half + Gap,sidesTop,half,columns);
        var bottom = Math.Max(left,right);
        Resize(leftPanel,bottom - sidesTop); Resize(rightPanel,bottom - sidesTop);
        _height = bottom + 4; _content.sizeDelta = new(0,_height); _scroll.verticalNormalizedPosition = 1;
    }
    private float Side(string side, string hero, JsonElement hand, JsonElement skills, float x, float top, float width, int columns)
    {
        Text(side + " · " + hero,x + Pad,top + 10,width - 2 * Pad,48,30,AndroidHistoryPanel.Heading,true);
        var y = Group(hand,"物品",x + Pad,top + 62,width - 2 * Pad,false,columns);
        Surface("Rule",x + Pad,y,width - 2 * Pad,2,Rule,false);
        y = Group(skills,"技能",x + Pad,y + 10,width - 2 * Pad,true,SkillColumns);
        return y + Pad - 12;
    }
    // The span follows the current template because the native face is drawn from it; a card
    // recorded as Medium that is now Large would otherwise be squeezed into two sockets.
    private static int Span(JsonElement card)
    {
        if (Data.IsManagerCreated() && Guid.TryParse(HistoryViewText.Value(card,"templateId"),out var id)
            && Data.GetStatic().GetCardById(new Il2CppSystem.Guid(id.ToString()))?.TryCast<TCardBase>() is { } template)
            return NativeCardFit.SizeInSlots(template.Size);
        return HistoryViewText.Value(card,"size") switch { "Medium" => 2, "Large" => 3, _ => 1 };
    }
    // Items keep their saved socket order on one board row; a later card never overlaps an earlier one.
    private static List<(JsonElement Card, int Slot, int Size)> Place(JsonElement group, bool skills)
    {
        var saved = group.GetProperty("cards").EnumerateArray().ToList();
        if (skills) return saved.Select((card,index) => (card,index,1)).ToList();
        var placed = new List<(JsonElement Card, int Slot, int Size)>(); var next = 0;
        foreach (var card in saved.Select((card,index) => (card,index))
            .OrderBy(entry => HistoryFormationLayout.ItemSlot(HistoryViewText.Value(entry.card,"socket"),128 + entry.index)).Select(entry => entry.card))
        {
            var slot = Math.Max(next,HistoryFormationLayout.ItemSlot(HistoryViewText.Value(card,"socket"),next));
            var size = Span(card); placed.Add((card,slot,size)); next = slot + size;
        }
        return placed;
    }
    private static int BoardColumns(JsonElement snapshots) => Math.Max(SkillColumns, new[] { "playerHand", "opponentHand" }
        .Max(name => Place(snapshots.GetProperty(name),false).Select(card => card.Slot + card.Size).DefaultIfEmpty(0).Max()));
    private float Group(JsonElement group, string name, float x, float top, float width, bool skills, int columns)
    {
        Text(name + " · " + HistoryViewText.Group(group),x,top,width,36,23,AndroidHistoryPanel.Muted); top += 40;
        var placed = Place(group,skills);
        if (placed.Count == 0) return top + 4;
        // Item art uses the native unit aspect with a caption strip below; current templates can need more than ten sockets.
        var slotWidth = width / columns;
        var height = (skills ? slotWidth : slotWidth / NativeCardFit.UnitAspect) + Caption;
        var board = skills ? null : Surface("ItemBoard",x,top,width,height,Board,false);
        var rows = 1;
        foreach (var (card, slot, size) in placed)
        {
            var row = skills ? slot / columns : 0; var column = skills ? slot % columns : slot;
            var cardTop = top + row * (height + RowGap);
            var box = Area("SavedCard",x + column * slotWidth,cardTop,size * slotWidth,height);
            var button = NativeUi.Button(box,_font,"",Vector2.zero,Vector2.one,() => { if (PlayerPreferences.Data?.AlternativeInput != true) ShowDetail(card); });
            button.GetComponent<Image>().color = new Color(0,0,0,.01f);
            var placeholder = NativeUi.Label(box,_font,HistoryViewText.Value(card,"name") + "\n" + HistoryViewText.Value(card,"tier"),19);
            placeholder.richText = false; placeholder.overflowMode = TextOverflowModes.Ellipsis; placeholder.color = AndroidHistoryPanel.Muted;
            var caption = NativeUi.Label(NativeUi.Rect("SavedName",box,Vector2.zero,new(1,Caption / height)),_font,HistoryViewText.Value(card,"name"),20);
            caption.richText = false; caption.textWrappingMode = TextWrappingModes.NoWrap; caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.enableAutoSizing = true; caption.fontSizeMin = 14; caption.fontSizeMax = 20; caption.color = AndroidHistoryPanel.Body;
            caption.rectTransform.offsetMin = new(2,0); caption.rectTransform.offsetMax = new(-2,0);
            var preview = new Preview { Saved=card,Box=box,Placeholder=placeholder,Top=cardTop,Height=height,Skill=skills };
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            Event(trigger,EventTriggerType.PointerDown,data => { if (_pressed == null) { _pressed=preview; _pointer=data.pointerId; _pressedAt=Time.realtimeSinceStartup; } });
            Event(trigger,EventTriggerType.PointerUp,data => { if (_pointer == data.pointerId) ReleaseGesture(); });
            Event(trigger,EventTriggerType.PointerExit,data => { if (_pointer == data.pointerId && !_detail.gameObject.activeSelf) ReleaseGesture(); });
            Event(trigger,EventTriggerType.InitializePotentialDrag,data => _scroll.OnInitializePotentialDrag(data));
            Event(trigger,EventTriggerType.BeginDrag,data => { ReleaseGesture(); data.eligibleForClick=false; _scroll.OnBeginDrag(data); });
            Event(trigger,EventTriggerType.Drag,data => _scroll.OnDrag(data));
            Event(trigger,EventTriggerType.EndDrag,data => _scroll.OnEndDrag(data));
            _previews.Add(preview); rows = Math.Max(rows,row+1);
        }
        var total = rows * height + (rows - 1) * RowGap;
        if (board != null) Resize(board,total);
        return top + total + 12;
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
                    NativeCardFit.Attach(request.Card,request.NativeRoot!.GetComponent<RectTransform>());
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
                // Native card materials do not support RectMask2D. Keep the whole preview
                // inside the scrolling viewport; the saved name and touch target stay masked.
                if (request.Ready && request.NativeRoot != null)
                    request.NativeRoot.SetActive(request.Top >= visibleTop
                        && request.Top + request.Height - 8 <= visibleTop + _viewport.rect.height);

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
        var host = NativeUi.Rect("OwnedHistoricalPreview",request.Box,new(0,Caption / request.Height),Vector2.one);
        request.NativeRoot = host.gameObject; request.NativeRoot.transform.SetAsFirstSibling();
        request.Cancellation = new(); request.Creation = loader.GenerateUICard(host,request.Cancellation.Token,template);
    }
    private static void Fit(Preview request)
    {
        var host = request.NativeRoot!.GetComponent<RectTransform>();
        if (request.Skill) NativeCardFit.FitFrame(request.Card!,host);
        else NativeCardFit.FitItem(request.Card!,host);
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
