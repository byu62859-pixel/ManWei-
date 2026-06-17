using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;
using ManWei.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// 候选池构建器（双源并集 + BangumiId 去重 + 排除已收藏）。
/// 来源 A: 本地 Anime 表（按 AnimeType 过滤，排除该用户所有状态的 Favorite）。
/// 来源 B: Bangumi 搜索（仅当 req.Keyword 非空时拉取，按 BangumiId 去重后并入）。
/// 临时 Anime（来源 B）不写库，仅供 Scorer 打分使用。
/// </summary>
public class CandidatePoolBuilder
{
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;

    public CandidatePoolBuilder(AppDbContext context, IBangumiService bangumiService)
    {
        _context = context;
        _bangumiService = bangumiService;
    }

    public async Task<List<Candidate>> BuildAsync(
        int userId,
        RecommendRequest req,
        CancellationToken ct = default)
    {
        var candidates = new List<Candidate>();
        var seenBangumiIds = new HashSet<int>();

        // ===== 来源 A：本地 Anime =====
        var query = _context.Anime
            .Include(a => a.AnimeTags)
            .Where(a => !_context.Favorites.Any(fav => fav.UserId == userId && fav.AnimeId == a.Id));

        if (!string.IsNullOrWhiteSpace(req.AnimeType))
        {
            query = query.Where(a => a.AnimeType == req.AnimeType);
        }

        var localAnimes = await query
            .OrderByDescending(a => a.BangumiRatingCount ?? 0)
            .ThenByDescending(a => a.BangumiScore ?? 0)
            .Take(50)
            .ToListAsync(ct);

        foreach (var a in localAnimes)
        {
            if (a.BangumiId.HasValue)
            {
                seenBangumiIds.Add(a.BangumiId.Value);
            }

            candidates.Add(new Candidate
            {
                LocalAnimeId = a.Id,
                BangumiId = a.BangumiId,
                Name = a.Name,
                Cover = a.Cover,
                AnimeType = a.AnimeType,
                BangumiScore = a.BangumiScore,
                Tags = a.AnimeTags.ToList()
            });
        }

        // ===== 来源 B：Bangumi 搜索（仅当 req.Keyword 非空） =====
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            // SearchAsync 内部已处理限流返回空 list，调用方无需 try/catch
            var bangumiHits = await _bangumiService.SearchAsync(req.Keyword, limit: 15);

            foreach (var subject in bangumiHits)
            {
                // BangumiId 去重：已存在 A 集合则跳过
                if (seenBangumiIds.Contains(subject.Id))
                {
                    continue;
                }
                seenBangumiIds.Add(subject.Id);

                // Tags：取 Top-5 by Count desc
                var topTags = (subject.Tags ?? new List<BangumiTagDto>())
                    .OrderByDescending(t => t.Count)
                    .Take(5)
                    .Select(t => new AnimeTag
                    {
                        AnimeId = 0,
                        Name = t.Name,
                        Count = t.Count
                    })
                    .ToList();

                candidates.Add(new Candidate
                {
                    LocalAnimeId = null,
                    BangumiId = subject.Id,
                    Name = !string.IsNullOrWhiteSpace(subject.NameCn) ? subject.NameCn : subject.Name,
                    Cover = subject.Images?.Large ?? subject.Images?.Medium ?? subject.Images?.Common ?? subject.Images?.Small,
                    AnimeType = subject.Platform,
                    BangumiScore = subject.Rating?.Score ?? subject.Score,
                    Tags = topTags
                });
            }
        }

        return candidates;
    }
}

/// <summary>
/// 推荐候选条目（来自候选池构建器，供 Scorer 打分使用）。
/// 来源 A（本地）：LocalAnimeId 有值，Tags 从 DB 加载。
/// 来源 B（Bangumi 搜索）：LocalAnimeId 为 null，Tags 临时构造。
/// </summary>
public class Candidate
{
    /// <summary>来源 A: Anime.Id; 来源 B: null</summary>
    public int? LocalAnimeId { get; set; }
    /// <summary>两源都有</summary>
    public int? BangumiId { get; set; }
    public string Name { get; set; } = "";
    public string? Cover { get; set; }
    public string? AnimeType { get; set; }
    public double? BangumiScore { get; set; }
    /// <summary>Source A 从 DB 加载；Source B 从 BangumiSubjectDto.Tags 构造（Count 取 Bangumi count）</summary>
    public List<AnimeTag> Tags { get; set; } = new();
}
