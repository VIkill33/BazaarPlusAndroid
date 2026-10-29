using System.Runtime.InteropServices;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.GameSimEvents;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Copy native data on the game thread; only managed values enter the history writer.
internal static class AndroidPurchaseCapture
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Purchased(IntPtr self, IntPtr data, IntPtr method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Hook(IntPtr target, IntPtr replacement, [MarshalAs(UnmanagedType.I1)] bool specialReturn);
    private static Purchased? _original;
    private static readonly Purchased Replacement = Observe;

    internal static unsafe void Install()
    {
        if (_original != null) return;
        var host = Environment.GetEnvironmentVariable("BPP_HISTORY_JNI_PATH") ?? throw new InvalidOperationException("History native host missing");
        var fusion = NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(host)!, "libfusion.so"));
        var hook = Marshal.GetDelegateForFunctionPointer<Hook>(NativeLibrary.GetExport(fusion, "hook"));
        var method = IL2CPP.il2cpp_class_get_method_from_name(Il2CppClassPointerStore<BoardManager>.NativeClassPtr, "OnCardPurchased", 1);
        if (method == IntPtr.Zero) throw new MissingMethodException("BoardManager.OnCardPurchased");
        var address = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method).MethodPointer;
        var trampoline = hook(address, Marshal.GetFunctionPointerForDelegate(Replacement), false);
        if (trampoline == IntPtr.Zero) throw new InvalidOperationException("Purchase native hook failed");
        _original = Marshal.GetDelegateForFunctionPointer<Purchased>(trampoline);
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=purchase.capture.ready");
    }

    private static void Observe(IntPtr self, IntPtr data, IntPtr method)
    {
        try
        {
            if (data != IntPtr.Zero && AndroidRunObserver.Instance is { } observer
                && AndroidRunObserver.CurrentPhase() is HistoryPhase.Choice or HistoryPhase.Encounter or HistoryPhase.Loot or HistoryPhase.LevelUp)
            {
                var purchase = new GameSimEventCardPurchased(data);
                var owner = NativeNullableValue.Read<ECombatantId>(purchase, "get_CombatantId");
                var card = Data.GetCard(purchase.InstanceId);
                owner ??= card?.Owner?.CombatantId;
                if (owner == ECombatantId.Player)
                {
                    if (card == null) throw new InvalidOperationException("Purchased card unavailable");
                    var template = card.Template?.TryCast<BazaarGameShared.Domain.Cards.TCardBase>();
                    var name = template?.Localization?.Title is { } title
                        ? TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(title) : template?.InternalName;
                    observer.Purchase(new PurchaseFact(purchase.InstanceId, card.TemplateId.ToString(),
                        string.IsNullOrWhiteSpace(name) ? card.TemplateId.ToString() : name,
                        card.Type.ToString(), card.Tier.ToString(), purchase.BuyPrice));
                }
                else if (owner == null)
                    Plugin.PluginLog?.LogWarning("[BPP][Android] event=purchase.capture.unavailable reason=missing_owner");
            }
        }
        catch (Exception error) { AndroidRunObserver.Instance?.Failure(error); }
        // Recording must never consume the purchase notification or alter the native game flow.
        _original!(self, data, method);
    }
}
