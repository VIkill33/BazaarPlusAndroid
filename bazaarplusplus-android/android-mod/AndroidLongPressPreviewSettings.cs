using UnityEngine;

namespace BazaarPlusPlus.Android;

internal enum AndroidLongPressPreviewMode { Enchant = 0, Upgrade = 1, None = 2 }

internal static class AndroidLongPressPreviewSettings
{
    // Keep the existing 0/1 preference values so saved enchant/upgrade choices survive.
    private const string Preference = "BPP.Android.LongPressUpgrade";
    private static AndroidLongPressPreviewMode? _mode;

    internal static AndroidLongPressPreviewMode Mode
    {
        get
        {
            if (_mode.HasValue) return _mode.Value;
            var stored = PlayerPrefs.GetInt(Preference, 0);
            _mode = Enum.IsDefined(typeof(AndroidLongPressPreviewMode), stored)
                ? (AndroidLongPressPreviewMode)stored : AndroidLongPressPreviewMode.Enchant;
            return _mode.Value;
        }
        private set
        {
            _mode = value;
            PlayerPrefs.SetInt(Preference, (int)value);
            PlayerPrefs.Save();
        }
    }

    internal static void Cycle() => Mode = Mode switch
    {
        AndroidLongPressPreviewMode.Enchant => AndroidLongPressPreviewMode.Upgrade,
        AndroidLongPressPreviewMode.Upgrade => AndroidLongPressPreviewMode.None,
        _ => AndroidLongPressPreviewMode.Enchant,
    };
}
