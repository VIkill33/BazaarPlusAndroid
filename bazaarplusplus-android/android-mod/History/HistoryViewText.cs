using System.Text;
using System.Text.Json;

namespace BazaarPlusPlus.Android;

internal static class HistoryViewText
{
    internal static string Value(JsonElement row, string key) => row.TryGetProperty(key, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value.ToString() : "未记录";
    internal static long Number(JsonElement row, string key) => row.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0;
    internal static string Mode(string mode) => mode switch { "Unranked" => "普通", "Ranked" => "排位", _ => mode };
    internal static string Run(JsonElement row)
    {
        var status = Value(row, "status") switch
        {
            "completed" => "已结束", "abandoned" => "已中断",
            _ => Value(row, "sourceKind") == "import" ? "中断记录" : Number(row, "recoveryPending") != 0 ? "待确认续接" : "进行中",
        };
        var missingFinal = Value(row, "reason") == "final_progress_unavailable";
        return $"{Value(row, "hero")} · {Mode(Value(row, "playMode"))} · {status}"
            + $"\n{Date(row, "startedAt")} · {(missingFinal ? "最后观察 " : "")}第 {Number(row, "day")} 天 · {Number(row, "victories")} 胜 / {Number(row, "losses")} 负"
            + $" · {Number(row, "battleCount")} 场 PvP 记录"
            + (Number(row, "startDay") > 1 ? $" · 从第 {Number(row, "startDay")} 天开始记录" : "")
            + (missingFinal ? " · 最终进度未记录" : "");
    }
    internal static string Battle(JsonElement row)
    {
        var opponent = row.TryGetProperty("opening", out var opening) ? Value(opening, "opponentHero") : Value(row, "opponentHero");
        var metadata = row.TryGetProperty("opening", out var details) ? details : row;
        var name = Value(metadata, "opponentName");
        if (name == "未记录" || string.IsNullOrWhiteSpace(name)) name = "玩家名称未记录";
        var outcome = Value(row, "outcome") switch { "win" => "胜", "loss" => "负", "draw" => "平", _ => "结果未记录" };
        return $"第 {Number(row, "day")} 天 {Number(row, "hour")} 时 · #{Number(row, "sequence")} · PvP · {outcome}"
            + $"\n对手：{name} · {opponent}"
            + (Number(row, "partialOpening") != 0 ? " · 开局信息不完整" : "");
    }
    internal static bool HasProgress(JsonElement row) => ValidCount(row, "victories") && ValidCount(row, "losses");
    private static bool ValidCount(JsonElement row, string key) => row.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0;
    internal static string Score(JsonElement row) => HasProgress(row) ? $"{Number(row, "victories")} 胜" : "胜场未记录";
    internal static string Tier(JsonElement row)
    {
        if (Value(row, "reason") == "surrender") return "已投降";
        if (Value(row, "reason") == "final_progress_unavailable") return "最后观察 · 最终进度未记录";
        if (Value(row, "status") != "completed") return Value(row, "status") == "abandoned" ? "已中断" :
            Value(row, "sourceKind") == "import" ? "中断记录" : Number(row, "recoveryPending") != 0
                || Number(row, "pendingIdentity") != 0 ? "待确认续接" : "进行中 · 最后观察";
        if (!HasProgress(row)) return "最终进度未记录";
        var wins = Number(row, "victories");
        return wins == 10 && Number(row, "losses") == 0 ? "钻石胜利" : wins >= 10 ? "黄金胜利"
            : wins >= 7 ? "白银胜利" : wins >= 4 ? "青铜胜利" : "未达奖杯";
    }
    internal static string? Trophy(JsonElement row) => Tier(row) switch
    {
        "青铜胜利" => "Trophy_Bronze_TUI", "白银胜利" => "Trophy_Silver_TUI",
        "黄金胜利" => "Trophy_Gold_TUI", "钻石胜利" => "Trophy_Diamond_TUI", _ => null,
    };
    internal static string Badge(JsonElement row) => Trophy(row) ?? (Tier(row) switch
    {
        "未达奖杯" => "state:broken", "已投降" => "state:flag", "已中断" => "state:pending", _ => "state:pending",
    });
    internal static string RunMeta(JsonElement row) => $"{Date(row, "startedAt")} · {Mode(Value(row, "playMode"))}"
        + $"\n第 {Number(row, "day")} 天 · {Number(row, "battleCount")} 场 PvP"
        + (Number(row, "startDay") > 1 ? " · 中途开始记录" : "");
    internal static JsonElement Opponent(JsonElement row) => row.TryGetProperty("opening", out var opening)
        && opening.ValueKind == JsonValueKind.Object ? opening : row;
    internal static string OpponentName(JsonElement row)
    {
        var name = Value(Opponent(row), "opponentName");
        return name == "未记录" || string.IsNullOrWhiteSpace(name) ? "玩家名称未记录" : name;
    }
    internal static string Result(JsonElement row) => Value(row, "outcome") switch
        { "win" => "胜", "loss" => "负", "draw" => "平", _ => "结果未记录" };
    internal static string Group(JsonElement group)
    {
        var count = group.TryGetProperty("cards", out var cards) ? cards.GetArrayLength() : 0;
        return Value(group, "status") switch
        {
            "captured" => count == 0 ? "完整采集，空阵容" : $"完整采集，{count} 张",
            "partial" => $"部分采集，{count} 张", "failed" => "采集失败", _ => "未采集",
        };
    }
    internal static string Date(JsonElement row, string key)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var time)) return "未记录";
        try { return DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime.ToString("yyyy-MM-dd HH:mm"); }
        catch (ArgumentOutOfRangeException) { return "时间未记录"; }
    }
    internal static string Fields(JsonElement row)
    {
        var text = new StringBuilder();
        foreach (var property in row.EnumerateObject())
        {
            var label = property.Name switch
            {
                "name" => "名称", "instanceId" => "已存实例 ID", "templateId" => "模板 ID", "tier" => "品质",
                "enchantment" => "附魔", "size" => "体型", "type" => "类型", "socket" => "位置", "section" => "所在区域",
                "attributes" => "已保存实例属性", "tags" => "标签", "playerHero" => "我方英雄", "opponentHero" => "对方英雄",
                "opponentName" => "对手名称", "gameVersion" => "采集时游戏版本", "pluginVersion" => "采集时插件版本", _ => property.Name,
            };
            text.Append(label).Append('：');
            if (property.Value.ValueKind == JsonValueKind.Object) text.Append('\n').Append(Fields(property.Value));
            else text.Append(property.Value.ValueKind == JsonValueKind.Null ? "未记录" : property.Value.ToString());
            text.Append('\n');
        }
        return text.ToString().TrimEnd();
    }
}
