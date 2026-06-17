using ManWei.Api.Models;

namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// Step 5: 候选番打分（纯静态函数，无状态、无 DI 依赖）。
///
/// 算法（与 docs/recommendation-impl-progress.md §二 第 3 节严格一致）：
///   1) tagOverlap   = Σ U_tag_norm(t) · tagVec_c(t)                       范围 [0, 1]
///   2) nearest      = argmax_a∈H  Σ_{t ∈ c.Tags ∩ a.Tags} U_tag_norm(t)  在编排里算好传入
///   3) emotionAff   = 0.6·(1-|E_avg_g - E_avg_n|/4) + 0.4·(1-|E_std_g - E_std_n|/2)
///   4) qualityBoost = (c.BangumiScore ?? 6.5) / 10
///   5) baseScore    = (full) 0.6·tag + 0.4·emo  |  (tag_only) 1.0·tag
///      finalScore   = baseScore + 0.1·qualityBoost                          范围 [0, 1.1]
///
/// 冷启动模式识别：
///   - popular 模式：编排层 ColdStartResolver 不会调用 Score，直接走 PopularScore
///   - tag_only 模式：emotion.HasProfile == false（无需显式传 mode）
/// </summary>
public static class Scorer
{
    /// <summary>
    /// 完整打分（full / tag_only 共用入口）。
    /// 编排层负责：① ColdStartResolver 判定 ② 候选无近邻时兜底 emotionAffinity=0.5
    /// 本函数只做点积 / 仿射 / 归一化裁剪，不涉及 IO、不做"近邻查找"。
    /// </summary>
    /// <param name="candidate">候选番（含 Tags / BangumiScore）</param>
    /// <param name="userTag">用户标签画像 U_tag_norm</param>
    /// <param name="emotion">用户情绪画像（HasProfile=false 时走 tag_only）</param>
    /// <param name="nearest">最近邻 H 中的番（含 EAvg / EStd / Tags）；tag_only 模式可传 default</param>
    /// <returns>(baseScore, ScoreBreakdown)</returns>
    public static (double baseScore, ScoreBreakdown breakdown) Score(
        Candidate candidate,
        UserTagProfile userTag,
        EmotionProfile emotion,
        AnimeWithProfile nearest)
    {
        // ===== 步骤 1：tagOverlap =====
        // tagVec_c(t) = t.Count / Σ t'.Count
        // tagOverlap  = Σ U_tag_norm(t) · tagVec_c(t), 范围 [0, 1]
        double tagOverlap = 0.0;
        if (candidate.Tags is { Count: > 0 })
        {
            var totalCount = candidate.Tags.Sum(t => (long)t.Count);
            if (totalCount > 0)
            {
                foreach (var t in candidate.Tags)
                {
                    // 候选的 tag 不在用户画像里 → 跳过该项（权重 0）
                    if (!userTag.Weights.TryGetValue(t.Name, out var weight))
                    {
                        continue;
                    }

                    var tagVec = (double)t.Count / totalCount;
                    tagOverlap += weight * tagVec;
                }
            }
        }
        tagOverlap = Math.Clamp(tagOverlap, 0.0, 1.0);

        // ===== 步骤 3：emotionAffinity =====
        // emo_avg_sim = 1 - |E_avg_global - E_avg(nearest)| / 4
        // emo_std_sim = 1 - |E_std_global - E_std(nearest)| / 2
        // emotionAffinity = 0.6·emo_avg_sim + 0.4·emo_std_sim
        double emotionAffinity;
        ScoreBreakdown breakdown = new();

        if (!emotion.HasProfile)
        {
            // tag_only 模式：情绪维度置 0（不参与 baseScore）
            emotionAffinity = 0.0;
        }
        else
        {
            // nearest 在 H 非空时必存在；编排层若走兜底会传 default(AnimeWithProfile)
            // 此时把 emotionAffinity 视为 0.5（与 progress doc §二 第 2 节"Defensive 分支"一致）
            if (nearest == null)
            {
                emotionAffinity = 0.5;
            }
            else
            {
                var emoAvgSim = 1.0 - Math.Abs(emotion.AvgGlobal - nearest.EAvg) / 4.0;
                var emoStdSim = 1.0 - Math.Abs(emotion.StdGlobal - nearest.EStd) / 2.0;
                emoAvgSim = Math.Clamp(emoAvgSim, 0.0, 1.0);
                emoStdSim = Math.Clamp(emoStdSim, 0.0, 1.0);
                emotionAffinity = 0.6 * emoAvgSim + 0.4 * emoStdSim;
                emotionAffinity = Math.Clamp(emotionAffinity, 0.0, 1.0);
            }
        }

        // ===== 步骤 4：qualityBoost =====
        var qualityBoost = (candidate.BangumiScore ?? 6.5) / 10.0;
        qualityBoost = Math.Clamp(qualityBoost, 0.0, 1.0);

        // ===== 步骤 5：综合分（两段式）=====
        double baseScore;
        if (emotion.HasProfile)
        {
            // full 模式：baseScore = 0.6·tag + 0.4·emo, 归一 [0, 1]
            baseScore = 0.6 * tagOverlap + 0.4 * emotionAffinity;
        }
        else
        {
            // tag_only 模式：baseScore = 1.0·tag + 0·emo
            baseScore = 1.0 * tagOverlap + 0.0 * emotionAffinity;
        }
        baseScore = Math.Clamp(baseScore, 0.0, 1.0);

        // 加成项：finalScore = baseScore + 0.1·qualityBoost, 范围 [0, 1.1]
        var finalScore = baseScore + 0.1 * qualityBoost;
        finalScore = Math.Clamp(finalScore, 0.0, 1.1);

        breakdown.TagOverlap = tagOverlap;
        breakdown.EmotionAffinity = emotionAffinity;
        breakdown.QualityBoost = qualityBoost;
        breakdown.BaseScore = baseScore;
        breakdown.FinalScore = finalScore;

        if (nearest != null)
        {
            breakdown.NearestNeighborName = string.IsNullOrEmpty(nearest.Name) ? null : nearest.Name;
            breakdown.NearestNeighborAnimeId = nearest.LocalAnimeId > 0 ? nearest.LocalAnimeId : (int?)null;
        }

        return (baseScore, breakdown);
    }

    /// <summary>
    /// popular 模式：跳过 baseScore 计算，按 BangumiScore 降序排序。
    /// 返回值约定：BangumiScore ?? 0（即无评分视为 0 分排末位）。
    /// </summary>
    public static double PopularScore(Candidate candidate)
    {
        return candidate.BangumiScore ?? 0.0;
    }
}

/// <summary>
/// H（用户高评分番集合）中某部番的"用于近邻匹配 + 情绪亲和度"投影。
/// 由编排层（RecommendAnimeService / EmotionProfileBuilder）构造后传入 Scorer.Score。
///
/// 字段来源：
///   - LocalAnimeId / Name：Anime 表
///   - EAvg / EStd：EmotionProfileBuilder 从 Favorite.EmotionRecords 聚合
///   - Tags：Anime.AnimeTags（与 candidate.Tags 同源，便于 c.Tags ∩ a.Tags 比对）
/// </summary>
public class AnimeWithProfile
{
    public int LocalAnimeId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>该番的 E_avg(a) = mean(EmotionLevel), 1-5</summary>
    public double EAvg { get; set; }
    /// <summary>该番的 E_std(a) = std(EmotionLevel), ≥ 0</summary>
    public double EStd { get; set; }
    /// <summary>该番的标签列表（Bangumi Top-5, 含 Count）</summary>
    public List<AnimeTag> Tags { get; set; } = new();
}
