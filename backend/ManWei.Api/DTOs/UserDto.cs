using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ManWei.Api.DTOs;

/// <summary>
/// 用户响应
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Avatar { get; set; }
    public string Role { get; set; } = "User";
    public bool IsEnabled { get; set; }
    [JsonPropertyName("createTime")]
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 用户查询参数
/// </summary>
public class UserQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 关键词搜索（按 OpenId 或 NickName 模糊匹配）
    /// </summary>
    public string? Keyword { get; set; }
}

/// <summary>
/// 更新用户状态请求
/// </summary>
public class UpdateUserStatusDto
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 修改昵称请求
/// </summary>
public class UpdateNicknameRequest
{
    [Required(ErrorMessage = "昵称不能为空")]
    [MaxLength(50, ErrorMessage = "昵称不能超过50个字符")]
    public string? NickName { get; set; }
}

/// <summary>
/// 追番数据统计
/// </summary>
public class UserStatsDto
{
    public int TotalEpisodes { get; set; }
    public double? AvgRating { get; set; }
    public string TotalEpisodesDisplay => TotalEpisodes.ToString();
    public string AvgRatingDisplay => AvgRating.HasValue
        ? AvgRating.Value.ToString("F1")
        : "-";
    public int ReviewCount { get; set; }
}
