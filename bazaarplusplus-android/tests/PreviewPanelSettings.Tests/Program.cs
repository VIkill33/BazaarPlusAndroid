using BazaarPlusPlus.Android;

foreach (var (json, expected) in new (string, bool)[] {
    ("{\"schema\":1,\"previewPanelPosition\":\"left\"}", true),
    ("{\"schema\":1,\"previewPanelPosition\":\"default\"}", false),
    ("{\"schema\":2,\"previewPanelPosition\":\"left\"}", false),
    ("{\"schema\":\"1\",\"previewPanelPosition\":\"left\"}", false),
    ("{\"schema\":1,\"previewPanelPosition\":5}", false),
    ("{\"schema\":1,\"previewPanelPosition\":\"unknown\"}", false),
    ("{}", false), ("null", false), ("[]", false), ("broken", false) })
    if (AndroidPreviewPanelSettings.Parse(json) != expected) throw new Exception(json);
if (AndroidPreviewPanelSettings.LeftEdge) throw new Exception("Missing file should use default");
try {
    AndroidPreviewPanelSettings.SetLeftEdge(true);
    if (!AndroidPreviewPanelSettings.LeftEdge || !AndroidPreviewPanelSettings.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "bpp-launcher-settings.json"))))
        throw new Exception("Left position was not saved");
    AndroidPreviewPanelSettings.SetLeftEdge(false);
    if (AndroidPreviewPanelSettings.LeftEdge || AndroidPreviewPanelSettings.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "bpp-launcher-settings.json"))))
        throw new Exception("Default position was not saved");
    var temporary = Path.Combine(BepInEx.Paths.BepInExRootPath, "bpp-launcher-settings.json.part");
    if (File.Exists(temporary)) throw new Exception("Temporary file remains");
    Directory.CreateDirectory(temporary);
    try { AndroidPreviewPanelSettings.SetLeftEdge(true); throw new Exception("Write failure ignored"); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    if (AndroidPreviewPanelSettings.LeftEdge || AndroidPreviewPanelSettings.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "bpp-launcher-settings.json"))))
        throw new Exception("Failed save changed the active or persisted setting");
} finally { Directory.Delete(BepInEx.Paths.BepInExRootPath, true); }
Console.WriteLine("Preview panel settings: parse and persistence checks passed");

namespace BepInEx { internal static class Paths { internal static string BepInExRootPath { get; } = Path.Combine(Path.GetTempPath(), "bpp-settings-test-" + Guid.NewGuid()); } }
namespace BazaarPlusPlus.Android {
    internal static class Plugin { internal static Logger? PluginLog => null; }
    internal sealed class Logger { internal void LogWarning(string message) { } }
}
