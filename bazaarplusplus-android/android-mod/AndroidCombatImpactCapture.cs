using System.Diagnostics;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using BazaarGameShared.Infra.Messages.Shared;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Projects one CombatSim into a managed CombatImpactReport. Registration runs in the Simulate hook;
// frames are walked on the main thread under a per-frame budget, and native CardStats are read only
// after the game's playback task completes.
internal static class AndroidCombatImpactCapture
{
    private const double FrameBudgetMs = 2;
    private static readonly ECardAttributeType[] Timing =
        [ECardAttributeType.ChargeAmount, ECardAttributeType.HasteAmount, ECardAttributeType.SlowAmount, ECardAttributeType.FreezeAmount];
    private static readonly Dictionary<(IntPtr, string), IntPtr> Getters = new();
    private static Pending? _pending;
    private static bool _reportedFailure;

    internal static CombatImpactReport Report { get; private set; } = CombatImpactReport.Empty;
    internal static long Generation { get; private set; }
    internal static bool Projecting => _pending != null;

    private sealed class Pending(CombatSim simulation, long generation, Dictionary<string, CombatImpactEntity> entities,
        Dictionary<string, int[]> attributes)
    {
        internal readonly CombatSim Simulation = simulation;
        internal readonly long Generation = generation;
        internal readonly Dictionary<string, CombatImpactEntity> Entities = entities;
        internal readonly Dictionary<string, int[]> Attributes = attributes;
        internal readonly List<CombatImpactExecution> Executions = new();
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
        var timer = Stopwatch.StartNew();
        var simulation = message.Data;
        if (simulation == null) return generation;
        var entities = new Dictionary<string, CombatImpactEntity>(StringComparer.Ordinal)
        {
            [CombatImpactReport.PlayerId(false)] = new(CombatImpactReport.PlayerId(false), NativeUi.Text("You", "己方"), CombatImpactEntityKind.Player),
            [CombatImpactReport.PlayerId(true)] = new(CombatImpactReport.PlayerId(true), NativeUi.Text("Opponent", "对手"), CombatImpactEntityKind.Player),
        };
        var attributes = new Dictionary<string, int[]>(StringComparer.Ordinal);
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
        }
        _pending = new Pending(simulation, generation, entities, attributes);
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
        var pending = _pending;
        if (pending == null) return;
        try
        {
            var frames = pending.Simulation.Frames;
            var timer = Stopwatch.StartNew();
            while (frames != null && pending.Frame < frames.Count && timer.Elapsed.TotalMilliseconds < FrameBudgetMs)
                Project(pending, frames[pending.Frame++]);
            if (!pending.Completed || (frames != null && pending.Frame < frames.Count)) return;
            var (totals, uses) = Stats(pending.Simulation);
            Report = new CombatImpactReport(pending.Entities, pending.Executions, totals, uses);
            _pending = null;
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.combat_impact.ready generation={pending.Generation} executions={pending.Executions.Count} sources={totals.Count}");
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

    private static void Project(Pending pending, CombatSimFrame frame)
    {
        Reconcile(pending, frame, previous: true);
        var executed = new List<CombatImpactExecution>();
        if (frame.Events != null)
            foreach (var item in frame.Events)
            {
                var effect = item?.TryCast<CombatSimEventEffectExecuted>();
                if (effect == null) continue;
                var kind = CombatImpactReport.Kind(effect.ActionType.ToString());
                if (kind == null) continue;
                var source = Activity(pending, InstanceIdOf(effect, "get_Source"))
                    ?? Activity(pending, InstanceIdOf(effect, "get_TriggerSource"));
                var target = TargetOf(effect.Target);
                if (source == null || target == null) continue;
                int? value = null;
                var timing = Array.IndexOf(Timing, TimingAttribute(kind.Value));
                if (timing >= 0 && pending.Attributes.TryGetValue(source, out var values)) value = values[timing];
                executed.Add(new CombatImpactExecution(source, target, kind.Value, value));
            }
        MarkCritical(executed, frame.PlayerUpdates, CombatImpactReport.PlayerId(false));
        MarkCritical(executed, frame.OpponentUpdates, CombatImpactReport.PlayerId(true));
        pending.Executions.AddRange(executed);
        Reconcile(pending, frame, previous: false);
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
