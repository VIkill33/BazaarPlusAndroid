using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

internal sealed class AndroidFavoriteBuildPopup
{
    private GameObject? _canvas;
    private AndroidBuildBoard? _board;
    private TextMeshProUGUI? _title;
    private TextMeshProUGUI? _page;
    private TextMeshProUGUI? _origin;
    private TextMeshProUGUI? _summary;
    private Image? _gem;
    private Button? _previous;
    private Button? _next;
    private IReadOnlyList<(CustomBuild Build, int Number)> _builds = Array.Empty<(CustomBuild, int)>();
    private int _index;
    internal bool IsOpen { get; private set; }

    internal void Open(Guid templateId, IReadOnlyList<(CustomBuild Build, int Number)> builds)
    {
        if (builds.Count == 0 || AndroidOwnedCards.Read() == null) return;
        if (_canvas == null)
        {
            var font = NotoFontFallbackRuntime._loadedSansPrimary;
            if (font == null) return;
            BuildUi(font);
        }
        _builds = builds.ToArray();
        _index = 0;
        IsOpen = true;
        _canvas!.SetActive(true);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_popup.opened template={templateId} builds={builds.Count}");
        ShowPage();
    }

    private void BuildUi(TMP_FontAsset font)
    {
        _canvas = new GameObject("BPP.FavoriteBuildPopup", new Il2CppSystem.Type[] { Il2CppType.Of<RectTransform>() });
        UnityEngine.Object.DontDestroyOnLoad(_canvas);
        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30001;
        var scaler = _canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        _canvas.AddComponent<GraphicRaycaster>();
        var mask = NativeUi.Rect("Mask", _canvas.transform, Vector2.zero, Vector2.one);
        var shade = mask.gameObject.AddComponent<Image>();
        shade.color = new Color(0, 0, 0, .6f);
        shade.raycastTarget = true;
        var dismiss = mask.gameObject.AddComponent<Button>();
        dismiss.targetGraphic = shade;
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(Close));
        // Sibling of the dismiss button: clicks in the panel cannot bubble to that button.
        var panel = NativeUi.Rect("Panel", _canvas.transform, new Vector2(.07f, .27f), new Vector2(.93f, .73f));
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(.07f, .035f, .10f, .99f);
        background.raycastTarget = true;
        var gemHost = NativeUi.Rect("BuildColour", panel, new Vector2(.04f, .86f), new Vector2(.04f, .86f));
        gemHost.sizeDelta = new Vector2(24, 24);
        gemHost.localRotation = Quaternion.Euler(0, 0, 45);
        _gem = gemHost.gameObject.AddComponent<Image>();
        _gem.raycastTarget = false;
        _title = NativeUi.Label(NativeUi.Rect("Title", panel, new Vector2(.065f, .77f), new Vector2(.40f, .96f)), font, string.Empty, 36);
        _title.alignment = TextAlignmentOptions.MidlineLeft;
        _title.fontStyle = FontStyles.Bold;
        _page = NativeUi.Label(NativeUi.Rect("Page", panel, new Vector2(.42f, .77f), new Vector2(.58f, .96f)), font, string.Empty, 28);
        Skin(NativeUi.Button(panel, font, () => NativeUi.Text("Close", "关闭"),
            new Vector2(.80f, .77f), new Vector2(.97f, .96f), Close));
        var remove = Skin(NativeUi.Button(panel, font, () => NativeUi.Text("Remove favorite", "取消收藏提醒"),
            new Vector2(.60f, .77f), new Vector2(.79f, .96f), RemoveFavorite));
        var caption = remove.GetComponentInChildren<TextMeshProUGUI>();
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 20;
        caption.fontSizeMax = 28;
        caption.textWrappingMode = TextWrappingModes.NoWrap;
        caption.rectTransform.offsetMin = new Vector2(4, 0);
        caption.rectTransform.offsetMax = new Vector2(-4, 0);
        _origin = NativeUi.Label(NativeUi.Rect("Origin", panel, new Vector2(.10f, .68f), new Vector2(.90f, .77f)),
            font, string.Empty, 24);
        _origin.rectTransform.offsetMin = Vector2.zero;
        _origin.rectTransform.offsetMax = Vector2.zero;
        var host = NativeUi.Rect("Board", panel, new Vector2(.10f, .27f), new Vector2(.90f, .66f));
        _board = new AndroidBuildBoard(host, (_, _, _) => { }, keepSlots: true, captionFont: font);
        _previous = Skin(NativeUi.Button(panel, font, "◀", new Vector2(.045f, .5f), new Vector2(.045f, .5f),
            () => Page(-1), new Vector2(-48, -48), new Vector2(48, 48)));
        _next = Skin(NativeUi.Button(panel, font, "▶", new Vector2(.955f, .5f), new Vector2(.955f, .5f),
            () => Page(1), new Vector2(-48, -48), new Vector2(48, 48)));
        _summary = NativeUi.Label(NativeUi.Rect("Summary", panel, new Vector2(.10f, .02f), new Vector2(.90f, .16f)), font, string.Empty, 28);
    }

    private static Button Skin(Button button)
    {
        AndroidPanelButtonSkin.Register(button);
        return button;
    }

    private void RemoveFavorite()
    {
        if (!IsOpen || _builds.Count == 0) return;
        try
        {
            // Another panel may have changed the file since this popup opened.
            var store = new CustomBuildStore(AndroidCustomBuildPanel.StorePath);
            store.Load();
            var id = _builds[_index].Build.Id;
            store.Delete(id);
            AndroidFavoriteMarks.Invalidate();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_popup.removed build={id} remaining={store.Builds.Count}");
            Close();
        }
        catch (Exception error)
        {
            _summary!.text = NativeUi.Text("Could not remove favorite. Try again.", "取消失败，请重试");
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.favorite_popup.remove_failed type={error.GetType().Name}");
        }
    }

    private void Page(int delta)
    {
        if (!IsOpen || _builds.Count == 0) return;
        _index = (_index + delta + _builds.Count) % _builds.Count;
        ShowPage();
    }

    private void ShowPage()
    {
        var owned = AndroidOwnedCards.Read();
        if (owned == null) { Close(); return; }
        var (build, number) = _builds[_index];
        var items = build.Items.OrderBy(item => item.Slot).ToArray();
        var states = CustomBuildOwnership.Classify(items, owned.Board, owned.Stash, owned.Offered);
        var have = states.Count(state => state is CustomBuildCardState.Board or CustomBuildCardState.Stash);
        var color = AndroidFavoriteMarks.Colors[CustomBuildStore.StarColor(build.Star ?? 0)];
        _gem!.color = color;
        _title!.color = color;
        _title.text = NativeUi.Text($"Build {number}", $"构筑 {number}");
        _origin!.text = build.Recommended switch
        {
            true => NativeUi.Text("Recommended 10-win build", "推荐十胜构筑"),
            false => NativeUi.Text("Custom build", "自定义构筑"),
            null => NativeUi.Text("Saved build · origin not recorded", "收藏构筑 · 来源未记录"),
        };
        _page!.text = $"{_index + 1} / {_builds.Count}";
        _summary!.text = NativeUi.Text($"Owned {have} / {items.Length} · {items.Length - have} missing",
            $"已有 {have} / {items.Length} 件 · 还缺 {items.Length - have} 件");
        _previous!.gameObject.SetActive(_builds.Count > 1);
        _next!.gameObject.SetActive(_builds.Count > 1);
        _board!.Show(AndroidCustomBuildPanel.Preview(items), states.Select(Caption).ToArray());
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.favorite_popup.paged index={_index} have={have} total={items.Length}");
    }

    private static string Caption(CustomBuildCardState state) => state switch
    {
        CustomBuildCardState.Board => $"<color=#8FE39A>{NativeUi.Text("Board", "场上")}</color>",
        CustomBuildCardState.Stash => $"<color=#82B8FF>{NativeUi.Text("Stash", "背包中")}</color>",
        CustomBuildCardState.Offered => $"<color=#FFD68C>{NativeUi.Text("Offered", "本次可选")}</color>",
        _ => $"<color=#B5A998>{NativeUi.Text("Missing", "未拥有")}</color>",
    };

    internal void Close()
    {
        if (!IsOpen) return;
        _board?.Hide();
        _canvas?.SetActive(false);
        _builds = Array.Empty<(CustomBuild, int)>();
        IsOpen = false;
        Plugin.PluginLog?.LogInfo("[BPP][Android] event=android.favorite_popup.closed");
    }

    internal void Tick()
    {
        if (IsOpen) _board?.Tick();
    }
}
