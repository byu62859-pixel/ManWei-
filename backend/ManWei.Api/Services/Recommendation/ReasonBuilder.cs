using System.Globalization;

namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// Step 7: 推荐解释模板拼接器。
/// 输入：mode（full / tag_only / popular）+ ScoreBreakdown + 候选番 BangumiScore
/// 输出：模板化中文解释字符串，填入 RecommendItem.Reason，供前端 / LLM 直接展示。
///
/// 模式分支：
///   full     — 双信号解释（标签 + 情绪 + 评分）
///   tag_only — 标签信号解释（无情绪画像时的退化）
///   popular  — 热门兜底（不依赖最近邻）
/// </summary>
public static class ReasonBuilder
{
    /// <summary>
    /// 拼接模板化解释。
    /// </summary>
    /// <param name="mode">推荐模式：full / tag_only / popular</param>
    /// <param name="breakdown">候选番的评分拆解（提供最近邻名、标签重合、情绪亲和度）</param>
    /// <param name="bangumiScore">候选番的 Bangumi 评分字符串（1 位小数，如 "8.5"）；null 时回退为 "N/A"</param>
    /// <returns>中文解释字符串</returns>
    public static string Build(
        string mode,
        ScoreBreakdown breakdown,
        string? bangumiScore = null)
    {
        // 公共百分比
        var tagOverlapPercent = (int)Math.Round(breakdown.TagOverlap * 100);
        var emotionPercent = (int)Math.Round(breakdown.EmotionAffinity * 100);
        var scoreText = string.IsNullOrWhiteSpace(bangumiScore) ? "N/A" : bangumiScore;

        // 最近邻兜底（理论上 full/tag_only 都会拿到；popular 不用）
        var neighborName = string.IsNullOrWhiteSpace(breakdown.NearestNeighborName)
            ? "你高评分的某部番"
            : breakdown.NearestNeighborName;

        return mode switch
        {
            "full" => string.Format(
                CultureInfo.InvariantCulture,
                "与《{0}》最相似，标签重合 {1}%，情绪曲线相近度 {2}%，Bangumi 评分 {3}。",
                neighborName,
                tagOverlapPercent,
                emotionPercent,
                scoreText),

            "tag_only" => string.Format(
                CultureInfo.InvariantCulture,
                "标签重合 {0}%，匹配你收藏中的风格类型，Bangumi 评分 {1}。",
                tagOverlapPercent,
                scoreText),

            "popular" => string.Format(
                CultureInfo.InvariantCulture,
                "Bangumi 热门推荐，评分 {0}。",
                scoreText),

            // 未知 mode：按 popular 兜底，确保返回永远非空
            _ => string.Format(
                CultureInfo.InvariantCulture,
                "Bangumi 热门推荐，评分 {0}。",
                scoreText)
        };
    }
}
