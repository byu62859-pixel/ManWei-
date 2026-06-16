using System.ComponentModel.DataAnnotations;
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
/// 动漫管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AnimeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly ILogger<AnimeController> _logger;

    public AnimeController(AppDbContext context, IBangumiService bangumiService,
        ILogger<AnimeController> logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _logger = logger;
    }

    /// <summary>
    /// 获取动漫列表（分页+搜索）
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>分页动漫列表</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<PagedResult<AnimeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<AnimeDto>>>> GetList([FromQuery] AnimeQueryDto query)
    {
        var queryable = _context.Anime.AsQueryable();

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(a => a.Name.Contains(query.Keyword));
        }

        // 类型筛选
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            queryable = queryable.Where(a => a.AnimeType == query.Type);
        }

        // 标签筛选（首页标签栏，预置标签）
        if (!string.IsNullOrWhiteSpace(query.TagName))
        {
            var tagName = query.TagName.Trim();
            queryable = queryable.Where(a =>
                _context.EmotionTags.Any(t =>
                    t.AnimeId == a.Id &&
                    t.UserId == null &&
                    t.Name == tagName
                ));
        }

        var totalCount = await queryable.CountAsync();

        var items = await queryable
            .OrderByDescending(a => a.CreateTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AnimeDto
            {
                Id = a.Id,
                BangumiId = a.BangumiId,
                Name = a.Name,
                Cover = a.Cover,
                Summary = a.Summary,
                AnimeType = a.AnimeType,
                TotalEpisodes = a.TotalEpisodes,
                AirDate = a.AirDate,
                Duration = a.Duration,
                Producer = a.Producer,
                Director = a.Director,
                BangumiScore = a.BangumiScore,
                BangumiRank = a.BangumiRank,
                BangumiRatingCount = a.BangumiRatingCount,
                CreateTime = a.CreateTime,
                FavoriteCount = _context.Favorites.Count(f => f.AnimeId == a.Id),
                AvgRating = _context.Favorites
                    .Where(f => f.AnimeId == a.Id && f.Rating != null)
                    .Average(f => (double?)f.Rating),
                ReviewCount = _context.Reviews.Count(r => r.Favorite.AnimeId == a.Id)
            })
            .ToListAsync();

        var result = new PagedResult<AnimeDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(Result<PagedResult<AnimeDto>>.Success(result));
    }

    /// <summary>
    /// 获取单个动漫详情
    /// </summary>
    /// <param name="id">动漫ID</param>
    /// <returns>动漫详情</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<AnimeDto>>> GetById(int id)
    {
        var anime = await _context.Anime
            .Include(a => a.AnimeTags)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (anime == null)
        {
            return NotFound(Result<AnimeDto>.Fail(404, "动漫不存在"));
        }

        // 懒拉取条件：有 BangumiId + 元信息字段都为空
        if (anime.BangumiId.HasValue &&
            (anime.AirDate == null || anime.Producer == null || anime.BangumiScore == null))
        {
            try
            {
                await _bangumiService.RefetchAnimeMetadataAsync(anime.Id);
                // Detach + 重新查询，强制刷新当前 DbContext
                _context.Entry(anime).State = EntityState.Detached;
                anime = await _context.Anime
                    .Include(a => a.AnimeTags)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (anime == null)
                {
                    return NotFound(Result<AnimeDto>.Fail(404, "动漫不存在"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "懒拉取后刷新失败 AnimeId={Id}", id);
                // 用旧对象继续返回
            }
        }

        var dto = new AnimeDto
        {
            Id = anime.Id,
            BangumiId = anime.BangumiId,
            Name = anime.Name,
            Cover = anime.Cover,
            Summary = anime.Summary,
            AnimeType = anime.AnimeType,
            TotalEpisodes = anime.TotalEpisodes,
            AirDate = anime.AirDate,
            Duration = anime.Duration,
            Producer = anime.Producer,
            Director = anime.Director,
            BangumiScore = anime.BangumiScore,
            BangumiRank = anime.BangumiRank,
            BangumiRatingCount = anime.BangumiRatingCount,
            Tags = anime.AnimeTags?.Select(t => new AnimeTagDto
            {
                Name = t.Name,
                Count = t.Count
            }).ToList() ?? new(),
            CreateTime = anime.CreateTime,
            FavoriteCount = _context.Favorites.Count(f => f.AnimeId == anime.Id),
            AvgRating = _context.Favorites
                .Where(f => f.AnimeId == anime.Id && f.Rating != null)
                .Average(f => (double?)f.Rating),
            ReviewCount = _context.Reviews.Count(r => r.Favorite.AnimeId == anime.Id)
        };

        return Ok(Result<AnimeDto>.Success(dto));
    }

    /// <summary>
    /// 从 Bangumi 一键同步动漫数据
    /// </summary>
    /// <param name="bangumiId">Bangumi 条目ID</param>
    /// <returns>同步后的动漫信息</returns>
    [HttpPost("Sync/{bangumiId}")]
    [Authorize]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<AnimeDto>>> Sync(int bangumiId)
    {
        if (bangumiId <= 0)
        {
            return BadRequest(Result<AnimeDto>.Fail(400, "Bangumi ID 无效"));
        }

        // 检查是否已存在
        var existing = await _context.Anime.FirstOrDefaultAsync(a => a.BangumiId == bangumiId);
        if (existing != null)
        {
            // 已存在则更新
            var result = await _bangumiService.GetAndMapAnimeAsync(bangumiId);
            if (result == null)
            {
                return BadRequest(Result<AnimeDto>.Fail(400, "从 Bangumi 获取数据失败"));
            }

            var (anime, _) = result.Value;

            existing.Name = anime.Name;
            existing.Summary = anime.Summary;
            existing.Cover = anime.Cover;
            existing.AnimeType = anime.AnimeType;

            // 拉取总集数（覆盖策略：仅在 null/0 时覆盖）
            try
            {
                var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(bangumiId);
                if (totalEpisodes.HasValue)
                {
                    if (existing.TotalEpisodes is null or 0)
                    {
                        existing.TotalEpisodes = totalEpisodes.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync 拉取总集数异常 BangumiId={BangumiId}", bangumiId);
            }

            await _context.SaveChangesAsync();

            var dto = new AnimeDto
            {
                Id = existing.Id,
                BangumiId = existing.BangumiId,
                Name = existing.Name,
                Cover = existing.Cover,
                Summary = existing.Summary,
                AnimeType = existing.AnimeType,
                TotalEpisodes = existing.TotalEpisodes,
                CreateTime = existing.CreateTime
            };

            return Ok(Result<AnimeDto>.Success(dto, "同步并更新成功"));
        }

        // 新增
        var syncResult = await _bangumiService.GetAndMapAnimeAsync(bangumiId);
        if (syncResult == null)
        {
            return BadRequest(Result<AnimeDto>.Fail(400, "从 Bangumi 获取数据失败，请检查 ID 是否正确"));
        }

        var (newAnime, tags) = syncResult.Value;

        _context.Anime.Add(newAnime);
        await _context.SaveChangesAsync();

        if (tags.Any())
        {
            foreach (var t in tags) t.AnimeId = newAnime.Id;
            await _context.AnimeTags.AddRangeAsync(tags);
            await _context.SaveChangesAsync();
        }

        var newDto = new AnimeDto
        {
            Id = newAnime.Id,
            BangumiId = newAnime.BangumiId,
            Name = newAnime.Name,
            Cover = newAnime.Cover,
            Summary = newAnime.Summary,
            AnimeType = newAnime.AnimeType,
            CreateTime = newAnime.CreateTime
        };

        return Ok(Result<AnimeDto>.Success(newDto, "同步成功"));
    }

    /// <summary>
    /// 批量回填所有 TotalEpisodes 为空的动漫（管理员）
    /// </summary>
    /// <returns>回填统计</returns>
    [HttpPost("admin/backfill-episodes")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<BackfillEpisodesResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<BackfillEpisodesResponseDto>>> BackfillEpisodes()
    {
        var resp = new BackfillEpisodesResponseDto();

        // 1) 先统计 BangumiId 为 null 的行（这些没法调 Bangumi，跳过）
        //    TotalEpisodes 遵循 "null or 0 = 未拉取到" 语义（见 Anime.cs 注释），故守卫用 is null or 0。
        resp.Skipped = await _context.Anime
            .CountAsync(a => a.BangumiId == null && (a.TotalEpisodes == null || a.TotalEpisodes == 0));

        // 2) 取出所有 BangumiId != null 且 TotalEpisodes 为 null/0 的行
        //    必须 ToList 一次性物化，避免每次迭代重新执行 LINQ（N+1 / 上下文生命周期问题）
        //    与 Anime.cs 中 "null or 0 = 未拉取到" 语义保持一致
        var candidates = await _context.Anime
            .Where(a => a.BangumiId != null && (a.TotalEpisodes == null || a.TotalEpisodes == 0))
            .ToListAsync();

        resp.Scanned = candidates.Count;
        _logger.LogInformation("回填 TotalEpisodes 启动：候选 {Count} 行", resp.Scanned);

        foreach (var anime in candidates)
        {
            // 防御性二次检查（LINQ 已经过滤 BangumiId != null，但保持跟 Sync action 的语义一致）
            if (anime.BangumiId == null) continue;

            try
            {
                var total = await _bangumiService.GetEpisodesTotalAsync(anime.BangumiId.Value);
                if (total.HasValue)
                {
                    anime.TotalEpisodes = total.Value;
                    resp.Updated++;
                    resp.UpdatedAnimeIds.Add(anime.Id);
                }
                else
                {
                    // GetEpisodesTotalAsync 内部已识别限速拒绝（_rateLimiter 返回 false）并 LogWarning
                    // controller 层无法区分"限速"和"真没数据"，统一计入 failed；
                    // 操作员如需细分看 BangumiService 的 logger（限速时会有 LogWarning）
                    resp.Failed++;
                    if (resp.Errors.Count < 20)
                        resp.Errors.Add($"BangumiId={anime.BangumiId}: 返回 null（限速或无数据）");
                }
            }
            catch (Exception ex)
            {
                resp.Failed++;
                _logger.LogWarning(ex, "回填总集数异常 AnimeId={AnimeId} BangumiId={BangumiId}",
                    anime.Id, anime.BangumiId);
                if (resp.Errors.Count < 20)
                    resp.Errors.Add($"BangumiId={anime.BangumiId}: {ex.GetType().Name}");
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "回填 TotalEpisodes 完成：scanned={Scanned} updated={Updated} failed={Failed} skipped={Skipped}",
            resp.Scanned, resp.Updated, resp.Failed, resp.Skipped);

        return Ok(Result<BackfillEpisodesResponseDto>.Success(resp,
            $"回填完成：更新 {resp.Updated}/{resp.Scanned}，失败 {resp.Failed}"));
    }

    /// <summary>
    /// 管理员手动添加动漫
    /// </summary>
    /// <param name="dto">动漫信息</param>
    /// <returns>添加的动漫</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<AnimeDto>>> Create([FromBody] CreateAnimeDto dto)
    {
        var anime = new Anime
        {
            Name = dto.Name,
            Cover = dto.Cover,
            Summary = dto.Summary,
            AnimeType = dto.AnimeType ?? "TV",
            CreateTime = DateTime.UtcNow
        };

        _context.Anime.Add(anime);
        await _context.SaveChangesAsync();

        var resultDto = new AnimeDto
        {
            Id = anime.Id,
            BangumiId = anime.BangumiId,
            Name = anime.Name,
            Cover = anime.Cover,
            Summary = anime.Summary,
            AnimeType = anime.AnimeType,
            CreateTime = anime.CreateTime
        };

        return Ok(Result<AnimeDto>.Success(resultDto, "添加成功"));
    }

    /// <summary>
    /// 管理员更新动漫
    /// </summary>
    /// <param name="id">动漫ID</param>
    /// <param name="dto">更新信息</param>
    /// <returns>更新后的动漫</returns>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<AnimeDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<AnimeDto>>> Update(int id, [FromBody] UpdateAnimeDto dto)
    {
        var anime = await _context.Anime.FindAsync(id);
        if (anime == null)
        {
            return NotFound(Result<AnimeDto>.Fail(404, "动漫不存在"));
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
            anime.Name = dto.Name;
        if (dto.Cover != null)
            anime.Cover = dto.Cover;
        if (dto.Summary != null)
            anime.Summary = dto.Summary;
        if (!string.IsNullOrWhiteSpace(dto.AnimeType))
            anime.AnimeType = dto.AnimeType;

        await _context.SaveChangesAsync();

        var resultDto = new AnimeDto
        {
            Id = anime.Id,
            BangumiId = anime.BangumiId,
            Name = anime.Name,
            Cover = anime.Cover,
            Summary = anime.Summary,
            AnimeType = anime.AnimeType,
            CreateTime = anime.CreateTime
        };

        return Ok(Result<AnimeDto>.Success(resultDto, "更新成功"));
    }

    /// <summary>
    /// 删除动漫
    /// </summary>
    /// <param name="id">动漫ID</param>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int id)
    {
        var anime = await _context.Anime.FindAsync(id);
        if (anime == null)
        {
            return NotFound(Result.Fail(404, "动漫不存在"));
        }

        _context.Anime.Remove(anime);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 批量删除动漫（管理员）。会级联清理所有用户的收藏、观后感、情绪记录，
    /// 情感标签的 AnimeId 会被置空（标签本身不删除）。
    /// 整个 batch 在一个 EF Core 事务里，失败整体回滚，无部分删除。
    /// </summary>
    /// <param name="dto">要删除的动漫 ID 列表（1-200 个）</param>
    /// <returns>删除明细（成功 / 不存在 / 错误）</returns>
    [HttpPost("admin/batch-delete")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<BatchDeleteAnimeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<BatchDeleteAnimeResponseDto>>> BatchDelete(
        [FromBody] BatchDeleteAnimeDto dto)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var adminUserName = User.Identity?.Name ?? "unknown";

        var ids = dto.Ids.Distinct().ToList();
        _logger.LogInformation(
            "[AUDIT] 管理员 {UserId}/{UserName} 请求批量删除 {Count} 个动漫: [{Ids}]",
            adminUserId, adminUserName, ids.Count, string.Join(",", ids));

        var resp = new BatchDeleteAnimeResponseDto
        {
            Requested = ids.Count,
            DeletedAnimeIds = new List<int>(),
            NotFoundAnimeIds = new List<int>(),
            Errors = new List<string>()
        };

        // 一次 query 找出存在的行
        var existing = await _context.Anime
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

        var existingIds = existing.Select(a => a.Id).ToHashSet();
        resp.NotFoundAnimeIds = ids.Where(id => !existingIds.Contains(id)).ToList();
        resp.NotFound = resp.NotFoundAnimeIds.Count;

        if (existing.Count > 0)
        {
            try
            {
                _context.Anime.RemoveRange(existing);
                await _context.SaveChangesAsync();
                resp.DeletedAnimeIds = existing.Select(a => a.Id).ToList();
                resp.Deleted = existing.Count;
            }
            catch (DbUpdateException ex)
            {
                // 单事务失败,整批回滚(EF Core 默认行为),此处只记录明细
                _logger.LogError(ex,
                    "[AUDIT] 批量删除失败（事务回滚）: 管理员 {UserId}/{UserName} 请求删除 [{Ids}]",
                    adminUserId, adminUserName, string.Join(",", ids));
                resp.Errors.Add($"数据库错误: {ex.InnerException?.Message ?? ex.Message}");
                resp.Deleted = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[AUDIT] 批量删除异常: 管理员 {UserId}/{UserName}",
                    adminUserId, adminUserName);
                resp.Errors.Add($"未知错误: {ex.Message}");
                resp.Deleted = 0;
            }
        }

        // 错误截断(防御性,通常不会超过)
        const int maxErrors = 20;
        if (resp.Errors.Count > maxErrors)
        {
            resp.ErrorsTruncated = true;
            resp.Errors = resp.Errors.Take(maxErrors).ToList();
        }

        _logger.LogInformation(
            "批量删除完成: 管理员 {UserId}/{UserName} 请求 {Requested}, 成功 {Deleted}, 不存在 {NotFound}, 错误 {ErrorCount}{Trunc}",
            adminUserId, adminUserName, resp.Requested, resp.Deleted, resp.NotFound, resp.Errors.Count,
            resp.ErrorsTruncated ? " (已截断)" : "");

        return Ok(Result<BatchDeleteAnimeResponseDto>.Success(resp, "批量删除完成"));
    }
}

/// <summary>
/// 创建动漫请求
/// </summary>
public class CreateAnimeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string? AnimeType { get; set; }
}

/// <summary>
/// 更新动漫请求
/// </summary>
public class UpdateAnimeDto
{
    public string? Name { get; set; }
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string? AnimeType { get; set; }
}

/// <summary>
/// 批量回填 TotalEpisodes 响应
/// </summary>
public class BackfillEpisodesResponseDto
{
    /// <summary>扫到的候选行总数（BangumiId != null 且 TotalEpisodes 为 null/0）</summary>
    public int Scanned { get; set; }

    /// <summary>成功写入新 TotalEpisodes 的行数</summary>
    public int Updated { get; set; }

    /// <summary>BangumiId 为 null 且 TotalEpisodes 为 null/0 跳过的行数（不计入 Scanned）</summary>
    public int Skipped { get; set; }

    /// <summary>Bangumi 调用异常/返回 null 的行数</summary>
    public int Failed { get; set; }

    /// <summary>本次成功回填的 Anime.Id 列表</summary>
    public List<int> UpdatedAnimeIds { get; set; } = new();

    /// <summary>最多前 20 条失败的 BangumiId + 原因</summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// 批量删除动漫请求
/// </summary>
public class BatchDeleteAnimeDto
{
    /// <summary>要删除的动漫 ID 列表</summary>
    [Required(ErrorMessage = "请提供要删除的动漫 ID 列表")]
    [MinLength(1, ErrorMessage = "至少选择一个 ID")]
    [MaxLength(200, ErrorMessage = "单次最多删除 200 个动漫,请分批操作")]
    public List<int> Ids { get; set; } = new();
}

/// <summary>
/// 批量删除动漫响应。整批在一个 EF Core 事务里执行,
/// 失败会整体回滚,不会产生"部分删除"的中间状态。
/// </summary>
public class BatchDeleteAnimeResponseDto
{
    /// <summary>请求删除的 ID 数量（去重后）</summary>
    public int Requested { get; set; }

    /// <summary>实际删除的 ID 数量</summary>
    public int Deleted { get; set; }

    /// <summary>数据库中不存在的 ID 数量</summary>
    public int NotFound { get; set; }

    /// <summary>实际删除的 Anime.Id 列表</summary>
    public List<int> DeletedAnimeIds { get; set; } = new();

    /// <summary>数据库中不存在的 Anime.Id 列表</summary>
    public List<int> NotFoundAnimeIds { get; set; } = new();

    /// <summary>最多前 20 条错误信息（数据库 deadlock / 索引冲突等）</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Errors 数组是否被截断（超过 20 条时为 true）</summary>
    public bool ErrorsTruncated { get; set; }
}
