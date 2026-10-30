package dev.allofus.fusioncore.tools;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.widget.TextView;

import java.util.Locale;

import dev.allofus.fusioncore.LauncherLog;
import dev.allofus.fusioncore.LauncherPreparation;
import dev.allofus.fusioncore.R;

public class JniBridge {
    private static final String TAG = "JniBridge";

    private static final Handler handler = new Handler(Looper.getMainLooper());
    private static Runnable timerRunnable;
    private static volatile android.content.Context app;

    public static void attach(android.content.Context context) {
        if (context == null) return;
        app = context.getApplicationContext();
        LauncherPreparation.watchCrashes(app);
    }

    public static native void SetRuntimeNotes(boolean enabled);

    /** Stop native-to-Java notes after the handoff. Safe before libfusion is loaded. */
    public static void stopRuntimeNotes() {
        try { SetRuntimeNotes(false); }
        catch (Throwable ignored) {}
    }

    /** Native runtime note. Only the handoff window is kept, and the launcher log still redacts it. */
    public static void NoteRuntime(String text) {
        android.content.Context context = app;
        if (context == null || text == null || text.isEmpty()) return;
        if (text.length() > 500) text = text.substring(0, 500);
        if (!LauncherPreparation.acceptRuntimeNote(text)) return;
        LauncherLog.note(context.getFilesDir(), "运行时 " + text);
    }

    public static void SetLoadingState(Activity activity, boolean enabled) {
        activity.runOnUiThread(() -> {
            View view = activity.findViewById(R.id.loader);
            if (view == null) {
                Log.e(TAG, "Could not set loading state, view is null!");
                return;
            }

            boolean shown = false;
            try { shown = activity.getSharedPreferences("launcher", 0).getBoolean("loaderActive", false); }
            catch (Exception ignored) {}
            view.setVisibility(enabled ? View.VISIBLE : View.GONE);
            LauncherPreparation.markLoader(activity, enabled);
            if (!enabled && shown) {
                LauncherPreparation.markHandoff(activity, false);
                stopRuntimeNotes();
            }
            Log.i(TAG, "Set loading state to "+enabled);

            if (!enabled && timerRunnable != null) {
                handler.removeCallbacks(timerRunnable);
            }
        });
    }

    public static void SetLoadingText(Activity activity, String text) {
        LauncherLog.note(activity.getFilesDir(), "加载状态 " + (text == null ? "" : text));
        String lower = text == null ? "" : text.toLowerCase(Locale.ROOT);
        final int userText = lower.contains("cpp2il") ? R.string.bpp_cpp2il
                : lower.contains("interop") ? R.string.bpp_interop
                : lower.contains("plugin") ? R.string.bpp_plugin_loading
                : lower.contains("fail") || lower.contains("error") ? R.string.bpp_runtime_failed
                : R.string.bpp_runtime_preparing;
        activity.runOnUiThread(() -> {
            TextView view = activity.findViewById(R.id.loadingText);
            if (view == null) {
                Log.e(TAG, "Could not set loading text, view is null!");
                return;
            }

            view.setText(activity.getString(userText));
            View loader = activity.findViewById(R.id.loader);
            if (loader != null && loader.getVisibility() == View.VISIBLE) LauncherPreparation.markLoader(activity, true);
            Log.i(TAG, "Set loading text to "+text);

            // reset timer
            if (timerRunnable != null) {
                handler.removeCallbacks(timerRunnable);
            }

            timerRunnable = new Runnable() {
                private int elapsedSeconds = 0;

                @Override
                public void run() {
                    elapsedSeconds++;
                    int minutes = elapsedSeconds / 60;
                    int seconds = elapsedSeconds % 60;

                    String timeString = String.format(Locale.getDefault(),
                            " (%02d:%02d)", minutes, seconds);

                    view.setText(activity.getString(userText) + timeString);

                    handler.postDelayed(this, 1000);
                }
            };
            handler.post(timerRunnable);
        });
    }
}
