package dev.allofus.fusioncore.history;

import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import java.io.File;
import org.json.JSONArray;
import org.json.JSONObject;

/** Read-only current-record inspection; never creates, repairs or upgrades the game's database. */
public final class HistoryReadDevice {
    public static void main(String[] args) throws Exception {
        if (args.length != 1 || !args[0].matches("/sdcard/BazaarPlusPlusAndroid/[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+"))
            throw new IllegalArgumentException("Expected the confirmed target game root");
        File file = new File(new File(args[0], HistoryAccessLock.DIRECTORY_NAME), HistoryStore.FILE_NAME);
        if (!file.isFile()) throw new IllegalStateException("History database does not exist");
        try (SQLiteDatabase db = SQLiteDatabase.openDatabase(file.getPath(), null, SQLiteDatabase.OPEN_READONLY, HistoryStore.PRESERVE_CORRUPTION)) {
            if (db.getVersion() != HistoryStore.SCHEMA_VERSION) throw new IllegalStateException("Unsupported schema");
            JSONArray records = new JSONArray();
            // One SELECT fixes the read snapshot across run, battle and four-group rows.
            try (Cursor rows = db.rawQuery("SELECT r.id AS runId,r.hero,r.play_mode,r.status,r.recovery_pending,r.start_day,r.day AS runDay,r.hour AS runHour,r.victories,r.losses,r.next_sequence,b.window_id,b.sequence,b.kind,b.day,b.hour,b.outcome,b.winner,b.loser,b.partial_opening,b.opening_json,s.group_name,s.status AS snapshotStatus,s.payload FROM runs r LEFT JOIN battles b ON b.run_id=r.id LEFT JOIN battle_snapshots s ON s.battle_id=b.id ORDER BY r.started_at,r.id,b.sequence,s.group_name", null)) {
                while (rows.moveToNext()) {
                    JSONObject row = new JSONObject();
                    for (int i = 0; i < rows.getColumnCount(); i++) {
                        Object value = rows.isNull(i) ? JSONObject.NULL : rows.getType(i) == Cursor.FIELD_TYPE_INTEGER
                            ? rows.getLong(i) : rows.getString(i);
                        row.put(rows.getColumnName(i), value);
                    }
                    records.put(row);
                }
            }
            String target = new File(args[0]).getName();
            HistoryReader reader = new HistoryReader(new File(args[0]), target, HistoryReader.Scope.launcher());
            JSONObject browser = reader.read(new JSONObject().put("op", "runs"));
            JSONObject output = new JSONObject().put("passed", true).put("readOnly", true).put("schema", db.getVersion())
                .put("records", records).put("browserRuns", browser);
            if (browser.optJSONArray("items") != null && browser.getJSONArray("items").length() > 0) {
                String run = browser.getJSONArray("items").getJSONObject(0).getString("id");
                JSONObject battles = reader.read(new JSONObject().put("op", "battles").put("runId", run));
                output.put("browserBattles", battles);
                if (battles.getJSONArray("items").length() > 0) output.put("browserBattle", reader.read(new JSONObject().put("op", "battle")
                    .put("runId", run).put("battleId", battles.getJSONArray("items").getJSONObject(0).getString("id"))));
            }
            System.out.println(output);
        }
    }
}
