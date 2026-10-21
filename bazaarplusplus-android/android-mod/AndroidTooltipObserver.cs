using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Event;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
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
    private const string LongPressUpgradePreference = "BPP.Android.LongPressUpgrade";
    private static bool? _upgradeOnLongPress;
    internal static bool UpgradeOnLongPress
    {
        get => _upgradeOnLongPress ??= PlayerPrefs.GetInt(LongPressUpgradePreference, 0) == 1;
        set
        {
            _upgradeOnLongPress = value;
            PlayerPrefs.SetInt(LongPressUpgradePreference, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private float _nextScan;
    private IntPtr _lastCard;
    private int _lastController;
    private bool _lastLocked;
    private int _lastLevel = -1;
    private int _lastItemState;
    private GameObject? _canvasRoot;
    private RectTransform? _panel;
    private RectTransform? _content;
    private TextMeshProUGUI? _text;
    private ScrollRect? _scroll;
    private AndroidMonsterCardGallery? _monsterGallery;
    private GameObject? _monsterBackdrop;
    private Button? _monsterClose;
    private bool _monsterPinned;
    private bool _monsterDetailVisible;
    private int _monsterScene;
    private IntPtr _dismissedMonster;
    private IntPtr _dismissedGalleryItem;
    private Button? _enchantButton;
    private Button? _upgradeButton;
    private Button? _questButton;
    private ItemCard? _currentItem;
    private EventEncounterCard? _currentEvent;
    private CombatEncounterCard? _currentMonster;
    private CardTooltipData? _currentCardData;
    private HeroLevelTooltipData? _currentLevelData;
    private CardTooltipController? _currentController;
    private PreviewMode _mode;
    private static bool _reportedFailure;

    private enum PreviewMode { Enchant, Upgrade, Quest }

    public void Update()
    {
        _monsterGallery?.Tick();
        if (Time.realtimeSinceStartup < _nextScan) return;
        _nextScan = Time.realtimeSinceStartup + .25f;
        try
        {
            if (_monsterPinned && (Data.IsInCombat || SceneManager.GetActiveScene().handle != _monsterScene))
                Hide();
            var visible = false;
            foreach (var controller in Resources.FindObjectsOfTypeAll<CardTooltipController>())
            {
                if (controller == null || !controller.isActiveAndEnabled) continue;
                var data = controller.CurrentTooltipData?.TryCast<CardTooltipData>();
                var levelData = controller.CurrentTooltipData?.TryCast<HeroLevelTooltipData>();
                var card = data?.CardInstance;
                var item = card?.TryCast<ItemCard>();
                var eventCard = item == null ? card?.TryCast<EventEncounterCard>() : null;
                var monsterCard = item == null && eventCard == null ? card?.TryCast<CombatEncounterCard>() : null;
                if (item == null && eventCard == null && monsterCard == null && levelData == null) continue;
                if (_monsterPinned && monsterCard == null) continue;
                var id = controller.GetInstanceID();
                var locked = controller.IsLocked;
                // Advanced mobile input can keep the live tooltip unlocked while
                // the card is inspected; standard mode uses the locked long press.
                if (levelData == null && !locked && PlayerPreferences.Data?.AlternativeInput != true) continue;
                visible = true;
                var pointer = card?.Pointer ?? levelData!.Pointer;
                if (monsterCard != null && pointer == _dismissedMonster) continue;
                if (item != null && pointer == _dismissedGalleryItem) continue;
                var currentLevel = levelData?.GetCurrentAndNextLevel().Item1 ?? -1;
                var itemState = item == null ? 0 : HashCode.Combine(item.Tier,
                    NativeNullableValue.Read<EEnchantmentType>(item, "get_Enchantment"),
                    AndroidEnchantPreview.AttributeDigest(item));
                if (_lastController == id && _lastCard == pointer && _lastLocked == locked
                    && _lastLevel == currentLevel && _lastItemState == itemState) continue;
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.observed controller={id} locked={locked} card={card?.TemplateId} level={currentLevel}");
                if (_panel == null) BuildUi(controller);
                if (_panel == null) continue;
                if (_lastCard != pointer)
                    _mode = item != null && UpgradeOnLongPress && item.CanCardUpgrade()
                        ? PreviewMode.Upgrade : PreviewMode.Enchant;
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
        if ((_currentItem == null && _currentEvent == null && _currentMonster == null && _currentLevelData == null)
            || _currentController == null || _text == null
            || _content == null || _scroll == null || _panel == null) return false;
        var preview = _currentLevelData != null
            ? AndroidEventPreview.BuildLevelRewards(_currentLevelData.GetCurrentAndNextLevel().Item1)
            : _currentMonster != null ? AndroidMonsterPreview.Build(_currentMonster, _currentCardData!)
            : _currentEvent != null ? AndroidEventPreview.Build(_currentEvent)
            : _mode == PreviewMode.Upgrade ? AndroidUpgradePreview.Build(_currentItem!)
            : _mode == PreviewMode.Quest ? AndroidQuestRewardPreview.Build(_currentItem!)
            : AndroidEnchantPreview.Build(_currentItem!);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.tooltip.preview_ready mode={_mode} characters={preview.Length}");
        if (string.IsNullOrWhiteSpace(preview))
        {
            if (_currentItem != null && _mode == PreviewMode.Enchant
                && !Data.IsInCombat && (_currentItem.CanCardUpgrade() ||
                    AndroidQuestRewardPreview.HasRewards(_currentItem)))
                preview = NativeUi.Text("No enchantment preview for this item.", "此物品暂无附魔预览。");
            else return false;
        }
        var nativeCanvas = _currentController.GetComponentInParent<Canvas>();
        var overlayCanvas = _canvasRoot?.GetComponent<Canvas>();
        if (nativeCanvas != null && overlayCanvas != null)
            overlayCanvas.sortingOrder = nativeCanvas.sortingOrder + 1;
        var monsterPreview = _currentMonster != null;
        if (monsterPreview)
        {
            if (_lastCard != _currentMonster!.Pointer) _monsterDetailVisible = false;
            _monsterGallery?.Show(AndroidMonsterPreview.GetMonster(_currentMonster, _currentCardData!));
            _scroll.gameObject.SetActive(_monsterDetailVisible);
            _monsterBackdrop?.SetActive(true);
            _monsterClose?.gameObject.SetActive(true);
            _monsterPinned = true;
            _monsterScene = SceneManager.GetActiveScene().handle;
        }
        else
        {
            _panel.offsetMin = new Vector2(-930, 45);
            _panel.offsetMax = new Vector2(-560, 1000);
            _scroll.gameObject.SetActive(true);
            _monsterBackdrop?.SetActive(false);
            _monsterClose?.gameObject.SetActive(false);
            _monsterGallery?.Hide();
            _monsterPinned = false;
        }
        NativeUi.RichDescription(_text, monsterPreview ? string.Empty : preview);
        var scrollRect = _scroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = monsterPreview ? Vector2.up : Vector2.zero;
        scrollRect.anchorMax = monsterPreview ? Vector2.one : new Vector2(1, .9f);
        if (monsterPreview)
        {
            scrollRect.pivot = new Vector2(.5f, 1);
            scrollRect.sizeDelta = new Vector2(0, 310);
            scrollRect.anchoredPosition = new Vector2(0, -36);
        }
        else
        {
            scrollRect.pivot = new Vector2(.5f, .5f);
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;
        }
        var height = _text.GetPreferredValues(preview, monsterPreview ? 600f : 330f, 0).y + 32;
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(height, monsterPreview ? 300 : 850));
        _scroll.verticalNormalizedPosition = 1;
        _enchantButton!.gameObject.SetActive(_currentItem != null);
        _upgradeButton!.gameObject.SetActive(_currentItem != null);
        _questButton!.gameObject.SetActive(_currentItem != null && AndroidQuestRewardPreview.HasRewards(_currentItem));
        if (_currentItem != null)
        {
            _enchantButton.interactable = _mode != PreviewMode.Enchant;
            _upgradeButton.interactable = _mode != PreviewMode.Upgrade && _currentItem.CanCardUpgrade();
            _questButton.interactable = _mode != PreviewMode.Quest;
        }
        _panel.gameObject.SetActive(true);
        return true;
    }

    private void SelectMode(PreviewMode mode)
    {
        if (_mode == mode) return;
        try
        {
            _mode = mode;
            if (!Render())
            {
                _mode = PreviewMode.Enchant;
                Render();
            }
        }
        catch (Exception error)
        {
            _mode = PreviewMode.Enchant;
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.tooltip.mode_failed mode={mode} exception={error}");
            Hide();
        }
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
                new Vector2(-930, 45), new Vector2(-560, 1000));
            var background = _panel.gameObject.AddComponent<Image>();
            background.color = new Color(.055f, .035f, .02f, .94f);
            background.raycastTarget = true;
            _enchantButton = NativeUi.Button(_panel, font, NativeUi.Text("Enchantments", "附魔"),
                new Vector2(0, .91f), new Vector2(1f / 3f, 1), () => SelectMode(PreviewMode.Enchant));
            _upgradeButton = NativeUi.Button(_panel, font, NativeUi.Text("Upgrade", "升级"),
                new Vector2(1f / 3f, .91f), new Vector2(2f / 3f, 1), () => SelectMode(PreviewMode.Upgrade));
            _questButton = NativeUi.Button(_panel, font, NativeUi.Text("Quest rewards", "任务奖励"),
                new Vector2(2f / 3f, .91f), Vector2.one, () => SelectMode(PreviewMode.Quest));
            (_text, _content, _scroll) = NativeUi.ScrollText(_panel, font, Vector2.zero, new Vector2(1, .9f));
            _text.fontSize = 24;
            _text.enableAutoSizing = false;
            _monsterGallery = new AndroidMonsterCardGallery(_panel, ShowMonsterDetails);
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
            _text = null;
            _scroll = null;
            _monsterGallery = null;
            _monsterBackdrop = null;
            _monsterClose = null;
            _enchantButton = _upgradeButton = _questButton = null;
            throw;
        }
    }

    private void Hide()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
        _monsterGallery?.Hide();
        _monsterBackdrop?.SetActive(false);
        _monsterPinned = false;
        _monsterDetailVisible = false;
        _lastController = 0;
        _lastCard = IntPtr.Zero;
        _lastLocked = false;
        _lastLevel = -1;
        _lastItemState = 0;
        _currentItem = null;
        _currentEvent = null;
        _currentMonster = null;
        _currentCardData = null;
        _currentLevelData = null;
        _currentController = null;
        _mode = PreviewMode.Enchant;
    }

    private void ShowMonsterDetails(string description)
    {
        if (!_monsterPinned || _panel == null || _scroll == null || _content == null || _text == null) return;
        if (!_monsterDetailVisible)
        {
            _panel.offsetMax += new Vector2(0, 350);
            _monsterDetailVisible = true;
        }
        NativeUi.RichDescription(_text, description);
        var height = _text.GetPreferredValues(description, _panel.rect.width - 24, 0).y + 24;
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 300));
        _scroll.verticalNormalizedPosition = 1;
        _scroll.gameObject.SetActive(true);
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
        if (_canvasRoot != null) UnityEngine.Object.Destroy(_canvasRoot);
    }
}
