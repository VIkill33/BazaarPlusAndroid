using BazaarGameShared.Domain.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;
using NativeCreation = Il2CppSystem.Threading.Tasks.Task<TheBazaar.UI.CardPreviewBase>;
using NativeTask = Il2CppSystem.Threading.Tasks.Task;

namespace BazaarPlusPlus.Android;

// Exports the native tier frame sprite that a synthetic item or skill preview loads.
// The preview has no canvas, so it is never drawn; only its frame sprite is copied.
internal sealed class AndroidHistoryFrameRender : IDisposable
{
    private const int MaxEdge = 512;
    private readonly TCardBase _template;
    private readonly ETier _tier;
    private readonly GameObject _root;
    private readonly Il2CppSystem.Threading.CancellationTokenSource _cancellation = new();
    private NativeCreation? _creation;
    private NativeTask? _setup;
    private CardPreviewBase? _card;

    internal AndroidHistoryFrameRender(TCardBase template, ETier tier)
    {
        _template = template; _tier = tier;
        _root = new GameObject("BPP.HistoryFrameExport", new Il2CppSystem.Type[] { Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>() });
        UnityEngine.Object.DontDestroyOnLoad(_root);
    }

    // Returns null until the native preview is ready; throws when it cannot be produced.
    internal byte[]? Step(AssetLoader loader)
    {
        if (_card == null && _creation == null)
            _creation = loader.GenerateUICard(_root.transform, _cancellation.Token, _template);
        if (_creation != null)
        {
            if (!_creation.IsCompleted) return null;
            var creation = _creation; _creation = null;
            if (creation.IsFaulted || creation.IsCanceled || creation.Result == null) throw new InvalidOperationException("Native frame preview unavailable");
            _card = creation.Result;
            _setup = _card.SetUp(_template, false, AndroidCardVariants.CreateInstance(_template, _tier, null), _cancellation.Token);
            return null;
        }
        if (_setup != null)
        {
            if (!_setup.IsCompleted) return null;
            var setup = _setup; _setup = null;
            if (setup.IsFaulted || setup.IsCanceled) throw new InvalidOperationException("Native frame setup unavailable");
        }
        if (_card!.Tier != _tier) throw new InvalidOperationException($"Native frame tier mismatch: {_card.Tier}");
        var image = _card._currentFrame?.GetComponent<Image>() ?? throw new InvalidOperationException("Native frame image missing");
        var sprite = image.sprite ?? throw new InvalidOperationException("Native frame sprite missing");
        if (image.type != Image.Type.Simple) throw new InvalidOperationException("Native frame is not a simple sprite: " + image.type);
        return CopyPng(sprite.texture, sprite.textureRect, image.color);
    }

    private static byte[] CopyPng(Texture source, Rect region, Color tint)
    {
        var scale = Math.Min(1f, MaxEdge / Math.Max(region.width, region.height));
        var width = Math.Max(1, (int)(region.width * scale)); var height = Math.Max(1, (int)(region.height * scale));
        var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active; Texture2D? copy = null;
        try
        {
            Graphics.Blit(source, temporary, new Vector2(region.width / source.width, region.height / source.height),
                new Vector2(region.x / source.width, region.y / source.height));
            RenderTexture.active = temporary;
            copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            if (tint != Color.white)
            {
                var pixels = copy.GetPixels();
                for (var i = 0; i < pixels.Length; i++) pixels[i] *= tint;
                copy.SetPixels(pixels);
            }
            copy.Apply();
            return ImageConversion.EncodeToPNG(copy).ToArray();
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
            if (copy != null) UnityEngine.Object.Destroy(copy);
        }
    }

    public void Dispose() => NativePreviewCleanup.Retire(_creation, _setup, _card, _cancellation, _root);
}
