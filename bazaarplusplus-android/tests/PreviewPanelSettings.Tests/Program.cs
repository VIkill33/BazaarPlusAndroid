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
Console.WriteLine("Preview panel settings: 11 checks passed");

namespace BepInEx { internal static class Paths { internal static string BepInExRootPath => Path.GetTempPath(); } }
namespace BazaarPlusPlus.Android {
    internal static class Plugin { internal static Logger? PluginLog => null; }
    internal sealed class Logger { internal void LogWarning(string message) { } }
}
