using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 动漫列表查询参数
/// </summary>
public class AnimeQueryDto
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页数量
    /// </summary>
    [Range(1, 100, ErrorMessage = "每页数量在1-100之间")]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 搜索关键词（按名称模糊搜索）
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 筛选类型：TV/剧场版/OVA
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 筛选情绪标签名称（首页标签栏）
    /// </summary>
    public string? TagName { get; set; }
}

/// <summary>
/// 动漫响应
/// </summary>
public class AnimeDto
{
    public int Id { get; set; }
    public int? BangumiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string AnimeType { get; set; } = string.Empty;
    public int? TotalEpisodes { get; set; }
    public DateOnly? AirDate { get; set; }
    public string? Duration { get; set; }
    public string? Producer { get; set; }
    public string? Director { get; set; }
    public double? BangumiScore { get; set; }
    public int? BangumiRank { get; set; }
    public int? BangumiRatingCount { get; set; }
    public List<AnimeTagDto> Tags { get; set; } = new();
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 收藏人数
    /// </summary>
    public int FavoriteCount { get; set; }

    /// <summary>
    /// 平均评分（1-10）
    /// </summary>
    public double? AvgRating { get; set; }

    /// <summary>
    /// 观后感数量
    /// </summary>
    public int ReviewCount { get; set; }
}

/// <summary>
/// 分页结果
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// 动漫标签 DTO（Bangumi 官方题材标签）
/// </summary>
public class AnimeTagDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
