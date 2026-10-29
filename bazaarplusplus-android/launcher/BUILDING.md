# 专用启动器构建

本目录是受版本管理的 FusionCore 定制工程。上游来源和二进制输入见
`upstream.json`；`README.md` 为保留的上游说明，产品当前行为见
[Android README](../README.md)，架构边界见 [ADR](../docs/adr/0001-dedicated-launcher.md)。

先检出 `upstream.json` 指定的上游提交。二进制无需手动放进此工程，构建准备脚本会
按摘要复制；不要提交 APK、运行时压缩包、代理或私钥。使用对应手机版代理运行
`../build_probe.py --help` 所述的插件构建；`build-inputs.json` 必须包含本次源码记录。

`build_launcher.py::bundle_dependencies` 在构建时获取 `compatibility.json` 固定的 Unity 依赖，
校验大小与 SHA-256 后一并写入 APK；手机初始化及修复仅提取内置资源。下载地址仍保留在
发行清单中用于来源追溯，不作为运行时下载入口。默认依赖缓存位于被忽略的
`artifacts/launcher-dependencies/`，也可通过 `--dependency-dir` 指定以 SHA-256 命名文件的缓存目录。
缓存齐全可离线准备输入，缺失或损坏则重新获取；任何依赖无法校验都会终止构建。
构建结束再次核对 APK 内框架、运行时、插件及全部 Unity 依赖的实际内容。

需要 JDK、Android SDK、网络与 Gradle wrapper。Gradle 管理 Android 插件、NDK 和 CMake
版本；SDK 必须接受相关许可。设置 `JAVA_HOME`、`ANDROID_HOME` 后，从仓库根运行：

```sh
python3 bazaarplusplus-android/build_launcher.py --help
python3 bazaarplusplus-android/build_launcher.py --prepare-only --upstream /path/to/FusionCore
```

签名构建使用持久 keystore，通过 `--keystore` 传入；`KEYSTORE_PASSWORD`、`KEY_PASSWORD`
和 `KEY_ALIAS` 从环境读取。不要在命令行、文档或 Git 中写密码，也不要使用通用 debug key。
脚本生成 APK、摘要、发行清单、签名验证报告和对应源码包到被忽略的 `artifacts/launcher/`。
源码包包含实际本地实现与插件引用的共享源码，不包含游戏代理及私钥；二进制输入仍从固定上游恢复。
安装与发布不由脚本自动执行。

只修改 `compatibility.json` 的匹配规则不能证明新游戏版本兼容。新增组合须重新完成
专用身份下的初始化、登录、代理生成、功能回归和另一台设备验收。
本地构建不等于获准公开分发；发行前需要完成许可清单与准确的对应源码包。

## GitHub Release 校验

签名 APK 在本机按导出规范构建并完成实机验收后，上传到本项目 GitHub Release。
附件命名为 `BazaarPlusAndroid-<tag>.apk`、同名 `.apk.sha256`、
`BazaarPlusAndroid-<tag>-source.tar.gz` 和 `release.json`。

`.github/workflows/release.yml` 的 Android Release 在发布 Release 后校验附件，
也可手动传入已发布的 tag 执行。校验包括 APK SHA-256、内置清单与依赖摘要，以及
对应源码包中的启动器版本；本机入口为 `.github/scripts/verify-android-release.py --help`。
该流程不在云端构建或签名，不依赖桌面签名、Apple 或 Cloudflare 凭据。
