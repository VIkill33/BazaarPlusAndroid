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
import java.util.Locale;
import java.util.regex.Matcher;
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
    private static final Pattern SIGNAL = Pattern.compile(
            "signal\\s+\\d+\\s+\\(([A-Za-z0-9]+)\\)(?:,\\s*code\\s+\\d+\\s+\\(([^)]*)\\))?(?:,\\s*fault addr\\s+(\\S+))?",
            Pattern.CASE_INSENSITIVE);
    private static final Pattern ABORT = Pattern.compile("Abort message:\\s*'(.*)'", Pattern.CASE_INSENSITIVE);
    private static final Pattern CAUSE = Pattern.compile("Cause:\\s*(.+)", Pattern.CASE_INSENSITIVE);
    private static final Pattern PAGE = Pattern.compile("page size\\s+(\\d+)", Pattern.CASE_INSENSITIVE);
    private static final Pattern FRAME = Pattern.compile("#\\d+\\s+pc\\s+([0-9a-fA-F]+)\\s+(\\S+)(?:\\s+\\((.*)\\))?");
    private static final String[] RUNTIME_LIBRARIES = {
            "libc.so", "libc++.so", "libm.so", "libdl.so", "libart.so", "libbase.so", "linker", "linker64",
            "libunwindstack.so", "libutils.so", "libcutils.so", "libandroid_runtime.so", "libnativeloader.so"};

    private LauncherLog() {}

    private static final class NativeCrash {
        String signal = "";
        String code = "";
        String address = "";
        String abort = "";
        String cause = "";
        String page = "";
        boolean sixteenKb;
        final List<String> frames = new ArrayList<>();
    }

    private static NativeCrash parseCrash(String trace) {
        NativeCrash crash = new NativeCrash();
        if (trace == null || trace.isBlank()) return crash;
        int seen = 0;
        for (String raw : trace.split("\n", 500)) {
            if (++seen > 500) break;
            String line = raw.trim();
            if (line.isEmpty()) continue;
            if (crash.signal.isEmpty()) {
                Matcher signal = SIGNAL.matcher(line);
                if (signal.find()) {
                    crash.signal = signal.group(1);
                    crash.code = signal.group(2) == null ? "" : signal.group(2).trim();
                    crash.address = signal.group(3) == null ? "" : signal.group(3).trim();
                    continue;
                }
            }
            if (crash.abort.isEmpty()) {
                Matcher abort = ABORT.matcher(line);
                if (abort.find()) { crash.abort = abort.group(1).trim(); continue; }
            }
            if (crash.cause.isEmpty()) {
                Matcher cause = CAUSE.matcher(line);
                if (cause.find()) { crash.cause = cause.group(1).trim(); continue; }
            }
            if (crash.page.isEmpty()) {
                Matcher page = PAGE.matcher(line);
                if (page.find()) { crash.page = page.group(1); continue; }
            }
            if (line.equalsIgnoreCase("16kb mode")) { crash.sixteenKb = true; continue; }
            if (crash.frames.size() >= 8) continue;
            Matcher frame = FRAME.matcher(line);
            if (!frame.find()) continue;
            String file = frame.group(2);
            int slash = Math.max(file.lastIndexOf('/'), file.lastIndexOf('\\'));
            String base = slash >= 0 ? file.substring(slash + 1) : file;
            if (base.isEmpty() || base.charAt(0) == '[') continue;
            String offset = frame.group(1).replaceFirst("^0+(?=.)", "");
            String function = frame.group(3) == null ? "" : frame.group(3).trim();
            crash.frames.add(base + "+0x" + offset + (function.isEmpty() ? "" : " " + function));
        }
        return crash;
    }

    private static String interestingLibrary(List<String> frames) {
        String fallback = "";
        for (String frame : frames) {
            int end = frame.indexOf('+');
            String library = end < 0 ? frame : frame.substring(0, end);
            if (fallback.isEmpty()) fallback = library;
            if (!runtimeLibrary(library)) return library;
        }
        return fallback;
    }

    private static boolean runtimeLibrary(String library) {
        String name = library.toLowerCase(Locale.ROOT);
        for (String runtime : RUNTIME_LIBRARIES) if (runtime.equals(name)) return true;
        return false;
    }

    private static List<String> javaCrashLines(String trace) {
        List<String> lines = new ArrayList<>();
        if (trace == null || !trace.contains("at ")) return lines;
        int seen = 0;
        for (String raw : trace.split("\n", 40)) {
            if (++seen > 40 || lines.size() >= 7) break;
            String line = raw.trim();
            if (line.isEmpty()) continue;
            if (lines.isEmpty() && !line.startsWith("at ") && !line.startsWith("Caused by")) {
                lines.add(sanitize("异常 " + line));
                continue;
            }
            if (line.startsWith("at ")) lines.add(sanitize("异常栈 " + line.substring(3).trim()));
        }
        return lines;
    }

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

    /** Short native-crash label, such as {@code SIGSEGV libil2cpp.so}. Empty when the trace has no signal. */
    public static String crashSignal(String trace) {
        NativeCrash crash = parseCrash(trace);
        if (crash.signal.isEmpty()) return "";
        String library = interestingLibrary(crash.frames);
        return library.isEmpty() ? crash.signal : crash.signal + " " + library;
    }

    /** Sanitized signal, abort, page size and basename frames. Java traces yield a short stack instead. */
    public static List<String> crashLines(String trace) {
        NativeCrash crash = parseCrash(trace);
        if (crash.signal.isEmpty()) return javaCrashLines(trace);
        List<String> lines = new ArrayList<>();
        String detail = crash.code;
        if (!crash.address.isEmpty()) detail = detail.isEmpty() ? crash.address : detail + " " + crash.address;
        if (!detail.isEmpty()) lines.add(sanitize("信号 " + detail));
        if (!crash.abort.isEmpty()) lines.add(sanitize("中止信息 " + crash.abort));
        if (!crash.cause.isEmpty()) lines.add(sanitize("崩溃原因 " + crash.cause));
        if (!crash.page.isEmpty()) lines.add(sanitize("页面大小 " + crash.page + (crash.sixteenKb ? " 16kb" : "")));
        else if (crash.sixteenKb) lines.add("页面大小 16kb");
        for (String frame : crash.frames) {
            if (lines.size() >= 12) break;
            lines.add(sanitize("崩溃栈 " + frame));
        }
        return lines;
    }

    public static void note(File filesDir, String message) {
        if (filesDir == null) return;
        try { append(file(filesDir), System.currentTimeMillis(), message); }
        catch (Exception ignored) {}
    }

    /** Unity wraps the original exception in a stackless Error; retain its causes separately. */
    public static List<String> throwableLines(Throwable error) {
        List<String> lines = new ArrayList<>();
        java.util.Set<Throwable> seen = java.util.Collections.newSetFromMap(new java.util.IdentityHashMap<>());
        Throwable current = error;
        for (int depth = 0; current != null && depth < 8; depth++) {
            if (!seen.add(current)) {
                lines.add("异常原因链循环，已停止记录");
                return lines;
            }
            lines.add(sanitize((depth == 0 ? "异常 " : "异常原因 ") + current.getClass().getName()
                    + (current.getMessage() == null ? "" : ": " + current.getMessage())));
            StackTraceElement[] frames = current.getStackTrace();
            for (int i = 0; i < frames.length && i < 6; i++) {
                StackTraceElement frame = frames[i];
                lines.add(sanitize("异常栈 " + frame.getClassName() + "."
                        + frame.getMethodName() + ":" + frame.getLineNumber()));
            }
            current = current.getCause();
        }
        if (current != null) lines.add("异常原因链过长，已停止记录");
        return lines;
    }

    public static void noteThrowable(File filesDir, Throwable error) {
        for (String line : throwableLines(error)) note(filesDir, line);
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
