using Microsoft.EntityFrameworkCore;
using ManWei.Api.Data;
using ManWei.Api.Models;

namespace ManWei.Api.Common;

/// <summary>
/// 数据库初始化器 - 预置情感标签
/// </summary>
public static class DataSeeder
{
    private static readonly string[] PresetTagNames =
    {
        "泪崩", "热血", "治愈", "致郁", "笑死", "神作"
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 确保数据库已创建
        await context.Database.EnsureCreatedAsync();

        // 检查预置标签是否已存在
        var existingTags = await context.EmotionTags
            .Where(t => t.IsPreset && t.UserId == null)
            .Select(t => t.Name)
            .ToListAsync();

        var missingTags = PresetTagNames.Except(existingTags, StringComparer.OrdinalIgnoreCase).ToList();

        if (missingTags.Any())
        {
            var newTags = missingTags.Select(name => new EmotionTag
            {
                Name = name,
                IsPreset = true,
                UserId = null,
                CreateTime = DateTime.UtcNow
            });

            context.EmotionTags.AddRange(newTags);
            await context.SaveChangesAsync();
        }
    }
}
