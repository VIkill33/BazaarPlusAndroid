using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BepInEx;
using Il2CppInterop.Runtime;
using TheBazaar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Owns its native previews. Hidden/off-screen rows retire their asynchronous card requests.
internal sealed class AndroidCustomBuildPanel
{
    private sealed record Row(RectTransform Root, AndroidBuild Preview, AndroidBuildBoard Board, int Index);
    private const float RowHeight = 250;
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
    private readonly CustomBuildStore _store = new(Path.Combine(Paths.ConfigPath, "BazaarPlusPlus.Android", "custom-builds.json"));
    private readonly List<Row> _rows = new();
    private readonly AndroidBuildBoard _draftBoard;
    private CustomBuildDraft? _draft;
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
            font, () => NativeUi.Text("My builds", "我的构筑"), 40);
        Button(_root, () => NativeUi.Text("Back", "返回"), .85f, .89f, .97f, .98f, Back);
        _status = NativeUi.Label(NativeUi.Rect("Status", _root, new Vector2(.03f, .01f), new Vector2(.97f, .08f)), font, string.Empty, 25);
        _status.enableAutoSizing = true;
        _status.fontSizeMin = 18;
        _status.fontSizeMax = 25;
        _list = NativeUi.Rect("List", _root, Vector2.zero, Vector2.one);
        _list.SetAsFirstSibling();
        var create = Button(_list, () => NativeUi.Text("New build", "新建构筑"), .64f, .89f, .82f, .98f, NewDraft);
        var list = Scroll(_list, new Vector2(.04f, .09f), new Vector2(.96f, .85f));
        (_listContent, _listScroll) = list;
        _editor = NativeUi.Rect("Editor", _root, Vector2.zero, Vector2.one);
        _editor.SetAsFirstSibling();
        _save = Button(_editor, () => NativeUi.Text("Save build", "收藏构筑"), .64f, .89f, .82f, .98f, Save);
        _add = Button(_editor, () => NativeUi.Text("Choose from collection", "从图鉴添加"), .70f, .76f, .94f, .84f, Pick);
        _capacity = NativeUi.Label(NativeUi.Rect("Capacity", _editor, new Vector2(.05f, .76f), new Vector2(.65f, .84f)), font, string.Empty, 30);
        var board = NativeUi.Rect("DraftBoard", _editor, new Vector2(.05f, .48f), new Vector2(.95f, .75f));
        _draftBoard = new AndroidBuildBoard(board, (_, _, _) => { });
        (_editContent, _editScroll) = Scroll(_editor, new Vector2(.05f, .10f), new Vector2(.95f, .46f));
        if (AndroidLauncherSettings.MarketPanels) AndroidMarketPanelTheme.Apply(_root, title);
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
        for (var index = 0; index < builds.Count; index++)
        {
            var build = builds[index];
            var row = RowRoot(_listContent, index, RowHeight);
            var missing = build.Items.Count(item => Template(item.TemplateId) == null);
            var unavailable = missing == 0 ? string.Empty
                : NativeUi.Text($" · {missing} unavailable", $" · {missing} 件物品不可用");
            var label = NativeUi.Label(NativeUi.Rect("Name", row, new Vector2(0, .77f), new Vector2(.68f, 1)), _font,
                NativeUi.Text($"Build {index + 1}", $"构筑 {index + 1}") + $" · {CurrentSlots(build.Items)}/10" +
                $"   <size=20>{build.CreatedAt.ToLocalTime():MM-dd HH:mm}</size>" + unavailable, 29);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 29;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var confirm = _deletePending == build.Id;
            Button(row, () => confirm ? NativeUi.Text("Confirm delete", "确认删除") : NativeUi.Text("Delete", "删除"),
                .77f, .78f, .99f, .98f, () => Delete(build.Id));
            if (confirm) Button(row, () => NativeUi.Text("Cancel", "取消"), .62f, .78f, .76f, .98f,
                () => { _deletePending = null; RenderList(); });
            var host = NativeUi.Rect("Board", row, new Vector2(.01f, .04f), new Vector2(.99f, .76f));
            var preview = Preview(build.Items);
            _rows.Add(new(row, preview, new AndroidBuildBoard(host, (_, _, _) => { }), index));
        }
        _listScroll.StopMovement();
        _listContent.anchoredPosition = position;
        if (!preservePosition) _listScroll.verticalNormalizedPosition = 1;
        if (_loaded) _status.text = builds.Count == 0
            ? NativeUi.Text("No saved builds. Create one to get started.", "还没有收藏构筑，点击「新建构筑」开始。")
            : NativeUi.Text($"{builds.Count} saved builds · Scroll to browse", $"已收藏 {builds.Count} 套构筑 · 上下滑动查看");
    }

    private void NewDraft()
    {
        if (!_loaded) { Show(); return; }
        ClearRows();
        _draft = new();
        _status.text = string.Empty;
        RenderDraft();
    }

    private void RenderDraft()
    {
        if (_draft == null) return;
        _list.gameObject.SetActive(false);
        _editor.gameObject.SetActive(true);
        ClearChildren(_editContent);
        _capacity.text = NativeUi.Text($"Used {_draft.UsedSlots}/10 slots", $"已用 {_draft.UsedSlots}/10 格");
        _add.interactable = _draft.UsedSlots < 10;
        _save.interactable = _draft.Items.Count > 0;
        _draftBoard.Show(Preview(_draft.Items));
        _editContent.sizeDelta = new Vector2(0, _draft.Items.Count * 76);
        for (var index = 0; index < _draft.Items.Count; index++)
        {
            var captured = index;
            var item = _draft.Items[index];
            var row = RowRoot(_editContent, index, 76);
            var label = NativeUi.Label(NativeUi.Rect("Name", row, Vector2.zero, new Vector2(.66f, 1)), _font,
                $"{index + 1}. {ItemName(item.TemplateId)} · {item.Size}" + NativeUi.Text(" slots", " 格"), 27);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 27;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.offsetMin = new Vector2(12, 0);
            label.rectTransform.offsetMax = new Vector2(-12, 0);
            var left = Button(row, () => "←", .67f, .12f, .74f, .88f, () => { _draft.Move(captured, -1); RenderDraft(); });
            var right = Button(row, () => "→", .75f, .12f, .82f, .88f, () => { _draft.Move(captured, 1); RenderDraft(); });
            left.interactable = index > 0;
            right.interactable = index < _draft.Items.Count - 1;
            Button(row, () => NativeUi.Text("Remove", "移除"), .84f, .12f, .99f, .88f,
                () => { _draft.RemoveAt(captured); _status.text = string.Empty; RenderDraft(); });
        }
    }

    private void Pick()
    {
        if (_draft == null) return;
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
            _store.Add(current);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.saved items={current.Length} slots={current.Sum(item => item.Size)} count={_store.Builds.Count}");
            _draft = null;
            RenderList(false);
        }
        catch (Exception error) { Failed("save", error); }
    }

    private void Delete(Guid id)
    {
        if (_deletePending != id) { _deletePending = id; RenderList(); return; }
        try
        {
            _store.Delete(id);
            _deletePending = null;
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.custom_build.deleted count={_store.Builds.Count}");
            RenderList();
        }
        catch (Exception error) { Failed("delete", error); }
    }

    private void Back()
    {
        if (_draft != null) { _draft = null; _status.text = string.Empty; RenderList(false); return; }
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
        if (_draft != null) { _draftBoard.Tick(); return; }
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
        IsOpen = false;
        _draft = null;
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

    private static RectTransform RowRoot(RectTransform content, int index, float height)
    {
        var row = NativeUi.Rect("Row", content, new Vector2(0, 1), Vector2.one,
            new Vector2(0, -(index + 1) * height + 6), new Vector2(0, -index * height - 6));
        row.gameObject.AddComponent<Image>().color = new Color(.10f, .07f, .12f, .85f);
        return row;
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

    private static AndroidBuild Preview(IReadOnlyList<CustomBuildItem> items)
    {
        var slot = 0;
        var cards = items.Select(item =>
        {
            var template = Template(item.TemplateId);
            var size = template == null ? item.Size : NativeCardFit.SizeInSlots(template.Size);
            var card = new AndroidBuildItem(item.TemplateId, slot, template == null ? 1 : (int)template.StartingTier + 1, null, size);
            slot += size;
            return card;
        }).ToArray();
        return new AndroidBuild(0, cards, 0, 0, null, 0);
    }
}
