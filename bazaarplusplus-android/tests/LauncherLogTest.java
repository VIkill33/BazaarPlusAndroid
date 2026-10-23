import dev.allofus.fusioncore.LauncherFiles;
import dev.allofus.fusioncore.LauncherLog;
import java.io.File;
import java.nio.file.Files;
import java.util.List;

public class LauncherLogTest {
    static void require(boolean value) { if (!value) throw new AssertionError(); }

    public static void main(String[] args) throws Exception {
        String kept = LauncherLog.sanitize("初始化失败：文件完整性校验失败，请重试");
        require(kept.equals("初始化失败：文件完整性校验失败，请重试"));
        String mixed = LauncherLog.sanitize("初始化失败：token=super-secret-token 请重试 user@example.com");
        require(mixed.equals("初始化失败：token=[redacted] 请重试 [redacted]"));
        require(!mixed.contains("super-secret-token") && !mixed.contains("user@example.com"));
        String secret = LauncherLog.sanitize("Bearer abc.def.ghi password=hunter2 https://example.com/a?token=abc /storage/emulated/0/Android/data/account");
        require(!secret.contains("hunter2") && !secret.contains("example.com") && !secret.contains("/storage") && !secret.contains("account"));
        require(secret.contains("bearer [redacted]") && secret.contains("[url]") && secret.contains("[path]"));
        String jwt = LauncherLog.sanitize("会话 eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signaturevalue 结束");
        require(!jwt.contains("eyJzdWI") && jwt.contains("[redacted]"));
        String hash = LauncherLog.sanitize("摘要 " + "ab".repeat(32) + " 保留 6000.3.11f1");
        require(!hash.contains("ab".repeat(32)) && hash.contains("6000.3.11f1"));
        require(LauncherLog.processExit(5).equals("原生崩溃"));
        require(LauncherLog.processExit(4).equals("应用崩溃"));
        require(LauncherLog.processExit(3).equals("内存不足"));
        require(LauncherLog.processExit(10).equals("被用户划掉"));
        require(LauncherLog.processExit(99).equals("进程已结束"));
        require(LauncherLog.sanitize(null).equals("（空）"));
        require(LauncherLog.sanitize("x".repeat(300)).endsWith("…") && LauncherLog.sanitize("x".repeat(300)).length() == 240);

        String trace = "signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x0\n"
                + "Abort message: 'token=super-secret-token /storage/emulated/0/a'\n"
                + "page size 16384\n"
                + "16kb mode\n"
                + "backtrace:\n"
                + "      #00 pc 00000000000abcde  /system/lib64/libc.so (abort+160)\n"
                + "      #01 pc 0000000000012345  /data/app/lib/arm64/libil2cpp.so (Runtime::Init+32)\n";
        require(LauncherLog.crashSignal(trace).equals("SIGSEGV libil2cpp.so"));
        require(LauncherLog.crashSignal(null).isEmpty());
        require(LauncherLog.crashSignal("no signal here").isEmpty());
        List<String> crash = LauncherLog.crashLines(trace);
        require(crash.contains("信号 SEGV_MAPERR 0x0"));
        require(crash.contains("页面大小 16384 16kb"));
        String abort = crash.stream().filter(line -> line.startsWith("中止信息")).findFirst().orElse("");
        require(abort.contains("token=[redacted]") && abort.contains("[path]") && !abort.contains("super-secret-token") && !abort.contains("/storage"));
        String nativeFrame = crash.stream().filter(line -> line.contains("libil2cpp.so")).findFirst().orElse("");
        require(nativeFrame.equals("崩溃栈 libil2cpp.so+0x12345 Runtime::Init+32") && !nativeFrame.contains("/data"));
        require(crash.stream().anyMatch(line -> line.startsWith("崩溃栈 libc.so+0xabcde")));
        require(LauncherLog.crashSignal("signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x0\n"
                + "#00 pc 0000000000000010  /system/lib64/libc.so (abort+1)\n").equals("SIGSEGV libc.so"));
        List<String> javaCrash = LauncherLog.crashLines("java.lang.IllegalStateException: token=abc\n"
                + "\tat dev.allofus.fusioncore.BootstrapActivity.prepare(BootstrapActivity.java:10)\n");
        require(javaCrash.size() == 2);
        require(javaCrash.get(0).contains("IllegalStateException") && javaCrash.get(0).contains("token=[redacted]") && !javaCrash.get(0).contains("token=abc"));
        require(javaCrash.get(1).contains("BootstrapActivity.prepare") && !javaCrash.get(1).contains("at "));
        require(LauncherLog.crashLines(null).isEmpty());

        // Same wrapping used by UnityPlayer's uncaught handler: empty outer stack, original in cause.
        Throwable original = new NoSuchMethodError("Missing API token=private-token /storage/emulated/0/private");
        original.setStackTrace(new StackTraceElement[] {
                new StackTraceElement("com.unity3d.player.UnityPlayer", "initialize", "UnityPlayer.java", 42)});
        Error unity = new Error("FATAL EXCEPTION [main]\nUnity version : 6000.3.11f1\n" + "Device info ".repeat(40));
        unity.setStackTrace(new StackTraceElement[0]);
        unity.initCause(new IllegalStateException("Cannot prepare activity", original));
        List<String> wrapped = LauncherLog.throwableLines(unity);
        require(wrapped.stream().anyMatch(line -> line.contains("异常原因 java.lang.NoSuchMethodError")));
        require(wrapped.stream().anyMatch(line -> line.equals("异常栈 com.unity3d.player.UnityPlayer.initialize:42")));
        require(wrapped.stream().anyMatch(line -> line.contains("Cannot prepare activity")));
        require(wrapped.stream().noneMatch(line -> line.contains("private-token") || line.contains("/storage")));
        require(wrapped.stream().allMatch(line -> line.length() <= 240));
        require(LauncherLog.throwableLines(null).isEmpty());
        Throwable first = new Throwable("first");
        Throwable second = new Throwable("second", first);
        first.initCause(second);
        require(LauncherLog.throwableLines(first).get(LauncherLog.throwableLines(first).size() - 1).contains("循环"));
        Throwable deep = original;
        for (int i = 0; i < 20; i++) deep = new Throwable("wrapper " + i, deep);
        List<String> boundedCauses = LauncherLog.throwableLines(deep);
        require(boundedCauses.stream().filter(line -> line.startsWith("异常原因 ")).count() == 7);
        require(boundedCauses.get(boundedCauses.size() - 1).contains("过长"));

        File root = Files.createTempDirectory("bpp-launcher-log").toFile();
        try {
            File log = new File(root, LauncherLog.FILE_NAME);
            LauncherLog.append(log, 1_700_000_000_000L, "准备运行框架");
            LauncherLog.append(log, 1_700_000_000_100L, "准备运行框架");
            List<String> once = LauncherLog.readLines(log);
            require(once.size() == 1);
            require(once.get(0).equals("2023-11-14T22:13:20.000Z 准备运行框架"));
            LauncherLog.append(log, 1_700_000_001_000L, "token=super-secret-token");
            List<String> two = LauncherLog.readLines(log);
            require(two.size() == 2 && two.get(1).endsWith("token=[redacted]") && !two.get(1).contains("super-secret-token"));
            LauncherLog.noteThrowable(root, unity);
            String recorded = String.join("\n", LauncherLog.readLines(log));
            require(recorded.contains("NoSuchMethodError") && recorded.contains("UnityPlayer.initialize:42"));
            require(!recorded.contains("private-token") && !recorded.contains("/storage"));
            for (int i = 0; i < 210; i++) LauncherLog.append(log, 1_700_000_002_000L + i, "步骤 " + i);
            List<String> bounded = LauncherLog.readLines(log);
            require(bounded.size() == 200);
            require(bounded.get(0).endsWith("步骤 10"));
            require(bounded.get(bounded.size() - 1).endsWith("步骤 209"));
            require(LauncherLog.readLines(new File(root, "missing.txt")).isEmpty());
            System.out.println("Launcher log redaction, dedupe and retention passed");
        } finally { LauncherFiles.removeOwned(root); }
    }
}
