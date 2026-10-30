package dev.allofus.fusioncore;

import android.os.Bundle;
import android.os.Environment;
import android.content.Context;
import android.content.Intent;
import android.view.View;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.Collections;
import java.util.HashSet;
import java.util.Set;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import org.json.JSONArray;
import org.json.JSONObject;
import android.widget.CompoundButton;
import android.widget.RadioGroup;
import android.widget.TextView;
import android.widget.Toast;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.history.HistoryArtCache;
import dev.allofus.fusioncore.history.HistoryReader;
import dev.allofus.fusioncore.tools.Utilities;

/** Position and dock button visibility are shared with the in-game native settings rows. */
public final class LauncherSettingsActivity extends AppCompatActivity {
    static final String PREFERENCES = "bpp_settings";
    static final String ART_CLEANED_AT = "historyArtCleanedAt";
    static final String ART_CLEANED_BYTES = "historyArtCleanedBytes";
    static final String ART_SIZED_AT = "historyArtSizedAt";
    static final String POSITION = "previewPanelPosition";
    static final String HIDDEN = "hiddenDockButtons";
    static final String PANEL_STYLE = "catalogBuildPanelStyle";
    // Keys must match AndroidLauncherSettings in the plugin.
    static final String[] BUTTONS = { "collection", "builds", "previewMode", "holdPreview", "history" };
    private static final int[] SWITCHES = { R.id.dock_collection, R.id.dock_builds, R.id.dock_preview_mode,
            R.id.dock_hold_preview, R.id.dock_history };
    private boolean binding;
    private final ExecutorService cacheWork = Executors.newSingleThreadExecutor();
    private int cacheGeneration;

    private static File file(String packageName) {
        return new File(LauncherPreparation.bep(packageName), "bpp-launcher-settings.json");
    }

    private static JSONObject readFile(String packageName) {
        try {
            File file = file(packageName);
            if (!file.isFile()) return null;
            JSONObject settings = new JSONObject(new String(Files.readAllBytes(file.toPath()), StandardCharsets.UTF_8));
            return settings.optInt("schema") == 1 ? settings : null;
        } catch (Exception ignored) { return null; } // Before storage authorization, keep the saved launcher value.
    }

    static String readPosition(Context context) {
        return readPosition(context, LauncherGames.selected(context));
    }

    static String readPosition(Context context, String packageName) {
        JSONObject settings = readFile(packageName);
        String position = settings == null ? "" : settings.optString(POSITION);
        if ("left".equals(position) || "default".equals(position)) return position;
        return context.getSharedPreferences(PREFERENCES, 0).getString(POSITION, "default");
    }

    static Set<String> readHidden(Context context, String packageName) {
        JSONObject settings = readFile(packageName);
        JSONArray array = settings == null ? null : settings.optJSONArray(HIDDEN);
        Set<String> hidden = new HashSet<>();
        if (array == null) {
            hidden.addAll(context.getSharedPreferences(PREFERENCES, 0).getStringSet(HIDDEN, Collections.emptySet()));
        } else {
            for (int i = 0; i < array.length(); i++) hidden.add(array.optString(i));
        }
        hidden.retainAll(Arrays.asList(BUTTONS));
        return hidden;
    }

    static boolean readMarketPanels(Context context, String packageName) {
        JSONObject settings = readFile(packageName);
        String style = settings == null ? "" : settings.optString(PANEL_STYLE);
        if ("market".equals(style) || "classic".equals(style)) return "market".equals(style);
        return context.getSharedPreferences(PREFERENCES, 0).getBoolean(PANEL_STYLE, false);
    }

    /** Merge into the shared file so keys written by newer plugins survive. */
    static void write(Context context, String packageName, String position, Set<String> hidden,
            boolean marketPanels) throws Exception {
        JSONObject settings = readFile(packageName);
        if (settings == null) settings = new JSONObject();
        JSONArray array = new JSONArray();
        for (String button : BUTTONS) if (hidden.contains(button)) array.put(button);
        settings.put("schema", 1).put(POSITION, "left".equals(position) ? "left" : "default")
                .put(HIDDEN, array).put(PANEL_STYLE, marketPanels ? "market" : "classic");
        LauncherFiles.write(file(packageName), LauncherRelease.bytes(settings.toString()));
        context.getSharedPreferences(PREFERENCES, 0).edit().putString(POSITION, position)
                .putStringSet(HIDDEN, new HashSet<>(hidden)).putBoolean(PANEL_STYLE, marketPanels).commit();
    }

    static void sync(Context context, String packageName) throws Exception {
        write(context, packageName, readPosition(context, packageName), readHidden(context, packageName),
                readMarketPanels(context, packageName));
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
            showSettings();
            Toast.makeText(this, getString(R.string.bpp_official_target_restored), Toast.LENGTH_LONG).show();
        });
        showSettings();
        RadioGroup position = findViewById(R.id.preview_position);
        position.setOnCheckedChangeListener((group, id) -> save());
        for (int id : SWITCHES)
            ((CompoundButton) findViewById(id)).setOnCheckedChangeListener((button, checked) -> save());
        ((CompoundButton) findViewById(R.id.market_panels)).setOnCheckedChangeListener((button, checked) -> save());
        ((TextView) findViewById(R.id.cache_policy)).setText(getString(R.string.bpp_cache_policy,
                HistoryArtCache.format(HistoryArtCache.SIZE_BUDGET_BYTES),
                HistoryArtCache.format(HistoryArtCache.GROWTH_BYTES)));
        findViewById(R.id.cache_clear).setOnClickListener(v -> new AlertDialog.Builder(this)
                .setTitle(R.string.bpp_cache_clear_title)
                .setMessage(R.string.bpp_cache_clear_message)
                .setNegativeButton(android.R.string.cancel, null)
                .setPositiveButton(R.string.bpp_cache_clear_action, (dialog, which) -> clearArtworkCache())
                .show());
    }

    private File gameRoot() {
        return new File(new File(Environment.getExternalStorageDirectory(), BuildConfig.LAUNCHER_STORAGE_ROOT),
                LauncherGames.selected(this));
    }

    private void showArtworkCache() {
        int generation = ++cacheGeneration;
        TextView size = findViewById(R.id.cache_size);
        size.setText(R.string.bpp_cache_measuring);
        cacheWork.execute(() -> {
            String text;
            try {
                long bytes = HistoryArtCache.bytes(gameRoot());
                text = bytes == 0 ? getString(R.string.bpp_cache_empty)
                        : getString(R.string.bpp_cache_size, HistoryArtCache.format(bytes));
            } catch (Exception error) { text = getString(R.string.bpp_cache_unreadable); }
            String shown = text;
            runOnUiThread(() -> { if (generation == cacheGeneration && !isFinishing()) size.setText(shown); });
        });
    }

    private void clearArtworkCache() {
        View button = findViewById(R.id.cache_clear);
        button.setEnabled(false);
        cacheWork.execute(() -> {
            String message;
            try {
                File root = gameRoot();
                Set<String> retained = null;
                try {
                    retained = new HistoryReader(root, LauncherGames.selected(this), HistoryReader.Scope.launcher())
                            .retainedCardTemplates();
                } catch (Exception ignored) { retained = null; }
                long freed = HistoryArtCache.clean(root, retained);
                long left = HistoryArtCache.bytes(root);
                long recordedAt = System.currentTimeMillis();
                getSharedPreferences(PREFERENCES, 0).edit().putLong(ART_CLEANED_AT, recordedAt)
                        .putLong(ART_CLEANED_BYTES, left).putLong(ART_SIZED_AT, recordedAt).apply();
                message = freed <= 0 ? getString(R.string.bpp_cache_none)
                        : getString(R.string.bpp_cache_freed, HistoryArtCache.format(freed), HistoryArtCache.format(left));
            } catch (Exception error) { message = getString(R.string.bpp_cache_failed); }
            String shown = message;
            runOnUiThread(() -> {
                if (isFinishing()) return;
                button.setEnabled(true);
                showArtworkCache();
                Toast.makeText(this, shown, Toast.LENGTH_LONG).show();
            });
        });
    }

    private void save() {
        if (binding) return;
        String target = LauncherGames.selected(this);
        String position = ((RadioGroup) findViewById(R.id.preview_position)).getCheckedRadioButtonId()
                == R.id.preview_position_left ? "left" : "default";
        Set<String> hidden = new HashSet<>();
        for (int i = 0; i < BUTTONS.length; i++)
            if (!((CompoundButton) findViewById(SWITCHES[i])).isChecked()) hidden.add(BUTTONS[i]);
        try {
            write(this, target, position, hidden,
                    ((CompoundButton) findViewById(R.id.market_panels)).isChecked());
        } catch (Exception error) {
            Toast.makeText(this, getString(R.string.bpp_settings_save_failed), Toast.LENGTH_LONG).show();
            recreate();
        }
    }

    @Override protected void onResume() {
        super.onResume();
        showTarget();
        showSettings();
        showArtworkCache();
    }

    @Override protected void onDestroy() { cacheWork.shutdownNow(); super.onDestroy(); }

    private void showSettings() {
        String target = LauncherGames.selected(this);
        Set<String> hidden = readHidden(this, target);
        binding = true;
        ((RadioGroup) findViewById(R.id.preview_position)).check("left".equals(readPosition(this, target))
                ? R.id.preview_position_left : R.id.preview_position_default);
        for (int i = 0; i < BUTTONS.length; i++)
            ((CompoundButton) findViewById(SWITCHES[i])).setChecked(!hidden.contains(BUTTONS[i]));
        ((CompoundButton) findViewById(R.id.market_panels)).setChecked(readMarketPanels(this, target));
        binding = false;
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
