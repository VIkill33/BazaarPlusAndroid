package dev.allofus.fusioncore.history;

import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import java.io.File;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.UUID;
import org.json.JSONArray;
import org.json.JSONObject;

/** Exercises production SQLite code in an isolated shell-owned directory, never the game's data. */
public final class HistoryStoreDevice {
    private static int checks;
    private static String id() { return UUID.randomUUID().toString(); }
    private static void check(boolean value, String label) {
        if (!value) throw new AssertionError(label);
        checks++; System.out.println("PASS " + label);
    }
    private static JSONObject observation(String session, long epoch, long sequence, String account, String run, String checkpoint) throws Exception {
        return new JSONObject().put("op", "observeRun").put("sessionId", session).put("epoch", epoch)
            .put("observationSequence", sequence).put("accountId", account == null ? JSONObject.NULL : account)
            .put("nativeRunId", run == null ? JSONObject.NULL : run).put("checkpointId", checkpoint)
            .put("phase", checkpoint == null ? "combat" : "choice").put("observedAt", 1000L + sequence)
            .put("day", 1).put("hour", 3).put("victories", 0).put("losses", 0);
    }
    private static JSONObject begin(String session, long epoch, String run, String checkpoint, String window, String message, boolean partial) throws Exception {
        JSONObject groups = new JSONObject();
        for (String group : new String[] {"playerHand", "playerSkills", "opponentHand", "opponentSkills"})
            groups.put(group, new JSONObject().put("status", "captured").put("cards", new JSONArray()));
        return new JSONObject().put("op", "beginBattle").put("sessionId", session).put("epoch", epoch)
            .put("runId", run).put("checkpointId", checkpoint).put("windowId", window).put("messageId", message)
            .put("kind", "pve").put("partialOpening", partial).put("day", 1).put("hour", 3)
            .put("observedAt", 2000L).put("opening", new JSONObject().put("name", "中文怪物"))
            .put("snapshots", groups);
    }
    private static long number(SQLiteDatabase db, String sql) {
        try (Cursor rows = db.rawQuery(sql, null)) { rows.moveToFirst(); return rows.getLong(0); }
    }
    private static String value(SQLiteDatabase db, String sql) {
        try (Cursor rows = db.rawQuery(sql, null)) { return rows.moveToFirst() ? rows.getString(0) : null; }
    }
    private static File database(File root) { return new File(new File(root, HistoryAccessLock.DIRECTORY_NAME), HistoryStore.FILE_NAME); }
    private static HistoryStore store(File root, String session) throws Exception { return new HistoryStore(root, "test.history.target", session); }
    private static void state(JSONObject response, String expected, String label) throws Exception {
        check(expected.equals(response.getString("state")), label + ": " + response);
    }
    public static void main(String[] args) throws Exception {
        Thread.currentThread().setUncaughtExceptionHandler((thread, error) -> { error.printStackTrace(System.err); System.exit(1); });
        if ((args.length != 1 && args.length != 2) || !args[0].matches("/sdcard/BppHistoryM1Check/[0-9a-f-]{36}"))
            throw new IllegalArgumentException("Only an isolated verifier UUID directory is accepted");
        File root = new File(args[0]);
        if (args.length == 2) { transactionDeath(root, args[1]); return; }
        if (root.exists() || !root.mkdirs()) throw new IllegalStateException("Verifier directory must be new");
        String session = id(), account = id(), nativeRun = id(), checkpoint = id();
        String localRun;
        try (HistoryAccessLock lease = HistoryAccessLock.acquire(root); HistoryStore store = store(root, session);
             SQLiteDatabase inspect = SQLiteDatabase.openDatabase(database(root).getPath(), null, SQLiteDatabase.OPEN_READWRITE, HistoryStore.PRESERVE_CORRUPTION)) {
            check(inspect.getVersion() == 1, "schema version");
            check("wal".equals(value(inspect, "PRAGMA journal_mode")), "WAL enabled");
            JSONObject bound = store.write(observation(session, 0, 0, account, nativeRun, checkpoint));
            state(bound, "bound", "confirmed binding"); localRun = bound.getString("runId");
            JSONObject opening = begin(session, 0, localRun, checkpoint, id(), "message-1", false);
            inspect.execSQL("CREATE TRIGGER fail_last_group BEFORE INSERT ON battle_snapshots WHEN NEW.group_name='opponentSkills' BEGIN INSERT INTO history_diagnostics VALUES ('injected','missing-parent','s',0,'test','{}'); END");
            boolean failed = false;
            try { store.write(opening); } catch (android.database.sqlite.SQLiteConstraintException expected) { failed = true; }
            check(failed, "foreign-key fault reaches caller");
            check(number(inspect, "SELECT COUNT(*) FROM battles") == 0 && number(inspect, "SELECT COUNT(*) FROM battle_snapshots") == 0
                && number(inspect, "SELECT COUNT(*) FROM opening_evidence") == 0, "opening transaction rollback leaves no partial rows");
            check(number(inspect, "SELECT next_sequence FROM runs") == 1 && number(inspect, "SELECT consumed FROM checkpoints") == 0,
                "rollback preserves sequence and checkpoint");
            inspect.execSQL("DROP TRIGGER fail_last_group");
            JSONObject recorded = store.write(opening);
            check(recorded.getLong("sequence") == 1 && number(inspect, "SELECT COUNT(*) FROM battle_snapshots") == 4,
                "retry commits all four groups atomically");
            check(store.write(opening).getBoolean("duplicate") && number(inspect, "SELECT COUNT(*) FROM battles") == 1, "duplicate original is idempotent");
            JSONObject conflict = new JSONObject(opening.toString()); conflict.getJSONObject("opening").put("name", "冲突");
            state(store.write(conflict), "quarantined", "conflicting original quarantined");
            check(value(inspect, "SELECT opening_json FROM battles").contains("中文怪物"), "Unicode opening preserved");
            JSONObject result = new JSONObject().put("op", "resolveBattle").put("sessionId", session).put("epoch", 0)
                .put("runId", localRun).put("windowId", opening.getString("windowId")).put("outcome", "win")
                .put("winner", "Player").put("loser", "Opponent").put("observedAt", 3000L)
                .put("day", 1).put("hour", 4).put("victories", 1).put("losses", 0);
            state(store.write(result), "resolved", "result commits");
            check(store.write(result).getBoolean("duplicate") && number(inspect, "SELECT victories FROM runs") == 1, "duplicate result cannot increment wins");
            JSONObject changedWinner = new JSONObject(result.toString()).put("winner", "Other");
            state(store.write(changedWinner), "quarantined", "winner conflict cannot overwrite result");
            String checkpoint2 = id();
            store.write(observation(session, 0, 2, account, nativeRun, checkpoint2));
            state(store.write(observation(session, 0, 1, account, nativeRun, id())), "stale", "older observation rejected");
            state(store.write(observation(session, 0, 2, account, nativeRun, id())), "stale", "equal sequence conflicting observation rejected");
            JSONObject second = begin(session, 0, localRun, checkpoint2, id(), "message-2", false);
            java.lang.reflect.Field writerField = HistoryStore.class.getDeclaredField("db"); writerField.setAccessible(true);
            SQLiteDatabase writerDatabase = (SQLiteDatabase) writerField.get(store);
            // Pin the primary WAL connection. Applying a per-connection page ceiling on a
            // pooled read connection would not constrain the production write transaction.
            writerDatabase.beginTransaction();
            long previousMaximum;
            try {
                previousMaximum = writerDatabase.getMaximumSize();
                writerDatabase.setMaximumSize(number(writerDatabase, "PRAGMA page_count") * writerDatabase.getPageSize());
                writerDatabase.setTransactionSuccessful();
            } finally { writerDatabase.endTransaction(); }
            char[] padding = new char[131072]; java.util.Arrays.fill(padding, 'x');
            second.getJSONObject("opening").put("padding", new String(padding));
            boolean diskFull = false;
            try { store.write(second); }
            catch (android.database.sqlite.SQLiteFullException expected) { diskFull = true; }
            check(diskFull, "SQLite page ceiling reports real SQLITE_FULL");
            check(!writerDatabase.inTransaction() && number(inspect, "SELECT COUNT(*) FROM battles") == 1
                && number(inspect, "SELECT COUNT(*) FROM battle_snapshots") == 4
                && number(inspect, "SELECT next_sequence FROM runs") == 2, "disk-full rollback preserves prior battle and sequence");
            writerDatabase.beginTransaction();
            try { writerDatabase.setMaximumSize(previousMaximum); writerDatabase.setTransactionSuccessful(); }
            finally { writerDatabase.endTransaction(); }
            check(store.write(second).getLong("sequence") == 2, "two battles at same game time have separate proven windows");
            check(store.write(opening).getBoolean("duplicate") && number(inspect, "SELECT COUNT(*) FROM battles") == 2,
                "old completed original after new checkpoint remains duplicate");
            state(store.write(observation(session, 1, 3, null, null, null)), "awaiting_run", "identity loss clears binding");
            state(store.write(result), "stale", "late result rejected after identity loss");
        }
        String restartedSession = id();
        try (HistoryAccessLock lease = HistoryAccessLock.acquire(root); HistoryStore restarted = store(root, restartedSession);
             SQLiteDatabase inspect = SQLiteDatabase.openDatabase(database(root).getPath(), null, SQLiteDatabase.OPEN_READONLY, HistoryStore.PRESERVE_CORRUPTION)) {
            check(number(inspect, "SELECT recovery_pending FROM runs") == 1 && "active".equals(value(inspect, "SELECT status FROM runs")),
                "restart marks recovery pending without abandonment");
            JSONObject recovered = restarted.write(observation(restartedSession, 0, 0, account, nativeRun, null));
            check(localRun.equals(recovered.getString("runId")), "reliable run identity resumes same local run");
            check(recovered.getJSONArray("evidence").length() == 2, "completed and pending opening evidence survives restart");
            JSONObject advanced = observation(restartedSession, 0, 1, account, nativeRun, null).put("day", 2).put("hour", 0);
            check(!restarted.write(advanced).getBoolean("allowFirstPartial"), "advanced server progress cannot prove a fresh partial after restart");
            JSONObject advancedOpening = begin(restartedSession, 0, localRun, id(), id(), "changed-message-after-reconnect", true).put("day", 2).put("hour", 0);
            state(restarted.write(advancedOpening), "quarantined", "changed message and advanced progress cannot duplicate a restored battle");
            state(restarted.write(begin(restartedSession, 0, localRun, id(), id(), "new-uncertain", true)), "quarantined", "ambiguous partial after restart quarantined");
            restarted.write(observation(restartedSession, 1, 1, id(), id(), id()));
            check(number(inspect, "SELECT COUNT(*) FROM runs WHERE status='active'") == 2, "another account cannot abandon previous account run");
            restarted.write(observation(restartedSession, 2, 2, account, id(), id()));
            check(number(inspect, "SELECT COUNT(*) FROM runs WHERE status='abandoned'") == 1, "new confirmed run abandons only prior same-account run");
        }
        File pendingRoot = new File(root, "pending"); String pendingSession = id(), pendingNative = id();
        try (HistoryAccessLock lease = HistoryAccessLock.acquire(pendingRoot); HistoryStore pending = store(pendingRoot, pendingSession)) {
            JSONObject first = pending.write(observation(pendingSession, 0, 0, null, pendingNative, null));
            state(first, "identity_pending", "account pending observation stored");
            JSONObject promoted = pending.write(observation(pendingSession, 1, 1, id(), pendingNative, id()));
            check(first.getString("runId").equals(promoted.getString("runId")), "same-process pending run promoted without replacing identity");
        }
        protect(root, "future", 2, false);
        protect(root, "unknown", 0, false);
        protect(root, "corrupt", 0, true);
        System.out.println(new JSONObject().put("passed", true).put("checks", checks).put("root", root.getPath()));
    }
    private static void transactionDeath(File root, String mode) throws Exception {
        if (mode.equals("hold")) {
            if (root.exists() || !root.mkdirs()) throw new IllegalStateException("Verifier directory must be new");
            String session = id(), checkpoint = id();
            try (HistoryAccessLock lease = HistoryAccessLock.acquire(root); HistoryStore writer = store(root, session)) {
                String run = writer.write(observation(session, 0, 0, id(), id(), checkpoint)).getString("runId");
                Files.write(new File(root, "committed-run.txt").toPath(), run.getBytes("UTF-8"));
                java.lang.reflect.Field field = HistoryStore.class.getDeclaredField("db"); field.setAccessible(true);
                SQLiteDatabase db = (SQLiteDatabase) field.get(writer);
                // Hold the production write inside an outer transaction so SIGKILL can hit a
                // deterministic uncommitted boundary. No production delay/fault hook is added.
                db.beginTransaction();
                writer.write(begin(session, 0, run, checkpoint, id(), "kill-during-opening", false));
                if (number(db, "SELECT COUNT(*) FROM battles") != 1) throw new AssertionError("Uncommitted opening missing");
                System.out.println(new JSONObject().put("holdingTransaction", true).put("pid", android.os.Process.myPid()));
                System.out.flush();
                new java.util.concurrent.CountDownLatch(1).await();
            }
        } else if (mode.equals("recover")) {
            String committedRun = new String(Files.readAllBytes(new File(root, "committed-run.txt").toPath()), "UTF-8");
            try (HistoryAccessLock lease = HistoryAccessLock.acquire(root); HistoryStore recovered = store(root, id());
                 SQLiteDatabase inspect = SQLiteDatabase.openDatabase(database(root).getPath(), null, SQLiteDatabase.OPEN_READONLY, HistoryStore.PRESERVE_CORRUPTION)) {
                check(committedRun.equals(value(inspect, "SELECT id FROM runs")), "SIGKILL preserves committed parent identity");
                check("active".equals(value(inspect, "SELECT status FROM runs")) && number(inspect, "SELECT recovery_pending FROM runs") == 1,
                    "SIGKILL restart waits for identity without abandoning run");
                check(number(inspect, "SELECT COUNT(*) FROM battles") == 0 && number(inspect, "SELECT COUNT(*) FROM battle_snapshots") == 0
                    && number(inspect, "SELECT COUNT(*) FROM opening_evidence") == 0, "SIGKILL discards entire uncommitted opening");
                check(number(inspect, "SELECT next_sequence FROM runs") == 1 && number(inspect, "SELECT consumed FROM checkpoints") == 0,
                    "SIGKILL rollback preserves next sequence and checkpoint");
                check("ok".equals(value(inspect, "PRAGMA integrity_check")), "SIGKILL database integrity");
                check(value(inspect, "PRAGMA foreign_key_check") == null, "SIGKILL database foreign keys");
                System.out.println(new JSONObject().put("passed", true).put("checks", checks).put("killedTransaction", true));
            }
        } else throw new IllegalArgumentException("Unknown verifier mode");
    }

    private static void protect(File parent, String name, int version, boolean corrupt) throws Exception {
        File root = new File(parent, name);
        File file = database(root); file.getParentFile().mkdirs();
        if (corrupt) Files.write(file.toPath(), "original corrupted history bytes".getBytes("UTF-8"));
        else try (SQLiteDatabase db = SQLiteDatabase.openOrCreateDatabase(file, null)) {
            db.execSQL("CREATE TABLE preserve_me(value TEXT)"); db.execSQL("INSERT INTO preserve_me VALUES ('原始数据')"); db.setVersion(version);
        }
        byte[] original = Files.readAllBytes(file.toPath()); boolean rejected = false;
        try (HistoryStore unexpected = store(root, id())) { }
        catch (Exception expected) { rejected = true; }
        check(rejected && file.isFile() && Arrays.equals(original, Files.readAllBytes(file.toPath())), name + " database rejected and bytes preserved");
    }
}
