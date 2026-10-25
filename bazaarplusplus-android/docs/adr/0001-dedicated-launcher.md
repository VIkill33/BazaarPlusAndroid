# ADR-0001：以受管理的 FusionCore 定制源码构建单 APK 启动器

状态：采用；专用 APK 的设备兼容性与发行许可分别验收。

## 决策

`launcher/` 持有可提交的 FusionCore Java、JNI、资源及 Gradle 工程。
`launcher/upstream.json` 绑定上游提交及未提交二进制输入的摘要；
`build_launcher.py::prepare` 从匹配的上游 checkout 恢复并核对二进制输入。
源码修改直接在此目录维护，不以被忽略的 tooling checkout 作为产品源码。

保留上游 Java namespace 与 JNI 类路径，只改变应用 ID。原生
`fusion_parse_config`、`find_class_in_app_classloader` 仍引用原类名；
`GameRuntime` 的宿主身份使用 `BuildConfig.APPLICATION_ID`。
`compatibility.json` 拥有目标游戏与独立共享目录名，Gradle 投影为 `BuildConfig`，
`Utilities.getExternalFusionCoreDirectory` 是统一目录入口；
游戏持久数据的 `CustomContextWrapper` 和传给 JNI 的配置都通过同一函数定位。
不读取或迁移原 FusionCore 的账号、插件、代理与缓存。

`LauncherActivity` 取代通用选择器与设置页；`BootstrapActivity.prepareConfig`
在上游启动链上调用 `LauncherPreparation.prepare`。准备任务以进程互斥防重复，
进程退出后重新核对输入，不把持久化阶段文字当作完成证据。
没有前台服务，进程被终止后须重新开始准备。

发行清单由插件构建记录、固定框架和运行时输入生成；游戏匹配规则与内置依赖由
`launcher/compatibility.json` 拥有。未知游戏或 Unity 版本拒绝启动。
Unity 依赖在构建时下载、校验并打包到 APK；初始化与修复从内置资源提取，
在临时文件完成大小与 SHA-256 校验后原子切换。运行时不访问 GitHub；不动态选较低补丁版本，
Java 和原生初始化均禁止下载／挂钩失败时回退原始 Unity 库。
BepInEx 的 `UnityBaseLibrariesSource` 只写入清单内 ZIP 的本地文件名，与实际校验和安装的
`unity-libs/` 文件一致；来源 URL 仅用于追溯，不能作为运行时配置，否则框架按 URL
末尾文件名寻找另一份缓存，并可能重新联网或读取未经校验的旧下载。备用入口同样固定在清单中，
仅在构建时使用同一 GitHub 上游的资源 API，仍对实际内容执行相同校验。

解压先进入独立暂存目录，拒绝越界路径，再逐文件原子安装。配置保留，
每次启动重新检查框架、运行时与插件；多文件切换不是一个整体事务，
但任何中断都不会在下一次准备跳过校验。仅对握手确认过的插件保留旧副本到应用私有目录，
替换失败保留原目标；不提供跨兼容名单的自动降级。

游戏资源键绑定 APK 集摘要与框架摘要；键变化或用户主动修复时只重建
`Assets`、`BepInEx/interop`、`BepInEx/cache`，不删除游戏根及持久数据。
修复／准备仅可在本进程没有启动游戏时执行。

`AndroidLauncherHandshake.Complete` 在插件入口完成后写入会话、插件版本、
实际 DLL 摘要和时间。`LauncherSession.matches` 拒绝旧会话、错版本、错摘要及
早于本次启动的确认。缺少证据显示待确认；握手不代表登录或游戏内功能通过。

诊断导出使用系统文件保存界面，只输出环境与构建白名单。原始异常原因只在本机显示，
不打包游戏日志、账号、令牌或持久目录。签名密钥放在 Git 忽略的 `signing-secrets/`，
后续覆盖升级须使用同一密钥；正式公开分发还须完成依赖许可与对应源码交付审计。

## 实施前独立评审

对固定上游源码的独立审阅识别了共享根、复制失败仍继续、无摘要的依赖缓存、
动态依赖回退、旧资源标记和无会话加载日志六项风险。上述边界逐项处理，
文件事务与会话判断通过执行生产 Java 类的离线测试；真机范围另见
[验证记录](../device-validation.md)，不以离线测试替代。
