using BazaarPlusPlus.Android;

var temporary = Path.Combine(Path.GetTempPath(), "bpp-artwork-" + Guid.NewGuid());
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}
try
{
    var root = Path.Combine(temporary, "history-art");
    var current = HistoryArtworkDirectory.Prepare(root, "first");
    var marker = Path.Combine(root, ".nomedia");
    Check(File.Exists(marker) && new FileInfo(marker).Length == 0, "Fresh cache must be excluded from media scanning");
    Check(current == Path.Combine(root, "v2", "first") && Directory.Exists(current), "Reader-compatible cache path");

    var image = Path.Combine(current, "card-example.png");
    byte[] bytes = [137, 80, 78, 71];
    File.WriteAllBytes(image, bytes);
    var legacy = Path.Combine(root, "v1", "older");
    Directory.CreateDirectory(legacy);
    File.WriteAllBytes(Path.Combine(legacy, "card-example.png"), bytes);
    File.Delete(marker);
    HistoryArtworkDirectory.Prepare(root, "first");
    Check(File.Exists(marker), "Existing cache must gain missing marker on upgrade");
    Check(File.ReadAllBytes(image).SequenceEqual(bytes), "Existing illustration remains readable");
    Check(File.ReadAllBytes(Path.Combine(legacy, "card-example.png")).SequenceEqual(bytes), "Legacy cache remains intact");

    var stamp = File.GetLastWriteTimeUtc(marker);
    HistoryArtworkDirectory.Prepare(root, "second");
    HistoryArtworkDirectory.Prepare(root, "second");
    Check(File.GetLastWriteTimeUtc(marker) == stamp, "Repeated initialization must not rewrite marker");
    Check(Directory.Exists(Path.Combine(root, "v2", "second")), "Game version change remains supported");

    var blocked = Path.Combine(temporary, "blocked");
    Directory.CreateDirectory(Path.Combine(blocked, ".nomedia"));
    var rejected = false;
    try { HistoryArtworkDirectory.Prepare(blocked, "version"); }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
    Check(rejected && !Directory.Exists(Path.Combine(blocked, "v2")), "Marker failure must stop cache initialization before image export");
    Console.WriteLine($"History artwork directory: {checks} checks passed");
}
finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
