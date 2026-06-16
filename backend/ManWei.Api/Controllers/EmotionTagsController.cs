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
/// 情感标签管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmotionTagsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EmotionTagsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取标签列表（预置标签 + 当前用户对指定动漫的自定义标签）
    /// </summary>
    /// <param name="animeId">动漫ID</param>
    /// <returns>标签列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Result<List<EmotionTagDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<EmotionTagDto>>>> GetList([FromQuery(Name = "animeId")] int animeId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<List<EmotionTagDto>>.Fail(401, "未授权"));

        var tags = await _context.EmotionTags
            .Where(t => t.IsPreset || (t.UserId == userId.Value && t.AnimeId == animeId))
            .OrderBy(t => t.IsPreset ? 0 : 1)
            .ThenBy(t => t.CreateTime)
            .Select(t => new EmotionTagDto
            {
                Id = t.Id,
                Name = t.Name,
                IsPreset = t.IsPreset,
                AnimeId = t.AnimeId,
                CreateTime = t.CreateTime
            })
            .ToListAsync();

        return Ok(Result<List<EmotionTagDto>>.Success(tags));
    }

    /// <summary>
    /// 获取当前用户在已收藏动漫中使用过的标签名（去重，F2 标签筛选专用）
    /// </summary>
    /// <returns>标签名列表</returns>
    [HttpGet("used")]
    [ProducesResponseType(typeof(Result<List<string>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<string>>>> GetUsedTags()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<List<string>>.Fail(401, "未授权"));

        // 只返回当前用户且有关联收藏的标签（排除已删除收藏的"死标签"）
        // 包含自定义标签（UserId = userId）和预置标签（UserId = null）
        var tags = await _context.EmotionTags
            .Where(t => t.UserId == userId.Value || t.UserId == null)
            .Where(t => _context.Favorites.Any(f => f.AnimeId == t.AnimeId && f.UserId == userId.Value))
            .Select(t => t.Name.Trim())
            .Distinct()
            .ToListAsync();

        return Ok(Result<List<string>>.Success(tags));
    }

    /// <summary>
    /// 创建自定义情感标签
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>新创建的标签</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Result<EmotionTagDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<EmotionTagDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<EmotionTagDto>>> Create([FromBody] CreateEmotionTagRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<EmotionTagDto>.Fail(401, "未授权"));

        // 检查当前用户是否已有同名自定义标签（同用户+同动漫）
        var exists = await _context.EmotionTags
            .AnyAsync(t => t.Name == request.Name && t.UserId == userId.Value && t.AnimeId == request.AnimeId);

        if (exists)
            return BadRequest(Result<EmotionTagDto>.Fail(400, "您已创建过同名标签"));

        // 创建自定义标签
        var tag = new EmotionTag
        {
            Name = request.Name,
            IsPreset = false,
            UserId = userId.Value,
            AnimeId = request.AnimeId,
            CreateTime = DateTime.UtcNow
        };

        _context.EmotionTags.Add(tag);
        await _context.SaveChangesAsync();

        var dto = new EmotionTagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            IsPreset = tag.IsPreset,
            AnimeId = tag.AnimeId,
            CreateTime = tag.CreateTime
        };

        return StatusCode(201, Result<EmotionTagDto>.Success(dto, "创建成功"));
    }

    /// <summary>
    /// 删除自定义情感标签
    /// </summary>
    /// <param name="id">标签ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        var tag = await _context.EmotionTags.FindAsync(id);
        if (tag == null)
            return NotFound(Result.Fail(404, "标签不存在"));

        // 预置标签不可删除
        if (tag.IsPreset)
            return BadRequest(Result.Fail(400, "预置标签不可删除"));

        // 只能删除自己的标签，Admin 可删除任意自定义标签
        if (tag.UserId != userId.Value && !User.IsInRole("Admin"))
            return Forbid();

        _context.EmotionTags.Remove(tag);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 获取指定动漫的用户自定义标签词云（Admin 专用）
    /// </summary>
    /// <param name="animeId">动漫ID</param>
    /// <returns>词云数据列表</returns>
    [HttpGet("anime/{animeId}/wordcloud")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<List<WordCloudItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<WordCloudItemDto>>>> GetAnimeWordCloud(int animeId)
    {
        var items = await _context.EmotionTags
            .Where(t => t.AnimeId == animeId && !t.IsPreset && t.UserId != null)
            .GroupBy(t => t.Name)
            .Select(g => new WordCloudItemDto
            {
                Name = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return Ok(Result<List<WordCloudItemDto>>.Success(items));
    }

    /// <summary>
    /// 获取词云统计数据（当前用户创建的自定义标签，按名称分组统计数量）
    /// </summary>
    /// <returns>词云数据列表</returns>
    [HttpGet("wordcloud")]
    [ProducesResponseType(typeof(Result<List<WordCloudItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<WordCloudItemDto>>>> GetWordCloud()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        var items = await _context.EmotionTags
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Name)
            .Select(g => new WordCloudItemDto
            {
                Name = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return Ok(Result<List<WordCloudItemDto>>.Success(items));
    }

    /// <summary>
    /// 获取标签管理统计数据（Admin专用，统计同名标签的使用次数和关联用户数）
    /// </summary>
    /// <returns>标签统计列表（分页）</returns>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<PagedResult<EmotionTagStatsDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<EmotionTagStatsDto>>>> GetStats([FromQuery] EmotionTagStatsQueryDto query)
    {
        var baseQuery = _context.EmotionTags.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            baseQuery = baseQuery.Where(t => t.Name.Contains(query.Keyword));
        }

        var dataQuery = baseQuery
            .Select(t => new EmotionTagStatsDto
            {
                Id = t.Id,
                Name = t.Name,
                IsPreset = t.IsPreset,
                CreateTime = t.CreateTime,
                UsageCount = _context.EmotionTags.Count(x => x.Name == t.Name),
                RelatedUserCount = _context.EmotionTags
                    .Where(x => x.Name == t.Name && x.UserId != null)
                    .Select(x => x.UserId)
                    .Distinct()
                    .Count()
            })
            .OrderByDescending(t => t.UsageCount);

        var totalCount = await baseQuery.CountAsync();

        var items = await dataQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var result = new PagedResult<EmotionTagStatsDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(Result<PagedResult<EmotionTagStatsDto>>.Success(result));
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
