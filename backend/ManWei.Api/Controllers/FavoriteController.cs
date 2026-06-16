using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManWei.Api.Common;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;
using ManWei.Api.Services;

namespace ManWei.Api.Controllers;

/// <summary>
/// 用户收藏控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly ILogger<FavoritesController> _logger;

    public FavoritesController(AppDbContext context, IBangumiService bangumiService,
        ILogger<FavoritesController> logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前用户的收藏列表
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>分页收藏列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Result<PagedResult<FavoriteDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<FavoriteDto>>>> GetList([FromQuery] FavoriteQueryDto query)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<PagedResult<FavoriteDto>>.Fail(401, "未授权"));

        var queryable = _context.Favorites
            .Where(f => f.UserId == userId.Value)
            .Include(f => f.Anime)
            .AsQueryable();

        // 状态筛选（原有）
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(f => f.Status == query.Status.Value);
        }

        // 标签筛选（F2 新增）
        // 条件：标签贴在该动漫上 AND (当前用户自定义标签 OR 预置标签 UserId=null) AND 标签名匹配
        if (!string.IsNullOrEmpty(query.TagName))
        {
            queryable = queryable.Where(f =>
                _context.EmotionTags.Any(t =>
                    t.AnimeId == f.AnimeId &&
                    (t.UserId == userId.Value || t.UserId == null) &&
                    t.Name == query.TagName.Trim()
                ));
        }

        // 排序逻辑（F3 预留，使用 switch 分支）
        // NULLS LAST 写法（SQL Server 不支持 NULLS LAST，用 CASE WHEN 模拟）
        // null=1 排后，null=0 排前，再按 Rating 排序
        queryable = query.OrderBy switch
        {
            "rating_desc" or "Rating" => queryable
                .OrderBy(f => f.Rating == null ? 1 : 0)
                .ThenByDescending(f => f.Rating),
            "rating_asc" => queryable
                .OrderBy(f => f.Rating == null ? 1 : 0)
                .ThenBy(f => f.Rating),
            "anime_name" or "AnimeName" => queryable
                .OrderBy(f => f.Anime.Name),
            _ => queryable.OrderByDescending(f => f.UpdateTime)
        };

        var totalCount = await queryable.CountAsync();

        var items = await queryable
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(f => new FavoriteDto
            {
                Id = f.Id,
                AnimeId = f.AnimeId,
                AnimeName = f.Anime.Name,
                AnimeCover = f.Anime.Cover,
                AnimeType = f.Anime.AnimeType,
                AnimeTotalEpisodes = f.Anime.TotalEpisodes,
                Status = f.Status,
                Progress = f.Progress,
                Rating = f.Rating,           // F3 新增
                CreateTime = f.CreateTime,
                UpdateTime = f.UpdateTime
            })
            .ToListAsync();

        var result = new PagedResult<FavoriteDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(Result<PagedResult<FavoriteDto>>.Success(result));
    }

    /// <summary>
    /// 获取当前用户各状态收藏数量
    /// </summary>
    /// <returns>各状态收藏数量统计</returns>
    [HttpGet("counts")]
    [ProducesResponseType(typeof(Result<FavoriteCountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<FavoriteCountDto>>> GetCounts()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteCountDto>.Fail(401, "未授权"));

        var favorites = _context.Favorites.Where(f => f.UserId == userId.Value);

        var counts = new FavoriteCountDto
        {
            All = await favorites.CountAsync(),
            Wish = await favorites.CountAsync(f => f.Status == FavoriteStatus.WantToWatch),
            Watching = await favorites.CountAsync(f => f.Status == FavoriteStatus.Watching),
            Watched = await favorites.CountAsync(f => f.Status == FavoriteStatus.Watched)
        };

        return Ok(Result<FavoriteCountDto>.Success(counts));
    }

    /// <summary>
    /// 检查当前用户是否已收藏指定动漫
    /// </summary>
    /// <param name="animeId">动漫ID</param>
    /// <returns>收藏状态</returns>
    [HttpGet("check/{animeId}")]
    [ProducesResponseType(typeof(Result<FavoriteCheckDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<FavoriteCheckDto>>> CheckFavorite(int animeId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteCheckDto>.Fail(401, "未授权"));

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.AnimeId == animeId);

        var dto = new FavoriteCheckDto
        {
            IsFavorited = favorite != null,
            FavoriteId = favorite?.Id,
            Status = favorite?.Status,
            Progress = favorite?.Progress,
            Rating = favorite?.Rating
        };

        return Ok(Result<FavoriteCheckDto>.Success(dto));
    }

    /// <summary>
    /// 获取收藏详情
    /// </summary>
    /// <param name="id">收藏ID</param>
    /// <returns>收藏详情</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<FavoriteDto>>> GetById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteDto>.Fail(401, "未授权"));

        var favorite = await _context.Favorites
            .Include(f => f.Anime)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result<FavoriteDto>.Fail(404, "收藏不存在"));

        var dto = MapToDto(favorite);
        return Ok(Result<FavoriteDto>.Success(dto));
    }

    /// <summary>
    /// 添加收藏（初始状态为"想看"）
    /// </summary>
    /// <param name="dto">创建收藏请求</param>
    /// <returns>创建的收藏</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<FavoriteDto>>> Create([FromBody] CreateFavoriteDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteDto>.Fail(401, "未授权"));

        // 检查动漫是否存在
        var anime = await _context.Anime.FindAsync(dto.AnimeId);
        if (anime == null)
            return BadRequest(Result<FavoriteDto>.Fail(400, "动漫不存在"));

        // 检查是否已收藏
        var exists = await _context.Favorites
            .AnyAsync(f => f.UserId == userId.Value && f.AnimeId == dto.AnimeId);

        if (exists)
            return BadRequest(Result<FavoriteDto>.Fail(400, "已收藏过该动漫"));

        var favorite = new Favorite
        {
            UserId = userId.Value,
            AnimeId = dto.AnimeId,
            Status = FavoriteStatus.WantToWatch,
            Progress = 0,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };

        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();

        // 重新查询以获取关联数据
        await _context.Entry(favorite).Reference(f => f.Anime).LoadAsync();

        var resultDto = MapToDto(favorite);
        return Ok(Result<FavoriteDto>.Success(resultDto, "收藏成功"));
    }

    /// <summary>
    /// 更新收藏状态或进度
    /// </summary>
    /// <param name="id">收藏ID</param>
    /// <param name="dto">更新内容</param>
    /// <returns>更新后的收藏</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<FavoriteDto>>> Update(int id, [FromBody] UpdateFavoriteDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteDto>.Fail(401, "未授权"));

        var favorite = await _context.Favorites
            .Include(f => f.Anime)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result<FavoriteDto>.Fail(404, "收藏不存在"));

        // 更新状态
        if (dto.Status.HasValue)
        {
            if (dto.Status.Value < 0 || dto.Status.Value > 2)
                return BadRequest(Result<FavoriteDto>.Fail(400, "状态值无效（0=想看 1=在看 2=看过）"));

            favorite.Status = dto.Status.Value;
        }

        // 更新进度
        if (dto.Progress.HasValue)
        {
            if (dto.Progress.Value < 0)
                return BadRequest(Result<FavoriteDto>.Fail(400, "进度不能为负数"));

            // 进度上限 = 动漫总集数（null/0 视为未知，按 500 兜底）
            var maxProgress = favorite.Anime?.TotalEpisodes is > 0
                ? favorite.Anime.TotalEpisodes.Value
                : 500;
            if (dto.Progress.Value > maxProgress)
                return BadRequest(Result<FavoriteDto>.Fail(400,
                    $"进度不能超过总集数 {maxProgress} 集"));

            favorite.Progress = dto.Progress.Value;
        }

        // 更新评分（F3 新增）
        if (dto.Rating.HasValue)
        {
            if (dto.Rating.Value < 1 || dto.Rating.Value > 10)
                return BadRequest(Result<FavoriteDto>.Fail(400, "评分范围为 1-10"));

            favorite.Rating = dto.Rating.Value;
        }
        else if (dto.Rating == null && dto.Status == null && dto.Progress == null)
        {
            // 三者都传 null 时视为取消评分（前端显式传 null）
            favorite.Rating = null;
        }

        favorite.UpdateTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var resultDto = MapToDto(favorite);
        return Ok(Result<FavoriteDto>.Success(resultDto, "更新成功"));
    }

    /// <summary>
    /// 删除收藏
    /// </summary>
    /// <param name="id">收藏ID</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result.Fail(401, "未授权"));

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId.Value);

        if (favorite == null)
            return NotFound(Result.Fail(404, "收藏不存在"));

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 搜索动漫（本地+Bangumi）
    /// </summary>
    /// <param name="keyword">搜索关键字</param>
    /// <returns>搜索结果列表</returns>
    [HttpGet("search-anime")]
    [ProducesResponseType(typeof(Result<List<AnimeSearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<List<AnimeSearchResultDto>>>> SearchAnime([FromQuery] string keyword)
    {
        Console.WriteLine($"[Debug] 收到前端的关键词：'{keyword}'");
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(Result<List<AnimeSearchResultDto>>.Success(new List<AnimeSearchResultDto>()));

        // 本地搜索
        var localResults = await _context.Anime
            .Where(a => a.Name.Contains(keyword) && a.BangumiId != null)
            .Take(5)
            .Select(a => new AnimeSearchResultDto
            {
                AnimeId = a.Id,
                BangumiId = a.BangumiId ?? 0,
                Name = a.Name,
                NameCn = null,
                Cover = a.Cover,
                AnimeType = a.AnimeType,
                Source = "local"
            })
            .ToListAsync();

        var localBangumiIds = localResults.Select(r => r.BangumiId).ToHashSet();

        // Bangumi 搜索
        var bangumiResults = await _bangumiService.SearchAsync(keyword, 15 - localResults.Count);
        var bangumiDtos = bangumiResults
            .Where(s => !localBangumiIds.Contains(s.Id))
            .Select(s => new AnimeSearchResultDto
            {
                AnimeId = null,
                BangumiId = s.Id,
                Name = !string.IsNullOrWhiteSpace(s.NameCn) ? s.NameCn : s.Name,
                NameCn = s.NameCn,
                Cover = s.Images?.Large ?? s.Images?.Medium,
                AnimeType = MapBangumiPlatform(s.Platform),
                Source = "bangumi"
            })
            .ToList();

        // 合并结果
        var merged = localResults.Concat(bangumiDtos).Take(15).ToList();
        return Ok(Result<List<AnimeSearchResultDto>>.Success(merged));
    }

    /// <summary>
    /// 通过 BangumiId 添加收藏
    /// </summary>
    /// <param name="dto">添加收藏请求</param>
    /// <returns>创建的收藏</returns>
    [HttpPost("add")]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<FavoriteDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<Result<FavoriteDto>>> AddByBangumi([FromBody] AddFavoriteByBangumiDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(Result<FavoriteDto>.Fail(401, "未授权"));

        // 参数互斥校验
        if (dto.AnimeId == null && dto.BangumiId == null)
            return BadRequest(Result<FavoriteDto>.Fail(400, "AnimeId 和 BangumiId 必须传一个"));
        if (dto.AnimeId != null && dto.BangumiId != null)
            return BadRequest(Result<FavoriteDto>.Fail(400, "AnimeId 和 BangumiId 只能传一个"));

        int targetAnimeId;

        // 分支A：传了 AnimeId
        if (dto.AnimeId != null)
        {
            var anime = await _context.Anime.FindAsync(dto.AnimeId);
            if (anime == null)
                return BadRequest(Result<FavoriteDto>.Fail(400, "动漫不存在"));
            targetAnimeId = dto.AnimeId.Value;
        }
        // 分支B：传了 BangumiId
        else
        {
            var existingAnime = await _context.Anime
                .FirstOrDefaultAsync(a => a.BangumiId == dto.BangumiId);

            if (existingAnime != null)
            {
                targetAnimeId = existingAnime.Id;
            }
            else
            {
                var result = await _bangumiService.GetAndMapAnimeAsync(dto.BangumiId!.Value);
                if (result == null)
                    return StatusCode(503, Result<FavoriteDto>.Fail(503, "Bangumi 服务繁忙，请稍后重试"));

                var (newAnime, tags) = result.Value;

                _context.Anime.Add(newAnime);
                try
                {
                    await _context.SaveChangesAsync();

                    // 拉取本篇总集数（拉取失败不影响添加收藏成功）
                    // GetEpisodesTotalAsync 内部已 try/catch 返回 null；
                    // 但这里再加一层 try/catch 是为了防御性：万一 Service 未来
                    // 改动漏了 catch，不会让异常冒泡到外层影响"添加收藏"成功。
                    try
                    {
                        var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(dto.BangumiId!.Value);
                        if (totalEpisodes.HasValue)
                        {
                            newAnime.TotalEpisodes = totalEpisodes.Value;
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "拉取总集数异常，忽略 BangumiId={BangumiId}", dto.BangumiId);
                    }

                    // tags 来自 GetAndMapAnimeAsync 同一次响应，绑回 AnimeId 后写入
                    if (tags.Any())
                    {
                        foreach (var t in tags) t.AnimeId = newAnime.Id;
                        await _context.AnimeTags.AddRangeAsync(tags);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    existingAnime = await _context.Anime
                        .FirstOrDefaultAsync(a => a.BangumiId == dto.BangumiId);
                    if (existingAnime == null)
                        return StatusCode(503, Result<FavoriteDto>.Fail(503, "同步失败"));
                    targetAnimeId = existingAnime.Id;
                }
                targetAnimeId = newAnime.Id;
            }
        }

        // 检查是否已收藏
        var alreadyFavorited = await _context.Favorites
            .AnyAsync(f => f.UserId == userId.Value && f.AnimeId == targetAnimeId);
        if (alreadyFavorited)
            return Conflict(Result<FavoriteDto>.Fail(409, "已收藏过该动漫"));

        var favorite = new Favorite
        {
            UserId = userId.Value,
            AnimeId = targetAnimeId,
            Status = FavoriteStatus.WantToWatch,
            Progress = 0,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow
        };

        _context.Favorites.Add(favorite);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(Result<FavoriteDto>.Fail(409, "已收藏过该动漫"));
        }

        await _context.Entry(favorite).Reference(f => f.Anime).LoadAsync();
        var resultDto = MapToDto(favorite);
        return Ok(Result<FavoriteDto>.Success(resultDto, "收藏成功"));
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

    /// <summary>
    /// 映射到 DTO
    /// </summary>
    private static FavoriteDto MapToDto(Favorite favorite)
    {
        return new FavoriteDto
        {
            Id = favorite.Id,
            AnimeId = favorite.AnimeId,
            AnimeName = favorite.Anime?.Name ?? string.Empty,
            AnimeCover = favorite.Anime?.Cover,
            AnimeType = favorite.Anime?.AnimeType ?? string.Empty,
            AnimeTotalEpisodes = favorite.Anime?.TotalEpisodes,
            Status = favorite.Status,
            Progress = favorite.Progress,
            Rating = favorite.Rating,   // F3 新增
            CreateTime = favorite.CreateTime,
            UpdateTime = favorite.UpdateTime
        };
    }

    /// <summary>
    /// 映射 Bangumi platform 到我们的 AnimeType
    /// </summary>
    private static string MapBangumiPlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
            return "TV";

        return platform switch
        {
            "TV" => "TV",
            "OVA" => "OVA",
            "WEB" => "WEB",
            "剧场版" => "剧场版",
            "动画" => "TV",
            _ => "TV"
        };
    }
}
