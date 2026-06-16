using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ManWei.Api.Services;

public static class ManualMigration
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ManualMigration");

        var conn = context.Database.GetDbConnection();
        try
        {
            await conn.OpenAsync();

            // 检查 AnimeTags 表是否存在（作为迁移是否已执行的标记）
            var checkTable = await ExecuteScalarAsync(conn,
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AnimeTags'");

            if ((int)checkTable! > 0)
            {
                logger.LogInformation("AnimeTags 表已存在，跳过手动迁移");
                return;
            }

            logger.LogInformation("开始手动迁移：添加 Anime 元数据列 + AnimeTags 表");

            var sql = @"
                ALTER TABLE [Anime] ADD [AirDate] date NULL;
                ALTER TABLE [Anime] ADD [Duration] nvarchar(max) NULL;
                ALTER TABLE [Anime] ADD [Producer] nvarchar(max) NULL;
                ALTER TABLE [Anime] ADD [Director] nvarchar(max) NULL;
                ALTER TABLE [Anime] ADD [BangumiScore] float NULL;
                ALTER TABLE [Anime] ADD [BangumiRank] int NULL;
                ALTER TABLE [Anime] ADD [BangumiRatingCount] int NULL;

                CREATE TABLE [AnimeTags] (
                    [Id] int NOT NULL IDENTITY,
                    [AnimeId] int NOT NULL,
                    [Name] nvarchar(max) NOT NULL,
                    [Count] int NOT NULL,
                    CONSTRAINT [PK_AnimeTags] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_AnimeTags_Anime_AnimeId] FOREIGN KEY ([AnimeId]) REFERENCES [Anime] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_AnimeTags_AnimeId_Count] ON [AnimeTags] ([AnimeId], [Count]);
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();

            logger.LogInformation("手动迁移完成：Anime 元数据列 + AnimeTags 表已创建");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "手动迁移失败（可能列已存在）");
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private static async Task<object?> ExecuteScalarAsync(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }
}
