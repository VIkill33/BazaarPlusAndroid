using TheBazaar.UI;
using UnityEngine;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;
using NativeTask = Il2CppSystem.Threading.Tasks.Task;
using NativeCancellation = Il2CppSystem.Threading.CancellationTokenSource;

namespace BazaarPlusPlus.Android;

// Lives on BepInEx's persistent manager independently of the collection controller.
// Native async operations may finish after their UI owner has been destroyed.
public sealed class NativePreviewCleanup(IntPtr pointer) : MonoBehaviour(pointer)
{
    private sealed record Request(NativePreviewTask? Creation, NativeTask? Setup,
        CardPreviewBase? Card, NativeCancellation? Cancellation, GameObject? OwnedRoot);
    private static readonly List<Request> Pending = new();

    internal static void Retire(NativePreviewTask? creation, NativeTask? setup,
        CardPreviewBase? card, NativeCancellation? cancellation, GameObject? ownedRoot = null)
    {
        if (ownedRoot != null)
        {
            // GenerateUICard and SetUp may still reference the parent, even before a card
            // is returned. Keep that hidden subtree alive independently of the UI owner.
            ownedRoot.transform.SetParent(null, false);
            ownedRoot.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(ownedRoot);
        }
        cancellation?.Cancel();
        if (card != null) card.gameObject.SetActive(false);
        if (creation == null && setup == null)
        {
            if (card != null) UnityEngine.Object.Destroy(card.gameObject);
            if (ownedRoot != null) UnityEngine.Object.Destroy(ownedRoot);
            cancellation?.Dispose();
            return;
        }
        Pending.Add(new Request(creation, setup, card, cancellation, ownedRoot));
    }

    public void Update()
    {
        for (var index = Pending.Count - 1; index >= 0; index--)
        {
            var request = Pending[index];
            if (request.Creation is { IsCompleted: false } || request.Setup is { IsCompleted: false }) continue;
            try
            {
                Observe(request.Setup);
                Observe(request.Creation);
                if (request.Creation is { IsFaulted: false, IsCanceled: false })
                {
                    var result = request.Creation.Result;
                    if (result != null) UnityEngine.Object.Destroy(result.gameObject);
                }
                if (request.Card != null) UnityEngine.Object.Destroy(request.Card.gameObject);
            }
            catch (Exception error)
            {
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.collection.cleanup_failed exception={error}");
            }
            finally
            {
                if (request.OwnedRoot != null) UnityEngine.Object.Destroy(request.OwnedRoot);
                request.Cancellation?.Dispose();
                Pending.RemoveAt(index);
            }
        }
    }

    private static void Observe(NativeTask? task)
    {
        if (task is { IsFaulted: true })
            Plugin.PluginLog?.LogDebug($"[BPP][Android] event=android.collection.retired_request_failed exception={task.Exception}");
    }
}
