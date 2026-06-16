using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManWei.Api.Common;
using ManWei.Api.Data;
using ManWei.Api.DTOs;

namespace ManWei.Api.Controllers;

/// <summary>
/// 数据看板控制器（PC端，仅管理员可用）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取全局统计数据
    /// </summary>
    /// <returns>统计数据</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(Result<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<DashboardStatsDto>>> GetStats()
    {
        var stats = new DashboardStatsDto
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalAnime = await _context.Anime.CountAsync(),
            TotalFavorites = await _context.Favorites.CountAsync(),
            TotalEmotionTags = await _context.EmotionTags.CountAsync(),
            TotalReviews = await _context.Reviews.CountAsync()
        };

        return Ok(Result<DashboardStatsDto>.Success(stats));
    }

    /// <summary>
    /// 获取今日概览
    /// </summary>
    /// <returns>今日新增数据</returns>
    [HttpGet("today-overview")]
    [ProducesResponseType(typeof(Result<TodayOverviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<TodayOverviewDto>>> GetTodayOverview()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var overview = new TodayOverviewDto
        {
            NewUsers = await _context.Users.CountAsync(u => u.CreateTime >= today && u.CreateTime < tomorrow),
            NewFavorites = await _context.Favorites.CountAsync(f => f.CreateTime >= today && f.CreateTime < tomorrow),
            NewTags = await _context.EmotionTags.CountAsync(t => t.CreateTime >= today && t.CreateTime < tomorrow),
            NewAnime = await _context.Anime.CountAsync(a => a.CreateTime >= today && a.CreateTime < tomorrow)
        };

        return Ok(Result<TodayOverviewDto>.Success(overview));
    }

    /// <summary>
    /// 获取用户增长趋势
    /// </summary>
    /// <param name="days">统计天数，默认30天</param>
    /// <returns>每日用户注册数</returns>
    [HttpGet("user-growth")]
    [ProducesResponseType(typeof(Result<List<UserGrowthDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<UserGrowthDto>>>> GetUserGrowth([FromQuery] int days = 30)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        var data = await _context.Users
            .Where(u => u.CreateTime >= startDate)
            .GroupBy(u => u.CreateTime.Date)
            .Select(g => new UserGrowthDto
            {
                Date = g.Key,
                UserCount = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return Ok(Result<List<UserGrowthDto>>.Success(data));
    }

    /// <summary>
    /// 获取动漫收藏排行榜
    /// </summary>
    /// <param name="top">返回数量，默认10</param>
    /// <returns>动漫收藏排行</returns>
    [HttpGet("anime-rank")]
    [ProducesResponseType(typeof(Result<List<AnimeFavoriteRankDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<AnimeFavoriteRankDto>>>> GetAnimeRank([FromQuery] int top = 10)
    {
        var data = await _context.Favorites
            .GroupBy(f => f.AnimeId)
            .Select(g => new { AnimeId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .Join(_context.Anime, f => f.AnimeId, a => a.Id, (f, a) => new AnimeFavoriteRankDto
            {
                AnimeId = a.Id,
                AnimeName = a.Name,
                Cover = a.Cover,
                FavoriteCount = f.Count
            })
            .ToListAsync();

        return Ok(Result<List<AnimeFavoriteRankDto>>.Success(data));
    }

    /// <summary>
    /// 获取标签使用排行 TOP10
    /// </summary>
    /// <returns>标签使用排行</returns>
    [HttpGet("tag-rank")]
    [ProducesResponseType(typeof(Result<List<TagUsageRankDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<TagUsageRankDto>>>> GetTagRank()
    {
        var data = await _context.EmotionTags
            .GroupBy(t => t.Name)
            .Select(g => new TagUsageRankDto
            {
                TagName = g.Key,
                UsageCount = g.Count()
            })
            .OrderByDescending(x => x.UsageCount)
            .Take(10)
            .ToListAsync();

        return Ok(Result<List<TagUsageRankDto>>.Success(data));
    }
}
