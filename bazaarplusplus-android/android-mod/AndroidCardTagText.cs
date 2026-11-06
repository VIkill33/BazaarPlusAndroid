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

    internal static string[] Labels(ECardSize? size, IEnumerable<ECardTag>? tags)
    {
        var names = new List<string>();
        if (size.HasValue)
            names.Add(new LocalizableText(size.Value.ToString()).GetLocalizedText());
        if (tags != null)
            names.AddRange(tags.Distinct().OrderBy(Name).Select(Name));
        return names.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
    }
}
