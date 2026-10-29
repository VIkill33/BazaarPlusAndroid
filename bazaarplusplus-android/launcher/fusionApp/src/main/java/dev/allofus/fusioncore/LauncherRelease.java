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
        if (manifest.getInt("schema") != 1) throw new IOException(context.getString(R.string.bpp_manifest_unsupported));
        return manifest;
    }

    public static PackageInfo game(Context context) throws Exception {
        return game(context, GAME);
    }

    public static PackageInfo game(Context context, String packageName) throws Exception {
        return context.getPackageManager().getPackageInfo(packageName, 0);
    }

    public static void check(Context context, JSONObject release) throws Exception {
        check(context, release, GAME);
    }

    /** Official installs keep the verified version pin. A chosen package must still be arm64 IL2CPP. */
    public static void check(Context context, JSONObject release, String packageName) throws Exception {
        PackageInfo installed = game(context, packageName);
        if (GAME.equals(packageName)) {
            JSONObject game = release.getJSONObject("game");
            if (installed.getLongVersionCode() != game.getLong("versionCode") ||
                    !game.getString("versionName").equals(installed.versionName))
                throw new IOException(context.getString(R.string.bpp_game_version_unverified));
        }
        if (!LauncherGames.hasArm64Il2Cpp(installed.applicationInfo))
            throw new IOException(context.getString(R.string.bpp_game_abi_unsupported));
    }

    public static byte[] bytes(String text) { return text.getBytes(StandardCharsets.UTF_8); }
}
