using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.UI;
using TheBazaar.Tooltips;
using TheBazaar.UI.Tooltips;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// A topmost input surface consumes the whole gesture. Only tooltip data crosses
// this boundary; native pointer, select and drag handlers are never forwarded.
public sealed class AndroidHoldPreview(IntPtr pointer) : MonoBehaviour(pointer)
{
    private static AndroidHoldPreview? _instance;
    private GameObject? _overlay;
    private AndroidHoldPreviewVisuals? _visuals;
    private Button? _button;
    private readonly AndroidPreviewGestureState _gesture = new();
    private int _scene;
    private bool _available;
    private CardTooltipData? _ownedData;
    private bool _hideRequested;
    private readonly HashSet<int> _seenTouches = new();
    private int? _holderTouchId;
    internal static int Revision { get; private set; }
    internal static bool Active => _instance?._gesture.Active == true;
    internal Button? Button => _button;

    internal static bool SuppressRetired(CardTooltipData data) =>
        _instance != null && !Active && _instance._ownedData?.Pointer == data.Pointer;

    internal void Build(Transform parent, TMP_FontAsset font)
    {
        _instance = this;
        _button = NativeUi.Button(parent, font,
            () => NativeUi.Text("Hold to preview", "按住预览"),
            Vector2.zero, Vector2.zero, () => { });
        _button.gameObject.SetActive(PlayerPreferences.Data?.AlternativeInput == true);
        Bind(_button.gameObject, EventTriggerType.PointerDown, data =>
        {
            if (!_gesture.Begin(data.pointerId, _available, PlayerPreferences.Data?.AlternativeInput == true)) return;
            _scene = SceneManager.GetActiveScene().handle;
            _seenTouches.Clear();
            _holderTouchId = data.TryCast<ExtendedPointerEventData>()?.touchId;
            foreach (var touch in PressedTouches()) _seenTouches.Add(touch.Id);
            _overlay!.SetActive(true);
            _visuals!.Show();
            Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.hold_preview.started");
        });
        Bind(_button.gameObject, EventTriggerType.PointerUp, data =>
        {
            if (_gesture.IsHolder(data.pointerId)
                && (!_holderTouchId.HasValue || !PressedTouches().Any(touch => touch.Id == _holderTouchId))) Stop();
        });
        _overlay = new GameObject("BPP.HoldPreviewCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        DontDestroyOnLoad(_overlay);
        var canvas = _overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        _overlay.AddComponent<GraphicRaycaster>();
        var surface = NativeUi.Rect("BPP.PreviewInputShield", _overlay.transform, Vector2.zero, Vector2.one);
        var shield = surface.gameObject.AddComponent<Image>();
        shield.color = Color.clear;
        shield.raycastTarget = true;
        Bind(surface.gameObject, EventTriggerType.PointerDown, data =>
        {
            if (_gesture.ContactDown(data.pointerId) && Touchscreen.current == null) Preview(data);
        });
        Bind(surface.gameObject, EventTriggerType.PointerUp, data => _gesture.ContactUp(data.pointerId));
        _visuals = new AndroidHoldPreviewVisuals(_overlay.transform, font);
        _overlay.SetActive(false);
    }

    internal void SetAvailable(bool available)
    {
        _available = available;
        if (!available) Stop();
    }

    private static void Bind(GameObject target, EventTriggerType type, Action<PointerEventData> callback)
    {
        var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(
            (BaseEventData data) => callback(data.Cast<PointerEventData>())));
        trigger.triggers.Add(entry);
    }

    private void Preview(PointerEventData pointer)
    {
        if (!Active || EventSystem.current == null) return;
        try
        {
            var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject == null || hit.gameObject.transform.IsChildOf(_overlay!.transform)) continue;
                if (hit.gameObject.name == "BPP.MonsterBackdrop") continue;
                // Respect the first underlying surface: never inspect through a menu or tooltip.
                var card = hit.gameObject.GetComponentInParent<CardController>();
                var preview = hit.gameObject.GetComponentInParent<CardPreviewBase>();
                var data = card?.CurrentTooltipData?.TryCast<CardTooltipData>() ?? preview?._tooltipData;
                var parent = Data.TooltipParentComponent;
                // Tooltips shown by this preview overlap neighbouring cards; look through them.
                if (data == null && IsPreviewSurface(hit.gameObject.transform)) continue;
                if (data == null || parent == null)
                {
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.hold_preview.target_unavailable hit={hit.gameObject.name} card_controller={card != null} preview={preview != null}");
                    return;
                }
                HideOwned();
                _ownedData = data;
                _hideRequested = false;
                Revision++;
                parent.ShowCardTooltipController(card != null ? card.transform : preview!.transform,
                    card != null ? card.TooltipOffset : preview!._tooltipWorldSpaceOffset,
                    data.Cast<ITooltipData>(), card == null);
                _visuals?.Select(card, preview);
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.hold_preview.shown card={data.CardInstance?.TemplateId}");
                return;
            }
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.hold_preview.failed exception={error}");
            Stop();
        }
    }

    private static bool IsPreviewSurface(Transform target)
    {
        var panel = AndroidTooltipObserver.FocusPanel;
        return (panel != null && target.IsChildOf(panel))
            || target.GetComponentInParent<CardTooltipController>() != null;
    }

    public void Update()
    {
        var advanced = PlayerPreferences.Data?.AlternativeInput == true;
        var shown = advanced && AndroidLauncherSettings.Shown(AndroidDockButton.HoldPreview);
        if (_button != null && _button.gameObject.activeSelf != shown)
            _button.gameObject.SetActive(shown);
        if (Active && (!advanced || !_available || Data.IsInCombat
            || SceneManager.GetActiveScene().handle != _scene)) Stop();
        if (Active) return;
        HideOwned();
        // Keep consuming other fingers already down when the hold is released.
        // Removing the shield mid-gesture must never turn their release into a purchase.
        if (Touchscreen.current != null && !PressedTouches().Any()) _gesture.Cancel();
        if (_overlay != null && _overlay.activeSelf && !_gesture.ShieldRequired
            && !PressedTouches().Any())
            _overlay.SetActive(false);
    }

    // Sample actual fingers independently of the UI module's pointer arbitration.
    public void LateUpdate()
    {
        if (!Active || EventSystem.current == null) return;
        if (_holderTouchId.HasValue && !PressedTouches().Any(touch => touch.Id == _holderTouchId))
        {
            Stop();
            return;
        }
        var pressed = new HashSet<int>();
        foreach (var touch in PressedTouches(includeBegan: true))
        {
            pressed.Add(touch.Id);
            if (!_seenTouches.Add(touch.Id)) continue;
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.hold_preview.touch id={touch.Id}");
            Preview(new PointerEventData(EventSystem.current) { pointerId = touch.Id, position = touch.Position });
        }
        _seenTouches.IntersectWith(pressed);
        _visuals?.Refresh();
    }

    private static IEnumerable<(int Id, Vector2 Position)> PressedTouches(bool includeBegan = false)
    {
        var screen = Touchscreen.current;
        if (screen == null) yield break;
        for (var index = 0; index < screen.touches.Count; index++)
        {
            var touch = screen.touches[index];
            if (touch.press.isPressed || (includeBegan && touch.press.wasPressedThisFrame)) yield return (touch.touchId.ReadValue(), touch.position.ReadValue());
        }
    }

    private void HideOwned()
    {
        var parent = Data.TooltipParentComponent;
        if (_ownedData == null || parent == null) return;
        var controller = parent.CardTooltipController;
        if (controller?.CurrentTooltipData?.Pointer != _ownedData.Pointer
            || parent.IsCardToCardTransitionActive) return;
        // A native card-to-card show can finish after the holder is released.
        // Wait for that show, then retain ownership until its hide has completed.
        if (!_hideRequested && parent.IsPrimaryTooltipDisplayed)
        {
            parent.UnlockCardTooltipController();
            parent.HideCardTooltipController();
            _hideRequested = true;
        }
        if (_hideRequested && !parent.IsPrimaryTooltipDisplayed)
        {
            _ownedData = null;
            _hideRequested = false;
        }
    }

    private void Stop()
    {
        if (!_gesture.Active) return;
        _gesture.Stop();
        _visuals?.Hide();
        HideOwned();
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.hold_preview.stopped");
    }

    private void Cancel()
    {
        Stop();
        _gesture.Cancel();
        _overlay?.SetActive(false);
    }

    public void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
    public void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    public void OnDestroy()
    {
        Stop();
        if (_overlay != null) Destroy(_overlay);
        if (_instance == this) _instance = null;
    }
}
