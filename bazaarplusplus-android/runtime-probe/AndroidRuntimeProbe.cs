using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TheBazaar;
using UnityEngine;

namespace BazaarPlusPlus.Android.RuntimeProbe;

[BepInPlugin("com.bazaarplusplus.android.runtimeprobe", "BazaarPlusPlus Android Runtime Probe", "0.1.0")]
public sealed class AndroidRuntimeProbe : BasePlugin
{
    public override void Load()
    {
        RuntimeProbeBehaviour.ProbeLog = Log;
        AddComponent<RuntimeProbeBehaviour>();
        Log.LogInfo("[BPP][Android] event=android.runtime.component_added");
    }
}

public sealed class RuntimeProbeBehaviour(IntPtr pointer) : MonoBehaviour(pointer)
{
    internal static ManualLogSource? ProbeLog;
    private bool _frameObserved;
    private bool _catalogObserved;
    private float _nextRead;
    private int _failures;

    public void Update()
    {
        if (!_frameObserved)
        {
            _frameObserved = true;
            ProbeLog?.LogInfo($"[BPP][Android] event=android.runtime.first_frame frame={Time.frameCount} thread={Environment.CurrentManagedThreadId}");
        }
        if (_catalogObserved || _failures >= 3 || Time.realtimeSinceStartup < _nextRead)
            return;
        _nextRead = Time.realtimeSinceStartup + 2;
        try
        {
            if (!Data.IsManagerCreated())
                return;
            var manager = Data.GetStatic();
            if (manager == null)
                return;
            // First validate the native collection and a single template on the Unity
            // thread. Production catalog loading needs its own measured thread policy.
            var cards = manager.GetCardMap();
            if (cards == null || cards.Count == 0)
                return;
            string? sampleId = null;
            string? sampleName = null;
            foreach (var pair in cards)
            {
                sampleId = pair.Value.Id.ToString();
                sampleName = pair.Value.InternalName;
                break;
            }
            var evidence = new
            {
                schemaVersion = 1,
                timestampUtc = DateTimeOffset.UtcNow,
                unityVersion = Application.unityVersion,
                persistentDataPath = Application.persistentDataPath,
                frameCount = Time.frameCount,
                threadId = Environment.CurrentManagedThreadId,
                width = Screen.width,
                height = Screen.height,
                cardCount = cards.Count,
                sampleId,
                sampleName,
            };
            File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "bpp-android-runtime-probe.json"),
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
            _catalogObserved = true;
            ProbeLog?.LogInfo($"[BPP][Android] event=android.runtime.catalog_read card_count={cards.Count} sample_id={sampleId}");
        }
        catch (Exception error)
        {
            _failures++;
            ProbeLog?.LogError($"[BPP][Android] event=android.runtime.catalog_failed attempt={_failures} exception={error}");
        }
    }
}
