using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManWei.Api.DTOs;

/// <summary>
/// Bangumi API 条目响应
/// </summary>
public class BangumiSubjectDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_cn")]
    public string? NameCn { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("images")]
    public BangumiImagesDto? Images { get; set; }

    [JsonPropertyName("tags")]
    public List<BangumiTagDto>? Tags { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("rating")]
    public BangumiRatingDto? Rating { get; set; }

    [JsonPropertyName("infobox")]
    public JsonElement? InfoboxRaw { get; set; }

    /// <summary>
    /// 将 InfoboxRaw (可能是数组 [] 或字典 {}) 统一转为字典
    /// </summary>
    private static readonly JsonSerializerOptions _infoboxOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [JsonIgnore]
    public Dictionary<string, BangumiInfoboxItemDto>? Infobox => InfoboxRaw switch
    {
        { ValueKind: JsonValueKind.Object } => JsonSerializer.Deserialize<Dictionary<string, BangumiInfoboxItemDto>>(
            InfoboxRaw.Value.GetRawText(), _infoboxOptions),
        _ => null
    };
}

/// <summary>
/// Bangumi 图片信息
/// </summary>
public class BangumiImagesDto
{
    [JsonPropertyName("small")]
    public string? Small { get; set; }

    [JsonPropertyName("medium")]
    public string? Medium { get; set; }

    [JsonPropertyName("large")]
    public string? Large { get; set; }

    [JsonPropertyName("grid")]
    public string? Grid { get; set; }

    [JsonPropertyName("common")]
    public string? Common { get; set; }
}

/// <summary>
/// Bangumi 标签
/// </summary>
public class BangumiTagDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Bangumi 评分（score/total/rank/count 分布）
/// </summary>
public class BangumiRatingDto
{
    [JsonPropertyName("rank")]
    public int? Rank { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("count")]
    public Dictionary<string, int>? Count { get; set; }
}

/// <summary>
/// Bangumi infobox 单项（key-value，value 可能是 string 或 array）
/// </summary>
public class BangumiInfoboxItemDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

/// <summary>
/// Bangumi API 批量查询响应（分页包装）
/// </summary>
public class BangumiSubjectListDto
{
    [JsonPropertyName("data")]
    public List<BangumiSubjectDto> Data { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}
