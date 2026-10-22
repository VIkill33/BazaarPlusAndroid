# Android 构筑推荐内置快照

`builds-2026-10-05.json.gz` 是 BazaarPlusPlus 5.7.0 macOS 正式发行包中
`BazaarPlusPlus.dll` 的 `BazaarPlusPlus.Data.BuildRecommendations.builds.json`
嵌入资源，经 gzip 压缩后的原样内容。原 JSON 的 `generated_at` 为
2026-10-06T16:13:49Z，统计窗口为 2026-09-29 至 2026-10-05；包括
5,689 条构筑，其中皮格马利翁 834 条。压缩文件 SHA-256：
`d6ac68fc29fc3ba6a5dd15dd0bdf3e09149122bb835dcdd643a8b592ba105073`。

来源：[BazaarPlusPlus 5.7.0 官方发行包](https://github.com/BazaarPlusPlus/BazaarPlusPlus/releases/tag/5.7.0)。
Android 插件将它作为可用基线，并在在线数据明显缩水时保留这份快照；
界面显示数据的生成日期，以便识别其时效。
