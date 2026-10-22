using BazaarPlusPlus.Android;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
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
    var online = new AndroidBuildRepository(new HttpClient(new FixedHandler(payload)), cachePath,
        loadBundled: false);
    await online.WarmAsync();
    if (online.Current?.BuildCount != 1 || !File.Exists(cachePath))
        throw new Exception("A valid remote corpus must publish and populate the cache.");
    var offline = new AndroidBuildRepository(new HttpClient(new FixedHandler(null)), cachePath,
        loadBundled: false);
    await offline.WarmAsync();
    if (offline.Find("Vanessa", new[] { firstCard }).Count != 1)
        throw new Exception("An offline startup must retain matching cached builds.");
    var bundledPath = Path.Combine(cacheFolder, "bundled.json");
    await File.WriteAllTextAsync(bundledPath, payload);
    var bundled = new AndroidBuildRepository(new HttpClient(new FixedHandler(payload)), bundledPath);
    await bundled.WarmAsync();
    if (bundled.Current?.CountForHero("Pygmalien") != 834 || bundled.Current.BuildCount < 4000)
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
