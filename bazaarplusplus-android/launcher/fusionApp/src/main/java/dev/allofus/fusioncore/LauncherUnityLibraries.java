package dev.allofus.fusioncore;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/** BepInEx resolves the source filename under unity-libs, even when the source is a URL. */
public final class LauncherUnityLibraries {
    private LauncherUnityLibraries() {}

    public static String configure(String config, String archiveName) {
        if (archiveName == null || !archiveName.matches("[A-Za-z0-9][A-Za-z0-9._-]*\\.zip"))
            throw new IllegalArgumentException("Unity 基础库必须使用内置 ZIP 文件名");
        String setting = "UnityBaseLibrariesSource = " + archiveName;
        String newline = config.contains("\r\n") ? "\r\n" : "\n";
        List<String> lines = new ArrayList<>(Arrays.asList(config.split("\\r?\\n", -1)));
        boolean inSection = false, hasSection = false, replaced = false;
        int sectionEnd = -1;
        for (int i = 0; i < lines.size(); i++) {
            String line = lines.get(i).trim();
            if (line.startsWith("[") && line.endsWith("]")) {
                if (inSection) sectionEnd = i;
                inSection = line.equals("[IL2CPP]");
                hasSection |= inSection;
            } else if (inSection && line.matches("UnityBaseLibrariesSource\\s*=.*")) {
                lines.set(i, setting);
                replaced = true;
            }
        }
        if (!replaced) {
            if (!hasSection) {
                lines.add("[IL2CPP]");
                lines.add(setting);
            } else {
                lines.add(sectionEnd < 0 ? lines.size() : sectionEnd, setting);
            }
        }
        return String.join(newline, lines);
    }
}
