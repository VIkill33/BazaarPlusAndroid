package dev.allofus.fusioncore;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.StatFs;
import android.provider.Settings;
import android.content.res.ColorStateList;
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
    private TextView stateTitle;
    private TextView stateHint;
    private ImageView stateIcon;
    private Button refreshButton;
    private boolean refreshing;
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
        // Some home screens create a new entry above the running game instead of
        // bringing its task forward. Reveal the existing top without relaunching Unity.
        Intent entry = getIntent();
        if (!isTaskRoot() && entry != null && Intent.ACTION_MAIN.equals(entry.getAction())
                && entry.hasCategory(Intent.CATEGORY_LAUNCHER)) {
            finish();
            return;
        }
        setContentView(R.layout.activity_launcher);
        ScrollView scroll = findViewById(R.id.launcher_scroll);
        Utilities.applyWindowInsets(scroll, 0);
        View body = findViewById(R.id.launcher_body);
        scroll.addOnLayoutChangeListener((v, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom) -> {
            int width = Math.min(right - left - scroll.getPaddingLeft() - scroll.getPaddingRight(),
                    Math.round(520 * getResources().getDisplayMetrics().density));
            if (width > 0 && body.getLayoutParams().width != width) {
                android.view.ViewGroup.LayoutParams params = body.getLayoutParams();
                params.width = width;
                body.setLayoutParams(params);
            }
        });
        status = findViewById(R.id.launcher_status);
        stateTitle = findViewById(R.id.launcher_state_title);
        stateHint = findViewById(R.id.launcher_state_hint);
        stateIcon = findViewById(R.id.launcher_state_icon);
        primary = findViewById(R.id.launcher_primary);
        primary.setOnClickListener(v -> launchOrExplain());
        refreshButton = findViewById(R.id.launcher_refresh);
        refreshButton.setOnClickListener(v -> {
            stateTitle.setText("正在检查环境");
            stateHint.setText("请稍候，即将完成启动前检查");
            primary.setEnabled(false);
            refresh();
        });
        findViewById(R.id.launcher_more).setOnClickListener(v -> help());
        ((TextView) findViewById(R.id.launcher_version)).setText("启动器 " + BuildConfig.VERSION_NAME);
    }

    private void help() {
        new AlertDialog.Builder(this).setTitle("帮助与更多")
                .setItems(new String[]{"游戏安装说明", "文件授权说明", "修复运行环境", "关于与开源许可", "退出启动器"},
                        (dialog, which) -> {
                            switch (which) {
                                case 0 -> new AlertDialog.Builder(this).setTitle("游戏安装说明")
                                        .setMessage("请通过官方渠道安装正版大巴扎（The Bazaar），再返回重新检查。首次登录及游戏资源下载由游戏完成。")
                                        .setPositiveButton("知道了", null).show();
                                case 1 -> new AlertDialog.Builder(this).setTitle("文件访问授权")
                                        .setMessage("运行框架、插件和游戏持久数据保存在启动器的独立共享目录。需要所有文件访问授权才能准备环境；不会迁移或覆盖原 FusionCore 环境。拒绝后可随时返回重试。")
                                        .setPositiveButton("知道了", null).show();
                                case 2 -> new AlertDialog.Builder(this).setTitle("修复运行环境")
                                        .setMessage("重新校验并准备框架、依赖与内置插件。保留游戏登录、持久数据和用户配置。请先退出正在运行的游戏。")
                                        .setPositiveButton("检查并修复", (d, w) -> launch(true))
                                        .setNegativeButton("取消", null).show();
                                case 3 -> licenses();
                                case 4 -> new AlertDialog.Builder(this).setTitle("退出启动器")
                                        .setMessage("结束本启动器与其中运行的游戏。下次打开将重新检查环境，请先确认游戏已完成保存。")
                                        .setPositiveButton("退出", (d, w) -> {
                                            finishAndRemoveTask(); android.os.Process.killProcess(android.os.Process.myPid());
                                        }).setNegativeButton("取消", null).show();
                            }
                        }).setNegativeButton("关闭", null).show();
    }

    private void licenses() {
        checks.execute(() -> {
            String details;
            try {
                details = "BazaarPlusPlus " + BuildConfig.VERSION_NAME + "\n独立社区工具，非游戏官方出品。\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/NOTICE.txt")) + "\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/BPP-LICENSE.txt")) + "\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/FusionCore-LICENSE.txt"));
            } catch (Exception error) { details = "许可读取失败，请关闭后重试"; }
            final String text = details;
            runOnUiThread(() -> {
                if (!isFinishing() && !isDestroyed()) new AlertDialog.Builder(this)
                        .setTitle("关于与开源许可").setMessage(text).setPositiveButton("关闭", null).show();
            });
        });
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
        if (refreshing || isFinishing() || isDestroyed()) return;
        refreshing = true;
        refreshButton.setEnabled(false);
        checks.execute(() -> {
            String message;
            boolean found = false, compatible = false;
            try {
                JSONObject release = LauncherRelease.manifest(this);
                LauncherRelease.game(this); found = true;
                LauncherRelease.check(this, release); compatible = true;
                message = "大巴扎已安装 · 内置插件 " +
                        release.getJSONObject("plugin").getString("version");
                if (!authorized()) message += "\n需要文件访问授权";
                else {
                    String phase = getSharedPreferences("launcher", 0).getString("phase", "尚未初始化");
                    if (LauncherPreparation.BUSY.get()) {
                        message += "\n" + phase;
                        if (getSharedPreferences("launcher", 0).contains("session"))
                            message += "\n" + LauncherPreparation.loadStatus(this);
                    } else if (phase.startsWith("初始化失败")) message += "\n" + phase;
                }
            } catch (android.content.pm.PackageManager.NameNotFoundException error) { message = "尚未安装正版大巴扎（The Bazaar）"; }
            catch (Exception error) { message = "兼容性待验证：" + error.getMessage(); }
            final String finalMessage = message;
            final boolean finalFound = found, finalCompatible = compatible;
            runOnUiThread(() -> {
                refreshing = false;
                if (isFinishing() || isDestroyed()) return;
                refreshButton.setEnabled(true);
                installed = finalFound; supported = finalCompatible;
                status.setText(finalMessage);
                boolean busy = LauncherPreparation.BUSY.get();
                boolean access = authorized();
                boolean failed = getSharedPreferences("launcher", 0).getString("phase", "").startsWith("初始化失败");
                boolean ready = installed && supported && access && !busy && !failed;
                stateTitle.setText(busy ? "准备中或游戏运行中" : !installed ? "请先安装游戏" : !supported ? "兼容性待确认" :
                        !access ? "需要文件授权" : failed ? "初始化需要重试" : "可以启动");
                stateHint.setText(busy ? "请等待准备完成，或返回当前游戏" : ready ? "启动时自动校验运行环境" :
                        !installed ? "安装正版大巴扎后返回检查" : !supported ? "当前版本尚未通过兼容检查" :
                        !access ? "授权后即可准备运行环境" : "可重试，或在帮助中修复环境");
                stateIcon.setImageResource(ready ? R.drawable.bpp_check : R.drawable.bpp_help);
                stateIcon.setImageTintList(ColorStateList.valueOf(getColor(ready ? R.color.bpp_success : R.color.bpp_accent)));
                primary.setEnabled(!busy);
                primary.setText(busy ? "准备中或游戏仍在运行" : !installed ? "查看安装说明" : !supported ? "检查兼容性" :
                        !authorized() ? "前往授权" : getSharedPreferences("launcher", 0).getString("phase", "").startsWith("初始化失败") ? "重试初始化" :
                        "启动游戏");
            });
        });
    }

    private void launchOrExplain() {
        launch(false);
    }

    private void launch(boolean repair) {
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
            intent.putExtra("bpp_repair", repair);
            startActivity(intent);
        } catch (Exception error) {
            stateTitle.setText("暂时无法启动");
            stateHint.setText("请按下方提示处理后重试");
            stateIcon.setImageResource(R.drawable.bpp_help);
            stateIcon.setImageTintList(ColorStateList.valueOf(getColor(R.color.bpp_accent)));
            status.setText(error.getMessage());
            new AlertDialog.Builder(this).setTitle("暂时无法启动")
                    .setMessage(error.getMessage()).setPositiveButton("知道了", null).show();
        }
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
        report.put("lastPreparationStep", getSharedPreferences("launcher", 0).getString("lastPreparationStep", "尚未初始化"));
        report.put("stageTrace", new org.json.JSONArray(getSharedPreferences("launcher", 0).getString("stageTrace", "[]")));
        File previous = new File(getFilesDir(), "previous-plugin.dll");
        if (previous.isFile()) report.put("previousPluginSha256", LauncherFiles.sha256(previous));
        if (authorized() && getSharedPreferences("launcher", 0).contains("session"))
            report.put("loadStatus", LauncherPreparation.loadStatus(this));
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

    @Override protected void onResume() {
        super.onResume();
        if (isFinishing()) return;
        visible = true;
        handler.post(refreshTask);
    }
    @Override protected void onPause() { visible = false; handler.removeCallbacks(refreshTask); super.onPause(); }
    @Override protected void onDestroy() { handler.removeCallbacks(refreshTask); checks.shutdown(); super.onDestroy(); }
}
