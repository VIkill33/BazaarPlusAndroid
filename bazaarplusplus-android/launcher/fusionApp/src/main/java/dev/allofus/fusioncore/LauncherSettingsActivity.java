package dev.allofus.fusioncore;

import android.os.Bundle;
import android.widget.RadioGroup;
import android.widget.Toast;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.tools.Utilities;

/** Launcher-owned preferences are exported to the plugin before each game launch. */
public final class LauncherSettingsActivity extends AppCompatActivity {
    static final String PREFERENCES = "bpp_settings";
    static final String POSITION = "previewPanelPosition";

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        setContentView(R.layout.activity_launcher_settings);
        Utilities.applyWindowInsets(findViewById(R.id.settings_scroll), 0);
        findViewById(R.id.settings_back).setOnClickListener(v -> finish());
        RadioGroup position = findViewById(R.id.preview_position);
        position.check("left".equals(getSharedPreferences(PREFERENCES, 0).getString(POSITION, "default"))
                ? R.id.preview_position_left : R.id.preview_position_default);
        position.setOnCheckedChangeListener((group, id) -> {
            String value = id == R.id.preview_position_left ? "left" : "default";
            if (!getSharedPreferences(PREFERENCES, 0).edit().putString(POSITION, value).commit()) {
                Toast.makeText(this, "保存失败，请重试", Toast.LENGTH_LONG).show();
                recreate();
            }
        });
    }
}
