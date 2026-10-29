using BazaarPlusPlus.Android;
using BazaarPlusPlus.Localization;
using TMPro;

string? language = null;
L.Install(() => language ?? "en", () => BppChineseLocaleMode.Mainland);
var copy = new LocalizedTextSet("Card Collection", "卡牌图鉴");
var label = new TextMeshProUGUI();
var binding = new AndroidLocalizedText(IntPtr.Zero);
binding.Bind(label, () => L.Resolve(copy));
Require(label.text == "Card Collection", "Initial English fallback");
language = "zh-CN";
binding.Update();
Require(label.text == "卡牌图鉴", "Late first-run language must replace fallback");
var writes = label.Writes;
binding.Update();
Require(label.Writes == writes, "Unchanged text must not dirty TMP every frame");
language = "en";
binding.Update();
Require(label.text == "Card Collection", "Language switch back to English");
foreach (var code in new[] { "en-US", "en_GB", "EN", "fr-FR", "ja-JP" })
{
    language = code;
    binding.Update();
    Require(label.text == "Card Collection", $"English fallback for {code}");
}
var previewCopy = new LocalizedTextSet("Quest rewards", "任务奖励");
binding.Bind(label, () => L.Resolve(previewCopy));
foreach (var (code, expected) in new[] { ("zh-CN", "任务奖励"), ("en-US", "Quest rewards"),
    ("zh_Hans", "任务奖励"), ("en-GB", "Quest rewards") })
{
    language = code;
    binding.Update();
    Require(label.text == expected, $"Preview label refresh for {code}");
}
var mode = "Enchants";
binding.Bind(label, () => mode);
mode = "Upgrade";
binding.Update();
Require(label.text == "Upgrade", "Dynamic preview mode must remain live");
new AndroidLocalizedText(IntPtr.Zero).Update();
L.Reset();
Console.WriteLine("Localization lifecycle tests passed.");

static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

namespace UnityEngine
{
    public class MonoBehaviour(IntPtr pointer)
    {
        public IntPtr Pointer { get; } = pointer;
    }
}
namespace TMPro
{
    public class TextMeshProUGUI
    {
        private string _text = "";
        public int Writes { get; private set; }
        public string text { get => _text; set { _text = value; Writes++; } }
    }
}

namespace Il2CppInterop.Runtime.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HideFromIl2CppAttribute : Attribute { }
}
