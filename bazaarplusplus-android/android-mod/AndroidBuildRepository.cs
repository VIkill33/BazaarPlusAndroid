using System.Net.Http;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;

namespace BazaarPlusPlus.Android;

internal sealed class AndroidBuildRepository(HttpClient client, string cachePath,
    Action<string>? info = null, Action<string>? warning = null)
{
    private const string Url = "https://bpp-metrics-v6.bazaarplusplus.com/builds/latest.json";
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
        try
        {
            if (Current == null && File.Exists(cachePath))
            {
                var cached = AndroidBuildCorpus.Parse(await File.ReadAllTextAsync(cachePath));
                if (cached != null) Volatile.Write(ref _corpus, cached);
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
}
