using System.Text.Json.Serialization;

namespace ManWei.Api.DTOs;

/// <summary>
/// Bangumi episodes 列表响应（仅用于获取 total 计数）
/// 完整字段见 https://bangumi.github.io/api/#/Episode
/// </summary>
public class BangumiEpisodeListDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("data")]
    public List<BangumiEpisodeDto> Data { get; set; } = new();
}

/// <summary>
/// Bangumi 单条 episode 数据
/// </summary>
public class BangumiEpisodeDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("ep")]
    public int? Ep { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
