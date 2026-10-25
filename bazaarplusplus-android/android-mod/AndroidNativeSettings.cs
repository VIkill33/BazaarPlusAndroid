using Il2CppInterop.Runtime;
using TheBazaar.UIScripts;
using TheBazaar.Localization;
using TheBazaar.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BazaarPlusPlus.Android;

// Observe the native window after its own initialization; no native method hooks.
public sealed class AndroidNativeSettings(IntPtr pointer) : MonoBehaviour(pointer)
{
    private readonly List<Rows> _windows = new();
    private float _nextScan;
    private int _failures;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _nextScan) return;
        _nextScan = Time.realtimeSinceStartup + .5f;
        try
        {
            _windows.RemoveAll(rows => rows.Window == null);
            foreach (var window in Resources.FindObjectsOfTypeAll<OptionsDialogController>())
            {
                if (window == null || !window.isActiveAndEnabled) continue;
                var rows = _windows.Find(candidate => candidate.Window == window);
                if (rows == null)
                {
                    rows = Install(window);
                    if (rows == null) continue;
                    _windows.Add(rows);
                }
                Refresh(rows);
            }
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.settings.failed exception={error}");
            // Do not retry a broken native hierarchy every frame.
            _nextScan = Time.realtimeSinceStartup + 5;
            if (++_failures >= 3) enabled = false;
        }
    }

    private static Rows? Install(OptionsDialogController window)
    {
        var donor = window._languageSetting;
        var spy = window.GetComponentInChildren<ScrollSpyController>(true);
        if (donor == null || spy == null || spy._entries == null) return null;
        RectTransform? section = null;
        foreach (var entry in spy._entries)
        {
            if (entry.SectionRoot != null && donor.transform.IsChildOf(entry.SectionRoot))
            {
                section = entry.SectionRoot;
                break;
            }
        }
        if (section == null) return null;
        var source = donor.transform;
        // Keep the row's native label, layout and dropdown, stopping before other settings.
        while (source.parent != null && source.parent != section)
        {
            var hasOtherControl = false;
            foreach (var selectable in source.parent.GetComponentsInChildren<Selectable>(true))
                if (selectable != donor && !selectable.transform.IsChildOf(donor.transform))
                    hasOtherControl = true;
            if (hasOtherControl) break;
            source = source.parent;
        }
        if (source == donor.transform)
            throw new InvalidOperationException("Native settings dropdown has no labelled row.");

        var staging = new GameObject("BPP.SettingsStaging");
        staging.SetActive(false);
        var items = new List<Row>();
        try
        {
            var rows = new Rows(window, items);
            foreach (var spec in Specs)
            {
                var dropdown = CloneRow(source, staging.transform, spec.Name, out var root, out var label);
                items.Add(new Row(root, dropdown, label, spec));
                dropdown.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                    Select(rows, spec.Log, () => spec.Write(value))));
            }
            Refresh(rows);
            var previous = source;
            foreach (var row in items)
            {
                row.Root.transform.SetParent(source.parent, false);
                row.Root.transform.SetSiblingIndex(previous.GetSiblingIndex() + 1);
                previous = row.Root.transform;
            }
            foreach (var row in items) row.Root.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(section);
            if (spy._scrollRect?.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(spy._scrollRect.content);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.settings.installed window={window.GetInstanceID()} mode={AndroidLongPressPreviewSettings.Mode} left={AndroidLauncherSettings.LeftEdge}");
            return rows;
        }
        catch
        {
            foreach (var row in items) if (row.Root != null) Destroy(row.Root);
            throw;
        }
        finally { Destroy(staging); }
    }

    private static TMP_Dropdown CloneRow(Transform source, Transform staging, string name,
        out GameObject root, out TextMeshProUGUI label)
    {
        root = Instantiate(source.gameObject, staging, false);
        root.name = name;
        root.SetActive(false);
        foreach (var localizable in root.GetComponentsInChildren<LocalizableTextComponent>(true))
            DestroyImmediate(localizable);
        var dropdown = root.GetComponentInChildren<TMP_Dropdown>(true)
            ?? throw new InvalidOperationException("Missing cloned dropdown.");
        // Replace the event object: RemoveAllListeners would leave serialized game callbacks.
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dropdown.interactable = true;
        // The language selector caption uses a restricted font; include option/template text too.
        var font = NotoFontFallbackRuntime._loadedSansPrimary
            ?? throw new InvalidOperationException("Native settings font is not ready.");
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) text.font = font;
        label = root.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(text => !text.transform.IsChildOf(dropdown.transform))
            ?? throw new InvalidOperationException("Missing cloned setting label.");
        return dropdown;
    }

    private static void Select(Rows rows, string setting, Action save)
    {
        try
        {
            save();
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.settings.changed setting={setting} mode={AndroidLongPressPreviewSettings.Mode} left={AndroidLauncherSettings.LeftEdge}");
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.settings.save_failed setting={setting} exception={error}");
        }
        Refresh(rows);
    }

    private static void Refresh(Rows rows)
    {
        foreach (var row in rows.Items)
        {
            var label = row.Spec.Label();
            var options = row.Spec.Options();
            if (row.Label.text != label || row.Dropdown.options.Count != options.Length
                || row.Dropdown.options[0].text != options[0])
            {
                row.Label.text = label;
                SetOptions(row.Dropdown, options);
            }
            row.Dropdown.SetValueWithoutNotify(row.Spec.Read());
        }
    }

    private static void SetOptions(TMP_Dropdown dropdown, params string[] options)
    {
        dropdown.ClearOptions();
        foreach (var option in options) dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        dropdown.RefreshShownValue();
    }

    public void OnDestroy()
    {
        foreach (var rows in _windows)
            foreach (var row in rows.Items)
                if (row.Root != null) Destroy(row.Root);
        _windows.Clear();
    }

    private sealed record Spec(string Name, string Log, Func<string> Label, Func<string[]> Options, Func<int> Read, Action<int> Write);
    private sealed record Row(GameObject Root, TMP_Dropdown Dropdown, TextMeshProUGUI Label, Spec Spec);
    private sealed record Rows(OptionsDialogController Window, List<Row> Items);

    private static string[] Visibility() => new[] { NativeUi.Text("Show", "显示"), NativeUi.Text("Hide", "隐藏") };

    private static Spec Button(string name, string log, string english, string chinese, AndroidDockButton button) =>
        new(name, log, () => NativeUi.Text(english, chinese), Visibility,
            () => AndroidLauncherSettings.Shown(button) ? 0 : 1, value => AndroidLauncherSettings.SetShown(button, value == 0));

    private static readonly Spec[] Specs =
    {
        new("BPP.LongPressSetting", "long_press", () => NativeUi.Text("BPP: Long press preview", "BPP：长按预览"),
            () => new[] { NativeUi.Text("Enchants", "附魔"), NativeUi.Text("Upgrade", "升级"), NativeUi.Text("None", "无") },
            () => (int)AndroidLongPressPreviewSettings.Mode, value => AndroidLongPressPreviewSettings.Mode = (AndroidLongPressPreviewMode)value),
        new("BPP.PreviewPositionSetting", "panel_position", () => NativeUi.Text("BPP: Preview panel position", "BPP：预览面板位置"),
            () => new[] { NativeUi.Text("Default", "默认位置"), NativeUi.Text("Left edge", "靠屏幕左侧边缘") },
            () => AndroidLauncherSettings.LeftEdge ? 1 : 0, value => AndroidLauncherSettings.SetLeftEdge(value == 1)),
        new("BPP.DockSetting", "dock_auto_hide", () => NativeUi.Text("BPP: Auto-hide buttons", "BPP：按钮自动收起"),
            () => new[] { NativeUi.Text("Off", "关闭"), NativeUi.Text("After 5 seconds", "闲置5秒后收起") },
            () => AndroidFloatingDock.AutoHide ? 1 : 0, value => AndroidFloatingDock.AutoHide = value == 1),
        new("BPP.ButtonStyleSetting", "dock_style", () => NativeUi.Text("BPP: Button style", "BPP：按钮样式"),
            () => new[] { NativeUi.Text("Icons", "图标"), NativeUi.Text("Text only (classic)", "纯文字（旧版）") },
            () => AndroidDockSettings.Current.TextButtons ? 1 : 0,
            value => AndroidDockSettings.Save(AndroidDockSettings.Current with { TextButtons = value == 1 })),
        Button("BPP.CollectionButtonSetting", "dock_collection", "BPP: Card collection button", "BPP：卡牌图鉴按钮", AndroidDockButton.Collection),
        Button("BPP.BuildsButtonSetting", "dock_builds", "BPP: Builds button", "BPP：构筑推荐按钮", AndroidDockButton.Builds),
        Button("BPP.PreviewModeButtonSetting", "dock_preview_mode", "BPP: Long press mode button", "BPP：长按模式按钮", AndroidDockButton.PreviewMode),
        Button("BPP.HoldPreviewButtonSetting", "dock_hold_preview", "BPP: Hold to preview button", "BPP：按住预览按钮", AndroidDockButton.HoldPreview),
        Button("BPP.HistoryButtonSetting", "dock_history", "BPP: History button", "BPP：对局历史按钮", AndroidDockButton.History),
    };
}
