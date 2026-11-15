namespace BazaarPlusPlus.Android;

internal static class CustomBuildViewport
{
    // Native card materials ignore RectMask2D; only fully contained rows may own cards.
    internal static bool IsFullyVisible(float rowTop, float rowHeight, float scrollTop, float viewportHeight) =>
        rowHeight > 0 && rowTop >= scrollTop && rowTop + rowHeight <= scrollTop + viewportHeight;
}
