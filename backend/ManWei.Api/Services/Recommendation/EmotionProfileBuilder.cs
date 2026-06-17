using ManWei.Api.Data;
using ManWei.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services.Recommendation;

/// <summary>
/// Step 3: 用户级情绪画像构建器。
/// 输入：H = {f.Anime | f in Favorites(user) and f.Rating >= 8 and f.Status == 2}
/// 输出：E_avg_global (1-5), E_std_global (>= 0), HasProfile, CoveredHighRatedCount
///
/// 算法：每部番先算自己的 E_avg / E_std（仅当有情绪记录时），再聚合为用户级。
/// std 用总体标准差（除以 N，不是 N-1）。
/// </summary>
public class EmotionProfileBuilder
{
    private readonly AppDbContext _context;

    public EmotionProfileBuilder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EmotionProfile> BuildAsync(
        int userId,
        CancellationToken ct = default)
    {
        // 1) 拉取 H：用户高分已看番（含 Anime 和 EmotionRecords）
        var highRatedFavorites = await _context.Favorites
            .Where(f => f.UserId == userId && f.Rating >= 8 && f.Status == 2)
            .Include(f => f.Anime)
            .Include(f => f.EmotionRecords)
            .ToListAsync(ct);

        // 2) H 为空：直接返回冷启动默认值
        if (highRatedFavorites.Count == 0)
        {
            return new EmotionProfile
            {
                AvgGlobal = 3.0,
                StdGlobal = 0.0,
                HasProfile = false,
                CoveredHighRatedCount = 0
            };
        }

        // 3) 逐番计算 E_avg / E_std（只统计有情绪记录的番）
        var perAnimeAvg = new List<double>();
        var perAnimeStd = new List<double>();
        var coveredCount = 0;

        foreach (var f in highRatedFavorites)
        {
            var levels = f.EmotionRecords
                .Select(r => (double)r.EmotionLevel)
                .ToList();

            if (levels.Count == 0)
            {
                // 该番无情绪记录，不参与全局聚合
                continue;
            }

            coveredCount++;

            var mean = levels.Average();
            // 总体方差：除以 N（不是 N-1）
            var variance = levels.Sum(x => (x - mean) * (x - mean)) / levels.Count;
            var std = Math.Sqrt(variance);

            perAnimeAvg.Add(mean);
            perAnimeStd.Add(std);
        }

        // 4) H 非空但所有番都没情绪记录
        if (coveredCount == 0)
        {
            return new EmotionProfile
            {
                AvgGlobal = 3.0,
                StdGlobal = 0.0,
                HasProfile = false,
                CoveredHighRatedCount = 0
            };
        }

        // 5) 聚合为用户级画像
        var avgGlobal = perAnimeAvg.Average();
        var stdGlobal = perAnimeStd.Average();

        return new EmotionProfile
        {
            AvgGlobal = avgGlobal,
            StdGlobal = stdGlobal,
            HasProfile = true,
            CoveredHighRatedCount = coveredCount
        };
    }
}
