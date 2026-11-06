using BazaarGameShared.Domain.Core.Types;
using TheBazaar.Localization;
using TheBazaar.Utilities;

namespace BazaarPlusPlus.Android;

internal static class AndroidCardTagText
{
    internal static string Name(ECardTag tag)
    {
        var key = tag.ToString();
        try
        {
            var localized = new LocalizableText(key).GetLocalizedText();
            return string.IsNullOrWhiteSpace(localized) ? key : localized;
        }
        catch (Exception) { return key; }
    }

    internal static string Append(string description, IEnumerable<ECardTag>? tags)
    {
        if (tags == null) return description;
        var names = tags.Distinct().OrderBy(tag => Name(tag)).Select(Name).ToArray();
        if (names.Length == 0) return description;
        var line = NativeUi.Text("Tags: ", "词条：") + string.Join(" · ", names);
        return string.IsNullOrWhiteSpace(description) ? line : line + "\n" + description;
    }
}
