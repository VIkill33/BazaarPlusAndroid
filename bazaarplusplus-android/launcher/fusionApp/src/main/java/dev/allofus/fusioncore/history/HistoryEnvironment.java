package dev.allofus.fusioncore.history;

import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.os.Looper;
import org.json.JSONObject;
import java.io.File;
import java.util.UUID;

/** M0 storage capability check; never opens or migrates the user's history database. */
public final class HistoryEnvironment {
    private HistoryEnvironment() { }

    public static String verify(String requestId) throws Exception {
        UUID.fromString(requestId);
        if (Looper.myLooper() == Looper.getMainLooper()) {
            throw new IllegalStateException("History I/O must run off the Android UI thread");
        }
        File directory = new File(HistoryRuntime.gameRoot(), HistoryAccessLock.DIRECTORY_NAME);
        File file = new File(directory, ".verify-" + UUID.randomUUID() + ".sqlite");
        long started = android.os.SystemClock.elapsedRealtime();
        try {
            try (SQLiteDatabase db = SQLiteDatabase.openOrCreateDatabase(file, null)) {
                db.setForeignKeyConstraintsEnabled(true);
                if (!db.enableWriteAheadLogging()) throw new IllegalStateException("WAL unavailable");
                db.execSQL("CREATE TABLE parent (id TEXT PRIMARY KEY)");
                db.execSQL("CREATE TABLE child (parent_id TEXT REFERENCES parent(id))");
                db.beginTransaction();
                try {
                    db.execSQL("INSERT INTO parent VALUES (?)", new Object[] {requestId});
                    db.execSQL("INSERT INTO child VALUES (?)", new Object[] {requestId});
                    db.setTransactionSuccessful();
                } finally { db.endTransaction(); }
                db.beginTransaction();
                try {
                    db.execSQL("DELETE FROM child");
                    // The writer stays open and uncommitted. An independent read handle must still
                    // observe the last committed child rather than block or see the pending deletion.
                    try (SQLiteDatabase reader = SQLiteDatabase.openDatabase(file.getPath(), null,
                            SQLiteDatabase.OPEN_READONLY);
                         Cursor rows = reader.rawQuery("SELECT parent_id FROM child", null)) {
                        if (!rows.moveToFirst() || !requestId.equals(rows.getString(0))) {
                            throw new IllegalStateException("Concurrent reader did not see committed transaction");
                        }
                    }
                } finally { db.endTransaction(); } // Deliberate rollback.
                boolean rejected = false;
                try { db.execSQL("INSERT INTO child VALUES ('missing-parent')"); }
                catch (android.database.sqlite.SQLiteConstraintException expected) { rejected = true; }
                if (!rejected) throw new IllegalStateException("Foreign key enforcement unavailable");
            }
            try (SQLiteDatabase reopened = SQLiteDatabase.openDatabase(file.getPath(), null,
                    SQLiteDatabase.OPEN_READONLY);
                 Cursor rows = reopened.rawQuery("SELECT parent_id FROM child", null)) {
                if (!rows.moveToFirst() || !requestId.equals(rows.getString(0))) {
                    throw new IllegalStateException("Committed transaction did not survive reopen");
                }
            }
            return new JSONObject().put("requestId", requestId)
                    .put("targetPackage", HistoryRuntime.targetPackage())
                    .put("directory", directory.getCanonicalPath())
                    .put("sqlite", true).put("wal", true).put("foreignKeys", true)
                    .put("rollback", true).put("reopened", true).put("concurrentReader", true)
                    .put("elapsedMs", android.os.SystemClock.elapsedRealtime() - started).toString();
        } finally {
            if (!SQLiteDatabase.deleteDatabase(file)) {
                android.util.Log.w("BppHistory", "M0 isolated database cleanup failed: " + file.getName());
            }
        }
    }
}
