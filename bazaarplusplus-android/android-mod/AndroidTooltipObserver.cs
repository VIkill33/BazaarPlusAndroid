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
    private Button? _enchantButton;
    private Button? _upgradeButton;
    private Button? _questButton;
    private ItemCard? _currentItem;
    private EventEncounterCard? _currentEvent;
    private HeroLevelTooltipData? _currentLevelData;
    private CardTooltipController? _currentController;
    private PreviewMode _mode;
    private static bool _reportedFailure;

    private enum PreviewMode { Enchant, Upgrade, Quest }

    public void Update()
    {
        if (Time.realtimeSinceStartup < _nextScan) return;
        _nextScan = Time.realtimeSinceStartup + .25f;
        try
        {
            var visible = false;
            foreach (var controller in Resources.FindObjectsOfTypeAll<CardTooltipController>())
            {
                if (controller == null || !controller.isActiveAndEnabled) continue;
                var data = controller.CurrentTooltipData?.TryCast<CardTooltipData>();
                var levelData = controller.CurrentTooltipData?.TryCast<HeroLevelTooltipData>();
                var card = data?.CardInstance;
                var item = card?.TryCast<ItemCard>();
                var eventCard = item == null ? card?.TryCast<EventEncounterCard>() : null;
                if (item == null && eventCard == null && levelData == null) continue;
                var id = controller.GetInstanceID();
                var locked = controller.IsLocked;
                // Advanced mobile input can keep the live tooltip unlocked while
                // the card is inspected; standard mode uses the locked long press.
                if (levelData == null && !locked && PlayerPreferences.Data?.AlternativeInput != true) continue;
                visible = true;
                var pointer = card?.Pointer ?? levelData!.Pointer;
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
            if (!visible) Hide();
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
        if ((_currentItem == null && _currentEvent == null && _currentLevelData == null)
            || _currentController == null || _text == null
            || _content == null || _scroll == null || _panel == null) return false;
        var preview = _currentLevelData != null
            ? AndroidEventPreview.BuildLevelRewards(_currentLevelData.GetCurrentAndNextLevel().Item1)
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
        NativeUi.RichDescription(_text, preview);
        var height = _text.GetPreferredValues(preview, 330f, 0).y + 32;
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 850));
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
            _panel.gameObject.SetActive(false);
        }
        catch
        {
            UnityEngine.Object.Destroy(_canvasRoot);
            _canvasRoot = null;
            _panel = _content = null;
            _text = null;
            _scroll = null;
            _enchantButton = _upgradeButton = _questButton = null;
            throw;
        }
    }

    private void Hide()
    {
        if (_panel != null) _panel.gameObject.SetActive(false);
        _lastController = 0;
        _lastCard = IntPtr.Zero;
        _lastLocked = false;
        _lastLevel = -1;
        _lastItemState = 0;
        _currentItem = null;
        _currentEvent = null;
        _currentLevelData = null;
        _currentController = null;
        _mode = PreviewMode.Enchant;
    }

    public void OnDestroy()
    {
        if (_canvasRoot != null) UnityEngine.Object.Destroy(_canvasRoot);
    }
}
