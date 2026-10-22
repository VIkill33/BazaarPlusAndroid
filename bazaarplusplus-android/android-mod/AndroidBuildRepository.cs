using System.Net.Http;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;

namespace BazaarPlusPlus.Android;

internal sealed class AndroidBuildRepository(HttpClient client, string cachePath,
    Action<string>? info = null, Action<string>? warning = null, bool loadBundled = true)
{
    private const string Url = "https://bpp-metrics-v6.bazaarplusplus.com/builds/latest.json";
    private const string BundledResource = "BazaarPlusPlus.Android.Data.BuildRecommendations.builds.json.gz";
    // Match the desktop production package's minimum for a complete build catalog.
    private const int MinimumRemoteBytes = 51200;
    private readonly object _sync = new();
    private AndroidBuildCorpus? _corpus;
    private Task? _warmTask;

    internal AndroidBuildCorpus? Current => Volatile.Read(ref _corpus);

    internal Task WarmAsync()
    {
        lock (_sync) return _warmTask ??= Task.Run(LoadAsync);
    }

    internal void Warm() => _ = WarmAsync();

    internal Task RefreshAsync()
    {
        lock (_sync)
            return _warmTask is { IsCompleted: false } ? _warmTask : _warmTask = Task.Run(LoadAsync);
    }

    internal IReadOnlyList<AndroidBuildMatch> Find(string hero, IReadOnlyCollection<Guid> selected,
        BuildLiveState? liveState = null) =>
        Current?.Find(hero, selected, liveState) ?? Array.Empty<AndroidBuildMatch>();

    private async Task LoadAsync()
    {
        if (Current == null && loadBundled)
        {
            try
            {
                using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(BundledResource);
                if (resource != null)
                {
                    using var gzip = new GZipStream(resource, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip, Encoding.UTF8);
                    var bundled = AndroidBuildCorpus.Parse(await reader.ReadToEndAsync());
                    if (bundled != null)
                    {
                        Volatile.Write(ref _corpus, bundled);
                        info?.Invoke($"[BPP][Android] event=android.builds.bundled count={bundled.BuildCount} generated_at={bundled.GeneratedAt:O}");
                    }
                }
            }
            catch (Exception error)
            {
                warning?.Invoke($"[BPP][Android] event=android.builds.bundled_failed exception={error}");
            }
        }

        try
        {
            if (File.Exists(cachePath))
            {
                var cached = AndroidBuildCorpus.Parse(await File.ReadAllTextAsync(cachePath));
                if (cached != null && HealthyReplacement(cached, Current,
                    new FileInfo(cachePath).Length))
                    Volatile.Write(ref _corpus, cached);
            }
        }
        catch (Exception error)
        {
            warning?.Invoke($"[BPP][Android] event=android.builds.cache_failed exception={error}");
        }

        try
        {
            using var response = await client.GetAsync(Url);
            response.EnsureSuccessStatusCode();
            var document = await response.Content.ReadAsStringAsync();
            var current = AndroidBuildCorpus.Parse(document)
                ?? throw new InvalidDataException("Build corpus schema was rejected.");
            var bytes = Encoding.UTF8.GetByteCount(document);
            if (!HealthyReplacement(current, Current, bytes))
            {
                warning?.Invoke($"[BPP][Android] event=android.builds.remote_rejected incoming={current.BuildCount} retained={Current?.BuildCount ?? 0} bytes={bytes}");
                return;
            }
            Volatile.Write(ref _corpus, current);
            info?.Invoke($"[BPP][Android] event=android.builds.ready count={current.BuildCount} generated_at={current.GeneratedAt:O}");
            try
            {
                var directory = Path.GetDirectoryName(cachePath)
                    ?? throw new InvalidOperationException("Build cache has no parent directory.");
                Directory.CreateDirectory(directory);
                var pending = cachePath + ".tmp";
                await File.WriteAllTextAsync(pending, document);
                File.Move(pending, cachePath, overwrite: true);
            }
            catch (Exception error)
            {
                warning?.Invoke($"[BPP][Android] event=android.builds.cache_write_failed exception={error}");
            }
        }
        catch (Exception error)
        {
            warning?.Invoke($"[BPP][Android] event=android.builds.remote_failed cached_count={Current?.BuildCount ?? 0} exception={error}");
        }
    }

    private static bool HealthyReplacement(AndroidBuildCorpus candidate, AndroidBuildCorpus? previous,
        long bytes)
    {
        if (previous == null) return true;
        if (bytes < MinimumRemoteBytes || candidate.BuildCount < previous.BuildCount / 4)
            return false;
        return previous.HeroBuildCounts.All(hero => hero.Value == 0 ||
            candidate.CountForHero(hero.Key) >= Math.Max(1, hero.Value / 10));
    }
}
