# Android 真机验证

## 单场对局败场样式与完整采集文案（2026-10-26，test.14）

`LauncherHistoryActivity.runRow` 仅在单场对局摘要中将败场缩为原 25sp 的四分之一（6.25sp），
使用 `bpp_muted` 灰色，与胜场间加入五个保持胜场字号的全角空格；沿同一基线显示。
`HistoryBattleView.groupLabel` 对完整采集返回分组名，不显示完整采集提示、张数或空阵容提示；
部分采集、失败、未采集的提示保留。仅调整启动器视图，不改历史数据或游戏内面板。
参考 Android README、功能清单、启动器 ADR／构建说明、桌面历史布局及当前 Java 实现。

设备 `OBJVWSPNSCFQU48L`、22041216C、Android 14，系统字体 1.0；目标游戏为
`1.0.12825-production-android-d9601ded`。测试开始位于历史对局列表，只读浏览既存记录，未启动游戏或推进对局。
按导出 skill 使用 JDK 24 和持久签名构建并覆盖安装 `0.1.0-test.14`，Lint／签名构建通过，
签名 `Verifies`、v2=true、`CN=BazaarPlusPlus Android Test`；内置依赖、插件摘要、已安装版本三项检查通过。
APK SHA-256：`4f8de06015ef53ebeed08b7219ec99ab2809719d450ec748a82afe1c2bc4faae`。

13 项 Python 离线回归通过，`verify_history_browser.py` 普通记录 138 项真机断言通过，
覆盖完整采集标签无冗余文案、全部卡图入屏、横竖屏与旋转恢复、筛选及返回位置。
人工复核单场对局截图中的败场大小、灰色及间距，以及单场对战横竖屏标签与卡图；
字号比例和五字符间距由 spans 与全角空格实现，本轮未对像素进行自动字体测量。
结束恢复全部英雄和原旋转设置；崩溃缓冲区最新记录早于本轮测试，无本轮新增启动器崩溃。
启动器操作与游戏标准／高级输入模式无关；其他设备、放大字体和非完整采集状态专项仍待验证。

证据位于忽略目录 `artifacts/history-score/`（`normal.log`、`normal/` 布局树／截图／138 项结果、
`installed/verification.json`）；离线记录为 `artifacts/history-score-tests.log`。

## 启动器历史列表原型实施与同行小头像（2026-10-26，test.14）

根据用户选定的紧凑列表原型，在现有原生 Java 启动器内调整历史页面：日期标题嵌在真实记录行内，
整局条目显示头像、突出胜负、可靠奖杯／状态及时间；交手列表显示天数、结果与对手。
三级标题为「历史对局 → 单场对局 → 单场对战」，不显示概念名称。
列表顶栏复用首页集市场景，详情缩短顶栏；英雄筛选及分页采用描边按钮。
`HistoryBattleView` 由名称实际字体行高计算方形头像尺寸，头像与名称同行，物品与技能继续使用完整阵容宽度。
字号放大时重新扣减文字预算；原生卡图比例、物品单行等高、技能 22dp 上限及竖屏实际内容衔接保留。

参考 Android README、功能清单、启动器 ADR／构建说明、图片缓存 ADR、桌面历史实现与采集文档；
独立只读评审核对日期分组索引、缺值语义、字体预算及异步状态。仅修改启动器视图和验收脚本，
不调整数据库、游戏内历史或卡图缓存。完整对局信息移到摘要点击入口，单场顶部信息仍可打开采集版本。

设备为 `OBJVWSPNSCFQU48L`、22041216C、Android 14；目标游戏已安装版本为
`1.0.12825-production-android-d9601ded`。测试前处于启动器首页，游戏进程未运行。
按导出 skill 使用 JDK 24 与持久签名构建并覆盖安装 `0.1.0-test.14`；Lint、内置依赖／插件摘要、
已安装版本验证通过，签名 `Verifies`、v2=true、证书 `CN=BazaarPlusPlus Android Test`。
最终 APK SHA-256：`f0532e3a749f4dac8466c4d6980d00203b7ba50a2cd0a44631593cff608fa9a0`。

- Python unittest 13 项通过；`git diff --check` 及验收脚本语法检查通过。
- `verify_history_browser.py` 普通记录 135 项、多技能记录 201 项真机断言通过；新增双方头像方形、与名称同高同行的边界断言。
- 130% 系统字体下，多技能记录另有 201 项断言通过，覆盖横竖屏重建、所有卡图同时入屏、物品单行等高／实测原生比例、技能尺寸、采集状态、无卡牌字段弹窗、筛选及返回锚点。
- 额外 11 项真机冒烟检查通过：两组不同日期的记录分别打开正确整局、对局／采集版本信息入口、连续刷新后返回以及概念标题缺席；另对已采集布局中的 423 个卡图节点检查 Android CLI 的交互标记，均无点击／长按动作。
- 人工复核默认／130% 字体的阵容横竖屏与默认字体三级页面截图；文字完整，头像位于名称左侧，不挤占物品和技能列。截图人工复核不等同自动图像识别。
- 验证只浏览本机既存记录，未启动游戏、推进战斗或更改账号；结束恢复全部英雄、字体 1.0 及原旋转设置。游戏标准／高级输入模式不影响原生启动器，本轮未回归游戏内功能。
- 初次只读检查误写目标包名导致临时 `HistoryReadDevice` 进程报告数据库不存在；依据兼容清单确认正确目标后只读检查通过。该诊断不是启动器应用崩溃，未初始化或修改错误路径下的数据库；启动器本轮无新增崩溃。

证据在忽略目录 `artifacts/history-redesign/`：`inspection.json`、`tests.log`、`installed/verification.json`、
`normal/`、`dense/`、`font130/` 的断言、布局树和截图，`smoke-checks.json` 及 `runs.png`。
其他设备、极短分屏窗口、更多历史数据和字体缩放级别待专项验证，不将本次单设备结果扩展为全量兼容性验收。

## 启动器竖屏双方阵容间距（2026-10-26，test.14）

用户确认上一版可用后反馈竖屏双方阵容距离过远。`HistoryBattleView.onMeasure` 原先将视口平分给双方，
物品按宽度缩小后仍保留整块高度，导致己方内容下方出现大量空白。现保留原图片预算和公共高度，
仅将对方起点改为己方技能区实际底部加小间距；横屏继续左右并列。
参考 Android README、历史图片缓存 ADR、桌面 `HistoryPanelLayout` 和当前启动器布局；仅修改启动器排版。

设备 `OBJVWSPNSCFQU48L`、22041216C、Android 14；当前目标游戏版本
`1.0.12825-production-android-d9601ded`，测试前停在启动器首页，未运行游戏对局。
使用导出 skill 的 JDK 24、持久签名完成 test.14 构建、Lint 和覆盖安装，签名 `Verifies`、v2=true、
证书 `CN=BazaarPlusPlus Android Test`；内置依赖、插件摘要和已安装版本三项检查通过。
APK SHA-256：`c1201f80b600be44ed4370794e9ed15c36c85bce060f8afa07736d5e7d38081b`。

Python unittest 13 项通过。普通记录的原布局中，己方技能标签底部 y=856，对方标题顶部 y=1346，
中间留白 490 像素；新布局对方标题顶部 y=878，中间留白缩为 22 像素（约 8dp），物品高度保持一致。
`verify_history_browser.py` 新增竖屏内容间距断言，检查普通及多技能记录的横竖屏、旋转恢复、
全部图片入屏、物品单行等高与原生比例、技能尺寸、筛选及返回锚点。
本次普通记录 129 项、多技能记录 195 项设备断言全部通过；人工复核两组横竖屏截图，
中间空白收紧，全部图片完整可见。结束恢复全部英雄和原旋转设置，未出现本日新增崩溃日志。

证据位于忽略目录 `artifacts/history-spacing/`，包含修改前截图和布局树、只读 `inspection.json`、
`tests.log`、`installed/verification.json` 以及 `normal/`、`dense/` 下的布局树、截图和验收结果。
启动器操作不依赖游戏标准／高级输入模式；本轮仅只读浏览保存记录，未推进游戏或更改账号状态。
其他设备、分屏小窗口及放大字体仍待专项验证。

## 启动器单场历史一屏纯图片布局（2026-10-26，test.14）

按用户反馈，将 `LauncherHistoryActivity` 的单场详情替换为 `HistoryBattleView` 固定视口。
横屏左右、竖屏上下排列双方；每方物品只排一行，不显示卡名、品质文字或大边框，点击和长按均不打开详情。
物品根据当前手机版原生 `CardPreviewItem._cardImage` 区域的实测比例排版，双方统一高度，整排宽度不足时共同等比缩小，不再把源材质 PNG 的方形宽高当作卡图比例。
技能图标边长上限为原固定图片高度的四分之一，密集时继续缩小及换行；英雄筛选项在名称旁展示头像。
顶部战斗信息保留对局和采集版本入口，数据库字段、原图缓存及游戏内历史不变。
旧详情滚动容器、详情锚点、文字行、卡牌名称／边框和字段弹窗代码已删除。

参考前轮游戏内历史、图片缓存边界及用户追加约束；独立只读评审覆盖安全区、列表锚点、
异步图片绑定、等高比例和密集间隔预算。最小图片仍无法入屏时，布局先缩小间隔，再求公共图片高度。
`HistoryBattleLayout` 拥有单行宽度、图片高度、原生卡图比例和物品／技能总预算，视图仅按结果摆放。
此前“按 PNG 比例且没裁切”的检查未识别材质纹理与卡图区域的差异；现改用独立实测数据逐张校验最终图片边界的宽高比。小／中／大型克隆的卡图区域分别为 469.0741×935.1852、975.1852×935.1852、1509.074×944.44446；锚点偏移为零，因此比例不依赖所选参考高度。实测原始数据在 `artifacts/native-card-ratios/validated.json`。
产品代码只修改 Java 启动器与验证脚本，未修改正式游戏插件、数据库或图片缓存。为校验真实比例，另用被忽略目录中的临时独立探针在游戏主菜单加载小／中／大型卡牌预制体，仅对克隆设置统一参考高度并读取 `_cardImage` 矩形；未继续对局或推进游戏。完成测量后已停止游戏、从设备删除探针 DLL 和临时报告。

设备 `OBJVWSPNSCFQU48L`、22041216C、Android 14；启动器操作与游戏标准／高级输入设置无关。
按导出 skill 使用 JDK 24、持久签名构建 test.14，覆盖安装成功，内置依赖／插件摘要和已安装版本校验通过。
APK SHA-256：`ce4634de8fe6926ec0aa410a46536536e7fe71b461d6e0766be90f67826a5e90`。
签名报告为 `Verifies`、v2=true、`CN=BazaarPlusPlus Android Test`。

- Python unittest 13 项通过，包含 3,060 组不同窗口宽高、图片比例、间隔、原生小／中／大型比例、0–16 物品及 0–128 技能的混合预算规则检查；签名构建及 Lint 通过。
- `verify_history_browser.py --expect-artwork` 对普通交手通过 100 项；增加 `--battle-sequence 12` 对技能较多的交手通过 160 项。
- 普通交手有 4／5 件双方物品，技能均为空；密集样本有 5／6 件物品和 1／7 个技能。横屏、竖屏及旋转恢复后，单次布局树中的预期卡图数量／模板多重集合完全匹配；每张卡图边界在视口内，每方所有物品的顶部坐标一致，双方物品高度一致。
- 技能图片边长不超过原来的四分之一，无卡牌名称／品质文字、无点击／长按详情动作、无滚动容器；四组采集状态、英雄筛选头像、空／匹配筛选、列表返回锚点及筛选恢复通过。
- 人工复核普通和密集样本的横竖屏截图，全部物品与技能同屏可见，物品保持单行、原图比例和等高，无大空框。自动布局断言不冒充自动图像识别。
- 用户已确认此前其他调整可用；本轮补齐单行约束并复测。脚本结束恢复全部英雄及原旋转设置；无新增启动器崩溃。
- 未将离线极端数量案例视为真机验收；其他手机、分屏小窗口、放大字体及更多技能样本仍待专项验证。

证据：忽略目录 `artifacts/history-fit/single-normal/`、`single-dense/` 下的 `passed.json`、布局树、
`snapshot-portrait.png`、`snapshot-landscape.png`、`hero-filter.png`；`single-loading/verification.json` 为当前 APK 校验。
离线输出为 `artifacts/history-fit-tests.log`。除静态预制体比例测量外，本轮只浏览已保存记录，没有更改对局或账号状态。

## 启动器历史 UI 与本机卡图缓存（2026-10-26，test.14）

参考 `android-mod/History/AndroidHistoryPanel`、`HistoryViewText`、`AndroidHistoryFormation`、
桌面历史页面与采集契约，启动器改为英雄头像、胜场／奖杯和 PvP 对手列表；
战斗详情保留物品槽位、体型占格及技能分组，横屏并列、竖屏上下排列，点击／长按查看图片和完整保存字段。
游戏进程通过 `AndroidHistoryArtwork` 回填当前账号范围内的模板插画，启动器仅异步读取本机缓存。
独立只读评审发现的暂停超时、失败重试、初始化恢复和 Sprite 图集裁切问题已修复。
首轮真机截图发现物品材质 alpha 是着色器遮罩，直接导出 RGBA 会隐藏卡牌主体；
最终卡图改为 RGB24，头像／奖杯保留 RGBA，并以 v2 缓存隔离旧试验图。

本次设备为 `OBJVWSPNSCFQU48L`、22041216C、Android 14，正版游戏
`1.0.12825-production-android-d9601ded`。测试前停在启动器帮助页，有两条皮格历史记录。
按导出 skill 使用 JDK 24 和持久签名构建、覆盖安装 test.14；签名 `Verifies`、v2 为 true，
证书为 `CN=BazaarPlusPlus Android Test`。最终 APK SHA-256：
`db3833e76ba32ebb6dbe2371934770cae4295321c6642f1cc082d344af506762`。
本次 `verify_launcher.py --expect-loaded` 的新会话、内置／部署 DLL 摘要及加载确认共 12 项通过，
游戏停在主菜单，没有继续、购买、选择事件、推进战斗或新开对局。图片缓存完成后关闭游戏进程，重开启动器验证离线读取。

实际检查结果：

- 插件构建零警告／零错误；Python unittest 12 项、HistoryBrowse 展示规则 26 项通过。
- `verify_history_store.py --reader` 在随机隔离目录通过 32 项，包括图片 ID 分页、去重、非法模板跳过、账号隔离及奖杯边界；不修改用户历史。
- `verify_history_artwork.py` 按只读历史检查结果核对 112 张 PNG，覆盖物品、技能、英雄、奖杯和状态图标；来源版本哈希、尺寸和 RGB／RGBA 格式均通过。
- 最终包 `verify_history_browser.py --expect-artwork` 21 项通过：英雄与卡图实际解码绑定、整局／交手身份、全部保存字段及属性、点击／长按、四组阵容滚动到底、返回锚点、横竖屏重建、空英雄筛选、匹配筛选及详情返回保留条件；结束恢复全部英雄和原屏幕旋转设置。
- 人工复核最终列表、横竖屏阵容和短柄斧详情截图，英雄、裂杯／待确认图标和卡牌主体均可见，物品体型及槽位留白保持。PNG 和无障碍断言不冒充自动视觉识别。
- 当前会话无图片加载异常、无新增原生崩溃；原框架启动钩子的既有诊断不作为本功能修复范围。

证据在忽略目录 `artifacts/history-art/`：`loading/verification.json`、`reader`、
`cache.json`、`final-browser/passed.json` 及截图、`game.log`、`crash.log`。
重复入口为上述脚本的 `--help`；缓存校验需提供已确认的目标游戏目录及只读 inspection。
启动器操作不依赖游戏标准／高级输入设置，本轮没有额外回归游戏内双输入模式。
第二账号／设备、下架模板缺图、磁盘失败重试和大量历史的低内存专项仍待真机验证；
首次升级及游戏版本变化后需进入游戏等待图片回填，当前模板插画仅供参考。

## Github 按钮正式应用身份验收（2026-10-24，test.13）

在 `feat/android-launcher-github` 分支将启动器版本升至 `0.1.0-test.13`（code 13），
高于手机现有 test.12。按导出 skill 使用 JDK 24 与持久签名构建，Lint、内置依赖校验、
v2 签名验证通过。APK SHA-256 为
`b74d2745bc2338a00b4eb22415e013621a8d404ead841c40a0ed9eebf48fb276`。
本次使用本分支插件产物，未混入其他 worktree 的开发内容。

设备为 `OBJVWSPNSCFQU48L`、22041216C、Android 14；测试前停在 test.12
启动器首页。`adb install -r` 成功，安装后包信息确认正式应用 ID
`com.bazaarplusplus.android.launcher`、versionCode 13。保留数据与文件授权，首页仍显示
「可以启动」。本次不启动游戏，不操作账号或对局，不验证游戏版本兼容性或输入模式。

Android CLI 根据控件 ID 点击 Github，Activity Intent 断言
`https://github.com/VIkill33/BazaarPlusPlusForAndroid`，Edge 在前台。
本轮浏览器页面标题未显示仓库名称，因此只确认正确 URL 已交给浏览器，
不计为网页加载完成。返回首页后，打赏弹窗打开并显示「关闭」，自动点击关闭成功。
竖屏默认字体和 130% 字体均通过布局树的按钮标签、可见性、等宽（误差不超过
1 像素）及纵向对齐断言，人工复核文字完整、没有重叠。

首次横屏尝试中 Android CLI 采集恢复到竖屏，旧 `landscape.png` 不算横屏证据。
改用 `wm user-rotation lock 1`，ADB 当前横屏截图定位后向上滚动两次，
`landscape-adb.png` 的实际尺寸为 2460×1080；人工复核按钮区可滚动到达、
打赏与 Github 完整并排。这是截图人工验收，不宣称自动视觉断言。
130% 字体与旋转测试结束后，系统字体恢复 1.0、自动旋转恢复 1、user_rotation 恢复 0。
崩溃缓冲区仅有测试前的历史记录，本次无新增崩溃。

证据在 `artifacts/github-button-test13/`：`install.txt`、`portrait.json/png`、
`font130.json/png`、`browser-activity.txt`、`donate.json/png`、
`landscape-adb.png`、`assertions.json`、`settings-before.json` 和 `crash-buffer.txt`。
`verify.py` 保留本轮正式应用的 CLI 验收步骤；横屏用 ADB 单独验证。
正式 APK 为 `artifacts/launcher/BazaarPlusPlus-launcher.apk`，已保留安装在手机上。
无浏览器异常分支、其他设备和游戏启动回归仍未验证。

## 首页 Github 按钮（2026-10-24）

参考 Android README、功能清单、本验证记录、启动器 ADR／构建说明、桌面插件
AGENTS／MEMORY／架构索引及 `docs/design.md`，核对现有
`LauncherActivity` 与 `activity_launcher.xml`。改动仅涉及原生启动器：首页
「打赏作者」与「Github」等宽并排，后者以系统 ACTION_VIEW 打开
`https://github.com/VIkill33/BazaarPlusPlusForAndroid`，无法处理时显示提示。

离线执行 `python3 -m unittest discover -s bazaarplusplus-android/tests -v`，9 项通过；
当前 worktree 的插件重新构建成功（零警告、零错误），签名 APK 构建及内置依赖校验通过，
Lint 通过，签名报告确认 v2 与 `CN=BazaarPlusPlus Android Test`。
原工作区的插件产物包含尚未提交的 History 源码，准备校验拒绝后改用本 worktree 新产物，
没有引入该并行功能。

设备 `OBJVWSPNSCFQU48L`、22041216C、Android 14，测试前为现有启动器首页。
现有应用版本号 12，高于本分支基线 10，覆盖安装被系统拒绝；保留原应用和数据，
临时使用独立应用 ID `com.bazaarplusplus.android.launcher.githubtest`，其余首页实现相同。
测试包通过 USB 安装确认后启动；未授权文件访问、未启动游戏、未操作账号或对局。
原版游戏安装可识别；本次不涉及游戏版本兼容性及标准／高级输入模式。

Android CLI 按控件 ID 自动操作，布局树断言两按钮文字、同一纵向范围、等宽（误差
不超过 1 像素）和左右分离；点击 Github 后通过 MIUI 外部应用确认进入 Edge，
Activity Intent 断言完整仓库 URL，浏览器页面标题确认目标仓库已加载。
随后返回首页，点击打赏按钮，断言「关闭」控件存在并执行关闭。
共 8 项设备断言通过；人工复核 `home.png` 与 `donate.png`，按钮完整并排，赞赏码弹窗正常。
测试使用竖屏默认字体；横屏、放大字体、无浏览器异常分支及正式应用身份覆盖安装未验证。
独立测试应用在验收后移除，返回原启动器首页。

证据：`artifacts/github-button/` 的首页／浏览器／打赏截图、布局树、
`browser-activity.txt`、`assertions.json`、测试构建及签名记录；
正式应用身份 APK 保存在 `artifacts/launcher/`，没有覆盖手机现有高版本应用。
临时 Gradle 应用 ID 已恢复，不属于提交内容。

## LookingIN 客户端末尾 RX 段（2026-10-24，test.10）

用户确认 test.9 原版可启动、第三方客户端失败。设备仍为 `OBJVWSPNSCFQU48L`、
22041216C、Android 14，目标 `com.lookingin.bazaar.mod`，游戏版本
`1.0.12825-production-android-d9601ded`、Unity 6000.3.11f1。
01:46:52 的 `LibraryUtils` 首先报告 `No writable last PT_LOAD`，随后因运行库副本
未生成而出现 `libil2cpp.so not found`；界面“硬件不支持”只是 Unity 原生加载失败通用提示。
证据为 `artifacts/startup-crash/third-party-failure/`。

从已安装客户端只读复制 `libil2cpp.so`，ELF 检查确认新增最高地址段为 RX，并包含迁移的
程序头表。`utilities/library.cpp::padded_dlopen` 现在允许最后 PT_LOAD 为 RW、R 或 RX，
只扩展其内存大小以预留整页池，保留原段权限；`allocate_setup_injected` 只对新池设置权限。
填充失败时清空分配状态并停止启动，不再对空池调用 mprotect 或继续连接不存在的副本。
`verify_launcher.py --package` 可核对当前所选客户端的会话与插件摘要，避免误读原版路径。

本次验证入口 `verify_launcher_padding.py --help`：使用指定 NDK 编译生产
`library.cpp`／`allocator.cpp`，在已授权 arm64 真机的独立 `/data/local/tmp/bpp-launcher-padding`
目录执行测试，不操作游戏状态。RW／R／RX 三类合成 ELF 均通过 dlopen、导出函数调用、
池内指令写入与实际执行、容量限制以及失败重置；末尾程序头布局与本次客户端一致。
测试源码在 `tests/native/`，日志为
`artifacts/startup-crash/third-party-failure/native-padding-tests.log`；Python 离线 9 项通过。
签名 test.10 已构建并覆盖安装。实际启动第三方客户端后，新的 1 MB 池、IL2CPP 初始化、
代理生成与插件加载均完成；使用 `--package com.lookingin.bazaar.mod` 的 12 项加载断言
全部通过，`test10-client/current.png` 人工复核登录页、LookingIN 标记及 BPP 入口可见。
未操作登录或账号表单、未新开对局，停止设备操作供用户继续验证；不扩展为登录完成或
游戏内功能全部通过。证据为 `artifacts/startup-crash/test10-client/`，本次插件摘要
与该 APK 内置输入和新会话匹配，不能用后续并行修改后的 DLL 替代本次证据。



## Activity 元数据包名修复（2026-10-24，test.9）

test.8 已越过 DEX 只读限制，随后于 01:37:50 在 `GameInstrumentation.prepare`
抛出 `NameNotFoundException`：查找的是启动器包下的 `UnityPlayerActivity`。
`BootstrapActivity` 的原始 Intent 使用宿主 Context 构造，使组件包名成为启动器；
配置保护适配又改为直接使用该组件查询安装元数据，从而引入回归。
证据为 `artifacts/startup-crash/test8-failure/`。

`BootstrapActivity` 现在为原始 Intent 显式设置已选游戏包名与原始入口类名；
`GameInstrumentation.prepare` 按游戏包名和原始入口类读取元数据，避免使用宿主包或
注入的 `FusionConfigActivity` 类名。原始游戏主题和横屏设置仍沿用既有流程。
离线 9 项通过，签名 test.9 构建、覆盖安装通过；本轮实际点击启动，
01:40:17 Unity／IL2CPP 原生库加载成功，随后完成 Cpp2IL 分析并进入代理生成。
本次新会话的 12 项加载断言全部通过（含会话、插件版本、三处 DLL 摘要及生成代理）；
当前进程日志未再出现可写 DEX、Activity 元数据查询或未绑定的配置回调崩溃。
截图先经历黑屏过渡及游戏 Loading 100%，最终 `final-screen.png` 人工复核已进入
现有皮格对局的事件选择画面，插件四个入口可见；进程仍为本次的 17440。
代理仅执行一次启动及只读证据采集，未操作登录表单、选择事件、购买或推进对局，
到达现有对局后停止设备操作。不扩展为预览功能、野怪技能或原始小米 9 故障全面通过。
证据目录 `artifacts/startup-crash/test9-launch/`，离线日志为
`artifacts/startup-crash/test9-offline-tests.log`。


## Android 14 拒绝可写 DEX（2026-10-24，test.8 修复候选）

用户安装 test.7 后报告启动失败。当前连接设备为 `OBJVWSPNSCFQU48L`、22041216C、
Android 14；本轮截图及 01:33:22 的 logcat 一致显示 `SecurityException: Writable dex file ...
fusion-config-activity.dex is not allowed`，栈在 `GameRuntime.prepareGameContext` 创建
`GameClassLoader`，本次尚未进入 Unity。历史 crash 缓冲区的 `nativeHidePreservedContent`
错误发生在此前启动，不作为本次异常。证据为被忽略的
`artifacts/startup-crash/test7-failure/`。

[Android 14 动态代码加载要求](https://developer.android.com/about/versions/14/behavior-changes-14#safer-dynamic-code-loading)
要求文件在写入内容前就设为只读。`LauncherFiles.writeReadOnly` 现在在同目录创建临时文件，
打开输出描述符后先设置只读，再写入 APK 内置字节、同步并原子替换目标；旧的可写缓存和
已有只读版本均按同一路径替换，失败清理临时文件。`GameRuntime.copyConfigActivity` 使用该方法。
不改变游戏持久目录、登录数据和已选游戏，不将其他启动问题标记为已解决。

离线 9 项通过，新增回归执行生产文件方法，检查可写旧缓存迁移、只读文件升级、字节内容、
最终不可写权限、替换失败时保留目标及临时文件清理；日志为
`artifacts/startup-crash/test8-offline-tests.log`。test.8 签名构建及覆盖安装成功，内置输入、插件与已安装版本三项断言通过，证据为 `artifacts/startup-crash/test8-installed/`；已停在启动器首页供用户验证。
游戏启动与原始小米 9 故障恢复仍待当前构建真机结果，不据离线检查宣称完整启动成功。


## 启动闪退诊断候选（2026-10-24，待真机验证）

参考 Android README、功能清单、启动器 ADR-0001、BUILDING、桌面插件架构／MEMORY
及当前启动链；未修改游戏内插件或共享桌面规则。用户提供的 `launcher-log5` 显示
小米 9、Android API 28、arm64、游戏 `1.0.12825-production-android-d9601ded`，
准备完成后在“正在打开游戏界面…”阶段发生主线程 `Error`，导出只留下 Unity 版本等包装信息。
现有证据无法判断真实异常类型，不能将此候选标记为已修复启动闪退。

从连接设备读取同版游戏 APK，仅提取 Java 字节码到被忽略的
`artifacts/startup-crash/`，分析后删除本次复制的 APK。`dexdump.txt` 中
`com.unity3d.player.a.Q.uncaughtException` 构造含设备信息的 `Error`，清空其调用栈，
再通过 `initCause` 保留原始异常。`LauncherPreparation.watchCrashes` 原先只记录外层，
所以错误信息被单行上限截断且没有原始栈。现在由 `LauncherLog.throwableLines` 分条记录
最多八层原因链及各层前六帧，复用脱敏和单行上限；循环链与过长链明确截断。
`BootstrapActivity.failAndFinish` 同样保留原因链。诊断仍由用户主动导出，不自动上传。

截图线索经上游原文核对：
[Il2CppInterop #283](https://github.com/BepInEx/Il2CppInterop/issues/283) 报告 Windows x64
钩子定位崩溃，明确说明元数据解析成功；
[PR #291](https://github.com/BepInEx/Il2CppInterop/pull/291) 增加 x64 签名与回退处理。
它们不足以证明本次 Android arm64 故障根因，未据此替换框架。

本轮检查：
- Python 离线 9 项通过；Java 回归覆盖 Unity 的空外层栈、嵌套原因、脱敏、循环链、
  过长链及原因栈实际写入日志。证据为 `artifacts/startup-crash/offline-tests.log`。
- 签名构建 `0.1.0-test.7` 通过，内置输入校验通过；签名报告 `Verifies`、v2 为 true，
  证书 `CN=BazaarPlusPlus Android Test`。构建日志为 `artifacts/launcher-apk-build.log`。
- 构建包含同一工作区的并行 Unity 预加载／配置变更修改，不属于独立验证包；
  用户明确要求保留双方修改并不安装混合构建，本轮没有执行覆盖安装或游戏启动。
- ADB 识别到设备 `OBJVWSPNSCFQU48L`、型号 22041216C、Android 14，检查时为启动器首页。
  它不是报错的小米 9，未将该连接状态、历史结果或构建成功当作本次真机通过。

剩余验收：在独立且来源一致的构建中，于受影响的小米 9 重现并导出新诊断，
核对“异常原因”与对应栈后再修复实际故障；确认前不提交为已验收功能。


## 三入口收起、拖动与吸边（2026-10-23）

参考 Android README、功能检查清单、本文既有设置记录、桌面
`BppDockButtonVisuals` 与 `input-and-settings.md`；从源码核对原按钮布局，未使用原 UI 截图。
保留图鉴、构筑和预览模式入口的业务回调及可见条件，由 `AndroidFloatingDock` 统一管理
位置和收起状态，使用原游戏字体及现有按钮样式；不修改桌面共享规则。
游戏设置新增「BPP：按钮自动收起」，默认关闭，开启后闲置 5 秒收成 BPP 小框，点击展开，
也可手动收起。任一按钮拖动整组、小框单独拖动，松手吸最近安全区域边缘。
`AndroidDockSettings` 使用独立配置文件保存开关、边缘及归一化位置，临时文件写完再替换。

本次设备 `OBJVWSPNSCFQU48L`（Redmi Note 11T Pro、Android 14），游戏
`1.0.12825-production-android-d9601ded`，横屏 2460×1080、界面缩放 100%、中文。
测试前为已有对局画面，重启后在大厅验证；未新开对局、购买、选择遭遇或推进战斗。
初始高级输入，切换标准模式后复测，最后恢复高级模式。已安装同签名更新 APK，
启动器内置插件、当前设备 DLL 及加载会话摘要一致，12 项加载断言通过。

- Python 9 项测试、`BuildCorpus.Tests`、`EnchantPolicy.Tests` 通过；新增 `DockSettings.Tests`
  覆盖损坏／未知配置、保存恢复、写入失败时保持旧值，全部通过。
- 插件编译零警告、零错误；签名 APK 构建、内置文件摘要及签名验证通过。
- 标准、高级输入模式各一轮 `verify_floating_dock.py`：小框左／上／右／下四边吸附、
  三个按钮分别拖动后不执行点击、点击展开、手动收起、闲置自动收起均通过。
  每轮断言 7 条吸边日志，无新增异常、游戏指令或场景变化。
- 设置关闭后即时恢复三个按钮，重新开启后恢复小框；配置文件对应值及截图通过检查。
- 图鉴打开后入口组隐藏，关闭后恢复；日志与矩形检查通过，隐藏效果由截图人工复核。
- 完全结束启动器进程后从首页重启，未手动替换 DLL；自动收起仍开启，配置内容相同，
  小框恢复到原边缘与位置，重启前后矩形坐标误差不超过 2 像素，三项断言通过。
- 初版小框中心触摸被设备边缘手势拦截，放大后四边中心拖动通过。初版使用
  `PlayerPrefs.GetFloat` 触发代理缺方法，改用整数后又观察到加载期间偏好变化，
  最终改用独立文件；以上失败版本不计入通过结果。

设备重复验收入口 `verify_floating_dock.py --help`，需要 Pillow；先启用自动收起、关闭设置，
对照当前截图提供安全区左边距。本次传入设备序列号及 `--safe-left 100`。
截图矩形断言仅识别插件按钮填色和尺寸，不代表文字、卡牌或任意游戏界面的语义识别。
证据位于被忽略的 `artifacts/floating-dock/`：`advanced-final/`、`standard-final/` 保存
截图、矩形与日志断言，`installed-session/` 保存加载校验，`advanced-restored.png`、
`collection-open.png`、`collection-closed.png` 保存输入模式及面板恢复截图；
`restart-before.json`、`restart-checks.json`、`restart-after.png` 保存重启恢复证据。
其他分辨率、界面缩放、第二台设备、多指并发及旋转中的拖动仍待验证。


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


## 按住预览模式（2026-10-23）

参考桌面预览／输入架构文档、Android 原有悬浮按钮与观察式预览，核对本手机版生成代理
`CardController.CurrentTooltipData`、`TooltipParentComponent.ShowCardTooltipController`、
`Touchscreen.touches` 和 `ExtendedPointerEventData.touchId`。新增第四个「按住预览」按钮，
仅高级模式可用。最上层 5% 黄色输入遮罩消费操作，只调用提示框接口；原有三个按钮继续负责拖动整组。
物品附加预览沿用长按偏好；切换目标与松开时清理观察面板，原生切换未完成的提示保留归属直到关闭。

设备 `OBJVWSPNSCFQU48L`（22041216C）、Android 14、2460×1080，游戏
`1.0.12825-production-android-d9601ded`。自动测试时恢复已有皮格第 7 天商店，未购买、
移动、出售或选择遭遇。初版旧 Unity 输入读取抛出异常，已改为事件触点状态；随后快速松开模式按钮
暴露原生提示晚完成后重现，已增加切换状态等待与退役提示抑制。失败证据保留，未当作通过。

`HoldPreview.Tests` 执行生产手势状态，验证高级模式门控、持有者身份、两种松手顺序、多个触点排空、
失焦清理与重新激活；Python 9 项、BuildCorpus、EnchantPolicy 与插件零警告／错误构建通过。
签名 APK 构建通过。`verify_hold_preview.py` 与 `tests/device/HoldGesture.java` 提供可重复双指注入，
坐标必须来自当前截图。旧候选的 `final-shop`／`final-holder-first` 日志断言均通过，覆盖商店私人喷气机
和场上铲子，原物品摘要未变，无对局命令、状态转换和新增异常。截图人工复核展示、黄色提示及关闭；
打开图鉴确认旧候选输入已恢复。这些结果只证明注入路径，不能代替真实触屏。

用户随后手动测试指出：按住有黄色遮罩，第二根手指点物品或事件无反应。
`user-touch.log` 对应操作只有模式进入／退出，没有 `hold_preview.shown`，旧候选因此未通过人工验收。
旧实现无法从该日志区分第二触点未进入 UI 回调与目标射线不可用，不据此断言唯一根因。
修复候选改为在 `LateUpdate` 直接采样游戏 Input System 的原始多点触摸与当帧短按，
持有者由原始 touchId 跟踪，不依赖 UI 模块对第二指的事件分发；遮罩继续消费原生点击。
新增触点与不可用目标的分类日志，用来核对真实触屏路径。修复候选完成编译／签名打包；一次覆盖安装被设备取消后停止操作，获得用户明确同意后重新覆盖安装，
当前会话 12 项加载与摘要检查通过，插件 SHA-256 为
`08411b8c57eec37cf721e0a3899028991aed2edeca1a918be1098aaed223258b`。
恢复用户现有的皮格第 1 天事件选择画面后，分别验证小圆猪发射器、丛林遗迹及事件的先松开模式按钮路径。
`raw-item/`、`raw-event/`、`raw-holder-first/` 各 10 项日志断言通过，均记录了原始触点 ID 与目标打开，
无退役后重新观察、原物品变更报告、对局命令、状态转换或新增异常；截图人工复核原生详情与附加内容、
黄色提示及关闭，未选择任何事件。测试后保持原画面并停止设备操作。
用户随后明确确认真实双指下物品和事件均可预览，并补充确认野怪自测成功。
三类目标的真实触屏展示以用户确认验收；野怪没有本轮代理采集的截图与日志，
不将该确认扩展为所有野怪、奖励数值或生命周期边界均已覆盖。

证据在被忽略的 `artifacts/hold-preview/`，包括历次构建、加载摘要检查、手势回归、
`final-shop/`、`final-holder-first/`、`input-restored.png`、`user-touch.log` 与 `raw-touch-*.log`。
仅高级输入模式属于本功能范围；物品、事件和野怪的真实触屏预览已由用户确认通过。
失焦／恢复场景、自动收起衔接、连续换目标、滚动与所有奖励数值仍未全面验收，保留为边界待验证。

## 按住预览视觉改版（2026-10-23）

按用户选定的细金边方案叠加聚焦效果，参考根 `docs/design.md` 的游戏原生 UI 边界、
桌面预览／输入架构及 Android 既有提示观察实现。`AndroidHoldPreviewVisuals` 提供安全区域内的
淡金细框、「预览中 · 松开退出」提示、当前目标浅金描边和背景压暗；卡牌碰撞盒投影与原生预览
矩形提供目标边界，现有原生详情和附加面板区域保持原色。`AndroidPreviewFocusLayout` 对亮区并集
做矩形扣除，压暗块不重叠。透明输入盾继续拦截所有触点，视觉挖空不产生输入穿透。
入口组在按住期间只隐藏绘制，保留持有按钮与原有手势状态，松开恢复，避免右侧入口覆盖详情。

设备 `OBJVWSPNSCFQU48L`（22041216C）、Android 14、2460×1080，游戏
`1.0.12825-production-android-d9601ded`。恢复已有皮格第 1 天野怪选择画面，原输入为高级模式，
自动收起开启，物品附加预览靠左。仅继续已有对局、操作预览与设置，未选择野怪、购买、出售、
升级或推进战斗。Android CLI 截图可用，布局服务安装失败，交互回退 ADB 和现有双指注入脚本。

早期候选在原生详情访问器与 `RectTransform.GetWorldCorners` 数组调用处出现原生崩溃。
临时逐步日志确认卡牌八角投影完成，而 UI 矩形数组调用前有日志、调用后没有；崩溃缓冲区有对应
SIGSEGV。最终实现观察已有控制器／内容字段，UI 四角改用局部矩形逐点变换和相机投影，
不再调用该数组接口。失败截图、日志与崩溃记录保留，不计为通过；临时诊断代码已删除。

最终插件 SHA-256：`abb22281a2f0dddb2797dc8e799ff49e8c93c107f235cddbc81ba2a4ef3b671a`。
插件零警告／错误编译、签名 APK 及内置输入验证通过；当前构建 12 项加载／会话／摘要检查通过。
启动尚未完成时一次检查拒绝了旧确认记录，待新握手完成后复查通过。Python 9 项、BuildCorpus、
EnchantPolicy、HoldPreview 手势与聚焦几何覆盖检查通过；几何检查覆盖重叠、越界、相邻、退化和
全屏亮区，断言每个背景采样点恰好压暗一次，亮区不压暗。

最终构建的 `visual-final-switch/` 在一次按住中查看小圆猪发射器与腺体，12 项断言通过；
`visual-final-monster/` 查看蕉宝，11 项断言通过；`visual-final-holder-first-retry/` 覆盖先松开
模式按钮、另一指仍按住的路径，11 项断言通过。均无新增错误、原生崩溃缓冲区变化、物品变更报告、
对局命令或状态转换；物品日志有 `source_unchanged=True`。截图人工复核细框、提示、目标切换、
背景压暗、原生详情／附加预览保持明亮、按钮避让及松手恢复，不声称自动视觉断言。
首次先松手注入因工具准备期间入口自动收起而未进入模式，不计通过；脚本新增 `--expand-dock`
在准备完成后展开，重新验证通过。`--next-target` 提供可重复的连续换目标路径，脚本同时保存并
比较操作前后的原生崩溃缓冲区，避免只检查插件错误日志漏掉原生崩溃。

通过原生设置切换标准模式，人工复核「仅高级模式」禁用状态，按住后日志断言没有启动预览且
没有新增错误。随后恢复高级模式，关闭设置与菜单，停留原野怪选择画面，入口恢复自动收起。
事件新视觉、其他野怪／所有体型、失焦与场景恢复、多设备及更大 UI 缩放仍待扩大验证；
本轮不验证实际奖励数值。证据位于被忽略的 `artifacts/hold-preview/visual-*`，最终源代码构建
日志为 `visual-final-build.log`，APK 构建日志为 `visual-final-apk-build.log`。


## 预览左侧贴边与遮罩加深（2026-10-23）

参考本文件的按住预览视觉改版记录、Android README／功能清单、桌面预览与输入架构文档、
`TooltipPreviewModePolicy`、`BppDockButtonVisuals` 及 Android 现有实现。截图中的左侧空隙来自
`Screen.safeArea.xMin`；`AndroidHoldPreviewVisuals.Refresh` 改为屏幕左右各留 8 个参考像素，
按屏高缩放；`AndroidFloatingDock.Bounds` 改用物理屏幕水平范围，保留上下安全区域。
背景黑色 alpha 从 0.22 提高到 0.55，沿用既有卡牌／详情挖空与透明输入盾，不改变预览数据规则。

当前设备 `OBJVWSPNSCFQU48L`（22041216C）、Android 14、2460×1080，游戏
`1.0.12825-production-android-d9601ded`；高级输入、自动收起开启、附魔附加预览靠左。
签名测试 APK 覆盖安装后从启动器进入，加载与本次会话／DLL 摘要 12 项检查全部通过。
启动期间第一次检查尚未得到新握手而拒绝旧记录，完成启动后重新通过。
继续已有皮格第 1 天野怪选择画面，仅操作设置、悬浮入口与预览，未购买、出售、升级、选择野怪或推进战斗。

离线：Python 9 项、BuildCorpus、EnchantPolicy、HoldPreview 手势与聚焦覆盖检查通过；
插件构建零警告／错误，APK 内置输入校验及签名验证通过。插件 SHA-256：
`db062dbbbed8632fbe073e4b7d1d2b8e54726c4514da55e3f8d9f117bcb4a6b0`。

真机：
- `edge-left-confirm.png`、`edge-expanded.png` 人工复核收起／展开入口组左边为屏幕边缘。
- `edge-switch/` 一次按住连续查看小圆猪发射器、腺体，12 项断言通过；截图人工复核左右金框、
  更深的背景遮罩、卡牌／原生详情／附加面板保持原色及松手恢复，无物品变更报告、对局命令、状态推进或新增崩溃。
- 标准模式 `edge-standard-expanded.png` 复核左侧贴边与「仅高级模式」禁用外观；
  `edge-standard-check.json` 断言按住不启动预览、同一日志会话及无新增错误。随后恢复高级模式。
- `edge-holder-first-retry/` 查看左侧野怪并先松开模式按钮，11 项日志／崩溃缓冲区断言通过。
  该路径截图拍摄时预览已退出，只据日志确认显示及退出，不把截图当作野怪新遮罩视觉验收。

本轮尝试将左侧入口拖回右边，注入未形成有效右移，其中边缘滑动打开了系统返回菜单；
关闭菜单后在已确认的左侧位置重跑先松手路径通过。`edge-holder-first/` 因入口坐标不匹配而没有
启动预览，不计通过；右边拖动复测仍待验证。当前保留高级模式与左侧自动收起入口。
多设备、界面放大、事件新视觉、系统边缘手势与场景恢复仍待扩大验证。
证据均位于被忽略的 `artifacts/hold-preview/edge-*`；截图为人工复核，没有自动视觉内容断言。


## 按住预览松手保留野怪面板（2026-10-24）

参考 Android README、功能清单、既有按住预览验证记录及桌面预览／输入架构文档，
核对 `AndroidHoldPreview` 的原生提示归属清理、`AndroidTooltipObserver` 的
`_monsterPinned` 生命周期和 `AndroidMonsterCardGallery` 的卡图／说明交互。
原统一松手清理调用 `Hide`，覆盖了野怪面板本身已有的保留逻辑。
本次在按住预览结束时保留已打开的野怪面板，仍清理物品和事件附加预览；
换目标、进入战斗和切换场景沿用现有清理，空白与 × 仍负责关闭野怪面板。
共享桌面规则及原生提示接口未修改。

离线：Python 9 项检查、HoldPreview 手势／聚焦几何、BuildCorpus、EnchantPolicy
全部通过；使用固定框架与 Android 生成代理构建插件，零警告／错误。
`verify_hold_preview.py --expect-monster-retained` 新增松手保留日志断言，
`released.png` 仍须人工复核，不能将日志事件等同于完整视觉验收。

初次检查时，设备 `OBJVWSPNSCFQU48L`（22041216C）停留启动器「正在准备运行环境」，
用户要求先完成代码与离线检查，因此当时未部署或操作设备。随后用户明确要求安装并自行验证。
本次重新构建插件及签名 APK，覆盖安装启动器 `0.1.0-test.10` 成功，并从首页启动游戏；
`verify_launcher.py --expect-loaded --not-before-ms` 的 12 项检查全部通过，确认新会话、
本次磁盘 DLL、APK 内置 DLL 与插件握手摘要一致。插件 SHA-256：
`015758a570a7303114ba3933b1ea95c4a73d6d16b86bf45f21153195ac401906`。
APK SHA-256：`2bf05a650fe49d8a177c2434586c1eb33ec0b2446bbfa793045fd5f2afbbac61`。
签名 v2 校验通过，证书为 `CN=BazaarPlusPlus Android Test`。代理仅检查加载，未操作对局。
用户安装本次构建后明确反馈「验证通过」，确认本次松开按住预览后保留野怪面板功能通过真实触屏验收。
该验收来自用户确认，没有本轮代理采集的功能操作截图／日志，不作为自动视觉断言。
两种松手顺序、物品／技能说明、空白和 × 关闭、连续换目标、跨场景清理及
物品／事件关闭与标准模式禁用未逐项报告，保留为扩大验证边界。
安装构建日志、初始／安装截图和本次加载证据保存在被忽略的
`artifacts/monster-hold-retention/`（`install-build.log`、`installed.png`、`loaded/verification.json`）；
APK 构建日志为 `artifacts/launcher-apk-build.log`。


## 帮助页内容精简（2026-10-24，用户确认真机通过）

参考 Android README、功能清单、启动器 ADR-0001、BUILDING、桌面设置架构与
`LauncherActivity`、`LauncherGames`、`LauncherSettingsActivity` 当前实现，
在 `activity_launcher_help.xml` 将首次安装五步合并为三步，合并重复的客户端选择与故障说明；
补齐高级模式双指按住预览、松手保留野怪面板、按钮组拖动及自动收起，
将游戏内面板位置说明修正为后续预览生效。保留原生 ScrollView、宽度约束、视觉资源和返回交互，
未改插件规则或启动链。布局由 XML 与 Activity 核对，设备 UI 由用户自行验证。

本次检查：

- JDK 24 下执行 `:fusionApp:processDebugResources --console=plain` 成功，资源编译与链接通过。
- `python3 -m unittest discover -s bazaarplusplus-android/tests -v`：9 项通过。
- XML 解析、内容与入口文案核对、`git diff --check` 通过。
- ADB 检测到已授权设备 `OBJVWSPNSCFQU48L`（22041216C）。工作区已有其他未提交的启动链与
  原生加载修改，初轮仅检查资源；后续按用户要求构建安装，结果见下节。

后续专项覆盖：从首页打开帮助，检查竖屏、横屏和放大字体下滚动到底部，
顶部／底部按钮与系统返回均回到首页，全文无截断。该页面独立于游戏输入模式，
本次未重新验收文案描述的插件功能。后续安装与用户验收结果见下节。


### 帮助页验证包覆盖安装（2026-10-24）

用户随后明确要求安装并自行验证。按导出 skill 使用 JDK 24 与原签名完成当前工作区构建，
APK 为 `0.1.0-test.10`，签名验证 `Verifies`，v2 为 true。
覆盖安装至 `OBJVWSPNSCFQU48L` 成功，并打开启动器首页供用户检查；没有启动游戏。
`verify_launcher.py` 的内置输入、插件摘要和已安装版本 3 项断言通过，
证据位于 `artifacts/help-update-installed/`，构建日志为 `artifacts/launcher-apk-build.log`。
APK SHA-256：`1fb0364b7b870186001ed3dac808520721fa22460be6c00421c743c3bb15864d`。
该包包含当前工作区既有启动链修改。用户自行验证后反馈「完美，提交并推送」，
确认本次帮助页真机验收通过；未逐项提供横屏、放大字体及三种返回路径的操作结果，
这些专项覆盖不据此推断为全部完成。本次提交仅包含帮助页及对应文档。


## 对局历史 M0 存储与互斥验证（2026-10-24，首轮）

参考历史实施方案、桌面 `RunLogSessionManager`、`PvpBattleSnapshotCollector` 和四组快照模型，
对照本地手机版生成代理核对 `NetMessageRunInitialized.RunId`、
`NetMessageCombatSim.MessageId`、`CombatSimHandler.Simulate`、`Data.Run` 与
`ClientCache.Profile`。UI 只核对现有源码及实际游戏首页；历史页面尚未实现。
独立只读评审补充游戏存活锁、账号身份和量化采集预算，修正隔离验证的并发读时点。

环境：设备 `OBJVWSPNSCFQU48L`，Redmi 22041216C，Android 14；两个客户端均为
`1.0.12825-production-android-d9601ded`，包名分别为 `com.tempogames.thebazaar` 和
`com.lookingin.bazaar.mod`。启动器为本轮工作区构建的 `0.1.0-test.10`，包含先前已有的
未提交启动链修改，不将其归为历史功能改动。此次为存储与启动链验证，不依赖输入模式；
标准／高级历史 UI、PvP／PvE 和卡牌数值均未验收。

### 已验证

- Python 离线回归 10 项、插件构建（零警告）、BuildCorpus 与 EnchantPolicy 测试通过。
  签名构建通过，`apksigner` 为 `Verifies`，v2 为 true，证书为
  `CN=BazaarPlusPlus Android Test`。具体源码和插件摘要由构建记录持有。
- 初次临时探针经 Unity AndroidJNI 代理在 .NET 后台调用时触发原生 SIGSEGV；
  已删除该桥实现，改用启动器独立 JNI ABI。之后按库名加载失败，按绝对路径加载得到
  无 JVM 的独立库实例；改为启动器发布进程内已初始化入口，见 ADR-0002。
- LookingIN 目标下两次后台 JNI 调用各自创建随机命名的隔离 SQLite 库，
  校验提交、外键拒绝、写事务未提交期间独立读取已提交版本、回滚与关闭重开。
  返回请求 UUID、目标包名、实际目录与能力标记均自动断言通过；Java 非法 UUID 异常
  成功传回 C#。两次隔离存储验证耗时为 156 ms、287 ms；不是业务写入延迟或帧预算。
  隔离文件已清理，目录只保留访问锁，未创建 `history.sqlite`。
- 真实游戏租约持有时，从独立 Android `app_process` 尝试同一目标，退出码为 3；
  完全停止启动器后同目录退出码为 0。另在独立测试目录启动持锁进程，竞争者退出 3，
  杀死持锁进程后竞争者退出 0；测试目录与 DEX 已清理。JVM 测试另覆盖同进程重入、
  同目录别名、不同目标、重入失败后原锁仍有效及 I/O 错误不误报为占用。
  重复检查入口为 `verify_history_access.py --help` 与 `tests/device/HistoryLockDevice.java`。
- 读取账号缓存时观察到先为全零 UUID，稍后才有有效账号；恢复现有对局可读到 Run ID。
  全零身份不能作为已确认作用域。未执行账号切换，也未证明消息 ID 在重复投递或重连后稳定。
- 临时战斗观察器使用加载器现有原生 hook 接口成功安装；直接使用 Harmony 时被
  `MonoMod.Core.Platforms.Systems.AndroidSystem` 创建 `/data/.cache` 的权限错误阻断。
  未因此改写框架、系统 HOME 或拷贝桌面程序集。安装成功只证明挂钩可建立，不证明已采集战斗。

### 尚未通过的门槛

两个客户端读到同一账号。正版恢复的是已有排位局，未购买、选择事件／野怪或推进战斗；
测试客户端首页同样只有「继续」入口，未提供休闲新局入口。用户授权范围为推进休闲对局，
因此本轮不推进或结束该排位，也不为测试弃局；用户随后选择保留排位，仅完成不推进对局的部分。
尚缺实际 PvP／PvE 的战斗前四组快照与结果、可靠战斗身份、账号切换及采集耗时／队列预算。
M0 整体未完成，未进入 M1–M4，自动保存、浏览、双方阵容、异常恢复、导出与导入均未实现。
未提交为已验收功能。第二台设备及原 Android 9 设备均未测试。

证据在被忽略的 `artifacts/history-m0/`：`python-tests.log`、`build.log`、
`corpus-tests.log`、`enchant-tests.log`、`storage.json`、`final-probe.log`、
`probe-session.json`、`device-lock-running-mod.json`、`device-lock-after-stop-mod.json`、
`device-lock-death-recovery.json`、`crash.txt` 和截图。截图均为人工复核，
不声称有自动视觉断言。临时采集源码只保留在此忽略目录，未装配到正式插件入口。

最终构建复测：正版目标的新会话／插件／APK 摘要等 12 项 `verify_launcher` 断言全部通过。
同一版 JNI 隔离测试再次通过，耗时 141 ms、161 ms；证据为 `final-loaded/verification.json`、
`final-storage.json` 与 `final-official-probe.log`。本轮两目标均能确认后台 Java 调用与目录归属，
尚未验证账号切换。停止游戏后第一次紧接重开首页的锁检查仍返回占用，按互斥规则保守拒绝；
再次确认测试进程退出后，`final-lock-released-retry.json` 的独立进程取得锁断言通过，
不以首页可见作为已关闭证据。

临时 HistoryProbe DLL 及其设备侧输出已从两个客户端撤下，测试进程已停止，默认启动目标恢复正版。
未清除、迁移或覆盖用户历史／游戏账号数据。最终验证包 APK SHA-256 为
`fc1a6610c17f1eb94d7f77c7b02b2695888d84640bcbd74cacde51f437fe5643`；
插件 SHA-256 为 `babaabcdf64d204abeb6874ed55ce3bdf146dcfc6f1173fa7696fbd3ee42bdde`。
此包用于 M0 验证，历史功能仍不可用；不据此宣称原 Android 9 故障或整个方案已通过。

### 休闲对局采集续测与定稿（2026-10-24）

用户完成原排位后，明确选择普通模式，新建 `Unranked` 对局，并在重启后恢复同一
Run ID。仅推进已授权的休闲局。临时探针对 `CombatSimHandler.Simulate` 的原生入口
复制托管 DTO；原调用始终继续，序列化和文件写入在后台执行。

第一天 PvE 的 `Data.CurrentState.StateName` 为 `Combat`，小时为 3；四组均采集成功，
我方物品／技能数量为 2／0，对方为 4／1，合计 190 项属性，DTO 为 5,224 UTF-8 字节。
胜者为 `Player`，原模拟消息处理成功并进入 `ReplayState`。第一天 PvP 为
`PVPCombat`、小时为 6，数量为 3／0／5／0，合计 252 项属性，DTO 为 6,495 字节；
胜者为 `Opponent`，处理结束后天数变为 2、胜负计数为 0／1。两场都保留品质、体型、
实例／模板身份、位置、附魔、标签和属性；空技能组为成功采集的空数组。

首轮采集耗时分别为 72.9503 ms、26.264 ms，尚未达到可交付性能结论；增加逐卡预热后
仍须复测战斗采集。原生卡牌 `Name` 实测为空，需要从模板本地化标题保存名称回退；
PvE 对方英雄为 `Common`，必须另外保存怪物模板身份，不能以该英雄字段代替怪物名称。

PvE 可取得 `CurrentEncounterId`，PvP 对应字段为空。点击原生重放会再次进入
`Simulate`，消息 ID 与刚结束的 PvP 相同，但当前天数／小时已变为 2／0；因此不能
用采集时的天数／小时识别新战斗。正式观察器必须在 `ReplayState` 排除回放，且原始
战斗身份不能依赖尚未证明跨重连稳定的短消息 ID。该身份策略仍须补齐并验证。

临时桥压力验证同时发起 16 个请求：容量 4，接受 4、明确拒绝 12；已接受调用结束后
再次调用成功。该结果证明有界桥的拒绝和恢复，不代表尚未实现的业务单写队列已经通过。
证据为忽略目录中的 `casual-battles.jsonl`、`casual-observations.jsonl`、
`casual-first-verification.json` 及 PvE／PvP 截图。自动断言覆盖同一休闲 Run、四组状态、
PvP 胜负计数和重放消息关系；卡面截图为人工复核，不是自动视觉识别。

增强探针重启期间另一任务覆盖安装不含历史桥的 test.11，当前输出出现 native host
unavailable；未把该次会话计为历史验证通过。用户随后授权本任务独占手机。
依导出 skill 构建临时 `0.1.0-test.11-history-m0`（code 11），避免降级或清除数据；
该版本号仅用于本次忽略目录验证产物，源文件版本已恢复。本次新会话加载 12 项断言通过，
探针 SHA-256 与设备一致，JNI 和原生钩子均重新确认生效。

增强采集第二天 PvE：数量 3／0／5／1、252 项属性、6,887 字节，快照 14.8132 ms；
第二天 PvP：数量 4／0／5／1、280 项属性、7,658 字节，快照 14.1516 ms。
后者完整入口（含回放判断、元数据、任务投递）为 27.2342 ms，不能把仅快照计时写成
完整入口。两场结果均由原模拟 Task 的 `RanToCompletion` 和窗口绑定消息再次确认；
所有已采集卡牌名称非空，包括中文名称。PvE 沙尘暴教学会暂停模拟，点击教学后正常
结束；因此不能把展示耗时视为数据库或采集延迟。重放实测记录 `replay_skipped`，
不新增采集行。预热首卡 40–45 ms、后续 1–8 ms；性能预算及边界由 ADR-0002 定稿。

另核对 PvE `CurrentEncounterId` 等于该怪物 `BoardManager` 日志中的 TemplateId；
战斗开始时 `GetCurrentEncounter()` 可返回空，模板身份应保留原始字段，不能依赖当时
仍有原生 encounter 实例。独立评审通过本地窗口 UUID、持久序号、completed 去重证据及
跨进程身份歧义隔离规则；这些是 M1 实现要求，不是业务恢复已经通过的证明。
第二账号不可用，真实切换移入 M4 保留待验证，不退出或替换用户登录。

自动断言验证增强 PvE／PvP 四组、中文回退非空、Task 结果、压力拒绝与恢复、回放排除。
证据追加 `casual-results.jsonl`、`casual-pressure.json`、`casual-final-log.txt`、
`pve2-log.txt`、`casual-loaded/` 及 `validation-build/`。临时包 SHA-256：
`9d4c0b00de88e938693e07f955cb230a8f02dd37170d00dd2fb2e3c6608e7a43`。
测试结束停止游戏，删除设备临时探针和其输出，确认两目标插件目录均无该 DLL，恢复原
test.11 APK 并自动核对设备与源 APK 摘要相同，证据为 `test11-restored.json`。
没有清除账号、游戏进度或历史目录。

至此 M0 采集／存储基线和设计定稿闭环，进入 M1；自动记录、浏览、阵容、恢复和文件
交换仍未实现。原 Android 9、第二设备、真实账号切换、大阵容及业务故障恢复均未验收。


## Android 9 初始化读取兼容修复（2026-10-24，待原设备验收）

用户提供的 test.10 诊断中，小米 9、Android API 28、arm64、正版客户端
`1.0.12825-production-android-d9601ded` 连续五次启动／修复均在
`GameRuntime.copyConfigActivity` 抛出 `NoSuchMethodError: InputStream.readAllBytes`。
该方法在 Android API 33 才提供；此路径在游戏初始化之前执行。
更早的两次 Unity 主线程错误内容被截断，不能据此次修复宣称已解决。

参考启动器 ADR-0001、BUILDING、既有初始化验证记录、
`GameRuntime.copyConfigActivity` 与 `LauncherFiles.writeReadOnly`。
本次新增 `LauncherFiles.readFully`，循环读取到 EOF 后交给现有只读 DEX 文件事务；
资源读取失败仍传播 IOException，由原调用方处理，不安装部分字节。
不改游戏 API、原生钩子、插件规则或界面交互。工作区原有启动链和历史 M0 改动保留，
不将其归为本次修复。

验证范围：

- Python 离线回归 10 项通过，新增空流、跨缓冲区短读、available 返回 0、
  禁止调用 InputStream.readAllBytes 以及中途 IOException 的检查。
- 生产 `LauncherFiles` 额外以 Java 8 API 编译，约束本读取工具不引用 Java 9 流方法；
  该检查不是完整 Android API 兼容性证明。
- `:fusionApp:compileDebugJavaWithJavac` 通过。完整 `:fusionApp:lintDebug` 未通过：
  报告工程其他位置 32 个错误、197 个警告，涉及返回手势、API 等级、AppCompat tint 和翻译。
  本次修复的读取方法及调用点没有 Lint 错误；没有关闭 Lint 或加入忽略基线。
  本次未打包签名 APK；完整工程检查仍未通过。
- 当前连接设备 `OBJVWSPNSCFQU48L`（22041216C，API 34／Android 14）在独立
  app_process 中运行本次生产文件类及 LauncherFilesTest，通过读取及既有文件事务断言。
  D8 min-api 26，测试目录为独立的 /data/local/tmp/bpp-android9-init-fix，运行后已删除。
  日志为被忽略的 `artifacts/android9-init-fix/device-file-tests.log`。
  未安装混合 APK，未重启游戏或操作账号、对局；本轮文件测试与游戏输入模式无关。

原小米 9 当前未连接，未验证 Android 9 完整初始化、修复、游戏加载和插件握手。
后续须在原设备覆盖安装对应修复包，分别重试启动与修复，确认不再出现该方法缺失，
并收集完整原因链或 logcat 判断是否仍存在更早的 Unity 主线程崩溃。
本项的原 Android 9 设备验收仍待完成；后续 Android 14 结果见下节。


### 当前 Android 14 手机启动回归（2026-10-24）

用户随后要求在当前手机验证。按 APK 导出 skill 使用 JDK 24 与原测试签名构建
`0.1.0-test.10`；Release assemble、内置输入校验和 lintVitalRelease 通过，
签名报告 Verifies、v2 true、证书 CN=BazaarPlusPlus Android Test。
这不代表上节完整 lintDebug 的 32 个错误已经解决。
APK SHA-256 为 `e9dbf43f0fa94176d6f823593c24a20692838f0c166c80111671159d58838d02`，
releaseId 为 `6d6b2d2b9896697dcc666ebdffb9b82341adb4ee7298d0f924289e381fd39dec`。

设备仍为 OBJVWSPNSCFQU48L、22041216C、Android 14、2460×1080，
目标正版游戏版本 `1.0.12825-production-android-d9601ded`。
测试前截图为已有皮格商店界面；覆盖安装会停止旧进程，本轮未操作对局。
安装后读取设备 base.apk 的 SHA-256，与此次本机 APK 完全一致。
首页横屏通过 Android CLI 按资源 ID 滚动并点击启动；没有改选客户端。
本轮只测试启动链，不切换或验收标准／高级输入模式，既有输入偏好保持。

最初加载检查拒绝旧握手；本次插件加载完成后，新会话 12 项 verify_launcher 断言
全部通过，包括新启动边界、会话一致、插件版本、APK／磁盘／握手 DLL 摘要和生成代理。
插件 SHA-256 为 `babaabcdf64d204abeb6874ed55ce3bdf146dcfc6f1173fa7696fbd3ee42bdde`。
最终截图人工确认游戏主菜单、继续按钮和 BPP 收起入口可见；没有点击继续。
本次进程 PID 22868 持续存活，启动后 logcat 中没有 FATAL EXCEPTION、Fatal signal、
NoSuchMethodError 或配置保护 DEX 安装失败。游戏日志有 Unity 辅助功能方法、
MANAGE_GAME_MODE 权限等错误及一次登录 401，随后自动进入主菜单，
因此只声明没有观察到启动崩溃，不声明游戏日志无错误。
未填写登录表单、未清除用户数据、未操作对局，也未执行完整修复或全新无缓存初始化。
原 Android 9 设备和更早 Unity 崩溃仍待专项验收。

证据在被忽略的 `artifacts/android9-init-fix/current-phone/`，包括
`installed-apk.json`、`verify-attempt.log`、`loaded/verification.json`、`final.png`、
`final-status.json` 和 `final-process-logcat.txt`。截图为人工复核，没有自动视觉断言。
签名构建日志为 `artifacts/launcher-apk-build.log`。
当前安装包包含工作区原有未提交的启动链及历史桥代码；本轮验证不能视为这些功能的验收。
本次提交包含配置保护加载链及其必要的只读 DEX、Activity 元数据与旧版 Android 读取修复；历史存储及其他并行改动未纳入提交。

提交前另对暂存区快照执行离线回归，9 项全部通过；该快照不包含工作区历史互斥测试，因此与完整工作区的 10 项计数不同。


## 对局历史 M1 事务存储与规则（2026-10-24）

目标为 `OBJVWSPNSCFQU48L`、22041216C、Android 14。沿用 M0 的 Java 系统 SQLite、
独立 JNI 和游戏进程租约，不引入桌面 SQLite 依赖。`HistoryStore` 拥有 schema、作用域、
Run、持久窗口、四组快照及幂等证据；`HistorySessionCore` 负责原始观察窗口和晚回调规则。

`verify_history_store.py` 编译实际生产 Java 存储代码，通过独立 `app_process` 在随机 UUID 的
`/sdcard/BppHistoryM1Check/` 下验证，与游戏数据完全分开。34 项断言通过：schema／WAL、
四组快照原子提交、外键失败回滚与同请求重试、重复 original／结果、冲突隔离、同时间多场、
旧序号通知、身份失效、重启待确认、同 Run 续接与新 Run 替换、账号隔离和本会话暂存归属、
进度前移且短消息改变时禁止重新授予 partial，以及较新、未知、损坏库保留原字节。

空间不足注入使用写连接的 SQLite 页上限，实际触发 `SQLITE_FULL`；原战斗及四组快照保留，
序号不前进，恢复上限后同请求成功。早期注入首先落在 WAL 读连接，未触发预期错误；固定写连接后，
又发现 SQLite 自行回滚导致 `endTransaction` 的后续异常掩盖原始错误。`finishTransaction` 现在
保留原始错误，将清理错误附加为 suppressed，修复后整套断言通过。不把受控页上限等同于
真实填满设备存储，也没有为测试填满手机。

同脚本的 `--kill-transaction` 使用隔离目录，在外层未提交事务内调用实际 `HistoryStore.write`，
打印已到达边界后对该测试 PID 执行 SIGKILL；新进程重新取得租约、打开同库，6 项断言通过：
已提交父 Run 身份保留、未弃局、整场未提交 opening 全部丢弃、序号和 checkpoint 未消耗、
完整性及外键通过。没有在生产代码添加暂停或故障开关。这项是 SQLite 事务边界测试，
实际游戏强制停止的验收单独记录，不用它代替游戏结算与生命周期验证。

C# 的 `HistorySession.Tests`、`HistoryBridge.Tests` 通过。后者直接链接生产 Bridge，验证单写
顺序、容量满显式拒绝、前置失败阻断后续、显式恢复、DTO camelCase 与中文／补充字符。
生产插件及签名 APK 构建通过；证书及 v2 校验通过。临时验证包使用已安装 test.13 的同版号，
原安装 APK 已只读备份；该临时包不作正式发布。当前 APK、校验、详细输出均在被忽略的
`artifacts/history-m1/`，其中 `store-device-final.json` 和 `store-sigkill.json` 是通过证据。

本条仅验收事务存储、桥与纯规则。实际采集器、可见失败／重试、整局正常结算和游戏强杀恢复
仍在独立验收，历史浏览与备份尚未提供；真实第二账号及 Android 9／第二设备仍待验证。


## 对局历史 M2：启动器只读浏览（2026-10-25）

本条只验收启动器浏览，不代表 M1 完整对局或 M2 游戏内面板完成。设备为
`OBJVWSPNSCFQU48L`、22041216C、Android 14；启动器原生触屏页，竖屏 1080×2460、
横屏 2460×1080。使用实际休闲皮格对局已保存的 7 场记录，其中第 3 场因先前强杀只有
战斗前快照，必须显示“结果未记录”；第 1 场有部分／失败组，不补写或猜测缺失数据。

`HistoryReader` 仅以 OPEN_READONLY 打开既有库，不调用会创建目录的 Utilities 路径方法，
无库、较新版本和损坏分别处理；corruption handler 保留原文件。列表分页使用时间＋ID，
战斗使用天／小时／持久序号，列表只投影必要元数据，选中战斗才用单 SELECT 读取四组快照。
Scope 由宿主固定：启动器显示本机档案，游戏读取预留账号或本会话 pending 限制，不能通过
请求 ID、archive 筛选或 continuation token 越权。没有在此阶段接入游戏内读取桥。

`verify_history_store.py --reader` 在随机隔离 UUID 目录运行实际生产 Reader，16 项通过：
46 个同时间整局、43 场同小时战斗多页无重复或固定总数上限、新插入不扰动旧 continuation、
显式首页刷新、账号／pending 会话和直接 ID 限制、四组状态与中文补充字符、写事务期间 WAL
读已提交事实、目标不匹配拒绝，以及较新／损坏文件保持原字节。无库浏览不创建目录。

`LauncherHistoryActivity` 固定进入时的目标游戏；单线程只保留一个等待查询，版本拒绝旧结果。
翻页成功才提交 token，失败保留列表并重试原目标页。头部进入列表滚动，避免横屏说明挤空内容；
旋转保存当前页、选择和滚动锚点，详情重新读取，不将大快照放进 Activity 保存状态。
真机发现旧 onBackPressed 覆盖未接管当前 Android 系统返回，改用生命周期绑定的返回 dispatcher。

可重复入口 `verify_history_browser.py` 从已打开的启动器历史列表开始，与
`verify_history_store.py --inspect-game-root` 的只读事实对照；不启动游戏或修改历史。
16 项自动断言通过：真实整局与战斗计数、卡牌 ID／品质／体型／类型及全部实例属性、长按相同卡牌、四组状态
和所有已存卡名滚动至末尾、未知结果换选、系统返回与精确列表锚点、横竖屏重建及返回整局列表。
首次滚动脚本速度过快跳过中间卡牌，减慢手势并保留视口重叠后重测；不把首次失败计为通过。
截图人工复核，文本／保存值和返回路径有自动断言，不宣称自动视觉比对。

首次前后比较的“最后观察时间”跨越较早游戏会话而不一致，未计为只读完整性通过。随后确认无
游戏加载进程，重新取 before 基线、执行浏览验证、取 after，整个 summary（含最后观察时间、
整局元数据、7 场及 28 行快照和生产 Reader 响应）相等。浏览未改变这些已保存业务字段。

10 项 Python 离线回归、Java 8／Android API 编译、签名 APK 构建及启动器包的 3 项只读校验通过。
当前验证包为临时同版号 `0.1.0-test.13-history-m1`，APK SHA-256：
`6609c0a9134a1db6a7dab413b2c1663cff2eccdb977f577a6ba3e5aec33438ca`；设备 base.apk 摘要一致，
签名 Verifies、v2 true、CN=BazaarPlusPlus Android Test。首页沿用已安装 test.13 的 Github 入口，
不因新增历史而回退该按钮；没有发布或推送。本包包含工作区原有启动链修改，不把浏览验收视作
这些修改的验收，也不以本条的 3 项包校验替代游戏启动的 12 项新会话校验。

证据在被忽略的 `artifacts/history-m1/`：`reader-device.json`、`reader-controlled-before.json`、
`reader-controlled-after.json`、`reader-final-integrity.json`、`browser-ui-controlled/` 和
`browser-launcher-verification/`。M1 正常结束及完整一局、M2 游戏内原生卡图／互斥／两种游戏输入、
启动器真实多页与故障 UI、空阵容 UI、切换另一目标数据、第二账号和第二设备仍待验证；
Reader 隔离分页／故障测试不替代这些 UI 或真实账号验收。


## 2026-10-25：整局正常结算、游戏内历史与历史阵容

本轮按用户要求优先完成正常记录和历史查看，进一步异常恢复专项后置。设备仍为 Redmi 22041216C／Android 14，官方客户端 1.0.12825，2460×1080；只推进已授权的普通对局。原安装 test.13 已留存，验收使用临时 history-m1 包；APK、日志、账号事实与截图仅保留在忽略的 `artifacts/history-m1/`。

- `History/AndroidRunObserver` 观察可靠账号、原生对局与阶段；`AndroidHistoryNativeCapture` 在实际 Simulate 前复制四组阵容，任务正常完成后另存结果；主线程只投影快照，后台执行序列化和 SQLite。Replay 不建立新战斗。`HistoryRunContinuity` 只在明确回放／结算阶段保留同身份的最后可靠进度，不能因原生对象消失推算结算或把缺失进度写零。
- 前一普通对局已正常进入 EndRunDefeatState，数据库为 completed、结束时间非空、0 胜 7 负、11 个唯一战斗序号；从第 3 天开始记录的事实在界面明确标注。此局中一场只有开局而无结果，界面显示“结果未记录”。这不是从第 1 天开始的完整验收。
- 最新游戏内 `AndroidHistoryPanel` 提供当前账号的历史列表、整局详情及选中战斗阵容，按开始时间／稳定 ID 分页；独立有界读取不占写入失败状态。关闭重开保留选择，从阵容返回整局可继续滚动到第 11 场。场景／战斗进入关闭和请求身份检查已实现；真实第二账号切换尚未验收。
- `AndroidHistoryFormation` 使用保存的位置／体型排布双方物品与技能，独立合成实例和属性字典；不会把真实对局卡牌传给预览。初次实机发现原生预览零尺寸造成黑块／放大，已按现有阵容实现初始化尺寸并按完整外框缩放。另发现 SetUp 重建模板默认属性，现于其完成后恢复独立 clientCard 的已存属性并重新初始化 gem；第 10 场帝王之刃显示 Toxic、伤害 80、施毒 8，与快照一致，不再显示模板 50／0。
- 标准模式点击打开只读保存字段，详情长文本可滚动到底并关闭；高级模式长按打开、松手关闭。物品、技能、附魔、四组完整性与旧局列表／整局／阵容已做截图人工复核；不能称为自动视觉识别。输入偏好已恢复原高级模式。历史保存字段为事实，当前模板描述仍仅供参考。
- 新普通对局从第 1 天起记录，首场 PvE 和 PvP 均正常获胜，两个窗口分别恰好一条，已进入第 2 天。读取对比断言旧局 11 场／44 组快照的全部输出字段不变，见 `game-history-normal-integrity.json`；新局尚未完成，不能宣称开局到整局结算已验收。
- 当前插件编译 0 警告／0 错误；正常展示 6 项纯规则断言和串行读取／待处理选择替换／Unicode 测试通过。最新包 `game-history-values-loaded/verification.json` 的 12 项新会话与摘要校验均通过。截图包含 `history-values-enchanted-formation.png`、`history-values-advanced-held.png`／`released.png`、`history-standard-fields.png`／`bottom.png`；只读事实见 `game-history-values-records.json`，正常结算见 `normal-end-*`。

限制：当前未实测排位推进，不推断排位完整验收；段位／评分尚未采集；原生对象清空后的结算分支只有既有纯规则／存储证据，未在此轮强制制造。部分冷采集曾超过主线程目标预算并留下部分阵容，界面如实区分；完整性能门槛仍未通过。游戏内多页真实账号、第二设备、备份交换及异常恢复专项仍待后续完成。


## 2026-10-25：按整局浏览 PvP 历史与原生卡图

按用户选择的方案 2 继续游戏内历史，参考桌面 `HistoryPanelView`、`PvpBattleSnapshotCollector`、Android ADR-0002 及既有三层浏览。当前实现保留稳定 Run 分组及按需快照读取，每局仅展示 PvP 交手，列表、计数和直接详情查询均排除旧 PvE。新 PvP opening 增加对手名称；旧记录缺名明确显示“玩家名称未记录”，不从当前对手补写。

野怪战斗未被其他核心功能依赖。`AndroidHistoryNativeCapture` 只在 PvP 原始模拟采集，`HistorySessionCore`／观察器忽略 PvE；`HistoryStore.begin` 对旧调用方的 PvE 请求直接返回 ignored，不保存快照、窗口、幂等证据或诊断载荷，也不消耗序号和检查点。整局身份、阶段、进度及结束状态仍独立观察；旧 PvE 数据不删除。没有重开 schema 或迁移旧库。

设备为 `OBJVWSPNSCFQU48L`、Redmi 22041216C／Android 14，官方游戏 1.0.12825、2460×1080。从已有普通局的野怪选择阶段恢复到大厅，仅浏览既有记录和切换输入偏好，没有选择野怪、推进战斗或新开对局。使用 test.14 验证包；包内也含工作区原有启动链修改，本次历史验收不代表那些改动全部验收。

实际观察与修复：

- 游戏内显示两整局：当前局 6 场 PvP、上一局 5 场 PvP；正常结束、待确认续接及中途开始记录提示可见。新会话、磁盘／包内／加载摘要等 12 项校验通过，见 `artifacts/history-m1/pvp-last-loaded/verification.json`。
- 首／末 PvP 的双方物品卡图、技能图标及保存字段已查看。发现 `Socket_0` 被当作一基槽位换算，保险箱与短柄斧、对方首件物品重叠；对照手机版生成代理 `EContainerSocketId` 后，提取 `HistoryFormationLayout.ItemSlot` 按零基位置排布，最后构建截图确认物品逐个可见且保留体型占格。
- 原生卡牌材质没有 RectMask2D 裁切能力；滚动时只显示完整进入视口的原生卡图，名称和点击区域继续用普通遮罩。末场对方七个技能可滚动至第二行，原生图不覆盖底栏。截图为人工复核，不宣称自动视觉断言。
- 标准模式点击打开保存字段并关闭；高级模式长按显示、松开关闭；完成后恢复高级模式。返回 PvP 列表、换选首末战斗、关闭重开保留选择已有本轮观察。真实账号多页及更多输入时序仍待专项覆盖。
- `pvp-last-integrity.json` 对照修改前后全部 92 行战斗／快照读取结果（含原始 opening、四组 payload、结果与身份），完全一致；恢复标记属于启动生命周期，不与保存战斗内容混为只读不变断言。

验证：插件编译零警告／零错误；10 项 Python 回归、HistorySession／HistoryObserver／HistoryBridge 测试及 10 项展示／槽位规则断言通过。真机隔离生产 SQLite 存储 40 项、Reader 19 项通过，覆盖 PvE 零写入、后续 PvP 窗口可用、混合旧库过滤、分页、身份隔离及文件保护。启动器现有文字浏览同步新查询语义，16 项 UI／字段断言及浏览前后全部读取字段一致通过，未增加启动器物品图片方案。

证据均在忽略的 `artifacts/history-m1/`：`pvp-store.json`、`pvp-reader.json`、`pvp-browser-ui/`、`pvp-browser-integrity.json`、`pvp-last-loaded/`、`pvp-last-runs.png`、`pvp-last-battles.png`、`pvp-last-formation.png`、`pvp-last-bottom.png`、`pvp-last-standard-detail.png` 及输入模式相关截图。早期隔离存储测试因 fixture 名称断言仍使用旧“中文怪物”而失败，修正为 PvP fixture 名称后全套 40 项通过，不把首轮失败记为通过。

边界：没有为本轮测试推进实际战斗；新版本实战对手名称、野怪跳过后连续 PvP 采集到正常结算仍待验证。旧记录缺失结果、缺名和部分阵容不会补造；原生描述依赖当前游戏模板。真实多账号／多设备、排位实战、备份交换和进一步异常恢复专项仍未完成。本地修改区分已验证的浏览／布局与待验证的新实战采集，不标记完整历史功能全部验收。


## 2026-10-25：构筑候选默认不选

参考桌面 panels 架构及 AndroidBuildPanelController.Refresh／SetOpen，移除首次刷新自动并入场上物品的逻辑；每次打开候选为空，刷新只剔除已不在场上／仓库／商店的选择。用户确认三个初始修复全部自测通过。本次 Redmi 22041216C／Android 14、官方游戏 1.0.12825、2460×1080 新会话 12 项加载校验通过，人工复核场上五种物品均未勾选、候选 0→1 及关闭重开归零。未购买、领奖或推进战斗，标准模式专项与其他屏幕比例未在本轮自动复测。

Python 10 项、BuildCorpus／EnchantPolicy 及插件编译（零警告／零错误）通过。证据：忽略目录 artifacts/feedback-fixes/loaded/、build-default.png、build-selected.png、build-reopened.png；截图为人工复核。测试包含工作区原有启动链修改，不将本轮结果视作那些修改的全面验收。


## 2026-10-25：推荐构筑附魔名称标签

参考桌面 text 架构与 AndroidBuildBoard.Select／AndroidBuildPanelController.ResolveName，构筑文字标题经 NativeUi.TitleText 移除原生 TMP style 包装；名称文字及其余富文本保持原有内容，不在无对应样式表的 BPP 标签上输出游戏专用 style。范围覆盖推荐物品详情标题、推荐清单及候选名称。用户确认该修复自测通过；本次真机选中短柄斧后打开带 Obsidian 附魔的皮格健身房详情，日志记录 card_selected，截图保存在 artifacts/feedback-fixes/build-enchanted-title.png（附加预览遮住部分详情，不将此图单独视为完整文字视觉验收）。

沿用本轮新构建编译、10 项 Python、BuildCorpus／EnchantPolicy 和 12 项新会话摘要校验；不修改桌面共享源码。其他语言与全部附魔名称未逐个复核。


## 2026-10-25：移除预览顶部标签（用户自测通过）

用户确认最初三项修复自测全部通过，随后要求直接删除升级、附魔和任务奖励三个标签，并覆盖安装后自行验证。AndroidTooltipObserver 不再创建标签或预留顶部空白；附魔／升级继续由现有长按偏好控制，任务奖励正文合并在当前物品预览之后，避免删除入口时丢失奖励信息。事件和等级奖励弹窗按正文高度收紧，长内容仍滚动，野怪布局保留原规则。参考桌面 previews 架构和现有 AndroidQuestRewardPreview；未更改游戏 API 或共享桌面代码。

最终无标签构建的离线结果及覆盖安装证据见忽略目录 artifacts/feedback-fixes/；本轮按用户要求不继续游戏内自动操作，由用户自行验证；覆盖安装后用户确认“可以了，提交代码”。未逐项提供双输入模式、全部奖励正文或数值对照结果，不据此扩大专项验收范围。较早 final-loaded 的 12 项通过对应尚保留任务奖励按钮的中间构建，不能冒充最终构建真机验收。原有启动链工作区修改不属于本次修复验收范围。

最终构建插件零警告／零错误、10 项 Python 回归及签名校验通过。已在设备 OBJVWSPNSCFQU48L 覆盖安装 0.1.0-test.14，安装包内置依赖、插件摘要及安装版本三项校验通过；设备 base.apk SHA-256 与本地一致：`26b24765e766b4b9cf7958866c787ecd3c8c8a0744340f4ef7b6ba3f9bb1eda4`。安装证据为 artifacts/feedback-fixes/no-tabs-installed/。代理未启动最终版本进入游戏；覆盖安装后用户确认自测通过。


## 2026-10-25：历史方案 1 头像与胜场奖杯（用户真机验证通过）

仅修改游戏内入口：`AndroidHistoryPanel` 分离英雄图、时间／模式、胜场和状态；
`AndroidHistoryArt` 在主线程轮询原生头像与四档奖杯，复用仓库三格状态徽章显示裂杯、白旗和沙漏。
`HistoryViewText` 仅对完整、可靠的已结束进度判定最终奖杯；0–3 胜显示裂杯。
中断不推断投降，真实旧档案仍缺少可靠投降采集证据。

离线：插件编译零警告／零错误，28 项展示与分级断言、HistorySession／HistoryObserver／HistoryBridge
测试通过，10 项 Python 回归通过。首次验证构建的新会话／包内／磁盘／加载摘要 12 项通过；
当前截图已看到两条真实记录和原生英雄图，其中 0 胜行缺杯，随后已补裂杯并重新构建。
最终验证包覆盖安装后，用户确认“可以了，验证通过”。本次界面及演示战绩验收记为用户真机验证通过；
用户未逐项报告的双输入模式、多设备、多账号或实际投降采集不据此扩大验收范围。
最终 APK 版本为 `0.1.0-test.14`，SHA-256：
`04ff1cc5800c2e55c1b02efd17235f971d0c23373b7fa1de3aecd7510cc498ab`。

按用户要求提供独立演示 fixture `tests/fixtures/history-preview.json`，覆盖 0、3、4、7、10 胜，
含黄金、零负钻石、明确模拟投降、进行中及四种对手英雄。
仅在 `BepInEx/config/BazaarPlusPlus.Android/history-preview.json` 存在时显示「演示战绩」开关；
演示使用独立 Page，不调用历史读写桥，底部明确标注演示数据，点击「真实记录」可返回。
删除该配置后重启可隐藏演示入口。此测试 fixture 不修改真实数据库。
设备为 OBJVWSPNSCFQU48L／Redmi 22041216C／Android 14，游戏 1.0.12825，2460×1080。
本轮仅恢复已有普通局、浏览页面，没有选择野怪或推进战斗。


## 2026-10-25：英雄筛选卡片、历史筛选与演示入口清理（用户反馈通过）

参考 `docs/design.md`、桌面 Collection／History 实现及 Android 已有头像列表。
`AndroidChoicePopup.ShowHeroes` 为图鉴与游戏内历史共用的三列头像＋本地化名称选择面板，
增大触控高度，使用高亮与「已选」文字表达当前条件；全部／通用选项使用文字占位。
原 `AndroidHistoryArt` 移至公共适配器 `android-mod/AndroidUiArt.cs`，保留主线程轮询、
Global 资源加载及关闭后解绑，避免筛选面板依赖历史视图内部实现。
`AndroidHistoryPanel.LoadPage` 将英雄条件传给 `HistoryReader.runs`，在 SQL 查询分页前筛选；
切换条件清空旧页游标，详情返回保留条件，账号作用域变化重置条件。
删除 `history-preview.json` 演示 fixture、复制配置、专用 fixture 断言及游戏内演示入口；
保留正式展示规则的独立断言，旧设备配置不再被读取。

按用户“尽管写代码，验证我自己来”的要求，本次未执行编译、测试、打包、部署或真机操作。
待用户覆盖：头像与名称同时显示、全部／通用／各英雄选中反馈、滚动与关闭重开、历史跨页筛选、
无结果提示、切换中旧请求晚完成、详情返回条件保留，以及标准／高级两种输入模式。
历史筛选涉及启动器 Java Reader，验证包需同时包含本次启动器与插件代码。

用户随后要求覆盖安装并启动游戏。本轮已重新编译插件（零警告／零错误），按 APK 导出 skill
构建签名包，v2 签名通过，证书为 `CN=BazaarPlusPlus Android Test`。
在 OBJVWSPNSCFQU48L 上 `adb install -r` 成功，通过首页「启动游戏」启动，
`verify_launcher.py --expect-loaded` 的 12 项会话／摘要检查全部通过。
启动截图显示游戏加载至 94%，功能交互留给用户验证；未进入新对局或进行筛选验收。
APK 版本 `0.1.0-test.14`，SHA-256：
`64ab1ade914a102b6843bf5e02d733f89bd5ae2c0ff5767c8a8beab15d83011c`。
证据：忽略目录 `artifacts/hero-filter-launch-evidence/`、`artifacts/hero-filter-launched.png`。

覆盖安装后用户反馈“不错”，要求先提交本次修改；未逐项报告的专项覆盖保持待验证。


## 2026-10-25：商店／训练师头像筛选卡片（2026-10-26 用户确认通过）

先提交上一轮英雄与历史筛选修改：`3bd8b834`。
本轮参考桌面 `EncounterPortraitSpriteProvider.LoadPortraitCoreAsync`，来源目录已有
`CollectionSourceEntry.PortraitTemplateId`，通过模板 `ArtKey` 加载 `EncounterAssetDataSO`，
读取其 `portraitTextureReference`。已检查本机 Android 生成代理，字段类型为 Unity Sprite，
与桌面读取路径一致；这仅确认 API 签名，不证明每位商人的实际资源可用。

`AndroidChoicePopup.ShowCards` 复用英雄选择的三列大卡片、名称与选中标记；
`CollectionController.ChooseSource` 保留原有英雄可见性、来源筛选和跟随商店逻辑，
为商人／训练师添加头像绑定。全部来源与跟随当前商店保留文字占位。
`AndroidUiArt` 在主线程轮询遭遇资源加载，每帧最多发起两次资源读取，使用 Global 作用域；
关闭选择框即解绑，加载失败时保留文字并按已有间隔重试。

沿用用户自行验证的约定，本轮未编译、测试、打包、覆盖安装或操作游戏。
待验证：各商人及训练师图片、全部／跟随选择状态、相同名称的不同来源、
切换英雄后可见来源、列表滚动、快速关闭重开及两种输入模式。

用户随后要求覆盖安装并启动。插件重新编译零警告／零错误，签名 APK 构建与 v2 校验通过；
在 OBJVWSPNSCFQU48L 上覆盖安装成功，经启动器「启动游戏」启动，新会话／插件摘要等
12 项加载检查全部通过。截图显示游戏加载至 94%；商人图片与筛选交互留给用户验证。
APK 版本 `0.1.0-test.14`，SHA-256：
`055b2f8ca7f485a4bee476679eb9402ea01026f05db039444485608a0e3bb5dd`。
证据：忽略目录 `artifacts/source-filter-launch-evidence/`、`artifacts/source-filter-launched.png`。

2026-10-26 用户反馈“可以了，提交代码”，本轮商人筛选改动记为用户真机验证通过；
未逐项报告的全部商人／训练师、相同名称来源及双输入模式专项不扩大为全部覆盖。


## 2026-10-26：移除顶部历史状态提示条（用户自测通过）

按用户要求，`AndroidRunObserver.Update` 不再创建或更新顶部历史提示条，
删除 `BuildStatus`、提示文字及其专用 UI 状态；等待、记录、保存和异常等状态均不显示。
历史采集、写入、日志及有序重试逻辑保留；原重试测试改为直接调用内部 `Retry`。
参考 Android README、功能清单、既有真机记录及桌面 capture 架构说明与 Android 观察器源码。

插件已通过 `build_probe.py --mod` 编译（零警告、零错误），
`HistoryObserver.Tests` 既有身份隔离、重复采集、有序重试与结束状态连续性检查通过。
新插件位于忽略目录 `artifacts/mod/BazaarPlusPlus.Android.dll`。
10 项 Python 离线回归通过。按用户要求，界面验收由用户自行完成。

后续按用户要求构建并覆盖安装至 Redmi 22041216C（`OBJVWSPNSCFQU48L`）：
`adb install -r` 返回 Success，已安装 base.apk 摘要与新构建一致。
版本 `0.1.0-test.14`，APK SHA-256 为 `e8b4bf04f8ec24495b30b2f3cd5bcece1545ae767c852c5d9a23b9eff668fac8`；
v2 签名及内置最新插件摘要核对通过。本包含工作区已有启动器兼容性修改。
本次代理只覆盖安装，未启动游戏或执行界面验收。
2026-10-26 用户反馈“可以了，提交代码”，本次提示条移除记为用户自测通过；
未逐项报告的状态切换与双输入模式专项不扩大为全部覆盖。


## 按住预览入口仅高级模式显示（2026-10-26，待验证）

`AndroidHoldPreview.Build`／`Update` 按当前输入模式隐藏或显示入口，标准输入模式不再显示禁用按钮；`AndroidFloatingDock.Layout` 同步收紧排列及高度，历史入口上移，切回高级模式恢复。参考现有输入／预览架构和按钮组实现，未改共享桌面预览规则。

按用户要求跳过编译、离线测试、部署与真机验证。本次没有设备操作或新增验收证据，既往标准模式禁用状态的验证不代表本次隐藏行为通过。待验证：标准模式入口不可见、高级模式恢复、运行时切换、历史入口排列及自动收起／拖动吸边。


## 2026-10-26：图标入口、按钮样式与历史入口开关（用户真机确认通过）

`AndroidFloatingDock` 的入口改为 115×115 图标按钮（参考 1080 屏高），按当前可见入口重排、不留空位；
`AndroidDockIcons` 读取嵌入的图标 PNG。长按模式按钮恢复，图标与文字随附魔／升级／无切换，
游戏设置中修改长按预览时同步。`AndroidNativeSettings` 新增「BPP：按钮样式」（图标／纯文字旧版 270×90）
和「BPP：对局历史按钮」（显示／隐藏），`AndroidDockSettings` 以可选字段保存，旧文件默认图标样式并显示历史。
图标来源与 CC BY 3.0 署名见 `assets/dock-icons/SOURCE.md`，`build_launcher.py` 将署名写入启动器许可页 `NOTICE.txt`。

插件经 `build_probe.py --mod` 编译零警告、零错误；DockSettings（含新字段解析与保存）、HoldPreview、
LongPressSettings 测试通过。签名 APK `0.1.0-test.14` 的 `apksigner` 校验通过（v2，`CN=BazaarPlusPlus Android Test`），
内置插件摘要与构建一致，`NOTICE.txt` 含图标署名；覆盖安装至 Redmi 22041216C（`OBJVWSPNSCFQU48L`）返回 Success，
最终 APK SHA-256 为 `dc1f19c0cfafd3204d06e5b5be9ff3aa67ba1270608c804bef81fcac199e77a8`。
代理未启动游戏或执行界面断言，`verify_floating_dock.py` 仅同步了新尺寸、未在本次运行。
用户在设备上查看后确认通过；未逐项报告的标准／高级模式、四边吸附、自动收起、纯文字样式与历史隐藏组合不扩大为全部覆盖。


## 2026-10-26：按住预览可穿过已打开的预览面板（用户真机确认通过）

问题：按住预览期间，另一根手指点击被附加预览面板（`BPP.EnchantPreview`）或原生详情遮住的物品／事件时，
`AndroidHoldPreview.Preview` 只取首个非遮罩命中，命中面板后记录 `target_unavailable` 并放弃，须松手后才能再选。
修正：新增 `AndroidHoldPreview.IsPreviewSurface`，命中位于 `AndroidTooltipObserver.FocusPanel` 或
`CardTooltipController` 下且无法解析卡牌时继续检查下一命中；其他菜单与界面仍阻挡；野怪浮层内的卡图仍可直接解析。

插件经 `build_probe.py --mod` 编译零警告、零错误，HoldPreview 与 PreviewTooltips 测试通过。
签名 APK `0.1.0-test.14` 的 `apksigner` 校验通过（v2，`CN=BazaarPlusPlus Android Test`），
APK SHA-256 为 `ae8218d0331533e0d2fd1083f543ee2e01e8a1a859e2c1ba2c1bd56109939146`，内置插件 SHA-256 `77df0f18c265a108f4488c3ffe0d06a47e74046fe14a078ce76c767a45c39346`；
覆盖安装至 Redmi 22041216C（`OBJVWSPNSCFQU48L`）返回 Success。代理未执行界面操作。
用户在高级模式真机测试后确认通过；野怪浮层下方目标与连续多次穿透切换未单独报告，不扩大为全部覆盖。
构建日志：忽略目录 `artifacts/hold-preview-passthrough-build.log`、`artifacts/launcher-apk-build.log`。

## 2026-10-26：入口按钮独立显示／隐藏与启动器同步（用户真机确认通过）

`AndroidLauncherSettings`（取代 `AndroidPreviewPanelSettings`）在 `bpp-launcher-settings.json` 中新增
`hiddenDockButtons`（collection、builds、previewMode、holdPreview、history），缺省或空数组即全部显示；
插件与启动器 `LauncherSettingsActivity`／`LauncherPreparation` 均读取已有对象后合并写入，保留未知字段。
游戏原生设置以五行「BPP：…按钮」（显示／隐藏）取代原历史按钮行，启动器设置页新增「游戏内按钮」五个开关；
原 `dock.json` 的 `showHistory` 移除，旧值不迁移。帮助页补充按钮显示、样式与对局历史说明，删除“尚无历史”的过时表述。

插件编译通过；`LauncherSettings.Tests`（原 `PreviewPanelSettings.Tests`）、DockSettings、HoldPreview、
LongPressSettings 与 10 项 Python 回归通过。签名包 apksigner 校验通过，覆盖安装到 Redmi 22041216C 后用户确认验证通过；
未逐项报告的双输入模式、启动器与游戏内交替修改等组合不扩大为全部覆盖。

## Android 9 配置变化空 Display 修复（2026-10-24，test.11）

用户提供 `bazaarplusplus-launcher-log8.json`：小米 9、API 28、arm64、正版游戏
`1.0.12825-production-android-d9601ded` 两次启动均越过准备与 Unity 原生接口注册，
随后主线程在 `ActivityThread.performConfigurationChangedForActivity` 因
`Display.getDisplayId()` 的接收对象为空退出。诊断不包含完整系统 logcat。

已对照启动器 ADR-0001、BUILDING、现有初始化验证记录、`GameRuntime.prepare`、
`CustomContextWrapper.getDisplay` 和 `FusionConfigActivity.onConfigurationChanged`。
[Android 9 系统源码](https://android.googlesource.com/platform/frameworks/base/+/android-9.0.0_r1/core/java/android/app/ActivityThread.java)
在配置变化流程直接调用 `activity.getDisplay().getDisplayId()`；原包装层在 API 30 以下
返回 null，替换 Activity base context 后该返回值进入系统流程。此处早于游戏自身的
配置回调，现有 Unity 原生接口保护无法处理这个空值。

修复仅适配宿主显示对象：API 26–29 使用宿主原 Activity context 的
`WindowManager.getDefaultDisplay`，API 30 及以上继续转发宿主 `getDisplay`。
不使用游戏包 context 或固定 display 0，不吞掉系统异常，不修改游戏 API／插件业务。
测试包版本升至 test.11；插件由独立 worktree 的已提交源码重新构建，未带入原目录
尚未提交的历史桥功能。此次未改变游戏内交互。

离线与构建验证：

- Python 回归 10 项通过。新增测试编译并执行生产 `CustomContextWrapper`，
  使用 JVM Android API doubles 覆盖 API 26、28、29、30、34，模拟 Activity 包装层的
  虚调用，验证重复配置查询返回宿主显示对象，旧分支不调用 API 30 公共接口。
  游戏与宿主使用不同的非零 display ID，防止错误转发或固定默认屏幕。
  原实现运行同一测试失败；这是分支行为测试，不是 Android 9 系统或 MIUI 真机验证。
- 插件使用固定框架和现有 Android 生成代理重新构建，0 警告、0 错误。
- 使用导出 skill、JDK 24 与原测试签名打包；assembleRelease、lintVitalRelease 及
  APK 内置依赖校验通过。签名 Verifies、v2 true，证书 CN=BazaarPlusPlus Android Test。
  未重新运行完整 lintDebug，不代表既有工程 Lint 问题已解决。
- APK SHA-256：`7915e7eceedd4945df1eeb9864e5ad116899c5034fb4afd6b42e0491a8afbe89`。
  releaseId：`700a0aab6641b286d46c51064858251091e48b289b3b000afcbbe05af14352c1`。

当前 Android 14 设备回归：

- 设备 `OBJVWSPNSCFQU48L`、22041216C、API 34，正版游戏版本同上，横屏 2460×1080。
  测试前在游戏主菜单；覆盖安装 test.11 成功，设备已安装 APK 摘要与本机修复包一致。
- 通过 Android CLI 按启动按钮资源 ID 发起新会话。初次检查正确拒绝旧握手；加载完成后
  `verify_launcher.py --expect-loaded --not-before-ms` 的 12 项断言全部通过，包括新会话、
  插件摘要、当前会话事件和游戏代理。本次插件摘要为
  `da7fda9b59d4f91e991af1dd84263388d560e04a7608b8d77a7e6f243f653e24`。
- 最终截图人工确认主菜单、继续按钮与 BPP 收起入口可见，PID 32644 保持存活。
  当前进程日志未发现 FATAL EXCEPTION、Fatal signal、NullPointerException 或 NoSuchMethodError。
  日志仍有 Unity 辅助功能方法、MANAGE_GAME_MODE 权限、自动登录 401 及设备原有独立
  History M0 Probe 的 native host unavailable 错误；未宣称游戏日志无错误或历史功能通过。
- 本轮只测启动链，未切换或验收标准／高级输入模式，未点击继续或操作对局，未清除账号数据，
  未验证完整修复与全新无缓存初始化。Android 14 结果不能替代 API 28 分支的真机验收。

本次证据位于被忽略的 `artifacts/display-context-fix/`；签名构建记录在
`artifacts/launcher-apk-build.log`，测试包及签名报告在 `artifacts/launcher/`。
原小米 9 未连接，须使用 test.11 重试初始化及启动，确认不再出现空 Display 配置异常，
并重新收集诊断以发现是否有下一处兼容问题。此轮原 Android 9 专项验收未完成；后续用户反馈与提交授权见 test.12 节，不将概括反馈扩展为小米 9 全部路径通过。


## Unity 基础库 ZIP 消费文件名修复（2026-10-24，test.12，用户验证通过）

用户提供 `bazaarplusplus-launcher-log (3).json`：test.11、API 37、arm64、正版游戏
`1.0.12825-production-android-d9601ded`，已进入 BepInEx Preloader。在提取 Unity
基础程序集时先报 `InvalidDataException: End of Central Directory record could not be found`，
之后继续初始化 Chainloader，最终 SIGABRT，栈落在 libcoreclr.so。记录的页面大小为 4096，
不能据此归因于 16 KB 页。诊断没有设备型号、完整 ZIP 路径、实际坏文件摘要或完整托管调用栈。

参考启动器 ADR-0001、BUILDING、既有内置依赖验收、`LauncherPreparation.prepare`、
固定框架 `BepInEx.Unity.IL2CPP.dll` 的 `Il2CppInteropManager.DownloadUnityAssemblies` IL，
并与 [BepInExFusion 源码](https://github.com/All-Of-Us-Mods/BepInExFusion/blob/master/Runtimes/Unity/BepInEx.Unity.IL2CPP/Il2CppInteropManager.cs)
交叉核对。IL 所用 DLL 已逐字节比对本工程固定框架 ZIP 内文件，避免仅凭可变上游源码推断。
它从配置提取来源 URL 的 AbsolutePath，再取 GetFileName，与 unity-libs 组合、检查 Exists，
最后 OpenRead 并交给 ZipArchive；已有同名文件不会重新下载或校验。

原准备流程安装并校验 `6000.3.11.zip`，却把来源 URL 写成末尾为 `Managed.zip` 的地址。
框架因此消费另一份 Managed.zip；缺失时尝试下载，已有损坏缓存时直接报 ZIP 中央目录错误。
既有真机记录保留了已生成代理，不能证明首次代理生成实际消费过正确 ZIP。
该缺陷可确定复现，并与此次首个错误吻合；脱敏日志不能确认用户实际坏文件内容，
也不能证明后续 libcoreclr 中止没有独立原因。

修复沿用现有依赖校验与原子安装，将实际 ZIP descriptor 的 name 传给
`LauncherUnityLibraries.configure`，只在 IL2CPP 节更新 UnityBaseLibrariesSource 为本地文件名；
缺少键或节时补齐，其他配置保留，拒绝 URL、越界路径及非 ZIP 名称。
清单缺少或包含多个基础库 ZIP 时在准备阶段拒绝继续，不让框架选择未校验来源。
原 Managed.zip 不再被读取，不删除它或账号、游戏持久数据；来源 URL 保留供构建及追溯。
本包继续包含 test.11 的旧版 Android Display 修复。

离线验证：

- Python 回归 11 项通过，包括生产 Java 配置方法的已有 URL／坏缓存、无代理、缺少配置键／节、
  CRLF、空配置、重复执行、其他节同名键和非法来源检查。原 URL 配置在同一夹具选择
  损坏 Managed.zip，复现 ZIP 失败；修复后选择已安装 ZIP 并成功解压。
- Java 夹具由 `LauncherUnityLibrariesTest` 导出，替换为 compatibility.json 固定摘要的真实 ZIP；
  `.NET 10` 的 `UnityLibraries.Tests` 按已核对的消费者文件名规则读取，旧配置复现相同的
  End of Central Directory 错误，新配置成功读完全部 77 个条目；未依赖已有 interop 目录。
  这是桌面 .NET ZIP 消费边界回归，未运行 Android 上的完整 BepInEx／Cpp2IL。
- 构建导出的对应源码包补入两次崩溃修复的 Java、Python、C# 回归测试。

test.12 签名构建：

- 使用导出 skill、JDK 24 与原测试签名；为避免干扰其他测试任务，使用独立 Gradle daemon
  目录，仅复用受锁保护的依赖缓存。assembleRelease、lintVitalRelease、内置依赖核验通过。
- 签名 Verifies、v2 true，证书 CN=BazaarPlusPlus Android Test；未执行完整 lintDebug。
- APK SHA-256：`72103b91eb94b44c967cd5afc15ee81632983b29b9cd36b10a9c17d54cbf7a51`。
  releaseId：`b901cd3ffcf6933c51636ba3e5e4ea1f6db77c15c08f1778a3ffd9dabd0f70f2`。
- 测试包、清单、签名报告与对应源码包另存于 `artifacts/interop-zip-fix/test12/`，
  旧包备份位于同级 `test11/`；下载目录另存带 test.12 名称及 .111 后缀的副本，原 test.11 副本摘要未变。

用户明确要求不覆盖本机安装包，当前手机正在进行其他测试：本次未使用 ADB、Android CLI，
未安装、停止、重启或操作手机应用。原 API 37 用户设备未连接，完整初始化、代理生成、
插件握手、游戏主菜单及后续原生中止是否消失在此轮构建结束时仍待复测。后续用户反馈如下。

证据在被忽略的 `artifacts/interop-zip-fix/`：`pinned-consumer-il.txt`、`offline-tests.log`、
`dotnet-consumer.log`、`consumer-fixture/`；测试源码为 `tests/LauncherUnityLibrariesTest.java`、
`tests/test_launcher_unity_libraries.py` 和 `tests/UnityLibraries.Tests/`。


### 用户验收与提交授权（2026-10-24）

用户收到下载目录的「修复测试版本2」test.12 包后，在本会话明确反馈：
“可以了，用户验证通过，提交吧”。该副本已确认与上述 test.12 APK 的 SHA-256 一致。
据此记录本次启动崩溃修复经用户验证通过，并执行本地提交。

此前补充的两份 test.10 日志分别来自 API 36 和 API 35，均有三次基础库联网下载超时，
随后代理生成失败及相同的 libcoreclr SIGABRT 栈，支持与 test.12 修正的基础库来源缺陷同类。
用户本次反馈未逐台列出验证设备、步骤或新的诊断，因此不声称 API 28／35／36／37
全部逐机验收完成，也不新增自动握手、游戏功能、全新初始化或完整修复的验证结论。
本次验收来源为用户反馈；本机手机继续保持其他测试任务，不重新安装或操作。


## 2026-10-26：BazaarPlusAndroid 更名

参考启动器 ADR-0001、BUILDING、插件设置文档与当前 `LauncherActivity`、
`AndroidNativeSettings`、`AndroidFloatingDock` 实现，仅调整产品名称与链接。
项目中英文说明、三种应用名资源、启动器首页／帮助／许可、插件与探针显示名、
游戏内设置、附魔预览标题、折叠入口和构建／发布附件统一品牌；折叠入口使用 BPA。
上游 BazaarPlusPlus 署名、签名证书、应用 ID、插件 GUID／程序集、JNI 类路径、
数据目录与配置键保持兼容。此前验证记录保留当时实际名称。
GitHub 仓库已在现有登录会话中更名为 `VIkill33/BazaarPlusAndroid`，
仓库 About 说明、项目链接及本地 android remote 已更新；未推送源码或发布 Release。

离线与构建：Python unittest 12 项通过，Android 插件／探针编译零警告、零错误；
JDK 24 签名构建、lintVitalRelease 与 APK 内置输入核验通过。
发布校验器验证新附件命名通过，并以实际签名包验证历史附件命名兼容路径。
最终 APK `0.1.0-test.14` SHA-256：
`656c2539174d9d80eb7b97edbaab42f5eeb9073fcc59342d59944e43c3a0b9c0`。
签名 Verifies、v2 true，沿用 `CN=BazaarPlusPlus Android Test`。

本次设备：22041216C（xaga），Android 14，官方游戏
`1.0.12825-production-android-d9601ded`；测试前停留启动器首页。
覆盖安装保留原有应用数据。Android CLI 获取布局与截图，断言首页和帮助页含新名称，
人工复核两页标题完整显示；帮助页通过文字定位打开。最终链接更新后再次构建并覆盖安装，
从启动器启动游戏，独立新会话、当前 APK／插件摘要及加载时序等 12 项断言全部通过。
首次采集发生于运行环境准备期间，仅旧握手可见，未计为通过；初始化完成后重采全部通过。
未新开或推进对局、购买、上传或清除数据。

证据：被忽略的 `artifacts/rename/` 下的首页／帮助布局及截图、
`evidence/verification.json`、加载日志、`final-release/` 的发行校验输入；
`artifacts/rename-tests.log`、`artifacts/rename-build.log`、`artifacts/launcher-apk-build.log`。
进入游戏大厅后打开设置，截图人工复核长按预览、预览位置、自动收起、按钮样式及
五个入口开关均显示新品牌且未截断；原生游戏字体按既有样式显示为大写英文，未修改设置值。
设置页证据为 `settings-brand.png`、`settings-buttons.png`。
附魔标题和 BPA 折叠条的逐项视觉验收，以及双输入模式、横屏启动器与放大字体组合
仍待覆盖；不扩展为这些路径均已真机通过。


## 2026-10-26：启动器首页第二版按钮网格

参考 `docs/design/launcher-home-grid.png` 与 `LauncherActivity`／`activity_launcher.xml` 原实现。
保留集市背景、品牌、启动前状态、既有点击处理；仅首页使用全宽启动按钮、两行三列带图标按钮与全宽更多入口，其他页面样式不变。

- 设备：`OBJVWSPNSCFQU48L`，Redmi 22041216C，Android 14，1080×2460，440dpi，字体初始为 1.0。起始前台为启动器历史页；本次没有启动或操作游戏对局，不涉及游戏双输入模式。
- 离线：`python3 -m unittest discover -s bazaarplusplus-android/tests -v`，13 项通过；签名构建通过，签名报告 `Verifies`、v2 为 true，证书 `CN=BazaarPlusPlus Android Test`。本机签名包覆盖安装成功，版本仍为 `0.1.0-test.14`。
- 原生入口：八个按钮均有可访问文本，首轮布局树断言全部可见、触控高度不低于 48dp；历史／设置／帮助进入预期 Activity，打赏打开可关闭弹窗，Github 打开外部处理应用，更多打开原菜单，许可弹窗可读取新增图标来源与许可证。重新检查完成后恢复可启动状态。
- 最终尺寸：网格最小高度由初版 76dp 收紧至 64dp，竖屏及 130% 字体下按钮标签完整、排列一致，截图人工复核通过；页脚可滚动查看。
- 横屏：确认实际截图为 2460×1080 后，通过 ADB 滑动至按钮区，六个按钮与更多入口完整无重叠，人工复核通过。Android CLI 布局／滚动服务会令设备回到竖屏，因此其命名为 landscape 的布局 JSON 不作为横屏证据；本轮横屏以 `home-grid-landscape-adb-buttons.png` 为准。系统 uiautomator 未生成预期 XML，不记为树断言通过。
- 测试结束恢复字体 1.0、自动旋转 1 与原 user_rotation 0，并回到竖屏主页。崩溃缓冲区所见记录为 10 月 23 日历史记录，本次未见新增启动器崩溃。

证据（均位于被忽略的 `artifacts/`）：`launcher-home-tests.log`、`launcher-apk-build.log`、`launcher/signing-report.txt`、`launcher/home-grid-checks/results.txt`、`launcher/home-grid-final.png`、`launcher/home-grid-font130-buttons.png`、`launcher/home-grid-landscape-adb-buttons.png`、`launcher/home-grid-comparison-final.png` 与 `launcher/home-grid-buttons-final.png`。

范围限制：本次验证启动器 UI，没有点击启动游戏、执行修复／退出、修改游戏设置、支付或提交外部内容。更大字体、第二设备与分屏待验证。


## 2026-10-26：购牌卡图回填遗漏修复（用户自测通过）

参考 Android README、功能清单、图片缓存 ADR-0003、桌面历史与卡图实现及当前购牌采集路径。
`HistoryReader.artwork` 原先只扫描 PvP 快照，购买后出售、合成或未带入战斗的牌可能只存在于 `card_purchases`，无法进入图片导出队列。
现按宿主确定的账号／暂存会话合并两类来源，每类独立分页并保留已耗尽来源的游标；同页去重，非法模板跳过，旧版无购买表的数据库继续读取快照。
`AndroidHistoryArtwork` 同步传递和重置购买游标，沿用现有原生插画、品质边框导出及后台落盘机制。

- 离线：Python unittest 20 项通过；固定工具链插件编译通过；签名构建通过，签名报告 Verifies、v2=true，证书为 CN=BazaarPlusPlus Android Test；`git diff --check` 通过。
- 用户提出“先不要实机验证”前，已启动的隔离 `verify_history_store.py --reader` 在 Android 14 设备 `OBJVWSPNSCFQU48L` 完成 99 项断言：包含 47 条购买记录跨页、仅有购买记录的模板、同页去重、非法模板、账号／暂存会话隔离、旧版无购牌表以及原历史读取契约。数据仅写入本次随机隔离目录，成功后由脚本清理，未写入正式历史库。
- 用户要求暂停后停止自动设备验收；随后按用户明确指令覆盖安装同一签名修复包 test.14，ADB 返回 Success。APK SHA-256 为 `e6cb8b83a59a6211f37a7781d86fb661ffe8cfc8bd50a8c4fe0fada4c73ddcfd`。安装后由用户自行验证，用户反馈“已验证，提交代码”。未继续自动操作游戏；隔离 SQLite 断言不等同自动图片导出验证。
- 用户已确认本次修复自测通过；未逐项提供卡牌样本、输入模式、跨账号／游戏版本或失败重试过程，不扩大为全部边界均已验收。
- 证据：忽略目录 `artifacts/purchase-art-fix/` 的 `offline-tests.log`、`build.log`、`reader.log` 与 `reader/`；签名包构建日志在 `artifacts/launcher-apk-build.log`。
- 本次提交包含图片回填及编译所需的购牌存储／查询依赖和回归测试；工作区其他购牌采集、页面与战斗计时器改动保持原状，不混入本次提交。


## 战斗计时器拖动与四边吸附（2026-10-26，用户自测通过）

本次以 `android-mod/AndroidFloatingDock.cs` 的 `Bind`、`Bounds`、`Snap` 为交互参考，
在 `AndroidCombatTimer` 中启用独立触摸拖动，松手吸附到最近的物理屏幕边缘，上下保留安全区域。
默认底部居中，边缘及沿边比例保存到原有 `combat-timer.json`，显示设置与位置互相保留；
旧配置缺少位置时回退到底部居中。屏幕、安全区域或 Canvas 缩放变化后重新布局，
失焦或隐藏时结束当前拖动；多指仅接受当前持有指针。计时帧来源与战斗生命周期不变。

离线结果：`CombatTimer.Tests` 37 项检查通过，覆盖旧配置兼容、位置保存与读取、范围限制、
显示切换保留位置和写入失败保护；Python unittest 20 项通过（Java 编译保留既有 source/target 8 过时警告）。
`build_probe.py --mod` 使用本机固定框架及 Android interop 编译通过，零警告／零错误；
构建日志位于被忽略的 `artifacts/mod/combat-timer-drag-build.log`，输入摘要为 `artifacts/mod/build-inputs.json`。

实现阶段按用户要求跳过代理实机验证，未部署、启动游戏或操作设备，也未新增设备截图。
随后用户确认“已验证，直接提交”，本次功能记为用户自测通过。用户未逐项报告设备、输入模式、
多指、隐藏／失焦、重启位置恢复及分辨率变化等覆盖范围，不扩大为所有边界已通过。
本次提交包含计时器及其必要的帧观察、设置和生命周期接入，其他购牌采集与启动器页面改动保留在工作区。
