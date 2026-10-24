import dev.allofus.fusioncore.history.HistoryAccessLock;
import java.io.File;

/** Run using app_process in a second Android process, never inside the game process. */
public final class HistoryLockDevice {
    public static void main(String[] args) throws Exception {
        if (args.length < 1 || args.length > 2 || (args.length == 2 && !"hold".equals(args[1]))) {
            throw new IllegalArgumentException("Expected the confirmed target game root and optional hold");
        }
        try (HistoryAccessLock ignored = HistoryAccessLock.acquire(new File(args[0]))) {
            System.out.println("history_lock_acquired");
            System.out.flush();
            if (args.length == 2) System.in.read();
        } catch (HistoryAccessLock.BusyException busy) {
            System.err.println("history_lock_rejected: " + busy.getMessage());
            System.exit(3);
        }
    }
}
