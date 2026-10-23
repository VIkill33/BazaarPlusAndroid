namespace BazaarPlusPlus.Android;

internal readonly record struct PreviewFocusRect(float Left, float Bottom, float Right, float Top)
{
    internal float Width => Right - Left;
    internal float Height => Top - Bottom;
}

internal static class AndroidPreviewFocusLayout
{
    // Subtract the union of all bright regions. Tiles never overlap, so the
    // background has uniform opacity even when tooltips overlap the target.
    internal static List<PreviewFocusRect> DimmedRegions(PreviewFocusRect screen,
        IEnumerable<PreviewFocusRect> brightRegions)
    {
        var tiles = new List<PreviewFocusRect> { screen };
        foreach (var hole in brightRegions)
        {
            if (hole.Width <= 0 || hole.Height <= 0) continue;
            var next = new List<PreviewFocusRect>();
            foreach (var tile in tiles)
            {
                var left = Math.Max(tile.Left, hole.Left);
                var right = Math.Min(tile.Right, hole.Right);
                var bottom = Math.Max(tile.Bottom, hole.Bottom);
                var top = Math.Min(tile.Top, hole.Top);
                if (left >= right || bottom >= top) { next.Add(tile); continue; }
                Add(tile.Left, tile.Bottom, tile.Right, bottom);
                Add(tile.Left, top, tile.Right, tile.Top);
                Add(tile.Left, bottom, left, top);
                Add(right, bottom, tile.Right, top);

                void Add(float x0, float y0, float x1, float y1)
                {
                    if (x1 > x0 && y1 > y0) next.Add(new(x0, y0, x1, y1));
                }
            }
            tiles = next;
        }
        return tiles;
    }
}
