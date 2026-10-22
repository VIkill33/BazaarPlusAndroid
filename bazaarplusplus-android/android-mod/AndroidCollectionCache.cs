using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BazaarPlusPlus.Game.CollectionPanel.Data;

namespace BazaarPlusPlus.Android;

// Only managed catalog fields go on disk; native templates remain owned by GameData.
internal static class AndroidCollectionCache
{
    internal sealed record Snapshot(int NativeCount, CollectionCardVm[] Cards);
    private sealed record Envelope(int Schema, string Source, string Sha256, string Payload);
    private const int Schema = 1;

    internal static string SourceKey(string database, string translations, string gameVersion, string language)
    {
        var sources = new List<string>
        {
            Schema.ToString(), typeof(AndroidCollectionCache).Assembly.ManifestModule.ModuleVersionId.ToString(),
            gameVersion, language, Path.GetFullPath(database), HashFile(database),
        };
        foreach (var path in new[] { database + "-wal", translations })
            sources.Add(File.Exists(path) ? HashFile(path) : "absent");
        return Hash(Encoding.UTF8.GetBytes(string.Join("\n", sources)));
    }

    internal static Snapshot? Read(string path, string source)
    {
        if (!File.Exists(path)) return null;
        var envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path));
        if (envelope == null || envelope.Schema != Schema || envelope.Source != source ||
            envelope.Payload == null || Hash(Encoding.UTF8.GetBytes(envelope.Payload)) != envelope.Sha256)
            return null;
        var snapshot = JsonSerializer.Deserialize<Snapshot>(envelope.Payload);
        Validate(snapshot);
        return snapshot;
    }

    internal static void Write(string path, string source, Snapshot snapshot)
    {
        Validate(snapshot);
        var payload = JsonSerializer.Serialize(snapshot);
        var envelope = new Envelope(Schema, source, Hash(Encoding.UTF8.GetBytes(payload)), payload);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(pending, JsonSerializer.Serialize(envelope));
            File.Move(pending, path, overwrite: true);
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }

    private static void Validate(Snapshot? snapshot)
    {
        if (snapshot?.Cards == null || snapshot.Cards.Length == 0 || snapshot.NativeCount < snapshot.Cards.Length ||
            snapshot.Cards.Any(card => card == null || card.Id == Guid.Empty || card.Heroes == null ||
                card.Tags == null || card.HiddenTags == null || card.Enchantments == null ||
                card.DisplayName == null || card.SearchText == null) ||
            snapshot.Cards.Select(card => card.Id).Distinct().Count() != snapshot.Cards.Length)
            throw new InvalidDataException("Invalid collection cache snapshot.");
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
