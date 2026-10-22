using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.CollectionPanel.Sources;
using BazaarPlusPlus.GameInterop.DayTiers;
using TheBazaar;

namespace BazaarPlusPlus.Android;

internal static class AndroidCollectionContext
{
    internal static bool InRun => TheBazaar.DataExtensions.IsInRun(TheBazaar.AppState.CurrentState);

    internal static Guid? TemplateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (Guid.TryParse(id, out var template)) return template;
        var card = Data.GetCard(id);
        return card == null ? null : Guid.Parse(card.TemplateId.ToString());
    }

    internal static CollectionSourceEntry? Current(IReadOnlyList<CollectionSourceEntry> entries, EHero hero)
    {
        var state = Data.CurrentState;
        if (!InRun || state == null) return null;
        CollectionSourceEntry? Find(Guid? id) => id.HasValue
            ? entries.FirstOrDefault(entry => entry.AppliesToHero(hero) && entry.SourceTemplateIds.Contains(id.Value)) : null;
        var current = Find(TemplateId(state.CurrentEncounterId));
        if (current != null) return current;
        // The selection set can contain offers as well as encounters; only catalog source IDs match.
        if (state.SelectionSet != null)
            for (var index = 0; index < state.SelectionSet.Count; index++)
            {
                var source = Find(TemplateId(state.SelectionSet[index]));
                if (source != null) return source;
            }
        return null;
    }

    internal static (int? Day, GameDataDayTierTable? Tiers) DayTiers()
    {
        var run = Data.Run;
        if (!InRun || run == null || run.Day == 0 || !Data.IsManagerCreated()) return (null, null);
        var mode = Data.GetStatic().GetGameModeById(run.GameModeId);
        var weights = mode?.ItemSkillSpawnTierPercantagesByDay;
        if (weights == null || !weights.TryGetValue(run.Day, out var day) || day == null)
            return (checked((int)run.Day), null);
        return (checked((int)run.Day), GameDataDayTierTable.FromWeights(day.Bronze, day.Silver, day.Gold, day.Diamond));
    }

}
