# 漫味 (ManWei) 项目测试报告 — 子代理输入规格

> 本文件作为 6 个子代理并行写报告的统一输入源。**所有 6 份文档必须基于以下事实**，避免各自"凑"内容。

## 0. 全局约束（6 份文档共用）

- **测试账号**：`<TEST_USER>`（密码已脱敏，原值仅在内部排查记录中保留，**所有报告文档不写明文密码**）
- **后端地址**：`http://localhost:5150`（HTTP，统一协议，不用 HTTPS）
- **DB**：SQL Server `localhost/ManWeiDB`
- **执行日期**：2026-06-20
- **项目根**：`<PROJECT_ROOT>`
- **报告目录**：`docs/test-report/`
- **后端启动命令**：`cd backend/ManWei.Api && dotnet run --launch-profile http`
- **接口测试脚本**：`backend/ManWei.Api/tests/api-tests.http`（37 个请求）

## 1. 用例口径统一

按 **12 个一级用例** 统计，每个用例有独立的目标/前置/步骤/预期/实际。

| 一级用例 | 子场景（仅作执行细节展开，**不单独计数**） |
|---|---|
| A1 邮箱登录 | A1.1 登录拿 JWT / A1.2 拿用户信息 |
| A2 无效 token | A2 无 header / A2b 错密码 |
| B1 搜索 | B1 关键词"进击的巨人" |
| B2 Sync | B2 同步 Bangumi 118335 |
| B3 收藏状态流转 | B3.1 创建 / B3.2 在看 / B3.3 看过 / B3.4 列表 / B3.5 计数 / B3.6 check |
| C1 5 集情绪 | C1.1-C1.5 五条 |
| **C2 Episode 上限校验** | C2 ep=999 / **C2b** ep=0 / **C2c** emo=6（**这是 CLAUDE.md L20 已知坑点验证场景，02 文档需独立列一行表格**） |
| D1 标签 + 词云 | D1.1-D1.5 五标签 / D1.6 used 列表 / D1.7 anime 词云（**403 Admin 专用**）/ D1.8 全局词云 |
| E1 观后感 | E1.1 创建 / E1.2 查询 / E1.3 列表 |
| F1 推荐论文复现 | F1 deterministic / F1b non-deterministic |
| G1 PcAiAgent | G1 流式 NDJSON（**注意：原计划写 `/api/AiAgent/chat`，正确路径是 `/api/pcaia/chat-stream`**） |
| G2 WxAiAgent | G2 中文对话 |

**01-test-design.md** 详列 12 用例；**06-summary.md** 统计基线写"12 个一级用例"。

## 2. 性能数据

| 接口 | min (ms) | avg (ms) | max (ms) | p95 (ms) | 备注 |
|---|---|---|---|---|---|
| GET /api/Anime/47 | 137 | 314.8 | 1012 | 146 | 首请求冷启动 1s，后续稳态 ~140ms |
| GET /api/recommendations | 9 | 20 | 55 | 14 | 推荐算法高效 |
| GET /api/Favorites | 2 | 3.6 | 9 | 3 | — |
| GET /api/EmotionCurves/170 | 1 | 2.2 | 5 | 2 | — |
| GET /api/EmotionTags/used | 1 | 2.8 | 9 | 2 | — |
| GET /api/Favorites/search-anime | 340 | 382.2 | 440 | 412 | 触发 Bangumi 远程 API |
| **2 并发 × 5 接口 wall** | — | — | **1318** | — | **场景**：启动 2 个 PowerShell `Start-Job` 并行，每个 job 串行跑完上述 5 个本地接口（Anime/recommendations/Favorites/EmotionCurves/EmotionTags），wall = 两个 job 中较慢的完成时间 + Start-Job/IPC 开销 |
| AI chat TTFB | 207 | — | — | — | `/api/pcaia/chat-stream` 首字节延迟 |
| AI chat Total | 2291 | — | — | — | 完整流式响应总耗时（不发并发，避免触发 DeepSeek 限流） |

**02-test-execution.md** 性能章节按此表渲染。"2 并发 wall"行的"测试方法"在 02 文档里要写清楚（**不要省略"2 个 job 并行各跑 5 接口"这个场景描述**）。

## 3. BUG-001 真实性质（**关键归类修正**）

### 性质
**测试工具链问题，非后端 BUG**。PowerShell `-Command` 在 Windows GBK 控制台下发送中文 JSON 时，单条 `for $t in $tags` 循环的特定路径会产生字符损坏（UTF-8 字节被错误按 Latin-1 解码后重新编码）。

### 证据
- **后端 `EmotionTag.Name` 字段类型 `string`，EF Core 默认映射 `nvarchar(max)`**，SQL Server `nvarchar` 完全兼容中文
- 同一批次测试里 6 条中文标签完整保存（"热血""冲击""剧情向""思考""热血少年""催泪""神作""治愈"），只有 2 条损坏

### 损坏记录 ID 来源（**两个 ID 均为 PowerShell `for` 循环失败产物，根因同类**）

| ID | 内容（显示） | Unicode | UTF-8 字节 | 来源说明 |
|---|---|---|---|---|
| 49 | `?` | U+003F | `3F` | **第 1 轮 PowerShell `for $t in $tags` 循环**，Windows GBK 控制台首次请求失败路径，将中文 UTF-8 字节截断为单字节 `0x3F` |
| 52 | `ȼ` | U+023C | `C8 BC` | **第 1 轮 PowerShell 循环的另一失败迭代**，将中文 UTF-8 三字节 `E7 87 83` 按 Latin-1 重编码为 `C8 BC` |

两条均为 PowerShell `for $t in $tags` **第一次循环**的不同失败模式产物，根因同类。**已通过 SQL Server `sqlcmd` 直接查 `EmotionTags` 表确认**，不是查询渲染问题。

### 修复策略
1. **客户端层**（推荐）：测试时用 UTF-8 编码的 JSON 文件 + bash `curl --data-binary @file` 直接发送，**完全避开 PowerShell `-Command` 的中文处理**
2. **数据清理**：已删除 Id=49、52，证据截图保留为 `bug-001-evidence-raw-bytes.png`

### 关键证据截图
- `docs/test-report/screenshots/bug-001-evidence-raw-bytes.png` —— 修复前（含 2 条脏数据 + Unicode/UTF-8 字节标注）
- `docs/test-report/screenshots/optimization-bug-001-after-cleanup.png` —— 修复后（仅 9 条正常标签）

## 4. 已确认的设计选择（**不是 BUG，归类到 02-test-execution.md "边界发现"段，不进 03-bugs.md**）

| 现象 | 真实性质 | 证据 |
|---|---|---|
| `/api/EmotionTags/anime/{id}/wordcloud` 普通用户 403 | **代码刻意限制** Admin 专用统计接口 | `EmotionTagsController.cs` L168 显式 `[Authorize(Roles="Admin")]` |
| `/api/AiAgent/chat` 普通用户 403 | **代码刻意限制** Admin 专用 | `AiAgentController.cs` L13 `[Authorize(Roles="Admin")]` |
| 推荐接口 popular 模式 breakdown 全 0 | **设计如此**（popular 分支不调用 Scorer，不算 tagOverlap/emotionAffinity） | `RecommendAnimeService.cs` L99-134 |
| 推荐冷启动进入 full 的硬门槛 | `Rating >= 8 && Status == 2`（看过+高分），非"≥N 条标签/情绪" | `TagProfileBuilder.cs` L51 `H = {f.Rating>=8 && f.Status==2}` |

## 5. 已验证修复的 CLAUDE.md 已知坑点

| 坑点 | 验证结果 | 证据位置 |
|---|---|---|
| L11 JWT UserId `int.TryParse` | ✅ 已修复 | `PcAiAgentController.cs` L48 `if (!int.TryParse(idStr, out var userId))` |
| L20 Episode 上限校验 | ✅ 已修复（且比 CLAUDE.md 描述更精细：用了真实 `TotalEpisodes` 而非 500 兜底） | `EmotionCurvesController` 测试响应 `"集数不能超过总集数 25 集"` |

## 6. 12 用例执行结果（**最终事实**）

> **口径说明**：本表共 **15 行**，是 12 个一级用例的完整执行明细（C2 拆成 C2/C2b/C2c 三行展示边界细节，D1 拆成 D1/D1.7 两行展示 Admin 403 行为）。**用例统计与通过率分母请以 §1 的 12 个一级用例为准**，不要直接用本表行数（15）做分母。
>
> 03-bugs.md / 06-summary.md 引用本表时，**C2/C2b/C2c 合并算 1 个用例（C2 Episode 上限校验）**，**D1/D1.7 合并算 1 个用例（D1 标签 + 词云）**。

| 用例 | 状态 | 关键数据 |
|---|---|---|
| A1 登录 | ✅ | userId=<UID>, JWT 已拿 |
| A2 无效 token | ✅ | 401 |
| B1 搜索 | ✅ | 命中 animeId=47 |
| B2 Sync | ✅ | 118335 |
| B3 收藏状态流转 | ✅ | status 0→2, rating=9, progress=25 |
| C1 5 集情绪 | ✅ | 5/5 写入 |
| C2 ep=999 | ✅ | 400 "集数不能超过总集数 25 集" |
| C2b ep=0 | ✅ | 400 "集数必须大于0" |
| C2c emo=6 | ✅ | 400 "情感等级必须在1-5之间" |
| D1 标签 | ⚠️ 部分 | 9 个正常标签 + 2 条脏数据（已清理） |

**D1 清理后回归验证（已执行）**：

- DELETE /api/EmotionTags/49 → 200 `删除成功`
- DELETE /api/EmotionTags/52 → 200 `删除成功`
- 随后 GET /api/EmotionTags/used → 200，仅含 9 条正常标签：`["ASCII_TAG","冲击","催泪","剧情向","热血","热血少年","神作","思考","治愈"]`，**无任何脏数据残留**
- 截图证据：`screenshots/optimization-bug-001-after-cleanup.png`

**05-regression.md** 必须写入上述具体数据，不要写"回归测试通过"这种无依据的笼统描述。
| D1.7 anime 词云 | ⚠️ 设计如此 | 403 Admin 专用 |
| E1 观后感 | ✅ | 中文 content 完整保留 |
| F1 推荐 full | ✅ | mode=full, 命中咒术回战/鬼灭之刃/夏日重现 |
| G1 PcAiAgent 流式 | ✅ | NDJSON delta 正常，TTFB 207ms |
| G2 WxAiAgent 中文 | ✅ | 中文 AI 回复 |

**通过率**：13/15 = 86.7%（D1.7 + AiAgent Admin 不算失败，属设计如此）
**真正失败**：0 个
**真实 BUG**：1 个（BUG-001 工具链问题）

## 7. 已交付的资产清单

```
docs/test-report/
├── INPUT-FOR-SUBAGENTS.md                       (本文件)
├── api-tests.http                              (接口脚本，37 请求)
└── screenshots/
    ├── swagger-overview.png
    ├── swagger-overview-full.png
    ├── swagger-all-expanded.png
    ├── pc-20260620-01-login.png
    ├── api-F1-recommendation-full.png          ← F1 full 模式推荐结果
    ├── bug-001-evidence-raw-bytes.png          ← BUG-001 修复前
    └── optimization-bug-001-after-cleanup.png  ← BUG-001 修复后
```

## 8. 6 份报告子代理分工

| 文档 | 输入文件 | 子代理读取本文件后立即动笔 |
|---|---|---|
| 01-test-design.md | 本文件 §1 + §6 + api-tests.http | 12 用例设计全表 |
| 02-test-execution.md | 本文件 §1 + §2 + §6 + §4 | 执行记录 + 性能表 + 边界发现 |
| 03-bugs.md | 本文件 §3 | BUG-001 完整描述（**仅此一条**） |
| 04-optimization.md | 本文件 §3 + 截图 2 张 | 脏数据清理对比 |
| 05-regression.md | 本文件 §3 + §6 | 优化后回归测试记录 |
| 06-summary.md | 本文件 §0 + §1 + §6 | 覆盖率（12 用例 + 6 性能 + 1 优化 = 19 项）+ 心得 |

**所有 6 份文档统一用中文**，文件名 `docs/test-report/NN-xxx.md`。

## 9. 子代理必须遵守的硬规则

1. **不写明文密码** —— 一律写 `<TEST_USER>`（密码已脱敏）
2. **不预设缺陷数量** —— 真实反映：1 个 BUG（工具链问题）+ 4 个已确认设计选择
3. **12 用例口径统一** —— 不拆子用例为独立计数
4. **冷启动判定条件写明 `Rating>=8 && Status==2`** —— 不要写"≥N 条标签/情绪"
5. **D1.7 与 AiAgent Admin 403 不进 03-bugs.md** —— 进 02-test-execution.md 的"边界发现"段
6. **截图引用使用相对路径**：`screenshots/xxx.png`
7. **所有代码引用使用纯文本格式，不用 markdown 锚点链接**（最终交付物要转 Word 提交，锚点链接转换后会失效变成死文本）：
   - ✅ 正确：`backend/ManWei.Api/Controllers/EmotionTagsController.cs` 第 168 行
   - ❌ 错误：`[EmotionTagsController.cs:168](backend/ManWei.Api/Controllers/EmotionTagsController.cs#L168)`

## 10. Markdown → Word 转换流程（**6 份 markdown 初稿完成后单独执行**）

- 6 子代理**只产出 markdown**（`docs/test-report/NN-xxx.md`），不直接生成 Word
- 6 份 markdown 内容定稿后，**单独起一个任务**统一走 docx 生成流程，把内容合并到符合课程要求的 Word 模板（标题字号、页眉、三线表样式等）
- 不建议 6 子代理各自生成 Word——会导致页眉/页码/目录等贯穿全文的格式无法统一

**当前阶段只关心 markdown 内容正确性，Word 格式由后续 docx 任务单独处理。**
