package dev.allofus.fusioncore;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.StatFs;
import android.provider.Settings;
import android.graphics.Color;
import android.view.View;
import android.widget.*;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import org.json.JSONObject;
import java.io.*;
import java.util.concurrent.Executors;
import java.util.concurrent.ExecutorService;
import dev.allofus.fusioncore.tools.Utilities;

/** Product entry point. Upstream generic selectors and debug settings are not launch entries. */
public final class LauncherActivity extends AppCompatActivity {
    private TextView status;
    private Button primary;
    private final Handler handler = new Handler();
    private final ExecutorService checks = Executors.newSingleThreadExecutor();
    private boolean visible;
    private boolean supported;
    private boolean installed;
    private final Runnable refreshTask = new Runnable() {
        @Override public void run() { if (visible) { refresh(); handler.postDelayed(this, 3000); } }
    };

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        ScrollView scroll = new ScrollView(this);
        LinearLayout body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setBackgroundColor(Color.rgb(20, 24, 31));
        scroll.addView(body);
        Utilities.applyWindowInsets(body, Math.round(24 * getResources().getDisplayMetrics().density));
        ImageView icon = new ImageView(this);
        icon.setImageResource(R.drawable.bpp_icon);
        icon.setContentDescription("集市增强图标");
        body.addView(icon, new LinearLayout.LayoutParams(180, 180));
        TextView title = new TextView(this);
        title.setText("集市增强\nBazaarPlusPlus"); title.setTextSize(28); title.setTextColor(Color.WHITE);
        body.addView(title);
        status = new TextView(this); status.setTextSize(17); status.setTextColor(Color.LTGRAY);
        status.setPadding(0, 24, 0, 24); body.addView(status);
        primary = button(body, "检查环境", this::launchOrExplain);
        button(body, "重新检测 / 检查环境", this::refresh);
        button(body, "修复运行环境", () -> new AlertDialog.Builder(this).setTitle("修复运行环境")
                .setMessage("重新校验并准备框架、依赖与内置插件。保留游戏登录、持久数据和用户配置。请先退出正在运行的游戏。")
                .setPositiveButton("检查并修复", (dialog, which) -> launchOrExplain())
                .setNegativeButton("取消", null).show());
        button(body, "查看授权用途", () -> new AlertDialog.Builder(this).setTitle("文件访问授权")
                .setMessage("运行框架、插件和游戏持久数据保存在启动器的独立共享目录。需要所有文件访问授权才能准备环境；不会迁移或覆盖原 FusionCore 环境。拒绝后可随时返回重试。")
                .setPositiveButton("知道了", null).show());
        button(body, "诊断与许可", this::diagnostics);
        button(body, "导出诊断", () -> {
            Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/json")
                    .addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE, "bazaarplusplus-diagnostics.json");
            startActivityForResult(intent, 9);
        });
        setContentView(scroll);
    }

    private Button button(LinearLayout body, String text, Runnable action) {
        Button button = new Button(this); button.setText(text);
        button.setOnClickListener(v -> action.run()); body.addView(button); return button;
    }

    private boolean authorized() {
        if (android.os.Build.VERSION.SDK_INT >= 30) return Environment.isExternalStorageManager();
        return checkSelfPermission(android.Manifest.permission.WRITE_EXTERNAL_STORAGE) == android.content.pm.PackageManager.PERMISSION_GRANTED;
    }

    private void requestAccess() {
        if (android.os.Build.VERSION.SDK_INT < 30) {
            requestPermissions(new String[]{android.Manifest.permission.WRITE_EXTERNAL_STORAGE}, 1); return;
        }
        try { startActivity(new Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION,
                Uri.parse("package:" + getPackageName()))); }
        catch (Exception error) { startActivity(new Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION)); }
    }

    private void refresh() {
        checks.execute(() -> {
            String message;
            boolean found = false, compatible = false;
            try {
                JSONObject release = LauncherRelease.manifest(this);
                String version = LauncherRelease.game(this).versionName; found = true;
                LauncherRelease.check(this, release); compatible = true;
                message = "游戏已安装：" + version + "\n兼容性：当前组合待专用 APK 真机验收\n内置插件：" +
                        release.getJSONObject("plugin").getString("version");
                if (!authorized()) message += "\n需要文件访问授权";
                else {
                    String phase = getSharedPreferences("launcher", 0).getString("phase", "尚未初始化");
                    // Arbitrary exception messages stay local; diagnostics export only a classified status.
                    message += "\n" + phase;
                    if (getSharedPreferences("launcher", 0).contains("session")) message += "\n" + LauncherPreparation.loadStatus(this);
                }
            } catch (android.content.pm.PackageManager.NameNotFoundException error) { message = "尚未安装正版 The Bazaar 游戏"; }
            catch (Exception error) { message = "兼容性待验证：" + error.getMessage(); }
            final String finalMessage = message;
            final boolean finalFound = found, finalCompatible = compatible;
            runOnUiThread(() -> {
                if (isDestroyed()) return;
                installed = finalFound; supported = finalCompatible;
                status.setText(finalMessage);
                boolean busy = LauncherPreparation.BUSY.get();
                primary.setEnabled(!busy);
                primary.setText(busy ? "准备中或游戏仍在运行" : !installed ? "查看安装说明" : !supported ? "检查兼容性" :
                        !authorized() ? "前往授权" : getSharedPreferences("launcher", 0).contains("session") ? "启动游戏" : "初始化");
            });
        });
    }

    private void launchOrExplain() {
        if (LauncherPreparation.BUSY.get()) { Toast.makeText(this, "请先退出当前游戏，再重新打开启动器", Toast.LENGTH_LONG).show(); return; }
        if (!installed) {
            new AlertDialog.Builder(this).setMessage("请通过游戏官方渠道安装正版 The Bazaar，再返回重新检测。首次登录及游戏资源下载由游戏完成。")
                    .setPositiveButton("知道了", null).show(); return;
        }
        if (!supported) { refresh(); return; }
        if (!authorized()) { requestAccess(); return; }
        try {
            if (new StatFs(getFilesDir().getPath()).getAvailableBytes() < 1024L * 1024 * 1024 ||
                    new StatFs(Environment.getExternalStorageDirectory().getPath()).getAvailableBytes() < 1024L * 1024 * 1024)
                throw new IOException("至少需要 1 GB 可用空间；空间释放后重试");
            Intent intent = new Intent(this, BootstrapActivity.class);
            intent.putExtra(BootstrapActivity.EXTRA_TARGET_PACKAGE, LauncherRelease.GAME);
            startActivity(intent);
        } catch (Exception error) { status.setText(error.getMessage()); }
    }

    private JSONObject report() throws Exception {
        JSONObject release = LauncherRelease.manifest(this);
        JSONObject report = new JSONObject().put("schema", 1).put("launcherVersion", BuildConfig.VERSION_NAME)
                .put("releaseId", release.getString("releaseId")).put("androidApi", android.os.Build.VERSION.SDK_INT)
                .put("abi", "arm64-v8a").put("storageAuthorized", authorized())
                .put("pluginVersion", release.getJSONObject("plugin").getString("version"))
                .put("expectedPluginSha256", release.getJSONObject("plugin").getString("sha256"));
        try { report.put("gameVersion", LauncherRelease.game(this).versionName); } catch (Exception ignored) {}
        String phase = getSharedPreferences("launcher", 0).getString("phase", "尚未初始化");
        report.put("phase", phase.startsWith("初始化失败") ? "初始化失败（本地查看原因）" : phase);
        if (authorized()) report.put("loadStatus", LauncherPreparation.loadStatus(this));
        return report;
    }

    private void diagnostics() {
        checks.execute(() -> {
            String details;
            try { details = report().toString(2) + "\n\n" + LauncherRelease.read(getAssets().open("launcher/NOTICE.txt")); }
            catch (Exception error) { details = "诊断读取失败"; }
            final String text = details;
            runOnUiThread(() -> new AlertDialog.Builder(this).setTitle("诊断与许可").setMessage(text)
                    .setPositiveButton("关闭", null).show());
        });
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request == 9 && result == RESULT_OK && data != null && data.getData() != null) {
            final Uri uri = data.getData();
            checks.execute(() -> {
                String message;
                try (OutputStream out = getContentResolver().openOutputStream(uri)) {
                    if (out == null) throw new IOException();
                    out.write(LauncherRelease.bytes(report().toString(2))); message = "诊断已导出，不包含原始游戏日志或账号数据";
                } catch (Exception error) { message = "诊断导出失败，请重试"; }
                final String text = message;
                runOnUiThread(() -> Toast.makeText(this, text, Toast.LENGTH_LONG).show());
            });
        }
    }

    @Override protected void onResume() { super.onResume(); visible = true; handler.post(refreshTask); }
    @Override protected void onPause() { visible = false; handler.removeCallbacks(refreshTask); super.onPause(); }
    @Override protected void onDestroy() { handler.removeCallbacks(refreshTask); checks.shutdown(); super.onDestroy(); }
}
