import dev.allofus.fusioncore.LauncherFiles;
import dev.allofus.fusioncore.LauncherSession;
import java.io.*;
import java.nio.file.*;
import java.util.zip.*;

public class LauncherFilesTest {
    static void require(boolean value) { if (!value) throw new AssertionError(); }
    public static void main(String[] args) throws Exception {
        require(LauncherSession.matches("new", "new", "1", "1", "hash", "hash", 100, 101, true, true));
        require(!LauncherSession.matches("new", "old", "1", "1", "hash", "hash", 100, 101, true, true));
        require(!LauncherSession.matches("new", "new", "2", "1", "hash", "hash", 100, 101, true, true));
        require(!LauncherSession.matches("new", "new", "1", "1", "newhash", "oldhash", 100, 101, true, true));
        require(!LauncherSession.matches("new", "new", "1", "1", "hash", "hash", 100, 99, true, true));
        require(!LauncherSession.matches("new", "new", "1", "1", "hash", "hash", 100, 101, false, true));
        require(!LauncherSession.matches("new", "new", "1", "1", "hash", "hash", 100, 101, true, false));
        require(LauncherFiles.readFully(new ByteArrayInputStream(new byte[0])).length == 0);
        byte[] asset = new byte[20003];
        for (int i = 0; i < asset.length; i++) asset[i] = (byte) i;
        InputStream shortReads = new ByteArrayInputStream(asset) {
            @Override public int available() { return 0; }
            @Override public synchronized int read(byte[] bytes, int offset, int length) {
                return super.read(bytes, offset, Math.min(length, 7));
            }
            @Override public byte[] readAllBytes() { throw new AssertionError("API 33 method used"); }
        };
        require(java.util.Arrays.equals(LauncherFiles.readFully(shortReads), asset));
        InputStream broken = new InputStream() {
            private boolean started;
            @Override public int read() throws IOException { throw new IOException("asset read failed"); }
            @Override public int read(byte[] bytes, int offset, int length) throws IOException {
                if (started) throw new IOException("asset read failed");
                started = true;
                bytes[offset] = 42;
                return 1;
            }
        };
        try { LauncherFiles.readFully(broken); throw new AssertionError("partial asset accepted"); }
        catch (IOException expected) { require(expected.getMessage().equals("asset read failed")); }
        File root = Files.createTempDirectory("bpp-launcher-tests").toFile();
        try {
            File installed = new File(root, "plugins/mod.dll");
            LauncherFiles.write(installed, new byte[]{1, 2, 3});
            String hash = LauncherFiles.sha256(installed);
            require(LauncherFiles.valid(installed, hash, 3));
            LauncherFiles.write(installed, new byte[]{3, 2, 1});
            require(!LauncherFiles.valid(installed, hash, 3)); // Same-size corruption.
            try { LauncherFiles.replace(new File(root, "missing"), installed); throw new AssertionError(); }
            catch (IOException expected) { require(installed.length() == 3); }
            File dex = new File(root, "code_cache/guard.dex");
            LauncherFiles.write(dex, new byte[]{0}); // Upgrade from the old writable cache.
            require(dex.canWrite());
            LauncherFiles.writeReadOnly(dex, new byte[]{1, 2});
            require(java.util.Arrays.equals(Files.readAllBytes(dex.toPath()), new byte[]{1, 2}));
            require(!dex.canWrite());
            LauncherFiles.writeReadOnly(dex, new byte[]{3, 4, 5}); // Replace a read-only old version.
            require(java.util.Arrays.equals(Files.readAllBytes(dex.toPath()), new byte[]{3, 4, 5}));
            require(!dex.canWrite());
            require(dex.getParentFile().list().length == 1); // No staging file is left behind.
            File blocked = new File(root, "code_cache/nonempty");
            LauncherFiles.write(new File(blocked, "keep"), new byte[]{9});
            try { LauncherFiles.writeReadOnly(blocked, new byte[]{1}); throw new AssertionError(); }
            catch (IOException expected) { require(new File(blocked, "keep").isFile()); }
            require(dex.getParentFile().list().length == 2);
            File archive = new File(root, "bad.zip");
            try (ZipOutputStream out = new ZipOutputStream(new FileOutputStream(archive))) {
                out.putNextEntry(new ZipEntry("../outside")); out.write(1); out.closeEntry();
            }
            try { LauncherFiles.extract(archive, new File(root, "stage")); throw new AssertionError(); }
            catch (IOException expected) { require(!new File(root, "outside").exists()); }
            File persistent = new File(root, "PersistentData/account");
            LauncherFiles.write(persistent, new byte[]{9});
            Path link = new File(root, "derived").toPath();
            Files.createSymbolicLink(link, persistent.getParentFile().toPath());
            try { LauncherFiles.removeOwned(link.toFile()); throw new AssertionError(); }
            catch (IOException expected) { require(persistent.isFile()); }
            Files.delete(link);
            LauncherFiles.removeOwned(new File(root, "stage"));
            require(persistent.isFile());
            System.out.println("Launcher file integrity, failed replacement, traversal and cleanup boundaries passed");
        } finally { LauncherFiles.removeOwned(root); }
    }
}
