# AGENTS.md

BazaarPlusPlus monorepo. The mod and the installer ship as one Product Release; the server, analyzer, and site are separate deployments. Each project keeps its own toolchain, lockfiles, `AGENTS.md`, and `CONTEXT.md` glossary. Work inside the project the change touches and follow its `AGENTS.md`; this file holds repo-wide rules and the Android port development workflow.

| The change touches | Project |
|---|---|
| In-game features, Harmony patches, uploads from the game | `bazaarplusplus-mod/` |
| Install, repair, update, local history, the OBS overlay | `bazaarplusplus-installer/` |
| Bundle ingest, Ghost discovery, BazaarDB delivery | `bazaarplusplus-server/` |
| Hero and build snapshots computed from Bundles | `bazaarplusplus-analyzer/` |
| bazaarplusplus.com, Hero Analysis, the download page | `bazaarplusplus-site/` |
| Product version, Payload Inventory, release pipeline | root `VERSION`, `release.mjs`, `release/`; read `docs/release.md` |

Cross-project terms (Product Release, Payload, Release Manifest, Bundle) are defined in `CONTEXT.md`.

## Contracts

A contract change lands in one pull request together with every consumer it breaks. Each row names the owning source; consumers read it, they do not restate it.

| Contract | Owner | Consumers |
|---|---|---|
| Bundle V6 binary and manifest | `bazaarplusplus-server/contracts/v6/`, shared goldens in `fixtures/` beneath it | mod writes and reads Ghost Bundles, server ingests, analyzer reads |
| Run segment inside a Bundle V6 | `bazaarplusplus-mod/docs/contracts/run-payload-v6.md`, shared golden `bazaarplusplus-mod/tests/BundleV6Codec.Tests/fixtures/run-payload-v6.fixture.b64` | server stores it opaque, analyzer decodes it |
| Mod API HTTP routes | `bazaarplusplus-server/docs/api-reference.md` | mod |
| Hero and build snapshots | `bazaarplusplus-analyzer/contracts/v6/`, shared goldens in `fixtures/` beneath it and the hero alias table `hero-aliases.json`, plus `bazaarplusplus-analyzer/docs/specs/consumer-data-contract.md` | site Hero Analysis, mod build recommendations, installer History (hero aliases) |
| Seal eligibility | `BundleQueueStore.SealEligibleRunCondition` in `bazaarplusplus-mod/src/BazaarPlusPlus.Storage/`, shared cases `bazaarplusplus-mod/tests/BundleQueueSqliteStore.Tests/fixtures/bundle-seal-eligibility.json` | mod replay maintenance, installer History cleanup (`PROTECTED_RUN_PREDICATE`) |
| Payload Inventory | `release/payload.json` | mod MSBuild, installer packaging and cleanup |
| Local history database schema (`user_version`), Data Root name, and replay payload retention | `RunLogSchema.LocalDatabaseSchemaVersion`, `PathConstants.DataRootDirectoryName`, and `ReplayPayloadRetentionPolicy`, mirrored as `historyDatabaseUserVersion`, `dataRootDirectoryName`, and `replayPayloadRetained*` in `bazaarplusplus-mod/src/BazaarPlusPlus.Storage/BazaarPlusPlus.history-database.json`; `release::check` gates the pair (`release/history-database.mjs`): same Data Root on both sides, mod schema newest the installer supports; shared schema file `bazaarplusplus-mod/src/BazaarPlusPlus.Storage/history-database-v6.schema.sql` | installer History, Reset, cleanup, and overlay reads (`bazaarplusplus-installer/src-tauri/history-database-compatibility.json`, whose `dataRootDirectoryName` a test pins to `BAZAAR_DATA_DIRECTORY`); the V5 import reads the installed copy's retention |
| Release Manifest and Platform Release Manifest | `release/manifest.mjs`, shared fixtures `release/fixtures/latest.json` and `release/fixtures/latest/` | site download page, installer updater, mod update check |

Root `release/` owns Product Release modules and tests. Imports run one way: installer scripts may import release modules, never the reverse.

Run `just release::sync` after editing `VERSION` or `release/payload.json`; it regenerates the projections the builds validate against.

## Verification

`just` lists every recipe. Gate one project with `just <project>::check` and `just <project>::test`; gate a contract change with the check and test recipes of the owner and every consumer. `just check` and `just test` cover the whole repo; `just release::test` gates the root release modules. `just fmt` formats everything. Goldens fail on any difference in every project; `BPP_UPDATE_GOLDENS=1` rewrites them (analyzer: `just analyzer::golden`), and every rewritten golden must be an intended diff in the PR. Scope and prerequisites: `docs/development.md`.

## Commits and pull requests

- Except for the Android per-feature local commit workflow below, commit only when the user asks, after reviewing your own diff: done when you can name every file in the commit and why it is there. Revert formatter-only edits to files outside your change.
- Commit messages and PR titles use Conventional Commits, `<type>(<scope>): <description>`. The scope is the project or feature area; root tooling uses `dev` or `release`.
- End every PR body with a `Release Notes:` line, one blank line, then one bullet per user-visible change starting with Added, Fixed, or Improved. Use `- N/A` when nothing is user-visible.
- `master` is protected. The wrap-up flow is: commit on a branch, `gh pr create`, merge, delete merged branches.

## Changing code

- Run an independent red-team review of a large refactor or design plan before implementing. Keep it review-only, landing `file:line` evidence rather than patches, incorporate the findings, and proceed within the user's authorized scope without another confirmation.
- A replacement ships only the new version: done when the old implementation is deleted and the project's check and test recipes pass.
- Scope a delete or change to its named target plus the members that fed only that target. Unrelated cleanup goes in its own change.

## Documentation

- Each fact has one owner. A term goes in the project's `CONTEXT.md` (in the root one when several projects use it). Current behavior goes in `docs/*.md`. A decision goes in `docs/adr/`. Plans, feature requests, and bugs go in GitHub issues (see Agent skills).
- Config files, scripts, and `--help` output own every value they state. A doc carries only the convention, the reason, or the gotcha they cannot state.
- Name domain concepts with the glossary's terms; a real naming gap gets a `CONTEXT.md` entry in the same change. When your output contradicts an ADR, name the ADR and say why it is worth reopening.
- In committed docs and comments, cite code by path plus symbol name; line numbers drift. `file:line` is fine in session evidence and reviews.
- ADRs are numbered `NNNN-slug.md` per project, in sequence. A number is never reused or renumbered. A replaced ADR gets a `superseded-by:` frontmatter pointer; a retired one is deleted and lives on in git history.
- Root `README.md`, `docs/development.md`, and `docs/release.md` are in Chinese. `README_en.md` mirrors `README.md` section for section; edit both in one change.

## Instructions

Add an instruction to an `AGENTS.md` only when it is non-obvious, keeps coming up, and is actionable; put it in the narrowest `AGENTS.md` it applies to. During ordinary work, propose it under a **Suggested AGENTS.md additions** heading in the wrap-up. Edit directly when the user asks or an existing instruction is wrong.

## Agent skills

### Issue tracker

GitHub Issues on this monorepo through `gh`; issues only, not PRs. Read `docs/agents/issue-tracker.md` before you create, read, or triage an issue.

### Triage labels

Each of the five canonical triage roles uses its own name as its label. Read `docs/agents/triage-labels.md` before applying one; most do not exist on the repo yet.

### Domain docs

`docs/agents/domain.md` names which `CONTEXT.md` and `docs/adr/` files to read before you explore an area.

## Android 移植开发规范

本节约束 `bazaarplusplus-android/` 及为 Android 移植而修改的共享代码，并补充上文仓库通用规范；修改其他子项目时还须遵守其目录内的 `AGENTS.md`。本文所有命令均从 Git 仓库根目录执行；外层 `BazaarPlusPlusForAndroid/` 是工作区目录，不是 Git 仓库。

### 1. 编码前先读文档、原实现与 UI

每个功能点开始前，必须完成以下阅读与核对，不能直接凭功能名称实现：

1. 阅读 Android [README](bazaarplusplus-android/README.md)、[功能清单](bazaarplusplus-android/docs/feature-checklist.md)和[真机验证记录](bazaarplusplus-android/docs/device-validation.md)，确认已实现、已验证、待验证和暂缓范围。
2. 阅读原插件 [AGENTS.md](bazaarplusplus-mod/AGENTS.md)、[MEMORY.md](bazaarplusplus-mod/docs/MEMORY.md)、[架构索引](bazaarplusplus-mod/docs/ARCHITECTURE.md)，再按下表阅读相应功能文档、Gotchas 和相关 ADR。
3. 沿文档中的路径阅读桌面插件当前源码和测试，理解数据来源、规则、边界条件、UI 层级及交互生命周期。文档与代码不一致时核对实际行为，不把旧说明直接照搬。
4. 涉及游戏 API 或崩溃时，对照对应游戏版本的本地反编译代码及 Android 生成代理，确认签名和运行时语义；桌面 API 不能直接作为 Android 兼容证据。
5. 编码前简要说明：参考了哪些文档和原实现、Android 复用与适配边界、预期交互、真机验收步骤。缺少原 UI 截图时可从 UI 源码核对布局与状态，明确尚未观察的部分。

| 功能 | 必读原插件文档 |
| --- | --- |
| 图鉴、搜索、分类和筛选 | [collection.md](bazaarplusplus-mod/docs/architecture/collection.md) |
| 附魔、升级、事件、奖励与原生卡牌预览 | [previews.md](bazaarplusplus-mod/docs/architecture/previews.md) |
| 构筑推荐、阵容与浮层生命周期 | [panels.md](bazaarplusplus-mod/docs/architecture/panels.md) |
| 设置与输入模式 | [input-and-settings.md](bazaarplusplus-mod/docs/architecture/input-and-settings.md) |
| 本地化、字体与文字渲染 | [text.md](bazaarplusplus-mod/docs/architecture/text.md) |
| 历史、回放、录制、上传与幽灵对战 | [capture.md](bazaarplusplus-mod/docs/architecture/capture.md)及对应数据契约 |

UI 设计还须阅读 [design.md](docs/design.md)：游戏内面板跟随游戏原生 UI；安装器和网站的桌面视觉 token 不直接套到 Android 游戏浮层。保留原功能的信息语义，并按触屏适配点击、长按、滚动、关闭、遮挡和输入法，不能把桌面悬停或快捷键当作唯一入口。

### 2. 项目架构与代码边界

本项目是 BepInEx / Unity IL2CPP 游戏内插件，不是普通 Android Activity 或 Gradle 应用。当前结构如下；具体构建引用以项目文件为准。

| 目录或文件 | 职责 |
| --- | --- |
| `bazaarplusplus-android/android-mod/Plugin.cs` | IL2CPP 插件入口、组件注册与生命周期装配 |
| `android-mod/AndroidCollectionCatalog.cs`、`CollectionController.cs` | 卡牌目录快照、图鉴交互与预览请求 |
| `android-mod/AndroidTooltipObserver.cs` 与各 `Android*Preview.cs` | 原生提示观察、附魔／升级／事件等奖励预览 |
| `android-mod/AndroidBuild*.cs` | 构筑数据、候选、匹配与展示 |
| `android-mod/NativeUi.cs`、`NativeNullableValue.cs`、`NativePreviewCleanup.cs` | Android 原生 UI、互操作与资源清理适配 |
| `bazaarplusplus-android/probe/`、`runtime-probe/` | 加载与运行时诊断探针，不等同于功能插件 |
| `bazaarplusplus-android/inspect_game.py`、`build_probe.py` | 设备／安装包检查与固定工具链构建 |
| `bazaarplusplus-android/tests/`、`data/`、`docs/` | 离线验证、注明来源的数据与功能证据 |
| `bazaarplusplus-mod/` | 桌面功能及部分源码链接共享规则的来源 |

- 优先通过现有 `.csproj` 源码链接复用纯规则和本地化逻辑。Android 负责 IL2CPP 集合、原生对象及触屏 UI 适配，避免复制后独立演进的业务规则。
- 功能流程、数据规则、互操作和视图分开；共享原生预览能力提取到公共适配边界，避免一个面板依赖另一个面板内部状态。不为小改动引入整套新框架。
- 修改共享源码前核对所有引用方，同时验证受影响桌面项目。跨项目契约修改须同步消费者，契约归属以根 `AGENTS.md` 为准。
- Android 必须引用对应手机版生成的 `BepInEx/interop/`，不能用桌面 Managed DLL 替代，也不能将桌面插件 DLL 重新打包充当移植。

### 3. 运行时、数据与 UI 规范

- Unity 对象创建、UI 更新和销毁在主线程执行；IL2CPP 后台访问须正确附加线程。大量数据分批投影为托管快照，避免阻塞游戏帧。
- 异步加载必须处理关闭、换卡、重开、场景切换及销毁：取消旧请求，以版本或请求身份拒绝过期结果，清理晚完成的原生对象。复用 `NativePreviewCleanup` 等现有机制。
- 预览使用独立卡牌实例和属性副本，不能修改玩家物品、目录模板或实际对局。升级、附魔和奖励文字须对照原规则与游戏数值；候选权重不能擅自解释为获得概率。
- 原生代理的空值、Nullable、集合枚举和对象生命周期须在适配边界处理。已有真机崩溃路径先查验证记录；重新引入原生钩子必须有明确依据和专项验证。
- 优先使用游戏原生卡面、图标、文本及变体规则。布局须考虑物品体型占格、横屏比例、中文长文本、滚动到底部和触控遮罩，关闭浮层不能穿透触发游戏选择。
- 缓存、在线更新和内置数据须保留来源、版本与校验信息；加载、空数据、断网、异常和重试均有明确状态。数据来源约定见 [data/README.md](bazaarplusplus-android/data/README.md)。
- 沿用现有命名、可空标注与日志风格，不通过关闭警告掩盖问题；临时诊断探针在问题定位后的修复中移除，避免逐帧刷日志。
- APK、游戏程序集、设备截图、日志、账号数据、私有路径和凭据不提交。构建与真机证据写入被忽略的 `bazaarplusplus-android/artifacts/`，提交前检查暂存区。

### 4. 编码后先离线检查，再自动测试连接的安卓真机

每个功能点按“阅读 → 实现 → 离线检查 → 部署 → 真机自动操作与断言 → 修复复测 → 记录 → 提交”闭环完成，不能积压多个未经真机验证的功能。

#### 离线检查

工具链版本、框架来源及 SHA-256 以 [toolchain.json](bazaarplusplus-android/toolchain.json)为准；参数以构建脚本 `--help` 为准，不在文档中维护版本副本。按改动范围执行：

```sh
python3 -m unittest discover -s bazaarplusplus-android/tests -v
python3 bazaarplusplus-android/build_probe.py --framework-zip /path/to/BepInEx-arm64.zip --dotnet /path/to/dotnet --interop /path/to/android-interop --mod
/path/to/dotnet run --project bazaarplusplus-android/tests/BuildCorpus.Tests/BuildCorpus.Tests.csproj
/path/to/dotnet run --project bazaarplusplus-android/tests/EnchantPolicy.Tests/EnchantPolicy.Tests.csproj -p:InteropPath=/path/to/android-interop
```

以上 `/path/to/...` 必须替换为本机实际路径；两个 C# 测试项目是可执行程序，使用 `dotnet run`。修改共享桌面规则时，还须执行对应桌面检查与测试，命令见 [development.md](docs/development.md)。缺少依赖、构建失败或跳过均不算通过。

#### 真机自动测试要求

1. 使用 `adb devices -l` 检查连接与授权；多设备时明确序列号，所有后续 ADB 操作使用 `-s <serial>`。记录设备、Android／游戏版本、输入模式及测试前游戏状态，不沿用历史记录推断当前环境。
2. 检查本次构建的 `artifacts/mod/build-inputs.json`，部署对应 `BazaarPlusPlus.Android.dll` 到已确认的 FusionCore 游戏插件目录。仅替换目标插件，不复制框架或游戏代理；日常功能测试移出会全目录读取的运行时探针。
3. 按加载器实际入口重启／重载，核对当前 DLL 校验值和本次启动日志，确认运行的是新构建。不能仅凭 push 成功或旧截图判定已更新。
4. 使用 ADB 或可用的设备自动化工具执行点击、长按、滑动、输入、关闭重开等步骤。每步检查屏幕状态，等待有超时；坐标操作依据当前截图与分辨率定位，不能盲点固定坐标。
5. 自动操作必须配合结果断言：收集截图、插件日志和必要的 `logcat`，核对文本、卡图、数值、筛选结果、选择状态及生命周期，检查异常与原生崩溃。截图人工复核须如实标注，不能声称已有自动视觉断言。
6. UI／长按相关改动分别覆盖标准与高级输入模式；涉及预览时核对原物品品质、附魔和属性未改变。覆盖正常路径及受影响边界，如空数据、连续切换、关闭中的异步完成、滚动末尾与恢复场景。
7. 测试需要新开对局时，只选择休闲模式，禁止选择排位模式。已经开始的对局无需检查或更换模式，不为此退出或重开。购买、实际附魔／升级、选择事件或野怪、推进战斗、清除数据及上传等改变游戏或账号状态的操作，只有当前任务已明确授权时才执行；其余可验证路径继续完成。
8. 设备断开、未授权、加载器失败、缺少对应场景或需要人工操作时，明确说明阻塞及已测范围。不得以模拟器、离线测试或历史真机结果替代本次验证，不得标记该功能“真机通过”。

当前仓库未提供覆盖所有功能的真机自动化验收脚本。应优先复用已有工具；重复执行的场景补充可重复脚本和断言，记录入口及参数，不能在文档中假称现有一键测试已覆盖。

### 5. 一个功能点通过后及时提交

- 本项目约定：用户要求开发或修复功能时，每个功能点在相关离线检查、当前构建真机验收和自查通过后及时执行本地 Git 提交，无需再次逐项询问。用户当前指令优先；“仅分析”“不要提交”等限制必须遵守。
- 每次提交围绕一个可独立说明、验证和回退的功能点，包含必要源码、测试及验证文档。不要夹带其他功能、无关格式化或用户已有改动。
- 提交前检查 `git status`、`git diff`、`git diff --cached` 和 `git diff --check`；显式暂存本次文件，避免 `git add .` 混入设备产物。确认每个暂存文件的用途。
- 使用 Conventional Commits，例如 `fix(android): prevent stale card previews after close` 或 `feat(android): add collection source filter`。不跳过 hooks，不因检查失败而强行提交“已完成”功能。
- 真机未通过的功能继续修复复测；受阻时保持待验证并报告，不提交为已验收功能。纯文档改动可通过内容与链接检查验收，不要求为文档启动手机。
- 本地提交不自动授权推送、创建 PR、合并或发布；远端操作遵循当前任务授权及仓库流程。不重写用户历史，不覆盖他人修改。

### 6. 文档同步与交付

- 每完成一个功能点，必须同步更新已有的[功能检查清单](bazaarplusplus-android/docs/feature-checklist.md)，在对应条目记录实际实现范围、真机验证状态及剩余待验证项，并与该功能代码一同提交；复用已有条目，不另建重复清单。
- 行为变化同步 Android README；本次真机验证的环境、步骤、预期／实际结果及证据位置更新到真机验证记录。代码引用使用路径与符号名，避免固定行号。
- 状态严格区分“已实现”“离线通过”“真机通过”和“待验证”。数值尚未比对、只测过单一输入模式或单个物品时，明确写出边界。
- 新架构决策记录在所属项目的 `docs/adr/`；命令与版本以脚本、配置为唯一来源，功能事实以当前源码和验证证据为准，避免多份文档重复维护。
- 交付简述：实现内容、参考依据、实际执行的检查、真机结果及局限、证据位置与提交哈希。未执行的检查和未完成事项明确列出，不把“能编译”写成“功能完成”。
