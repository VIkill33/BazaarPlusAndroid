namespace BazaarPlusPlus.Android;

internal static class HistoryArtworkDirectory
{
    public static string Prepare(string artworkRoot, string version)
    {
        Directory.CreateDirectory(artworkRoot);
        // Protect all cache versions, including existing PNGs, before publishing any new images.
        // Do not continue exporting if the exclusion marker cannot be created.
        using (File.Open(Path.Combine(artworkRoot, ".nomedia"), FileMode.OpenOrCreate,
            FileAccess.Write, FileShare.Read)) { }
        var directory = Path.Combine(artworkRoot, "v2", version);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
