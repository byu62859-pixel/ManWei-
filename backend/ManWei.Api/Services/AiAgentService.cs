using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

public interface IAiAgentService
{
    Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default);
}

public class AiAgentService : BaseAiAgentService, IAiAgentService
{
    private readonly AppDbContext _context;

    private const string SystemPrompt = """
        你是一个数据分析助手，名字叫"漫味小助手"。
        你的职责是用自然语言回答管理员关于系统数据的问题。

        可用工具：
        当需要查询数据时，你必须调用工具，不能凭空编造数据。

        回答原则：
        - 数据统计类回答先给结论，再列明细，用换行分隔层级，不要把所有数字堆在一段话里
        - 列举数据时每条单独一行，格式：emoji + 名称 + 数值，例如：🔥 热血 — 6次
        - 重要结论或关键数字前加 emoji 强调，如 📊 👥 🎬 ⭐ 📝 🏆 等
        - 回答末尾可以加一句简短的数据洞察或建议，帮助管理员理解数据含义
        - 不使用 Markdown 加粗（**）和标题（#），用换行和 emoji 代替层级结构
        - 回答简洁不冗长，重点突出，管理员看一眼就能抓到关键信息
        """;

    public AiAgentService(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AiAgentService> logger)
        : base(httpClientFactory, config, (ILogger)logger)
    {
        _context = context;
    }

    protected override string AgentSystemPrompt => SystemPrompt;

    protected override IEnumerable<object> GetTools() => PredefinedTools.AllTools.Select(t => (object)new
    {
        type = "function",
        function = new
        {
            name = t.Name,
            description = t.Description,
            parameters = JsonDocument.Parse(t.Parameters).RootElement
        }
    }).ToList();

    public async Task<ChatResponseDto> ChatAsync(ChatRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("[AI] 收到聊天请求，消息: {Message}", request.Message);

        var messages = new List<object>
        {
            new { role = "system", content = AgentSystemPrompt },
            new { role = "user", content = request.Message }
        };

        var result = await CallDeepSeekAsync(messages, ct);
        var content = ExtractContent(result);
        _logger.LogInformation("[AI] 最终结果长度: {Length}", content.Length);
        return ParseResponse(content);
    }

    protected override async Task<string> ExecuteToolAsync(string name, Dictionary<string, object?> args, CancellationToken ct)
    {
        return name switch
        {
            "get_user_stats" => await GetUserStatsAsync(ct),
            "get_user_list" => await GetUserListAsync(args, ct),
            "get_user_growth" => await GetUserGrowthAsync(args, ct),
            "get_anime_stats" => await GetAnimeStatsAsync(ct),
            "get_anime_list" => await GetAnimeListAsync(args, ct),
            "get_anime_rank" => await GetAnimeRankAsync(args, ct),
            "get_favorite_stats" => await GetFavoriteStatsAsync(ct),
            "get_favorite_stats_by_anime" => await GetFavoriteStatsByAnimeAsync(args, ct),
            "get_tag_stats" => await GetTagStatsAsync(args, ct),
            "get_tag_wordcloud" => await GetTagWordCloudAsync(ct),
            "get_review_stats" => await GetReviewStatsAsync(ct),
            "get_review_list" => await GetReviewListAsync(args, ct),
            "get_emotion_curve_stats" => await GetEmotionCurveStatsAsync(ct),
            _ => "{}"
        };
    }

    private static List<AiToolCall> ParseToolCalls(string content)
    {
        var calls = new List<AiToolCall>();
        var regex = new Regex(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        foreach (Match match in regex.Matches(content))
        {
            try
            {
                var obj = JsonDocument.Parse(match.Groups[1].Value).RootElement;
                if (obj.TryGetProperty("tool", out var toolName))
                {
                    var toolNameStr = toolName.ValueKind == JsonValueKind.String ? toolName.GetString() ?? "" : "";
                    calls.Add(new AiToolCall
                    {
                        Name = toolNameStr,
                        Arguments = obj.TryGetProperty("arguments", out var args)
                            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(args.GetRawText()) ?? new()
                            : new()
                    });
                }
            }
            catch { }
        }
        return calls;
    }

    private async Task<List<string>> ExecuteToolsAsync(List<AiToolCall> calls, CancellationToken ct)
    {
        var results = new List<string>();
        foreach (var call in calls)
        {
            var result = call.Name switch
            {
                "get_user_stats" => await GetUserStatsAsync(ct),
                "get_user_list" => await GetUserListAsync(call.Arguments, ct),
                "get_user_growth" => await GetUserGrowthAsync(call.Arguments, ct),
                "get_anime_stats" => await GetAnimeStatsAsync(ct),
                "get_anime_list" => await GetAnimeListAsync(call.Arguments, ct),
                "get_anime_rank" => await GetAnimeRankAsync(call.Arguments, ct),
                "get_favorite_stats" => await GetFavoriteStatsAsync(ct),
                "get_favorite_stats_by_anime" => await GetFavoriteStatsByAnimeAsync(call.Arguments, ct),
                "get_tag_stats" => await GetTagStatsAsync(call.Arguments, ct),
                "get_tag_wordcloud" => await GetTagWordCloudAsync(ct),
                "get_review_stats" => await GetReviewStatsAsync(ct),
                "get_emotion_curve_stats" => await GetEmotionCurveStatsAsync(ct),
                _ => "{}"
            };
            results.Add(result);
        }
        return results;
    }

    private async Task<string> GetUserStatsAsync(CancellationToken ct)
    {
        var total = await _context.Users.CountAsync(ct);
        var disabled = await _context.Users.CountAsync(u => !u.IsEnabled, ct);
        var admins = await _context.Users.CountAsync(u => u.Role == "Admin", ct);
        var todayNew = await _context.Users.CountAsync(u => u.CreateTime >= DateTime.UtcNow.Date, ct);
        return JsonSerializer.Serialize(new { 总用户数 = total, 今日新增用户 = todayNew, 禁用用户数 = disabled, 管理员数 = admins, 普通用户数 = total - admins });
    }

    private async Task<string> GetUserListAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var page = GetInt(args, "page", 1);
        var pageSize = GetInt(args, "pageSize", 20);
        var keyword = GetString(args, "keyword");
        var status = GetString(args, "status") ?? "all";

        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(u => u.NickName != null && u.NickName.Contains(keyword));
        if (status == "enabled")
            query = query.Where(u => u.IsEnabled);
        else if (status == "disabled")
            query = query.Where(u => !u.IsEnabled);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreateTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.NickName, u.Avatar, u.Role, u.IsEnabled, 创建时间 = u.CreateTime })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }

    private async Task<string> GetUserGrowthAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var days = GetInt(args, "days", 30);
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        var data = await _context.Users
            .Where(u => u.CreateTime >= startDate)
            .GroupBy(u => u.CreateTime.Date)
            .Select(g => new { date = g.Key, count = g.Count() })
            .OrderBy(x => x.date)
            .ToListAsync(ct);
        return JsonSerializer.Serialize(data);
    }

    private async Task<string> GetAnimeStatsAsync(CancellationToken ct)
    {
        var total = await _context.Anime.CountAsync(ct);
        var todayNew = await _context.Anime.CountAsync(a => a.CreateTime >= DateTime.UtcNow.Date, ct);
        var byType = await _context.Anime.GroupBy(a => a.AnimeType).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync(ct);
        return JsonSerializer.Serialize(new { 总动漫数 = total, 今日新增动漫 = todayNew, 各类型分布 = byType });
    }

    private async Task<string> GetAnimeListAsync(Dictionary<string, object?> args, CancellationToken ct)
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
            .Select(a => new { a.Id, a.Name, a.AnimeType, a.Cover, a.Summary, 创建时间 = a.CreateTime })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }

    private async Task<string> GetAnimeRankAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var limit = GetInt(args, "limit", 10);
        var data = await _context.Favorites
            .GroupBy(f => f.AnimeId)
            .Select(g => new { animeId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(limit)
            .Join(_context.Anime, f => f.animeId, a => a.Id, (f, a) => new { a.Name, 收藏数 = f.count })
            .ToListAsync(ct);
        return JsonSerializer.Serialize(data);
    }

    private async Task<string> GetFavoriteStatsAsync(CancellationToken ct)
    {
        var total = await _context.Favorites.CountAsync(ct);
        var byStatus = await _context.Favorites.GroupBy(f => f.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        return JsonSerializer.Serialize(new { 总收藏数 = total, 各状态分布 = byStatus });
    }

    private async Task<string> GetFavoriteStatsByAnimeAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var animeId = GetInt(args, "animeId", -1);
        if (animeId == -1) return "{}";
        var total = await _context.Favorites.CountAsync(f => f.AnimeId == animeId, ct);
        var byStatus = await _context.Favorites.Where(f => f.AnimeId == animeId).GroupBy(f => f.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        var avgRating = await _context.Favorites.Where(f => f.AnimeId == animeId && f.Rating != null).AverageAsync(f => (double?)f.Rating, ct);
        return JsonSerializer.Serialize(new { animeId, 总收藏数 = total, 各状态分布 = byStatus, 平均评分 = avgRating });
    }

    private async Task<string> GetTagStatsAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var limit = GetInt(args, "limit", 10);
        var todayNew = await _context.EmotionTags.CountAsync(t => t.CreateTime >= DateTime.UtcNow.Date, ct);
        var data = await _context.EmotionTags
            .GroupBy(t => t.Name)
            .Select(g => new { tag = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(limit)
            .ToListAsync(ct);
        return JsonSerializer.Serialize(new { 总数 = data.Sum(x => x.count), 今日新增标签 = todayNew, 热门标签 = data });
    }

    private async Task<string> GetTagWordCloudAsync(CancellationToken ct)
    {
        var data = await _context.EmotionTags
            .Where(t => t.IsPreset || t.UserId != null)
            .GroupBy(t => t.Name)
            .Select(g => new { name = g.Key, value = g.Count() })
            .OrderByDescending(x => x.value)
            .ToListAsync(ct);
        return JsonSerializer.Serialize(data);
    }

    private async Task<string> GetReviewStatsAsync(CancellationToken ct)
    {
        var total = await _context.Reviews.CountAsync(ct);
        var today = await _context.Reviews.CountAsync(r => r.CreateTime >= DateTime.UtcNow.Date, ct);
        return JsonSerializer.Serialize(new { 总观后感数 = total, 今日新增 = today });
    }

    private async Task<string> GetReviewListAsync(Dictionary<string, object?> args, CancellationToken ct)
    {
        var page = GetInt(args, "page", 1);
        var pageSize = GetInt(args, "pageSize", 10);
        var keyword = GetString(args, "keyword");

        var query = _context.Reviews
            .Include(r => r.Favorite).ThenInclude(f => f!.User)
            .Include(r => r.Favorite).ThenInclude(f => f!.Anime)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(r => r.Content != null && r.Content.Contains(keyword));

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.UpdateTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id,
                作者 = r.Favorite!.User!.NickName,
                动漫名称 = r.Favorite!.Anime!.Name,
                内容摘要 = r.Content != null && r.Content.Length > 60
                    ? r.Content.Substring(0, 60) + "..."
                    : r.Content ?? "",
                完整内容 = r.Content ?? "",
                r.UpdateTime
            })
            .ToListAsync(ct);

        return JsonSerializer.Serialize(new { items, total, page, pageSize });
    }

    private async Task<string> GetEmotionCurveStatsAsync(CancellationToken ct)
    {
        var data = await _context.EmotionRecords
            .GroupBy(r => r.EmotionLevel)
            .Select(g => new { level = g.Key, count = g.Count() })
            .OrderBy(x => x.level)
            .ToListAsync(ct);
        return JsonSerializer.Serialize(data);
    }

    private static ChatResponseDto ParseResponse(string content)
    {
        var dataRegex = new Regex(@"```json\s*(\{[\s\S]*?\})\s*```");
        var dataItems = new List<DataResultItem>();

        foreach (Match match in dataRegex.Matches(content))
        {
            try
            {
                var obj = JsonDocument.Parse(match.Groups[1].Value).RootElement;
                if (obj.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText());
                        dataItems.Add(new DataResultItem(dict ?? new()));
                    }
                    content = content.Replace(match.Value, "[数据表格]");
                }
                else
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(obj.GetRawText());
                    if (dict != null && dict.Count > 0)
                        dataItems.Add(new DataResultItem(dict));
                }
            }
            catch { }
        }

        return new ChatResponseDto
        {
            Answer = content,
            DataResults = dataItems.Count > 0 ? dataItems : null,
            DisplayType = dataItems.Count > 0 ? "table" : "text"
        };
    }
}
