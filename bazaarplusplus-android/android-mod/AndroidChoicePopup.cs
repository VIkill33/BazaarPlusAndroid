using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// A shared touch picker: direct selection, a scrollable list, and a blocking outside-dismiss area.
internal sealed class AndroidChoicePopup(RectTransform parent, TMP_FontAsset font)
{
    private RectTransform? _root;

    internal void Close()
    {
        if (_root == null) return;
        _root.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(_root.gameObject);
        _root = null;
    }

    internal void Show(string title, IReadOnlyList<(string Label, Action Select)> options)
    {
        Close();
        _root = NativeUi.Rect("BPP.ChoicePopup", parent, Vector2.zero, Vector2.one);
        NativeUi.Button(_root, font, string.Empty, Vector2.zero, Vector2.one, Close)
            .GetComponent<Image>().color = new Color(0, 0, 0, .7f);
        var panel = NativeUi.Rect("BPP.ChoiceList", _root, new Vector2(.18f, .13f), new Vector2(.82f, .87f));
        panel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .12f, 1);
        NativeUi.Label(NativeUi.Rect("BPP.ChoiceTitle", panel, new Vector2(.03f, .86f), new Vector2(.86f, .98f)), font, title, 34);
        NativeUi.Button(panel, font, "×", new Vector2(.88f, .86f), new Vector2(.98f, .98f), Close);
        var viewport = NativeUi.Rect("BPP.ChoiceViewport", panel, new Vector2(.03f, .03f), new Vector2(.97f, .84f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var backdrop = viewport.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0, 0, 0, .01f);
        var content = NativeUi.Rect("BPP.Choices", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var rows = (options.Count + 1) / 2;
        content.sizeDelta = new Vector2(0, rows * 78);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var column = index % 2;
            var row = index / 2;
            NativeUi.Button(content, font, option.Label, new Vector2(column * .5f, 1), new Vector2((column + 1) * .5f, 1),
                () => { Close(); option.Select(); }, new Vector2(4, -row * 78 - 72), new Vector2(-4, -row * 78 - 4));
        }
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50;
    }
}
