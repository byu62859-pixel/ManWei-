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
/// 观后感管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取当前用户观后感 Feed 列表（分页）
    /// </summary>
    /// <param name="page">页码，默认1</param>
    /// <param name="pageSize">每页数量，默认10</param>
    [HttpGet]
    [ProducesResponseType(typeof(Result<PagedResult<ReviewSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<ReviewSummaryDto>>>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? orderBy = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        var query = _context.Reviews
            .Where(r => r.Favorite.UserId == userId.Value);

        query = orderBy switch
        {
            "AnimeName" => query.OrderBy(r => r.Favorite.Anime.Name),
            _ => query.OrderByDescending(r => r.UpdateTime).ThenByDescending(r => r.Id)
        };

        var total = await query.CountAsync();

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.FavoriteId,
                r.Favorite.AnimeId,
                AnimeName  = r.Favorite.Anime.Name,
                AnimeCover = r.Favorite.Anime.Cover ?? string.Empty,
                r.Content,
                r.UpdateTime
            })
            .ToListAsync();

        static string BuildSummary(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            var flat = content.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            return flat.Length > 60 ? flat[..60] + "..." : flat;
        }

        var items = rawItems.Select(r => new ReviewSummaryDto
        {
            ReviewId       = r.Id,
            FavoriteId     = r.FavoriteId,
            AnimeId        = r.AnimeId,
            AnimeName      = r.AnimeName,
            AnimeCover     = r.AnimeCover,
            ContentSummary = BuildSummary(r.Content),
            UpdatedAt      = r.UpdateTime
        }).ToList();

        var result = new PagedResult<ReviewSummaryDto>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };

        return Ok(Result<PagedResult<ReviewSummaryDto>>.Success(result, "success"));
    }

    /// <summary>
    /// 获取指定收藏的观后感
    /// </summary>
    /// <param name="favoriteId">收藏ID</param>
    /// <returns>观后感</returns>
    [HttpGet("{favoriteId}")]
    [ProducesResponseType(typeof(Result<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<ReviewDto>>> GetByFavoriteId(int favoriteId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.FavoriteId == favoriteId);

        if (review == null)
            return NotFound(Result.Fail(404, "该收藏暂无观后感"));

        var dto = new ReviewDto
        {
            Id = review.Id,
            FavoriteId = review.FavoriteId,
            Content = review.Content,
            CreateTime = review.CreateTime,
            UpdateTime = review.UpdateTime
        };

        return Ok(Result<ReviewDto>.Success(dto));
    }

    /// <summary>
    /// 创建或更新观后感（Upsert）
    /// </summary>
    /// <param name="request">创建/更新请求</param>
    /// <returns>观后感</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Result<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<ReviewDto>>> Upsert([FromBody] CreateReviewRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == request.FavoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        // 按 FavoriteId 查询是否已存在记录
        var existing = await _context.Reviews
            .FirstOrDefaultAsync(r => r.FavoriteId == request.FavoriteId);

        Review review;
        if (existing != null)
        {
            // 更新
            existing.Content = request.Content;
            existing.UpdateTime = DateTime.UtcNow;
            review = existing;
        }
        else
        {
            // 创建
            review = new Review
            {
                FavoriteId = request.FavoriteId,
                Content = request.Content,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            };
            _context.Reviews.Add(review);
        }

        await _context.SaveChangesAsync();

        var dto = new ReviewDto
        {
            Id = review.Id,
            FavoriteId = review.FavoriteId,
            Content = review.Content,
            CreateTime = review.CreateTime,
            UpdateTime = review.UpdateTime
        };

        return Ok(Result<ReviewDto>.Success(dto, existing != null ? "更新成功" : "创建成功"));
    }

    /// <summary>
    /// 删除观后感
    /// </summary>
    /// <param name="favoriteId">收藏ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{favoriteId}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int favoriteId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        // 先查 Favorite 确认归属，防止跨用户数据访问
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在或无权访问"));

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.FavoriteId == favoriteId);

        if (review == null)
            return NotFound(Result.Fail(404, "该收藏暂无观后感"));

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 获取所有观后感列表（Admin 专用，支持关键词搜索）
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<PagedResult<AdminReviewDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<AdminReviewDto>>>> GetAdminList([FromQuery] AdminReviewQueryDto query)
    {
        var queryable = _context.Reviews
            .Include(r => r.Favorite)
            .ThenInclude(f => f.User)
            .Include(r => r.Favorite)
            .ThenInclude(f => f.Anime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(r => r.Content.Contains(query.Keyword));
        }

        var totalCount = await queryable.CountAsync();

        var rawItems = await queryable
            .OrderByDescending(r => r.UpdateTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new
            {
                r.Id,
                r.FavoriteId,
                r.Favorite.AnimeId,
                AnimeName = r.Favorite.Anime.Name,
                AnimeCover = r.Favorite.Anime.Cover,
                r.Favorite.UserId,
                NickName = r.Favorite.User.NickName,
                r.Content,
                r.UpdateTime
            })
            .ToListAsync();

        static string BuildSummary(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            var flat = content.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            return flat.Length > 60 ? flat[..60] + "..." : flat;
        }

        var items = rawItems.Select(r => new AdminReviewDto
        {
            ReviewId = r.Id,
            FavoriteId = r.FavoriteId,
            AnimeId = r.AnimeId,
            AnimeName = r.AnimeName,
            AnimeCover = r.AnimeCover,
            UserId = r.UserId,
            NickName = r.NickName,
            ContentSummary = BuildSummary(r.Content),
            UpdatedAt = r.UpdateTime
        }).ToList();

        var result = new PagedResult<AdminReviewDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(Result<PagedResult<AdminReviewDto>>.Success(result));
    }

    /// <summary>
    /// 获取观后感详情（Admin 专用）
    /// </summary>
    [HttpGet("{favoriteId}/admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<AdminReviewDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<AdminReviewDetailDto>>> GetAdminDetail(int favoriteId)
    {
        var review = await _context.Reviews
            .Include(r => r.Favorite)
            .ThenInclude(f => f.User)
            .Include(r => r.Favorite)
            .ThenInclude(f => f.Anime)
            .FirstOrDefaultAsync(r => r.FavoriteId == favoriteId);

        if (review == null)
            return NotFound(Result.Fail(404, "观后感不存在"));

        var dto = new AdminReviewDetailDto
        {
            ReviewId = review.Id,
            FavoriteId = review.FavoriteId,
            AnimeId = review.Favorite.AnimeId,
            AnimeName = review.Favorite.Anime.Name,
            AnimeCover = review.Favorite.Anime.Cover,
            UserId = review.Favorite.UserId,
            NickName = review.Favorite.User.NickName,
            Content = review.Content,
            UpdatedAt = review.UpdateTime
        };

        return Ok(Result<AdminReviewDetailDto>.Success(dto));
    }

    /// <summary>
    /// 删除指定用户的观后感（Admin 专用）
    /// </summary>
    [HttpDelete("{favoriteId}/admin")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> DeleteByAdmin(int favoriteId)
    {
        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.FavoriteId == favoriteId);

        if (review == null)
            return NotFound(Result.Fail(404, "观后感不存在"));

        _context.Reviews.Remove(review);
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
