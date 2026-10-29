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
            stateTitle.setText(getString(R.string.bpp_home_checking_title));
            stateHint.setText(getString(R.string.bpp_home_checking_hint));
            primary.setEnabled(false);
            refresh();
        });
        findViewById(R.id.launcher_donate).setOnClickListener(v -> donate());
        findViewById(R.id.launcher_history).setOnClickListener(v -> startActivity(new Intent(this, LauncherHistoryActivity.class)
                .putExtra(LauncherHistoryActivity.TARGET, LauncherGames.selected(this))));
        findViewById(R.id.launcher_purchases).setOnClickListener(v -> startActivity(new Intent(this, LauncherPurchaseActivity.class)
                .putExtra(LauncherHistoryActivity.TARGET, LauncherGames.selected(this))));
        findViewById(R.id.launcher_github).setOnClickListener(v -> {
            try {
                startActivity(new Intent(Intent.ACTION_VIEW,
                        Uri.parse("https://github.com/VIkill33/BazaarPlusAndroid")));
            } catch (ActivityNotFoundException error) {
                Toast.makeText(this, getString(R.string.bpp_link_open_failed), Toast.LENGTH_SHORT).show();
            }
        });
        findViewById(R.id.launcher_settings).setOnClickListener(v -> startActivity(new Intent(this, LauncherSettingsActivity.class)));
        findViewById(R.id.launcher_help).setOnClickListener(v -> startActivity(new Intent(this, LauncherHelpActivity.class)));
        findViewById(R.id.launcher_more).setOnClickListener(v -> help());
        ((TextView) findViewById(R.id.launcher_version)).setText(getString(R.string.bpp_launcher_version_prefix) + BuildConfig.VERSION_NAME);
        showCredit();
    }

    private void showCredit() {
        TextView credit = findViewById(R.id.launcher_credit);
        String label = "BazaarPlusPlus";
        String sentence = getString(R.string.bpp_credit, label);
        SpannableString text = new SpannableString(sentence);
        int start = sentence.indexOf(label);
        text.setSpan(new ClickableSpan() {
            @Override public void onClick(View widget) {
                try {
                    startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://bazaarplusplus.com")));
                } catch (ActivityNotFoundException error) {
                    Toast.makeText(LauncherActivity.this, getString(R.string.bpp_link_open_failed), Toast.LENGTH_SHORT).show();
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
        AlertDialog dialog = new AlertDialog.Builder(this).setView(view).setPositiveButton(getString(R.string.bpp_close), null).create();
        view.setOnClickListener(v -> dialog.dismiss());
        dialog.show();
        if (dialog.getWindow() != null) {
            int width = Math.min(getResources().getDisplayMetrics().widthPixels,
                    Math.round(520 * getResources().getDisplayMetrics().density));
            dialog.getWindow().setLayout(width, android.view.ViewGroup.LayoutParams.WRAP_CONTENT);
        }
    }

    private void help() {
        new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_more_title))
                .setItems(new String[]{getString(R.string.bpp_installation_help), getString(R.string.bpp_file_access_help), getString(R.string.bpp_repair_environment), getString(R.string.bpp_export_diagnostics), getString(R.string.bpp_about_licenses), getString(R.string.bpp_exit_launcher)},
                        (dialog, which) -> {
                            switch (which) {
                                case 0 -> new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_installation_help))
                                        .setMessage(getString(R.string.bpp_installation_instructions))
                                        .setPositiveButton(getString(R.string.bpp_ok), null).show();
                                case 1 -> new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_file_access_title))
                                        .setMessage(getString(R.string.bpp_file_access_description))
                                        .setPositiveButton(getString(R.string.bpp_ok), null).show();
                                case 2 -> new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_repair_environment))
                                        .setMessage(getString(R.string.bpp_repair_description))
                                        .setPositiveButton(getString(R.string.bpp_repair_action), (d, w) -> launch(true))
                                        .setNegativeButton(getString(R.string.bpp_cancel), null).show();
                                case 3 -> exportLog();
                                case 4 -> licenses();
                                case 5 -> new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_exit_launcher))
                                        .setMessage(getString(R.string.bpp_exit_description))
                                        .setPositiveButton(getString(R.string.bpp_exit_action), (d, w) -> {
                                            finishAndRemoveTask(); android.os.Process.killProcess(android.os.Process.myPid());
                                        }).setNegativeButton(getString(R.string.bpp_cancel), null).show();
                            }
                        }).setNegativeButton(getString(R.string.bpp_close), null).show();
    }

    private void licenses() {
        checks.execute(() -> {
            String details;
            try {
                details = getString(R.string.app_name) + " " + BuildConfig.VERSION_NAME + getString(R.string.bpp_about_disclaimer) +
                        LauncherRelease.read(getAssets().open("launcher/NOTICE.txt")) + "\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/BPP-LICENSE.txt")) + "\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/MDI-LICENSE.txt")) + "\n\n" +
                        LauncherRelease.read(getAssets().open("launcher/FusionCore-LICENSE.txt"));
            } catch (Exception error) { details = getString(R.string.bpp_licenses_read_failed); }
            final String text = details;
            runOnUiThread(() -> {
                if (!isFinishing() && !isDestroyed()) new AlertDialog.Builder(this)
                        .setTitle(getString(R.string.bpp_about_licenses)).setMessage(text).setPositiveButton(getString(R.string.bpp_close), null).show();
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
                message = (official ? getString(R.string.bpp_official_plugin_prefix) + release.getJSONObject("plugin").getString("version")
                        : getString(R.string.bpp_custom_plugin, label, release.getJSONObject("plugin").getString("version")));
                if (reverted) message += getString(R.string.bpp_target_reverted_note);
                if (!authorized()) message += getString(R.string.bpp_file_access_note);
                else {
                    String phase = LauncherPreparation.phaseText(this);
                    if (LauncherPreparation.BUSY.get()) {
                        message += "\n" + phase;
                        if (getSharedPreferences("launcher", 0).contains("session"))
                            message += "\n" + LauncherPreparation.loadStatus(this);
                    } else if (LauncherPreparation.failed(this)) message += "\n" + phase;
                }
            } catch (android.content.pm.PackageManager.NameNotFoundException error) {
                message = official ? getString(R.string.bpp_official_not_installed) : getString(R.string.bpp_target_not_installed);
                if (reverted) message += getString(R.string.bpp_target_reverted_note);
            }
            catch (Exception error) {
                message = getString(R.string.bpp_compatibility_error_prefix) + LauncherMessages.error(this, error.getMessage());
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
                boolean failed = LauncherPreparation.failed(this);
                boolean ready = installed && supported && access && !busy && !failed;
                stateTitle.setText(busy ? getString(R.string.bpp_home_busy_title) : !installed ? getString(R.string.bpp_home_install_title) : !supported ? getString(R.string.bpp_home_compatibility_title) :
                        !access ? getString(R.string.bpp_home_access_title) : failed ? getString(R.string.bpp_home_retry_title) : getString(R.string.bpp_home_ready_title));
                stateHint.setText(busy ? getString(R.string.bpp_home_busy_hint) : ready
                        ? (finalOfficial ? getString(R.string.bpp_home_ready_hint) : getString(R.string.bpp_custom_ready, finalLabel))
                        : !installed ? (finalOfficial ? getString(R.string.bpp_home_install_hint) : getString(R.string.bpp_home_select_hint))
                        : !supported ? getString(R.string.bpp_home_compatibility_hint) :
                        !access ? getString(R.string.bpp_home_access_hint) : getString(R.string.bpp_home_retry_hint));
                stateIcon.setImageResource(ready ? R.drawable.bpp_check : R.drawable.bpp_help);
                stateIcon.setImageTintList(ColorStateList.valueOf(getColor(ready ? R.color.bpp_success : R.color.bpp_accent)));
                primary.setEnabled(!busy);
                primary.setText(busy ? getString(R.string.bpp_home_busy_action) : !installed ? getString(R.string.bpp_home_install_action) : !supported ? getString(R.string.bpp_home_compatibility_action) :
                        !authorized() ? getString(R.string.bpp_home_access_action) : LauncherPreparation.failed(this) ? getString(R.string.bpp_home_retry_action) :
                        getString(R.string.bpp_launch_game));
            });
        });
    }

    private void launchOrExplain() {
        launch(false);
    }

    private void launch(boolean repair) {
        if (LauncherPreparation.BUSY.get()) {
            LauncherLog.note(getFilesDir(), getString(R.string.bpp_log_launch_busy));
            Toast.makeText(this, getString(R.string.bpp_exit_current_game), Toast.LENGTH_LONG).show(); return;
        }
        String target = LauncherGames.selected(this);
        if (checkedPackage == null || !target.equals(checkedPackage)) {
            refresh();
            Toast.makeText(this, getString(R.string.bpp_checking_target), Toast.LENGTH_SHORT).show();
            return;
        }
        if (!installed) {
            new AlertDialog.Builder(this).setMessage(LauncherRelease.GAME.equals(target)
                    ? getString(R.string.bpp_install_official_instructions)
                    : getString(R.string.bpp_missing_target_instructions))
                    .setPositiveButton(getString(R.string.bpp_ok), null).show(); return;
        }
        if (!supported) { refresh(); return; }
        if (!authorized()) { requestAccess(); return; }
        try {
            if (new StatFs(getFilesDir().getPath()).getAvailableBytes() < 1024L * 1024 * 1024 ||
                    new StatFs(Environment.getExternalStorageDirectory().getPath()).getAvailableBytes() < 1024L * 1024 * 1024)
                throw new IOException(getString(R.string.bpp_insufficient_space));
            Intent intent = new Intent(this, BootstrapActivity.class);
            intent.putExtra(BootstrapActivity.EXTRA_TARGET_PACKAGE, target);
            intent.putExtra("bpp_repair", repair);
            LauncherLog.note(getFilesDir(), repair ? getString(R.string.bpp_log_repair_started) : getString(R.string.bpp_log_launch_started));
            startActivity(intent);
        } catch (Exception error) {
            LauncherLog.note(getFilesDir(), getString(R.string.bpp_log_launch_not_started) + LauncherMessages.error(this, error.getMessage()));
            stateTitle.setText(getString(R.string.bpp_home_error_title));
            stateHint.setText(getString(R.string.bpp_home_error_hint));
            stateIcon.setImageResource(R.drawable.bpp_help);
            stateIcon.setImageTintList(ColorStateList.valueOf(getColor(R.color.bpp_accent)));
            status.setText(LauncherMessages.error(this, error.getMessage()));
            new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_home_error_title))
                    .setMessage(LauncherMessages.error(this, error.getMessage())).setPositiveButton(getString(R.string.bpp_ok), null).show();
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
        String phase = LauncherPreparation.phaseText(this);
        report.put("phase", LauncherLog.sanitize(phase));
        report.put("lastPreparationStep", getSharedPreferences("launcher", 0).getString("lastPreparationStep", getString(R.string.bpp_not_initialized)));
        report.put("stageTrace", new org.json.JSONArray(getSharedPreferences("launcher", 0).getString("stageTrace", "[]")));
        File previous = new File(getFilesDir(), "previous-plugin.dll");
        if (previous.isFile()) report.put("previousPluginSha256", LauncherFiles.sha256(previous));
        if (authorized() && getSharedPreferences("launcher", 0).contains("session"))
            report.put("loadStatus", LauncherPreparation.loadStatus(this));
        org.json.JSONArray log = new org.json.JSONArray();
        try {
            for (String line : LauncherLog.readLines(LauncherLog.file(getFilesDir()))) log.put(line);
        } catch (Exception error) { log.put(getString(R.string.bpp_log_read_failed)); }
        report.put("log", log);
        return report;
    }

    private void exportLog() {
        new AlertDialog.Builder(this).setTitle(getString(R.string.bpp_export_diagnostics))
                .setMessage(getString(R.string.bpp_diagnostics_description))
                .setPositiveButton(getString(R.string.bpp_diagnostics_choose_location), (dialog, which) -> {
                    Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
                    intent.addCategory(Intent.CATEGORY_OPENABLE);
                    intent.setType("application/json");
                    intent.putExtra(Intent.EXTRA_TITLE, "bazaarplusplus-launcher-log.json");
                    try { startActivityForResult(intent, 9); }
                    catch (ActivityNotFoundException error) { Toast.makeText(this, getString(R.string.bpp_diagnostics_save_dialog_failed), Toast.LENGTH_LONG).show(); }
                }).setNegativeButton(getString(R.string.bpp_cancel), null).show();
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request == 9 && result == RESULT_OK && data != null && data.getData() != null) {
            final Uri uri = data.getData();
            checks.execute(() -> {
                String message;
                try (OutputStream out = getContentResolver().openOutputStream(uri)) {
                    if (out == null) throw new IOException();
                    out.write(LauncherRelease.bytes(report().toString(2))); message = getString(R.string.bpp_diagnostics_saved);
                } catch (Exception error) { message = getString(R.string.bpp_diagnostics_export_failed); }
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
