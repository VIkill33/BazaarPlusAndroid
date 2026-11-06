using TheBazaar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Copies the look of the native HealthBarController bar (Bars + HPNumber) without its
// behaviours. Clones are stripped under an inactive parent so no native Awake/OnEnable
// runs, and the health shader gets its own material so the player's bar is never touched.
internal sealed class AndroidMonsterHealthBar
{
    private const float NativeBarWidth = 1000;
    private const float NativeBarHeight = 50;
    private const float NativeEndHeight = 62;
    private const float NativeEndOverhang = 44.5f;
    private static readonly Color HealthColor = new(.126f, .868f, .139f, 1);
    private static readonly HashSet<string> VisualParts =
        new() { "Back", "HealthBar", "BarOverlay", "BarGloss", "BarEnd_Left", "BarEnd_Right" };

    private readonly RectTransform _host;
    private readonly TMP_FontAsset _font;
    private GameObject? _bar;
    private Material? _material;

    internal AndroidMonsterHealthBar(RectTransform host, TMP_FontAsset font)
    {
        _host = host;
        _font = font;
    }

    internal void Show(string health, float width, float height)
    {
        Hide();
        var source = FindSource();
        var native = false;
        try
        {
            native = source != null && TryClone(source, health, width, height);
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.monster_health_bar.clone_failed exception={error}");
            Hide();
        }
        if (!native) BuildFallback(health, width, height);
        Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.monster_health_bar.rendered source={(native ? "native" : "fallback")} health={health} width={width:0.#}");
    }

    internal void Hide()
    {
        if (_bar != null) UnityEngine.Object.Destroy(_bar);
        if (_material != null) UnityEngine.Object.Destroy(_material);
        _bar = null;
        _material = null;
    }

    private static HealthBarController? FindSource()
    {
        HealthBarController? template = null;
        foreach (var controller in Resources.FindObjectsOfTypeAll<HealthBarController>())
        {
            if (controller == null || controller.BarParent == null || controller.HealthNumber == null) continue;
            if (controller.gameObject.activeInHierarchy) return controller;
            template ??= controller;
        }
        return template;
    }

    private bool TryClone(HealthBarController source, string health, float width, float height)
    {
        var sourceHealth = source.BarParent.Find("HealthBar")?.GetComponent<Image>();
        if (sourceHealth?.material == null) return false;
        var staging = new GameObject("BPP.MonsterHealthStaging");
        staging.SetActive(false);
        staging.transform.SetParent(_host, false);
        try
        {
            var bar = UnityEngine.Object.Instantiate(source.BarParent.gameObject, staging.transform, false)
                .GetComponent<RectTransform>();
            bar.name = "BPP.MonsterHealthBar";
            for (var index = bar.childCount - 1; index >= 0; index--)
            {
                var child = bar.GetChild(index);
                if (!VisualParts.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var number = UnityEngine.Object.Instantiate(source.HealthNumber.gameObject, bar, false)
                .GetComponent<TMP_Text>();
            StripBehaviours(bar);
            var scale = height / NativeEndHeight;
            var innerWidth = Mathf.Max(width / scale - 2 * NativeEndOverhang, NativeBarHeight);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(.5f, .5f);
            bar.anchoredPosition = Vector2.zero;
            bar.localScale = new Vector3(scale, scale, 1);
            bar.sizeDelta = new Vector2(innerWidth, NativeBarHeight);
            for (var index = 0; index < bar.childCount; index++)
            {
                var child = bar.GetChild(index).TryCast<RectTransform>();
                // Centre-anchored layers are authored against the 1000-unit native bar.
                if (child == null || child.anchorMin.x != .5f || child.anchorMax.x != .5f) continue;
                child.sizeDelta = new Vector2(child.sizeDelta.x - NativeBarWidth + innerWidth, child.sizeDelta.y);
            }
            foreach (var graphic in bar.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var fill = bar.Find("HealthBar").GetComponent<Image>();
            _material = new Material(sourceHealth.material);
            SetFloat(_material, "_HealthAmount", 1);
            SetFloat(_material, "_DamageAmount", 1);
            SetFloat(_material, "_FireAmount", 0);
            SetFloat(_material, "_PoisonAmount", 0);
            fill.material = _material;
            fill.color = HealthColor;
            var numberRect = number.rectTransform;
            numberRect.anchorMin = Vector2.zero;
            numberRect.anchorMax = Vector2.one;
            numberRect.pivot = new Vector2(.5f, .5f);
            numberRect.offsetMin = numberRect.offsetMax = Vector2.zero;
            numberRect.localScale = Vector3.one;
            number.enableAutoSizing = false;
            number.fontSize = source.HealthNumber.fontSize;
            number.alignment = TextAlignmentOptions.Center;
            number.text = health;
            number.gameObject.SetActive(true);
            bar.SetParent(_host, false);
            bar.gameObject.SetActive(true);
            _bar = bar.gameObject;
            return true;
        }
        finally
        {
            UnityEngine.Object.Destroy(staging);
        }
    }

    private static void StripBehaviours(Transform root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.TryCast<Transform>() != null
                || component.TryCast<CanvasRenderer>() != null || component.TryCast<Graphic>() != null) continue;
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void SetFloat(Material material, string name, float value)
    {
        if (material.HasProperty(name)) material.SetFloat(name, value);
    }

    private void BuildFallback(string health, float width, float height)
    {
        var frame = NativeUi.Rect("BPP.MonsterHealthBar", _host, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        frame.sizeDelta = new Vector2(width, height * .8f);
        var back = frame.gameObject.AddComponent<Image>();
        back.color = new Color(0, 0, 0, .55f);
        back.raycastTarget = false;
        var fill = NativeUi.Rect("BPP.MonsterHealthFill", frame, Vector2.zero, Vector2.one,
            new Vector2(3, 3), new Vector2(-3, -3)).gameObject.AddComponent<Image>();
        fill.color = HealthColor;
        fill.raycastTarget = false;
        var label = NativeUi.Label(frame, _font, health, height * .6f);
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.outlineWidth = .2f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        _bar = frame.gameObject;
    }
}
