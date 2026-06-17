using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManWei.Api.Services;
using ManWei.Api.Services.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManWei.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendAnimeService _service;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendAnimeService service,
        ILogger<RecommendationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<RecommendResult>> Get(
        [FromQuery] string? keyword,
        [FromQuery] string? animeType,
        [FromQuery] int topK = 5,
        [FromQuery] bool deterministic = false,
        CancellationToken ct = default)
    {
        // userId (CLAUDE.md 红线: int.TryParse)
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId))
        {
            return Unauthorized(new { error = "未登录" });
        }

        var req = new RecommendRequest
        {
            Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            AnimeType = string.IsNullOrWhiteSpace(animeType) ? null : animeType,
            TopK = topK,
            Deterministic = deterministic
        };

        var result = await _service.RecommendAsync(userId, req, ct);
        return Ok(result);
    }
}
