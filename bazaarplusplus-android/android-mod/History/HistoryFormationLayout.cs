namespace BazaarPlusPlus.Android;

internal static class HistoryFormationLayout
{
    // Android EContainerSocketId names use zero-based Socket_0 / OpponentSocket_0.
    internal static int ItemSlot(string socket, int fallback) =>
        int.TryParse(socket[(socket.LastIndexOf('_') + 1)..], out var slot) && slot is >= 0 and < 128
            ? slot : fallback;
}
