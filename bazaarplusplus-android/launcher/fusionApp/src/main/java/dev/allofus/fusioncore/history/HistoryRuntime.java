package dev.allofus.fusioncore.history;

import java.io.File;
import java.io.IOException;
import dev.allofus.fusioncore.tools.Utilities;

/** Unity may outlive its Activity. Keep its lease until process death once native loading begins. */
public final class HistoryRuntime {
    private static HistoryAccessLock gameLease;
    private static String target;
    private static boolean nativeLoading;

    private HistoryRuntime() { }

    public static synchronized void beginGame(String targetPackage) throws IOException {
        if (gameLease != null) throw new IOException("本进程已准备游戏，请完全关闭启动器后重试");
        File root = Utilities.getExternalFusionCoreDirectory(targetPackage, null);
        gameLease = HistoryAccessLock.acquire(root);
        target = targetPackage;
        try { System.loadLibrary("bpphistory"); }
        catch (LinkageError error) {
            android.util.Log.w("BppHistory", "History JNI host unavailable; game loading continues", error);
        }
    }

    public static synchronized void markNativeLoading() { nativeLoading = true; }

    public static synchronized void preparationFailed() {
        if (nativeLoading || gameLease == null) return;
        try { gameLease.close(); }
        catch (IOException ignored) { /* Closing the channel still releases the OS lease. */ }
        gameLease = null;
        target = null;
    }

    static synchronized File gameRoot() {
        if (gameLease == null || target == null) throw new IllegalStateException("History host is not configured");
        return Utilities.getExternalFusionCoreDirectory(target, null);
    }

    static synchronized String targetPackage() {
        gameRoot();
        return target;
    }
}
