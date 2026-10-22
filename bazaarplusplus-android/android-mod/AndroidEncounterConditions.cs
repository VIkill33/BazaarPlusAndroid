using BazaarGameShared.Domain.Prerequisites;
using BazaarGameShared.Domain.Prerequisites.Conditionals;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Spawning.SpawnFilters.Constraints;
using BazaarGameShared.Domain.Values;
using Il2CppInterop.Runtime;
using TheBazaar.Localization;
using TheBazaar.Utilities;

namespace BazaarPlusPlus.Android;

// Explicit Android proxy casts: CLR pattern matching sees the interface wrapper,
// rather than the native concrete conditional type.
internal static class AndroidEncounterConditions
{
    internal static string Label<T>(T value)
    {
        var key = value?.ToString() ?? string.Empty;
        return key switch
        {
            "Item" => Text("Item", "物品"), "Skill" => Text("Skill", "技能"),
            "SelfHand" or "AbsolutePlayerHand" => Text("Your items", "己方场上物品"),
            "SelfStash" or "AbsolutePlayerStash" => Text("Your stash", "己方仓库"),
            "SelfHandAndStash" or "AbsolutePlayerHandAndStash" => Text("Your board and stash", "己方场上及仓库"),
            "SelfSkills" or "AbsolutePlayerSkills" => Text("Your skills", "己方技能"),
            "SelfBoard" => Text("Your board", "己方场上"),
            _ => new LocalizableText(key).GetLocalizedText(),
        };
    }
    private static string Text(string en, string zh) => NativeUi.Text(en, zh);
    private static string Not(bool isNot, string value) => isNot ? Text("excluding ", "非：") + value : value;
    internal static IEnumerable<T> Read<T>(Il2CppSystem.Collections.Generic.List<T> values)
    {
        for (var index = 0; index < values.Count; index++) yield return values[index];
    }
    internal static IEnumerable<T> Read<T>(Il2CppSystem.Collections.Generic.HashSet<T> values)
    {
        foreach (var value in values) yield return value;
    }
    private static string Join<T>(Il2CppSystem.Collections.Generic.HashSet<T>? values) =>
        values == null ? string.Empty : string.Join(" / ", Read(values).Select(Label));
    private static string Op(EComparisonOperator comparison) => comparison.ToString() switch
    {
        "Equal" => "=", "NotEqual" => "≠", "GreaterThan" => ">", "GreaterThanOrEqual" => "≥",
        "LessThan" => "<", "LessThanOrEqual" => "≤", _ => comparison.ToString(),
    };
    private static string ListOp(EListComparisonOperator op, string value) => op.ToString() switch
    {
        "None" => Text("none of ", "不含：") + value,
        "All" => Text("all of ", "全部：") + value,
        _ => Text("any of ", "任一：") + value,
    };

    internal static string Prerequisites(Il2CppSystem.Collections.Generic.List<ITPrerequisite>? values, ValueContext context)
    {
        if (values == null || values.Count == 0) return string.Empty;
        var lines = new List<string>();
        foreach (var value in values)
        {
            if (value.TryCast<TPrerequisiteCardCount>() is { } count)
            {
                var scope = count.Subject?.TryCast<BazaarGameShared.Domain.Targeting.TTargetCardSection>();
                var section = scope == null ? string.Empty : Label(scope.TargetSection) + " · ";
                lines.Add(section + Card(count.Subject?.Conditions, context) + " " +
                    Text("count", "数量") + $" {Op(count.Comparison)} {count.Amount}");
            }
            else if (value.TryCast<TPrerequisiteRun>() is { } run) lines.Add(Run(run.Conditions, context));
            else if (value.TryCast<TPrerequisitePlayer>() is { } player) lines.Add(Player(player.Subject?.Conditions, context));
            else lines.Add(Text("Additional condition could not be resolved", "另有条件暂未解析"));
        }
        return string.Join(Text(" AND ", " 且 "), lines.Distinct());
    }

    internal static string Card(ITCardConditional? value, ValueContext context, int depth = 0)
    {
        if (value == null) return Text("cards", "卡牌");
        if (depth > 10) return Text("nested condition unavailable", "嵌套条件暂未解析");
        if (value.TryCast<TCardConditionalAnd>() is { } and)
            return "(" + string.Join(Text(" AND ", " 且 "), Read(and.Conditions).Select(c => Card(c, context, depth + 1))) + ")";
        if (value.TryCast<TCardConditionalOr>() is { } or)
            return "(" + string.Join(Text(" OR ", " 或 "), Read(or.Conditions).Select(c => Card(c, context, depth + 1))) + ")";
        if (value.TryCast<TCardConditionalId>() is { } id) return Not(id.IsNot, AndroidEventPreview.ResolveCardName(id.Id) ?? Text("unknown card", "未知卡牌"));
        if (value.TryCast<TCardConditionalTag>() is { } tag) return ListOp(tag.Operator, Join(tag.Tags));
        if (value.TryCast<TCardConditionalHiddenTag>() is { } hidden) return ListOp(hidden.Operator, Join(hidden.Tags));
        if (value.TryCast<TCardConditionalHero>() is { } hero) return ListOp(hero.Operator, Join(hero.Heroes));
        if (value.TryCast<TCardConditionalState>() is { } state) return ListOp(state.Operator, Join(state.States));
        if (value.TryCast<TCardConditionalType>() is { } type) return Not(type.IsNot, Label(type.CardType));
        if (value.TryCast<TCardConditionalTier>() is { } tier) return Not(tier.IsNot, Join(tier.Tiers));
        if (value.TryCast<TCardConditionalSize>() is { } size) return Not(size.IsNot, Join(size.Sizes));
        if (value.TryCast<TCardConditionalAttribute>() is { } attribute)
            return $"{Label(attribute.Attribute)} {Op(attribute.ComparisonOperator)} {Number(attribute.ComparisonValue, context)}";
        if (value.TryCast<TCardConditionalPlayerHero>() is { } own)
            return own.IsSameAsPlayerHero ? Text("your hero's cards", "当前英雄的卡牌") : Text("other heroes' cards", "其他英雄的卡牌");
        return Text("card condition not yet supported", "卡牌条件暂未解析");
    }

    private static string Run(ITRunConditional? value, ValueContext context, int depth = 0)
    {
        if (value == null || depth > 10) return Text("run condition unavailable", "对局条件暂未解析");
        if (value.TryCast<TRunConditionalAnd>() is { } and)
            return "(" + string.Join(Text(" AND ", " 且 "), Read(and.Conditions).Select(c => Run(c, context, depth + 1))) + ")";
        if (value.TryCast<TRunConditionalOr>() is { } or)
            return "(" + string.Join(Text(" OR ", " 或 "), Read(or.Conditions).Select(c => Run(c, context, depth + 1))) + ")";
        if (value.TryCast<TRunConditionalCurrentDay>() is { } day) return Text("Day", "天数") + $" {Op(day.ComparisonOperator)} {day.CurrentDay}";
        if (value.TryCast<TRunConditionalCurrentHour>() is { } hour) return Text("Hour", "时段") + $" {Op(hour.ComparisonOperator)} {hour.CurrentHour}";
        if (value.TryCast<TRunConditionalPlayerHero>() is { } hero) return Text("Hero: ", "英雄：") + ListOp(hero.Operator, Join(hero.Heroes));
        if (value.TryCast<TRunConditionalCurrentEncounter>() is { } encounter) return Card(encounter.Conditions, context);
        if (value.TryCast<TRunConditionalHasVisitedEncounter>() is { } visited)
            return Text("Visited: ", "已到访：") + ListOp(visited.Operator, string.Join(" / ", Read(visited.EncounterIds).Select(id => AndroidEventPreview.ResolveCardName(id))));
        return Text("run condition not yet supported", "对局条件暂未解析");
    }

    private static string Player(ITPlayerConditional? value, ValueContext context, int depth = 0)
    {
        if (value == null || depth > 10) return Text("player condition unavailable", "玩家条件暂未解析");
        if (value.TryCast<TPlayerConditionalAnd>() is { } and)
            return "(" + string.Join(Text(" AND ", " 且 "), Read(and.Conditions).Select(c => Player(c, context, depth + 1))) + ")";
        if (value.TryCast<TPlayerConditionalOr>() is { } or)
            return "(" + string.Join(Text(" OR ", " 或 "), Read(or.Conditions).Select(c => Player(c, context, depth + 1))) + ")";
        if (value.TryCast<TPlayerConditionalAttribute>() is { } attribute)
            return $"{Label(attribute.Attribute)} {Op(attribute.ComparisonOperator)} {Number(attribute.ComparisonValue, context)}";
        return Text("player condition not yet supported", "玩家条件暂未解析");
    }

    internal static string Number(ITValue? value, ValueContext context) => value == null
        ? "?" : value.GetValue(context).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    internal static string Pool(IConstraint? value, int depth = 0)
    {
        if (value == null) return Text("random card", "随机卡牌");
        if (depth > 10) return Text("nested pool unavailable", "嵌套卡池暂未解析");
        if (value.TryCast<ConstraintAnd>() is { } and)
            return "(" + string.Join(Text(" AND ", " 且 "), Read(and.Constraints).Select(c => Pool(c, depth + 1))) + ")";
        if (value.TryCast<ConstraintOr>() is { } or)
            return "(" + string.Join(Text(" OR ", " 或 "), Read(or.Constraints).Select(c => Pool(c, depth + 1))) + ")";
        if (value.TryCast<ConstraintCardType>() is { } type) return Not(type.IsNot, Join(type.Types));
        if (value.TryCast<ConstraintTag>() is { } tag) return Not(tag.IsNot, Join(tag.Tags));
        if (value.TryCast<ConstraintHiddenTag>() is { } hidden) return Not(hidden.IsNot, Join(hidden.HiddenTags));
        if (value.TryCast<ConstraintTier>() is { } tier) return Not(tier.IsNot, Join(tier.Tiers));
        if (value.TryCast<ConstraintSize>() is { } size) return Not(size.IsNot, Join(size.Sizes));
        if (value.TryCast<ConstraintHero>() is { } hero) return Not(hero.IsNot, Join(hero.Heroes));
        if (value.TryCast<ConstraintId>() is { } id)
            return Not(id.IsNot, string.Join(" / ", Read(id.Ids).Select(i => AndroidEventPreview.ResolveCardName(i))));
        if (value.TryCast<ConstraintEnchantmentEligible>() is { } enchant) return Not(enchant.IsNot, Text("Enchantable: ", "可附魔：") + Join(enchant.Enchantments));
        return Text("additional pool filter not yet supported", "另有卡池条件暂未解析");
    }
}
