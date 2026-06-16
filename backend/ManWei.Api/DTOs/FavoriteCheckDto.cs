namespace ManWei.Api.DTOs;

/// <summary>
/// 检查收藏状态响应
/// </summary>
public class FavoriteCheckDto
{
    /// <summary>
    /// 是否已收藏
    /// </summary>
    public bool IsFavorited { get; set; }

    /// <summary>
    /// 收藏ID（若已收藏）
    /// </summary>
    public int? FavoriteId { get; set; }

    /// <summary>
    /// 收藏状态：0=想看 1=在看 2=看过（若已收藏）
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// 收藏状态文本
    /// </summary>
    public string? StatusText => Status.HasValue ? FavoriteStatus.ToText(Status.Value) : null;

    /// <summary>
    /// 已看集数/进度（若已收藏）
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// 用户评分（1-10，若已评分）
    /// </summary>
    public int? Rating { get; set; }
}
