import dev.allofus.fusioncore.history.HistoryBattleLayout;

public final class HistoryBattleLayoutTest {
    public static void main(String[] args) {
        if (!(HistoryBattleLayout.artworkAspect("Small") < .6f && HistoryBattleLayout.artworkAspect("Medium") > 1f
            && HistoryBattleLayout.artworkAspect("Large") > 1.5f)) throw new AssertionError("Native card profiles were replaced by square texture proportions");
        if (HistoryBattleLayout.frameAspect("Skill") != 1f || HistoryBattleLayout.artworkAspect("Skill") != 1f)
            throw new AssertionError("Native skill frames are square");
        float[] skillInsets = HistoryBattleLayout.artInsets("Skill");
        if (skillInsets.length != 4 || skillInsets[0] <= 0 || skillInsets[0] >= .5f || skillInsets[0] != skillInsets[1]
            || skillInsets[1] != skillInsets[2] || skillInsets[2] != skillInsets[3])
            throw new AssertionError("Skill art sits in a circular window inside the frame");
        int cases = 0;
        for (int width : new int[] {160, 320, 540, 1080, 1230}) {
            for (int height : new int[] {100, 180, 300, 700}) {
                for (int otherCount : new int[] {0, 6, 128}) for (int count = 0; count <= 16; count++) {
                    float[] left = new float[count], right = new float[otherCount];
                    for (int i = 0; i < count; i++) left[i] = HistoryBattleLayout.artworkAspect(new String[] {"Small", "Medium", "Large"}[i % 3]);
                    java.util.Arrays.fill(right, HistoryBattleLayout.artworkAspect("Medium"));
                    for (int gap : new int[] {2, 8, 12}) {
                        float[][] items = {left, java.util.Arrays.copyOf(right, Math.min(16, right.length))}, skills = {right, left};
                        HistoryBattleLayout.Plan plan = HistoryBattleLayout.plan(width, height, items, skills, gap, 66);
                        if (plan.itemHeight < 1 || plan.skillEdge < 1 || plan.skillEdge > 66) throw new AssertionError("Invalid image height");
                        for (int side = 0; side < 2; side++) {
                            int used = (items[side].length == 0 ? 0 : plan.itemHeight)
                                + plan.gap + HistoryBattleLayout.rowsHeight(skills[side], width, plan.skillEdge, plan.gap);
                            if (used > height) throw new AssertionError("Combined items and skills exceed viewport");
                            if (HistoryBattleLayout.rowWidth(items[side], plan.itemHeight, plan.gap) > width)
                                throw new AssertionError("Single item row exceeds available width");
                        }
                        cases++;
                    }
                }
            }
        }
        System.out.println("PASS " + cases + " viewport/card-density layouts fit and preserve a single equal-height item row and compact skills");
    }
}
