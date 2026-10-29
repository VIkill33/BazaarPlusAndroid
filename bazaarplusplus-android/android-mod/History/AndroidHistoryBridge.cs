using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace BazaarPlusPlus.Android;

// No Unity/IL2CPP API is invoked by the storage worker. The launcher owns JVM attachment.
internal static class AndroidHistoryBridge
{
    private const int ResponseCapacity = 8192;
    private static readonly SemaphoreSlim Capacity = new(HistoryLimits.QueueCapacity, HistoryLimits.QueueCapacity);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VerifyCall([MarshalAs(UnmanagedType.LPUTF8Str)] string requestId,
        [Out] byte[] response, int capacity);
    private static readonly Lazy<VerifyCall> Verify = new(() => Entry("BPP_HISTORY_VERIFY_ADDRESS"));
    private static readonly Lazy<VerifyCall> Writer = new(() => Entry("BPP_HISTORY_WRITE_ADDRESS"));
    private static readonly Lazy<VerifyCall> Reader = new(() => Entry("BPP_HISTORY_READ_ADDRESS"));
    private sealed record ReadRequest(object Dto, TaskCompletionSource<System.Text.Json.JsonElement> Completion);
    private static readonly object ReadGate = new();
    private static ReadRequest? _pendingRead;
    private static bool _reading;
    private static readonly object QueueGate = new();
    private static Task _tail = Task.CompletedTask;
    private static int _queued;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    private static VerifyCall Entry(string variable)
    {
        var entry = Environment.GetEnvironmentVariable(variable);
        if (Environment.GetEnvironmentVariable("BPP_HISTORY_BRIDGE_ABI") != "1"
            || !ulong.TryParse(entry, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
            || address == 0)
            throw new InvalidOperationException("History native host is unavailable");
        return Marshal.GetDelegateForFunctionPointer<VerifyCall>(new IntPtr(unchecked((long)address)));
    }

    // One running query and one replaceable waiting query. This queue never depends on writes.
    internal static Task<System.Text.Json.JsonElement> ReadAsync(object dto)
    {
        var completion = new TaskCompletionSource<System.Text.Json.JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (ReadGate)
        {
            _pendingRead?.Completion.TrySetCanceled();
            _pendingRead = new(dto, completion);
            if (!_reading) { _reading = true; _ = Task.Run(ReadLoop); }
        }
        return completion.Task;
    }

    private static void ReadLoop()
    {
        while (true)
        {
            ReadRequest request;
            lock (ReadGate)
            {
                if (_pendingRead == null) { _reading = false; return; }
                request = _pendingRead; _pendingRead = null;
            }
            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(request.Dto, JsonOptions);
                if (Encoding.UTF8.GetByteCount(payload) > HistoryLimits.ReadRequestBytes)
                    throw new InvalidOperationException("历史查询超出限制");
                var response = new byte[HistoryLimits.ReadResponseCapacity];
                var status = Reader.Value(payload, response, response.Length);
                var end = Array.IndexOf(response, (byte)0);
                if (end < 0) throw new InvalidOperationException("Invalid history read response");
                var message = Encoding.UTF8.GetString(response, 0, end);
                if (status != 0) throw new InvalidOperationException($"History reader failed ({status}): {message}");
                using var document = System.Text.Json.JsonDocument.Parse(message);
                request.Completion.TrySetResult(document.RootElement.Clone());
            }
            catch (Exception error) { request.Completion.TrySetException(error); }
        }
    }

    internal static void ResumeAfterFailure()
    {
        lock (QueueGate)
        {
            if (_queued != 0) throw new InvalidOperationException("历史写入仍在完成，请稍后重试");
            _tail = Task.CompletedTask;
        }
    }

    // Immutable managed DTOs only. Serialization and JNI execute after the preceding write.
    internal static Task<System.Text.Json.JsonElement> WriteAsync(object dto)
    {
        lock (QueueGate)
        {
            if (_queued >= HistoryLimits.QueueCapacity)
                return Task.FromException<System.Text.Json.JsonElement>(new InvalidOperationException("历史写入队列已满，请重试"));
            _queued++;
            var preceding = _tail;
            var accepted = System.Diagnostics.Stopwatch.StartNew();
            var task = Task.Run(async () =>
            {
                try
                {
                    await preceding.ConfigureAwait(false); // Stop dependent writes after the first failed transaction.
                    var payload = System.Text.Json.JsonSerializer.Serialize(dto, JsonOptions);
                    if (Encoding.UTF8.GetByteCount(payload) > HistoryLimits.MaxPayloadBytes)
                        throw new InvalidOperationException("历史写入数据超出限制");
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var response = new byte[HistoryLimits.ResponseCapacity];
                    var status = Writer.Value(payload, response, response.Length);
                    var end = Array.IndexOf(response, (byte)0);
                    if (end < 0) throw new InvalidOperationException("Invalid history bridge response");
                    var message = Encoding.UTF8.GetString(response, 0, end);
                    if (status != 0) throw new InvalidOperationException($"History bridge failed ({status}): {message}");
                    if (timer.Elapsed.TotalMilliseconds > HistoryLimits.WriteTargetMs || accepted.Elapsed.TotalMilliseconds > HistoryLimits.AcceptedTargetMs)
                        Plugin.PluginLog?.LogWarning($"[BPP][Android] event=history.write.slow writeMs={timer.Elapsed.TotalMilliseconds:F1} acceptedMs={accepted.Elapsed.TotalMilliseconds:F1}");
                    using var document = System.Text.Json.JsonDocument.Parse(message);
                    return document.RootElement.Clone();
                }
                finally { lock (QueueGate) _queued--; }
            });
            _tail = task;
            return task;
        }
    }

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
