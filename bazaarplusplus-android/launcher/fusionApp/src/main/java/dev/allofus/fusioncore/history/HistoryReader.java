package dev.allofus.fusioncore.history;

import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.os.Looper;
import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;
import org.json.JSONArray;
import org.json.JSONObject;

/** Read-only, bounded queries. Opening a browser never initializes or migrates history. */
public final class HistoryReader {
    public static final int PAGE_SIZE = 20;
    private static final String[] GROUPS = {"playerHand", "playerSkills", "opponentHand", "opponentSkills"};
    private static final String RUN_COLUMNS = "r.id,r.hero,r.play_mode AS playMode,r.status,r.recovery_pending AS recoveryPending,"
        + "r.started_at AS startedAt,r.ended_at AS endedAt,r.last_observed AS lastObserved,r.day,r.hour,r.victories,r.losses,"
        + "r.start_day AS startDay,r.source_kind AS sourceKind,r.game_version AS gameVersion,r.plugin_version AS pluginVersion,"
        + "r.timezone_offset AS timezoneOffset,r.reason,r.superseded_by AS supersededBy,s.archive_id AS archiveId,"
        + "CASE WHEN s.account_key IS NULL THEN 1 ELSE 0 END AS pendingIdentity,"
        + "(SELECT COUNT(*) FROM battles b WHERE b.run_id=r.id) AS battleCount";
    private static final String RUN_FROM = " FROM runs r JOIN history_scopes s ON s.id=r.scope_id";
    private static final String BATTLE_COLUMNS = "b.id,b.run_id AS runId,b.sequence,b.kind,b.day,b.hour,b.opened_at AS openedAt,"
        + "b.resolved_at AS resolvedAt,b.outcome,b.winner,b.loser,b.partial_opening AS partialOpening";
    private final File file;
    private final String target;
    private final Scope scope;

    /** Scope is supplied by the host, never widened by a query or continuation token. */
    public static final class Scope {
        private final String clause;
        private final String value;
        private Scope(String clause, String value) { this.clause = clause; this.value = value; }
        public static Scope launcher() { return new Scope("1=1", null); }
        public static Scope account(String accountId) { return new Scope("s.account_key=?", uuid(accountId)); }
        public static Scope pending(String sessionId) { return new Scope("s.account_key IS NULL AND s.pending_session=?", uuid(sessionId)); }
    }

    public HistoryReader(File gameRoot, String targetPackage, Scope readScope) {
        if (readScope == null || targetPackage == null) throw new IllegalArgumentException("History read scope is required");
        file = new File(new File(gameRoot, HistoryAccessLock.DIRECTORY_NAME), HistoryStore.FILE_NAME);
        target = targetPackage;
        scope = readScope;
    }

    public JSONObject read(JSONObject request) throws Exception {
        if (Looper.getMainLooper() != null && Looper.myLooper() == Looper.getMainLooper())
            throw new IllegalStateException("History I/O must run off the UI thread");
        String operation = request.getString("op");
        if (!Arrays.asList("runs", "run", "battles", "battle").contains(operation))
            throw new IllegalArgumentException("Unknown history read operation");
        if (!file.isFile()) return new JSONObject().put("state", "missing").put("items", new JSONArray());
        try (SQLiteDatabase db = SQLiteDatabase.openDatabase(file.getPath(), null,
                SQLiteDatabase.OPEN_READONLY, HistoryStore.PRESERVE_CORRUPTION)) {
            if (db.getVersion() > HistoryStore.SCHEMA_VERSION)
                return new JSONObject().put("state", "newer_format");
            if (db.getVersion() != HistoryStore.SCHEMA_VERSION) throw new IOException("无法识别历史库格式，原文件已保留");
            try (Cursor metadata = db.rawQuery("SELECT value FROM metadata WHERE key='target_package'", null)) {
                if (!metadata.moveToFirst() || !target.equals(metadata.getString(0)))
                    throw new IOException("历史目录与目标游戏不匹配");
            }
            if (operation.equals("runs")) return runs(db, request);
            if (operation.equals("run")) return run(db, request);
            if (operation.equals("battles")) return battles(db, request);
            return battle(db, request);
        }
    }

    private JSONObject runs(SQLiteDatabase db, JSONObject request) throws Exception {
        String where = scope.clause;
        List<String> args = arguments();
        if (request.has("archiveId") && !request.isNull("archiveId")) {
            where += " AND s.archive_id=?"; args.add(uuid(request.getString("archiveId")));
        }
        JSONObject after = request.optJSONObject("after");
        if (after != null) {
            long time = nonnegative(after, "startedAt");
            where += " AND (r.started_at<? OR (r.started_at=? AND r.id<?))";
            args.add(Long.toString(time)); args.add(Long.toString(time)); args.add(uuid(after.getString("id")));
        }
        try (Cursor rows = query(db, "SELECT " + RUN_COLUMNS + RUN_FROM + " WHERE " + where
                + " ORDER BY r.started_at DESC,r.id DESC LIMIT " + (PAGE_SIZE + 1), args)) {
            return page(rows, true);
        }
    }

    private JSONObject run(SQLiteDatabase db, JSONObject request) throws Exception {
        List<String> args = arguments(); args.add(uuid(request.getString("runId")));
        try (Cursor rows = query(db, "SELECT " + RUN_COLUMNS + RUN_FROM + " WHERE " + scope.clause + " AND r.id=?", args)) {
            return rows.moveToFirst() ? new JSONObject().put("state", "ok").put("run", row(rows)) : state("not_found");
        }
    }

    private JSONObject battles(SQLiteDatabase db, JSONObject request) throws Exception {
        List<String> args = arguments(); args.add(uuid(request.getString("runId")));
        String where = scope.clause + " AND r.id=?";
        JSONObject after = request.optJSONObject("after");
        if (after != null) {
            long day = nonnegative(after, "day"), hour = nonnegative(after, "hour"), sequence = nonnegative(after, "sequence");
            where += " AND (b.day>? OR (b.day=? AND b.hour>?) OR (b.day=? AND b.hour=? AND b.sequence>?))";
            for (long value : new long[] {day, day, hour, day, hour, sequence}) args.add(Long.toString(value));
        }
        // Project only opponent metadata from the small opening object; never query snapshot payloads here.
        try (Cursor rows = query(db, "SELECT " + BATTLE_COLUMNS + ",b.opening_json AS openingMetadata" + RUN_FROM + " JOIN battles b ON b.run_id=r.id WHERE "
                + where + " ORDER BY b.day,b.hour,b.sequence LIMIT " + (PAGE_SIZE + 1), args)) {
            return page(rows, false);
        }
    }

    private JSONObject battle(SQLiteDatabase db, JSONObject request) throws Exception {
        List<String> args = arguments();
        args.add(uuid(request.getString("runId"))); args.add(uuid(request.getString("battleId")));
        // One SELECT holds a consistent SQLite read snapshot for the opening and all four groups.
        try (Cursor rows = query(db, "SELECT " + BATTLE_COLUMNS
                + ",b.opening_json AS opening,x.group_name AS groupName,x.status AS snapshotStatus,x.payload AS snapshotPayload"
                + RUN_FROM + " JOIN battles b ON b.run_id=r.id LEFT JOIN battle_snapshots x ON x.battle_id=b.id WHERE "
                + scope.clause + " AND r.id=? AND b.id=? ORDER BY x.group_name", args)) {
            if (!rows.moveToFirst()) return state("not_found");
            JSONObject result = row(rows);
            result.remove("groupName"); result.remove("snapshotStatus"); result.remove("snapshotPayload");
            result.put("opening", new JSONObject(result.getString("opening")));
            JSONObject groups = new JSONObject();
            do {
                String group = rows.getString(rows.getColumnIndexOrThrow("groupName"));
                if (group == null) continue;
                JSONObject payload = new JSONObject(rows.getString(rows.getColumnIndexOrThrow("snapshotPayload")));
                String status = rows.getString(rows.getColumnIndexOrThrow("snapshotStatus"));
                if (!status.equals(payload.getString("status"))) throw new IOException("历史快照状态不一致");
                groups.put(group, payload);
            } while (rows.moveToNext());
            for (String group : GROUPS) if (!groups.has(group)) groups.put(group,
                new JSONObject().put("status", "unavailable").put("cards", new JSONArray()).put("reason", "快照未记录"));
            result.put("snapshots", groups);
            return state("ok").put("battle", result);
        }
    }

    private static JSONObject page(Cursor rows, boolean runs) throws Exception {
        JSONArray items = new JSONArray();
        while (items.length() < PAGE_SIZE && rows.moveToNext()) {
            JSONObject item = row(rows);
            if (!runs) {
                JSONObject opening = new JSONObject(item.getString("openingMetadata")); item.remove("openingMetadata");
                for (String key : new String[] {"opponentHero", "monsterTemplateId"}) item.put(key, opening.opt(key));
            }
            items.put(item);
        }
        boolean more = rows.moveToNext();
        JSONObject result = state("ok").put("items", items).put("hasMore", more);
        if (more) {
            JSONObject last = items.getJSONObject(items.length() - 1), after = new JSONObject();
            for (String key : runs ? new String[] {"startedAt", "id"} : new String[] {"day", "hour", "sequence"})
                after.put(key, last.get(key));
            result.put("after", after);
        }
        return result;
    }

    private List<String> arguments() {
        List<String> args = new ArrayList<>(); if (scope.value != null) args.add(scope.value); return args;
    }
    private static Cursor query(SQLiteDatabase db, String sql, List<String> args) { return db.rawQuery(sql, args.toArray(new String[0])); }
    private static JSONObject state(String value) throws Exception { return new JSONObject().put("state", value); }
    private static JSONObject row(Cursor rows) throws Exception {
        JSONObject result = new JSONObject();
        for (int i = 0; i < rows.getColumnCount(); i++) result.put(rows.getColumnName(i), rows.isNull(i) ? JSONObject.NULL
            : rows.getType(i) == Cursor.FIELD_TYPE_INTEGER ? rows.getLong(i) : rows.getString(i));
        return result;
    }
    private static String uuid(String value) {
        UUID id = UUID.fromString(value);
        if (id.equals(new UUID(0, 0)) || !id.toString().equals(value)) throw new IllegalArgumentException("Invalid history identity");
        return value;
    }
    private static long nonnegative(JSONObject value, String key) throws Exception {
        Object number = value.get(key);
        if (!(number instanceof Integer) && !(number instanceof Long) || ((Number) number).longValue() < 0)
            throw new IllegalArgumentException("Invalid history continuation");
        return ((Number) number).longValue();
    }
}
