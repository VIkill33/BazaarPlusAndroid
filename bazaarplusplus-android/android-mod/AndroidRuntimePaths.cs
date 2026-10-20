using System.Reflection;
using BepInEx;

namespace BazaarPlusPlus.Android;

internal static class AndroidRuntimePaths
{
    internal static void PrepareNativeHooks()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        try
        {
            ProbeCache(folder);
            return;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            // FusionCore's pinned Mono runtime reports /data as LocalApplicationData.
            // MonoMod unconditionally extracts executable helpers beneath that folder.
            // Correct this one per-process cached folder to the app's private data root.
            var owned = Path.GetFullPath(Path.Combine(Paths.GameRootPath, "BazaarPlusPlus.Android", "runtime-data"));
            if (!owned.StartsWith("/data/user/", StringComparison.Ordinal) && !owned.StartsWith("/data/data/", StringComparison.Ordinal))
                throw new PlatformNotSupportedException("Native helpers require an app-private internal data directory.");
            ProbeCache(owned);
            var field = typeof(Environment).GetField("s_specialFolders", BindingFlags.NonPublic | BindingFlags.Static);
            if (field?.GetValue(null) is not IDictionary<Environment.SpecialFolder, string> folders)
                throw new PlatformNotSupportedException("The pinned Android runtime folder cache is unavailable.", error);
            lock (folders)
            {
                if (folders.TryGetValue(Environment.SpecialFolder.LocalApplicationData, out var current) && current != folder)
                {
                    try { ProbeCache(current); return; }
                    catch (Exception changedError) when (changedError is UnauthorizedAccessException or IOException) { }
                }
                folders[Environment.SpecialFolder.LocalApplicationData] = owned;
            }
            if (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) != owned)
                throw new InvalidOperationException("Android local application data correction did not take effect.");
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.runtime.local_data_repaired previous={folder} current={owned}");
        }
    }

    private static void ProbeCache(string folder)
    {
        var cache = Directory.CreateDirectory(Path.Combine(folder, ".cache"));
        var probe = Path.Combine(cache.FullName, "bpp-write-" + Guid.NewGuid().ToString("N"));
        try { File.WriteAllText(probe, string.Empty); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
    }
}
