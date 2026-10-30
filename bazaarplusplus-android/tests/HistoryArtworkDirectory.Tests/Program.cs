using BazaarPlusPlus.Android;

var temporary = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "bpp-artwork-" + Guid.NewGuid());
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}
try
{
    var root = Path.Combine(temporary, "BepInEx", "history-art");
    var version = new string('a', 64);
    var current = HistoryArtworkDirectory.Prepare(root, version);
    var marker = Path.Combine(root, ".nomedia");
    var localMarker = Path.Combine(current, ".nomedia");
    Check(File.Exists(marker) && new FileInfo(marker).Length == 0, "Fresh cache must be excluded from media scanning");
    Check(current == Path.Combine(root, ".cache", "v3", version) && Directory.Exists(current), "New cache uses hidden tree");
    Check(File.Exists(localMarker), "Image directory has its own exclusion marker");

    var image = HistoryArtworkDirectory.ImagePath(current, "card-example");
    byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
    HistoryArtworkDirectory.Write(current, "card-example", bytes);
    Check(image.EndsWith(".bppart") && !Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories).Any(), "New exports never publish public PNG filenames");
    Check(File.ReadAllBytes(image).SequenceEqual(bytes), "Cache publication preserves PNG payload bytes");
    Check(!File.Exists(image + ".part"), "Successful publication leaves no temporary file");

    foreach (var schema in new[] { "v1", "v2" })
    {
        var legacy = Path.Combine(root, schema, "older");
        Directory.CreateDirectory(legacy);
        File.WriteAllBytes(Path.Combine(legacy, "card-example.png"), bytes);
    }
    File.Delete(marker);
    HistoryArtworkDirectory.Prepare(root, version);
    Check(File.Exists(marker), "Existing cache must regain missing root marker");
    Check(File.ReadAllBytes(image).SequenceEqual(bytes), "Existing illustration remains readable");
    foreach (var schema in new[] { "v1", "v2" })
        Check(File.ReadAllBytes(Path.Combine(root, schema, "older", "card-example.png")).SequenceEqual(bytes), "Legacy cache remains untouched: " + schema);

    File.Delete(localMarker);
    HistoryArtworkDirectory.Write(current, "card-example", bytes);
    Check(File.Exists(localMarker), "Write restores marker removed during the session");
    var stamp = File.GetLastWriteTimeUtc(marker);
    HistoryArtworkDirectory.Prepare(root, "second");
    HistoryArtworkDirectory.Prepare(root, "second");
    Check(File.GetLastWriteTimeUtc(marker) == stamp, "Repeated initialization must not rewrite marker");
    Check(File.Exists(Path.Combine(root, ".cache", "v3", "second", ".nomedia")), "Game version change creates a protected directory");

    var blocked = Path.Combine(temporary, "blocked");
    Directory.CreateDirectory(Path.Combine(blocked, ".nomedia"));
    var rejected = false;
    try { HistoryArtworkDirectory.Prepare(blocked, version); }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
    Check(rejected && !Directory.Exists(Path.Combine(blocked, ".cache")), "Root marker failure must stop initialization");

    File.Delete(localMarker);
    Directory.CreateDirectory(localMarker);
    rejected = false;
    try { HistoryArtworkDirectory.Write(current, "blocked", bytes); }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
    Check(rejected && !File.Exists(HistoryArtworkDirectory.ImagePath(current, "blocked")), "Local marker failure prevents publication");
    Check(File.ReadAllBytes(image).SequenceEqual(bytes), "Failed write preserves existing cache bytes");
    Directory.Delete(localMarker);
    HistoryArtworkDirectory.Write(current, "card-example", bytes);
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(current)!, "current.txt"), version);
    Console.WriteLine($"History artwork directory: {checks} checks passed");
}
finally { if (args.Length == 0 && Directory.Exists(temporary)) Directory.Delete(temporary, true); }
