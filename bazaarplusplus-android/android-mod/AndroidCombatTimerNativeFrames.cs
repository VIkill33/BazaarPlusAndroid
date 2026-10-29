using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Static void Process(int framesLeft, MethodInfo*) on the Android-generated native surface.
internal static class AndroidCombatTimerNativeFrames
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Process(int framesLeft, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Hook(IntPtr target, IntPtr replacement, [MarshalAs(UnmanagedType.I1)] bool specialReturn);
    private static Process? _original;
    private static readonly Process Replacement = Observe;
    internal static unsafe void Install()
    {
        if (_original != null) return;
        var host = Environment.GetEnvironmentVariable("BPP_HISTORY_JNI_PATH") ?? throw new InvalidOperationException("Native host missing");
        var fusion = NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(host)!, "libfusion.so"));
        var hook = Marshal.GetDelegateForFunctionPointer<Hook>(NativeLibrary.GetExport(fusion, "hook"));
        var method = IL2CPP.il2cpp_class_get_method_from_name(IL2CPP.GetIl2CppClass("TheBazaarRuntime.dll", "TheBazaar", "FinalBlowSlowDownController"), "Process", 1);
        if (method == IntPtr.Zero) throw new InvalidOperationException("Combat frame Process unavailable");
        var address = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method).MethodPointer;
        var trampoline = hook(address, Marshal.GetFunctionPointerForDelegate(Replacement), false);
        if (trampoline == IntPtr.Zero) throw new InvalidOperationException("Combat frame hook failed");
        _original = Marshal.GetDelegateForFunctionPointer<Process>(trampoline);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.combat_timer.ready");
    }
    private static void Observe(int framesLeft, IntPtr method)
    {
        _original!(framesLeft, method);
        AndroidCombatTimer.Clock.Advance();
    }
}
