import dev.allofus.fusioncore.history.PurchaseSql;
import java.nio.charset.StandardCharsets;
import java.util.Base64;

/** Emit production SQL, not a second implementation of its aggregation logic. */
public final class PurchaseSqlHarness {
    private static void emit(String name, String sql) {
        System.out.println(name + ":" + Base64.getEncoder().encodeToString(sql.getBytes(StandardCharsets.UTF_8)));
    }
    public static void main(String[] args) {
        emit("create", PurchaseSql.CREATE); emit("index", PurchaseSql.INDEX); emit("insert", PurchaseSql.INSERT);
        emit("ranking", PurchaseSql.page("1=1", true, 0, 20));
        emit("recent", PurchaseSql.page("1=1", false, 0, 20));
        emit("page2", PurchaseSql.page("1=1", false, 20, 20));
        emit("account", PurchaseSql.page("s.account_key=?", true, 0, 20));
        emit("hero", PurchaseSql.page("r.hero=?", true, 0, 20));
        emit("cutoff", PurchaseSql.page("p.purchased_at<=?", false, 0, 20));
    }
}
