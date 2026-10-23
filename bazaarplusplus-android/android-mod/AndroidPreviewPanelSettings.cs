using System.Text.Json;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal static class AndroidPreviewPanelSettings
{
    // The launcher writes this snapshot atomically before starting Unity.
    internal static bool LeftEdge { get; } = Load();

    private static bool Load()
    {
        try
        {
            var path = Path.Combine(Paths.BepInExRootPath, "bpp-launcher-settings.json");
            if (!File.Exists(path)) return false;
            return Parse(File.ReadAllText(path));
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.preview.settings_failed type={error.GetType().Name}");
            return false;
        }
    }

    internal static bool Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version == 1
                && root.TryGetProperty("previewPanelPosition", out var position)
                && position.ValueKind == JsonValueKind.String && position.GetString() == "left";
        }
        catch (JsonException) { return false; }
    }
}
