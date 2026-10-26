package dev.allofus.fusioncore.history;

/** Fits complete, equal-height artwork rows without cropping or a scrolling viewport. */
public final class HistoryBattleLayout {
    private HistoryBattleLayout() { }
    // Measured from the mobile CardPreviewItem._cardImage RectTransform, excluding its frame.
    // The source material texture's pixel dimensions do not describe the native artwork aspect.
    public static float artworkAspect(String size) {
        switch (size) {
            case "Small": return 469.0741f / 935.1852f;
            case "Medium": return 975.1852f / 935.1852f;
            case "Large": return 1509.074f / 944.44446f;
            default: return 0f;
        }
    }
    public static final class Plan {
        public final int gap, itemHeight, skillEdge;
        private Plan(int gap, int itemHeight, int skillEdge) { this.gap = gap; this.itemHeight = itemHeight; this.skillEdge = skillEdge; }
    }
    public static int rowsHeight(float[] aspects, int width, int size, int gap) {
        if (aspects.length == 0) return 0;
        int rows = 1, used = 0;
        for (float aspect : aspects) {
            int imageWidth = Math.max(1, (int)Math.ceil(size * aspect));
            if (imageWidth > width) return Integer.MAX_VALUE;
            if (used > 0 && used + gap + imageWidth > width) { rows++; used = 0; }
            used += (used == 0 ? 0 : gap) + imageWidth;
        }
        return rows * size + (rows - 1) * gap;
    }
    public static boolean fits(float[] aspects, int width, int height, int size, int gap) {
        return rowsHeight(aspects, width, size, gap) <= height;
    }
    public static int rowWidth(float[] aspects, int size, int gap) {
        int width = Math.max(0, aspects.length - 1) * gap;
        for (float aspect : aspects) width += Math.max(1, (int)Math.ceil(size * aspect));
        return width;
    }
    public static int itemHeight(int width, int leftHeight, int rightHeight, float[] left, float[] right, int gap) {
        if (rowWidth(left, 1, gap) > width || rowWidth(right, 1, gap) > width || leftHeight < 1 || rightHeight < 1) return 0;
        int low = 1, high = Math.max(1, Math.min(leftHeight, rightHeight));
        while (low < high) {
            int middle = (low + high + 1) / 2;
            if (rowWidth(left, middle, gap) <= width && rowWidth(right, middle, gap) <= width) low = middle;
            else high = middle - 1;
        }
        return low;
    }
    public static Plan plan(int width, int budget, float[][] items, float[][] skills, int requestedGap, int maxSkillEdge) {
        int gap = Math.min(requestedGap, Math.max(0, budget - 1));
        while (gap > 0) {
            int space = budget - gap; boolean enough = true;
            for (int side = 0; side < 2; side++)
                enough &= rowWidth(items[side], 1, gap) <= width && fits(skills[side], width, space / 3, 1, gap);
            if (enough) break;
            gap--;
        }
        int skillEdge = Math.max(1, maxSkillEdge);
        while (skillEdge > 1 && (!fits(skills[0], width, (budget - gap) / 3, skillEdge, gap)
                || !fits(skills[1], width, (budget - gap) / 3, skillEdge, gap))) skillEdge--;
        int left = budget - gap - rowsHeight(skills[0], width, skillEdge, gap);
        int right = budget - gap - rowsHeight(skills[1], width, skillEdge, gap);
        return new Plan(gap, itemHeight(width, left, right, items[0], items[1], gap), skillEdge);
    }
}
