using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// The launcher and these two game overlays share a palette, while cards remain native game art.
internal static class AndroidMarketPanelTheme
{
    private static Texture2D? _backdrop;
    internal static readonly Color Background = new(.055f, .025f, .075f, .99f);
    internal static readonly Color Surface = new(.105f, .065f, .13f, .98f);
    internal static readonly Color Selected = new(.29f, .14f, .085f, 1f);
    private static readonly Color Gold = new(.91f, .60f, .28f, .85f);

    internal static void Apply(RectTransform panel, TextMeshProUGUI title)
    {
        panel.GetComponent<Image>().color = Background;
        AddBackdrop(panel);
        title.color = new Color(1f, .84f, .55f, 1f);
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.targetGraphic is Image image)
            {
                image.color = Surface;
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = false;
            }
        }
        Frame(panel, "BPP.Preview");
        Frame(panel, "BPP.RecommendedBoard");
        Frame(panel, "BPP.Description");
        Rule(panel, .875f);
        Rule(panel, .015f);
    }

    private static void Frame(RectTransform panel, string name)
    {
        var target = panel.Find(name);
        if (target == null) return;
        var image = target.gameObject.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
        image.color = new Color(.07f, .035f, .10f, .75f);
        image.raycastTarget = false;
        var outline = target.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = false;
    }

    private static void AddBackdrop(RectTransform panel)
    {
        try
        {
            if (_backdrop == null)
            {
                using var stream = typeof(AndroidMarketPanelTheme).Assembly.GetManifestResourceStream(
                    "BazaarPlusPlus.Android.Market.background.jpg");
                if (stream == null) return;
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!ImageConversion.LoadImage(texture, bytes.ToArray()))
                {
                    UnityEngine.Object.Destroy(texture);
                    return;
                }
                texture.filterMode = FilterMode.Bilinear;
                UnityEngine.Object.DontDestroyOnLoad(texture);
                _backdrop = texture;
            }
            var rect = NativeUi.Rect("BPP.MarketBackdrop", panel, Vector2.zero, Vector2.one);
            rect.SetAsFirstSibling();
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = _backdrop;
            image.uvRect = new Rect(0, .36f, 1, .34f);
            image.color = new Color(.65f, .38f, .63f, .22f);
            image.raycastTarget = false;
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.market_background.failed type={error.GetType().Name}");
        }
    }

    private static void Rule(RectTransform panel, float y)
    {
        var rule = NativeUi.Rect("BPP.MarketRule", panel, new Vector2(.03f, y), new Vector2(.97f, y),
            new Vector2(0, -1), new Vector2(0, 1));
        var image = rule.gameObject.AddComponent<Image>();
        image.color = new Color(.80f, .47f, .24f, .55f);
        image.raycastTarget = false;
    }
}
