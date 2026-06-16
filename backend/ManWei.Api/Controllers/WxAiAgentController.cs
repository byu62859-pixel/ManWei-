using System.Security.Claims;
using ManWei.Api.Common;
using ManWei.Api.DTOs;
using ManWei.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManWei.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class WxAiAgentController : ControllerBase
{
    private readonly WxAiAgentService _wxAiAgentService;
    private readonly ILogger<WxAiAgentController> _logger;

    public WxAiAgentController(WxAiAgentService wxAiAgentService, ILogger<WxAiAgentController> logger)
    {
        _wxAiAgentService = wxAiAgentService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<Result<WxChatResponseDto>>> Chat([FromBody] WxChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(Result<WxChatResponseDto>.Fail(400, "消息内容不能为空"));

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(Result<WxChatResponseDto>.Fail(401, "未登录"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        try
        {
            var result = await _wxAiAgentService.ChatAsync(request, userId.Value, cts.Token);
            return Ok(Result<WxChatResponseDto>.Success(result));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, Result<WxChatResponseDto>.Fail(504, "AI 响应超时，请重试"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WxAiAgent error, Message: {ErrorMsg}", ex.Message);
            return StatusCode(500, Result<WxChatResponseDto>.Fail(500, "AI 服务错误"));
        }
    }

    private int? GetUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out var id)) return id;
        return null;
    }
}
