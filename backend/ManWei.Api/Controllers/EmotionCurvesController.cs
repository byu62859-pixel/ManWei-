using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManWei.Api.Common;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;

namespace ManWei.Api.Controllers;

/// <summary>
/// 情感曲线管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmotionCurvesController : ControllerBase
{
    private readonly AppDbContext _context;

    public EmotionCurvesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取指定收藏的情感曲线数据（按集数升序，用于折线图渲染）
    /// </summary>
    /// <param name="favoriteId">收藏ID</param>
    /// <returns>情感曲线数据列表</returns>
    [HttpGet("{favoriteId}")]
    [ProducesResponseType(typeof(Result<List<EmotionCurveDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<List<EmotionCurveDto>>>> GetCurve(int favoriteId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        var curves = await _context.EmotionRecords
            .Where(er => er.FavoriteId == favoriteId)
            .OrderBy(er => er.Episode)
            .Select(er => new EmotionCurveDto
            {
                Episode = er.Episode,
                EmotionLevel = er.EmotionLevel,
                CreateTime = er.CreateTime
            })
            .ToListAsync();

        return Ok(Result<List<EmotionCurveDto>>.Success(curves));
    }

    /// <summary>
    /// 创建或更新情感记录（Upsert）
    /// </summary>
    /// <param name="request">情感记录请求</param>
    /// <returns>创建/更新结果</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Result<EmotionCurveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<EmotionCurveDto>>> Upsert([FromBody] CreateEmotionCurveRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .Include(f => f.Anime)
            .FirstOrDefaultAsync(f => f.Id == request.FavoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        // Episode 上限校验：与 Favorite.Progress 保持一致
        // null/0 视为未知，按 500 兜底
        var maxEpisode = favorite.Anime?.TotalEpisodes is > 0
            ? favorite.Anime.TotalEpisodes.Value
            : 500;
        if (request.Episode <= 0)
            return BadRequest(Result<EmotionCurveDto>.Fail(400, "集数必须大于 0"));
        if (request.Episode > maxEpisode)
            return BadRequest(Result<EmotionCurveDto>.Fail(400,
                $"集数不能超过总集数 {maxEpisode} 集"));

        // 按 (FavoriteId, Episode) 复合键查询是否已存在记录
        var existing = await _context.EmotionRecords
            .FirstOrDefaultAsync(er => er.FavoriteId == request.FavoriteId
                                      && er.Episode == request.Episode);

        EmotionRecord record;
        if (existing != null)
        {
            // 更新
            existing.EmotionLevel = request.EmotionLevel;
            record = existing;
        }
        else
        {
            // 创建
            record = new EmotionRecord
            {
                FavoriteId = request.FavoriteId,
                Episode = request.Episode,
                EmotionLevel = request.EmotionLevel,
                CreateTime = DateTime.UtcNow
            };
            _context.EmotionRecords.Add(record);
        }

        await _context.SaveChangesAsync();

        var dto = new EmotionCurveDto
        {
            Episode = record.Episode,
            EmotionLevel = record.EmotionLevel,
            CreateTime = record.CreateTime
        };

        return Ok(Result<EmotionCurveDto>.Success(dto, existing != null ? "更新成功" : "创建成功"));
    }

    /// <summary>
    /// 删除指定收藏某一集的情感记录
    /// </summary>
    /// <param name="favoriteId">收藏ID</param>
    /// <param name="episode">集数</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{favoriteId}/{episode}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int favoriteId, int episode)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        var record = await _context.EmotionRecords
            .FirstOrDefaultAsync(er => er.FavoriteId == favoriteId && er.Episode == episode);

        if (record == null)
            return NotFound(Result.Fail(404, "该集情感记录不存在"));

        _context.EmotionRecords.Remove(record);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 从 Claims 获取当前用户ID
    /// </summary>
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }
}
