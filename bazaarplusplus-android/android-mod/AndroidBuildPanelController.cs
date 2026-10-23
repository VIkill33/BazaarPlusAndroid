using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidBuildPanelController(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static bool AnyOpen { get; private set; }
    private GameObject? _canvas;
    private RectTransform? _panel;
    private Button? _entry;
    private Button? _previewModeButton;
    private TextMeshProUGUI? _text;
    private RectTransform? _content;
    private ScrollRect? _scroll;
    private AndroidBuildBoard? _board;
    private RectTransform? _boardHost;
    private Button? _previousBuildButton;
    private Button? _nextBuildButton;
    private TextMeshProUGUI? _buildPosition;
    private AndroidBuildMatch[] _matches = Array.Empty<AndroidBuildMatch>();
    private int _buildIndex;
    private Button? _sourceButton;
    private Button? _candidatePageButton;
    private readonly List<Button> _candidateButtons = new();
    private readonly List<TextMeshProUGUI> _candidateMarkers = new();
    private readonly AndroidBuildCandidates _candidateCards = new();
    private readonly List<(Guid Id, string Name)> _boardCandidates = new();
    private readonly List<(Guid Id, string Name)> _stashCandidates = new();
    private readonly List<(Guid Id, string Name)> _shopCandidates = new();
    private readonly HashSet<Guid> _selectedCandidates = new();
    private CandidateSource _source;
    private int _candidatePage;
    private bool _selectionInitialized;
    private bool _open;
    private int _scene = -1;
    private float _nextRefresh;
    private int _failures;
    private string? _lastDescription;
    private string? _itemDetail;

    private enum CandidateSource { Board, Stash, Shop }

    public void Update()
    {
        try
        {
            if (_open)
            {
                _board?.Tick();
                _candidateCards.Tick();
            }
            if (string.IsNullOrWhiteSpace(PlayerPreferences.Data?.LanguageCode)) return;
            if (_canvas == null)
            {
                var font = NotoFontFallbackRuntime._loadedSansPrimary;
                if (font == null) return;
                BuildUi(font);
            }
            var scene = SceneManager.GetActiveScene().handle;
            if (_scene != -1 && scene != _scene) SetOpen(false);
            _scene = scene;
            UpdateEntryVisibility();
            if (_open && Time.realtimeSinceStartup >= _nextRefresh)
            {
                _nextRefresh = Time.realtimeSinceStartup + 1;
                Refresh();
            }
        }
        catch (Exception error)
        {
            _failures++;
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.build_panel.failed count={_failures} exception={error}");
            SetOpen(false);
            if (_failures >= 3) enabled = false;
        }
    }

    private void BuildUi(TMP_FontAsset font)
    {
        _canvas = new GameObject("BPP.BuildCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        try
        {
            UnityEngine.Object.DontDestroyOnLoad(_canvas);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30001;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            _canvas.AddComponent<GraphicRaycaster>();
            _entry = NativeUi.Button(_canvas.transform, font, () => NativeUi.Text("Builds", "构筑推荐"),
                new Vector2(0, .5f), new Vector2(0, .5f), () => SetOpen(true),
                new Vector2(30, -145), new Vector2(300, -55));
            _previewModeButton = NativeUi.Button(_canvas.transform, font, PreviewModeLabel,
                new Vector2(0, .5f), new Vector2(0, .5f), TogglePreviewMode,
                new Vector2(30, -240), new Vector2(300, -150));
            _panel = NativeUi.Rect("BPP.BuildPanel", _canvas.transform, Vector2.zero, Vector2.one);
            var background = _panel.gameObject.AddComponent<Image>();
            background.color = new Color(.025f, .04f, .065f, .995f);
            background.raycastTarget = true;
            NativeUi.Label(NativeUi.Rect("BPP.BuildTitle", _panel, new Vector2(.03f, .89f),
                new Vector2(.36f, .98f)), font, () => NativeUi.Text("Recommended builds", "构筑推荐"), 40);
            _sourceButton = NativeUi.Button(_panel, font, NativeUi.Text("Board", "场上"),
                new Vector2(.37f, .89f), new Vector2(.53f, .98f), CycleSource);
            _candidatePageButton = NativeUi.Button(_panel, font, "1/1",
                new Vector2(.54f, .89f), new Vector2(.68f, .98f), NextCandidatePage);
            NativeUi.Button(_panel, font, () => NativeUi.Text("Refresh", "刷新"),
                new Vector2(.70f, .89f), new Vector2(.82f, .98f), () =>
                {
                    _ = Plugin.BuildRepository?.RefreshAsync();
                    _nextRefresh = 0;
                });
            NativeUi.Button(_panel, font, () => NativeUi.Text("Close", "关闭"),
                new Vector2(.85f, .89f), new Vector2(.97f, .98f), () => SetOpen(false));
            for (var slot = 0; slot < 10; slot++)
            {
                var captured = slot;
                var left = .03f + slot * .095f;
                var button = NativeUi.Button(_panel, font, string.Empty,
                    new Vector2(left, .68f), new Vector2(left + .085f, .86f),
                    () => ToggleCandidate(captured));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 25;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18;
                label.fontSizeMax = 25;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = new Vector2(1, .22f);
                labelRect.offsetMin = new Vector2(5, 3);
                labelRect.offsetMax = new Vector2(-5, -3);
                var markerRect = NativeUi.Rect("BPP.CandidateSelection", button.transform,
                    Vector2.one, Vector2.one, new Vector2(-38, -38), new Vector2(-4, -4));
                var markerBackground = markerRect.gameObject.AddComponent<Image>();
                markerBackground.color = new Color(.06f, .10f, .12f, .95f);
                markerBackground.raycastTarget = false;
                var marker = NativeUi.Label(markerRect, font, string.Empty, 26);
                marker.GetComponent<RectTransform>().offsetMin = Vector2.zero;
                marker.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                _candidateMarkers.Add(marker);
                button.gameObject.SetActive(false);
                _candidateButtons.Add(button);
            }
            _boardHost = NativeUi.Rect("BPP.RecommendedBoard", _panel,
                new Vector2(.03f, .36f), new Vector2(.97f, .65f));
            _board = new AndroidBuildBoard(_boardHost, ShowItemDetail);
            _previousBuildButton = NativeUi.Button(_panel, font, "‹",
                new Vector2(.03f, .31f), new Vector2(.15f, .36f), PreviousBuild);
            _buildPosition = NativeUi.Label(NativeUi.Rect("BPP.BuildPosition", _panel,
                new Vector2(.16f, .31f), new Vector2(.84f, .36f)), font, string.Empty, 25);
            _nextBuildButton = NativeUi.Button(_panel, font, "›",
                new Vector2(.85f, .31f), new Vector2(.97f, .36f), NextBuild);
            (_text, _content, _scroll) = NativeUi.ScrollText(_panel, font,
                new Vector2(.03f, .04f), new Vector2(.97f, .30f));
            _text.fontSize = 29;
            _text.enableAutoSizing = false;
            SetOpen(false);
        }
        catch
        {
            UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _panel = null;
            _entry = null;
            _previewModeButton = null;
            throw;
        }
    }

    private void Refresh()
    {
        var run = Data.Run;
        var player = run?.Player;
        var corpus = Plugin.BuildRepository?.Current;
        if (player == null || corpus == null)
        {
            Show(NativeUi.Text("Build data is loading or no run is active.", "构筑数据加载中，或当前没有对局。"));
            return;
        }

        var board = new HashSet<Guid>();
        var stash = new HashSet<Guid>();
        var shop = new HashSet<Guid>();
        _boardCandidates.Clear();
        _stashCandidates.Clear();
        _shopCandidates.Clear();
        var container = player.Hand?.Container;
        if (container != null)
        {
            foreach (var socket in container.GetCardsAndSockets())
            {
                var item = socket.Item1?.TryCast<ItemCard>();
                if (item == null) continue;
                var id = Guid.Parse(item.TemplateId.ToString());
                if (board.Add(id)) _boardCandidates.Add((id, ResolveName(id)));
            }
        }
        var stashContainer = player.Stash?.Container;
        if (stashContainer != null)
        {
            foreach (var socket in stashContainer.GetCardsAndSockets())
            {
                var item = socket.Item1?.TryCast<ItemCard>();
                if (item == null) continue;
                var id = Guid.Parse(item.TemplateId.ToString());
                if (stash.Add(id)) _stashCandidates.Add((id, ResolveName(id)));
            }
        }
        var selectionSet = Data.CurrentState?.SelectionSet;
        if (selectionSet != null)
        {
            for (var index = 0; index < selectionSet.Count; index++)
            {
                var item = Data.GetCard(selectionSet[index])?.TryCast<ItemCard>();
                if (item == null) continue;
                var id = Guid.Parse(item.TemplateId.ToString());
                if (shop.Add(id)) _shopCandidates.Add((id, ResolveName(id)));
            }
        }
        if (!_selectionInitialized)
        {
            _selectedCandidates.UnionWith(board);
            _selectionInitialized = true;
        }
        else _selectedCandidates.IntersectWith(board.Concat(stash).Concat(shop));
        UpdateCandidateButtons();
        var hero = player.Hero.ToString();
        var liveState = BuildLiveState.From(board, stash, shop);
        var matches = Plugin.BuildRepository!.Find(hero, _selectedCandidates, liveState).Take(3).ToArray();
        var changed = _matches.Length != matches.Length ||
            _matches.Where((match, index) => index < matches.Length &&
                (match.Build.Id != matches[index].Build.Id || match.LiveStateScore != matches[index].LiveStateScore ||
                 match.MatchedCards != matches[index].MatchedCards)).Any();
        _matches = matches;
        if (changed) { _buildIndex = 0; _itemDetail = null; }
        if (_buildIndex >= matches.Length) _buildIndex = 0;
        var chosen = matches.Length > 0 ? matches[_buildIndex] : null;
        _board?.Show(chosen?.Build);
        if (_boardHost != null) _boardHost.gameObject.SetActive(chosen != null);
        if (_previousBuildButton != null) _previousBuildButton.gameObject.SetActive(chosen != null);
        if (_nextBuildButton != null) _nextBuildButton.gameObject.SetActive(chosen != null);
        if (_buildPosition != null) _buildPosition.gameObject.SetActive(chosen != null);
        if (_scroll != null)
            _scroll.GetComponent<RectTransform>().anchorMax =
                new Vector2(.97f, chosen == null ? .66f : .30f);
        if (_buildPosition != null) _buildPosition.text = matches.Length == 0 ? string.Empty :
            $"{_buildIndex + 1}/{matches.Length} · {NativeUi.Text("Matched", "匹配")} " +
            $"{chosen!.MatchedCards}/{_selectedCandidates.Count}";
        if (_previousBuildButton != null) _previousBuildButton.interactable = matches.Length > 1;
        if (_nextBuildButton != null) _nextBuildButton.interactable = matches.Length > 1;
        var lines = new List<string>
        {
            $"<b>{TheBazaar.Utilities.LocalizableShared.GetLocalizedHero(player.Hero)}</b> · {NativeUi.Text("Board/Stash/Shop", "场上／仓库／商店")} " +
            $"{board.Count}/{stash.Count}/{shop.Count} · " +
            $"{NativeUi.Text("Candidates", "候选")} {_selectedCandidates.Count} · " +
            $"{NativeUi.Text("Data generated", "数据生成于")} {corpus.GeneratedAt:yyyy-MM-dd}"
        };
        if (board.Count + stash.Count + shop.Count == 0)
            lines.Add(NativeUi.Text("No items are available to select.", "目前没有可选择的物品。"));
        else if (_selectedCandidates.Count == 0)
            lines.Add(NativeUi.Text("Tap a card above to select a candidate.", "点击上方物品，选择用于查询的候选卡。"));
        else if (corpus.CountForHero(hero) == 0)
            lines.Add(NativeUi.Text("No ten-win build data is available for this hero yet.",
                "当前构筑库尚无该英雄的十胜数据。"));
        else if (matches.Length == 0)
            lines.Add(NativeUi.Text("No matching ten-win builds in the current data.", "当前数据中没有匹配的十胜构筑。"));
        if (chosen != null)
        {
            var build = chosen.Build;
            if (!string.IsNullOrWhiteSpace(_itemDetail))
                lines.Add("\n" + _itemDetail);
            lines.Add($"\n<b>#{_buildIndex + 1} · {NativeUi.Text("Matched", "匹配")} {chosen.MatchedCards}/{_selectedCandidates.Count} " +
                $"· {NativeUi.Text("Ten-win runs", "十胜场次")} {build.TenWinRuns} " +
                $"· {build.TenWinRateBps / 100.0:0.#}%</b>");
            foreach (var item in build.Items.OrderBy(value => value.Slot))
            {
                var name = ResolveName(item.TemplateId);
                var owned = board.Contains(item.TemplateId) ? NativeUi.Text("Board", "场上")
                    : stash.Contains(item.TemplateId) ? NativeUi.Text("Stash", "仓库")
                    : shop.Contains(item.TemplateId) ? NativeUi.Text("Shop", "商店")
                    : NativeUi.Text("Missing", "未拥有");
                lines.Add($"{item.Slot + 1:00} · {name} · {owned} · {item.Tier} " +
                    (string.IsNullOrWhiteSpace(item.Enchantment) ? string.Empty : $"· {item.Enchantment}"));
            }
        }
        Show(string.Join("\n", lines));
    }

    private static string ResolveName(Guid id)
    {
        var nativeId = Il2CppSystem.Guid.Parse(id.ToString());
        var template = Data.GetStatic()?.GetCardById(nativeId)?.TryCast<TCardBase>();
        var title = template?.Localization?.Title;
        return title == null ? template?.InternalName ?? id.ToString()
            : TooltipExtensions.GetLocalizedText(title);
    }

    private void UpdateCandidateButtons()
    {
        var candidates = ActiveCandidates();
        var pageCount = Math.Max(1, (candidates.Count + _candidateButtons.Count - 1) / _candidateButtons.Count);
        _candidatePage = Math.Min(_candidatePage, pageCount - 1);
        if (_sourceButton != null)
            _sourceButton.GetComponentInChildren<TextMeshProUGUI>().text = _source switch
            {
                CandidateSource.Board => NativeUi.Text("Board", "场上") + $" ({_boardCandidates.Count})",
                CandidateSource.Stash => NativeUi.Text("Stash", "仓库") + $" ({_stashCandidates.Count})",
                _ => NativeUi.Text("Shop", "商店") + $" ({_shopCandidates.Count})",
            };
        if (_candidatePageButton != null)
        {
            _candidatePageButton.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{_candidatePage + 1}/{pageCount}";
            _candidatePageButton.interactable = pageCount > 1;
        }
        for (var slot = 0; slot < _candidateButtons.Count; slot++)
        {
            var button = _candidateButtons[slot];
            var index = _candidatePage * _candidateButtons.Count + slot;
            if (index >= candidates.Count)
            {
                _candidateCards.Remove(button);
                button.gameObject.SetActive(false);
                continue;
            }
            var candidate = candidates[index];
            var selected = _selectedCandidates.Contains(candidate.Id);
            button.GetComponentInChildren<TextMeshProUGUI>().text = candidate.Name;
            var marker = _candidateMarkers[slot];
            marker.text = selected ? "✓" : "○";
            marker.color = selected ? new Color(.65f, 1, .71f, 1) : new Color(.80f, .83f, .85f, 1);
            button.GetComponent<Image>().color = selected
                ? new Color(.19f, .32f, .25f, 1) : new Color(.15f, .20f, .24f, 1);
            button.gameObject.SetActive(true);
            _candidateCards.Show(button, candidate.Id, captionHeight: .22f);
            marker.transform.parent.SetAsLastSibling();
        }
    }

    private void ToggleCandidate(int slot)
    {
        var candidates = ActiveCandidates();
        var index = _candidatePage * _candidateButtons.Count + slot;
        if (slot < 0 || index >= candidates.Count) return;
        var id = candidates[index].Id;
        if (!_selectedCandidates.Add(id)) _selectedCandidates.Remove(id);
        _itemDetail = null;
        _nextRefresh = 0;
    }

    private List<(Guid Id, string Name)> ActiveCandidates() => _source switch
    {
        CandidateSource.Board => _boardCandidates,
        CandidateSource.Stash => _stashCandidates,
        _ => _shopCandidates,
    };

    private void CycleSource()
    {
        _source = (CandidateSource)(((int)_source + 1) % 3);
        _candidatePage = 0;
        UpdateCandidateButtons();
        _nextRefresh = 0;
    }

    private void NextCandidatePage()
    {
        var pageCount = Math.Max(1, (ActiveCandidates().Count + _candidateButtons.Count - 1)
            / _candidateButtons.Count);
        _candidatePage = (_candidatePage + 1) % pageCount;
        UpdateCandidateButtons();
    }

    private void PreviousBuild()
    {
        if (_matches.Length < 2) return;
        _buildIndex = (_buildIndex + _matches.Length - 1) % _matches.Length;
        _itemDetail = null;
        _lastDescription = null;
        _nextRefresh = 0;
    }

    private void NextBuild()
    {
        if (_matches.Length < 2) return;
        _buildIndex = (_buildIndex + 1) % _matches.Length;
        _itemDetail = null;
        _lastDescription = null;
        _nextRefresh = 0;
    }

    private void ShowItemDetail(string detail)
    {
        _itemDetail = detail;
        _lastDescription = null;
        _nextRefresh = 0;
    }

    private void Show(string description)
    {
        if (_text == null || _content == null || _scroll == null) return;
        if (_lastDescription == description) return;
        _lastDescription = description;
        NativeUi.RichDescription(_text, description);
        var height = _text.GetPreferredValues(description, 1650f, 0).y + 40;
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 820));
        _scroll.verticalNormalizedPosition = 1;
    }

    private void SetOpen(bool open)
    {
        if (!open)
        {
            _board?.Hide();
            _candidateCards.Hide();
            _matches = Array.Empty<AndroidBuildMatch>();
        }
        _open = open;
        AnyOpen = open;
        if (_panel != null) _panel.gameObject.SetActive(open);
        UpdateEntryVisibility();
        if (open)
        {
            _nextRefresh = 0;
            _lastDescription = null;
            _selectionInitialized = false;
            _selectedCandidates.Clear();
            _itemDetail = null;
            _source = CandidateSource.Board;
            _candidatePage = 0;
        }
    }

    private void UpdateEntryVisibility()
    {
        var visible = !_open && !CollectionController.AnyOpen && Data.Run?.Player != null;
        if (_entry != null) _entry.gameObject.SetActive(visible);
        if (_previewModeButton != null) _previewModeButton.gameObject.SetActive(visible);
    }

    private static string PreviewModeLabel() => AndroidLongPressPreviewSettings.Mode switch
    {
        AndroidLongPressPreviewMode.Upgrade => NativeUi.Text("Hold: Upgrade", "长按预览：升级"),
        AndroidLongPressPreviewMode.None => NativeUi.Text("Hold: None", "长按预览：无"),
        _ => NativeUi.Text("Hold: Enchants", "长按预览：附魔"),
    };

    private void TogglePreviewMode()
    {
        AndroidLongPressPreviewSettings.Cycle();
        if (_previewModeButton != null)
            _previewModeButton.GetComponentInChildren<TextMeshProUGUI>().text = PreviewModeLabel();
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.default_mode mode={AndroidLongPressPreviewSettings.Mode}");
    }

    public void OnDestroy()
    {
        AnyOpen = false;
        _board?.Hide();
        _candidateCards.Hide();
        if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
    }
}
