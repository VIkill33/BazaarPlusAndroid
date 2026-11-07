using System.Globalization;

namespace BazaarPlusPlus.Android;

// Number formats mirror desktop CombatImpactMetricFormatter. Chinese labels use the mobile
// game's own keywords (灼烧／剧毒), not desktop CombatImpactAttributeLabel's 燃烧／中毒.
internal static class AndroidCombatImpactText
{
    private const int MaxLines = 3;

    internal static string Build(CombatImpactReport report, string id, bool received, bool chinese)
    {
        var groups = received ? report.Received(id) : report.Caused(id);
        var uses = report.UseCount(id);
        var summary = received || uses <= 0 ? null
            : AndroidPreviewText.Muted(chinese ? $"使用 {uses} 次" : $"{uses} use{(uses == 1 ? "" : "s")}");
        if (groups.Count == 0)
            return AndroidPreviewText.Lines(summary, AndroidPreviewText.Muted(received
                ? chinese ? "本场未受到其他卡牌的效果。" : "No effects received from other cards."
                : chinese ? "本场未造成可统计的效果。" : "No recorded effects this combat."));
        return AndroidPreviewText.Lines(summary, string.Join("\n", groups.Select(group => Group(group, chinese))));
    }

    internal static string Label(CombatImpactKind kind, bool chinese) => kind switch
    {
        CombatImpactKind.Damage => chinese ? "伤害" : "Damage",
        CombatImpactKind.Burn => chinese ? "灼烧" : "Burn",
        CombatImpactKind.Poison => chinese ? "剧毒" : "Poison",
        CombatImpactKind.Healing => chinese ? "治疗" : "Heal",
        CombatImpactKind.Shield => chinese ? "护盾" : "Shield",
        CombatImpactKind.Regen => chinese ? "再生" : "Regen",
        CombatImpactKind.Charge => chinese ? "充能" : "Charge",
        CombatImpactKind.Haste => chinese ? "加速" : "Haste",
        CombatImpactKind.Slow => chinese ? "减速" : "Slow",
        CombatImpactKind.Freeze => chinese ? "冻结" : "Freeze",
        _ => chinese ? "摧毁" : "Destroy",
    };

    internal static string Value(CombatImpactKind kind, long value) =>
        CombatImpactReport.IsDuration(kind) ? Duration(value) : Integer(value);

    private static string Group(CombatImpactGroup group, bool chinese)
    {
        var parts = new List<string>();
        if (group.Count > 0)
        {
            var count = $"×{group.Count}";
            if (group.CriticalCount > 0)
                count = chinese ? $"{count}（暴击 {group.CriticalCount}）" : $"{count} ({group.CriticalCount} crit)";
            parts.Add(count);
        }
        if (group.Total is { } total)
            parts.Add(chinese ? $"总计 {Value(group.Kind, total)}" : $"{Value(group.Kind, total)} total");
        var head = AndroidPreviewText.Colored(Label(group.Kind, chinese), Color(group.Kind))
            + (parts.Count == 0 ? "" : " " + string.Join(" · ", parts));
        var lines = group.Lines.Take(MaxLines).Select(line => line.Value is { } value
            ? $"{line.Name} ×{line.Count} · {Value(group.Kind, value)}" : $"{line.Name} ×{line.Count}").ToList();
        var hidden = group.Lines.Count - MaxLines;
        if (hidden > 0) lines.Add(chinese ? $"另 {hidden} 张" : $"+{hidden} more");
        return AndroidPreviewText.Bullet(lines.Count == 0 ? head
            : head + "\n" + AndroidPreviewText.Muted(string.Join("\n", lines)), "<color=#" + Color(group.Kind) + ">•</color>");
    }

    private static string Color(CombatImpactKind kind) => kind switch
    {
        CombatImpactKind.Damage => "F07A5E",
        CombatImpactKind.Burn => "F5A04A",
        CombatImpactKind.Poison => "8ED36B",
        CombatImpactKind.Healing => "7FD98A",
        CombatImpactKind.Shield => "F2D16B",
        CombatImpactKind.Regen => "6CCFB0",
        CombatImpactKind.Charge => "B9A3F2",
        CombatImpactKind.Haste => "F0D85A",
        CombatImpactKind.Slow => "C9A27A",
        CombatImpactKind.Freeze => "86D0F2",
        _ => "D07070",
    };

    private static string Duration(long milliseconds)
    {
        var seconds = milliseconds / 1000m;
        return (decimal.Truncate(seconds) == seconds
            ? seconds.ToString("0", CultureInfo.InvariantCulture)
            : seconds.ToString("0.00", CultureInfo.InvariantCulture)) + "s";
    }

    private static string Integer(long value) =>
        value is <= -1_000_000_000 or >= 1_000_000_000 ? Scaled(value, 1_000_000_000, "B")
        : value is <= -1_000_000 or >= 1_000_000 ? Scaled(value, 1_000_000, "M")
        : value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Scaled(long value, long scale, string suffix) =>
        ((decimal)value / scale).ToString("0.##", CultureInfo.InvariantCulture) + suffix;
}
