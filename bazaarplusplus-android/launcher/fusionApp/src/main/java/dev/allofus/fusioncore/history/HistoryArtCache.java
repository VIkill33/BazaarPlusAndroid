package dev.allofus.fusioncore.history;

import dev.allofus.fusioncore.LauncherFiles;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Locale;
import java.util.Set;

/** Deletes regenerable history artwork. Never opens the history database or the system gallery. */
public final class HistoryArtCache {
    public static final long CLEAN_INTERVAL_MS = 24L * 60L * 60L * 1000L;
    public static final long GROWTH_INTERVAL_MS = 60L * 60L * 1000L;
    public static final long SIZE_BUDGET_BYTES = 96L * 1024L * 1024L;
    public static final long GROWTH_BYTES = 8L * 1024L * 1024L;
    static final long FRESH_MS = 10L * 60L * 1000L;
    private static final Object GATE = new Object();

    private HistoryArtCache() { }

    /** First launch, a day since the last sweep, or growth past the budget after an hour. */
    public static boolean due(long now, long lastRun, long bytes, long bytesAtLastRun) {
        if (lastRun <= 0 || now < lastRun) return true;
        if (now - lastRun >= CLEAN_INTERVAL_MS) return true;
        return bytes > SIZE_BUDGET_BYTES && bytes > bytesAtLastRun + GROWTH_BYTES && now - lastRun >= GROWTH_INTERVAL_MS;
    }

    public static String format(long bytes) {
        if (bytes < 1024) return bytes + " B";
        double value;
        String unit;
        if (bytes < 1024L * 1024L) { value = bytes / 1024.0; unit = "KB"; }
        else if (bytes < 1024L * 1024L * 1024L) { value = bytes / (1024.0 * 1024.0); unit = "MB"; }
        else { value = bytes / (1024.0 * 1024.0 * 1024.0); unit = "GB"; }
        String pattern = value >= 10 || value == Math.rint(value) ? "%.0f %s" : "%.1f %s";
        return String.format(Locale.US, pattern, value, unit);
    }

    public static long bytes(File gameRoot) throws IOException {
        File art = artRoot(gameRoot);
        if (art == null) return 0;
        return measure(art);
    }

    /**
     * Removes artwork the current history and purchase screens do not need.
     * A null retention set keeps every card image and still removes obsolete trees.
     * Returns the number of bytes deleted.
     */
    public static long clean(File gameRoot, Set<String> retainedCards) throws IOException {
        synchronized (GATE) {
            File art = artRoot(gameRoot);
            if (art == null) return 0;
            long now = System.currentTimeMillis();
            long freed = 0;
            File[] roots = list(art);
            for (File child : roots) {
                if (child.getName().equals(".cache") || child.getName().equals(".nomedia")) continue;
                freed += deleteTreeIfStable(child, now);
            }
            File versions = new File(new File(art, ".cache"), "v3");
            if (Files.isSymbolicLink(versions.toPath()) || !versions.isDirectory()) return freed;
            String current = pointer(versions);
            File currentDir = current == null ? null : new File(versions, current);
            boolean pointerOk = currentDir != null && currentDir.isDirectory() && !Files.isSymbolicLink(currentDir.toPath());
            for (File child : list(versions)) {
                if (Files.isSymbolicLink(child.toPath())) continue;
                String name = child.getName();
                if (name.equals("current.txt")) continue;
                if (name.equals("current.part")) {
                    if (!fresh(child, now)) freed += deleteFile(child);
                    continue;
                }
                if (pointerOk && name.equals(current)) freed += cleanCurrent(child, retainedCards, now);
                else if (pointerOk && child.isDirectory() && name.matches("[a-f0-9]{64}")) freed += deleteTreeIfStable(child, now);
            }
            return freed;
        }
    }

    private static long cleanCurrent(File directory, Set<String> retainedCards, long now) throws IOException {
        long freed = 0;
        for (File file : list(directory)) {
            if (Files.isSymbolicLink(file.toPath()) || !file.isFile()) continue;
            if (keep(file.getName(), retainedCards) || fresh(file, now)) continue;
            freed += deleteFile(file);
        }
        return freed;
    }

    static boolean keep(String name, Set<String> retainedCards) {
        if (name.equals(".nomedia") || name.equals("source.json")) return true;
        if (name.endsWith(".bppart.part")) return false;
        if (!name.endsWith(".bppart")) return true;
        String key = name.substring(0, name.length() - ".bppart".length());
        if (key.startsWith("hero-") || key.startsWith("frame-") || key.startsWith("state-") || key.startsWith("Trophy_"))
            return true;
        if (!key.startsWith("card-")) return false;
        if (retainedCards == null) return true;
        return retainedCards.contains(key.substring("card-".length()));
    }

    private static File artRoot(File gameRoot) {
        if (gameRoot == null) return null;
        File art = new File(gameRoot, "BepInEx/history-art");
        if (Files.isSymbolicLink(art.toPath()) || !art.isDirectory()) return null;
        return art;
    }

    private static String pointer(File versions) throws IOException {
        File current = new File(versions, "current.txt");
        if (Files.isSymbolicLink(current.toPath()) || !current.isFile() || current.length() > 128) return null;
        String version = new String(Files.readAllBytes(current.toPath()), StandardCharsets.UTF_8).trim();
        return version.matches("[a-f0-9]{64}") ? version : null;
    }

    private static boolean fresh(File file, long now) { return file.lastModified() > 0 && now - file.lastModified() < FRESH_MS; }

    private static boolean stable(File file, long now) throws IOException {
        if (Files.isSymbolicLink(file.toPath())) return false;
        if (file.isFile()) return !fresh(file, now);
        if (!file.isDirectory()) return false;
        for (File child : list(file)) if (!stable(child, now)) return false;
        return true;
    }

    private static long deleteTreeIfStable(File file, long now) throws IOException {
        if (!stable(file, now)) return 0;
        long size = measure(file);
        LauncherFiles.removeOwned(file);
        return size;
    }

    private static long deleteFile(File file) throws IOException {
        long size = file.length();
        if (!file.delete()) throw new IOException("无法清理图片缓存");
        return size;
    }

    private static long measure(File file) throws IOException {
        if (Files.isSymbolicLink(file.toPath())) return 0;
        if (file.isFile()) return file.length();
        if (!file.isDirectory()) return 0;
        long total = 0;
        for (File child : list(file)) total += measure(child);
        return total;
    }

    private static File[] list(File directory) throws IOException {
        File[] children = directory.listFiles();
        if (children == null) throw new IOException("无法读取图片缓存");
        return children;
    }
}
