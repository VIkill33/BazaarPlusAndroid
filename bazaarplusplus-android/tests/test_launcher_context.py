"""Run the production context wrapper against small JVM Android API doubles."""
import pathlib
import shutil
import subprocess
import tempfile
import unittest


STUBS = {
    "android/content/Context.java": """
        package android.content;
        import java.io.File;
        import android.database.sqlite.SQLiteDatabase;
        import android.database.DatabaseErrorHandler;
        public class Context {
            public static final String WINDOW_SERVICE = "window";
            public android.content.pm.ApplicationInfo getApplicationInfo() {
                return new android.content.pm.ApplicationInfo();
            }
            public SharedPreferences getSharedPreferences(String n, int m) { return null; }
            public boolean deleteSharedPreferences(String n) { return false; }
            public boolean moveSharedPreferencesFrom(Context c, String n) { return false; }
            public File getDatabasePath(String n) { return null; }
            public SQLiteDatabase openOrCreateDatabase(String n, int m, SQLiteDatabase.CursorFactory f) { return null; }
            public SQLiteDatabase openOrCreateDatabase(String n, int m, SQLiteDatabase.CursorFactory f,
                    DatabaseErrorHandler e) { return null; }
            public boolean deleteDatabase(String n) { return false; }
            public String[] databaseList() { return null; }
            public boolean moveDatabaseFrom(Context c, String n) { return false; }
            public File getFilesDir() { return null; }
            public File getNoBackupFilesDir() { return null; }
            public File getDir(String n, int m) { return null; }
            public File getCodeCacheDir() { return null; }
            public File getCacheDir() { return null; }
            public File getExternalCacheDir() { return null; }
            public File[] getExternalCacheDirs() { return null; }
            public File getExternalFilesDir(String t) { return null; }
            public android.view.Display getDisplay() { throw new AssertionError("public display API used before R"); }
            public Object getSystemService(String n) { return null; }
            public Context getApplicationContext() { return this; }
            public File getObbDir() { return null; }
            public File[] getObbDirs() { return null; }
            public String getPackageName() { return "game"; }
        }
    """,
    "android/content/ContextWrapper.java": """
        package android.content;
        public class ContextWrapper extends Context {
            private Context base;
            public ContextWrapper(Context b) { base = b; }
            protected void attachBaseContext(Context b) { base = b; }
            public Context getBaseContext() { return base; }
            @Override public android.view.Display getDisplay() { return base.getDisplay(); }
        }
    """,
    "android/app/Application.java": """
        package android.app;
        public class Application extends android.content.ContextWrapper {
            public Application() { super(null); }
            public interface ActivityLifecycleCallbacks {}
            public void registerActivityLifecycleCallbacks(ActivityLifecycleCallbacks c) {}
            public void unregisterActivityLifecycleCallbacks(ActivityLifecycleCallbacks c) {}
            public void registerComponentCallbacks(android.content.ComponentCallbacks c) {}
            public void unregisterComponentCallbacks(android.content.ComponentCallbacks c) {}
        }
    """,
    "android/content/pm/ApplicationInfo.java": "package android.content.pm; public class ApplicationInfo { public String dataDir, nativeLibraryDir; }",
    "android/content/SharedPreferences.java": "package android.content; public interface SharedPreferences {}",
    "android/content/ComponentCallbacks.java": "package android.content; public interface ComponentCallbacks {}",
    "android/content/res/AssetManager.java": "package android.content.res; public class AssetManager {}",
    "android/database/DatabaseErrorHandler.java": "package android.database; public interface DatabaseErrorHandler {}",
    "android/database/sqlite/SQLiteDatabase.java": "package android.database.sqlite; public class SQLiteDatabase { public interface CursorFactory {} }",
    "android/os/Build.java": "package android.os; public class Build { public static class VERSION { public static int SDK_INT; } public static class VERSION_CODES { public static final int R = 30; } }",
    "android/util/Log.java": "package android.util; public class Log { public static int i(String t, String m) { return 0; } }",
    "android/view/Display.java": "package android.view; public class Display { private final int id; public Display(int i) { id = i; } public int getDisplayId() { return id; } }",
    "android/view/WindowManager.java": "package android.view; public interface WindowManager { Display getDefaultDisplay(); }",
    "org/jetbrains/annotations/Nullable.java": "package org.jetbrains.annotations; public @interface Nullable {}",
    "dev/allofus/fusioncore/hooks/GameRuntime.java": "package dev.allofus.fusioncore.hooks; public class GameRuntime { public static android.app.Application getGameApplication() { return null; } }",
    "dev/allofus/fusioncore/tools/Utilities.java": "package dev.allofus.fusioncore.tools; public class Utilities { public static java.io.File getExternalFusionCoreDirectory(String p, String s) { return new java.io.File(\"persistent\"); } }",
}


class LauncherContextTests(unittest.TestCase):
    def test_configuration_display_on_old_and_new_android(self):
        root = pathlib.Path(__file__).resolve().parents[1]
        javac, java = shutil.which("javac"), shutil.which("java")
        self.assertIsNotNone(javac, "JDK required for launcher context verification")
        self.assertIsNotNone(java, "JDK required for launcher context verification")
        with tempfile.TemporaryDirectory() as temp:
            output = pathlib.Path(temp)
            sources = []
            for name, code in STUBS.items():
                target = output / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(code)
                sources.append(str(target))
            sources += [str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/tools/CustomContextWrapper.java"),
                        str(root / "tests/LauncherContextTest.java")]
            subprocess.run([javac, "--release", "8", "-d", temp, *sources], check=True)
            subprocess.run([java, "-cp", temp, "LauncherContextTest"], check=True)
