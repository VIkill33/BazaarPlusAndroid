using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BazaarPlusPlus.Android;

internal static class Program
{
    private static readonly ManualResetEventSlim Entered = new();
    private static readonly ManualResetEventSlim Release = new();
    private static readonly ConcurrentQueue<string> Payloads = new();
    private sealed record Group(string Status, object[] Cards);
    public static async Task Main(string[] args)
    {
        if (args.Contains("--reader")) { await ReadSelection(); return; }
        SetEntry();
        var first = AndroidHistoryBridge.WriteAsync(new { order = 1, name = "中文😀", group = new Group("captured", []) });
        if (!Entered.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Native boundary not entered");
        var second = AndroidHistoryBridge.WriteAsync(new { order = 2 });
        var third = AndroidHistoryBridge.WriteAsync(new { order = 3, fail = true });
        var fourth = AndroidHistoryBridge.WriteAsync(new { order = 4 });
        var overflow = AndroidHistoryBridge.WriteAsync(new { order = 5 });
        try { await overflow; throw new Exception("Overflow falsely accepted"); }
        catch (InvalidOperationException error) when (error.Message.Contains("队列已满")) { }
        Release.Set();
        await first; await second;
        try { await third; throw new Exception("Native failure hidden"); }
        catch (InvalidOperationException error) when (error.Message.Contains("injected")) { }
        try { await fourth; throw new Exception("Dependent write ran after its predecessor failed"); }
        catch (InvalidOperationException error) when (error.Message.Contains("injected")) { }
        AndroidHistoryBridge.ResumeAfterFailure();
        await AndroidHistoryBridge.WriteAsync(new { order = 4 });
        await AndroidHistoryBridge.WriteAsync(new { order = 6 });
        var rows = Payloads.Select(row => JsonDocument.Parse(row)).ToArray();
        if (!rows.Select(row => row.RootElement.GetProperty("order").GetInt32()).SequenceEqual(new[] {1,2,3,4,6}))
            throw new Exception("Writer reordered or dropped an accepted request");
        var opening = rows[0].RootElement;
        if (opening.GetProperty("name").GetString() != "中文😀"
            || opening.GetProperty("group").GetProperty("status").GetString() != "captured"
            || opening.GetProperty("group").GetProperty("cards").GetArrayLength() != 0)
            throw new Exception("DTO casing or UTF-8 contract mismatch");
        foreach (var row in rows) row.Dispose();
        Console.WriteLine("History writer ordering, capacity rejection, recovery after failure, DTO casing and Unicode passed.");
    }
    private static async Task ReadSelection()
    {
        SetReader();
        var first=AndroidHistoryBridge.ReadAsync(new {order=1});
        if(!Entered.Wait(TimeSpan.FromSeconds(5)))throw new Exception("Reader did not enter native boundary");
        var replaced=AndroidHistoryBridge.ReadAsync(new {order=2});
        var selected=AndroidHistoryBridge.ReadAsync(new {order=3});
        if(!replaced.IsCanceled)throw new Exception("Superseded waiting selection was retained");
        Release.Set(); await first;
        var result=await selected;
        if(result.GetProperty("order").GetInt32()!=3 || result.GetProperty("name").GetString()!="历史😀"
            || !Payloads.SequenceEqual(new[]{"1","3"}))throw new Exception("Reader selection or Unicode contract mismatch");
        Console.WriteLine("History reader serial execution, replaced pending selection and Unicode passed.");
    }
    private static unsafe void SetReader()
    {
        delegate* unmanaged[Cdecl]<byte*,byte*,int,int> pointer=&NativeReader;
        Environment.SetEnvironmentVariable("BPP_HISTORY_BRIDGE_ABI","1");
        Environment.SetEnvironmentVariable("BPP_HISTORY_READ_ADDRESS",((nuint)pointer).ToString("x"));
    }
    [UnmanagedCallersOnly(CallConvs=new[]{typeof(CallConvCdecl)})]
    private static unsafe int NativeReader(byte* request,byte* response,int capacity)
    {
        using var document=JsonDocument.Parse(Marshal.PtrToStringUTF8((IntPtr)request)!);
        var order=document.RootElement.GetProperty("order").GetInt32(); Payloads.Enqueue(order.ToString());
        Entered.Set(); if(!Release.Wait(TimeSpan.FromSeconds(10)))return -1;
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new {state="ok",order,name="历史😀"});
        if(bytes.Length>=capacity)return 2;
        for(var index=0;index<bytes.Length;index++)response[index]=bytes[index];response[bytes.Length]=0;return 0;
    }
    private static unsafe void SetEntry()
    {
        delegate* unmanaged[Cdecl]<byte*, byte*, int, int> pointer = &Native;
        Environment.SetEnvironmentVariable("BPP_HISTORY_BRIDGE_ABI", "1");
        Environment.SetEnvironmentVariable("BPP_HISTORY_WRITE_ADDRESS", ((nuint)pointer).ToString("x"));
    }
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe int Native(byte* request, byte* response, int capacity)
    {
        var json = Marshal.PtrToStringUTF8((IntPtr)request)!;
        Payloads.Enqueue(json);
        Entered.Set();
        if (!Release.Wait(TimeSpan.FromSeconds(10))) return -1;
        using var document = JsonDocument.Parse(json);
        var fail = document.RootElement.TryGetProperty("fail", out var value) && value.GetBoolean();
        var bytes = Encoding.UTF8.GetBytes(fail ? "injected" : "{\"state\":\"recorded\"}");
        if (bytes.Length >= capacity) return 2;
        for (var i = 0; i < bytes.Length; i++) response[i] = bytes[i];
        response[bytes.Length] = 0;
        return fail ? 1 : 0;
    }
}
namespace BazaarPlusPlus.Android
{
    internal static class Plugin { internal static LogStub? PluginLog => null; }
    internal sealed class LogStub { internal void LogWarning(string message) { } }
}
