using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.CollectionPanel.Data;

namespace BazaarPlusPlus.Android;

// Separate from the ordinary atlas. Only a completed item selection updates this session's memory.
internal sealed class AndroidCollectionPickerState
{
    private (CollectionFilterState Filter, string? SourceKey, bool Follow)? _saved;

    internal (CollectionFilterState Filter, string? SourceKey, bool Follow) Open(EHero? hero)
    {
        if (_saved is { } saved) return (Copy(saved.Filter), saved.SourceKey, saved.Follow);
        var filter = new CollectionFilterState { ActiveType = ECardType.Item, UseRunDayFilter = false };
        if (hero.HasValue) filter.ToggleHero(hero.Value);
        else filter.ToggleAllHeroes();
        return (filter, null, false);
    }

    internal void Remember(CollectionFilterState filter, string? sourceKey, bool follow) =>
        _saved = (Copy(filter), sourceKey, follow);

    private static CollectionFilterState Copy(CollectionFilterState source)
    {
        var copy = new CollectionFilterState
        {
            ActiveType = ECardType.Item,
            SearchQuery = source.SearchQuery,
            UseRunDayFilter = source.UseRunDayFilter,
            SelectedSourceKey = source.SelectedSourceKey,
            TagMatchMode = source.TagMatchMode,
            KeywordMatchMode = source.KeywordMatchMode,
            SortPriority = source.SortPriority,
        };
        copy.ToggleHero(source.EffectiveHero);
        if (source.AllHeroesSelected) copy.ToggleAllHeroes();
        copy.Tiers.UnionWith(source.Tiers);
        copy.Sizes.UnionWith(source.Sizes);
        copy.Tags.UnionWith(source.Tags);
        copy.Keywords.UnionWith(source.Keywords);
        copy.Mechanics.UnionWith(source.Mechanics);
        return copy;
    }
}
