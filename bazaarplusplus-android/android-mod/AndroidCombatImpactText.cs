using System.Globalization;

namespace BazaarPlusPlus.Android;

// Number formats mirror desktop CombatImpactMetricFormatter. Chinese labels use the mobile
// game's own keywords (灼烧／剧毒), not desktop CombatImpactAttributeLabel's 燃烧／中毒.
internal static class AndroidCombatImpactText
{
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
        CombatImpactKind.Attribute => chinese ? "属性变化" : "Attribute Change",
        _ => chinese ? "摧毁" : "Destroy",
    };

    // Desktop CombatImpactAttributeLabel, limited to card and hero attributes a combat can modify.
    internal static string AttributeLabel(string? key, long? change, bool mixed, bool chinese)
    {
        var (zh, en) = key switch
        {
            "AmmoMax" => ("最大弹药", "Max Ammo"),
            "ReloadAmount" => ("装填", "Reload"),
            "Reload" => ("装填", "Reload"),
            "CooldownMax" => ("冷却时间", "Cooldown"),
            "ChargeAmount" => ("充能", "Charge"),
            "HasteAmount" => ("加速", "Haste"),
            "SlowAmount" => ("减速", "Slow"),
            "FreezeAmount" => ("冻结", "Freeze"),
            "BurnApplyAmount" => ("灼烧", "Burn"),
            "BurnRemoveAmount" => ("移除灼烧", "Burn Removed"),
            "PoisonApplyAmount" => ("剧毒", "Poison"),
            "PoisonRemoveAmount" => ("移除剧毒", "Poison Removed"),
            "RegenApplyAmount" => ("再生", "Regen"),
            "RegenRemoveAmount" => ("移除再生", "Regen Removed"),
            "RageApplyAmount" => ("怒气", "Rage"),
            "RageRemoveAmount" => ("移除怒气", "Rage Removed"),
            "Multicast" => ("多重施放", "Multicast"),
            "Lifesteal" => ("吸血", "Lifesteal"),
            "CritChance" => ("暴击率", "Crit Chance"),
            "DamageAmount" => ("伤害", "Damage"),
            "DamageCrit" => ("伤害暴击", "Damage Crit"),
            "HealAmount" => ("治疗", "Heal"),
            "HealCrit" => ("治疗暴击", "Heal Crit"),
            "ShieldApplyAmount" => ("护盾", "Shield"),
            "ShieldRemoveAmount" => ("移除护盾", "Shield Removed"),
            "ShieldCrit" => ("护盾暴击", "Shield Crit"),
            "BurnCrit" => ("灼烧暴击", "Burn Crit"),
            "PoisonCrit" => ("剧毒暴击", "Poison Crit"),
            "RegenCrit" => ("再生暴击", "Regen Crit"),
            "HealthMax" => ("最大生命值", "Max Health"),
            "FlatCooldownReduction" => ("固定冷却缩减", "Flat Cooldown Reduction"),
            "PercentCooldownReduction" => ("冷却缩减", "Cooldown Reduction"),
            "PercentChargeReduction" => ("充能缩减", "Charge Reduction"),
            "PercentHasteReduction" => ("加速缩减", "Haste Reduction"),
            "PercentSlowReduction" => ("减速抗性", "Slow Resistance"),
            "PercentFreezeReduction" => ("冻结抗性", "Freeze Resistance"),
            "DestroyImmunity" => ("摧毁免疫", "Destroy Immunity"),
            "BuyPrice" => ("购买价格", "Buy Price"),
            "SellPrice" => ("出售价格", "Sell Price"),
            "TempoCost" => ("节奏消耗", "Tempo Cost"),
            "TempoApplyAmount" => ("节奏获取量", "Tempo Gain"),
            "TempoRemoveAmount" => ("节奏移除量", "Tempo Removal"),
            _ => ("属性变化", "Attribute Change"),
        };
        if (key is not ("AmmoMax" or "ReloadAmount" or "ChargeAmount" or "HasteAmount" or "SlowAmount" or "FreezeAmount"
            or "BurnApplyAmount" or "PoisonApplyAmount" or "RegenApplyAmount" or "RageApplyAmount" or "Multicast"
            or "Lifesteal" or "CritChance" or "DamageAmount" or "HealAmount" or "ShieldApplyAmount"))
            return chinese ? zh : en;
        if (mixed) return chinese ? zh + "变化" : en + " Change";
        if (change > 0) return chinese ? zh + "增加" : en + " Gain";
        if (change < 0) return chinese ? zh + "减少" : en + " Loss";
        return chinese ? zh : en;
    }

    internal static string Value(CombatImpactKind kind, long value) =>
        CombatImpactReport.IsDuration(kind) ? Duration(value) : Integer(value);

    // Desktop CombatImpactProjector.UnitFor; attribute deltas are signed.
    internal static string AttributeValue(string? key, long value)
    {
        var text = key == "Lifesteal" || key?.Contains("Percent", StringComparison.Ordinal) == true
            || key?.Contains("CritChance", StringComparison.Ordinal) == true ? Integer(value) + "%"
            : key is "CooldownMax" or "ChargeAmount" or "HasteAmount" or "SlowAmount" or "FreezeAmount" or "FlatCooldownReduction"
                ? Duration(value)
            : Integer(value);
        return value > 0 ? "+" + text : text;
    }

    private static string Group(CombatImpactGroup group, bool chinese)
    {
        var attribute = group.Kind == CombatImpactKind.Attribute;
        string Format(long value) => attribute ? AttributeValue(group.Attribute, value) : Value(group.Kind, value);
        var parts = new List<string>();
        if (group.Count > 0)
        {
            var count = $"×{group.Count}";
            if (group.CriticalCount > 0)
                count = chinese ? $"{count}（暴击 {group.CriticalCount}）" : $"{count} ({group.CriticalCount} crit)";
            parts.Add(count);
        }
        if (group.Total is { } total)
            parts.Add(chinese ? $"总计 {Format(total)}" : $"{Format(total)} total");
        var label = attribute ? AttributeLabel(group.Attribute, group.Total, group.Mixed, chinese) : Label(group.Kind, chinese);
        var head = AndroidPreviewText.Colored(label, Color(group.Kind))
            + (parts.Count == 0 ? "" : " " + string.Join(" · ", parts));
        var details = new List<string>();
        if (Periodic(group, chinese) is { } periodic) details.Add(periodic);
        if (group.Triggers.Count > 0)
        {
            var prefix = chinese ? "触发来源：" : "Triggered by: ";
            var sources = group.Triggers.Select(line => $"{line.Name} ×{line.Count}").ToArray();
            details.Add(AndroidPreviewText.Muted(sources.Length == 1
                ? prefix + sources[0]
                : prefix + "\n" + string.Join("\n", sources)));
        }
        var lines = group.Lines.Select(line => line.Value is { } value
            ? $"{line.Name} ×{line.Count} · {Format(value)}" : $"{line.Name} ×{line.Count}").ToList();
        if (lines.Count > 0) details.Add(AndroidPreviewText.Muted(string.Join("\n", lines)));
        return AndroidPreviewText.Bullet(details.Count == 0 ? head : head + "\n" + string.Join("\n", details),
            "<color=#" + Color(group.Kind) + ">•</color>");
    }

    private static string? Periodic(CombatImpactGroup group, bool chinese)
    {
        if (group.Periodic is not { } impact || (impact.Health <= 0 && impact.Shield <= 0)) return null;
        var parts = new List<string>();
        if (impact.Health > 0)
            parts.Add(group.Kind == CombatImpactKind.Regen
                ? chinese ? $"实际治疗 {Integer(impact.Health)}" : $"Healed {Integer(impact.Health)}"
                : chinese ? $"实际伤害 {Integer(impact.Health)}" : $"Dealt {Integer(impact.Health)}");
        if (impact.Shield > 0)
            parts.Add(chinese ? $"消耗护盾 {Integer(impact.Shield)}" : $"Shield consumed {Integer(impact.Shield)}");
        return string.Join(" · ", parts);
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
        CombatImpactKind.Attribute => "E3C58F",
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
