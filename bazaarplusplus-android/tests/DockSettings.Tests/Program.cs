using BazaarPlusPlus.Android;

foreach (var json in new[] { "null", "[]", "broken", "{}", "{\"schema\":2}", "{\"schema\":\"1\"}" })
    if (AndroidDockSettings.Parse(json) != new AndroidDockPreferences()) throw new Exception(json);
var saved = new AndroidDockPreferences(true, 3, 7300);
if (AndroidDockSettings.Parse("{\"schema\":1,\"autoHide\":true,\"edge\":3,\"along\":7300}") != saved)
    throw new Exception("Valid settings rejected");
if (AndroidDockSettings.Parse("{\"schema\":1,\"autoHide\":\"true\",\"edge\":9,\"along\":-1}") != new AndroidDockPreferences())
    throw new Exception("Invalid values accepted");
if (AndroidDockSettings.Parse("{\"schema\":1,\"textButtons\":true}") != new AndroidDockPreferences(TextButtons: true)
    || AndroidDockSettings.Parse("{\"schema\":1,\"textButtons\":1}") != new AndroidDockPreferences())
    throw new Exception("Button style parsed incorrectly");
foreach (var size in Enum.GetValues<AndroidDockIconSize>())
    if (AndroidDockSettings.Parse($"{{\"schema\":1,\"iconSize\":{(int)size}}}") != new AndroidDockPreferences(IconSize: size))
        throw new Exception($"Icon size {size} parsed incorrectly");
foreach (var json in new[] { "{\"schema\":1,\"iconSize\":-1}", "{\"schema\":1,\"iconSize\":3}",
    "{\"schema\":1,\"iconSize\":\"Large\"}" })
    if (AndroidDockSettings.Parse(json).IconSize != AndroidDockIconSize.Medium)
        throw new Exception($"Invalid icon size accepted: {json}");
saved = saved with { TextButtons = true, IconSize = AndroidDockIconSize.Large };
var path = Path.Combine(BepInEx.Paths.ConfigPath, "BazaarPlusPlus.Android", "dock.json");
try
{
    AndroidDockSettings.Save(saved);
    if (AndroidDockSettings.Current != saved || AndroidDockSettings.Parse(File.ReadAllText(path)) != saved)
        throw new Exception("Settings did not round-trip");
    if (File.Exists(path + ".part")) throw new Exception("Temporary file remains");
    Directory.CreateDirectory(path + ".part");
    try { AndroidDockSettings.Save(new()); throw new Exception("Write failure ignored"); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    if (AndroidDockSettings.Current != saved || AndroidDockSettings.Parse(File.ReadAllText(path)) != saved)
        throw new Exception("Failed save changed preferences");
}
finally { Directory.Delete(BepInEx.Paths.ConfigPath, true); }
Console.WriteLine("Dock settings: corrupt data, round-trip and failed save preservation passed.");
namespace BepInEx { internal static class Paths { internal static string ConfigPath { get; } = Path.Combine(Path.GetTempPath(), "bpp-dock-test-" + Guid.NewGuid()); } }
namespace BazaarPlusPlus.Android {
    internal static class Plugin { internal static Logger? PluginLog => null; }
    internal sealed class Logger { internal void LogWarning(string message) { } }
}
