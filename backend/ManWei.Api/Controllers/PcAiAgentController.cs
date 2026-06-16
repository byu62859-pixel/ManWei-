using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    // 复用静态实例, 避免每次序列化都 new 一个 JsonSerializerOptions
    private static readonly JsonSerializerOptions _ndjsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        Response.Headers.CacheControl = "no-store";

        try
        {
            await foreach (var evt in _service.StreamChatAsync(
                new[] {
                    new { role = "system", content = _service.SystemPrompt },
                    new { role = "user", content = request.Message }
                },
                ct))
            {
                var json = JsonSerializer.Serialize(evt, _ndjsonOptions);
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
                    "{\"type\":\"error\",\"error\":\"AI 服务暂时不可用，请稍后重试\"}\n",
                    CancellationToken.None);
            }
            catch { /* connection probably dead */ }
        }
    }
}

public class PcChatRequestDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    // History 字段保留但不传给后端 (前端 UI 多轮但 server 无状态)
    [JsonPropertyName("history")]
    public List<object>? History { get; set; }
}
