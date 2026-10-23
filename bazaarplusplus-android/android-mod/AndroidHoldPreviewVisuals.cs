using TheBazaar;
using TheBazaar.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Screen-space holes preserve the real card and tooltip colors without moving,
// cloning or changing the sorting of any game-owned objects.
internal sealed class AndroidHoldPreviewVisuals
{
    private static readonly Color Gold = new(.88f, .70f, .39f, .85f);
    private readonly RectTransform _root;
    private readonly RectTransform _dimRoot;
    private readonly RectTransform[] _frame;
    private readonly RectTransform[] _focus;
    private readonly RectTransform _status;
    private readonly TextMeshProUGUI _statusLabel;
    private readonly List<RectTransform> _tiles = new();
    private readonly List<PreviewFocusRect> _holes = new();
    private CardController? _card;
    private CardPreviewBase? _preview;

    internal AndroidHoldPreviewVisuals(Transform parent, TMP_FontAsset font)
    {
        _root = NativeUi.Rect("BPP.HoldPreviewVisuals", parent, Vector2.zero, Vector2.one);
        _dimRoot = NativeUi.Rect("BPP.FocusDim", _root, Vector2.zero, Vector2.one);
        _frame = Border("BPP.PreviewFrame", Gold);
        _focus = Border("BPP.TargetHighlight", new Color(1, .91f, .66f, .95f));
        _status = Image("BPP.PreviewStatus", _root, Gold);
        var fill = Image("BPP.PreviewStatusFill", _status, new Color(.10f, .065f, .03f, .96f));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2, 2);
        fill.offsetMax = new Vector2(-2, -2);
        _statusLabel = NativeUi.Label(fill, font, () => NativeUi.Text("Previewing · Release to exit", "预览中 · 松开退出"), 26);
        Hide();
    }

    internal void Show()
    {
        _card = null;
        _preview = null;
        _root.gameObject.SetActive(true);
        Refresh();
    }

    internal void Select(CardController? card, CardPreviewBase? preview)
    {
        _card = card;
        _preview = preview;
    }

    internal void Hide()
    {
        _root.gameObject.SetActive(false);
        _card = null;
        _preview = null;
    }

    internal void Refresh()
    {
        var scale = Screen.height / 1080f;
        var safe = Screen.safeArea;
        var edge = new Rect(8 * scale, safe.yMin + 8 * scale,
            Screen.width - 16 * scale, safe.height - 16 * scale);
        PlaceBorder(_frame, edge, 2 * scale);
        Place(_status, new Rect(edge.center.x - 180 * scale, edge.yMax - 62 * scale, 360 * scale, 48 * scale));
        // The root uses physical pixels, while the status typography follows the
        // same 1080-high reference as the existing preview panels.
        _statusLabel.fontSize = 26 * scale;

        _holes.Clear();
        var target = TargetBounds();
        foreach (var line in _focus) line.gameObject.SetActive(target.HasValue);
        if (target.HasValue)
        {
            var bounds = target.Value;
            bounds.xMin -= 12 * scale;
            bounds.xMax += 12 * scale;
            bounds.yMin -= 12 * scale;
            bounds.yMax += 12 * scale;
            AddHole(bounds);
            PlaceBorder(_focus, bounds, 2.5f * scale);
        }

        // Read existing fields instead of invoking native controller getters
        // during the asynchronous show.
        var parent = target.HasValue ? Data.TooltipParentComponent : null;
        if (parent != null)
        {
            var controller = parent._cardTooltipController;
            if (controller != null && controller.isActiveAndEnabled)
            {
                AddUiHole(controller._canvasContentRectTransform);
                AddUiHole(controller.activeTooltipFrame != null ? controller.activeTooltipFrame.rectTransform : null);
                AddUiHole(controller.nestedTooltipFrame != null ? controller.nestedTooltipFrame.rectTransform : null);
                AddUiHole(controller.cooldownClock != null ? controller.cooldownClock.GetComponent<RectTransform>() : null);
            }
        }
        AddUiHole(AndroidTooltipObserver.FocusPanel);
        var regions = AndroidPreviewFocusLayout.DimmedRegions(new(0, 0, Screen.width, Screen.height), _holes);
        while (_tiles.Count < regions.Count)
            _tiles.Add(Image("BPP.FocusDimTile", _dimRoot, new Color(0, 0, 0, .55f)));
        for (var i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].gameObject.SetActive(i < regions.Count);
            if (i < regions.Count)
            {
                var r = regions[i];
                Place(_tiles[i], Rect.MinMaxRect(r.Left, r.Bottom, r.Right, r.Top));
            }
        }
    }

    private Rect? TargetBounds()
    {
        if (_card != null && _card.gameObject.activeInHierarchy && _card.BoxCollider != null)
        {
            var collider = _card.BoxCollider;
            var camera = _card.mainCamera != null ? _card.mainCamera : Camera.main;
            if (camera == null) return null;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < 8; i++)
            {
                var offset = Vector3.Scale(collider.size, new Vector3((i & 1) == 0 ? -.5f : .5f,
                    (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f));
                var point = camera.WorldToScreenPoint(collider.transform.TransformPoint(collider.center + offset));
                if (point.z <= 0) return null;
                min = Vector2.Min(min, new Vector2(point.x, point.y));
                max = Vector2.Max(max, new Vector2(point.x, point.y));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        return _preview != null ? UiBounds(_preview._rectTransform) : null;
    }

    private void AddUiHole(RectTransform? rect)
    {
        var bounds = UiBounds(rect);
        if (bounds.HasValue) AddHole(bounds.Value);
    }

    private void AddHole(Rect bounds)
    {
        if (bounds.width > 0 && bounds.height > 0)
            _holes.Add(new(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax));
    }

    private Rect? UiBounds(RectTransform? rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return null;
        var nearestCanvas = rect.GetComponentInParent<Canvas>();
        var canvas = nearestCanvas != null ? nearestCanvas.rootCanvas : null;
        var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        // Project local corners individually: this Android proxy's native
        // GetWorldCorners array call crashes instead of filling the array.
        var local = rect.rect;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < 4; i++)
        {
            var world = rect.TransformPoint(new Vector3((i & 1) == 0 ? local.xMin : local.xMax,
                (i & 2) == 0 ? local.yMin : local.yMax, 0));
            var projected = camera != null ? camera.WorldToScreenPoint(world) : world;
            var point = new Vector2(projected.x, projected.y);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private RectTransform[] Border(string name, Color color) =>
        Enumerable.Range(0, 4).Select(_ => Image(name, _root, color)).ToArray();

    private static RectTransform Image(string name, Transform parent, Color color)
    {
        var rect = NativeUi.Rect(name, parent, Vector2.zero, Vector2.zero);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void Place(RectTransform rect, Rect bounds)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = bounds.position;
        rect.sizeDelta = bounds.size;
    }

    private static void PlaceBorder(RectTransform[] lines, Rect r, float width)
    {
        Place(lines[0], new Rect(r.xMin, r.yMin, r.width, width));
        Place(lines[1], new Rect(r.xMin, r.yMax - width, r.width, width));
        Place(lines[2], new Rect(r.xMin, r.yMin + width, width, Mathf.Max(0, r.height - 2 * width)));
        Place(lines[3], new Rect(r.xMax - width, r.yMin + width, width, Mathf.Max(0, r.height - 2 * width)));
    }
}
