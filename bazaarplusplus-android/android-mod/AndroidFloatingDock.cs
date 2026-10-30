using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using TheBazaar;
using TheBazaar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidFloatingDock(IntPtr pointer) : MonoBehaviour(pointer)
{
    private const float OriginalFrameSize = 86f;
    private sealed record Style(Vector2 Button, float Gap, float HandleHeight, Vector2 Collapsed, float HandleFont, float CollapsedFont);
    private static readonly Style TextStyle = new(new Vector2(270, 90), 5, 70, new Vector2(60, 132), 30, 28);

    private sealed class Entry
    {
        internal required RectTransform Rect;
        internal required TextMeshProUGUI Label;
        internal required AndroidLocalizedText Localized;
        internal required Image? Icon;
        internal required Image Frame;
        internal required Func<string> Caption;
        internal required Func<string> Text;
    }

    private static AndroidFloatingDock? _instance;
    private RectTransform? _group;
    private RectTransform? _buttons;
    private CanvasGroup? _opacity;
    private Button? _handle;
    private Canvas? _canvas;
    private AndroidHoldPreview? _holdPreview;
    private bool _collapsed;
    private bool _autoHide;
    private float _lastInteraction;
    private int? _pointer;
    private bool _dragged;
    private Vector2 _press;
    private Vector2 _start;
    private int _edge;
    private float _along;
    private Rect _safe;
    private Vector2 _screen;
    private int _visibleEntries;
    private bool _textButtons;
    private AndroidDockIconSize _iconSize = AndroidDockIconSize.Medium;
    private Sprite? _nativeButtonFrame;
    private float _nextFrameScan;
    private readonly SortedDictionary<int, Entry> _entries = new();
    private float IconScale => _iconSize switch
    {
        AndroidDockIconSize.Large => 1.5f,
        AndroidDockIconSize.Small => 1f,
        _ => 1.2f,
    };
    private float IconFactor => IconScale / 1.2f;
    private Style CurrentStyle => _textButtons ? TextStyle
        : new Style(new Vector2(115, 140) * IconFactor, 10 * IconFactor, 58,
            new Vector2(72, 158), 26, 34);

    internal static bool AutoHide
    {
        get => AndroidDockSettings.Current.AutoHide;
        set => AndroidDockSettings.Save(AndroidDockSettings.Current with { AutoHide = value });
    }

    // caption labels the icon tile; text is the full label of the text-only style.
    [HideFromIl2Cpp]
    internal static void Register(Button button, TMP_FontAsset font, int slot, AndroidDockIcon icon, Func<string> caption, Func<string> text)
    {
        if (_instance == null)
        {
            var host = new GameObject("BPP.DockCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
            DontDestroyOnLoad(host);
            var instance = host.AddComponent<AndroidFloatingDock>();
            try { instance.Build(font); }
            catch { Destroy(host); throw; }
            _instance = instance;
        }
        var dock = _instance;
        dock.Place(button, slot, icon, caption, text);
        dock.Bind(button);
        dock.Layout();
    }

    [HideFromIl2Cpp]
    private void Place(Button button, int slot, AndroidDockIcon icon, Func<string> caption, Func<string> text)
    {
        var rect = button.GetComponent<RectTransform>();
        rect.SetParent(_buttons, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleButtonText(label);
        var localized = label.GetComponent<AndroidLocalizedText>();
        if (localized == null) localized = label.gameObject.AddComponent<AndroidLocalizedText>();
        Image? image = null;
        var frameRect = NativeUi.Rect("BPP.DockFrame", rect, new Vector2(.5f, 1), new Vector2(.5f, 1));
        frameRect.pivot = new Vector2(.5f, 1);
        frameRect.sizeDelta = new Vector2(OriginalFrameSize * IconScale, OriginalFrameSize * IconScale);
        frameRect.anchoredPosition = new Vector2(0, -2 * IconFactor);
        frameRect.SetAsFirstSibling();
        var frameImage = frameRect.gameObject.AddComponent<Image>();
        frameImage.preserveAspect = true;
        frameImage.raycastTarget = false;
        var sprite = AndroidDockIcons.Get(icon);
        if (sprite != null)
        {
            var art = NativeUi.Rect("BPP.DockIcon", rect, new Vector2(.5f, 1), new Vector2(.5f, 1));
            art.pivot = new Vector2(.5f, .5f);
            art.sizeDelta = new Vector2(69, 71) * IconFactor;
            art.anchoredPosition = new Vector2(0, -2 * IconFactor - OriginalFrameSize * IconScale / 2);
            image = art.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        var entry = new Entry { Rect = rect, Label = label, Localized = localized, Icon = image,
            Frame = frameImage, Caption = caption, Text = text };
        _entries[slot] = entry;
        Restyle(entry);
    }

    [HideFromIl2Cpp]
    private void Restyle(Entry entry)
    {
        entry.Rect.sizeDelta = CurrentStyle.Button;
        var factor = IconFactor;
        var frameRect = entry.Frame.rectTransform;
        frameRect.sizeDelta = new Vector2(OriginalFrameSize * IconScale, OriginalFrameSize * IconScale);
        frameRect.anchoredPosition = new Vector2(0, -2 * factor);
        if (entry.Icon != null)
        {
            var art = entry.Icon.rectTransform;
            art.sizeDelta = new Vector2(69, 71) * factor;
            art.anchoredPosition = new Vector2(0, -2 * factor - OriginalFrameSize * IconScale / 2);
        }
        var frame = entry.Rect.GetComponent<Image>();
        frame.sprite = null;
        frame.color = _textButtons ? new Color(.15f, .20f, .24f, 1) : Color.clear;
        entry.Frame.sprite = _nativeButtonFrame;
        entry.Frame.gameObject.SetActive(!_textButtons && _nativeButtonFrame != null);
        var icon = !_textButtons && entry.Icon != null;
        entry.Icon?.gameObject.SetActive(icon);
        entry.Localized.Bind(entry.Label, _textButtons ? entry.Text : entry.Caption);
        var labelRect = entry.Label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = new Vector2(1, icon ? 0 : 1);
        labelRect.offsetMin = icon ? new Vector2(5 * factor, 0) : new Vector2(12, 6);
        labelRect.offsetMax = icon ? new Vector2(-5 * factor, 30 * factor) : new Vector2(-12, -6);
        entry.Label.enableAutoSizing = !_textButtons;
        entry.Label.fontSizeMin = icon ? 17 * factor : 17;
        entry.Label.fontSizeMax = icon ? 24 * factor : 24;
        if (_textButtons) entry.Label.fontSize = 30;
        entry.Label.GetComponent<Shadow>().effectDistance = icon ? new Vector2(2, -2) * factor : new Vector2(2, -2);
        entry.Label.textWrappingMode = _textButtons ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
    }

    private static void StyleButtonText(TextMeshProUGUI label)
    {
        label.fontStyle |= FontStyles.Bold;
        var shadow = label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);
        shadow.useGraphicAlpha = false;
    }

    internal static void SetIcon(Button button, AndroidDockIcon icon)
    {
        var image = button.transform.Find("BPP.DockIcon")?.GetComponent<Image>();
        var sprite = AndroidDockIcons.Get(icon);
        if (image != null && sprite != null) image.sprite = sprite;
    }

    private void Build(TMP_FontAsset font)
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 29999;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        gameObject.AddComponent<GraphicRaycaster>();
        _group = NativeUi.Rect("BPP.Dock", transform, Vector2.zero, Vector2.zero);
        _opacity = _group.gameObject.AddComponent<CanvasGroup>();
        _group.pivot = Vector2.zero;
        _buttons = NativeUi.Rect("BPP.DockButtons", _group, Vector2.zero, Vector2.one);
        _handle = NativeUi.Button(_group, font, "BPA", Vector2.zero, Vector2.zero, () =>
        {
            if (_dragged) return;
            _collapsed = !_collapsed;
            _lastInteraction = Time.realtimeSinceStartup;
            Layout();
        });
        StyleButtonText(_handle.GetComponentInChildren<TextMeshProUGUI>());
        Bind(_handle);
        _holdPreview = gameObject.AddComponent<AndroidHoldPreview>();
        _holdPreview.Build(_buttons!, font);
        _textButtons = AndroidDockSettings.Current.TextButtons;
        _iconSize = AndroidDockSettings.Current.IconSize;
        Place(_holdPreview.Button!, 3, AndroidDockIcon.HoldPreview, () => NativeUi.Text("Hold", "按住预览"),
            () => NativeUi.Text("Hold to preview", "按住预览"));
        _edge = AndroidDockSettings.Current.Edge;
        _along = AndroidDockSettings.Current.Along / 10000f;
        _autoHide = AutoHide;
        _collapsed = _autoHide;
        _lastInteraction = Time.realtimeSinceStartup;
        Layout();
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.dock.ready");
    }

    private void Bind(Button button)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        Add(EventTriggerType.PointerDown, data =>
        {
            if (_pointer.HasValue) return;
            _pointer = data.pointerId;
            _press = data.position;
            _start = _group!.anchoredPosition;
            _dragged = false;
            _lastInteraction = Time.realtimeSinceStartup;
        });
        Add(EventTriggerType.Drag, data =>
        {
            if (_pointer != data.pointerId) return;
            _dragged = true;
            data.eligibleForClick = false;
            _group!.anchoredPosition = Clamp(_start + (data.position - _press) / _canvas!.scaleFactor);
            _lastInteraction = Time.realtimeSinceStartup;
        });
        Add(EventTriggerType.PointerUp, data =>
        {
            if (_pointer != data.pointerId) return;
            _pointer = null;
            _lastInteraction = Time.realtimeSinceStartup;
            if (!_dragged) return;
            data.eligibleForClick = false;
            Snap();
        });
        void Add(EventTriggerType type, Action<PointerEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(
                (BaseEventData data) => callback(data.Cast<PointerEventData>())));
            trigger.triggers.Add(entry);
        }
    }

    public void LateUpdate()
    {
        if (_group == null) return;
        if (!_textButtons && _nativeButtonFrame == null && Time.realtimeSinceStartup >= _nextFrameScan)
        {
            _nextFrameScan = Time.realtimeSinceStartup + 1;
            TryResolveNativeButtonFrame();
        }
        // Keep the held button alive for pointer ownership, but let the native
        // details use either screen edge without being covered by the dock.
        _opacity!.alpha = AndroidHoldPreview.Active ? 0 : 1;
        if (AndroidHoldPreview.Active) _lastInteraction = Time.realtimeSinceStartup;
        var visible = !CollectionController.AnyOpen && !AndroidBuildPanelController.AnyOpen && !AndroidHistoryPanel.AnyOpen;
        _group.gameObject.SetActive(visible);
        _holdPreview?.SetAvailable(visible && !_collapsed);
        if (!visible)
        {
            _pointer = null;
            _lastInteraction = Time.realtimeSinceStartup;
        }
        var autoHide = AutoHide;
        if (autoHide != _autoHide)
        {
            _autoHide = autoHide;
            _collapsed = autoHide;
            _pointer = null;
            _lastInteraction = Time.realtimeSinceStartup;
            Layout();
        }
        if (_textButtons != AndroidDockSettings.Current.TextButtons || _iconSize != AndroidDockSettings.Current.IconSize)
        {
            _textButtons = AndroidDockSettings.Current.TextButtons;
            _iconSize = AndroidDockSettings.Current.IconSize;
            foreach (var entry in _entries.Values) Restyle(entry);
            Layout();
        }
        if (_screen != new Vector2(Screen.width, Screen.height) || _safe != Screen.safeArea
            || _visibleEntries != VisibleEntries()) Layout();
        if (visible && _autoHide && !_collapsed && !_pointer.HasValue && !AndroidHoldPreview.Active
            && Time.realtimeSinceStartup - _lastInteraction >= 5)
        {
            _collapsed = true;
            Layout();
        }
    }

    private void TryResolveNativeButtonFrame()
    {
        foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (image == null || image.gameObject.name != "Btn_Frame"
                || image.sprite == null || image.sprite.name != "Btn_Rnd_Frame_TUI") continue;
            _nativeButtonFrame = image.sprite;
            foreach (var entry in _entries.Values) Restyle(entry);
            return;
        }
    }

    // Dock to the physical screen edges; retain the vertical safe area.
    private Rect Bounds()
    {
        var area = Screen.safeArea;
        var scale = _canvas!.scaleFactor;
        return new Rect(0, area.y / scale, Screen.width / scale, area.height / scale);
    }

    private Vector2 Clamp(Vector2 position)
    {
        var area = Bounds();
        var size = _group!.sizeDelta;
        return new Vector2(Math.Clamp(position.x, area.xMin, Math.Max(area.xMin, area.xMax - size.x)),
            Math.Clamp(position.y, area.yMin, Math.Max(area.yMin, area.yMax - size.y)));
    }

    // Bitmask of entries their owners currently show; hidden entries leave no gap.
    private int VisibleEntries()
    {
        var mask = 0;
        foreach (var entry in _entries)
            if (entry.Value.Rect != null && entry.Value.Rect.gameObject.activeSelf) mask |= 1 << entry.Key;
        return mask;
    }

    private void Layout()
    {
        _screen = new Vector2(Screen.width, Screen.height);
        _safe = Screen.safeArea;
        _visibleEntries = VisibleEntries();
        var style = CurrentStyle;
        var step = style.Button.y + style.Gap;
        var shown = 0;
        foreach (var entry in _entries)
        {
            if ((_visibleEntries & (1 << entry.Key)) == 0) continue;
            entry.Value.Rect.anchoredPosition = new Vector2(0, -shown * step);
            shown++;
        }
        var buttonHeight = Math.Max(0, shown * step - style.Gap);
        _group!.sizeDelta = _collapsed ? style.Collapsed
            : new Vector2(style.Button.x, buttonHeight + (_autoHide ? style.HandleHeight + style.Gap : 0));
        _buttons!.gameObject.SetActive(!_collapsed);
        _handle!.gameObject.SetActive(_collapsed || _autoHide);
        var handle = _handle.GetComponent<RectTransform>();
        handle.pivot = Vector2.zero;
        handle.sizeDelta = _collapsed ? style.Collapsed : new Vector2(style.Button.x, style.HandleHeight);
        handle.anchoredPosition = Vector2.zero;
        var label = _handle.GetComponentInChildren<TextMeshProUGUI>();
        label.text = _collapsed ? "B\nP\nA" : _textButtons ? NativeUi.Text("Collapse", "收起") : NativeUi.Text("Hide", "收起");
        label.fontSize = _collapsed ? style.CollapsedFont : style.HandleFont;
        var area = Bounds();
        var travel = new Vector2(Math.Max(0, area.width - _group.sizeDelta.x), Math.Max(0, area.height - _group.sizeDelta.y));
        _group.anchoredPosition = _edge switch
        {
            1 => new Vector2(area.xMin + travel.x, area.yMin + travel.y * _along),
            2 => new Vector2(area.xMin + travel.x * _along, area.yMin),
            3 => new Vector2(area.xMin + travel.x * _along, area.yMin + travel.y),
            _ => new Vector2(area.xMin, area.yMin + travel.y * _along),
        };
    }

    private void Snap()
    {
        var area = Bounds();
        var position = Clamp(_group!.anchoredPosition);
        var width = Math.Max(0, area.width - _group.sizeDelta.x);
        var height = Math.Max(0, area.height - _group.sizeDelta.y);
        var distances = new[] { position.x - area.xMin, area.xMin + width - position.x,
            position.y - area.yMin, area.yMin + height - position.y };
        _edge = Array.IndexOf(distances, distances.Min());
        _along = _edge < 2 ? (height > 0 ? (position.y - area.yMin) / height : 0)
            : (width > 0 ? (position.x - area.xMin) / width : 0);
        try
        {
            AndroidDockSettings.Save(AndroidDockSettings.Current with { Edge = _edge, Along = (int)MathF.Round(_along * 10000) });
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.dock.save_failed exception={error}");
        }
        Layout();
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.dock.snapped edge={_edge} along={_along}");
    }

    public void OnApplicationFocus(bool focus)
    {
        if (focus || !_pointer.HasValue) return;
        _pointer = null;
        Snap();
    }

    public void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
