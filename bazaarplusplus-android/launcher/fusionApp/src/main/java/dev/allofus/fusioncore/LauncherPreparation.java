package dev.allofus.fusioncore;

import android.content.Context;
import org.json.JSONObject;
import org.json.JSONArray;
import java.io.*;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

import dev.allofus.fusioncore.tools.Utilities;

/** One process, one preparation. A killed process always revalidates files on retry. */
public final class LauncherPreparation {
    static final AtomicBoolean BUSY = new AtomicBoolean();
    private LauncherPreparation() {}

    public static File root() { return Utilities.getExternalFusionCoreDirectory(LauncherRelease.GAME, null); }
    public static File bep() { return new File(root(), "BepInEx"); }

    public static void phase(Context context, String phase) {
        context.getSharedPreferences("launcher", 0).edit().putString("phase", phase)
                .putLong("phaseTime", System.currentTimeMillis()).commit();
    }

    static File asset(Context context, JSONObject descriptor) throws Exception {
        File output = new File(context.getCacheDir(), "launcher-inputs/" + descriptor.getString("sha256"));
        if (!LauncherFiles.valid(output, descriptor.getString("sha256"), descriptor.getLong("size"))) {
            LauncherFiles.mkdir(output.getParentFile());
            File temp = new File(output.getPath() + ".part");
            try (InputStream in = context.getAssets().open(descriptor.getString("asset"));
                 OutputStream out = new FileOutputStream(temp)) { copy(in, out); }
            verify(temp, descriptor);
            LauncherFiles.replace(temp, output);
        }
        return output;
    }

    static void verify(File file, JSONObject descriptor) throws Exception {
        if (!LauncherFiles.valid(file, descriptor.getString("sha256"), descriptor.getLong("size")))
            throw new IOException("文件完整性校验失败，请重试");
    }

    static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[65536]; int count;
        while ((count = in.read(buffer)) != -1) out.write(buffer, 0, count);
    }

    public interface Progress { void update(String text); }

    static File dependency(Context context, JSONObject descriptor, Progress progress) throws Exception {
        File output = new File(context.getCacheDir(), "launcher-downloads/" + descriptor.getString("sha256"));
        if (LauncherFiles.valid(output, descriptor.getString("sha256"), descriptor.getLong("size"))) return output;
        LauncherFiles.mkdir(output.getParentFile());
        File temp = new File(output.getPath() + ".part");
        URL url = new URL(descriptor.getString("url"));
        if (!url.getProtocol().equals("https")) throw new IOException("依赖来源必须使用 HTTPS");
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        connection.setConnectTimeout(15000); connection.setReadTimeout(30000);
        try {
            if (connection.getResponseCode() != 200 || !connection.getURL().getProtocol().equals("https"))
                throw new IOException("依赖下载失败，请检查网络后重试");
            long expected = descriptor.getLong("size"), done = 0, last = 0;
            try (InputStream in = connection.getInputStream(); OutputStream out = new FileOutputStream(temp)) {
                byte[] buffer = new byte[65536]; int count;
                while ((count = in.read(buffer)) != -1) {
                    done += count;
                    if (done > expected) throw new IOException("依赖文件长度不符");
                    out.write(buffer, 0, count);
                    if (System.currentTimeMillis() - last > 200) {
                        progress.update("下载匹配依赖 " + done / 1024 + " / " + expected / 1024 + " KB");
                        last = System.currentTimeMillis();
                    }
                }
            }
            verify(temp, descriptor);
            LauncherFiles.replace(temp, output);
            return output;
        } finally { connection.disconnect(); if (temp.exists()) temp.delete(); }
    }

    /** Stage the whole archive; individually atomic file installs are reverified before every launch. */
    static void unpack(File archive, File destination, boolean preserveConfig) throws Exception {
        File stage = new File(destination.getParentFile(), "." + destination.getName() + "-stage");
        LauncherFiles.removeOwned(stage);
        LauncherFiles.extract(archive, stage);
        installTree(stage, destination, stage, preserveConfig);
        LauncherFiles.removeOwned(stage);
    }

    static void installTree(File source, File destination, File stageRoot, boolean preserveConfig) throws Exception {
        LauncherFiles.mkdir(destination);
        File[] children = source.listFiles();
        if (children == null) throw new IOException("解压目录不可读");
        for (File child : children) {
            File target = new File(destination, child.getName());
            if (child.isDirectory()) installTree(child, target, stageRoot, preserveConfig);
            else {
                String relative = stageRoot.toPath().relativize(child.toPath()).toString();
                if (preserveConfig && relative.startsWith("config/") && target.isFile()) continue;
                if (target.isFile() && LauncherFiles.sha256(child).equals(LauncherFiles.sha256(target))) continue;
                LauncherFiles.replace(child, target);
            }
        }
    }

    public static void prepare(Context context, File runtime, String unity, Progress progress) throws Exception {
        JSONObject release = LauncherRelease.manifest(context);
        LauncherRelease.check(context, release);
        if (!release.getJSONObject("game").getString("unity").equals(unity))
            throw new IOException("游戏 Unity 版本与发行清单不符");
        step(context, progress, "准备匹配依赖");
        JSONArray dependencies = release.getJSONArray("dependencies");
        for (int i = 0; i < dependencies.length(); i++) {
            JSONObject dependency = dependencies.getJSONObject(i);
            File input = dependency(context, dependency, progress);
            String name = dependency.getString("name");
            File target = new File(runtime, name);
            if (name.endsWith(".zip")) target = new File(bep(), "unity-libs/" + name);
            if (!LauncherFiles.valid(target, dependency.getString("sha256"), dependency.getLong("size"))) {
                LauncherFiles.mkdir(target.getParentFile());
                File temp = new File(target.getPath() + ".part");
                try (InputStream in = new FileInputStream(input); OutputStream out = new FileOutputStream(temp)) { copy(in, out); }
                verify(temp, dependency); LauncherFiles.replace(temp, target);
            }
        }
        step(context, progress, "准备运行框架");
        unpack(asset(context, release.getJSONObject("framework")), bep(), true);
        unpack(asset(context, release.getJSONObject("runtime")), new File(runtime.getParentFile(), "dotnet"), false);
        if (!new File(bep(), "core/BepInEx.Unity.IL2CPP.dll").isFile() ||
                !new File(runtime.getParentFile(), "dotnet/System.Private.CoreLib.dll").isFile())
            throw new IOException("运行框架不完整");
        // BepInEx consumes the already verified ZIP under unity-libs. Never resolve versions online.
        File config = new File(bep(), "config/BepInEx.cfg");
        String text = LauncherRelease.read(new FileInputStream(config));
        text = text.replaceAll("(?m)^UnityBaseLibrariesSource\\s*=.*$",
                "UnityBaseLibrariesSource = " + release.getString("baseLibrariesUrl"));
        LauncherFiles.write(config, LauncherRelease.bytes(text));
        step(context, progress, "安装插件");
        JSONObject plugin = release.getJSONObject("plugin");
        File input = asset(context, plugin), target = new File(bep(), "plugins/BazaarPlusPlus.Android.dll");
        if (!LauncherFiles.valid(target, plugin.getString("sha256"), plugin.getLong("size"))) {
            LauncherFiles.mkdir(target.getParentFile());
            File temp = new File(target.getPath() + ".part");
            try (InputStream in = new FileInputStream(input); OutputStream out = new FileOutputStream(temp)) { copy(in, out); }
            verify(temp, plugin);
            if (target.isFile()) {
                File backup = new File(context.getFilesDir(), "previous-plugin.dll");
                try (InputStream in = new FileInputStream(target); OutputStream out = new FileOutputStream(backup)) { copy(in, out); }
            }
            LauncherFiles.replace(temp, target);
        }
        verify(target, plugin);
        JSONObject session = new JSONObject().put("session", UUID.randomUUID().toString())
                .put("pluginVersion", plugin.getString("version")).put("sha256", plugin.getString("sha256"))
                .put("startedAt", System.currentTimeMillis());
        LauncherFiles.write(new File(bep(), "bpp-launch-session.json"), LauncherRelease.bytes(session.toString()));
        context.getSharedPreferences("launcher", 0).edit().putString("session", session.toString())
                .putString("preparedRelease", release.getString("releaseId")).commit();
        step(context, progress, "文件已准备，等待运行时加载");
    }

    static void step(Context context, Progress progress, String text) { phase(context, text); progress.update(text); }

    public static String loadStatus(Context context) {
        try {
            JSONObject session = new JSONObject(context.getSharedPreferences("launcher", 0).getString("session", "{}"));
            JSONObject ack = new JSONObject(LauncherRelease.read(new FileInputStream(new File(bep(), "bpp-launch-loaded.json"))));
            JSONObject plugin = LauncherRelease.manifest(context).getJSONObject("plugin");
            if (ack.getString("session").equals(session.getString("session")) &&
                    ack.getString("sha256").equals(plugin.getString("sha256")) &&
                    ack.getString("pluginVersion").equals(plugin.getString("version")) &&
                    ack.getBoolean("loaded") && ack.getLong("loadedAt") >= session.getLong("startedAt") &&
                    LauncherFiles.valid(new File(bep(), "plugins/BazaarPlusPlus.Android.dll"), plugin.getString("sha256"), plugin.getLong("size")))
                return "本次插件已加载（游戏登录及功能验收另行确认）";
        } catch (Exception ignored) {}
        return "已启动，加载状态待确认；首次代理生成可能需要数分钟";
    }
}
