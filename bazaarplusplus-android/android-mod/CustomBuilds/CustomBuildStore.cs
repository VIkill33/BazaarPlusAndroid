using System.Text.Json;

namespace BazaarPlusPlus.Android;

internal sealed record CustomBuildItem(Guid TemplateId, int Size);
internal sealed record CustomBuild(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<CustomBuildItem> Items);

internal sealed class CustomBuildDraft
{
    private readonly List<CustomBuildItem> _items = new();
    internal IReadOnlyList<CustomBuildItem> Items => _items.AsReadOnly();
    internal int UsedSlots => _items.Sum(item => item.Size);

    internal bool TryAdd(Guid templateId, int size)
    {
        if (templateId == Guid.Empty || size is < 1 or > 3 || UsedSlots + size > 10) return false;
        _items.Add(new(templateId, size));
        return true;
    }

    internal void RemoveAt(int index)
    {
        if (index >= 0 && index < _items.Count) _items.RemoveAt(index);
    }

    internal void Move(int index, int direction)
    {
        var target = index + direction;
        if (direction is not (-1 or 1) || index < 0 || index >= _items.Count || target < 0 || target >= _items.Count) return;
        (_items[index], _items[target]) = (_items[target], _items[index]);
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
        }
        _builds = builds;
        _loaded = true;
    }

    internal void Add(IReadOnlyList<CustomBuildItem> items)
    {
        Validate(items);
        Persist(_builds.Append(new CustomBuild(Guid.NewGuid(), DateTimeOffset.UtcNow,
            Array.AsReadOnly(items.ToArray()))).ToArray());
    }

    internal bool Delete(Guid id)
    {
        var remaining = _builds.Where(build => build.Id != id).ToArray();
        if (remaining.Length == _builds.Length) return false;
        Persist(remaining);
        return true;
    }

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
