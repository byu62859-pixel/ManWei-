using Microsoft.EntityFrameworkCore;
using ManWei.Api.Models;

namespace ManWei.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Anime> Anime => Set<Anime>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<EmotionTag> EmotionTags => Set<EmotionTag>();
    public DbSet<AnimeTag> AnimeTags => Set<AnimeTag>();
    public DbSet<EmotionRecord> EmotionRecords => Set<EmotionRecord>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User: OpenId 唯一索引
        modelBuilder.Entity<User>()
            .HasIndex(u => u.OpenId)
            .IsUnique();

        // Anime: BangumiId 唯一索引
        modelBuilder.Entity<Anime>()
            .HasIndex(a => a.BangumiId)
            .IsUnique()
            .HasFilter("[BangumiId] IS NOT NULL");

        // Anime: Name 索引，优化模糊查询性能
        // 注意：LIKE '%keyword%' 前导通配符在 SQL Server 中无法利用 B-Tree 索引
        // 此索引对 LIKE 'keyword%' 有效，建议业务上同时支持前缀匹配
        modelBuilder.Entity<Anime>()
            .HasIndex(a => a.Name)
            .HasDatabaseName("IX_Anime_Name");

        // Favorite: 复合唯一索引 (UserId, AnimeId)
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.AnimeId })
            .IsUnique();

        // Favorite: 与 User 的多对一关系
        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Favorite: 与 Anime 的多对一关系
        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Anime)
            .WithMany(a => a.Favorites)
            .HasForeignKey(f => f.AnimeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Favorite: Rating CHECK 约束（F3 评分系统）
        modelBuilder.Entity<Favorite>()
            .Property(f => f.Rating)
            .HasColumnType("int");

        modelBuilder.Entity<Favorite>()
            .ToTable(t => t.HasCheckConstraint(
                "CK_Favorites_Rating",
                "Rating IS NULL OR (Rating >= 1 AND Rating <= 10)"));

        // EmotionRecord: 复合唯一索引 (FavoriteId, Episode)
        modelBuilder.Entity<EmotionRecord>()
            .HasIndex(er => new { er.FavoriteId, er.Episode })
            .IsUnique();

        // EmotionRecord: 与 Favorite 的多对一关系
        modelBuilder.Entity<EmotionRecord>()
            .HasOne(er => er.Favorite)
            .WithMany(f => f.EmotionRecords)
            .HasForeignKey(er => er.FavoriteId)
            .OnDelete(DeleteBehavior.Cascade);

        // EmotionTag: 与 User 的多对一关系（可选，用于自定义标签）
        modelBuilder.Entity<EmotionTag>()
            .HasOne(et => et.User)
            .WithMany(u => u.EmotionTags)
            .HasForeignKey(et => et.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // EmotionTag: 复合唯一索引 (UserId, AnimeId, Name)
        modelBuilder.Entity<EmotionTag>()
            .HasIndex(et => new { et.UserId, et.AnimeId, et.Name })
            .IsUnique();

        // EmotionTag: 查询优化索引 (AnimeId, UserId, Name)
        // 用于 F2 标签筛选时：WHERE AnimeId = xxx AND (UserId = yyy OR UserId = NULL) AND Name = 'xxx'
        modelBuilder.Entity<EmotionTag>()
            .HasIndex(et => new { et.AnimeId, et.UserId, et.Name });

        // EmotionTag: 与 Anime 的多对一关系（可选，SetNull 避免多 cascade 路径）
        modelBuilder.Entity<EmotionTag>()
            .HasOne(et => et.Anime)
            .WithMany()
            .HasForeignKey(et => et.AnimeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Review: FavoriteId 唯一索引（一篇观后感对应一次收藏）
        modelBuilder.Entity<Review>()
            .HasIndex(r => r.FavoriteId)
            .IsUnique();

        // Review: 与 Favorite 的一对一关系
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Favorite)
            .WithOne(f => f.Review)
            .HasForeignKey<Review>(r => r.FavoriteId)
            .OnDelete(DeleteBehavior.Cascade);

        // SystemConfig: Key 唯一索引（用于 Bangumi 同步状态持久化）
        modelBuilder.Entity<SystemConfig>()
            .HasIndex(sc => sc.Key)
            .IsUnique();

        // AnimeTag: 复合索引 (AnimeId, Count DESC) — 标签按热度排序展示
        modelBuilder.Entity<AnimeTag>()
            .HasIndex(at => new { at.AnimeId, at.Count });

        // AnimeTag: 与 Anime 的多对一关系（Cascade 删除避免孤儿标签）
        modelBuilder.Entity<AnimeTag>()
            .HasOne(at => at.Anime)
            .WithMany(a => a.AnimeTags)
            .HasForeignKey(at => at.AnimeId)
            .OnDelete(DeleteBehavior.Cascade);

        }
}
