using BazaarPlusPlus.Localization;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Text.RegularExpressions;

namespace BazaarPlusPlus.Android;

internal static class NativeUi
{
    private static TMP_SpriteAsset[]? _spriteAssets;
    private static readonly Regex SpriteName = new("<sprite\\b[^>]*\\bname\\s*=\\s*(?:\"([^\"]+)\"|'([^']+)'|([^\\s>]+))", RegexOptions.IgnoreCase);

    internal static void RichDescription(TextMeshProUGUI label, string text)
    {
        var names = SpriteName.Matches(text).Select(match => match.Groups.Cast<Group>().Skip(1)
            .First(group => group.Success).Value).Distinct().ToArray();
        if (names.Length > 0)
        {
            _spriteAssets ??= Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>().ToArray();
            TMP_SpriteAsset? best = null;
            var bestScore = 0;
            foreach (var asset in _spriteAssets)
            {
                if (asset == null) continue;
                var score = names.Count(name => asset.GetSpriteIndexFromName(name) >= 0);
                if (score <= bestScore) continue;
                best = asset;
                bestScore = score;
            }
            if (best != null) label.spriteAsset = best;
            else _spriteAssets = null; // The game's icon atlas may load after the first card.
        }
        label.text = text;
    }

    internal static string Text(string english, string chinese) => L.Resolve(new LocalizedTextSet(english, chinese));

    internal static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var root = new GameObject(name, new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    internal static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string text, float size = 30)
    {
        var rect = Rect("BPP.Text", parent, Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.91f, 0.79f, 1);
        label.raycastTarget = false;
        return label;
    }

    internal static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, Func<string> text, float size = 30)
    {
        var label = Label(parent, font, text(), size);
        label.gameObject.AddComponent<AndroidLocalizedText>().Bind(label, text);
        return label;
    }

    internal static Button Button(Transform parent, TMP_FontAsset font, Func<string> text, Vector2 min, Vector2 max,
        Action callback, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var button = Button(parent, font, text(), min, max, callback, offsetMin, offsetMax);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.gameObject.AddComponent<AndroidLocalizedText>().Bind(label, text);
        return button;
    }

    internal static Button Button(Transform parent, TMP_FontAsset font, string text, Vector2 min, Vector2 max,
        Action callback, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var rect = Rect("BPP.Button", parent, min, max, offsetMin, offsetMax);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.20f, 0.24f, 1);
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(callback));
        Label(rect, font, text);
        return button;
    }

    internal static TMP_InputField Search(Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max,
        Action<string> changed)
    {
        var rect = Rect("BPP.Search", parent, min, max);
        rect.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.19f, 1);
        var viewport = Rect("BPP.SearchViewport", rect, Vector2.zero, new Vector2(.88f, 1), new Vector2(16, 8), new Vector2(-16, -8));
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = Label(viewport, font, string.Empty, 30);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = Label(viewport, font, () => Text("Search cards", "搜索卡牌"), 30);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = new Color(0.65f, 0.68f, 0.71f, 1);
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(changed));
        Button(rect, font, "×", new Vector2(.88f, 0), Vector2.one,
            () => { input.text = string.Empty; input.DeactivateInputField(); });
        return input;
    }

    internal static (TextMeshProUGUI Text, RectTransform Content, ScrollRect Scroll) ScrollText(
        Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max)
    {
        var root = Rect("BPP.Description", parent, min, max);
        var viewport = Rect("BPP.DescriptionViewport", root, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        var background = viewport.gameObject.AddComponent<Image>();
        background.color = new Color(0, 0, 0, .01f);
        background.raycastTarget = true;
        var content = Rect("BPP.DescriptionContent", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var text = Label(content, font, string.Empty, 28);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        return (text, content, scroll);
    }
}
