package dev.allofus.fusioncore.history;

import android.database.sqlite.SQLiteDatabase;
import java.io.File;
import java.nio.file.Files;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;
import org.json.JSONArray;
import org.json.JSONObject;

/** Real Android SQLite reader contract tests; fixture writes stay in a fresh verifier UUID directory. */
public final class HistoryReaderDevice {
    private static int checks;
    private static String id() { return UUID.randomUUID().toString(); }
    private static void check(boolean value, String label) {
        if (!value) throw new AssertionError(label);
        checks++; System.out.println("PASS " + label);
    }
    private static File database(File root) { return new File(new File(root, HistoryAccessLock.DIRECTORY_NAME), HistoryStore.FILE_NAME); }
    private static JSONObject request(String op) throws Exception { return new JSONObject().put("op", op); }
    private static JSONObject observe(String session, int epoch, String account) throws Exception {
        return request("observeRun").put("sessionId", session).put("epoch", epoch).put("observationSequence", epoch)
            .put("accountId", account == null ? JSONObject.NULL : account).put("nativeRunId", id()).put("phase", "choice")
            .put("checkpointId", id()).put("observedAt", 1000).put("day", 1).put("hour", 1).put("victories", 0).put("losses", 0);
    }
    private static byte[] digest(File file) throws Exception { return MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(file.toPath())); }
    public static void main(String[] args) throws Exception {
        Thread.currentThread().setUncaughtExceptionHandler((thread, error) -> { error.printStackTrace(System.err); System.exit(1); });
        if (args.length != 1 || !args[0].matches("/sdcard/BppHistoryM1Check/[0-9a-f-]{36}"))
            throw new IllegalArgumentException("Only an isolated verifier UUID directory is accepted");
        File root = new File(args[0]);
        if (root.exists() || !root.mkdirs()) throw new IllegalStateException("Verifier directory must be new");
        String session = id(), account = id(), other = id(), first = null;
        HistoryReader all = new HistoryReader(root, "test.history.target", HistoryReader.Scope.launcher());
        HistoryReader own = new HistoryReader(root, "test.history.target", HistoryReader.Scope.account(account));
        File absent = new File(root, "absent");
        check("missing".equals(new HistoryReader(absent, "test.history.target", HistoryReader.Scope.launcher())
            .read(request("runs")).getString("state")) && !absent.exists(), "missing browser does not create a directory or database");
        try (HistoryAccessLock lease = HistoryAccessLock.acquire(root); HistoryStore store = new HistoryStore(root, "test.history.target", session)) {
            for (int i = 0; i < 45; i++) {
                String run = store.write(observe(session, i, i == 44 ? other : account)).getString("runId");
                if (i == 0) first = run;
            }
            store.write(observe(session, 45, null));
            try (SQLiteDatabase fixture = SQLiteDatabase.openDatabase(database(root).getPath(), null, SQLiteDatabase.OPEN_READWRITE, HistoryStore.PRESERVE_CORRUPTION)) {
                for (int i = 1; i <= 43; i++) {
                    String battle = id();
                    fixture.execSQL("INSERT INTO battles(id,run_id,export_id,window_id,sequence,source_session,epoch,kind,day,hour,opened_at,opening_json,partial_opening) VALUES(?,?,?,?,?,?,0,'pvp',1,3,2000,?,0)",
                        new Object[] {battle, first, id(), id(), i, session, new JSONObject().put("opponentHero", "中文🐸").put("opponentName", "对手🐸").toString()});
                    for (String group : new String[] {"playerHand", "playerSkills", "opponentHand", "opponentSkills"}) {
                        String status = group.equals("opponentHand") ? "partial" : group.equals("opponentSkills") ? "failed" : "captured";
                        JSONObject payload = new JSONObject().put("status", status).put("cards", new JSONArray());
                        if (group.equals("opponentHand")) payload.getJSONArray("cards").put(new JSONObject().put("name", "旧版本卡牌").put("attributes", new JSONObject().put("Damage", 17)));
                        fixture.execSQL("INSERT INTO battle_snapshots VALUES(?,?,?,?)", new Object[] {battle, group, status, payload.toString()});
                    }
                }
                String monster = id();
                fixture.execSQL("INSERT INTO battles(id,run_id,export_id,window_id,sequence,source_session,epoch,kind,day,hour,opened_at,opening_json,partial_opening) VALUES(?,?,?,?,?,?,0,'pve',1,3,2000,'{}',0)",
                    new Object[] {monster, first, id(), id(), 44, session});
                check(all.read(request("run").put("runId", first)).getJSONObject("run").getInt("battleCount") == 43,
                    "run summary counts PvP only despite legacy PvE rows");
                check("not_found".equals(all.read(request("battle").put("runId", first).put("battleId", monster)).getString("state")),
                    "legacy PvE cannot be opened by direct battle ID");
                JSONObject page = all.read(request("runs"));
                check(page.getJSONArray("items").length() == 20 && page.getBoolean("hasMore"), "default page contains twenty summaries");
                Set<String> ids = new HashSet<>(); String previous = null;
                while (true) {
                    JSONArray items = page.getJSONArray("items");
                    for (int i = 0; i < items.length(); i++) {
                        JSONObject row = items.getJSONObject(i); String current = row.getString("id");
                        if (!ids.add(current) || previous != null && previous.compareTo(current) <= 0) throw new AssertionError("unstable run pagination");
                        if (row.has("accountId") || row.has("nativeRunId") || row.has("opening") || row.has("snapshots")) throw new AssertionError("large/private fields leaked");
                        previous = current;
                    }
                    if (!page.getBoolean("hasMore")) break;
                    page = all.read(request("runs").put("after", page.getJSONObject("after")));
                }
                check(ids.size() == 46, "same-time keyset pages contain every run exactly once");
                JSONObject ownPage = own.read(request("runs"));
                int total = 0;
                do {
                    total += ownPage.getJSONArray("items").length();
                    if (!ownPage.getBoolean("hasMore")) break;
                    ownPage = own.read(request("runs").put("after", ownPage.getJSONObject("after")));
                } while (true);
                check(total == 44, "account scope excludes other accounts and pending sessions");
                check(new HistoryReader(root, "test.history.target", HistoryReader.Scope.pending(session)).read(request("runs")).getJSONArray("items").length() == 1
                    && new HistoryReader(root, "test.history.target", HistoryReader.Scope.pending(id())).read(request("runs")).getJSONArray("items").length() == 0,
                    "unconfirmed identity sees only its own pending session");
                check("not_found".equals(new HistoryReader(root, "test.history.target", HistoryReader.Scope.account(other))
                    .read(request("run").put("runId", first)).getString("state")), "direct run lookup enforces account scope");
                page = own.read(request("battles").put("runId", first));
                check("对手🐸".equals(page.getJSONArray("items").getJSONObject(0).getString("opponentName")), "opponent name survives list projection");
                String selected = page.getJSONArray("items").getJSONObject(0).getString("id");
                int expected = 1;
                while (true) {
                    JSONArray items = page.getJSONArray("items");
                    for (int i = 0; i < items.length(); i++) {
                        JSONObject row = items.getJSONObject(i);
                        if (row.getInt("sequence") != expected++ || row.has("opening") || row.has("snapshots")) throw new AssertionError("battle page contract");
                    }
                    if (!page.getBoolean("hasMore")) break;
                    page = own.read(request("battles").put("runId", first).put("after", page.getJSONObject("after")));
                }
                check(expected == 44, "same-hour battles paginate by persisted sequence without a fixed cap");
                JSONObject detailRequest = request("battle").put("runId", first).put("battleId", selected);
                JSONObject detail = own.read(detailRequest).getJSONObject("battle");
                JSONObject groups = detail.getJSONObject("snapshots");
                check(detail.isNull("outcome") && "中文🐸".equals(detail.getJSONObject("opening").getString("opponentHero"))
                    && groups.length() == 4 && groups.getJSONObject("playerHand").getJSONArray("cards").length() == 0
                    && "partial".equals(groups.getJSONObject("opponentHand").getString("status"))
                    && groups.getJSONObject("opponentHand").getJSONArray("cards").getJSONObject(0).getJSONObject("attributes").getInt("Damage") == 17
                    && "failed".equals(groups.getJSONObject("opponentSkills").getString("status")), "selected detail preserves Unicode, saved facts, unknown outcome and distinct group states");
                check("not_found".equals(new HistoryReader(root, "test.history.target", HistoryReader.Scope.account(other)).read(detailRequest).getString("state")),
                    "direct snapshot lookup cannot bypass scope");
                check("not_found".equals(own.read(new JSONObject(detailRequest.toString()).put("runId", id())).getString("state")), "battle must belong to selected run");
                String artId = id();
                fixture.execSQL("UPDATE battle_snapshots SET payload=? WHERE battle_id=? AND group_name='playerHand'",
                    new Object[] {new JSONObject().put("status","captured").put("cards",new JSONArray()
                        .put(new JSONObject().put("templateId",artId)).put(new JSONObject().put("templateId",artId))
                        .put(new JSONObject().put("templateId","invalid"))).toString(), selected});
                Set<String> artIds = new HashSet<>(); long artAfter = 0; int artPages = 0;
                do {
                    JSONObject art = own.read(request("artwork").put("afterRow",artAfter)); artPages++;
                    JSONArray values = art.getJSONArray("items");
                    for (int i=0;i<values.length();i++) artIds.add(values.getString(i));
                    if (!art.getBoolean("hasMore")) break;
                    check(art.getLong("afterRow") > artAfter, "artwork continuation advances monotonically");
                    artAfter = art.getLong("afterRow");
                } while (true);
                check(artPages > 1 && artIds.size()==1 && artIds.contains(artId), "artwork backfill paginates, deduplicates and skips invalid templates");
                check(new HistoryReader(root,"test.history.target",HistoryReader.Scope.account(other)).read(request("artwork")).getJSONArray("items").length()==0,
                    "artwork backfill cannot read another account's snapshots");
                check("state-pending".equals(HistoryPresentation.trophy(new JSONObject().put("status","active").put("victories",10).put("losses",0)))
                    && "state-pending".equals(HistoryPresentation.trophy(new JSONObject().put("status","completed"))), "unknown and unfinished progress cannot award trophies");
                check("Trophy_Diamond_TUI".equals(HistoryPresentation.trophy(new JSONObject().put("status","completed").put("victories",10).put("losses",0)))
                    && "state-broken".equals(HistoryPresentation.trophy(new JSONObject().put("status","completed").put("victories",3).put("losses",4))), "reliable final progress awards diamond and broken cup consistently with the game panel");
                check(HistoryPresentation.slot("Socket_0",9)==0 && HistoryPresentation.slot("OpponentSocket_12",0)==12
                    && HistoryPresentation.slot("invalid",3)==3, "formation slots retain zero-based positions and safe fallbacks");
                fixture.beginTransaction();
                try {
                    fixture.execSQL("UPDATE battles SET outcome='win' WHERE id=?", new Object[] {selected});
                    check(own.read(detailRequest).getJSONObject("battle").isNull("outcome"), "concurrent WAL reader sees committed facts during writer transaction");
                } finally { fixture.endTransaction(); }
                JSONObject beforeInsert = all.read(request("runs"));
                String inserted = store.write(observe(session, 46, account).put("observedAt", 2000)).getString("runId");
                Set<String> continued = new HashSet<>();
                JSONArray firstItems = beforeInsert.getJSONArray("items");
                for (int i = 0; i < firstItems.length(); i++) continued.add(firstItems.getJSONObject(i).getString("id"));
                JSONObject continuation = beforeInsert.getJSONObject("after");
                do {
                    JSONObject following = all.read(request("runs").put("after", continuation));
                    JSONArray items = following.getJSONArray("items");
                    for (int i = 0; i < items.length(); i++) if (!continued.add(items.getJSONObject(i).getString("id"))) throw new AssertionError("duplicate after concurrent insert");
                    if (!following.getBoolean("hasMore")) break;
                    continuation = following.getJSONObject("after");
                } while (true);
                check(continued.size() == 46 && !continued.contains(inserted), "newer insertion does not shift or duplicate an existing page continuation");
                check(inserted.equals(all.read(request("runs")).getJSONArray("items").getJSONObject(0).getString("id")), "explicit first-page refresh reveals the newer record");
                boolean mismatch = false;
                try { new HistoryReader(root, "wrong.target", HistoryReader.Scope.launcher()).read(request("runs")); }
                catch (java.io.IOException expectedError) { mismatch = true; }
                check(mismatch, "wrong target rejected before exposing records");
            }
        }
        File futureRoot = new File(root, "future");
        database(futureRoot).getParentFile().mkdirs();
        try (SQLiteDatabase future = SQLiteDatabase.openOrCreateDatabase(database(futureRoot), null)) { future.setVersion(2); }
        byte[] before = digest(database(futureRoot));
        check("newer_format".equals(new HistoryReader(futureRoot, "test.history.target", HistoryReader.Scope.launcher()).read(request("runs")).getString("state"))
            && Arrays.equals(before, digest(database(futureRoot))), "future schema is reported distinctly and remains byte-identical");
        File corruptRoot = new File(root, "corrupt"); database(corruptRoot).getParentFile().mkdirs();
        byte[] corrupt = "preserve this damaged history".getBytes(java.nio.charset.StandardCharsets.UTF_8);
        Files.write(database(corruptRoot).toPath(), corrupt);
        boolean corruptFailed = false;
        try { new HistoryReader(corruptRoot, "test.history.target", HistoryReader.Scope.launcher()).read(request("runs")); }
        catch (android.database.sqlite.SQLiteDatabaseCorruptException expectedError) { corruptFailed = true; }
        check(corruptFailed && Arrays.equals(corrupt, Files.readAllBytes(database(corruptRoot).toPath())), "corruption is surfaced without deleting or repairing the file");
        System.out.println(new JSONObject().put("passed", true).put("checks", checks));
    }
}
