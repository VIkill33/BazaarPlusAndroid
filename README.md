# BazaarPlusPlusForAndroid

An Android port for *The Bazaar*, with a dedicated launcher and in-game tools adapted for touch: a card collection browser, enchant and upgrade previews, encounter previews, and build recommendations.

[中文](README_zh.md) · [Download APK](https://github.com/VIkill33/BazaarPlusPlusForAndroid/releases) · [Report an issue](https://github.com/VIkill33/BazaarPlusPlusForAndroid/issues)

The project is experimental. See the [device validation records](bazaarplusplus-android/docs/device-validation.md) for tested devices, systems, and game versions, and the [feature checklist](bazaarplusplus-android/docs/feature-checklist.md) for implementation status. These detailed documents are currently in Chinese.

## Installation and usage

### Find and download the APK

1. Click **Download APK** at the top of this README, or open [GitHub Releases](https://github.com/VIkill33/BazaarPlusPlusForAndroid/releases) directly. This link opens the release list even if the repository homepage sidebar only shows **1 tag** or **Create a new release**.
2. Choose the Android version you want to install and read its release notes and compatibility information. Experimental test releases are marked **Pre-release**.
3. Scroll below that version's release notes and click **Assets** to expand the attached files.
4. Click the file ending in `.apk` (named `BazaarPlusPlus-Android-<version-tag>.apk`) to download the installer. The `.apk.sha256` file is for checksum verification; `-source.tar.gz` and **Source code** contain source code. These files are not required for installation.

### Install and launch

1. Install the Android client of *The Bazaar* on your phone.
2. Open the `.apk` downloaded above and follow the system prompts to allow installation.
3. Open the launcher, grant file access as prompted, and wait for the pre-launch checks.
4. Tap 「启动游戏」 (Launch game). The launcher prepares the runtime and loads the plugin. Initial game login and game resource downloads require internet access.
5. Use the in-game card collection and build recommendation buttons. Open 「帮助」 (Help) in the launcher for instructions.

The APK bundles the framework, plugin, and matching Unity dependencies. Setup and repair extract and verify these files locally without downloading runtime dependencies from GitHub. 「可以启动」 (Ready to launch) means the pre-launch checks passed; login and in-game functionality still depend on the actual game session.

Under 「设置」 (Settings), you can select an installed game client that meets the launcher's filtering requirements. The official client is the default. Under 「更多：修复与许可」 (More: repair and licenses), you can repair the runtime or export diagnostics. You choose where to save diagnostic files; they are not uploaded automatically.

## Main features

| Feature | Current capabilities |
| --- | --- |
| Dedicated launcher | File permissions, compatibility checks, bundled runtime setup, retries, repair, diagnostics, and offline help; donation and Github buttons on the home screen |
| Card collection | Browse items and skills, search names and descriptions, filter by hero, tier, size, merchant, and day, and inspect native card art and details |
| Enchant and upgrade previews | Long-press for additional information, select enchant previews, upgrade previews, or no additional preview, and configure panel position |
| Shop and encounter previews | Show resolvable tier probabilities, conditions, rewards, and follow-up encounters; coverage is listed in the feature checklist |
| Build recommendations | Select candidates from the board, stash, and shop, and view matches against bundled build snapshots |
| Touch controls | Drag the button group, snap it to an edge, save its position, and optionally collapse it automatically; advanced input mode supports two-finger hold-to-preview |

Implementation and validation coverage differ. Some values, encounters, and devices still require testing. Run history, combat replay, video recording, uploads, and automatic updates are not available in the current Android version.

## Compatibility

The current target is arm64 Android IL2CPP clients. The [compatibility configuration](bazaarplusplus-android/launcher/compatibility.json) defines game versions, Unity dependencies, and checksums. The [toolchain configuration](bazaarplusplus-android/toolchain.json) defines the toolchain and framework sources.

Most existing device validation uses Android 14. The complete Android 9 startup flow and other devices still require further testing. Check release notes and validation records before updating the game or switching clients.

The launcher uses a separate runtime directory and does not automatically migrate data from other loaders. To update the launcher while retaining application data, install an APK signed with the same key over the existing installation.

## Source and development

The project is named **BazaarPlusPlusForAndroid**; its local root directory is named **`BazaarPlusPlus`**. The main development code is in `bazaarplusplus-android/`.

```sh
git clone https://github.com/VIkill33/BazaarPlusPlusForAndroid.git BazaarPlusPlus
cd BazaarPlusPlus
```

| Path | Purpose |
| --- | --- |
| `bazaarplusplus-android/launcher/` | Android launcher Java/JNI code, UI resources, and Gradle project |
| `bazaarplusplus-android/android-mod/` | Android IL2CPP in-game plugin and touch adaptations |
| `bazaarplusplus-android/tests/` | Offline tests and device testing helpers |
| `bazaarplusplus-android/docs/` | Feature status, device validation, and architecture documentation |
| `bazaarplusplus-android/build_probe.py` | Build entry point for the plugin and loading probes |
| `bazaarplusplus-android/build_launcher.py` | Launcher dependency preparation and signed APK build entry point |

Before making changes, read the root [AGENTS.md](AGENTS.md) and the [Android development notes](bazaarplusplus-android/README.md). See the [build guide](bazaarplusplus-android/launcher/BUILDING.md) for launcher builds and the [export instructions](.cursor/skills/export-launcher-apk/SKILL.md) for signed APKs.

Use these commands to inspect build options and run the Python offline checks:

```sh
python3 bazaarplusplus-android/build_probe.py --help
python3 bazaarplusplus-android/build_launcher.py --help
python3 -m unittest discover -s bazaarplusplus-android/tests -v
```

A full build requires the matching toolchain, pinned framework inputs, Android game interop assemblies, and local signing material. Do not commit game files, APKs, device logs, or private signing keys. Local outputs belong in the ignored `bazaarplusplus-android/artifacts/` directory. A successful compilation does not replace device validation of the current build.

## Feedback and support

Report problems through [GitHub Issues](https://github.com/VIkill33/BazaarPlusPlusForAndroid/issues), including the launcher version, phone model, Android version, game client version, and reproduction steps. Attach exported launcher diagnostics when needed, after checking for and removing personal information.

Use 「打赏作者」 (Support the author) on the launcher home screen to support maintenance. The 「Github」 button opens this project's repository.

## Acknowledgements and license

Thanks to [BazaarPlusPlus](https://github.com/BazaarPlusPlus/BazaarPlusPlus), [FusionCore](https://github.com/All-Of-Us-Mods/FusionCore), [BepInEx](https://github.com/BepInEx/BepInEx), and the other open-source projects providing source code and runtime components.

Repository code is licensed under the [MIT License](LICENSE). Bundled dependencies retain their own licenses, available in the launcher. This is an independent community tool with no official affiliation with the game.
