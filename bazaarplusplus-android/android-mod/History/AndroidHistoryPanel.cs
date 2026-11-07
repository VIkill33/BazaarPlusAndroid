using System.Text.Json;
using BazaarGameShared.Domain.Core.Types;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidHistoryPanel(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static bool AnyOpen { get; private set; }
    private enum PageKind { Runs, Battles, Formation }
    private sealed class Page
    {
        internal JsonElement? Data;
        internal readonly List<JsonElement?> Tokens = [null];
        internal float Scroll = 1;
    }
    private sealed record Query(Task<JsonElement> Task, int Version, string Scope, Action<JsonElement> Apply);
    private readonly Page _runs = new(), _battles = new();
    private EHero? _hero;
    private string? _mode;
    private static readonly string?[] Modes = [null, "Ranked", "Unranked"];
    private AndroidChoicePopup? _choices;
    private Button? _heroButton, _modeButton;
    private readonly List<Query> _queries = new();
    private sealed record Row(Button Button, Image Portrait, TextMeshProUGUI HeroFallback,
        TextMeshProUGUI Primary, TextMeshProUGUI Secondary, Image Trophy, TextMeshProUGUI Score, TextMeshProUGUI Detail);
    private readonly List<Row> _rows = new();
    private readonly AndroidUiArt _art = new();
    private Row? _runHeader;
    internal static readonly Color Body = new(.953f,.906f,.80f);
    internal static readonly Color Heading = new(.949f,.769f,.42f);
    internal static readonly Color Muted = new(.722f,.675f,.58f);
    internal static readonly Color Frame = new(.91f,.60f,.28f,.45f);
    private static readonly Color Amber = new(1f,.74f,.40f);
    internal static readonly Color Success = new(.44f,.84f,.67f);
    internal static readonly Color Loss = new(.95f,.40f,.36f);
    private static readonly Color RowSurface = new(.157f,.110f,.141f,.96f);
    private const float RowHeight = 124;
    private readonly List<JsonElement> _items = new();
    private GameObject? _canvas;
    private RectTransform? _panel, _listRoot, _content;
    private ScrollRect? _scroll;
    private Button? _entry, _previous, _next, _retryButton;
    private TextMeshProUGUI? _title, _summary, _status, _pageLabel;
    private AndroidHistoryFormation? _formation;
    private TMP_FontAsset? _font;
    private PageKind _kind;
    private JsonElement? _run, _battle;
    private int _version, _scene = -1;
    private bool _open, _loading;
    private string? _scope;
    private Action? _retry;

    private Page CurrentPage => _kind == PageKind.Runs ? _runs : _battles;
    private static HistoryScope? Scope => AndroidRunObserver.Instance?.Session.Scope;
    private static string? AccountId => HistorySessionCore.NormalizeId(ClientCache.Profile?.HasData == true ? ClientCache.Profile.Value?.AccountId.ToString() : null);
    private static string ScopeKey => Scope is { } scope ? scope.SessionId + ":" + AccountId : "";
    private static bool Combat => AndroidRunObserver.CurrentPhase() is HistoryPhase.Combat or HistoryPhase.PvpCombat;

    public void Update()
    {
        try
        {
            DrainQueries();
            if (_canvas == null)
            {
                if (string.IsNullOrWhiteSpace(PlayerPreferences.Data?.LanguageCode)) return;
                var font = NotoFontFallbackRuntime._loadedSansPrimary;
                if (font == null) return;
                Build(font);
            }
            var scene = SceneManager.GetActiveScene().handle;
            if (_open && (_scope != ScopeKey || Combat || (_scene != -1 && _scene != scene))) SetOpen(false);
            _scene = scene;
            if (_entry != null) _entry.gameObject.SetActive(!_open && !Combat && !CollectionController.AnyOpen && !AndroidBuildPanelController.AnyOpen
                && AndroidLauncherSettings.Shown(AndroidDockButton.History));
            if (_entry != null) _entry.interactable = Scope != null;
            if (!_open) return;
            AndroidPanelButtonSkin.Tick();
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Back();
            _formation?.Tick();
            _art.Tick();
            _choices?.Tick();
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=history.panel.failed exception={error}");
            SetOpen(false);
        }
    }

    private TextMeshProUGUI Label(Transform parent, string value, float size = 26)
    {
        var label = NativeUi.Label(parent, _font!, value, size); label.richText = false; return label;
    }
    private TextMeshProUGUI Line(Transform parent, Vector2 min, Vector2 max, float size, Color color, Vector2 offsetMin = default)
    {
        var label = Label(NativeUi.Rect("Line", parent, min, max, offsetMin), "", size);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        // Ellipsis also drops a line that is taller than its rect, so keep the full row half for the text.
        label.rectTransform.offsetMin = new Vector2(8, 0); label.rectTransform.offsetMax = new Vector2(-8, 0);
        return label;
    }
    internal static void AddFrame(Component target, Color color)
    {
        var outline = target.gameObject.GetComponent<Outline>();
        if (outline == null) outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = color; outline.effectDistance = new Vector2(1.5f, -1.5f); outline.useGraphicAlpha = false;
    }
    private Button Button(Transform parent, string value, Vector2 min, Vector2 max, Action action)
    {
        var button = NativeUi.Button(parent, _font!, value, min, max, action);
        button.GetComponentInChildren<TextMeshProUGUI>().richText = false; return button;
    }
    private void Build(TMP_FontAsset font)
    {
        _font = font;
        _canvas = new GameObject("BPP.HistoryCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        DontDestroyOnLoad(_canvas);
        var canvas = _canvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        var scaler = _canvas.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        _canvas.AddComponent<GraphicRaycaster>();
        _entry = Button(_canvas.transform, "对局历史", Vector2.zero, Vector2.zero, () => SetOpen(true));
        AndroidFloatingDock.Register(_entry, font, 4, AndroidDockIcon.History, () => NativeUi.Text("History", "历史"),
            () => NativeUi.Text("History", "对局历史"));
        _panel = NativeUi.Rect("BPP.History", _canvas.transform, Vector2.zero, Vector2.one);
        _panel.gameObject.AddComponent<Image>().color = new Color(.106f, .071f, .078f, 1);
        var back = Button(_panel, "返回", new(.02f,.91f), new(.12f,.98f), Back);
        var refresh = Button(_panel, "刷新", new(.74f,.91f), new(.86f,.98f), Refresh);
        var close = Button(_panel, "关闭", new(.88f,.91f), new(.98f,.98f), () => SetOpen(false));
        _title = Label(NativeUi.Rect("Title", _panel, new(.14f,.91f), new(.72f,.98f)), "对局历史", 42);
        _title.fontStyle = FontStyles.Bold; _title.color = Heading;
        _choices = new AndroidChoicePopup(_panel, font);
        _modeButton = Button(_panel, "模式：全部", new(.50f,.79f), new(.72f,.87f), ChooseMode);
        _heroButton = Button(_panel, "英雄：全部", new(.74f,.79f), new(.97f,.87f), ChooseHero);
        _summary = Label(NativeUi.Rect("Summary", _panel, new(.03f,.785f), new(.49f,.875f)), "", 26);
        _summary.alignment = TextAlignmentOptions.MidlineLeft; _summary.color = Muted;
        _status = Label(NativeUi.Rect("Status", _panel, new(.03f,.005f), new(.85f,.058f)), "", 24);
        _status.color = Muted;
        _runHeader = CreateRow(_panel, () => { });
        var headerRect = _runHeader.Button.GetComponent<RectTransform>();
        headerRect.anchorMin = new(.03f,.73f); headerRect.anchorMax = new(.97f,.875f);
        headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;
        _runHeader.Button.interactable = false;
        _listRoot = NativeUi.Rect("List", _panel, new(.03f,.16f), new(.97f,.77f));
        var viewport = NativeUi.Rect("Viewport", _listRoot, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,.02f);
        _content = NativeUi.Rect("Rows", viewport, new(0,1), Vector2.one); _content.pivot = new(.5f,1);
        _scroll = _listRoot.gameObject.AddComponent<ScrollRect>(); _scroll.viewport = viewport; _scroll.content = _content;
        _scroll.horizontal = false; _scroll.movementType = ScrollRect.MovementType.Clamped;
        for (var index = 0; index < 20; index++)
        {
            var slot = index;
            var row = CreateRow(_content, () => Select(slot));
            var rect = row.Button.GetComponent<RectTransform>(); rect.pivot = new(.5f,1);
            rect.offsetMin = new(0,-index * RowHeight - RowHeight + 8); rect.offsetMax = new(0,-index * RowHeight);
            _rows.Add(row);
        }
        _previous = Button(_panel, "上一页", new(.03f,.065f), new(.20f,.14f), () => Move(false));
        _next = Button(_panel, "下一页", new(.80f,.065f), new(.97f,.14f), () => Move(true));
        _pageLabel = Label(NativeUi.Rect("Page", _panel, new(.22f,.065f), new(.78f,.14f)), "", 26);
        _pageLabel.color = Muted;
        _retryButton = Button(_panel, "重试", new(.87f,.008f), new(.97f,.056f), () => _retry?.Invoke());
        // Dividers must clear the run header below the title bar and the status line below paging.
        if (AndroidLauncherSettings.MarketPanels) AndroidMarketPanelTheme.Apply(_panel, _title, .903f, .06f);
        foreach (var button in new[] { back, refresh, close, _modeButton, _heroButton, _previous, _next, _retryButton })
            AndroidPanelButtonSkin.Register(button);
        foreach (var row in _rows.Append(_runHeader!))
        {
            row.Button.GetComponent<Image>().color = AndroidLauncherSettings.MarketPanels ? AndroidMarketPanelTheme.Surface : RowSurface;
            AddFrame(row.Button, Frame);
        }
        _formation = new AndroidHistoryFormation(_panel, font);
        SetOpen(false);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=history.panel.ready");
    }

    private Row CreateRow(Transform parent, Action select)
    {
        var button = Button(parent, "", new(0,1), new(1,1), select);
        button.GetComponent<Image>().color = new Color(.157f,.110f,.141f,1);
        // The old single text child is replaced by independently aligned fields.
        button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
        var portraitRect = NativeUi.Rect("Hero", button.transform, new(0,.5f), new(0,.5f), new(16,-46), new(108,46));
        var portrait = portraitRect.gameObject.AddComponent<Image>(); portrait.preserveAspect = true; portrait.raycastTarget = false;
        var fallback = Label(portraitRect, "英雄未记录", 20); fallback.enableWordWrapping = true; fallback.color = Muted;
        var primary = Line(button.transform, new(.065f,.50f), new(.57f,.94f), 29, Body, new(18,0));
        var secondary = Line(button.transform, new(.065f,.06f), new(.57f,.50f), 24, Muted, new(18,0));
        var trophy = NativeUi.Rect("Trophy", button.transform, new(.61f,.5f), new(.61f,.5f), new(-40,-40), new(40,40))
            .gameObject.AddComponent<Image>(); trophy.preserveAspect = true; trophy.raycastTarget = false;
        var score = Line(button.transform, new(.65f,.42f), new(.96f,.99f), 40, Amber);
        score.fontStyle = FontStyles.Bold;
        var detail = Line(button.transform, new(.65f,.04f), new(.96f,.44f), 24, Muted);
        return new(button, portrait, fallback, primary, secondary, trophy, score, detail);
    }
    private void FillRun(Row row, JsonElement run)
    {
        var hero = HistoryViewText.Value(run,"hero");
        row.HeroFallback.text = hero == "未记录" ? "英雄未记录" : hero;
        _art.Bind(row.Portrait, AndroidUiArt.Hero(hero), row.HeroFallback);
        _art.Bind(row.Trophy, HistoryViewText.Badge(run));
        row.Primary.text = HistoryViewText.RunWhen(run);
        row.Secondary.text = HistoryViewText.RunProgress(run);
        row.Score.text = HistoryViewText.Score(run); row.Score.color = Amber;
        var losses = HistoryViewText.HasProgress(run) ? $"{HistoryViewText.Number(run,"losses")} 负 · " : "";
        row.Detail.text = losses + HistoryViewText.Tier(run);
    }
    private void FillBattle(Row row, JsonElement battle)
    {
        var hero = HistoryViewText.Value(HistoryViewText.Opponent(battle),"opponentHero");
        row.HeroFallback.text = hero == "未记录" ? "英雄未记录" : hero;
        _art.Bind(row.Portrait, AndroidUiArt.Hero(hero), row.HeroFallback);
        _art.Bind(row.Trophy, null);
        row.Primary.text = HistoryViewText.OpponentName(battle);
        row.Secondary.text = HistoryViewText.BattleWhen(battle);
        row.Score.text = HistoryViewText.Result(battle);
        row.Score.color = HistoryViewText.Value(battle,"outcome") switch { "win" => Success, "loss" => Loss, _ => Muted };
        row.Detail.text = "查看阵容 ›";
    }

    private void ChooseHero()
    {
        var heroes = Enum.GetValues<EHero>().Where(hero => hero != EHero.Common).ToArray();
        _choices?.ShowHeroes(heroes, _hero, hero =>
        {
            if (_hero == hero) return;
            _hero = hero; ReloadRuns();
        });
    }
    private static string ModeName(string? mode) => mode switch
    {
        "Ranked" => NativeUi.Text("Ranked", "排位"),
        "Unranked" => NativeUi.Text("Normal", "普通（休闲）"),
        _ => NativeUi.Text("All modes", "全部模式"),
    };
    private void ChooseMode()
    {
        var options = Modes.Select(mode => (ModeName(mode), (Action)(() =>
        {
            if (_mode == mode) return;
            _mode = mode; ReloadRuns();
        }))).ToArray();
        _choices?.Show(NativeUi.Text("Mode", "模式"), options, Array.IndexOf(Modes, _mode));
    }
    private void ReloadRuns()
    {
        ++_version; _loading = false; _retry = null;
        _runs.Data = null; _runs.Tokens.Clear(); _runs.Tokens.Add(null); _runs.Scroll = 1;
        Render(); Refresh();
    }

    private void SetOpen(bool open)
    {
        if (open && (Scope == null || Combat || CollectionController.AnyOpen || AndroidBuildPanelController.AnyOpen)) return;
        if (open && _scope != ScopeKey)
        {
            _runs.Data = _battles.Data = null; _runs.Tokens.Clear(); _runs.Tokens.Add(null); _battles.Tokens.Clear(); _battles.Tokens.Add(null);
            _hero = null; _mode = null;
            _runs.Scroll = _battles.Scroll = 1; _run = _battle = null; _kind = PageKind.Runs;
        }
        _open = open; AnyOpen = open; ++_version; _loading = false; _retry = null;
        _panel?.gameObject.SetActive(open);
        _formation?.Hide();
        _choices?.Close();
        _art.Clear();
        if (!open) return;
        _scope = ScopeKey;
        Render(); Refresh();
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=history.panel.open");
    }
    private void Remember()
    {
        // An empty page has no meaningful offset; saving it would reopen loaded rows at the bottom.
        if (_kind != PageKind.Formation && _scroll != null && _items.Count > 0) CurrentPage.Scroll = _scroll.verticalNormalizedPosition;
    }
    private void Back()
    {
        if (_choices?.IsOpen == true) { _choices.Close(); return; }
        if (_formation?.CloseDetail() == true) return;
        Remember(); ++_version; _loading = false; _retry = null;
        if (_kind == PageKind.Runs) { SetOpen(false); return; }
        _formation?.Hide(); _kind = _kind == PageKind.Formation ? PageKind.Battles : PageKind.Runs; Render();
    }
    private void Refresh()
    {
        Remember();
        if (_kind != PageKind.Runs && _run.HasValue)
        {
            var metadata = Request("run"); metadata["runId"] = HistoryViewText.Value(_run.Value,"id");
            QueryRead(metadata, result => { _run = result.GetProperty("run"); LoadCurrent(); });
        }
        else LoadCurrent();
    }
    private void LoadCurrent()
    {
        if (_kind == PageKind.Formation && _run.HasValue && _battle.HasValue)
        {
            var request = Request("battle"); request["runId"] = HistoryViewText.Value(_run.Value,"id"); request["battleId"] = HistoryViewText.Value(_battle.Value,"id");
            QueryRead(request, result => { _battle = result.GetProperty("battle"); Render(); });
        }
        else LoadPage(CurrentPage, CurrentPage.Tokens.ToArray());
    }
    private void Move(bool forward)
    {
        if (_loading || _kind == PageKind.Formation) return;
        var page = CurrentPage; var tokens = page.Tokens.ToList();
        if (forward)
        {
            if (!page.Data.HasValue || !page.Data.Value.GetProperty("hasMore").GetBoolean()) return;
            tokens.Add(page.Data.Value.GetProperty("after"));
        }
        else if (tokens.Count > 1) tokens.RemoveAt(tokens.Count - 1); else return;
        LoadPage(page, tokens.ToArray(), true);
    }
    private Dictionary<string, object?> Request(string operation)
    {
        var scope = Scope ?? throw new InvalidOperationException("正在确认账号，请稍后重试");
        return new() { ["op"] = operation, ["sessionId"] = scope.SessionId, ["accountId"] = AccountId };
    }
    private void LoadPage(Page page, JsonElement?[] tokens, bool moving = false)
    {
        var request = Request(_kind == PageKind.Runs ? "runs" : "battles");
        if (_kind == PageKind.Runs && _hero.HasValue) request["hero"] = _hero.Value.ToString();
        if (_kind == PageKind.Runs && _mode != null) request["playMode"] = _mode;
        if (_kind != PageKind.Runs && _run.HasValue) request["runId"] = HistoryViewText.Value(_run.Value,"id");
        if (tokens[^1].HasValue) request["after"] = tokens[^1];
        QueryRead(request, result =>
        {
            page.Data = result; page.Tokens.Clear(); page.Tokens.AddRange(tokens); if (moving) page.Scroll = 1; Render();
        }, () => LoadPage(page, tokens, moving));
    }
    private void QueryRead(Dictionary<string, object?> request, Action<JsonElement> apply, Action? retry = null)
    {
        ++_version; _loading = true; _retry = retry ?? Refresh;
        if (_status != null) _status.text = "正在读取历史…";
        Paging();
        _queries.Add(new(AndroidHistoryBridge.ReadAsync(request), _version, request["sessionId"] + ":" + request["accountId"], apply));
    }
    private void DrainQueries()
    {
        for (var index = _queries.Count - 1; index >= 0; index--)
        {
            var query = _queries[index]; if (!query.Task.IsCompleted) continue; _queries.RemoveAt(index);
            var current = _open && query.Version == _version && query.Scope == ScopeKey;
            try
            {
                var result = query.Task.GetAwaiter().GetResult(); if (!current) continue;
                _loading = false;
                var state = HistoryViewText.Value(result,"state");
                if (state != "ok")
                {
                    if (_status != null) _status.text = state switch { "missing" => "尚无已保存的历史记录", "newer_format" => "历史格式较新，请升级启动器", _ => "所选记录不存在，请返回刷新" };
                }
                else { _retry = null; query.Apply(result); }
                Paging();
            }
            catch (Exception error)
            {
                if (!current) continue; _loading = false;
                if (_status != null) _status.text = "读取失败，已展示的记录保留。点击重试。";
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=history.query.failed exception={error}"); Paging();
            }
        }
    }
    private void Select(int slot)
    {
        if (_loading || slot >= _items.Count) return;
        Remember(); var selected = _items[slot];
        if (_kind == PageKind.Runs)
        {
            _run = selected; _kind = PageKind.Battles;
            var battles = CurrentPage; battles.Data = null; battles.Tokens.Clear(); battles.Tokens.Add(null); battles.Scroll = 1;
            Render(); Refresh();
        }
        else if (_kind == PageKind.Battles)
        {
            _battle = selected; _kind = PageKind.Formation; Render(); Refresh();
        }
    }
    private void Render()
    {
        if (_title == null || _listRoot == null || _content == null || _scroll == null) return;
        _title.text = _kind switch { PageKind.Runs => "对局历史", PageKind.Battles => "整局详情", _ => "双方历史阵容" };
        _art.Clear();
        if (_heroButton != null)
        {
            _heroButton.gameObject.SetActive(_kind == PageKind.Runs);
            _heroButton.GetComponentInChildren<TextMeshProUGUI>().text = NativeUi.Text("Hero: ", "英雄：")
                + (_hero.HasValue ? AndroidChoicePopup.HeroName(_hero.Value) : NativeUi.Text("All", "全部")) + " v";
            AndroidPanelButtonSkin.SetSelected(_heroButton, _hero.HasValue);
        }
        if (_modeButton != null)
        {
            _modeButton.gameObject.SetActive(_kind == PageKind.Runs);
            _modeButton.GetComponentInChildren<TextMeshProUGUI>().text = NativeUi.Text("Mode: ", "模式：")
                + (_mode == null ? NativeUi.Text("All", "全部") : ModeName(_mode)) + " v";
            AndroidPanelButtonSkin.SetSelected(_modeButton, _mode != null);
        }
        var showHeader = _kind == PageKind.Battles && _run.HasValue;
        _runHeader?.Button.gameObject.SetActive(showHeader);
        if (showHeader)
        {
            FillRun(_runHeader!, _run!.Value);
            _runHeader!.Secondary.text += " · 结束 " + HistoryViewText.Date(_run.Value,"endedAt");
        }
        if (_summary != null)
        {
            _summary.gameObject.SetActive(!showHeader);
            _summary.text = _kind == PageKind.Runs ? "按开始时间倒序 · 点击整局查看 PvP 交手"
                : _run.HasValue ? HistoryViewText.Run(_run.Value) : "";
        }
        _listRoot.anchorMax = new(.97f, showHeader ? .705f : .77f);
        if (_status != null) _status.text = AccountId == null ? "账号待确认，仅显示本会话暂存记录"
            : _kind == PageKind.Formation ? "点击或长按卡牌查看完整已存字段 · 保存的属性是历史事实，原生卡面使用当前模板，仅供参考" : "";
        var formation = _kind == PageKind.Formation;
        _listRoot.gameObject.SetActive(!formation); _items.Clear();
        _formation?.Hide();
        if (formation)
        {
            if (_battle.HasValue && _battle.Value.TryGetProperty("snapshots", out _)) _formation?.Show(_battle.Value);
        }
        else
        {
            if (CurrentPage.Data.HasValue && CurrentPage.Data.Value.TryGetProperty("items", out var items)) _items.AddRange(items.EnumerateArray());
            for (var index = 0; index < _rows.Count; index++)
            {
                var visible = index < _items.Count; _rows[index].Button.gameObject.SetActive(visible);
                if (visible)
                {
                    if (_kind == PageKind.Runs) FillRun(_rows[index], _items[index]);
                    else FillBattle(_rows[index], _items[index]);
                }
            }
            _content.sizeDelta = new(0, Math.Max(1, _items.Count * RowHeight)); _scroll.verticalNormalizedPosition = CurrentPage.Scroll;
            if (_items.Count == 0 && _status != null) _status.text = _kind == PageKind.Battles ? "本局暂无已保存的 PvP 交手记录"
                : _hero.HasValue || _mode != null ? "当前筛选暂无已保存的整局记录，可调整英雄或模式" : "尚无已保存的整局记录";
        }
        Paging();
    }
    private void Paging()
    {
        var formation = _kind == PageKind.Formation;
        if (_previous != null) { _previous.gameObject.SetActive(!formation); _previous.interactable = !_loading && CurrentPage.Tokens.Count > 1; }
        if (_next != null) { _next.gameObject.SetActive(!formation); _next.interactable = !_loading && CurrentPage.Data.HasValue && CurrentPage.Data.Value.GetProperty("hasMore").GetBoolean(); }
        if (_pageLabel != null) { _pageLabel.gameObject.SetActive(!formation); _pageLabel.text = $"第 {CurrentPage.Tokens.Count} 页"; }
        if (_retryButton != null) _retryButton.gameObject.SetActive(!_loading && _retry != null);
        foreach (var row in _rows) row.Button.interactable = !_loading;
    }
    public void OnApplicationFocus(bool focus) { if (!focus && _open) SetOpen(false); }
    public void OnApplicationPause(bool paused) { if (paused && _open) SetOpen(false); }
    public void OnDestroy()
    {
        SetOpen(false);
        foreach (var query in _queries) _ = query.Task.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        _queries.Clear(); _choices?.Dispose(); _art.Dispose(); if (_canvas != null) Destroy(_canvas);
    }
}
