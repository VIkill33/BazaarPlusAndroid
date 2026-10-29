# Android 构筑库：拉取并采纳新快照

状态：待实现。只改 Android 插件的构筑库加载，不改分析器、召回排序和面板布局。

## 问题

插件已经会请求 `https://bpp-metrics-v6.bazaarplusplus.com/builds/latest.json`。`AndroidBuildRepository.LoadAsync` 的顺序是：先装内置快照，再用 `HealthyReplacement` 决定缓存和在线响应能不能换上。

内置快照是桌面 5.7.0 的种子，统计窗口为 2026-09-29 至 2026-10-05，共 5,689 条。换上规则要求新文档至少 51,200 字节，总条数不少于当前的四分之一，且每个已有英雄不少于当前条数的十分之一。2026-10-22 线上只有 3 条多利样本，这条规则正确地拒绝了它。

同一条规则也会拒绝一份合格的新快照。新窗口的样本可以合法地少于 10 月 5 日那份种子，某个英雄也可以低于旧条数的十分之一。更严重的是启动顺序：进程每次都先把内置快照放进 `Current`，再拿缓存跟它比。即使上一局已经把新快照写入 `builds.json`，下次冷启动在断网时仍会退回 5,689 条旧数据。

桌面版用 `window.end` 表示数据新鲜度，缓存 20 小时后再后台刷新。Android 解析了窗口却没有留下，也没有缓存有效期；面板上的「刷新」只是再跑一遍同一套缩水比较。

## 结果

玩家打开构筑面板时先看到本地最好的一份库。本地缓存比内置种子新，就用缓存。在线快照的统计窗口更新，并且文档本身完整，就换上在线快照并写回缓存。残缺、更旧，或同一窗口内条数塌缩的响应留在日志里，界面继续用已经显示的库。

数据地址不变，仍是分析器发布的 `builds/latest.json`。匹配仍在本地完成，不按当前阵容请求服务器。

## 采纳规则

把比较从 `AndroidBuildRepository.HealthyReplacement` 抽成纯函数，例如 `AndroidBuildAdoption.Decide(retained, candidate, byteCount)`。`AndroidBuildCorpus` 在现有解析结果上保留 `WindowStart`、`WindowEnd` 和 `GeneratedAt`。窗口合法性仍由现有解析负责：1 至 7 天，且 `end - start + 1 == days`。

非内置文档先过绝对下限，与上一份库的条数无关：

- UTF-8 字节数不少于 51,200。这与桌面嵌入种子的 `MinBytes` 相同。
- `BuildCount` 不少于 100。

过不了下限就是残缺响应。2026-10-22 的 3 条样本同时过不了这两条。

下限通过之后，只按统计窗口和生成时间决定：

| 条件 | 决定 |
|---|---|
| `candidate.WindowEnd` 早于 `retained.WindowEnd` | 拒绝，保留现有库 |
| `WindowEnd` 更晚 | 采纳。不要求总条数达到旧库的四分之一，也不要求旧英雄保留十分之一 |
| 同一 `WindowEnd`，`GeneratedAt` 不晚于现有库 | 拒绝 |
| 同一 `WindowEnd`，生成时间更新，但总条数低于现有库的四分之一，或任一已有英雄低于 `max(1, 现有条数 / 10)` | 拒绝 |
| 同一 `WindowEnd`，生成时间更新，条数没有这样塌缩 | 采纳 |

没有现有库时，一份能通过解析和绝对下限的文档可以直接采纳。内置种子是随包发布的已知文档，不套用 100 条和 51,200 字节的下限。

英雄从新窗口消失时条数可以为 0。同一窗口里的十分之一下限只保护「这份数据自己突然缩水」，不用 10 月 5 日的皮格马利翁 834 条去卡后来的赛季。

## 加载顺序

缓存路径保持 `Paths.ConfigPath/BazaarPlusPlus.Android/builds.json`。一次加载的顺序：

1. 解析内置 gzip `BazaarPlusPlus.Android.Data.BuildRecommendations.builds.json.gz`。
2. 解析缓存。解析失败只记 `android.builds.cache_failed`，当作没有缓存。
3. 在内置种子和缓存之间用上面的规则选出本地赢家，立刻写入 `Current`。缓存窗口更新则缓存胜出，即使它比 5,689 条少。缓存更旧或同一窗口里塌缩，则内置种子胜出。
4. 缓存就是赢家，且文件 `LastWriteTimeUtc` 距离现在不足 20 小时，则本次启动不再请求网络，记 `android.builds.skipped_fresh`。
5. 其余情况在后台 `GET` 线上快照。成功后对当前 `Current` 做一次采纳判断。采纳则替换内存中的库，并用现有的临时文件加 `File.Move` 写回缓存。拒绝或失败不改内存，也不改缓存文件。

面板「刷新」始终发请求，不受 20 小时限制。启动加载还在进行时，这次点击不能被丢掉：进行中的加载结束后再拉一次。用递增代号包住发布和写缓存；后启动的加载使先启动的加载在写回前失效，避免慢响应覆盖新结果。

`HttpClient` 继续使用 10 秒超时，只发 GET。增加请求头 `User-Agent: BazaarPlusAndroid/{Plugin.Version}`。请求留在 `Task.Run` 上，不改用 `UnityWebRequest`，也不放到 Unity 主线程。

## 界面

构筑面板每秒已经会重读 `Current`，内存中的库换掉之后，匹配结果会在下一次刷新出现，不必关闭面板。

状态行在现有「数据生成于」后面加上统计截止日，取 `WindowEnd`：

`数据生成于 2026-10-26 · 统计至 2026-10-25`

仓库用一个易失状态记下最近一次在线结果。面板只在失败时多写一行，成功时靠日期变化本身说明：

- 拒绝：`在线更新未采用，仍显示本地数据`
- 请求或解析失败：`在线更新失败，仍显示本地数据`

加载期间保留当前匹配和十格阵容。没有本地库时仍显示「构筑数据加载中，或当前没有对局。」

## 日志

沿用现有事件名，补上判断所需的字段：

- `android.builds.bundled`、`android.builds.cache_loaded`：`count`、`generated_at`、`window_end`
- `android.builds.ready`：同上，表示在线快照已采纳
- `android.builds.skipped_fresh`：缓存未过期，跳过请求
- `android.builds.remote_rejected`：`reason` 取 `truncated`、`older_window` 或 `same_window_shrink`，并带上 `incoming`、`retained`、`bytes`、双方 `window_end`
- `android.builds.remote_failed`、`android.builds.cache_write_failed`、`android.builds.bundled_failed` 保持现有含义

## 代码落点

- `android-mod/AndroidBuildCorpus.cs`：解析时保留窗口起止日期。
- 新增 `android-mod/AndroidBuildAdoption.cs`：上表的纯函数，不读文件、不发请求。
- `android-mod/AndroidBuildRepository.cs`：按「本地择优 → 过期才请求 → 刷新必请求」替换 `LoadAsync`；删除把内置库先写入后再用条数比例过滤缓存的路径。
- `android-mod/AndroidBuildPanelController.cs`：状态行增加统计截止日和失败说明。
- `android-mod/Plugin.cs`：地址、缓存路径和启动时 `Warm()` 保持不变。

不更换内置 gzip，也不在这次修改里重新嵌入种子。运行时缓存负责把新窗口带给已安装的包。以后某份线上快照已经稳定，再单独把该文件压进程序集。

## 测试

扩展 `tests/BuildCorpus.Tests`。采纳函数用构造出的语料和传入的字节数覆盖决策表，不依赖真实网络。

仓库测试继续使用现有的单条种子和 `FixedHandler`：

- 内置库已装入时，短于 51,200 字节的缓存和在线响应都不能替换它，也不能改写原来的缓存文件。这是现有断言，保留。
- 缓存窗口更晚、字节数达到下限、总条数可以少于 5,689 时，断网启动应读出缓存而不是内置种子。测试文档可以在合法 JSON 后补空格，使 UTF-8 长度达到 51,200。
- 该缓存的写入时间在 20 小时以内时，启动不应调用 `HttpMessageHandler`。
- 写入时间超过 20 小时时，先公布缓存，再在请求成功后换上窗口更晚的响应。
- 面板刷新对应的第二次加载应覆盖仍在进行的第一次加载；后完成的旧响应不能写回缓存。

`just` 或现有测试入口里的构筑解析与召回用例要一起通过。召回排序的断言保持不动。

## 真机核对

实现完成后在现有设备上核对这些日志和界面，不把离线测试写成已经通过：

- 冷启动、有网：先出现本地库，若线上 `window_end` 更晚且体积达标，日志出现 `android.builds.ready`，状态行的生成日和统计截止日变为新值，无需重启。
- 飞行模式：沿用内置库或未过期缓存，匹配仍可用，日志为 `android.builds.remote_failed` 或 `android.builds.skipped_fresh`。
- 打开面板后点「刷新」：成功时日期更新；失败时原推荐还在，并出现对应的一行说明。
- 若再次遇到只有数条样本的响应：`reason=truncated`，日期保持不变，缓存文件字节数不变。
