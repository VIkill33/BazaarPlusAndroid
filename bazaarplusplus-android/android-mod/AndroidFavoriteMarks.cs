using BazaarGameClient.Domain.Models.Cards;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Flags offered cards (shop, rewards, events) that belong to a starred custom build,
// one gem per build colour. Screen-space only: game-owned card objects are never touched.
public sealed class AndroidFavoriteMarks(IntPtr pointer) : MonoBehaviour(pointer)
{
    // Indexed by CustomBuildStore.StarColor; the build list paints each starred row with the same colour.
    internal static readonly Color[] Colors =
    {
        new(1f, .78f, .30f, 1), new(.35f, .75f, 1f, 1), new(.45f, .90f, .50f, 1),
        new(1f, .50f, .75f, 1), new(.72f, .55f, 1f, 1), new(1f, .55f, .40f, 1),
    };
    private const int MaxGemsPerCard = 3;
    private static bool _stale = true;

    // Call after any change to the saved builds.
    internal static void Invalidate() => _stale = true;

    private readonly CustomBuildStore _store = new(AndroidCustomBuildPanel.StorePath);
    private readonly Dictionary<Guid, int[]> _stars = new();
    private readonly List<(CardController Card, int[] Colors)> _targets = new();
    private readonly List<RectTransform> _gems = new();
    private RectTransform? _root;
    private float _nextMatch;
    private int _shown = -1;
    private int _failures;

    public void Update()
    {
        try
        {
            if (_stale) Reload();
            if (_stars.Count == 0 || AndroidBuildPanelController.AnyOpen || CollectionController.AnyOpen
                || AndroidHistoryPanel.AnyOpen || AndroidHoldPreview.Active)
            {
                _targets.Clear();
                _nextMatch = 0;
            }
            else if (Time.realtimeSinceStartup >= _nextMatch)
            {
                _nextMatch = Time.realtimeSinceStartup + .25f;
                Match();
            }
            Draw();
        }
        catch (Exception error)
        {
            _failures++;
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.favorite_marks.failed count={_failures} exception={error}");
            _targets.Clear();
            foreach (var gem in _gems) if (gem != null) gem.gameObject.SetActive(false);
            if (_failures >= 3) enabled = false;
        }
    }

    private void Reload()
    {
        _stale = false;
        _stars.Clear();
        _nextMatch = 0;
        try { _store.Load(); }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.favorite_marks.load_failed type={error.GetType().Name}");
            return;
        }
        var starred = _store.Builds.Where(build => build.Star != null).ToArray();
        foreach (var cards in starred.SelectMany(build => build.Items.Select(item =>
            (item.TemplateId, Color: CustomBuildStore.StarColor(build.Star!.Value)))).GroupBy(entry => entry.TemplateId))
            _stars[cards.Key] = cards.Select(entry => entry.Color).Distinct().Take(MaxGemsPerCard).ToArray();
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_marks.loaded builds={starred.Length} cards={_stars.Count}");
    }

    // Shop stock, monster loot and event offers are all read from the current selection set.
    private void Match()
    {
        _targets.Clear();
        var offered = Data.CurrentState?.SelectionSet;
        var lookup = Data.CardAndSkillLookup;
        if (offered != null && lookup != null)
        {
            for (var index = 0; index < offered.Count; index++)
            {
                var card = Data.GetCard(offered[index]);
                var item = card?.TryCast<ItemCard>();
                if (item == null || !_stars.TryGetValue(Guid.Parse(item.TemplateId.ToString()), out var colors)) continue;
                var controller = lookup.GetCardController(card!)?.TryCast<CardController>();
                if (controller != null) _targets.Add((controller, colors));
            }
        }
        if (_targets.Count == _shown) return;
        _shown = _targets.Count;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_marks.matched offered={offered?.Count ?? 0} marked={_shown}");
    }

    private void Draw()
    {
        var used = 0;
        foreach (var (card, colors) in _targets)
        {
            if (card == null) continue;
            var bounds = AndroidHoldPreviewVisuals.CardBounds(card);
            if (!bounds.HasValue) continue;
            var size = Mathf.Clamp(bounds.Value.height * .16f, 18, 46);
            for (var index = 0; index < colors.Length; index++)
            {
                var gem = Gem(used++);
                gem.sizeDelta = new Vector2(size, size);
                gem.anchoredPosition = new Vector2(bounds.Value.xMin + size * (.75f + index * 1.3f), bounds.Value.yMax - size * .75f);
                gem.GetChild(0).GetComponent<Image>().color = Colors[colors[index]];
                gem.gameObject.SetActive(true);
            }
        }
        for (var index = used; index < _gems.Count; index++) _gems[index].gameObject.SetActive(false);
    }

    private RectTransform Gem(int index)
    {
        if (_root == null)
        {
            var canvas = new GameObject("BPP.FavoriteMarksCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
            DontDestroyOnLoad(canvas);
            var overlay = canvas.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the table, below every plugin panel. No raycaster: touches always reach the game.
            overlay.sortingOrder = 5;
            _root = canvas.GetComponent<RectTransform>();
        }
        while (_gems.Count <= index)
        {
            // A dark-rimmed square turned 45 degrees: readable on any card art without a font glyph or sprite.
            var gem = NativeUi.Rect("BPP.FavoriteMark", _root, Vector2.zero, Vector2.zero);
            gem.localRotation = Quaternion.Euler(0, 0, 45);
            var rim = gem.gameObject.AddComponent<Image>();
            rim.color = new Color(.05f, .03f, .07f, .95f);
            rim.raycastTarget = false;
            NativeUi.Rect("Gem", gem, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3))
                .gameObject.AddComponent<Image>().raycastTarget = false;
            _gems.Add(gem);
        }
        return _gems[index];
    }
}
