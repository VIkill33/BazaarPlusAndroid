package dev.allofus.fusioncore.history;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.nio.channels.FileLock;
import java.nio.channels.OverlappingFileLockException;

/** Exclusive game/maintenance lease. Never unlink the lock file while an app may use it. */
public final class HistoryAccessLock implements AutoCloseable {
    public static final String DIRECTORY_NAME = "BppAndroidHistory";
    private static final java.util.Set<String> HELD = new java.util.HashSet<>();
    private final RandomAccessFile file;
    private final FileLock lock;
    private final String identity;
    private boolean closed;

    public static final class BusyException extends IOException {
        public BusyException() { super("游戏或历史维护正在运行，请完全关闭启动器后重试"); }
    }

    private HistoryAccessLock(RandomAccessFile file, FileLock lock, String identity) {
        this.file = file;
        this.lock = lock;
        this.identity = identity;
    }

    public static synchronized HistoryAccessLock acquire(File gameRoot) throws IOException {
        return acquireNamed(gameRoot, ".access.lock");
    }

    static synchronized HistoryAccessLock acquireSchema(File gameRoot) throws IOException {
        return acquireNamed(gameRoot, ".schema.lock");
    }

    private static HistoryAccessLock acquireNamed(File gameRoot, String name) throws IOException {
        File directory = new File(gameRoot, DIRECTORY_NAME);
        if (!directory.isDirectory() && !directory.mkdirs()) {
            throw new IOException("Cannot create history directory");
        }
        String identity = new File(directory, name).getCanonicalPath();
        // POSIX locks can be released by closing ANY descriptor for the same inode in this process.
        // Reject re-entry before opening a second descriptor, including aliases of the same path.
        if (HELD.contains(identity)) throw new BusyException();
        RandomAccessFile file = new RandomAccessFile(identity, "rw");
        try {
            FileLock lock;
            try { lock = file.getChannel().tryLock(); }
            catch (OverlappingFileLockException busy) { lock = null; }
            if (lock == null) throw new BusyException();
            HELD.add(identity);
            return new HistoryAccessLock(file, lock, identity);
        } catch (IOException | RuntimeException error) {
            file.close();
            throw error;
        }
    }

    @Override public void close() throws IOException {
        synchronized (HistoryAccessLock.class) {
            if (closed) return;
            closed = true;
            try { lock.release(); }
            finally {
                try { file.close(); }
                finally { HELD.remove(identity); }
            }
        }
    }
}
