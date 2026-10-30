package dev.allofus.fusioncore;

import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
import android.widget.ImageView;
import android.widget.ListView;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import dev.allofus.fusioncore.tools.Utilities;

/** Saves a launch target. The home screen performs the actual launch. */
public final class LauncherGamePickerActivity extends AppCompatActivity {
    private final List<LauncherGames.Choice> games = new ArrayList<>();
    private final ExecutorService scan = Executors.newSingleThreadExecutor();
    private ArrayAdapter<LauncherGames.Choice> adapter;
    private String current;

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        setContentView(R.layout.activity_launcher_games);
        Utilities.applyWindowInsets(findViewById(R.id.games_root), 0);
        findViewById(R.id.games_back).setOnClickListener(v -> finish());
        current = LauncherGames.selected(this);
        games.add(official());
        ListView list = findViewById(R.id.games_list);
        adapter = new ArrayAdapter<>(this, R.layout.item_launcher_game, games) {
            @NonNull @Override public View getView(int position, View convert, @NonNull ViewGroup parent) {
                View row = convert == null
                        ? LayoutInflater.from(getContext()).inflate(R.layout.item_launcher_game, parent, false)
                        : convert;
                LauncherGames.Choice choice = getItem(position);
                if (choice == null) return row;
                ImageView icon = row.findViewById(R.id.game_icon);
                icon.setImageDrawable(choice.icon != null ? choice.icon : getPackageManager().getDefaultActivityIcon());
                ((TextView) row.findViewById(R.id.game_name)).setText(choice.label);
                ((TextView) row.findViewById(R.id.game_package)).setText(choice.packageName);
                ((TextView) row.findViewById(R.id.game_version)).setText(choice.version);
                row.findViewById(R.id.game_current).setVisibility(
                        choice.packageName.equals(current) ? View.VISIBLE : View.GONE);
                row.setOnClickListener(v -> choose(choice.packageName));
                return row;
            }
        };
        list.setAdapter(adapter);
        ProgressBar progress = findViewById(R.id.games_progress);
        TextView status = findViewById(R.id.games_status);
        scan.execute(() -> {
            List<LauncherGames.Choice> found;
            String failure = null;
            try { found = LauncherGames.installed(this); }
            catch (Exception error) {
                found = List.of();
                failure = getString(R.string.bpp_games_scan_failed);
            }
            List<LauncherGames.Choice> ready = found;
            String message = failure;
            runOnUiThread(() -> {
                if (isFinishing() || isDestroyed()) return;
                progress.setVisibility(View.GONE);
                if (message != null) status.setText(message);
                else if (ready.isEmpty()) status.setText(getString(R.string.bpp_games_empty));
                else status.setText(getString(R.string.bpp_game_count, ready.size()));
                games.addAll(ready);
                adapter.notifyDataSetChanged();
            });
        });
    }

    private LauncherGames.Choice official() {
        PackageManager manager = getPackageManager();
        String version = getString(R.string.bpp_game_not_installed);
        Drawable icon = manager.getDefaultActivityIcon();
        try {
            PackageInfo info = manager.getPackageInfo(LauncherRelease.GAME, 0);
            long code = info.getLongVersionCode();
            String name = info.versionName == null || info.versionName.isEmpty() ? getString(R.string.bpp_unknown) : info.versionName;
            version = Utilities.formatVersionText(name, code);
            icon = manager.getApplicationIcon(LauncherRelease.GAME);
        } catch (Exception ignored) {}
        return new LauncherGames.Choice(LauncherRelease.GAME, getString(R.string.bpp_official_game_label), version, icon);
    }

    private void choose(String packageName) {
        if (!LauncherGames.select(this, packageName)) {
            Toast.makeText(this, getString(R.string.bpp_game_ineligible), Toast.LENGTH_LONG).show();
            return;
        }
        Toast.makeText(this, LauncherRelease.GAME.equals(packageName)
                ? getString(R.string.bpp_official_target_restored)
                : getString(R.string.bpp_game_selection_saved), Toast.LENGTH_LONG).show();
        finish();
    }

    @Override protected void onDestroy() {
        scan.shutdownNow();
        super.onDestroy();
    }
}
