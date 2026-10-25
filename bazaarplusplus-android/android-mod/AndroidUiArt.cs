using BazaarGameShared.Domain.Core.Types;
using BazaarPlusPlus.GameInterop.Heroes;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.UI;
using NativeTask = Il2CppSystem.Threading.Tasks.Task<UnityEngine.Sprite>;

namespace BazaarPlusPlus.Android;

// Native tasks are polled on Unity's thread. Bindings belong only to the latest render.
internal sealed class AndroidUiArt
{
    private sealed class Load
    {
        internal NativeTask? Task;
        internal Il2CppSystem.Threading.Tasks.Task<Texture2D>? Texture;
        internal Sprite? Sprite;
        internal float RetryAt;
        internal bool Reported;
    }
    private sealed record Binding(Image Image, TMPro.TextMeshProUGUI? Fallback, string Key);
    private readonly Dictionary<string, Load> _loads = new();
    private readonly List<Binding> _bindings = new();
    private readonly List<Sprite> _owned = new();
    private Texture2D? _stateAtlas;
    internal void Clear() => _bindings.Clear();
    internal void Bind(Image image, string? key, TMPro.TextMeshProUGUI? fallback = null)
    {
        image.sprite = null; image.enabled = false;
        if (fallback != null) fallback.gameObject.SetActive(true);
        if (key == null) return;
        _bindings.Add(new(image, fallback, key));
    }
    internal static string? Hero(string hero) => TheDragonsHeroIdentity.TryResolve(hero, out var parsed)
        && parsed != EHero.Common && Enum.IsDefined(parsed) ? "hero:" + parsed : null;
    internal void Tick()
    {
        if (!Services.TryGet<AssetLoader>(out var loader) || loader == null) return;
        foreach (var binding in _bindings)
        {
            if (binding.Image == null) continue;
            if (!_loads.TryGetValue(binding.Key, out var load)) _loads.Add(binding.Key, load = new());
            try
            {
                if (load.Sprite == null && load.Task == null && load.Texture == null && Time.realtimeSinceStartup >= load.RetryAt)
                {
                    if (binding.Key.StartsWith("state:", StringComparison.Ordinal))
                    {
                        LoadStates(); load.Sprite = _loads[binding.Key].Sprite;
                    }
                    else if (binding.Key.StartsWith("hero:", StringComparison.Ordinal))
                    {
                        if (!Services.TryGet<CollectionManager>(out var collection) || collection == null) continue;
                        var skin = collection.GetDefaultHeroSkin(Enum.Parse<EHero>(binding.Key[5..]));
                        if (skin == null) { load.RetryAt = Time.realtimeSinceStartup + 10; continue; }
                        load.Task = loader.LoadAssetAsyncByReference<Sprite>(skin.portraitTextureReference,
                            NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                    }
                    else load.Task = loader.LoadAssetAsyncByAddress<Sprite>(binding.Key, NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                }
                if (load.Task is { IsCompleted: true } task)
                {
                    load.Sprite = !task.IsFaulted && !task.IsCanceled ? task.Result : null;
                    load.Task = null;
                    if (load.Sprite == null && binding.Key.StartsWith("hero:", StringComparison.Ordinal)
                        && Services.TryGet<CollectionManager>(out var collection) && collection != null)
                    {
                        var skin = collection.GetDefaultHeroSkin(Enum.Parse<EHero>(binding.Key[5..]));
                        if (skin != null) load.Texture = loader.LoadAssetAsyncByReference<Texture2D>(skin.storePortraitTextureReference,
                            NativeNullableValue.Box<AssetScope>(AssetScope.Global));
                    }
                    if (load.Sprite == null) load.RetryAt = Time.realtimeSinceStartup + 10;
                }
                if (load.Texture is { IsCompleted: true } textureTask)
                {
                    var texture = !textureTask.IsFaulted && !textureTask.IsCanceled ? textureTask.Result : null;
                    load.Texture = null;
                    if (texture != null)
                    {
                        load.Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f,.5f));
                        _owned.Add(load.Sprite);
                    }
                    else load.RetryAt = Time.realtimeSinceStartup + 10;
                }
                if (load.Sprite == null) continue;
                binding.Image.sprite = load.Sprite; binding.Image.enabled = true;
                if (binding.Fallback != null) binding.Fallback.gameObject.SetActive(false);
                if (!load.Reported)
                {
                    load.Reported = true;
                    Plugin.PluginLog?.LogInfo($"[BPP][Android] event=ui.art.ready key={binding.Key}");
                }
            }
            catch (Exception error)
            {
                load.Task = null; load.Texture = null; load.RetryAt = Time.realtimeSinceStartup + 10;
                Plugin.PluginLog?.LogWarning($"[BPP][Android] event=ui.art.failed key={binding.Key} exception={error}");
            }
        }
    }
    private void LoadStates()
    {
        if (_stateAtlas != null) return;
        using var stream = typeof(AndroidUiArt).Assembly.GetManifestResourceStream("BazaarPlusPlus.Android.History.run-state-badges.png")
            ?? throw new InvalidOperationException("Missing history state artwork");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!ImageConversion.LoadImage(texture, bytes.ToArray()))
        {
            UnityEngine.Object.Destroy(texture); throw new InvalidOperationException("Cannot decode history state artwork");
        }
        _stateAtlas = texture;
        var keys = new[] { "state:broken", "state:flag", "state:pending" };
        for (var index=0; index<keys.Length; index++)
        {
            if (!_loads.TryGetValue(keys[index],out var state)) _loads.Add(keys[index],state=new());
            state.Sprite = Sprite.Create(texture,new Rect(index*128,0,128,128),new Vector2(.5f,.5f));
            _owned.Add(state.Sprite);
        }
    }
    internal void Dispose()
    {
        Clear(); _loads.Clear();
        foreach (var sprite in _owned) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        _owned.Clear();
        if (_stateAtlas != null) UnityEngine.Object.Destroy(_stateAtlas);
        _stateAtlas = null;
    }
}
