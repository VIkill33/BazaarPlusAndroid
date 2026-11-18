namespace BazaarPlusPlus.Android;

internal enum CustomBuildCardState { Board, Stash, Offered, Missing }

internal static class CustomBuildOwnership
{
    // Preserve input order; one physical copy can cover only one build item.
    internal static CustomBuildCardState[] Classify(IReadOnlyList<CustomBuildItem> items,
        IReadOnlyDictionary<Guid, int> board, IReadOnlyDictionary<Guid, int> stash, IReadOnlySet<Guid> offered)
    {
        var boardLeft = new Dictionary<Guid, int>(board);
        var stashLeft = new Dictionary<Guid, int>(stash);
        return items.Select(item => Take(boardLeft, item.TemplateId) ? CustomBuildCardState.Board
            : Take(stashLeft, item.TemplateId) ? CustomBuildCardState.Stash
            : offered.Contains(item.TemplateId) ? CustomBuildCardState.Offered : CustomBuildCardState.Missing).ToArray();
    }

    private static bool Take(Dictionary<Guid, int> counts, Guid template)
    {
        if (!counts.TryGetValue(template, out var count) || count <= 0) return false;
        counts[template] = count - 1;
        return true;
    }
}
