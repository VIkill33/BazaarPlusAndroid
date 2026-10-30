using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.UI;
using NativeSpriteTask = Il2CppSystem.Threading.Tasks.Task<UnityEngine.Sprite>;

namespace BazaarPlusPlus.Android;

// Two native faces, both already used by the desktop build and history panels:
// brown center is a resting action, blue center is the selected face. The gold
// frame sits on top of either. Card tiles and the choice-popup scrim stay bare.
internal static class AndroidPanelButtonSkin
{
    private const string Root = "Assets/TheBazaar/Art/UI/Buttons/Btn_Rectangle/";
    private static readonly string[] Addresses =
    [
        Root + "Btn_Rct_Frame_S_TUI.png",
        Root + "Btn_Rct_Brown_S_Center_Active_TUI.png",
        Root + "Btn_Rct_Blue_S_Center_Active_TUI.png",
    ];

    private static readonly Color Disabled = new(.62f, .62f, .62f, .5f);

    private sealed class Binding(Button button, bool selected)
    {
        internal Button Button = button;
        internal bool Selected = selected;
        internal Image? Frame;
        internal bool Painted;
        internal bool PaintedInteractable;
    }

    private static readonly List<Binding> Bindings = new();
    private static readonly Sprite?[] Sprites = new Sprite?[3];
    private static readonly NativeSpriteTask?[] Pending = new NativeSpriteTask?[3];
    private static float _retryAt;

    internal static void Register(Button? button, bool selected = false)
    {
        if (button == null || button.targetGraphic is not Image) return;
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
        if (Bindings.Count == 0) return;
        var changed = false;
        if (Time.realtimeSinceStartup >= _retryAt
            && Services.TryGet<AssetLoader>(out var loader) && loader != null)
        {
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
        }
        if (Sprites[0] == null && Sprites[1] == null && Sprites[2] == null) return;
        foreach (var binding in Bindings)
        {
            if (changed || !binding.Painted || binding.Button.interactable != binding.PaintedInteractable)
                Apply(binding);
        }
    }

    private static void Apply(Binding binding)
    {
        if (binding.Button == null || binding.Button.targetGraphic is not Image image) return;
        var tint = binding.Button.interactable ? Color.white : Disabled;
        var painted = false;
        var backing = Sprites[binding.Selected ? 2 : 1];
        if (backing != null)
        {
            image.sprite = backing;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Slice(backing);
            image.color = tint;
            var outline = binding.Button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            painted = true;
        }
        if (Sprites[0] is { } frameSprite)
        {
            if (binding.Frame == null)
            {
                var rect = NativeUi.Rect("BPP.NativeButtonFrame", binding.Button.transform, Vector2.zero, Vector2.one);
                binding.Frame = rect.gameObject.AddComponent<Image>();
                binding.Frame.raycastTarget = false;
                binding.Frame.transform.SetAsFirstSibling();
            }
            binding.Frame.sprite = frameSprite;
            binding.Frame.type = Image.Type.Sliced;
            binding.Frame.pixelsPerUnitMultiplier = Slice(frameSprite);
            binding.Frame.color = tint;
            painted = true;
        }
        if (!painted) return;
        binding.Painted = true;
        binding.PaintedInteractable = binding.Button.interactable;
    }

    private static float Slice(Sprite sprite) =>
        sprite.rect.height / 38f * 100f / sprite.pixelsPerUnit;
}
