namespace BazaarPlusPlus.Android;

internal static class HistoryLimits
{
    internal const int QueueCapacity = 4;
    internal const int MaxPayloadBytes = 262144;
    internal const int ResponseCapacity = 262144;
    internal const int MaxCardsPerGroup = 128;
    internal const int MaxAttributesPerCard = 128;
    internal const double CaptureTargetMs = 33;
    internal const double CaptureAbortMs = 50;
    internal const double WriteTargetMs = 500;
    internal const double AcceptedTargetMs = 2000;
}
