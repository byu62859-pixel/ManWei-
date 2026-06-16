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
[Authorize(Roles = "Admin")]
public class AiAgentController : ControllerBase
{
    private readonly IAiAgentService _aiAgentService;
    private readonly ILogger<AiAgentController> _logger;

    public AiAgentController(IAiAgentService aiAgentService, ILogger<AiAgentController> logger)
    {
        _aiAgentService = aiAgentService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<Result<ChatResponseDto>>> Chat([FromBody] ChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(Result<ChatResponseDto>.Fail(400, "消息内容不能为空"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        try
        {
            var result = await _aiAgentService.ChatAsync(request, cts.Token);
            return Ok(Result<ChatResponseDto>.Success(result));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, Result<ChatResponseDto>.Fail(504, "AI 响应超时，请重试"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Agent error");
            return StatusCode(500, Result<ChatResponseDto>.Fail(500, "AI 服务错误"));
        }
    }
}
