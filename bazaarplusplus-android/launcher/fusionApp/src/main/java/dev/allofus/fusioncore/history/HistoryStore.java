package dev.allofus.fusioncore.history;

import android.content.ContentValues;
import android.database.Cursor;
import android.database.DatabaseErrorHandler;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteDatabaseCorruptException;
import android.os.Looper;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;
import java.io.File;
import java.io.IOException;
import java.util.Locale;
import java.util.UUID;

/** One game writer. Callers own the process-life game lease; readers never initialize a database. */
final class HistoryStore implements AutoCloseable {
    static final int SCHEMA_VERSION = 1;
    static final int MAX_PAYLOAD_BYTES = 262144;
    static final int MAX_CARDS_PER_GROUP = 128;
    static final int MAX_ATTRIBUTES_PER_CARD = 128;
    static final String FILE_NAME = "history.sqlite";
    private static final String[] GROUPS = {"playerHand", "playerSkills", "opponentHand", "opponentSkills"};
    // Android's default corruption handler can delete even a database opened for reading.
    static final DatabaseErrorHandler PRESERVE_CORRUPTION = database -> {
        throw new SQLiteDatabaseCorruptException("历史库损坏，原文件已保留");
    };
    private final SQLiteDatabase db;
    private final String session;
    private long epoch = -1;
    private String boundAccount;
    private String boundNativeRun;
    private String boundRun;
    private long lastObservation = -1;
    private String lastObservationPayload;

    HistoryStore(File gameRoot, String targetPackage, String sessionId) throws Exception {
        requireWorker();
        session = uuid(sessionId);
        File file = new File(new File(gameRoot, HistoryAccessLock.DIRECTORY_NAME), FILE_NAME);
        SQLiteDatabase opened = null;
        try (HistoryAccessLock ignored = HistoryAccessLock.acquireSchema(gameRoot)) {
            if (file.isFile()) {
                try (SQLiteDatabase reader = SQLiteDatabase.openDatabase(file.getPath(), null, SQLiteDatabase.OPEN_READONLY, PRESERVE_CORRUPTION)) {
                    if (reader.getVersion() > SCHEMA_VERSION) throw new IOException("历史库版本较新，请升级启动器");
                    if (reader.getVersion() == 0 && scalar(reader,
                            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", null) != 0)
                        throw new IOException("无法识别历史库格式，原文件已保留");
                }
            }
            opened = SQLiteDatabase.openOrCreateDatabase(file.getPath(), null, PRESERVE_CORRUPTION);
            opened.setForeignKeyConstraintsEnabled(true);
            if (!opened.enableWriteAheadLogging()) throw new IOException("历史库无法启用 WAL");
            opened.beginTransaction();
            Exception initializationFailure = null;
            try {
                if (opened.getVersion() == 0) createSchema(opened);
                String existing = text(opened, "SELECT value FROM metadata WHERE key='target_package'", null);
                if (existing != null && !existing.equals(targetPackage)) throw new IOException("历史目录与目标游戏不匹配");
                metadata(opened, "target_package", targetPackage);
                if (text(opened, "SELECT value FROM metadata WHERE key='origin_uuid'", null) == null)
                    metadata(opened, "origin_uuid", newId());
                // Restart alone never abandons a run. Wait for a reliable current identity.
                opened.execSQL("UPDATE runs SET recovery_pending=1 WHERE status='active' AND source_kind='local'");
                opened.setTransactionSuccessful();
            } catch (Exception error) { initializationFailure = error; throw error; }
            finally { finishTransaction(opened, initializationFailure); }
            db = opened;
        } catch (Exception error) {
            if (opened != null) opened.close();
            throw error;
        }
    }

    private static void createSchema(SQLiteDatabase db) {
        String[] statements = {
            "CREATE TABLE metadata(key TEXT PRIMARY KEY NOT NULL,value TEXT NOT NULL)",
            "CREATE TABLE history_scopes(id TEXT PRIMARY KEY NOT NULL,scope_key TEXT UNIQUE NOT NULL,account_key TEXT,pending_session TEXT,archive_id TEXT UNIQUE NOT NULL)",
            "CREATE TABLE runs(id TEXT PRIMARY KEY NOT NULL,scope_id TEXT NOT NULL REFERENCES history_scopes(id),native_run_key TEXT,owner_session TEXT NOT NULL,origin_profile_id TEXT NOT NULL,export_id TEXT UNIQUE NOT NULL,source_kind TEXT NOT NULL CHECK(source_kind IN ('local','import')),status TEXT NOT NULL CHECK(status IN ('active','completed','abandoned')),recovery_pending INTEGER NOT NULL DEFAULT 0,started_at INTEGER NOT NULL,last_observed INTEGER NOT NULL,ended_at INTEGER,day INTEGER NOT NULL,hour INTEGER NOT NULL,victories INTEGER NOT NULL,losses INTEGER NOT NULL,start_day INTEGER NOT NULL,hero TEXT,play_mode TEXT,game_version TEXT,plugin_version TEXT,timezone_offset INTEGER NOT NULL,next_sequence INTEGER NOT NULL DEFAULT 1,superseded_by TEXT REFERENCES runs(id),reason TEXT,UNIQUE(scope_id,native_run_key))",
            "CREATE INDEX runs_page ON runs(started_at DESC,id DESC)",
            "CREATE TABLE checkpoints(id TEXT PRIMARY KEY NOT NULL,run_id TEXT NOT NULL REFERENCES runs(id),session_id TEXT NOT NULL,epoch INTEGER NOT NULL,phase TEXT NOT NULL,consumed INTEGER NOT NULL DEFAULT 0,valid INTEGER NOT NULL DEFAULT 1)",
            "CREATE TABLE battles(id TEXT PRIMARY KEY NOT NULL,run_id TEXT NOT NULL REFERENCES runs(id),export_id TEXT UNIQUE NOT NULL,window_id TEXT UNIQUE NOT NULL,sequence INTEGER NOT NULL,source_session TEXT NOT NULL,epoch INTEGER NOT NULL,checkpoint_id TEXT REFERENCES checkpoints(id),kind TEXT NOT NULL CHECK(kind IN ('pve','pvp')),day INTEGER NOT NULL,hour INTEGER NOT NULL,opened_at INTEGER NOT NULL,resolved_at INTEGER,opening_json TEXT NOT NULL,partial_opening INTEGER NOT NULL,outcome TEXT CHECK(outcome IN ('win','loss','draw','unknown')),winner TEXT,loser TEXT,UNIQUE(run_id,sequence),UNIQUE(run_id,checkpoint_id))",
            "CREATE INDEX battles_run ON battles(run_id,sequence)",
            "CREATE TABLE opening_evidence(run_id TEXT NOT NULL REFERENCES runs(id),session_id TEXT NOT NULL,message_id TEXT NOT NULL,checkpoint_key TEXT NOT NULL,window_id TEXT NOT NULL REFERENCES battles(window_id),PRIMARY KEY(run_id,session_id,message_id))",
            "CREATE INDEX evidence_message ON opening_evidence(run_id,message_id)",
            "CREATE TABLE battle_snapshots(battle_id TEXT NOT NULL REFERENCES battles(id),group_name TEXT NOT NULL,status TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(battle_id,group_name))",
            "CREATE TABLE history_diagnostics(id TEXT PRIMARY KEY NOT NULL,run_id TEXT REFERENCES runs(id),session_id TEXT NOT NULL,observed_at INTEGER NOT NULL,kind TEXT NOT NULL,payload TEXT NOT NULL)",
            "CREATE TABLE history_import_receipts(archive_id TEXT NOT NULL,content_hash TEXT NOT NULL,imported_at INTEGER NOT NULL,result_json TEXT NOT NULL,PRIMARY KEY(archive_id,content_hash))",
            "CREATE TABLE history_source_records(origin_profile_id TEXT NOT NULL,export_id TEXT NOT NULL,run_id TEXT NOT NULL REFERENCES runs(id),content_hash TEXT NOT NULL,canonical_json TEXT NOT NULL,PRIMARY KEY(origin_profile_id,export_id))"
        };
        for (String statement : statements) db.execSQL(statement);
        db.setVersion(SCHEMA_VERSION);
    }

    synchronized JSONObject write(JSONObject request) throws Exception {
        requireWorker();
        if (request.toString().getBytes(java.nio.charset.StandardCharsets.UTF_8).length > MAX_PAYLOAD_BYTES)
            throw new IOException("历史写入数据超出限制");
        if (!session.equals(uuid(required(request, "sessionId", 36)))) return state("stale");
        String operation = required(request, "op", 32);
        long requestedEpoch = nonnegative(request, "epoch");
        JSONObject response;
        db.beginTransaction();
        Exception transactionFailure = null;
        try {
            if (operation.equals("observeRun")) response = observe(request, requestedEpoch);
            else if (requestedEpoch != epoch || boundAccount == null || !equal(boundRun, optional(request, "runId", 36)))
                response = state("stale");
            else if (operation.equals("beginBattle")) response = begin(request);
            else if (operation.equals("resolveBattle")) response = resolve(request);
            else if (operation.equals("quarantine")) response = quarantine(boundRun, "identity_uncertain", request);
            else throw new JSONException("Unknown history operation");
            db.setTransactionSuccessful();
        } catch (Exception error) { transactionFailure = error; throw error; }
        finally { finishTransaction(db, transactionFailure); }
        // A failed transaction must not advance the in-memory scope guard.
        if (operation.equals("observeRun") && !response.getString("state").equals("stale")) {
            epoch = requestedEpoch;
            lastObservation = nonnegative(request, "observationSequence");
            lastObservationPayload = request.toString();
            boundAccount = nullableUuid(request, "accountId");
            boundNativeRun = nullableUuid(request, "nativeRunId");
            boundRun = response.has("runId") ? response.getString("runId") : null;
        }
        return response;
    }

    private JSONObject observe(JSONObject request, long requestedEpoch) throws Exception {
        String account = nullableUuid(request, "accountId");
        String nativeRun = nullableUuid(request, "nativeRunId");
        if (requestedEpoch < epoch) return state("stale");
        long observationSequence = nonnegative(request, "observationSequence");
        if (requestedEpoch == epoch && (observationSequence < lastObservation
                || (observationSequence == lastObservation && !request.toString().equals(lastObservationPayload))))
            return state("stale");
        if (requestedEpoch == epoch && (!equal(account, boundAccount) || !equal(nativeRun, boundNativeRun)))
            return state("stale");
        if (nativeRun == null) return state("awaiting_run");
        long now = nonnegative(request, "observedAt");
        long day = nonnegative(request, "day"), hour = nonnegative(request, "hour");
        String scopeKey = account == null ? "pending:" + session : "account:" + account;
        String scopeId = scope(scopeKey, account);
        String runId = text(db, "SELECT id FROM runs WHERE scope_id=? AND native_run_key=?", new String[] {scopeId, nativeRun});
        if (account != null) {
            String pending = text(db, "SELECT r.id FROM runs r JOIN history_scopes s ON s.id=r.scope_id WHERE s.pending_session=? AND r.native_run_key=? AND r.owner_session=? AND r.superseded_by IS NULL",
                    new String[] {session, nativeRun, session});
            if (pending != null && runId == null) {
                db.execSQL("UPDATE runs SET scope_id=? WHERE id=?", new Object[] {scopeId, pending});
                runId = pending;
            } else if (pending != null && !pending.equals(runId)) {
                // Keep the original identity and facts as an alias; never delete a pending observation.
                db.execSQL("UPDATE runs SET scope_id=?,native_run_key=NULL,status='abandoned',reason='identity_confirmed_existing',superseded_by=? WHERE id=?",
                        new Object[] {scopeId, runId, pending});
            }
        }
        if (runId == null) {
            runId = newId();
            ContentValues row = new ContentValues();
            row.put("id", runId); row.put("scope_id", scopeId); row.put("native_run_key", nativeRun);
            row.put("owner_session", session); row.put("export_id", newId());
            row.put("origin_profile_id", text(db, "SELECT archive_id FROM history_scopes WHERE id=?", new String[] {scopeId}));
            row.put("source_kind", "local"); row.put("status", "active"); row.put("started_at", now);
            row.put("last_observed", now); row.put("day", day); row.put("hour", hour); row.put("start_day", day);
            row.put("victories", nonnegative(request, "victories")); row.put("losses", nonnegative(request, "losses"));
            row.put("hero", optional(request, "hero", 128)); row.put("play_mode", optional(request, "playMode", 64));
            row.put("game_version", optional(request, "gameVersion", 256)); row.put("plugin_version", optional(request, "pluginVersion", 64));
            row.put("timezone_offset", request.optInt("timezoneOffset", 0));
            db.insertOrThrow("runs", null, row);
        }
        progress(runId, request, now);
        db.execSQL("UPDATE runs SET hero=COALESCE(?,hero),play_mode=COALESCE(?,play_mode),game_version=COALESCE(?,game_version),plugin_version=COALESCE(?,plugin_version) WHERE id=?",
                new Object[] {optional(request, "hero", 128), optional(request, "playMode", 64),
                    optional(request, "gameVersion", 256), optional(request, "pluginVersion", 64), runId});
        if (account != null) {
            db.execSQL("UPDATE runs SET status='abandoned',reason='new_run_confirmed',ended_at=?,recovery_pending=0 WHERE scope_id=? AND id<>? AND status='active' AND source_kind='local' AND superseded_by IS NULL",
                    new Object[] {now, scopeId, runId});
            db.execSQL("UPDATE runs SET recovery_pending=0 WHERE id=?", new Object[] {runId});
        }
        String phase = required(request, "phase", 32);
        String checkpoint = optional(request, "checkpointId", 36);
        if (account != null && isCheckpoint(phase) && checkpoint != null
                && "active".equals(text(db, "SELECT status FROM runs WHERE id=?", new String[] {runId}))) {
            checkpoint = uuid(checkpoint);
            if (scalar(db, "SELECT COUNT(*) FROM checkpoints WHERE id=?", new String[] {checkpoint}) == 0) {
                db.execSQL("UPDATE checkpoints SET valid=0 WHERE run_id=? AND session_id=? AND consumed=0", new Object[] {runId, session});
                db.execSQL("INSERT INTO checkpoints(id,run_id,session_id,epoch,phase) VALUES (?,?,?,?,?)",
                        new Object[] {checkpoint, runId, session, requestedEpoch, phase});
            }
        } else if (phase.equals("unknown")) {
            db.execSQL("UPDATE checkpoints SET valid=0 WHERE run_id=? AND session_id=? AND consumed=0", new Object[] {runId, session});
        }
        if (phase.equals("victory") || phase.equals("defeat")) {
            db.execSQL("UPDATE runs SET status='completed',ended_at=COALESCE(ended_at,?),recovery_pending=0 WHERE id=? AND status='active'",
                    new Object[] {now, runId});
        }
        boolean partialAllowed = account != null && scalar(db,
                "SELECT COUNT(*) FROM battles WHERE run_id=?", new String[] {runId}) == 0;
        return state(account == null ? "identity_pending" : "bound").put("runId", runId)
                .put("allowFirstPartial", partialAllowed).put("evidence", evidence(runId));
    }

    private JSONObject begin(JSONObject request) throws Exception {
        String window = uuid(required(request, "windowId", 36));
        String existing = text(db, "SELECT id FROM battles WHERE window_id=? AND run_id=?", new String[] {window, boundRun});
        if (existing != null) {
            if (scalar(db, "SELECT COUNT(*) FROM battles WHERE id=? AND source_session=? AND epoch=?",
                    new String[] {existing, session, "" + epoch}) != 1)
                return quarantine(boundRun, "window_from_another_scope", request);
            if (!request.getJSONObject("opening").toString().equals(text(db,
                    "SELECT opening_json FROM battles WHERE id=?", new String[] {existing})))
                return quarantine(boundRun, "opening_conflict", request);
            for (String name : GROUPS) {
                if (!request.getJSONObject("snapshots").getJSONObject(name).toString().equals(text(db,
                        "SELECT payload FROM battle_snapshots WHERE battle_id=? AND group_name=?", new String[] {existing, name})))
                    return quarantine(boundRun, "snapshot_conflict", request);
            }
            return state("recorded").put("battleId", existing).put("duplicate", true);
        }
        if (!"active".equals(text(db, "SELECT status FROM runs WHERE id=?", new String[] {boundRun})))
            return quarantine(boundRun, "run_not_active", request);
        String message = required(request, "messageId", 128);
        if (scalar(db, "SELECT COUNT(*) FROM opening_evidence WHERE run_id=? AND message_id=?", new String[] {boundRun, message}) != 0)
            return quarantine(boundRun, "message_identity_ambiguous", request);
        String kind = required(request, "kind", 8);
        if (!kind.equals("pve") && !kind.equals("pvp")) throw new JSONException("Invalid battle kind");
        String checkpoint = uuid(required(request, "checkpointId", 36));
        boolean partial = request.getBoolean("partialOpening");
        long day = nonnegative(request, "day"), hour = nonnegative(request, "hour");
        if (partial) {
            if (scalar(db, "SELECT COUNT(*) FROM battles WHERE run_id=?", new String[] {boundRun}) != 0)
                return quarantine(boundRun, "partial_identity_ambiguous", request);
        } else if (scalar(db, "SELECT COUNT(*) FROM checkpoints WHERE id=? AND run_id=? AND session_id=? AND epoch=? AND valid=1 AND consumed=0",
                new String[] {checkpoint, boundRun, session, "" + epoch}) != 1)
            return quarantine(boundRun, "checkpoint_not_proven", request);
        JSONObject groups = request.getJSONObject("snapshots");
        for (String name : GROUPS) validateGroup(groups.getJSONObject(name));
        JSONObject opening = request.getJSONObject("opening");
        String id = newId();
        long sequence = scalar(db, "SELECT next_sequence FROM runs WHERE id=?", new String[] {boundRun});
        db.execSQL("INSERT INTO battles(id,run_id,export_id,window_id,sequence,source_session,epoch,checkpoint_id,kind,day,hour,opened_at,opening_json,partial_opening) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                new Object[] {id, boundRun, newId(), window, sequence, session, epoch, partial ? null : checkpoint,
                    kind, day, hour, nonnegative(request, "observedAt"), opening.toString(), partial ? 1 : 0});
        for (String name : GROUPS) {
            JSONObject group = groups.getJSONObject(name);
            db.execSQL("INSERT INTO battle_snapshots VALUES (?,?,?,?)", new Object[] {id, name, group.getString("status"), group.toString()});
        }
        db.execSQL("INSERT INTO opening_evidence VALUES (?,?,?,?,?)", new Object[] {boundRun, session, message, checkpoint, window});
        db.execSQL("UPDATE runs SET next_sequence=next_sequence+1 WHERE id=?", new Object[] {boundRun});
        if (!partial) db.execSQL("UPDATE checkpoints SET consumed=1 WHERE id=?", new Object[] {checkpoint});
        return state("recorded").put("battleId", id).put("sequence", sequence).put("duplicate", false);
    }

    private JSONObject resolve(JSONObject request) throws Exception {
        String window = uuid(required(request, "windowId", 36));
        String outcome = required(request, "outcome", 16);
        if (!outcome.equals("win") && !outcome.equals("loss") && !outcome.equals("draw") && !outcome.equals("unknown"))
            throw new JSONException("Invalid battle outcome");
        String id = text(db, "SELECT id FROM battles WHERE window_id=? AND run_id=? AND source_session=? AND epoch=?",
                new String[] {window, boundRun, session, "" + epoch});
        if (id == null) return quarantine(boundRun, "before_snapshot_missing", request);
        String old = text(db, "SELECT outcome FROM battles WHERE id=?", new String[] {id});
        if (old != null) {
            if (!old.equals(outcome)
                    || !equal(optional(request, "winner", 32), text(db, "SELECT winner FROM battles WHERE id=?", new String[] {id}))
                    || !equal(optional(request, "loser", 32), text(db, "SELECT loser FROM battles WHERE id=?", new String[] {id})))
                return quarantine(boundRun, "result_conflict", request);
            return state("resolved").put("battleId", id).put("duplicate", true);
        }
        long now = nonnegative(request, "observedAt");
        db.execSQL("UPDATE battles SET outcome=?,winner=?,loser=?,resolved_at=? WHERE id=?",
                new Object[] {outcome, optional(request, "winner", 32), optional(request, "loser", 32), now, id});
        progress(boundRun, request, now);
        return state("resolved").put("battleId", id).put("duplicate", false);
    }

    private void progress(String runId, JSONObject request, long now) throws Exception {
        long day = nonnegative(request, "day"), hour = nonnegative(request, "hour");
        db.execSQL("UPDATE runs SET last_observed=MAX(last_observed,?),victories=MAX(victories,?),losses=MAX(losses,?) WHERE id=?",
                new Object[] {now, nonnegative(request, "victories"), nonnegative(request, "losses"), runId});
        db.execSQL("UPDATE runs SET day=?,hour=? WHERE id=? AND (day<? OR (day=? AND hour<=?))", new Object[] {day, hour, runId, day, day, hour});
    }

    private String scope(String key, String account) {
        String id = text(db, "SELECT id FROM history_scopes WHERE scope_key=?", new String[] {key});
        if (id != null) return id;
        id = newId();
        db.execSQL("INSERT INTO history_scopes VALUES (?,?,?,?,?)", new Object[] {id, key, account, account == null ? session : null, newId()});
        return id;
    }

    private JSONArray evidence(String runId) throws JSONException {
        JSONArray values = new JSONArray();
        try (Cursor rows = db.rawQuery("SELECT e.session_id,e.checkpoint_key,e.message_id,e.window_id,s.account_key,r.native_run_key FROM opening_evidence e JOIN runs r ON r.id=e.run_id JOIN history_scopes s ON s.id=r.scope_id WHERE e.run_id=?", new String[] {runId})) {
            while (rows.moveToNext()) values.put(new JSONObject().put("sessionId", rows.getString(0)).put("checkpointId", rows.getString(1))
                    .put("messageId", rows.getString(2)).put("windowId", rows.getString(3)).put("accountId", rows.getString(4)).put("nativeRunId", rows.getString(5)));
        }
        return values;
    }

    private JSONObject quarantine(String runId, String kind, JSONObject data) throws JSONException {
        db.execSQL("INSERT INTO history_diagnostics VALUES (?,?,?,?,?,?)", new Object[] {newId(), runId, session,
            data.optLong("observedAt", 0), kind, data.toString()});
        return state("quarantined").put("reason", kind);
    }

    private static void validateGroup(JSONObject group) throws Exception {
        String status = required(group, "status", 32);
        if (!status.equals("captured") && !status.equals("partial") && !status.equals("unavailable") && !status.equals("failed"))
            throw new JSONException("Invalid snapshot status");
        JSONArray cards = group.getJSONArray("cards");
        if (cards.length() > MAX_CARDS_PER_GROUP) throw new JSONException("Too many snapshot cards");
        if ((status.equals("failed") || status.equals("unavailable")) && cards.length() != 0)
            throw new JSONException("Unavailable snapshot cannot claim cards");
        for (int i = 0; i < cards.length(); i++) {
            JSONObject card = cards.getJSONObject(i);
            required(card, "instanceId", 256); uuid(required(card, "templateId", 36));
            required(card, "name", 2048); required(card, "size", 32); required(card, "tier", 32); required(card, "type", 32);
            optional(card, "socket", 32); optional(card, "section", 32); optional(card, "enchantment", 64);
            JSONObject attributes = card.getJSONObject("attributes");
            if (attributes.length() > MAX_ATTRIBUTES_PER_CARD) throw new JSONException("Too many card attributes");
            java.util.Iterator<String> keys = attributes.keys();
            while (keys.hasNext()) {
                String key = keys.next(); if (key.length() > 128) throw new JSONException("Attribute key too long");
                Object value = attributes.get(key);
                if (!(value instanceof Integer) && !(value instanceof Long)) throw new JSONException("Attribute must be integer");
                long number = ((Number) value).longValue();
                if (number < Integer.MIN_VALUE || number > Integer.MAX_VALUE) throw new JSONException("Attribute out of range");
            }
            JSONArray tags = card.getJSONArray("tags");
            if (tags.length() > 128) throw new JSONException("Too many card tags");
            for (int j = 0; j < tags.length(); j++) if (!(tags.get(j) instanceof String) || tags.getString(j).length() > 128)
                throw new JSONException("Invalid card tag");
        }
    }

    private static void finishTransaction(SQLiteDatabase database, Exception originalFailure) {
        try { database.endTransaction(); }
        catch (RuntimeException rollbackFailure) {
            // SQLITE_FULL may already roll back at the engine level. Android can then throw
            // "no transaction is active" from endTransaction; retain the actionable original.
            if (originalFailure == null) throw rollbackFailure;
            originalFailure.addSuppressed(rollbackFailure);
        }
    }

    private static boolean isCheckpoint(String phase) { return phase.equals("choice") || phase.equals("encounter") || phase.equals("loot") || phase.equals("level_up"); }
    private static JSONObject state(String value) throws JSONException { return new JSONObject().put("state", value); }
    private static boolean equal(String left, String right) { return left == null ? right == null : left.equals(right); }
    private static String newId() { return UUID.randomUUID().toString(); }
    private static String uuid(String value) throws JSONException {
        try {
            UUID id = UUID.fromString(value);
            if (id.getMostSignificantBits() == 0 && id.getLeastSignificantBits() == 0) throw new IllegalArgumentException();
            if (!id.toString().equals(value.toLowerCase(Locale.ROOT))) throw new IllegalArgumentException();
            return id.toString();
        } catch (IllegalArgumentException error) { throw new JSONException("Expected a nonzero UUID"); }
    }
    private static String nullableUuid(JSONObject value, String key) throws JSONException {
        String raw = optional(value, key, 36); return raw == null ? null : uuid(raw);
    }
    private static String required(JSONObject object, String key, int limit) throws JSONException {
        String value = optional(object, key, limit);
        if (value == null || value.trim().isEmpty()) throw new JSONException("Missing " + key);
        return value;
    }
    private static String optional(JSONObject object, String key, int limit) throws JSONException {
        if (!object.has(key) || object.isNull(key)) return null;
        Object value = object.get(key);
        if (!(value instanceof String) || ((String) value).length() > limit) throw new JSONException("Invalid " + key);
        return (String) value;
    }
    private static long nonnegative(JSONObject object, String key) throws JSONException {
        Object value = object.get(key);
        if (!(value instanceof Integer) && !(value instanceof Long)) throw new JSONException("Invalid " + key);
        long number = ((Number) value).longValue();
        if (number < 0) throw new JSONException("Negative " + key);
        return number;
    }
    private static String text(SQLiteDatabase db, String sql, String[] args) {
        try (Cursor rows = db.rawQuery(sql, args)) { return rows.moveToFirst() && !rows.isNull(0) ? rows.getString(0) : null; }
    }
    private static long scalar(SQLiteDatabase db, String sql, String[] args) {
        try (Cursor rows = db.rawQuery(sql, args)) { return rows.moveToFirst() ? rows.getLong(0) : 0; }
    }
    private static void metadata(SQLiteDatabase db, String key, String value) {
        db.execSQL("INSERT OR IGNORE INTO metadata VALUES (?,?)", new Object[] {key, value});
    }
    private static void requireWorker() {
        if (Looper.getMainLooper() != null && Looper.myLooper() == Looper.getMainLooper())
            throw new IllegalStateException("History I/O must run off the UI thread");
    }
    @Override public synchronized void close() { db.close(); }
}
