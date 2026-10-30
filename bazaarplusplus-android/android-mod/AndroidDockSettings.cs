using System.Text.Json;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal enum AndroidDockIconSize { Large, Medium, Small }

internal sealed record AndroidDockPreferences(bool AutoHide = false, int Edge = 0, int Along = 3500,
    bool TextButtons = false, AndroidDockIconSize IconSize = AndroidDockIconSize.Medium);

internal static class AndroidDockSettings
{
    private static string SettingsPath => Path.Combine(Paths.ConfigPath, "BazaarPlusPlus.Android", "dock.json");
    internal static AndroidDockPreferences Current { get; private set; } = Load();

    internal static void Save(AndroidDockPreferences value)
    {
        value = value with { Edge = Math.Clamp(value.Edge, 0, 3), Along = Math.Clamp(value.Along, 0, 10000),
            IconSize = Enum.IsDefined(value.IconSize) ? value.IconSize : AndroidDockIconSize.Medium };
        var path = SettingsPath;
        var temporary = path + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new
            {
                schema = 1, autoHide = value.AutoHide, edge = Math.Clamp(value.Edge, 0, 3),
                along = Math.Clamp(value.Along, 0, 10000), textButtons = value.TextButtons,
                iconSize = (int)value.IconSize,
            }));
            File.Move(temporary, path, overwrite: true);
            Current = value;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static AndroidDockPreferences Load()
    {
        try { return File.Exists(SettingsPath) ? Parse(File.ReadAllText(SettingsPath)) : new(); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.dock.settings_failed type={error.GetType().Name}");
            return new();
        }
    }

    internal static AndroidDockPreferences Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
                return new();
            var autoHide = root.TryGetProperty("autoHide", out var hide) && hide.ValueKind == JsonValueKind.True;
            var edge = root.TryGetProperty("edge", out var e) && e.ValueKind == JsonValueKind.Number
                && e.TryGetInt32(out var index) && index is >= 0 and <= 3 ? index : 0;
            var along = root.TryGetProperty("along", out var a) && a.ValueKind == JsonValueKind.Number
                && a.TryGetInt32(out var fraction) && fraction is >= 0 and <= 10000 ? fraction : 3500;
            var textButtons = root.TryGetProperty("textButtons", out var text) && text.ValueKind == JsonValueKind.True;
            var iconSize = root.TryGetProperty("iconSize", out var size) && size.ValueKind == JsonValueKind.Number
                && size.TryGetInt32(out var sizeIndex) && sizeIndex is >= 0 and <= 2
                ? (AndroidDockIconSize)sizeIndex : AndroidDockIconSize.Medium;
            return new(autoHide, edge, along, textButtons, iconSize);
        }
        catch (JsonException) { return new(); }
    }
}
