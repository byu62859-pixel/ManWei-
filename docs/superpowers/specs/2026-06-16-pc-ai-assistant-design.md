# PC 用户端 AI 助手 — 设计文档

**Date:** 2026-06-16
**Status:** Draft (待用户复核)
**Author:** brainstorming session

## Context

ManWei 项目已有一个面向微信小程序的 AI 助手（`WxAiAgentController` + `WxAiAgentService`），底层接 DeepSeek，支持 6 个面向用户的 tool 调用。但 PC 用户端目前没有 AI 助手能力，而 PC 端恰恰是用户进行深度数据查询（追番统计、情绪曲线、跨番对比）的高频场景。

本次目标：为 PC 用户端新增一个**独立**的 AI 助手，与现有 WX agent 解耦，差异化在三点：

1. **独立的 system prompt** — 偏向"专业数据助手 / 推荐顾问"语气，支持更长上下文与多步推理
2. **流式输出 (NDJSON)** — DeepSeek 流式响应 + tool_call 流式聚合 + 工具结果再注入上下文继续流式
3. **5 个 tool（首版 3 个 + 2 个桩）** — 全部面向"用户自己的数据"

预期成果：用户在任何已登录页面，都能从右上角唤起一个右侧抽屉助手，发送问题后看到流式打字机式的回答，助手能在对话中自动调用工具查询用户数据并基于真实数据回答。

## Goals

1. 新增 `PcAiAgentController` 与 `PcAiAgentService`，独立于 WX/Admin agent
2. 后端流式输出 NDJSON，前端 fetch + ReadableStream 解析
3. 支持 tool 调用，且 tool_call 在流式 chunk 中分片下发时正确聚合
4. assistant message 仍按 CLAUDE.md 红线保持为 raw JsonElement（不重建成 C# 类）
5. 顶部导航增加"AI 助手"入口，右侧抽屉呈现对话
6. 顺便修复 DeepSeek API key 的加载方式（env var 优先）

## Non-Goals

- **不做对话持久化** — 关闭 Drawer 或刷新页面即清空（用户已确认）
- **不做流式中断后的断点续传** — 中断则该轮丢弃已收到的部分 delta 仅用于显示，**不重发**
- **首版 5 个 tool 不全部实现** — `query_my_favorites` / `query_user_stats` / `query_anime_emotion_curve` 3 个完整实现；`search_anime` / `query_global_emotion_tags` 注册为桩（返回 `{"error":"not_implemented"}`）
- **不复用 `CallDeepSeekAsync`** — 现有 `stream: false` 调用路径不修改，避免影响 WX/Admin
- **不修改 WX/Admin agent 的行为**
- **不做多用户协同 / 共享对话**

## Architecture Overview

```
┌────────────────────────────────────┐
│  PC Client (React 19)              │
│  ┌──────────────────────────────┐  │
│  │ AppShell                     │  │
│  │  ├─ <Button> AI 助手 ───────┼──┼──┐ openDrawer()
│  │  ├─ <Outlet /> (routes)      │  │  │
│  │  └─ <AiAssistantDrawer /> ───┼──┘  │
│  │       │ Zustand store        │     │
│  │       │ messages[],          │     │
│  │       │ isStreaming,         │     │
│  │       │ error                │     │
│  │       │                      │     │
│  │       └─ fetch POST          │     │
│  │            /api/pcaia/       │     │
│  │            chat-stream       │     │
│  └──────────────────────────────┘     │
└─────────────────┬─────────────────────┘
                  │ application/x-ndjson
                  │ Authorization: Bearer <jwt>
                  ▼
┌────────────────────────────────────┐
│  Backend (ASP.NET Core 8)          │
│  ┌──────────────────────────────┐  │
│  │ PcAiAgentController          │  │
│  │   POST /api/pcaia/chat-stream│  │
│  │   [Authorize]                │  │
│  └──────────────┬───────────────┘  │
│                 ▼                  │
│  ┌──────────────────────────────┐  │
│  │ PcAiAgentService             │  │
│  │  : BaseAiAgentService        │  │
│  │                              │  │
│  │  - GetSystemPrompt()         │  │
│  │  - GetTools()                │  │ → 5 tool 注册
│  │  - ExecuteToolAsync()        │  │ → 3 实现 + 2 桩
│  └──────────────┬───────────────┘  │
│                 ▼                  │
│  ┌──────────────────────────────┐  │
│  │ BaseAiAgentService           │  │
│  │  + StreamChatAsync()         │  │ → 新增
│  │    IAsyncEnumerable<...>     │  │
│  │    流式 + tool_call 聚合     │  │
│  │    + 工具结果再注入          │  │
│  └──────────────────────────────┘  │
└────────────────────────────────────┘
```

## Data Flow

### 单轮对话（无 tool）

```
1. User sends "我最近看了什么番"
2. Frontend: store.appendUserMessage(text)
3. Frontend: POST /api/pcaia/chat-stream { message }
   headers: Authorization: Bearer <jwt>
   body: { message: string, history: [] }
4. Backend PcAiAgentController.ChatStream:
   - extracts userId from JWT
   - calls _pcAiAgentService.StreamChatAsync(userId, message, ct)
   - returns FileStreamResult / writes NDJSON to Response.Body
5. PcAiAgentService.StreamChatAsync:
   - builds messages = [system, user]
   - calls BaseAiAgentService.StreamChatAsync(messages, ct)
6. BaseAiAgentService.StreamChatAsync:
   - sends POST to DeepSeek /chat/completions with stream=true
   - reads SSE chunks, parses JSON per chunk
   - emits NDJSON events:
       { type: "delta", content: "..." }      ← 每次 content_delta
       { type: "done" }                         ← 收到 finish_reason=stop
   - if tool_calls present: aggregate across chunks, emit
       { type: "tool_call", id, name, args }, then execute tool, emit
       { type: "tool_result", id, content }, then recursively call DeepSeek for second stream, emit deltas, until done
7. Frontend streamChat():
   - reader.read() loop
   - per chunk: switch (event.type) { delta → append to last assistant message; tool_call/tool_result → append as separate UI bubble; done → finish; error → setError }
```

### 关键分层

| 层 | 范围 | 状态 |
|---|---|---|
| **UI 消息列表** (Zustand `messages[]`) | 前端组件状态，纯展示 | 关闭 Drawer 即清空 |
| **每轮请求** | 后端只接收 `message`（单条 user），不接收 history | 无状态 |
| **DeepSeek 调用上下文** | 后端内部流式构造 `messages: [system, user]`，必要时加入 tool message | 一次性，不跨请求持久化 |

**重要**：前端 `messages[]` 是**展示用 UI 状态**，不是多轮上下文。后端每次只看到当前一条 user message + system prompt。这个分层必须在代码注释里写明，避免后续维护者误解。

## Backend Changes

### 1. BaseAiAgentService 新增流式方法

**不修改现有 `CallDeepSeekAsync`**，新增一个独立的流式路径：

```csharp
// BaseAiAgentService.cs - 新增
public async IAsyncEnumerable<StreamEvent> StreamChatAsync(
    IEnumerable<object> messages,
    [EnumeratorCancellation] CancellationToken ct)
{
    var client = _httpClientFactory.CreateClient("DeepSeek");
    var apiKey = ResolveApiKey(); // ← 见 API Key 修复
    var model = _config["DeepSeek:Model"] ?? "deepseek-chat";

    var tools = GetTools().ToList();
    var messageList = messages.ToList();

    // 递归深度上限保护（防 tool 调用无限循环）
    var maxRounds = 8;
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
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        // 流式聚合状态
        var contentBuilder = new StringBuilder();
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

        // 4) 收敛：构造 assistant message(JsonElement 风格) 并 append 到 messageList
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

        // 6) 有 tool_call → 执行工具，注入 tool message，再发下一轮请求
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

            // tool message 也按 anonymous object 注入，保持 tool_call_id 字段名
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

private class ToolCallAccumulator
{
    public string Id = "";
    public string? Type;
    public string Name = "";
    public StringBuilder ArgumentsJson = new();
}

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

**关键设计点**：
- assistant message 用匿名对象构造，**不引入 `ChatMessage` C# 类**——延续 CLAUDE.md 红线
- tool message 也用匿名对象，`tool_call_id` 字段名直接落到 JSON
- 流式 tool_call 按 `index` 聚合（DeepSeek 的 SSE 协议约定）
- 递归用 `for` 循环 + 深度上限，避免工具相互调用导致栈溢出
- `HttpCompletionOption.ResponseHeadersRead` 避免 HttpClient 缓冲整个流

### 2. API Key 加载修复

**文件**: `BaseAiAgentService.cs`

**当前代码**:
```csharp
var apiKey = _config["DeepSeek:ApiKey"];
```

**修改后**:
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

**`appsettings.json` 更新** (仅注释，不删 key，避免破坏 dev):
```json
"DeepSeek": {
  "_comment": "API key should be set via env var DEEPSEEK__APIKEY in production. The value below is a dev-only fallback.",
  "ApiKey": "YOUR_DEEPSEEK_API_KEY_HERE",
  "BaseUrl": "https://api.deepseek.com",
  "Model": "deepseek-chat"
}
```

`_comment` 字段不影响 JSON 反序列化（DeepSeek 客户端用 `IConfiguration["DeepSeek:ApiKey"]` 取值时拿到 string，注释字段对 ASP.NET Core 配置系统无影响）。

### 3. PcAiAgentService 子类

**文件**: `backend/ManWei.Api/Services/PcAiAgentService.cs` (新建)

```csharp
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
    // 同时 override BaseAiAgentService.AgentSystemPrompt 抽象方法
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

**已知 latent bug 修复**：现有的 `WxAiAgentService._userId` 是实例字段（Scoped 服务复用），本次 `PcAiAgentService` 同样模式，但 `SetUserId` 在 controller 每个请求首部显式调用，避免继承上一个请求的用户。

### 4. PcAiTools 工具注册表

**文件**: `backend/ManWei.Api/Services/PcAiTools.cs` (新建)

#### AiTool 类型定义

`AiTool` 类已存在于 `backend/ManWei.Api/Services/AiTools.cs:3`：

```csharp
public class AiTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}
```

**直接复用现有定义**，无需新增。所有 agent（Admin / WX / PC）共享此类型。

#### 工具列表

```csharp
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
            Description = "查询当前用户的追番统计：" +
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

### 5. PcAiAgentController

**文件**: `backend/ManWei.Api/Controllers/PcAiAgentController.cs` (新建)

```csharp
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

        // 提取 userId (按 CLAUDE.md 红线)
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
            _logger.LogInformation("PC AI chat stream cancelled by client, userId={UserId}", userId);
            // 不再尝试写 body，连接已断开
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
    // History 字段保留但不传给后端（前端 UI 多轮但 server 无状态）
    public List<object>? History { get; set; }
}
```

### 6. DI 注册

**文件**: `Program.cs`

```csharp
// 在现有 AddScoped<WxAiAgentService>() 附近加一行
builder.Services.AddScoped<PcAiAgentService>();
```

## Frontend Changes

### 1. 类型扩展

**文件**: `frontend/pc-client/src/types/api.ts`

新增：

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

### 2. Zustand store

**文件**: `frontend/pc-client/src/stores/aiAssistantStore.ts` (新建)

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

  appendUserMessage: (text: string) => string;  // 返回 message id
  startAssistantMessage: () => string;          // 创建空 assistant 消息
  appendDelta: (id: string, content: string) => void;
  // 追加新的 tool_call (支持一轮多个)
  pushToolCall: (id: string, item: ChatToolCallItem) => void;
  // 用 tool_result 填充对应 toolCall 的 resultJson (按 id 匹配)
  patchToolCallResult: (id: string, toolCallId: string, resultJson: string) => void;
  finishAssistantMessage: (id: string) => void;
  setError: (err: string | null) => void;
  markAssistantError: (id: string) => void;
  reset: () => void;  // 清空 messages + error
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

### 3. Chat service (NDJSON stream)

**文件**: `frontend/pc-client/src/services/chat.ts` (新建)

```typescript
import type { ChatStreamEvent } from '../types/api';

const API_BASE = '/api';

// 与 services/request.ts 拦截器保持一致：直接从 localStorage 读 token，
// 避免 service 层依赖 Zustand store（防止循环依赖 + 字段名变动时静默失败）
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
    // 401 等错误: body 可能是 NDJSON 第一行 {type:"error",...}
    if (response.status === 401) {
      handlers.onError('登录已过期, 请重新登录');
      // 与 request.ts 401 行为一致: 跳转登录页
      localStorage.removeItem('mw_token');
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

### 4. AiAssistantDrawer 组件

**文件**: `frontend/pc-client/src/components/AiAssistantDrawer/index.tsx` (新建)
**文件**: `frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css` (新建)

```tsx
// index.tsx
import { useEffect, useRef, useState } from 'react';
import { Drawer, Input, Button, Empty } from 'antd';
import { RobotOutlined, CloseOutlined, SendOutlined } from '@ant-design/icons';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { streamChat } from '../../services/chat';
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

  // 关闭时清空消息(用户已确认不持久化)
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

**CSS module** 关键样式（节选）：

```css
.body {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--color-bg);
}

.messageList {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.bubble {
  margin-bottom: 12px;
  max-width: 85%;
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

.toolCall {
  margin-top: 8px;
  font-size: 12px;
  color: var(--color-text-secondary);
  background: var(--color-bg);
  padding: 8px;
  border-radius: 4px;
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
```

### 5. AppShell 集成

**文件**: `frontend/pc-client/src/components/AppShell/index.tsx`

在用户区（line 58-69 附近）增加按钮：

```tsx
import { RobotOutlined } from '@ant-design/icons';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { AiAssistantDrawer } from '../AiAssistantDrawer';

// 在用户区按钮之前
<Button
  type="text"
  icon={<RobotOutlined />}
  onClick={openDrawer}
  className={styles.aiButton}
>
  AI 助手
</Button>

// 在 return 末尾, 与 <Outlet /> 同级
<AiAssistantDrawer />
```

`openDrawer` 从 store 取。

## Risks & Mitigations

### 风险 1：流式 tool_call 聚合复杂度（最高风险）

**描述**：DeepSeek 流式响应中 `tool_calls` 字段是分片下发的，每次 chunk 可能只携带 `id`/`name`/`arguments` 的一部分；需要在后端按 `index` 聚合完整后才能触发 `ExecuteToolAsync`。

**缓解**：
- `BaseAiAgentService.StreamChatAsync` 内部用 `Dictionary<int, ToolCallAccumulator>` 按 index 聚合（见 §后端 1）
- assistant message 仍按 CLAUDE.md 风格用匿名对象构造，**不引入 C# ChatMessage 类**
- 聚合失败 / arguments 解析失败时 `args = new()`，tool 仍可调用但拿不到参数
- 写测试：构造一个 mock DeepSeek 响应（含 3 个分片 tool_call），验证聚合后能拿到完整 `{id, name, arguments}`

### 风险 2：5 个 tool schema 维护 vs 实现质量

**描述**：5 个 tool 的 JSON Schema 与 description 写错会导致 DeepSeek 调用混乱或失败。

**缓解**：用户已确认首版只实现 3 个，剩 2 个注册为桩（dispatch 时返回 `{"error":"not_implemented"}`）。这样：
- 工具注册表完整（5 个），system prompt 可放心提"我拥有 5 个工具"
- 桩的 description 也清晰标明"本版未实现"
- v2 实现时只需替换桩方法 + 更新 description

### 风险 3：Drawer + SSE 鉴权

**描述**：`EventSource` 不支持自定义 header，JWT 鉴权困难。

**缓解**：已选 NDJSON over `fetch` + `ReadableStream` 方案（A2）：
- JWT 通过 `Authorization: Bearer` header 正常走，不污染 URL
- 前端 `AbortController` 支持中断
- 后端用 `Response.WriteAsync(json + "\n")` 逐行输出，无需特殊 middleware

### 风险 4：流式中断体验

**描述**：网络抖动、用户关闭 Drawer、后端超时——都可能让流断在中间。

**缓解**：
- 后端 catch `OperationCanceledException` 时**不再尝试写 body**（连接已断开）
- 前端 `catch` 区分 AbortError 与其他错误
- UI 不清空已收到的 delta，仅在对应 assistant 消息上标记 `isError: true` + 在底部 banner 显示错误
- 用户已关闭 Drawer 时 `abortRef.current?.abort()` 主动中断

### 风险 5：assistant message 重建陷阱（CLAUDE.md 红线）

**描述**：历史踩过 — 把 DeepSeek 返回的 assistant message 重建成 C# 类会丢 `tool_call_id`，下次请求 400。

**缓解**：
- `BaseAiAgentService.StreamChatAsync` 中 assistant message 用 `new { role = "assistant", content = ..., tool_calls = ... }` 匿名对象
- tool message 用 `new { role = "tool", tool_call_id = ..., content = ... }`
- 代码注释明确写"assistant message 保留 raw JSON 风格，不引入 ChatMessage 类"
- 与现有 WX agent 行为完全一致

### 风险 6：API Key 改动可能影响 dev

**描述**：把 `_config["DeepSeek:ApiKey"]` 改为"env var 优先 + config 兜底"，dev 环境若没设 env var 也能跑（兜底生效）。

**缓解**：
- `ResolveApiKey()` 逻辑：`env var → config → throw`
- dev 当前 `appsettings.json` 有 key，兜底能跑
- `appsettings.json` 加 `_comment` 字段说明 prod 应设 env var
- 不删除现有 key，避免 dev 中断

### 风险 7：UI 消息 vs Server 上下文分层误解

**描述**：未来维护者看到 `messages[]` 数组可能误以为是多轮上下文，去改后端支持 history。

**缓解**：
- 设计文档此节明确分层
- `PcChatRequestDto.History` 字段保留但永远传空数组 `[]`
- `types/api.ts` 中 `PcChatRequest.history: never[]` 用 TypeScript 标注强制空
- `aiAssistantStore.ts` 顶部加注释："messages 仅供 UI 展示; 后端每次只收到单条 user message"

## Data Flow (Final Diagram)

```
[User types message] → [Drawer Send] → [aiAssistantStore.appendUserMessage]
                                              ↓
                          [startAssistantMessage (empty, isStreaming=true)]
                                              ↓
                          [streamChat(message, handlers, signal)]
                                              ↓
                              fetch POST /api/pcaia/chat-stream
                              Bearer <jwt>
                                              ↓
                          [PcAiAgentController.ChatStream]
                                              ↓
                              [extract userId via CLAUDE.md pattern]
                                              ↓
                              Response.ContentType = application/x-ndjson
                                              ↓
                          [PcAiAgentService.StreamChatAsync]
                                              ↓
                              [BaseAiAgentService.StreamChatAsync]
                                              ↓
                              POST https://api.deepseek.com/chat/completions
                              stream=true, messages=[system,user], tools=[5]
                                              ↓
                          ┌─ Read SSE chunks line by line
                          │
                          ├─ content delta → emit NDJSON {type:"delta"}
                          │                        ↓
                          │              onDelta → appendDelta(assistantId, content)
                          │
                          ├─ tool_call delta (partial) → aggregate by index
                          │                                          ↓
                          │                              accumulate id/name/args
                          │
                          ├─ finish_reason + complete tool_call
                          │                          ↓
                          │              emit NDJSON {type:"tool_call"}
                          │                        ↓
                          │              onToolCall → store.toolCall (id+name+args)
                          │                          ↓
                          │              ExecuteToolAsync(name, args)
                          │                          ↓
                          │              emit NDJSON {type:"tool_result"}
                          │                        ↓
                          │              onToolResult → store.toolCall.resultJson
                          │                          ↓
                          │              append tool message to messages list
                          │                          ↓
                          │              [next round] POST again with stream=true
                          │                              (assistant + tool messages appended)
                          │
                          └─ finish_reason=stop (no tool_call)
                                              ↓
                              emit NDJSON {type:"done"}
                                              ↓
                              onDone → finishAssistantMessage
                                              ↓
                              [UI: assistant message marked isStreaming=false]
```

## Testing Strategy

### 后端

1. **编译验证**：`dotnet build` 通过
2. **tool 聚合单测**：构造 mock DeepSeek 流响应（3 个分片 tool_call），断言 `ExecuteToolAsync` 被以完整参数调用
3. **手动 curl 测试**：
   ```bash
   curl -X POST http://localhost:5150/api/pcaia/chat-stream \
     -H "Authorization: Bearer <jwt>" \
     -H "Content-Type: application/json" \
     -d '{"message":"我最近看了什么番","history":[]}' \
     --no-buffer
   ```
   预期：逐行输出 NDJSON
4. **API key 验证**：dev 环境不设 env var 也能跑（config 兜底）；设了 `DEEPSEEK__APIKEY` 后优先用 env var

### 前端

1. **构建**：`npm run build` 通过
2. **Drawer 打开/关闭**：登录后任一页 → 点"AI 助手" → 抽屉从右滑入 → 点 X → 抽屉关闭 + messages 清空
3. **基础对话**：输入"你好" → 流式打出回复 → 输入框恢复可用
4. **tool 调用可视化**：输入"我最近看了什么番" → 流式过程中能看到 `🔧 调用了 query_my_favorites` 折叠面板 → 展开看到 JSON 结果 → 后续 deltas 基于该数据继续流式
5. **流式中断**：DevTools Network → Throttling Slow 3G → 中途关闭 Drawer → 验证不发异常、不跳登录页
6. **401 处理**：手动清 localStorage `mw_token` → 发送消息 → 验证跳转登录页（与现有 `request.ts` 一致）

### E2E 场景

| # | 场景 | 预期 |
|---|---|---|
| 1 | 登录用户进入首页 → 点 "AI 助手" → 输入"我有什么看过" | 流式输出, tool_call 调用 query_my_favorites, 结果以列表形式回答 |
| 2 | 上一轮完成后立即发"那平均评分呢" | 流式输出, tool_call 调用 query_user_stats (注意: 这是新请求, server 不依赖前一轮) |
| 3 | 输入"帮我查 XX 番(animeId=5)的情绪曲线" | 若用户已收藏: tool_call query_anime_emotion_curve 成功, 输出曲线数据; 若未收藏: error=not_favorited |
| 4 | 调用桩 tool (search_anime) | tool_call 后, AI 收到 `{"error":"not_implemented"}`, 自然降级回答 |
| 5 | 中途关闭 Drawer | UI 消失, abort 触发, 后端日志记录 cancellation |
| 6 | 后端返回 401 | 前端跳转登录页 |
| 7 | 网络中断 5 秒 | UI 显示 ⚠️ banner, assistant message 标记 isError=true, 已收到的 delta 保留 |
| 8 | 关闭后再开 Drawer | messages 清空(用户已确认) |

## Files to Modify

**Backend (新建)**：
- `backend/ManWei.Api/Services/PcAiAgentService.cs` — 新 agent service
- `backend/ManWei.Api/Services/PcAiTools.cs` — 5 个 tool 注册表
- `backend/ManWei.Api/Controllers/PcAiAgentController.cs` — 新 controller + DTO

**Backend (修改)**：
- `backend/ManWei.Api/Services/BaseAiAgentService.cs` — 新增 `StreamChatAsync` (IAsyncEnumerable) + `ResolveApiKey` + `StreamEvent` 类 + `ToolCallAccumulator` 私有类
- `backend/ManWei.Api/Program.cs` — 注册 `AddScoped<PcAiAgentService>()`
- `backend/ManWei.Api/appsettings.json` — 加 `_comment` 字段说明 API key 应走 env var

**Frontend (新建)**：
- `frontend/pc-client/src/services/chat.ts` — NDJSON fetch 流式客户端
- `frontend/pc-client/src/stores/aiAssistantStore.ts` — Zustand store
- `frontend/pc-client/src/components/AiAssistantDrawer/index.tsx` — Drawer 组件
- `frontend/pc-client/src/components/AiAssistantDrawer/AiAssistantDrawer.module.css` — 样式

**Frontend (修改)**：
- `frontend/pc-client/src/types/api.ts` — 加 `ChatMessage` / `ChatStreamEvent` / `PcChatRequest`
- `frontend/pc-client/src/components/AppShell/index.tsx` — 顶部加 "AI 助手" 按钮 + 挂载 Drawer

## Out of Scope (Future)

- 对话历史持久化（数据库 / localStorage）
- 多 userId 并发（同一用户在多 tab 打开 Drawer 的处理）
- 流式响应中断后的断点续传
- 5 个 tool 中 2 个桩（search_anime / query_global_emotion_tags）的实际实现
- 工具调用的人工审核 / 暂停机制（PC 端暂时不暴露 tool 调用确认 UI）
- Admin 端共享此 AI 助手（目前仍是独立 `AiAgentController` for admin）
