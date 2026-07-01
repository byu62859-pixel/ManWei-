# 漫味 (ManWei) 项目测试报告 — 02 测试执行

> 本文档记录 2026-06-20 对漫味项目后端 API + 前端三端共 12 个一级用例的执行过程、结果与性能数据。

## 1. 执行概述

- **执行日期**：2026-06-20
- **测试账号**：`<TEST_USER>`（密码已脱敏）
- **后端地址**：`http://localhost:5150`
- **DB**：SQL Server `localhost/ManWeiDB`
- **接口脚本**：`backend/ManWei.Api/tests/api-tests.http`（37 个请求）
- **工具栈**：
  - 后端：.NET 8 + ASP.NET Core + EF Core 8 + SQL Server
  - PC 端：React 19 + Vite + TypeScript + Axios
  - 后台：Vue 3 + Element Plus + Vite
  - 小程序：微信开发者工具 + 原生小程序 + Canvas 2D
  - 接口测试：VS Code REST Client + PowerShell `Invoke-RestMethod`
  - 性能测试：PowerShell `Start-Job` 并发 + `Measure-Command`

## 2. 执行环境

| 环境维度 | 实际值 |
|---|---|
| 操作系统 | <OS>（GBK 控制台）|
| .NET SDK | .NET 8 |
| 数据库 | SQL Server（实例 `localhost`，库名 `ManWeiDB`）|
| Web 框架 | ASP.NET Core 8（JWT Bearer 鉴权）|
| PC 端 | React 19 + TypeScript + Vite |
| 后台 | Vue 3 + Element Plus + Vite |
| 小程序 | 微信开发者工具 + 原生小程序 + Canvas 2D |
| 后端启动命令 | `cd backend/ManWei.Api && dotnet run --launch-profile http` |

## 3. 执行记录（12 个一级用例）

> **口径说明**：下表共 15 行（C2 拆 3 行 + D1 拆 2 行），**用例统计以 12 个一级用例为准**。

| 用例 | 状态 | 关键数据 | 备注 |
|---|---|---|---|
| A1 登录 | ✅ | userId=<UID>，JWT 已拿 | A1.1 登录 + A1.2 拿用户信息 |
| A2 无效 token | ✅ | 401 | A2 无 header 401；A2b 错密码 401 |
| B1 搜索 | ✅ | 命中 animeId=47 | 关键词"进击的巨人"|
| B2 Sync | ✅ | 118335 | Bangumi 同步成功，TotalEpisodes=25 |
| B3 收藏状态流转 | ✅ | status 0→2, rating=9, progress=25 | B3.1-B3.6 全流程闭环 |
| C1 5 集情绪 | ✅ | 5/5 写入 | 5 条情绪记录全部落库 |
| C2 ep=999 | ✅ | 400 "集数不能超过总集数 25 集" | CLAUDE.md L20 验证：真实 TotalEpisodes 兜底 |
| C2b ep=0 | ✅ | 400 "集数必须大于0" | 下界校验 |
| C2c emo=6 | ✅ | 400 "情感等级必须在1-5之间" | 枚举越界校验 |
| D1 标签 | ⚠️ | 9 正常 + 2 脏数据（已清理）| BUG-001 工具链问题，详见 03-bugs.md |
| D1.7 anime 词云 | ⚠️ 设计如此 | 403 Admin 专用 | **不是 BUG**（详见 §5 边界发现）|
| E1 观后感 | ✅ | 中文 content 完整保留 | 创建/查询/列表均正常 |
| F1 推荐 full | ✅ | mode=full，命中咒术回战/鬼灭之刃/夏日重现 | 冷启动门槛 `Rating>=8 && Status==2` |
| G1 PcAiAgent | ✅ | NDJSON delta 正常，TTFB 207ms | `/api/pcaia/chat-stream` |
| G2 WxAiAgent | ✅ | 中文 AI 回复 | 小程序端对话正常 |

**执行率**：12/12 = 100%
**通过率**：13/15 = 86.7%（D1.7 不算失败，属设计如此）

## 4. 性能测试结果

### 4.1 测试方法

| 类别 | 方法描述 |
|---|---|
| **单接口 RT** | 每个接口连续请求 5 次（首请求除外），取 min / avg / max / p95 |
| **2 并发 × 5 接口 wall** | 启动 2 个 PowerShell `Start-Job` 并行，每个 job 串行跑 5 个本地接口（Anime/47、recommendations、Favorites、EmotionCurves/170、EmotionTags/used），wall = 较慢 job 完成时间 + IPC 开销 |
| **AI chat TTFB / Total** | 单独 1 个请求，**不发并发**——避免触发 DeepSeek API 限流 |

### 4.2 性能数据表

| 接口 / 场景 | min (ms) | avg (ms) | max (ms) | p95 (ms) | 备注 |
|---|---|---|---|---|---|
| GET /api/Anime/47 | 137 | 314.8 | 1012 | 146 | 首请求冷启动 1s，后续稳态 ~140ms |
| GET /api/recommendations | 9 | 20 | 55 | 14 | 推荐算法高效 |
| GET /api/Favorites | 2 | 3.6 | 9 | 3 | — |
| GET /api/EmotionCurves/170 | 1 | 2.2 | 5 | 2 | — |
| GET /api/EmotionTags/used | 1 | 2.8 | 9 | 2 | — |
| GET /api/Favorites/search-anime | 340 | 382.2 | 440 | 412 | 触发 Bangumi 远程 API |
| **2 并发 × 5 接口 wall** | — | — | **1318** | — | 2 个 Start-Job 并行各跑 5 个本地接口，wall = 较慢 job + IPC 开销 |
| AI chat TTFB | 207 | — | — | — | `/api/pcaia/chat-stream` 首字节延迟（单请求）|
| AI chat Total | 2291 | — | — | — | 完整流式响应（单请求，避免 DeepSeek 限流）|

## 5. 边界发现（**已确认设计选择，非 BUG**）

> 本节 4 条均为已确认设计选择，**03-bugs.md 不收录**。

| 编号 | 现象 | 性质 | 代码位置（纯文本）|
|---|---|---|---|
| 边界-1 | `/api/EmotionTags/anime/{id}/wordcloud` 普通用户 403 | Admin 专用统计接口 | `backend/ManWei.Api/Controllers/EmotionTagsController.cs` 第 168 行 `[Authorize(Roles="Admin")]` |
| 边界-2 | `/api/AiAgent/chat` 普通用户 403 | Admin 专用 AI 接口 | `backend/ManWei.Api/Controllers/AiAgentController.cs` 第 13 行 `[Authorize(Roles="Admin")]` |
| 边界-3 | 推荐 popular 模式 breakdown 全 0 | popular 分支不调用 Scorer | `backend/ManWei.Api/Services/Recommendation/RecommendAnimeService.cs` 第 99-134 行 |
| 边界-4 | 冷启动进入 full 硬门槛 | `Rating >= 8 && Status == 2`（看过+高分）| `backend/ManWei.Api/Services/Recommendation/TagProfileBuilder.cs` 第 51 行 |

## 6. 已验证的 CLAUDE.md 已知坑点

| 坑点 | 验证结果 | 证据 |
|---|---|---|
| L11 JWT UserId `int.TryParse` | ✅ 已修复 | `PcAiAgentController.cs` 第 48 行 `if (!int.TryParse(idStr, out var userId))` |
| L20 Episode 上限校验 | ✅ 已修复（比 CLAUDE.md 描述更精细：真实 TotalEpisodes 非 500 兜底）| C2 响应：`"集数不能超过总集数 25 集"` |

## 7. 执行统计

| 指标 | 数值 |
|---|---|
| 一级用例总数 | 12 个 |
| 执行率 | 12/12 = 100% |
| 通过率 | 13/15 = 86.7%（D1.7 设计如此，不计失败）|
| 真实失败 | 0 个 |
| 真实 BUG | 1 个（BUG-001 工具链问题）|
| 已确认设计选择 | 4 条 |
| 已验证 CLAUDE.md 坑点 | 2 条（L11、L20）|
