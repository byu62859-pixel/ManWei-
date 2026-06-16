# PC 用户端 AI 助手 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 PC 用户端新增一个独立 AI 助手（独立 prompt + 5 tool + NDJSON 流式 + 右侧 Drawer），与现有 WX/Admin agent 解耦。

**Architecture:**
- 后端：新增 `PcAiAgentService`（继承 `BaseAiAgentService`）+ `PcAiAgentController`，`BaseAiAgentService` 新增 `StreamChatAsync` 流式方法（IAsyncEnumerable + ToolCallAccumulator 按 index 聚合），不动现有 `CallDeepSeekAsync`。
- 前端：fetch + ReadableStream 解析 NDJSON，Zustand store 管理 UI 状态，antd Drawer 在 `AppShell` 全局挂载。
- API key 加载顺序：env var `DEEPSEEK__APIKEY` 优先 → `appsettings.json` 兜底 → 抛错。

**Tech Stack:**
- Backend: ASP.NET Core 8, `IAsyncEnumerable<T>`, `System.Text.Json`, Entity Framework Core 8
- Frontend: React 19, TypeScript, Vite 8, antd 6, Zustand 5, fetch + ReadableStream

**Spec:** `docs/superpowers/specs/2026-06-16-pc-ai-assistant-design.md`

**Testing strategy:** This project has **no existing test framework** (no xUnit, no vitest). Each task uses **build verification** (`dotnet build` / `npm run build`) as the primary gate, with **manual smoke tests** (curl, browser click) for end-to-end checks. Adding test infrastructure is out of scope.

**Codebase conventions:**
- Models in `Models/` directory only (per CLAUDE.md hard rule)
- JWT userId: `int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)` (per CLAUDE.md)
- assistant message round-trip: anonymous objects, NOT a C# `ChatMessage` class (per CLAUDE.md)
- `dotnet run` blocks ManWei.Api.exe — kill process before rebuilding: `taskkill //F //IM "ManWei.Api.exe"`
- Frontend CSS Modules only, no styled-components

---

## File Structure

**Backend — Create:**
- `backend/ManWei.Api/Services/PcAiTools.cs` — 5 tool 注册表
- `backend/ManWei.Api/Services/PcAiAgentService.cs` — 继承 BaseAiAgentService, 3 个真实 tool + 2 个桩
- `backend/ManWei.Api/Controllers/PcAiAgentController.cs` — POST `/api/pcaia/chat-stream`, 返回 NDJSON

**Backend — Modify:**
- `backend/ManWei.Api/Services/BaseAiAgentService.cs` — 新增 `StreamChatAsync` + `ResolveApiKey` + `StreamEvent` 类 + `ToolCallAccumulator` 私有类
- `backend/ManWei.Api/Program.cs` — 注册 `AddScoped<PcAiAgentService>()`
- `backend/ManWei.Api/appsettings.json` — 加 `_comment` 字段

**Frontend — Create:**
- `frontend/pc-client/src/services/chat.ts` — NDJSON fetch 流式客户端
- `frontend/pc-client/src/stores/aiAssistantStore.ts` — Zustand store
- `frontend/pc-client/src/components/AiAssistantDrawer/index.tsx` — Drawer 组件
- `frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css` — 样式

**Frontend — Modify:**
- `frontend/pc-client/src/types/api.ts` — 加 `ChatMessage` / `ChatToolCallItem` / `ChatStreamEvent` / `PcChatRequest`
- `frontend/pc-client/src/components/AppShell/index.tsx` — 顶部加 "AI 助手" 按钮 + 挂载 Drawer

---

## Task 1: 后端 — API Key 加载修复 (`ResolveApiKey`)

**Files:**
- Modify: `backend/ManWei.Api/Services/BaseAiAgentService.cs:29`

- [ ] **Step 1: 替换 `CallDeepSeekAsync` 中的 API key 读取**

在 `BaseAiAgentService.cs:29` 找到:

```csharp
var apiKey = _config["DeepSeek:ApiKey"];
```

替换为:

```csharp
var apiKey = ResolveApiKey();
```

- [ ] **Step 2: 在 `BaseAiAgentService` 类内添加 `ResolveApiKey` 私有方法**

在 `BaseAiAgentService` 类内任意位置（建议放在 `CallDeepSeekAsync` 下方）添加:

```csharp
private string ResolveApiKey()
{
    // 优先 env var (约定: DEEPSEEK__APIKEY)
    var envKey = Environment.GetEnvironmentVariable("DEEPSEEK__APIKEY");
    if (!string.IsNullOrWhiteSpace(envKey)) return envKey;

    // fallback: appsettings.json (仅 dev 兜底)
    var configKey = _config["DeepSeek:ApiKey"];
    if (!string.IsNullOrWhiteSpace(configKey)) return configKey;

    throw new InvalidOperationException(
        "DeepSeek API key not configured. Set DEEPSEEK__APIKEY env var " +
        "or DeepSeek:ApiKey in appsettings (dev only).");
}
```

- [ ] **Step 3: 验证 dev 仍可工作**

如果当前 `appsettings.json` 里有 `DeepSeek:ApiKey`，无需设 env var 也能跑。手动 `dotnet build` 确认编译通过:

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
taskkill //F //IM "ManWei.Api.exe" 2>nul || true
dotnet build
```

Expected: `Build succeeded` (无错误)。如有警告可忽略。

- [ ] **Step 4: Commit**

```bash
git add backend/ManWei.Api/Services/BaseAiAgentService.cs
git commit -m "feat(ai): prefer DEEPSEEK__APIKEY env var with appsettings fallback"
```

---

## Task 2: 后端 — `BaseAiAgentService.StreamChatAsync` 流式核心

**Files:**
- Modify: `backend/ManWei.Api/Services/BaseAiAgentService.cs` (末尾追加新方法 + 私有类)

- [ ] **Step 1: 添加 `StreamEvent` 公共类**

在 `BaseAiAgentService` 类内（类闭合 `}` 之内）添加:

```csharp
public class StreamEvent
{
    public string Type { get; set; } = ""; // delta | tool_call | tool_result | done | error
    public string? Content { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public string? ToolArgsJson { get; set; }
    public string? ToolResultJson { get; set; }
    public string? Error { get; set; }
}
```

> **注意**: `StreamEvent` 虽然是 `public`，仍放在类内（C# 允许嵌套 public 类）。`StreamChatAsync` 方法返回 `IAsyncEnumerable<StreamEvent>`，调用方需要可见性，因此 public 是必需的。

- [ ] **Step 2: 添加 `ToolCallAccumulator` 私有嵌套类**

直接嵌套在 `BaseAiAgentService` 类内（不在 namespace 顶层，不需要 `partial` 也不需要 `public abstract`）:

```csharp
private class ToolCallAccumulator
{
    public string Id = "";
    public string? Type;
    public string Name = "";
    public System.Text.StringBuilder ArgumentsJson = new();
}
```

**位置**: 把 `StreamEvent`（Step 1）和 `ToolCallAccumulator`（本 Step）都放在 `BaseAiAgentService` 类内（namespace 闭合 `}` 之前、类闭合 `}` 之内）。后续 Step 3 的 `StreamChatAsync` 也放在类内。

- [ ] **Step 3: 添加 `StreamChatAsync` 方法**

在 `BaseAiAgentService` 类内、`ResolveApiKey` 方法后添加:

```csharp
public async IAsyncEnumerable<StreamEvent> StreamChatAsync(
    IEnumerable<object> messages,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
{
    var client = _httpClientFactory.CreateClient("DeepSeek");
    var apiKey = ResolveApiKey();
    var model = _config["DeepSeek:Model"] ?? "deepseek-chat";

    var tools = GetTools().ToList();
    var messageList = messages.ToList();

    // 递归深度上限保护（防 tool 调用无限循环）
    const int maxRounds = 8;
    for (int round = 0; round < maxRounds; round++)
    {
        var payload = new
        {
            model,
            messages = messageList,
            tools = tools.Count > 0 ? tools : null,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        // 流式聚合状态
        var contentBuilder = new System.Text.StringBuilder();
        var toolCalls = new Dictionary<int, ToolCallAccumulator>();
        var finishReason = (string?)null;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var data = line.Substring(5).Trim();
            if (data == "[DONE]") break;

            JsonElement chunk;
            try { chunk = JsonDocument.Parse(data).RootElement.Clone(); }
            catch { continue; }

            var choice = chunk.GetProperty("choices")[0];
            var delta = choice.GetProperty("delta");

            // 1) content delta
            if (delta.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.String)
            {
                var piece = contentEl.GetString() ?? "";
                contentBuilder.Append(piece);
                yield return new StreamEvent { Type = "delta", Content = piece };
            }

            // 2) tool_calls delta - 按 index 聚合
            if (delta.TryGetProperty("tool_calls", out var tcDelta) &&
                tcDelta.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcDelta.EnumerateArray())
                {
                    var idx = tc.GetProperty("index").GetInt32();
                    if (!toolCalls.TryGetValue(idx, out var acc))
                    {
                        acc = new ToolCallAccumulator();
                        toolCalls[idx] = acc;
                    }
                    if (tc.TryGetProperty("id", out var idEl))
                        acc.Id = idEl.GetString() ?? acc.Id;
                    if (tc.TryGetProperty("type", out var typeEl))
                        acc.Type = typeEl.GetString() ?? acc.Type;
                    if (tc.TryGetProperty("function", out var fnEl))
                    {
                        if (fnEl.TryGetProperty("name", out var nameEl))
                            acc.Name = nameEl.GetString() ?? acc.Name;
                        if (fnEl.TryGetProperty("arguments", out var argsEl))
                            acc.ArgumentsJson.Append(argsEl.GetString() ?? "");
                    }
                }
            }

            // 3) finish_reason
            if (choice.TryGetProperty("finish_reason", out var frEl) &&
                frEl.ValueKind == JsonValueKind.String)
            {
                finishReason = frEl.GetString();
            }
        }

        // 4) 收敛: 构造 assistant message (anonymous object 风格, 不引入 ChatMessage 类)
        var assistantMessage = new
        {
            role = "assistant",
            content = contentBuilder.ToString(),
            tool_calls = toolCalls.Count > 0
                ? toolCalls.OrderBy(kv => kv.Key).Select(kv => new
                {
                    id = kv.Value.Id,
                    type = kv.Value.Type ?? "function",
                    function = new
                    {
                        name = kv.Value.Name,
                        arguments = kv.Value.ArgumentsJson.ToString()
                    }
                }).ToArray<object>()
                : null
        };
        messageList.Add(assistantMessage);

        // 5) 无 tool_call → 正常结束
        if (toolCalls.Count == 0)
        {
            yield return new StreamEvent { Type = "done" };
            yield break;
        }

        // 6) 有 tool_call → 执行工具, 注入 tool message, 再发下一轮请求
        foreach (var (idx, acc) in toolCalls.OrderBy(kv => kv.Key))
        {
            yield return new StreamEvent
            {
                Type = "tool_call",
                ToolCallId = acc.Id,
                ToolName = acc.Name,
                ToolArgsJson = acc.ArgumentsJson.ToString()
            };

            Dictionary<string, object?> args;
            try
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    acc.ArgumentsJson.ToString()) ?? new();
            }
            catch
            {
                args = new();
            }

            var toolResult = await ExecuteToolAsync(acc.Name, args, ct);
            yield return new StreamEvent
            {
                Type = "tool_result",
                ToolCallId = acc.Id,
                ToolResultJson = toolResult
            };

            // tool message 也按 anonymous object 注入, 保持 tool_call_id 字段名
            messageList.Add(new
            {
                role = "tool",
                tool_call_id = acc.Id,
                content = toolResult
            });
        }

        // 7) continue outer for-loop → 下一轮 stream 请求
    }

    // 超过 maxRounds 仍未 done
    yield return new StreamEvent { Type = "error", Error = "Tool call rounds exceeded" };
}
```

- [ ] **Step 4: 编译验证**

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
taskkill //F //IM "ManWei.Api.exe" 2>nul || true
dotnet build
```

Expected: `Build succeeded`。如报 `IAsyncEnumerable` / `EnumeratorCancellation` 找不到，文件顶部检查 `using System;` 和 `using System.Runtime.CompilerServices;` 是否存在。`EnumeratorCancellation` attribute 加全限定名 `System.Runtime.CompilerServices.EnumeratorCancellationAttribute` 也行。

- [ ] **Step 5: Commit**

```bash
git add backend/ManWei.Api/Services/BaseAiAgentService.cs
git commit -m "feat(ai): add StreamChatAsync with tool_call aggregation via IAsyncEnumerable"
```

---

## Task 3: 后端 — `PcAiTools` 工具注册表

**Files:**
- Create: `backend/ManWei.Api/Services/PcAiTools.cs`

- [ ] **Step 1: 验证 `AiTool` 类型已存在**

```bash
grep -n "class AiTool" d:\AnimeEmotion\backend\ManWei.Api\Services\AiTools.cs
```

Expected: `3:public class AiTool`（或其他行号，但应找到该定义）。如果**未找到**，先在 `PcAiTools.cs` 中添加:

```csharp
public class AiTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 创建 `PcAiTools.cs`**

新建 `backend/ManWei.Api/Services/PcAiTools.cs`:

```csharp
namespace ManWei.Api.Services;

public static class PcAiTools
{
    public static readonly List<AiTool> AllTools = new()
    {
        new AiTool
        {
            Name = "query_my_favorites",
            Description = "查询当前用户的收藏列表。" +
                          "可选参数: status (0=想看 1=在看 2=看过, 不传=全部), " +
                          "take (返回数量, 默认10, 最大50)。" +
                          "返回字段: id, animeId, animeName, status, progress, rating。",
            Parameters = """{
                "type": "object",
                "properties": {
                    "status": { "type": "integer", "enum": [0, 1, 2] },
                    "take": { "type": "integer", "minimum": 1, "maximum": 50 }
                }
            }"""
        },
        new AiTool
        {
            Name = "query_user_stats",
            Description = "查询当前用户的追番统计: " +
                          "收藏总数、在看数量、已看数量、平均评分(1-10, 可能 null)。" +
                          "无入参。",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "query_anime_emotion_curve",
            Description = "查询用户对某部动漫的情绪曲线。" +
                          "必须参数: animeId (整数)。" +
                          "若用户未收藏该动漫则返回 error=not_favorited。" +
                          "返回: animeId, favoriteId, pointCount, " +
                          "points: [{episode, emotionLevel}]。",
            Parameters = """{
                "type": "object",
                "properties": {
                    "animeId": { "type": "integer" }
                },
                "required": ["animeId"]
            }"""
        },
        new AiTool
        {
            Name = "search_anime",
            Description = "按关键词搜索动漫(本版未实现, 调用将返回 not_implemented)。",
            Parameters = """{
                "type": "object",
                "properties": { "keyword": { "type": "string" } }
            }"""
        },
        new AiTool
        {
            Name = "query_global_emotion_tags",
            Description = "查询用户常用的情绪标签(本版未实现, 调用将返回 not_implemented)。",
            Parameters = """{"type":"object","properties":{}}"""
        }
    };
}
```

- [ ] **Step 3: 编译验证**

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
taskkill //F //IM "ManWei.Api.exe" 2>nul || true
dotnet build
```

Expected: `Build succeeded`。

- [ ] **Step 4: Commit**

```bash
git add backend/ManWei.Api/Services/PcAiTools.cs
git commit -m "feat(ai): add PcAiTools registry with 5 tools (3 real + 2 stub)"
```

---

## Task 4: 后端 — `PcAiAgentService` 子类

**Files:**
- Create: `backend/ManWei.Api/Services/PcAiAgentService.cs`

- [ ] **Step 1: 创建 `PcAiAgentService.cs`**

新建 `backend/ManWei.Api/Services/PcAiAgentService.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

public class PcAiAgentService : BaseAiAgentService
{
    private readonly AppDbContext _context;
    private int? _userId;

    public PcAiAgentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<PcAiAgentService> logger,
        AppDbContext context)
        : base(httpClientFactory, config, logger)
    {
        _context = context;
    }

    public void SetUserId(int userId) => _userId = userId;

    // 公开 (非 protected override): controller 构造请求消息时需读取
    public string SystemPrompt => """
        你是漫味(ManyAi)PC 端的私人追番顾问助手，与微信小程序端共享同一份用户数据。
        与小程序端的"轻量闲聊"定位不同，PC 端助手更偏向专业数据查询：

        - 用户可能在分析自己的追番习惯、情绪分布、年度总结
        - 用户可能在寻找"和 XX 类似"的番剧
        - 用户可能想深入了解某部番的情绪曲线

        你拥有 5 个工具可以查询用户数据：
        - query_my_favorites: 查询我的收藏(支持状态筛选: 0=想看 1=在看 2=看过)
        - query_user_stats: 查询我的追番统计(总数/时长/评分分布)
        - query_anime_emotion_curve: 查询某部番(按 animeId)我的情绪曲线数据
        - search_anime: 搜索动漫(留桩,本版未实现)
        - query_global_emotion_tags: 查询我常用的情绪标签(留桩)

        使用工具时要主动、简洁，不要过度调用。
        回答要有数据支撑，但避免堆砌数字。
        """;

    protected override string AgentSystemPrompt => SystemPrompt;

    protected override IEnumerable<object> GetTools() =>
        PcAiTools.AllTools.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonDocument.Parse(t.Parameters).RootElement
            }
        }).ToList();

    protected override async Task<string> ExecuteToolAsync(
        string name, Dictionary<string, object?> args, CancellationToken ct)
    {
        if (_userId == null) return """{"error":"unauthenticated"}""";

        return name switch
        {
            "query_my_favorites" => await QueryMyFavoritesAsync(args, ct),
            "query_user_stats" => await QueryUserStatsAsync(ct),
            "query_anime_emotion_curve" => await QueryEmotionCurveAsync(args, ct),
            "search_anime" => """{"error":"not_implemented","message":"该工具将在 v2 实现"}""",
            "query_global_emotion_tags" => """{"error":"not_implemented","message":"该工具将在 v2 实现"}""",
            _ => """{"error":"unknown_tool"}"""
        };
    }

    private async Task<string> QueryMyFavoritesAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        var status = args.TryGetValue("status", out var s) && s != null
            ? Convert.ToInt32(s) : (int?)null;
        var take = args.TryGetValue("take", out var t) && t != null
            ? Convert.ToInt32(t) : 10;

        var query = _context.Favorites
            .Where(f => f.UserId == _userId);
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);

        var items = await query
            .OrderByDescending(f => f.CreateTime)
            .Take(take)
            .Select(f => new
            {
                f.Id,
                f.AnimeId,
                AnimeName = f.Anime != null ? f.Anime.Name : "(未知)",
                f.Status,
                f.Progress,
                f.Rating
            })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { count = items.Count, items });
    }

    private async Task<string> QueryUserStatsAsync(CancellationToken ct)
    {
        var userId = _userId!.Value;
        var total = await _context.Favorites.CountAsync(f => f.UserId == userId, ct);
        var watching = await _context.Favorites
            .CountAsync(f => f.UserId == userId && f.Status == 1, ct);
        var watched = await _context.Favorites
            .CountAsync(f => f.UserId == userId && f.Status == 2, ct);
        var avgRating = await _context.Favorites
            .Where(f => f.UserId == userId && f.Rating != null)
            .Select(f => (double?)f.Rating)
            .AverageAsync(ct);

        return JsonSerializer.Serialize(new
        {
            total,
            watching,
            watched,
            avgRating
        });
    }

    private async Task<string> QueryEmotionCurveAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("animeId", out var aidObj) || aidObj == null)
            return """{"error":"animeId required"}""";
        var animeId = Convert.ToInt32(aidObj);

        var userId = _userId!.Value;
        var fav = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.AnimeId == animeId, ct);
        if (fav == null) return """{"error":"not_favorited"}""";

        var points = await _context.EmotionCurves
            .Where(e => e.FavoriteId == fav.Id)
            .OrderBy(e => e.Episode)
            .Select(e => new { e.Episode, e.EmotionLevel })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new
        {
            animeId,
            favoriteId = fav.Id,
            pointCount = points.Count,
            points
        });
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
taskkill //F //IM "ManWei.Api.exe" 2>nul || true
dotnet build
```

Expected: `Build succeeded`。如报 `_context.Favorites` / `_context.EmotionCurves` / `_context.Anime` 不存在，对照 `Models/Favorite.cs` / `Models/EmotionCurve.cs` / `Models/Anime.cs` 检查字段名是否匹配 (`UserId` / `AnimeId` / `FavoriteId` / `Status` / `Progress` / `Rating` / `CreateTime` / `Id` / `Name` / `Episode` / `EmotionLevel`)。如有偏差，按实际字段名调整 LINQ。

- [ ] **Step 3: Commit**

```bash
git add backend/ManWei.Api/Services/PcAiAgentService.cs
git commit -m "feat(ai): add PcAiAgentService with 3 real tools and 2 stubs"
```

---

## Task 5: 后端 — `PcAiAgentController` + DTO

**Files:**
- Create: `backend/ManWei.Api/Controllers/PcAiAgentController.cs`

- [ ] **Step 1: 创建 `PcAiAgentController.cs`**

新建 `backend/ManWei.Api/Controllers/PcAiAgentController.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using ManWei.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManWei.Api.Controllers;

[ApiController]
[Route("api/pcaia")]
[Authorize]
public class PcAiAgentController : ControllerBase
{
    private readonly PcAiAgentService _service;
    private readonly ILogger<PcAiAgentController> _logger;

    public PcAiAgentController(
        PcAiAgentService service,
        ILogger<PcAiAgentController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("chat-stream")]
    public async Task ChatStream(
        [FromBody] PcChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync(
                "{\"type\":\"error\",\"error\":\"消息内容不能为空\"}\n", ct);
            return;
        }

        // 提取 userId (按 CLAUDE.md 红线: 必须 int.TryParse)
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId))
        {
            Response.StatusCode = 401;
            await Response.WriteAsync(
                "{\"type\":\"error\",\"error\":\"未登录\"}\n", ct);
            return;
        }
        _service.SetUserId(userId);

        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (var evt in _service.StreamChatAsync(
                new[] {
                    new { role = "system", content = _service.AgentSystemPrompt },
                    new { role = "user", content = request.Message }
                },
                ct))
            {
                var json = JsonSerializer.Serialize(evt);
                await Response.WriteAsync(json + "\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "PC AI chat stream cancelled by client, userId={UserId}", userId);
            // 不再尝试写 body, 连接已断开
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PC AI chat stream error, userId={UserId}", userId);
            try
            {
                await Response.WriteAsync(
                    $"{{\"type\":\"error\",\"error\":\"AI 服务错误: {ex.Message}\"}}\n",
                    CancellationToken.None);
            }
            catch { /* connection probably dead */ }
        }
    }
}

public class PcChatRequestDto
{
    public string Message { get; set; } = "";
    // History 字段保留但不传给后端 (前端 UI 多轮但 server 无状态)
    public List<object>? History { get; set; }
}
```

- [ ] **Step 2: 注册到 DI**

修改 `backend/ManWei.Api/Program.cs`，找到现有的:

```csharp
builder.Services.AddScoped<WxAiAgentService>();
```

在它**下方**添加:

```csharp
builder.Services.AddScoped<PcAiAgentService>();
```

- [ ] **Step 3: 更新 `appsettings.json` 加注释**

修改 `backend/ManWei.Api/appsettings.json` 找到 `"DeepSeek"` 段, 改为:

```json
"DeepSeek": {
  "_comment": "API key should be set via env var DEEPSEEK__APIKEY in production. The value below is a dev-only fallback.",
  "ApiKey": "YOUR_DEEPSEEK_API_KEY_HERE",
  "BaseUrl": "https://api.deepseek.com",
  "Model": "deepseek-chat"
}
```

- [ ] **Step 4: 编译验证**

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
taskkill //F //IM "ManWei.Api.exe" 2>nul || true
dotnet build
```

Expected: `Build succeeded`。

- [ ] **Step 5: 启动 API 并 curl 验证 (手动 smoke test)**

```bash
cd d:\AnimeEmotion\backend\ManWei.Api
dotnet run
```

(在另一终端) 获取一个有效 JWT (先登录):

```bash
# 登录拿 token
curl -s -X POST http://localhost:5150/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"YOUR_USER","password":"YOUR_PASS"}'
```

复制返回的 `token` 字段, 然后:

```bash
curl -N -X POST http://localhost:5150/api/pcaia/chat-stream \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"message":"你好,简单介绍一下你自己","history":[]}'
```

Expected: 看到逐行 NDJSON 输出, 至少有一行 `{"type":"delta","content":"..."}` 和最后一行 `{"type":"done"}`。

- [ ] **Step 6: Commit**

```bash
git add backend/ManWei.Api/Controllers/PcAiAgentController.cs \
        backend/ManWei.Api/Program.cs \
        backend/ManWei.Api/appsettings.json
git commit -m "feat(ai): add PcAiAgentController exposing NDJSON chat-stream endpoint"
```

---

## Task 6: 前端 — 类型扩展

**Files:**
- Modify: `frontend/pc-client/src/types/api.ts` (末尾追加)

- [ ] **Step 1: 追加类型定义**

打开 `frontend/pc-client/src/types/api.ts`, 在文件末尾添加:

```typescript
// PC AI 助手
export interface ChatMessage {
  id: string;                    // 本地生成的 uuid, 仅用于 React key
  role: 'user' | 'assistant' | 'tool';
  content: string;
  // 一轮 assistant 消息可能触发多个 tool_call, 用数组
  toolCalls?: ChatToolCallItem[];
  isStreaming?: boolean;         // assistant 消息正在流式接收
  isError?: boolean;             // 这一轮流式中断
}

export interface ChatToolCallItem {
  id: string;
  name: string;
  argsJson: string;
  resultJson: string;            // 收到 tool_result 后填充
}

export type ChatStreamEvent =
  | { type: 'delta'; content: string }
  | { type: 'tool_call'; toolCallId: string; toolName: string; toolArgsJson: string }
  | { type: 'tool_result'; toolCallId: string; toolResultJson: string }
  | { type: 'done' }
  | { type: 'error'; error: string };

export interface PcChatRequest {
  message: string;
  history: never[];  // 显式空数组, 标注 server 无状态
}
```

- [ ] **Step 2: 类型检查**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npx tsc -b --noEmit
```

Expected: 无错误。`history: never[]` 的目的是用 TypeScript 强制空数组——调用方传 `[]` 合法, 传其他值编译报错。

- [ ] **Step 3: Commit**

```bash
git add frontend/pc-client/src/types/api.ts
git commit -m "feat(ai): add ChatMessage/ChatToolCallItem/ChatStreamEvent/PcChatRequest types"
```

---

## Task 7: 前端 — Zustand store

**Files:**
- Create: `frontend/pc-client/src/stores/aiAssistantStore.ts`

- [ ] **Step 1: 创建 store**

新建 `frontend/pc-client/src/stores/aiAssistantStore.ts`:

```typescript
import { create } from 'zustand';
import type { ChatMessage, ChatToolCallItem } from '../types/api';

interface AiAssistantState {
  isOpen: boolean;
  messages: ChatMessage[];
  isStreaming: boolean;
  error: string | null;

  openDrawer: () => void;
  closeDrawer: () => void;
  toggleDrawer: () => void;

  appendUserMessage: (text: string) => string;
  startAssistantMessage: () => string;
  appendDelta: (id: string, content: string) => void;
  // 追加新的 tool_call (支持一轮多个)
  pushToolCall: (id: string, item: ChatToolCallItem) => void;
  // 用 tool_result 填充对应 toolCall 的 resultJson (按 id 匹配)
  patchToolCallResult: (id: string, toolCallId: string, resultJson: string) => void;
  finishAssistantMessage: (id: string) => void;
  setError: (err: string | null) => void;
  markAssistantError: (id: string) => void;
  reset: () => void;
}

export const useAiAssistantStore = create<AiAssistantState>((set) => ({
  isOpen: false,
  messages: [],
  isStreaming: false,
  error: null,

  openDrawer: () => set({ isOpen: true }),
  closeDrawer: () => set({ isOpen: false }),
  toggleDrawer: () => set(s => ({ isOpen: !s.isOpen })),

  appendUserMessage: (text) => {
    const id = crypto.randomUUID();
    set(s => ({
      messages: [...s.messages, {
        id, role: 'user', content: text,
      }],
      isStreaming: true,
      error: null,
    }));
    return id;
  },

  startAssistantMessage: () => {
    const id = crypto.randomUUID();
    set(s => ({
      messages: [...s.messages, {
        id, role: 'assistant', content: '', isStreaming: true,
      }],
    }));
    return id;
  },

  appendDelta: (id, content) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, content: m.content + content } : m
    ),
  })),

  pushToolCall: (id, item) => set(s => ({
    messages: s.messages.map(m => {
      if (m.id !== id) return m;
      const existing = m.toolCalls ?? [];
      // 同 id 重复时 (网络重发) 更新而非重复追加
      const idx = existing.findIndex(t => t.id === item.id);
      if (idx >= 0) {
        const next = existing.slice();
        next[idx] = { ...next[idx], ...item };
        return { ...m, toolCalls: next };
      }
      return { ...m, toolCalls: [...existing, item] };
    }),
  })),

  patchToolCallResult: (id, toolCallId, resultJson) => set(s => ({
    messages: s.messages.map(m => {
      if (m.id !== id || !m.toolCalls) return m;
      return {
        ...m,
        toolCalls: m.toolCalls.map(t =>
          t.id === toolCallId ? { ...t, resultJson } : t
        ),
      };
    }),
  })),

  finishAssistantMessage: (id) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, isStreaming: false } : m
    ),
    isStreaming: false,
  })),

  setError: (err) => set({ error: err }),

  markAssistantError: (id) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, isStreaming: false, isError: true } : m
    ),
    isStreaming: false,
  })),

  reset: () => set({
    messages: [],
    isStreaming: false,
    error: null,
  }),
}));
```

- [ ] **Step 2: 类型检查**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npx tsc -b --noEmit
```

Expected: 无错误。

- [ ] **Step 3: Commit**

```bash
git add frontend/pc-client/src/stores/aiAssistantStore.ts
git commit -m "feat(ai): add aiAssistantStore with multi-tool-call support"
```

---

## Task 8: 前端 — Chat service (NDJSON stream)

**Files:**
- Create: `frontend/pc-client/src/services/chat.ts`

- [ ] **Step 1: 创建 `chat.ts`**

新建 `frontend/pc-client/src/services/chat.ts`:

```typescript
import type { ChatStreamEvent } from '../types/api';

const API_BASE = '/api';

// 与 services/request.ts 拦截器保持一致: 直接从 localStorage 读 token,
// 避免 service 层依赖 Zustand store (防止循环依赖 + 字段名变动时静默失败)
const TOKEN_KEY = 'mw_token';

export interface StreamChatHandlers {
  onDelta: (content: string) => void;
  onToolCall: (e: Extract<ChatStreamEvent, { type: 'tool_call' }>) => void;
  onToolResult: (e: Extract<ChatStreamEvent, { type: 'tool_result' }>) => void;
  onDone: () => void;
  onError: (err: string) => void;
}

export async function streamChat(
  message: string,
  handlers: StreamChatHandlers,
  signal: AbortSignal,
): Promise<void> {
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    handlers.onError('未登录');
    return;
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}/pcaia/chat-stream`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({ message, history: [] }),
      signal,
    });
  } catch (err) {
    handlers.onError(err instanceof Error ? err.message : '网络错误');
    return;
  }

  if (!response.ok) {
    if (response.status === 401) {
      handlers.onError('登录已过期, 请重新登录');
      // 与 request.ts 401 行为一致: 跳转登录页
      localStorage.removeItem(TOKEN_KEY);
      window.location.href = '/login';
      return;
    }
    handlers.onError(`HTTP ${response.status}`);
    return;
  }

  if (!response.body) {
    handlers.onError('No response body');
    return;
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder('utf-8');
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // 按行切分 NDJSON
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';  // 最后一段可能不完整, 留到下次

      for (const line of lines) {
        if (!line.trim()) continue;
        let evt: ChatStreamEvent;
        try {
          evt = JSON.parse(line);
        } catch {
          continue;  // 忽略坏行
        }
        switch (evt.type) {
          case 'delta':
            handlers.onDelta(evt.content);
            break;
          case 'tool_call':
            handlers.onToolCall(evt);
            break;
          case 'tool_result':
            handlers.onToolResult(evt);
            break;
          case 'done':
            handlers.onDone();
            return;
          case 'error':
            handlers.onError(evt.error);
            return;
        }
      }
    }
    // 流自然结束但没有收到 done → 也算 done
    handlers.onDone();
  } catch (err) {
    if ((err as Error).name === 'AbortError') {
      handlers.onError('已取消');
    } else {
      handlers.onError(err instanceof Error ? err.message : '流读取错误');
    }
  }
}
```

- [ ] **Step 2: 类型检查**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npx tsc -b --noEmit
```

Expected: 无错误。

- [ ] **Step 3: Commit**

```bash
git add frontend/pc-client/src/services/chat.ts
git commit -m "feat(ai): add NDJSON streamChat service with AbortController support"
```

---

## Task 9: 前端 — `AiAssistantDrawer` 组件 (CSS 部分)

**Files:**
- Create: `frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css`

- [ ] **Step 1: 创建 CSS module**

新建 `frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css`:

```css
.body {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--color-bg);
}

.drawerHeader {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}

.statusDot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #d1d5db;
  margin-left: auto;
}

.statusDot[data-streaming="true"] {
  background: var(--color-accent);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.messageList {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.emptyWrap {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.emptyText {
  color: var(--color-text-secondary);
  text-align: center;
  line-height: 1.6;
  font-size: 14px;
}

.bubble {
  margin-bottom: 12px;
  max-width: 85%;
  word-wrap: break-word;
}

.bubble.user {
  margin-left: auto;
  background: var(--color-accent);
  color: white;
  padding: 10px 14px;
  border-radius: 12px 12px 4px 12px;
}

.bubble.assistant {
  background: #ffffff;
  color: var(--color-text);
  padding: 10px 14px;
  border-radius: 12px 12px 12px 4px;
  border: 1px solid var(--color-border);
}

.role {
  font-size: 11px;
  color: var(--color-text-secondary);
  margin-bottom: 4px;
  font-weight: 600;
}

.bubbleContent {
  white-space: pre-wrap;
}

.cursor {
  display: inline-block;
  animation: blink 1s step-start infinite;
  color: var(--color-accent);
  font-weight: 600;
}

@keyframes blink {
  0%, 50% { opacity: 1; }
  50.01%, 100% { opacity: 0; }
}

.toolCall {
  margin-top: 8px;
  font-size: 12px;
  color: var(--color-text-secondary);
  background: var(--color-bg);
  padding: 8px;
  border-radius: 4px;
  border: 1px solid var(--color-border);
}

.toolCall summary {
  cursor: pointer;
  user-select: none;
}

.toolCall pre {
  margin: 8px 0 0 0;
  font-size: 11px;
  white-space: pre-wrap;
  max-height: 200px;
  overflow-y: auto;
}

.messageError {
  margin-top: 8px;
  font-size: 12px;
  color: #b91c1c;
}

.errorBanner {
  padding: 8px 16px;
  background: #fef2f2;
  color: #b91c1c;
  font-size: 13px;
  border-top: 1px solid #fee2e2;
}

.inputBar {
  display: flex;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  background: #ffffff;
  align-items: flex-end;
}

.inputBar textarea {
  flex: 1;
}

.aiButton {
  font-size: 14px;
}
```

- [ ] **Step 2: 提交 CSS**

```bash
git add frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css
git commit -m "feat(ai): add AiAssistantDrawer styles with design tokens"
```

---

## Task 10: 前端 — `AiAssistantDrawer` 组件 (TSX 部分)

**Files:**
- Create: `frontend/pc-client/src/components/AiAssistantDrawer/index.tsx`

- [ ] **Step 1: 创建 `index.tsx`**

新建 `frontend/pc-client/src/components/AiAssistantDrawer/index.tsx`:

```tsx
import { useEffect, useRef, useState } from 'react';
import { Drawer, Input, Button, Empty } from 'antd';
import { RobotOutlined, CloseOutlined, SendOutlined } from '@ant-design/icons';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { streamChat } from '../../services/chat';
import type { ChatMessage } from '../../types/api';
import styles from './AiAssistantDrawer.module.css';

export function AiAssistantDrawer() {
  const isOpen = useAiAssistantStore(s => s.isOpen);
  const closeDrawer = useAiAssistantStore(s => s.closeDrawer);
  const messages = useAiAssistantStore(s => s.messages);
  const isStreaming = useAiAssistantStore(s => s.isStreaming);
  const error = useAiAssistantStore(s => s.error);
  const setError = useAiAssistantStore(s => s.setError);
  const appendUserMessage = useAiAssistantStore(s => s.appendUserMessage);
  const startAssistantMessage = useAiAssistantStore(s => s.startAssistantMessage);
  const appendDelta = useAiAssistantStore(s => s.appendDelta);
  const pushToolCall = useAiAssistantStore(s => s.pushToolCall);
  const patchToolCallResult = useAiAssistantStore(s => s.patchToolCallResult);
  const finishAssistantMessage = useAiAssistantStore(s => s.finishAssistantMessage);
  const markAssistantError = useAiAssistantStore(s => s.markAssistantError);
  const reset = useAiAssistantStore(s => s.reset);

  const [input, setInput] = useState('');
  const abortRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // 关闭时清空消息 (用户已确认不持久化)
  useEffect(() => {
    if (!isOpen) {
      abortRef.current?.abort();
      reset();
      setInput('');
    }
  }, [isOpen, reset]);

  // 自动滚动到底部
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  async function handleSend() {
    const text = input.trim();
    if (!text || isStreaming) return;
    setInput('');
    setError(null);

    appendUserMessage(text);
    const assistantId = startAssistantMessage();
    const ctrl = new AbortController();
    abortRef.current = ctrl;

    await streamChat(
      text,
      {
        onDelta: (content) => appendDelta(assistantId, content),
        onToolCall: (e) => {
          pushToolCall(assistantId, {
            id: e.toolCallId,
            name: e.toolName,
            argsJson: e.toolArgsJson,
            resultJson: '',
          });
        },
        onToolResult: (e) => {
          patchToolCallResult(assistantId, e.toolCallId, e.toolResultJson);
        },
        onDone: () => finishAssistantMessage(assistantId),
        onError: (err) => {
          markAssistantError(assistantId);
          setError(err);
        },
      },
      ctrl.signal,
    );
  }

  return (
    <Drawer
      title={
        <div className={styles.drawerHeader}>
          <RobotOutlined />
          <span>AI 助手</span>
          <span className={styles.statusDot} data-streaming={isStreaming} />
        </div>
      }
      placement="right"
      width={480}
      open={isOpen}
      onClose={closeDrawer}
      closeIcon={<CloseOutlined />}
      styles={{ body: { padding: 0 } }}
    >
      <div className={styles.body}>
        <div className={styles.messageList}>
          {messages.length === 0 ? (
            <div className={styles.emptyWrap}>
              <Empty
                description={
                  <div className={styles.emptyText}>
                    问我关于你追番的任何问题。<br />
                    例如: "我最近看了什么番" / "我的平均评分是多少"
                  </div>
                }
              />
            </div>
          ) : (
            messages.map(m => <MessageBubble key={m.id} message={m} />)
          )}
          <div ref={messagesEndRef} />
        </div>

        {error && (
          <div className={styles.errorBanner}>
            ⚠️ {error}
          </div>
        )}

        <div className={styles.inputBar}>
          <Input.TextArea
            value={input}
            onChange={e => setInput(e.target.value)}
            onPressEnter={e => {
              if (!e.shiftKey) {
                e.preventDefault();
                handleSend();
              }
            }}
            placeholder="输入消息, Enter 发送, Shift+Enter 换行"
            autoSize={{ minRows: 1, maxRows: 4 }}
            disabled={isStreaming}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={handleSend}
            disabled={!input.trim() || isStreaming}
            loading={isStreaming}
          >
            发送
          </Button>
        </div>
      </div>
    </Drawer>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === 'user';
  return (
    <div className={`${styles.bubble} ${isUser ? styles.user : styles.assistant}`}>
      {!isUser && <div className={styles.role}>AI</div>}
      <div className={styles.bubbleContent}>
        {message.content}
        {message.isStreaming && <span className={styles.cursor}>▍</span>}
        {message.toolCalls?.map(tc => (
          <details key={tc.id} className={styles.toolCall}>
            <summary>🔧 调用了 {tc.name}</summary>
            <pre>{(() => {
              try { return JSON.stringify(JSON.parse(tc.resultJson || '{}'), null, 2); }
              catch { return tc.resultJson || '{}'; }
            })()}</pre>
          </details>
        ))}
        {message.isError && (
          <div className={styles.messageError}>⚠️ 连接中断, 以上内容可能不完整</div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: 类型检查**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npx tsc -b --noEmit
```

Expected: 无错误。

- [ ] **Step 3: Commit**

```bash
git add frontend/pc-client/src/components/AiAssistantDrawer/index.tsx
git commit -m "feat(ai): add AiAssistantDrawer with message bubbles and tool_call details"
```

---

## Task 11: 前端 — `AppShell` 集成

**Files:**
- Modify: `frontend/pc-client/src/components/AppShell/index.tsx`

- [ ] **Step 1: 读取现有 `AppShell/index.tsx`**

```bash
cat d:\AnimeEmotion\frontend\pc-client\src\components\AppShell\index.tsx
```

(查看顶部 imports, 找到用户区 JSX 位置和 `<Outlet />` 位置)

- [ ] **Step 2: 添加 imports**

在文件顶部, 其他 import 旁边添加:

```tsx
import { RobotOutlined } from '@ant-design/icons';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { AiAssistantDrawer } from '../AiAssistantDrawer';
```

- [ ] **Step 3: 在组件内取出 store action**

在 `AppShell` 函数组件内 (其他 `useAuthStore` 调用旁边) 添加:

```tsx
const openDrawer = useAiAssistantStore(s => s.openDrawer);
```

- [ ] **Step 4: 在用户区按钮之前插入 "AI 助手" 按钮**

找到用户区 (通常在 header 内的右侧, 含 "退出" 按钮的 flex 容器) 的 JSX, 在最前面插入:

```tsx
<Button
  type="text"
  icon={<RobotOutlined />}
  onClick={openDrawer}
  className={styles.aiButton}
>
  AI 助手
</Button>
```

> 如果现有 `AppShell` 的用户区用的是 div 而非 antd 组件, 确认 `Button` 是从 antd 导入的 (顶部 import)。如果没有 import, 添加 `import { Button } from 'antd';` 到文件顶部。

- [ ] **Step 5: 在 `<Outlet />` 之后挂载 Drawer**

找到组件 return 末尾的 JSX (通常是 `<>...<Outlet /></>` 或 `<div>...<Outlet /></div>`), 在同级位置添加:

```tsx
<AiAssistantDrawer />
```

- [ ] **Step 6: 类型检查 + 构建**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npx tsc -b --noEmit
npm run build
```

Expected: `tsc` 无错误, `vite build` 输出 `dist/` 文件夹。

- [ ] **Step 7: 手动 smoke test (浏览器)**

```bash
cd d:\AnimeEmotion\frontend\pc-client
npm run dev
```

打开 `http://localhost:5173`, 登录, 进入任一页面, 验证:
1. 右上角看到 "AI 助手" 按钮 (RobotOutlined icon + 文字)
2. 点击后右侧抽屉滑出, 标题显示 "AI 助手" 旁边有灰色圆点
3. 看到 Empty 状态: "问我关于你追番的任何问题..."
4. 输入 "你好" 按 Enter, 应该看到流式打字机效果
5. 输入 "我最近看了什么番", 应该看到流式过程中出现 "🔧 调用了 query_my_favorites" 折叠面板, 展开后看到 JSON 结果
6. 关闭抽屉, 重新打开 → messages 清空 (empty 状态)
7. DevTools Network → 选中 `/pcaia/chat-stream` → 应该看到 `Transfer-Encoding: chunked` 或 `Content-Type: application/x-ndjson`

- [ ] **Step 8: Commit**

```bash
git add frontend/pc-client/src/components/AppShell/index.tsx
git commit -m "feat(ai): mount AiAssistantDrawer in AppShell globally"
```

---

## Task 12: 端到端验证 + 收尾

**Files:** (无代码改动, 仅验证)

- [ ] **Step 1: 后端冒烟**

后端 `dotnet run`, 确认:
- 进程启动无异常
- 启动日志看到 DeepSeek 客户端初始化

- [ ] **Step 2: 工具调用冒烟**

前端登录, 打开 AI 助手, 依次问:

| # | 输入 | 预期 |
|---|---|---|
| 1 | "我最近看了什么番" | 看到 tool_call 触发 query_my_favorites, AI 列出最近收藏 |
| 2 | "我的平均评分呢" | 看到 tool_call 触发 query_user_stats, AI 给出平均评分 |
| 3 | "帮我查 animeId=1 的情绪曲线" | 若收藏: 显示曲线数据; 若未收藏: 收到 `not_favorited`, AI 优雅降级 |
| 4 | "帮我搜一下孤独摇滚" | 触发 search_anime 桩, AI 收到 `not_implemented`, 降级回答 |

每条问题后等流式完成, 再发下一条 (前端的 `isStreaming` 状态会阻止重叠发送)。

- [ ] **Step 3: 异常路径冒烟**

| # | 场景 | 操作 | 预期 |
|---|---|---|---|
| 5 | 401 | DevTools → Application → Local Storage → 删除 `mw_token` → 发送消息 | 跳转 `/login` |
| 6 | 网络中断 | DevTools Network → Offline → 发送消息 | 看到 ⚠️ banner, assistant 消息标 isError, 已收到的 delta 保留 |
| 7 | 中途关闭 | 流式过程中点 X 关闭抽屉 | UI 消失, 重新打开后 messages 清空 (用户已确认) |

- [ ] **Step 4: 回归验证 — 现有 WX/Admin agent 不受影响**

- 后端 WX agent 端到端 (需在小程序端测, 或用 Postman 调 `/api/wxaiagent/chat`)
- Admin agent 端到端 (`/api/aiagent/chat`)
- 两者都应继续工作 (无 tool_call 时返回非流式 JSON)

- [ ] **Step 5: 收尾**

- 确认 `git log` 中本计划的 8 个 commit 全部存在
- 在 `docs/TECH_DEBT.md` 末尾追加"已修复"条目, 引用本计划的 commit hash
- 不需要更新 [docs/COLLABORATION.md](docs/COLLABORATION.md) (新接口是新 path `/pcaia/...`, 不影响其他端点约定)

---

## Self-Review

### Spec coverage

| Spec 章节 | 任务 |
|---|---|
| Backend 1 — StreamChatAsync | Task 2 |
| Backend 2 — API key 修复 | Task 1 |
| Backend 3 — PcAiAgentService | Task 4 |
| Backend 4 — PcAiTools | Task 3 |
| Backend 5 — PcAiAgentController | Task 5 |
| Backend 6 — DI 注册 | Task 5 Step 2 |
| Frontend 1 — 类型扩展 | Task 6 |
| Frontend 2 — Zustand store | Task 7 |
| Frontend 3 — chat service | Task 8 |
| Frontend 4 — Drawer 组件 | Tasks 9 + 10 |
| Frontend 5 — AppShell 集成 | Task 11 |
| Risk 5 — assistant message 匿名对象 | Task 2 注释 + 实施 |
| Risk 7 — UI/Server 分层 | Task 6 (`history: never[]`) + Task 8 (chat.ts 注释) |
| E2E 验证 | Task 12 |

### Type consistency

- `ChatMessage.toolCalls: ChatToolCallItem[]` 在 Task 6 定义, Task 7 (store), Task 10 (MessageBubble) 使用 — 一致
- `ChatToolCallItem` 在 Task 6 export, Task 7 import — 一致
- `PcAiAgentController` 用 `_service.AgentSystemPrompt` (Task 5), 与 Task 4 的 `protected override string AgentSystemPrompt => SystemPrompt` 一致
- `streamChat(message, handlers, signal)` 在 Task 8 定义, Task 10 调用 — 签名一致

### Placeholders / TODOs

无 "TBD" / "TODO" / "实现后补" 标记。所有步骤含完整代码或具体命令。

### Potential gotchas flagged inline

- Task 2 Step 2 含笔误修正说明 (避免工程师照抄 `public abstract partial class` 错误代码)
- Task 4 Step 2 提示核对 `Models/` 字段名 (CLAUDE.md 强制 Models 目录)
- Task 5 Step 5 提供完整 curl 命令 (token 替换为 `<TOKEN>` 即可执行)
- Task 11 Step 1 提醒工程师先 cat 文件再编辑 (避免猜位置)
- Task 12 Step 4 提醒做 WX/Admin 回归 (避免破坏现有功能)
