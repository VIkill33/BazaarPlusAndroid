using System.IO.Compression;

if (args.Length != 1)
    throw new ArgumentException("Pass the fixture directory exported by LauncherUnityLibrariesTest.");
var root = Path.GetFullPath(args[0]);

string ResolveArchive(string configName)
{
    var inSection = false;
    foreach (var raw in File.ReadLines(Path.Combine(root, configName)))
    {
        var line = raw.Trim();
        if (line.StartsWith('[')) inSection = line == "[IL2CPP]";
        else if (inSection && line.StartsWith("UnityBaseLibrariesSource", StringComparison.Ordinal))
        {
            var source = line[(line.IndexOf('=') + 1)..].Trim();
            // Pinned BepInEx DownloadUnityAssemblies resolves a URL's basename, not the Unity version.
            var uriIsValid = Uri.TryCreate(source, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            return Path.Combine(root, "unity-libs", Path.GetFileName(uriIsValid ? uri!.AbsolutePath : source));
        }
    }
    throw new InvalidDataException("Missing IL2CPP base library source");
}

var legacy = ResolveArchive("before.cfg");
if (Path.GetFileName(legacy) != "Managed.zip") throw new Exception("Expected legacy filename mismatch");
var legacyBytes = File.ReadAllBytes(legacy);
try
{
    using var invalid = new ZipArchive(File.OpenRead(legacy), ZipArchiveMode.Read);
    throw new Exception("Expected legacy End of Central Directory failure");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Legacy consumer reproduced: {ex.Message}");
}
var selected = ResolveArchive("BepInEx.cfg");
if (Path.GetFileName(selected) != "6000.3.11.zip") throw new Exception("Wrong verified archive selected");
using (var zip = new ZipArchive(File.OpenRead(selected), ZipArchiveMode.Read))
{
    if (zip.Entries.Count == 0 || !zip.Entries.Any(e => e.Name.EndsWith(".dll", StringComparison.Ordinal)))
        throw new Exception("Missing managed assemblies");
    foreach (var entry in zip.Entries)
    {
        using var content = entry.Open();
        content.CopyTo(Stream.Null);
    }
    Console.WriteLine($"Fixed .NET consumer read all {zip.Entries.Count} entries from the verified ZIP");
}
if (!legacyBytes.SequenceEqual(File.ReadAllBytes(legacy))) throw new Exception("Unrelated old cache changed");
if (Directory.Exists(Path.Combine(root, "interop"))) throw new Exception("Test relied on existing game proxies");
