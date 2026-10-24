using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace BazaarPlusPlus.Android;

// No Unity/IL2CPP API is invoked by the storage worker. The launcher owns JVM attachment.
internal static class AndroidHistoryBridge
{
    private const int ResponseCapacity = 8192;
    private static readonly SemaphoreSlim Capacity = new(4, 4);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VerifyCall([MarshalAs(UnmanagedType.LPUTF8Str)] string requestId,
        [Out] byte[] response, int capacity);
    private static readonly Lazy<VerifyCall> Verify = new(() =>
    {
        var entry = Environment.GetEnvironmentVariable("BPP_HISTORY_VERIFY_ADDRESS");
        if (Environment.GetEnvironmentVariable("BPP_HISTORY_BRIDGE_ABI") != "1"
            || !ulong.TryParse(entry, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
            || address == 0)
            throw new InvalidOperationException("History native host is unavailable");
        // This in-process entry is published before CoreCLR starts; it is never persisted or imported.
        return Marshal.GetDelegateForFunctionPointer<VerifyCall>(new IntPtr(unchecked((long)address)));
    });

    internal static async Task<string> VerifyEnvironmentAsync(string requestId)
    {
        if (requestId.Length > 128 || requestId.Any(character => character > 127))
            throw new ArgumentException("Environment check request must be a bounded ASCII identifier", nameof(requestId));
        if (!Capacity.Wait(0)) throw new InvalidOperationException("History bridge is busy");
        try
        {
            return await Task.Run(() =>
            {
                var response = new byte[ResponseCapacity];
                var status = Verify.Value(requestId, response, response.Length);
                var end = Array.IndexOf(response, (byte)0);
                if (end < 0) throw new InvalidOperationException("Invalid history bridge response");
                var message = Encoding.UTF8.GetString(response, 0, end);
                if (status != 0) throw new InvalidOperationException($"History bridge failed ({status}): {message}");
                return message;
            }).ConfigureAwait(false);
        }
        finally { Capacity.Release(); }
    }
}
