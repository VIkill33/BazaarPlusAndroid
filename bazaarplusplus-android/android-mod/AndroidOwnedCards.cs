using BazaarGameClient.Domain.Models.Cards;
using TheBazaar;

namespace BazaarPlusPlus.Android;

// Main-thread snapshot. Dictionaries retain first-seen order for recommendation candidates.
internal sealed record AndroidOwnedCards(IReadOnlyDictionary<Guid, int> Board, IReadOnlyDictionary<Guid, int> Stash,
    IReadOnlySet<Guid> Offered)
{
    internal static AndroidOwnedCards? Read()
    {
        var player = Data.Run?.Player;
        if (player == null) return null;
        var board = new Dictionary<Guid, int>();
        var stash = new Dictionary<Guid, int>();
        var offered = new HashSet<Guid>();
        var container = player.Hand?.Container;
        if (container != null)
        {
            foreach (var socket in container.GetCardsAndSockets())
            {
                var item = socket.Item1?.TryCast<ItemCard>();
                if (item == null) continue;
                var id = Guid.Parse(item.TemplateId.ToString());
                board[id] = board.GetValueOrDefault(id) + 1;
            }
        }
        var stashContainer = player.Stash?.Container;
        if (stashContainer != null)
        {
            foreach (var socket in stashContainer.GetCardsAndSockets())
            {
                var item = socket.Item1?.TryCast<ItemCard>();
                if (item == null) continue;
                var id = Guid.Parse(item.TemplateId.ToString());
                stash[id] = stash.GetValueOrDefault(id) + 1;
            }
        }
        var selectionSet = Data.CurrentState?.SelectionSet;
        if (selectionSet != null)
        {
            for (var index = 0; index < selectionSet.Count; index++)
            {
                var item = Data.GetCard(selectionSet[index])?.TryCast<ItemCard>();
                if (item != null) offered.Add(Guid.Parse(item.TemplateId.ToString()));
            }
        }
        return new AndroidOwnedCards(board, stash, offered);
    }
}
