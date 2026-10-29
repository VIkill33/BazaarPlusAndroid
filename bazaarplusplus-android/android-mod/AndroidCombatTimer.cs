using Il2CppInterop.Runtime;
using TheBazaar.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace BazaarPlusPlus.Android;

public sealed class AndroidCombatTimer(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static readonly AndroidCombatClock Clock = new();
    private GameObject? _root;
    private TextMeshProUGUI? _label;
    private RectTransform? _rect;
    private Canvas? _canvas;
    private int? _pointer;
    private Vector2 _press;
    private Vector2 _start;
    private int _edge;
    private float _along;
    private Vector2 _screen;
    private Rect _safe;
    private float _scale;
    private float _retryAt;
    private bool _installed;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _retryAt) return;
        try
        {
            if (!_installed)
            {
                AndroidCombatTimerNativeFrames.Install();
                _installed = true;
            }
            var phase = AndroidRunObserver.CurrentPhase();
            var combat = phase is HistoryPhase.Combat or HistoryPhase.PvpCombat or HistoryPhase.Replay;
            if (!combat && Clock.HasPlayback) Clock.Reset();
            var show = combat && Clock.HasPlayback && AndroidCombatTimerSettings.Shown;
            if (show && _root == null) Build();
            if (_root == null) return;
            _root.SetActive(show);
            if (!show)
            {
                if (_pointer.HasValue) FinishDrag();
                return;
            }
            var text = Clock.Text;
            if (_label!.text != text) _label.text = text;
            if (_screen != new Vector2(Screen.width, Screen.height) || _safe != Screen.safeArea
                || _scale != _canvas!.scaleFactor)
            {
                _pointer = null;
                Layout();
            }
        }
        catch (Exception error)
        {
            _pointer = null;
            if (_root != null) _root.SetActive(false);
            _retryAt = Time.realtimeSinceStartup + 5;
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_timer.failed exception={error}");
        }
    }

    private void Build()
    {
        var font = NotoFontFallbackRuntime._loadedSansPrimary;
        if (font == null) return;
        var host = new GameObject("BPP.CombatTimerCanvas");
        host.transform.SetParent(transform, false);
        var canvas = host.AddComponent<Canvas>();
        _canvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29998;
        var scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        host.AddComponent<GraphicRaycaster>();
        var rect = NativeUi.Rect("BPP.CombatTimer", host.transform, Vector2.zero, Vector2.zero);
        rect.pivot = Vector2.zero;
        _rect = rect;
        rect.sizeDelta = new Vector2(210, 60);
        var background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(.08f, .065f, .045f, .85f);
        background.raycastTarget = true;
        _label = NativeUi.Label(rect, font, Clock.Text, 32);
        _root = rect.gameObject;
        _edge = AndroidCombatTimerSettings.Position.Edge;
        _along = AndroidCombatTimerSettings.Position.Along / 10000f;
        BindDrag();
        Layout();
    }

    private void BindDrag()
    {
        var trigger = _root!.AddComponent<EventTrigger>();
        Add(EventTriggerType.PointerDown, data =>
        {
            if (_pointer.HasValue) return;
            _pointer = data.pointerId;
            _press = data.position;
            _start = _rect!.anchoredPosition;
        });
        Add(EventTriggerType.Drag, data =>
        {
            if (_pointer != data.pointerId) return;
            data.eligibleForClick = false;
            _rect!.anchoredPosition = Clamp(_start + (data.position - _press) / _canvas!.scaleFactor);
        });
        Add(EventTriggerType.PointerUp, data =>
        {
            if (_pointer != data.pointerId) return;
            data.eligibleForClick = false;
            FinishDrag();
        });
        void Add(EventTriggerType type, Action<PointerEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(
                (BaseEventData data) => callback(data.Cast<PointerEventData>())));
            trigger.triggers.Add(entry);
        }
    }

    // Match the dock: physical left/right edges and the vertical safe area.
    private Rect Bounds()
    {
        var area = Screen.safeArea;
        var scale = _canvas!.scaleFactor;
        return new Rect(0, area.y / scale, Screen.width / scale, area.height / scale);
    }

    private Vector2 Clamp(Vector2 position)
    {
        var area = Bounds();
        var size = _rect!.sizeDelta;
        return new Vector2(Math.Clamp(position.x, area.xMin, Math.Max(area.xMin, area.xMax - size.x)),
            Math.Clamp(position.y, area.yMin, Math.Max(area.yMin, area.yMax - size.y)));
    }

    private void Layout()
    {
        _screen = new Vector2(Screen.width, Screen.height);
        _safe = Screen.safeArea;
        _scale = _canvas!.scaleFactor;
        var area = Bounds();
        var width = Math.Max(0, area.width - _rect!.sizeDelta.x);
        var height = Math.Max(0, area.height - _rect.sizeDelta.y);
        _rect.anchoredPosition = _edge switch
        {
            0 => new Vector2(area.xMin, area.yMin + height * _along),
            1 => new Vector2(area.xMin + width, area.yMin + height * _along),
            3 => new Vector2(area.xMin + width * _along, area.yMin + height),
            _ => new Vector2(area.xMin + width * _along, area.yMin),
        };
    }

    private void FinishDrag()
    {
        _pointer = null;
        var area = Bounds();
        var position = Clamp(_rect!.anchoredPosition);
        var width = Math.Max(0, area.width - _rect.sizeDelta.x);
        var height = Math.Max(0, area.height - _rect.sizeDelta.y);
        var distances = new[] { position.x - area.xMin, area.xMin + width - position.x,
            position.y - area.yMin, area.yMin + height - position.y };
        _edge = Array.IndexOf(distances, distances.Min());
        _along = _edge < 2 ? (height > 0 ? (position.y - area.yMin) / height : 0)
            : (width > 0 ? (position.x - area.xMin) / width : 0);
        Layout();
        try { AndroidCombatTimerSettings.SavePosition(_edge, (int)MathF.Round(_along * 10000)); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_timer.save_failed exception={error}");
        }
    }

    public void OnApplicationFocus(bool focus)
    {
        if (!focus && _pointer.HasValue) FinishDrag();
    }

    public void OnDestroy()
    {
        Clock.Reset();
        if (_root != null) Destroy(_root.transform.parent.gameObject);
    }
}
