using System.Text.Json;
using ManWei.Api.Data;
using ManWei.Api.Services.Recommendation;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

public class PcAiAgentService : BaseAiAgentService
{
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly IRecommendAnimeService _recommendService;
    private readonly ILogger<PcAiAgentService> _pcLogger;
    private int? _userId;

    public PcAiAgentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<PcAiAgentService> logger,
        AppDbContext context,
        IBangumiService bangumiService,
        IRecommendAnimeService recommendService)
        : base(httpClientFactory, config, (ILogger)logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _recommendService = recommendService;
        _pcLogger = logger;
    }

    public void SetUserId(int userId) => _userId = userId;

    public string SystemPrompt => """
        你是漫味（英文名 ManWei）PC 端的私人追番顾问助手，与微信小程序端共享同一份用户数据。
        与小程序端的"轻量闲聊"定位不同，PC 端助手更偏向专业数据查询：

        - 用户可能在分析自己的追番习惯、情绪分布、年度总结
        - 用户可能在寻找"和 XX 类似"的番剧
        - 用户可能想深入了解某部番的情绪曲线

        你拥有 5 个工具可以查询用户数据：
        - query_my_favorites: 查询我的收藏(支持状态筛选: 0=想看 1=在看 2=看过)
        - query_user_stats: 查询我的追番统计(总数/时长/评分分布)
        - query_anime_emotion_curve: 查询某部番(按 animeId)我的情绪曲线数据
        - search_anime: 按关键词搜索动漫(调 Bangumi API)
        - recommend_anime: 基于高分番 + 情绪画像 + Bangumi 搜索的个性化推荐(返回 top5)

        使用工具时要主动、简洁，不要过度调用。
        回答要有数据支撑，但避免堆砌数字。
        """;

    protected override string AgentSystemPrompt => SystemPrompt;

    protected override IEnumerable<object> GetTools() =>
        PcAiTools.AllTools.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonDocument.Parse(t.Parameters).RootElement
            }
        }).ToList();

    protected override async Task<string> ExecuteToolAsync(
        string name, Dictionary<string, object?> args, CancellationToken ct)
    {
        if (_userId == null) return """{"error":"unauthenticated"}""";

        return name switch
        {
            "query_my_favorites" => await QueryMyFavoritesAsync(args, ct),
            "query_user_stats" => await QueryUserStatsAsync(ct),
            "query_anime_emotion_curve" => await QueryEmotionCurveAsync(args, ct),
            "search_anime" => await SearchAnimeAsync(args, ct),
            "recommend_anime" => await RecommendAnimeAsync(args, ct),
            _ => """{"error":"unknown_tool"}"""
        };
    }

    private async Task<string> SearchAnimeAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        var keyword = GetString(args, "keyword");
        if (string.IsNullOrWhiteSpace(keyword))
            return """{"error":"keyword required"}""";

        var limit = GetInt(args, "limit", 10);
        if (limit < 1) limit = 1;
        if (limit > 25) limit = 25;

        var hits = await _bangumiService.SearchAsync(keyword, limit);

        return JsonSerializer.Serialize(new
        {
            keyword,
            count = hits.Count,
            items = hits.Select(h => new
            {
                h.Id,
                Name = !string.IsNullOrWhiteSpace(h.NameCn) ? h.NameCn : h.Name,
                h.Summary,
                Cover = h.Images?.Large ?? h.Images?.Medium ?? h.Images?.Common ?? h.Images?.Small,
                Score = h.Rating?.Score ?? h.Score,
                Tags = h.Tags?.OrderByDescending(t => t.Count).Take(5).Select(t => t.Name).ToList()
            })
        });
    }

    private async Task<string> RecommendAnimeAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        var req = new RecommendRequest
        {
            Keyword = GetString(args, "keyword"),
            AnimeType = GetString(args, "animeType"),
            TopK = GetInt(args, "topK", 5),
            Deterministic = GetBool(args, "deterministic")
        };
        if (req.TopK < 1) req.TopK = 5;
        if (req.TopK > 20) req.TopK = 20;

        var result = await _recommendService.RecommendAsync(_userId!.Value, req, ct);
        // 包装为标准 Result<T> JSON，与 REST 接口 /api/recommendations 一致
        var wrapped = new { code = 200, message = "操作成功", data = result, isSuccess = true };
        var json = JsonSerializer.Serialize(wrapped);
        _pcLogger.LogDebug("[RECOMMEND-TOOL] userId={UserId} mode={Mode} topK={TopK} names=[{Names}]",
            _userId, result.Mode, req.TopK, string.Join(", ", result.Items.Select(i => i.Name)));
        return json;
    }

    private async Task<string> QueryMyFavoritesAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        // 使用 BaseAiAgentService.GetInt 而非 Convert.ToInt32——参数值是 JsonElement 类型
        var hasStatus = args.TryGetValue("status", out var s) && s != null;
        var status = hasStatus ? GetInt(args, "status", -1) : (int?)null;
        var take = GetInt(args, "take", 10);

        var query = _context.Favorites
            .Where(f => f.UserId == _userId);
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);

        var items = await query
            .OrderByDescending(f => f.CreateTime)
            .Take(take)
            .Select(f => new
            {
                f.Id,
                f.AnimeId,
                AnimeName = f.Anime != null ? f.Anime.Name : "(未知)",
                f.Status,
                f.Progress,
                f.Rating
            })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { count = items.Count, items });
    }

    private async Task<string> QueryUserStatsAsync(CancellationToken ct)
    {
        var userId = _userId!.Value;
        var total = await _context.Favorites.CountAsync(f => f.UserId == userId, ct);
        var watching = await _context.Favorites
            .CountAsync(f => f.UserId == userId && f.Status == 1, ct);
        var watched = await _context.Favorites
            .CountAsync(f => f.UserId == userId && f.Status == 2, ct);

        // AverageAsync throws InvalidOperationException on empty set — handle gracefully
        double? avgRating = null;
        if (await _context.Favorites.AnyAsync(f => f.UserId == userId && f.Rating != null, ct))
        {
            avgRating = await _context.Favorites
                .Where(f => f.UserId == userId && f.Rating != null)
                .Select(f => (double?)f.Rating)
                .AverageAsync(ct);
        }

        return JsonSerializer.Serialize(new
        {
            total,
            watching,
            watched,
            avgRating
        });
    }

    private async Task<string> QueryEmotionCurveAsync(
        Dictionary<string, object?> args, CancellationToken ct)
    {
        // Reuse base helper for robust int parsing (handles JsonElement / string / number)
        var animeId = GetInt(args, "animeId", 0);
        if (animeId <= 0) return """{"error":"animeId required"}""";

        var userId = _userId!.Value;
        var fav = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.AnimeId == animeId, ct);
        if (fav == null) return """{"error":"not_favorited"}""";

        var points = await _context.EmotionRecords
            .Where(e => e.FavoriteId == fav.Id)
            .OrderBy(e => e.Episode)
            .Select(e => new { e.Episode, e.EmotionLevel })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new
        {
            animeId,
            favoriteId = fav.Id,
            pointCount = points.Count,
            points
        });
    }
}
