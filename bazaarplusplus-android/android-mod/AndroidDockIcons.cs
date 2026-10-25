using UnityEngine;

namespace BazaarPlusPlus.Android;

internal enum AndroidDockIcon { Collection, Builds, HoldPreview, History, PreviewEnchant, PreviewUpgrade, PreviewNone }

internal static class AndroidDockIcons
{
    private static readonly Dictionary<AndroidDockIcon, Sprite?> Cache = new();

    internal static Sprite? Get(AndroidDockIcon icon)
    {
        if (Cache.TryGetValue(icon, out var cached) && cached != null) return cached;
        Sprite? sprite = null;
        try { sprite = Load(icon); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.dock.icon_failed icon={icon} exception={error}");
        }
        Cache[icon] = sprite;
        return sprite;
    }

    private static Sprite? Load(AndroidDockIcon icon)
    {
        var name = icon switch
        {
            AndroidDockIcon.Builds => "builds",
            AndroidDockIcon.HoldPreview => "hold-preview",
            AndroidDockIcon.History => "history",
            AndroidDockIcon.PreviewEnchant => "preview-enchant",
            AndroidDockIcon.PreviewUpgrade => "preview-upgrade",
            AndroidDockIcon.PreviewNone => "preview-none",
            _ => "collection",
        };
        using var stream = typeof(AndroidDockIcons).Assembly.GetManifestResourceStream($"BazaarPlusPlus.Android.Dock.{name}.png");
        if (stream == null) return null;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        if (!ImageConversion.LoadImage(texture, bytes.ToArray()))
        {
            UnityEngine.Object.Destroy(texture);
            return null;
        }
        UnityEngine.Object.DontDestroyOnLoad(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        UnityEngine.Object.DontDestroyOnLoad(sprite);
        return sprite;
    }
}
