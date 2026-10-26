using BepInEx;
using System.Text.Json;

namespace BazaarPlusPlus.Android;

internal sealed record AndroidCombatTimerPosition(int Edge = 2, int Along = 5000);

internal static class AndroidCombatTimerSettings
{
    private static string SettingsPath => Path.Combine(Paths.ConfigPath, "BazaarPlusPlus.Android", "combat-timer.json");
    internal static bool Shown { get; private set; } = Load();
    internal static AndroidCombatTimerPosition Position { get; private set; } = LoadPosition();
    internal static void Save(bool shown) => Save(shown, Position);
    internal static void SavePosition(int edge, int along) => Save(Shown,
        new(Math.Clamp(edge, 0, 3), Math.Clamp(along, 0, 10000)));
    private static void Save(bool shown, AndroidCombatTimerPosition position)
    {
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".part";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schema = 1, shown, edge = position.Edge, along = position.Along }));
            File.Move(temporary, path, overwrite: true);
            Shown = shown;
            Position = position;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static bool Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return !(root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schema", out var schema) && schema.TryGetInt32(out var version) && version == 1
                && root.TryGetProperty("shown", out var shown) && shown.ValueKind == JsonValueKind.False);
        }
        catch (JsonException) { return true; }
        catch (InvalidOperationException) { return true; }
    }
    internal static AndroidCombatTimerPosition ParsePosition(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Number
                && schema.TryGetInt32(out var version) && version == 1
                && root.TryGetProperty("edge", out var edge) && edge.ValueKind == JsonValueKind.Number
                && edge.TryGetInt32(out var e) && e is >= 0 and <= 3
                && root.TryGetProperty("along", out var along) && along.ValueKind == JsonValueKind.Number
                && along.TryGetInt32(out var a) && a is >= 0 and <= 10000)
                return new(e, a);
        }
        catch (JsonException) { }
        return new();
    }
    private static AndroidCombatTimerPosition LoadPosition()
    {
        try { return File.Exists(SettingsPath) ? ParsePosition(File.ReadAllText(SettingsPath)) : new(); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_timer.position_failed type={error.GetType().Name}");
            return new();
        }
    }
    private static bool Load()
    {
        try { return !File.Exists(SettingsPath) || Parse(File.ReadAllText(SettingsPath)); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_timer.settings_failed type={error.GetType().Name}");
            return true;
        }
    }
}
