using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 情感曲线数据点（供前端折线图渲染）
/// </summary>
public class EmotionCurveDto
{
    public int Episode { get; set; }
    public int EmotionLevel { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 记录/更新情感曲线数据请求
/// </summary>
public class CreateEmotionCurveRequest
{
    /// <summary>
    /// 收藏ID
    /// </summary>
    [Required(ErrorMessage = "收藏ID不能为空")]
    public int FavoriteId { get; set; }

    /// <summary>
    /// 集数（默认第1集）
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "集数必须大于0")]
    public int Episode { get; set; } = 1;

    /// <summary>
    /// 情感等级（1-5）
    /// </summary>
    [Required(ErrorMessage = "情感等级不能为空")]
    [Range(1, 5, ErrorMessage = "情感等级必须在1-5之间")]
    public int EmotionLevel { get; set; }
}
