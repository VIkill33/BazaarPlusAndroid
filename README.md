# BazaarPlusPlusForAndroid

《The Bazaar》（大巴扎）的 Android 移植项目，提供独立启动器和游戏内辅助功能，围绕触屏操作适配卡牌图鉴、附魔与升级预览、事件预览及构筑推荐。

[English](README_en.md) · [下载 APK](https://github.com/VIkill33/BazaarPlusPlusForAndroid/releases) · [反馈问题](https://github.com/VIkill33/BazaarPlusPlusForAndroid/issues)

项目目前处于实验性测试阶段。不同设备、系统和游戏版本的验证范围见[真机验证记录](bazaarplusplus-android/docs/device-validation.md)，完整功能状态见[功能清单](bazaarplusplus-android/docs/feature-checklist.md)。

## 安装与使用

1. 在手机上安装《The Bazaar》Android 客户端。
2. 从本项目的 [GitHub Releases](https://github.com/VIkill33/BazaarPlusPlusForAndroid/releases) 下载并安装 `.apk`，按系统提示允许安装。
3. 打开启动器，按引导授予文件访问权限，等待启动前检查完成。
4. 点击「启动游戏」，由启动器准备运行环境并加载插件。首次游戏登录和游戏资源下载需要联网。
5. 进入游戏后，通过「卡牌图鉴」「构筑推荐」等入口使用功能；操作说明可在启动器「帮助」中查看。

启动器内置框架、插件和匹配的 Unity 依赖，初始化与修复从 APK 提取校验，不需要从 GitHub 下载运行依赖。首页「可以启动」表示启动前检查通过，完整登录与游戏内功能仍以实际运行结果为准。

首页「设置」可选择符合筛选条件的本机游戏客户端；未选择时使用默认官方客户端。遇到问题可在「更多：修复与许可」中修复运行环境或导出诊断日志。诊断文件由用户选择保存位置，不会自动上传。

## 主要功能

| 功能 | 当前能力 |
| --- | --- |
| 独立启动器 | 文件授权、兼容检查、内置运行环境准备、失败重试、修复、诊断日志和离线帮助；首页提供「打赏作者」与「Github」入口 |
| 卡牌图鉴 | 查阅物品与技能，搜索名称和描述，按英雄、品质、体型、商人及天数筛选，查看原生卡面与详情 |
| 附魔与升级预览 | 长按查看附加信息，可切换附魔、升级或关闭附加预览，并设置预览面板位置 |
| 商店与事件预览 | 展示可解析的品质概率、条件、奖励信息及后续遭遇；具体事件覆盖范围见功能清单 |
| 构筑推荐 | 根据场上、仓库和商店物品选择候选，查看内置构筑快照的匹配结果 |
| 触屏入口 | 按钮组拖动、贴边、位置保存和可选自动收起；高级输入模式支持「按住预览」双指操作 |

各功能的实现范围与验收范围并不相同，部分数值、事件和设备仍待验证。对局历史、战斗回放、视频录制、上传和自动更新尚未在当前 Android 版本提供。

## 兼容范围

当前面向 arm64 Android IL2CPP 客户端。具体游戏版本、Unity 依赖与校验值以[兼容配置](bazaarplusplus-android/launcher/compatibility.json)为准，工具链与框架来源以[工具链配置](bazaarplusplus-android/toolchain.json)为准。

现有主要真机验证设备运行 Android 14；Android 9 的完整启动流程和其他设备仍需进一步验证。升级游戏或更换客户端后，请先查看对应版本的发布说明与验证记录。

启动器使用独立运行目录，不自动迁移原有加载器的数据。更新启动器时使用同一签名的 APK 覆盖安装，保留现有应用数据。

## 源码与开发

项目名称为 **BazaarPlusPlusForAndroid**，本地主目录名为 **`BazaarPlusPlus`**。主要开发代码位于 `bazaarplusplus-android/`。

```sh
git clone https://github.com/VIkill33/BazaarPlusPlusForAndroid.git BazaarPlusPlus
cd BazaarPlusPlus
```

| 路径 | 用途 |
| --- | --- |
| `bazaarplusplus-android/launcher/` | Android 启动器 Java／JNI、界面资源与 Gradle 工程 |
| `bazaarplusplus-android/android-mod/` | Android IL2CPP 游戏内插件与触屏适配 |
| `bazaarplusplus-android/tests/` | 离线测试与设备测试辅助代码 |
| `bazaarplusplus-android/docs/` | 功能状态、真机验证与架构说明 |
| `bazaarplusplus-android/build_probe.py` | 插件及加载探针构建入口 |
| `bazaarplusplus-android/build_launcher.py` | 启动器依赖准备与签名 APK 构建入口 |

开发前阅读根目录 [AGENTS.md](AGENTS.md) 和 [Android 开发说明](bazaarplusplus-android/README.md)。启动器构建步骤见[构建说明](bazaarplusplus-android/launcher/BUILDING.md)，签名 APK 导出约定见[导出规范](.cursor/skills/export-launcher-apk/SKILL.md)。

可从以下命令查看构建参数并执行 Python 离线检查：

```sh
python3 bazaarplusplus-android/build_probe.py --help
python3 bazaarplusplus-android/build_launcher.py --help
python3 -m unittest discover -s bazaarplusplus-android/tests -v
```

完整构建需要对应工具链、固定框架输入、匹配的 Android 游戏互操作程序集，以及本机签名材料。游戏文件、APK、设备日志和签名私钥不提交到仓库；本地产物放在被忽略的 `bazaarplusplus-android/artifacts/` 中。编译通过不能替代当前构建的实机验收。

## 反馈与支持

请通过 [GitHub Issues](https://github.com/VIkill33/BazaarPlusPlusForAndroid/issues) 反馈问题，附上启动器版本、手机型号、Android 版本、游戏客户端版本和复现步骤。需要时可附启动器导出的诊断文件，提交前检查并移除个人信息。

支持作者可使用启动器首页的「打赏作者」按钮；「Github」按钮可直接打开本项目仓库。

## 致谢与许可

感谢 [BazaarPlusPlus](https://github.com/BazaarPlusPlus/BazaarPlusPlus)、[FusionCore](https://github.com/All-Of-Us-Mods/FusionCore)、[BepInEx](https://github.com/BepInEx/BepInEx) 及相关开源项目提供基础代码与运行能力。

仓库代码采用 [MIT License](LICENSE)。打包依赖保留各自许可，启动器内可查看开源许可。本项目是独立社区工具，与游戏官方无隶属关系。
