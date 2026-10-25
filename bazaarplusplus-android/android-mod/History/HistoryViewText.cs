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
            + $" · {Number(row, "battleCount")} 场记录"
            + (Number(row, "startDay") > 1 ? $" · 从第 {Number(row, "startDay")} 天开始记录" : "")
            + (missingFinal ? " · 最终进度未记录" : "");
    }
    internal static string Battle(JsonElement row)
    {
        var opponent = row.TryGetProperty("opening", out var opening) ? Value(opening, "opponentHero") : Value(row, "opponentHero");
        var kind = Value(row, "kind");
        var outcome = Value(row, "outcome") switch { "win" => "胜", "loss" => "负", "draw" => "平", _ => "结果未记录" };
        return $"第 {Number(row, "day")} 天 {Number(row, "hour")} 时 · #{Number(row, "sequence")} · {kind.ToUpperInvariant()} · {outcome}"
            + $"\n对手：{(kind == "pve" ? "野怪（名称未记录）" : opponent)}"
            + (Number(row, "partialOpening") != 0 ? " · 开局信息不完整" : "");
    }
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
                "monsterTemplateId" => "野怪模板 ID", "gameVersion" => "采集时游戏版本", "pluginVersion" => "采集时插件版本", _ => property.Name,
            };
            text.Append(label).Append('：');
            if (property.Value.ValueKind == JsonValueKind.Object) text.Append('\n').Append(Fields(property.Value));
            else text.Append(property.Value.ValueKind == JsonValueKind.Null ? "未记录" : property.Value.ToString());
            text.Append('\n');
        }
        return text.ToString().TrimEnd();
    }
}
