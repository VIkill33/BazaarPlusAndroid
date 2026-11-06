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
    private static readonly Color Ink = new(.96f, .89f, .73f);
    private static readonly Color MutedInk = new(.78f, .70f, .57f);
    private static readonly Color Fallback = new(.08f, .065f, .045f, .85f);
    private static readonly Color PausedInk = new(1f, .78f, .35f);
    private static readonly Color DisabledAlpha = new(1f, 1f, 1f, .4f);
    private GameObject? _root;
    private TextMeshProUGUI? _label;
    private static readonly Color PressedTint = new(.92f, .67f, .26f, .24f);
    private sealed class Control(RectTransform rect, Image tint, Action click)
    {
        internal readonly RectTransform Rect = rect;
        internal readonly Image Tint = tint;
        internal readonly Action Click = click;
        internal bool Enabled;
    }
    private Control? _speed;
    private TextMeshProUGUI? _speedLabel;
    private Control? _pause;
    private Control? _pressed;
    private bool _dragging;
    private Image[]? _pauseBars;
    private TextMeshProUGUI? _playLabel;
    private bool _pausedByTimer;
    private Image? _background;
    private Image? _backing;
    private Image? _frame;
    private bool _skinned;
    private float _blend;
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
                try { AndroidCombatSpeedHook.Install(); }
                catch (Exception error)
                {
                    Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_speed.failed exception={error}");
                }
            }
            var phase = AndroidRunObserver.CurrentPhase();
            var combat = phase is HistoryPhase.Combat or HistoryPhase.PvpCombat or HistoryPhase.Replay;
            if (!combat && Clock.HasPlayback) Clock.Reset();
            if (AndroidCombatControls.ReleasePause(_pausedByTimer, Clock.Active, AndroidCombatTimerSettings.Shown)) ReleasePause();
            if (Clock.Active) AndroidCombatSpeedHook.ApplyOnce();
            var show = combat && Clock.HasPlayback && AndroidCombatTimerSettings.Shown;
            if (show && _root == null) Build();
            if (_root == null) return;
            _root.SetActive(show);
            if (!show)
            {
                if (_pointer.HasValue) FinishDrag();
                return;
            }
            if (!_skinned) Skin();
            var text = Clock.Text;
            if (_label!.text != text) _label.text = text;
            _blend = AndroidCombatClock.Blend(_blend, Clock.Active, Time.unscaledDeltaTime);
            _label.color = Color.Lerp(MutedInk, Ink, _blend);
            RefreshControls();
            if (_screen != new Vector2(Screen.width, Screen.height) || _safe != Screen.safeArea
                || _scale != _canvas!.scaleFactor)
            {
                _pointer = null;
                Press(null);
                Layout();
            }
        }
        catch (Exception error)
        {
            _pointer = null;
            if (_root != null) _root.SetActive(false);
            _retryAt = Time.realtimeSinceStartup + 5;
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_timer.failed exception={error}");
            try { ReleasePause(); }
            catch (Exception release)
            {
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_pause.release_failed exception={release}");
            }
        }
    }

    private void RefreshControls()
    {
        var speed = AndroidCombatControls.SpeedText(AndroidCombatTimerSettings.Speed);
        if (_speedLabel!.text != speed) _speedLabel.text = speed;
        _speed!.Enabled = AndroidCombatSpeedHook.Ready;
        _speedLabel.color = AndroidCombatSpeedHook.Ready ? Ink : Ink * DisabledAlpha;
        var manager = Singleton<GameServiceManager>.Instance;
        var interactable = Clock.Active && manager != null;
        var paused = interactable && manager!.GamePaused;
        if (_pausedByTimer && manager != null && !manager.GamePaused) _pausedByTimer = false;
        _pause!.Enabled = interactable;
        var color = (paused ? PausedInk : Ink) * (interactable ? Color.white : DisabledAlpha);
        foreach (var bar in _pauseBars!)
        {
            bar.enabled = !paused;
            bar.color = color;
        }
        _playLabel!.enabled = paused;
        _playLabel.color = color;
    }

    private void TogglePause()
    {
        var manager = Singleton<GameServiceManager>.Instance;
        if (manager == null || !Clock.Active) return;
        manager.PauseOrUnpauseGame(!manager.GamePaused);
        _pausedByTimer = manager.GamePaused;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_pause.toggled paused={_pausedByTimer}");
    }

    private void ReleasePause()
    {
        if (!_pausedByTimer) return;
        _pausedByTimer = false;
        var manager = Singleton<GameServiceManager>.Instance;
        if (manager != null && manager.GamePaused) manager.PauseOrUnpauseGame(false);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.combat_pause.released");
    }

    private void CycleSpeed()
    {
        try
        {
            AndroidCombatTimerSettings.SaveSpeed(AndroidCombatControls.NextSpeed(AndroidCombatTimerSettings.Speed));
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_speed.save_failed type={error.GetType().Name}");
            return;
        }
        AndroidCombatSpeedHook.Apply();
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_speed.changed speed={AndroidCombatControls.SpeedText(AndroidCombatTimerSettings.Speed)}");
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
        rect.sizeDelta = new Vector2(460, 88);
        _background = rect.gameObject.AddComponent<Image>();
        _background.color = Fallback;
        _background.raycastTarget = true;
        _backing = Layer("BPP.CombatTimerBacking", rect, new Vector2(2, 2));
        _frame = Layer("BPP.CombatTimerFrame", rect, Vector2.zero);
        // Desktop order [speed | time | pause]; each control keeps a ~114x82 touch target.
        const float left = .26f, right = .74f;
        var time = NativeUi.Rect("BPP.CombatTimerTime", rect, new Vector2(left, 0), new Vector2(right, 1));
        _label = NativeUi.Label(time, font, Clock.Text, 32);
        _label.color = MutedInk;
        Divider(rect, new Vector2(left, .28f), new Vector2(left, .72f));
        Divider(rect, new Vector2(right, .28f), new Vector2(right, .72f));
        _speed = ControlArea("BPP.CombatSpeed", rect, 0, left, CycleSpeed);
        _speedLabel = NativeUi.Label(_speed.Rect, font, AndroidCombatControls.SpeedText(AndroidCombatTimerSettings.Speed), 26);
        _pause = ControlArea("BPP.CombatPause", rect, right, 1, TogglePause);
        _pauseBars = new[] { Bar(_pause.Rect, -8), Bar(_pause.Rect, 8) };
        _playLabel = NativeUi.Label(_pause.Rect, font,
            font.HasCharacter('\u25B6', true, true) ? "\u25B6" : NativeUi.Text("Resume", "继续"), 26);
        _playLabel.enabled = false;
        _root = rect.gameObject;
        _edge = AndroidCombatTimerSettings.Position.Edge;
        _along = AndroidCombatTimerSettings.Position.Along / 10000f;
        BindDrag();
        Layout();
    }

    private static Image Layer(string name, RectTransform parent, Vector2 inset)
    {
        var image = NativeUi.Rect(name, parent, Vector2.zero, Vector2.one, inset, -inset).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static void Divider(RectTransform parent, Vector2 min, Vector2 max)
    {
        var image = NativeUi.Rect("BPP.CombatTimerDivider", parent, min, max, new Vector2(-.5f, 0), new Vector2(.5f, 0))
            .gameObject.AddComponent<Image>();
        image.color = new Color(.69f, .52f, .255f, .32f);
        image.raycastTarget = false;
    }

    // Plain areas rather than Buttons: the root trigger then owns press and release, so a tap
    // survives finger jitter beyond Unity's small pixel drag threshold.
    private static Control ControlArea(string name, RectTransform parent, float left, float right, Action click)
    {
        var rect = NativeUi.Rect(name, parent, new Vector2(left, 0), new Vector2(right, 1), new Vector2(3, 3), new Vector2(-3, -3));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        return new Control(rect, image, click);
    }

    private Control? Hit(PointerEventData data)
    {
        var hit = data.pointerCurrentRaycast.gameObject;
        if (hit == null) return null;
        foreach (var control in new[] { _speed, _pause })
            if (control != null && hit.transform.IsChildOf(control.Rect)) return control;
        return null;
    }

    private void Press(Control? control)
    {
        if (_pressed != null) _pressed.Tint.color = Color.clear;
        _pressed = control is { Enabled: true } ? control : null;
        if (_pressed != null) _pressed.Tint.color = PressedTint;
    }

    private static Image Bar(Transform parent, float x)
    {
        var rect = NativeUi.Rect("BPP.CombatPauseBar", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        rect.sizeDelta = new Vector2(9, 28);
        rect.anchoredPosition = new Vector2(x, 0);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // Keep the plain backdrop until both plaque sprites exist, so the timer never renders half-skinned.
    private void Skin()
    {
        AndroidNativeButtonArt.Tick();
        if (AndroidNativeButtonArt.Brown is not { } brown || AndroidNativeButtonArt.Frame is not { } frame) return;
        Paint(_backing!, brown);
        Paint(_frame!, frame);
        _background!.color = Color.clear;
        _skinned = true;
        static void Paint(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = AndroidNativeButtonArt.Slice(sprite);
            image.color = Color.white;
            image.enabled = true;
        }
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
            _dragging = false;
            Press(Hit(data));
        });
        Add(EventTriggerType.Drag, data =>
        {
            if (_pointer != data.pointerId) return;
            var delta = (data.position - _press) / _canvas!.scaleFactor;
            if (!_dragging && !AndroidCombatControls.IsDrag(delta.x, delta.y)) return;
            _dragging = true;
            Press(null);
            _rect!.anchoredPosition = Clamp(_start + delta);
        });
        Add(EventTriggerType.PointerUp, data =>
        {
            if (_pointer != data.pointerId) return;
            var tapped = !_dragging && _pressed != null && Hit(data) == _pressed ? _pressed : null;
            Press(null);
            if (_dragging) FinishDrag();
            else _pointer = null;
            tapped?.Click();
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
        _dragging = false;
        Press(null);
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
        ReleasePause();
        Clock.Reset();
        if (_root != null) Destroy(_root.transform.parent.gameObject);
    }
}
