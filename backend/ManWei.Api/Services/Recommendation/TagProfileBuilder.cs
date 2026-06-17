using ManWei.Api.Data;
using ManWei.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// 构建用户标签画像（TF-IDF + max-pool + L2 归一）。
///
/// 算法（严格一致版，来自 docs/recommendation-impl-progress.md §二）：
///   H = { f.Anime | f in Favorites(user), f.Rating >= 8, f.Status == 2 }
///   For each a in H:
///       TF_a(t) = t.Count / Σ t'.Count        // a 内归一
///   For each tag t in union of all H.Anime.AnimeTags:
///       df(t) = count of a in H where t ∈ a.AnimeTags
///       IDF(t) = log( N / (1 + df(t)) )       // N = 候选池大小
///   For each tag t in any a.AnimeTags (a ∈ H):
///       U_tag(t) = max_{a ∈ H}  TF_a(t) * IDF(t)
///   ||U_tag||₂ = sqrt(Σ U_tag(t)²)
///   U_tag_norm(t) = U_tag(t) / ||U_tag||₂
///
/// 为什么用 max-pool 而非 sum：用户的"最爱"标签往往来自单部神作而非多部均值；
/// max 更能反映"被这部番打动的核心标签"。
/// </summary>
public class TagProfileBuilder
{
    private readonly AppDbContext _context;

    public TagProfileBuilder(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 构建用户标签画像。
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="candidatePoolSize">候选池大小 N（用于 IDF 计算）。若传 0 也按 1 处理，防 log(0)。</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>UserTagProfile（Weights key 为归一化后的小写 trim 标签名）</returns>
    public async Task<UserTagProfile> BuildAsync(
        int userId,
        int candidatePoolSize,
        CancellationToken ct = default)
    {
        // 防 log(0)：N 必须 ≥ 1
        int n = candidatePoolSize <= 0 ? 1 : candidatePoolSize;

        // 拉取 H：高评分（>=8）+ 看过（Status=2）的收藏，含 Anime 及其 AnimeTags
        var favorites = await _context.Favorites
            .Where(f => f.UserId == userId && f.Rating >= 8 && f.Status == 2)
            .Include(f => f.Anime)
                .ThenInclude(a => a.AnimeTags)
            .ToListAsync(ct);

        // 高评分番数（按 AnimeId 唯一）
        int highRatedCount = favorites
            .Where(f => f.Anime != null)
            .Select(f => f.AnimeId)
            .Distinct()
            .Count();

        // 空 H 场景：直接返回空画像（IsL2Normalized=true 以满足"无标签"约定）
        if (favorites.Count == 0)
        {
            return new UserTagProfile
            {
                Weights = new Dictionary<string, double>(),
                IsL2Normalized = true,
                HighRatedCount = 0
            };
        }

        // 第一步：对每部番 a，先做 a 内归一 TF
        // 同时统计 df(t) = 在 H 中多少部番含有 t
        var tfByAnime = new Dictionary<int, Dictionary<string, double>>();
        var dfCount = new Dictionary<string, int>();

        foreach (var fav in favorites)
        {
            var anime = fav.Anime;
            if (anime == null || anime.AnimeTags == null || anime.AnimeTags.Count == 0)
                continue;

            // Σ t'.Count
            int totalCount = anime.AnimeTags.Sum(t => t.Count);
            if (totalCount <= 0)
                continue;

            var tfDict = new Dictionary<string, double>();
            var seenTagsInAnime = new HashSet<string>();

            foreach (var tag in anime.AnimeTags)
            {
                // 规范化 key
                string key = NormalizeKey(tag.Name);
                if (string.IsNullOrEmpty(key))
                    continue;

                // a 内归一 TF
                double tf = (double)tag.Count / totalCount;

                // 同一 key 在同一部番内可能出现多次（理论上不应发生），按 max 处理
                if (tfDict.TryGetValue(key, out double existing))
                {
                    if (tf > existing) tfDict[key] = tf;
                }
                else
                {
                    tfDict[key] = tf;
                }

                // df 统计：每部番每 key 只计 1 次
                if (seenTagsInAnime.Add(key))
                {
                    dfCount[key] = dfCount.GetValueOrDefault(key, 0) + 1;
                }
            }

            // 记录该番的 TF 字典（key 已经是 animeId）
            tfByAnime[fav.AnimeId] = tfDict;
        }

        // 第二步：U_tag(t) = max_{a ∈ H} TF_a(t) * IDF(t)
        //   其中 IDF(t) = log( N / (1 + df(t)) )
        var uTag = new Dictionary<string, double>();

        foreach (var (animeId, tfDict) in tfByAnime)
        {
            foreach (var (tagKey, tfVal) in tfDict)
            {
                int df = dfCount.GetValueOrDefault(tagKey, 0);
                double idf = Math.Log((double)n / (1.0 + df));
                double score = tfVal * idf;

                if (uTag.TryGetValue(tagKey, out double existing))
                {
                    if (score > existing) uTag[tagKey] = score;
                }
                else
                {
                    uTag[tagKey] = score;
                }
            }
        }

        // 第三步：L2 归一
        double l2 = Math.Sqrt(uTag.Values.Sum(v => v * v));

        Dictionary<string, double> weights;
        bool isL2Normalized;

        if (l2 <= 0 || uTag.Count == 0)
        {
            // 全 0 或无标签：返回空 Dictionary，IsL2Normalized = true
            weights = new Dictionary<string, double>();
            isL2Normalized = true;
        }
        else
        {
            weights = new Dictionary<string, double>(uTag.Count);
            foreach (var (k, v) in uTag)
            {
                weights[k] = v / l2;
            }
            // 数值校验：归一后 ∑w² ≈ 1（浮点容差）
            double sumSq = weights.Values.Sum(w => w * w);
            isL2Normalized = Math.Abs(sumSq - 1.0) < 1e-9;
        }

        return new UserTagProfile
        {
            Weights = weights,
            IsL2Normalized = isL2Normalized,
            HighRatedCount = highRatedCount
        };
    }

    /// <summary>
    /// 标签 key 规范化：小写 + trim。
    /// 防止中英文不一致或前后空格导致 key 不匹配。
    /// </summary>
    private static string NormalizeKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return name.Trim().ToLowerInvariant();
    }
}
