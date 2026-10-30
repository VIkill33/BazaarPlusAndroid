# Android 构筑推荐内置快照

`builds-2026-10-26.json.gz` 是原项目 Android 端使用的十胜构筑目录
`https://cdn.bazaarplusplus.com/metrics/v6/builds/latest.json`，
于 2026-10-30 拉取后按原样 gzip 压缩。JSON 的 `generated_at` 为
2026-10-27T05:33:27Z，统计窗口为 2026-10-20 至 2026-10-26；包括
6,846 条构筑，其中皮格马利翁 1,004 条。压缩文件 SHA-256：
`b57709895c8dbb9026b6f4c52e2fbc793d706ba7b9500fb0176e1b49d4b22e7c`。

插件启动时嵌入这份快照，并按同一地址请求更新的统计窗口；
在线响应残缺或更旧时保留本地阵容。界面显示数据的生成日期和统计截止日。
