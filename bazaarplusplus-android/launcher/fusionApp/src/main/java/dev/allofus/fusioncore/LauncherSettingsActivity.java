package dev.allofus.fusioncore;

import android.os.Bundle;
import android.content.Context;
import java.io.File;
import java.nio.file.Files;
import org.json.JSONObject;
import android.widget.RadioGroup;
import android.widget.Toast;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.tools.Utilities;

/** Position is shared with the in-game native settings rows. */
public final class LauncherSettingsActivity extends AppCompatActivity {
    static final String PREFERENCES = "bpp_settings";
    static final String POSITION = "previewPanelPosition";

    static String readPosition(Context context) {
        try {
            File file = new File(LauncherPreparation.bep(), "bpp-launcher-settings.json");
            if (file.isFile()) {
                JSONObject settings = new JSONObject(new String(Files.readAllBytes(file.toPath()), java.nio.charset.StandardCharsets.UTF_8));
                if (settings.optInt("schema") == 1) {
                    String position = settings.optString(POSITION);
                    if ("left".equals(position) || "default".equals(position)) return position;
                }
            }
        } catch (Exception ignored) { /* Before storage authorization, keep the saved launcher value. */ }
        return context.getSharedPreferences(PREFERENCES, 0).getString(POSITION, "default");
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        setContentView(R.layout.activity_launcher_settings);
        Utilities.applyWindowInsets(findViewById(R.id.settings_scroll), 0);
        findViewById(R.id.settings_back).setOnClickListener(v -> finish());
        RadioGroup position = findViewById(R.id.preview_position);
        position.check("left".equals(readPosition(this))
                ? R.id.preview_position_left : R.id.preview_position_default);
        position.setOnCheckedChangeListener((group, id) -> {
            String value = id == R.id.preview_position_left ? "left" : "default";
            try {
                JSONObject settings = new JSONObject().put("schema", 1).put(POSITION, value);
                LauncherFiles.write(new File(LauncherPreparation.bep(), "bpp-launcher-settings.json"),
                        LauncherRelease.bytes(settings.toString()));
                getSharedPreferences(PREFERENCES, 0).edit().putString(POSITION, value).commit();
            } catch (Exception error) {
                Toast.makeText(this, "保存失败，请重试", Toast.LENGTH_LONG).show();
                recreate();
            }
        });
    }
}
