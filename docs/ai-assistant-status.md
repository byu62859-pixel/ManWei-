# PC AI 助手 — 实施完成报告

**日期**: 2026-06-16
**分支**: `feat/pc-ai-assistant`
**总 commits**: 15 (baseline + 11 feature + 3 fixup)

## 已实现

### 后端 (`backend/ManWei.Api/`)
- `BaseAiAgentService.ResolveApiKey()` — env var `DEEPSEEK__APIKEY` 优先，`appsettings.json` 兜底
- `BaseAiAgentService.StreamChatAsync()` — `IAsyncEnumerable<StreamEvent>` 流式 + ToolCallAccumulator 按 index 聚合 + for 循环 + maxRounds=8 防止死循环
- `BaseAiAgentService.StreamEvent` 公共嵌套类
- `BaseAiAgentService.ToolCallAccumulator` 私有嵌套类
- `PcAiTools` — 5 个 tool 注册表（3 实现 + 2 桩）
- `PcAiAgentService` — 继承 BaseAiAgentService，3 真实 tool + 2 桩
- `PcAiAgentController` — `POST /api/pcaia/chat-stream` 返回 NDJSON

### 前端 (`frontend/pc-client/`)
- `types/api.ts` — 新增 `ChatMessage` / `ChatToolCallItem` / `ChatStreamEvent` / `PcChatRequest`
- `stores/aiAssistantStore.ts` — Zustand store，14 个 action
- `services/chat.ts` — `streamChat` NDJSON fetch + ReadableStream 解析 + AbortController
- `components/AiAssistantDrawer/index.tsx` — Drawer + MessageBubble + Input
- `components/AiAssistantDrawer/AiAssistantDrawer.module.css` — 样式（用 design tokens）
- `components/AppShell/index.tsx` — 顶部"AI 助手"按钮 + 挂载 Drawer
- `index.css` — 新增 5 个 design tokens (`--color-surface`, `--color-error`, `--color-error-bg`, `--color-error-border`, `--color-muted`)

### Secret 处理
- `appsettings.json` 4 个 secret 全部替换为 `YOUR_*_HERE` 占位符
- 通过 4 个 env var 提供（`DEEPSEEK__APIKEY` 等）
- 任何 commit 都不含真实 secret

## 自动化验证

| 检查 | 结果 |
|---|---|
| `dotnet build` (所有 5 个后端 task) | ✅ 0 errors |
| `npx tsc -b --noEmit` (所有 6 个前端 task) | ✅ 0 errors |
| `npm run build` | ✅ Built in 1.32s |
| Backend 启动 + 401 auth gate smoke test | ✅ 路由 + JWT 验证正常 |
| 12+ git commits | ✅ 15 commits (含 fixup) |

## 需要手动验证的部分

以下需要用户实际操作（无法在 CI/脚本中完成）：

1. **登录后真实对话** — 启动 backend + frontend dev server，登录任一用户，访问任一页面 → 点"AI 助手"按钮 → 问"你好" → 应看到流式打字机效果
2. **tool 调用** — 问"我最近看了什么番" → 应看到 🔧 调用面板 → 展开看到 JSON 结果 → AI 基于数据继续流式
3. **多 tool 调用** — DeepSeek 可能一次性触发多个 tool（如果 prompt 暗示），应看到多个折叠面板
4. **stub tool 降级** — 问"帮我搜一下孤独摇滚" → 触发 search_anime 桩 → AI 应优雅降级回答
5. **流式中断** — 流式过程中关闭抽屉 → 应 abort 成功，messages 清空
6. **401 跳转** — DevTools 清 `mw_token` → 发消息 → 应跳 /login
7. **网络中断** — DevTools Offline → 发消息 → 应看到 ⚠️ banner + 已收到的 delta 保留
8. **WX/Admin 回归** — `/api/wxaiagent/chat` 和 `/api/aiagent/chat` 仍能正常工作（无流式，JSON 响应）

## 已知遗留项（plan 已声明为 out of scope）

- 对话历史持久化
- 2 个 stub tool（search_anime / query_global_emotion_tags）实际实现
- Admin 端共享此助手
- 工具调用人工审核机制

## 后续可优化项（非阻塞，已在 review 中识别）

1. `StreamChatAsync` 加日志（round 入口 / 超过 maxRounds）— plan 范围内但建议 polish
2. `GetTools()` 中 `JsonDocument` 未释放 — project-wide 模式（WxAiAgentService / AiAgentService 也有）
3. `WxAiAgentService._userId` 跨请求污染 — 已知 latent bug，本项目未引入新恶化
