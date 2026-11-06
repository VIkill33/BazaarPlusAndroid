using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Two native faces, both already used by the desktop build and history panels:
// brown center is a resting action, blue center is the selected face. The gold
// frame sits on top of either. Card tiles and the choice-popup scrim stay bare.
internal static class AndroidPanelButtonSkin
{
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
        var changed = AndroidNativeButtonArt.Tick();
        if (AndroidNativeButtonArt.Frame == null && AndroidNativeButtonArt.Brown == null
            && AndroidNativeButtonArt.Blue == null) return;
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
        var backing = binding.Selected ? AndroidNativeButtonArt.Blue : AndroidNativeButtonArt.Brown;
        if (backing != null)
        {
            image.sprite = backing;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = AndroidNativeButtonArt.Slice(backing);
            image.color = tint;
            var outline = binding.Button.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            painted = true;
        }
        if (AndroidNativeButtonArt.Frame is { } frameSprite)
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
            binding.Frame.pixelsPerUnitMultiplier = AndroidNativeButtonArt.Slice(frameSprite);
            binding.Frame.color = tint;
            painted = true;
        }
        if (!painted) return;
        binding.Painted = true;
        binding.PaintedInteractable = binding.Button.interactable;
    }
}
