package dev.allofus.fusioncore;

import java.io.*;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/** File transactions shared by preparation and repair. Never owns game persistent data. */
public final class LauncherFiles {
    private LauncherFiles() {}

    public static String sha256(File file) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (InputStream in = new FileInputStream(file)) {
            byte[] buffer = new byte[65536];
            int count;
            while ((count = in.read(buffer)) != -1) digest.update(buffer, 0, count);
        }
        StringBuilder result = new StringBuilder();
        for (byte b : digest.digest()) result.append(String.format("%02x", b & 255));
        return result.toString();
    }

    public static boolean valid(File file, String hash, long size) throws Exception {
        return file.isFile() && (size < 0 || file.length() == size) && sha256(file).equals(hash);
    }

    /** Reads asset streams on every supported Android version without API 33 stream methods. */
    public static byte[] readFully(InputStream in) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = in.read(buffer)) != -1) out.write(buffer, 0, count);
        return out.toByteArray();
    }

    public static void write(File file, byte[] data) throws IOException {
        mkdir(file.getParentFile());
        File temp = new File(file.getPath() + ".tmp");
        try (FileOutputStream out = new FileOutputStream(temp)) {
            out.write(data);
            out.getFD().sync();
        }
        replace(temp, file);
    }

    public static void replace(File source, File target) throws IOException {
        mkdir(target.getParentFile());
        Files.move(source.toPath(), target.toPath(), StandardCopyOption.ATOMIC_MOVE,
                StandardCopyOption.REPLACE_EXISTING);
    }

    /** Android 14 requires dynamically loaded code to be read-only before writing its contents. */
    public static void writeReadOnly(File file, byte[] data) throws IOException {
        mkdir(file.getParentFile());
        File temp = File.createTempFile("readonly-", ".tmp", file.getParentFile());
        try {
            try (FileOutputStream out = new FileOutputStream(temp)) {
                if (!temp.setReadOnly()) throw new IOException("无法将运行代码设为只读");
                out.write(data);
                out.getFD().sync();
            }
            replace(temp, file);
        } finally {
            Files.deleteIfExists(temp.toPath());
        }
    }

    public static void mkdir(File dir) throws IOException {
        if (!dir.isDirectory() && !dir.mkdirs()) throw new IOException("无法创建运行目录");
    }

    public static void extract(File archive, File directory) throws IOException {
        mkdir(directory);
        String root = directory.getCanonicalPath() + File.separator;
        long expanded = 0;
        try (ZipInputStream in = new ZipInputStream(new FileInputStream(archive))) {
            ZipEntry entry;
            byte[] buffer = new byte[65536];
            while ((entry = in.getNextEntry()) != null) {
                File target = new File(directory, entry.getName());
                if (!target.getCanonicalPath().startsWith(root)) throw new IOException("拒绝越界压缩文件");
                if (entry.isDirectory()) { mkdir(target); continue; }
                mkdir(target.getParentFile());
                try (OutputStream out = new FileOutputStream(target)) {
                    int count;
                    while ((count = in.read(buffer)) != -1) {
                        expanded += count;
                        if (expanded > 1024L * 1024 * 1024) throw new IOException("解压内容超出限制");
                        out.write(buffer, 0, count);
                    }
                }
            }
        }
    }

    /** Only callers with an explicitly owned staging/cache directory may use this. */
    public static void removeOwned(File file) throws IOException {
        if (Files.isSymbolicLink(file.toPath())) throw new IOException("拒绝清理符号链接");
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children == null) throw new IOException("无法读取运行目录");
            for (File child : children) removeOwned(child);
        }
        if (file.exists() && !file.delete()) throw new IOException("无法清理派生文件");
    }
}
