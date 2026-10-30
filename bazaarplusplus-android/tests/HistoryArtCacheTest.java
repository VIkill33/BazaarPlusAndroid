package dev.allofus.fusioncore.history;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Collections;
import java.util.HashSet;

public final class HistoryArtCacheTest {
    private static int checks;
    private static final String CURRENT = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static final String OLDER = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
        checks++;
    }

    public static void main(String[] args) throws Exception {
        File root = new File(args[0]);
        long now = System.currentTimeMillis();
        check(HistoryArtCache.due(now, 0, 1, 0), "First sweep runs");
        check(!HistoryArtCache.due(now, now - 1000, 1, 1), "Recent small cache waits");
        check(HistoryArtCache.due(now, now - HistoryArtCache.CLEAN_INTERVAL_MS, 1, 1), "Daily sweep runs");
        check(HistoryArtCache.due(now, now + 1000, 1, 1), "Clock going backwards runs a sweep");
        check(!HistoryArtCache.due(now, now - HistoryArtCache.GROWTH_INTERVAL_MS, HistoryArtCache.SIZE_BUDGET_BYTES,
            HistoryArtCache.SIZE_BUDGET_BYTES), "A retained cache at the budget does not sweep early");
        check(HistoryArtCache.due(now, now - HistoryArtCache.GROWTH_INTERVAL_MS,
            HistoryArtCache.SIZE_BUDGET_BYTES + HistoryArtCache.GROWTH_BYTES + 1,
            HistoryArtCache.SIZE_BUDGET_BYTES), "Growth past the budget sweeps after an hour");
        check(!HistoryArtCache.due(now, now - 1000, HistoryArtCache.SIZE_BUDGET_BYTES + HistoryArtCache.GROWTH_BYTES + 1,
            HistoryArtCache.SIZE_BUDGET_BYTES), "Growth inside the hour waits");
        check(HistoryArtCache.format(512).equals("512 B") && HistoryArtCache.format(1536).equals("1.5 KB")
            && HistoryArtCache.format(HistoryArtCache.SIZE_BUDGET_BYTES).equals("96 MB"), "Sizes use binary units");
        check(HistoryArtCache.keep("hero-Vanessa.bppart", Collections.emptySet())
            && HistoryArtCache.keep("frame-Small-Gold.bppart", Collections.emptySet())
            && HistoryArtCache.keep("state-broken.bppart", Collections.emptySet())
            && HistoryArtCache.keep("Trophy_Gold_TUI.bppart", Collections.emptySet())
            && HistoryArtCache.keep(".nomedia", Collections.emptySet())
            && HistoryArtCache.keep("source.json", Collections.emptySet()), "History chrome stays");
        check(HistoryArtCache.keep("card-kept.bppart", Collections.singleton("kept"))
            && !HistoryArtCache.keep("card-dropped.bppart", Collections.singleton("kept"))
            && HistoryArtCache.keep("card-unknown.bppart", null)
            && !HistoryArtCache.keep("card-x.bppart.part", null), "Card retention follows the supplied set");

        File game = new File(root, "game");
        File current = version(game, CURRENT);
        File older = version(game, OLDER);
        write(new File(current, "card-kept.bppart"), "kept");
        write(new File(current, "card-dropped.bppart"), "drop");
        File freshCard = new File(current, "card-fresh.bppart");
        write(freshCard, "fresh");
        check(freshCard.setLastModified(now), "Mark a card written moments ago");
        write(new File(current, "hero-Vanessa.bppart"), "hero");
        write(new File(current, "frame-Small-Gold.bppart"), "frame");
        write(new File(current, "source.json"), "{}");
        write(new File(current, ".nomedia"), "");
        write(new File(current, "card-stale.bppart.part"), "part");
        File freshPart = new File(current, "card-fresh.bppart.part");
        write(freshPart, "writing");
        check(freshPart.setLastModified(now), "Mark an in-progress image");
        File legacy = new File(game, "BepInEx/history-art/v2/old.png");
        write(legacy, "legacy");
        write(new File(game, "BepInEx/history-art/.nomedia"), "");
        write(new File(older, "card-kept.bppart"), "old-copy");
        File outside = new File(root, "outside.txt");
        write(outside, "safe");
        Files.createSymbolicLink(new File(current, "linked.bppart").toPath(), outside.toPath());
        long before = HistoryArtCache.bytes(game);
        long freed = HistoryArtCache.clean(game, Collections.singleton("kept"));
        check(freed > 0 && freed < before, "Cleanup frees obsolete artwork and reports only deleted bytes");
        check(new File(current, "card-kept.bppart").isFile(), "History and first-page card stays");
        check(new File(current, "card-fresh.bppart").isFile(), "A card written moments ago stays");
        check(!new File(current, "card-dropped.bppart").exists(), "Unreferenced card image is deleted");
        check(new File(current, "hero-Vanessa.bppart").isFile() && new File(current, "frame-Small-Gold.bppart").isFile(),
            "Hero and frame images stay");
        check(new File(current, "source.json").isFile() && new File(current, ".nomedia").isFile(), "Cache markers stay");
        check(!new File(current, "card-stale.bppart.part").exists() && freshPart.isFile(), "Only a stale partial image is deleted");
        check(!older.exists() && !legacy.exists(), "Old version directories and legacy trees are deleted");
        check(new File(game, "BepInEx/history-art/.nomedia").isFile(), "Root exclusion marker stays");
        check(new File(current, "linked.bppart").isFile() && outside.isFile(), "Symbolic links and their targets stay");
        check(HistoryArtCache.bytes(game) == before - freed, "Remaining size matches the freed byte count");

        File second = new File(root, "unknown");
        File unknownCurrent = version(second, CURRENT);
        write(new File(unknownCurrent, "card-anything.bppart"), "card");
        write(new File(second, "BepInEx/history-art/v1/old.png"), "old");
        check(HistoryArtCache.clean(second, null) > 0 && new File(unknownCurrent, "card-anything.bppart").isFile()
            && !new File(second, "BepInEx/history-art/v1").exists(), "Unknown retention still removes obsolete trees");

        File blocked = new File(root, "blocked");
        File blockedCurrent = version(blocked, CURRENT);
        File blockedOld = version(blocked, OLDER);
        write(new File(blockedCurrent, "card-kept.bppart"), "kept");
        write(new File(blockedOld, "card-kept.bppart"), "old");
        write(new File(blocked, "BepInEx/history-art/.cache/v3/current.txt"), "not-a-version".getBytes(StandardCharsets.UTF_8));
        check(HistoryArtCache.clean(blocked, new HashSet<String>()) == 0 && blockedOld.isDirectory(),
            "An unreadable version pointer does not choose a version directory to delete");
        System.out.println("History art cache: " + checks + " checks passed");
    }

    private static File version(File game, String name) throws Exception {
        File directory = new File(game, "BepInEx/history-art/.cache/v3/" + name);
        if (!directory.mkdirs() && !directory.isDirectory()) throw new IllegalStateException(directory.getPath());
        if (name.equals(CURRENT))
            write(new File(directory.getParentFile(), "current.txt"), name);
        return directory;
    }

    private static void write(File file, String text) throws Exception { write(file, text.getBytes(StandardCharsets.UTF_8)); }

    private static void write(File file, byte[] bytes) throws Exception {
        File parent = file.getParentFile();
        if (!parent.isDirectory() && !parent.mkdirs()) throw new IllegalStateException(parent.getPath());
        Files.write(file.toPath(), bytes);
        if (!file.setLastModified(System.currentTimeMillis() - HistoryArtCache.FRESH_MS - 1000))
            throw new IllegalStateException(file.getPath());
    }
}
