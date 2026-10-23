using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidFloatingDock(IntPtr pointer) : MonoBehaviour(pointer)
{
    private static readonly Vector2 CollapsedSize = new(60, 132);
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

    internal static bool AutoHide
    {
        get => AndroidDockSettings.Current.AutoHide;
        set => AndroidDockSettings.Save(AndroidDockSettings.Current with { AutoHide = value });
    }

    internal static void Register(Button button, TMP_FontAsset font, int slot)
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
        var rect = button.GetComponent<RectTransform>();
        rect.SetParent(dock._buttons, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(270, 90);
        rect.anchoredPosition = new Vector2(0, -slot * 95);
        dock.Bind(button);
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
        _handle = NativeUi.Button(_group, font, "BPP", Vector2.zero, Vector2.zero, () =>
        {
            if (_dragged) return;
            _collapsed = !_collapsed;
            _lastInteraction = Time.realtimeSinceStartup;
            Layout();
        });
        Bind(_handle);
        _holdPreview = gameObject.AddComponent<AndroidHoldPreview>();
        _holdPreview.Build(_buttons!, font);
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
        // Keep the held button alive for pointer ownership, but let the native
        // details use either screen edge without being covered by the dock.
        _opacity!.alpha = AndroidHoldPreview.Active ? 0 : 1;
        if (AndroidHoldPreview.Active) _lastInteraction = Time.realtimeSinceStartup;
        var visible = !CollectionController.AnyOpen && !AndroidBuildPanelController.AnyOpen;
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
        if (_screen != new Vector2(Screen.width, Screen.height) || _safe != Screen.safeArea) Layout();
        if (visible && _autoHide && !_collapsed && !_pointer.HasValue && !AndroidHoldPreview.Active
            && Time.realtimeSinceStartup - _lastInteraction >= 5)
        {
            _collapsed = true;
            Layout();
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

    private void Layout()
    {
        _screen = new Vector2(Screen.width, Screen.height);
        _safe = Screen.safeArea;
        _group!.sizeDelta = _collapsed ? CollapsedSize : new Vector2(270, _autoHide ? 450 : 375);
        _buttons!.gameObject.SetActive(!_collapsed);
        _handle!.gameObject.SetActive(_collapsed || _autoHide);
        var handle = _handle.GetComponent<RectTransform>();
        handle.pivot = Vector2.zero;
        handle.sizeDelta = _collapsed ? CollapsedSize : new Vector2(270, 70);
        handle.anchoredPosition = Vector2.zero;
        var label = _handle.GetComponentInChildren<TextMeshProUGUI>();
        label.text = _collapsed ? "B\nP\nP" : NativeUi.Text("Collapse", "收起");
        label.fontSize = _collapsed ? 28 : 30;
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
