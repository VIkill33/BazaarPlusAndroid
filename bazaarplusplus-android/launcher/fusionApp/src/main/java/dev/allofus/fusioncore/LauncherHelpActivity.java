package dev.allofus.fusioncore;

import android.os.Bundle;
import android.view.View;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.tools.Utilities;

/** Offline first-install guide; preparation and permission requests stay on the home screen. */
public final class LauncherHelpActivity extends AppCompatActivity {
    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        setContentView(R.layout.activity_launcher_help);
        View scroll = findViewById(R.id.help_scroll);
        Utilities.applyWindowInsets(scroll, 0);
        View body = findViewById(R.id.help_body);
        scroll.addOnLayoutChangeListener((v, left, top, right, bottom, oldLeft, oldTop, oldRight, oldBottom) -> {
            int width = Math.min(right - left - scroll.getPaddingLeft() - scroll.getPaddingRight(),
                    Math.round(640 * getResources().getDisplayMetrics().density));
            if (width > 0 && body.getLayoutParams().width != width) {
                android.view.ViewGroup.LayoutParams params = body.getLayoutParams();
                params.width = width;
                body.setLayoutParams(params);
            }
        });
        findViewById(R.id.help_back).setOnClickListener(v -> finish());
        findViewById(R.id.help_start).setOnClickListener(v -> finish());
    }
}
