using BazaarGameShared.Domain.Core.Types;

namespace BazaarPlusPlus.GameInterop.Cards;

// Adapt only the collection representation; the shared owner still decides identity.
internal static partial class PackageIdentity
{
    public static bool IsPackage(Il2CppSystem.Collections.Generic.HashSet<EHiddenTag>? source)
    {
        var tags = new List<EHiddenTag>();
        if (source != null)
            foreach (var tag in source) tags.Add(tag);
        return IsPackage(tags);
    }
}
