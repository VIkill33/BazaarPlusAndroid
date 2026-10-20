using System.Runtime.InteropServices;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace BazaarPlusPlus.Android.Probe;

[BepInPlugin("com.bazaarplusplus.android.probe", "BazaarPlusPlus Android Probe", "0.1.0")]
public sealed class AndroidBootstrapProbe : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("[BPP][Android] event=android.bootstrap.loaded");

        // Kept independent of game proxies so loader failures can be distinguished
        // from Collection, SQLite, and replay integration failures.
        var evidence = new
        {
            schemaVersion = 1,
            probeVersion = "0.1.0",
            timestampUtc = DateTimeOffset.UtcNow,
            framework = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            threadId = Environment.CurrentManagedThreadId,
            gameRoot = Paths.GameRootPath,
            bepInExRoot = Paths.BepInExRootPath,
        };
        try
        {
            var path = Path.Combine(Paths.BepInExRootPath, "bpp-android-probe.json");
            File.WriteAllText(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            Log.LogInfo($"[BPP][Android] event=android.bootstrap.evidence_saved path={path}");
        }
        catch (Exception error)
        {
            Log.LogError($"[BPP][Android] event=android.bootstrap.evidence_failed exception={error}");
        }
    }
}
