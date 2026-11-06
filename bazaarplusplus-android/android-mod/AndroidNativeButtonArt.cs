using TheBazaar.AppFramework;
using UnityEngine;
using NativeSpriteTask = Il2CppSystem.Threading.Tasks.Task<UnityEngine.Sprite>;

namespace BazaarPlusPlus.Android;

// The game's rectangle button sprites, pinned in Global scope. Each consumer
// polls Tick on Unity's thread; loads continue whichever consumer is visible.
internal static class AndroidNativeButtonArt
{
    private const string Root = "Assets/TheBazaar/Art/UI/Buttons/Btn_Rectangle/";
    private static readonly string[] Addresses =
    [
        Root + "Btn_Rct_Frame_S_TUI.png",
        Root + "Btn_Rct_Brown_S_Center_Active_TUI.png",
        Root + "Btn_Rct_Blue_S_Center_Active_TUI.png",
    ];
    private static readonly Sprite?[] Sprites = new Sprite?[3];
    private static readonly NativeSpriteTask?[] Pending = new NativeSpriteTask?[3];
    private static float _retryAt;

    internal static Sprite? Frame => Sprites[0];
    internal static Sprite? Brown => Sprites[1];
    internal static Sprite? Blue => Sprites[2];

    // Returns true when a sprite finished loading during this call.
    internal static bool Tick()
    {
        if (Time.realtimeSinceStartup < _retryAt
            || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return false;
        var changed = false;
        for (var index = 0; index < Addresses.Length; index++)
        {
            if (Sprites[index] != null) continue;
            try
            {
                var task = Pending[index];
                if (task == null)
                {
                    Pending[index] = loader.LoadAssetAsyncByAddress<Sprite>(Addresses[index],
                        NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                    continue;
                }
                if (!task.IsCompleted) continue;
                Pending[index] = null;
                if (!task.IsFaulted && !task.IsCanceled && task.Result != null)
                {
                    Sprites[index] = task.Result;
                    changed = true;
                }
                else _retryAt = Time.realtimeSinceStartup + 10;
            }
            catch (Exception error)
            {
                Pending[index] = null;
                _retryAt = Time.realtimeSinceStartup + 10;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.panel_button_art.failed index={index} type={error.GetType().Name}");
            }
        }
        return changed;
    }

    // Keep the bevel thin at every size; stretch only the nine-slice center.
    internal static float Slice(Sprite sprite) =>
        sprite.rect.height / 38f * 100f / sprite.pixelsPerUnit;
}
