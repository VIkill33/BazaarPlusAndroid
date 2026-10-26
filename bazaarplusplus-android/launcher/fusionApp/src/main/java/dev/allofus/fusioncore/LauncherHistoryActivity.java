package dev.allofus.fusioncore;

import android.os.Bundle;
import android.os.Environment;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.ImageView;
import android.widget.FrameLayout;
import dev.allofus.fusioncore.history.HistoryBattleView;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.text.TextUtils;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.style.ForegroundColorSpan;
import android.text.style.RelativeSizeSpan;
import android.view.Gravity;
import android.widget.ImageButton;
import dev.allofus.fusioncore.history.HistoryArtwork;
import dev.allofus.fusioncore.history.HistoryPresentation;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.activity.OnBackPressedCallback;
import dev.allofus.fusioncore.history.HistoryReader;
import dev.allofus.fusioncore.tools.Utilities;
import java.io.File;
import java.text.DateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.Iterator;
import java.util.List;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import org.json.JSONArray;
import org.json.JSONObject;

/** Native Android browser. The selected target is fixed for the lifetime of this screen. */
public final class LauncherHistoryActivity extends AppCompatActivity {
    public static final String TARGET = "historyTarget";
    private final ThreadPoolExecutor worker = new ThreadPoolExecutor(1, 1, 0, TimeUnit.MILLISECONDS, new LinkedBlockingQueue<>(1));
    private final Page runs = new Page(), battles = new Page();
    private HistoryReader reader;
    private HistoryArtwork artwork;
    private Button heroFilter;
    private String hero;
    private String target, screen = "runs", runId, battleId;
    private JSONObject run, battle;
    private TextView title, status, summary;
    private LinearLayout filterBar, runOverview;
    private FrameLayout masthead;
    private ListView list;
    private FrameLayout formationHost;
    private Button previous, next, retry;
    private int requestVersion;
    private boolean busy;
    private ArrayList<String> retryTokens;
    private static final class Page {
        JSONObject data;
        final ArrayList<String> tokens = new ArrayList<>();
        int position, top;
        Page() { tokens.add(""); }
        void save(Bundle state, String key) {
            state.putString(key + "Data", data == null ? null : data.toString());
            state.putStringArrayList(key + "Tokens", tokens);
            state.putInt(key + "Position", position); state.putInt(key + "Top", top);
        }
        void restore(Bundle state, String key) throws Exception {
            String saved = state.getString(key + "Data"); data = saved == null ? null : new JSONObject(saved);
            ArrayList<String> savedTokens = state.getStringArrayList(key + "Tokens");
            if (savedTokens != null && !savedTokens.isEmpty()) { tokens.clear(); tokens.addAll(savedTokens); }
            position = state.getInt(key + "Position"); top = state.getInt(key + "Top");
        }
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        target = state == null ? getIntent().getStringExtra(TARGET) : state.getString(TARGET);
        if (target == null || !target.matches("[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+")) { finish(); return; }
        // Utilities.getExternalFusionCoreDirectory creates directories; browsing must not use it.
        reader = new HistoryReader(new File(new File(Environment.getExternalStorageDirectory(), BuildConfig.LAUNCHER_STORAGE_ROOT), target),
                target, HistoryReader.Scope.launcher());
        artwork = new HistoryArtwork(new File(new File(Environment.getExternalStorageDirectory(), BuildConfig.LAUNCHER_STORAGE_ROOT), target));
        if (state != null) try {
            hero = state.getString("hero");
            screen = state.getString("screen", "runs"); runId = state.getString("runId"); battleId = state.getString("battleId");
            String savedRun = state.getString("run"); run = savedRun == null ? null : new JSONObject(savedRun);
            runs.restore(state, "runs"); battles.restore(state, "battles");
        } catch (Exception error) { screen = "runs"; runs.data = null; }
        buildUi(); render(); restorePosition();
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override public void handleOnBackPressed() { back(); }
        });
        if (screen.equals("battle") || page().data == null) load(false);
    }

    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private TextView text(int size) {
        TextView view = new TextView(this); view.setTextSize(size); view.setTextColor(getColor(R.color.bpp_text));
        view.setPadding(dp(12), dp(8), dp(12), dp(8)); return view;
    }
    private Button button(String label, Runnable action) {
        com.google.android.material.button.MaterialButton view = new com.google.android.material.button.MaterialButton(
            this, null, com.google.android.material.R.attr.materialButtonOutlinedStyle);
        view.setCornerRadius(dp(8));
        view.setText(label); view.setAllCaps(false); view.setMinHeight(dp(48)); view.setOnClickListener(v -> action.run()); return view;
    }
    private void buildUi() {
        LinearLayout root = new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(getColor(R.color.bpp_background)); Utilities.applyWindowInsets(root, 0); setContentView(root);
        masthead = new FrameLayout(this);
        ImageView backdrop = new ImageView(this); backdrop.setImageResource(R.drawable.bpp_market_background);
        backdrop.setScaleType(ImageView.ScaleType.CENTER_CROP); backdrop.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        masthead.addView(backdrop, new FrameLayout.LayoutParams(-1, -1));
        View shade = new View(this); shade.setBackgroundColor(getColor(R.color.bpp_history_scrim));
        masthead.addView(shade, new FrameLayout.LayoutParams(-1, -1));
        LinearLayout heading = new LinearLayout(this); heading.setOrientation(LinearLayout.VERTICAL);
        LinearLayout bar = new LinearLayout(this); bar.setGravity(Gravity.CENTER_VERTICAL); bar.setPadding(dp(8), dp(4), dp(8), 0);
        bar.addView(iconButton(R.drawable.ic_back, "返回", this::back)); title = text(24);
        title.setTypeface(Typeface.create("serif", Typeface.BOLD)); title.setGravity(Gravity.CENTER);
        title.setAccessibilityHeading(true);
        bar.addView(title, new LinearLayout.LayoutParams(0, -2, 1)); bar.addView(iconButton(R.drawable.bpp_refresh, "刷新", () -> load(true)));
        heading.addView(bar);
        ImageView rule = new ImageView(this); rule.setImageResource(R.drawable.bpp_divider);
        rule.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        LinearLayout.LayoutParams ruleSize = new LinearLayout.LayoutParams(dp(180), dp(16)); ruleSize.gravity = Gravity.CENTER_HORIZONTAL;
        heading.addView(rule, ruleSize); masthead.addView(heading, new FrameLayout.LayoutParams(-1, -2, Gravity.CENTER));
        root.addView(masthead, new LinearLayout.LayoutParams(-1, dp(104)));
        LinearLayout header = new LinearLayout(this); header.setOrientation(LinearLayout.VERTICAL);
        filterBar = new LinearLayout(this); filterBar.setGravity(Gravity.CENTER_VERTICAL);
        heroFilter = button("全部英雄", this::chooseHero); filterBar.addView(heroFilter);
        TextView scope = text(12); scope.setText("本机档案"); scope.setTextColor(getColor(R.color.bpp_muted)); scope.setGravity(Gravity.END);
        scope.setOnClickListener(v -> showRunInformation());
        filterBar.addView(scope, new LinearLayout.LayoutParams(0, -2, 1)); header.addView(filterBar);
        runOverview = new LinearLayout(this); runOverview.setOrientation(LinearLayout.VERTICAL); header.addView(runOverview);
        summary = text(12); summary.setTextColor(getColor(R.color.bpp_muted)); header.addView(summary);
        status = text(14); status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        status.setTextColor(getColor(R.color.bpp_accent)); root.addView(status);
        retry = button("重试读取", () -> load(false, retryTokens)); retry.setVisibility(View.GONE); root.addView(retry);
        list = new ListView(this); list.setDivider(new ColorDrawable(getColor(R.color.bpp_history_line))); list.setDividerHeight(dp(1));
        list.setPadding(dp(16), 0, dp(16), 0); list.setClipToPadding(false);
        list.addHeaderView(header, null, false);
        root.addView(list, new LinearLayout.LayoutParams(-1, 0, 1));
        formationHost = new FrameLayout(this); formationHost.setPadding(dp(16), 0, dp(16), dp(8));
        root.addView(formationHost, new LinearLayout.LayoutParams(-1, 0, 1));
        LinearLayout paging = new LinearLayout(this); paging.setPadding(dp(16), 0, dp(16), dp(4));
        previous = button("上一页", () -> movePage(false)); next = button("下一页", () -> movePage(true));
        paging.addView(previous, new LinearLayout.LayoutParams(0, -2, 1));
        LinearLayout.LayoutParams nextSize = new LinearLayout.LayoutParams(0, -2, 1); nextSize.setMarginStart(dp(8));
        paging.addView(next, nextSize); root.addView(paging);
        list.setOnItemClickListener((parent, view, position, id) -> { if (position > 0) select(position - 1); });
    }

    private ImageButton iconButton(int drawable, String label, Runnable action) {
        ImageButton view = new ImageButton(this); view.setImageResource(drawable);
        view.setImageTintList(android.content.res.ColorStateList.valueOf(getColor(R.color.bpp_text)));
        view.setContentDescription(label); view.setPadding(dp(12), dp(12), dp(12), dp(12));
        android.util.TypedValue background = new android.util.TypedValue();
        getTheme().resolveAttribute(android.R.attr.selectableItemBackgroundBorderless, background, true);
        view.setBackgroundResource(background.resourceId); view.setLayoutParams(new LinearLayout.LayoutParams(dp(48), dp(48)));
        view.setOnClickListener(v -> action.run()); return view;
    }

    private void showRunInformation() {
        new AlertDialog.Builder(this).setTitle(screen.equals("runs") ? "本机档案" : "对局信息")
            .setMessage("目标游戏：" + target + "\n本机全部档案 · 仅显示已保存数据"
                + (run == null || screen.equals("runs") ? "" : "\n\n" + runLabel(run)
                    + "\n最后观察：" + time(run, "lastObserved") + "\n结束时间：" + time(run, "endedAt")))
            .setPositiveButton("关闭", null).show();
    }

    private Page page() { return screen.equals("runs") ? runs : battles; }
    private void rememberPosition() {
        if (screen.equals("battle")) return;
        if (list == null || list.getAdapter() == null || list.getAdapter().getCount() == 0) return;
        if (page().data == null) return;
        int position = list.getFirstVisiblePosition(), top = list.getChildAt(0) == null ? 0 : list.getChildAt(0).getTop();
        page().position = position; page().top = top;
    }
    private void restorePosition() {
        if (screen.equals("battle")) return;
        int position = page().position;
        int top = page().top;
        list.setSelectionFromTop(position, top);
    }
    private void load(boolean refresh) {
        load(refresh, null);
    }
    private void load(boolean refresh, ArrayList<String> requestedTokens) {
        rememberPosition();
        final int version = ++requestVersion;
        final String requestedScreen = screen, selectedRun = runId, selectedBattle = battleId, selectedHero = hero;
        final Page selectedPage = page();
        final ArrayList<String> tokens = requestedTokens == null ? new ArrayList<>(selectedPage.tokens) : requestedTokens;
        final String token = tokens.get(tokens.size() - 1);
        retryTokens = requestedTokens == null ? null : new ArrayList<>(tokens);
        busy = true; status.setVisibility(View.VISIBLE); status.setText("正在读取历史…"); retry.setVisibility(View.GONE); updatePaging();
        // At most one query runs and one waits. Repeated taps replace the waiting request.
        worker.getQueue().clear();
        worker.execute(() -> {
            JSONObject response = null, runResponse = null; String failure = null;
            try {
                JSONObject request = new JSONObject().put("op", requestedScreen.equals("runs") ? "runs" : requestedScreen.equals("battles") ? "battles" : "battle");
                if (requestedScreen.equals("runs") && selectedHero != null) request.put("hero", selectedHero);
                if (!requestedScreen.equals("runs")) request.put("runId", selectedRun);
                if (requestedScreen.equals("battle")) request.put("battleId", selectedBattle);
                else {
                    if (!token.isEmpty()) request.put("after", new JSONObject(token));
                }
                response = reader.read(request);
                if (!requestedScreen.equals("runs")) runResponse = reader.read(new JSONObject().put("op", "run").put("runId", selectedRun));
            } catch (Exception error) { failure = error.getMessage(); }
            final JSONObject result = response, currentRun = runResponse; final String error = failure;
            runOnUiThread(() -> {
                if (isDestroyed() || isFinishing() || version != requestVersion) return;
                busy = false;
                if (error != null) {
                    status.setText("读取失败，原文件已保留。可重试。\n" + error); retry.setVisibility(View.VISIBLE); updatePaging(); return;
                }
                String state = result.optString("state");
                if (!state.equals("ok")) {
                    status.setText(state.equals("missing") ? "尚无历史记录。启用插件后将保存可靠观察到的对局。"
                        : state.equals("newer_format") ? "历史格式较新，请升级启动器后重试。" : "所选记录不存在，请返回列表刷新。");
                    retry.setVisibility(View.VISIBLE);
                    updatePaging(); return;
                }
                if (currentRun != null && currentRun.has("run")) run = currentRun.optJSONObject("run");
                if (requestedScreen.equals("battle")) battle = result.optJSONObject("battle");
                else {
                    selectedPage.data = result;
                    if (requestedTokens != null) {
                        selectedPage.tokens.clear(); selectedPage.tokens.addAll(tokens);
                        selectedPage.position = 0; selectedPage.top = 0;
                    }
                }
                render(); restorePosition();
                status.setText(!requestedScreen.equals("battle") && result.optJSONArray("items").length() == 0 ? "此页没有记录。" : "");
                status.setVisibility(status.length() == 0 ? View.GONE : View.VISIBLE);
            });
        });
    }

    private void movePage(boolean forward) {
        if (busy || screen.equals("battle")) return;
        Page current = page();
        ArrayList<String> tokens = new ArrayList<>(current.tokens);
        if (forward) {
            if (current.data == null || !current.data.optBoolean("hasMore")) return;
            tokens.add(current.data.optJSONObject("after").toString());
        } else if (tokens.size() > 1) tokens.remove(tokens.size() - 1); else return;
        load(false, tokens);
    }
    private void updatePaging() {
        boolean detail = screen.equals("battle"); previous.setVisibility(detail ? View.GONE : View.VISIBLE); next.setVisibility(detail ? View.GONE : View.VISIBLE);
        previous.setEnabled(!busy && page().tokens.size() > 1);
        next.setEnabled(!busy && page().data != null && page().data.optBoolean("hasMore"));
    }
    private void select(int position) {
        if (screen.equals("battle")) return;
        JSONArray items = page().data == null ? null : page().data.optJSONArray("items");
        if (items == null || position >= items.length()) return;
        rememberPosition(); JSONObject selected = items.optJSONObject(position);
        if (screen.equals("runs")) {
            run = selected; runId = selected.optString("id"); screen = "battles";
            battles.data = null; battles.tokens.clear(); battles.tokens.add(""); battles.position = 0; battles.top = 0;
        } else { battleId = selected.optString("id"); battle = null; screen = "battle"; }
        render(); load(false);
    }
    private void back() {
        rememberPosition(); ++requestVersion; busy = false; worker.getQueue().clear();
        if (screen.equals("battle")) { screen = "battles"; battle = null; }
        else if (screen.equals("battles")) screen = "runs";
        else { finish(); return; }
        render(); restorePosition();
        if (page().data == null) load(false);
    }
    private void render() {
        title.setText(screen.equals("runs") ? "历史对局" : screen.equals("battles") ? "单场对局" : "单场对战");
        masthead.getLayoutParams().height = dp(screen.equals("battle") ? 72 : 104); masthead.requestLayout();
        summary.setText(screen.equals("runs") ? "按时间排列 · 每页 " + HistoryReader.PAGE_SIZE + " 条"
                : run == null ? "正在读取整局信息" : "点击交手记录查看双方阵容");
        filterBar.setVisibility(screen.equals("runs") ? View.VISIBLE : View.GONE);
        runOverview.removeAllViews();
        if (screen.equals("battles") && run != null) {
            View overview = runRow(run, true); overview.setOnClickListener(v -> showRunInformation()); runOverview.addView(overview);
        }
        heroFilter.setText(hero == null ? "全部英雄" : "英雄：" + hero);
        List<View> rows = new ArrayList<>();
        boolean detail = screen.equals("battle");
        list.setVisibility(detail ? View.GONE : View.VISIBLE);
        formationHost.setVisibility(detail ? View.VISIBLE : View.GONE); formationHost.removeAllViews();
        if (detail && battle != null) {
            formationHost.addView(new HistoryBattleView(this, artwork, battle,
                "第 " + value(battle, "day") + " 天 · " + battleResult(battle) + " · 对手：" + opponentName(battle.optJSONObject("opening")),
                () -> new AlertDialog.Builder(this).setTitle("对战记录")
                    .setMessage((run == null ? "" : runLabel(run) + "\n") + battleLabel(battle) + "\n"
                        + fields(battle.optJSONObject("opening")))
                    .setPositiveButton("关闭", null).show()), new FrameLayout.LayoutParams(-1, -1));
        } else if (!screen.equals("battle") && page().data != null) {
            JSONArray items = page().data.optJSONArray("items");
            String previousDate = null;
            for (int i = 0; i < items.length(); i++) {
                JSONObject row = items.optJSONObject(i);
                if (screen.equals("runs")) {
                    // Date headings belong to the record, preserving the adapter-to-record index.
                    LinearLayout group = new LinearLayout(this); group.setOrientation(LinearLayout.VERTICAL);
                    String date = dateGroup(row);
                    if (!date.equals(previousDate)) {
                        TextView day = text(13); day.setText(date); day.setTextColor(getColor(R.color.bpp_accent)); day.setAccessibilityHeading(true);
                        day.setPadding(0, dp(16), 0, dp(4)); group.addView(day);
                    }
                    previousDate = date; group.addView(runRow(row, false)); rows.add(group);
                } else rows.add(battleRow(row));
            }
        }
        list.setAdapter(new BaseAdapter() {
            @Override public int getCount() { return rows.size(); }
            @Override public Object getItem(int position) { return rows.get(position); }
            @Override public long getItemId(int position) { return position; }
            @Override public View getView(int position, View recycled, ViewGroup parent) { return rows.get(position); }
        });
        status.setText(""); status.setVisibility(View.GONE); retryTokens = null; retry.setVisibility(View.GONE); updatePaging();
    }

    private ImageView image(String key, int width, int height) {
        ImageView image = new ImageView(this); image.setLayoutParams(new LinearLayout.LayoutParams(dp(width), dp(height)));
        image.setScaleType(ImageView.ScaleType.FIT_CENTER); image.setContentDescription(key + " · 图片未缓存"); artwork.bind(image, key); return image;
    }
    private TextView rowText(String content, int size, int color) {
        TextView view = text(size); view.setText(content); view.setTextColor(getColor(color));
        view.setPadding(dp(10), dp(2), dp(6), dp(2)); return view;
    }
    private static String dateGroup(JSONObject row) {
        return row.opt("startedAt") instanceof Number
            ? DateFormat.getDateInstance(DateFormat.MEDIUM).format(new Date(row.optLong("startedAt"))) : "时间未记录";
    }
    private View runRow(JSONObject row, boolean overview) {
        LinearLayout layout = new LinearLayout(this); layout.setGravity(android.view.Gravity.CENTER_VERTICAL);
        layout.setPadding(0, dp(12), 0, dp(12));
        layout.addView(image(HistoryArtwork.hero(value(row, "hero")), overview ? 64 : 52, overview ? 72 : 60));
        LinearLayout copy = new LinearLayout(this); copy.setOrientation(LinearLayout.VERTICAL);
        TextView name = rowText(value(row, "hero"), 16, R.color.bpp_text); name.setTypeface(null, Typeface.BOLD); name.setId(android.R.id.text1);
        copy.addView(name);
        TextView score = rowText(HistoryPresentation.hasProgress(row) ? row.optLong("victories") + " 胜 / " + row.optLong("losses") + " 负" : "胜场未记录", overview ? 25 : 21, R.color.bpp_accent);
        score.setTypeface(null, Typeface.BOLD);
        if (overview && HistoryPresentation.hasProgress(row)) {
            // Three full-width spaces keep the loss count near the victory text.
            String wins = row.optLong("victories") + " 胜";
            String gap = "\u3000\u3000\u3000";
            SpannableString result = new SpannableString(wins + gap + row.optLong("losses") + " 负");
            int lossStart = wins.length() + gap.length();
            result.setSpan(new RelativeSizeSpan(.5f), lossStart, result.length(), Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
            result.setSpan(new ForegroundColorSpan(getColor(R.color.bpp_muted)), lossStart, result.length(), Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
            score.setText(result);
        }
        copy.addView(score);
        copy.addView(rowText(time(row, "startedAt") + " · " + HistoryPresentation.tier(row), 12, R.color.bpp_muted));
        copy.addView(rowText((overview ? mode(row) + " · 第 " + value(row, "day") + " 天 · " : "")
            + row.optInt("battleCount") + " 场已存 PvP 交手"
            + (row.optInt("startDay") > 1 ? " · 中途开始记录" : ""), 12, R.color.bpp_muted));
        layout.addView(copy, new LinearLayout.LayoutParams(0, -2, 1));
        String trophy = HistoryPresentation.trophy(row);
        if (trophy != null) layout.addView(image(trophy, 32, 36));
        if (!overview) layout.addView(chevron()); return layout;
    }
    private ImageView chevron() {
        ImageView view = new ImageView(this); view.setImageResource(R.drawable.bpp_home_chevron_right);
        view.setImageTintList(android.content.res.ColorStateList.valueOf(getColor(R.color.bpp_muted)));
        view.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        view.setLayoutParams(new LinearLayout.LayoutParams(dp(20), dp(20))); return view;
    }
    private View battleRow(JSONObject row) {
        LinearLayout layout = new LinearLayout(this); layout.setGravity(Gravity.CENTER_VERTICAL); layout.setPadding(0, dp(12), 0, dp(12));
        JSONObject metadata = row.has("opening") ? row.optJSONObject("opening") : row;
        LinearLayout copy = new LinearLayout(this); copy.setOrientation(LinearLayout.VERTICAL);
        TextView heading = rowText("第 " + value(row, "day") + " 天 · " + battleResult(row), 16, resultColor(row));
        heading.setTypeface(null, Typeface.BOLD); copy.addView(heading);
        TextView name = rowText(value(metadata, "opponentHero") + " · " + opponentName(metadata), 14, R.color.bpp_text);
        name.setId(android.R.id.text1); name.setMaxLines(2); name.setEllipsize(TextUtils.TruncateAt.END); copy.addView(name);
        TextView sequence = rowText("#" + value(row, "sequence") + " · " + value(row, "hour") + " 时"
            + (row.optInt("partialOpening") != 0 ? " · 开局信息不完整" : ""), 12, R.color.bpp_muted);
        sequence.setId(android.R.id.text1); copy.addView(sequence);
        layout.addView(image(HistoryArtwork.hero(value(metadata, "opponentHero")), 40, 44));
        layout.addView(copy, new LinearLayout.LayoutParams(0, -2, 1)); layout.addView(chevron()); return layout;
    }
    private static String mode(JSONObject row) {
        String mode = value(row, "playMode"); return mode.equals("Unranked") ? "普通" : mode.equals("Ranked") ? "排位" : mode;
    }
    private static String opponentName(JSONObject metadata) {
        String name = value(metadata, "opponentName"); return name.equals("未记录") || name.trim().isEmpty() ? "玩家名称未记录" : name;
    }
    private static String battleResult(JSONObject row) {
        switch (row.optString("outcome")) {
            case "win": return "胜利";
            case "loss": return "失败";
            case "draw": return "平局";
            default: return "结果未记录";
        }
    }
    private static int resultColor(JSONObject row) {
        return row.optString("outcome").equals("win") ? R.color.bpp_success
            : row.optString("outcome").equals("loss") ? R.color.bpp_history_danger : R.color.bpp_text;
    }
    private void chooseHero() {
        String[] heroes = {"全部英雄", "Vanessa", "Pygmalien", "Dooley", "Mak", "Stelle", "Jules", "Karnok", "TheDragons"};
        BaseAdapter choices = new BaseAdapter() {
            @Override public int getCount() { return heroes.length; }
            @Override public Object getItem(int position) { return heroes[position]; }
            @Override public long getItemId(int position) { return position; }
            @Override public View getView(int position, View recycled, ViewGroup parent) {
                LinearLayout row = new LinearLayout(LauncherHistoryActivity.this); row.setGravity(android.view.Gravity.CENTER_VERTICAL);
                row.setPadding(dp(12), dp(4), dp(12), dp(4));
                if (position > 0) row.addView(image(HistoryArtwork.hero(heroes[position]), 40, 40));
                TextView name = text(16); name.setText(heroes[position]); row.addView(name); return row;
            }
        };
        new AlertDialog.Builder(this).setTitle("按英雄筛选").setAdapter(choices, (dialog, index) -> {
            hero = index == 0 ? null : heroes[index]; ++requestVersion; busy = false;
            runs.data = null; runs.tokens.clear(); runs.tokens.add(""); runs.position = 0; runs.top = 0;
            render(); load(false);
        }).setNegativeButton("取消",null).show();
    }
    private static String value(JSONObject object, String key) { return object == null || object.isNull(key) || !object.has(key) ? "未记录" : object.optString(key); }
    private static String time(JSONObject object, String key) {
        return object.isNull(key) || !object.has(key) ? "未记录" : DateFormat.getDateTimeInstance(DateFormat.SHORT, DateFormat.SHORT).format(new Date(object.optLong(key)));
    }
    private static String runLabel(JSONObject row) {
        String state = row.optString("status");
        if (state.equals("active")) state = row.optString("sourceKind").equals("import") ? "中断记录"
            : row.optInt("recoveryPending") != 0 || row.optInt("pendingIdentity") != 0 ? "待确认续接" : "进行中（上次观察）";
        else state = state.equals("completed") ? "已结束" : "已中断";
        boolean missingFinal = "final_progress_unavailable".equals(row.optString("reason"));
        if (missingFinal) state += "（最终进度未记录）";
        String archive = value(row, "archiveId");
        return value(row, "hero") + " · " + value(row, "playMode") + " · " + state + "\n"
            + time(row, "startedAt") + " · " + (missingFinal ? "最后观察 " : "") + "第 " + value(row, "day") + " 天 · "
            + (HistoryPresentation.hasProgress(row) ? row.optLong("victories") + " 胜 / " + row.optLong("losses") + " 负" : "胜负未记录")
            + "\n档案 " + archive.substring(0, Math.min(8, archive.length())) + " · " + row.optInt("battleCount") + " 场已存 PvP 交手"
            + (row.optInt("startDay") > 1 ? " · 中途开始记录" : "");
    }
    private static String battleLabel(JSONObject row) {
        String outcome = value(row, "outcome");
        outcome = outcome.equals("win") ? "胜" : outcome.equals("loss") ? "负" : outcome.equals("draw") ? "平" : "结果未记录";
        JSONObject metadata = row.has("opening") ? row.optJSONObject("opening") : row;
        String opponent = value(metadata, "opponentHero");
        String name = value(metadata, "opponentName");
        if (name.equals("未记录") || name.trim().isEmpty()) name = "玩家名称未记录";
        return "第 " + row.optInt("day") + " 天 " + row.optInt("hour") + " 时 · #" + row.optInt("sequence")
            + " · " + "PvP" + " · " + outcome
            + "\n对手：" + name + " · " + opponent
            + (row.optInt("partialOpening") != 0 ? "\n开局信息不完整" : "");
    }
    /** Every persisted card field remains inspectable, including fields unknown to this UI version. */
    private static String fields(JSONObject object) {
        if (object == null) return "未记录";
        StringBuilder result = new StringBuilder(); Iterator<String> keys = object.keys();
        while (keys.hasNext()) {
            String key = keys.next(); Object value = object.opt(key);
            result.append(fieldLabel(key)).append("：");
            if (value instanceof JSONObject) result.append('\n').append(fields((JSONObject) value));
            else result.append(value == JSONObject.NULL ? "未记录" : value);
            result.append('\n');
        }
        return result.toString().trim();
    }
    private static String fieldLabel(String key) {
        switch (key) {
            case "name": return "名称";
            case "instanceId": return "实例 ID";
            case "templateId": return "卡牌模板 ID";
            case "tier": return "品质";
            case "size": return "体型";
            case "type": return "类型";
            case "enchantment": return "附魔";
            case "socket": return "位置";
            case "section": return "所在区域";
            case "attributes": return "已保存实例属性";
            case "tags": return "标签";
            case "playerHero": return "我方英雄";
            case "opponentHero": return "对方英雄";
            case "opponentName": return "对手名称";
            case "gameVersion": return "采集时游戏版本";
            case "pluginVersion": return "采集时插件版本";
            default: return key;
        }
    }
    @Override protected void onSaveInstanceState(Bundle state) {
        rememberPosition(); super.onSaveInstanceState(state);
        state.putString("hero", hero); state.putString(TARGET, target); state.putString("screen", screen); state.putString("runId", runId); state.putString("battleId", battleId);
        state.putString("run", run == null ? null : run.toString()); runs.save(state, "runs"); battles.save(state, "battles");
    }
    @Override protected void onDestroy() { ++requestVersion; worker.shutdownNow(); if (artwork != null) artwork.close(); super.onDestroy(); }
}
