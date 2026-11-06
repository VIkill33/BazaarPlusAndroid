using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Instance void SetSpeed(float speed, bool log, MethodInfo*) on the Android-generated native surface.
// Desktop prefixes the same method (CombatSpeedPatch); Android swaps the argument before the original runs.
internal static class AndroidCombatSpeedHook
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetSpeed(IntPtr self, float speed, [MarshalAs(UnmanagedType.I1)] bool log, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Hook(IntPtr target, IntPtr replacement, [MarshalAs(UnmanagedType.I1)] bool specialReturn);
    private static SetSpeed? _original;
    private static readonly SetSpeed Replacement = Observe;
    private static IntPtr _method;
    private static IntPtr _handler;
    private static long _generation = -1;
    private static long _applied = -1;
    private static float _requested = 1;

    internal static bool Ready => _original != null;

    internal static unsafe void Install()
    {
        if (_original != null) return;
        var host = Environment.GetEnvironmentVariable("BPP_HISTORY_JNI_PATH") ?? throw new InvalidOperationException("Native host missing");
        var fusion = NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(host)!, "libfusion.so"));
        var hook = Marshal.GetDelegateForFunctionPointer<Hook>(NativeLibrary.GetExport(fusion, "hook"));
        var method = IL2CPP.il2cpp_class_get_method_from_name(Il2CppClassPointerStore<CombatSimHandler>.NativeClassPtr, "SetSpeed", 2);
        if (method == IntPtr.Zero) throw new InvalidOperationException("Combat SetSpeed unavailable");
        var address = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method).MethodPointer;
        var trampoline = hook(address, Marshal.GetFunctionPointerForDelegate(Replacement), false);
        if (trampoline == IntPtr.Zero) throw new InvalidOperationException("Combat speed hook failed");
        _method = method;
        _original = Marshal.GetDelegateForFunctionPointer<SetSpeed>(trampoline);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.combat_speed.ready");
    }

    // The simulating handler is alive for its own generation only; never reuse it afterwards.
    internal static void Bind(IntPtr handler, long generation)
    {
        _handler = handler;
        _generation = generation;
    }

    private static void Observe(IntPtr self, float speed, bool log, IntPtr method)
    {
        _requested = speed;
        if (AndroidCombatTimer.Clock.Active && AndroidCombatControls.Overrides(speed))
            speed = AndroidCombatTimerSettings.Speed;
        _original!(self, speed, log, method);
    }

    // Main thread only. Applies the chosen speed once per combat, before or after the game's first request.
    internal static void ApplyOnce()
    {
        if (_applied == _generation) return;
        _applied = _generation;
        if (AndroidCombatTimerSettings.Speed != 1) Apply();
    }

    internal static void Apply()
    {
        var clock = AndroidCombatTimer.Clock;
        if (_original == null || _handler == IntPtr.Zero || _generation != clock.Generation || !clock.Active
            || !AndroidCombatControls.Overrides(_requested)) return;
        _original(_handler, AndroidCombatTimerSettings.Speed, false, _method);
    }
}
