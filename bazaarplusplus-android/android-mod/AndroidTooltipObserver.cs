using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Event;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Localization;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.UI.Tooltips;
using TheBazaar.Localization;
using TheBazaar.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidTooltipObserver(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static RectTransform? FocusPanel { get; private set; }
    // Native item details include a cooldown clock protruding to the left of
    // their frame. Reserve that space while keeping the preview 370 units wide.
    private const float PreviewRightInset = 640;
    private const float PreviewWidth = 370;
    private static readonly Color PanelBackground = new(.07f, .05f, .035f, .96f);
    private static readonly Color Frame = new(.88f, .70f, .39f, .85f);
    // Scroll viewport inset inside the frame; the right inset holds the scroll thumb.
    private static readonly Vector2 ViewportMin = new(4, 8);
    private static readonly Vector2 ViewportMax = new(-12, -8);
    // ScrollText's label adds 12 units of horizontal padding on each side.
    private const float TextWidth = PreviewWidth - 16 - 24;
    private static AndroidTooltipObserver? _instance;

    private float _nextScan;
    private string? _language;
    private bool _wasHoldPreview;
    private int _holdRevision;
    private IntPtr _lastCard;
    private int _lastController;
    private bool _lastLocked;
    private int _lastLevel = -1;
    private int _lastItemState;
    private AndroidLongPressPreviewMode? _lastLongPressMode;
    private GameObject? _canvasRoot;
    private RectTransform? _panel;
    private Image? _panelBackground;
    private Outline? _panelFrame;
    private RectTransform? _content;
    private TextMeshProUGUI? _text;
    private ScrollRect? _scroll;
    private RectTransform? _scrollThumb;
    private GameObject? _moreHint;
    private AndroidMonsterCardGallery? _monsterGallery;
    private GameObject? _monsterBackdrop;
    private Button? _monsterClose;
    private bool _monsterPinned;
    private int _monsterScene;
    private IntPtr _dismissedMonster;
    private IntPtr _dismissedGalleryItem;
    private ItemCard? _currentItem;
    private EventEncounterCard? _currentEvent;
    private CombatEncounterCard? _currentMonster;
    private CardTooltipData? _currentCardData;
    private HeroLevelTooltipData? _currentLevelData;
    private CardTooltipController? _currentController;
    private PreviewMode _mode;
    private static bool _reportedFailure;
    private bool _historySuppressed;
    // Post-combat recap cards show combat impact instead of the enchant/upgrade preview.
    private const float ImpactHeaderHeight = 64;
    private string? _impactCard;
    private bool _impactReceived;
    private int _lastImpactState;
    private GameObject? _impactHeader;
    private Button? _causedButton;
    private Button? _receivedButton;
    private RectTransform? _viewport;

    private enum PreviewMode { Enchant, Upgrade }

    public void Update()
    {
        if (AndroidHistoryPanel.AnyOpen)
        {
            if (!_historySuppressed) Hide();
            _historySuppressed = true; return;
        }
        _historySuppressed = false;
        var holdPreview = AndroidHoldPreview.Active;
        // Monster galleries have their own backdrop/close controls and outlive
        // the native tooltip. A new hold target outside the gallery retires it.
        if (_holdRevision != AndroidHoldPreview.Revision
            && !(_monsterPinned && AndroidHoldPreview.TargetInFocusPanel)) Hide();
        else if (_wasHoldPreview && !holdPreview)
        {
            if (!_monsterPinned) Hide();
            else Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.monster_preview.retained_after_hold");
        }
        _holdRevision = AndroidHoldPreview.Revision;
        _wasHoldPreview = holdPreview;
        _monsterGallery?.Tick();
        RefreshScrollCue();
        if (Time.realtimeSinceStartup < _nextScan) return;
        _nextScan = Time.realtimeSinceStartup + .25f;
        try
        {
            var language = PlayerPreferences.Data?.LanguageCode;
            if (_language != language)
            {
                _language = language;
                // Retire native descriptions and in-flight gallery cards in the old language.
                Hide();
            }
            // Standard input already shows monster boards natively; the gallery is advanced-input only.
            var advancedInput = PlayerPreferences.Data?.AlternativeInput == true;
            if (_monsterPinned && (!advancedInput || Data.IsInCombat
                || SceneManager.GetActiveScene().handle != _monsterScene))
                Hide();
            var visible = false;
            var recap = Singleton<BoardManager>.Instance?.IsRecapViewOpen == true;
            // The recap tooltip stays open under our panels, and the left-edge
            // overlay sorts above them.
            var recapCovered = recap && (CollectionController.AnyOpen || AndroidBuildPanelController.AnyOpen);
            foreach (var controller in Resources.FindObjectsOfTypeAll<CardTooltipController>())
            {
                if (controller == null || !controller.isActiveAndEnabled) continue;
                var data = controller.CurrentTooltipData?.TryCast<CardTooltipData>();
                if (data != null && AndroidHoldPreview.SuppressRetired(data)) continue;
                var levelData = controller.CurrentTooltipData?.TryCast<HeroLevelTooltipData>();
                var card = data?.CardInstance;
                var item = card?.TryCast<ItemCard>();
                var skill = recap && item == null ? card?.TryCast<SkillCard>() : null;
                var eventCard = item == null && skill == null ? card?.TryCast<EventEncounterCard>() : null;
                var monsterCard = item == null && skill == null && eventCard == null && advancedInput
                    ? card?.TryCast<CombatEncounterCard>() : null;
                if (item == null && skill == null && eventCard == null && monsterCard == null && levelData == null) continue;
                if (recapCovered && (item != null || skill != null)) continue;
                if (_monsterPinned && monsterCard == null) continue;
                var impactCard = recap && (item != null || skill != null) ? card!.InstanceId.Value : null;
                var id = controller.GetInstanceID();
                var locked = controller.IsLocked;
                // Advanced mobile input can keep the live tooltip unlocked while
                // the card is inspected; standard mode uses the locked long press.
                // A recap tap opens the tooltip unlocked in both modes.
                if (levelData == null && impactCard == null && !locked && !AndroidHoldPreview.Active && !advancedInput) continue;
                var longPressMode = AndroidLongPressPreviewSettings.Mode;
                if (item != null && impactCard == null && longPressMode == AndroidLongPressPreviewMode.None)
                {
                    Hide();
                    continue;
                }
                visible = true;
                var pointer = card?.Pointer ?? levelData!.Pointer;
                if (monsterCard != null && pointer == _dismissedMonster) continue;
                if (item != null && pointer == _dismissedGalleryItem) continue;
                var currentLevel = levelData?.GetCurrentAndNextLevel().Item1 ?? -1;
                var itemState = item == null || impactCard != null ? 0 : HashCode.Combine(item.Tier,
                    NativeNullableValue.Read<EEnchantmentType>(item, "get_Enchantment"),
                    AndroidEnchantPreview.AttributeDigest(item));
                var impactState = impactCard == null ? 0 : HashCode.Combine(impactCard,
                    AndroidCombatImpactCapture.Generation, AndroidCombatImpactCapture.Projecting, _impactReceived);
                if (_lastController == id && _lastCard == pointer && _lastLocked == locked
                    && _lastLevel == currentLevel && _lastItemState == itemState
                    && _lastLongPressMode == longPressMode && _lastImpactState == impactState) continue;
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.observed controller={id} locked={locked} card={card?.TemplateId} level={currentLevel}");
                if (_panel == null) BuildUi(controller);
                if (_panel == null) continue;
                if (_lastCard != pointer || _lastLongPressMode != longPressMode)
                    _mode = item != null && longPressMode == AndroidLongPressPreviewMode.Upgrade && item.CanCardUpgrade()
                        ? PreviewMode.Upgrade : PreviewMode.Enchant;
                if (impactCard != _impactCard)
                {
                    _impactCard = impactCard;
                    _impactReceived = false;
                    impactState = impactCard == null ? 0 : HashCode.Combine(impactCard,
                        AndroidCombatImpactCapture.Generation, AndroidCombatImpactCapture.Projecting, false);
                }
                _currentItem = item;
                _currentEvent = eventCard;
                _currentMonster = monsterCard;
                _currentCardData = data;
                _currentLevelData = levelData;
                _currentController = controller;
                if (!Render()) { Hide(); continue; }
                _lastController = id;
                _lastCard = pointer;
                _lastLocked = locked;
                _lastLevel = currentLevel;
                _lastItemState = itemState;
                _lastLongPressMode = longPressMode;
                _lastImpactState = impactState;
                break;
            }
            if (!visible)
            {
                if (!_monsterPinned) Hide();
                _dismissedMonster = IntPtr.Zero;
                _dismissedGalleryItem = IntPtr.Zero;
            }
        }
        catch (Exception error)
        {
            Hide();
            if (_reportedFailure) return;
            _reportedFailure = true;
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.tooltip.observe_failed exception={error}");
        }
    }

    private bool Render()
    {
        var impact = _impactCard != null;
        if ((_currentItem == null && _currentEvent == null && _currentMonster == null && _currentLevelData == null && !impact)
            || _currentController == null || _text == null
            || _content == null || _scroll == null || _panel == null) return false;
        var preview = impact ? ImpactText(_impactCard!)
            : _currentLevelData != null
            ? AndroidEventPreview.BuildLevelRewards(_currentLevelData.GetCurrentAndNextLevel().Item1)
            : _currentMonster != null ? AndroidMonsterPreview.Build(_currentMonster, _currentCardData!)
            : _currentEvent != null ? AndroidEventPreview.Build(_currentEvent)
            : _mode == PreviewMode.Upgrade ? AndroidUpgradePreview.Build(_currentItem!)
            : AndroidEnchantPreview.Build(_currentItem!);
        ShowImpactHeader(impact);
        if (!impact && _currentItem != null && AndroidQuestRewardPreview.HasRewards(_currentItem))
        {
            var rewards = AndroidQuestRewardPreview.Build(_currentItem);
            if (!string.IsNullOrWhiteSpace(rewards))
                preview = AndroidPreviewText.Sections([preview, rewards]);
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.preview_ready mode={_mode} characters={preview.Length}");
        if (string.IsNullOrWhiteSpace(preview))
        {
            if (_currentItem != null && _mode == PreviewMode.Enchant
                && !Data.IsInCombat && (_currentItem.CanCardUpgrade() ||
                    AndroidQuestRewardPreview.HasRewards(_currentItem)))
                preview = NativeUi.Text("No enchantment preview for this item.", "此物品暂无附魔预览。");
            else return false;
        }
        var leftEdge = (_currentItem != null || impact) && AndroidLauncherSettings.LeftEdge;
        var nativeCanvas = _currentController.GetComponentInParent<Canvas>();
        var overlayCanvas = _canvasRoot?.GetComponent<Canvas>();
        if (nativeCanvas != null && overlayCanvas != null)
            // Left placement overlaps the collection/build entry canvases (30000/30001).
            overlayCanvas.sortingOrder = leftEdge ? Math.Max(30002, nativeCanvas.sortingOrder + 1)
                : nativeCanvas.sortingOrder + 1;
        var monsterPreview = _currentMonster != null;
        // The panel Image stays a raycast target while clear so taps on gaps do not dismiss.
        if (_panelBackground != null) _panelBackground.color = monsterPreview ? Color.clear : PanelBackground;
        if (_panelFrame != null) _panelFrame.enabled = !monsterPreview;
        _panel.anchorMin = _panel.anchorMax = Vector2.right;
        if (monsterPreview)
        {
            _monsterGallery?.Show(AndroidMonsterPreview.GetMonster(_currentMonster!, _currentCardData!));
            _scroll.gameObject.SetActive(false);
            _monsterBackdrop?.SetActive(true);
            _monsterClose?.gameObject.SetActive(true);
            _monsterPinned = true;
            _monsterScene = SceneManager.GetActiveScene().handle;
        }
        else
        {
            if (leftEdge) _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            // Keep a small touch margin inside the device safe area (notch/navigation).
            var left = leftEdge ? 16 + Screen.safeArea.xMin * 1080f / Screen.height
                : -PreviewRightInset - PreviewWidth;
            _panel.offsetMin = new Vector2(left, 45);
            _panel.offsetMax = new Vector2(left + PreviewWidth, 1000);
            if (_currentItem != null)
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.preview.placement position={(leftEdge ? "left" : "default")} x={left}");
            _scroll.gameObject.SetActive(true);
            _monsterBackdrop?.SetActive(false);
            _monsterClose?.gameObject.SetActive(false);
            _monsterGallery?.Hide();
            _monsterPinned = false;
        }
        if (!monsterPreview)
        {
            NativeUi.RichDescription(_text, preview);
            var height = _text.GetPreferredValues(preview, TextWidth, 0).y + 32;
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var header = impact ? ImpactHeaderHeight : 0;
            _panel.offsetMin = new Vector2(_panel.offsetMin.x,
                Mathf.Max(45, 1000 - height - header - (ViewportMin.y - ViewportMax.y)));
            _scroll.verticalNormalizedPosition = 1;
        }
        _panel.gameObject.SetActive(true);
        FocusPanel = _panel;
        return true;
    }

    private void BuildUi(CardTooltipController controller)
    {
        var font = NotoFontFallbackRuntime._loadedSansPrimary;
        if (font == null) return;
        var canvas = controller.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.canvas order={canvas.sortingOrder} mode={canvas.renderMode}");
        _canvasRoot = new GameObject("BPP.EnchantCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        try
        {
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);
            var overlay = _canvasRoot.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = canvas.sortingOrder + 1;
            var scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            _canvasRoot.AddComponent<GraphicRaycaster>();
            var backdrop = NativeUi.Rect("BPP.MonsterBackdrop", _canvasRoot.transform,
                Vector2.zero, Vector2.one);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0, 0, 0, .001f);
            backdropImage.raycastTarget = true;
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            backdropButton.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(DismissMonster));
            _monsterBackdrop = backdrop.gameObject;
            _monsterBackdrop.SetActive(false);
            _panel = NativeUi.Rect("BPP.EnchantPreview", _canvasRoot.transform, Vector2.right, Vector2.right,
                new Vector2(-PreviewRightInset - PreviewWidth, 45), new Vector2(-PreviewRightInset, 1000));
            _panelBackground = _panel.gameObject.AddComponent<Image>();
            _panelBackground.color = PanelBackground;
            _panelBackground.raycastTarget = true;
            _panelFrame = _panel.gameObject.AddComponent<Outline>();
            _panelFrame.effectColor = Frame;
            _panelFrame.effectDistance = new Vector2(1.5f, -1.5f);
            _panelFrame.useGraphicAlpha = false;
            (_text, _content, _scroll) = NativeUi.ScrollText(_panel, font, Vector2.zero, Vector2.one);
            var viewport = _scroll.GetComponent<RectTransform>();
            viewport.offsetMin = ViewportMin;
            viewport.offsetMax = ViewportMax;
            _viewport = viewport;
            BuildImpactHeader(font);
            _text.fontSize = 24;
            _text.enableAutoSizing = false;
            _text.color = ParseColor(AndroidPreviewText.BodyColor);
            _text.lineSpacing = 4;
            BuildScrollCue(font);
            _instance = this;
            _monsterGallery = new AndroidMonsterCardGallery(_panel, font);
            _monsterClose = NativeUi.Button(_panel, font, "×", Vector2.one, Vector2.one,
                DismissMonster, new Vector2(-54, -42), new Vector2(-8, -4));
            _monsterClose.gameObject.SetActive(false);
            _panel.gameObject.SetActive(false);
        }
        catch
        {
            UnityEngine.Object.Destroy(_canvasRoot);
            _canvasRoot = null;
            _panel = _content = null;
            _panelBackground = null;
            _panelFrame = null;
            _text = null;
            _scroll = null;
            _scrollThumb = null;
            _moreHint = null;
            _monsterGallery = null;
            _monsterBackdrop = null;
            _monsterClose = null;
            _impactHeader = null;
            _causedButton = _receivedButton = null;
            _viewport = null;
            throw;
        }
    }

    private void BuildImpactHeader(TMP_FontAsset font)
    {
        var header = NativeUi.Rect("BPP.ImpactHeader", _panel!, new Vector2(0, 1), Vector2.one,
            new Vector2(10, -ImpactHeaderHeight), new Vector2(-14, -8));
        _causedButton = NativeUi.Button(header, font, () => NativeUi.Text("Caused", "造成"),
            Vector2.zero, new Vector2(.5f, 1), () => SetImpactPerspective(false), Vector2.zero, new Vector2(-4, 0));
        _receivedButton = NativeUi.Button(header, font, () => NativeUi.Text("Received", "承受"),
            new Vector2(.5f, 0), Vector2.one, () => SetImpactPerspective(true), new Vector2(4, 0), Vector2.zero);
        foreach (var button in new[] { _causedButton, _receivedButton })
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 24;
            label.enableAutoSizing = false;
            AndroidPanelButtonSkin.Register(button, button == _causedButton);
        }
        _impactHeader = header.gameObject;
        _impactHeader.SetActive(false);
    }

    private void ShowImpactHeader(bool impact)
    {
        if (_impactHeader == null || _viewport == null) return;
        _impactHeader.SetActive(impact);
        _viewport.offsetMax = impact ? new Vector2(ViewportMax.x, ViewportMax.y - ImpactHeaderHeight + 4) : ViewportMax;
        if (!impact) return;
        AndroidPanelButtonSkin.SetSelected(_causedButton, !_impactReceived);
        AndroidPanelButtonSkin.SetSelected(_receivedButton, _impactReceived);
    }

    private void SetImpactPerspective(bool received)
    {
        if (_impactCard == null || _impactReceived == received) return;
        _impactReceived = received;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.perspective card={_impactCard} received={received}");
        if (!Render()) Hide();
        _lastImpactState = HashCode.Combine(_impactCard, AndroidCombatImpactCapture.Generation,
            AndroidCombatImpactCapture.Projecting, _impactReceived);
    }

    private string ImpactText(string card)
    {
        var chinese = LanguageCodeMatcher.IsChinese(L.CurrentLanguageCode);
        var report = AndroidCombatImpactCapture.Report;
        var body = AndroidCombatImpactCapture.Projecting
            ? AndroidPreviewText.Muted(NativeUi.Text("Analyzing this combat…", "正在分析本场战斗…"))
            : report.Contains(card) ? AndroidCombatImpactText.Build(report, card, _impactReceived, chinese)
            : AndroidPreviewText.Muted(NativeUi.Text("No analysis for this combat.", "本场战斗暂无分析数据。"));
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.rendered card={card} received={_impactReceived} projecting={AndroidCombatImpactCapture.Projecting} known={report.Contains(card)} caused={report.Caused(card).Count} incoming={report.Received(card).Count}");
        return AndroidPreviewText.Lines(AndroidPreviewText.Heading(NativeUi.Text("Combat impact", "战斗影响")), body);
    }

    // The thumb and hint only report position; dragging stays with the ScrollRect
    // (or the hold-preview finger), so neither intercepts raycasts.
    private void BuildScrollCue(TMP_FontAsset font)
    {
        var track = NativeUi.Rect("BPP.PreviewScrollTrack", _panel!, new Vector2(1, 0), Vector2.one,
            new Vector2(-8, 10), new Vector2(-5, -10));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1, 1, 1, .08f);
        trackImage.raycastTarget = false;
        _scrollThumb = NativeUi.Rect("BPP.PreviewScrollThumb", track, Vector2.zero, Vector2.one);
        var thumb = _scrollThumb.gameObject.AddComponent<Image>();
        thumb.color = Frame;
        thumb.raycastTarget = false;
        var hint = NativeUi.Rect("BPP.PreviewMoreHint", _panel!, Vector2.zero, new Vector2(1, 0),
            new Vector2(4, 8), new Vector2(-12, 42));
        var hintImage = hint.gameObject.AddComponent<Image>();
        hintImage.color = new Color(PanelBackground.r, PanelBackground.g, PanelBackground.b, .9f);
        hintImage.raycastTarget = false;
        var label = NativeUi.Label(hint, font, () => NativeUi.Text("▼ Swipe up for more", "▼ 上滑查看更多"), 18);
        label.color = ParseColor(AndroidPreviewText.MutedColor);
        _moreHint = hint.gameObject;
        track.gameObject.SetActive(false);
        _moreHint.SetActive(false);
    }

    private void RefreshScrollCue()
    {
        if (_scroll == null || _content == null || _scrollThumb == null || _moreHint == null) return;
        var track = _scrollThumb.parent.gameObject;
        // Monster details sit above the card gallery, which the bottom hint would cover.
        var visible = _panel != null && _panel.gameObject.activeSelf && _scroll.gameObject.activeSelf && !_monsterPinned;
        var viewport = _scroll.viewport.rect.height;
        var content = _content.rect.height;
        var overflow = visible && content > viewport + 1;
        if (track.activeSelf != overflow) track.SetActive(overflow);
        var more = overflow && _scroll.verticalNormalizedPosition > .01f;
        if (_moreHint.activeSelf != more) _moreHint.SetActive(more);
        if (!overflow) return;
        var size = Mathf.Clamp(viewport / content, .08f, 1);
        var top = 1 - (1 - Mathf.Clamp01(_scroll.verticalNormalizedPosition)) * (1 - size);
        _scrollThumb.anchorMin = new Vector2(0, top - size);
        _scrollThumb.anchorMax = new Vector2(1, top);
        _scrollThumb.offsetMin = _scrollThumb.offsetMax = Vector2.zero;
    }

    // Hold preview keeps an input shield above this panel, so it forwards a
    // finger that starts on the panel as a scroll distance in screen pixels.
    internal static void ScrollBy(float screenDelta)
    {
        var observer = _instance;
        if (observer?._scroll == null || observer._content == null || FocusPanel == null) return;
        var scale = observer._canvasRoot?.GetComponent<Canvas>()?.scaleFactor ?? 1;
        var limit = Mathf.Max(0, observer._content.rect.height - observer._scroll.viewport.rect.height);
        var position = observer._content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + screenDelta / Mathf.Max(scale, .01f), 0, limit);
        observer._scroll.StopMovement();
        observer._content.anchoredPosition = position;
    }

    private static Color ParseColor(string hex) =>
        ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.white;

    private void Hide()
    {
        FocusPanel = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
        _monsterGallery?.Hide();
        _monsterBackdrop?.SetActive(false);
        _monsterPinned = false;
        _lastController = 0;
        _lastCard = IntPtr.Zero;
        _lastLocked = false;
        _lastLevel = -1;
        _lastItemState = 0;
        _lastLongPressMode = null;
        _currentItem = null;
        _currentEvent = null;
        _currentMonster = null;
        _currentCardData = null;
        _currentLevelData = null;
        _currentController = null;
        _mode = PreviewMode.Enchant;
        _impactCard = null;
        _impactReceived = false;
        _lastImpactState = 0;
        ShowImpactHeader(false);
    }

    private void DismissMonster()
    {
        _dismissedMonster = _lastCard;
        foreach (var controller in Resources.FindObjectsOfTypeAll<CardTooltipController>())
        {
            if (controller == null || !controller.isActiveAndEnabled) continue;
            var item = controller.CurrentTooltipData?.TryCast<CardTooltipData>()?.CardInstance?.TryCast<ItemCard>();
            if (item != null) _dismissedGalleryItem = item.Pointer;
        }
        Hide();
    }

    public void OnDestroy()
    {
        FocusPanel = null;
        if (_instance == this) _instance = null;
        if (_canvasRoot != null) UnityEngine.Object.Destroy(_canvasRoot);
    }
}
