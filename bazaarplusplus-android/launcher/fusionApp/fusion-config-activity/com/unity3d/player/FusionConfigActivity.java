package com.unity3d.player;

import android.content.res.Configuration;
import android.util.Log;

/**
 * Injected into the game class loader.
 * Rebuild with javac against the stub, then d8, and replace assets/fusion-config-activity.dex.
 */
public class FusionConfigActivity extends UnityPlayerActivity {
    /** Caller class must belong to the game loader so JNI registration lands on its UnityPlayer. */
    public static void loadUnity(String path) {
        System.load(path);
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        try {
            super.onConfigurationChanged(newConfig);
        } catch (UnsatisfiedLinkError error) {
            // Xiaomi delivers a configuration change before Unity finishes RegisterNatives.
            Log.w("GameRuntime", "Ignored Unity configuration change before native registration", error);
        }
    }
}
