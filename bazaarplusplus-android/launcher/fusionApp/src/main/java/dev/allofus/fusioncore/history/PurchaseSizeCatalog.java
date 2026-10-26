package dev.allofus.fusioncore.history;

import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.json.JSONObject;

/** Current local game templates supply size; purchase facts and the game cache stay read-only. */
final class PurchaseSizeCatalog {
    private final File file;
    private long modified = -1, length = -1;
    private Map<String, List<String>> sizes;
    private Map<String, String> templateSizes;

    PurchaseSizeCatalog(File gameRoot) {
        file = new File(gameRoot, "PersistentData/prod/cache/GameData.db");
    }

    String predicate(String size) throws IOException {
        load();
        return PurchaseSql.templatePredicate(sizes.get(size));
    }

    Map<String, String> artworkSizes() throws IOException {
        load();
        return templateSizes;
    }

    private void load() throws IOException {
        if (!file.isFile()) throw new IOException("Local card catalog missing");
        if (sizes == null || modified != file.lastModified() || length != file.length()) {
            long stamp = file.lastModified(), bytes = file.length();
            Map<String, List<String>> loaded = new HashMap<>();
            Map<String, String> artwork = new HashMap<>();
            for (String key : new String[] {"Small", "Medium", "Large"}) loaded.put(key, new ArrayList<>());
            try (SQLiteDatabase db = SQLiteDatabase.openDatabase(file.getPath(), null,
                    SQLiteDatabase.OPEN_READONLY, HistoryStore.PRESERVE_CORRUPTION);
                    Cursor rows = db.rawQuery("SELECT Id,Data FROM cards", null)) {
                while (rows.moveToNext()) {
                    JSONObject card = new JSONObject(new String(rows.getBlob(1), StandardCharsets.UTF_8));
                    List<String> ids = loaded.get(card.optString("Size"));
                    if (ids != null && card.optString("Type").equals("Item")) {
                        ids.add(rows.getString(0));
                        artwork.put(rows.getString(0), card.optString("Size"));
                    } else if (card.optString("Type").equals("Skill")) artwork.put(rows.getString(0), "Skill");
                }
                if (loaded.values().stream().allMatch(List::isEmpty)) throw new IOException("Empty card catalog");
            } catch (Exception error) { throw new IOException("Cannot read local card catalog", error); }
            if (stamp != file.lastModified() || bytes != file.length()) throw new IOException("Card catalog changed");
            sizes = loaded; templateSizes = artwork; modified = stamp; length = bytes;
        }
    }
}
