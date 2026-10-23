package dev.allofus.fusioncore;

import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.graphics.drawable.Drawable;
import java.io.File;
import java.text.Collator;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import dev.allofus.fusioncore.tools.Utilities;

/**
 * Optional launch target. The official package stays the default.
 * Other choices follow FusionCore's installed-IL2CPP scan, limited to arm64.
 */
public final class LauncherGames {
    static final String PREFERENCE = "selectedPackage";
    private LauncherGames() {}

    public static final class Choice {
        public final String packageName;
        public final String label;
        public final String version;
        public final Drawable icon;

        public Choice(String packageName, String label, String version, Drawable icon) {
            this.packageName = packageName;
            this.label = label;
            this.version = version;
            this.icon = icon;
        }
    }

    /** Saved choice when it is still installed and eligible; otherwise the official package. */
    public static String selected(Context context) {
        String saved = context.getSharedPreferences("launcher", 0).getString(PREFERENCE, "");
        if (saved == null || saved.isEmpty() || LauncherRelease.GAME.equals(saved)) return LauncherRelease.GAME;
        try {
            ApplicationInfo info = context.getPackageManager().getApplicationInfo(saved, 0);
            if (eligible(context, info)) return saved;
        } catch (PackageManager.NameNotFoundException ignored) {}
        return LauncherRelease.GAME;
    }

    public static boolean custom(Context context) {
        return !LauncherRelease.GAME.equals(selected(context));
    }

    /** Empty or the official package clears the override. Returns false when a custom package is rejected. */
    public static boolean select(Context context, String packageName) {
        if (packageName == null || packageName.equals(LauncherRelease.GAME)) {
            context.getSharedPreferences("launcher", 0).edit().remove(PREFERENCE).commit();
            return true;
        }
        try {
            ApplicationInfo info = context.getPackageManager().getApplicationInfo(packageName, 0);
            if (!eligible(context, info)) return false;
        } catch (PackageManager.NameNotFoundException error) { return false; }
        context.getSharedPreferences("launcher", 0).edit().putString(PREFERENCE, packageName).commit();
        return true;
    }

    public static String label(PackageManager manager, ApplicationInfo info) {
        try {
            CharSequence value = manager.getApplicationLabel(info);
            if (value != null) {
                String text = value.toString().replace('\n', ' ').trim();
                if (!text.isEmpty()) return text;
            }
        } catch (Exception ignored) {}
        return info.packageName;
    }

    /** Launchable, non-system arm64 IL2CPP apps other than this launcher and the official package. */
    public static List<Choice> installed(Context context) {
        PackageManager manager = context.getPackageManager();
        List<Choice> result = new ArrayList<>();
        Set<String> seen = new HashSet<>();
        Intent launch = new Intent(Intent.ACTION_MAIN);
        launch.addCategory(Intent.CATEGORY_LAUNCHER);
        List<ResolveInfo> activities = manager.queryIntentActivities(launch, PackageManager.MATCH_ALL);
        Drawable fallback = manager.getDefaultActivityIcon();
        for (ResolveInfo resolve : activities) {
            if (resolve.activityInfo == null) continue;
            String packageName = resolve.activityInfo.packageName;
            if (packageName == null || !seen.add(packageName)) continue;
            ApplicationInfo info;
            try { info = manager.getApplicationInfo(packageName, 0); }
            catch (PackageManager.NameNotFoundException error) { continue; }
            if (!eligible(context, info)) continue;
            String version = "版本未知";
            Drawable icon = fallback;
            try {
                PackageInfo installed = manager.getPackageInfo(packageName, 0);
                long code = installed.getLongVersionCode();
                String name = installed.versionName == null || installed.versionName.isEmpty()
                        ? "未知" : installed.versionName;
                version = Utilities.formatVersionText(name, code);
                icon = manager.getApplicationIcon(info);
            } catch (Exception ignored) {}
            result.add(new Choice(packageName, label(manager, info), version, icon));
        }
        Collator order = Collator.getInstance(Locale.CHINA);
        result.sort((left, right) -> order.compare(left.label, right.label));
        return result;
    }

    static boolean eligible(Context context, ApplicationInfo info) {
        if (info == null || info.packageName == null) return false;
        if (info.packageName.equals(context.getPackageName()) || info.packageName.equals(LauncherRelease.GAME))
            return false;
        if ((info.flags & ApplicationInfo.FLAG_SYSTEM) != 0) return false;
        if (!safePackage(info.packageName)) return false;
        if (context.getPackageManager().getLaunchIntentForPackage(info.packageName) == null) return false;
        return hasArm64Il2Cpp(info);
    }

    /** Same arm64 library the loader reads from the installed native directory. */
    static boolean hasArm64Il2Cpp(ApplicationInfo info) {
        String nativeDir = info == null ? null : info.nativeLibraryDir;
        return nativeDir != null && nativeDir.contains("arm64")
                && new File(nativeDir, "libil2cpp.so").isFile();
    }

    private static boolean safePackage(String packageName) {
        return packageName.matches("[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+")
                && !packageName.contains("..");
    }
}
