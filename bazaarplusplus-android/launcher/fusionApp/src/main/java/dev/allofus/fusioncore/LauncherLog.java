package dev.allofus.fusioncore;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

/** Bounded launcher log. Export is user-triggered and must not carry game logs, accounts, or tokens. */
public final class LauncherLog {
    public static final String FILE_NAME = "launcher-log.txt";
    static final int MAX_LINES = 200;
    static final int MAX_LINE = 240;
    private static final int MAX_BYTES = 256 * 1024;
    private static final Object LOCK = new Object();
    private static final DateTimeFormatter TIMESTAMP = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'")
            .withZone(ZoneOffset.UTC);
    private static final Pattern BEARER = Pattern.compile("(?i)\\bbearer\\s+\\S+");
    private static final Pattern JWT = Pattern.compile("(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{12,}\\.[A-Za-z0-9_-]{12,}\\.[A-Za-z0-9_-]{8,}(?![A-Za-z0-9_-])");
    private static final Pattern SECRET = Pattern.compile("(?i)\\b(token|password|passwd|secret|authorization|api[_-]?key|access[_-]?token|refresh[_-]?token|session(?:id)?|account(?:id)?|player(?:id)?|user(?:id)?|uid)\\s*[=:]\\s*\\S+");
    private static final Pattern URL = Pattern.compile("(?i)(?:https?|content)://\\S+");
    private static final Pattern PATH = Pattern.compile("(?:[A-Za-z]:)?(?:\\\\|/)(?:[A-Za-z0-9._@+-]+(?:\\\\|/))+[A-Za-z0-9._@+-]*");
    private static final Pattern EMAIL = Pattern.compile("[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}");
    private static final Pattern HEX = Pattern.compile("\\b[a-fA-F0-9]{32,}\\b");

    private LauncherLog() {}

    public static File file(File filesDir) { return new File(filesDir, FILE_NAME); }

    /** Android ApplicationExitInfo.Reason values, kept free of the Android SDK. */
    public static String processExit(int reason) {
        return switch (reason) {
            case 4 -> "应用崩溃";
            case 5 -> "原生崩溃";
            case 6 -> "无响应";
            case 3 -> "内存不足";
            case 1 -> "自行退出";
            case 2 -> "被系统结束";
            case 10, 11 -> "被用户划掉";
            default -> "进程已结束";
        };
    }

    public static void note(File filesDir, String message) {
        if (filesDir == null) return;
        try { append(file(filesDir), System.currentTimeMillis(), message); }
        catch (Exception ignored) {}
    }

    public static void append(File file, long epochMs, String message) throws IOException {
        String body = sanitize(message);
        synchronized (LOCK) {
            List<String> lines = readUnlocked(file);
            if (!lines.isEmpty() && body(lines.get(lines.size() - 1)).equals(body)) return;
            lines.add(TIMESTAMP.format(Instant.ofEpochMilli(epochMs)) + " " + body);
            if (lines.size() > MAX_LINES) lines.subList(0, lines.size() - MAX_LINES).clear();
            LauncherFiles.write(file, String.join("\n", lines).concat("\n").getBytes(StandardCharsets.UTF_8));
        }
    }

    public static List<String> readLines(File file) throws IOException {
        synchronized (LOCK) { return readUnlocked(file); }
    }

    public static String sanitize(String message) {
        String clean = message == null ? "" : message.replaceAll("[\\u0000-\\u001F]+", " ");
        clean = BEARER.matcher(clean).replaceAll("bearer [redacted]");
        clean = JWT.matcher(clean).replaceAll("[redacted]");
        clean = SECRET.matcher(clean).replaceAll("$1=[redacted]");
        clean = URL.matcher(clean).replaceAll("[url]");
        clean = PATH.matcher(clean).replaceAll("[path]");
        clean = EMAIL.matcher(clean).replaceAll("[redacted]");
        clean = HEX.matcher(clean).replaceAll("[redacted]");
        clean = clean.replaceAll(" {2,}", " ").trim();
        if (clean.isEmpty()) return "（空）";
        if (clean.length() > MAX_LINE) return clean.substring(0, MAX_LINE - 1) + "…";
        return clean;
    }

    private static String body(String line) {
        int space = line.indexOf(' ');
        return space < 0 ? line : line.substring(space + 1);
    }

    private static List<String> readUnlocked(File file) throws IOException {
        List<String> lines = new ArrayList<>();
        if (file == null || !file.isFile() || file.length() == 0) return lines;
        byte[] data;
        boolean partial = file.length() > MAX_BYTES;
        if (partial) {
            data = new byte[MAX_BYTES];
            try (RandomAccessFile in = new RandomAccessFile(file, "r")) {
                in.seek(file.length() - MAX_BYTES);
                in.readFully(data);
            }
        } else data = java.nio.file.Files.readAllBytes(file.toPath());
        String text = new String(data, StandardCharsets.UTF_8);
        int start = 0;
        if (partial) {
            int newline = text.indexOf('\n');
            start = newline < 0 ? text.length() : newline + 1;
        }
        for (String line : text.substring(start).split("\n", -1)) {
            if (!line.isBlank()) lines.add(line.length() > MAX_LINE + 40 ? line.substring(0, MAX_LINE + 40) : line);
        }
        if (lines.size() > MAX_LINES) return new ArrayList<>(lines.subList(lines.size() - MAX_LINES, lines.size()));
        return lines;
    }
}
