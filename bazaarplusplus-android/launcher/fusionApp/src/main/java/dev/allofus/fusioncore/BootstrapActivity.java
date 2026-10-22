package dev.allofus.fusioncore;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager.NameNotFoundException;
import android.os.Bundle;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;
import androidx.appcompat.app.AppCompatActivity;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.util.Map;
import dev.allofus.fusioncore.hooks.InstrumentationHooks;
import dev.allofus.fusioncore.hooks.ActivityManagerHooks;
import dev.allofus.fusioncore.hooks.NotificationManagerHooks;
import dev.allofus.fusioncore.hooks.PackageManagerHooks;
import dev.allofus.fusioncore.hooks.GameRuntime;
import dev.allofus.fusioncore.tools.FusionConfig;
import dev.allofus.fusioncore.tools.Il2CppApiMapper;
import dev.allofus.fusioncore.tools.NativeLibraryManager;
import dev.allofus.fusioncore.tools.Utilities;
import dev.allofus.fusioncore.tools.UnityUtils;

public class BootstrapActivity extends AppCompatActivity {

    private static final String TAG = "FusionCore";

    public static final String EXTRA_TARGET_PACKAGE = "target_package";
    private TextView statusView;
    private TextView progressDetailsView;
    private ProgressBar spinnerProgress;
    private ProgressBar downloadProgress;
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (!LauncherPreparation.BUSY.compareAndSet(false, true)) { finish(); return; }
        LauncherPreparation.rememberLastGood(getApplicationContext());
        LauncherPreparation.CANCEL.set(false);
        LauncherPreparation.activeSession = null;
        getSharedPreferences("launcher", 0).edit().remove("session").commit();
        setContentView(R.layout.activity_bootstrap);
        statusView = findViewById(R.id.bootstrap_status);
        progressDetailsView = findViewById(R.id.bootstrap_progress_details);
        spinnerProgress = findViewById(R.id.bootstrap_progress);
        downloadProgress = findViewById(R.id.bootstrap_download_progress);
        setPhaseStatus(getString(R.string.bootstrap_status_preparing));
        android.widget.LinearLayout root = findViewById(R.id.bootstrap_root);
        android.widget.Button cancel = new android.widget.Button(this);
        cancel.setText("取消准备");
        cancel.setOnClickListener(v -> {
            LauncherPreparation.CANCEL.set(true);
            cancel.setEnabled(false);
            cancel.setText("等待当前安全步骤结束…");
        });
        root.addView(cancel);
        TextView elapsed = new TextView(this);
        elapsed.setGravity(android.view.Gravity.CENTER);
        root.addView(elapsed);
        long started = android.os.SystemClock.elapsedRealtime();
        elapsed.post(new Runnable() {
            @Override public void run() {
                if (isFinishing() || isDestroyed()) return;
                elapsed.setText("准备运行环境已耗时 " + (android.os.SystemClock.elapsedRealtime() - started) / 1000 + " 秒");
                elapsed.postDelayed(this, 1000);
            }
        });

        String targetPackage = getIntent().getStringExtra(EXTRA_TARGET_PACKAGE);
        if (targetPackage == null || targetPackage.isEmpty()) {
            failAndFinish("No target package specified in intent extras!", null);
            return;
        }

        // Let the loading screen render first, then perform initialization work.
        statusView.post(() -> new Thread(() -> runBootstrapFlow(targetPackage), "bootstrap-flow").start());
    }

    @Override
    public void onBackPressed() {
        if (LauncherPreparation.BUSY.get()) {
            Toast.makeText(this, "正在准备运行环境，请等待当前步骤完成", Toast.LENGTH_SHORT).show();
        } else { super.onBackPressed(); }
    }

    private void runBootstrapFlow(String targetPackage) {
        Context gameContext;
        try {
            gameContext = createPackageContext(targetPackage, CONTEXT_IGNORE_SECURITY | CONTEXT_INCLUDE_CODE);
        } catch (Exception e) {
            failAndFinish("Failed to create package context for target package: " + targetPackage, e);
            return;
        }

        try {
            gameContext = GameRuntime.prepareGameContext(gameContext, getClassLoader());
        } catch (Exception e) {
            failAndFinish("Failed to configure classes.", e);
            return;
        }

        Intent launchIntent = getPackageManager().getLaunchIntentForPackage(targetPackage);
        if (launchIntent == null) {
            failAndFinish("No launch intent for target package: " + targetPackage, null);
            return;
        }

        ComponentName launcherComponent = launchIntent.getComponent();
        if (launcherComponent == null) {
            launcherComponent = launchIntent.resolveActivity(getPackageManager());
        }

        if (launcherComponent == null) {
            failAndFinish("Failed to resolve launcher activity for target package: " + targetPackage, null);
            return;
        }

        final int targetOrientation = resolveTargetOrientation(launcherComponent);

        FusionConfig config;

        try {
            config = prepareConfig(
                    getApplicationContext(),
                    gameContext,
                    launcherComponent,
                    targetPackage
            );
        } catch (Throwable t) {
            failAndFinish("Failed while preparing Fusion runtime.", t);
            return;
        }

        Class<?> launcherClass;
        try {
            launcherClass = gameContext.getClassLoader().loadClass(launcherComponent.getClassName());
        } catch (ClassNotFoundException e) {
            failAndFinish("Failed to get class for launcher activity", e);
            return;
        }

        setPhaseStatus(getString(R.string.bootstrap_status_installing_hooks));
        try {
            LauncherPreparation.checkCancelled();
            PackageManagerHooks.install(getApplicationContext(), gameContext);
            ActivityManagerHooks.install(getApplicationContext(), gameContext);
            NotificationManagerHooks.install(getApplicationContext(), gameContext);
            GameRuntime.install(gameContext);
        } catch (Exception e) {
            failAndFinish("Failed to install base hooks", e);
            return;
        }

        var className = launcherComponent.getClassName();
        final Context preparedGameContext = gameContext;

        try {
            setPhaseStatus(getString(R.string.bootstrap_status_launching));
            initializeFusion(config);
            runOnMainThread(() -> {
                try {
                    GameRuntime.initializeApplication(preparedGameContext, getApplicationContext());
                    var intent = new Intent(this, launcherClass);

                    // Using the stub activity intent here avoids one extra layer of hooks running.
                    // Its not necessary but could be more performant.
                    var intentWrapped = new Intent(this, StubActivity.class);
                    intentWrapped.putExtra(InstrumentationHooks.EXTRA_IS_DYNAMIC_ACTIVITY, true);
                    intentWrapped.putExtra(InstrumentationHooks.EXTRA_ORIGINAL_INTENT, intent);
                    intentWrapped.putExtra(InstrumentationHooks.EXTRA_FUSION_CONFIG, config);
                    intentWrapped.putExtra(InstrumentationHooks.EXTRA_TARGET_ORIENTATION, targetOrientation);
                    startActivity(intentWrapped);
                    finish();
                } catch (Throwable t) {
                    failAndFinish("Failed to launch target app's launcher activity: " + className, t);
                }
            });
        } catch (Exception e) {
            failAndFinish("Failed to launch target app's launcher activity: " + className, e);
        }
    }

    private void setPhaseStatus(String status) {
        LauncherPreparation.phase(getApplicationContext(), status);
        runOnMainThread(() -> {
            if (statusView != null) {
                statusView.setText(status);
            }
            if (spinnerProgress != null) {
                spinnerProgress.setVisibility(View.VISIBLE);
            }
            if (downloadProgress != null) {
                downloadProgress.setVisibility(View.GONE);
                downloadProgress.setIndeterminate(false);
                downloadProgress.setProgress(0);
            }
            if (progressDetailsView != null) {
                progressDetailsView.setVisibility(View.GONE);
                progressDetailsView.setText("");
            }
        });
    }

    private void failAndFinish(String message, Throwable error) {
        LauncherPreparation.BUSY.set(false);
        LauncherPreparation.activeSession = null;
        getSharedPreferences("launcher", 0).edit().remove("session").commit();
        LauncherPreparation.phase(getApplicationContext(), "初始化失败：" + (error != null && error.getMessage() != null ? error.getMessage() : "运行环境准备失败，可重试"));
        runOnMainThread(() -> {
            if (error != null) {
                Log.e(TAG, message, error);
            } else {
                Log.e(TAG, message);
            }
            if (statusView != null) {
                statusView.setText(getString(R.string.bootstrap_status_error));
            }
            Toast.makeText(this, message, Toast.LENGTH_LONG).show();
            finish();
        });
    }

    private void runOnMainThread(Runnable runnable) {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            runnable.run();
        } else {
            runOnUiThread(runnable);
        }
    }

    private void initializeFusion(FusionConfig config) {
        Log.i(TAG, "Initializing Fusion for " + config.gamePackageId + " via " + config.gameLauncherName);

        try {
            NativeLibraryManager.addFusionLibrary("main");
            NativeLibraryManager.addFusionLibrary("fusion");
            NativeLibraryManager.addCacheLibrary("il2cpp");
            NativeLibraryManager.addCacheLibrary("unity");
            NativeLibraryManager.setupLibraryHooks(config);
        } catch (Throwable t) {
            throw new IllegalStateException("Failed to initialize Fusion library routing", t);
        }
    }

    private Map<String, String> resolveIl2CppApiMap(File gameLibDir, File codeCacheScoped) throws IOException {
        File il2cpp = new File(gameLibDir, "libil2cpp.so");
        Map<String, String> map = Il2CppApiMapper.prepare(new File(gameLibDir, "libunity.so"), il2cpp,
                new File(codeCacheScoped, "libunity.so"));
        if (map.isEmpty() && Il2CppApiMapper.hasObfuscatedExports(il2cpp))
            throw new IOException("当前游戏的运行接口无法自动适配，请导出诊断");
        return map;
    }

    private static File getRuntimeDir(Context context) {
        return context.getDir("fusion_runtime", Context.MODE_PRIVATE);
    }

    private FusionConfig prepareConfig(Context appContext,
                                       Context gameContext,
                                       ComponentName launcherComponent,
                                       String targetPackage) throws Exception {

        LauncherRelease.check(appContext, LauncherRelease.manifest(appContext));
        String gameLibDir = gameContext.getApplicationInfo().nativeLibraryDir;
        String appLibDir = appContext.getApplicationInfo().nativeLibraryDir;

        File appDataDir = new File(appContext.getFilesDir(), targetPackage);
        LauncherFiles.mkdir(appDataDir);

        File dataOnSdCard = Utilities.getExternalFusionCoreDirectory(targetPackage, null);
        File bepInExDir = new File(dataOnSdCard, "BepInEx");
        File codeCacheScoped = new File(getRuntimeDir(appContext), targetPackage);

        setPhaseStatus(getString(R.string.bootstrap_status_copy_assets));
        File Assets = new File(dataOnSdCard, "Assets");
        setPhaseStatus("核对游戏安装包，首次准备可能需要一分钟…");
        File resourceStamp = new File(appContext.getFilesDir(), "launcher-resource-key");
        StringBuilder key = new StringBuilder(LauncherRelease.game(appContext).versionName)
                .append(LauncherRelease.manifest(appContext).getJSONObject("framework").getString("sha256"));
        java.util.List<String> apks = new java.util.ArrayList<>();
        apks.add(gameContext.getApplicationInfo().sourceDir);
        if (gameContext.getApplicationInfo().splitSourceDirs != null)
            java.util.Collections.addAll(apks, gameContext.getApplicationInfo().splitSourceDirs);
        for (String apk : apks) key.append(LauncherFiles.sha256(new File(apk)));
        String resourceKey = key.toString();
        if (getIntent().getBooleanExtra("bpp_repair", false) || !resourceStamp.isFile() ||
                !resourceKey.equals(LauncherRelease.read(new FileInputStream(resourceStamp)))) {
            LauncherFiles.removeOwned(Assets);
            LauncherFiles.removeOwned(new File(bepInExDir, "interop"));
            LauncherFiles.removeOwned(new File(bepInExDir, "cache"));
        }
        setPhaseStatus("准备游戏运行资源…");
        if (!new File(Assets, ".fusion_copied").isFile()) {
            long required = 0;
            for (String apk : apks) {
                try (java.util.zip.ZipFile archive = new java.util.zip.ZipFile(apk)) {
                    java.util.Enumeration<? extends java.util.zip.ZipEntry> entries = archive.entries();
                    while (entries.hasMoreElements()) {
                        java.util.zip.ZipEntry entry = entries.nextElement();
                        if (entry.getName().startsWith("assets/") && !entry.isDirectory())
                            required += Math.max(0L, entry.getSize());
                    }
                }
            }
            if (new android.os.StatFs(dataOnSdCard.getPath()).getAvailableBytes() < required + 512L * 1024 * 1024)
                throw new IOException("可用空间不足，无法准备游戏运行资源；请释放空间后重试");
        }
        boolean copied = Utilities.copyAssets(gameContext.getApplicationInfo(), Assets);
        if (!copied) {
            throw new IOException("无法读取已安装游戏资源，请检查空间及权限");
        }
        File copiedData = new File(Assets, "bin/Data");
        setPhaseStatus(getString(R.string.bootstrap_status_detecting_version));
        String version = UnityUtils.TryGetVersion(copiedData);
        if (version == null) throw new IOException("无法识别游戏 Unity 版本");
        LauncherFiles.write(resourceStamp, LauncherRelease.bytes(resourceKey));
        LauncherPreparation.prepare(appContext, codeCacheScoped, version, this::setPhaseStatus);
        File dotnetDir = new File(getRuntimeDir(appContext), "dotnet");

        Map<String, String> il2cppMap = resolveIl2CppApiMap(new File(gameLibDir), codeCacheScoped);

        return new FusionConfig(
                targetPackage,
                launcherComponent.flattenToString(),
                gameLibDir,
                appLibDir,
                appDataDir.getAbsolutePath(),
                codeCacheScoped.getAbsolutePath(),
                bepInExDir.getAbsolutePath(),
                dotnetDir.getAbsolutePath(),
                copiedData.getAbsolutePath(),
                version,
                false,
                false,
                new String[]{},
                new String[]{},
                il2cppMap
        );
    }

    private int resolveTargetOrientation(ComponentName launcher) {
        try {
            ActivityInfo info = getPackageManager().getActivityInfo(launcher, 0);
            if (info.screenOrientation == ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED) {
                Log.i(TAG, "Target orientation unspecified; defaulting to landscape");
                return ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
            }
            return info.screenOrientation;
        } catch (NameNotFoundException e) {
            return ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE;
        }
    }

}
