using BazaarPlusPlus.Android;
using BazaarPlusPlus.Game.CollectionPanel.Data;
using BazaarGameShared.Domain.Core.Types;

var folder = Path.Combine(Path.GetTempPath(), "bpp-collection-test-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
try
{
    var database = Path.Combine(folder, "cards.db");
    var translations = Path.Combine(folder, "translations.json");
    var path = Path.Combine(folder, "cache.json");
    File.WriteAllText(database, "data-v1");
    File.WriteAllText(translations, "translation-v1");
    string Key(string language = "zh-CN") => AndroidCollectionCache.SourceKey(database, translations, "game-v1", language);
    var key = Key();
    Check(AndroidCollectionCache.Read(path, key) == null, "Missing cache must miss");
    var card = new CollectionCardVm
    {
        Id = Guid.NewGuid(), DisplayName = "测试卡", SearchText = "中文 search description", Description = "效果",
        Type = ECardType.Item, Size = ECardSize.Medium, StartingTier = ETier.Gold,
        Heroes = new[] { EHero.Common }, Tags = new[] { (ECardTag)1 }, HiddenTags = new[] { (EHiddenTag)2 },
        IsEnchantable = true, IsPackage = true, InternalName = "test", ArtKey = "art",
        Enchantments = new Dictionary<EEnchantmentType, CollectionCardEnchantmentFacets>
        {
            [(EEnchantmentType)1] = new((EEnchantmentType)1, new[] { (ECardTag)3 }, new[] { (EHiddenTag)4 }),
        },
    };
    var snapshot = new AndroidCollectionCache.Snapshot(3, new[] { card });
    AndroidCollectionCache.Write(path, key, snapshot);
    var loaded = AndroidCollectionCache.Read(path, key)!;
    var result = loaded.Cards.Single();
    Check(loaded.NativeCount == 3 && result.Id == card.Id && result.DisplayName == card.DisplayName &&
        result.SearchText == card.SearchText && result.Description == card.Description && result.IsPackage &&
        result.Size == card.Size && result.StartingTier == card.StartingTier && result.Type == card.Type &&
        result.InternalName == card.InternalName && result.ArtKey == card.ArtKey && result.IsEnchantable &&
        result.Heroes.SequenceEqual(card.Heroes) && result.Tags.SequenceEqual(card.Tags) && result.HiddenTags.SequenceEqual(card.HiddenTags) &&
        result.Enchantments.Single().Value.Tags.Single() == (ECardTag)3 &&
        result.Enchantments.Single().Value.HiddenTags.Single() == (EHiddenTag)4,
        "Persist all search, source, and filter fields");
    Check(AndroidCollectionCache.Read(path, Key("en")) == null, "Language change must invalidate");
    Check(AndroidCollectionCache.SourceKey(database, translations, "game-v2", "zh-CN") != key,
        "Game update must invalidate");
    File.WriteAllText(database, "data-v2");
    Check(Key() != key, "Database content change must invalidate");
    File.WriteAllText(database, "data-v1");
    File.WriteAllText(translations, "translation-v2");
    Check(Key() != key, "Translation update must invalidate");
    File.WriteAllText(translations, "translation-v1");
    File.WriteAllText(database + "-wal", "updated pages");
    Check(Key() != key, "SQLite WAL must invalidate");
    File.Delete(database + "-wal");
    File.WriteAllText(path, File.ReadAllText(path).Replace("art", "bad"));
    Check(AndroidCollectionCache.Read(path, key) == null, "Checksum must reject corrupted content");
    AndroidCollectionCache.Write(path, key, snapshot);
    try { AndroidCollectionCache.Write(path, key, new(3, new[] { card, card })); throw new Exception("Duplicate accepted"); }
    catch (InvalidDataException) { }
    Check(AndroidCollectionCache.Read(path, key)!.Cards.Length == 1, "Invalid write must preserve previous cache");
    Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "No partial writes left behind");
    Console.WriteLine("Collection cache round-trip, identity invalidation, corruption, and atomic replacement checks passed.");
}
finally { Directory.Delete(folder, recursive: true); }
