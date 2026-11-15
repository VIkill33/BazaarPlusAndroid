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
draft.MoveTo(3, 2);
Check(draft.Items.Select(i => i.TemplateId).SequenceEqual(new[] { large, medium, small, medium }), "Move changes saved order");
draft.MoveTo(0, -1);
draft.RemoveAt(-1);
Check(draft.UsedSlots == 8 && draft.Items.Count == 4, "Invalid move and removal are no-ops");
var dragDraft = new CustomBuildDraft();
dragDraft.TryAdd(small, 1); dragDraft.TryAdd(medium, 2); dragDraft.TryAdd(large, 3); dragDraft.TryAdd(small, 1);
dragDraft.MoveTo(0, 3);
Check(dragDraft.Items.Select(i => i.TemplateId).SequenceEqual(new[] { medium, large, small, small }), "Drag inserts at end without swapping neighbors");
dragDraft.MoveTo(3, 0);
Check(dragDraft.Items.Select(i => i.TemplateId).SequenceEqual(new[] { small, medium, large, small }), "Drag inserts at beginning and retains duplicates");
dragDraft.MoveTo(-1, 0); dragDraft.MoveTo(0, 4);
Check(dragDraft.UsedSlots == 7 && dragDraft.Items[0].TemplateId == small, "Invalid drag preserves slots and order");
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
    Check(reopened.Builds[0].Items.Select(i => i.TemplateId).SequenceEqual(new[] { large, medium, small, medium }), "Disk retains order and repeats");
    Check(reopened.Builds[0].Items.Select(i => i.Size).SequenceEqual(new[] { 3, 2, 1, 2 }), "Disk retains slot spans");
    var first = reopened.Builds[0].Id;
    reopened.Delete(first);
    reopened.Load();
    Check(reopened.Builds.Count == 1 && reopened.Builds[0].Items[0].TemplateId == small, "Deletion persists and preserves other builds");
    Check(!reopened.Delete(first), "Deleting missing identity is harmless");
    using (var json = JsonDocument.Parse(File.ReadAllText(path)))
        Check(json.RootElement.GetProperty("Builds")[0].GetProperty("Items")[0].EnumerateObject().Count() == 2, "Only template and size are stored per item");
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
