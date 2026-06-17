namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// ColdStartResolver — 冷启动 3 档模式判定（纯函数，无 DI）。
///
/// 判定顺序（严格按 docs/recommendation-impl-progress.md §二 第 4 节）：
///   1. 高分集 H 为空                          → popular
///   2. 候选池 candidates 为空                  → popular
///   3. H 中没有任何番有情绪画像（无 EmotionRecord）→ tag_only
///   4. 其余                                   → full
/// </summary>
public static class ColdStartResolver
{
    public const string ModeFull = "full";
    public const string ModeTagOnly = "tag_only";
    public const string ModePopular = "popular";

    /// <summary>
    /// 判定冷启动模式。
    /// </summary>
    /// <param name="highRatedCount">高分集 H 的大小（用户给出 >= 4 星评分的番数）。</param>
    /// <param name="candidatePoolSize">候选池 candidates 的大小（过滤后的候选动漫数）。</param>
    /// <param name="hasEmotionProfile">H 中是否至少 1 部番拥有 ≥1 条 EmotionRecord。</param>
    /// <returns>三种 mode 之一：<see cref="ModeFull"/> / <see cref="ModeTagOnly"/> / <see cref="ModePopular"/>。</returns>
    public static string Resolve(
        int highRatedCount,
        int candidatePoolSize,
        bool hasEmotionProfile)
    {
        // 1) H 为空 → 无信号，直接走热门兜底
        if (highRatedCount == 0)
        {
            return ModePopular;
        }

        // 2) 候选池空 → 哪怕有 H 也没东西可推，走热门
        if (candidatePoolSize == 0)
        {
            return ModePopular;
        }

        // 3) 有 H 有 candidates，但 H 全是"看了就忘"无情绪画像 → 退化为 tag_only
        if (!hasEmotionProfile)
        {
            return ModeTagOnly;
        }

        // 4) 完整信号：H + candidates + 情绪画像齐全 → full
        return ModeFull;
    }
}
