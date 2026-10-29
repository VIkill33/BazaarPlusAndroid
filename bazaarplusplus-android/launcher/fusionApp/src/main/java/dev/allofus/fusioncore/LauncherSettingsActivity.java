package dev.allofus.fusioncore;

import android.os.Bundle;
import android.content.Context;
import android.content.Intent;
import android.view.View;
import java.io.File;
import java.nio.file.Files;
import org.json.JSONObject;
import android.widget.RadioGroup;
import android.widget.TextView;
import android.widget.Toast;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.tools.Utilities;

/** Position is shared with the in-game native settings rows. */
public final class LauncherSettingsActivity extends AppCompatActivity {
    static final String PREFERENCES = "bpp_settings";
    static final String POSITION = "previewPanelPosition";
    private boolean bindingPosition;

    static String readPosition(Context context) {
        return readPosition(context, LauncherGames.selected(context));
    }

    static String readPosition(Context context, String packageName) {
        try {
            File file = new File(LauncherPreparation.bep(packageName), "bpp-launcher-settings.json");
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
        findViewById(R.id.target_choose).setOnClickListener(v ->
                startActivity(new Intent(this, LauncherGamePickerActivity.class)));
        findViewById(R.id.target_reset).setOnClickListener(v -> {
            LauncherGames.select(this, LauncherRelease.GAME);
            showTarget();
            showPosition();
            Toast.makeText(this, getString(R.string.bpp_official_target_restored), Toast.LENGTH_LONG).show();
        });
        RadioGroup position = findViewById(R.id.preview_position);
        showPosition();
        position.setOnCheckedChangeListener((group, id) -> {
            if (bindingPosition) return;
            String value = id == R.id.preview_position_left ? "left" : "default";
            try {
                JSONObject settings = new JSONObject().put("schema", 1).put(POSITION, value);
                LauncherFiles.write(new File(LauncherPreparation.bep(LauncherGames.selected(this)), "bpp-launcher-settings.json"),
                        LauncherRelease.bytes(settings.toString()));
                getSharedPreferences(PREFERENCES, 0).edit().putString(POSITION, value).commit();
            } catch (Exception error) {
                Toast.makeText(this, getString(R.string.bpp_settings_save_failed), Toast.LENGTH_LONG).show();
                recreate();
            }
        });
    }

    @Override protected void onResume() {
        super.onResume();
        showTarget();
        showPosition();
    }

    private void showPosition() {
        RadioGroup position = findViewById(R.id.preview_position);
        bindingPosition = true;
        position.check("left".equals(readPosition(this))
                ? R.id.preview_position_left : R.id.preview_position_default);
        bindingPosition = false;
    }

    private void showTarget() {
        TextView current = findViewById(R.id.target_current);
        boolean custom = LauncherGames.custom(this);
        String target = LauncherGames.selected(this);
        if (!custom) current.setText(getString(R.string.bpp_official_target_current));
        else {
            String label = target;
            try {
                label = LauncherGames.label(getPackageManager(), getPackageManager().getApplicationInfo(target, 0));
            } catch (Exception ignored) {}
            current.setText(getString(R.string.bpp_current_target, label, target));
        }
        findViewById(R.id.target_reset).setVisibility(custom ? View.VISIBLE : View.GONE);
    }
}
