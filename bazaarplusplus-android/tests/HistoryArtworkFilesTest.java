package dev.allofus.fusioncore.history;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import javax.imageio.ImageIO;

public final class HistoryArtworkFilesTest {
    private static int checks;
    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
        checks++;
    }
    public static void main(String[] args) throws Exception {
        File game = new File(args[0]);
        HistoryArtworkFiles reader = new HistoryArtworkFiles(game);
        File image = reader.resolve("card-example");
        check(image != null, "Java reader must find the C# exporter output");
        check(image.getName().endsWith(".bppart"), "Reader uses cache extension");
        check(ImageIO.read(image).getWidth() == 1, "PNG payload remains decodable without PNG extension (JVM decoder)");
        check(reader.resolve("../card-example") == null, "Reject path traversal");
        check(reader.resolve(null) == null, "Reject missing key");
        check(reader.resolve("missing") == null, "Missing art remains missing");
        File pointer = new File(image.getParentFile().getParentFile(), "current.txt");
        byte[] version = Files.readAllBytes(pointer.toPath());
        Files.write(pointer.toPath(), "../older".getBytes(StandardCharsets.UTF_8));
        check(reader.resolve("card-example") == null, "Reject invalid version pointer");
        Files.write(pointer.toPath(), new byte[129]);
        check(reader.resolve("card-example") == null, "Reject oversized pointer");
        Files.write(pointer.toPath(), version);
        byte[] bytes = Files.readAllBytes(image.toPath());
        Files.write(image.toPath(), new byte[2 * 1024 * 1024 + 1]);
        check(reader.resolve("card-example") == null, "Reject oversized image");
        Files.write(image.toPath(), bytes);
        Files.delete(pointer.toPath());
        check(reader.resolve("card-example") == null, "Do not fall back to indexed legacy PNG tree");
        Files.write(pointer.toPath(), version);
        check(reader.resolve("card-example").equals(image), "Restore new cache reading");
        System.out.println("History artwork reader: " + checks + " checks passed");
    }
}
