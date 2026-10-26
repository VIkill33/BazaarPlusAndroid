package dev.allofus.fusioncore.history;

/** SQLite statements shared by the Android reader/writer and the host SQLite behavior tests. */
public final class PurchaseSql {
    private PurchaseSql() { }
    public static final String CREATE = "CREATE TABLE card_purchases("
        + "purchase_id TEXT PRIMARY KEY NOT NULL,run_id TEXT NOT NULL REFERENCES runs(id),"
        + "instance_id TEXT NOT NULL,template_id TEXT NOT NULL,name TEXT NOT NULL,card_type TEXT NOT NULL,"
        + "tier TEXT NOT NULL,price INTEGER NOT NULL CHECK(price>=0 AND price<=4294967295),"
        + "purchased_at INTEGER NOT NULL,day INTEGER NOT NULL,hour INTEGER NOT NULL,"
        + "UNIQUE(run_id,instance_id))";
    public static final String INDEX = "CREATE INDEX purchases_time ON card_purchases(purchased_at DESC,purchase_id DESC)";
    public static final String INSERT = "INSERT INTO card_purchases(purchase_id,run_id,instance_id,template_id,name,card_type,tier,price,purchased_at,day,hour) VALUES (?,?,?,?,?,?,?,?,?,?,?)";
    private static final String FROM = " FROM card_purchases p JOIN runs r ON r.id=p.run_id JOIN history_scopes s ON s.id=r.scope_id WHERE ";

    public static final String RECENT = "p.purchased_at>=? AND p.purchased_at<=?";

    public static long since(int days, long through) {
        if ((days != 3 && days != 7) || through < 0) throw new IllegalArgumentException("Invalid recent window");
        return Math.max(0, through - days * 86400000L);
    }

    public static String templatePredicate(java.util.List<String> ids) {
        if (ids.isEmpty()) return "0=1";
        StringBuilder sql = new StringBuilder("p.template_id IN (");
        for (String id : ids) {
            // Catalog-owned UUID literals avoid the older SQLite bind limit when both projections repeat the filter.
            if (id == null || !id.matches("[a-fA-F0-9]{8}(-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}"))
                throw new IllegalArgumentException("Invalid card template UUID");
            if (sql.charAt(sql.length() - 1) != '(') sql.append(',');
            sql.append('\'').append(id).append('\'');
        }
        return sql.append(')').toString();
    }

    // Both projections share one SQLite SELECT snapshot, including an empty result page.
    // The scope clause is host-owned SQL; filter values always use placeholders.
    public static String page(String scope, boolean ranking, int offset, int pageSize) {
        String from = FROM + scope;
        String summary = "SELECT COUNT(*) AS totalPurchases,COALESCE(SUM(p.price),0) AS totalGold,"
            + "COUNT(DISTINCT p.template_id) AS uniqueCards,COUNT(DISTINCT p.run_id) AS purchaseRuns,"
            + "MIN(p.purchased_at) AS firstPurchase,MAX(p.purchased_at) AS lastPurchase" + from;
        String rows = ranking
            ? "SELECT p.template_id AS templateId,MAX(p.name) AS name,MAX(p.card_type) AS cardType,"
                + "COUNT(*) AS purchaseCount,SUM(p.price) AS gold,COUNT(DISTINCT p.run_id) AS runCount,"
                + "MAX(p.purchased_at) AS lastPurchasedAt" + from
                + " GROUP BY p.template_id ORDER BY purchaseCount DESC,gold DESC,p.template_id"
            : "SELECT p.purchase_id AS purchaseId,p.template_id AS templateId,p.name,p.card_type AS cardType,"
                + "p.tier,p.price,p.purchased_at AS purchasedAt,p.day,p.hour,r.hero,p.run_id AS runId" + from
                + " ORDER BY p.purchased_at DESC,p.purchase_id DESC";
        return "SELECT totals.*,items.* FROM (" + summary + ") totals LEFT JOIN (" + rows
            + " LIMIT " + (pageSize + 1) + " OFFSET " + offset + ") items ON 1=1";
    }
}
