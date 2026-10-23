---
name: export-launcher-apk
description: >-
  Build and export the signed BazaarPlusPlus Android launcher APK.
  Use when the user asks to 导出 APK, 打包, export the launcher, or run
  build_launcher.py / assembleRelease for bazaarplusplus-android.
---

# 导出已签名启动器 APK

从 Git 仓库根 `BazaarPlusPlus/` 执行。不要提交 APK、`artifacts/` 或 `signing-secrets/`。不要打印密钥、口令或证书私钥。不要使用 `~/.android/debug.keystore`。

## 环境

- `JAVA_HOME` 必须是 `/Library/Java/JavaVirtualMachines/jdk-24.jdk/Contents/Home`。不要用 Android Studio 自带 JBR（JDK 25），也不要设置 `JAVA_TOOL_OPTIONS`。JDK 25 连 `dl.google.com` 时 TLS 握手会被断开。
- `ANDROID_HOME` 与 `ANDROID_SDK_ROOT` 都是 `$HOME/Library/Android/sdk`。`PATH` 以 `$JAVA_HOME/bin` 开头。
- 签名材料：`signing-secrets/android-launcher.jks` 与 `signing-secrets/android-launcher.json`。JSON 只含 `KEYSTORE_PASSWORD`、`KEY_PASSWORD`、`KEY_ALIAS`，合并进环境变量，不要写进命令行参数。
- 磁盘剩余至少 2 GB。默认上游是 `bazaarplusplus-android/artifacts/tooling/FusionCore`，插件目录是 `bazaarplusplus-android/artifacts/mod`。

先在 `bazaarplusplus-android/launcher` 执行 `./gradlew --stop`，避免沿用其他 JDK 启动的 Gradle 守护进程。

## 构建

在仓库根运行 `bazaarplusplus-android/build_launcher.py`，传入 `--keystore signing-secrets/android-launcher.jks`。脚本会准备发行清单、执行 `:fusionApp:assembleRelease`、核对 APK 内置依赖，并写入：

- `bazaarplusplus-android/artifacts/launcher/BazaarPlusPlus-launcher.apk`
- 同目录的 `.sha256`、`release.json`、`signing-report.txt`
- `launcher-source.tar.gz`

把标准输出和标准错误追加到 `bazaarplusplus-android/artifacts/launcher-apk-build.log`。

## Lint 下载失败时

只有日志里已经出现 `Prepared launcher release`，并且失败任务是 `:fusionApp:lintVitalAnalyzeRelease`、原因是下载 `lint-api` 时 `Remote host terminated the handshake`，才重试。不要加 `--offline`：本机即使已有 jar，Gradle 仍可能报告没有可用缓存。

用同一套 JDK 24 和签名环境，在 `bazaarplusplus-android/launcher` 再执行一次：

```sh
./gradlew :fusionApp:assembleRelease --console=plain
```

这次成功后，再从仓库根运行一遍 `build_launcher.py`。Gradle 任务会是 UP-TO-DATE，脚本会完成核对、复制、签名报告和源码包。

准备阶段失败（上游提交、插件摘要、磁盘）时不要跳过脚本直接打包。

## 完成标准

`apksigner verify --verbose --print-certs` 的结果必须是 `Verifies`，且 v2 为 true，签名证书 DN 为 `CN=BazaarPlusPlus Android Test`。把 APK 路径、版本和 SHA-256 告诉用户。安装不由该脚本执行。
