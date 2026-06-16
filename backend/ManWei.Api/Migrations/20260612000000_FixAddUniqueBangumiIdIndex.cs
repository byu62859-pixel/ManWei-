using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManWei.Api.Migrations
{
    /// <summary>
    /// 修复 20260608031125_AddUniqueBangumiIdIndex 迁移的 Up()/Down() 方法体为空的问题。
    /// 那个迁移在历史里被 EF Core 标为已应用，但 IX_Anime_BangumiId 唯一 filtered 索引实际上从未在 DB 上创建。
    /// 本迁移兜底创建该索引；如有历史重复 BangumiId，则让 DB 报错并提示运维清理（不会自动去重 — 数据完整性优先）。
    /// </summary>
    public partial class FixAddUniqueBangumiIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 防御：如果 DB 里 BangumiId 已有重复行，CreateIndex 会失败。
            // 让它失败（不要静默去重），并把失败信息留给运维人员。
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT BangumiId FROM dbo.Anime
                    WHERE BangumiId IS NOT NULL
                    GROUP BY BangumiId
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    ;THROW 50001, 'Anime.BangumiId 存在重复行，无法创建唯一索引。请先手动清理（SELECT BangumiId, COUNT(*) FROM dbo.Anime WHERE BangumiId IS NOT NULL GROUP BY BangumiId HAVING COUNT(*) > 1）', 1;
                END
            ");

            // 创建 filtered unique index（BangumiId 是 int?，按 CLAUDE.md 禁止在唯一索引里放可空字段的显式 NULL，用 HasFilter 过滤）。
            // 仅在索引尚未存在时创建 — 避免对已通过手工 SQL 建好索引的 DB 重复执行。
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT * FROM sys.indexes
                    WHERE name = 'IX_Anime_BangumiId'
                      AND object_id = OBJECT_ID('dbo.Anime')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Anime_BangumiId]
                        ON [dbo].[Anime] ([BangumiId])
                        WHERE [BangumiId] IS NOT NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT * FROM sys.indexes
                    WHERE name = 'IX_Anime_BangumiId'
                      AND object_id = OBJECT_ID('dbo.Anime')
                )
                BEGIN
                    DROP INDEX [IX_Anime_BangumiId] ON [dbo].[Anime];
                END
            ");
        }
    }
}
