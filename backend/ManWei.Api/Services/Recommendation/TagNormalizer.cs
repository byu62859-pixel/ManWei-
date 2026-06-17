namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// 标签名称规范化工具（公共方法，供 TagProfileBuilder / Scorer / FindNearest 共用）。
///
/// 背景：Bangumi 标签名来自第三方 API，不同条目之间同一标签可能出现大小写/前后空格不一致
/// （如 "P.A.WORKS" vs "p.a.works"），字典查找（TryGetValue）必须两侧用同一规范化 key，
/// 否则点积/近邻查找全部失效。
///
/// 规则：Trim + ToLowerInvariant（与 TagProfileBuilder 原 NormalizeKey 完全一致）。
/// </summary>
public static class TagNormalizer
{
    /// <summary>
    /// 标签名称规范化：去除前后空格 → 转小写（CultureInvariant）。
    /// 返回空字符串表示无效标签名（应在调用侧跳过）。
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return name.Trim().ToLowerInvariant();
    }
}
