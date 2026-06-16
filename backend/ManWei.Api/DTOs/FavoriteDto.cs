using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 收藏状态枚举
/// </summary>
public static class FavoriteStatus
{
    public const int WantToWatch = 0;   // 想看
    public const int Watching = 1;      // 在看
    public const int Watched = 2;       // 看过

    public static string ToText(int status) => status switch
    {
        WantToWatch => "想看",
        Watching => "在看",
        Watched => "看过",
        _ => "未知"
    };
}

/// <summary>
/// 收藏查询参数
/// </summary>
public class FavoriteQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 筛选状态：0=想看 1=在看 2=看过
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// 筛选标签名称（F2 新增）
    /// </summary>
    public string? TagName { get; set; }

    /// <summary>
    /// 排序字段（F3 预留扩展位：rating_desc/rating_asc）
    /// </summary>
    public string? OrderBy { get; set; }
}

/// <summary>
/// 收藏统计结果
/// </summary>
public class FavoriteCountDto
{
    public int All { get; set; }
    public int Wish { get; set; }
    public int Watching { get; set; }
    public int Watched { get; set; }
}

/// <summary>
/// 收藏响应
/// </summary>
public class FavoriteDto
{
    public int Id { get; set; }
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string? AnimeCover { get; set; }
    public string AnimeType { get; set; } = string.Empty;
    /// <summary>
    /// 动漫总集数（用于限制 Progress 上限；null=未拉取到）
    /// </summary>
    public int? AnimeTotalEpisodes { get; set; }
    public int Status { get; set; }
    public string StatusText => FavoriteStatus.ToText(Status);
    public int Progress { get; set; }
    /// <summary>
    /// 用户评分 1-10，null=未评分（F3 新增）
    /// </summary>
    public int? Rating { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 创建收藏请求
/// </summary>
public class CreateFavoriteDto
{
    /// <summary>
    /// 动漫ID
    /// </summary>
    [Required(ErrorMessage = "动漫ID不能为空")]
    public int AnimeId { get; set; }
}

/// <summary>
/// 更新收藏请求
/// </summary>
public class UpdateFavoriteDto
{
    /// <summary>
    /// 状态：0=想看 1=在看 2=看过
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// 已看集数/进度
    /// </summary>
    public int? Progress { get; set; }

    /// <summary>
    /// 用户评分 1-10，null=取消评分（F3 新增）
    /// </summary>
    public int? Rating { get; set; }
}

/// <summary>
/// 通过 BangumiId 添加收藏的请求 DTO
/// </summary>
public class AddFavoriteByBangumiDto
{
    /// <summary>
    /// 本地库已有动漫，直接传 AnimeId
    /// </summary>
    public int? AnimeId { get; set; }

    /// <summary>
    /// 本地库没有时传 BangumiId，后端自动同步
    /// </summary>
    public int? BangumiId { get; set; }
}

/// <summary>
/// 搜索结果条目
/// </summary>
public class AnimeSearchResultDto
{
    /// <summary>
    /// 本地库有则有值，否则为 null
    /// </summary>
    public int? AnimeId { get; set; }

    /// <summary>
    /// 始终有值
    /// </summary>
    public int BangumiId { get; set; }

    /// <summary>
    /// 动漫名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 中文名
    /// </summary>
    public string? NameCn { get; set; }

    /// <summary>
    /// 封面图
    /// </summary>
    public string? Cover { get; set; }

    /// <summary>
    /// 动漫类型
    /// </summary>
    public string AnimeType { get; set; } = "TV";

    /// <summary>
    /// 来源：local 或 bangumi
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
