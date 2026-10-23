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
