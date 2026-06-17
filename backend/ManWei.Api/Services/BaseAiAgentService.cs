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

    protected static bool GetBool(Dictionary<string, object?> args, string key, bool defaultValue = false)
    {
        if (!args.TryGetValue(key, out var val)) return defaultValue;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.True) return true;
            if (je.ValueKind == JsonValueKind.False) return false;
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                if (bool.TryParse(str, out var b)) return b;
            }
        }
        if (val is bool bv) return bv;
        if (bool.TryParse(val?.ToString(), out var pv)) return pv;
        return defaultValue;
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

    private class ToolCallAccumulator
    {
        public string Id = "";
        public string? Type;
        public string Name = "";
        public System.Text.StringBuilder ArgumentsJson = new();
    }

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
}
