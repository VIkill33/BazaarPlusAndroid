# BazaarPlusPlus Android

当前提供实验性 Android IL2CPP 游戏内图鉴、运行时检查工具和加载探针，尚未提供完整安卓模组。

`android-mod/` 包含使用 Android 互操作程序集编译的独立游戏内图鉴。其数据适配和
UGUI 触屏界面由 Android 层负责，筛选、名称搜索及本地化引擎通过源码链接复用桌面项目。
这不是桌面 DLL 的重新打包；桌面回放、视频录制、历史与上传尚未接入。

`inspect_game.py` 使用 Python 3.9+ 标准库和 Android SDK 的 ADB。它自动寻找常见 SDK
目录中的 ADB，选择已授权的设备，复制游戏的 base APK 与所有 split APK 到本机，
生成含 SHA-256、ABI、原生库、Managed 程序集和 IL2CPP metadata 路径的 JSON 报告。
运行时分类基于原生库；未知或同时存在两种运行时的结果需要进一步检查。

```sh
python3 bazaarplusplus-android/inspect_game.py
python3 bazaarplusplus-android/inspect_game.py --serial DEVICE --package GAME_PACKAGE
python3 bazaarplusplus-android/inspect_game.py --apk /path/base.apk --apk /path/split_config.arm64_v8a.apk
python3 -m unittest discover -s bazaarplusplus-android/tests -v
```

默认报告与 APK 写入本目录被 Git 忽略的 `artifacts/device/`。通过 `--output` 可选择其他
目录。多个设备或不能唯一识别的游戏包名必须显式指定，未授权的设备直接报错。
工具只读取设备信息和安装包，不安装、卸载、重签或重启游戏，不读取游戏账号数据。
离线检查必须提供完整 APK 集；缺少 ABI split 时可能无法识别运行时。

## 独立加载探针

`probe/` 是 BepInEx IL2CPP 的 `BasePlugin.Load` 插件，不引用桌面模组或游戏程序集。
入口执行时写入 `android.bootstrap.loaded` 日志，并尝试在 BepInEx 目录写入
`bpp-android-probe.json`，记录运行时、架构、线程和路径。这个标记只证明插件入口执行，
不证明 Unity 帧回调、图鉴或其他游戏功能已经可用。

框架来源和 SHA-256 由 `toolchain.json` 固定，构建工具先校验再提取引用程序集。
从该版本的 [FusionCore 源码](https://github.com/All-Of-Us-Mods/FusionCore/tree/0.045)
取得框架压缩包，安装 .NET 10 SDK 后运行：

```sh
python3 bazaarplusplus-android/build_probe.py \
  --framework-zip /path/to/FusionCore/fusionApp/src/main/assets/BepInEx-arm64.zip \
  --dotnet /path/to/dotnet
```

插件 DLL 生成在 `artifacts/probe/`；框架引用只供本机构建，不随插件复制。
使用 FusionCore 时，插件位置是
`/sdcard/FusionCore/<game-package>/BepInEx/plugins/`。此探针不包含自动部署步骤。

`runtime-probe/` 进一步注册一个 IL2CPP `MonoBehaviour`，在首帧记录回调，再等待
`Data.IsManagerCreated()` 后读取原生卡牌字典与一张模板。成功时写入
`bpp-android-runtime-probe.json`，记录帧、分辨率、卡牌数量和样本模板 ID。
读取失败最多尝试三次；不会创建 run、执行战斗或上传数据。

先使用加载器为对应手机版游戏生成 `BepInEx/interop/`，复制到本机，再向构建命令增加
`--interop /path/to/android-interop`，即可同时构建这个运行时探针。不要用桌面版 Managed
程序集代替。`probe/` 仍可在没有游戏程序集的情况下独立构建。

## 构建图鉴

在上面的构建命令同时提供 `--interop /path/to/android-interop --mod`。
图鉴 DLL 与包含框架、输入程序集、插件 SHA-256 的构建记录生成在 `artifacts/mod/`。
仅把 `BazaarPlusPlus.Android.dll` 放入加载器的 `BepInEx/plugins/`，不复制游戏代理或
框架 DLL。运行时探针会主动读取整个目录，日常使用图鉴时请把该探针移出 plugins；
图鉴本身只在首次打开时后台读取卡牌，再分帧创建托管快照并缓存。

游戏大厅左侧的「卡牌图鉴」打开触屏界面。可以切换物品／技能、循环选择英雄、
起始品质和体型，并搜索卡名或描述；输入法的「确定」提交搜索，输入框右侧 × 清空。
右上方「重置筛选」恢复全部英雄、品质、体型和空查询；技能页隐藏体型条件。
点选列表后，右侧显示游戏原生卡面和效果描述，描述区域可以上下拖动。
卡面上方的品质与附魔按钮循环切换预览变体，可选项和名称来自游戏原生工具提示规则。
每次预览创建独立卡牌实例，不升级或附魔玩家持有的卡牌；技能不提供附魔操作。
首次读取游戏卡牌数据需要等待；关闭界面不会丢弃已加载目录。

当前适配边界和真机验证记录见 [docs/device-validation.md](docs/device-validation.md)。

对局物品长按打开的原生详情和附魔下拉预览已验证可用。Android 版改由
`AndroidTooltipObserver` 观察原生详情并调用 `AndroidEnchantPreview` 生成多附魔信息，
已替换会使游戏闪退的原生 Harmony 钩子。果篮的多附魔浮层已在标准和高级输入模式验证长按、滚动及关闭；
物品预览现有「附魔／升级／任务奖励」切换，升级页投影所有可用的更高品质属性及光环倍率；
左侧「长按预览」按钮可在按住物品前选择默认展示附魔或升级，并记住选择，适用于高级输入模式；
事件卡与英雄等级提示可尝试显示可解析的结果、后续遭遇及升级奖励。对局中的「构筑推荐」按钮读取场上、
仓库及商店物品，可切换来源、分页点选候选卡；按候选交集及场上／仓库／商店权重排序，
显示最多三条十胜构筑匹配。
多档升级描述已在真机显示；构筑面板和场上候选已在真机打开。具体数值、事件奖励、
构筑匹配及其他物品仍需继续验证。
已完成和待验证功能的逐项清单见 [docs/feature-checklist.md](docs/feature-checklist.md)。
