using BazaarPlusPlus.Android;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.IO.Compression;
using System.Text;
using BazaarPlusPlus.Game.LiveBuildPanel.Recommendations;

var payload = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "builds.json"));
var corpus = AndroidBuildCorpus.Parse(payload)
    ?? throw new Exception("The shipped recommendation seed must parse.");
if (corpus.BuildCount != 1) throw new Exception("Expected one seed build.");
var firstCard = Guid.Parse("11111111-1111-1111-1111-111111111111");
var matches = corpus.Find("Vanessa", new[] { firstCard });
if (matches.Count != 1 || matches[0].MatchedCards != 1 || matches[0].Build.Items.Count != 5)
    throw new Exception("A matching seed card must recall its board.");
if (corpus.Find("Pygmalien", new[] { firstCard }).Count != 0)
    throw new Exception("A hero without builds must not receive another hero's board.");
if (corpus.CountForHero("Vanessa") != 1 || corpus.CountForHero("Pygmalien") != 0)
    throw new Exception("Hero coverage must distinguish missing data from unmatched cards.");
if (AndroidBuildCorpus.Parse(payload.Replace("\"schema_version\": 2", "\"schema_version\": 3")) != null)
    throw new Exception("An unsupported schema must be rejected.");
var expanded = JsonNode.Parse(payload)!.AsObject();
expanded["cards"]!.AsArray().Add("66666666-6666-6666-6666-666666666666");
var heroRows = expanded["heroes"]!["Vanessa"]!;
var secondBuild = heroRows["builds"]![0]!.DeepClone();
secondBuild[0]![4] = 5;
secondBuild[1]![4]![0] = 5;
secondBuild[2]![4] = 600000;
heroRows["builds"]!.AsArray().Add(secondBuild);
var index = heroRows["card_index"]!.AsArray();
for (var card = 0; card < 4; card++) index[card]![1]!.AsArray().Add(1);
index.Add(JsonNode.Parse("[5,[1]]"));
var ranked = AndroidBuildCorpus.Parse(expanded.ToJsonString())
    ?? throw new Exception("Two valid builds must parse.");
var fifthCard = Guid.Parse("55555555-5555-5555-5555-555555555555");
var sixthCard = Guid.Parse("66666666-6666-6666-6666-666666666666");
if (ranked.Find("Vanessa", new[] { firstCard, fifthCard }).Select(match => match.Build.Id)
    .SequenceEqual(new[] { 0 }) == false)
    throw new Exception("Covered candidates must recall their intersection.");
if (ranked.Find("Vanessa", new[] { fifthCard, sixthCard }).Count != 2)
    throw new Exception("An empty intersection must fall back to the union.");
if (ranked.Find("Vanessa", new[] { firstCard, Guid.NewGuid() }).Count != 2)
    throw new Exception("An uncovered candidate must not erase covered matches.");
if (ranked.Find("Vanessa", new[] { firstCard },
        BuildLiveState.From(new[] { fifthCard }, Array.Empty<Guid>(), Array.Empty<Guid>()))[0].Build.Id != 0)
    throw new Exception("Live board weight must break a match tie before analyzer score.");
var cacheFolder = Path.Combine(Path.GetTempPath(), "bpp-android-builds-" + Guid.NewGuid().ToString("N"));
var cachePath = Path.Combine(cacheFolder, "builds.json");
try
{
    using var embedded = typeof(AndroidBuildRepository).Assembly.GetManifestResourceStream(
        "BazaarPlusPlus.Android.Data.BuildRecommendations.builds.json.gz")!;
    using var gzip = new GZipStream(embedded, CompressionMode.Decompress);
    using var reader = new StreamReader(gzip);
    var complete = await reader.ReadToEndAsync();
    var updated = JsonNode.Parse(complete)!.AsObject();
    updated["generated_at"] = "2026-10-28T12:00:00Z";
    updated["window"]!["start"] = "2026-10-21";
    updated["window"]!["end"] = "2026-10-27";
    var newer = updated.ToJsonString();
    var completeCorpus = AndroidBuildCorpus.Parse(complete)!;
    var newerCorpus = AndroidBuildCorpus.Parse(newer)!;
    var reduced = JsonNode.Parse(newer)!.AsObject();
    foreach (var entry in reduced["heroes"]!.AsObject())
    {
        var rows = entry.Value!["builds"]!.AsArray();
        while (rows.Count > (entry.Key == "Pygmalien" ? 100 : 0)) rows.RemoveAt(rows.Count - 1);
        var indices = new Dictionary<int, List<int>>();
        for (var buildId = 0; buildId < rows.Count; buildId++)
            foreach (var cardRef in rows[buildId]![0]!.AsArray()
                .Select(value => value!.GetValue<int>()).Distinct())
            {
                if (!indices.TryGetValue(cardRef, out var ids)) indices[cardRef] = ids = new();
                ids.Add(buildId);
            }
        var cardIndex = entry.Value["card_index"]!.AsArray();
        cardIndex.Clear();
        foreach (var indexEntry in indices.OrderBy(value => value.Key))
        {
            var ids = new JsonArray();
            foreach (var id in indexEntry.Value) ids.Add(id);
            cardIndex.Add(new JsonArray(indexEntry.Key, ids));
        }
    }
    var reducedText = reduced.ToJsonString().PadRight(51200);
    var reducedCorpus = AndroidBuildCorpus.Parse(reducedText)
        ?? throw new Exception("A valid smaller new-window corpus must parse.");
    if (reducedCorpus.BuildCount != 100 ||
        AndroidBuildAdoption.RejectionReason(completeCorpus, reducedCorpus,
            Encoding.UTF8.GetByteCount(reducedText)) != null)
        throw new Exception("A newer window with fewer builds must replace an older full snapshot.");
    reduced["window"]!["start"] = "2026-10-20";
    reduced["window"]!["end"] = "2026-10-26";
    var sameWindow = AndroidBuildCorpus.Parse(reduced.ToJsonString())!;
    if (AndroidBuildAdoption.RejectionReason(completeCorpus, sameWindow, 51200) != "same_window_shrink")
        throw new Exception("A collapsing response for the same window must be rejected.");
    if (AndroidBuildAdoption.RejectionReason(completeCorpus, newerCorpus,
        Encoding.UTF8.GetByteCount(newer)) != null)
        throw new Exception("A newer complete window must be adopted.");
    if (AndroidBuildAdoption.RejectionReason(newerCorpus, completeCorpus,
        Encoding.UTF8.GetByteCount(complete)) != "older_window")
        throw new Exception("An older window must not replace a newer one.");
    if (AndroidBuildAdoption.RejectionReason(completeCorpus, corpus, payload.Length) != "truncated")
        throw new Exception("A tiny response must be rejected.");
    var online = new AndroidBuildRepository(new HttpClient(new FixedHandler(complete)), cachePath,
        loadBundled: false);
    await online.WarmAsync();
    if (online.Current?.BuildCount != 6846 || !File.Exists(cachePath))
        throw new Exception("A valid remote corpus must publish and populate the cache.");
    var offline = new AndroidBuildRepository(new HttpClient(new FixedHandler(null)), cachePath,
        loadBundled: false);
    await offline.WarmAsync();
    if (offline.Current?.BuildCount != 6846)
        throw new Exception("A fresh cached corpus must load without network.");
    await File.WriteAllTextAsync(cachePath, reducedText);
    var fresh = new CountingHandler(null);
    var fromCache = new AndroidBuildRepository(new HttpClient(fresh), cachePath);
    await fromCache.WarmAsync();
    if (fromCache.Current?.WindowEnd != reducedCorpus.WindowEnd ||
        fromCache.Current.BuildCount != 100 || fresh.Count != 0)
        throw new Exception("A newer fresh cache must beat the bundled seed and skip network.");
    File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddHours(-21));
    var stale = new CountingHandler(complete);
    var staleCache = new AndroidBuildRepository(new HttpClient(stale), cachePath);
    await staleCache.WarmAsync();
    if (staleCache.Current?.WindowEnd != reducedCorpus.WindowEnd ||
        staleCache.Current.BuildCount != 100 || stale.Count != 1)
        throw new Exception("An expired cache must remain visible while older remote data is rejected.");
    var queuedPath = Path.Combine(cacheFolder, "queued.json");
    var delayed = new DelayedFirstHandler(complete, newer);
    var queued = new AndroidBuildRepository(new HttpClient(delayed), queuedPath);
    var warming = queued.WarmAsync();
    await delayed.FirstStarted;
    var refreshing = queued.RefreshAsync();
    delayed.ReleaseFirst();
    await Task.WhenAll(warming, refreshing);
    if (delayed.Count != 2 || queued.Current?.WindowEnd != newerCorpus.WindowEnd ||
        AndroidBuildCorpus.Parse(await File.ReadAllTextAsync(queuedPath))?.WindowEnd != newerCorpus.WindowEnd)
        throw new Exception("A refresh clicked during warmup must run afterward and win the cache.");
    var bundledPath = Path.Combine(cacheFolder, "bundled.json");
    await File.WriteAllTextAsync(bundledPath, payload);
    var bundled = new AndroidBuildRepository(new HttpClient(new FixedHandler(payload)), bundledPath);
    await bundled.WarmAsync();
    if (bundled.Current?.CountForHero("Pygmalien") != 1004 || bundled.Current.BuildCount != 6846)
        throw new Exception("The release's bundled corpus must retain Pygmalien recommendations.");
    if (await File.ReadAllTextAsync(bundledPath) != payload)
        throw new Exception("A tiny cached or remote response must not replace the bundled corpus.");
}
finally
{
    if (Directory.Exists(cacheFolder)) Directory.Delete(cacheFolder, recursive: true);
}
Console.WriteLine("Android build corpus parsing and recall passed.");
if (args.Length > 0)
{
    var current = AndroidBuildCorpus.Parse(File.ReadAllText(args[0]))
        ?? throw new Exception("The supplied corpus must parse.");
    Console.WriteLine($"Supplied corpus: {current.BuildCount} builds, generated {current.GeneratedAt:u}.");
}

sealed class FixedHandler(string? body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(body == null
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
}

sealed class CountingHandler(string? body) : HttpMessageHandler
{
    internal int Count { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Count++;
        return Task.FromResult(body == null
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

sealed class DelayedFirstHandler(string first, string second) : HttpMessageHandler
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task FirstStarted => _started.Task;
    internal int Count { get; private set; }
    internal void ReleaseFirst() => _release.SetResult();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Count++;
        if (Count == 1)
        {
            _started.SetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(first) };
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(second) };
    }
}
