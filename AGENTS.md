# 漫味 (ManWei) 项目约束

> 记录从真实错误中得出的约束，非通用建议

## Hard Rules

- 实体类只能在 `Models/` 目录，根目录发现旧版本立即删除，否则与 partial 类冲突编译失败

## 已知坑点

- **EmotionTags.Name 空格污染** — 写入时 `Trim()`，查询时 `t.Name == query.TagName.Trim()`
- **Rating null 排最后** — SQL Server 的 `OrderByDescending(f => f.Rating == null).ThenByDescending(f => f.Rating)` 实现 NULLS LAST
- **onShow 深分页重置陷阱** — 小程序收藏页需用 `needRefreshFavorites` 标志位控制刷新时机
- **收藏页 wx:for 变量遮蔽** — 内外层 `wx:for` 必须显式命名 `wx:for-item` / `wx:for-index`
- **dotnet run 进程锁定** — 编译前先 `taskkill //F //IM "ManWei.Api.exe"`
- **JWT Token UserId 类型** — `User.FindFirstValue` 返回 string，必须 `int.TryParse` 转换，不能直接赋值
- **echarts-for-weixin 与微信基础库不兼容** — 情绪曲线改用原生 Canvas 2D API，不依赖 echarts-for-weixin
- **DeepSeek tool_call_id 字段丢失** — 必须从原始 `JsonElement` 取，不能用 `ChatMessage` 类重建，否则报 400


## 禁止事项

- 禁止在根目录创建与 `Models/` 同名的实体文件
- 禁止在 `dotnet run` 运行时编译（ManWei.Api.exe 被锁定）
- 禁止在 .NET 中使用 `DateTimeZoneHandling`（不存在，删掉）
- 禁止在 SQL Server 唯一索引中放可空字段的显式 NULL（索引会失效）