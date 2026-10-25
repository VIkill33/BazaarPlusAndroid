package dev.allofus.fusioncore;

import android.os.Bundle;
import android.os.Environment;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.ImageView;
import android.widget.FrameLayout;
import android.widget.HorizontalScrollView;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.content.res.Configuration;
import dev.allofus.fusioncore.history.HistoryArtwork;
import dev.allofus.fusioncore.history.HistoryPresentation;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.ScrollView;
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
    private ListView list;
    private Button previous, next, retry;
    private int requestVersion, detailPosition, detailTop;
    private boolean busy;
    private ArrayList<String> retryTokens;
    private final List<JSONObject> shownCards = new ArrayList<>();
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
            detailPosition = state.getInt("detailPosition"); detailTop = state.getInt("detailTop");
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
        Button view = new com.google.android.material.button.MaterialButton(this);
        view.setText(label); view.setAllCaps(false); view.setMinHeight(dp(48)); view.setOnClickListener(v -> action.run()); return view;
    }
    private void buildUi() {
        LinearLayout root = new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(getColor(R.color.bpp_background)); Utilities.applyWindowInsets(root, dp(8)); setContentView(root);
        LinearLayout bar = new LinearLayout(this);
        bar.addView(button("返回", this::back)); title = text(21);
        bar.addView(title, new LinearLayout.LayoutParams(0, -2, 1)); bar.addView(button("刷新", () -> load(true))); root.addView(bar);
        LinearLayout header = new LinearLayout(this); header.setOrientation(LinearLayout.VERTICAL);
        TextView scope = text(12); scope.setText("目标游戏：" + target + "\n本机全部档案 · 仅显示已保存数据"); header.addView(scope);
        heroFilter = button("全部英雄", this::chooseHero); header.addView(heroFilter);
        summary = text(14); header.addView(summary);
        status = text(14); status.setAccessibilityLiveRegion(View.ACCESSIBILITY_LIVE_REGION_POLITE);
        status.setTextColor(getColor(R.color.bpp_accent)); root.addView(status);
        retry = button("重试读取", () -> load(false, retryTokens)); retry.setVisibility(View.GONE); root.addView(retry);
        list = new ListView(this); list.setDividerHeight(dp(8)); list.setPadding(dp(4), 0, dp(4), 0);
        list.addHeaderView(header, null, false);
        root.addView(list, new LinearLayout.LayoutParams(-1, 0, 1));
        LinearLayout paging = new LinearLayout(this);
        previous = button("上一页", () -> movePage(false)); next = button("下一页", () -> movePage(true));
        paging.addView(previous, new LinearLayout.LayoutParams(0, -2, 1)); paging.addView(next, new LinearLayout.LayoutParams(0, -2, 1)); root.addView(paging);
        list.setOnItemClickListener((parent, view, position, id) -> { if (position > 0) select(position - 1); });
        list.setOnItemLongClickListener((parent, view, position, id) -> { if (position > 0 && screen.equals("battle")) { showCard(position - 1); return true; } return false; });
    }

    private Page page() { return screen.equals("runs") ? runs : battles; }
    private void rememberPosition() {
        if (list == null || list.getAdapter() == null || list.getAdapter().getCount() == 0) return;
        if (screen.equals("battle") ? battle == null : page().data == null) return;
        int position = list.getFirstVisiblePosition(), top = list.getChildAt(0) == null ? 0 : list.getChildAt(0).getTop();
        if (screen.equals("battle")) { detailPosition = position; detailTop = top; }
        else { page().position = position; page().top = top; }
    }
    private void restorePosition() {
        int position = screen.equals("battle") ? detailPosition : page().position;
        int top = screen.equals("battle") ? detailTop : page().top;
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
        busy = true; status.setText("正在读取历史…"); retry.setVisibility(View.GONE); updatePaging();
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
                status.setText(requestedScreen.equals("battle") ? "点击或长按卡牌，查看完整已存字段。" : result.optJSONArray("items").length() == 0 ? "此页没有记录。" : "");
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
        if (screen.equals("battle")) { showCard(position); return; }
        JSONArray items = page().data == null ? null : page().data.optJSONArray("items");
        if (items == null || position >= items.length()) return;
        rememberPosition(); JSONObject selected = items.optJSONObject(position);
        if (screen.equals("runs")) {
            run = selected; runId = selected.optString("id"); screen = "battles";
            battles.data = null; battles.tokens.clear(); battles.tokens.add(""); battles.position = 0; battles.top = 0;
        } else { battleId = selected.optString("id"); battle = null; screen = "battle"; detailPosition = 0; detailTop = 0; }
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
        title.setText(screen.equals("runs") ? "对局历史" : screen.equals("battles") ? "本局 PvP 交手记录" : "双方阵容");
        summary.setText(screen.equals("runs") ? "每条记录是一整局游戏 · 点击查看 PvP 交手 · 每页 " + HistoryReader.PAGE_SIZE + " 条"
                : run == null ? "正在读取整局信息" : runLabel(run) + "\n最后观察：" + time(run, "lastObserved")
                    + " · 结束时间：" + time(run, "endedAt") + "\n段位 / 评分：未记录");
        heroFilter.setVisibility(screen.equals("runs") ? View.VISIBLE : View.GONE);
        heroFilter.setText(hero == null ? "全部英雄" : "英雄：" + hero);
        List<View> rows = new ArrayList<>(); shownCards.clear();
        if (screen.equals("battle") && battle != null) {
            TextView info = text(14);
            info.setText(battleLabel(battle) + "\n保存版本：" + value(battle.optJSONObject("opening"), "gameVersion")
                + "\n图片来自本机游戏模板缓存，仅供参考；品质、附魔与数值以保存字段为准。缺图时启动一次游戏以缓存历史图片。");
            rows.add(info); shownCards.add(null);
            LinearLayout sides = new LinearLayout(this);
            boolean landscape = getResources().getConfiguration().orientation == Configuration.ORIENTATION_LANDSCAPE;
            sides.setOrientation(landscape ? LinearLayout.HORIZONTAL : LinearLayout.VERTICAL);
            JSONObject opening = battle.optJSONObject("opening"), snapshots = battle.optJSONObject("snapshots");
            for (int side = 0; side < 2; side++) {
                LinearLayout board = new LinearLayout(this); board.setOrientation(LinearLayout.VERTICAL);
                String who = side == 0 ? "我方" : "对方";
                String savedHero = value(opening, side == 0 ? "playerHero" : "opponentHero");
                LinearLayout heading = new LinearLayout(this); heading.setGravity(android.view.Gravity.CENTER_VERTICAL);
                heading.addView(image(HistoryArtwork.hero(savedHero), 56, 56));
                TextView name = text(18); name.setText(who + "：" + savedHero); heading.addView(name); board.addView(heading);
                board.addView(formation(snapshots == null ? null : snapshots.optJSONObject(side == 0 ? "playerHand" : "opponentHand"), who + "物品", false));
                board.addView(formation(snapshots == null ? null : snapshots.optJSONObject(side == 0 ? "playerSkills" : "opponentSkills"), who + "技能", true));
                sides.addView(board, landscape ? new LinearLayout.LayoutParams(0, -2, 1) : new LinearLayout.LayoutParams(-1, -2));
            }
            rows.add(sides); shownCards.add(null);
        } else if (!screen.equals("battle") && page().data != null) {
            JSONArray items = page().data.optJSONArray("items");
            for (int i = 0; i < items.length(); i++) rows.add(historyRow(items.optJSONObject(i), screen.equals("runs")));
        }
        list.setAdapter(new BaseAdapter() {
            @Override public int getCount() { return rows.size(); }
            @Override public Object getItem(int position) { return rows.get(position); }
            @Override public long getItemId(int position) { return position; }
            @Override public View getView(int position, View recycled, ViewGroup parent) { return rows.get(position); }
        });
        status.setText(""); retryTokens = null; retry.setVisibility(View.GONE); updatePaging();
    }

    private void showCard(int position) {
        if (position >= shownCards.size() || shownCards.get(position) == null) return;
        showCard(shownCards.get(position));
    }
    private void showCard(JSONObject card) {
        ScrollView scroll = new ScrollView(this); LinearLayout content = new LinearLayout(this); content.setOrientation(LinearLayout.VERTICAL);
        content.addView(image("card-" + card.optString("templateId"), 240, 200));
        TextView details = text(15); details.setText(fields(card)); details.setTextIsSelectable(true); content.addView(details); scroll.addView(content);
        new AlertDialog.Builder(this).setTitle(value(card, "name")).setView(scroll).setPositiveButton("关闭", null).show();
    }
    private ImageView image(String key, int width, int height) {
        ImageView image = new ImageView(this); image.setLayoutParams(new LinearLayout.LayoutParams(dp(width), dp(height)));
        image.setScaleType(ImageView.ScaleType.FIT_CENTER); image.setContentDescription(key + " · 图片未缓存"); artwork.bind(image, key); return image;
    }
    private View historyRow(JSONObject row, boolean isRun) {
        LinearLayout layout = new LinearLayout(this); layout.setGravity(android.view.Gravity.CENTER_VERTICAL);
        layout.setPadding(dp(8), dp(12), dp(8), dp(12)); layout.setBackgroundColor(getColor(R.color.bpp_surface));
        JSONObject metadata = row.has("opening") ? row.optJSONObject("opening") : row;
        layout.addView(image(HistoryArtwork.hero(value(isRun ? row : metadata, isRun ? "hero" : "opponentHero")), 64, 76));
        TextView meta = text(14); meta.setId(android.R.id.text1);
        meta.setText(isRun ? value(row,"hero") + " · " + time(row,"startedAt") + "\n"
            + (row.optString("playMode").equals("Unranked") ? "普通" : row.optString("playMode").equals("Ranked") ? "排位" : value(row,"playMode"))
            + " · 第 " + row.optInt("day") + " 天\n" + row.optInt("battleCount") + " 场已存 PvP 交手"
            + (row.optInt("startDay") > 1 ? " · 中途开始记录" : "") : battleLabel(row));
        layout.addView(meta, new LinearLayout.LayoutParams(0, -2, 1));
        LinearLayout result = new LinearLayout(this); result.setOrientation(LinearLayout.VERTICAL); result.setGravity(android.view.Gravity.CENTER);
        if (isRun && HistoryPresentation.trophy(row) != null) result.addView(image(HistoryPresentation.trophy(row), 44, 44));
        TextView score = text(25); score.setTypeface(null, Typeface.BOLD); score.setTextColor(getColor(R.color.bpp_accent));
        score.setText(isRun ? HistoryPresentation.hasProgress(row) ? row.optLong("victories") + " 胜" : "胜场未记录"
            : row.optString("outcome").equals("win") ? "胜" : row.optString("outcome").equals("loss") ? "负" : row.optString("outcome").equals("draw") ? "平" : "未记录");
        result.addView(score); TextView tier = text(12);
        tier.setText(isRun ? (HistoryPresentation.hasProgress(row) ? row.optLong("losses") + " 负 · " : "") + HistoryPresentation.tier(row) : "查看阵容"); result.addView(tier);
        layout.addView(result, new LinearLayout.LayoutParams(dp(136), -2)); return layout;
    }
    private View formation(JSONObject group, String name, boolean skills) {
        LinearLayout section = new LinearLayout(this); section.setOrientation(LinearLayout.VERTICAL);
        JSONArray cards = group == null ? null : group.optJSONArray("cards");
        TextView heading = text(14); heading.setId(android.R.id.text1);
        heading.setText(name + " · " + groupState(group == null ? "unavailable" : group.optString("status"), cards == null ? 0 : cards.length())
            + (group == null || group.isNull("reason") ? "" : "\n原因：" + group.optString("reason"))); section.addView(heading);
        if (cards == null || cards.length() == 0) return section;
        HorizontalScrollView scroll = new HorizontalScrollView(this); scroll.setFillViewport(true);
        FrameLayout board = new FrameLayout(this); int columns = skills ? 5 : 10, unit = skills ? 104 : 52, height = 172, nextSlot = 0, rowCount = 1;
        for (int index = 0; index < cards.length(); index++) {
            JSONObject card = cards.optJSONObject(index); if (card == null) continue;
            int slot = skills ? nextSlot : HistoryPresentation.slot(card.optString("socket"), nextSlot);
            int size = skills ? 1 : HistoryPresentation.size(card), column = slot % columns, row = slot / columns;
            LinearLayout tile = new LinearLayout(this); tile.setOrientation(LinearLayout.VERTICAL); tile.setGravity(android.view.Gravity.CENTER);
            GradientDrawable border = new GradientDrawable(); border.setColor(getColor(R.color.bpp_surface)); border.setCornerRadius(dp(6));
            int color = card.optString("tier").equals("Diamond") ? 0xff90d8ef : card.optString("tier").equals("Gold") ? 0xffffc466
                : card.optString("tier").equals("Silver") ? 0xffc5ced8 : 0xffb78b69;
            border.setStroke(dp(2), color); tile.setBackground(border); tile.setPadding(dp(3), dp(3), dp(3), dp(3));
            ImageView art = image("card-" + card.optString("templateId"), Math.min(size,columns-column)*unit-12, 88); tile.addView(art);
            TextView caption = text(11); caption.setPadding(dp(2),0,dp(2),0); caption.setId(android.R.id.text1);
            caption.setMaxLines(4); caption.setEllipsize(android.text.TextUtils.TruncateAt.END);
            caption.setText(value(card,"name") + "\n品质 " + value(card,"tier") + (card.isNull("enchantment") ? "" : " · " + value(card,"enchantment"))); tile.addView(caption);
            tile.setContentDescription(value(card,"name") + " · 点击或长按查看已保存字段");
            tile.setOnClickListener(v -> showCard(card)); tile.setOnLongClickListener(v -> { showCard(card); return true; });
            FrameLayout.LayoutParams bounds = new FrameLayout.LayoutParams(dp(Math.min(size,columns-column)*unit-6), dp(height-8));
            bounds.leftMargin = dp(column*unit); bounds.topMargin = dp(row*height); board.addView(tile,bounds);
            nextSlot = Math.max(nextSlot,slot+size); rowCount = Math.max(rowCount,row+1);
        }
        scroll.addView(board,new ViewGroup.LayoutParams(dp(columns*unit),dp(rowCount*height))); section.addView(scroll); return section;
    }
    private void chooseHero() {
        String[] heroes = {"全部英雄", "Vanessa", "Pygmalien", "Dooley", "Mak", "Stelle", "Jules", "Karnok", "TheDragons"};
        new AlertDialog.Builder(this).setTitle("按英雄筛选").setItems(heroes, (dialog, index) -> {
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
            + time(row, "startedAt") + " · " + (missingFinal ? "最后观察 " : "") + "第 " + row.optInt("day") + " 天 · " + row.optInt("victories") + " 胜 / " + row.optInt("losses") + " 负"
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
    private static String groupState(String status, int count) {
        return status.equals("captured") ? count == 0 ? "已完整采集，空阵容" : "已完整采集，" + count + " 张"
            : status.equals("partial") ? "部分采集，" + count + " 张" : status.equals("failed") ? "采集失败" : "未采集";
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
        state.putInt("detailPosition", detailPosition); state.putInt("detailTop", detailTop);
    }
    @Override protected void onDestroy() { ++requestVersion; worker.shutdownNow(); if (artwork != null) artwork.close(); super.onDestroy(); }
}
