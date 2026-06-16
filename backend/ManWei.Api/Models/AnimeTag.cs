namespace ManWei.Api.Models;

/// <summary>
/// 动漫题材标签（来自 Bangumi API，区别于用户自定义情感标签 EmotionTag）
/// </summary>
public class AnimeTag
{
    public int Id { get; set; }
    public int AnimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Bangumi 标签热度权重（用于排序）
    /// </summary>
    public int Count { get; set; }

    public Anime Anime { get; set; } = null!;
}
