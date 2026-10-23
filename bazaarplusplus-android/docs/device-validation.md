# Android 真机验证

## 游戏内长按预览与面板位置设置（2026-10-23，用户确认通过）

用户确认「长按预览：附魔／升级／无」与「预览面板位置」已加入游戏设置，
并已验证通过。本次按该确认同步文档和提交，未重新执行真机操作；
不将此前面板位置或长按预览的历史测试扩展为本次新增设置控件的自动验收。

实现范围：

- `android-mod/Plugin.Load` 注册 `AndroidNativeSettings`，其 `Install` 在原生设置
  语言选项下方复用带标签的原生下拉行，`Refresh` 同步中英文标签与当前选择。
- 「BPP：长按预览」直接选择附魔、升级或无；`AndroidLongPressPreviewSettings.Mode`
  与左侧快捷按钮共用 PlayerPrefs 偏好，保留已有选择并拒绝非法枚举值。
  「无」保留游戏原生物品详情，关闭物品附加预览。
- 「BPP：预览面板位置」选择默认或靠屏幕左侧边缘；
  `AndroidPreviewPanelSettings.SetLeftEdge` 先写临时文件再替换
  `BepInEx/bpp-launcher-settings.json`，成功后更新运行时值，后续预览渲染使用新位置。
  作用范围为物品附加预览共用面板（含任务奖励页），事件与怪物面板位置不变。
- `LauncherSettingsActivity.readPosition` 优先读取共用文件，旧启动器偏好作为回退；
  启动器设置写入同一文件，`LauncherPreparation` 启动前读取最新位置，保留游戏内选择。

本次提交前复跑 Python 9 项离线检查、`LongPressSettings.Tests`（缺省、0／1／2、
非法 -1／99 的加载、循环、直接选择与保存）及 `PreviewPanelSettings.Tests`
（解析、持久化、写入失败时保留原设置）均通过；文档相对链接与 `git diff --check` 通过。
本次未重新构建 APK 或插件，已有本地构建与检查日志保存在被忽略的 `artifacts/settings-build.log`、
`artifacts/settings-apk-build.log`、`artifacts/settings-tests.log`；设置相关截图包括
`artifacts/settings-native-before.png`、`artifacts/settings-open.png`、`artifacts/settings-fresh.png`。
这些文件仅作为本地记录入口，不据此宣称完整自动视觉断言。
本次用户确认未另行列出设备环境、标准／高级模式覆盖、重启持久化步骤及故障注入结果，
因此不补写逐项通过结论，也不标记其他物品数值或设备兼容性已验收。

## 长按浮层避让冷却圆圈（2026-10-23）

参考 Android 预览文档、`AndroidTooltipObserver.Render` / `BuildUi` 和桌面
`NativePairedTooltipHost.GetPrimaryVisibleBounds`。旧浮层的右侧预留仅覆盖原生详情框，
未覆盖向左突出的冷却圆圈；真机长按白银「私人喷气机」复现圆圈左侧被遮。
本次只调整 Android 附加预览的水平预留，初建与再次渲染使用同一组常量，
保持原有宽度、文字、滚动及预览规则。用户确认不再遮挡后要求缩小间距，已进一步收紧。

设备 `OBJVWSPNSCFQU48L`（Redmi Note 11T Pro、Android 14），游戏
`1.0.12825-production-android-d9601ded`，横屏 2460×1080、中文、移动界面缩放 100%。
测试前为已有对局商店，初始高级输入、长按默认为升级；没有购买、实际升级／附魔或推进遭遇。
编译使用本机对应 Android 代理，构建记录为 `artifacts/mod/build-inputs.json`。
测试时在启动器完成准备后替换目标插件并同步测试会话摘要，核对新会话的
`bpp-launch-loaded.json` 与本次 DLL SHA-256；未重新打包 APK，普通重启仍会恢复 APK 内置插件。

- 插件编译零警告、零错误；Python 9 项检查与 `PreviewTooltips.Tests` 通过。
- 收紧后的标准模式：私人喷气机附魔／升级页均可见，冷却圆圈完整且保留小间距，
  点击页签能切换内容。附魔预览日志断言原物品未变化、无预览异常、无对局命令或状态推进。
- 收紧后的高级模式：同一物品附魔／升级长按均正常，圆圈完整、间距紧凑，松手关闭；两轮各 5 项日志断言通过。结束时恢复高级输入、长按默认升级。
- 标准模式锁定详情的空白关闭未在本次自动操作中确认，最终通过重启恢复场景；不将此项记为通过。
- 截图为人工复核，未实现自动视觉边界断言；未覆盖其他物品、其他分辨率、界面缩放和第二台设备。

证据位于被忽略的 `artifacts/tooltip-placement/`：`overlap.png` 为原问题，
`build.log` / `tests.log` 为离线结果，`loaded.json` 为新会话确认，
`tight-standard-enchant-*` / `tight-standard-upgrade.png` 为收紧后的标准模式，
`tight-advanced-*` 为收紧后的高级模式。
重复长按使用该目录的 `check_preview.py <名称> <当前截图的 x> <y> <enchant|upgrade>`，
自动断言渲染日志、原物品未变化、无异常、无对局命令及状态推进；圆圈和间距仍须看图。


## 桌面重新打开恢复运行中的游戏（2026-10-23）

参考 `LauncherActivity.onCreate`、`BootstrapActivity.runBootstrapFlow`、
`InstrumentationHooks` 与 ADR-0001。真机故障栈为同一任务内的
`LauncherActivity → StubActivity → LauncherActivity`：桌面 MAIN/LAUNCHER 入口
新增首页并遮住仍存活的游戏。`LauncherActivity.onCreate` 现对非任务根的
MAIN/LAUNCHER 重复入口立即 `finish()`，保留原游戏与任务；结束中的入口不启动首页刷新。
不改变游戏加载、修复互斥、桌面插件及游戏内 UI。

本次设备 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14，游戏版本
`1.0.12825-production-android-d9601ded`（12825）。旧版有已加载游戏及重复首页；
修复版使用原签名覆盖安装，冷启动首页后点击「启动游戏」，进入游戏加载页及大厅。
没有选择继续对局、购买、选择遭遇或推进战斗；最后保留大厅设置界面。

- Python 9 项离线检查、签名 release 构建（含 Lint vital）及 APK 签名验证通过。
- 新增 `verify_launcher_resume.py`，要求已运行的游戏处于前台；通过 HOME 及实测桌面
  MAIN/LAUNCHER flags 重开入口，等待原游戏和完整栈恢复，断言 ActivityRecord、任务栈和
  PID 均不变。旧版一次复现失败（前台及栈改变，PID 不变），修复版连续三轮的 9 项断言通过。
- 使用非 MAIN 的显式入口冷启动，覆盖原故障的入口不一致条件；首页和启动游戏正常。
  `verify_launcher.py --expect-loaded --not-before-ms …` 的 12 项当前构建／会话／DLL 断言通过。
- 实际横屏底部连续两次上滑返回 MIUI 桌面（游戏手势防误触），按当前 UI XML 的
  「集市增强启动器」坐标点击图标；返回原大厅设置页。ActivityRecord、任务栈和 PID
  三项断言通过；画面保留由截图人工复核，不是自动视觉断言。

证据位于被忽略的 `artifacts/launcher/`：`resume-before/` 为旧版失败，
`resume-build.log` 为构建日志，`resume-loaded/` 为当前会话确认，`resume/` 为三轮
任务栈、截图、`verification.json`、实际手势与点击的 `gesture-home.xml`、
`gesture-home.png`、`gesture-return.txt`、`gesture-return.png` 和 `gesture-verification.json`。
准备过程中返回、系统杀进程后的恢复、最近任务卡片及第二设备仍待验证。

## 启动器内置 Unity 依赖（2026-10-23）

参考 `LauncherPreparation.prepare`、`build_launcher.py::prepare` 与
[ADR-0001](adr/0001-dedicated-launcher.md)，把三个固定 GitHub 输入移至构建阶段下载、
校验并写入 APK。运行时仅从内置资源提取，继续校验大小和 SHA-256；未修改桌面插件及
游戏内 UI。来源 URL 和备用资源 API 保留在发行清单供追溯，备用入口仅用于构建。

本次设备为 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14、arm64，
游戏版本仍为 `1.0.12825-production-android-d9601ded`（12825）。开始时处于已有对局的
战斗画面，待战斗自行结束、停在遭遇选择界面后进行覆盖安装；没有点击遭遇或推进对局。
用户确认手机系统安装提示后，同一签名的 `0.1.0-test.6` 覆盖安装成功。

验收步骤与结果：

- Python 9 项离线测试通过，包含执行生产 Java 文件事务／握手测试；新增依赖测试覆盖
  已校验缓存无网络打包、坏缓存与主来源坏摘要时备用来源恢复、长度及摘要不符时拒绝构建、
  APK 内依赖缺失／篡改时拒绝验收。
- 签名 release 构建、Lint vital 与 APK 签名验证通过；直接读取 APK 内框架、运行时、插件及
  三个 Unity 依赖，实际内容均与清单的大小和 SHA-256 匹配。
- 停止启动器后，仅暂存 `BepInEx/unity-libs/6000.3.11.zip` 到同目录备份，
  不清理账号、插件、代理或游戏持久数据。关闭 Wi-Fi 并开启飞行模式，确认
  `Active default network: none`，通过中文首页的「启动游戏」执行准备。
- 缺失 ZIP 从 APK 恢复，摘要与固定输入一致。本次会话与 DLL 摘要一致，插件入口和
  会话加载日志出现；`verify_launcher.py --expect-loaded --not-before-ms …` 的
  12 项自动断言通过，会话创建到握手约 18.0 秒。没有依赖上一轮加载确认。
- 验证后移除测试备份，关闭飞行模式，恢复 Wi-Fi；移动数据保持原启用状态。
  截图人工复核停在游戏加载画面；断网时游戏登录及自身资源加载不在通过范围内。

证据位于被忽略的 `artifacts/launcher/`：`offline-tests.log`、`bundled-build.log`、
`signing-report.txt` 和 `bundled-offline/` 下的验证脚本、网络状态、XML、截图、
`verification.json`、`validation.log`、筛选后的当前会话事件。
本次未清除全部应用缓存或重建游戏代理，因此全新设备无缓存初始化、完整修复、
第二台设备和其他游戏版本仍待验证；没有将历史验收计作本次验收。

## 专用单 APK 启动器（2026-10-23）

实现参考固定 FusionCore 的 `BootstrapActivity.prepareConfig`、`GameRuntime`、JNI
`fusion_parse_config` 与独立实施前评审；架构见 [ADR-0001](adr/0001-dedicated-launcher.md)。
桌面插件规则没有修改，只给 Android `Plugin.Load` 增加版本常量与会话握手。
指定品牌 PNG 同时用于首页、普通图标与自适应图标；后者保留安全边距，避免裁掉 `++`。

本轮设备为 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14，游戏为
`1.0.12825-production-android-d9601ded`（12825）。测试前原 FusionCore 停在已有对局的大厅，
未恢复对局或改变库存。本轮使用独立应用身份和新目录，没有迁移账号、推送 DLL、复制旧代理，
没有购买、选择遭遇、推进战斗或上传操作。ADB 仅用于安装测试 APK、UI 操作及采集证据。

已完成的设备检查：

- 测试 APK 安装后正确识别游戏；指定图标与中文首页由截图人工复核。
- 点击授权进入系统页，未授权返回后仍显示缺少权限；随后通过系统开关授权，返回显示初始化入口。
  没有自动重复弹窗。系统授权状态通过 AppOps 和 UI 文本核对。
- 全新目录由 APK 从已安装游戏准备约 2.5 GB 资源。首次下载连接中断，初始化停止，未创建加载成功证据。
  失败原因记录在本机；用户调整网络后重试，已复制资源保留，依赖由 APK 下载并校验后继续。
- 测试 APK 覆盖更新保留授权与资源。新身份下完成原生加载器启动、首次代理生成与内置插件加载；
  本次生成的代理目录约 69 MB，没有复制开发手机原环境的代理。
- `verify_launcher.py --expect-loaded --not-before-ms …` 的 11 项自动断言通过：APK 内置 DLL、
  已安装启动器版本、新会话、握手会话、插件版本、三处 DLL 摘要、时间顺序、成功值、
  本会话加载事件、插件入口事件和游戏代理存在性。会话分配到握手约 141.7 秒，
  不包括此前游戏资源复制和依赖下载，不能作为完整首次初始化耗时。
- 原 FusionCore 的插件摘要在测试前后相同。未读取或导出其账号文件。
- 用户完成首次登录并确认进入大厅；缓存启动的新会话 11 项断言通过，会话分配到握手约
  17.3 秒。没有执行完整断网测试。大厅图鉴可打开，显示皮格马利翁 156 件物品，
  目录缓存命中 1,850 张，单卡预览就绪并可关闭；未推进对局。
- 通过首页修复入口重建资源及代理，新会话 11 项断言通过；修复后截图确认仍在已登录大厅。
  `PersistentData` 中专门创建的无账号测试标记在修复前后摘要相同，验证后已清除。
- 最终签名包 `0.1.0-test.5` 覆盖安装成功，内置插件从 `0.1.0` 升到 `0.1.1`，
  新会话 11 项断言通过。经系统文件选择器导出诊断，核对私有旧版备份摘要等于上一版
  已成功加载的 DLL；落盘 DLL、发行清单与新会话确认均对应新版。
- 诊断导出为限定字段的 JSON，核对版本、阶段与摘要；未导出游戏原始日志或账号数据。

离线检查：固定工具链插件构建零警告／错误，签名 APK 构建及 Lint vital 门禁通过，
APK 签名验证通过；Python 5 项测试、BuildCorpus 与 EnchantPolicy 通过。
新增 Java 执行测试覆盖同长度文件篡改、替换失败保留目标、ZIP 越界、符号链接清理拒绝，
以及旧会话／错版本／错摘要／过早确认／未加载／落盘文件失效时拒绝握手。

证据位于被忽略的 `artifacts/launcher/`：签名报告、构建日志、授权 XML、初始化失败日志、
`first-initialization/verification.json`、`warm-start/verification.json`、
`repair/verification.json`、`final-upgrade/verification.json`、导出的诊断、
筛选后的插件事件、截图及原环境插件前后摘要。
截图只做人工复核，没有声称自动视觉断言。

用户确认暂时没有第二台设备，要求先交付单机测试版。第二台不同环境 arm64 设备、
完整故障注入、未知游戏更新、专用启动器下的双输入模式游戏内回归及公开发行许可审计
仍待验证。现有加载握手不能代表上述场景通过，尚未提供自动更新。

## 图鉴磁盘缓存（2026-10-23）

参考桌面 `docs/architecture/collection.md` 的静态目录与筛选语义、
`AndroidCollectionCatalog` 原有后台读取／分帧投影，以及手机版生成代理
`JsonGameDataManager._dbPath`、`GetCardById` 和翻译清单路径接口。
Android 只持久化 `CollectionCardVm` 托管快照；预览模板按需通过原生单卡接口获取，
未修改共享桌面规则，没有本地可用的原生反编译方法体。

缓存位于 BepInEx cache 下的 `bpp-collection-v1.json`。来源键包含静态数据库及存在时
SQLite WAL、翻译清单的 SHA-256、游戏版本、当前语言、插件构建标识和缓存格式版本；
负载另有 SHA-256。仅完整且没有投影异常的非空目录写入缓存，先写临时文件再原子替换。
缺失、过期或损坏自动回退到完整读取，写入失败保留当前可浏览目录；关闭重开继续复用内存，
重开时管理器身份或语言变化会丢弃旧模板与快照，未完成的旧读取结果不再接收。

固定工具链编译零警告／错误；Python 4 项检查、BuildCorpus、EnchantPolicy、
CollectionSources、PreviewTooltips 和新增 CollectionCache 测试通过。
后者覆盖中文／搜索／筛选／附魔字段往返、数据库／翻译／语言／WAL 变更失效、
负载篡改拒绝、无效快照不覆盖旧缓存及临时文件清理。

本轮设备 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14、2460×1080，
游戏 `1.0.12825-production-android-d9601ded`（12825）。初始在已有对局三选一界面，
随后通过 FusionCore 选择器重启，在大厅验证，没有恢复或推进对局，没有修改实际物品。
构建产物与设备 DLL SHA-256 前缀为 `adad9d899100`，启动日志确认 `android.mod.loaded`。

可重复入口 `verify_collection_cache.py --help`，分别在无有效缓存／重启后运行
`--expect cold` 与 `--expect warm`。坐标须先对照当前截图确认；脚本断言目录来源、
非空卡数、加载耗时、原生预览就绪与关闭日志，截图另行人工复核。
本次证据保存在被忽略的 `artifacts/collection-cache/`：

- `cold-final/`：已有旧构建缓存，本构建自动失效并完整读取；接受 1,850 张，拒绝 0 张，
  目录耗时 30,116 ms，确认 `cache_written`，点击乌瓦希瓦利鸟后预览就绪并关闭。
- `warm-standard/`：完全退出加载器后从选择器重启，日志仅有 `cache_hit`，
  相同 1,850 张目录耗时 839 ms，没有执行全目录重建；列表显示皮格马利翁 156 件物品，
  原生卡图与中文详情正常，预览就绪及关闭断言通过。
- `reopen-standard/` 与 `reopen-advanced/`：标准／高级模式各两轮点选、点击及长按预览、
  关闭重开；日志断言通过，没有图鉴加载／预览失败与额外原生提示观察事件。
  模式截图确认切换，测试后已恢复标准模式。

卡图、详情和关闭后无残留由截图人工复核，未声称自动视觉断言。缓存的语言／翻译／WAL
内容变更、损坏与写入失败的设备故障注入，以及管理器热替换时序仍待真机验证；
其中数据失效、损坏与无效写入保护已有离线覆盖。此次 839 ms 指目录恢复，不包括游戏启动或后续卡图资源加载。

## 图鉴关闭后原生物品提示残留修复（2026-10-22）

参考桌面 `GameInterop/CardPreview` 的提示生命周期、图鉴独立详情语义，以及当前手机版
生成代理 `CardPreviewBase` 的 `_showTooltips`、`OnHover`／`OnHoverOut` 与
`TooltipParentComponent` 的主／副提示关闭接口。本地没有可用的游戏反编译方法体，
因此未声称已对 Android 原生方法体完成根因验证；代码核对显示原图鉴关闭仅销毁卡面，
没有释放原生提示。`NativePreviewTooltips.Suppress` 在卡面获取和设置完成后关闭提示触发与
原生图形射线检测；`Release` 按原生数据指针匹配主／副提示，先解锁再关闭，避免影响其他物品。
图鉴缩略图仅在 `AndroidBuildCandidates` 的 `FillCell` 路径应用；没有修改共享桌面源码。

固定工具链构建零警告／错误，Python 4 项检查、BuildCorpus、EnchantPolicy、
CollectionSources 和新增 PreviewTooltips 回归测试通过。后者验证独立代理包装但原生身份相同、
主／副提示清理、锁定提示解锁、不关闭无关游戏提示、重复清理和未就绪路径。

本轮设备 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14、2460×1080；游戏
`1.0.12825-production-android-d9601ded`（12825）。初始在系统桌面，从 FusionCore
选择器启动；设备 DLL 与 `artifacts/mod/build-inputs.json` 的 SHA-256 一致，前缀
`5997527820eb`，本次日志确认 `android.mod.loaded` 和 `android.collection.ui_ready`。
恢复已有对局后为皮格马利翁第 2 天三选一画面，400 生命、9 金币、五件场上物品。
初始标准模式，测试后恢复标准模式；没有购买、移动、出售或修改物品，没有选择遭遇或推进战斗。

标准与高级模式分别完成三轮：打开图鉴、选择物品、点击并长按右侧卡面、关闭、重开。
标准模式查看双头刃，高级模式查看传家宝，另查看乌瓦希瓦利鸟；卡面与文字详情正常。
两种模式各补查连续选择前三张卡后立即关闭。返回游戏的截图均未见原生提示残留。
高级模式额外长按实际场上疾速火柴盒，原生详情与附魔预览正常显示，松手后关闭。
画面信息和无残留结论来自截图人工复核，不是自动视觉断言。

可重复入口 `verify_collection_close.py --help`；先在游戏设置选择模式并关闭暂停菜单，
等图鉴目录就绪后关闭，坐标必须依当前截图确认。例如本轮标准模式：

```sh
python3 bazaarplusplus-android/verify_collection_close.py \
  --serial OBJVWSPNSCFQU48L --mode standard \
  --entry 160 540 --slot 480 550 --preview 1980 450 --close 2240 75 \
  --output bazaarplusplus-android/artifacts/tooltip-cleanup/standard
```

脚本每轮等待预览与关闭日志，断言没有原生提示观察事件和失败事件，保存操作前后截图，
不声称自动识别屏幕内容。两种模式的 `validation.json` 均记录三轮通过；证据在被忽略的
`artifacts/tooltip-cleanup/`，包含 `standard/`、`advanced/`、`input-initial.png`、
`input-advanced.png`、`input-restored.png`、`rapid-closed.png`、`standard-rapid-closed.png`、
`game-native-pressed.png`、`final-session.log`、`logcat.txt` 和 `build.log`。
尚未执行场景销毁、晚完成任务和锁定预览提示的真机故障注入；锁定清理由离线回归覆盖。

## 图鉴首次加载提示（2026-10-22）

`CollectionController.BuildUi` 在卡牌区域增加“首次打开数据加载可能较慢”；
`Refresh` 仅在目录未就绪且没有加载错误时显示，完成或失败后隐藏，重开沿用缓存状态。
参考桌面图鉴的加载状态及 Android 现有目录生命周期，没有修改共享规则。
固定工具链构建通过，零警告／错误，Python 4 项检查通过。

本轮连接 `OBJVWSPNSCFQU48L`、Android 14，游戏版本 12825。
初始已有 Fusion Core 无响应弹窗；按用户要求强制停止加载器，部署新版并经选择器重启成功。
设备 DLL SHA-256 与本次构建一致，前缀 `a0e183150e74`，本次日志确认 `android.mod.loaded`。
首次打开图鉴截图可见提示，目录加载后提示消失，关闭重开显示缓存卡牌且不再显示提示。
目录投影 1,850 张，拒绝 0 张；当前英雄筛选显示 156 件物品。未恢复或推进对局。

截图人工复核证据为被忽略的 `artifacts/phone/loading-hint-visible.png`、
`loading-hint-reopened.png` 与 `loading-hint-cached.png`；不声称自动视觉断言。
`loading-hint-validation.json` 的 5 项日志断言通过，日志为 `loading-hint-session.log`。
本轮仅验证当前输入模式，未切换标准／高级模式或注入加载失败，相关边界仍待验证。

## 商店品质概率与事件详情增强（2026-10-22）

`AndroidEventPreview` 现按模板 `ECardTag.Merchant` 识别商店，与桌面
`EncounterPreviewModule` 一致。品质分布读取当前游戏模式与天数，复用
`GameDataDayTierTable`；固定单品质显示 100%，多品质集合不伪造等概率。
忽略天数表、继承品质及无法确认的排除规则保留不可计算提示。降档供货仍保留
当天品质分布，与桌面 `EncounterStructuredParser` 的分布标记一致。

事件选项通过独立客户端卡牌与原生 `TooltipBuilder` 渲染 Description／Tooltips，
显示奖励数值及游戏自带条件文字；`AndroidEncounterConditions` 适配手机版代理的
卡牌数量、标签、指定物品、品质、体型、玩家属性、英雄、天数、时段及 AND／OR 条件。
动态奖励显示已解析的卡池限制、数量和品质规则，后续遭遇递归展开并限制深度、避免循环。
未支持条件明确提示未解析，不据此判定选项可用。

本轮仍为 `OBJVWSPNSCFQU48L`、Android 14、游戏 12825。测试前为皮格马利翁
第 1 天米透／丛林遗迹／挖掘行动三选一，4 件场上物品、400 生命、17 金币。
最终设备 DLL 与 `artifacts/mod/build-inputs.json` 的 SHA-256 一致，前缀
`5890ca93fc97`；通过 FusionCore 选择器重启并确认本次 `android.mod.loaded`。
一次选择器 ANR 发生在游戏加载前，重开恢复。

最终构建在标准和高级模式分别长按三张卡，截图人工复核确认：

- 米透：显示“商店品质概率”“第 1 天：青铜 100%”及中型物品限制，日志 37 字符。
- 丛林遗迹：显示 3 件战利品、永久生命再生 +3，以及瓦内莎／卡诺克的小型白银武器或服饰、
  皮格马利翁／双龙的 2 收入、马克的白银原料、杜利／斯黛尔的小型白银工具、朱尔斯的中型青铜食物，
  英雄条件与奖励数值均可读；日志 914 字符。标准模式在浮层内滑动后内容仍显示。
- 挖掘行动：显示随机物品、遗物限制及固定青铜 100%，与相邻原生描述一致，日志 40 字符。
- 高级模式松开关闭；标准模式保留原生放大详情，再次长按放大卡图可关闭。标准模式首次
  切到遗迹的操作实际只关闭上张卡详情，因此没有新渲染日志，断言如实失败；确认关闭后重试通过。

`verify_encounter_preview.py` 是新增的可重复入口，参数见 `--help`；坐标必须来自刚复核的截图。
它只发送 5 秒长按，保存操作前、按住中及松开后截图，并对同一日志会话、新预览渲染、
无网络操作命令、无对局状态切换、无预览异常进行断言。最终六个有效场景的五项断言均通过。
视觉内容属于截图人工复核，不是自动视觉断言。证据为被忽略的
`artifacts/phone/event-final-*.png`、对应 JSON／日志及 `event-final-validation.log`。
本轮仅恢复现有对局，最终日志除恢复用 `InitializeRunCommand` 外没有操作命令，
没有选择遭遇、购买、退出事件或推进战斗；结束时恢复高级模式并保留同一三选一场景。

离线通过固定工具链构建（零警告／错误）、Python 4 项检查、CollectionSources、BuildCorpus、
EnchantPolicy 及新增 `EncounterPreview.Tests` 品质分布场景。没有修改桌面源码。
仍待验证其他天数的混合概率、固定高品质商店、物品持有条件、复杂随机链、深层遭遇和真实领取
奖励后的数值对照；未支持的条件类型、特殊品质规则及超过深度限制的后续内容仍会明确降级。

## 商店与事件预览专项检查（2026-10-22）

本轮在设备 `OBJVWSPNSCFQU48L`、Android 14、游戏版本
`1.0.12825-production-android-d9601ded` 上检查现有构建。设备 DLL 与本地
`artifacts/mod/build-inputs.json` 的 SHA-256 一致（前缀 `4d7663b956e0`），未重新部署。
初始为第 1 天吉娜、茸茸小怪兽、安保中心三选一，高级输入模式。

ADB 长按三张卡并在按住时截图，人工复核及日志检查确认：

- 茸茸小怪兽显示爱抚、吓唬、抓住它、为它奏一曲、玩捉迷藏、投喂、玩耍七个结果；后五项标有条件。日志记录 77 字符。
- 吉娜仅显示“100% 按条件抽取的卡牌池”，日志记录 34 字符，具体非武器物品未展开，概率语义未验收。
- 安保中心仅显示“按条件抽取的卡牌池”，日志记录 23 字符，具体青铜护盾物品未展开。
- 松开后浮层消失，切换后内容对应各自选项。本轮未见观察器异常。这是截图人工复核，不是自动视觉断言。

尝试切换标准模式时，设置列表移动，点击未选中标准模式；操作前未充分核实，
随后在仍为高级模式下误选茸茸小怪兽。日志记录 `SelectEncounterCommand`，
随后又发生 `ExitCurrentStateCommand`，由 `ChoiceState` 经 `EncounterState` 进入
`LevelUpState`。这是本次自动操作造成的非预期状态变更，不能记为“未选择事件或推进对局”。
发现后停止设备操作，最终截图为等级提升画面，未继续选择升级奖励。
标准模式未通过；文件名含 standard 的截图不能作为该模式通过证据。

截图在被忽略的 `artifacts/phone/shop-event-*.png`，日志在
`artifacts/phone/shop-event-validation.log`。具体供货池、完整条件过滤、事件奖励数值和
进入商店后的自动来源识别均未验证。本轮未修改插件源码，不能标为预览功能完整通过。

## 运行环境

2026-10-20 至 2026-10-22，在 Redmi Note 11T Pro、Android 14、arm64-v8a 上验证。
游戏包为 `com.tempogames.thebazaar`，版本为
`1.0.12825-production-android-d9601ded`（12825）；Unity 为 `6000.3.11f1`，
IL2CPP metadata 为 39。其他游戏版本和设备尚未验证。

加载器、框架压缩包和 SDK 固定在 `../toolchain.json`。FusionCore 使用独立应用数据，
首次登录需要在手机手动完成；原版游戏的安装和数据保留。

本次使用与游戏 Unity 版本匹配的
[FusionCore Unity 依赖](https://github.com/All-Of-Us-Mods/FusionCore.UnityDependencies/releases/tag/6000.3.11f1)。
原生库和符号库由加载器缓存；托管 Unity 库提供给 IL2CPP 代理生成器。
该历史通用加载器流程需要下载；当前专用启动器改为构建时内置，验收见本文件开头。
游戏 APK、代理程序集、依赖压缩包、设备记录和截图均留在被忽略的 `artifacts/`，
不作为模组源代码分发。

## 当前实现

`android-mod/Plugin.Load` 使用 IL2CPP 插件入口，注册 Unity 帧回调和持久清理组件。
`AndroidCollectionCatalog.Start` 在已附加 IL2CPP 的工作线程读取静态卡牌字典；
`AndroidCollectionCatalog.Tick` 在主线程分批创建 CLR 快照。分类、包裹身份、筛选和
搜索规则通过项目文件链接桌面代码，Android 层负责原生集合转换和触屏视图。

`CollectionController` 的右侧详情在同一时刻只创建一张原生预览，网格缩略图独立管理。原生 `GenerateUICard` 返回预制体后，
单独等待原生 `SetUp` 完成，再显示卡面与工具提示数据。关闭或换卡会取消请求并隐藏准备对象；
版本校验丢弃过期结果。Controller 销毁时，`NativePreviewCleanup` 在主线程观察晚完成的任务、
销毁返回对象并释放取消源。场景切换自动关闭图鉴。

`AndroidCardVariants` 创建带独立 ID、原生 Nullable 和独立 Attributes 字典的合成卡牌实例，
交给原生四参数 `SetUp` 计算品质与附魔效果；它不改写目录模板或玩家实例。
可选变体和本地化名称由 `CardTooltipData.BuildLockVariantOptions` 提供。
请求开始时捕获整个实例，随后切换按钮不会改变已在执行的请求参数。

| 功能 | 适配状态 |
|---|---|
| 加载入口、Unity 帧回调 | 真机已验证 |
| 游戏静态卡牌目录、中文名称 | 真机已验证 |
| 物品／技能、起始品质、体型、英雄筛选 | 已接入共享筛选引擎 |
| 搜索、清空、分页 | 已实现；英文查询命中中文卡名已验证 |
| 小／中／大型物品及技能原生卡面 | 真机已验证 |
| 卡牌基础效果描述、原生属性图标、触屏滚动 | 已实现；效果描述和图标真机已验证 |
| 预览品质、附魔变体 | 真机已验证原生品质数值、附魔效果与恢复 |
| 来源列表、对局日期过滤 | 已接入共享供货池及游戏模式天数表；新版当前商店识别仍待商店场景验收 |
| 对局物品原生长按详情及附魔下拉预览 | 关闭新增钩子后真机已验证 |
| 对局物品 Tooltip 同时展示多种附魔信息 | 标准和高级模式的果篮 13 种效果已由用户确认可见、可滚动、松开后关闭 |
| 构筑推荐 | 官方 5.7.0 内置快照、皮格马利翁匹配结果、十格卡图、小尺寸场上候选卡图及选择交互已真机验证 |
| 对局升级预览 | 用户在高级输入模式确认显示并滚动查看 4 档更高品质描述；具体数值待比对 |
| 物品任务奖励、事件结果与英雄等级奖励提示 | 事件详情已在双输入模式局部真机验证，见预览详情增强节；物品任务及英雄等级奖励仍待验证 |
| 回放、视频录制、历史、上传 | 尚未移植 |
| 战斗状态栏、战后影响分析、语音字幕 | 尚未移植 |
| 匿名模式、主播模式 | 按用户要求暂缓 |
| 传奇名次显示 | 尚未移植 |
| 游戏设置中的中文术语模式及其他模组选项 | 尚未接入；图鉴文字目前跟随游戏语言，中文采用大陆术语 |
| Android 安装器和自动更新 | 尚未提供 |

## 验证方法与证据

Python 检查工具的四项测试覆盖设备选择、包名选择、split APK 合并和运行时识别。
`build_probe.py --interop … --mod` 成功构建加载探针、运行时探针和图鉴，编译无警告或错误。
构建输出 `artifacts/mod/build-inputs.json` 记录插件、框架和每个代理程序集的 SHA-256。

设备日志先后出现 `android.mod.loaded`、`android.collection.ui_ready` 和
`android.collection.catalog_ready`。游戏静态字典有 3,375 个模板，投影接受 1,850 张，
投影异常为零；共享筛选排除包裹等内容后，全部英雄视图显示 1,243 个物品和 517 个技能。

手动通过 ADB 触屏操作验证了 `gloves` 搜索得到「举重手套」「无指手套」、× 清空、
英雄／起始品质／体型切换、小／中／大型物品和技能预览、重置筛选、翻页、
连续十次关闭后重开，以及切至系统桌面后从最近任务恢复并继续换卡。
这些操作没有进行购买、战斗、对局选择或上传。

同步异常注入和生成途中销毁 Controller 的故障场景仅完成独立源码复核，
尚未作为真机故障注入测试通过；不能把正常切卡验证当作这些故障场景的证据。
长描述滚动到末尾的专项触屏验证尚未完成。

## 品质与附魔验证

2026-10-21 的变体验证使用同一设备与游戏版本。举重手套预览从青铜切为白银后，
伤害加成从 3 变为 6；选择炽焰后，原生卡面出现火焰效果与 4 灼烧，描述同步增加灼烧效果。
循环附魔回到「无」后，额外效果消失，保留白银基础数值。

连续切换品质与附魔、关闭重开后，再次选择同一模板恢复青铜无附魔及数值 3。
日志中同一模板的 StartingTier 一直为 Bronze；所有已完成请求的 requested_tier 与
actual_tier 相符，覆盖青铜、白银、黄金、钻石。切到专家飞行员技能后，附魔选项数为零，
不继承此前物品附魔；该技能的青铜与白银预览均成功。

原生空附魔 Nullable 的生成代理会对空装箱结果直接构造包装器，造成空引用异常。
`NativeNullableValue.Read` 在此 getter 边界直接调用同一原生方法，
先处理空指针，再按实际原生类解包枚举或 Nullable，不修改选项生成策略。
该适配已通过上述空附魔和非空附魔真机路径。

本次过滤后的日志和逐请求品质比对结果保存在本机被忽略的
`artifacts/phone/variant-validation.log` 与 `variant-validation.json`。

## 对局详情崩溃对照

旧版 `AndroidEnchantTooltip.Install` 使用原生 IL2CPP Harmony patcher 注册五个钩子；
启用后查看皮格健身房出现 UnityMain SIGSEGV。后续阶段日志又定位到
`ReadOnlyDictionary<EEnchantmentType,TEnchantment>.GetEnumerator`；改用枚举值逐项
`ContainsKey` 查询后，同一物品的 13 种附魔效果完成计算且玩家实例未改变。

观察式浮层曾在标准模式显示这些效果，原生下拉选择「炽焰」后内容随之更新，
滚动和关闭详情亦已手动检查。之后调整了浮层层级以避开暂停菜单，但手机断开连接，
当时的层级调整未完成复测，不能将它或高级模式记为通过。上述操作没有购买物品、
附魔实际物品、选择事件或推进战斗。截图和日志仅存于本机被忽略的 `artifacts/`。

2026-10-22 的高级输入模式复测中，手机处于选择野怪的画面，尚未进入战斗。
左侧新增「长按预览：附魔／升级」按钮，长按前切换并持久保存选择。
附魔日志确认两件场上物品分别计算出 12、13 种候选效果。升级初版使用
`CardTooltipData.GetCardDescription`，实际返回空，用户只看到「暂时无法生成升级描述」。
改为按模板 Tooltip 条目逐条渲染后，日志确认余烬从青铜投影到白银，产生 1057 字符描述，
原物品品质和属性摘要未变化；用户确认看到了升级内容，并要求同时展示所有更高品质。
代码已扩展到枚举当前品质以上、模板中存在的每一档。真机日志记录果篮从青铜
生成 4 档描述，共 4294 字符，原物品未变化；用户确认全部 4 档可见且能滚动。
尚未逐项与真实升级后的数值比对，也未验证标准输入模式。

随后用户将输入模式切至标准，长按果篮确认多附魔浮层可见、可滚动，松开后关闭；
再切回高级，重复验证同样的交互。日志分别记录 `locked=True` 与 `locked=False`，
均生成果篮的 13 条附魔效果（4963 字符），无观察器异常。其他物品和实际附魔数值
仍待对照。

构筑推荐点击后曾发生原生 SIGSEGV。阶段日志定位到商店候选中访问
`Data.Entities.TryGetValue` 的 IL2CPP 代理；改用游戏原生 `Data.GetCard` 后，
面板和「余烬」「果篮」场上候选在真机显示且未再闪退。当前英雄皮格的本地构筑库无样本，
因此仍不能确认匹配结果的显示和排序。修复后已移除逐秒阶段日志。
再次打开面板，点选「余烬」后候选数由 2 变为 1；来源按钮依次显示仓库 (0) 和
商店 (0)，均无闪退。关闭面板后回到原来的选怪画面。

2026-10-22 新版 `2345c56` 在同一台手机重启后加载。用户打开构筑推荐时，截图确认
场上显示「余烬」「果篮」两个已选候选，当前英雄为皮格马利翁；在线构筑库日志显示
共 3 条构筑，生成于当天，但没有皮格马利翁样本。面板因此显示明确的无数据提示，
并收起空白阵容区与切换按钮。通过 ADB 取消再选中「余烬」，候选数 2→1→2；
切到空商店后仍显示无数据提示，再切回场上。上述操作未见
`android.build_panel.failed` 或 `android.build_board.card_failed`。原生卡面的十格阵容与
前三条切换仍需在有样本的英雄对局中真机验证；本次没有更换英雄或推进对局。

同一选怪画面长按三只野怪，不选择或交战。野怪浮层使用游戏静态怪物模板读取
生命、物品和技能，物品区复用图鉴已验证的原生卡牌渲染器。蕉宝、獠牙英格狼、
毒蛇分别显示 4 张原生物品卡面，重复物品按实际数量分别显示；加载日志各有
4 条 `android.monster_gallery.card_ready`，截图人工确认未缺卡。蕉宝的生命值
100、4 件物品、1 个技能与卡面同时可见；三只野怪的战斗金币和经验分别为
2／3、2／3、3／3，与原生侧栏一致。当时的旧版在长按松开后关闭浮层，
当前对局仍停在选怪画面。
此轮只覆盖高级输入模式及单行四卡阵容；多行阵容和标准模式仍需后续验证。

按用户后续要求，野怪浮层改为默认只显示卡图。每张卡的图像宽度按
`Screen.width / 26` 计算，并等比缩放高度；手机屏宽 2460 像素时日志记录
`card_width_px=94.6`。蕉宝的 4 张卡均加载成功，松开长按后小型卡图继续显示；
点击医疗包后出现对应效果说明，点击浮层外的空白处后浮层关闭且没有选择野怪。
浮层拥有透明触控遮罩和 × 关闭入口，避免为了关闭而误触交战卡。此版详情点击
与关闭只在高级输入模式的蕉宝上真机确认。

随后复查发现，野怪卡位以左上角为原点，但原生画框按中心点定位，导致最左侧
物品越出浮层。`AndroidMonsterCardGallery.Show` 现按模板体型分配 1／2／3 格、
最多十格一行；`AndroidMonsterCardGallery.Fit` 同时按画框宽高缩放，并将中心
对齐卡位中心。2460 像素屏宽下每格仍为 94.6 像素。用户已在真机自行确认
物品展示不再越界；本次未推进对局。多行及混合尺寸阵容仍待遇到对应野怪时验证。

2026-10-22 后续检查发现，在线 `builds/latest.json` 只有 3 条多利样本，
没有皮格马利翁样本；官方 5.7.0 桌面发行包内置的同格式快照却包含
5,689 条构筑、其中皮格马利翁 834 条。Android 插件现嵌入该快照，
并在在线数据明显缩水时保留它。手机日志记录
`android.builds.bundled count=5689` 和
`android.builds.remote_rejected incoming=3 retained=5689`，面板显示数据生成日期
2026-10-06。来源及校验值见 [内置快照说明](../data/README.md)。

同一选怪画面中，皮格马利翁推荐阵容的 7 件物品均加载出游戏原生卡图，
按其体型占满十格；曾出现卡图短暂变白后退回占位的现象，原因是预览根布局
为 `0×0`。给根布局预设尺寸并以实际画框尺寸缩放后，连续截图确认卡图
稳定且处于各自槽位。点击推荐物品显示原生效果详情。随后场上「余烬」和
「果篮」两个候选也显示小尺寸卡图；点击余烬卡图后，选中标记消失，
匹配数从 `1/2` 变为 `1/1`，再次点击恢复。日志有两条
`android.build_candidates.card_ready`，未见相关失败。未选择野怪或推进对局。

## 待验证项

离线复核桌面版 `LevelUpPreviewTextFormatter.Build` 后，Android 英雄升级奖励列表已改为
「可能出现的奖励选项」；候选组的权重不再显示成直接获得某奖励的概率。带前置条件的
结果会标为「有条件」。该修正只通过编译，尚未在英雄升级提示中真机比对。

以下均未通过，不能以离线编译代替：

1. 在「移动设备 → 输入模式」分别选择标准和高级。果篮的附魔列表、滚动与松开关闭已通过；
   还需验证其他物品、原生附魔切换后的内容更新、暂停菜单时浮层消失。
2. 在两种模式的物品详情里切换「升级」，逐项比对所有可用的更高品质描述、冷却和属性数值；
   退出后确认玩家原物品的品质、附魔和属性未改变。特别检查受其他物品光环影响的物品。
3. 查看物品任务奖励、随机事件、普通事件及英雄等级提示，核对奖励文字、结果名称、后续遭遇、生命上限、格数和
   奖励候选；对照实际游戏选择，记录不支持的筛选或条件，不能仅以列表可见判定通过。
4. 构筑推荐已有皮格马利翁快照样本及十格卡图通过真机检查；继续验证前三条切换、
   匹配排序、非空仓库／商店、候选分页、缓存、断网、图鉴与推荐面板互斥、关闭重开及
   长列表触屏滚动，并观察在线数据恢复后的更新行为。

## 图鉴来源、选择控件与按体型占格网格（2026-10-22）

`AndroidCollectionSources.Load` 读取与桌面同源的嵌入目录，Android 只适配 JSON 和原生集合；
供货池匹配复用 `CollectionSourceOfferPoolResolver`，天数上限和固定品质来源豁免复用
`CollectionFilterEngine` 与 `GameDataDayTierTable`。`AndroidCollectionContext` 优先匹配当前遭遇，
再解析选择项；实例 ID 经 `Data.GetCard` 转为模板 ID，未重新引入已发生原生崩溃的
`Data.Entities.TryGetValue`。大厅残留的对局对象不作为有效对局，无法读取天数时不额外排除卡牌。

`CollectionController` 复用桌面 `CollectionGridLayout`：物品宽度为 1／2／3 格，等高并自动换行；
技能为 7 列方格。分页按完整行划分，不再按固定卡牌数量切断一行。英雄、品质、体型、来源、
天数及预览变体使用可滚动的直接选择弹窗，点击外部遮罩关闭且不穿透。缩略图按实际卡图区域
缩放；`NativePreviewCleanup.Retire` 可接管完整缩略图父节点，等待创建与设置任务结束后再销毁，
避免快速翻页、关闭或销毁面板时提前销毁原生异步任务仍引用的父节点。

新增离线入口：

```sh
bazaarplusplus-android/artifacts/tooling/dotnet/dotnet run --project bazaarplusplus-android/tests/CollectionSources.Tests
```

该测试覆盖当前手机版枚举下的 74 个来源、来源身份唯一性、英雄／中立／武器商店卡池，
来源与天数的交集、手动品质与天数的交集、固定品质来源豁免、缺失或无效天数权重，
混合尺寸等高占格、不重叠、不越过十列、末行完整及技能布局。Android 构建、Python 检查、
构筑数据与附魔策略测试也已通过；未修改桌面源码。

本次连接设备仍为 `OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14，游戏包及版本
与本记录顶部一致；测试前为皮格马利翁第 1 天的选怪画面，没有购买、选择野怪或推进战斗。
新版已在真机显示混合小／中型单元格和大型占格、完整末页与点击物品详情；原生天数表解析为
`Bronze`，同英雄条件从不限定天数的 156 件缩小为第 1 天的 71 件。
这些截图为人工复核，不是自动视觉断言。

最新构建和设备 DLL SHA-256 保存在本机被忽略的 `artifacts/mod/build-inputs.json`；
本次截图与日志保存在 `artifacts/phone/collection-*.png` 及后续验收日志。
最新 DLL 已与设备文件核对 SHA-256，前缀为 `4d7663b956e0`，并核对本次启动的
`android.mod.loaded` 与 `android.collection.ui_ready`。标准模式验证了直接选择来源与品质、
不限天数 156 件／第 1 天 71 件、Goldie 的固定品质来源 153 件及黄金起始品质 29 件，
改为 Jay Jay 后黄金与第 1 天交集为空，界面显示空结果。大型筛选显示 4 件物品，
技能页显示 27 个技能并隐藏体型条件；快速翻页和关闭无相关失败日志。

恢复测试前的高级模式后，验证小／中型混排和大型三格卡图、点击壶铃详情、预览品质
直接选择白银、附魔直接选择炽焰；原生描述从基础伤害加成 10 更新为 20，并附加 4 灼烧。
日志确认 requested_tier 与 actual_tier 均为 Silver、模板起始品质仍为 Bronze。
来源弹窗滑动可访问后面的商人，点击滚动后的来源实际选中 Mr. Morland，过滤为 6 件。
遮罩关闭和关闭重开正常。这些操作没有改变实际对局卡牌或推进对局。

`artifacts/phone/collection-validation.json` 保存 9 项日志断言的通过结果，包括天数开关、
来源豁免、手动品质交集、空结果、技能数量、白银炽焰变体及无相关失败日志；
卡图比例与位置仍属于截图人工复核，没有自动视觉断言。
当前仍缺实际商店场景的自动识别证据、不同天数切换及异常注入验证，不能标为全功能真机通过。
重启加载器过程中曾在 FusionCore 游戏选择页出现一次 ANR，发生在游戏和插件加载前；
关闭后重开选择器恢复，未将其当作插件异常或通过证据。

## 构筑候选按钮布局调整（2026-10-22）

参考桌面 `LiveBuildPanelView` 的原生卡面与独立候选标记语义，Android
`AndroidBuildPanelController.BuildUi` 将五列两行、左图右字按钮改为十列单行，
卡图在上、名称在下，右上角用勾选／空圈独立显示选择状态。每页仍为十件物品，
保留候选身份、来源切换、分页和匹配规则；名称自动缩小并在超长时省略。
`AndroidBuildCandidates.Show` 为名称预留底部空间，原生卡图等比约束在上方。
按钮宽度约为旧版的 47%，卡图区高度约为旧版的两倍。

首次部署后 FusionCore 选择页出现 FocusEvent 超时 ANR，未进入新版游戏；
当时未将旧加载日志记为通过。随后用户恢复加载器，本次继续验证设备
`OBJVWSPNSCFQU48L`、Redmi Note 11T Pro、Android 14，游戏版本
`1.0.12825-production-android-d9601ded`（12825），横屏 2460×1080。
设备 DLL 与本次构建记录 SHA-256 一致，前缀 `a0e183150e74`；核对当前会话
`android.mod.loaded`，且新版布局已实际显示。本次固定工具链重建零警告／错误，
SHA-256 与设备保持一致；此前 Python 4 项检查、BuildCorpus 解析与召回测试通过。

恢复现有对局后为皮格马利翁第 1 天商店：场上展品柜、火柴盒、果篮、獠牙四件物品，
仓库巧克力棒一件，商店铁腕战士、回旋镖、高尔夫球杆三件，400 生命、17 金币。
标准与高级输入模式分别验证：

- 小／中型原生卡图放大且保持比例，名称完整可读，右上角标不遮挡卡图或名称。
- 点击展品柜卡图反选，候选 4→3、匹配 2/4→2/3；点击名称复选，恢复 4 与 2/4。
- 切换仓库，点击巧克力棒角标，候选 4→5；切换商店，点击高尔夫球杆名称，候选 5→6。
- 切换来源保留选择；关闭重开恢复场上四件默认候选和 2/4 匹配。标准模式另连续切换来源六次后关闭重开，无相关异常。

名称、比例、角标和候选数均为截图人工复核，不是自动视觉断言。
`artifacts/candidate-layout/validation.json` 的八项自动日志／校验值断言通过：
同一日志会话、新版插件加载、设备与构建一致、八种候选均加载、无面板／卡图失败、
无新增插件错误、仅恢复对局的 `InitializeRunCommand`、未见对应游戏进程崩溃。
本轮未购买、移动或出售物品，未刷新商店、选择遭遇或推进战斗。
结束时恢复高级输入模式，并打开新版构筑面板供查看。

证据保存在被忽略的 `artifacts/candidate-layout/`：`board-initial.png`、
`board-deselected.png`、`stash-selected.png`、`shop-selected.png`、`standard-*.png`、
`input-mode-initial.png`、`input-standard.png`、`input-restored.png`、`final.png`、
`validation.log`、`validation-logcat.txt`、`validation.json` 与 `build-current.log`。
当前场景未覆盖超过十件候选的分页、空来源、极长名称省略、其他语言及其他横屏比例，
这些边界仍待验证；没有为验收改变对局库存。


## 长按升级预览双模式失效修复（2026-10-23）

参考桌面 `UpgradePreviewAttributeMultiplierResolver.Targets`、预览和输入架构文档，
并核对本手机版生成代理 `TTargetCardBase.GetTargets` 的 IL：第二参数
`Il2CppSystem.Nullable<uint>` 经 `Il2CppObjectBaseToPtrNotNull` 后拆箱。
旧 `AndroidUpgradeAuraResolver.Targets` 传入空引用，设备日志显示升级预览在此抛出
`NullReferenceException`；附魔预览不经过此路径，两种模式均正常。
修复复用 `NativeNullableValue.Box<uint>(null)` 表示无目标数量上限，保留光环计算规则。
不修改桌面源码，不重新引入原生 Harmony 钩子。

设备为 22041216C，Android 14，分辨率 2460×1080，游戏
`1.0.12825-production-android-d9601ded`；测试前在高级输入模式、已有皮格对局的选怪画面。
插件构建零警告／错误，Python 9 项测试、BuildCorpus 和 EnchantPolicy 通过。
签名启动器构建及覆盖安装成功；`verify_launcher.py --expect-loaded` 的 12 项断言通过，
确认 APK、磁盘插件、当前会话握手和清单的 DLL SHA-256 一致：
`e6222f33eb40dd7492ec44066aeb9e00424eb4194630d9add9889e13e6e700b9`。
启动器每次准备会恢复内置插件，因此最终以包含修复 DLL 的 APK 验收，未以 push 成功替代加载证据。

ADB 按当次截图定位，分别在标准和高级模式长按壶铃、公文包、回复多肉植物，
六个有效用例各生成四档升级描述。每例日志自动断言生成成功、原物品品质及属性摘要未变、
无预览异常、无对局命令及状态转换；截图人工复核各档文字。
标准模式壶铃向下滑动可见传奇档；点击左侧空白处关闭原生放大详情和升级浮层。
高级模式三例松开后浮层消失。一次标准模式公文包操作被仍打开的放大详情遮挡，
未产生渲染日志，断言如实失败；关闭原生详情后重试通过，不计入上述六个有效用例。
未购买、移动、出售或实际升级物品，未选择野怪或推进对局，结束恢复高级模式和「长按预览：升级」。
当前启动日志及所采 logcat 未见预览错误或原生崩溃。

证据保存在被忽略的 `artifacts/upgrade-fix/`：旧版 `before.log`、
`target-proxy-il.txt`、构建与离线测试日志、`launcher-check/verification.json`、
`advanced-*-holding.png`／`released.png`、`standard-*.json`／截图、
`standard-scrolled.png`、`standard-dismissed.png`、`restored-mode.png`、`final.log` 和 `final-logcat.txt`。
本轮确认空引用故障修复及双模式展示；实际升级后的数值对照、所有光环组合和其他设备仍待验证。

## 升级品质上限与长按「无」选项（2026-10-23，跳过真机）

按用户要求，本次未连接或操作设备，不执行覆盖安装、双输入模式或实物升级验证。
此前“四档描述”和滚动至传奇档仅证明展示路径可用；普通物品出现传奇升级目标是本次修复的错误。

`AndroidUpgradePreview.Build` 改用 `AndroidUpgradeTierPolicy.TargetTiers`，只枚举高于当前品质、
且不超过钻石的候选，再由现有模板查询和独立卡牌副本生成效果。青铜、白银、黄金、钻石、传奇
五种起始品质的回归用例通过，钻石和传奇均无更高升级候选。

`AndroidLongPressPreviewSettings` 以原偏好键保留 0=附魔、1=升级，新增 2=无，
按钮按「附魔 → 升级 → 无」循环并保存；非法值回退附魔。`AndroidTooltipObserver.Update`
在「无」模式跳过物品附加预览并隐藏已有浮层，保留游戏原生详情；模式变化会重新解析当前物品。
事件、野怪和英雄等级预览保持原有入口。缺省值、旧 0/1、新 2、非法 -1/99 的加载、循环和保存
均通过使用生产设置类与 PlayerPrefs 替身的离线回归。

Python 9 项检查、BuildCorpus、EnchantPolicy 和上述新增回归通过；Android 插件编译零警告、零错误。
签名 APK 与摘要见被忽略的 `artifacts/launcher/`，插件构建输入见 `artifacts/mod/build-inputs.json`，
构建日志位于 `artifacts/upgrade-fix/`。
两项改动的真机展示、关闭和重启后设置保留仍待验证，不标记为本次真机通过。

## 启动器暮紫琥珀 UI（2026-10-23）

参考最近提交的 `docs/design/launcher-concept-c.png`、启动器 ADR-0001、
`LauncherActivity` 的任务恢复保护与 `LauncherPreparation.loadStatus` 的会话语义。
首页改为 `activity_launcher.xml` 原生布局：独立集市插画、英文 BazaarPlusPlus 品牌、
启动前状态、琥珀主按钮与并排的重新检查／帮助；普通界面隐藏诊断和日志入口。
帮助提供安装、授权、修复、许可与退出确认；许可独立读取 NOTICE、MIT 与 GPL 正文，
不依赖诊断报告。「可以启动」仅代表启动前检查，初始化及游戏加载仍走原有校验链。
启动异常另有持久到用户关闭的弹窗，避免原因被周期刷新覆盖。

本次设备 `OBJVWSPNSCFQU48L`（22041216C / Redmi Note 11T Pro）、Android 14，
游戏 `1.0.12825-production-android-d9601ded`，竖屏 1080×2460、横屏 2460×1080。
测试前设备在系统桌面，已安装游戏且文件授权已开启。使用原签名覆盖安装本次 APK，
未修改游戏数据、未进入对局、未切换游戏输入模式或执行购买／遭遇选择。
启动器独立于游戏输入模式，本轮未重新验收游戏内 UI。

- Python 9 项离线检查、签名 release 构建及 APK 签名／内置输入校验通过。
- `artifacts/launcher-ui/verify_ui.py` 的 16 项 UI XML 断言通过：品牌与主操作、
  无旧中文品牌及诊断入口、刷新恢复、安装／授权／修复取消／许可弹窗、许可不含诊断 JSON、
  横屏滚动与帮助、大字体操作可达。横屏与 130% 字体的截图另经人工复核，
  不是自动视觉断言。字体变化由系统重建首页，旋转继续使用同一份自适应布局。
- `artifacts/launcher-ui/verify_launch.py` 的 3 项断言通过：现有文件授权设置页返回就绪、
  退出有确认并可取消、主按钮进入游戏 Activity；没有撤销授权或执行实际修复。
- `verify_launcher.py --expect-loaded --not-before-ms …` 的 12 项断言通过，
  核对当前 APK、清单、插件 SHA-256 与本次会话握手；不代表游戏登录或插件功能已验收。
- `verify_launcher_resume.py` 三轮的 9 项断言通过，ActivityRecord、完整任务栈和 PID 均保持。
  测试结束关闭测试启动的游戏并重新打开首页，系统恢复原来的自动旋转及 100% 字体设置。

证据在被忽略的 `artifacts/launcher-ui/`：`build.log`、`offline-tests.log`、
`validation.json`、`launch-ui-validation.json`、`loaded/`、`resume/`、
`portrait.png`、`landscape-bottom.png`、`large-font-top.png`、`large-font-bottom.png`、
`permission-return.png`、`licenses.png`、`final.png` 及 `logcat.txt`；
本地重复操作脚本为同目录的 `verify_ui.py` 与 `verify_launch.py`。
APK、签名报告、发行清单及对应源码包仍在 `artifacts/launcher/`。
无游戏安装、未知版本、撤销授权、空间不足、实际修复、超大字体、平板及第二设备未做本轮故障注入，
不能将正常路径结果扩展为这些场景通过。


## 首次启动语言初始化与固定英文文案（2026-10-23）

参考桌面本地化、图鉴、面板和输入架构文档，核对 Android 生成代理的
`PlayerPreferences.Data`／`PreferencesData.LanguageCode` 与 `LocalizationService`。
本地未提供桌面反编译源码，因此未据此声称确认手机版原生初始化内部实现。
原 `Plugin.Load` 的语言提供器在偏好未就绪时返回英文，而两个 Controller 仅等待字体，
`BuildUi` 一次性写入的按钮与标题不会随稍后到达的语言值刷新。

两个 Controller 现等待有效语言偏好，固定文字由 `AndroidLocalizedText` 在显示时解析，
值未变化时不重复写入 TMP；筛选值等动态文字仍由原视图状态刷新，避免覆盖选择。
图鉴检测语言变化时清理旧预览、刷新控件并使目录语言失效；构筑英雄显示名改用
`LocalizableShared.GetLocalizedHero`，查询仍使用原英雄标识。共享桌面源码未修改。

设备 `OBJVWSPNSCFQU48L`（22041216C）、Android 14、2460×1080，游戏
`1.0.12825-production-android-d9601ded`。测试前为启动器首页，保留游戏登录和现有对局。
签名 APK 覆盖安装后冷启动，最终插件 SHA-256：`c4f43150d9bab94dcb438a6fe72898e0fa314fe98635362f8aa60a46dbfd8f30`。
`verify_launcher.py --expect-loaded --not-before-ms` 的 12 项断言通过，确认本次新会话、
APK／磁盘／握手插件摘要及入口日志一致。新增组件注册、无托管回调封送警告、无插件
UI tick 错误的 3 项日志断言通过；框架既有通用钩子警告不作为本修复引入的问题。

ADB 按当次截图打开、关闭并重开图鉴和构筑面板；三个入口、标题、重置、关闭、物品／技能、
搜索占位、加载提示、翻页和构筑英雄名经截图人工复核为中文，不是自动视觉断言。
选择白银后筛选按钮保持「品质：白银」，未被固定文案绑定覆盖。
当前停留大厅，构筑为空候选路径；没有继续对局、购买、选择遭遇、清除数据或变更游戏输入模式。

本地化生命周期测试执行生产绑定组件与共享本地化引擎，覆盖无偏好英文回退→延迟中文、
切回英文、动态模式更新、未绑定实例及相同文字不重写；Python 9 项检查、BuildCorpus、
EnchantPolicy 通过，插件编译零警告／错误。
证据在被忽略的 `artifacts/localization-fix/`：构建与测试日志、`final-loaded/verification.json`、
`ui-log-assertions.json`、`final-collection.png`、`final-builds.png`、`tier-selected.png` 和 `final.log`。
全新无偏好安装、真实首次语言初始化时序、双输入模式及游戏内热切换仍待真机验证；
未清除账号／游戏数据来模拟首次安装，因此本项不标记完整真机验收，不作已验收功能提交。

## 启动器帮助页与首次安装引导（2026-10-23）

参考启动器 ADR-0001、`LauncherActivity`、`BootstrapActivity` 的准备与授权流程，
以及 https://playthebazaar.com 的深棕、暖金、衬线品牌和分隔装饰。
`LauncherHelpActivity` 与 `activity_launcher_help.xml` 提供原生离线帮助页：
首次安装五步、图鉴／长按预览／构筑说明、授权与初始化等常见问题。
首页独立「帮助」直接打开页面；原菜单移至「更多：修复与许可」，修复仍经原有确认流程。
顶部、底部和系统返回均结束帮助 Activity 回到首页，不自动请求授权或启动游戏。

本次设备 `OBJVWSPNSCFQU48L`（22041216C / Redmi Note 11T Pro）、Android 14，
游戏 `1.0.12825-production-android-d9601ded`（12825）。验证前为已有皮格对局界面；
使用原签名覆盖安装新 APK 后仅验证启动器，没有推进对局、购买、选择遭遇或清除数据。
Android CLI 截图成功，布局服务因无法安装 instrumentation APK 失败，故回退 ADB 的
UI Automator XML 与实时截图定位；点击坐标由当次控件边界计算。

Python 9 项离线测试、签名 release 构建、APK 签名及内置输入校验通过。
页面导航、五步与常见问题内容、横屏滚动、130% 字体及原菜单的设备检查结果保存在
被忽略的 `artifacts/launcher-help/validation.json`；截图另作人工复核，不视为自动视觉断言。
旋转与字体设置在测试后恢复原值。代码自查与 `git diff --check` 通过。

证据在被忽略的 `artifacts/launcher-help/`：`build.log`、`offline-tests.log`、
`verify_ui.py`、`continue_ui.py`、`validation.json`、`portrait-top.png`、
`portrait-guide-*.png`／XML、`landscape-*.png`／XML、`large-font-*.png`／XML，
以及 `ui-test.log` 和 `ui-continuation.log`。首次脚本因滚动跳过部分文字、
以及底部按钮仅露出边缘时过早点击而失败，修正扫描与可见高度判断后继续验证。
本轮未重新验收实际授权、初始化、登录、修复或插件加载；全新安装、第二设备、
平板与更大字号仍待验证，帮助内容不等于这些流程本次均已通过。
