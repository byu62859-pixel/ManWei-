using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ManWei.Api.DTOs;

/// <summary>
/// 观后感响应
/// </summary>
public class ReviewDto
{
    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }
    [JsonPropertyName("updateTime")]
    public DateTime UpdateTime { get; set; }
    public int Id { get; set; }
    public int FavoriteId { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 创建/更新观后感请求
/// </summary>
public class CreateReviewRequest
{
    /// <summary>
    /// 收藏ID
    /// </summary>
    [Required(ErrorMessage = "收藏ID不能为空")]
    public int FavoriteId { get; set; }

    /// <summary>
    /// 观后感内容
    /// </summary>
    [Required(ErrorMessage = "内容不能为空")]
    [MinLength(1, ErrorMessage = "内容不能为空")]
    [MaxLength(1000, ErrorMessage = "内容不能超过1000个字符")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 观后感 Feed 摘要（用于列表展示）
/// </summary>
public class ReviewSummaryDto
{
    public int ReviewId { get; set; }
    public int FavoriteId { get; set; }
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string AnimeCover { get; set; } = string.Empty;
    public string ContentSummary { get; set; } = string.Empty;
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 观后感管理列表查询参数（Admin）
/// </summary>
public class AdminReviewQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
}

/// <summary>
/// 观后感管理列表项（Admin）
/// </summary>
public class AdminReviewDto
{
    public int ReviewId { get; set; }
    public int FavoriteId { get; set; }
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string? AnimeCover { get; set; }
    public int UserId { get; set; }
    public string NickName { get; set; } = string.Empty;
    public string ContentSummary { get; set; } = string.Empty;
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 观后感详情（Admin）
/// </summary>
public class AdminReviewDetailDto
{
    public int ReviewId { get; set; }
    public int FavoriteId { get; set; }
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string? AnimeCover { get; set; }
    public int UserId { get; set; }
    public string NickName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
