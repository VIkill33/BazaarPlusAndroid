package dev.allofus.fusioncore;

import android.os.Bundle;
import android.os.Environment;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.ImageButton;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import dev.allofus.fusioncore.history.HistoryReader;
import dev.allofus.fusioncore.history.HistoryArtwork;
import dev.allofus.fusioncore.history.PurchaseCardView;
import dev.allofus.fusioncore.tools.Utilities;
import java.io.File;
import java.text.DateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import org.json.JSONArray;
import org.json.JSONObject;

/** Read-only statistics for the selected game's local purchase records. */
public final class LauncherPurchaseActivity extends AppCompatActivity {
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private HistoryReader reader;
    private HistoryArtwork artwork;
    private String target, hero = "", mode = "ranking", filter = "all";
    private static final String[] FILTERS = {"all", "3d", "7d", "Small", "Medium", "Large"};
    private static final String[] FILTER_LABELS = {"全部", "近三日", "近七日", "小型", "中型", "大型"};
    private final Button[] filterButtons = new Button[FILTERS.length];
    private int offset, requestVersion, restoreItemIndex = -1, restoreTop;
    private long through, rankMaximum;
    private boolean singleColumn;
    private boolean busy, more;
    private JSONArray items = new JSONArray();
    private ListView list;
    private TextView status, summary, page, totals, period;
    private View paging;
    private ImageButton refresh;
    private Button heroButton, rankingTab, recentTab, previous, next, retry;
    private final BaseAdapter adapter = new BaseAdapter() {
        @Override public int getCount() {
            if (!mode.equals("ranking") || singleColumn) return items.length();
            int lead = offset == 0 && items.length() > 0 ? 1 : 0;
            return lead + (items.length() - lead + 1) / 2;
        }
        private int index(int position) { return itemIndexForRow(position); }
        @Override public Object getItem(int position) { return items.optJSONObject(index(position)); }
        @Override public long getItemId(int position) { return position; }
        @Override public boolean areAllItemsEnabled() { return false; }
        @Override public boolean isEnabled(int position) { return false; }
        @Override public View getView(int position, View recycled, ViewGroup parent) {
            int first = index(position);
            LinearLayout row = new LinearLayout(LauncherPurchaseActivity.this);
            row.setPadding(dp(12), dp(6), dp(12), dp(6));
            boolean featured = mode.equals("ranking") && offset == 0 && first == 0;
            row.addView(tile(first, featured), new LinearLayout.LayoutParams(0, -2, 1));
            if (mode.equals("ranking") && !singleColumn && !featured) {
                View gap = new View(LauncherPurchaseActivity.this); row.addView(gap, new LinearLayout.LayoutParams(dp(8), 1));
                row.addView(first + 1 < items.length() ? tile(first + 1, false) : new View(LauncherPurchaseActivity.this),
                    new LinearLayout.LayoutParams(0, -2, 1));
            }
            return row;
        }
    };

    private int itemIndexForRow(int position) {
        if (!mode.equals("ranking") || singleColumn) return position;
        return offset == 0 ? position == 0 ? 0 : 1 + (position - 1) * 2 : position * 2;
    }

    private View tile(int index, boolean featured) {
        JSONObject item = items.optJSONObject(index);
        return new PurchaseCardView(this, artwork, item, mode.equals("ranking") ? offset + index + 1 : 0,
            rankMaximum, featured, () -> showDetails(item));
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        target = state == null ? getIntent().getStringExtra(LauncherHistoryActivity.TARGET) : state.getString("target");
        if (target == null || !target.matches("[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+")) { finish(); return; }
        singleColumn = getResources().getConfiguration().screenWidthDp < 340 || getResources().getConfiguration().fontScale >= 1.3f;
        through = state == null ? System.currentTimeMillis() : state.getLong("through", System.currentTimeMillis());
        if (state != null) {
            hero = state.getString("hero", ""); mode = state.getString("mode", "ranking"); offset = state.getInt("offset");
            rankMaximum = state.getLong("rankMaximum");
            filter = state.getString("filter", "all");
            restoreItemIndex = state.getInt("itemIndex", -1); restoreTop = state.getInt("top");
        }
        File root = new File(new File(Environment.getExternalStorageDirectory(), BuildConfig.LAUNCHER_STORAGE_ROOT), target);
        reader = new HistoryReader(root, target, HistoryReader.Scope.launcher());
        artwork = new HistoryArtwork(root);
        buildUi(); load();
    }

    private void buildUi() {
        LinearLayout root = new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(getColor(R.color.bpp_background)); Utilities.applyWindowInsets(root, 0); setContentView(root);
        list = new ListView(this); list.setDivider(new ColorDrawable(android.graphics.Color.TRANSPARENT));
        list.setDividerHeight(0); list.setClipToPadding(false);
        list.setItemsCanFocus(true); root.addView(list, new LinearLayout.LayoutParams(-1, -1));
        LinearLayout header = column();
        FrameLayout masthead = new FrameLayout(this);
        ImageView backdrop = new ImageView(this); backdrop.setImageResource(R.drawable.bpp_market_background);
        backdrop.setScaleType(ImageView.ScaleType.MATRIX);
        // The portrait homepage asset has a dark lower half; retain its lanterns and skyline in this short masthead.
        backdrop.addOnLayoutChangeListener((view, l, t, r, b, oldL, oldT, oldR, oldB) -> {
            android.graphics.drawable.Drawable drawable = backdrop.getDrawable();
            float scale = Math.max((float)(r - l) / drawable.getIntrinsicWidth(), (float)(b - t) / drawable.getIntrinsicHeight());
            android.graphics.Matrix matrix = new android.graphics.Matrix(); matrix.setScale(scale, scale);
            float overflow = Math.max(0, drawable.getIntrinsicHeight() * scale - (b - t));
            matrix.postTranslate(((r - l) - drawable.getIntrinsicWidth() * scale) / 2,
                -Math.min(overflow, drawable.getIntrinsicHeight() * scale * .14f));
            backdrop.setImageMatrix(matrix);
        });
        backdrop.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        masthead.addView(backdrop, new FrameLayout.LayoutParams(-1, -1));
        View shade = new View(this); shade.setBackgroundColor(getColor(R.color.bpp_purchase_scrim));
        masthead.addView(shade, new FrameLayout.LayoutParams(-1, -1));
        LinearLayout heading = column();
        LinearLayout bar = new LinearLayout(this); bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setPadding(dp(8), 0, dp(8), 0);
        bar.addView(iconButton(R.drawable.ic_back, "返回", this::finish));
        TextView title = text(24); title.setText("购牌统计"); title.setTypeface(null, Typeface.BOLD);
        title.setGravity(Gravity.CENTER); title.setAccessibilityHeading(true); title.setPadding(0, 0, 0, 0);
        bar.addView(title, new LinearLayout.LayoutParams(0, -2, 1));
        refresh = iconButton(R.drawable.bpp_refresh, "刷新", () -> {
            offset = 0; rankMaximum = 0; restoreItemIndex = -1; restoreTop = 0;
            through = System.currentTimeMillis(); load();
        }); bar.addView(refresh); heading.addView(bar); heading.addView(divider());
        masthead.addView(heading, new FrameLayout.LayoutParams(-1, -2, Gravity.CENTER));
        header.addView(masthead, new LinearLayout.LayoutParams(-1, dp(Math.max(104, (int)Math.ceil(72 * getResources().getConfiguration().fontScale)))));

        LinearLayout controls = new LinearLayout(this);
        controls.setOrientation(singleColumn ? LinearLayout.VERTICAL : LinearLayout.HORIZONTAL);
        controls.setGravity(Gravity.CENTER_VERTICAL); controls.setPadding(dp(12), dp(10), dp(12), 0);
        heroButton = button("全部英雄", this::chooseHero);
        slim(heroButton, 48);
        controls.addView(heroButton, singleColumn ? new LinearLayout.LayoutParams(-1, -2)
            : new LinearLayout.LayoutParams(0, -2, 1));
        summary = text(14); summary.setTextColor(getColor(R.color.bpp_accent)); summary.setGravity(singleColumn ? Gravity.START : Gravity.END);
        summary.setPadding(dp(12), dp(4), dp(12), dp(4));
        controls.addView(summary, singleColumn ? new LinearLayout.LayoutParams(-1, -2)
            : new LinearLayout.LayoutParams(0, -2, 1)); header.addView(controls);

        LinearLayout filters = new LinearLayout(this);
        filters.setPadding(dp(12), dp(4), dp(12), dp(8));
        for (int i = 0; i < FILTERS.length; i++) {
            final int index = i;
            com.google.android.material.button.MaterialButton capsule =
                (com.google.android.material.button.MaterialButton)button(FILTER_LABELS[i], () -> chooseFilter(FILTERS[index]));
            capsule.setCornerRadius(dp(12)); capsule.setTextSize(11);
            capsule.setMinHeight(0); capsule.setMinimumHeight(0);
            capsule.setMinWidth(0); capsule.setMinimumWidth(0);
            capsule.setInsetTop(0); capsule.setInsetBottom(0);
            capsule.setPadding(dp(2), 0, dp(2), 0); capsule.setSingleLine(true);
            LinearLayout.LayoutParams cell = new LinearLayout.LayoutParams(0, dp(24), 1);
            if (i < FILTERS.length - 1) cell.setMarginEnd(dp(4));
            filters.addView(capsule, cell); filterButtons[i] = capsule;
        }
        header.addView(filters);

        LinearLayout tabs = new LinearLayout(this); tabs.setPadding(dp(12), 0, dp(12), dp(8));
        rankingTab = button("常买排行", () -> chooseMode("ranking"));
        recentTab = button("购买明细", () -> chooseMode("recent"));
        com.google.android.material.button.MaterialButton ranking = (com.google.android.material.button.MaterialButton)rankingTab;
        ranking.setIconResource(R.drawable.bpp_crown); ranking.setIconSize(dp(18)); ranking.setIconPadding(dp(6));
        slim(rankingTab, 44); slim(recentTab, 44);
        LinearLayout.LayoutParams left = new LinearLayout.LayoutParams(0, -2, 1); left.setMarginEnd(dp(8));
        tabs.addView(rankingTab, left); tabs.addView(recentTab, new LinearLayout.LayoutParams(0, -2, 1)); header.addView(tabs);
        status = text(13); status.setTextColor(getColor(R.color.bpp_muted));
        status.setPadding(dp(12), dp(2), dp(12), dp(6));
        status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE); header.addView(status);
        list.addHeaderView(header, null, false);

        LinearLayout footer = column(); footer.setPadding(dp(12), dp(8), dp(12), dp(12));
        footer.addView(divider());
        totals = text(15); totals.setGravity(Gravity.CENTER); footer.addView(totals);
        LinearLayout navigation = new LinearLayout(this); navigation.setGravity(Gravity.CENTER_VERTICAL);
        previous = button("上一页", () -> { offset = Math.max(0, offset - HistoryReader.PAGE_SIZE); load(); });
        next = button("下一页", () -> { offset += HistoryReader.PAGE_SIZE; load(); });
        slim(previous, 48); slim(next, 48);
        page = text(14); page.setGravity(Gravity.CENTER);
        navigation.addView(previous, new LinearLayout.LayoutParams(0, -2, 1));
        navigation.addView(page, new LinearLayout.LayoutParams(0, -2, 1));
        navigation.addView(next, new LinearLayout.LayoutParams(0, -2, 1)); footer.addView(navigation); paging = navigation;
        retry = button("重试", this::load); footer.addView(retry);
        period = text(12); period.setTextColor(getColor(R.color.bpp_muted)); period.setGravity(Gravity.CENTER); footer.addView(period);
        TextView scope = text(12); scope.setGravity(Gravity.CENTER); scope.setTextColor(getColor(R.color.bpp_muted));
        scope.setText("本机全部档案 · 从新版插件启用后累计\n仅统计成功购买，包含物品与技能；奖励不计入。\n卡图来自本机历史缓存，仅供参考；未缓存的卡牌仍显示统计。"); footer.addView(scope);
        list.addFooterView(footer, null, false); list.setAdapter(adapter);
    }

    private void chooseFilter(String selected) {
        if (busy || filter.equals(selected)) return;
        filter = selected; offset = 0; rankMaximum = 0; restoreItemIndex = -1; restoreTop = 0; load();
    }

    private void chooseMode(String selected) {
        if (busy || mode.equals(selected)) return;
        mode = selected; offset = 0; rankMaximum = 0; restoreItemIndex = -1; restoreTop = 0; load();
    }
    private LinearLayout column() { LinearLayout view = new LinearLayout(this); view.setOrientation(LinearLayout.VERTICAL); return view; }
    private ImageView divider() {
        ImageView view = new ImageView(this); view.setImageResource(R.drawable.bpp_divider);
        view.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        view.setLayoutParams(new LinearLayout.LayoutParams(-1, dp(16))); return view;
    }
    private ImageButton iconButton(int resource, String label, Runnable action) {
        ImageButton view = new ImageButton(this); view.setImageResource(resource); view.setContentDescription(label);
        view.setImageTintList(ColorStateList.valueOf(getColor(R.color.bpp_text)));
        view.setBackgroundColor(android.graphics.Color.TRANSPARENT); view.setPadding(dp(12), dp(12), dp(12), dp(12));
        view.setLayoutParams(new LinearLayout.LayoutParams(dp(48), dp(48))); view.setOnClickListener(v -> action.run()); return view;
    }

    private void chooseHero() {
        String[] heroes = {"全部英雄", "Vanessa", "Pygmalien", "Dooley", "Mak", "Stelle", "Jules", "Karnok", "TheDragons"};
        new AlertDialog.Builder(this).setTitle("按英雄筛选").setItems(heroes, (dialog, index) -> {
            hero = index == 0 ? "" : heroes[index]; offset = 0; rankMaximum = 0; restoreItemIndex = -1; restoreTop = 0; load();
        }).setNegativeButton("取消", null).show();
    }

    private void load() {
        if (busy) return;
        busy = true; final int version = ++requestVersion;
        paging.setVisibility(View.GONE);
        status.setText("正在读取…"); status.setVisibility(View.VISIBLE); summary.setText(""); totals.setText(""); period.setText(""); retry.setVisibility(View.GONE); items = new JSONArray(); adapter.notifyDataSetChanged();
        heroButton.setText((hero.isEmpty() ? "全部英雄" : hero) + " ▾");
        updateControls();
        try {
            JSONObject query = new JSONObject().put("op", "purchases").put("hero", hero).put("view", mode).put("offset", offset).put("through", through).put("filter", filter);
            worker.execute(() -> {
                try { JSONObject result = reader.read(query); runOnUiThread(() -> complete(version, result, null)); }
                catch (Exception error) { runOnUiThread(() -> complete(version, null, error)); }
            });
        } catch (Exception error) { complete(version, null, error); }
    }

    private void complete(int version, JSONObject result, Exception error) {
        if (version != requestVersion || isFinishing() || isDestroyed()) return;
        busy = false; more = false; long totalRows = 0;
        if (error != null) status.setText("读取失败，请检查存储授权后重试。\n" + error.getClass().getSimpleName());
        else if (result.optString("state").equals("catalog_unavailable")) status.setText("本机卡牌目录不可用，暂时无法按体型筛选。可选择全部或时间筛选。");
        else if (result.optString("state").equals("newer_format")) status.setText("记录来自更新版本，请升级启动器。");
        else if (result.optString("state").equals("purchases_not_enabled")) status.setText("尚未启用购牌记录，请使用新版插件启动游戏。");
        else if (result.optString("state").equals("missing")) status.setText("暂无购牌记录，请启动游戏后购买卡牌。旧对局无法补录。");
        else if (result.optString("state").equals("ok")) {
            JSONObject totals = result.optJSONObject("summary");
            summary.setText(number(totals.optLong("totalPurchases")) + " 次购买 · " + number(totals.optLong("totalGold")) + " 金币");
            this.totals.setText(number(totals.optLong("uniqueCards")) + " 种卡牌 · " + number(totals.optLong("purchaseRuns")) + " 局记录");
            items = result.optJSONArray("items"); more = result.optBoolean("hasMore");
            totalRows = totals.optLong(mode.equals("ranking") ? "uniqueCards" : "totalPurchases");
            if (mode.equals("ranking") && items.length() > 0 && (offset == 0 || rankMaximum == 0))
                rankMaximum = items.optJSONObject(0).optLong("purchaseCount");
            status.setText(totals.optLong("totalPurchases") == 0 ? "当前范围暂无购买记录，旧对局无法补录。" : "");
            if (totals.optLong("totalPurchases") > 0)
                period.setText("记录时间：" + time(totals.optLong("firstPurchase")) + " — " + time(totals.optLong("lastPurchase")));

        } else status.setText("暂时无法读取购牌记录，请重试。");
        status.setVisibility(status.length() == 0 ? View.GONE : View.VISIBLE);
        retry.setVisibility(error != null || result != null && !result.optString("state").equals("ok") ? View.VISIBLE : View.GONE);
        page.setText((offset / HistoryReader.PAGE_SIZE + 1) + " / " + Math.max(1, (totalRows + HistoryReader.PAGE_SIZE - 1) / HistoryReader.PAGE_SIZE));
        paging.setVisibility(totalRows > 0 ? View.VISIBLE : View.GONE);
        adapter.notifyDataSetChanged(); updateControls();
        int position = 0, top = restoreTop;
        if (restoreItemIndex == -2) position = adapter.getCount() + list.getHeaderViewsCount();
        else if (restoreItemIndex >= 0 && items.length() > 0) {
            int item = Math.min(restoreItemIndex, items.length() - 1);
            int row = !mode.equals("ranking") || singleColumn ? item
                : offset == 0 ? item == 0 ? 0 : 1 + (item - 1) / 2 : item / 2;
            position = row + list.getHeaderViewsCount();
        }
        int restoredPosition = position;
        list.post(() -> { if (!isDestroyed() && version == requestVersion) list.setSelectionFromTop(restoredPosition, top); });
        restoreItemIndex = -1; restoreTop = 0;
    }

    private void updateControls() {
        heroButton.setEnabled(!busy); rankingTab.setEnabled(!busy); recentTab.setEnabled(!busy); refresh.setEnabled(!busy); retry.setEnabled(!busy);
        for (int i = 0; i < filterButtons.length; i++) {
            filterButtons[i].setEnabled(!busy); tabState(filterButtons[i], filter.equals(FILTERS[i]));
        }
        tabState(rankingTab, mode.equals("ranking")); tabState(recentTab, mode.equals("recent"));
        previous.setEnabled(!busy && offset > 0);
        next.setEnabled(!busy && more && offset < Integer.MAX_VALUE - 2 * HistoryReader.PAGE_SIZE);
    }
    private void tabState(Button tab, boolean selected) {
        tab.setSelected(selected);
        int ink = getColor(selected ? R.color.bpp_background : R.color.bpp_text);
        tab.setTextColor(ink);
        com.google.android.material.button.MaterialButton material = (com.google.android.material.button.MaterialButton)tab;
        material.setBackgroundTintList(ColorStateList.valueOf(getColor(selected ? R.color.bpp_accent : R.color.bpp_surface)));
        material.setIconTint(ColorStateList.valueOf(ink));
    }
    private void slim(Button button, int height) {
        button.setMinHeight(dp(height)); button.setMinimumHeight(dp(height));
        ((com.google.android.material.button.MaterialButton)button).setInsetTop(0);
        ((com.google.android.material.button.MaterialButton)button).setInsetBottom(0);
    }

    private void showDetails(JSONObject item) {
        if (item == null) return;
        String message = mode.equals("ranking")
            ? "购买次数：" + item.optLong("purchaseCount") + "\n总花费：" + item.optLong("gold") + " 金币\n平均单价："
                + String.format(Locale.getDefault(), "%.1f", (double)item.optLong("gold") / Math.max(1, item.optLong("purchaseCount")))
                + " 金币\n出现于 " + item.optLong("runCount") + " 局\n最近购买：" + time(item.optLong("lastPurchasedAt"))
            : "品质：" + tier(item.optString("tier")) + "\n价格：" + item.optLong("price") + " 金币\n英雄：" + item.optString("hero", "未记录")
                + "\n第 " + item.optLong("day") + " 天 · 第 " + item.optLong("hour") + " 小时\n购买时间：" + time(item.optLong("purchasedAt"));
        new AlertDialog.Builder(this).setTitle(item.optString("name")).setMessage(message).setPositiveButton("关闭", null).show();
    }

    @Override protected void onSaveInstanceState(Bundle state) {
        state.putString("filter", filter);
        state.putLong("rankMaximum", rankMaximum);
        state.putString("target", target); state.putString("hero", hero); state.putString("mode", mode); state.putInt("offset", offset); state.putLong("through", through);
        if (list != null) {
            int row = list.getFirstVisiblePosition() - list.getHeaderViewsCount();
            // Store logical card identity: font/width changes can regroup the same items into different rows.
            int itemIndex = row < 0 ? -1 : row >= adapter.getCount() ? -2 : itemIndexForRow(row);
            state.putInt("itemIndex", busy ? restoreItemIndex : itemIndex);
            View first = list.getChildAt(0);
            state.putInt("top", busy ? restoreTop : first == null ? 0 : first.getTop());
        }
        super.onSaveInstanceState(state);
    }
    @Override protected void onDestroy() { ++requestVersion; worker.shutdownNow(); if (artwork != null) artwork.close(); super.onDestroy(); }
    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private TextView text(int size) { TextView view = new TextView(this); view.setTextSize(size); view.setTextColor(getColor(R.color.bpp_text)); view.setPadding(dp(12), dp(8), dp(12), dp(8)); return view; }
    private Button button(String label, Runnable action) {
        com.google.android.material.button.MaterialButton view = new com.google.android.material.button.MaterialButton(this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        view.setCornerRadius(dp(8)); view.setStrokeColor(ColorStateList.valueOf(getColor(R.color.bpp_border)));
        view.setTextColor(getColor(R.color.bpp_text)); view.setText(label); view.setAllCaps(false); view.setMinHeight(dp(48)); view.setOnClickListener(v -> action.run()); return view;
    }
    private static String number(long value) { return String.format(Locale.getDefault(), "%,d", value); }
    private static String time(long millis) { return DateFormat.getDateTimeInstance(DateFormat.SHORT, DateFormat.SHORT).format(new Date(millis)); }
    private static String tier(String value) {
        switch (value) { case "Bronze": return "青铜"; case "Silver": return "白银"; case "Gold": return "黄金"; case "Diamond": return "钻石"; case "Legendary": return "传奇"; default: return value; }
    }
}
