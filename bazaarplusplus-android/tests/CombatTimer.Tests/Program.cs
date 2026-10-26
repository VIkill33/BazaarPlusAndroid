using BazaarPlusPlus.Android;

var checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
var clock = new AndroidCombatClock();
clock.Advance();
Check(!clock.HasPlayback && clock.Text == "0:00.00", "Idle frames ignored");
var first = clock.Begin();
clock.Advance();
Check(clock.Active && clock.Text == "0:00.00", "First frame starts at zero");
for (var i = 0; i < 201; i++) clock.Advance();
Check(clock.Text == "0:10.05", "Logical elapsed follows presented frames");
Check(clock.Complete(first, true) && clock.HasPlayback && !clock.Active, "Completion freezes duration");
clock.Advance();
Check(clock.Text == "0:10.05", "Frames after completion ignored");
var replay = clock.Begin();
Check(clock.Text == "0:00.00", "Replay resets same battle");
Check(!clock.Complete(first, false) && clock.Active, "Old completion cannot cancel replay");
for (var i = 0; i < 1202; i++) clock.Advance();
Check(clock.Text == "1:00.05", "Minute rollover");
Check(clock.Complete(replay, false) && !clock.HasPlayback, "Cancellation hides partial timer");
var leaving = clock.Begin(); clock.Advance(); clock.Reset();
Check(!clock.Complete(leaving, true) && !clock.HasPlayback && clock.Frames == 0, "State exit rejects late result");
foreach (var json in new[] { "null", "[]", "broken", "{}", "{\"schema\":2,\"shown\":false}", "{\"schema\":\"1\",\"shown\":false}", "{\"schema\":1,\"shown\":\"false\"}" })
    Check(AndroidCombatTimerSettings.Parse(json), "Invalid settings default visible: " + json);
Check(!AndroidCombatTimerSettings.Parse("{\"schema\":1,\"shown\":false}"), "Hidden setting parsed");
var path = Path.Combine(BepInEx.Paths.ConfigPath, "BazaarPlusPlus.Android", "combat-timer.json");
try
{
    Check(AndroidCombatTimerSettings.Shown, "Default shown");
    AndroidCombatTimerSettings.Save(false);
    Check(!AndroidCombatTimerSettings.Shown && !AndroidCombatTimerSettings.Parse(File.ReadAllText(path)), "Hide saved");
    clock.Begin(); clock.Advance(); clock.Advance();
    AndroidCombatTimerSettings.Save(true);
    Check(AndroidCombatTimerSettings.Shown && clock.Text == "0:00.05", "Visibility preserves combat progress");
    Check(AndroidCombatTimerSettings.Position == new AndroidCombatTimerPosition(), "Default bottom center position");
    AndroidCombatTimerSettings.SavePosition(1, 7300);
    Check(AndroidCombatTimerSettings.Position == new AndroidCombatTimerPosition(1, 7300), "Position updated");
    Check(AndroidCombatTimerSettings.ParsePosition(File.ReadAllText(path)) == AndroidCombatTimerSettings.Position,
        "Position round trip");
    AndroidCombatTimerSettings.Save(false);
    Check(AndroidCombatTimerSettings.Position == new AndroidCombatTimerPosition(1, 7300)
        && AndroidCombatTimerSettings.ParsePosition(File.ReadAllText(path)) == AndroidCombatTimerSettings.Position,
        "Visibility toggle preserves position");
    AndroidCombatTimerSettings.SavePosition(-5, 15000);
    Check(!AndroidCombatTimerSettings.Shown && AndroidCombatTimerSettings.Position == new AndroidCombatTimerPosition(0, 10000),
        "Position clamps without changing visibility");
    AndroidCombatTimerSettings.Save(true);
    foreach (var json in new[] { "null", "[]", "broken", "{}", "{\"schema\":1,\"shown\":false}",
        "{\"schema\":2,\"edge\":1,\"along\":5000}", "{\"schema\":1,\"edge\":4,\"along\":5000}",
        "{\"schema\":1,\"edge\":1,\"along\":-1}", "{\"schema\":1,\"edge\":\"1\",\"along\":5000}" })
        Check(AndroidCombatTimerSettings.ParsePosition(json) == new AndroidCombatTimerPosition(),
            "Old or invalid position defaults to bottom center: " + json);
    Directory.CreateDirectory(path + ".part");
    try { AndroidCombatTimerSettings.Save(false); throw new Exception("Write failure ignored"); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    Check(AndroidCombatTimerSettings.Shown && AndroidCombatTimerSettings.Parse(File.ReadAllText(path)), "Failed save preserves preferences");
    try { AndroidCombatTimerSettings.SavePosition(3, 2500); throw new Exception("Position write failure ignored"); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    Check(AndroidCombatTimerSettings.Position == new AndroidCombatTimerPosition(0, 10000)
        && AndroidCombatTimerSettings.ParsePosition(File.ReadAllText(path)) == AndroidCombatTimerSettings.Position,
        "Failed position save preserves preferences");
}
finally { Directory.Delete(BepInEx.Paths.ConfigPath, true); }
Console.WriteLine($"Combat timer: {checks} checks passed.");
namespace BepInEx { internal static class Paths { internal static string ConfigPath { get; } = Path.Combine(Path.GetTempPath(), "bpp-timer-test-" + Guid.NewGuid()); } }
namespace BazaarPlusPlus.Android {
    internal static class Plugin { internal static Logger? PluginLog => null; }
    internal sealed class Logger { internal void LogWarning(string message) { } }
}
