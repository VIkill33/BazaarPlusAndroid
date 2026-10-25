using System.Collections.Concurrent;
using System.Text.Json;
namespace BazaarPlusPlus.Android
{
    internal static class Plugin { internal const string Version="test"; internal static LogStub? PluginLog=>null; }
    internal sealed class LogStub { internal void LogWarning(string value) {} internal void LogInfo(string value) {} }
    internal static class AndroidBattleCapture { internal static void WarmOne() {} }
    internal static class AndroidHistoryNativeCapture { internal static void Install() {} internal static void Tick() {} }
    internal static class AndroidHistoryBridge
    {
        internal sealed record Call(JsonElement Request, TaskCompletionSource<JsonElement> Completion);
        internal static readonly ConcurrentQueue<Call> Calls = new();
        internal static void ResumeAfterFailure() {}
        internal static Task<JsonElement> WriteAsync(object value)
        {
            var completion=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Enqueue(new(JsonSerializer.SerializeToElement(value),completion)); return completion.Task;
        }
    }
    internal static class NativeUi
    {
        internal static Action? Click;
        internal static UnityEngine.UI.Button Button(object parent,object font,string text,object min,object max,Action click)
        { Click=click; return new(); }
    }
}
namespace TheBazaar
{
    internal sealed record State(string Name) { internal State GetIl2CppType()=>this; }
    internal static class AppState { internal static State? CurrentState; internal static Initialized? _cachedRunInitialized; }
    internal sealed record Initialized(string RunId);
    internal sealed class Run { internal uint Day=1,Hour=3,Victories=0,Losses=0; internal Player? Player=new(); internal string PlayMode="Unranked"; }
    internal sealed class Player { internal string Hero="Pygmalien"; }
    internal static class Data { internal static Run? Run; }
    internal sealed class ProfileData { internal Guid AccountId; }
    internal sealed class Profile { internal bool HasData=true; internal ProfileData? Value=new(); }
    internal static class ClientCache { internal static Profile? Profile; }
}
namespace TheBazaar.Localization { internal static class NotoFontFallbackRuntime { internal static object? _loadedSansPrimary=new(); } }
namespace Il2CppSystem { public sealed class Type {} }
namespace Il2CppInterop.Runtime { internal static class Il2CppType { internal static Il2CppSystem.Type Of<T>()=>new(); } }
namespace TMPro { public sealed class TextMeshProUGUI { public string text=""; public int fontSize; public bool richText; } }
namespace UnityEngine
{
    public class MonoBehaviour { public MonoBehaviour(IntPtr pointer) { _=pointer; } protected static void DontDestroyOnLoad(object value) {} }
    public sealed class GameObject { public GameObject(string name,Il2CppSystem.Type[] types) { _=name; _=types; } public object transform=new(); public T AddComponent<T>() where T:new()=>new(); }
    public sealed class RectTransform {}
    public sealed class Canvas { public RenderMode renderMode; public int sortingOrder; }
    public enum RenderMode { ScreenSpaceOverlay }
    public sealed record Vector2(float X,float Y);
    public static class Time { public static float realtimeSinceStartup; }
    public static class Application { public static string version="test"; }
}
namespace UnityEngine.UI
{
    public sealed class CanvasScaler { public enum ScaleMode { ScaleWithScreenSize } public ScaleMode uiScaleMode; public UnityEngine.Vector2? referenceResolution; public int matchWidthOrHeight; }
    public sealed class GraphicRaycaster {}
    public sealed class Button { public T GetComponentInChildren<T>() where T:new()=>new(); }
}
