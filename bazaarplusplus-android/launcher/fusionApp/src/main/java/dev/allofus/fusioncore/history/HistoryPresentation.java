package dev.allofus.fusioncore.history;

import org.json.JSONObject;

/** Presentation rules mirror Android HistoryViewText; never infer final progress from missing values. */
public final class HistoryPresentation {
    private HistoryPresentation() { }
    public static boolean hasProgress(JSONObject row) {
        return row.opt("victories") instanceof Number && row.opt("losses") instanceof Number
            && row.optLong("victories", -1) >= 0 && row.optLong("losses", -1) >= 0;
    }
    public static String tier(JSONObject row) {
        String reason = row.optString("reason"), status = row.optString("status");
        if (reason.equals("surrender")) return "已投降";
        if (reason.equals("final_progress_unavailable")) return "最后观察 · 最终进度未记录";
        if (!status.equals("completed")) return status.equals("abandoned") ? "已中断"
            : row.optString("sourceKind").equals("import") ? "中断记录"
            : row.optInt("recoveryPending") != 0 || row.optInt("pendingIdentity") != 0 ? "待确认续接" : "进行中 · 最后观察";
        if (!hasProgress(row)) return "最终进度未记录";
        long wins = row.optLong("victories");
        return wins == 10 && row.optLong("losses") == 0 ? "钻石胜利" : wins >= 10 ? "黄金胜利"
            : wins >= 7 ? "白银胜利" : wins >= 4 ? "青铜胜利" : "未达奖杯";
    }
    public static String trophy(JSONObject row) {
        switch (tier(row)) {
            case "青铜胜利": return "Trophy_Bronze_TUI";
            case "白银胜利": return "Trophy_Silver_TUI";
            case "黄金胜利": return "Trophy_Gold_TUI";
            case "钻石胜利": return "Trophy_Diamond_TUI";
            case "未达奖杯": return "state-broken";
            case "已投降": return "state-flag";
            default: return "state-pending";
        }
    }
    public static int slot(String socket, int fallback) {
        try { int slot = Integer.parseInt(socket.substring(socket.lastIndexOf('_') + 1)); return slot >= 0 && slot < 128 ? slot : fallback; }
        catch (NumberFormatException invalid) { return fallback; }
    }
    public static int size(JSONObject card) { return card.optString("size").equals("Large") ? 3 : card.optString("size").equals("Medium") ? 2 : 1; }
}
