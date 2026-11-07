namespace BazaarPlusPlus.Android;

internal enum PeriodicStatus { Burn, Poison, Regen }

internal enum PeriodicPool { Health, Shield, Other }

internal enum PeriodicDamage { Damage, Burn, Poison, Heal, Shield, Regen, Other }

internal enum PeriodicAttribute { Health, HealthMax, Shield, Burn, Poison, HealthRegen }

internal readonly record struct PeriodicAdjustment(PeriodicDamage Type, PeriodicPool Pool, int Amount);

internal readonly record struct PeriodicTransition(int Previous, int Current);

// One combatant in one frame. A null apply source is an execution whose card could not be resolved.
internal sealed class PeriodicSide
{
    internal bool Died;
    internal readonly List<PeriodicAdjustment> Adjustments = new();
    internal readonly Dictionary<PeriodicAttribute, PeriodicTransition> Attributes = new();
    internal readonly Dictionary<PeriodicStatus, List<string?>> Applies = new();
}

internal sealed record PeriodicFrame(PeriodicSide? Player, PeriodicSide? Opponent);

// Managed port of desktop PeriodicEffectAttribution (periodic-impact-v7): book typed burn/poison
// health and burn shield movement as realized impact (capped at remaining health on the death
// frame), reconstruct accepted regen through the health ledger, and split each tick by the
// status stacks each source currently owns. Unknown shares and the Exact/Proportional proof stay
// internal; only realized per-source amounts leave this class.
internal static class CombatImpactPeriodicLedger
{
    private const string Unknown = "\0unknown-periodic-source";
    private static readonly PeriodicStatus[] Statuses = [PeriodicStatus.Burn, PeriodicStatus.Poison, PeriodicStatus.Regen];

    internal static IReadOnlyDictionary<(string Source, CombatImpactKind Kind), CombatImpactPeriodic> Project(
        IReadOnlyList<PeriodicFrame> frames, Func<string, PeriodicStatus, int> weight)
    {
        var result = new Dictionary<(string, CombatImpactKind), CombatImpactPeriodic>();
        foreach (var opponent in new[] { false, true })
        {
            PeriodicSide? SideOf(PeriodicFrame frame) => opponent ? frame.Opponent : frame.Player;
            var ledger = BuildLedger(frames.Select(SideOf));
            var regenFallback = frames.Select(SideOf)
                .SelectMany(side => side?.Applies.GetValueOrDefault(PeriodicStatus.Regen) ?? [])
                .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach (var frame in frames)
            {
                var side = SideOf(frame);
                var pools = ReconstructPools(side, ledger.Attributes);
                var reconciled = new HashSet<PeriodicStatus>();
                foreach (var status in Statuses)
                {
                    if (!ShouldReconcileBeforeImpact(status, pools, ledger, side)) continue;
                    Reconcile(side, status, ledger, weight);
                    reconciled.Add(status);
                }
                AttributeFrameImpact(pools, ledger, result, regenFallback, weight);
                foreach (var status in Statuses)
                    if (!reconciled.Contains(status)) Reconcile(side, status, ledger, weight);
                if (side != null)
                    foreach (var pair in side.Attributes) ledger.Attributes[pair.Key] = pair.Value.Current;
            }
        }
        return result;
    }

    private static Ledger BuildLedger(IEnumerable<PeriodicSide?> sides)
    {
        var attributes = new Dictionary<PeriodicAttribute, long>();
        foreach (var side in sides)
            if (side != null)
                foreach (var pair in side.Attributes) attributes.TryAdd(pair.Key, pair.Value.Previous);
        foreach (var attribute in new[] { PeriodicAttribute.Burn, PeriodicAttribute.Poison, PeriodicAttribute.HealthRegen, PeriodicAttribute.Shield })
            attributes.TryAdd(attribute, 0);
        if (!attributes.ContainsKey(PeriodicAttribute.HealthMax) && attributes.TryGetValue(PeriodicAttribute.Health, out var health))
            attributes[PeriodicAttribute.HealthMax] = health;
        var ledger = new Ledger(attributes);
        foreach (var status in Statuses)
            ledger.Owners[status].AddUnknown(ledger.Read(Attribute(status)), Origin.InitialState);
        return ledger;
    }

    private static Pools ReconstructPools(PeriodicSide? side, Dictionary<PeriodicAttribute, long> attributes)
    {
        if (side == null) return default;
        var adjustments = side.Adjustments;
        var died = side.Died;
        var burnHealth = Loss(adjustments, PeriodicDamage.Burn, PeriodicPool.Health);
        var burnShield = Loss(adjustments, PeriodicDamage.Burn, PeriodicPool.Shield);
        var poisonHealth = Loss(adjustments, PeriodicDamage.Poison, PeriodicPool.Health);
        var regenAttempted = adjustments.Where(item => item.Type == PeriodicDamage.Regen && item.Pool == PeriodicPool.Health && item.Amount > 0)
            .Sum(item => (long)item.Amount);
        var regenRealized = 0L;
        var hasHealthTransition = side.Attributes.TryGetValue(PeriodicAttribute.Health, out var healthTransition);
        var hasMaxTransition = side.Attributes.TryGetValue(PeriodicAttribute.HealthMax, out var maxTransition);
        var hasHealth = hasHealthTransition || attributes.ContainsKey(PeriodicAttribute.Health);
        var hasMax = hasMaxTransition || attributes.ContainsKey(PeriodicAttribute.HealthMax);
        var healthReconciled = hasHealth && hasMax;
        if (healthReconciled)
        {
            long health = hasHealthTransition ? healthTransition.Previous : attributes[PeriodicAttribute.Health];
            long max = hasMaxTransition ? maxTransition.Previous : attributes[PeriodicAttribute.HealthMax];
            long closingMax = hasMaxTransition ? maxTransition.Current : max;
            var cursor = health + (closingMax - max);
            var terminal = died && cursor <= 0;
            long burnHealthBooked = 0, burnShieldBooked = 0, poisonBooked = 0;
            for (var index = 0; index < adjustments.Count; index++)
            {
                var adjustment = adjustments[index];
                // Contiguous burn health/shield adjustments are one atomic tick, so a lethal health
                // component does not discard its paired shield consumption.
                if (IsBurnTick(adjustment))
                {
                    var alive = !died || !terminal;
                    for (; index < adjustments.Count && IsBurnTick(adjustments[index]); index++)
                    {
                        var burn = adjustments[index];
                        if (burn.Pool == PeriodicPool.Health)
                        {
                            long loss = -burn.Amount;
                            if (alive) burnHealthBooked += died ? Math.Min(loss, Math.Max(0, cursor)) : loss;
                            cursor += burn.Amount;
                            if (died && cursor <= 0) terminal = true;
                        }
                        else if (alive) burnShieldBooked += -burn.Amount;
                    }
                    index--;
                    continue;
                }
                if (adjustment.Pool != PeriodicPool.Health) continue;
                if (adjustment.Amount <= 0)
                {
                    long loss = -adjustment.Amount;
                    var booked = died && !terminal ? Math.Min(loss, Math.Max(0, cursor)) : died ? 0 : loss;
                    if (adjustment.Type == PeriodicDamage.Poison) poisonBooked += booked;
                    cursor += adjustment.Amount;
                    if (died && cursor <= 0) terminal = true;
                    continue;
                }
                var accepted = Math.Min(adjustment.Amount, Math.Max(0, closingMax - cursor));
                cursor += accepted;
                if (adjustment.Type == PeriodicDamage.Regen && (!died || !terminal)) regenRealized += accepted;
            }
            burnHealth = burnHealthBooked;
            burnShield = burnShieldBooked;
            poisonHealth = poisonBooked;
            var closingHealth = hasHealthTransition ? healthTransition.Current : cursor;
            healthReconciled = cursor == closingHealth;
            attributes[PeriodicAttribute.Health] = closingHealth;
            attributes[PeriodicAttribute.HealthMax] = closingMax;
        }
        return new Pools(healthReconciled, burnHealth, burnShield, poisonHealth, regenAttempted, regenRealized);
    }

    private static void AttributeFrameImpact(Pools pools, Ledger ledger,
        Dictionary<(string, CombatImpactKind), CombatImpactPeriodic> result, IReadOnlyList<string> regenFallback,
        Func<string, PeriodicStatus, int> weight)
    {
        if (pools.BurnHealth > 0 || pools.BurnShield > 0)
            Allocate(ledger.Owners[PeriodicStatus.Burn], PeriodicStatus.Burn, pools.BurnHealth, pools.BurnShield, result, regenFallback, weight);
        if (pools.PoisonHealth > 0)
            Allocate(ledger.Owners[PeriodicStatus.Poison], PeriodicStatus.Poison, pools.PoisonHealth, 0, result, regenFallback, weight);
        if (pools.RegenAttempted <= 0 || !pools.HealthReconciled) return;
        if (ledger.Read(PeriodicAttribute.HealthRegen) <= 0 && regenFallback.Count == 0) return;
        Allocate(ledger.Owners[PeriodicStatus.Regen], PeriodicStatus.Regen, pools.RegenRealized, 0, result, regenFallback, weight);
    }

    private static bool ShouldReconcileBeforeImpact(PeriodicStatus status, Pools pools, Ledger ledger, PeriodicSide? side)
    {
        if (side == null) return false;
        var measured = status switch
        {
            PeriodicStatus.Burn => pools.BurnHealth > 0 || pools.BurnShield > 0,
            PeriodicStatus.Poison => pools.PoisonHealth > 0,
            _ => pools.RegenAttempted > 0,
        };
        if (!measured || !side.Attributes.TryGetValue(Attribute(status), out var transition)) return false;
        var opening = ledger.Read(Attribute(status));
        if (opening <= 0 && transition.Current > 0) return true;
        return status == PeriodicStatus.Regen && pools.RegenAttempted != opening && pools.RegenAttempted == transition.Current;
    }

    private static void Allocate(Owners owners, PeriodicStatus status, long health, long shield,
        Dictionary<(string, CombatImpactKind), CombatImpactPeriodic> result, IReadOnlyList<string> regenFallback,
        Func<string, PeriodicStatus, int> weight)
    {
        var healthShares = new Dictionary<string, long>(owners.Share(health, out var unknownHealth), StringComparer.Ordinal);
        var shieldShares = owners.Share(shield, out _);
        // Initial regen with no tick provenance goes to sources this combat proves applied regen.
        if (status == PeriodicStatus.Regen && unknownHealth > 0 && (owners.Origins & ~Origin.InitialState) == 0)
        {
            foreach (var (source, amount) in owners.SplitKnown(unknownHealth))
            {
                healthShares[source] = healthShares.GetValueOrDefault(source) + amount;
                unknownHealth -= amount;
            }
            if (unknownHealth > 0)
                foreach (var (source, amount) in Split(unknownHealth, regenFallback, source => weight(source, status)))
                    healthShares[source] = healthShares.GetValueOrDefault(source) + amount;
        }
        var kind = status switch
        {
            PeriodicStatus.Burn => CombatImpactKind.Burn,
            PeriodicStatus.Poison => CombatImpactKind.Poison,
            _ => CombatImpactKind.Regen,
        };
        foreach (var source in healthShares.Keys.Union(shieldShares.Keys, StringComparer.Ordinal))
        {
            var healthShare = healthShares.GetValueOrDefault(source);
            var shieldShare = shieldShares.GetValueOrDefault(source);
            if (healthShare == 0 && shieldShare == 0) continue;
            var current = result.GetValueOrDefault((source, kind));
            result[(source, kind)] = new CombatImpactPeriodic(current.Health + healthShare, current.Shield + shieldShare);
        }
    }

    private static void Reconcile(PeriodicSide? side, PeriodicStatus status, Ledger ledger, Func<string, PeriodicStatus, int> weight)
    {
        var attribute = Attribute(status);
        var opening = ledger.Read(attribute);
        var owners = ledger.Owners[status];
        var tickType = status switch
        {
            PeriodicStatus.Burn => PeriodicDamage.Burn,
            PeriodicStatus.Poison => PeriodicDamage.Poison,
            _ => PeriodicDamage.Regen,
        };
        var ticked = side?.Adjustments.Any(item => item.Type == tickType) == true;
        // The game decays burn by 3% (at least one stack) on each burn tick.
        var decay = status == PeriodicStatus.Burn && ticked && opening > 0 ? Math.Max(1, (long)Math.Floor(opening * 0.03)) : 0;
        if (decay > 0) owners.Remove(decay);
        var baseline = opening - decay;
        long closing = side != null && side.Attributes.TryGetValue(attribute, out var transition) ? transition.Current : baseline;
        var change = closing - baseline;
        var applies = side?.Applies.GetValueOrDefault(status) ?? [];
        var sources = applies.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (applies.Any(source => source == null)) sources.Add(Unknown);
        if (change > 0) owners.Add(change, sources, source => weight(source, status));
        else if (change < 0)
        {
            owners.Remove(-change);
            if (sources.Count > 0) owners.Contaminate();
        }
        else if (sources.Count > 0) owners.Contaminate();
        owners.ReconcileTotal(closing);
        ledger.Attributes[attribute] = closing;
    }

    private static bool IsBurnTick(PeriodicAdjustment item) =>
        item.Type == PeriodicDamage.Burn && item.Amount < 0 && item.Pool is PeriodicPool.Health or PeriodicPool.Shield;

    private static long Loss(IEnumerable<PeriodicAdjustment> adjustments, PeriodicDamage type, PeriodicPool pool) =>
        -adjustments.Where(item => item.Type == type && item.Pool == pool && item.Amount < 0).Sum(item => (long)item.Amount);

    private static PeriodicAttribute Attribute(PeriodicStatus status) => status switch
    {
        PeriodicStatus.Burn => PeriodicAttribute.Burn,
        PeriodicStatus.Poison => PeriodicAttribute.Poison,
        _ => PeriodicAttribute.HealthRegen,
    };

    // Largest-remainder split; ties go to the ordinally first key so results are deterministic.
    private static Dictionary<string, long> Split(long amount, IReadOnlyList<string> keys, Func<string, long> weight)
    {
        var result = keys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal);
        if (amount <= 0 || keys.Count == 0) return result;
        var weights = keys.ToDictionary(key => key, key => Math.Max(0, weight(key)), StringComparer.Ordinal);
        var total = weights.Values.Sum();
        if (total == 0)
        {
            foreach (var key in keys) weights[key] = 1;
            total = keys.Count;
        }
        long assigned = 0;
        var remainders = new List<(string Key, long Remainder)>();
        foreach (var key in keys)
        {
            var product = amount * weights[key];
            result[key] = product / total;
            assigned += product / total;
            remainders.Add((key, product % total));
        }
        foreach (var item in remainders.OrderByDescending(item => item.Remainder).ThenBy(item => item.Key, StringComparer.Ordinal)
                     .Take((int)(amount - assigned)))
            result[item.Key]++;
        return result;
    }

    [Flags]
    private enum Origin { None = 0, InitialState = 1, MissingSource = 2, Deficit = 4, Contaminated = 8 }

    private readonly record struct Pools(bool HealthReconciled, long BurnHealth, long BurnShield, long PoisonHealth,
        long RegenAttempted, long RegenRealized);

    private sealed class Ledger(Dictionary<PeriodicAttribute, long> attributes)
    {
        internal readonly Dictionary<PeriodicAttribute, long> Attributes = attributes;
        internal readonly Dictionary<PeriodicStatus, Owners> Owners = Statuses.ToDictionary(status => status, _ => new Owners());
        internal long Read(PeriodicAttribute attribute) => Attributes.GetValueOrDefault(attribute);
    }

    private sealed class Owners
    {
        private readonly Dictionary<string, long> _balances = new(StringComparer.Ordinal);

        internal Origin Origins { get; private set; }

        internal void AddUnknown(long amount, Origin origin)
        {
            if (amount <= 0) return;
            Origins |= origin;
            _balances[Unknown] = _balances.GetValueOrDefault(Unknown) + amount;
        }

        internal void Add(long amount, IReadOnlyList<string> sources, Func<string, long> weight)
        {
            if (amount <= 0) return;
            if (sources.Count == 0)
            {
                AddUnknown(amount, Origin.MissingSource);
                return;
            }
            foreach (var (source, share) in CombatImpactPeriodicLedger.Split(amount, sources, source => source == Unknown ? 1 : weight(source)))
                if (source == Unknown) AddUnknown(share, Origin.MissingSource);
                else if (share > 0) _balances[source] = _balances.GetValueOrDefault(source) + share;
        }

        internal void Remove(long amount)
        {
            var total = _balances.Values.Sum();
            if (amount <= 0 || total <= 0) return;
            var keys = _balances.Keys.Order(StringComparer.Ordinal).ToArray();
            foreach (var (key, removal) in CombatImpactPeriodicLedger.Split(Math.Min(amount, total), keys, key => _balances[key]))
            {
                var remaining = _balances[key] - removal;
                if (remaining <= 0) _balances.Remove(key);
                else _balances[key] = remaining;
            }
            if (!_balances.ContainsKey(Unknown)) Origins = Origin.None;
        }

        internal void ReconcileTotal(long expected)
        {
            expected = Math.Max(0, expected);
            var current = _balances.Values.Sum();
            if (current < expected) AddUnknown(expected - current, Origin.Deficit);
            else if (current > expected) Remove(current - expected);
        }

        internal void Contaminate()
        {
            var total = _balances.Values.Sum();
            _balances.Clear();
            Origins = Origin.None;
            AddUnknown(total, Origin.Contaminated);
        }

        internal IReadOnlyDictionary<string, long> Share(long amount, out long unknown)
        {
            unknown = 0;
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            if (amount <= 0) return result;
            if (_balances.Count == 0)
            {
                unknown = amount;
                return result;
            }
            var keys = _balances.Keys.Order(StringComparer.Ordinal).ToArray();
            foreach (var (key, share) in CombatImpactPeriodicLedger.Split(amount, keys, key => _balances[key]))
                if (key == Unknown) unknown = share;
                else if (share > 0) result[key] = share;
            return result;
        }

        internal IReadOnlyDictionary<string, long> SplitKnown(long amount) =>
            CombatImpactPeriodicLedger.Split(amount, _balances.Keys.Where(key => key != Unknown).Order(StringComparer.Ordinal).ToArray(),
                key => _balances[key]);
    }
}
