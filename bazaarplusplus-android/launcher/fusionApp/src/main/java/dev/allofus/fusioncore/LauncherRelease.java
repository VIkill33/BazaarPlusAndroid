package dev.allofus.fusioncore;

import android.content.Context;
import android.content.pm.PackageInfo;
import org.json.JSONObject;
import java.io.*;
import java.nio.charset.StandardCharsets;

public final class LauncherRelease {
    public static final String GAME = BuildConfig.TARGET_GAME;
    private LauncherRelease() {}

    public static String read(InputStream in) throws IOException {
        try (InputStream source = in; ByteArrayOutputStream out = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = source.read(buffer)) != -1) {
                if (out.size() + count > 2 * 1024 * 1024) throw new IOException("状态文件超出大小限制");
                out.write(buffer, 0, count);
            }
            return out.toString("UTF-8");
        }
    }

    public static JSONObject manifest(Context context) throws Exception {
        JSONObject manifest = new JSONObject(read(context.getAssets().open("launcher/release.json")));
        if (manifest.getInt("schema") != 1) throw new IOException("不支持的发行清单");
        return manifest;
    }

    public static PackageInfo game(Context context) throws Exception {
        return context.getPackageManager().getPackageInfo(GAME, 0);
    }

    public static void check(Context context, JSONObject release) throws Exception {
        JSONObject game = release.getJSONObject("game");
        PackageInfo installed = game(context);
        if (installed.getLongVersionCode() != game.getLong("versionCode") ||
                !game.getString("versionName").equals(installed.versionName))
            throw new IOException("当前游戏版本尚未验证，请等待匹配的启动器版本");
        if (!new File(installed.applicationInfo.nativeLibraryDir, "libil2cpp.so").isFile() ||
                !installed.applicationInfo.nativeLibraryDir.contains("arm64"))
            throw new IOException("仅支持已安装原生库的 arm64 游戏");
    }

    public static byte[] bytes(String text) { return text.getBytes(StandardCharsets.UTF_8); }
}
