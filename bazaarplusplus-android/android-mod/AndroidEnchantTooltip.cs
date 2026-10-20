using System.Reflection;
using BazaarGameClient.Domain.Models.Cards;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

public sealed class AndroidEnchantTooltip(IntPtr pointer) : MonoBehaviour(pointer)
{
    private sealed record View(CardTooltipController Owner, GameObject Block, CardEffectTooltipController Text);
    private static readonly Dictionary<int, View> Views = new();
    private static readonly HashSet<string> Failures = new();

    internal static void Install()
    {
        try { AndroidRuntimePaths.PrepareNativeHooks(); }
        catch (Exception error) { Report(error); return; }
        var harmony = new Harmony("com.bazaarplusplus.android.enchant");
        Patch(harmony, "RenderPassiveEffectTextBlock", nameof(AfterRender));
        foreach (var method in new[] { "ResetValues", "ClearCurrentCard", "OnDisable" })
            Patch(harmony, method, nameof(AfterHide));
        Patch(harmony, "OnDestroy", nameof(AfterDestroy));
    }

    private static void Patch(Harmony harmony, string target, string postfix)
    {
        try
        {
            var original = AccessTools.Method(typeof(CardTooltipController), target)
                ?? throw new MissingMethodException(target);
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(AndroidEnchantTooltip), postfix));
            var patcher = HarmonyLib.Public.Patching.PatchManager.GetMethodPatcher(original);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.enchant_preview.hook_ready method={target} patcher={patcher.GetType().FullName}");
        }
        catch (Exception error) { Report(error); }
    }

    private static void AfterRender(CardTooltipController __instance, Il2CppSystem.Collections.Generic.List<CardQuestGroupData>? questData)
    {
        Hide(__instance);
        if (questData == null || Data.IsInCombat) return;
        try
        {
            if (UnityEngine.Object.FindObjectOfType<BoardManager>()?.IsRecapViewOpen == true) return;
            var data = __instance.CurrentTooltipData?.TryCast<CardTooltipData>();
            var item = data?.CardInstance?.TryCast<ItemCard>();
            if (item == null) return;
            var content = AndroidEnchantPreview.Build(item);
            if (string.IsNullOrWhiteSpace(content)) return;
            var id = __instance.GetInstanceID();
            if (!Views.TryGetValue(id, out var view) || view.Block == null)
            {
                var donor = __instance.passiveEffectParent;
                if (donor == null) return;
                var block = UnityEngine.Object.Instantiate(donor, donor.transform.parent);
                block.name = "BPP.Android.EnchantPreview";
                var text = block.GetComponentInChildren<CardEffectTooltipController>(true);
                if (text == null) { UnityEngine.Object.Destroy(block); return; }
                for (var index = 0; index < block.transform.childCount; index++)
                {
                    var child = block.transform.GetChild(index);
                    if (child != text.transform && !text.transform.IsChildOf(child)) child.gameObject.SetActive(false);
                }
                foreach (var group in block.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
                var layout = block.GetComponent<LayoutElement>() ?? block.AddComponent<LayoutElement>();
                layout.ignoreLayout = false;
                if (block.GetComponent<LayoutGroup>() is { } groupLayout) groupLayout.padding.top = 0;
                text.textObject.enableAutoSizing = false;
                text.textObject.fontSize *= .65f;
                text.textObject.raycastTarget = false;
                view = new View(__instance, block, text);
                Views[id] = view;
            }
            view.Block.transform.SetSiblingIndex(__instance.passiveEffectParent.transform.GetSiblingIndex() + 1);
            view.Text.SetText(content);
            view.Text.gameObject.SetActive(true);
            view.Block.SetActive(true);
            LayoutRebuilder.MarkLayoutForRebuild(view.Block.transform.parent.GetComponent<RectTransform>());
        }
        catch (Exception error) { Report(error); }
    }

    private static void Hide(CardTooltipController owner)
    {
        if (Views.TryGetValue(owner.GetInstanceID(), out var view) && view.Block != null) view.Block.SetActive(false);
    }

    private static void AfterHide(CardTooltipController __instance) => Hide(__instance);
    private static void AfterDestroy(CardTooltipController __instance)
    {
        if (!Views.Remove(__instance.GetInstanceID(), out var view)) return;
        if (view.Block != null) UnityEngine.Object.Destroy(view.Block);
    }

    public void Update()
    {
        foreach (var key in Views.Keys.ToArray())
        {
            var view = Views[key];
            if (view.Owner == null || view.Block == null)
            {
                if (view.Block != null) UnityEngine.Object.Destroy(view.Block);
                Views.Remove(key);
                continue;
            }
            if (Data.IsInCombat || !view.Owner.isActiveAndEnabled) view.Block.SetActive(false);
        }
    }

    private static void Report(Exception error)
    {
        if (Failures.Add(error.GetType().Name + error.Message))
            Plugin.PluginLog?.LogError($"[BPP][Android] event=android.enchant_preview.failed exception={error}");
    }
}
