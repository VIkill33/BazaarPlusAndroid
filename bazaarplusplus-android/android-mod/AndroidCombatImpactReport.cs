namespace BazaarPlusPlus.Android;

internal enum CombatImpactKind { Damage, Burn, Poison, Healing, Shield, Regen, Charge, Haste, Slow, Freeze, Destroy, Attribute }

internal enum CombatImpactEntityKind { Item, Skill, Player }

internal sealed record CombatImpactEntity(string Id, string Name, CombatImpactEntityKind Kind);

// Value is the source card's configured duration (ms) for timing kinds and the target's net frame
// delta for attribute changes; amounts come from native CardStats totals instead, never from
// summed observations (desktop ADR-0008). Frame identifies the activation batch for trigger counts.
internal readonly record struct CombatImpactExecution(
    string SourceId, string TargetId, CombatImpactKind Kind, int? Value = null, bool Critical = false,
    string? Attribute = null, string? TriggerId = null, int Frame = 0);

internal sealed record CombatImpactLine(string Name, int Count, long? Value);

// Realized periodic movement: health damage (or regen healing) and shield consumed by burn.
internal readonly record struct CombatImpactPeriodic(long Health, long Shield);

internal sealed record CombatImpactGroup(
    CombatImpactKind Kind, int Count, int CriticalCount, long? Total, IReadOnlyList<CombatImpactLine> Lines)
{
    internal string? Attribute { get; init; }
    internal bool Mixed { get; init; }
    internal CombatImpactPeriodic? Periodic { get; init; }
    internal IReadOnlyList<CombatImpactLine> Triggers { get; init; } = [];
}

internal sealed class CombatImpactReport(
    IReadOnlyDictionary<string, CombatImpactEntity> entities,
    IReadOnlyList<CombatImpactExecution> executions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<CombatImpactKind, int>> totals,
    IReadOnlyDictionary<string, int> uses,
    IReadOnlyDictionary<(string Source, CombatImpactKind Kind), CombatImpactPeriodic>? periodic = null)
{
    internal static readonly CombatImpactReport Empty = new(
        new Dictionary<string, CombatImpactEntity>(), [],
        new Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>(), new Dictionary<string, int>());

    internal static string PlayerId(bool opponent) => opponent ? "player:Opponent" : "player:Player";

    internal static bool HasNativeTotal(CombatImpactKind kind) => kind <= CombatImpactKind.Regen;

    internal static bool IsDuration(CombatImpactKind kind) =>
        kind is CombatImpactKind.Charge or CombatImpactKind.Haste or CombatImpactKind.Slow or CombatImpactKind.Freeze;

    internal static CombatImpactKind? Kind(string action) => action switch
    {
        "PlayerDamage" => CombatImpactKind.Damage,
        "PlayerBurnApply" => CombatImpactKind.Burn,
        "PlayerPoisonApply" => CombatImpactKind.Poison,
        "PlayerHeal" => CombatImpactKind.Healing,
        "PlayerShieldApply" => CombatImpactKind.Shield,
        "PlayerRegenApply" => CombatImpactKind.Regen,
        "CardCharge" => CombatImpactKind.Charge,
        "CardHaste" => CombatImpactKind.Haste,
        "CardSlow" => CombatImpactKind.Slow,
        "CardFreeze" => CombatImpactKind.Freeze,
        "CardDestroy" => CombatImpactKind.Destroy,
        "CardModifyAttribute" or "CardReload" or "PlayerMaxHealthIncrease" or "PlayerMaxHealthDecrease"
            => CombatImpactKind.Attribute,
        _ => null,
    };

    // An attribute change without a resolved key, or on a designer scratch slot, is not shown (desktop
    // CombatImpactProjector.IsDisplayableAttributeChange).
    internal static bool Displayable(string? attribute) =>
        !string.IsNullOrEmpty(attribute) && !attribute.StartsWith("Custom_", StringComparison.Ordinal);

    // A frame proves crits only when every or no execution of the kind on that hero crit;
    // anything in between cannot be assigned to particular executions.
    internal static bool? CriticalFrame(int executions, int criticalAdjustments) =>
        criticalAdjustments == 0 ? false : criticalAdjustments == executions ? true : null;

    internal bool Contains(string id) => entities.ContainsKey(id);

    internal int UseCount(string id) => uses.TryGetValue(id, out var count) ? count : 0;

    internal IReadOnlyList<CombatImpactGroup> Caused(string id)
    {
        totals.TryGetValue(id, out var native);
        var keys = executions.Where(item => item.SourceId == id).Select(item => (item.Kind, item.Attribute))
            .Concat(native?.Where(pair => pair.Value > 0 && HasNativeTotal(pair.Key))
                .Select(pair => (pair.Key, (string?)null)) ?? [])
            .Distinct().OrderBy(key => key.Item1).ThenBy(key => key.Item2, StringComparer.Ordinal);
        return keys.Select(key =>
        {
            var (kind, attribute) = key;
            var items = executions.Where(item => item.SourceId == id && item.Kind == kind && item.Attribute == attribute)
                .ToArray();
            long? total = HasNativeTotal(kind)
                ? native != null && native.TryGetValue(kind, out var value) && value > 0 ? value : null
                : Sum(items);
            var cards = items.Where(item => IsCard(item.TargetId)).ToArray();
            CombatImpactPeriodic? impact = periodic != null && periodic.TryGetValue((id, kind), out var found) ? found : null;
            return new CombatImpactGroup(kind, items.Length, items.Count(item => item.Critical), total,
                Lines(cards, item => item.TargetId))
            {
                Attribute = attribute,
                Mixed = MixedDirections(items),
                Periodic = impact,
                Triggers = Triggers(id, items),
            };
        }).ToArray();
    }

    // Hero targets stay out of the received view, so it is never a caused-vs-received balance;
    // a card's effects on itself are caused, not received.
    internal IReadOnlyList<CombatImpactGroup> Received(string id)
    {
        if (!IsCard(id)) return [];
        return executions.Where(item => item.TargetId == id && item.SourceId != id)
            .GroupBy(item => (item.Kind, item.Attribute))
            .OrderBy(group => group.Key.Kind).ThenBy(group => group.Key.Attribute, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToArray();
                return new CombatImpactGroup(group.Key.Kind, items.Length, items.Count(item => item.Critical),
                    IsDuration(group.Key.Kind) || group.Key.Kind == CombatImpactKind.Attribute ? Sum(items) : null,
                    Lines(items, item => item.SourceId))
                {
                    Attribute = group.Key.Attribute,
                    Mixed = MixedDirections(items),
                };
            }).ToArray();
    }

    private bool IsCard(string id) =>
        entities.TryGetValue(id, out var entity) && entity.Kind != CombatImpactEntityKind.Player;

    private static long? Sum(IReadOnlyCollection<CombatImpactExecution> items) =>
        items.Count > 0 && items.All(item => item.Value.HasValue) ? items.Sum(item => (long)item.Value!.Value) : null;

    private static bool MixedDirections(IReadOnlyCollection<CombatImpactExecution> items) =>
        items.Any(item => item.Value > 0) && items.Any(item => item.Value < 0);

    // Counts distinct trigger-source frames (desktop ObservedActivationBatchCount); a card
    // triggering itself is not a trigger source worth naming.
    private IReadOnlyList<CombatImpactLine> Triggers(string id, IEnumerable<CombatImpactExecution> items) =>
        items.Where(item => item.TriggerId != null && item.TriggerId != id)
            .GroupBy(item => item.TriggerId!)
            .Select(group => new CombatImpactLine(Name(group.Key), group.Select(item => item.Frame).Distinct().Count(), null))
            .OrderByDescending(line => line.Count).ThenBy(line => line.Name, StringComparer.Ordinal).ToArray();

    private string Name(string id) => entities.TryGetValue(id, out var entity) ? entity.Name : id;

    private IReadOnlyList<CombatImpactLine> Lines(IEnumerable<CombatImpactExecution> items, Func<CombatImpactExecution, string> key) =>
        items.GroupBy(key).Select(group =>
            {
                var array = group.ToArray();
                return new CombatImpactLine(Name(group.Key), array.Length, Sum(array));
            })
            .OrderByDescending(line => line.Count).ThenByDescending(line => line.Value ?? 0)
            .ThenBy(line => line.Name, StringComparer.Ordinal).ToArray();
}
