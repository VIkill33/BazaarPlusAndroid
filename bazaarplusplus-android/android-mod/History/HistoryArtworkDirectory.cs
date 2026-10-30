namespace BazaarPlusPlus.Android;

internal static class HistoryArtworkDirectory
{
    public static string Prepare(string artworkRoot, string version)
    {
        Directory.CreateDirectory(artworkRoot);
        // Protect all cache versions, including existing PNGs, before publishing any new images.
        // Do not continue exporting if the exclusion marker cannot be created.
        Exclude(artworkRoot);
        // Keep new images outside the previously indexed PNG tree. Readers decode the bytes,
        // so the cache does not need a public media extension.
        var directory = Path.Combine(artworkRoot, ".cache", "v3", version);
        Directory.CreateDirectory(directory);
        Exclude(directory);
        return directory;
    }

    public static string ImagePath(string directory, string key) => Path.Combine(directory, key + ".bppart");

    public static void Write(string directory, string key, byte[] bytes)
    {
        // Restore a removed marker before each publication, including retries in a live session.
        Exclude(directory);
        var path = ImagePath(directory, key);
        try { File.WriteAllBytes(path + ".part", bytes); File.Move(path + ".part", path, true); }
        finally { if (File.Exists(path + ".part")) File.Delete(path + ".part"); }
    }

    private static void Exclude(string directory)
    {
        using (File.Open(Path.Combine(directory, ".nomedia"), FileMode.OpenOrCreate,
            FileAccess.Write, FileShare.Read)) { }
    }
}
