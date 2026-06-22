# 漫味 (ManWei) 项目约束

> 记录从真实错误中得出的约束，非通用建议

## Hard Rules

- 实体类只能在 `Models/` 目录，根目录发现旧版本立即删除，否则与 partial 类冲突编译失败
- 删除文件前必须先检查 git 跟踪状态——已跟踪用 `git rm`（可从 HEAD 恢复），未跟踪必须先输出 `git status` 或 `git ls-files <文件>` 的结果给用户看，并等待用户明确回复"确认删除"后才能执行 `rm`。这条规则在 agent 执行删除时无条件生效，不因"看起来不重要"而省略。
- `git commit` 前检查 commit message 本身是否包含真实姓名、手机号、邮箱——commit message 和文件内容一样公开且不可逆。
- 个人写作类文件（论文、笔记、个人测试记录）物理路径必须在项目根目录之外，不允许放在项目目录下任何子文件夹（包括 `docs/`），以杜绝被 `git add .` 误添加的可能。

## Git 上传 / 本地保留规则

> 核心逻辑：git 历史是公开且几乎不可逆的，本地磁盘是私有且可控的。
> "删了就回不来"或"公开了就回不来"的内容，判断标准从严。

### 一、不该上传到 GitHub 的内容

- PII（真实姓名、学号、身份证号、手机号、邮箱）——含文件名、**commit message**、代码注释
- 凭证/密钥（API key、密码、JWT secret）——用 `.env.example` 替代，且 `.env.example` 中的占位符必须带有明显的无效特征（如 `PASSWORD=CHANGE_ME_OR_BREAK`），不能用看起来像真密钥的格式（如 `PASSWORD=your_password_here`），避免被直接复制改名就投入使用
- 内部草稿类写作过程文件（论文正文、笔记、测试报告原始稿）
- 大体积/频繁变动的二进制文件（`.docx`、`.pptx` 等）

**强化措施**：建议在 `.git/hooks/prepare-commit-msg` 或全局 Git 模板中加一条正则过滤，拦截 commit message 中的中文姓名模式和 11 位手机号格式，作为人工检查之外的兜底。

### 二、可以/应该上传的内容

- 源代码（含注释，不含 PII）
- 项目级文档：README、架构图、API 文档、CHANGELOG
- 配置文件模板（`.env.example`，占位符需带无效特征）
- 自动化测试代码（不是"测试报告"——代码和报告是两回事）
- License、贡献指南

### 三、本地存储原则：禁止以项目根目录为唯一存储源

> 注意："本地保留"本身不等于"备份"。如果文件只存在于项目本地磁盘的某一份，硬盘损坏、误操作 `rm -f` 都会导致彻底丢失。`Thesis-FullText.md` 的丢失正是因为它的唯一副本恰好落在了项目目录下且未被 git 跟踪。

凡是符合以下任一条件的文件，**不允许只存在于项目根目录内**：

- 论文/报告等个人产出
- `.env` 真实密钥
- 大体积素材（截图、视频原始稿等）

**实现方式（任选其一，降低执行门槛）**：

- **简单方式**：实体文件放在云盘同步目录（OneDrive/iCloud/坚果云等）内，项目目录中只保留一份纯文本索引记录（如在 `docs/外部文件索引.md` 中写明"论文实体文件位置：`D:\OneDrive\Thesis\FullText.md`"）
- **进阶方式**：项目目录内用软链接（symlink）指向云盘同步目录中的实体文件（需要熟悉命令行操作，Windows 下可能需要开发者模式或管理员权限）

`.env` 真实密钥额外要求：用密码管理器或加密笔记做一份独立备份。

### 四、删除前必须检查（行为层硬规则，对 agent 无条件生效）

> 人脑在执行删除操作的瞬间，无法保证会冷静地停下来检查。这条规则因此不依赖"记得检查"，而是写成强制的执行步骤。

任何 `rm` / 覆盖写入操作前，必须按以下顺序执行：

1. **先检查跟踪状态**：跑 `git status` 或 `git ls-files <文件路径>`，把结果展示出来
2. **已被 git 跟踪** → 改用 `git rm` 而非系统 `rm`（`git rm` 删除后仍可从 HEAD/历史恢复），执行前向用户说明"此文件已纳入版本控制，删除后可通过 `git checkout <commit> -- <文件>` 恢复"
3. **未被 git 跟踪** → **必须停止，不能直接执行删除**。向用户清晰说明："该文件未被 git 跟踪，删除后无法通过 git 恢复"，并等待用户明确回复"确认删除"（而不是默认假设用户会做出正确判断）
4. **文件大小 > 1MB 或后缀为 `.docx`/`.pdf`/`.md`（论文/报告类）**：无论是否被跟踪，必须在确认请求中明确强调"此文件无法轻易恢复"，用语言强度提醒用户，而不是用一句轻量的"是否需要备份？"带过

**人工终端操作补充建议**（适用于用户手动操作，非 agent 自动执行场景）：可考虑将 `rm` 别名为 `rm -i` 或使用 `trash-cli` 替代直接删除，作为人工操作层面的额外保护。但这不能替代上面对 agent 行为的强制规则，因为 agent 通过脚本/工具调用执行命令时，不一定会触发 shell 的 alias 机制。

### 五、事故补救 SOP（一旦已经发生泄露，立即执行）

> 以下是补救步骤，区别于上面四条的"预防"性质——这一节回答的是"已经传上去了怎么办"。

1. **如果是凭证/密钥泄露**：第一步永远是立即吊销/轮换密钥，不管后续是否清理 git 历史。凭证已暴露就视为已泄露，撤销是唯一能终止风险的动作。
2. **如果是 PII 或其他敏感内容泄露**：
   - 不能只删除文件重新提交——历史 commit 里数据依然存在
   - 必须使用 `git filter-repo`（优先）或 BFG Repo-Cleaner 清洗历史，针对具体敏感字符串做 `--replace-text` 替换
   - 清洗后 `git push --force` 推送，且需同步处理 tag（`--tags --force`）
3. **清洗本地历史后，仍需处理远端缓存**：
   - 如果仓库是 Public：提交 GitHub Private Information Removal 请求（凭证类走常规 sensitive data removal 流程），清除 GitHub 服务器端缓存的 commit 视图
   - 检查仓库 Fork 数：如果存在 Fork，需联系 Fork 所有者协调清理，GitHub 无法强制清除他人 Fork 中的数据
4. **如果仓库是 Public 且泄露的是密钥**：视为安全事故，按严重等级上报（即使仓库当时 star/fork 数为 0，也应假设可能已被自动化爬虫抓取）。

## 已知坑点

- **EmotionTags.Name 空格污染** — 写入时 `Trim()`，查询时 `t.Name == query.TagName.Trim()`
- **Tag 字典 key 规范化不一致** — `Dictionary<string,T>` 用 normalized key 写入（`Trim().ToLowerInvariant()`），所有查询方也必须走同一 `TagNormalizer.Normalize()` 再做 `TryGetValue`；否则 ASCII 大小写标签（如 `"P.A.WORKS"` vs `"p.a.works"`）全部匹配失败 → tagOverlap=0。Recommendation 子目录抽了公共 `TagNormalizer` 杜绝重复定义。
- **Shuffle 后不能再 OrderByDescending 整个 window** — "从 Top 窗口随机取 K 条"的正确顺序是 `window.Take(K).OrderByDescending(...)`（先选后排），**反之** `window.OrderByDescending(...).Take(K)`（对整个 window 排序取 Top K）会让随机完全失效——因为排序后 Top K 必然是固定的。看似等价的两个写法行为截然不同。`docs/recommendation.md §5.5` 论文复现需带 `?deterministic=true` 才能稳定截图。
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
- **DeepSeek tool arguments bool 类型** — DeepSeek tool_call args JSON 里的 bool 值用 `JsonElement.ValueKind == JsonValueKind.True/False` 判断，`Convert.ToBoolean` 在某些序列化路径上会抛 FormatException；统一用 `BaseAiAgentService.GetBool(args, key, defaultValue)` 辅助方法（与 `GetInt`/`GetString` 风格一致）
- **`BangumiService` 需要注 `AppDbContext`** — 原 Service 只有 HttpClient + Logger，加 `RefetchAnimeMetadataAsync`（详情页懒拉取）后需访问数据库。**注意**：该类是 Typed HttpClient（Scoped），加 `AppDbContext`（Scoped）不会冲突，但加了 `Microsoft.EntityFrameworkCore` using 后才能用 `ToListAsync()` 等扩展方法
- **Details 页左列变高布局** — 右列 `.infoSection` 加 `position: sticky; top: 16px; align-self: start` 防止左列（封面+基本信息+标签）过长时右列不对齐

- 禁止在根目录创建与 `Models/` 同名的实体文件
- 禁止在 `dotnet run` 运行时编译（ManWei.Api.exe 被锁定）
- 禁止在 .NET 中使用 `DateTimeZoneHandling`（不存在，删掉）
- 禁止在 SQL Server 唯一索引中放可空字段的显式 NULL（索引会失效）