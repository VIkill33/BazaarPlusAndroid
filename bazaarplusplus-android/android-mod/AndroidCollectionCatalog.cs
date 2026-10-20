using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.Game.CollectionPanel.Data;
using BazaarPlusPlus.GameInterop.Cards;
using Il2CppInterop.Runtime;
using TheBazaar.DataManagement.Json;
using NativeCardMap = Il2CppSystem.Collections.Generic.Dictionary<Il2CppSystem.Guid, BazaarGameShared.Domain.Cards.ITCard>;

namespace BazaarPlusPlus.Android;

internal sealed class AndroidCollectionCatalog
{
    private Task<NativeCardMap>? _load;
    private Queue<ITCard>? _pending;
    private int _rejected;
    private Exception? _firstProjectionError;
    internal List<CollectionCardVm> Cards { get; } = new();
    internal Dictionary<Guid, TCardBase> Templates { get; } = new();
    internal bool Ready { get; private set; }
    internal string? Error { get; private set; }
    internal int NativeCount { get; private set; }

    internal void Retry()
    {
        if (Error == null) return;
        Error = null;
        Cards.Clear();
        Templates.Clear();
        _rejected = 0;
        _firstProjectionError = null;
    }

    internal void Start(JsonGameDataManager manager)
    {
        if (_load != null || _pending != null || Ready)
            return;
        Error = null;
        _load = Task.Run(() =>
        {
            // This call reads static SQLite/JSON data, not Unity objects. A CLR worker
            // must explicitly attach to the game's IL2CPP domain before native calls.
            var attachedHere = IL2CPP.il2cpp_thread_current() == IntPtr.Zero;
            var thread = attachedHere ? IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get()) : IntPtr.Zero;
            try { return manager.GetCardMap(); }
            finally { if (thread != IntPtr.Zero) IL2CPP.il2cpp_thread_detach(thread); }
        });
    }

    internal bool Tick()
    {
        if (Ready || Error != null)
            return false;
        try
        {
            if (_load != null)
            {
                if (!_load.IsCompleted)
                    return false;
                var map = _load.GetAwaiter().GetResult();
                NativeCount = map.Count;
                _pending = new Queue<ITCard>(map.Count);
                foreach (var pair in map)
                    _pending.Enqueue(pair.Value);
                _load = null;
            }
            if (_pending == null)
                return false;
            for (var index = 0; index < 40 && _pending.Count > 0; index++)
            {
                var raw = _pending.Dequeue();
                try
                {
                    var template = raw.TryCast<TCardBase>();
                    if (template == null || !CollectionCardClassifier.IsCatalogCard(template))
                        continue;
                    var card = Project(template);
                    if (Templates.TryAdd(card.Id, template))
                        Cards.Add(card);
                }
                catch (Exception error) { _rejected++; _firstProjectionError ??= error; }
            }
            if (_pending.Count > 0)
                return false;
            _pending = null;
            Ready = true;
            if (_firstProjectionError != null)
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.collection.catalog_partial rejected_count={_rejected} exception={_firstProjectionError}");
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.collection.catalog_ready native_count={NativeCount} projected_count={Cards.Count} rejected_count={_rejected}");
            return true;
        }
        catch (Exception error)
        {
            Error = error.Message;
            _load = null;
            _pending = null;
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.collection.catalog_failed exception={error}");
            return true;
        }
    }

    private static CollectionCardVm Project(TCardBase card)
    {
        var heroes = Copy(card.Heroes);
        var tags = Copy(card.Tags);
        var hidden = Copy(card.HiddenTags);
        var title = card.Localization?.Title;
        var description = card.Localization?.Description;
        var displayName = title == null ? card.InternalName : Localize(title);
        var displayDescription = description == null ? string.Empty : Localize(description);
        return new CollectionCardVm
        {
            Id = Guid.Parse(card.Id.ToString()), Type = card.Type, Size = card.Size,
            StartingTier = card.StartingTier, Heroes = heroes, Tags = tags, HiddenTags = hidden,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? card.InternalName : displayName,
            Description = displayDescription, InternalName = card.InternalName, ArtKey = card.ArtKey,
            IsPackage = PackageIdentity.IsPackage(hidden),
            SearchText = string.Join(" ", displayName, title?.Text, card.InternalName, card.ArtKey,
                displayDescription, description?.Text, card.Type, card.Size, card.StartingTier,
                string.Join(" ", heroes), string.Join(" ", tags), string.Join(" ", hidden)),
        };
    }

    private static string Localize(BazaarGameShared.Domain.Core.TLocalizableText text)
    {
        try { return TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(text) ?? text.Text ?? string.Empty; }
        catch (Exception) { return text.Text ?? string.Empty; }
    }

    private static T[] Copy<T>(Il2CppSystem.Collections.Generic.HashSet<T>? source)
    {
        if (source == null) return Array.Empty<T>();
        var values = new List<T>(source.Count);
        foreach (var value in source) values.Add(value);
        return values.ToArray();
    }
}
