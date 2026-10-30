using System.Net.Http;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;

namespace BazaarPlusPlus.Android;

internal sealed class AndroidBuildRepository(HttpClient client, string cachePath,
    Action<string>? info = null, Action<string>? warning = null, bool loadBundled = true,
    string? productVersion = null)
{
    private const string Url = "https://bpp-metrics-v6.bazaarplusplus.com/builds/latest.json";
    private const string BundledResource = "BazaarPlusPlus.Android.Data.BuildRecommendations.builds.json.gz";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(20);
    private readonly object _sync = new();
    private AndroidBuildCorpus? _corpus;
    private Task? _flight;
    private string? _lastOnlineFailure;

    internal AndroidBuildCorpus? Current => Volatile.Read(ref _corpus);
    internal string? LastOnlineFailure => Volatile.Read(ref _lastOnlineFailure);

    internal Task WarmAsync()
    {
        lock (_sync) return _flight ??= Task.Run(LoadStartupAsync);
    }

    internal void Warm() => _ = WarmAsync();

    internal Task RefreshAsync()
    {
        lock (_sync)
        {
            var previous = _flight;
            return _flight = Task.Run(async () =>
            {
                if (previous != null) await previous;
                await LoadRemoteAsync();
            });
        }
    }

    internal IReadOnlyList<AndroidBuildMatch> Find(string hero, IReadOnlyCollection<Guid> selected,
        BuildLiveState? liveState = null) =>
        Current?.Find(hero, selected, liveState) ?? Array.Empty<AndroidBuildMatch>();

    private async Task LoadStartupAsync()
    {
        AndroidBuildCorpus? bundled = null;
        if (loadBundled)
        {
            try
            {
                using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(BundledResource);
                if (resource != null)
                {
                    using var gzip = new GZipStream(resource, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip, Encoding.UTF8);
                    bundled = AndroidBuildCorpus.Parse(await reader.ReadToEndAsync());
                    if (bundled != null)
                        info?.Invoke($"[BPP][Android] event=android.builds.bundled count={bundled.BuildCount} generated_at={bundled.GeneratedAt:O} window_end={bundled.WindowEnd:yyyy-MM-dd}");
                }
            }
            catch (Exception error)
            {
                warning?.Invoke($"[BPP][Android] event=android.builds.bundled_failed exception={error}");
            }
        }

        var local = bundled;
        var cacheWon = false;
        try
        {
            if (File.Exists(cachePath))
            {
                var cachedText = await File.ReadAllTextAsync(cachePath);
                var cached = AndroidBuildCorpus.Parse(cachedText);
                if (cached == null)
                    warning?.Invoke("[BPP][Android] event=android.builds.cache_failed reason=invalid_response");
                else if (AndroidBuildAdoption.RejectionReason(local, cached,
                    Encoding.UTF8.GetByteCount(cachedText)) == null)
                {
                    local = cached;
                    cacheWon = true;
                    info?.Invoke($"[BPP][Android] event=android.builds.cache_loaded count={cached.BuildCount} generated_at={cached.GeneratedAt:O} window_end={cached.WindowEnd:yyyy-MM-dd}");
                }
            }
        }
        catch (Exception error)
        {
            warning?.Invoke($"[BPP][Android] event=android.builds.cache_failed exception={error}");
        }
        Volatile.Write(ref _corpus, local);
        if (cacheWon && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < CacheDuration)
        {
            info?.Invoke("[BPP][Android] event=android.builds.skipped_fresh");
            return;
        }
        await LoadRemoteAsync();
    }

    private async Task LoadRemoteAsync()
    {
        Volatile.Write(ref _lastOnlineFailure, null);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);
            if (productVersion != null)
                request.Headers.UserAgent.ParseAdd($"BazaarPlusAndroid/{productVersion}");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var document = await response.Content.ReadAsStringAsync();
            var candidate = AndroidBuildCorpus.Parse(document)
                ?? throw new InvalidDataException("Build corpus schema was rejected.");
            var bytes = Encoding.UTF8.GetByteCount(document);
            var retained = Current;
            var rejection = AndroidBuildAdoption.RejectionReason(retained, candidate, bytes);
            if (rejection != null)
            {
                Volatile.Write(ref _lastOnlineFailure, "rejected");
                warning?.Invoke($"[BPP][Android] event=android.builds.remote_rejected reason={rejection} incoming={candidate.BuildCount} retained={retained?.BuildCount ?? 0} bytes={bytes} incoming_window_end={candidate.WindowEnd:yyyy-MM-dd} retained_window_end={retained?.WindowEnd:yyyy-MM-dd}");
                return;
            }
            Volatile.Write(ref _corpus, candidate);
            info?.Invoke($"[BPP][Android] event=android.builds.ready count={candidate.BuildCount} generated_at={candidate.GeneratedAt:O} window_end={candidate.WindowEnd:yyyy-MM-dd}");
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
            Volatile.Write(ref _lastOnlineFailure, "failed");
            warning?.Invoke($"[BPP][Android] event=android.builds.remote_failed cached_count={Current?.BuildCount ?? 0} exception={error}");
        }
    }
}
