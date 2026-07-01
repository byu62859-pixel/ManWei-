# 漫味 (ManWei) 项目测试设计文档

> 报告体（"测试了/执行了/验证了"），不写主观意图
> 文档版本：v1.0  ·  执行日期：2026-06-20  ·  对应项目根：`<PROJECT_ROOT>`

---

## 1. 报告概述

### 1.1 测试目标

漫味（ManWei）是一款以"动漫情感标签 + 情绪曲线 + 智能推荐"为核心玩法的二次元向 Web 应用。本测试设计的核心目标包括：

- 验证 12 个核心业务接口的功能正确性，覆盖用户认证、动漫检索、收藏状态机、情绪曲线、情感标签、观后感、推荐算法、AI 助手 7 大模块；
- 验证边界条件与已知坑点（CLAUDE.md L20 Episode 上限校验、JWT UserId 类型转换、admin-only 接口授权等）；
- 量化接口响应性能基线（min/avg/max/p95）与并发场景下的 wall time；
- 形成可复用的接口测试脚本与执行清单，作为后续回归测试与课程提交的工程资产。

### 1.2 测试范围（12 个一级用例）

按 INPUT-FOR-SUBAGENTS.md §1 的口径统一，本设计文档覆盖 **12 个一级用例**（子场景仅作为执行步骤展开，不单独计数）：

| 模块 | 一级用例 ID | 标题 |
|---|---|---|
| A 用户认证 | A1 | 邮箱登录 |
| A 用户认证 | A2 | 无效 token |
| B 检索+收藏 | B1 | 搜索 |
| B 检索+收藏 | B2 | Sync（同步 Bangumi 详情）|
| B 检索+收藏 | B3 | 收藏状态流转 |
| C 情绪曲线 | C1 | 5 集情绪记录 |
| C 情绪曲线 | C2 | Episode 上限校验（含 C2/C2b/C2c 三边界）|
| D 标签词云 | D1 | 标签 + 词云（含 D1.7 Admin 403）|
| E 观后感 | E1 | 观后感 |
| F 推荐 | F1 | 推荐论文复现（full 模式）|
| G AI 助手 | G1 | PcAiAgent 流式对话 |
| G AI 助手 | G2 | WxAiAgent 中文对话 |

### 1.3 不包含项

- **不预设"几个 BUG"作为前置假设**——最终 BUG 数量以 03-bugs.md 的真实记录为准；
- **不涉及压测工具（JMeter / wrk / k6）的深度性能调优**——性能基线通过 PowerShell `Measure-Command` 与 `Start-Job` 并发采样；
- **不涉及小程序端 UI 自动化**——G2 WxAiAgent 走接口层验证，不做 wx-side 端到端操作。

### 1.4 工具栈

| 工具 / 脚本 | 用途 | 路径 |
|---|---|---|
| Swagger UI | 浏览所有接口、参数/响应 schema 自检 | `http://localhost:5150/swagger` |
| REST Client (`*.http`) | 37 个核心请求的标准化脚本 | `backend/ManWei.Api/tests/api-tests.http` |
| VS Code REST Client 扩展 | 一键执行 `*.http`、自动提取链式变量 | 客户端侧 |
| PowerShell `Measure-Command` | 单接口 min/avg/max/p95 采样（5 次循环）| Windows 端 |
| PowerShell `Start-Job` × 2 | 2 并发 wall time 测量 | Windows 端 |
| curl + bash `--data-binary @file` | UTF-8 编码 JSON 直发，规避 PowerShell GBK 问题 | 客户端侧 |
| SQL Server `sqlcmd` | 脏数据 `EmotionTags` 表直查取证 | DB 直连 |

---

## 2. 测试维度矩阵

| 维度 | 覆盖范围 | 执行方式 |
|---|---|---|
| **功能** | 7 大业务模块核心读写 | REST Client 单调用 + 断言 |
| **接口（契约）** | 路径/Header/参数/返回码 | Swagger 校验 + `*.http` 实际请求 |
| **边界** | ep=999/0, emo=6, 无token, 错密码 | REST Client 显式边界请求 |
| **授权** | Admin-only 接口 403 | 普通用户 token 请求 |
| **性能** | 单接口 RT, 2 并发 wall, AI TTFB | PowerShell |
| **兼容性（字符编码）** | 中文 tag 在 GBK/UTF-8 下差异 | UTF-8 JSON + curl 直发 |
| **回归** | 脏数据删除后再查询 | DELETE + GET 验证 |

---

## 3. 12 个测试用例设计全表

### 3.1 用例 A1 — 邮箱登录

| 字段 | 内容 |
|---|---|
| **编号** | A1 |
| **标题** | 邮箱登录拿 JWT，验证当前用户信息 |
| **目标** | 验证 `POST /api/auth/login` 与 `GET /api/users/me` 链路 |
| **前置条件** | 后端 `http://localhost:5150` 已启动；SQL Server 已有用户 `<TEST_USER>` |
| **测试步骤** | 1. `POST /api/auth/login`，body `{"username":"<TEST_USER>","password":"<TEST_PASSWORD>"}`（密码已脱敏）<br>2. 提取 `data.token` 与 `data.userId`<br>3. `GET /api/users/me`，Header `Authorization: Bearer {{token}}` |
| **预期结果** | 步骤1 返回 200，含 JWT 与 userId=<UID>；步骤3 返回 200，含用户档案 |
| **实际结果** | userId=<UID>，JWT 已拿到 |
| **状态** | ✅ |

### 3.2 用例 A2 — 无效 token

| 字段 | 内容 |
|---|---|
| **编号** | A2 |
| **标题** | 无效 token / 错误密码 → 401 |
| **目标** | 验证鉴权层在缺失/错误凭证下正确返回 401 |
| **前置条件** | A1 已成功拿到合法 token |
| **测试步骤** | 1. `GET /api/users/me`（不携带 Authorization 头）<br>2. `POST /api/auth/login`，body `{"username":"<TEST_USER>","password":"<WRONG_PASSWORD>"}` |
| **预期结果** | 步骤1 401；步骤2 401 |
| **实际结果** | 401（A2 与 A2b 均符合预期）|
| **状态** | ✅ |

### 3.3 用例 B1 — 搜索

| 字段 | 内容 |
|---|---|
| **编号** | B1 |
| **标题** | 按关键词搜索动漫（中文"进击的巨人"）|
| **目标** | 验证中文 URL 编码下命中 Bangumi API 与本地缓存 |
| **测试步骤** | `GET /api/Favorites/search-anime?keyword=%E8%BF%9B%E5%87%BB%E7%9A%84%E5%B7%A8%E4%BA%BA` |
| **实际结果** | 命中 animeId=47（进击的巨人）|
| **状态** | ✅ |

### 3.4 用例 B2 — Sync 同步 Bangumi 详情

| 字段 | 内容 |
|---|---|
| **编号** | B2 |
| **标题** | 同步 Bangumi ID=118335 动漫详情到本地库 |
| **目标** | 验证 Bangumi 远程详情（标题/总集数/staff/infobox）落库 |
| **前置条件** | Bangumi ID 118335 → 进击的巨人 第二季，TotalEpisodes=25 |
| **测试步骤** | 1. `POST /api/Anime/Sync/118335`<br>2. `GET /api/Anime/47` |
| **实际结果** | 118335 同步成功，animeId=47，TotalEpisodes=25 |
| **状态** | ✅ |

### 3.5 用例 B3 — 收藏状态流转

| 字段 | 内容 |
|---|---|
| **编号** | B3 |
| **标题** | 状态机：创建 → 在看 → 看过，全链路 |
| **目标** | 验证 status: 0(想看)→1(在看)→2(看过)，rating=9/progress=25 |
| **测试步骤** | 1. `POST /api/Favorites`，body `{"animeId":47}`<br>2. `PUT /api/Favorites/170`，status=1, progress=5<br>3. `PUT /api/Favorites/170`，status=2, rating=9, progress=25<br>4. `GET /api/Favorites?Page=1&PageSize=10`<br>5. `GET /api/Favorites/counts`<br>6. `GET /api/Favorites/check/47` |
| **实际结果** | status 0→2, rating=9, progress=25；列表/计数/check 三接口正常 |
| **状态** | ✅ |

### 3.6 用例 C1 — 5 集情绪记录

| 字段 | 内容 |
|---|---|
| **编号** | C1 |
| **标题** | 为收藏 170 连续记录 5 个集数的情绪等级 |
| **目标** | 验证 POST/GET 写入-读取链路，5 条记录覆盖"低开-高潮-回落" |
| **前置条件** | favoriteId=170 已 status=2, progress=25；TotalEpisodes=25 |
| **测试步骤** | 依次 POST ep=1(emo=5)/ep=5(4)/ep=12(3)/ep=20(5)/ep=25(4)，最后 GET /api/EmotionCurves/170 |
| **实际结果** | 5/5 写入成功；GET 返回 5 条曲线点 |
| **状态** | ✅ |

### 3.7 用例 C2 — Episode 上限校验

| 字段 | 内容 |
|---|---|
| **编号** | C2 |
| **标题** | Episode / emotionLevel 边界值校验（C2/C2b/C2c 三边界）|
| **目标** | 验证 CLAUDE.md L20 已知坑点：用真实 TotalEpisodes 而非 500 兜底 |
| **测试步骤** | 1. `POST /api/EmotionCurves` ep=999, emo=5<br>2. ep=0, emo=3<br>3. emo=6, ep=15 |
| **实际结果** | C2 400 "集数不能超过总集数 25 集"；C2b 400 "集数必须大于0"；C2c 400 "情感等级必须在1-5之间" |
| **状态** | ✅ |

### 3.8 用例 D1 — 情感标签 + 词云

| 字段 | 内容 |
|---|---|
| **编号** | D1 |
| **标题** | 自定义标签 5 个 + 已使用列表 + 词云（含 Admin 403）|
| **目标** | 验证标签写入（中文 nvarchar 完整性）、列表、词云 4 个端点 |
| **前置条件** | 客户端用 UTF-8 JSON + curl `--data-binary @file` 发送（避免 PowerShell GBK 问题，见 BUG-001）|
| **测试步骤** | 1. 写入 5 条中文标签（"热血""燃""剧情向""思考""冲击"）<br>2. GET /api/EmotionTags/used<br>3. GET /api/EmotionTags/anime/47/wordcloud（期望 403 Admin 专用）<br>4. GET /api/EmotionTags/wordcloud |
| **实际结果** | 9 个正常标签 + 2 条脏数据（已清理）；D1.7 返回 403 Admin 专用（设计如此，见 `EmotionTagsController.cs` 第 168 行）|
| **状态** | ⚠️ → ✅（清理后回归通过）|

**D1 清理后回归验证**：
- DELETE /api/EmotionTags/49 → 200
- DELETE /api/EmotionTags/52 → 200
- GET /api/EmotionTags/used → 200，仅含 9 条正常标签：`["ASCII_TAG","冲击","催泪","剧情向","热血","热血少年","神作","思考","治愈"]`

### 3.9 用例 E1 — 观后感

| 字段 | 内容 |
|---|---|
| **编号** | E1 |
| **标题** | 写短评、查单条、分页列表 |
| **测试步骤** | 1. `POST /api/Reviews`，body `{"favoriteId":170,"content":"巨人的结局出人意料，作者对人性的刻画很深刻。"}`<br>2. `GET /api/Reviews/170`<br>3. `GET /api/Reviews?Page=1&PageSize=10` |
| **实际结果** | 中文 content 完整保留（无乱码）|
| **状态** | ✅ |

### 3.10 用例 F1 — 推荐论文复现（full 模式）

| 字段 | 内容 |
|---|---|
| **编号** | F1 |
| **标题** | 混合推荐 Top-K（`?deterministic=true` 论文复现）|
| **目标** | 验证冷启动条件 `Rating>=8 && Status==2` 下进入 full 模式 |
| **前置条件** | favoriteId=170 已 status=2, rating=9（满足 `Rating>=8 && Status==2` 硬门槛，定义见 `TagProfileBuilder.cs` 第 51 行 `H = {f.Rating>=8 && f.Status==2}`）；已写 5 条情绪曲线 |
| **测试步骤** | 1. `GET /api/recommendations?limit=10&deterministic=true`<br>2. `GET /api/recommendations?limit=10` |
| **实际结果** | mode=full，命中咒术回战/鬼灭之刃/夏日重现 |
| **状态** | ✅ |

### 3.11 用例 G1 — PcAiAgent 流式对话

| 字段 | 内容 |
|---|---|
| **编号** | G1 |
| **标题** | PC 端 AI 流式对话，NDJSON delta 正常 |
| **目标** | 验证 `POST /api/pcaia/chat-stream`（**不是 `/api/AiAgent/chat`**，后者 Admin 专用）|
| **前置条件** | DeepSeek API Key 已配置 |
| **测试步骤** | `POST /api/pcaia/chat-stream`，body `{"message":"推荐一部类似进击的巨人的热血番","history":[]}` |
| **实际结果** | NDJSON delta 正常，TTFB 207ms |
| **状态** | ✅ |

### 3.12 用例 G2 — WxAiAgent 中文对话

| 字段 | 内容 |
|---|---|
| **编号** | G2 |
| **标题** | 小程序端 AI 对话（中文）|
| **测试步骤** | `POST /api/WxAiAgent/chat`，body `{"message":"帮我推荐","history":[]}` |
| **实际结果** | 中文 AI 回复 |
| **状态** | ✅ |

---

## 4. 性能用例补充

### P1 — 单接口响应时间基线

| 接口 | min (ms) | avg (ms) | max (ms) | p95 (ms) | 备注 |
|---|---|---|---|---|---|
| GET /api/Anime/47 | 137 | 314.8 | 1012 | 146 | 首请求冷启动 1s |
| GET /api/recommendations | 9 | 20 | 55 | 14 | 推荐算法高效 |
| GET /api/Favorites | 2 | 3.6 | 9 | 3 | — |
| GET /api/EmotionCurves/170 | 1 | 2.2 | 5 | 2 | — |
| GET /api/EmotionTags/used | 1 | 2.8 | 9 | 2 | — |
| GET /api/Favorites/search-anime | 340 | 382.2 | 440 | 412 | 触发 Bangumi 远程 API |

### P2 — 2 并发 wall time

| 字段 | 内容 |
|---|---|
| **场景** | 2 个 PowerShell `Start-Job` 并行，每个 job 串行跑 5 个本地接口 |
| **实测 wall** | 1318 ms |

### P3 — AI 流式 TTFB

| 字段 | 内容 |
|---|---|
| **TTFB** | 207 ms |
| **Total** | 2291 ms |
| **限制** | 单请求，不并发（避免 DeepSeek 限流）|

---

## 5. 用例汇总

| 编号 | 标题 | 状态 |
|---|---|---|
| A1 | 邮箱登录 | ✅ |
| A2 | 无效 token | ✅ |
| B1 | 搜索 | ✅ |
| B2 | Sync | ✅ |
| B3 | 收藏状态流转 | ✅ |
| C1 | 5 集情绪 | ✅ |
| C2 | Episode 上限校验 | ✅ |
| D1 | 标签 + 词云 | ⚠️ → ✅ |
| E1 | 观后感 | ✅ |
| F1 | 推荐 full | ✅ |
| G1 | PcAiAgent 流式 | ✅ |
| G2 | WxAiAgent 中文 | ✅ |
| P1 | 单接口 RT | ✅ |
| P2 | 2 并发 wall | ✅ |
| P3 | AI 流式 TTFB | ✅ |

**汇总**：12 功能用例 + 3 性能用例 = 15 测试项，14 ✅、1 ⚠️→✅。

**通过率**：13/15 = 86.7%（D1.7 + AiAgent Admin 不算失败，属设计如此）；**真正失败：0 个**；**真实 BUG：1 个（BUG-001 工具链问题）**。
