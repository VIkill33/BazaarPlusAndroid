using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Interfaces;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Effect;
using BazaarGameShared.Domain.Effect.AuraActions;
using BazaarGameShared.Domain.Prerequisites;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Domain.Targeting;
using BazaarGameShared.Domain.Values;
using Il2CppInterop.Runtime;
using TheBazaar;

namespace BazaarPlusPlus.Android;

internal static class AndroidUpgradeAuraResolver
{
    internal readonly record struct Multipliers(float Current, float Projected);

    internal static Dictionary<ECardAttributeType, Multipliers> Resolve(ItemCard source, ItemCard projected)
    {
        var result = new Dictionary<ECardAttributeType, Multipliers>();
        var run = Data.Run?.Cast<IRun>();
        foreach (var auraSource in Sources(source, run))
        {
            Accumulate(source, auraSource, run, projectedValue: false, result);
            var projectedSource = auraSource.InstanceId.Equals(source.InstanceId) ? projected : auraSource;
            Accumulate(projected, projectedSource, run, projectedValue: true, result);
        }
        return result;
    }

    private static IEnumerable<Card> Sources(ItemCard source, IRun? run)
    {
        yield return source;
        if (run == null) yield break;
        var seen = new HashSet<string> { source.InstanceId.ToString() };
        foreach (var player in new[] { run.GetPlayer(), run.GetOpponent() })
        {
            if (player == null) continue;
            foreach (var inventory in new[] { player.Hand, player.Stash, player.Socket })
            {
                if (inventory?.Container == null) continue;
                foreach (var socket in inventory.Container.GetCardsAndSockets())
                {
                    var model = socket.Item1?.TryCast<Card>();
                    if (model != null && seen.Add(model.InstanceId.ToString())) yield return model;
                }
            }
            foreach (var card in Il2CppSystem.Linq.Enumerable.ToArray(player.GetSkills()))
            {
                var model = card?.TryCast<Card>();
                if (model != null && seen.Add(model.InstanceId.ToString())) yield return model;
            }
        }
    }

    private static void Accumulate(ItemCard target, Card source, IRun? run, bool projectedValue,
        Dictionary<ECardAttributeType, Multipliers> result)
    {
        foreach (var aura in Auras(source))
        {
            var action = aura.Action?.TryCast<TAuraActionCardModifyAttribute>();
            if (action == null || action.Operation != EAttributeModifierOperation.Multiply ||
                !IsActive(source, aura, run) || !Targets(target, source, action, run)) continue;
            var value = action.Value.GetValue(new ValueContext(run, source.Cast<ICard>(), null));
            var old = result.GetValueOrDefault(action.AttributeType, new Multipliers(1f, 1f));
            result[action.AttributeType] = projectedValue
                ? old with { Projected = old.Projected * value }
                : old with { Current = old.Current * value };
        }
    }

    private static IEnumerable<TCardAura> Auras(Card card)
    {
        var tierData = card.Template?.TryCast<IHasTierData>();
        if (tierData != null)
        {
            foreach (var aura in tierData.GetAuraTemplatesByTier(card.Tier)) yield return aura;
        }
        else if (card.Template?.Auras != null)
        {
            foreach (var aura in card.Template.Auras.Values) yield return aura;
        }

        var item = card.TryCast<ItemCard>();
        var template = item?.Template?.TryCast<TCardItem>();
        if (item == null || template == null) yield break;
        var enchantment = NativeNullableValue.Read<EEnchantmentType>(item, "get_Enchantment");
        if (enchantment.HasValue && template.TryGetEnchantmentTemplate(enchantment.Value, out var enchanted)
            && enchanted?.Auras != null)
            foreach (var aura in enchanted.Auras.Values) yield return aura;
        if (template.Quests == null) yield break;
        foreach (var group in template.Quests)
            foreach (var entry in group.Entries)
                if (entry.IsComplete(item.Cast<ICard>()) && entry.Reward?.HasAuras == true)
                    foreach (var aura in entry.Reward.Auras.Values) yield return aura;
    }

    private static bool IsActive(Card source, TCardAura aura, IRun? run)
    {
        if ((aura.WorksIn & EEffectWorksIn.OutOfCombatOnly) == 0) return false;
        if (source.Type == ECardType.Item)
        {
            var section = NativeNullableValue.Read<EInventorySection>(source, "get_Section");
            if (aura.ActiveIn == EEffectActiveIn.HandOnly && section != EInventorySection.Hand ||
                aura.ActiveIn == EEffectActiveIn.StashOnly && section != EInventorySection.Stash ||
                aura.ActiveIn == EEffectActiveIn.HandAndStash && section == null) return false;
        }
        if (aura.Prerequisites == null) return true;
        var context = new PrereqContext(run, source.Cast<ICard>(), null);
        foreach (var prerequisite in aura.Prerequisites)
            if (!prerequisite.IsSatisfiedBy(context)) return false;
        return true;
    }

    private static bool Targets(ItemCard target, Card source, TAuraActionCardModifyAttribute action, IRun? run)
    {
        if (run == null)
            return source.InstanceId.Equals(target.InstanceId) && action.Target?.TryCast<TTargetCardSelf>() != null;
        var context = new TargetingContext(run, source.Cast<ICard>(), null);
        foreach (var card in action.Target.GetTargets(context, null, null, null))
            if (card.GetInstanceId().Equals(target.InstanceId)) return true;
        return false;
    }
}
