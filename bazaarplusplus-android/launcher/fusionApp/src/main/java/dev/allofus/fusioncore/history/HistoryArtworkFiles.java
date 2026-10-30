package dev.allofus.fusioncore.history;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

/** On-disk contract shared with the game's HistoryArtworkDirectory exporter. */
final class HistoryArtworkFiles {
    private final File root;

    HistoryArtworkFiles(File gameRoot) {
        root = new File(gameRoot, "BepInEx/history-art/.cache/v3");
    }

    File resolve(String key) throws IOException {
        if (key == null || !key.matches("[A-Za-z0-9_-]{1,100}")) return null;
        File current = new File(root, "current.txt");
        if (!current.isFile() || current.length() > 128) return null;
        String version = new String(Files.readAllBytes(current.toPath()), StandardCharsets.UTF_8).trim();
        if (!version.matches("[a-f0-9]{64}")) return null;
        File file = new File(new File(root, version), key + ".bppart");
        return file.isFile() && file.length() <= 2 * 1024 * 1024 ? file : null;
    }
}
