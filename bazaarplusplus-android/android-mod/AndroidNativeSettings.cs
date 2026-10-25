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
        GameObject? modeRoot = null;
        GameObject? positionRoot = null;
        GameObject? dockRoot = null;
        GameObject? historyRoot = null;
        GameObject? styleRoot = null;
        try
        {
            var mode = CloneRow(source, staging.transform, "BPP.LongPressSetting", out modeRoot, out var modeLabel);
            var position = CloneRow(source, staging.transform, "BPP.PreviewPositionSetting", out positionRoot, out var positionLabel);
            var dock = CloneRow(source, staging.transform, "BPP.DockSetting", out dockRoot, out var dockLabel);
            var history = CloneRow(source, staging.transform, "BPP.HistoryButtonSetting", out historyRoot, out var historyLabel);
            var style = CloneRow(source, staging.transform, "BPP.ButtonStyleSetting", out styleRoot, out var styleLabel);
            var rows = new Rows(window, modeRoot, positionRoot, dockRoot, historyRoot, styleRoot, mode, position, dock, history, style,
                modeLabel, positionLabel, dockLabel, historyLabel, styleLabel);
            mode.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                Select(rows, "long_press", () => AndroidLongPressPreviewSettings.Mode = (AndroidLongPressPreviewMode)value)));
            position.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                Select(rows, "panel_position", () => AndroidPreviewPanelSettings.SetLeftEdge(value == 1))));
            dock.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                Select(rows, "dock_auto_hide", () => AndroidFloatingDock.AutoHide = value == 1)));
            history.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                Select(rows, "dock_history", () => AndroidDockSettings.Save(AndroidDockSettings.Current with { ShowHistory = value == 0 }))));
            style.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<int>>((int value) =>
                Select(rows, "dock_style", () => AndroidDockSettings.Save(AndroidDockSettings.Current with { TextButtons = value == 1 }))));
            Refresh(rows);
            modeRoot.transform.SetParent(source.parent, false);
            positionRoot.transform.SetParent(source.parent, false);
            dockRoot.transform.SetParent(source.parent, false);
            historyRoot.transform.SetParent(source.parent, false);
            styleRoot.transform.SetParent(source.parent, false);
            modeRoot.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
            positionRoot.transform.SetSiblingIndex(modeRoot.transform.GetSiblingIndex() + 1);
            dockRoot.transform.SetSiblingIndex(positionRoot.transform.GetSiblingIndex() + 1);
            styleRoot.transform.SetSiblingIndex(dockRoot.transform.GetSiblingIndex() + 1);
            historyRoot.transform.SetSiblingIndex(styleRoot.transform.GetSiblingIndex() + 1);
            dockRoot.SetActive(true);
            historyRoot.SetActive(true);
            styleRoot.SetActive(true);
            modeRoot.SetActive(true);
            positionRoot.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(section);
            if (spy._scrollRect?.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(spy._scrollRect.content);
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.settings.installed window={window.GetInstanceID()} mode={AndroidLongPressPreviewSettings.Mode} left={AndroidPreviewPanelSettings.LeftEdge}");
            return rows;
        }
        catch
        {
            if (modeRoot != null) Destroy(modeRoot);
            if (positionRoot != null) Destroy(positionRoot);
            if (dockRoot != null) Destroy(dockRoot);
            if (historyRoot != null) Destroy(historyRoot);
            if (styleRoot != null) Destroy(styleRoot);
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
            Plugin.PluginLog?.LogInfo($"[BPP][Android] event=android.settings.changed setting={setting} mode={AndroidLongPressPreviewSettings.Mode} left={AndroidPreviewPanelSettings.LeftEdge}");
        }
        catch (Exception error)
        {
            Plugin.PluginLog?.LogWarning($"[BPP][Android] event=android.settings.save_failed setting={setting} exception={error}");
        }
        Refresh(rows);
    }

    private static void Refresh(Rows rows)
    {
        var modeLabel = NativeUi.Text("BPP: Long press preview", "BPP：长按预览");
        var positionLabel = NativeUi.Text("BPP: Preview panel position", "BPP：预览面板位置");
        if (rows.ModeLabel.text != modeLabel || rows.PositionLabel.text != positionLabel || rows.Mode.options.Count != 3 || rows.Dock.options.Count != 2
            || rows.History.options.Count != 2 || rows.Style.options.Count != 2)
        {
            rows.ModeLabel.text = modeLabel;
            rows.PositionLabel.text = positionLabel;
            rows.DockLabel.text = NativeUi.Text("BPP: Auto-hide buttons", "BPP：按钮自动收起");
            rows.HistoryLabel.text = NativeUi.Text("BPP: History button", "BPP：对局历史按钮");
            SetOptions(rows.Dock, NativeUi.Text("Off", "关闭"), NativeUi.Text("After 5 seconds", "闲置5秒后收起"));
            SetOptions(rows.History, NativeUi.Text("Show", "显示"), NativeUi.Text("Hide", "隐藏"));
            rows.StyleLabel.text = NativeUi.Text("BPP: Button style", "BPP：按钮样式");
            SetOptions(rows.Style, NativeUi.Text("Icons", "图标"), NativeUi.Text("Text only (classic)", "纯文字（旧版）"));
            SetOptions(rows.Mode, NativeUi.Text("Enchants", "附魔"), NativeUi.Text("Upgrade", "升级"), NativeUi.Text("None", "无"));
            SetOptions(rows.Position, NativeUi.Text("Default", "默认位置"), NativeUi.Text("Left edge", "靠屏幕左侧边缘"));
        }
        rows.Mode.SetValueWithoutNotify((int)AndroidLongPressPreviewSettings.Mode);
        rows.Position.SetValueWithoutNotify(AndroidPreviewPanelSettings.LeftEdge ? 1 : 0);
        rows.Dock.SetValueWithoutNotify(AndroidFloatingDock.AutoHide ? 1 : 0);
        rows.History.SetValueWithoutNotify(AndroidDockSettings.Current.ShowHistory ? 0 : 1);
        rows.Style.SetValueWithoutNotify(AndroidDockSettings.Current.TextButtons ? 1 : 0);
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
        {
            if (rows.ModeRoot != null) Destroy(rows.ModeRoot);
            if (rows.PositionRoot != null) Destroy(rows.PositionRoot);
            if (rows.DockRoot != null) Destroy(rows.DockRoot);
            if (rows.HistoryRoot != null) Destroy(rows.HistoryRoot);
            if (rows.StyleRoot != null) Destroy(rows.StyleRoot);
        }
        _windows.Clear();
    }

    private sealed record Rows(OptionsDialogController Window, GameObject ModeRoot, GameObject PositionRoot, GameObject DockRoot, GameObject HistoryRoot, GameObject StyleRoot,
        TMP_Dropdown Mode, TMP_Dropdown Position, TMP_Dropdown Dock, TMP_Dropdown History, TMP_Dropdown Style,
        TextMeshProUGUI ModeLabel, TextMeshProUGUI PositionLabel, TextMeshProUGUI DockLabel, TextMeshProUGUI HistoryLabel, TextMeshProUGUI StyleLabel);
}
