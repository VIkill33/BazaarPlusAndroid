using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using BepInEx;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Assets.Scripts.ScriptableObjectsScripts;
using UnityEngine;

namespace BazaarPlusPlus.Android;

// Exports template illustrations, never player portraits, names or live card instances.
// Native tasks and GPU readback stay on Unity's thread; disk writes receive managed bytes only.
public sealed class AndroidHistoryArtwork(IntPtr pointer) : MonoBehaviour(pointer)
{
    private readonly Queue<string> _queue = new();
    private readonly HashSet<string> _seen = new();
    private Task<JsonElement>? _query;
    private Task? _write;
    private Il2CppSystem.Threading.Tasks.Task<UnityEngine.Object>? _asset;
    private Il2CppSystem.Threading.Tasks.Task<Texture2D>? _texture;
    private Il2CppSystem.Threading.Tasks.Task<Sprite>? _sprite;
    private AndroidHistoryFrameRender? _frame;
    private readonly Dictionary<ECardSize, TCardBase> _frameTemplates = new();
    private string? _key, _directory, _scope, _writingKey;
    private readonly Dictionary<string, (int Attempts, float At)> _retry = new();
    private float _lastTick;
    private Texture2D? _stateAtlas;
    private Texture? _directTexture;
    private Rect? _directRegion;
    private EHero? _portraitHero;
    private long _after;
    private float _nextQuery, _deadline, _nextFrame;
    private void Enqueue(string key)
    {
        if (_seen.Add(key) && !File.Exists(Path.Combine(_directory!, key + ".png"))) _queue.Enqueue(key);
    }
    private void Retry(string? key)
    {
        if (key == null) return;
        var attempts = _retry.TryGetValue(key, out var previous) ? previous.Attempts + 1 : 1;
        _retry[key] = (attempts, attempts <= 3 ? Time.realtimeSinceStartup + 30 : float.PositiveInfinity);
    }
    public void Update()
    {
        var now = Time.realtimeSinceStartup;
        var elapsed = now - _lastTick; _lastTick = now;
        if (AndroidHistoryPanel.AnyOpen || AndroidRunObserver.CurrentPhase() is HistoryPhase.Combat or HistoryPhase.PvpCombat or HistoryPhase.Replay) { _deadline += elapsed; return; }
        if (Time.realtimeSinceStartup < _nextFrame || !Data.IsManagerCreated()
            || !Services.TryGet<AssetLoader>(out var loader) || loader == null
            || !Services.TryGet<CollectionManager>(out var collection) || collection == null) return;
        _nextFrame = Time.realtimeSinceStartup + .1f;
        try
        {
            if (_directory == null)
            {
                var root = Path.Combine(Paths.BepInExRootPath, "history-art", "v2");
                var version = Application.version;
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(version))).ToLowerInvariant();
                var directory = Path.Combine(root, hash);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "source.json"), JsonSerializer.Serialize(new { gameVersion = version, schema = 2 }));
                File.WriteAllText(Path.Combine(root, "current.part"), hash);
                File.Move(Path.Combine(root, "current.part"), Path.Combine(root, "current.txt"), true);
                _directory = directory;
                foreach (var hero in Enum.GetValues<EHero>()) if (hero != EHero.Common) Enqueue("hero-" + (hero.ToString() == "Hero8" ? "TheDragons" : hero.ToString()));
                foreach (var state in new[] { "broken", "flag", "pending" }) Enqueue("state-" + state);
                foreach (var trophy in new[] { "Bronze", "Silver", "Gold", "Diamond" }) Enqueue("Trophy_" + trophy + "_TUI");
            }
            if (_write != null)
            {
                if (!_write.IsCompleted) return;
                if (_write.IsFaulted) { Retry(_writingKey); Plugin.PluginLog?.LogWarning("[BPP][Android] event=history.art.write_failed"); }
                else if (_writingKey != null) _retry.Remove(_writingKey);
                _writingKey = null;
                _write = null;
            }
            if (_key != null)
            {
                if (Time.realtimeSinceStartup > _deadline) throw new TimeoutException("Artwork load timed out");
                if (_frame != null)
                {
                    var frame = _frame.Step(loader);
                    if (frame == null) return;
                    _frame.Dispose(); _frame = null;
                    Save(_key, frame);
                    return;
                }
                if (_asset is { IsCompleted: true } asset)
                {
                    _asset = null;
                    if (asset.IsFaulted || asset.IsCanceled || asset.Result == null) throw new InvalidOperationException("No template illustration");
                    var cardData = asset.Result.TryCast<CardAssetDataSO>();
                    if (cardData != null)
                    {
                        _directTexture = cardData.cardMaterial?.mainTexture;
                        if (_directTexture == null) throw new InvalidOperationException("Card material has no main texture");
                    }
                    else
                    {
                        var loadedTexture = asset.Result.TryCast<Texture2D>();
                        if (loadedTexture != null) _directTexture = loadedTexture;
                        else if (asset.Result.TryCast<Material>() is { } material) _directTexture = material.mainTexture;
                        else if (asset.Result.TryCast<Sprite>() is { } illustration)
                        { _directTexture = illustration.texture; _directRegion = illustration.textureRect; }
                        else throw new InvalidOperationException("Unsupported illustration asset: " + asset.Result.GetIl2CppType().FullName);
                    }
                }
                Texture? texture = _directTexture; Rect? region = _directRegion;
                if (_key.StartsWith("state-", StringComparison.Ordinal))
                {
                    if (_stateAtlas == null)
                    {
                        using var resource = typeof(AndroidHistoryArtwork).Assembly.GetManifestResourceStream("BazaarPlusPlus.Android.History.run-state-badges.png")!;
                        using var buffer = new MemoryStream(); resource.CopyTo(buffer);
                        _stateAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(_stateAtlas, buffer.ToArray())) throw new InvalidOperationException("State atlas unavailable");
                    }
                    texture = _stateAtlas;
                    region = new Rect((_key == "state-broken" ? 0 : _key == "state-flag" ? 1 : 2) * 128, 0, 128, 128);
                }
                if (_texture is { IsCompleted: true } pending)
                {
                    if (pending.IsFaulted || pending.IsCanceled) throw new InvalidOperationException("No illustration texture");
                    texture = pending.Result;
                }
                if (_sprite is { IsCompleted: true } sprite)
                {
                    if (sprite.IsFaulted || sprite.IsCanceled || sprite.Result == null)
                    {
                        if (_portraitHero is { } hero)
                        {
                            _portraitHero = null; _sprite = null;
                            _texture = loader.LoadAssetAsyncByReference<Texture2D>(collection.GetDefaultHeroSkin(hero).storePortraitTextureReference,
                                NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                            return;
                        }
                        throw new InvalidOperationException("No portrait sprite");
                    }
                    texture = sprite.Result.texture; region = sprite.Result.textureRect;
                }
                if (texture == null) return;
                Save(_key, CopyPng(texture, region, _key.StartsWith("card-", StringComparison.Ordinal)));
                return;
            }
            foreach (var key in _retry.Keys.ToArray())
            {
                var retry = _retry[key];
                if (now < retry.At) continue;
                _retry[key] = (retry.Attempts, float.PositiveInfinity); _queue.Enqueue(key);
            }
            if (_queue.TryDequeue(out var next))
            {
                _key = next; _portraitHero = null; _deadline = Time.realtimeSinceStartup + 15;
                if (next.StartsWith("card-", StringComparison.Ordinal))
                {
                    var template = Data.GetStatic().GetCardById(new Il2CppSystem.Guid(next[5..])).TryCast<TCardBase>();
                    if (template == null || string.IsNullOrWhiteSpace(template.ArtKey)) throw new InvalidOperationException("Template no longer available");
                    _asset = loader.LoadAssetAsyncByAddress<UnityEngine.Object>(template.ArtKey, NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                }
                else if (next.StartsWith("frame-", StringComparison.Ordinal))
                {
                    var parts = next.Split('-');
                    _frame = new AndroidHistoryFrameRender(_frameTemplates[Enum.Parse<ECardSize>(parts[1])], Enum.Parse<ETier>(parts[2]));
                }
                else if (next.StartsWith("state-", StringComparison.Ordinal)) return;
                else if (next.StartsWith("hero-", StringComparison.Ordinal))
                {
                    _portraitHero = Enum.Parse<EHero>(next[5..] == "TheDragons" ? "Hero8" : next[5..]);
                    var skin = collection.GetDefaultHeroSkin(_portraitHero.Value);
                    if (skin == null) throw new InvalidOperationException("Hero skin unavailable");
                    _sprite = loader.LoadAssetAsyncByReference<Sprite>(skin.portraitTextureReference, NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                }
                else _sprite = loader.LoadAssetAsyncByAddress<Sprite>(next, NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                return;
            }
            var scope = AndroidRunObserver.Instance?.Session.Scope;
            if (scope == null) return;
            var identity = scope.SessionId + ":" + scope.AccountId;
            if (_scope != identity) { _scope = identity; _after = 0; _query = null; _nextQuery = 0; }
            if (_query is { IsCompleted: true } query)
            {
                _query = null; _nextQuery = Time.realtimeSinceStartup + 5;
                if (!query.IsFaulted && !query.IsCanceled && query.Result.GetProperty("state").GetString() == "ok")
                {
                    foreach (var id in query.Result.GetProperty("items").EnumerateArray())
                    {
                        if (!Guid.TryParse(id.GetString(), out var guid)) continue;
                        Enqueue("card-" + guid.ToString("D"));
                        var template = Data.GetStatic().GetCardById(new Il2CppSystem.Guid(guid.ToString("D")))?.TryCast<TCardBase>();
                        if (template is { Type: ECardType.Item } && _frameTemplates.TryAdd(template.Size, template))
                            foreach (var tier in Enum.GetValues<ETier>()) Enqueue($"frame-{template.Size}-{tier}");
                    }
                    _after = query.Result.GetProperty("afterRow").GetInt64();
                    if (!query.Result.GetProperty("hasMore").GetBoolean()) { _after = 0; _nextQuery = Time.realtimeSinceStartup + 60; }
                }
            }
            if (_query == null && Time.realtimeSinceStartup >= _nextQuery)
                _query = AndroidHistoryBridge.ReadAsync(new { op = "artwork", sessionId = scope.SessionId, accountId = scope.AccountId, afterRow = _after });
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=history.art.unavailable key={_key} reason={error.Message}");
            if (_directory == null) _nextFrame = Time.realtimeSinceStartup + 30;
            Retry(_key);
            _frame?.Dispose(); _frame = null;
            _key = null; _asset = null; _texture = null; _sprite = null; _directTexture = null; _directRegion = null; _nextQuery = Time.realtimeSinceStartup + 30;
        }
    }
    private void Save(string key, byte[] bytes)
    {
        var path = Path.Combine(_directory!, key + ".png");
        _writingKey = key;
        _key = null; _texture = null; _sprite = null; _directTexture = null; _directRegion = null;
        _write = Task.Run(() =>
        {
            try { File.WriteAllBytes(path + ".part", bytes); File.Move(path + ".part", path, true); }
            finally { if (File.Exists(path + ".part")) File.Delete(path + ".part"); }
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=history.art.saved key={key}");
        });
    }
    public void OnDestroy() { _frame?.Dispose(); if (_stateAtlas != null) UnityEngine.Object.Destroy(_stateAtlas); }
    private static byte[] CopyPng(Texture source, Rect? crop, bool opaque)
    {
        var region = crop ?? new Rect(0, 0, source.width, source.height);
        var scale = Math.Min(1f, 384f / Math.Max(region.width, region.height));
        var width = Math.Max(1, (int)(region.width * scale)); var height = Math.Max(1, (int)(region.height * scale));
        var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active; Texture2D? copy = null;
        try
        {
            Graphics.Blit(source, temporary, new Vector2(region.width / source.width, region.height / source.height),
                new Vector2(region.x / source.width, region.y / source.height));
            RenderTexture.active = temporary;
            // Card artwork alpha is a native shader mask, not image transparency.
            copy = new Texture2D(width, height, opaque ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0); copy.Apply();
            return ImageConversion.EncodeToPNG(copy).ToArray();
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
            if (copy != null) UnityEngine.Object.Destroy(copy);
        }
    }
}
