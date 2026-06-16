using System.Text.Json;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

public class WxAiAgentService : BaseAiAgentService
{
    private readonly AppDbContext _context;
    private int _userId;

    private const string SystemPrompt = """
        你叫"漫味小助手"，是一个热情的动漫爱好者，也是用户的私人追番顾问。
        你的专长：
        1. 根据用户的收藏偏好、评分记录、情感标签，推荐他可能感兴趣的动漫，并解释推荐理由
        2. 协助用户撰写、润色观后感，给出情感分析和写作建议
        3. 回答用户关于追番进度、收藏统计、情感曲线等个人数据的问题
        4. 帮助用户发现新番

        回答要求：
        - 口语化、亲切自然，像朋友聊天
        - 推荐时给出具体动漫名称和简单理由
        - 不使用 Markdown 加粗（**）、标题（#）等符号，用自然语言和 emoji 表达重点
        - 适当使用 emoji 表情增加亲切感，比如推荐动漫时用 🎬✨，表达惊喜用 😱🔥，鼓励用 💪，询问用 🤔，开心聊天用 😄👀，情感共鸣用 🥺💕，但不要每句话都加，自然融入即可
        - 不知道答案时诚实告知

        数据使用原则：
        - 需要数据时调用工具获取，不凭空编造
        - 推荐类问题：先调 get_my_favorites + get_my_stats 了解用户偏好，再调 search_anime 搜索候选集，AI 自己分析给出推荐
        """;

    public WxAiAgentService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<WxAiAgentService> logger)
        : base(httpClientFactory, config, (ILogger)logger)
    {
        _context = context;
    }

    protected override string AgentSystemPrompt => SystemPrompt;

    protected override IEnumerable<object> GetTools() => WxPredefinedTools.AllTools.Select(t => (object)new
    {
        type = "function",
        function = new
        {
            name = t.Name,
            description = t.Description,
            parameters = JsonDocument.Parse(t.Parameters).RootElement
        }
    });

    public async Task<WxChatResponseDto> ChatAsync(WxChatRequestDto request, int userId, CancellationToken ct)
    {
        _userId = userId;
        _logger.LogInformation("[WxAI] 收到聊天请求，用户: {UserId}, 消息: {Message}", userId, request.Message);

        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt },
            new { role = "user", content = request.Message }
        };

        var result = await CallDeepSeekAsync(messages, ct);
        var content = ExtractContent(result);
        _logger.LogInformation("[WxAI] 最终结果长度: {Length}, 内容: {Content}", content.Length, content);
        return new WxChatResponseDto { Answer = content };
    }

    protected override async Task<string> ExecuteToolAsync(string name, Dictionary<string, object?> args, CancellationToken ct)
    {
        return name switch
        {
            "get_my_favorites" => await GetMyFavoritesAsync(args, ct),
            "get_my_reviews" => await GetMyReviewsAsync(args, ct),
            "get_my_stats" => await GetMyStatsAsync(ct),
            "get_my_emotion_curves" => await GetMyEmotionCurvesAsync(args, ct),
            "get_anime_detail" => await GetAnimeDetailAsync(args, ct),
            "search_anime" => await SearchAnimeAsync(args, ct),
            _ => "{}"
        };
    }

    private async Task<string> GetMyFavoritesAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var page = GetInt(args, "page", 1);
        var pageSize = GetInt(args, "pageSize", 20);
        var status = GetInt(args, "status", -1);
        var keyword = GetString(args, "keyword");

        var query = _context.Favorites
            .Include(f => f.Anime)
            .Where(f => f.UserId == _userId)
            .AsQueryable();

        if (status >= 0)
            query = query.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(f => f.Anime != null && f.Anime.Name.Contains(keyword));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(f => f.UpdateTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new { f.Id, f.Status, f.Progress, f.Rating, 动漫名称 = f.Anime!.Name, 封面 = f.Anime.Cover })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }

    private async Task<string> GetMyReviewsAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var page = GetInt(args, "page", 1);
        var pageSize = GetInt(args, "pageSize", 10);
        var keyword = GetString(args, "keyword");

        var query = _context.Reviews
            .Include(r => r.Favorite).ThenInclude(f => f!.Anime)
            .Where(r => r.Favorite!.UserId == _userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(r => r.Content != null && r.Content.Contains(keyword));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.UpdateTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new { r.Id, 动漫名称 = r.Favorite!.Anime!.Name, 内容摘要 = r.Content != null && r.Content.Length > 60 ? r.Content.Substring(0, 60) + "..." : r.Content ?? "", r.UpdateTime })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }

    private async Task<string> GetMyStatsAsync(CancellationToken ct)
    {
        var total = await _context.Favorites.CountAsync(f => f.UserId == _userId, ct);
        var byStatus = await _context.Favorites.Where(f => f.UserId == _userId).GroupBy(f => f.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        var totalEpisodes = await _context.Favorites.Where(f => f.UserId == _userId && f.Progress != null).SumAsync(f => f.Progress, ct);
        var avgRating = await _context.Favorites.Where(f => f.UserId == _userId && f.Rating != null).AverageAsync(f => (double?)f.Rating, ct);
        var reviewCount = await _context.Reviews.Include(r => r.Favorite).CountAsync(r => r.Favorite!.UserId == _userId, ct);

        return JsonSerializer.Serialize(new { 总收藏数 = total, 各状态分布 = byStatus, 总集数 = totalEpisodes, 平均评分 = avgRating, 观后感数 = reviewCount });
    }

    private async Task<string> GetMyEmotionCurvesAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var animeId = GetString(args, "animeId");

        List<int> favoriteIds;
        if (!string.IsNullOrWhiteSpace(animeId) && int.TryParse(animeId, out var aid))
        {
            favoriteIds = await _context.Favorites
                .Where(f => f.UserId == _userId && f.AnimeId == aid)
                .Select(f => f.Id)
                .ToListAsync(ct);
        }
        else
        {
            favoriteIds = await _context.Favorites
                .Where(f => f.UserId == _userId)
                .OrderByDescending(f => f.UpdateTime)
                .Take(5)
                .Select(f => f.Id)
                .ToListAsync(ct);
        }

        if (favoriteIds.Count == 0)
            return JsonSerializer.Serialize(new { items = new object[] { } });

        var records = await _context.EmotionRecords
            .Where(r => favoriteIds.Contains(r.FavoriteId))
            .Include(r => r.Favorite!).ThenInclude(f => f!.Anime)
            .OrderBy(r => r.CreateTime)
            .Select(r => new { 动漫名称 = r.Favorite!.Anime!.Name, Episode = r.Episode, EmotionLevel = r.EmotionLevel })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(records);
    }

    private async Task<string> GetAnimeDetailAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var animeId = GetString(args, "animeId");
        if (!int.TryParse(animeId, out var id))
            return "{}";

        var anime = await _context.Anime
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Name, a.Cover, a.Summary, a.AnimeType })
            .FirstOrDefaultAsync(ct);

        return anime == null ? "{}" : JsonSerializer.Serialize(anime);
    }

    private async Task<string> SearchAnimeAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var page = GetInt(args, "page", 1);
        var pageSize = GetInt(args, "pageSize", 20);
        var keyword = GetString(args, "keyword");
        var animeType = GetString(args, "animeType");

        var query = _context.Anime.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(a => a.Name.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(animeType))
            query = query.Where(a => a.AnimeType == animeType);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreateTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.Name, a.Cover, a.Summary, a.AnimeType })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }
}
