namespace ManWei.Api.Models;

public class Anime
{
    public int Id { get; set; }
    public int? BangumiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string AnimeType { get; set; } = "TV";
    /// <summary>
    /// 总集数（从 Bangumi 拉取）。
    /// null 或 0 均视为"未拉取到/老数据/拉取失败"（Backfill 候选 + Sync 覆盖策略都用 is null or 0 守卫）。
    /// Bangumi 对已上映作品不会合法返回 0；任何 TotalEpisodes = 0 的行需要重新拉取。
    /// </summary>
    public int? TotalEpisodes { get; set; }
    /// <summary>
    /// 放送日期（from Bangumi date，yyyy-MM-dd 解析为 DateOnly）
    /// </summary>
    public DateOnly? AirDate { get; set; }
    /// <summary>
    /// 片长（from Bangumi infobox.片长）
    /// </summary>
    public string? Duration { get; set; }
    /// <summary>
    /// 制作公司（from Bangumi infobox.动画制作 / 制作）
    /// </summary>
    public string? Producer { get; set; }
    /// <summary>
    /// 监督/导演（from Bangumi infobox.导演 / 监督）
    /// </summary>
    public string? Director { get; set; }
    /// <summary>
    /// 官方评分（from Bangumi rating.score, 0-10）
    /// </summary>
    public double? BangumiScore { get; set; }
    /// <summary>
    /// 官方排名（from Bangumi rating.rank）
    /// </summary>
    public int? BangumiRank { get; set; }
    /// <summary>
    /// 评分人数（from Bangumi rating.total）
    /// </summary>
    public int? BangumiRatingCount { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<AnimeTag> AnimeTags { get; set; } = new List<AnimeTag>();
}
