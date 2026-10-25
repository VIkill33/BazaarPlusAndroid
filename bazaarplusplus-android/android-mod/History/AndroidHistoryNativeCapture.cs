using System.Diagnostics;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TheBazaar;
using BazaarGameShared.Infra.Messages;

namespace BazaarPlusPlus.Android;

// The same native ABI was verified in M0. Harmony's Android temporary-path failure is not bypassed.
internal static class AndroidHistoryNativeCapture
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Simulate(IntPtr self, IntPtr message, IntPtr cancellation, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Hook(IntPtr target, IntPtr replacement, [MarshalAs(UnmanagedType.I1)] bool specialReturn);
    private static Simulate? _original;
    private static readonly Simulate Replacement = Observe;
    private sealed record Pending(Il2CppSystem.Threading.Tasks.Task Task, AndroidRunObserver.Ticket Ticket, string Winner, string Loser);
    private static readonly List<Pending> Tasks = new();

    internal static unsafe void Install()
    {
        if (_original != null) return;
        var host = Environment.GetEnvironmentVariable("BPP_HISTORY_JNI_PATH") ?? throw new InvalidOperationException("History native host missing");
        var fusion = NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(host)!, "libfusion.so"));
        var hook = Marshal.GetDelegateForFunctionPointer<Hook>(NativeLibrary.GetExport(fusion, "hook"));
        var method = IL2CPP.il2cpp_class_get_method_from_name(Il2CppClassPointerStore<CombatSimHandler>.NativeClassPtr, "Simulate", 2);
        if (method == IntPtr.Zero) throw new InvalidOperationException("Combat Simulate method unavailable");
        var address = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method).MethodPointer;
        var trampoline = hook(address, Marshal.GetFunctionPointerForDelegate(Replacement), false);
        if (trampoline == IntPtr.Zero) throw new InvalidOperationException("History native hook failed");
        _original = Marshal.GetDelegateForFunctionPointer<Simulate>(trampoline);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=history.capture.ready");
    }

    private static IntPtr Observe(IntPtr self, IntPtr message, IntPtr cancellation, IntPtr method)
    {
        AndroidRunObserver.Ticket? ticket = null;
        var winner = "unknown"; var loser = "unknown";
        var observer = AndroidRunObserver.Instance;
        var timer = Stopwatch.StartNew();
        try
        {
            var phase = AndroidRunObserver.CurrentPhase();
            if (observer != null && phase == HistoryPhase.PvpCombat)
            {
                observer.Observe();
                var data = new NetMessageCombatSim(message);
                winner = data.Data.Winner.ToString(); loser = data.Data.Loser.ToString();
                var capture = AndroidBattleCapture.Capture(timer);
                ticket = observer.Begin(data.MessageId, phase, capture.Opening, capture.Snapshots, Data.Run?.Day ?? 0, Data.Run?.Hour ?? 0, capture.Complete);
                timer.Stop();
                var elapsedMs = timer.Elapsed.TotalMilliseconds;
                Plugin.PluginLog?.LogInfo($"[BPP][Android] event=history.capture.finished elapsedMs={elapsedMs:F1} targetExceeded={elapsedMs > HistoryLimits.CaptureTargetMs}");
            }
        }
        catch (Exception error) { observer?.Failure(error); }
        // History failure must never skip the game's original simulation.
        var result = _original!(self, message, cancellation, method);
        try
        {
            if (ticket != null && result != IntPtr.Zero)
                Tasks.Add(new(new Il2CppSystem.Threading.Tasks.Task(result), ticket, winner, loser));
        }
        catch (Exception error) { observer?.Failure(error); }
        return result;
    }

    internal static void Tick()
    {
        for (var index = Tasks.Count - 1; index >= 0; index--)
        {
            var item = Tasks[index];
            if (!item.Task.IsCompleted) continue;
            Tasks.RemoveAt(index);
            AndroidRunObserver.Instance?.Complete(item.Ticket, item.Winner, item.Loser, item.Task.Status.ToString() == "RanToCompletion");
        }
    }
}
