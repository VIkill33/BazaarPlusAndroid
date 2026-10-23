using System.Text.Json;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal static class AndroidPreviewPanelSettings
{
    private static string SettingsPath => Path.Combine(Paths.BepInExRootPath, "bpp-launcher-settings.json");
    internal static bool LeftEdge { get; private set; } = Load();

    internal static void SetLeftEdge(bool value)
    {
        var path = SettingsPath;
        var temporary = path + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schema = 1, previewPanelPosition = value ? "left" : "default" }));
            File.Move(temporary, path, overwrite: true);
            LeftEdge = value;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static bool Load()
    {
        try
        {
            var path = SettingsPath;
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
