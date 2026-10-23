using BazaarPlusPlus.Localization;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using System.Net.Http;

namespace BazaarPlusPlus.Android;

[BepInPlugin("com.bazaarplusplus.android", "BazaarPlusPlus Android", Plugin.Version)]
public sealed class Plugin : BasePlugin
{
    public const string Version = "0.1.1";
    internal static ManualLogSource? PluginLog;
    internal static AndroidBuildRepository? BuildRepository;

    public override void Load()
    {
        PluginLog = Log;
        L.Install(() => PlayerPreferences.Data?.LanguageCode ?? "en", () => BppChineseLocaleMode.Mainland);
        AddComponent<AndroidLocalizedText>();
        AddComponent<AndroidNativeSettings>();
        AddComponent<NativePreviewCleanup>();
        AddComponent<CollectionController>();
        AddComponent<AndroidTooltipObserver>();
        AddComponent<AndroidBuildPanelController>();
        BuildRepository = new AndroidBuildRepository(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            Path.Combine(Paths.ConfigPath, "BazaarPlusPlus.Android", "builds.json"),
            info: message => Log.LogInfo(message),
            warning: message => Log.LogWarning(message));
        BuildRepository.Warm();
        Log.LogInfo("[BPP][Android] event=android.mod.loaded");
        AndroidLauncherHandshake.Complete();
    }
}
