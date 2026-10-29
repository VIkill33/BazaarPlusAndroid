using BazaarPlusPlus.Android;

foreach (var (json, expected) in new (string, bool)[] {
    ("{\"schema\":1,\"previewPanelPosition\":\"left\"}", true),
    ("{\"schema\":1,\"previewPanelPosition\":\"default\"}", false),
    ("{\"schema\":2,\"previewPanelPosition\":\"left\"}", false),
    ("{\"schema\":\"1\",\"previewPanelPosition\":\"left\"}", false),
    ("{\"schema\":1,\"previewPanelPosition\":5}", false),
    ("{\"schema\":1,\"previewPanelPosition\":\"unknown\"}", false),
    ("{}", false), ("null", false), ("[]", false), ("broken", false) })
    if (AndroidLauncherSettings.Parse(json).LeftEdge != expected) throw new Exception(json);
var parsed = AndroidLauncherSettings.Parse("{\"schema\":1,\"hiddenDockButtons\":[\"history\",\"holdPreview\",\"unknown\",3]}");
if (!parsed.Hidden.SetEquals(new[] { AndroidDockButton.History, AndroidDockButton.HoldPreview }))
    throw new Exception("Hidden buttons parsed incorrectly");
if (AndroidLauncherSettings.Parse("{\"schema\":1,\"hiddenDockButtons\":\"history\"}").Hidden.Count != 0
    || AndroidLauncherSettings.Parse("{\"schema\":2,\"hiddenDockButtons\":[\"history\"]}").Hidden.Count != 0)
    throw new Exception("Invalid hidden buttons accepted");
if (AndroidLauncherSettings.LeftEdge || !Enum.GetValues<AndroidDockButton>().All(AndroidLauncherSettings.Shown))
    throw new Exception("Missing file should use defaults");
var path = Path.Combine(BepInEx.Paths.BepInExRootPath, "bpp-launcher-settings.json");
AndroidLauncherPreferences Saved() => AndroidLauncherSettings.Parse(File.ReadAllText(path));
try {
    AndroidLauncherSettings.SetLeftEdge(true);
    if (!AndroidLauncherSettings.LeftEdge || !Saved().LeftEdge) throw new Exception("Left position was not saved");
    AndroidLauncherSettings.SetShown(AndroidDockButton.Builds, false);
    AndroidLauncherSettings.SetShown(AndroidDockButton.History, false);
    AndroidLauncherSettings.SetShown(AndroidDockButton.Builds, true);
    if (AndroidLauncherSettings.Shown(AndroidDockButton.History) || !AndroidLauncherSettings.Shown(AndroidDockButton.Builds)
        || !Saved().Hidden.SetEquals(new[] { AndroidDockButton.History }) || !Saved().LeftEdge)
        throw new Exception("Button visibility was not saved alongside the position");
    File.WriteAllText(path, File.ReadAllText(path).TrimEnd('}') + ",\"futureKey\":7}");
    AndroidLauncherSettings.SetLeftEdge(false);
    if (AndroidLauncherSettings.LeftEdge || Saved().LeftEdge || !File.ReadAllText(path).Contains("\"futureKey\":7"))
        throw new Exception("Default position was not saved or unknown keys were dropped");
    var temporary = path + ".part";
    if (File.Exists(temporary)) throw new Exception("Temporary file remains");
    Directory.CreateDirectory(temporary);
    try { AndroidLauncherSettings.SetLeftEdge(true); throw new Exception("Write failure ignored"); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    if (AndroidLauncherSettings.LeftEdge || Saved().LeftEdge)
        throw new Exception("Failed save changed the active or persisted setting");
} finally { Directory.Delete(BepInEx.Paths.BepInExRootPath, true); }
Console.WriteLine("Launcher settings: parse, button visibility, key preservation and persistence checks passed");

namespace BepInEx { internal static class Paths { internal static string BepInExRootPath { get; } = Path.Combine(Path.GetTempPath(), "bpp-settings-test-" + Guid.NewGuid()); } }
namespace BazaarPlusPlus.Android {
    internal static class Plugin { internal static Logger? PluginLog => null; }
    internal sealed class Logger { internal void LogWarning(string message) { } }
}
