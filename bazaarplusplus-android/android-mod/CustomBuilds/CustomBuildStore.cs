using System.Text.Json;

namespace BazaarPlusPlus.Android;

// Slot is the first of the ten board slots the item occupies; files written before slots existed omit it.
internal sealed record CustomBuildItem(Guid TemplateId, int Size, int? Slot = null);
// Star is set while the build's cards are flagged in shops and rewards; its value picks the flag colour.
internal sealed record CustomBuild(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<CustomBuildItem> Items, int? Star = null, bool? Recommended = null);

internal sealed class CustomBuildDraft
{
    private List<CustomBuildItem> _items = new();
    internal IReadOnlyList<CustomBuildItem> Items => _items.AsReadOnly();
    internal int UsedSlots => _items.Sum(item => item.Size);

    internal CustomBuildDraft() { }
    internal CustomBuildDraft(IEnumerable<CustomBuildItem> items) => _items = Arrange(items);

    // Takes the first gap that fits; with enough free slots but no such gap, neighbours close up.
    internal bool TryAdd(Guid templateId, int size)
    {
        if (templateId == Guid.Empty || size is < 1 or > 3 || UsedSlots + size > 10) return false;
        var slot = Enumerable.Range(0, 11 - size).Where(start => _items.All(item =>
            item.Slot!.Value + item.Size <= start || item.Slot!.Value >= start + size)).DefaultIfEmpty(10).First();
        _items = Arrange(_items.Append(new CustomBuildItem(templateId, size, slot)));
        return true;
    }

    internal void RemoveAt(int index)
    {
        if (index >= 0 && index < _items.Count) _items.RemoveAt(index);
    }

    // Drops the item with its left edge on `slot`. A free run is taken as is; an occupied one pushes neighbours aside.
    internal void MoveTo(int index, int slot)
    {
        if (index < 0 || index >= _items.Count) return;
        var moved = _items[index];
        moved = moved with { Slot = Math.Clamp(slot, 0, 10 - moved.Size) };
        _items = Arrange(_items.Where((_, other) => other != index).Prepend(moved));
    }

    // Returns the items left to right on non-overlapping slots inside the ten-slot board.
    // Items keep their slot when they can; overlaps resolve by item centre, the earlier input winning a tie.
    internal static List<CustomBuildItem> Arrange(IEnumerable<CustomBuildItem> source)
    {
        var items = source.ToList();
        var packed = items.Any(item => item.Slot == null);
        if (!packed) items = items.OrderBy(item => 2 * item.Slot!.Value + item.Size).ToList();
        var edge = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var slot = packed ? edge : Math.Max(items[index].Slot!.Value, edge);
            items[index] = items[index] with { Slot = slot };
            edge = slot + items[index].Size;
        }
        if (edge <= 10) return items;
        edge = 10;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            edge = Math.Min(items[index].Slot!.Value, edge - items[index].Size);
            items[index] = items[index] with { Slot = edge };
        }
        return items;
    }
}

// A failed read never silently turns an unreadable collection into an empty writable one.
// Publish in-memory changes only after atomic replacement of the on-disk document succeeds.
internal sealed class CustomBuildStore(string path)
{
    private sealed record Document(int Schema, CustomBuild[] Builds);
    private CustomBuild[] _builds = Array.Empty<CustomBuild>();
    internal IReadOnlyList<CustomBuild> Builds => Array.AsReadOnly(_builds);
    private bool _loaded;

    internal void Load()
    {
        _loaded = false;
        var builds = Array.Empty<CustomBuild>();
        if (File.Exists(path))
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (document is not { Schema: 1, Builds: not null }) throw new InvalidDataException("Unsupported custom builds file.");
            builds = document.Builds;
            if (builds.Any(build => build == null || build.Id == Guid.Empty)
                || builds.Select(build => build.Id).Distinct().Count() != builds.Length)
                throw new InvalidDataException("Invalid custom build identity.");
            foreach (var build in builds) Validate(build.Items);
            builds = builds.Select(build => build with { Items = Arranged(build.Items) }).ToArray();
        }
        _builds = builds;
        _loaded = true;
    }

    internal void Add(IReadOnlyList<CustomBuildItem> items)
    {
        Validate(items);
        Persist(_builds.Append(new CustomBuild(Guid.NewGuid(), DateTimeOffset.UtcNow, Arranged(items), Recommended: false)).ToArray());
    }

    // Replaces the items in place: identity, creation time and list position are kept.
    internal bool Update(Guid id, IReadOnlyList<CustomBuildItem> items)
    {
        Validate(items);
        if (_builds.All(build => build.Id != id)) return false;
        Persist(_builds.Select(build => build.Id == id ? build with { Items = Arranged(items), Recommended = false } : build).ToArray());
        return true;
    }

    internal const int StarColors = 6;
    internal static int StarColor(int star) => (int)((uint)star % StarColors);

    internal bool SetStar(Guid id, bool starred)
    {
        var build = _builds.FirstOrDefault(candidate => candidate.Id == id);
        if (build == null || starred == (build.Star != null)) return false;
        var star = starred ? NextStar() : (int?)null;
        Persist(_builds.Select(candidate => candidate.Id == id ? candidate with { Star = star } : candidate).ToArray());
        return true;
    }

    // Saves the items as a starred build, or stars the saved build that already holds exactly these items.
    internal void AddStarred(IReadOnlyList<CustomBuildItem> items, bool recommended = false)
    {
        Validate(items);
        var arranged = Arranged(items);
        var same = _builds.FirstOrDefault(build => build.Items.SequenceEqual(arranged) && (build.Recommended == recommended || (!recommended && build.Recommended == null)));
        if (same != null) SetStar(same.Id, true);
        else Persist(_builds.Append(new CustomBuild(Guid.NewGuid(), DateTimeOffset.UtcNow, arranged, NextStar(), recommended)).ToArray());
    }

    internal CustomBuild? FindRecommended(IReadOnlyList<CustomBuildItem> items) =>
        _builds.FirstOrDefault(build => build.Recommended == true && build.Items.SequenceEqual(Arranged(items)));

    // Returns true when removed, false when saved. Persistence errors leave memory and disk unchanged.
    internal bool ToggleRecommended(IReadOnlyList<CustomBuildItem> items)
    {
        Validate(items);
        if (FindRecommended(items) is { } saved) { Delete(saved.Id); return true; }
        AddStarred(items, recommended: true);
        return false;
    }

    // The first colour no starred build uses; once all are taken, colours repeat.
    private int NextStar()
    {
        var used = _builds.Where(build => build.Star != null).Select(build => StarColor(build.Star!.Value)).ToList();
        return Enumerable.Range(0, StarColors).Where(color => !used.Contains(color)).DefaultIfEmpty(used.Count % StarColors).First();
    }

    private static IReadOnlyList<CustomBuildItem> Arranged(IReadOnlyList<CustomBuildItem> items) =>
        Array.AsReadOnly(CustomBuildDraft.Arrange(items).ToArray());

    internal bool Delete(Guid id)
    {
        var remaining = _builds.Where(build => build.Id != id).ToArray();
        if (remaining.Length == _builds.Length) return false;
        Persist(remaining);
        return true;
    }

    internal void Clear() => Persist(Array.Empty<CustomBuild>());

    private static void Validate(IReadOnlyList<CustomBuildItem>? items)
    {
        if (items == null || items.Count is < 1 or > 10 || items.Any(item => item == null
            || item.TemplateId == Guid.Empty || item.Size is < 1 or > 3) || items.Sum(item => item.Size) > 10)
            throw new InvalidDataException("A custom build must contain 1–10 occupied slots.");
    }

    private void Persist(CustomBuild[] builds)
    {
        if (!_loaded) throw new InvalidDataException("Load custom builds before saving.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".part";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, builds)));
            File.Move(temporary, path, overwrite: true);
            _builds = builds;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
