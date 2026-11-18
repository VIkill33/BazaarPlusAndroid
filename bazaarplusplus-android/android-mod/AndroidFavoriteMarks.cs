using BazaarGameClient.Domain.Models.Cards;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BazaarPlusPlus.Android;

// Flags offered cards (shop, rewards, events) that belong to a starred custom build with
// one gem per card, split into one band per build colour. Screen-space only: game-owned card objects are never touched.
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
    private readonly Dictionary<Guid, (CustomBuild Build, int Number)[]> _stars = new();
    private readonly List<(CardController Card, Guid Template, int[] Colors)> _targets = new();
    private readonly List<RectTransform> _gems = new();
    private sealed class Hit(RectTransform root)
    {
        internal readonly RectTransform Root = root;
        internal CardController? Card;
        internal Guid Template;
        internal IntPtr PressedCard;
        internal Guid PressedTemplate;
    }
    private readonly List<Hit> _hits = new();
    private readonly AndroidFavoriteBuildPopup _popup = new();
    private string[] _selection = Array.Empty<string>();
    private IntPtr _state;
    private IntPtr _player;
    private int _scene;
    private RectTransform? _root;
    private float _nextMatch;
    private int _shown = -1;
    private int _failures;

    public void Update()
    {
        try
        {
            if (_stale) { _popup.Close(); Reload(); }
            var hidden = _stars.Count == 0 || AndroidBuildPanelController.AnyOpen || CollectionController.AnyOpen
                || AndroidHistoryPanel.AnyOpen || AndroidHoldPreview.Active;
            if (_popup.IsOpen && (hidden || !SameSelection())) _popup.Close();
            _popup.Tick();
            if (hidden || _popup.IsOpen)
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
            _popup.Close();
            _targets.Clear();
            foreach (var hit in _hits) hit.Root.gameObject.SetActive(false);
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
        var starred = _store.Builds.Select((build, index) => (Build: build, Number: index + 1))
            .Where(entry => entry.Build.Star != null).ToArray();
        foreach (var cards in starred.SelectMany(entry => entry.Build.Items.Select(item =>
            (item.TemplateId, entry.Build, entry.Number))).GroupBy(entry => entry.TemplateId))
            _stars[cards.Key] = cards.Select(entry => (entry.Build, entry.Number)).Distinct().ToArray();
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
                if (item == null || !_stars.TryGetValue(Guid.Parse(item.TemplateId.ToString()), out var builds)) continue;
                var controller = lookup.GetCardController(card!)?.TryCast<CardController>();
                if (controller != null) _targets.Add((controller, Guid.Parse(item.TemplateId.ToString()),
                    builds.Select(entry => CustomBuildStore.StarColor(entry.Build.Star!.Value)).Distinct().Take(MaxGemsPerCard).ToArray()));
            }
        }
        if (_targets.Count == _shown) return;
        _shown = _targets.Count;
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_marks.matched offered={offered?.Count ?? 0} marked={_shown}");
    }

    private void Draw()
    {
        var used = 0;
        var hits = 0;
        foreach (var (card, template, colors) in _targets)
        {
            if (card == null) continue;
            var bounds = AndroidHoldPreviewVisuals.CardBounds(card);
            if (!bounds.HasValue) continue;
            var size = Mathf.Clamp(bounds.Value.height * .16f, 18, 46);
            // Centred on the card's corner so a near miss lands on the table or the hit area, not the card body.
            var corner = new Vector2(bounds.Value.xMin, bounds.Value.yMax);
            var gem = Gem(used++);
            gem.sizeDelta = new Vector2(size, size);
            gem.anchoredPosition = corner;
            var bands = gem.GetChild(0);
            for (var index = 0; index < bands.childCount; index++)
            {
                var band = bands.GetChild(index).GetComponent<RectTransform>();
                var shown = index < colors.Length;
                band.gameObject.SetActive(shown);
                if (!shown) continue;
                band.anchorMin = new Vector2((float)index / colors.Length, 0);
                band.anchorMax = new Vector2((float)(index + 1) / colors.Length, 1);
                band.GetComponent<Image>().color = Colors[colors[index]];
            }
            gem.gameObject.SetActive(true);
            var hit = HitArea(hits++);
            hit.Card = card;
            hit.Template = template;
            // About 8 mm for a fingertip, but never more than half the card so the card body stays tappable.
            var reach = Mathf.Min(Mathf.Max(size * 2, (Screen.dpi > 0 ? Screen.dpi : 160) * .32f), bounds.Value.width * .5f);
            hit.Root.sizeDelta = new Vector2(reach, reach);
            hit.Root.anchoredPosition = corner;
            hit.Root.gameObject.SetActive(true);
        }
        for (var index = hits; index < _hits.Count; index++)
        {
            _hits[index].PressedCard = IntPtr.Zero;
            _hits[index].Root.gameObject.SetActive(false);
        }
        for (var index = used; index < _gems.Count; index++) _gems[index].gameObject.SetActive(false);
    }

    private Hit HitArea(int index)
    {
        while (_hits.Count <= index)
        {
            var rect = NativeUi.Rect("BPP.FavoriteMarkHit", _root!, Vector2.zero, Vector2.zero);
            var hit = new Hit(rect);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0, 0, 0, .01f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var trigger = rect.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((BaseEventData _) =>
            {
                hit.PressedCard = hit.Card?.Pointer ?? IntPtr.Zero;
                hit.PressedTemplate = hit.Template;
            }));
            trigger.triggers.Add(down);
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(() =>
            {
                if (hit.Card == null || hit.PressedCard == IntPtr.Zero || hit.PressedCard != hit.Card.Pointer
                    || hit.PressedTemplate != hit.Template || !_stars.TryGetValue(hit.Template, out var builds)) return;
                // Match once more at release: a removed offer must not open stale data.
                var selected = Data.CurrentState?.SelectionSet;
                var lookup = Data.CardAndSkillLookup;
                var present = false;
                if (selected != null && lookup != null)
                    for (var i = 0; i < selected.Count; i++)
                    {
                        var card = Data.GetCard(selected[i]);
                        if (card != null && lookup.GetCardController(card)?.Pointer == hit.PressedCard) { present = true; break; }
                    }
                hit.PressedCard = IntPtr.Zero;
                if (!present) return;
                _selection = Selection();
                _state = Data.CurrentState?.Pointer ?? IntPtr.Zero;
                _player = Data.Run?.Player?.Pointer ?? IntPtr.Zero;
                _scene = SceneManager.GetActiveScene().handle;
                _popup.Open(hit.Template, builds);
                _targets.Clear();
                Draw();
            }));
            _hits.Add(hit);
        }
        return _hits[index];
    }

    private static string[] Selection()
    {
        var selection = Data.CurrentState?.SelectionSet;
        if (selection == null) return Array.Empty<string>();
        var ids = new string[selection.Count];
        for (var i = 0; i < ids.Length; i++) ids[i] = selection[i].ToString();
        return ids;
    }

    private bool SameSelection() => _selection.Length > 0
        && _state == (Data.CurrentState?.Pointer ?? IntPtr.Zero)
        && _player != IntPtr.Zero && _player == (Data.Run?.Player?.Pointer ?? IntPtr.Zero)
        && _scene == SceneManager.GetActiveScene().handle && _selection.SequenceEqual(Selection());

    public void OnDisable()
    {
        _popup.Close();
        _targets.Clear();
        foreach (var gem in _gems) if (gem != null) gem.gameObject.SetActive(false);
        foreach (var hit in _hits) if (hit.Root != null) hit.Root.gameObject.SetActive(false);
    }

    private RectTransform Gem(int index)
    {
        if (_root == null)
        {
            var canvas = new GameObject("BPP.FavoriteMarksCanvas", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
            DontDestroyOnLoad(canvas);
            var overlay = canvas.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the table, below every plugin panel. Only flag hit areas receive touches.
            overlay.sortingOrder = 5;
            canvas.AddComponent<GraphicRaycaster>();
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
            var bands = NativeUi.Rect("Gem", gem, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            for (var band = 0; band < MaxGemsPerCard; band++)
                NativeUi.Rect("Band", bands, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>().raycastTarget = false;
            _gems.Add(gem);
        }
        return _gems[index];
    }
}
