using System.Globalization;
using ManWei.Api.Data;
using ManWei.Api.Models;
using ManWei.Api.Services.Recommendation;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

/// <summary>
/// 推荐服务顶层编排接口。
/// 由 AI tool (recommend_anime) / REST controller 共用同一实现。
/// </summary>
public interface IRecommendAnimeService
{
    Task<RecommendResult> RecommendAsync(
        int userId,
        RecommendRequest req,
        CancellationToken ct = default);
}

/// <summary>
/// Step 8: 推荐服务编排实现（按 docs/recommendation-impl-progress.md §六 数据流图）。
///
/// 编排顺序：
///   1) EmotionProfileBuilder.BuildAsync(userId)                    → EmotionProfile
///   2) CandidatePoolBuilder.BuildAsync(userId, req)                 → List<Candidate>
///   3) TagProfileBuilder.BuildAsync(userId, candidatePoolSize)      → UserTagProfile
///   4) ColdStartResolver.Resolve(highRatedCount, N, hasEmotion)     → Mode
///   5) 分支：
///      - popular 模式：按 BangumiScore 降序，跳过 Scorer.Score
///      - full / tag_only 模式：构造 hAnimeWithProfile（含情绪记录），
///        对每个 candidate 找 nearest，调用 Scorer.Score
///   6) OrderByDescending(score) → Take(topK)
///   7) RecommendItem.Reason = ReasonBuilder.Build(mode, breakdown, bangumiScoreStr)
/// </summary>
public class RecommendAnimeService : IRecommendAnimeService
{
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly TagProfileBuilder _tagProfileBuilder;
    private readonly EmotionProfileBuilder _emotionProfileBuilder;
    private readonly CandidatePoolBuilder _candidatePoolBuilder;
    private readonly ILogger<RecommendAnimeService> _logger;

    public RecommendAnimeService(
        AppDbContext context,
        IBangumiService bangumiService,
        TagProfileBuilder tagProfileBuilder,
        EmotionProfileBuilder emotionProfileBuilder,
        CandidatePoolBuilder candidatePoolBuilder,
        ILogger<RecommendAnimeService> logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _tagProfileBuilder = tagProfileBuilder;
        _emotionProfileBuilder = emotionProfileBuilder;
        _candidatePoolBuilder = candidatePoolBuilder;
        _logger = logger;
    }

    public async Task<RecommendResult> RecommendAsync(
        int userId,
        RecommendRequest req,
        CancellationToken ct = default)
    {
        // ===== TopK 钳位：1-20, 默认 5 =====
        int topK = req.TopK;
        if (topK <= 0) topK = 5;
        if (topK > 20) topK = 20;

        // ===== 步骤 1：用户情绪画像 =====
        var emotion = await _emotionProfileBuilder.BuildAsync(userId, ct);

        // ===== 步骤 2：候选池 =====
        var candidates = await _candidatePoolBuilder.BuildAsync(userId, req, ct);
        int candidatePoolSize = candidates.Count;

        // ===== 空候选池：直接返回 popular + Error =====
        if (candidates.Count == 0)
        {
            return new RecommendResult
            {
                Mode = ColdStartResolver.ModePopular,
                CandidatePoolSize = 0,
                Items = new List<RecommendItem>(),
                Error = "no_candidates"
            };
        }

        // ===== 步骤 3：用户标签画像（用 N 算 IDF）=====
        var userTag = await _tagProfileBuilder.BuildAsync(userId, candidatePoolSize, ct);

        int highRatedCount = userTag.HighRatedCount;

        // ===== 步骤 4：冷启动模式判定 =====
        string mode = ColdStartResolver.Resolve(highRatedCount, candidatePoolSize, emotion.HasProfile);

        // ===== 步骤 5：分支打分 =====
        if (mode == ColdStartResolver.ModePopular)
        {
            // popular 模式：按 BangumiScore 降序，跳过 Scorer
            var popularItems = candidates
                .OrderByDescending(c => c.BangumiScore ?? 0.0)
                .Take(topK)
                .Select(c => BuildPopularItem(c, mode))
                .ToList();

            return new RecommendResult
            {
                Mode = mode,
                CandidatePoolSize = candidatePoolSize,
                Items = popularItems,
                Error = null
            };
        }

        // ===== full / tag_only 模式：构造 H 的 AnimeWithProfile =====
        // 仅查一次 DB，复用于所有 candidate 的 nearest 计算。
        var highRatedFavorites = await _context.Favorites
            .Where(f => f.UserId == userId && f.Rating >= 8 && f.Status == 2)
            .Include(f => f.Anime)
                .ThenInclude(a => a.AnimeTags)
            .Include(f => f.EmotionRecords)
            .ToListAsync(ct);

        // 构造 hAnimeWithProfile：含 EAvg / EStd
        // 注：candidate.Tags.Name 与 hAnime.Tags.Name 是同源 Bangumi tag 字符串，
        //     candidate.Tags 的 Name 没被 NormalizeKey（来自 CandidatePoolBuilder 直接透传），
        //     但 hAnime.Tags 的 Name 也没 NormalizeKey；userTag.Weights 的 key 是 NormalizeKey 后的。
        //     因此比对时需要 NormalizeKey；详见 FindNearest。
        var hAnimeWithProfile = new List<AnimeWithProfile>();
        foreach (var f in highRatedFavorites)
        {
            if (f.Anime == null) continue;

            var records = f.EmotionRecords?.ToList() ?? new List<EmotionRecord>();
            double eAvg;
            double eStd;
            if (records.Count == 0)
            {
                // 无情绪记录：默认值；tag_only 模式 nearest 不影响最终分（emotionAffinity=0）
                eAvg = 3.0;
                eStd = 0.0;
            }
            else
            {
                var levels = records.Select(r => (double)r.EmotionLevel).ToList();
                var mean = levels.Average();
                var variance = levels.Sum(x => (x - mean) * (x - mean)) / levels.Count;
                eAvg = mean;
                eStd = Math.Sqrt(variance);
            }

            hAnimeWithProfile.Add(new AnimeWithProfile
            {
                LocalAnimeId = f.AnimeId,
                Name = f.Anime.Name,
                EAvg = eAvg,
                EStd = eStd,
                Tags = f.Anime.AnimeTags?.ToList() ?? new List<AnimeTag>()
            });
        }

        // ===== 对每个 candidate 打分 =====
        var scored = new List<(Candidate candidate, double finalScore, ScoreBreakdown breakdown)>();

        foreach (var c in candidates)
        {
            // 1) 找 nearest
            var nearest = FindNearest(c, hAnimeWithProfile, userTag);

            // tag_only 模式（emotion.HasProfile=false）时 nearest 不参与 Scorer 计算
            AnimeWithProfile? nearestForScorer = emotion.HasProfile ? nearest : null;

            var (baseScore, breakdown) = Scorer.Score(c, userTag, emotion, nearestForScorer!);

            scored.Add((c, breakdown.FinalScore, breakdown));
        }

        // ===== 步骤 6：排序 + TopK =====
        var top = scored
            .OrderByDescending(x => x.finalScore)
            .Take(topK)
            .ToList();

        // ===== 步骤 7：构造 RecommendItem =====
        var items = new List<RecommendItem>(top.Count);
        foreach (var (c, finalScore, breakdown) in top)
        {
            string? bangumiScoreStr = c.BangumiScore?.ToString("F1", CultureInfo.InvariantCulture);
            string reason = ReasonBuilder.Build(mode, breakdown, bangumiScoreStr);

            items.Add(new RecommendItem
            {
                AnimeId = c.LocalAnimeId,
                BangumiId = c.BangumiId,
                Name = c.Name,
                Cover = c.Cover,
                AnimeType = c.AnimeType,
                BangumiScore = c.BangumiScore,
                Tags = c.Tags
                    .OrderByDescending(t => t.Count)
                    .Select(t => t.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList(),
                Score = finalScore,
                Breakdown = breakdown,
                Reason = reason
            });
        }

        return new RecommendResult
        {
            Mode = mode,
            CandidatePoolSize = candidatePoolSize,
            Items = items,
            Error = null
        };
    }

    /// <summary>
    /// 找 candidate 的最近邻 H 中番。
    /// 算法：argmax_{a ∈ H} Σ_{t ∈ c.Tags ∩ a.Tags} U_tag_norm(t)
    /// 两边都按 NormalizeKey(t.Name) 比对（c.Tags.Name 与 hAnime.Tags.Name 是同源 Bangumi 字符串）。
    /// </summary>
    private static AnimeWithProfile? FindNearest(
        Candidate candidate,
        List<AnimeWithProfile> hAnimeWithProfile,
        UserTagProfile userTag)
    {
        if (hAnimeWithProfile.Count == 0) return null;
        if (candidate.Tags == null || candidate.Tags.Count == 0) return null;

        // 预计算 candidate 各 tag 的归一化 key
        var cTagKeys = candidate.Tags
            .Select(t => NormalizeKey(t.Name))
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();
        if (cTagKeys.Count == 0) return null;

        AnimeWithProfile? bestAnime = null;
        double bestSim = double.NegativeInfinity;

        foreach (var hAnime in hAnimeWithProfile)
        {
            if (hAnime.Tags == null || hAnime.Tags.Count == 0) continue;

            // hAnime 的 tag key 集合（用于交集判断）
            // 性能优化：构造 HashSet 但只算一次
            double sim = 0.0;
            foreach (var cKey in cTagKeys)
            {
                // 候选的 tag key 在用户画像里
                if (!userTag.Weights.TryGetValue(cKey, out var w)) continue;

                // hAnime 是否含相同 key
                bool hit = false;
                foreach (var ht in hAnime.Tags)
                {
                    if (NormalizeKey(ht.Name) == cKey)
                    {
                        hit = true;
                        break;
                    }
                }
                if (hit)
                {
                    sim += w;
                }
            }

            if (sim > bestSim)
            {
                bestSim = sim;
                bestAnime = hAnime;
            }
        }

        return bestAnime;
    }

    /// <summary>
    /// 构造 popular 模式的 RecommendItem。
    /// Breakdown 留空（TagOverlap/EmotionAffinity/QualityBoost=0）。
    /// Score = BangumiScore ?? 0。
    /// </summary>
    private static RecommendItem BuildPopularItem(Candidate c, string mode)
    {
        string? bangumiScoreStr = c.BangumiScore?.ToString("F1", CultureInfo.InvariantCulture);
        var breakdown = new ScoreBreakdown
        {
            TagOverlap = 0,
            EmotionAffinity = 0,
            QualityBoost = 0,
            BaseScore = 0,
            FinalScore = c.BangumiScore ?? 0
        };
        string reason = ReasonBuilder.Build(mode, breakdown, bangumiScoreStr);

        return new RecommendItem
        {
            AnimeId = c.LocalAnimeId,
            BangumiId = c.BangumiId,
            Name = c.Name,
            Cover = c.Cover,
            AnimeType = c.AnimeType,
            BangumiScore = c.BangumiScore,
            Tags = c.Tags
                .OrderByDescending(t => t.Count)
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList(),
            Score = c.BangumiScore ?? 0,
            Breakdown = breakdown,
            Reason = reason
        };
    }

    /// <summary>
    /// 标签 key 规范化：小写 + trim。和 TagProfileBuilder.NormalizeKey 保持一致。
    /// </summary>
    private static string NormalizeKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return name.Trim().ToLowerInvariant();
    }
}
