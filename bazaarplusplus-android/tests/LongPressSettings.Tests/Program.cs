using BazaarPlusPlus.Android;
using UnityEngine;

var stored = args.Length == 0 ? (int?)null : int.Parse(args[0]);
PlayerPrefs.StoredValue = stored;
var initial = stored is >= 0 and <= 2
    ? (AndroidLongPressPreviewMode)stored.Value : AndroidLongPressPreviewMode.Enchant;
Require(AndroidLongPressPreviewSettings.Mode == initial, "Saved preference/default was not preserved.");
for (var i = 0; i < 3; i++)
{
    var previous = AndroidLongPressPreviewSettings.Mode;
    AndroidLongPressPreviewSettings.Cycle();
    var expected = previous switch
    {
        AndroidLongPressPreviewMode.Enchant => AndroidLongPressPreviewMode.Upgrade,
        AndroidLongPressPreviewMode.Upgrade => AndroidLongPressPreviewMode.None,
        _ => AndroidLongPressPreviewMode.Enchant,
    };
    Require(AndroidLongPressPreviewSettings.Mode == expected, "Incorrect three-mode cycle.");
    Require(PlayerPrefs.StoredValue == (int)expected, "Mode was not persisted.");
}
Require(AndroidLongPressPreviewSettings.Mode == initial, "Cycle did not return to initial mode.");
Require(PlayerPrefs.SaveCount == 3, "Each change must save the preference.");
foreach (var mode in Enum.GetValues<AndroidLongPressPreviewMode>()) {
    AndroidLongPressPreviewSettings.Mode = mode;
    Require(PlayerPrefs.StoredValue == (int)mode, "Direct selection was not persisted.");
}
var saved = PlayerPrefs.StoredValue;
try { AndroidLongPressPreviewSettings.Mode = (AndroidLongPressPreviewMode)99; throw new Exception("Invalid mode accepted."); }
catch (ArgumentOutOfRangeException) { }
Require(PlayerPrefs.StoredValue == saved, "Invalid selection changed saved mode.");
Console.WriteLine($"Long press settings passed for stored value {stored?.ToString() ?? "missing"}.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

namespace UnityEngine
{
    internal static class PlayerPrefs
    {
        internal static int? StoredValue;
        internal static int SaveCount;
        public static int GetInt(string key, int fallback) { CheckKey(key); return StoredValue ?? fallback; }
        public static void SetInt(string key, int value) { CheckKey(key); StoredValue = value; }
        public static void Save() => SaveCount++;
        private static void CheckKey(string key)
        {
            if (key != "BPP.Android.LongPressUpgrade")
                throw new InvalidOperationException("Existing preference key changed.");
        }
    }
}
