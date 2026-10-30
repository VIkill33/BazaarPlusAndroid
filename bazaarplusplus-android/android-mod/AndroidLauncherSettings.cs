using System.Text.Json;
using System.Text.Json.Nodes;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal enum AndroidDockButton { Collection, Builds, PreviewMode, HoldPreview, History }

internal sealed record AndroidLauncherPreferences(bool LeftEdge, IReadOnlySet<AndroidDockButton> Hidden, bool MarketPanels)
{
    internal static AndroidLauncherPreferences Default { get; } = new(false, new HashSet<AndroidDockButton>(), false);
}

// Shared with the launcher settings page (LauncherSettingsActivity); keys must match on both sides.
internal static class AndroidLauncherSettings
{
    private static readonly Dictionary<AndroidDockButton, string> Keys = new()
    {
        [AndroidDockButton.Collection] = "collection",
        [AndroidDockButton.Builds] = "builds",
        [AndroidDockButton.PreviewMode] = "previewMode",
        [AndroidDockButton.HoldPreview] = "holdPreview",
        [AndroidDockButton.History] = "history",
    };

    private static string SettingsPath => Path.Combine(Paths.BepInExRootPath, "bpp-launcher-settings.json");
    internal static AndroidLauncherPreferences Current { get; private set; } = Load();
    internal static bool LeftEdge => Current.LeftEdge;
    internal static bool MarketPanels => Current.MarketPanels;
    internal static bool Shown(AndroidDockButton button) => !Current.Hidden.Contains(button);

    internal static void SetLeftEdge(bool value) => Save(Current with { LeftEdge = value });
    internal static void SetMarketPanels(bool value) => Save(Current with { MarketPanels = value });

    internal static void SetShown(AndroidDockButton button, bool shown)
    {
        var hidden = new HashSet<AndroidDockButton>(Current.Hidden);
        if (shown) hidden.Remove(button); else hidden.Add(button);
        Save(Current with { Hidden = hidden });
    }

    private static void Save(AndroidLauncherPreferences value)
    {
        var path = SettingsPath;
        var temporary = path + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Keep keys this plugin version does not know about.
        JsonObject root;
        try { root = (File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null) ?? new JsonObject(); }
        catch (JsonException) { root = new JsonObject(); }
        root["schema"] = 1;
        root["previewPanelPosition"] = value.LeftEdge ? "left" : "default";
        root["catalogBuildPanelStyle"] = value.MarketPanels ? "market" : "classic";
        root["hiddenDockButtons"] = new JsonArray(Keys.Where(key => value.Hidden.Contains(key.Key))
            .Select(key => (JsonNode?)JsonValue.Create(key.Value)).ToArray());
        try
        {
            File.WriteAllText(temporary, root.ToJsonString());
            File.Move(temporary, path, overwrite: true);
            Current = value;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static AndroidLauncherPreferences Load()
    {
        try
        {
            var path = SettingsPath;
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : AndroidLauncherPreferences.Default;
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.launcher_settings.failed type={error.GetType().Name}");
            return AndroidLauncherPreferences.Default;
        }
    }

    internal static AndroidLauncherPreferences Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1)
                return AndroidLauncherPreferences.Default;
            var left = root.TryGetProperty("previewPanelPosition", out var position)
                && position.ValueKind == JsonValueKind.String && position.GetString() == "left";
            var market = root.TryGetProperty("catalogBuildPanelStyle", out var style)
                && style.ValueKind == JsonValueKind.String && style.GetString() == "market";
            var hidden = new HashSet<AndroidDockButton>();
            if (root.TryGetProperty("hiddenDockButtons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
                foreach (var item in buttons.EnumerateArray())
                    foreach (var key in Keys)
                        if (item.ValueKind == JsonValueKind.String && item.GetString() == key.Value) hidden.Add(key.Key);
            return new(left, hidden, market);
        }
        catch (JsonException) { return AndroidLauncherPreferences.Default; }
    }
}
