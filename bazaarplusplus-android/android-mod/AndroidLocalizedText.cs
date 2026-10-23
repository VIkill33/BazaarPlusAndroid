using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;

namespace BazaarPlusPlus.Android;

// Resolve at display time: first-run preferences can arrive after the font/UI.
public sealed class AndroidLocalizedText(IntPtr pointer) : MonoBehaviour(pointer)
{
    private TextMeshProUGUI? _label;
    private Func<string>? _resolve;

    [HideFromIl2Cpp]
    internal void Bind(TextMeshProUGUI label, Func<string> resolve)
    {
        _label = label;
        _resolve = resolve;
        Update();
    }

    public void Update()
    {
        if (_label == null || _resolve == null) return;
        var text = _resolve();
        if (_label.text != text) _label.text = text;
    }
}
