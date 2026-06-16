using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 情感标签响应
/// </summary>
public class EmotionTagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPreset { get; set; }
    public int? AnimeId { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建情感标签请求
/// </summary>
public class CreateEmotionTagRequest
{
    /// <summary>
    /// 标签名称
    /// </summary>
    [Required(ErrorMessage = "标签名不能为空")]
    [MaxLength(50, ErrorMessage = "标签名不能超过50个字符")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 动漫ID
    /// </summary>
    [Required(ErrorMessage = "动漫ID不能为空")]
    public int AnimeId { get; set; }
}

/// <summary>
/// 词云项
/// </summary>
public class WordCloudItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 情感标签管理统计查询参数
/// </summary>
public class EmotionTagStatsQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
}

/// <summary>
/// 情感标签管理统计
/// </summary>
public class EmotionTagStatsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPreset { get; set; }
    public int UsageCount { get; set; }
    public int RelatedUserCount { get; set; }
    public DateTime CreateTime { get; set; }
}
