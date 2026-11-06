using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Skill;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Players;
using TMPro;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;
using NativePreviewTask = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;

namespace BazaarPlusPlus.Android;

// CardPreviewBase is the same native card renderer used by the Android collection.
// Every rental belongs to one observed tooltip and is retired when that tooltip closes.
internal sealed class AndroidMonsterCardGallery
{
    private sealed class Request
    {
        internal required RectTransform Host;
        internal required TCardInstance Instance;
        internal required bool Skill;
        internal NativePreviewTask? Creation;
        internal Il2CppSystem.Threading.Tasks.Task? Setup;
        internal Il2CppSystem.Threading.CancellationTokenSource? Cancellation;
        internal CardPreviewBase? Card;
        internal bool Ready;
        internal int FitAttempts;
        internal int NextFitFrame;
    }

    private const float CanvasHeight = 1080;
    private const float SkillSlotScale = 1.2f;
    private const float SkillSeparatorGap = 14;
    private const float HealthHeaderHeight = 44;
    private const float HealthHeaderRightInset = 58;

    private readonly RectTransform _root;
    private readonly RectTransform _panel;
    private readonly RectTransform _healthHeader;
    private readonly AndroidMonsterHealthBar _healthBar;
    private readonly List<Request> _requests = new();
    private RectTransform? _separator;
    private string? _monsterId;

    internal AndroidMonsterCardGallery(RectTransform panel, TMP_FontAsset font)
    {
        _panel = panel;
        _root = NativeUi.Rect("BPP.MonsterCards", panel, Vector2.zero, Vector2.one,
            new Vector2(16, 12), new Vector2(-16, -36));
        _root.gameObject.AddComponent<RectMask2D>();
        _root.gameObject.SetActive(false);
        _healthHeader = NativeUi.Rect("BPP.MonsterHealth", panel, Vector2.up, Vector2.one,
            new Vector2(12, -HealthHeaderHeight - 8), new Vector2(-HealthHeaderRightInset, -8));
        _healthBar = new AndroidMonsterHealthBar(_healthHeader, font);
        _healthHeader.gameObject.SetActive(false);
    }

    internal void Show(TMonster? monster)
    {
        var id = monster?.Id.ToString();
        if (_monsterId == id && (_root.gameObject.activeSelf || _healthHeader.gameObject.activeSelf)) return;
        Hide();
        var health = monster?.Player?.Attributes != null &&
            monster.Player.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out var maxHealth)
                ? maxHealth.ToString() : null;
        var hasHealth = health != null;
        _healthHeader.gameObject.SetActive(hasHealth);
        var items = monster?.Player?.Hand?.Items;
        var skills = new List<TCardInstanceSkill>();
        if (monster?.Player?.Skills != null)
            foreach (var skill in monster.Player.Skills)
                if (skill != null) skills.Add(skill);
        var count = items?.Count ?? 0;
        var skillCount = skills.Count;
        if (monster == null || count + skillCount == 0 && !hasHealth) return;
        _monsterId = id;
        var canvas = _panel.GetComponentInParent<Canvas>();
        var scale = canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
        var slotWidth = Screen.width / 26f / scale;
        var cardHeight = slotWidth / NativeCardFit.UnitAspect;
        var rowHeight = cardHeight + 12;
        var placements = new List<(TCardInstance Instance, bool Skill, Vector2 Position, Vector2 Size)>();
        var row = 0;
        var occupied = 0;
        var widestRow = 0;
        for (var index = 0; index < count; index++)
        {
            var instance = items![index];
            var template = Data.GetStatic()?.GetCardById(instance.TemplateId)?.TryCast<TCardBase>();
            var size = NativeCardFit.SizeInSlots(template?.Size ?? ECardSize.Small);
            if (occupied + size > 10) { row++; occupied = 0; }
            placements.Add((instance, false, new Vector2(occupied * slotWidth + 4, -row * rowHeight - 8),
                new Vector2(size * slotWidth, cardHeight)));
            occupied += size;
            widestRow = Math.Max(widestRow, occupied);
        }
        var itemsHeight = count == 0 ? 0 : (row + 1) * rowHeight;
        var skillSize = slotWidth * SkillSlotScale;
        var skillPitch = skillSize + 8;
        var skillsPerRow = Math.Max(1, (int)(10 * slotWidth / skillPitch));
        var skillRows = (skillCount + skillsPerRow - 1) / skillsPerRow;
        var skillsTop = itemsHeight + (count > 0 && skillCount > 0 ? SkillSeparatorGap : 0);
        for (var index = 0; index < skillCount; index++)
            placements.Add((skills[index], true, new Vector2(index % skillsPerRow * skillPitch + 4,
                -skillsTop - index / skillsPerRow * skillPitch - 8), new Vector2(skillSize, skillSize)));
        var contentWidth = Math.Max(widestRow * slotWidth, Math.Min(skillCount, skillsPerRow) * skillPitch);
        var contentHeight = skillsTop + skillRows * skillPitch;
        var panelWidth = hasHealth ? Math.Max(contentWidth + 40, 240) : contentWidth + 40;
        var headerHeight = hasHealth ? HealthHeaderHeight : 0;
        var panelHeight = contentHeight + 44 + headerHeight;
        var centerY = Mathf.Max(panelHeight / 2 + 16,
            Mathf.Min(520, CanvasHeight - 16 - panelHeight / 2));
        _panel.offsetMin = new Vector2(-550 - panelWidth, centerY - panelHeight / 2);
        _panel.offsetMax = new Vector2(-550, centerY + panelHeight / 2);
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.right;
        _root.pivot = new Vector2(.5f, 0);
        _root.offsetMin = new Vector2(16, 16);
        _root.offsetMax = new Vector2(-16, panelHeight - 24 - headerHeight);
        _root.gameObject.SetActive(count + skillCount > 0);
        if (hasHealth) _healthBar.Show(health!, panelWidth - 12 - HealthHeaderRightInset, HealthHeaderHeight);
        if (count > 0 && skillCount > 0)
        {
            _separator = NativeUi.Rect("BPP.MonsterSkillSeparator", _root, Vector2.up, Vector2.one);
            _separator.pivot = new Vector2(.5f, 1);
            _separator.offsetMin = new Vector2(8, -itemsHeight - SkillSeparatorGap / 2 - 8);
            _separator.offsetMax = new Vector2(-8, -itemsHeight - SkillSeparatorGap / 2 - 6);
            var line = _separator.gameObject.AddComponent<Image>();
            line.color = new Color(.85f, .68f, .38f, .35f);
            line.raycastTarget = false;
        }
        foreach (var placement in placements)
        {
            var host = NativeUi.Rect("BPP.MonsterCard", _root, Vector2.up, Vector2.up);
            host.pivot = Vector2.up;
            host.sizeDelta = placement.Size;
            host.anchoredPosition = placement.Position;
            _requests.Add(new Request { Host = host, Instance = placement.Instance, Skill = placement.Skill });
        }
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_gallery.started monster={monster.InternalName} items={count} skills={skillCount} slots={widestRow} item_rows={(count == 0 ? 0 : row + 1)} skill_rows={skillRows} slot_width_px={slotWidth * scale:0.#} screen_width={Screen.width}");
    }

    internal void Tick()
    {
        if (!_root.gameObject.activeSelf || !Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var request in _requests)
        {
            if (request.Ready)
            {
                if (request.FitAttempts > 0 && Time.frameCount >= request.NextFitFrame)
                {
                    Fit(request);
                    request.FitAttempts--;
                    request.NextFitFrame = Time.frameCount + 2;
                }
                continue;
            }
            try
            {
                if (request.Creation == null)
                {
                    var template = Data.GetStatic()?.GetCardById(request.Instance.TemplateId)?.TryCast<TCardBase>();
                    if (template == null) { request.Ready = true; continue; }
                    request.Cancellation = new Il2CppSystem.Threading.CancellationTokenSource();
                    request.Creation = loader.GenerateUICard(request.Host, request.Cancellation.Token, template);
                    continue;
                }
                if (request.Card == null && request.Creation.IsCompleted)
                {
                    if (request.Creation.IsFaulted || request.Creation.IsCanceled) { request.Ready = true; continue; }
                    request.Card = request.Creation.Result;
                    var template = Data.GetStatic()?.GetCardById(request.Instance.TemplateId)?.TryCast<TCardBase>();
                    if (request.Card == null || template == null) { request.Ready = true; continue; }
                    NativeCardFit.Attach(request.Card, request.Host);
                    request.Setup = request.Card.SetUp(template, false, request.Instance, request.Cancellation!.Token);
                }
                if (request.Setup is { IsCompleted: true } && !request.Ready)
                {
                    request.Ready = true;
                    if (request.Setup.IsFaulted || request.Setup.IsCanceled) continue;
                    request.Card!.Resize();
                    request.Card.Show(true);
                    Fit(request);
                    request.FitAttempts = 3;
                    request.NextFitFrame = Time.frameCount + 2;
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_gallery.card_ready template_id={request.Instance.TemplateId}");
                }
            }
            catch (Exception error)
            {
                request.Ready = true;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.monster_gallery.card_failed template_id={request.Instance.TemplateId} exception={error}");
            }
        }
    }

    private static void Fit(Request request)
    {
        if (request.Skill) NativeCardFit.FitFrame(request.Card!, request.Host);
        else NativeCardFit.FitItem(request.Card!, request.Host);
    }

    internal void Hide()
    {
        _root.gameObject.SetActive(false);
        _healthHeader.gameObject.SetActive(false);
        _healthBar.Hide();
        _monsterId = null;
        if (_separator != null) UnityEngine.Object.Destroy(_separator.gameObject);
        _separator = null;
        foreach (var request in _requests)
        {
            NativePreviewCleanup.Retire(request.Creation, request.Setup, request.Card, request.Cancellation);
            UnityEngine.Object.Destroy(request.Host.gameObject);
        }
        _requests.Clear();
    }
}
