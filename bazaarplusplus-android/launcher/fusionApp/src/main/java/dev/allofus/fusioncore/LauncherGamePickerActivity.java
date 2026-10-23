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
                failure = "暂时无法读取已安装应用，请稍后重试。";
            }
            List<LauncherGames.Choice> ready = found;
            String message = failure;
            runOnUiThread(() -> {
                if (isFinishing() || isDestroyed()) return;
                progress.setVisibility(View.GONE);
                if (message != null) status.setText(message);
                else if (ready.isEmpty()) status.setText("没有找到其他符合要求的已安装游戏。正版仍可在第一项选回。");
                else status.setText("另有 " + ready.size() + " 个符合要求的游戏。点选后回到首页再启动。");
                games.addAll(ready);
                adapter.notifyDataSetChanged();
            });
        });
    }

    private LauncherGames.Choice official() {
        PackageManager manager = getPackageManager();
        String version = "未安装";
        Drawable icon = manager.getDefaultActivityIcon();
        try {
            PackageInfo info = manager.getPackageInfo(LauncherRelease.GAME, 0);
            long code = info.getLongVersionCode();
            String name = info.versionName == null || info.versionName.isEmpty() ? "未知" : info.versionName;
            version = Utilities.formatVersionText(name, code);
            icon = manager.getApplicationIcon(LauncherRelease.GAME);
        } catch (Exception ignored) {}
        return new LauncherGames.Choice(LauncherRelease.GAME, "正版大巴扎（默认）", version, icon);
    }

    private void choose(String packageName) {
        if (!LauncherGames.select(this, packageName)) {
            Toast.makeText(this, "这款游戏目前不符合要求", Toast.LENGTH_LONG).show();
            return;
        }
        Toast.makeText(this, LauncherRelease.GAME.equals(packageName)
                ? "已恢复正版大巴扎。回到首页后点「启动游戏」。"
                : "已保存。回到首页后点「启动游戏」。", Toast.LENGTH_LONG).show();
        finish();
    }

    @Override protected void onDestroy() {
        scan.shutdownNow();
        super.onDestroy();
    }
}
