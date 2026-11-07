using System.Diagnostics;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Effect.Actions;
using BazaarGameShared.Infra.Messages;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using BazaarGameShared.Infra.Messages.Shared;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Projects one CombatSim into a managed CombatImpactReport. Registration runs in the Simulate hook;
// frames are walked on the main thread under a per-frame budget, and native CardStats are read only
// after the game's playback task completes. The periodic ledger and report grouping touch no native
// objects, so they run on a worker and are published back on the main thread.
internal static class AndroidCombatImpactCapture
{
    private const double FrameBudgetMs = 2;
    private static readonly ECardAttributeType[] Timing =
        [ECardAttributeType.ChargeAmount, ECardAttributeType.HasteAmount, ECardAttributeType.SlowAmount, ECardAttributeType.FreezeAmount];
    private static readonly Dictionary<(IntPtr, string), IntPtr> Getters = new();
    private static Pending? _pending;
    private static Task<(CombatImpactReport Report, int Periodic, double ElapsedMs)>? _analysis;
    private static long _analysisGeneration;
    private static bool _reportedFailure;
    private static bool _reportedModifierFailure;

    internal static CombatImpactReport Report { get; private set; } = CombatImpactReport.Empty;
    internal static long Generation { get; private set; }
    internal static bool Projecting => _pending != null || _analysis != null;

    private sealed class Pending(CombatSim simulation, long generation, Dictionary<string, CombatImpactEntity> entities,
        Dictionary<string, int[]> attributes, Dictionary<string, Card> cards)
    {
        internal readonly CombatSim Simulation = simulation;
        internal readonly long Generation = generation;
        internal readonly Dictionary<string, CombatImpactEntity> Entities = entities;
        internal readonly Dictionary<string, int[]> Attributes = attributes;
        internal readonly Dictionary<string, Card> Cards = cards;
        // Template modifiers are read on first use inside the budgeted frame walk.
        internal readonly Dictionary<string, Dictionary<string, string>?> Modifiers = new(StringComparer.Ordinal);
        internal readonly List<CombatImpactExecution> Executions = new();
        internal readonly List<PeriodicFrame> Periodic = new();
        internal int Frame;
        internal bool Completed;
    }

    // Every Simulate call (combat or replay) retires the previous report, so a recap never shows
    // numbers from a different combat.
    internal static long Begin(NetMessageCombatSim message)
    {
        var generation = ++Generation;
        Report = CombatImpactReport.Empty;
        _pending = null;
        _analysis = null;
        var timer = Stopwatch.StartNew();
        var simulation = message.Data;
        if (simulation == null) return generation;
        var entities = new Dictionary<string, CombatImpactEntity>(StringComparer.Ordinal)
        {
            [CombatImpactReport.PlayerId(false)] = new(CombatImpactReport.PlayerId(false), NativeUi.Text("You", "己方"), CombatImpactEntityKind.Player),
            [CombatImpactReport.PlayerId(true)] = new(CombatImpactReport.PlayerId(true), NativeUi.Text("Opponent", "对手"), CombatImpactEntityKind.Player),
        };
        var attributes = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var cards = new Dictionary<string, Card>(StringComparer.Ordinal);
        foreach (var pair in Data.Entities)
        {
            var card = pair.Value;
            var id = card?.InstanceId.Value;
            if (card == null || string.IsNullOrEmpty(id)) continue;
            var kind = card.TryCast<ItemCard>() != null ? CombatImpactEntityKind.Item
                : card.TryCast<SkillCard>() != null ? CombatImpactEntityKind.Skill : (CombatImpactEntityKind?)null;
            if (kind == null) continue;
            entities[id] = new CombatImpactEntity(id, Name(card), kind.Value);
            var values = new int[Timing.Length];
            foreach (var attribute in card.Attributes)
            {
                var index = Array.IndexOf(Timing, attribute.Key);
                if (index >= 0) values[index] = attribute.Value;
            }
            attributes[id] = values;
            cards[id] = card;
        }
        _pending = new Pending(simulation, generation, entities, attributes, cards);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.registered generation={generation} frames={simulation.Frames?.Count ?? 0} entities={entities.Count} elapsedMs={timer.Elapsed.TotalMilliseconds:F1}");
        return generation;
    }

    internal static void Complete(long generation, bool successful)
    {
        if (_pending?.Generation != generation) return;
        if (successful) _pending.Completed = true;
        else _pending = null;
    }

    internal static void Tick()
    {
        Publish();
        var pending = _pending;
        if (pending == null) return;
        try
        {
            var frames = pending.Simulation.Frames;
            var timer = Stopwatch.StartNew();
            while (frames != null && pending.Frame < frames.Count && timer.Elapsed.TotalMilliseconds < FrameBudgetMs)
            {
                Project(pending, frames[pending.Frame], pending.Frame);
                pending.Frame++;
            }
            if (!pending.Completed || (frames != null && pending.Frame < frames.Count)) return;
            var (totals, uses) = Stats(pending.Simulation);
            _pending = null;
            _analysisGeneration = pending.Generation;
            _analysis = Task.Run(() => Analyze(pending, totals, uses));
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.projected generation={pending.Generation} executions={pending.Executions.Count} sources={totals.Count} modifiers={pending.Modifiers.Count(pair => pair.Value != null)} elapsedMs={timer.Elapsed.TotalMilliseconds:F1}");
        }
        catch (Exception error)
        {
            _pending = null;
            Report = CombatImpactReport.Empty;
            if (_reportedFailure) return;
            _reportedFailure = true;
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_impact.failed exception={error}");
        }
    }

    private static (CombatImpactReport, int, double) Analyze(Pending pending,
        Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>> totals, Dictionary<string, int> uses)
    {
        var timer = Stopwatch.StartNew();
        var periodic = CombatImpactPeriodicLedger.Project(pending.Periodic, (source, status) =>
            Math.Max(1, totals.TryGetValue(source, out var stats) && stats.TryGetValue(status switch
            {
                PeriodicStatus.Burn => CombatImpactKind.Burn,
                PeriodicStatus.Poison => CombatImpactKind.Poison,
                _ => CombatImpactKind.Regen,
            }, out var added) ? added : 0));
        return (new CombatImpactReport(pending.Entities, pending.Executions, totals, uses, periodic), periodic.Count,
            timer.Elapsed.TotalMilliseconds);
    }

    private static void Publish()
    {
        if (_analysis is not { IsCompleted: true } analysis) return;
        _analysis = null;
        if (_analysisGeneration != Generation) return;
        if (analysis.IsCompletedSuccessfully)
        {
            var (report, periodic, elapsedMs) = analysis.Result;
            Report = report;
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.ready generation={_analysisGeneration} periodic={periodic} workerMs={elapsedMs:F1}");
        }
        else if (!_reportedFailure)
        {
            _reportedFailure = true;
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_impact.failed exception={analysis.Exception}");
        }
    }

    private static void Project(Pending pending, CombatSimFrame frame, int index)
    {
        Reconcile(pending, frame, previous: true);
        var player = Side(frame.PlayerUpdates);
        var opponent = Side(frame.OpponentUpdates);
        if (frame.Events != null)
            foreach (var item in frame.Events)
                if (item?.TryCast<CombatSimEventCombatantDied>() is { } died)
                    (died.CombatantId == ECombatantId.Opponent ? opponent : player).Died = true;
        var executed = new List<CombatImpactExecution>();
        if (frame.Events != null)
            foreach (var item in frame.Events)
            {
                var effect = item?.TryCast<CombatSimEventEffectExecuted>();
                if (effect == null) continue;
                var action = effect.ActionType.ToString();
                var kind = CombatImpactReport.Kind(action);
                if (kind == null) continue;
                var direct = InstanceIdOf(effect, "get_Source");
                var trigger = InstanceIdOf(effect, "get_TriggerSource");
                var source = Activity(pending, direct) ?? Activity(pending, trigger);
                var target = TargetOf(effect.Target);
                if (Status(kind.Value) is { } status
                    && (target == CombatImpactReport.PlayerId(true) || target == CombatImpactReport.PlayerId(false)))
                {
                    var side = target == CombatImpactReport.PlayerId(true) ? opponent : player;
                    if (!side.Applies.TryGetValue(status, out var applies)) side.Applies[status] = applies = new List<string?>();
                    applies.Add(source);
                }
                if (source == null || target == null) continue;
                string? attribute = null;
                if (kind == CombatImpactKind.Attribute)
                {
                    attribute = action switch
                    {
                        "CardReload" => "Reload",
                        "PlayerMaxHealthIncrease" or "PlayerMaxHealthDecrease" => "HealthMax",
                        _ => ModifiedAttribute(pending, direct, effect.EffectId) ?? ModifiedAttribute(pending, trigger, effect.EffectId),
                    };
                    if (!CombatImpactReport.Displayable(attribute)) continue;
                }
                int? value = null;
                var timing = Array.IndexOf(Timing, TimingAttribute(kind.Value));
                if (timing >= 0 && pending.Attributes.TryGetValue(source, out var values)) value = values[timing];
                var triggerId = Activity(pending, trigger);
                executed.Add(new CombatImpactExecution(source, target, kind.Value, value, Attribute: attribute,
                    TriggerId: triggerId == source ? null : triggerId, Frame: index));
            }
        MarkCritical(executed, frame.PlayerUpdates, CombatImpactReport.PlayerId(false));
        MarkCritical(executed, frame.OpponentUpdates, CombatImpactReport.PlayerId(true));
        ResolveAttributeValues(executed, CardDeltas(frame), player, opponent);
        pending.Executions.AddRange(executed);
        pending.Periodic.Add(new PeriodicFrame(player, opponent));
        Reconcile(pending, frame, previous: false);
    }

    // A target's net frame delta belongs to an attribute change only when one execution claims
    // that target and attribute in the frame; otherwise the count stays without a value.
    private static void ResolveAttributeValues(List<CombatImpactExecution> executed,
        Dictionary<string, Dictionary<ECardAttributeType, int>> cards, PeriodicSide player, PeriodicSide opponent)
    {
        foreach (var group in executed.Select((item, position) => (item, position))
                     .Where(pair => pair.item.Kind == CombatImpactKind.Attribute)
                     .GroupBy(pair => (pair.item.TargetId, pair.item.Attribute)))
        {
            if (group.Count() != 1) continue;
            var (item, position) = group.First();
            int? delta = null;
            if (item.Attribute == "HealthMax")
            {
                var side = item.TargetId == CombatImpactReport.PlayerId(true) ? opponent
                    : item.TargetId == CombatImpactReport.PlayerId(false) ? player : null;
                if (side != null && side.Attributes.TryGetValue(PeriodicAttribute.HealthMax, out var change))
                    delta = change.Current - change.Previous;
            }
            else if (cards.TryGetValue(item.TargetId, out var deltas))
            {
                if (item.Attribute == "Reload")
                    delta = deltas.TryGetValue(ECardAttributeType.Ammo, out var ammo) && ammo > 0 ? ammo : null;
                else if (Enum.TryParse<ECardAttributeType>(item.Attribute, out var type) && deltas.TryGetValue(type, out var found))
                    delta = found;
            }
            if (delta is { } value && value != 0) executed[position] = item with { Value = value };
        }
    }

    private static Dictionary<string, Dictionary<ECardAttributeType, int>> CardDeltas(CombatSimFrame frame)
    {
        var result = new Dictionary<string, Dictionary<ECardAttributeType, int>>(StringComparer.Ordinal);
        if (frame.CardUpdates == null) return result;
        foreach (var pair in frame.CardUpdates)
        {
            var id = pair.Key?.Value;
            if (string.IsNullOrEmpty(id) || pair.Value?.Attributes == null) continue;
            var deltas = new Dictionary<ECardAttributeType, int>();
            foreach (var attribute in pair.Value.Attributes)
                if (attribute.Value != null && attribute.Value.CurrentValue != attribute.Value.PreviousValue)
                    deltas[attribute.Key] = attribute.Value.CurrentValue - attribute.Value.PreviousValue;
            if (deltas.Count > 0) result[id] = deltas;
        }
        return result;
    }

    private static PeriodicSide Side(CombatSimPlayerUpdate? update)
    {
        var side = new PeriodicSide();
        if (update == null) return side;
        if (update.HealthAdjustments != null)
            foreach (var adjustment in update.HealthAdjustments)
            {
                if (adjustment == null) continue;
                var type = adjustment.DamageType switch
                {
                    EDamageType.Damage => PeriodicDamage.Damage,
                    EDamageType.Burn => PeriodicDamage.Burn,
                    EDamageType.Poison => PeriodicDamage.Poison,
                    EDamageType.Heal => PeriodicDamage.Heal,
                    EDamageType.Shield => PeriodicDamage.Shield,
                    EDamageType.Regen => PeriodicDamage.Regen,
                    _ => PeriodicDamage.Other,
                };
                var pool = adjustment.AttributeChanged switch
                {
                    EPlayerHealthChangeType.Health => PeriodicPool.Health,
                    EPlayerHealthChangeType.Shield => PeriodicPool.Shield,
                    _ => PeriodicPool.Other,
                };
                side.Adjustments.Add(new PeriodicAdjustment(type, pool, adjustment.Amount));
            }
        if (update.Attributes != null)
            foreach (var pair in update.Attributes)
            {
                PeriodicAttribute? attribute = pair.Key switch
                {
                    EPlayerAttributeType.Health => PeriodicAttribute.Health,
                    EPlayerAttributeType.HealthMax => PeriodicAttribute.HealthMax,
                    EPlayerAttributeType.Shield => PeriodicAttribute.Shield,
                    EPlayerAttributeType.Burn => PeriodicAttribute.Burn,
                    EPlayerAttributeType.Poison => PeriodicAttribute.Poison,
                    EPlayerAttributeType.HealthRegen => PeriodicAttribute.HealthRegen,
                    _ => null,
                };
                if (attribute != null && pair.Value != null)
                    side.Attributes[attribute.Value] = new PeriodicTransition(pair.Value.PreviousValue, pair.Value.CurrentValue);
            }
        return side;
    }

    private static PeriodicStatus? Status(CombatImpactKind kind) => kind switch
    {
        CombatImpactKind.Burn => PeriodicStatus.Burn,
        CombatImpactKind.Poison => PeriodicStatus.Poison,
        CombatImpactKind.Regen => PeriodicStatus.Regen,
        _ => null,
    };

    private static string? ModifiedAttribute(Pending pending, string? card, string? effectId)
    {
        if (card == null || effectId == null) return null;
        if (!pending.Modifiers.TryGetValue(card, out var map))
        {
            map = null;
            // A template the reader cannot walk only loses that card's attribute changes.
            try { if (pending.Cards.TryGetValue(card, out var source)) map = Modifiers(source); }
            catch (Exception error) when (!_reportedModifierFailure)
            {
                _reportedModifierFailure = true;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.combat_impact.modifiers_failed card={card} exception={error}");
            }
            catch (Exception) { }
            pending.Modifiers[card] = map;
        }
        return map != null && map.TryGetValue(effectId, out var attribute) ? attribute : null;
    }

    // Ability id → modified attribute (desktop CombatImpactAbilityAttributeModifierReader); an id
    // naming two different attributes is ambiguous and dropped.
    private static Dictionary<string, string>? Modifiers(Card card)
    {
        var abilities = card.Template?.TryCast<BazaarGameShared.Domain.Cards.TCardBase>()?.Abilities;
        if (abilities == null) return null;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in abilities)
        {
            var ability = pair.Value;
            var id = ability?.Id ?? pair.Key;
            var modifier = ability?.Action?.TryCast<TActionCardModifyAttribute>();
            if (modifier == null || string.IsNullOrEmpty(id) || ambiguous.Contains(id)) continue;
            var attribute = modifier.AttributeType.ToString();
            if (!map.TryGetValue(id, out var existing)) map[id] = attribute;
            else if (existing != attribute)
            {
                map.Remove(id);
                ambiguous.Add(id);
            }
        }
        return map.Count == 0 ? null : map;
    }

    // The source's configured amount is read at the frame start, before the frame's own updates.
    private static void Reconcile(Pending pending, CombatSimFrame frame, bool previous)
    {
        if (frame.CardUpdates == null) return;
        foreach (var pair in frame.CardUpdates)
        {
            if (pair.Value?.Attributes == null) continue;
            var id = pair.Key.Value;
            if (string.IsNullOrEmpty(id)) continue;
            if (!pending.Attributes.TryGetValue(id, out var values))
                pending.Attributes[id] = values = new int[Timing.Length];
            foreach (var attribute in pair.Value.Attributes)
            {
                var index = Array.IndexOf(Timing, attribute.Key);
                if (index >= 0 && attribute.Value != null)
                    values[index] = previous ? attribute.Value.PreviousValue : attribute.Value.CurrentValue;
            }
        }
    }

    private static void MarkCritical(List<CombatImpactExecution> executed, CombatSimPlayerUpdate? update, string hero)
    {
        if (update?.HealthAdjustments == null) return;
        foreach (var kind in new[] { CombatImpactKind.Damage, CombatImpactKind.Healing, CombatImpactKind.Shield })
        {
            var count = executed.Count(item => item.Kind == kind && item.TargetId == hero);
            if (count == 0) continue;
            var critical = 0;
            foreach (var adjustment in update.HealthAdjustments)
                if (adjustment != null && adjustment.IsCrit && Matches(kind, adjustment)) critical++;
            if (CombatImpactReport.CriticalFrame(count, critical) != true) continue;
            for (var index = 0; index < executed.Count; index++)
                if (executed[index].Kind == kind && executed[index].TargetId == hero)
                    executed[index] = executed[index] with { Critical = true };
        }
    }

    private static bool Matches(CombatImpactKind kind, CombatSimPlayerHealthAdjustment adjustment) => kind switch
    {
        CombatImpactKind.Damage => adjustment.DamageType == EDamageType.Damage && adjustment.Amount < 0,
        CombatImpactKind.Healing => adjustment.DamageType == EDamageType.Heal
            && adjustment.AttributeChanged == EPlayerHealthChangeType.Health && adjustment.Amount > 0,
        CombatImpactKind.Shield => adjustment.DamageType == EDamageType.Shield
            && adjustment.AttributeChanged == EPlayerHealthChangeType.Shield && adjustment.Amount > 0,
        _ => false,
    };

    private static (Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>, Dictionary<string, int>) Stats(CombatSim simulation)
    {
        var totals = new Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>(StringComparer.Ordinal);
        var uses = new Dictionary<string, int>(StringComparer.Ordinal);
        if (simulation.CardStats == null) return (totals, uses);
        foreach (var pair in simulation.CardStats)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Value == null) continue;
            var values = new Dictionary<CombatImpactKind, int>();
            foreach (var stat in pair.Value)
            {
                CombatImpactKind? kind = stat.Key switch
                {
                    ECardStats.DamageDone => CombatImpactKind.Damage,
                    ECardStats.BurnAdded => CombatImpactKind.Burn,
                    ECardStats.PoisonAdded => CombatImpactKind.Poison,
                    ECardStats.HealAdded => CombatImpactKind.Healing,
                    ECardStats.ShieldAdded => CombatImpactKind.Shield,
                    ECardStats.RegenAdded => CombatImpactKind.Regen,
                    _ => null,
                };
                if (kind != null) values[kind.Value] = stat.Value;
                else if (stat.Key == ECardStats.UseCount) uses[pair.Key] = stat.Value;
            }
            if (values.Count > 0) totals[pair.Key] = values;
        }
        return (totals, uses);
    }

    private static ECardAttributeType? TimingAttribute(CombatImpactKind kind) => kind switch
    {
        CombatImpactKind.Charge => ECardAttributeType.ChargeAmount,
        CombatImpactKind.Haste => ECardAttributeType.HasteAmount,
        CombatImpactKind.Slow => ECardAttributeType.SlowAmount,
        CombatImpactKind.Freeze => ECardAttributeType.FreezeAmount,
        _ => null,
    };

    private static string? Activity(Pending pending, string? id) =>
        id != null && pending.Entities.TryGetValue(id, out var entity) && entity.Kind != CombatImpactEntityKind.Player ? id : null;

    private static string? TargetOf(IEffectTarget? target)
    {
        if (target == null) return null;
        if (target.TryCast<EffectTargetCard>() is { } card) return card.Target.Value;
        if (target.TryCast<EffectTargetPlayer>() is { } player)
            return CombatImpactReport.PlayerId(player.Target == ECombatantId.Opponent);
        return null;
    }

    // Nullable<InstanceId> wraps a reference-carrying struct, which the unmanaged
    // NativeNullableValue path cannot unbox; invoke the getter and unwrap the boxed value.
    private static unsafe string? InstanceIdOf(Il2CppObjectBase owner, string getter)
    {
        var type = IL2CPP.il2cpp_object_get_class(owner.Pointer);
        if (!Getters.TryGetValue((type, getter), out var method))
        {
            method = IL2CPP.il2cpp_class_get_method_from_name(type, getter, 0);
            if (method == IntPtr.Zero) throw new MissingMethodException(getter);
            Getters[(type, getter)] = method;
        }
        var exception = IntPtr.Zero;
        var result = IL2CPP.il2cpp_runtime_invoke(method, owner.Pointer, null, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        if (result == IntPtr.Zero) return null;
        var resultClass = IL2CPP.il2cpp_object_get_class(result);
        if (resultClass == Il2CppClassPointerStore<InstanceId>.NativeClassPtr) return new InstanceId(result).Value;
        if (resultClass == Il2CppClassPointerStore<Il2CppSystem.Nullable<InstanceId>>.NativeClassPtr)
        {
            var nullable = new Il2CppSystem.Nullable<InstanceId>(result);
            return nullable.HasValue ? nullable.Value.Value : null;
        }
        throw new InvalidOperationException("Unexpected instance id result for " + getter);
    }

    private static string Name(Card card)
    {
        var template = card.Template?.TryCast<BazaarGameShared.Domain.Cards.TCardBase>();
        var name = template?.Localization?.Title is { } title ? TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(title) : null;
        if (string.IsNullOrWhiteSpace(name)) name = template?.InternalName;
        return string.IsNullOrWhiteSpace(name) ? card.TemplateId.ToString() : name!;
    }
}
