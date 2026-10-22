using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.CollectionPanel.Data;
using BazaarPlusPlus.Game.CollectionPanel.Sources;
using BazaarPlusPlus.Game.CollectionPanel.Grid;
using BazaarPlusPlus.GameInterop.DayTiers;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Localization;
using TheBazaar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;

namespace BazaarPlusPlus.Android;

public sealed class CollectionController(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static bool AnyOpen { get; private set; }
    private const int PageSize = 21;
    private RectTransform? _gridHost;
    private int _pageFirst;
    private int _pageLast = -1;
    private readonly AndroidBuildCandidates _thumbnails = new();
    private IReadOnlyList<CollectionSourceEntry> _sources = Array.Empty<CollectionSourceEntry>();
    private CollectionSourceEntry? _source;
    private bool _followSource = true;
    private AndroidChoicePopup? _choices;
    private Button? _sourceButton;
    private Button? _dayButton;
    private GameDataDayTierTable? _dayTiers;
    private int? _day;
    private float _nextContextRead;
    private readonly AndroidCollectionCatalog _catalog = new();
    private readonly CollectionFilterState _filter = new();
    private readonly List<Button> _rows = new();
    private List<CollectionCardVm> _visible = new();
    private EHero[] _heroes = Array.Empty<EHero>();
    private GameObject? _canvas;
    private RectTransform? _panel;
    private RectTransform? _previewHost;
    private Button? _entry;
    private Button? _heroButton;
    private Button? _tierButton;
    private Button? _sizeButton;
    private Button? _itemsButton;
    private Button? _skillsButton;
    private Button? _previewTierButton;
    private Button? _enchantmentButton;
    private List<VariantChoice<ETier>> _previewTiers = new();
    private List<VariantChoice<EEnchantmentType?>> _enchantments = new();
    private ETier _previewTier;
    private EEnchantmentType? _enchantment;
    private BazaarGameShared.Domain.Cards.TCardInstance? _pendingInstance;
    private TMP_InputField? _search;
    private TextMeshProUGUI? _pageLabel;
    private TextMeshProUGUI? _loadingHint;
    private TextMeshProUGUI? _status;
    private TextMeshProUGUI? _description;
    private RectTransform? _descriptionContent;
    private ScrollRect? _descriptionScroll;
    private bool _open;
    private bool _uiBuilt;
    private int _page;
        private float _nextTry;
    private int _failures;
    private int _scene = -1;
    private Guid? _requested;
    private Guid? _shown;
    private int _requestVersion;
    private int _pendingVersion;
    private NativePreviewTask? _pendingPreview;
    private Il2CppSystem.Threading.CancellationTokenSource? _requestCancellation;
    private CardPreviewBase? _preview;
    private CardPreviewBase? _preparing;
    private Il2CppSystem.Threading.Tasks.Task? _pendingSetup;
    private Guid _pendingTemplate;
    private int _fitFrame;
    private int _fitAttempts;
    private static readonly ETier[] Tiers = { ETier.Bronze, ETier.Silver, ETier.Gold, ETier.Diamond, ETier.Legendary };
    private static readonly ECardSize[] Sizes = { ECardSize.Small, ECardSize.Medium, ECardSize.Large };

    public void Update()
    {
        try
        {
            if (_canvas == null)
            {
                if (_failures >= 3 || Time.realtimeSinceStartup < _nextTry) return;
                _nextTry = Time.realtimeSinceStartup + 2;
                var font = NotoFontFallbackRuntime._loadedSansPrimary;
                if (font == null) return;
                BuildUi(font);
            }
            var scene = SceneManager.GetActiveScene().handle;
            if (_scene != -1 && scene != _scene) SetOpen(false);
            _scene = scene;
            if (_entry != null) _entry.gameObject.SetActive(!_open && !AndroidBuildPanelController.AnyOpen);
            if (_open && !_catalog.Ready && _catalog.Error == null && Data.IsManagerCreated())
                _catalog.Start(Data.GetStatic());
            if (_catalog.Tick())
            {
                _heroes = _catalog.Cards.SelectMany(card => card.Heroes).Distinct().OrderBy(hero => (int)hero).ToArray();
                Refresh();
            }
            if (_open && Time.realtimeSinceStartup >= _nextContextRead)
            {
                _nextContextRead = Time.realtimeSinceStartup + 1;
                if (ReadContext()) Changed();
            }
            _thumbnails.Tick();
            try { TickPreview(); }
            catch (Exception error)
            {
                RetireRequest();
                if (_preview != null) UnityEngine.Object.Destroy(_preview.gameObject);
                _preview = null;
                _shown = _requested;
                if (_status != null) _status.text = NativeUi.Text("Preview unavailable", "卡牌预览暂不可用");
                Plugin.PluginLog?.LogError($"[BPP][Android] event=android.collection.preview_transition_failed exception={error}");
            }
        }
        catch (Exception error)
        {
            _failures++;
            if (!_uiBuilt && _canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas);
                _canvas = null;
            }
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.collection.tick_failed attempt={_failures} exception={error}");
            if (_failures >= 3)
            {
                AnyOpen = false;
                enabled = false;
            }
        }
    }

    private void BuildUi(TMP_FontAsset font)
    {
        _canvas = new GameObject("BazaarPlusPlus.Android.Canvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        UnityEngine.Object.DontDestroyOnLoad(_canvas);
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = _canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        _canvas.AddComponent<GraphicRaycaster>();
        _entry = NativeUi.Button(_canvas.transform, font, NativeUi.Text("Card Collection", "卡牌图鉴"),
            new Vector2(0, .5f), new Vector2(0, .5f), () => SetOpen(true),
            new Vector2(30, -45), new Vector2(300, 45));
        _panel = NativeUi.Rect("BPP.Collection", _canvas.transform, Vector2.zero, Vector2.one);
        var background = _panel.gameObject.AddComponent<Image>();
        background.color = new Color(.025f, .04f, .065f, .995f);
        background.raycastTarget = true;
        _sources = AndroidCollectionSources.Load();
        _choices = new AndroidChoicePopup(_panel, font);
        var title = NativeUi.Rect("BPP.Title", _panel, new Vector2(.03f, .88f), new Vector2(.6f, .98f));
        NativeUi.Label(title, font, NativeUi.Text("Card Collection", "卡牌图鉴"), 44);
        NativeUi.Button(_panel, font, NativeUi.Text("Reset filters", "重置筛选"), new Vector2(.64f, .88f), new Vector2(.82f, .98f), ResetFilters);
        NativeUi.Button(_panel, font, NativeUi.Text("Close", "关闭"), new Vector2(.85f, .88f), new Vector2(.97f, .98f), () => SetOpen(false));
        _heroButton = NativeUi.Button(_panel, font, NativeUi.Text("Hero: All", "英雄：全部"), new Vector2(.03f, .78f), new Vector2(.24f, .85f), ChooseHero);
        _tierButton = NativeUi.Button(_panel, font, NativeUi.Text("Tier: All", "品质：全部"), new Vector2(.25f, .78f), new Vector2(.42f, .85f), ChooseTier);
        _sizeButton = NativeUi.Button(_panel, font, NativeUi.Text("Size: All", "体型：全部"), new Vector2(.43f, .78f), new Vector2(.6f, .85f), ChooseSize);
        _itemsButton = NativeUi.Button(_panel, font, NativeUi.Text("Items", "物品"), new Vector2(.03f, .69f), new Vector2(.12f, .76f), () => { _filter.ActiveType = ECardType.Item; Changed(); });
        _skillsButton = NativeUi.Button(_panel, font, NativeUi.Text("Skills", "技能"), new Vector2(.13f, .69f), new Vector2(.22f, .76f), () => { _filter.ActiveType = ECardType.Skill; Changed(); });
        _search = NativeUi.Search(_panel, font, new Vector2(.24f, .69f), new Vector2(.6f, .76f), text => { _filter.SearchQuery = text; Changed(); });
        _sourceButton = NativeUi.Button(_panel, font, string.Empty, new Vector2(.03f, .60f), new Vector2(.40f, .67f), ChooseSource);
        _dayButton = NativeUi.Button(_panel, font, string.Empty, new Vector2(.41f, .60f), new Vector2(.60f, .67f), ChooseDay);
        _gridHost = NativeUi.Rect("BPP.CollectionGrid", _panel, new Vector2(.03f, .14f), new Vector2(.60f, .58f));
        for (var index = 0; index < PageSize; index++)
        {
            var slot = index;
            _rows.Add(NativeUi.Button(_gridHost, font, string.Empty, Vector2.zero, Vector2.zero, () => SelectSlot(slot)));
        }
        _loadingHint = NativeUi.Label(NativeUi.Rect("BPP.LoadingHint", _gridHost, Vector2.zero, Vector2.one), font,
            NativeUi.Text("Data may take longer to load the first time", "首次打开数据加载可能较慢"), 30);
        _loadingHint.alignment = TextAlignmentOptions.Center;
        _loadingHint.raycastTarget = false;
        NativeUi.Button(_panel, font, NativeUi.Text("Previous", "上一页"), new Vector2(.03f, .04f), new Vector2(.18f, .12f), () => { _page--; Refresh(); });
        NativeUi.Button(_panel, font, NativeUi.Text("Next", "下一页"), new Vector2(.45f, .04f), new Vector2(.6f, .12f), () => { _page++; Refresh(); });
        _pageLabel = NativeUi.Label(NativeUi.Rect("BPP.Page", _panel, new Vector2(.19f, .04f), new Vector2(.44f, .12f)), font, string.Empty, 27);
        _previewTierButton = NativeUi.Button(_panel, font, NativeUi.Text("Preview tier", "预览品质"),
            new Vector2(.64f, .76f), new Vector2(.80f, .82f), ChoosePreviewTier);
        _enchantmentButton = NativeUi.Button(_panel, font, NativeUi.Text("Enchantment", "附魔"),
            new Vector2(.81f, .76f), new Vector2(.97f, .82f), ChooseEnchantment);
        _previewTierButton.interactable = _enchantmentButton.interactable = false;
        _previewHost = NativeUi.Rect("BPP.Preview", _panel, new Vector2(.64f, .38f), new Vector2(.97f, .75f));
        _status = NativeUi.Label(NativeUi.Rect("BPP.Status", _panel, new Vector2(.64f, .83f), new Vector2(.97f, .87f)), font,
            NativeUi.Text("Select a card", "点选左侧卡牌"), 28);
        (_description, _descriptionContent, _descriptionScroll) = NativeUi.ScrollText(_panel, font,
            new Vector2(.64f, .04f), new Vector2(.97f, .35f));
        _filter.ToggleAllHeroes();
        SetOpen(false);
        _uiBuilt = true;
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.collection.ui_ready");
    }

    private static void Caption(Button? button, string text)
    {
        if (button != null) button.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    private void ChooseHero()
    {
        var options = new List<(string, Action)> { (NativeUi.Text("All heroes", "全部英雄"), () =>
        { if (!_filter.AllHeroesSelected) _filter.ToggleAllHeroes(); Changed(); }) };
        foreach (var hero in _heroes)
            options.Add((HeroName(hero), () =>
            { if (_filter.AllHeroesSelected || _filter.EffectiveHero != hero) _filter.ToggleHero(hero); Changed(); }));
        _choices?.Show(NativeUi.Text("Hero", "英雄"), options);
    }

    private void ChooseTier()
    {
        var options = new List<(string, Action)> { (NativeUi.Text("All tiers", "全部品质"), () =>
        { _filter.Tiers.Clear(); Changed(); }) };
        foreach (var tier in Tiers)
            options.Add((TierName(tier), () => { _filter.Tiers.Clear(); _filter.Tiers.Add(tier); Changed(); }));
        _choices?.Show(NativeUi.Text("Starting tier", "起始品质"), options);
    }

    private void ChooseSize()
    {
        var options = new List<(string, Action)> { (NativeUi.Text("All sizes", "全部体型"), () =>
        { _filter.Sizes.Clear(); Changed(); }) };
        foreach (var size in Sizes)
            options.Add((SizeName(size), () => { _filter.Sizes.Clear(); _filter.Sizes.Add(size); Changed(); }));
        _choices?.Show(NativeUi.Text("Size", "体型"), options);
    }

    private EHero SourceHero() => _filter.AllHeroesSelected && AndroidCollectionContext.InRun && Data.Run?.Player != null
        ? Data.Run.Player.Hero : _filter.EffectiveHero;

    private void ChooseSource()
    {
        var options = new List<(string, Action)>
        {
            (NativeUi.Text("Follow current shop", "跟随当前商店"), () => { _followSource = true; ReadContext(); Changed(); }),
            (NativeUi.Text("All sources", "全部来源"), () => { _followSource = false; _source = null; Changed(); }),
        };
        var kind = _filter.ActiveType == ECardType.Item ? CollectionSourceKind.Merchant : CollectionSourceKind.Trainer;
        foreach (var source in _sources.Where(source => source.Kind == kind && source.IsVisibleForHero(SourceHero())))
            options.Add((SourceName(source), () => { _followSource = false; _source = source; Changed(); }));
        _choices?.Show(NativeUi.Text("Shop / Trainer", "商店／训练师"), options);
    }

    private void ChooseDay() => _choices?.Show(NativeUi.Text("Day filter", "天数筛选"),
        new List<(string, Action)>
        {
            (NativeUi.Text("Follow run day", "跟随对局天数"), () => { _filter.UseRunDayFilter = true; Changed(); }),
            (NativeUi.Text("All days", "不限天数"), () => { _filter.UseRunDayFilter = false; Changed(); }),
        });

    private string SourceName(CollectionSourceEntry source)
    {
        try
        {
            var template = Data.GetStatic()?.GetCardById(Il2CppSystem.Guid.Parse(source.PortraitTemplateId.ToString()));
            var card = template?.TryCast<BazaarGameShared.Domain.Cards.TCardBase>();
            var title = card?.Localization?.Title;
            return title == null ? source.Name : TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(title) ?? source.Name;
        }
        catch (Exception) { return source.Name; }
    }

    private bool ReadContext()
    {
        var previousSource = _source;
        var previousDay = _day;
        var previousTier = _dayTiers?.MaximumTier;
        try
        {
            (_day, _dayTiers) = AndroidCollectionContext.DayTiers();
            if (_followSource)
                _source = Data.Run?.Player == null ? null : AndroidCollectionContext.Current(_sources, Data.Run.Player.Hero);
        }
        catch (Exception error)
        {
            _day = null;
            _dayTiers = null;
            if (_followSource) _source = null;
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.collection.context_failed exception={error}");
        }
        return !ReferenceEquals(previousSource, _source) || previousDay != _day || previousTier != _dayTiers?.MaximumTier;
    }

    private static string HeroName(EHero hero) => hero == EHero.Common
        ? NativeUi.Text("Common", "通用") : TheBazaar.Utilities.LocalizableShared.GetLocalizedHero(hero);

    private static string TierName(ETier tier) => tier switch
    {
        ETier.Bronze => NativeUi.Text("Bronze", "青铜"), ETier.Silver => NativeUi.Text("Silver", "白银"),
        ETier.Gold => NativeUi.Text("Gold", "黄金"), ETier.Diamond => NativeUi.Text("Diamond", "钻石"),
        ETier.Legendary => NativeUi.Text("Legendary", "传奇"), _ => tier.ToString(),
    };
    private static string SizeName(ECardSize size) => size switch
    {
        ECardSize.Small => NativeUi.Text("Small", "小型"), ECardSize.Medium => NativeUi.Text("Medium", "中型"),
        ECardSize.Large => NativeUi.Text("Large", "大型"), _ => size.ToString(),
    };

    private void Changed() { _page = 0; Refresh(); }

    private void ResetFilters()
    {
        _filter.ResetFacets();
        _filter.ToggleAllHeroes();
        _search?.SetTextWithoutNotify(string.Empty);
        _search?.DeactivateInputField();
        _followSource = true;
        _filter.UseRunDayFilter = true;
        ReadContext();
        Changed();
    }

    private void Refresh()
    {
        _loadingHint?.gameObject.SetActive(!_catalog.Ready && _catalog.Error == null);
        var items = _filter.ActiveType == ECardType.Item;
        _sizeButton?.gameObject.SetActive(items);
        if (_itemsButton != null) _itemsButton.GetComponent<Image>().color = items ? new Color(.28f, .35f, .25f, 1) : new Color(.15f, .20f, .24f, 1);
        if (_skillsButton != null) _skillsButton.GetComponent<Image>().color = !items ? new Color(.28f, .35f, .25f, 1) : new Color(.15f, .20f, .24f, 1);
        if (_source != null && !_source.AppliesToHero(SourceHero()) && !_followSource) _source = null;
        var source = _source != null && (_source.Kind == CollectionSourceKind.Merchant) == items ? _source : null;
        var poolHero = SourceHero();
        if (source != null && !source.AppliesToHero(poolHero)) source = null;
        var pool = CollectionSourceOfferPoolResolver.Resolve(source, poolHero, _catalog.Cards);
        _visible = CollectionFilterEngine.Apply(_catalog.Cards, _filter,
            new CollectionFilterContext
            {
                ApplyHeroFilter = !_filter.AllHeroesSelected && (source == null ||
                    (!items && !source.HasAllHeroesOfferSegment)),
                OfferedCardIds = source == null ? null : pool.OfferedCardIds,
                DayTiers = _dayTiers, SuppressDayGate = source?.SuppressDayGate ?? false,
            });
        Caption(_heroButton, NativeUi.Text("Hero: ", "英雄：") + (_filter.AllHeroesSelected
            ? NativeUi.Text("All", "全部") : HeroName(_filter.EffectiveHero)) + " v");
        Caption(_tierButton, NativeUi.Text("Tier: ", "品质：") + (_filter.Tiers.Count == 0 ? NativeUi.Text("All", "全部") : TierName(_filter.Tiers.First())) + " v");
        Caption(_sizeButton, NativeUi.Text("Size: ", "体型：") + (_filter.Sizes.Count == 0 ? NativeUi.Text("All", "全部") : SizeName(_filter.Sizes.First())) + " v");
        Caption(_sourceButton, (_followSource ? NativeUi.Text("Auto: ", "自动：") : string.Empty) +
            (source == null ? NativeUi.Text("All sources", "全部来源") : SourceName(source)) + " v");
        Caption(_dayButton, !_filter.UseRunDayFilter ? NativeUi.Text("All days v", "不限天数 v") :
            _day.HasValue ? NativeUi.Text("Day ", "第 ") + _day + (_dayTiers == null ? NativeUi.Text(" (unavailable) v", " 天（不可用）v") : NativeUi.Text(" v", " 天 v")) : NativeUi.Text("No run v", "未在对局 v"));
        var layout = CollectionGridLayout.Build(_visible, _filter.ActiveTab);
        var shelvesPerPage = items ? 2 : 3;
        var pages = Math.Max(1, (layout.ShelfCount + shelvesPerPage - 1) / shelvesPerPage);
        _page = Math.Clamp(_page, 0, pages - 1);
        var firstShelf = _page * shelvesPerPage;
        _pageFirst = layout.Count == 0 ? 0 : layout.ShelfAt(firstShelf).FirstIndex;
        _pageLast = layout.Count == 0 ? -1 : layout.ShelfAt(Math.Min(layout.ShelfCount - 1, firstShelf + shelvesPerPage - 1)).LastIndex;
        Canvas.ForceUpdateCanvases();
        var bounds = _gridHost!.rect;
        const float gap = 8;
        var unit = Math.Max(1, Math.Min((bounds.width - (layout.Columns - 1) * gap) / layout.Columns,
            bounds.height / (shelvesPerPage * layout.ShelfHeightUnits) - gap));
        var originX = (bounds.width - layout.Columns * (unit + gap) + gap) * .5f;
        var scrollY = firstShelf * layout.ShelfPitch(unit, gap);
        for (var slot = 0; slot < _rows.Count; slot++)
        {
            var index = _pageFirst + slot;
            var active = index <= _pageLast;
            _rows[slot].gameObject.SetActive(active);
            if (!_open || !active) _thumbnails.Remove(_rows[slot]);
            if (active)
            {
                var cell = layout.ContentRectFor(index, unit, gap, originX, 0);
                var rect = _rows[slot].GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(cell.X, -(cell.Y - scrollY));
                rect.sizeDelta = new Vector2(cell.Width, cell.Height);
                Caption(_rows[slot], string.Empty);
                if (_open) _thumbnails.Show(_rows[slot], _visible[index].Id, fillCell: true);
            }
        }
        if (_pageLabel != null) _pageLabel.text = !_catalog.Ready ? NativeUi.Text("Loading…", "载入中…") :
            _visible.Count == 0 ? NativeUi.Text("No matching cards", "没有匹配的卡牌") : $"{_page + 1} / {pages}    ·    {_visible.Count}";
        if (_catalog.Error != null && _status != null) _status.text = NativeUi.Text("Could not load cards. Close and reopen to retry.", "卡牌载入失败，请关闭后重试。");
        if (_requested.HasValue && !_visible.Any(card => card.Id == _requested)) ClearPreview();
        if (_open && _catalog.Ready)
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.filtered type={_filter.ActiveType} hero={poolHero} source={source?.Name} day={_day} day_filter={_filter.UseRunDayFilter} max_tier={_dayTiers?.MaximumTier} suppress_day={source?.SuppressDayGate ?? false} cards={_visible.Count} page={_page + 1} pages={pages}");
    }

    private void SelectSlot(int slot)
    {
        var index = _pageFirst + slot;
        if (index > _pageLast || index >= _visible.Count) return;
        ClearPreview();
        _requested = _visible[index].Id;
        _previewTier = _visible[index].StartingTier;
        _enchantment = null;
        if (_status != null) _status.text = _visible[index].DisplayName;
    }

    private void ChoosePreviewTier()
    {
        if (!_requested.HasValue) return;
        _choices?.Show(NativeUi.Text("Preview tier", "预览品质"), _previewTiers.Select(choice =>
            (choice.Label, (Action)(() => { _previewTier = choice.Value; RestartVariant(); }))).ToArray());
    }

    private void ChooseEnchantment()
    {
        if (!_requested.HasValue) return;
        _choices?.Show(NativeUi.Text("Enchantment", "附魔"), _enchantments.Select(choice =>
            (choice.Label, (Action)(() => { _enchantment = choice.Value; RestartVariant(); }))).ToArray());
    }

    private void RestartVariant()
    {
        var selected = _requested;
        ClearPreview(preserveVariants: true);
        _requested = selected;
        UpdateVariantCaptions();
    }

    private void UpdateVariantCaptions()
    {
        Caption(_previewTierButton, _previewTiers.FirstOrDefault(choice => choice.Value == _previewTier)?.Label
            ?? NativeUi.Text("Preview tier", "预览品质"));
        Caption(_enchantmentButton, _enchantments.FirstOrDefault(choice => choice.Value == _enchantment)?.Label
            ?? NativeUi.Text("Enchantment", "附魔"));
        if (_previewTierButton != null) _previewTierButton.interactable = _previewTiers.Count > 1;
        if (_enchantmentButton != null) _enchantmentButton.interactable = _enchantments.Count > 1;
    }

    private void TickPreview()
    {
        if (_pendingPreview != null && _pendingPreview.IsCompleted)
        {
            var pending = _pendingPreview;
            _pendingPreview = null;
            if (pending.IsFaulted || pending.IsCanceled)
            {
                PreviewFailed(pending);
                FinishRequest();
            }
            else
            {
                _preparing = pending.Result;
                if (_preparing == null || !_open || _pendingVersion != _requestVersion || _previewHost == null)
                    FinishRequest();
                else
                {
                    var rect = _preparing.GetComponent<RectTransform>();
                    rect.SetParent(_previewHost, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    _preparing.gameObject.SetActive(true);
                    // GenerateUICard rents the prefab; its state machine has only a
                    // Task<GameObject> awaiter. SetUp is a separate native async operation.
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.preview_setup_started template_id={_pendingTemplate} had_card_data={_preparing._cardData != null}");
                    _pendingSetup = _preparing.SetUp(_catalog.Templates[_pendingTemplate], false, _pendingInstance!, _requestCancellation!.Token);
                }
            }
        }
        if (_pendingSetup != null && _pendingSetup.IsCompleted)
        {
            var setup = _pendingSetup;
            _pendingSetup = null;
            if (setup.IsFaulted || setup.IsCanceled)
                PreviewFailed(setup);
            else if (_open && _pendingVersion == _requestVersion && _preparing != null)
            {
                _preview = _preparing;
                _preparing = null;
                _shown = _pendingTemplate;
                _preview.Resize();
                _preview.Show(true);
                if (_preview._tooltipData != null)
                {
                    try
                    {
                        (_previewTiers, _enchantments) = AndroidCardVariants.ReadOptions(_preview._tooltipData);
                        UpdateVariantCaptions();
                    }
                    catch (Exception error)
                    {
                        _previewTiers.Clear();
                        _enchantments.Clear();
                        UpdateVariantCaptions();
                        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.collection.variant_options_failed exception={error}");
                    }
                }
                if (_description != null && _descriptionContent != null && _preview._tooltipData != null)
                {
                    NativeUi.RichDescription(_description, AndroidItemDescription.Build(_preview._tooltipData));
                    var width = Math.Max(1, _descriptionContent.rect.width - 24);
                    var preferred = _description.GetPreferredValues(_description.text, width, float.PositiveInfinity);
                    _descriptionContent.sizeDelta = new Vector2(0, Math.Max(60, preferred.y + 24));
                    if (_descriptionScroll != null) _descriptionScroll.verticalNormalizedPosition = 1;
                }
                _fitFrame = Time.frameCount + 2;
                _fitAttempts = 3;
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.preview_ready template_id={_shown} requested_tier={_pendingInstance?.Tier} actual_tier={_preview._tooltipData?.CardInstance?.Tier} enchantment={_enchantment} template_starting_tier={_catalog.Templates[_pendingTemplate].StartingTier} tier_options={_previewTiers.Count} enchantment_options={_enchantments.Count}");
            }
            FinishRequest();
        }
        if (_preview != null && _fitAttempts > 0 && Time.frameCount >= _fitFrame)
        {
            FitPreview();
            _fitFrame = Time.frameCount + 2;
            _fitAttempts--;
        }
        if (!_open || !_requested.HasValue || _shown == _requested || _pendingPreview != null ||
            _pendingSetup != null || _preparing != null || _previewHost == null) return;
        if (!Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        _pendingVersion = _requestVersion;
        _pendingTemplate = _requested.Value;
        _pendingInstance = AndroidCardVariants.CreateInstance(_catalog.Templates[_pendingTemplate], _previewTier, _enchantment);
        _requestCancellation = new Il2CppSystem.Threading.CancellationTokenSource();
        _pendingPreview = loader.GenerateUICard(_previewHost, _requestCancellation.Token, _catalog.Templates[_pendingTemplate]);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.preview_started template_id={_requested}");
    }

    private void PreviewFailed(Il2CppSystem.Threading.Tasks.Task task)
    {
        if (!task.IsCanceled && _pendingVersion == _requestVersion)
        {
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.collection.preview_failed exception={task.Exception}");
            if (_status != null) _status.text = NativeUi.Text("Preview unavailable", "卡牌预览暂不可用");
            _shown = _requested;
        }
    }

    private void FinishRequest()
    {
        if (_preparing != null) UnityEngine.Object.Destroy(_preparing.gameObject);
        _preparing = null;
        _requestCancellation?.Dispose();
        _requestCancellation = null;
        _pendingInstance = null;
    }

    private void FitPreview()
    {
        if (_preview == null || _previewHost == null) return;
        var rect = _preview.GetComponent<RectTransform>();
        var fitter = _preview.GetComponent<AspectRatioFitter>();
        var ratio = fitter != null && fitter.aspectRatio > .01f ? fitter.aspectRatio : .8f;
        if (rect.rect.width <= 1 || rect.rect.height <= 1)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500 * ratio);
        }
        Canvas.ForceUpdateCanvases();
        var art = _preview._cardImage.rectTransform.rect;
        var bounds = _previewHost.rect;
        var width = art.width > 1 ? art.width : rect.rect.width;
        var height = art.height > 1 ? art.height : rect.rect.height;
        var scale = Math.Clamp(Math.Min(bounds.width / Math.Max(1, width), bounds.height / Math.Max(1, height)) * .8f, .02f, 4);
        rect.localScale = new Vector3(scale, scale, scale);
        var imageRect = _preview._cardImage.rectTransform;
        var center = _previewHost.InverseTransformPoint(imageRect.TransformPoint(new Vector3(art.center.x, art.center.y, 0)));
        rect.anchoredPosition -= new Vector2(center.x, center.y);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.preview_fit art_width={art.width} art_height={art.height} active={_preview.gameObject.activeInHierarchy} texture={_preview._cardImage.texture != null} scale={scale}");
    }

    private void ClearPreview(bool preserveVariants = false)
    {
        _requestVersion++;
        _requested = null;
        _shown = null;
        _requestCancellation?.Cancel();
        if (_preparing != null) _preparing.gameObject.SetActive(false);
        if (_preview != null) UnityEngine.Object.Destroy(_preview.gameObject);
        _preview = null;
        if (!preserveVariants)
        {
            _previewTiers.Clear();
            _enchantments.Clear();
            UpdateVariantCaptions();
        }
        if (_description != null) _description.text = string.Empty;
    }

    private void SetOpen(bool open)
    {
        _open = open;
        AnyOpen = open;
        _panel?.gameObject.SetActive(open);
        _entry?.gameObject.SetActive(!open && !AndroidBuildPanelController.AnyOpen);
        _choices?.Close();
        if (!open) { ClearPreview(); _thumbnails.Hide(); }
        else
        {
            _catalog.Retry();
            ReadContext();
            var player = Data.Run?.Player;
            if (_followSource && player != null && AndroidCollectionContext.InRun)
            {
                _filter.ApplySelection(new CollectionPanelSelectionState(player.Hero, null, CollectionSourceKind.Merchant));
                if (_source != null) _filter.ActiveType = _source.Kind == CollectionSourceKind.Merchant ? ECardType.Item : ECardType.Skill;
            }
            Changed();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.context source={_source?.Name} day={_day} max_tier={_dayTiers?.MaximumTier} count={_visible.Count}");
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.visibility open={open}");
    }

    public void OnDestroy()
    {
        AnyOpen = false;
        _choices?.Close();
        _thumbnails.Hide();
        ClearPreview();
        RetireRequest();
        if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
        _canvas = null;
    }

    private void RetireRequest()
    {
        NativePreviewCleanup.Retire(_pendingPreview, _pendingSetup, _preparing, _requestCancellation);
        _pendingPreview = null;
        _pendingSetup = null;
        _preparing = null;
        _requestCancellation = null;
        _pendingInstance = null;
    }
}
