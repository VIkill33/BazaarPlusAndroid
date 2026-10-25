using BazaarGameShared.Domain.Core.Types;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using Il2CppInterop.Runtime;
using System.Diagnostics;
using TheBazaar;

namespace BazaarPlusPlus.Android;

internal static class AndroidBattleCapture
{
    private static readonly HashSet<string> Warmed = new();
    private sealed record Group(string Status, object[] Cards, string? Reason = null);
    internal static void WarmOne()
    {
        if (AndroidRunObserver.CurrentPhase() is HistoryPhase.Replay or HistoryPhase.Combat or HistoryPhase.PvpCombat) return;
        if (Data.Run?.Player?.Hand?.Container is not { } hand) return;
        foreach (var pair in hand.GetCardsAndSockets())
        {
            var card = pair.Item1?.TryCast<Card>();
            if (card == null || !Warmed.Add(card.InstanceId.ToString())) continue;
            try { Project(card, Stopwatch.StartNew()); }
            catch (TimeoutException) { Plugin.PluginLog?.LogWarning("[BPP][Android] event=history.warm.budget_exceeded"); }
            break;
        }
        if (Warmed.Count > 512) Warmed.Clear();
    }

    internal static (object Opening, object Snapshots, bool Complete) Capture(Stopwatch timer)
    {
        var run = Data.Run;
        var opening = new
        {
            playerHero = run?.Player?.Hero.ToString(), opponentHero = run?.Opponent?.Hero.ToString(),
            opponentName = Data.SimPvpOpponent?.Name,
            gameVersion = UnityEngine.Application.version, pluginVersion = Plugin.Version,
        };
        var snapshots = new Dictionary<string, Group>
        {
            ["playerHand"] = Hand(run?.Player, timer), ["playerSkills"] = Skills(run?.Player, timer),
            ["opponentHand"] = Hand(run?.Opponent, timer), ["opponentSkills"] = Skills(run?.Opponent, timer),
        };
        return (opening, snapshots, snapshots.Values.All(group => group.Status == "captured"));
    }
    private static Group Hand(Player? player, Stopwatch timer)
    {
        var cards = new List<object>();
        try
        {
            if (player?.Hand?.Container is not { } container) return new("unavailable", []);
            foreach (var pair in container.GetCardsAndSockets())
            {
                Budget(timer); if (cards.Count >= HistoryLimits.MaxCardsPerGroup) throw new InvalidOperationException("card_limit");
                var card = pair.Item1?.TryCast<Card>(); if (card != null) cards.Add(Project(card, timer));
            }
            return new("captured", cards.ToArray());
        }
        catch (Exception error) { return new(cards.Count == 0 ? "failed" : "partial", cards.ToArray(), error.GetType().Name); }
    }
    private static Group Skills(Player? player, Stopwatch timer)
    {
        var cards = new List<object>();
        try
        {
            if (player?.Skills is not { } skills) return new("unavailable", []);
            foreach (var card in skills)
            {
                Budget(timer); if (cards.Count >= HistoryLimits.MaxCardsPerGroup) throw new InvalidOperationException("card_limit");
                if (card != null) cards.Add(Project(card, timer));
            }
            return new("captured", cards.ToArray());
        }
        catch (Exception error) { return new(cards.Count == 0 ? "failed" : "partial", cards.ToArray(), error.GetType().Name); }
    }
    private static object Project(Card card, Stopwatch timer)
    {
        var attributes = new Dictionary<string, int>();
        foreach (var pair in card.Attributes)
        {
            Budget(timer);
            if (attributes.Count >= HistoryLimits.MaxAttributesPerCard) throw new InvalidOperationException("attribute_limit");
            attributes[pair.Key.ToString()] = pair.Value;
        }
        var tags = new List<string>();
        foreach (var tag in card.Tags)
        {
            Budget(timer);
            if (tags.Count >= 128) throw new InvalidOperationException("tag_limit");
            tags.Add(tag.ToString());
        }
        Budget(timer);
        var template = card.Template?.TryCast<BazaarGameShared.Domain.Cards.TCardBase>();
        Budget(timer);
        var name = template?.Localization?.Title is { } title ? TheBazaar.Tooltips.TooltipExtensions.GetLocalizedText(title) : null;
        if (string.IsNullOrWhiteSpace(name)) name = template?.InternalName;
        if (string.IsNullOrWhiteSpace(name)) name = card.TemplateId.ToString();
        Budget(timer);
        var item = card.TryCast<ItemCard>();
        var socket = NativeNullableValue.Read<EContainerSocketId>(card, "get_LeftSocketId")?.ToString();
        Budget(timer);
        var section = NativeNullableValue.Read<EInventorySection>(card, "get_Section")?.ToString();
        Budget(timer);
        var enchantment = item == null ? null : NativeNullableValue.Read<EEnchantmentType>(item, "get_Enchantment")?.ToString();
        Budget(timer);
        return new
        {
            instanceId = card.InstanceId.ToString(), templateId = card.TemplateId.ToString(), name,
            size = card.Size.ToString(), tier = card.Tier.ToString(), type = card.Type.ToString(),
            socket, section, enchantment,
            attributes, tags,
        };
    }
    private static void Budget(Stopwatch timer)
    {
        if (timer.Elapsed.TotalMilliseconds > HistoryLimits.CaptureAbortMs) throw new TimeoutException("capture_budget");
    }
}
