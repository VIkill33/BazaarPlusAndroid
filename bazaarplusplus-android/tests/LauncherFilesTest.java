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
