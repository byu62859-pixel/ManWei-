namespace ManWei.Api.DTOs;

/// <summary>
/// 数据看板统计数据
/// </summary>
public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalAnime { get; set; }
    public int TotalFavorites { get; set; }
    public int TotalEmotionTags { get; set; }
    public int TotalReviews { get; set; }
}

/// <summary>
/// 今日概览
/// </summary>
public class TodayOverviewDto
{
    public int NewUsers { get; set; }
    public int NewFavorites { get; set; }
    public int NewTags { get; set; }
    public int NewAnime { get; set; }
}

/// <summary>
/// 用户增长趋势
/// </summary>
public class UserGrowthDto
{
    public DateTime Date { get; set; }
    public int UserCount { get; set; }
}

/// <summary>
/// 动漫收藏排行
/// </summary>
public class AnimeFavoriteRankDto
{
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public int FavoriteCount { get; set; }
}

/// <summary>
/// 标签使用排行
/// </summary>
public class TagUsageRankDto
{
    public string TagName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
