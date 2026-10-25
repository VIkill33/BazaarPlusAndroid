package dev.allofus.fusioncore;

import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.net.Uri;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.TextPaint;
import android.text.method.LinkMovementMethod;
import android.text.style.ClickableSpan;
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

/** Product entry point. The home button launches the official game unless settings chose another. */
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
    private String checkedPackage;
    private final Runnable refreshTask = new Runnable() {
        @Override public void run() { if (visible) { refresh(); handler.postDelayed(this, 3000); } }
    };

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        dev.allofus.fusioncore.tools.JniBridge.attach(this);
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
        findViewById(R.id.launcher_donate).setOnClickListener(v -> donate());
        findViewById(R.id.launcher_history).setOnClickListener(v -> startActivity(new Intent(this, LauncherHistoryActivity.class)
                .putExtra(LauncherHistoryActivity.TARGET, LauncherGames.selected(this))));
        findViewById(R.id.launcher_github).setOnClickListener(v -> {
            try {
                startActivity(new Intent(Intent.ACTION_VIEW,
                        Uri.parse("https://github.com/VIkill33/BazaarPlusAndroid")));
            } catch (ActivityNotFoundException error) {
                Toast.makeText(this, "无法打开链接", Toast.LENGTH_SHORT).show();
            }
        });
        findViewById(R.id.launcher_settings).setOnClickListener(v -> startActivity(new Intent(this, LauncherSettingsActivity.class)));
        findViewById(R.id.launcher_help).setOnClickListener(v -> startActivity(new Intent(this, LauncherHelpActivity.class)));
        findViewById(R.id.launcher_more).setOnClickListener(v -> help());
        ((TextView) findViewById(R.id.launcher_version)).setText("启动器 " + BuildConfig.VERSION_NAME);
        showCredit();
    }

    private void showCredit() {
        TextView credit = findViewById(R.id.launcher_credit);
        String label = "BazaarPlusPlus";
        String sentence = "本插件基于原版 " + label + " 开发";
        SpannableString text = new SpannableString(sentence);
        int start = sentence.indexOf(label);
        text.setSpan(new ClickableSpan() {
            @Override public void onClick(View widget) {
                try {
                    startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://bazaarplusplus.com")));
                } catch (ActivityNotFoundException error) {
                    Toast.makeText(LauncherActivity.this, "无法打开链接", Toast.LENGTH_SHORT).show();
                }
            }
            @Override public void updateDrawState(TextPaint paint) {
                super.updateDrawState(paint);
                paint.setColor(getColor(R.color.bpp_accent));
                paint.setUnderlineText(true);
            }
        }, start, start + label.length(), Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        credit.setText(text);
        credit.setMovementMethod(LinkMovementMethod.getInstance());
        credit.setHighlightColor(0x55FFBD67);
    }

    private void donate() {
        View view = getLayoutInflater().inflate(R.layout.dialog_donate, null);
        AlertDialog dialog = new AlertDialog.Builder(this).setView(view).setPositiveButton("关闭", null).create();
        view.setOnClickListener(v -> dialog.dismiss());
        dialog.show();
        if (dialog.getWindow() != null) {
            int width = Math.min(getResources().getDisplayMetrics().widthPixels,
                    Math.round(520 * getResources().getDisplayMetrics().density));
            dialog.getWindow().setLayout(width, android.view.ViewGroup.LayoutParams.WRAP_CONTENT);
        }
    }

    private void help() {
        new AlertDialog.Builder(this).setTitle("帮助与更多")
                .setItems(new String[]{"游戏安装说明", "文件授权说明", "修复运行环境", "导出诊断日志", "关于与开源许可", "退出启动器"},
                        (dialog, which) -> {
                            switch (which) {
                                case 0 -> new AlertDialog.Builder(this).setTitle("游戏安装说明")
                                        .setMessage("请通过官方渠道安装正版大巴扎（The Bazaar），再返回重新检查。也可在「设置」中选择本机其他符合要求的游戏。首次登录及游戏资源下载由游戏完成。")
                                        .setPositiveButton("知道了", null).show();
                                case 1 -> new AlertDialog.Builder(this).setTitle("文件访问授权")
                                        .setMessage("运行框架、插件和游戏持久数据保存在启动器的独立共享目录。需要所有文件访问授权才能准备环境；不会迁移或覆盖原 FusionCore 环境。拒绝后可随时返回重试。")
                                        .setPositiveButton("知道了", null).show();
                                case 2 -> new AlertDialog.Builder(this).setTitle("修复运行环境")
                                        .setMessage("重新校验并准备框架、依赖与内置插件。保留游戏登录、持久数据和用户配置。请先退出正在运行的游戏。")
                                        .setPositiveButton("检查并修复", (d, w) -> launch(true))
                                        .setNegativeButton("取消", null).show();
                                case 3 -> exportLog();
                                case 4 -> licenses();
                                case 5 -> new AlertDialog.Builder(this).setTitle("退出启动器")
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
                details = getString(R.string.app_name) + " " + BuildConfig.VERSION_NAME + "\n独立社区工具，非游戏官方出品。\n\n" +
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
            LauncherPreparation.noteInterruptedLoader(this);
            String message;
            boolean found = false, compatible = false;
            String target = LauncherGames.selected(this);
            boolean official = LauncherRelease.GAME.equals(target);
            String saved = getSharedPreferences("launcher", 0).getString(LauncherGames.PREFERENCE, "");
            boolean reverted = saved != null && !saved.isEmpty() && !saved.equals(target);
            String label = target;
            try {
                label = LauncherGames.label(getPackageManager(), getPackageManager().getApplicationInfo(target, 0));
            } catch (Exception ignored) {}
            try {
                JSONObject release = LauncherRelease.manifest(this);
                LauncherRelease.game(this, target); found = true;
                LauncherRelease.check(this, release, target); compatible = true;
                message = (official ? "大巴扎已安装 · 内置插件 " : "将启动「" + label + "」· 内置插件 ") +
                        release.getJSONObject("plugin").getString("version");
                if (reverted) message += "\n先前选择的游戏已不可用，已改回正版";
                if (!authorized()) message += "\n需要文件访问授权";
                else {
                    String phase = getSharedPreferences("launcher", 0).getString("phase", "尚未初始化");
                    if (LauncherPreparation.BUSY.get()) {
                        message += "\n" + phase;
                        if (getSharedPreferences("launcher", 0).contains("session"))
                            message += "\n" + LauncherPreparation.loadStatus(this);
                    } else if (phase.startsWith("初始化失败")) message += "\n" + phase;
                }
            } catch (android.content.pm.PackageManager.NameNotFoundException error) {
                message = official ? "尚未安装正版大巴扎（The Bazaar）" : "所选游戏已不在本机";
                if (reverted) message += "\n先前选择的游戏已不可用，已改回正版";
            }
            catch (Exception error) {
                message = "兼容性待验证：" + error.getMessage();
                LauncherLog.note(getFilesDir(), message);
            }
            final String finalMessage = message;
            final String finalTarget = target;
            final String finalLabel = label;
            final boolean finalOfficial = official;
            final boolean finalFound = found, finalCompatible = compatible;
            runOnUiThread(() -> {
                refreshing = false;
                if (isFinishing() || isDestroyed()) return;
                refreshButton.setEnabled(true);
                checkedPackage = finalTarget;
                installed = finalFound; supported = finalCompatible;
                status.setText(finalMessage);
                boolean busy = LauncherPreparation.BUSY.get();
                boolean access = authorized();
                boolean failed = getSharedPreferences("launcher", 0).getString("phase", "").startsWith("初始化失败");
                boolean ready = installed && supported && access && !busy && !failed;
                stateTitle.setText(busy ? "准备中或游戏运行中" : !installed ? "请先安装游戏" : !supported ? "兼容性待确认" :
                        !access ? "需要文件授权" : failed ? "初始化需要重试" : "可以启动");
                stateHint.setText(busy ? "请等待准备完成，或返回当前游戏" : ready
                        ? (finalOfficial ? "启动时自动校验运行环境" : "将启动「" + finalLabel + "」，并自动校验运行环境")
                        : !installed ? (finalOfficial ? "安装正版大巴扎后返回检查，或在设置中选择其他游戏" : "请在设置中重新选择游戏")
                        : !supported ? "当前版本尚未通过兼容检查" :
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
        if (LauncherPreparation.BUSY.get()) {
            LauncherLog.note(getFilesDir(), "启动被拒绝：准备或游戏仍在进行");
            Toast.makeText(this, "请先退出当前游戏，再重新打开启动器", Toast.LENGTH_LONG).show(); return;
        }
        String target = LauncherGames.selected(this);
        if (checkedPackage == null || !target.equals(checkedPackage)) {
            refresh();
            Toast.makeText(this, "正在确认所选游戏", Toast.LENGTH_SHORT).show();
            return;
        }
        if (!installed) {
            new AlertDialog.Builder(this).setMessage(LauncherRelease.GAME.equals(target)
                    ? "请通过游戏官方渠道安装正版 The Bazaar，再返回重新检测。也可在「设置」中选择本机其他符合要求的游戏。首次登录及游戏资源下载由游戏完成。"
                    : "所选游戏已不在本机。请在「设置」中重新选择，或恢复正版大巴扎。")
                    .setPositiveButton("知道了", null).show(); return;
        }
        if (!supported) { refresh(); return; }
        if (!authorized()) { requestAccess(); return; }
        try {
            if (new StatFs(getFilesDir().getPath()).getAvailableBytes() < 1024L * 1024 * 1024 ||
                    new StatFs(Environment.getExternalStorageDirectory().getPath()).getAvailableBytes() < 1024L * 1024 * 1024)
                throw new IOException("至少需要 1 GB 可用空间；空间释放后重试");
            Intent intent = new Intent(this, BootstrapActivity.class);
            intent.putExtra(BootstrapActivity.EXTRA_TARGET_PACKAGE, target);
            intent.putExtra("bpp_repair", repair);
            LauncherLog.note(getFilesDir(), repair ? "用户开始修复" : "用户开始启动");
            startActivity(intent);
        } catch (Exception error) {
            LauncherLog.note(getFilesDir(), "启动未开始：" + error.getMessage());
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
        try {
            String target = LauncherGames.selected(this);
            report.put("gamePackage", target);
            report.put("officialTarget", LauncherRelease.GAME.equals(target));
            report.put("gameVersion", LauncherRelease.game(this, target).versionName);
        } catch (Exception ignored) {}
        String phase = getSharedPreferences("launcher", 0).getString("phase", "尚未初始化");
        report.put("phase", LauncherLog.sanitize(phase));
        report.put("lastPreparationStep", getSharedPreferences("launcher", 0).getString("lastPreparationStep", "尚未初始化"));
        report.put("stageTrace", new org.json.JSONArray(getSharedPreferences("launcher", 0).getString("stageTrace", "[]")));
        File previous = new File(getFilesDir(), "previous-plugin.dll");
        if (previous.isFile()) report.put("previousPluginSha256", LauncherFiles.sha256(previous));
        if (authorized() && getSharedPreferences("launcher", 0).contains("session"))
            report.put("loadStatus", LauncherPreparation.loadStatus(this));
        org.json.JSONArray log = new org.json.JSONArray();
        try {
            for (String line : LauncherLog.readLines(LauncherLog.file(getFilesDir()))) log.put(line);
        } catch (Exception error) { log.put("日志读取失败"); }
        report.put("log", log);
        return report;
    }

    private void exportLog() {
        new AlertDialog.Builder(this).setTitle("导出诊断日志")
                .setMessage("保存启动器版本、准备步骤和本机记录的运行情况。不包含游戏日志、账号或登录数据，也不会自动上传。保存后可用系统分享把文件发给维护者。")
                .setPositiveButton("选择保存位置", (dialog, which) -> {
                    Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                    intent.addCategory(Intent.CATEGORY_OPENABLE);
                    intent.setType("application/json");
                    intent.putExtra(Intent.EXTRA_TITLE, "bazaarplusplus-launcher-log.json");
                    try { startActivityForResult(intent, 9); }
                    catch (ActivityNotFoundException error) { Toast.makeText(this, "无法打开系统保存界面", Toast.LENGTH_LONG).show(); }
                }).setNegativeButton("取消", null).show();
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request == 9 && result == RESULT_OK && data != null && data.getData() != null) {
            final Uri uri = data.getData();
            checks.execute(() -> {
                String message;
                try (OutputStream out = getContentResolver().openOutputStream(uri)) {
                    if (out == null) throw new IOException();
                    out.write(LauncherRelease.bytes(report().toString(2))); message = "诊断日志已保存。其中没有游戏日志、账号或登录数据，可用系统分享发出";
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
