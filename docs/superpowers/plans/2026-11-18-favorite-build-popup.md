# 收藏角标缺卡弹窗 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 点对局卡牌左上角的收藏角标，弹出面板展示该卡所属的提醒中构筑，每张卡标注【场上】【背包中】【本次可选】【未拥有】，多套构筑可左右翻页。

**Architecture:** 状态判定是不依赖 Unity 的纯函数，离线测试。持有读取从构筑推荐页抽成公用方法。弹窗是独立画布上的新类，用现有 `AndroidBuildBoard` 画卡面；`AndroidFavoriteMarks` 负责热区与打开弹窗。

**Tech Stack:** C#（BepInEx / Unity IL2CPP，uGUI + TextMeshPro），`dotnet run` 可执行测试工程，Python `unittest` 回归。

**Spec:** `docs/superpowers/specs/2026-11-18-favorite-build-popup-design.md`

## Global Constraints

- 所有命令从 Git 仓库根（本工作树根）执行；分支 `feat/android-custom-build-ui`。
- 遵守根 `AGENTS.md`「Android 移植开发规范」：编码前按其第 1 节阅读 `bazaarplusplus-mod/docs/architecture/panels.md` 与 `docs/design.md`。
- 插件零警告编译；测试工程 `TreatWarningsAsErrors`。
- Unity 组件收窄用 `TryCast<T>()`，不用 `is`／`as`。单行省略的 `NativeUi.Label` 须把 `offsetMin`／`offsetMax` 上下内边距清零。
- 不修改任何游戏拥有的卡牌对象；点角标或面板不得触发选牌、购买。
- 文案固定：场上／背包中／本次可选／未拥有；英文 Board／Stash／Offered／Missing。颜色：场上 `8FE39A`、背包中 `82B8FF`、本次可选用 `AndroidCustomBuildPanel` 的金色、未拥有用其弱化色。
- 构筑推荐页的「仓库」用词不改。
- **提交节奏按 `AGENTS.md` 第 5 节**：本功能是一个功能点，Task 1–5 不单独提交，Task 6 真机通过后一次提交。Task 0 的编译修复按交接文档各自提交。
- 真机测试新开对局只选休闲；不购买、不选奖励，除非用户当次授权。设备产物写入 `bazaarplusplus-android/artifacts/`，不提交。

## Review Focus

1. 构筑含已失效物品（模板查不到）：该卡标【未拥有】，面板照常打开不抛异常。→ Task 1 离线断言 + Task 6 真机项。
2. 快速连续点 ◀ ▶：上一页晚完成的原生卡面被清理，不残留在新页。→ Task 6 真机项。
3. 弹窗开着时可选卡牌变化（商店刷新、选牌结束、进入战斗）：弹窗自动关闭，不留孤立遮罩挡住游戏。→ Task 6 真机项。
4. 一张卡同属三套以上提醒构筑：角标仍画三枚，翻页覆盖全部。→ Task 6 真机项。
5. 卡牌飞入或重排动画中点热区：打开的是手指下那张卡的构筑，不是上一帧位置的卡。→ Task 6 真机项。

## File Structure

| 文件 | 职责 |
|---|---|
| 新建 `bazaarplusplus-android/android-mod/CustomBuilds/CustomBuildOwnership.cs` | 状态枚举与纯判定函数 |
| 新建 `bazaarplusplus-android/android-mod/AndroidOwnedCards.cs` | 从游戏状态读场上／背包张数与本次可选集合 |
| 新建 `bazaarplusplus-android/android-mod/AndroidFavoriteBuildPopup.cs` | 弹窗画布、翻页、关闭 |
| 改 `android-mod/AndroidBuildBoard.cs` | 可选的每卡下方说明文字 |
| 改 `android-mod/AndroidBuildPanelController.cs` | 改用 `AndroidOwnedCards` |
| 改 `android-mod/AndroidFavoriteMarks.cs` | 模板→构筑映射、点击热区、弹窗生命周期 |
| 改 `android-mod/CustomBuilds/AndroidCustomBuildPanel.cs` | `Preview` 改为 `internal` |
| 改 `tests/CustomBuilds.Tests/{CustomBuilds.Tests.csproj,Program.cs}` | 链接新文件、加断言 |

---

### Task 0: 让分支编译并通过离线测试（前置）

本分支 `33b6392`、`f8bc4ff`、`a17201f` 从未编译。本机 `PATH` 上没有 `dotnet`。

- [ ] **Step 1:** 确认 .NET SDK、手机版 `BepInEx/interop`、框架 zip 的本机路径（`bazaarplusplus-android/toolchain.json` 给出版本与来源）。缺任何一项即停下向用户报告，不继续后续任务。
- [ ] **Step 2:** 执行 `bazaarplusplus-android/docs/handoff-custom-build-verification.md` 的第 0 步与第 1 步。编译错误在本分支修复，每个修复单独提交。
- [ ] **Step 3:** 验证：`build_probe.py ... --mod` 零警告成功；`dotnet run --project bazaarplusplus-android/tests/CustomBuilds.Tests/CustomBuilds.Tests.csproj` 打印断言总数且退出码 0。记下该总数（Task 1 用）。

### Task 1: 状态判定规则

**Files:**
- Create: `bazaarplusplus-android/android-mod/CustomBuilds/CustomBuildOwnership.cs`
- Modify: `bazaarplusplus-android/tests/CustomBuilds.Tests/CustomBuilds.Tests.csproj`（`<Compile Include>` 链接新文件）
- Test: `bazaarplusplus-android/tests/CustomBuilds.Tests/Program.cs`

**Interfaces:**
- Produces:
  ```csharp
  internal enum CustomBuildCardState { Board, Stash, Offered, Missing }
  internal static class CustomBuildOwnership
  {
      // 结果与 items 等长同序。
      internal static CustomBuildCardState[] Classify(IReadOnlyList<CustomBuildItem> items,
          IReadOnlyDictionary<Guid, int> board, IReadOnlyDictionary<Guid, int> stash, IReadOnlySet<Guid> offered);
  }
  ```

- [ ] **Step 1: 写失败的测试**，加在 `Program.cs` 打印总数之前：

```csharp
var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
CustomBuildItem[] owned = { new(a, 1, 0), new(a, 1, 1), new(b, 2, 2), new(c, 1, 4), new(d, 1, 5) };
var states = CustomBuildOwnership.Classify(owned,
    new Dictionary<Guid, int> { [a] = 1 }, new Dictionary<Guid, int> { [a] = 1, [b] = 1 }, new HashSet<Guid> { b, c });
Check(states.SequenceEqual(new[] { CustomBuildCardState.Board, CustomBuildCardState.Stash, CustomBuildCardState.Stash,
    CustomBuildCardState.Offered, CustomBuildCardState.Missing }), "Board, then stash, then offered, then missing");
var twice = CustomBuildOwnership.Classify(new CustomBuildItem[] { new(a, 1, 0), new(a, 1, 1) },
    new Dictionary<Guid, int> { [a] = 1 }, new Dictionary<Guid, int>(), new HashSet<Guid>());
Check(twice.SequenceEqual(new[] { CustomBuildCardState.Board, CustomBuildCardState.Missing }), "One owned copy covers one build item");
var offeredTwice = CustomBuildOwnership.Classify(new CustomBuildItem[] { new(c, 1, 0), new(c, 1, 1) },
    new Dictionary<Guid, int>(), new Dictionary<Guid, int>(), new HashSet<Guid> { c });
Check(offeredTwice.All(state => state == CustomBuildCardState.Offered), "An offer is not consumed");
Check(CustomBuildOwnership.Classify(Array.Empty<CustomBuildItem>(), new Dictionary<Guid, int>(),
    new Dictionary<Guid, int>(), new HashSet<Guid>()).Length == 0, "Empty build classifies to nothing");
```

- [ ] **Step 2:** 运行 `dotnet run --project bazaarplusplus-android/tests/CustomBuilds.Tests/CustomBuilds.Tests.csproj`。Expected：编译失败，`CustomBuildOwnership` 不存在。
- [ ] **Step 3: 实现 `Classify`。** 复制两份张数做余量，逐件依次扣场上、扣背包、查 offered（不扣）、否则 Missing。
- [ ] **Step 4:** 再次运行。Expected：退出码 0，断言总数 = Task 0 记下的数 + 4。
- [ ] **Step 5:** `python3 -m unittest bazaarplusplus-android/tests/test_custom_builds.py -v`。Expected：OK。

### Task 2: 公用持有读取

**Files:**
- Create: `bazaarplusplus-android/android-mod/AndroidOwnedCards.cs`
- Modify: `bazaarplusplus-android/android-mod/AndroidBuildPanelController.cs`（刷新方法里读取 `player.Hand.Container`、`player.Stash.Container`、`Data.CurrentState.SelectionSet` 的三段循环）

**Interfaces:**
- Produces:
  ```csharp
  internal sealed record AndroidOwnedCards(IReadOnlyDictionary<Guid, int> Board, IReadOnlyDictionary<Guid, int> Stash,
      IReadOnlySet<Guid> Offered)
  {
      // 没有对局时返回 null。必须在主线程调用。
      internal static AndroidOwnedCards? Read();
  }
  ```

- [ ] **Step 1:** 把三段循环原样搬进 `Read()`（保留 `TryCast<ItemCard>()` 与空值判断），场上与背包改为按模板计张数。玩家取法与构筑推荐页刷新方法里取 `player` 的方式相同。
- [ ] **Step 2:** 构筑推荐页改为调用 `Read()`，用 `Board.Keys`／`Stash.Keys`／`Offered` 填它原来的 `board`／`stash`／`shop` 集合与三个候选列表；候选的顺序与去重结果必须与改动前相同。删除被搬走的循环。
- [ ] **Step 3:** 验证：`build_probe.py ... --mod` 零警告成功。行为回归在 Task 6 真机做（构筑推荐页「场上／仓库／商店」三个数量与改动前截图一致）。

### Task 3: 阵容卡下方说明文字

**Files:**
- Modify: `bazaarplusplus-android/android-mod/AndroidBuildBoard.cs`（构造函数、`Show`）

**Interfaces:**
- Produces: 构造函数新增末位可选参数 `TMP_FontAsset? captionFont = null`；`Show(AndroidBuild? build, IReadOnlyList<string>? captions = null)`。`captions` 为富文本，按 `build.Items` 的 `Slot` 升序一一对应。

- [ ] **Step 1:** `Show` 里为每张卡在 `_row` 下加一个 `NativeUi.Label`，锚定在该卡的横向范围、`_row` 底边之下约 44 单位高，居中、单行、`raycastTarget = false`、上下内边距清零。仅当 `captions` 与 `captionFont` 都非空时创建。它们是 `_row` 的子物体，`Hide()` 现有的子物体清理会带走它们，不另写清理。
- [ ] **Step 2:** `ReferenceEquals(_shownBuild, build)` 的早退保持不变；弹窗每次翻页传新的 `AndroidBuild` 实例。
- [ ] **Step 3:** 验证：`build_probe.py ... --mod` 零警告成功；两个现有调用点不传新参数，无需改动。

### Task 4: 弹窗

**Files:**
- Create: `bazaarplusplus-android/android-mod/AndroidFavoriteBuildPopup.cs`
- Modify: `bazaarplusplus-android/android-mod/CustomBuilds/AndroidCustomBuildPanel.cs`（`Preview` 由 `private static` 改 `internal static`）

**Interfaces:**
- Consumes: `CustomBuildOwnership.Classify`、`AndroidOwnedCards.Read`、`AndroidBuildBoard` 的 Task 3 接口、`AndroidCustomBuildPanel.Preview(IReadOnlyList<CustomBuildItem>)`、`AndroidFavoriteMarks.Colors`、`CustomBuildStore.StarColor`。
- Produces:
  ```csharp
  internal sealed class AndroidFavoriteBuildPopup
  {
      internal bool IsOpen { get; }
      // builds：含该卡的提醒中构筑及其在「我的构筑」列表中的 1 起序号。
      internal void Open(Guid templateId, IReadOnlyList<(CustomBuild Build, int Number)> builds);
      internal void Close();
      internal void Tick();   // 每帧：驱动 board.Tick()
  }
  ```

- [ ] **Step 1:** 画布首次 `Open` 时惰性创建：`DontDestroyOnLoad`，ScreenSpaceOverlay，`sortingOrder = 30001`，带 `GraphicRaycaster`。字体取法与 `AndroidBuildPanelController` 建面板时相同。
- [ ] **Step 2:** 全屏遮罩（黑色约 60% 透明，`raycastTarget = true`，点击 → `Close`）。居中面板约占屏宽 86%、屏高 46%，自身 `raycastTarget = true` 以免点面板时关闭。
- [ ] **Step 3:** 面板内容：
  - 标题行：构筑色菱形、`NativeUi.Text($"Build {n}", $"构筑 {n}")`（`n` 为 `Number`）、居中的 `{页} / {总}`、右侧「关闭」按钮（`NativeUi.Button` + `AndroidPanelButtonSkin`，与「我的构筑」页按钮一致）。
  - 中部：`new AndroidBuildBoard(host, (_, _, _) => { }, keepSlots: true, captionFont: font)`。
  - 两侧 ◀ ▶ 按钮，触控区不小于 96×96；`builds.Count == 1` 时隐藏，首尾循环。
  - 底部汇总：`NativeUi.Text($"Owned {have} / {total} · {total - have} missing", $"已有 {have} / {total} 件 · 还缺 {total - have} 件")`，`have` = Board + Stash 的件数。
- [ ] **Step 4:** 翻页／打开时执行一次：`AndroidOwnedCards.Read()`（为 null 则 `Close`）→ `Classify(build.Items, ...)` → 按 Global Constraints 的文案与颜色生成 captions → `board.Show(AndroidCustomBuildPanel.Preview(build.Items), captions)`。`Classify` 的输入与 `Preview` 的输出都按 `Slot` 升序，保证 captions 对位。
- [ ] **Step 5:** `Close`：`board.Hide()`，画布 `SetActive(false)`，`IsOpen = false`。
- [ ] **Step 6:** 日志各一次、不逐帧：`event=android.favorite_popup.opened template=.. builds=..`、`event=android.favorite_popup.paged index=.. have=.. total=..`、`event=android.favorite_popup.closed`。
- [ ] **Step 7:** 验证：`build_probe.py ... --mod` 零警告成功。

### Task 5: 角标可点击

**Files:**
- Modify: `bazaarplusplus-android/android-mod/AndroidFavoriteMarks.cs`

**Interfaces:**
- Consumes: Task 4 的 `AndroidFavoriteBuildPopup`。

- [ ] **Step 1:** `_stars` 改为 `Dictionary<Guid, (CustomBuild Build, int Number)[]>`，`Reload` 里从 `_store.Builds` 带列表序号建立；每张卡要画的颜色由前三套构筑的 `StarColor` 去重得到，画法不变。`_targets` 同时记下模板 Guid。
- [ ] **Step 2:** 角标画布加 `GraphicRaycaster`。每个 `_targets` 项一个透明 `Image`（alpha .01，`raycastTarget = true`）+ `Button`，池化方式与 `_gems` 相同；在 `Draw` 里定位到整簇角标的包围盒，再外扩到边长不小于 72 像素。菱形 `raycastTarget` 保持 false。
- [ ] **Step 3:** 热区点击 → `_popup.Open(templateId, _stars[templateId])`。按钮回调在 `Draw` 重新绑定时不能捕获过期的模板：每帧把当前模板写进与热区同下标的数组，回调按下标读。
- [ ] **Step 4:** `Update` 里：`_popup.Tick()`；现有的隐藏条件（其他面板打开、长按预览激活、无提醒构筑）成立，或 `Data.CurrentState?.SelectionSet` 为空／计数变化，或 `Data.Run?.Player == null` 时，若弹窗开着则 `Close`。弹窗开着时清空 `_targets`（角标与热区隐藏）。
- [ ] **Step 5:** 现有 `catch` 分支里补上 `_popup.Close()` 与隐藏全部热区。
- [ ] **Step 6:** 验证：`build_probe.py ... --mod` 零警告成功；`python3 -m unittest discover -s bazaarplusplus-android/tests -v` OK；`CustomBuilds.Tests` 退出码 0。

### Task 6: 真机验收、文档、提交

- [ ] **Step 1:** 按 `.cursor/skills/export-launcher-apk/SKILL.md` 重建签名 APK 并 `adb install -r`；核对新会话日志中的 DLL 摘要与 `artifacts/mod/build-inputs.json` 一致。记录设备、Android／游戏版本、输入模式、测试前状态。
- [ ] **Step 2:** 准备：「我的构筑」里至少两套提醒中构筑共含同一张卡，其中一套含重复卡；进入含该卡的商店（需新开对局时只选休闲）。
- [ ] **Step 3:** 标准与高级输入模式各做一遍，每项截图 + 日志断言：
  - 点角标 → 弹窗出现，日志 `favorite_popup.opened`；该卡**未被选中或购买**（商店金币与库存不变）。
  - 点不到或穿透选牌时：把热区移到单独的高层级画布（低于 29998）后复测，并在验证记录写明。
  - 各卡状态与实际一致：对照场上、背包、当前商店逐张核对；重复卡只有一张时一件已有一件未拥有；汇总数字正确。
  - ◀ ▶ 翻页：标题色、序号、`N / M` 变化；快速连点十次无残留卡面（Review Focus 2）。
  - 仅属一套构筑的卡：不显示翻页箭头。
  - 同属四套提醒构筑的卡：角标三枚，可翻四页（Review Focus 4）。
  - 含失效物品的构筑：弹窗正常，该位标未拥有（Review Focus 1；没有这样的存档则如实记为未覆盖）。
  - 点遮罩、点「关闭」都收起，日志 `closed`，角标恢复，随后点卡牌的长按预览正常。
  - 弹窗开着时刷新商店或等选牌结束：弹窗自动关闭，无孤立遮罩（Review Focus 3；刷新商店花金币，需用户授权，否则记为待验证）。
  - 卡牌飞入动画期间点热区，打开的是正确的卡（Review Focus 5）。
  - 小、中、大型卡的热区都不妨碍点卡牌主体区域触发游戏原有操作的观察（不实际购买）。
  - 回归：构筑推荐页「场上／仓库／商店」数量与候选列表和改动前一致；「我的构筑」列表与编辑页卡面下方没有多出文字。
  - 全程无 `android.favorite_marks.failed`，无逐帧日志。
- [ ] **Step 4:** 更新文档：`bazaarplusplus-android/docs/feature-checklist.md` 构筑推荐／自定义构筑条目写明实现范围与真机状态；`docs/device-validation.md` 追加本次环境、步骤、预期／实际、证据位置；`bazaarplusplus-android/README.md` 的收藏提醒描述补上「点角标查看缺卡」。未通过或未覆盖的项标为待验证。
- [ ] **Step 5:** 自查并提交（真机未通过则不提交，继续修复）：

```bash
git status
git diff --check
git add docs/superpowers/specs/2026-11-18-favorite-build-popup-design.md docs/superpowers/plans/2026-11-18-favorite-build-popup.md \
  bazaarplusplus-android/android-mod/CustomBuilds/CustomBuildOwnership.cs bazaarplusplus-android/android-mod/AndroidOwnedCards.cs \
  bazaarplusplus-android/android-mod/AndroidFavoriteBuildPopup.cs bazaarplusplus-android/android-mod/AndroidBuildBoard.cs \
  bazaarplusplus-android/android-mod/AndroidBuildPanelController.cs bazaarplusplus-android/android-mod/AndroidFavoriteMarks.cs \
  bazaarplusplus-android/android-mod/CustomBuilds/AndroidCustomBuildPanel.cs bazaarplusplus-android/tests/CustomBuilds.Tests \
  bazaarplusplus-android/docs/feature-checklist.md bazaarplusplus-android/docs/device-validation.md bazaarplusplus-android/README.md
git diff --cached --stat
git commit -m "feat(android): show missing cards of starred builds from the card flag"
```
