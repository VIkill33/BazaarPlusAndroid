package dev.allofus.fusioncore;

import android.os.Bundle;
import android.os.Environment;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ArrayAdapter;
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
        if (state != null) try {
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
        final String requestedScreen = screen, selectedRun = runId, selectedBattle = battleId;
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
        title.setText(screen.equals("runs") ? "对局历史" : screen.equals("battles") ? "整局详情" : "双方阵容");
        summary.setText(screen.equals("runs") ? "按开始时间倒序 · 每页 " + HistoryReader.PAGE_SIZE + " 条"
                : run == null ? "正在读取整局信息" : runLabel(run) + "\n最后观察：" + time(run, "lastObserved")
                    + " · 结束时间：" + time(run, "endedAt") + "\n段位 / 评分：未记录");
        List<String> labels = new ArrayList<>(); shownCards.clear();
        if (screen.equals("battle") && battle != null) {
            labels.add(battleLabel(battle) + "\n" + fields(battle.optJSONObject("opening")) + "\n卡牌描述以保存字段为准；启动器不渲染游戏原生卡面。"); shownCards.add(null);
            JSONObject snapshots = battle.optJSONObject("snapshots");
            String[] groups = {"playerHand", "playerSkills", "opponentHand", "opponentSkills"};
            String[] names = {"我方物品", "我方技能", "对方物品", "对方技能"};
            for (int i = 0; i < groups.length; i++) {
                JSONObject group = snapshots.optJSONObject(groups[i]); JSONArray cards = group == null ? new JSONArray() : group.optJSONArray("cards");
                String savedState = group == null ? "unavailable" : group.optString("status");
                labels.add(names[i] + " · " + groupState(savedState, cards == null ? 0 : cards.length())
                    + (group == null || group.isNull("reason") ? "" : "\n原因：" + group.optString("reason"))); shownCards.add(null);
                if (cards != null) for (int c = 0; c < cards.length(); c++) {
                    JSONObject card = cards.optJSONObject(c); if (card == null) continue;
                    labels.add(value(card, "name") + "\n品质 " + value(card, "tier") + " · 附魔 " + value(card, "enchantment")
                        + " · 体型 " + value(card, "size") + "\n位置 " + value(card, "socket") + " · " + value(card, "section")); shownCards.add(card);
                }
            }
        } else if (!screen.equals("battle") && page().data != null) {
            JSONArray items = page().data.optJSONArray("items");
            for (int i = 0; i < items.length(); i++) labels.add(screen.equals("runs") ? runLabel(items.optJSONObject(i)) : battleLabel(items.optJSONObject(i)));
        }
        list.setAdapter(new ArrayAdapter<String>(this, android.R.layout.simple_list_item_1, labels) {
            @Override public View getView(int position, View recycled, ViewGroup parent) {
                TextView view = (TextView) super.getView(position, recycled, parent);
                view.setTextColor(getColor(R.color.bpp_text)); view.setTextSize(16); view.setPadding(dp(16), dp(16), dp(16), dp(16));
                view.setBackgroundColor(getColor(R.color.bpp_surface)); return view;
            }
        });
        status.setText(""); retryTokens = null; retry.setVisibility(View.GONE); updatePaging();
    }

    private void showCard(int position) {
        if (position >= shownCards.size() || shownCards.get(position) == null) return;
        JSONObject card = shownCards.get(position); ScrollView scroll = new ScrollView(this); TextView details = text(15);
        details.setText(fields(card)); details.setTextIsSelectable(true); scroll.addView(details);
        new AlertDialog.Builder(this).setTitle(value(card, "name")).setView(scroll).setPositiveButton("关闭", null).show();
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
        String archive = value(row, "archiveId");
        return value(row, "hero") + " · " + value(row, "playMode") + " · " + state + "\n"
            + time(row, "startedAt") + " · 第 " + row.optInt("day") + " 天 · " + row.optInt("victories") + " 胜 / " + row.optInt("losses") + " 负"
            + "\n档案 " + archive.substring(0, Math.min(8, archive.length())) + " · " + row.optInt("battleCount") + " 场已存战斗"
            + (row.optInt("startDay") > 1 ? " · 中途开始记录" : "");
    }
    private static String battleLabel(JSONObject row) {
        String outcome = value(row, "outcome");
        outcome = outcome.equals("win") ? "胜" : outcome.equals("loss") ? "负" : outcome.equals("draw") ? "平" : "结果未记录";
        String opponent = row.has("opening") ? value(row.optJSONObject("opening"), "opponentHero") : value(row, "opponentHero");
        return "第 " + row.optInt("day") + " 天 " + row.optInt("hour") + " 时 · #" + row.optInt("sequence")
            + " · " + row.optString("kind").toUpperCase(java.util.Locale.ROOT) + " · " + outcome
            + "\n对手：" + (row.optString("kind").equals("pve") ? "野怪（名称未记录）" : opponent)
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
            case "monsterTemplateId": return "野怪模板 ID";
            case "gameVersion": return "采集时游戏版本";
            case "pluginVersion": return "采集时插件版本";
            default: return key;
        }
    }
    @Override protected void onSaveInstanceState(Bundle state) {
        rememberPosition(); super.onSaveInstanceState(state);
        state.putString(TARGET, target); state.putString("screen", screen); state.putString("runId", runId); state.putString("battleId", battleId);
        state.putString("run", run == null ? null : run.toString()); runs.save(state, "runs"); battles.save(state, "battles");
        state.putInt("detailPosition", detailPosition); state.putInt("detailTop", detailTop);
    }
    @Override protected void onDestroy() { ++requestVersion; worker.shutdownNow(); super.onDestroy(); }
}
