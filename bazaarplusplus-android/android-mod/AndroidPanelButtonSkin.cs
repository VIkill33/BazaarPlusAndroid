using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.UI;
using NativeSpriteTask = Il2CppSystem.Threading.Tasks.Task<UnityEngine.Sprite>;

namespace BazaarPlusPlus.Android;

// The same sliced game artwork used by the desktop build and history panels.
// Keep the original solid colors until Android's Addressables become available.
internal static class AndroidPanelButtonSkin
{
    private const string Root = "Assets/TheBazaar/Art/UI/Buttons/Btn_Rectangle/";
    private static readonly string[] Addresses =
    [
        Root + "Btn_Rct_Frame_S_TUI.png",
        Root + "Btn_Rct_Brown_S_Center_Active_TUI.png",
        Root + "Btn_Rct_Blue_S_Center_Active_TUI.png",
    ];

    private sealed class Binding(Button button, bool selected)
    {
        internal Button Button = button;
        internal bool Selected = selected;
        internal Image? Frame;
    }

    private static readonly List<Binding> Bindings = new();
    private static readonly Sprite?[] Sprites = new Sprite?[3];
    private static readonly NativeSpriteTask?[] Pending = new NativeSpriteTask?[3];
    private static float _retryAt;

    internal static void Register(Button button, bool selected = false)
    {
        if (button.targetGraphic is not Image) return;
        var binding = Bindings.FirstOrDefault(item => item.Button == button);
        if (binding == null) Bindings.Add(binding = new Binding(button, selected));
        binding.Selected = selected;
        Apply(binding);
    }

    internal static void SetSelected(Button? button, bool selected)
    {
        if (button == null) return;
        var binding = Bindings.FirstOrDefault(item => item.Button == button);
        if (binding == null) return;
        binding.Selected = selected;
        Apply(binding);
    }

    internal static void Tick()
    {
        Bindings.RemoveAll(item => item.Button == null);
        if (Bindings.Count == 0 || Time.realtimeSinceStartup < _retryAt) return;
        if (!Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
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
        if (changed) foreach (var binding in Bindings) Apply(binding);
    }

    private static void Apply(Binding binding)
    {
        if (binding.Button == null || binding.Button.targetGraphic is not Image image) return;
        var backing = Sprites[binding.Selected ? 2 : 1];
        if (backing != null)
        {
            image.sprite = backing;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = backing.rect.height / 38f * 100f / backing.pixelsPerUnit;
            image.color = Color.white;
            var outline = binding.Button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
        if (Sprites[0] is not { } frame) return;
        if (binding.Frame == null)
        {
            var rect = NativeUi.Rect("BPP.NativeButtonFrame", binding.Button.transform, Vector2.zero, Vector2.one);
            binding.Frame = rect.gameObject.AddComponent<Image>();
            binding.Frame.raycastTarget = false;
        }
        binding.Frame.sprite = frame;
        binding.Frame.type = Image.Type.Sliced;
        binding.Frame.pixelsPerUnitMultiplier = frame.rect.height / 38f * 100f / frame.pixelsPerUnit;
        binding.Frame.color = Color.white;
    }
}
