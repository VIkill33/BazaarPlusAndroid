namespace BazaarPlusPlus.Android;

internal enum CombatImpactKind { Damage, Burn, Poison, Healing, Shield, Regen, Charge, Haste, Slow, Freeze, Destroy }

internal enum CombatImpactEntityKind { Item, Skill, Player }

internal sealed record CombatImpactEntity(string Id, string Name, CombatImpactEntityKind Kind);

// Value is the source card's configured duration (ms) for timing kinds; amounts come from
// native CardStats totals instead, never from summed observations (desktop ADR-0008).
internal readonly record struct CombatImpactExecution(
    string SourceId, string TargetId, CombatImpactKind Kind, int? Value = null, bool Critical = false);

internal sealed record CombatImpactLine(string Name, int Count, long? Value);

internal sealed record CombatImpactGroup(
    CombatImpactKind Kind, int Count, int CriticalCount, long? Total, IReadOnlyList<CombatImpactLine> Lines);

internal sealed class CombatImpactReport(
    IReadOnlyDictionary<string, CombatImpactEntity> entities,
    IReadOnlyList<CombatImpactExecution> executions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<CombatImpactKind, int>> totals,
    IReadOnlyDictionary<string, int> uses)
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
        _ => null,
    };

    // A frame proves crits only when every or no execution of the kind on that hero crit;
    // anything in between cannot be assigned to particular executions.
    internal static bool? CriticalFrame(int executions, int criticalAdjustments) =>
        criticalAdjustments == 0 ? false : criticalAdjustments == executions ? true : null;

    internal bool Contains(string id) => entities.ContainsKey(id);

    internal int UseCount(string id) => uses.TryGetValue(id, out var count) ? count : 0;

    internal IReadOnlyList<CombatImpactGroup> Caused(string id)
    {
        totals.TryGetValue(id, out var native);
        var kinds = executions.Where(item => item.SourceId == id).Select(item => item.Kind)
            .Concat(native?.Where(pair => pair.Value > 0 && HasNativeTotal(pair.Key)).Select(pair => pair.Key) ?? [])
            .Distinct().Order();
        return kinds.Select(kind =>
        {
            var items = executions.Where(item => item.SourceId == id && item.Kind == kind).ToArray();
            long? total = HasNativeTotal(kind)
                ? native != null && native.TryGetValue(kind, out var value) && value > 0 ? value : null
                : Sum(items);
            var cards = items.Where(item => IsCard(item.TargetId)).ToArray();
            return new CombatImpactGroup(kind, items.Length, items.Count(item => item.Critical), total,
                Lines(cards, item => item.TargetId));
        }).ToArray();
    }

    // Hero targets stay out of the received view, so it is never a caused-vs-received balance.
    internal IReadOnlyList<CombatImpactGroup> Received(string id)
    {
        if (!IsCard(id)) return [];
        return executions.Where(item => item.TargetId == id).GroupBy(item => item.Kind).OrderBy(group => group.Key)
            .Select(group =>
            {
                var items = group.ToArray();
                return new CombatImpactGroup(group.Key, items.Length, items.Count(item => item.Critical),
                    IsDuration(group.Key) ? Sum(items) : null, Lines(items, item => item.SourceId));
            }).ToArray();
    }

    private bool IsCard(string id) =>
        entities.TryGetValue(id, out var entity) && entity.Kind != CombatImpactEntityKind.Player;

    private static long? Sum(IReadOnlyCollection<CombatImpactExecution> items) =>
        items.Count > 0 && items.All(item => item.Value.HasValue) ? items.Sum(item => (long)item.Value!.Value) : null;

    private IReadOnlyList<CombatImpactLine> Lines(IEnumerable<CombatImpactExecution> items, Func<CombatImpactExecution, string> key) =>
        items.GroupBy(key).Select(group =>
            {
                var array = group.ToArray();
                var name = entities.TryGetValue(group.Key, out var entity) ? entity.Name : group.Key;
                return new CombatImpactLine(name, array.Length, Sum(array));
            })
            .OrderByDescending(line => line.Count).ThenByDescending(line => line.Value ?? 0)
            .ThenBy(line => line.Name, StringComparer.Ordinal).ToArray();
}
