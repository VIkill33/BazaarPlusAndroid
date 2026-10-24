import dev.allofus.fusioncore.history.HistoryAccessLock;
import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.util.concurrent.TimeUnit;

public final class HistoryAccessLockTest {
    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static Process child(String mode, File root) throws IOException {
        return new ProcessBuilder(new File(System.getProperty("java.home"), "bin/java").getPath(),
                "-cp", System.getProperty("java.class.path"), "HistoryAccessLockTest", mode,
                root.getPath()).redirectError(ProcessBuilder.Redirect.INHERIT).start();
    }

    public static void main(String[] args) throws Exception {
        if (args.length > 0) {
            try (HistoryAccessLock ignored = HistoryAccessLock.acquire(new File(args[1]))) {
                if (args[0].equals("hold")) {
                    System.out.println("locked");
                    System.out.flush();
                    System.in.read();
                }
            } catch (HistoryAccessLock.BusyException busy) { System.exit(3); }
            return;
        }
        File root = Files.createTempDirectory("history-access-lock").toFile();
        try {
            try (HistoryAccessLock ignored = HistoryAccessLock.acquire(root)) {
                boolean rejected = false;
                try (HistoryAccessLock unexpected = HistoryAccessLock.acquire(root)) { }
                catch (HistoryAccessLock.BusyException expected) { rejected = true; }
                check(rejected, "Same-process maintenance must not overlap a game lease");
                rejected = false;
                try (HistoryAccessLock unexpected = HistoryAccessLock.acquire(new File(root, "."))) { }
                catch (HistoryAccessLock.BusyException expected) { rejected = true; }
                check(rejected, "An alias of the same root must not open another descriptor");
                Process blocked = child("try", root);
                check(blocked.waitFor(10, TimeUnit.SECONDS), "Child lock attempt timed out");
                check(blocked.exitValue() == 3, "Other process must observe the lease");
                try (HistoryAccessLock otherTarget = HistoryAccessLock.acquire(new File(root, "other-game"))) { }
            }
            File badRoot = new File(root, "not-a-directory");
            check(badRoot.createNewFile(), "Cannot create path failure fixture");
            boolean ioFailure = false;
            try (HistoryAccessLock unexpected = HistoryAccessLock.acquire(badRoot)) { }
            catch (HistoryAccessLock.BusyException wrongFailure) { throw new AssertionError("I/O failure misreported as busy"); }
            catch (IOException expected) { ioFailure = true; }
            check(ioFailure, "Non-directory root must fail without reporting a busy lease");
            Process holder = child("hold", root);
            try {
                check("locked".equals(new java.io.BufferedReader(new java.io.InputStreamReader(
                        holder.getInputStream())).readLine()), "Child did not acquire lease");
                holder.destroyForcibly();
                check(holder.waitFor(10, TimeUnit.SECONDS), "Child did not die");
                try (HistoryAccessLock recovered = HistoryAccessLock.acquire(root)) { }
            } finally { holder.destroyForcibly(); }
            System.out.println("HistoryAccessLock: overlap, target isolation, process exclusion and death recovery passed");
        } finally {
            try (var paths = Files.walk(root.toPath())) {
                for (var path : paths.sorted(java.util.Comparator.reverseOrder()).toList()) Files.delete(path);
            }
        }
    }
}
