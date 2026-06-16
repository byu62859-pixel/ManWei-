using System.Net.Http.Headers;
using System.Text.Json;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

/// <summary>
/// Bangumi API 服务
/// </summary>
public interface IBangumiService
{
    /// <summary>
    /// 根据 Bangumi ID 获取条目，构造 Anime 实体 + Top 5 标签
    /// </summary>
    Task<(Anime? anime, List<AnimeTag> tags)?> GetAndMapAnimeAsync(int bangumiId);

    /// <summary>
    /// 批量获取动漫条目（用于同步）
    /// </summary>
    Task<List<Anime>> GetAnimeBatchAsync(int limit, int offset);

    /// <summary>
    /// 搜索动漫条目
    /// </summary>
    Task<List<BangumiSubjectDto>> SearchAsync(string keyword, int limit = 10);

    /// <summary>
    /// 获取本篇总集数（type=0）
    /// </summary>
    /// <param name="bangumiId">Bangumi 条目 ID</param>
    /// <returns>本篇集数；失败/未发布返回 null</returns>
    Task<int?> GetEpisodesTotalAsync(int bangumiId);

    /// <summary>
    /// 详情页懒拉取 Bangumi 元信息（老数据补齐用）
    /// </summary>
    /// <remarks>
    /// 同 animeId 并发请求会被合并为一个 Task；
    /// 保护字段（Duration/Producer/Director/AirDate）仅在原值为 null 时填充；
    /// 覆盖字段（BangumiScore/BangumiRank/BangumiRatingCount）每次覆盖；
    /// 失败静默，不抛异常。
    /// </remarks>
    Task RefetchAnimeMetadataAsync(int animeId);
}

public class BangumiService : IBangumiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BangumiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly BangumiRateLimiter _rateLimiter;
    private readonly AppDbContext _context;

    public BangumiService(HttpClient httpClient, ILogger<BangumiService> logger,
        BangumiRateLimiter rateLimiter, AppDbContext context)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ManWei/1.0 (Windows PC Client)");
        _logger = logger;
        _rateLimiter = rateLimiter;
        _context = context;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <summary>
    /// 根据 Bangumi ID 获取条目并映射到 Anime 实体
    /// </summary>
    public async Task<(Anime? anime, List<AnimeTag> tags)?> GetAndMapAnimeAsync(int bangumiId)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi API 限流，拒绝请求 ID: {BangumiId}", bangumiId);
            return null;
        }

        try
        {
            var url = $"/v0/subjects/{bangumiId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi API 返回失败: {StatusCode}, ID: {BangumiId}",
                    response.StatusCode, bangumiId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);

            if (subject == null)
            {
                _logger.LogWarning("Bangumi API 解析失败，ID: {BangumiId}", bangumiId);
                return null;
            }

            // 1) 构造 Anime
            var anime = new Anime
            {
                BangumiId = subject.Id,
                Name = !string.IsNullOrWhiteSpace(subject.NameCn) ? subject.NameCn : subject.Name,
                Summary = subject.Summary,
                Cover = subject.Images?.Large ?? subject.Images?.Medium,
                AnimeType = MapPlatform(subject.Platform),
                BangumiScore = subject.Rating?.Score,
                BangumiRank = subject.Rating?.Rank,
                BangumiRatingCount = subject.Rating?.Total,
                CreateTime = DateTime.UtcNow
            };

            // 2) 解析 date (顶层结构化字段)
            if (!string.IsNullOrEmpty(subject.Date) && DateOnly.TryParse(subject.Date, out var airDate))
                anime.AirDate = airDate;

            // 3) 解析 infobox 提取片长/制作/监督
            if (subject.InfoboxRaw.HasValue && subject.InfoboxRaw.Value.ValueKind == JsonValueKind.Array)
            {
                var infoboxes = JsonSerializer.Deserialize<List<BangumiInfoboxItemDto>>(
                    subject.InfoboxRaw.Value.GetRawText(), _jsonOptions);
                if (infoboxes != null)
                {
                    anime.Duration = ExtractInfoboxString(infoboxes, "片长") ?? ExtractInfoboxString(infoboxes, "时长");
                    anime.Producer = ExtractInfoboxString(infoboxes, "动画制作")
                                     ?? ExtractInfoboxString(infoboxes, "制作");
                    anime.Director = ExtractInfoboxString(infoboxes, "导演")
                                     ?? ExtractInfoboxString(infoboxes, "监督");
                }
            }

            // 4) 解析 tags (Top 5, count>0)
            var tags = (subject.Tags ?? new())
                .Where(t => t.Count > 0)
                .OrderByDescending(t => t.Count)
                .Take(5)
                .Select(t => new AnimeTag { AnimeId = 0, Name = t.Name, Count = t.Count })
                .ToList();

            _logger.LogInformation("成功从 Bangumi 同步: {Name} (ID: {BangumiId}, Tags: {TagCount})",
                anime.Name, bangumiId, tags.Count);
            return (anime, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从 Bangumi 获取数据失败，ID: {BangumiId}", bangumiId);
            return null;
        }
    }

    private static string? ExtractInfoboxString(
        List<BangumiInfoboxItemDto> box, string key)
    {
        var item = box.FirstOrDefault(v => v.Key == key);
        if (item == null) return null;
        return item.Value.ValueKind == JsonValueKind.String
            ? item.Value.GetString()
            : null;
    }

    /// <summary>
    /// 批量获取动漫条目（用于同步）
    /// </summary>
    public async Task<List<Anime>> GetAnimeBatchAsync(int limit, int offset)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi API 限流，拒绝批量请求");
            return new List<Anime>();
        }

        try
        {
            var url = $"/v0/subjects?type=2&limit={limit}&offset={offset}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi API 返回失败: {StatusCode}", response.StatusCode);
                return new List<Anime>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            // API 返回格式: {"data": [...], "total": N, "limit": M, "offset": O}
            var listResponse = JsonSerializer.Deserialize<BangumiSubjectListDto>(content, options);
            var subjects = listResponse?.Data ?? new List<BangumiSubjectDto>();

            return subjects.Select(subject => new Anime
            {
                BangumiId = subject.Id,
                Name = !string.IsNullOrWhiteSpace(subject.NameCn) ? subject.NameCn : subject.Name,
                Summary = subject.Summary,
                Cover = subject.Images?.Large ?? subject.Images?.Medium,
                AnimeType = MapPlatform(subject.Platform),
                CreateTime = DateTime.UtcNow
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量获取 Bangumi 数据失败");
            return new List<Anime>();
        }
    }

    /// <summary>
    /// 搜索动漫条目
    /// </summary>
    public async Task<List<BangumiSubjectDto>> SearchAsync(string keyword, int limit = 15)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi API 限流，拒绝搜索请求");
            return new List<BangumiSubjectDto>();
        }

        try
        {
            Console.WriteLine($"[DEBUG-BEFORE] 准备请求 Bangumi。传入的原始 keyword 变量内容是: '{keyword}'");
            var url = "/v0/search/subjects";
            var requestBody = new
            {
                keyword = keyword,
                filter = new
                {
                    type = new int[] { 2 }
                }
            };
            Console.WriteLine($"[DEBUG-AFTER] 最终拼接出来的远程 URL 是: https://api.bgm.tv{url}?limit={limit}");
            var response = await _httpClient.PostAsJsonAsync($"{url}?limit={limit}", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi 搜索 API 返回失败: {StatusCode}", response.StatusCode);
                return new List<BangumiSubjectDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var listResponse = JsonSerializer.Deserialize<BangumiSubjectListDto>(content, options);
            var data = listResponse?.Data ?? new List<BangumiSubjectDto>();
            Console.WriteLine($"[Debug] Bangumi 返回 {data.Count} 条数据: {string.Join(", ", data.Select(s => $"{s.Id}-{s.Name}"))}");
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bangumi 搜索失败: {Keyword}", keyword);
            return new List<BangumiSubjectDto>();
        }
    }

    /// <summary>
    /// 获取本篇总集数
    /// </summary>
    public async Task<int?> GetEpisodesTotalAsync(int bangumiId)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi 限流，episodes 拉取被拒 ID: {BangumiId}", bangumiId);
            return null;
        }

        try
        {
            // limit=1 仅用于节省带宽（不需要返回 data 列表，只取 total 字段）。
            // ⚠️ Bangumi 的 total 字段是全量计数（与 limit 无关），
            // 不是当前页的条目数，所以 limit=1 时 total 仍是全量本篇集数。
            // 未来若有人误以为 total 跟随 limit 变化而改大 limit，请注意这一点。
            var url = $"/v0/episodes?subject_id={bangumiId}&type=0&limit=1";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi episodes 拉取失败: {StatusCode}, ID: {BangumiId}",
                    response.StatusCode, bangumiId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var listResponse = JsonSerializer.Deserialize<BangumiEpisodeListDto>(content, _jsonOptions);
            return listResponse?.Total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bangumi episodes 拉取异常 ID: {BangumiId}", bangumiId);
            return null;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Task> _pendingFetches = new();

    /// <summary>
    /// 详情页懒拉取入口（包装并发锁 + KeyValuePair 清理）
    /// </summary>
    public async Task RefetchAnimeMetadataAsync(int animeId)
    {
        // 1) 并发锁：GetOrAdd 原子地保证同 animeId 只有一个 Task
        var task = _pendingFetches.GetOrAdd(animeId, _ => DoRefetchAsync(animeId));
        try
        {
            await task;
        }
        finally
        {
            // 2) KeyValuePair 重载：仅当 key+value 都匹配时才删，防误删
            _pendingFetches.TryRemove(new System.Collections.Generic.KeyValuePair<int, Task>(animeId, task));
        }
    }

    private async Task DoRefetchAsync(int animeId)
    {
        var anime = await _context.Anime.FindAsync(animeId);
        if (anime == null || anime.BangumiId == null) return;

        var bangumiId = anime.BangumiId.Value;

        try
        {
            if (!await _rateLimiter.WaitForTokenAsync())
            {
                _logger.LogWarning("懒拉取被限流 AnimeId={Id}", animeId);
                return;
            }

            var url = $"/v0/subjects/{bangumiId}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("懒拉取 Bangumi 失败: {StatusCode}, AnimeId={Id}",
                    response.StatusCode, animeId);
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);
            if (subject == null) return;

            // 字段更新策略：保护字段不覆盖，纯 Bangumi 数据每次覆盖
            if (subject.Rating != null)
            {
                anime.BangumiScore = subject.Rating.Score;
                anime.BangumiRank = subject.Rating.Rank;
                anime.BangumiRatingCount = subject.Rating.Total;
            }
            if (subject.InfoboxRaw.HasValue && subject.InfoboxRaw.Value.ValueKind == JsonValueKind.Array)
            {
                var infoboxes = JsonSerializer.Deserialize<List<BangumiInfoboxItemDto>>(
                    subject.InfoboxRaw.Value.GetRawText(), _jsonOptions);
                _logger.LogInformation("懒拉取 infobox 有 {Count} 个条目", infoboxes?.Count ?? 0);
                if (infoboxes != null)
                {
                    if (anime.Duration == null)
                        anime.Duration = ExtractInfoboxString(infoboxes, "片长") ?? ExtractInfoboxString(infoboxes, "时长");
                    if (anime.Producer == null)
                        anime.Producer = ExtractInfoboxString(infoboxes, "动画制作")
                                         ?? ExtractInfoboxString(infoboxes, "制作");
                    if (anime.Director == null)
                        anime.Director = ExtractInfoboxString(infoboxes, "导演")
                                         ?? ExtractInfoboxString(infoboxes, "监督");
                }
            }
            else
            {
                _logger.LogInformation("懒拉取 infobox ValueKind={Kind}", subject.InfoboxRaw.HasValue ? subject.InfoboxRaw.Value.ValueKind.ToString() : "null");
            }
            if (!string.IsNullOrEmpty(subject.Date) && DateOnly.TryParse(subject.Date, out var airDate))
            {
                if (anime.AirDate == null) anime.AirDate = airDate;
            }

            await _context.SaveChangesAsync();

            // 替换 tags
            var existingTags = await _context.AnimeTags
                .Where(t => t.AnimeId == animeId)
                .ToListAsync();
            _context.AnimeTags.RemoveRange(existingTags);

            var newTags = (subject.Tags ?? new())
                .Where(t => t.Count > 0)
                .OrderByDescending(t => t.Count)
                .Take(5)
                .Select(t => new AnimeTag { AnimeId = animeId, Name = t.Name, Count = t.Count })
                .ToList();
            await _context.AnimeTags.AddRangeAsync(newTags);
            await _context.SaveChangesAsync();

            _logger.LogInformation("懒拉取 Bangumi 元信息成功 AnimeId={Id}, Tags={Count}",
                animeId, newTags.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "懒拉取 Bangumi 元数据失败 AnimeId={Id}", animeId);
        }
    }

    /// <summary>
    /// 映射 Bangumi platform 到我们的 AnimeType
    /// </summary>
    private static string MapPlatform(string? platform)
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
