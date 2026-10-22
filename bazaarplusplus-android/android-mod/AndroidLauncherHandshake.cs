using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal static class AndroidLauncherHandshake
{
    internal static void Complete()
    {
        var requestPath = Path.Combine(Paths.BepInExRootPath, "bpp-launch-session.json");
        if (!File.Exists(requestPath))
            return;
        try
        {
            using var request = JsonDocument.Parse(File.ReadAllText(requestPath));
            var root = request.RootElement;
            var session = root.GetProperty("session").GetString();
            if (!Guid.TryParse(session, out _) || root.GetProperty("pluginVersion").GetString() != Plugin.Version)
                return;
            var dll = Path.Combine(Paths.PluginPath, "BazaarPlusPlus.Android.dll");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant();
            if (!string.Equals(root.GetProperty("sha256").GetString(), hash, StringComparison.Ordinal))
                return;
            var target = Path.Combine(Paths.BepInExRootPath, "bpp-launch-loaded.json");
            var temp = target + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new
            {
                session,
                pluginVersion = Plugin.Version,
                sha256 = hash,
                loaded = true,
                loadedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            }));
            File.Move(temp, target, overwrite: true);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.launcher.loaded session={session} sha256={hash}");
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.launcher.handshake_failed type={error.GetType().Name}");
        }
    }
}
