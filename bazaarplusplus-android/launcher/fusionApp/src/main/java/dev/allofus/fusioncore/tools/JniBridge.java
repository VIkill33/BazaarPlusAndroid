package dev.allofus.fusioncore.tools;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.widget.TextView;

import java.util.Locale;

import dev.allofus.fusioncore.R;

public class JniBridge {
    private static final String TAG = "JniBridge";

    private static final Handler handler = new Handler(Looper.getMainLooper());
    private static Runnable timerRunnable;

    public static void SetLoadingState(Activity activity, boolean enabled) {
        activity.runOnUiThread(() -> {
            View view = activity.findViewById(R.id.loader);
            if (view == null) {
                Log.e(TAG, "Could not set loading state, view is null!");
                return;
            }

            view.setVisibility(enabled ? View.VISIBLE : View.GONE);
            Log.i(TAG, "Set loading state to "+enabled);

            if (!enabled && timerRunnable != null) {
                handler.removeCallbacks(timerRunnable);
            }
        });
    }

    public static void SetLoadingText(Activity activity, String text) {
        String lower = text == null ? "" : text.toLowerCase(Locale.ROOT);
        final String userText = lower.contains("cpp2il") ? "首次准备游戏运行环境，请保持应用在前台"
                : lower.contains("interop") ? "正在生成游戏适配文件"
                : lower.contains("plugin") ? "正在加载集市增强插件"
                : lower.contains("fail") || lower.contains("error") ? "运行环境加载失败，请返回启动器查看诊断"
                : "正在准备运行环境";
        activity.runOnUiThread(() -> {
            TextView view = activity.findViewById(R.id.loadingText);
            if (view == null) {
                Log.e(TAG, "Could not set loading text, view is null!");
                return;
            }

            view.setText(userText);
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

                    view.setText(userText + timeString);

                    handler.postDelayed(this, 1000);
                }
            };
            handler.post(timerRunnable);
        });
    }
}
