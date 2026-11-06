using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Shared item-detail tag row. The dark rounded pills and cool blue text mirror
// the native card header; the row wraps within the scrollable detail content.
internal static class AndroidCardTagChips
{
    private static Sprite? _background;

    internal static float Render(RectTransform row, TextMeshProUGUI measure,
        IReadOnlyList<string> labels, float width)
    {
        for (var index = row.childCount - 1; index >= 0; index--)
        {
            var child = row.GetChild(index).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
        row.gameObject.SetActive(labels.Count > 0);
        if (labels.Count == 0) return 0;

        var available = Mathf.Max(64, width - 24);
        var x = 12f;
        var y = 0f;
        foreach (var label in labels)
        {
            var chipWidth = Mathf.Min(available, Mathf.Max(48,
                measure.GetPreferredValues(label, float.PositiveInfinity, 34).x + 20));
            if (x > 12 && x + chipWidth > available + 12)
            {
                x = 12;
                y += 40;
            }
            var chip = NativeUi.Rect("BPP.CardTagChip", row, Vector2.up, Vector2.up,
                new Vector2(x, -y - 38), new Vector2(x + chipWidth, -y - 4));
            var image = chip.gameObject.AddComponent<Image>();
            image.sprite = Background();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var text = NativeUi.Label(chip, measure.font, label, 24);
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(.68f, .75f, .94f, 1);
            text.enableAutoSizing = false;
            x += chipWidth + 8;
        }
        return y + 48;
    }

    private static Sprite Background()
    {
        if (_background != null) return _background;
        const int size = 20;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Max(0, Mathf.Abs(x - 9.5f) - 4.5f);
                var dy = Mathf.Max(0, Mathf.Abs(y - 9.5f) - 4.5f);
                texture.SetPixel(x, y, dx * dx + dy * dy <= 25
                    ? new Color(.08f, .065f, .13f, .96f) : Color.clear);
            }
        texture.Apply();
        _background = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
        return _background;
    }
}
