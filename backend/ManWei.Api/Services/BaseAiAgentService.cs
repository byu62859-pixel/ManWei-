using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManWei.Api.Services;

public abstract class BaseAiAgentService
{
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly IConfiguration _config;
    protected readonly ILogger _logger;

    protected BaseAiAgentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    protected abstract string AgentSystemPrompt { get; }
    protected abstract IEnumerable<object> GetTools();
    protected abstract Task<string> ExecuteToolAsync(string name, Dictionary<string, object?> args, CancellationToken ct);

    protected async Task<JsonElement> CallDeepSeekAsync(List<object> messages, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("DeepSeek");
        var apiKey = ResolveApiKey();
        var model = _config["DeepSeek:Model"] ?? "deepseek-chat";

        _logger.LogInformation("[AI] 开始构建请求，消息数: {Count}", messages.Count);

        var tools = GetTools();

        var payload = new
        {
            model,
            messages,
            tools,
            stream = false
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        _logger.LogInformation("[AI] 请求体构建完成，长度: {Length}", payloadJson.Length);

        var request = new HttpRequestMessage(HttpMethod.Post, "/chat/completions")
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        _logger.LogInformation("[AI] 发送请求到 DeepSeek...");
        var response = await client.SendAsync(request, ct);
        _logger.LogInformation("[AI] 收到响应，状态码: {StatusCode}", response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[AI] 请求失败: {Json}", json);
            throw new Exception($"DeepSeek API error: {json}");
        }

        var doc = JsonDocument.Parse(json);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        if (message.TryGetProperty("tool_calls", out var toolCallsElement)
            && toolCallsElement.ValueKind == JsonValueKind.Array
            && toolCallsElement.GetArrayLength() > 0)
        {
            _logger.LogInformation("[AI] 检测到 tool_calls 数量: {Count}", toolCallsElement.GetArrayLength());

            var toolCallsList = toolCallsElement.EnumerateArray().ToList();

            messages.Add(message);

            foreach (var tc in toolCallsList)
            {
                var func = tc.GetProperty("function");
                var toolName = func.GetProperty("name").GetString() ?? "";
                var argsJson = func.GetProperty("arguments").GetString() ?? "{}";
                var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson) ?? new();

                _logger.LogInformation("[AI] 执行工具: {Name}, 参数: {Args}", toolName, argsJson);

                var toolResult = await ExecuteToolAsync(toolName, args, ct);

                var toolCallId = tc.GetProperty("id").GetString() ?? "";
                messages.Add(new { role = "tool", tool_call_id = toolCallId, content = toolResult });
            }

            var finalResponse = await CallDeepSeekAsync(messages, ct);
            return finalResponse;
        }

        return message;
    }

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

    protected static string ExtractContent(JsonElement message)
    {
        if (message.TryGetProperty("content", out var contentEl))
        {
            if (contentEl.ValueKind == JsonValueKind.String)
                return contentEl.GetString() ?? "";
            if (contentEl.ValueKind == JsonValueKind.Null)
                return "";
        }
        return "";
    }

    protected static string SafeGetString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? defaultValue;
        return defaultValue;
    }

    protected static int GetInt(Dictionary<string, object?> args, string key, int defaultValue = 1)
    {
        if (!args.TryGetValue(key, out var val)) return defaultValue;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var i)) return i;
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                if (int.TryParse(str, out var si)) return si;
            }
        }
        if (val is int iv) return iv;
        if (int.TryParse(val?.ToString(), out var pv)) return pv;
        return defaultValue;
    }

    protected static string? GetString(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var val)) return null;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number) return je.GetRawText();
            if (je.ValueKind == JsonValueKind.String) return je.GetString();
            if (je.ValueKind == JsonValueKind.True) return "true";
            if (je.ValueKind == JsonValueKind.False) return "false";
            return null;
        }
        return val?.ToString();
    }
}
