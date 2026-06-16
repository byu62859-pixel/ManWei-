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
- **DeepSeek tool arguments 类型** — `JsonSerializer.Deserialize<Dictionary<string, object?>>` 后的值是 `JsonElement` 类型，不能用 `Convert.ToInt32` / `Convert.ToString`，统一用 `BaseAiAgentService.GetInt` / `GetString` 系列方法
- **Bangumi API 返回格式** — 批量查询返回 `{"data": [...], "total": N}`，不是直接数组，需用 `BangumiSubjectListDto` 包装
- **Bangumi V0 搜索接口** — 必须用 `POST /v0/search/subjects`，带 JSON 请求体 `{keyword, filter: {type: [2]}}`，`limit` 作为 URL Query 参数；旧版 GET `/v0/subjects?q=` 会被 Bangumi 降级为默认热门列表
- **Bangumi UserAgent 写法** — 需用 `UserAgent.ParseAdd()` 解析含空格/括号的 UA 字符串，直接 `Add()` 会抛 FormatException
- **Bangumi episodes total 字段** — `total` 是全量计数，与 `limit` 无关，limit=1 也能拿到完整 total
- **Episode 上限校验** — `Favorite.Progress` 与 `EmotionRecord.Episode` 都用 `favorite.Anime?.TotalEpisodes is > 0 ? value : 500` 兜底，null/0 视为未知上限 500
- **BackgroundService 依赖注入** — `BackgroundService` 是 Singleton，不能直接注入 Scoped 服务（如 `DbContext`），需在 `ExecuteAsync` 内用 `IServiceProvider.CreateScope()` 创建作用域
- **定时任务测试技巧** — 测试 `BackgroundService` 时只注释时间判断，保留状态机对比逻辑，否则无法验证"周标记防重复"机制是否正常
- **Bangumi infobox 是数组 `[]` 不是字典 `{}`** — `/v0/subjects/{id}` 返回的 `infobox` 字段是 `[{key, value}]` 数组，不能用 `Dictionary<string, T>` 反序列化；部分动漫 infobox 是空数组 `[]`，会导致字典反序列化崩。统一用 `JsonElement` + `List<BangumiInfoboxItemDto>` 解析
- **EF snapshot Review 双重配置预置 bug** — `AppDbContextModelSnapshot` 中 Review 实体被配置了两次，导致 `dotnet ef database update` 报 "Navigation 'Review.Review' was not found"。绕过方案：启动时手动跑 SQL 迁移（`ManualMigration.RunAsync`），不依赖 EF 迁移引擎
- **`JsonElement?` 可空访问** — `JsonElement?` 的属性（`ValueKind`/`GetRawText()`）需通过 `.Value.` 访问，且要先判 `.HasValue`，否则编译报 CS1061
- **`BangumiService` 需要注 `AppDbContext`** — 原 Service 只有 HttpClient + Logger，加 `RefetchAnimeMetadataAsync`（详情页懒拉取）后需访问数据库。**注意**：该类是 Typed HttpClient（Scoped），加 `AppDbContext`（Scoped）不会冲突，但加了 `Microsoft.EntityFrameworkCore` using 后才能用 `ToListAsync()` 等扩展方法
- **Details 页左列变高布局** — 右列 `.infoSection` 加 `position: sticky; top: 16px; align-self: start` 防止左列（封面+基本信息+标签）过长时右列不对齐

- 禁止在根目录创建与 `Models/` 同名的实体文件
- 禁止在 `dotnet run` 运行时编译（ManWei.Api.exe 被锁定）
- 禁止在 .NET 中使用 `DateTimeZoneHandling`（不存在，删掉）
- 禁止在 SQL Server 唯一索引中放可空字段的显式 NULL（索引会失效）