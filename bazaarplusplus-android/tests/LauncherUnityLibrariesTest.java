import dev.allofus.fusioncore.LauncherFiles;
import dev.allofus.fusioncore.LauncherUnityLibraries;
import java.io.*;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.zip.*;

public final class LauncherUnityLibrariesTest {
    private static final String NAME = "6000.3.11.zip";
    private static final String OLD_SOURCE = "https://github.com/All-Of-Us-Mods/FusionCore.UnityDependencies/releases/download/6000.3.11f1/Managed.zip";
    private static void require(boolean value) { if (!value) throw new AssertionError(); }

    private static String source(String config) {
        boolean inSection = false;
        for (String line : config.split("\\r?\\n")) {
            line = line.trim();
            if (line.startsWith("[")) inSection = line.equals("[IL2CPP]");
            else if (inSection && line.startsWith("UnityBaseLibrariesSource"))
                return line.substring(line.indexOf('=') + 1).trim();
        }
        throw new AssertionError("Missing IL2CPP source");
    }

    // Filename resolution matches the pinned BepInEx DownloadUnityAssemblies consumer.
    private static File consumedArchive(File libs, String source) throws Exception {
        URI uri = new URI(source);
        return new File(libs, new File(uri.isAbsolute() ? uri.getPath() : source).getName());
    }

    public static void main(String[] args) throws Exception {
        String original = "# keep comment\n[Other]\nUnityBaseLibrariesSource = untouched\n[IL2CPP]\n"
                + "# keep generation setting\nUpdateInteropAssemblies = true\nUnityBaseLibrariesSource = " + OLD_SOURCE + "\n"
                + "[Logging.Console]\nEnabled = true\n";
        String configured = LauncherUnityLibraries.configure(original, NAME);
        require(source(configured).equals(NAME));
        require(configured.contains("[Other]\nUnityBaseLibrariesSource = untouched"));
        require(configured.contains("# keep generation setting\nUpdateInteropAssemblies = true"));
        require(configured.endsWith("[Logging.Console]\nEnabled = true\n"));
        require(LauncherUnityLibraries.configure(configured, NAME).equals(configured));
        require(source(LauncherUnityLibraries.configure(original.replace("\n", "\r\n"), NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure("[IL2CPP]\nOther = keep\n[Next]\nX = y\n", NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure("[IL2CPP]\nOther = keep", NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure("[Other]\nX = y\n", NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure("", NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure("[IL2CPP]\n UnityBaseLibrariesSource  = broken", NAME)).equals(NAME));
        require(source(LauncherUnityLibraries.configure(configured, "6000.3.12.zip")).equals("6000.3.12.zip"));
        for (String invalid : new String[] {null, "", "../outside.zip", "/tmp/archive.zip", "http://host/Managed.zip", "dir\\file.zip", "libunity.so"}) {
            try { LauncherUnityLibraries.configure(original, invalid); throw new AssertionError("Invalid source accepted"); }
            catch (IllegalArgumentException expected) { }
        }
        File root = Files.createTempDirectory("bpp-unity-libraries").toFile();
        try {
            File libs = new File(root, "unity-libs");
            LauncherFiles.mkdir(libs);
            File archive = new File(libs, NAME);
            try (ZipOutputStream zip = new ZipOutputStream(new FileOutputStream(archive))) {
                zip.putNextEntry(new ZipEntry("UnityEngine.CoreModule.dll"));
                zip.write("fixture assembly".getBytes(StandardCharsets.UTF_8)); zip.closeEntry();
            }
            require(!consumedArchive(libs, source(original)).exists()); // Old config would download another filename.
            LauncherFiles.write(new File(libs, "Managed.zip"), "partial old download".getBytes(StandardCharsets.UTF_8));
            try (ZipFile old = new ZipFile(consumedArchive(libs, source(original)))) {
                throw new AssertionError("Expected the original ZIP failure");
            } catch (ZipException expected) { }
            File selected = consumedArchive(libs, source(configured));
            require(selected.getCanonicalFile().equals(archive.getCanonicalFile()));
            try (ZipFile zip = new ZipFile(selected)) {
                require(zip.getEntry("UnityEngine.CoreModule.dll") != null);
            }
            // Regeneration must work with no interop directory and an unrelated corrupt Managed.zip present.
            require(!new File(root, "interop").exists());
            LauncherFiles.extract(selected, new File(root, "expanded"));
            require(new File(root, "expanded/UnityEngine.CoreModule.dll").length() == 16);
            require(new File(libs, "Managed.zip").isFile()); // No unrelated cache or user data cleanup.
            if (args.length == 1) {
                File fixture = new File(args[0]);
                LauncherFiles.mkdir(new File(fixture, "unity-libs"));
                Files.copy(archive.toPath(), new File(fixture, "unity-libs/" + NAME).toPath(), StandardCopyOption.REPLACE_EXISTING);
                LauncherFiles.write(new File(fixture, "unity-libs/Managed.zip"), "partial old download".getBytes(StandardCharsets.UTF_8));
                LauncherFiles.write(new File(fixture, "BepInEx.cfg"), configured.getBytes(StandardCharsets.UTF_8));
                LauncherFiles.write(new File(fixture, "before.cfg"), original.getBytes(StandardCharsets.UTF_8));
            }
        } finally { LauncherFiles.removeOwned(root); }
        System.out.println("Unity library source, config preservation and corrupt legacy ZIP regression passed");
    }
}
