using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.GameInterop.Heroes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// A shared touch picker: direct selection, a scrollable list, and a blocking outside-dismiss area.
internal sealed class AndroidChoicePopup(RectTransform parent, TMP_FontAsset font)
{
    private RectTransform? _root;
    private readonly AndroidUiArt _art = new();
    internal bool IsOpen => _root != null;
    internal void Tick() { if (IsOpen) _art.Tick(); }
    internal void Dispose() { Close(); _art.Dispose(); }
    internal static string HeroName(EHero hero) => hero == EHero.Common
        ? NativeUi.Text("Common", "通用") : TheDragonsHeroIdentity.ResolveDisplayName(hero);

    internal void ShowHeroes(IReadOnlyList<EHero> heroes, EHero? selected, Action<EHero?> select)
    {
        var values = new EHero?[] { null }.Concat(heroes.Distinct().Select(hero => (EHero?)hero)).ToArray();
        var options = values.Select(hero => (hero.HasValue ? HeroName(hero.Value) : NativeUi.Text("All heroes", "全部英雄"),
            (Action)(() => select(hero)))).ToArray();
        ShowCards(NativeUi.Text("Hero", "英雄"), options,
            values.Select(hero => hero.HasValue ? AndroidUiArt.Hero(hero.Value.ToString()) : null).ToArray(),
            Array.IndexOf(values, selected));
    }

    internal void Close()
    {
        _art.Clear();
        if (_root == null) return;
        _root.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(_root.gameObject);
        _root = null;
    }

    internal void Show(string title, IReadOnlyList<(string Label, Action Select)> options)
        => ShowOptions(title, options);

    internal void ShowCards(string title, IReadOnlyList<(string Label, Action Select)> options,
        IReadOnlyList<string?> artwork, int selectedIndex)
        => ShowOptions(title, options, artwork, selectedIndex);

    private void ShowOptions(string title, IReadOnlyList<(string Label, Action Select)> options,
        IReadOnlyList<string?>? artwork = null, int selectedIndex = -1)
    {
        Close();
        _root = NativeUi.Rect("BPP.ChoicePopup", parent, Vector2.zero, Vector2.one);
        NativeUi.Button(_root, font, string.Empty, Vector2.zero, Vector2.one, Close)
            .GetComponent<Image>().color = new Color(0, 0, 0, .7f);
        var panel = NativeUi.Rect("BPP.ChoiceList", _root, new Vector2(.18f, .13f), new Vector2(.82f, .87f));
        panel.gameObject.AddComponent<Image>().color = artwork == null
            ? new Color(.06f, .09f, .12f, 1) : new Color(.106f, .071f, .078f, 1);
        NativeUi.Label(NativeUi.Rect("BPP.ChoiceTitle", panel, new Vector2(.03f, .86f), new Vector2(.86f, .98f)), font, title, 34);
        AndroidPanelButtonSkin.Register(NativeUi.Button(panel, font, "×",
            new Vector2(.88f, .86f), new Vector2(.98f, .98f), Close));
        var viewport = NativeUi.Rect("BPP.ChoiceViewport", panel, new Vector2(.03f, .03f), new Vector2(.97f, .84f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var backdrop = viewport.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0, 0, 0, .01f);
        var content = NativeUi.Rect("BPP.Choices", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        var columns = artwork == null ? 2 : 3;
        var rowHeight = artwork == null ? 78 : 194;
        var rows = (options.Count + columns - 1) / columns;
        content.sizeDelta = new Vector2(0, rows * rowHeight);
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var column = index % columns;
            var row = index / columns;
            var button = NativeUi.Button(content, font, option.Label,
                new Vector2(column / (float)columns, 1), new Vector2((column + 1) / (float)columns, 1),
                () => { Close(); option.Select(); },
                new Vector2(6, -row * rowHeight - rowHeight + 6), new Vector2(-6, -row * rowHeight - 6));
            if (artwork == null)
            {
                AndroidPanelButtonSkin.Register(button, index == selectedIndex);
                continue;
            }
            var chosen = index == selectedIndex;
            button.GetComponent<Image>().color = chosen ? new Color(.38f,.25f,.15f,1) : new Color(.157f,.110f,.141f,1);
            AndroidPanelButtonSkin.Register(button, chosen);
            var caption = button.GetComponentInChildren<TextMeshProUGUI>();
            caption.richText = false;
            caption.fontSize = 28;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = new Vector2(1,.30f);
            caption.rectTransform.offsetMin = new Vector2(8,6);
            caption.rectTransform.offsetMax = new Vector2(-8,-2);
            var portraitRoot = NativeUi.Rect("ChoicePortrait", button.transform, new Vector2(.5f,.64f), new Vector2(.5f,.64f),
                new Vector2(-54,-54), new Vector2(54,54));
            var portrait = portraitRoot.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            var fallback = NativeUi.Label(portraitRoot, font,
                option.Label, 24);
            fallback.richText = false;
            _art.Bind(portrait, artwork[index], fallback);
            if (chosen)
            {
                var marker = NativeUi.Label(NativeUi.Rect("Selected", button.transform, new Vector2(.66f,.80f), new Vector2(1,1)),
                    font, NativeUi.Text("Selected", "已选"), 20);
                marker.color = new Color(1f,.74f,.40f);
            }
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
