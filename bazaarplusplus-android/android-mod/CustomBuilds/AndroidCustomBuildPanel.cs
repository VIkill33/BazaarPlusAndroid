using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BepInEx;
using Il2CppInterop.Runtime;
using TheBazaar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BazaarPlusPlus.Android;

// Owns its native previews. Hidden/off-screen rows retire their asynchronous card requests.
internal sealed class AndroidCustomBuildPanel
{
    private sealed record Row(RectTransform Root, AndroidBuild Preview, AndroidBuildBoard Board, int Index);
    private const float RowHeight = 250;
    private const float ItemRowHeight = 68;
    // One palette for the page: gold = heading/accent, cream = body, muted = meta, coral = destructive.
    private const string GoldHex = "#FFD68C";
    private const string MutedHex = "#B5A998";
    private const string CoralHex = "#FF8F80";
    private static readonly Color Gold = new(1f, .84f, .55f, 1f);
    private static readonly Color Muted = new(.71f, .66f, .60f, 1f);
    private static readonly Color Coral = new(1f, .56f, .50f, 1f);
    private static readonly Color PipEmpty = new(1f, 1f, 1f, .13f);
    private readonly Image[] _pips = new Image[10];
    private readonly RectTransform _root;
    private readonly RectTransform _list;
    private readonly RectTransform _editor;
    private readonly RectTransform _listContent;
    private readonly RectTransform _editContent;
    private readonly ScrollRect _listScroll;
    private readonly ScrollRect _editScroll;
    private readonly TMP_FontAsset _font;
    private readonly TextMeshProUGUI _status;
    private readonly TextMeshProUGUI _capacity;
    private readonly Button _save;
    private readonly Button _add;
    private readonly Action _back;
    private readonly Action<int, Action<CustomBuildItem?>> _pick;
    internal static readonly string StorePath = Path.Combine(Paths.ConfigPath, "BazaarPlusPlus.Android", "custom-builds.json");
    private readonly CustomBuildStore _store = new(StorePath);
    private readonly List<Row> _rows = new();
    private readonly AndroidBuildBoard _draftBoard;
    private readonly RectTransform _boardRoot;
    private readonly RectTransform _deleteZone;
    private readonly TextMeshProUGUI _deleteHint;
    private readonly CustomBuildDragState _drag = new();
    private readonly TextMeshProUGUI _summary;
    private PointerEventData? _dragPointer;
    private RectTransform? _dragHost;
    private RectTransform? _dropMark;
    private Vector2 _dragStart;
    private Vector2 _dragPress;
    private int _dragIndex = -1;
    private int _dropSlot = -1;
    private bool _overDelete;
    private CustomBuildDraft? _draft;
    private Guid? _editing;
    private Guid? _deletePending;
    private bool _loaded;
    private string? _language;
    internal bool IsOpen { get; private set; }

    internal AndroidCustomBuildPanel(Transform parent, TMP_FontAsset font, Action back,
        Action<int, Action<CustomBuildItem?>> pick)
    {
        _font = font;
        _back = back;
        _pick = pick;
        _root = NativeUi.Rect("BPP.CustomBuilds", parent, Vector2.zero, Vector2.one);
        _root.gameObject.AddComponent<Image>().color = new Color(.025f, .04f, .065f, .995f);
        var title = NativeUi.Label(NativeUi.Rect("Title", _root, new Vector2(.03f, .89f), new Vector2(.48f, .98f)),
            font, () => _draft == null ? NativeUi.Text("My builds", "我的构筑")
                : _editing != null ? NativeUi.Text("Edit build", "编辑构筑") : NativeUi.Text("New build", "新建构筑"), 46);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontStyle = FontStyles.Bold;
        title.color = Gold;
        Button(_root, () => NativeUi.Text("Back", "返回"), .85f, .89f, .97f, .98f, Back);
        _status = NativeUi.Label(NativeUi.Rect("Status", _root, new Vector2(.03f, .01f), new Vector2(.97f, .08f)), font, string.Empty, 24);
        _status.enableAutoSizing = true;
        _status.fontSizeMin = 18;
        _status.fontSizeMax = 24;
        _status.color = Muted;
        _list = NativeUi.Rect("List", _root, Vector2.zero, Vector2.one);
        _list.SetAsFirstSibling();
        var create = Button(_list, () => NativeUi.Text("New build", "新建构筑"), .64f, .89f, .82f, .98f, NewDraft);
        _summary = NativeUi.Label(NativeUi.Rect("Summary", _list, new Vector2(.23f, .89f), new Vector2(.62f, .97f)), font, string.Empty, 26);
        _summary.alignment = TextAlignmentOptions.BottomLeft;
        _summary.color = Muted;
        _summary.textWrappingMode = TextWrappingModes.NoWrap;
        var list = Scroll(_list, new Vector2(.04f, .09f), new Vector2(.96f, .85f));
        (_listContent, _listScroll) = list;
        _editor = NativeUi.Rect("Editor", _root, Vector2.zero, Vector2.one);
        _editor.SetAsFirstSibling();
        _save = Button(_editor, () => NativeUi.Text("Save build", "保存构筑"), .64f, .89f, .82f, .98f, Save);
        _add = Button(_editor, () => NativeUi.Text("Choose from collection", "从图鉴添加"), .70f, .76f, .94f, .84f, Pick);
        _capacity = NativeUi.Label(NativeUi.Rect("Capacity", _editor, new Vector2(.05f, .76f), new Vector2(.27f, .84f)), font, string.Empty, 28);
        _capacity.alignment = TextAlignmentOptions.MidlineLeft;
        _capacity.textWrappingMode = TextWrappingModes.NoWrap;
        var pips = NativeUi.Rect("CapacityPips", _editor, new Vector2(.28f, .79f), new Vector2(.66f, .81f));
        for (var slot = 0; slot < _pips.Length; slot++)
        {
            _pips[slot] = NativeUi.Rect("Pip", pips, new Vector2(slot / 10f, 0), new Vector2((slot + 1) / 10f, 1),
                new Vector2(3, 0), new Vector2(-3, 0)).gameObject.AddComponent<Image>();
            _pips[slot].raycastTarget = false;
        }
        _boardRoot = NativeUi.Rect("DraftBoard", _editor, new Vector2(.05f, .48f), new Vector2(.95f, .75f));
        _draftBoard = new AndroidBuildBoard(_boardRoot, (_, _, _) => { }, BindDrag, keepSlots: true);
        _deleteZone = NativeUi.Rect("DeleteDropZone", _root, new Vector2(.03f, .01f), new Vector2(.97f, .17f));
        var deleteBacking = _deleteZone.gameObject.AddComponent<Image>();
        deleteBacking.color = new Color(.65f, .06f, .08f, .97f);
        deleteBacking.raycastTarget = false;
        _deleteHint = NativeUi.Label(_deleteZone, font, string.Empty, 36);
        _deleteHint.alignment = TextAlignmentOptions.Center;
        _deleteHint.fontStyle = FontStyles.Bold;
        _deleteHint.raycastTarget = false;
        _deleteZone.gameObject.SetActive(false);
        (_editContent, _editScroll) = Scroll(_editor, new Vector2(.05f, .10f), new Vector2(.95f, .46f));
        if (AndroidLauncherSettings.MarketPanels) AndroidMarketPanelTheme.Apply(_root, title);
        AndroidPanelButtonSkin.Register(_save, selected: true);
        _root.gameObject.SetActive(false);
        create.interactable = true;
    }

    private Button Button(Transform parent, Func<string> caption, float x1, float y1, float x2, float y2, Action action)
    {
        var button = NativeUi.Button(parent, _font, caption, new Vector2(x1, y1), new Vector2(x2, y2), action);
        AndroidPanelButtonSkin.Register(button);
        return button;
    }

    private static (RectTransform, ScrollRect) Scroll(Transform parent, Vector2 min, Vector2 max)
    {
        var root = NativeUi.Rect("Scroll", parent, min, max);
        var viewport = NativeUi.Rect("Viewport", root, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
        var content = NativeUi.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        return (content, scroll);
    }

    internal void Show()
    {
        IsOpen = true;
        _root.gameObject.SetActive(true);
        _draft = null;
        _editing = null;
        _deletePending = null;
        _status.text = string.Empty;
        try { _store.Load(); _loaded = true; }
        catch (Exception error) { _loaded = false; Failed("load", error); }
        RenderList(false);
    }

    private void RenderList(bool preservePosition = true)
    {
        _draftBoard.Hide();
        _editor.gameObject.SetActive(false);
        _list.gameObject.SetActive(true);
        var position = preservePosition ? _listContent.anchoredPosition : Vector2.zero;
        ClearRows();
        var builds = _loaded ? _store.Builds : Array.Empty<CustomBuild>();
        _listContent.sizeDelta = new Vector2(0, builds.Count * RowHeight);
        if (_loaded && builds.Count == 0)
        {
            var empty = NativeUi.Label(NativeUi.Rect("Empty", _listContent, new Vector2(0, 1), Vector2.one,
                new Vector2(0, -360), Vector2.zero), _font,
                $"<b><color={GoldHex}>" + NativeUi.Text("No saved builds yet", "还没有收藏构筑") + "</color></b>\n" +
                $"<size=26><color={MutedHex}>" + NativeUi.Text("Tap \"New build\" at the top right to start.",
                    "点击右上角「新建构筑」开始。") + "</color></size>", 38);
            empty.alignment = TextAlignmentOptions.Center;
        }
        for (var index = 0; index < builds.Count; index++)
        {
            var build = builds[index];
            // A starred build wears its alert colour on the row edge, matching the gems drawn on offered cards.
            var starColor = build.Star is { } star ? AndroidFavoriteMarks.Colors[CustomBuildStore.StarColor(star)] : (Color?)null;
            var row = RowRoot(_listContent, index, RowHeight, starColor ?? new Color(1f, 1f, 1f, .22f));
            var missing = build.Items.Count(item => Template(item.TemplateId) == null);
            var unavailable = missing == 0 ? string.Empty
                : NativeUi.Text($" · {missing} unavailable", $" · {missing} 件物品不可用");
            var label = NativeUi.Label(NativeUi.Rect("Name", row, new Vector2(.012f, .77f), new Vector2(.585f, 1)), _font,
                $"<b><color={GoldHex}>" + NativeUi.Text($"Build {index + 1}", $"构筑 {index + 1}") + "</color></b>" +
                $"   <size=22><color={MutedHex}>" + NativeUi.Text($"{build.Items.Count} items", $"{build.Items.Count} 件") +
                $" · {CurrentSlots(build.Items)}/10" + NativeUi.Text(" slots", " 格") +
                $" · {build.CreatedAt.ToLocalTime():MM-dd HH:mm}</color>" +
                (unavailable.Length == 0 ? string.Empty : $"<color={CoralHex}>{unavailable}</color>") + "</size>", 32);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 32;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var confirm = _deletePending == build.Id;
            var delete = Button(row, () => confirm ? NativeUi.Text("Confirm delete", "确认删除") : NativeUi.Text("Delete", "删除"),
                confirm ? .79f : .86f, .79f, .99f, .97f, () => Delete(build.Id));
            Caption(delete, 24, Coral, confirm);
            if (confirm) Caption(Button(row, () => NativeUi.Text("Cancel", "取消"), .66f, .79f, .78f, .97f,
                () => { _deletePending = null; RenderList(); }), 24, Muted, false);
            else
            {
                Caption(Button(row, () => NativeUi.Text("Edit", "编辑"), .73f, .79f, .85f, .97f,
                    () => Edit(build)), 24, Gold, true);
                Caption(Button(row, () => starColor.HasValue ? NativeUi.Text("Alert on", "提醒中") : NativeUi.Text("Alert off", "开启提醒"),
                    .59f, .79f, .72f, .97f, () => ToggleStar(build)), 24, starColor ?? Muted, starColor.HasValue);
            }
            var host = NativeUi.Rect("Board", row, new Vector2(.01f, .04f), new Vector2(.99f, .76f));
            var preview = Preview(build.Items);
            _rows.Add(new(row, preview, new AndroidBuildBoard(host, (_, _, _) => { }, keepSlots: true), index));
        }
        _listScroll.StopMovement();
        _listContent.anchoredPosition = position;
        if (!preservePosition) _listScroll.verticalNormalizedPosition = 1;
        _summary.text = !_loaded || builds.Count == 0 ? string.Empty
            : NativeUi.Text($"{builds.Count} saved", $"共 {builds.Count} 套");
        if (_loaded) _status.text = builds.Count == 0 ? string.Empty
            : NativeUi.Text("Turn an alert on to flag that build's cards in shops and rewards with its colour",
                "点「开启提醒」后，商店与奖励里属于该构筑的牌会带同色角标");
    }

    private void NewDraft()
    {
        if (!_loaded) { Show(); return; }
        ClearRows();
        _draft = new();
        _editing = null;
        _status.text = string.Empty;
        RenderDraft();
    }

    private void Edit(CustomBuild build)
    {
        ClearRows();
        // Spans follow the current templates, like the board that draws them; saved slots are kept where they still fit.
        _draft = new(build.Items.Select(item => Template(item.TemplateId) is { } template
            ? item with { Size = NativeCardFit.SizeInSlots(template.Size) } : item));
        _editing = build.Id;
        _deletePending = null;
        _status.text = string.Empty;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.edit_started items={build.Items.Count}");
        RenderDraft();
    }

    private void RenderDraft()
    {
        if (_draft == null) return;
        CancelDrag();
        _list.gameObject.SetActive(false);
        _editor.gameObject.SetActive(true);
        ClearChildren(_editContent);
        var used = $"<size=38><b><color={GoldHex}>{_draft.UsedSlots}</color></b></size><color={MutedHex}> / 10</color>";
        _capacity.text = NativeUi.Text($"Slots {used}", $"已用 {used} 格");
        for (var slot = 0; slot < _pips.Length; slot++) _pips[slot].color = slot < _draft.UsedSlots ? Gold : PipEmpty;
        _add.interactable = _draft.UsedSlots < 10;
        _save.interactable = _draft.Items.Count > 0;
        _draftBoard.Show(Preview(_draft.Items));
        if (string.IsNullOrEmpty(_status.text))
            _status.text = NativeUi.Text("Hold an item, then drag it to any free slots · Drag down to delete", "按住物品拖到任意空格 · 拖到下方删除");
        _editContent.sizeDelta = new Vector2(0, _draft.Items.Count * ItemRowHeight);
        for (var index = 0; index < _draft.Items.Count; index++)
        {
            var item = _draft.Items[index];
            var row = RowRoot(_editContent, index, ItemRowHeight, null);
            var label = NativeUi.Label(NativeUi.Rect("Name", row, Vector2.zero, new Vector2(.84f, 1)), _font,
                $"<b><color={GoldHex}>{index + 1}</color></b>   {ItemName(item.TemplateId)}", 27);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 27;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.offsetMin = new Vector2(16, 0);
            label.rectTransform.offsetMax = new Vector2(-12, 0);
            var size = NativeUi.Label(NativeUi.Rect("Size", row, new Vector2(.84f, 0), Vector2.one), _font,
                item.Size + NativeUi.Text(" slots", " 格"), 23);
            size.alignment = TextAlignmentOptions.MidlineRight;
            size.color = Muted;
            size.textWrappingMode = TextWrappingModes.NoWrap;
            size.rectTransform.offsetMin = new Vector2(0, 0);
            size.rectTransform.offsetMax = new Vector2(-16, 0);
        }
    }

    private void BindDrag(RectTransform host, int index)
    {
        var trigger = host.gameObject.AddComponent<EventTrigger>();
        Bind(EventTriggerType.PointerDown, data =>
        {
            if (!_drag.Press(data.pointerId, Time.realtimeSinceStartup, data.position.x, data.position.y)) return;
            _dragPointer = data;
            _dragHost = host;
            _dragIndex = index;
            _dragStart = host.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(host.parent.Cast<RectTransform>(),
                data.position, data.pressEventCamera, out _dragPress);
        });
        Bind(EventTriggerType.Drag, UpdateDrag);
        Bind(EventTriggerType.PointerUp, data =>
        {
            if (!_drag.Owns(data.pointerId)) return;
            var touchId = data.TryCast<ExtendedPointerEventData>()?.touchId;
            var screen = Touchscreen.current;
            if (screen != null && touchId.HasValue)
            {
                for (var i = 0; i < screen.touches.Count; i++)
                {
                    var touch = screen.touches[i];
                    if (touch.touchId.ReadValue() == touchId.Value && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                    { CancelDrag(); return; }
                }
            }
            UpdateDrag(data);
            var commit = _drag.Release(data.pointerId);
            var remove = _overDelete;
            var target = _dropSlot;
            CancelDrag();
            if (!commit || _draft == null) return;
            data.eligibleForClick = false;
            if (remove) _draft.RemoveAt(index);
            else if (target >= 0) _draft.MoveTo(index, target);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.drop from={index} slot={target} removed={remove} count={_draft.Items.Count}");
            _status.text = string.Empty;
            RenderDraft();
        });
        void Bind(EventTriggerType type, Action<PointerEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(
                (BaseEventData data) => callback(data.Cast<PointerEventData>())));
            trigger.triggers.Add(entry);
        }
    }

    private void UpdateDrag(PointerEventData data)
    {
        if (_dragHost == null || !_drag.Owns(data.pointerId)) return;
        if (!_drag.Update(data.pointerId, Time.realtimeSinceStartup, data.position.x, data.position.y)) return;
        data.eligibleForClick = false;
        _deleteZone.gameObject.SetActive(true);
        _deleteZone.SetAsLastSibling();
        _status.transform.parent.Cast<RectTransform>().anchorMin = new Vector2(.03f, .845f);
        _status.transform.parent.Cast<RectTransform>().anchorMax = new Vector2(.97f, .89f);
        _overDelete = RectTransformUtility.RectangleContainsScreenPoint(_deleteZone, data.position, data.pressEventCamera);
        _deleteHint.text = _overDelete ? NativeUi.Text("Release to delete", "松手删除")
            : NativeUi.Text("Drag here to delete", "拖到此处删除");
        _deleteZone.GetComponent<Image>().color = _overDelete
            ? new Color(.9f, .08f, .1f, 1) : new Color(.65f, .06f, .08f, .97f);
        var row = _dragHost.parent.Cast<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(row, data.position, data.pressEventCamera, out var local);
        _dropSlot = -1;
        var item = _draft != null && _dragIndex >= 0 && _dragIndex < _draft.Items.Count ? _draft.Items[_dragIndex] : null;
        if (item != null && !_overDelete
            && RectTransformUtility.RectangleContainsScreenPoint(_boardRoot, data.position, data.pressEventCamera))
        {
            // The card snaps to the slots under its dragged left edge. Hosts are inset 1 unit on each side.
            var unit = (_dragHost.rect.width + 2) / item.Size;
            _dropSlot = Math.Clamp(item.Slot!.Value + Mathf.RoundToInt((local.x - _dragPress.x) / unit), 0, 10 - item.Size);
        }
        if (_dropSlot < 0) { if (_dropMark != null) _dropMark.gameObject.SetActive(false); }
        else
        {
            if (_dropMark == null)
            {
                _dropMark = NativeUi.Rect("DropMark", row, Vector2.zero, Vector2.one);
                var mark = _dropMark.gameObject.AddComponent<Image>();
                mark.color = new Color(Gold.r, Gold.g, Gold.b, .30f);
                mark.raycastTarget = false;
                _dropMark.SetAsFirstSibling();
            }
            // shortcut: assumes the ten-unit row; a board widened by since-grown cards shows the mark offset, upgrade if that case matters.
            _dropMark.anchorMin = new Vector2(_dropSlot / 10f, 0);
            _dropMark.anchorMax = new Vector2((_dropSlot + item!.Size) / 10f, 1);
            _dropMark.gameObject.SetActive(true);
        }
        _dragHost.anchoredPosition = _dragStart + local - _dragPress;
        _dragHost.SetAsLastSibling();
        _status.text = _overDelete ? NativeUi.Text("Release to delete", "松手删除")
            : _dropSlot >= 0 ? NativeUi.Text($"Release to place at slot {_dropSlot + 1}", $"松手放到第 {_dropSlot + 1} 格")
            : NativeUi.Text("Release to cancel", "松手取消移动");
    }

    private void CancelDrag()
    {
        if (_dragHost != null) _dragHost.anchoredPosition = _dragStart;
        _drag.Cancel();
        if (_dragPointer != null)
            _status.text = NativeUi.Text("Hold an item, then drag it to any free slots · Drag down to delete", "按住物品拖到任意空格 · 拖到下方删除");
        if (_dropMark != null) UnityEngine.Object.Destroy(_dropMark.gameObject);
        _dropMark = null;
        _dragPointer = null;
        _dragHost = null;
        _dragIndex = -1;
        _dropSlot = -1;
        _overDelete = false;
        _deleteZone.gameObject.SetActive(false);
        _status.transform.parent.Cast<RectTransform>().anchorMin = new Vector2(.03f, .01f);
        _status.transform.parent.Cast<RectTransform>().anchorMax = new Vector2(.97f, .08f);
    }

    private void Pick()
    {
        if (_draft == null) return;
        CancelDrag();
        _draftBoard.Hide();
        _root.gameObject.SetActive(false);
        _pick(10 - _draft.UsedSlots, item =>
        {
            if (!IsOpen || _draft == null) return;
            _root.gameObject.SetActive(true);
            if (item != null && !_draft.TryAdd(item.TemplateId, item.Size))
                _status.text = NativeUi.Text("This item does not fit.", "剩余格数不足，无法添加此物品。");
            RenderDraft();
        });
    }

    private void Save()
    {
        CancelDrag();
        if (_draft == null || _draft.Items.Count == 0) return;
        try
        {
            // Templates can change between game versions; never persist an over-capacity board.
            var current = _draft.Items.Select(item =>
            {
                var template = Template(item.TemplateId);
                if (template == null || template.Type != ECardType.Item) throw new InvalidDataException("Missing item template.");
                return item with { Size = NativeCardFit.SizeInSlots(template.Size) };
            }).ToArray();
            var edited = _editing is { } id && _store.Update(id, current);
            if (!edited) _store.Add(current);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.saved edited={edited} items={current.Length} slots={current.Sum(item => item.Size)} count={_store.Builds.Count}");
            _draft = null;
            _editing = null;
            AndroidFavoriteMarks.Invalidate();
            RenderList(edited);
        }
        catch (Exception error) { Failed("save", error); }
    }

    private void ToggleStar(CustomBuild build)
    {
        try
        {
            _store.SetStar(build.Id, build.Star == null);
            AndroidFavoriteMarks.Invalidate();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.star on={build.Star == null} starred={_store.Builds.Count(saved => saved.Star != null)}");
            RenderList();
        }
        catch (Exception error) { Failed("save", error); }
    }

    private static CustomBuildItem[] RecommendedItems(AndroidBuild build) =>
        build.Items.OrderBy(item => item.Slot).Select(item => new CustomBuildItem(item.TemplateId,
            Template(item.TemplateId) is { } template ? NativeCardFit.SizeInSlots(template.Size) : item.Size)).ToArray();

    internal bool IsFavorite(AndroidBuild build) => _store.FindRecommended(RecommendedItems(build)) != null;

    internal void ReloadFavorites()
    {
        try { _store.Load(); }
        catch (Exception error) { Failed("load", error); }
    }

    internal bool Favorite(AndroidBuild build)
    {
        try
        {
            _store.Load();
            var removed = _store.ToggleRecommended(RecommendedItems(build));
            AndroidFavoriteMarks.Invalidate();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.favorite_toggled removed={removed} items={build.Items.Count} count={_store.Builds.Count}");
            return true;
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.custom_build.favorite_failed type={error.GetType().Name}");
            return false;
        }
    }

    internal bool ClearFavorites()
    {
        try
        {
            _store.Load();
            var count = _store.Builds.Count;
            _store.Clear();
            _deletePending = null;
            AndroidFavoriteMarks.Invalidate();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.cleared count={count}");
            return true;
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.custom_build.clear_failed type={error.GetType().Name}");
            return false;
        }
    }

    private void Delete(Guid id)
    {
        if (_deletePending != id) { _deletePending = id; RenderList(); return; }
        try
        {
            _store.Delete(id);
            _deletePending = null;
            AndroidFavoriteMarks.Invalidate();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.deleted count={_store.Builds.Count}");
            RenderList();
        }
        catch (Exception error) { Failed("delete", error); }
    }

    private void Back()
    {
        CancelDrag();
        if (_draft != null)
        {
            var edited = _editing != null;
            _draft = null;
            _editing = null;
            _status.text = string.Empty;
            RenderList(edited);
            return;
        }
        Close();
        _back();
    }

    internal void Tick()
    {
        if (!IsOpen || !_root.gameObject.activeSelf) return;
        var language = PlayerPreferences.Data?.LanguageCode;
        if (_language != language)
        {
            _language = language;
            if (_draft != null) RenderDraft(); else RenderList();
        }
        if (_draft != null)
        {
            _draftBoard.Tick();
            if (!Application.isFocused) CancelDrag();
            else if (_dragPointer != null) UpdateDrag(_dragPointer);
            return;
        }
        var top = _listContent.anchoredPosition.y;
        var viewportHeight = _listScroll.viewport.rect.height;
        foreach (var row in _rows)
        {
            if (CustomBuildViewport.IsFullyVisible(row.Index * RowHeight, RowHeight, top, viewportHeight))
            { row.Board.Show(row.Preview); row.Board.Tick(); }
            else row.Board.Hide();
        }
    }

    internal void Close()
    {
        CancelDrag();
        IsOpen = false;
        _draft = null;
        _editing = null;
        _draftBoard.Hide();
        ClearRows();
        ClearChildren(_editContent);
        _root.gameObject.SetActive(false);
    }

    private void Failed(string operation, Exception error)
    {
        _status.text = operation == "load"
            ? NativeUi.Text("Could not read saved builds. Reopen to retry; existing data is preserved.", "收藏读取失败，请返回重试；原文件已保留。")
            : NativeUi.Text("Could not save changes. Your build is still here; retry.", "保存失败，当前内容已保留，请重试。");
        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.custom_build.{operation}_failed type={error.GetType().Name}");
    }

    // Build cards carry a coloured edge; item rows are a quieter zebra list.
    private static RectTransform RowRoot(RectTransform content, int index, float height, Color? edgeColor)
    {
        var accent = edgeColor.HasValue;
        var gap = accent ? 6 : 2;
        var row = NativeUi.Rect("Row", content, new Vector2(0, 1), Vector2.one,
            new Vector2(0, -(index + 1) * height + gap), new Vector2(0, -index * height - gap));
        row.gameObject.AddComponent<Image>().color = accent ? new Color(.12f, .08f, .15f, .92f)
            : new Color(1f, 1f, 1f, index % 2 == 0 ? .07f : .03f);
        if (!accent) return row;
        var edge = NativeUi.Rect("Accent", row, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(5, 0))
            .gameObject.AddComponent<Image>();
        edge.color = edgeColor!.Value;
        edge.raycastTarget = false;
        return row;
    }

    private static void Caption(Button button, float size, Color color, bool bold)
    {
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = color;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
    }

    private void ClearRows()
    {
        foreach (var row in _rows) row.Board.Hide();
        _rows.Clear();
        ClearChildren(_listContent);
    }

    private static void ClearChildren(RectTransform content)
    {
        for (var index = content.childCount - 1; index >= 0; index--)
        {
            var child = content.GetChild(index);
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static TCardBase? Template(Guid id) => Data.GetStatic()?.GetCardById(Il2CppSystem.Guid.Parse(id.ToString()))?.TryCast<TCardBase>();

    private static string ItemName(Guid id)
    {
        var title = Template(id)?.Localization?.Title;
        return title == null ? NativeUi.Text("Unavailable item", "物品暂不可用")
            : NativeUi.TitleText(TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(title));
    }

    private static int CurrentSlots(IReadOnlyList<CustomBuildItem> items) => items.Sum(item =>
        Template(item.TemplateId) is { } template ? NativeCardFit.SizeInSlots(template.Size) : item.Size);

    internal static AndroidBuild Preview(IReadOnlyList<CustomBuildItem> items)
    {
        var next = 0;
        var cards = items.Select(item =>
        {
            var template = Template(item.TemplateId);
            var size = template == null ? item.Size : NativeCardFit.SizeInSlots(template.Size);
            var slot = Math.Max(item.Slot ?? next, next);
            next = slot + size;
            return new AndroidBuildItem(item.TemplateId, slot, template == null ? 1 : (int)template.StartingTier + 1, null, size);
        }).ToArray();
        return new AndroidBuild(0, cards, 0, 0, null, 0);
    }
}
