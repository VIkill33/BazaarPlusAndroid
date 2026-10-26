import dev.allofus.fusioncore.history.PurchaseSql;
import java.nio.charset.StandardCharsets;
import java.util.Base64;

/** Emit production SQL, not a second implementation of its aggregation logic. */
public final class PurchaseSqlHarness {
    private static void emit(String name, String sql) {
        System.out.println(name + ":" + Base64.getEncoder().encodeToString(sql.getBytes(StandardCharsets.UTF_8)));
    }
    public static void main(String[] args) {
        emit("window", PurchaseSql.page("r.hero=? AND " + PurchaseSql.RECENT, false, 0, 20));
        emit("size", PurchaseSql.page("r.hero=? AND p.card_type='Item' AND " + PurchaseSql.templatePredicate(
            java.util.Arrays.asList("00000000-0000-0000-0000-000000000001")), true, 0, 20));
        emit("emptySize", PurchaseSql.page(PurchaseSql.templatePredicate(java.util.Collections.emptyList()), true, 0, 20));
        if (PurchaseSql.since(3, 8L * 86400000L) != 5L * 86400000L || PurchaseSql.since(7, 1) != 0)
            throw new AssertionError("Recent window bounds");
        try { PurchaseSql.templatePredicate(java.util.Arrays.asList("' OR 1=1 --")); throw new AssertionError("Unsafe template"); }
        catch (IllegalArgumentException expected) { }
        emit("create", PurchaseSql.CREATE); emit("index", PurchaseSql.INDEX); emit("insert", PurchaseSql.INSERT);
        emit("ranking", PurchaseSql.page("1=1", true, 0, 20));
        emit("recent", PurchaseSql.page("1=1", false, 0, 20));
        emit("page2", PurchaseSql.page("1=1", false, 20, 20));
        emit("account", PurchaseSql.page("s.account_key=?", true, 0, 20));
        emit("hero", PurchaseSql.page("r.hero=?", true, 0, 20));
        emit("cutoff", PurchaseSql.page("p.purchased_at<=?", false, 0, 20));
    }
}
