using BazaarPlusPlus.Android;
using System.Text.Json;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
    { checks++; return; }
    throw new Exception(message);
}
Check(CustomBuildViewport.IsFullyVisible(0, 250, 0, 820), "First row fits at scroll start");
Check(CustomBuildViewport.IsFullyVisible(500, 250, 0, 820), "Third complete row fits");
Check(!CustomBuildViewport.IsFullyVisible(750, 250, 0, 820), "Partial bottom row cannot leak native art");
Check(!CustomBuildViewport.IsFullyVisible(0, 250, 180, 820), "Partial top row cannot cover title");
Check(CustomBuildViewport.IsFullyVisible(750, 250, 180, 820), "Final row appears completely at scroll end");
Check(!CustomBuildViewport.IsFullyVisible(1000, 250, 0, 820), "Offscreen rows are retired");
var small = Guid.NewGuid();
var medium = Guid.NewGuid();
var large = Guid.NewGuid();
var draft = new CustomBuildDraft();
Check(!draft.TryAdd(Guid.Empty, 1) && !draft.TryAdd(small, 0) && !draft.TryAdd(small, 4), "Reject invalid cards and spans");
Check(draft.TryAdd(large, 3) && draft.TryAdd(large, 3) && draft.TryAdd(medium, 2) && draft.TryAdd(medium, 2), "Accept exactly ten mixed slots");
Check(draft.UsedSlots == 10 && !draft.TryAdd(small, 1) && draft.Items.Count == 4, "Overflow cannot change the draft");
draft.RemoveAt(1);
Check(draft.UsedSlots == 7 && draft.TryAdd(small, 1), "Removal restores capacity");
bool Layout(CustomBuildDraft target, Guid[] ids, int[] slots) =>
    target.Items.Select(i => i.TemplateId).SequenceEqual(ids) && target.Items.Select(i => i.Slot).SequenceEqual(slots.Cast<int?>());
Check(Layout(draft, new[] { large, small, medium, medium }, new[] { 0, 3, 6, 8 }), "Add takes the first gap that fits");
draft.MoveTo(1, 5);
Check(Layout(draft, new[] { large, small, medium, medium }, new[] { 0, 5, 6, 8 }), "Drop on free slots keeps the gap it leaves");
draft.MoveTo(1, 3);
draft.MoveTo(0, -1); draft.MoveTo(9, 2); draft.RemoveAt(-1);
Check(Layout(draft, new[] { large, small, medium, medium }, new[] { 0, 3, 6, 8 }), "Clamped and invalid moves and removals keep the layout");
var dragDraft = new CustomBuildDraft();
dragDraft.TryAdd(small, 1); dragDraft.TryAdd(medium, 2); dragDraft.TryAdd(large, 3); dragDraft.TryAdd(small, 1);
dragDraft.MoveTo(0, 9);
Check(Layout(dragDraft, new[] { medium, large, small, small }, new[] { 1, 3, 6, 9 }), "Drag to far free slot leaves gaps and retains duplicates");
dragDraft.MoveTo(1, 0);
Check(Layout(dragDraft, new[] { large, medium, small, small }, new[] { 0, 3, 6, 9 }), "Occupied drop pushes the neighbour right");
dragDraft.MoveTo(0, 8);
Check(Layout(dragDraft, new[] { medium, small, large, small }, new[] { 3, 5, 6, 9 }), "Drop past the edge clamps and pushes left");
Check(dragDraft.UsedSlots == 7, "Moves never change capacity");
var tight = new CustomBuildDraft(new[] { new CustomBuildItem(large, 3, 0), new CustomBuildItem(large, 3, 3), new CustomBuildItem(small, 1, 8) });
Check(tight.TryAdd(large, 3) && Layout(tight, new[] { large, large, small, large }, new[] { 0, 3, 6, 7 }), "Add closes gaps when no single gap fits");
Check(CustomBuildDraft.Arrange(new[] { new CustomBuildItem(large, 3), new CustomBuildItem(small, 1, 7) })
    .Select(i => i.Slot).SequenceEqual(new int?[] { 0, 3 }), "Items without slots pack left in order");
Check(CustomBuildDraft.Arrange(new[] { new CustomBuildItem(medium, 2, 0), new CustomBuildItem(large, 3, 1) })
    .Select(i => i.Slot).SequenceEqual(new int?[] { 0, 2 }), "Overlapping saved slots are separated");
var gesture = new CustomBuildDragState();
Check(gesture.Press(7, 0, 10, 10), "First pointer owns drag");
Check(!gesture.Press(8, 0, 10, 10), "Second pointer cannot steal drag");
Check(!gesture.Update(7, .2f, 10, 10), "Short touch does not drag");
Check(gesture.Update(7, .36f, 10, 10), "Stationary hold starts drag");
Check(!gesture.Release(8) && gesture.Dragging, "Other pointer release does not finish drag");
Check(gesture.Release(7) && !gesture.Dragging, "Owner release finishes drag once");
Check(!gesture.Release(7), "Repeated release cannot mutate twice");
gesture.Press(1, 1, 0, 0);
Check(!gesture.Update(1, 1.1f, 30, 0) && !gesture.Update(1, 2, 30, 0), "Swipe before hold stays scrolling");
gesture.Cancel(); gesture.Press(2, 3, 0, 0); gesture.Update(2, 4, 0, 0); gesture.Cancel();
Check(!gesture.Release(2), "Canceled gesture cannot commit");
var directory = Path.Combine(Path.GetTempPath(), "bpp-custom-builds-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "builds.json");
    var store = new CustomBuildStore(path);
    store.Load();
    Check(store.Builds.Count == 0, "Missing file is an empty collection");
    Reject(() => store.Add(Array.Empty<CustomBuildItem>()), "Cannot save an empty build");
    Reject(() => store.Add(Enumerable.Repeat(new CustomBuildItem(large, 3), 4).ToArray()), "Store independently enforces capacity");
    store.Add(draft.Items);
    store.Add(new[] { new CustomBuildItem(small, 1) });
    var reopened = new CustomBuildStore(path);
    reopened.Load();
    Check(reopened.Builds.Count == 2, "Reopen retains multiple builds");
    Check(reopened.Builds[0].Items.Select(i => i.TemplateId).SequenceEqual(new[] { large, small, medium, medium }), "Disk retains order and repeats");
    Check(reopened.Builds[0].Items.Select(i => i.Size).SequenceEqual(new[] { 3, 1, 2, 2 }), "Disk retains slot spans");
    Check(reopened.Builds[0].Items.Select(i => i.Slot).SequenceEqual(new int?[] { 0, 3, 6, 8 }), "Disk retains slot positions");
    var first = reopened.Builds[0].Id;
    var created = reopened.Builds[0].CreatedAt;
    var edit = new[] { new CustomBuildItem(medium, 2, 8), new CustomBuildItem(small, 1, 2) };
    Check(reopened.Update(first, edit), "Existing build can be edited");
    reopened.Load();
    Check(reopened.Builds.Count == 2 && reopened.Builds[0].Id == first && reopened.Builds[0].CreatedAt == created, "Edit keeps identity, time and list position");
    Check(reopened.Builds[0].Items.Select(i => i.TemplateId).SequenceEqual(new[] { small, medium })
        && reopened.Builds[0].Items.Select(i => i.Slot).SequenceEqual(new int?[] { 2, 8 }), "Edit persists items left to right with gaps");
    Check(!reopened.Update(Guid.NewGuid(), edit) && reopened.Builds.Count == 2, "Editing a missing build changes nothing");
    Reject(() => reopened.Update(first, Array.Empty<CustomBuildItem>()), "Cannot edit a build down to empty");
    var legacyPath = Path.Combine(directory, "legacy.json");
    File.WriteAllText(legacyPath, "{\"Schema\":1,\"Builds\":[{\"Id\":\"" + Guid.NewGuid() + "\",\"CreatedAt\":\"2026-11-15T00:00:00+00:00\",\"Items\":[{\"TemplateId\":\""
        + large + "\",\"Size\":3},{\"TemplateId\":\"" + small + "\",\"Size\":1}]}]}");
    var legacy = new CustomBuildStore(legacyPath);
    legacy.Load();
    Check(legacy.Builds[0].Items.Select(i => i.Slot).SequenceEqual(new int?[] { 0, 3 }), "Files saved before slots existed load packed left");
    reopened.Delete(first);
    reopened.Load();
    Check(reopened.Builds.Count == 1 && reopened.Builds[0].Items[0].TemplateId == small, "Deletion persists and preserves other builds");
    Check(!reopened.Delete(first), "Deleting missing identity is harmless");
    using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        Check(json.RootElement.GetProperty("Builds")[0].GetProperty("Items")[0].EnumerateObject().Count() == 3, "Only template, size and slot are stored per item");
    var before = File.ReadAllText(path);
    Directory.CreateDirectory(path + ".part");
    Reject(() => reopened.Add(draft.Items), "Write failure must be reported");
    Check(reopened.Builds.Count == 1 && File.ReadAllText(path) == before, "Failed write preserves memory and disk");
    Reject(() => reopened.Delete(reopened.Builds[0].Id), "Failed deletion must be reported");
    Check(reopened.Builds.Count == 1 && File.ReadAllText(path) == before, "Failed deletion preserves memory and disk");
    Directory.Delete(path + ".part");
    foreach (var bad in new[] { "{", "{\"Schema\":2,\"Builds\":[]}", "{\"Schema\":1,\"Builds\":null}",
        "{\"Schema\":1,\"Builds\":[{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"Items\":[]}]}" })
    {
        File.WriteAllText(path, bad);
        Reject(reopened.Load, "Malformed or unsupported data must be reported");
        Check(File.ReadAllText(path) == bad && reopened.Builds.Count == 1, "Invalid load cannot overwrite saved data or current state");
        Reject(() => reopened.Add(draft.Items), "Cannot overwrite a collection after a failed read");
        Check(File.ReadAllText(path) == bad, "Rejected save leaves corrupt or future data intact");
    }
}
finally { Directory.Delete(directory, true); }
Console.WriteLine($"Custom builds: {checks} checks passed.");
