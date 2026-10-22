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
    static final AtomicBoolean CANCEL = new AtomicBoolean();
    static volatile String activeSession;
    private LauncherPreparation() {}

    public static File root() { return Utilities.getExternalFusionCoreDirectory(LauncherRelease.GAME, null); }
    public static File bep() { return new File(root(), "BepInEx"); }

    public static void phase(Context context, String phase) {
        android.content.SharedPreferences preferences = context.getSharedPreferences("launcher", 0);
        android.content.SharedPreferences.Editor edit = preferences.edit().putString("phase", phase)
                .putLong("phaseTime", System.currentTimeMillis());
        if (!phase.startsWith("初始化失败")) edit.putString("lastPreparationStep", phase);
        // Keep a bounded stage trace. Raw error messages never enter the exported trace.
        String safe = phase.startsWith("初始化失败") ? "初始化失败" : phase.startsWith("下载匹配依赖") ? "下载匹配依赖" : phase;
        try {
            JSONArray previous = new JSONArray(preferences.getString("stageTrace", "[]"));
            if (previous.length() == 0 || !previous.getJSONObject(previous.length() - 1).getString("stage").equals(safe)) {
                JSONArray trace = new JSONArray();
                for (int i = Math.max(0, previous.length() - 15); i < previous.length(); i++) trace.put(previous.get(i));
                trace.put(new JSONObject().put("stage", safe).put("at", System.currentTimeMillis()));
                edit.putString("stageTrace", trace.toString());
            }
        } catch (Exception ignored) {}
        edit.commit();
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
        JSONArray fallback = descriptor.optJSONArray("fallbackUrls");
        int count = 1 + (fallback == null ? 0 : fallback.length());
        Exception last = null;
        for (int i = 0; i < count; i++) {
            checkCancelled();
            try {
                return downloadDependency(context, descriptor, i == 0 ? descriptor.getString("url") : fallback.getString(i - 1), progress);
            } catch (IOException error) { last = error; }
        }
        checkCancelled();
        throw new IOException("依赖下载或校验失败，请切换可访问 GitHub 的网络后重试（" + descriptor.getString("name") + "）", last);
    }

    static File downloadDependency(Context context, JSONObject descriptor, String sourceUrl, Progress progress) throws Exception {
        File output = new File(context.getCacheDir(), "launcher-downloads/" + descriptor.getString("sha256"));
        if (LauncherFiles.valid(output, descriptor.getString("sha256"), descriptor.getLong("size"))) return output;
        LauncherFiles.mkdir(output.getParentFile());
        File temp = new File(output.getPath() + ".part");
        URL url = new URL(sourceUrl);
        if (!url.getProtocol().equals("https")) throw new IOException("依赖来源必须使用 HTTPS");
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        connection.setConnectTimeout(15000); connection.setReadTimeout(30000);
        connection.setRequestProperty("Accept", "application/octet-stream");
        connection.setRequestProperty("User-Agent", "BazaarPlusPlusAndroid/" + BuildConfig.VERSION_NAME);
        try {
            if (connection.getResponseCode() != 200 || !connection.getURL().getProtocol().equals("https"))
                throw new IOException("依赖下载失败，请检查网络后重试");
            long expected = descriptor.getLong("size"), done = 0, last = 0;
            try (InputStream in = connection.getInputStream(); OutputStream out = new FileOutputStream(temp)) {
                byte[] buffer = new byte[65536]; int count;
                while ((count = in.read(buffer)) != -1) {
                    checkCancelled();
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
        checkCancelled();
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
            String previousHash = context.getSharedPreferences("launcher", 0).getString("lastGoodPluginSha256", "");
            if (target.isFile() && LauncherFiles.sha256(target).equals(previousHash)) {
                File backup = new File(context.getFilesDir(), "previous-plugin.dll");
                File backupTemp = new File(backup.getPath() + ".part");
                try (InputStream in = new FileInputStream(target); OutputStream out = new FileOutputStream(backupTemp)) { copy(in, out); }
                if (!LauncherFiles.valid(backupTemp, previousHash, target.length())) throw new IOException("保留旧插件失败");
                LauncherFiles.replace(backupTemp, backup);
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
        activeSession = session.getString("session");
        step(context, progress, "文件已准备，等待运行时加载");
    }

    public static void checkCancelled() throws IOException {
        if (CANCEL.get()) throw new IOException("已取消准备，可重新初始化；已校验的缓存会保留");
    }

    static void step(Context context, Progress progress, String text) throws IOException {
        checkCancelled(); phase(context, text); progress.update(text);
    }

    /** Preserve a verified prior launch before starting a new session or changing the bundled DLL. */
    public static void rememberLastGood(Context context) {
        try {
            JSONObject request = new JSONObject(context.getSharedPreferences("launcher", 0).getString("session", "{}"));
            JSONObject ack = new JSONObject(LauncherRelease.read(new FileInputStream(new File(bep(), "bpp-launch-loaded.json"))));
            String hash = request.getString("sha256");
            if (LauncherSession.matches(request.getString("session"), ack.getString("session"),
                    request.getString("pluginVersion"), ack.getString("pluginVersion"), hash, ack.getString("sha256"),
                    request.getLong("startedAt"), ack.getLong("loadedAt"), ack.getBoolean("loaded"),
                    LauncherFiles.valid(new File(bep(), "plugins/BazaarPlusPlus.Android.dll"), hash, -1)))
                context.getSharedPreferences("launcher", 0).edit().putString("lastGoodPluginSha256", hash).commit();
        } catch (Exception ignored) {}
    }

    public static String loadStatus(Context context) {
        try {
            JSONObject session = new JSONObject(context.getSharedPreferences("launcher", 0).getString("session", "{}"));
            if (!session.getString("session").equals(activeSession))
                return "上次启动记录已保留；下次启动将重新核对加载状态";
            JSONObject ack = new JSONObject(LauncherRelease.read(new FileInputStream(new File(bep(), "bpp-launch-loaded.json"))));
            JSONObject plugin = LauncherRelease.manifest(context).getJSONObject("plugin");
            if (LauncherSession.matches(session.getString("session"), ack.getString("session"),
                    plugin.getString("version"), ack.getString("pluginVersion"),
                    plugin.getString("sha256"), ack.getString("sha256"),
                    session.getLong("startedAt"), ack.getLong("loadedAt"), ack.getBoolean("loaded"),
                    LauncherFiles.valid(new File(bep(), "plugins/BazaarPlusPlus.Android.dll"), plugin.getString("sha256"), plugin.getLong("size")))) {
                context.getSharedPreferences("launcher", 0).edit().putString("lastGoodPluginSha256", plugin.getString("sha256")).apply();
                return "本次插件已加载（游戏登录及功能验收另行确认）";
            }
        } catch (Exception ignored) {}
        return "已启动，加载状态待确认；首次代理生成可能需要数分钟";
    }
}
