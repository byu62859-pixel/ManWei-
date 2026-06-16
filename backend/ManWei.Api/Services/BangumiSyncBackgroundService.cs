using Microsoft.EntityFrameworkCore;
using ManWei.Api.Data;
using ManWei.Api.Models;

namespace ManWei.Api.Services;

public class BangumiSyncBackgroundService : BackgroundService
{
    private const int BatchSize = 50;
    private const int CheckIntervalHours = 1;
    private const string OffsetKey = "BangumiSyncOffset";
    private const string WeekKey = "BangumiSyncWeek";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BangumiSyncBackgroundService> _logger;

    public BangumiSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BangumiSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bangumi 同步后台服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (ShouldSyncNow())
                {
                    await PerformSyncAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bangumi 同步失败");
            }

            await Task.Delay(TimeSpan.FromHours(CheckIntervalHours), stoppingToken);
        }
    }

    private bool ShouldSyncNow()
    {
        var now = DateTime.Now;

        // ─── 彻底废除旧的硬编码时间限制 ───
        // if (now.DayOfWeek != DayOfWeek.Wednesday || now.Hour < 6) return false;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. 计算当前的 ISO 周标记 (例如：2026-W24)
        var currentWeek = GetIsoWeekString(now);

        // 2. 从数据库获取上一次成功同步的周标记
        var lastSyncWeek = context.SystemConfigs
            .FirstOrDefault(c => c.Key == WeekKey)?.Value;

        // 3. 🌟 核心新逻辑：只要【当前周】不等于【上一次同步的周】，就说明这周还没进货
        // 不管今天是周几，立刻触发同步补货！
        if (currentWeek != lastSyncWeek)
        {
            _logger.LogInformation("【状态机检测】检测到新的一周 [{CurrentWeek}]，本地尚未同步（上次同步：{LastWeek}）。准备自动补货！", currentWeek, lastSyncWeek ?? "无记录");
            return true;
        }

        // 如果相同，说明这周已经进过货了，安心拦截，继续等待下周
        return false;
    }

    private async Task PerformSyncAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bangumiService = scope.ServiceProvider.GetRequiredService<IBangumiService>();

        // 读取当前 offset
        var offsetStr = context.SystemConfigs
            .FirstOrDefault(c => c.Key == OffsetKey)?.Value ?? "0";
        var offset = int.TryParse(offsetStr, out var o) ? o : 0;

        // 获取 20 部动漫
        var animes = await bangumiService.GetAnimeBatchAsync(BatchSize, offset);
        _logger.LogInformation("API 返回了 {Count} 部动漫", animes.Count);

        // 过滤已存在的（根据 BangumiId）
        var existingIds = await context.Anime
            .Where(a => a.BangumiId != null)
            .Select(a => a.BangumiId!.Value)
            .ToListAsync(stoppingToken);
        _logger.LogInformation("数据库已有 {Count} 个 BangumiId", existingIds.Count);

        var newAnimes = animes.Where(a => !existingIds.Contains(a.BangumiId!.Value)).ToList();
        _logger.LogInformation("过滤后有 {Count} 部新动漫", newAnimes.Count);

        if (newAnimes.Any())
        {
            int savedCount = 0;
            try
            {
                context.Anime.AddRange(newAnimes);
                await context.SaveChangesAsync(stoppingToken);
                savedCount = newAnimes.Count;
            }
            catch (DbUpdateException)
            {
                // 并发写入导致唯一冲突（用户手动添加与后台同步碰撞），逐条重试
                context.ChangeTracker.Clear();
                foreach (var anime in newAnimes)
                {
                    try
                    {
                        context.Anime.Add(anime);
                        await context.SaveChangesAsync(stoppingToken);
                        savedCount++;
                    }
                    catch (DbUpdateException)
                    {
                        context.ChangeTracker.Clear();
                        _logger.LogDebug("跳过已存在动漫 BangumiId: {Id}", anime.BangumiId);
                    }
                }
            }
            _logger.LogInformation("Bangumi 同步成功：新增 {Count} 部动漫", savedCount);
        }
        else
        {
            _logger.LogInformation("Bangumi 同步：本次无新增动漫（可能均已存在）");
        }

        // 更新 offset 和周标记
        var offsetConfig = context.SystemConfigs.FirstOrDefault(c => c.Key == OffsetKey);
        if (offsetConfig == null)
        {
            context.SystemConfigs.Add(new SystemConfig
            {
                Key = OffsetKey,
                Value = (offset + BatchSize).ToString(),
                LastUpdated = DateTime.UtcNow
            });
        }
        else
        {
            offsetConfig.Value = (offset + BatchSize).ToString();
            offsetConfig.LastUpdated = DateTime.UtcNow;
        }

        var weekConfig = context.SystemConfigs.FirstOrDefault(c => c.Key == WeekKey);
        var currentWeek = GetIsoWeekString(DateTime.Now);
        if (weekConfig == null)
        {
            context.SystemConfigs.Add(new SystemConfig
            {
                Key = WeekKey,
                Value = currentWeek,
                LastUpdated = DateTime.UtcNow
            });
        }
        else
        {
            weekConfig.Value = currentWeek;
            weekConfig.LastUpdated = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(stoppingToken);
    }

    private static string GetIsoWeekString(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        return $"{date.Year}-W{week:D2}";
    }
}
