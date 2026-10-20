using BazaarPlusPlus.Localization;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace BazaarPlusPlus.Android;

[BepInPlugin("com.bazaarplusplus.android", "BazaarPlusPlus Android", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource? PluginLog;

    public override void Load()
    {
        PluginLog = Log;
        L.Install(() => PlayerPreferences.Data?.LanguageCode ?? "en", () => BppChineseLocaleMode.Mainland);
        AddComponent<NativePreviewCleanup>();
        AddComponent<CollectionController>();
        Log.LogInfo("[BPP][Android] event=android.mod.loaded");
    }
}
