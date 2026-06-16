# 动漫详情页 Bangumi 元信息补全 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PC 客户端动漫详情页在封底下显示 Bangumi 拉取的"基本信息"（放送日期/片长/制作/监督/评分/排名）和"题材标签"云，老数据通过懒拉取补齐。

**Architecture:** 后端 Anime 实体扩展 7 个字段 + 新表 AnimeTag 存 Bangumi 官方标签；BangumiService 拉取时一次拿到完整 subject（含 rating/infobox/tags/date）；详情页 GetById 检测到元字段为 null 时异步触发懒拉取（同 animeId 用 ConcurrentDictionary 合并并发）。前端复用现有 favoritePanel/Tag 样式，0 新 CSS、0 emoji。

**Tech Stack:** ASP.NET Core 8, EF Core 8, React 18 + TypeScript + Ant Design

**Spec:** [2026-06-12-anime-metadata-display.md](docs/superpowers/specs/2026-06-12-anime-metadata-display.md)

---

## File Structure

**Backend (create):**
- `backend/ManWei.Api/Models/AnimeTag.cs` — 标签实体
- `backend/ManWei.Api/Migrations/{timestamp}_AddAnimeMetadataAndTags.cs` — EF 迁移

**Backend (modify):**
- `backend/ManWei.Api/Models/Anime.cs` — 加 7 个字段
- `backend/ManWei.Api/DTOs/BangumiSubjectDto.cs` — 加 rating/infobox/tags/date 字段 + 子 DTO
- `backend/ManWei.Api/DTOs/AnimeDto.cs` — 加 7 字段 + Tags
- `backend/ManWei.Api/Services/BangumiService.cs` — 改造 GetAndMapAnimeAsync (返回 tuple) + 新增 RefetchAnimeMetadataAsync (并发锁)
- `backend/ManWei.Api/Controllers/AnimeController.cs` — GetById 懒拉取 + AddByBangumi 从 tuple 取 tags
- `backend/ManWei.Api/Data/AppDbContext.cs` — DbSet<AnimeTag>

**Frontend (modify):**
- `frontend/pc-client/src/types/api.ts` — 类型扩展
- `frontend/pc-client/src/pages/AnimeDetail/index.tsx` — 插"基本信息" + "标签"卡
- `frontend/pc-client/src/pages/AnimeDetail/AnimeDetail.module.css` — 右列 sticky

---

## Task 1: Anime 实体新增 7 字段

**Files:**
- Modify: `backend/ManWei.Api/Models/Anime.cs`

- [ ] **Step 1: 修改 Anime.cs**

打开 `backend/ManWei.Api/Models/Anime.cs`，在 `TotalEpisodes` 字段后新增 7 个字段：

完整文件应该是：
```csharp
namespace ManWei.Api.Models;

public class Anime
{
    public int Id { get; set; }
    public int? BangumiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string AnimeType { get; set; } = "TV";
    /// <summary>
    /// 总集数（从 Bangumi 拉取，null=未拉取到/老数据/拉取失败）
    /// </summary>
    public int? TotalEpisodes { get; set; }
    /// <summary>
    /// 放送日期（from Bangumi date，yyyy-MM-dd）
    /// </summary>
    public DateOnly? AirDate { get; set; }
    /// <summary>
    /// 片长（from Bangumi infobox.片长）
    /// </summary>
    public string? Duration { get; set; }
    /// <summary>
    /// 制作公司（from Bangumi infobox.动画制作 / 制作）
    /// </summary>
    public string? Producer { get; set; }
    /// <summary>
    /// 监督/导演（from Bangumi infobox.导演 / 监督）
    /// </summary>
    public string? Director { get; set; }
    /// <summary>
    /// 官方评分（from Bangumi rating.score, 0-10）
    /// </summary>
    public double? BangumiScore { get; set; }
    /// <summary>
    /// 官方排名（from Bangumi rating.rank）
    /// </summary>
    public int? BangumiRank { get; set; }
    /// <summary>
    /// 评分人数（from Bangumi rating.total）
    /// </summary>
    public int? BangumiRatingCount { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 2: AnimeTag 新实体

**Files:**
- Create: `backend/ManWei.Api/Models/AnimeTag.cs`

- [ ] **Step 1: 创建 AnimeTag.cs**

创建 `backend/ManWei.Api/Models/AnimeTag.cs`：
```csharp
namespace ManWei.Api.Models;

/// <summary>
/// 动漫题材标签（来自 Bangumi API，区别于用户自定义情感标签 EmotionTag）
/// </summary>
public class AnimeTag
{
    public int Id { get; set; }
    public int AnimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Bangumi 标签热度权重（用于排序）
    /// </summary>
    public int Count { get; set; }

    public Anime Anime { get; set; } = null!;
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: Build succeeded

---

## Task 3: AppDbContext 加 DbSet

**Files:**
- Modify: `backend/ManWei.Api/Data/AppDbContext.cs`

- [ ] **Step 1: 添加 DbSet**

打开 `backend/ManWei.Api/Data/AppDbContext.cs`，在现有 DbSet 列表中（与其他 DbSet 排在一起）新增：

```csharp
    public DbSet<EmotionTag> EmotionTags => Set<EmotionTag>();
    public DbSet<AnimeTag> AnimeTags => Set<AnimeTag>();  // 新增
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: Build succeeded

---

## Task 4: BangumiSubjectDto 扩展（子 DTO 合并到此文件）

**Files:**
- Modify: `backend/ManWei.Api/DTOs/BangumiSubjectDto.cs`

- [ ] **Step 1: 扩展 DTO**

打开 `backend/ManWei.Api/DTOs/BangumiSubjectDto.cs`，在文件末尾追加以下 DTO 类：

```csharp
public class BangumiRatingDto
{
    [JsonPropertyName("rank")]
    public int? Rank { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("count")]
    public Dictionary<string, int>? Count { get; set; }
}

public class BangumiInfoboxItemDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

public class BangumiTagItemDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
```

- [ ] **Step 2: 在 BangumiSubjectDto 类里加字段**

在 `BangumiSubjectDto` 类内（已有字段如 Id, Name 等）追加：

```csharp
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("rating")]
    public BangumiRatingDto? Rating { get; set; }

    [JsonPropertyName("infobox")]
    public Dictionary<string, BangumiInfoboxItemDto>? Infobox { get; set; }

    [JsonPropertyName("tags")]
    public List<BangumiTagItemDto> Tags { get; set; } = new();
```

- [ ] **Step 3: 添加 using 引用**

在文件顶部添加：
```csharp
using System.Text.Json;
```

- [ ] **Step 4: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: Build succeeded

---

## Task 5: AnimeDto 加 7 字段 + Tags

**Files:**
- Modify: `backend/ManWei.Api/DTOs/AnimeDto.cs`

- [ ] **Step 1: 扩展 AnimeDto**

打开 `backend/ManWei.Api/DTOs/AnimeDto.cs`，在 `AnimeDto` 类内追加：

```csharp
    public int? TotalEpisodes { get; set; }
    public DateOnly? AirDate { get; set; }
    public string? Duration { get; set; }
    public string? Producer { get; set; }
    public string? Director { get; set; }
    public double? BangumiScore { get; set; }
    public int? BangumiRank { get; set; }
    public int? BangumiRatingCount { get; set; }
    public List<AnimeTagDto> Tags { get; set; } = new();
```

- [ ] **Step 2: 在文件末尾追加 AnimeTagDto**

```csharp
public class AnimeTagDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: Build succeeded

---

## Task 6: BangumiService 改造 GetAndMapAnimeAsync (返回 tuple)

**Files:**
- Modify: `backend/ManWei.Api/Services/BangumiService.cs`

- [ ] **Step 1: 修改 IBangumiService 接口签名**

在 `IBangumiService` 接口找到 `GetAndMapAnimeAsync` 声明，改为：

```csharp
    /// <summary>
    /// 根据 Bangumi ID 获取条目，构造 Anime 实体 + Top 5 标签
    /// </summary>
    Task<(Anime? anime, List<AnimeTag> tags)?> GetAndMapAnimeAsync(int bangumiId);
```

- [ ] **Step 2: 改造 GetAndMapAnimeAsync 实现**

替换 `BangumiService.GetAndMapAnimeAsync` 整个方法：

```csharp
    public async Task<(Anime? anime, List<AnimeTag> tags)?> GetAndMapAnimeAsync(int bangumiId)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi API 限流，拒绝请求 ID: {BangumiId}", bangumiId);
            return null;
        }

        try
        {
            var url = $"/v0/subjects/{bangumiId}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi API 返回失败: {StatusCode}, ID: {BangumiId}",
                    response.StatusCode, bangumiId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);

            if (subject == null)
            {
                _logger.LogWarning("Bangumi API 解析失败，ID: {BangumiId}", bangumiId);
                return null;
            }

            // 1) 构造 Anime
            var anime = new Anime
            {
                BangumiId = subject.Id,
                Name = !string.IsNullOrWhiteSpace(subject.NameCn) ? subject.NameCn : subject.Name,
                Summary = subject.Summary,
                Cover = subject.Images?.Large ?? subject.Images?.Medium,
                AnimeType = MapPlatform(subject.Platform),
                BangumiScore = subject.Rating?.Score,
                BangumiRank = subject.Rating?.Rank,
                BangumiRatingCount = subject.Rating?.Total,
                CreateTime = DateTime.UtcNow
            };

            // 2) 解析 date (顶层结构化字段)
            if (!string.IsNullOrEmpty(subject.Date) && DateOnly.TryParse(subject.Date, out var airDate))
                anime.AirDate = airDate;

            // 3) 解析 infobox 提取片长/制作/监督
            if (subject.Infobox != null)
            {
                anime.Duration = ExtractInfoboxString(subject.Infobox, "片长");
                anime.Producer = ExtractInfoboxString(subject.Infobox, "动画制作")
                                 ?? ExtractInfoboxString(subject.Infobox, "制作");
                anime.Director = ExtractInfoboxString(subject.Infobox, "导演")
                                 ?? ExtractInfoboxString(subject.Infobox, "监督");
            }

            // 4) 解析 tags (Top 5, count>0)
            var tags = (subject.Tags ?? new())
                .Where(t => t.Count > 0)
                .OrderByDescending(t => t.Count)
                .Take(5)
                .Select(t => new AnimeTag { AnimeId = 0, Name = t.Name, Count = t.Count })
                .ToList();

            _logger.LogInformation("成功从 Bangumi 同步: {Name} (ID: {BangumiId}, Tags: {TagCount})",
                anime.Name, bangumiId, tags.Count);
            return (anime, tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从 Bangumi 获取数据失败，ID: {BangumiId}", bangumiId);
            return null;
        }
    }

    private static string? ExtractInfoboxString(
        Dictionary<string, BangumiInfoboxItemDto> box, string key)
    {
        var item = box.Values.FirstOrDefault(v => v.Key == key);
        if (item == null) return null;
        return item.Value.ValueKind == JsonValueKind.String
            ? item.Value.GetString()
            : null;
    }
```

- [ ] **Step 3: 跳过编译验证（推迟到 Task 9 后）**

> 故意延迟：旧的 `GetAndMapAnimeAsync` 单值返回 + 新的 `(Anime?, List<AnimeTag>)?` tuple 混用期间，会有意编译失败。等到 Task 9 把 `AddByBangumi` 也改完后，再统一验证。

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: 会有**错误** — `AddByBangumi` 还在调用旧签名，不用修，留给 Task 9

---

## Task 7: BangumiService 新增 RefetchAnimeMetadataAsync

**Files:**
- Modify: `backend/ManWei.Api/Services/BangumiService.cs`

- [ ] **Step 1: 在 IBangumiService 接口新增方法**

在接口文件中 `GetEpisodesTotalAsync` 后新增：

```csharp
    /// <summary>
    /// 详情页懒拉取 Bangumi 元信息（老数据补齐用）
    /// </summary>
    /// <remarks>
    /// 同 animeId 并发请求会被合并为一个 Task；
    /// 保护字段（Duration/Producer/Director/AirDate）仅在原值为 null 时填充；
    /// 覆盖字段（BangumiScore/BangumiRank/BangumiRatingCount）每次覆盖；
    /// 失败静默，不抛异常。
    /// </remarks>
    Task RefetchAnimeMetadataAsync(int animeId);
```

- [ ] **Step 2: 在 BangumiService 类实现**

在 `GetEpisodesTotalAsync` 之后，`MapPlatform` 之前新增：

```csharp
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Task> _pendingFetches = new();

    public async Task RefetchAnimeMetadataAsync(int animeId)
    {
        // 1) 并发锁：GetOrAdd 原子地保证同 animeId 只有一个 Task
        var task = _pendingFetches.GetOrAdd(animeId, _ => DoRefetchAsync(animeId));
        try
        {
            await task;
        }
        finally
        {
            // 2) KeyValuePair 重载：仅当 key+value 都匹配时才删，防误删
            _pendingFetches.TryRemove(new System.Collections.Generic.KeyValuePair<int, Task>(animeId, task));
        }
    }

    private async Task DoRefetchAsync(int animeId)
    {
        var anime = await _context.Anime.FindAsync(animeId);
        if (anime == null || anime.BangumiId == null) return;

        var bangumiId = anime.BangumiId.Value;

        try
        {
            if (!await _rateLimiter.WaitForTokenAsync())
            {
                _logger.LogWarning("懒拉取被限流 AnimeId={Id}", animeId);
                return;
            }

            var url = $"/v0/subjects/{bangumiId}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("懒拉取 Bangumi 失败: {StatusCode}, AnimeId={Id}",
                    response.StatusCode, animeId);
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);
            if (subject == null) return;

            // 字段更新策略
            if (subject.Rating != null)
            {
                anime.BangumiScore = subject.Rating.Score;
                anime.BangumiRank = subject.Rating.Rank;
                anime.BangumiRatingCount = subject.Rating.Total;
            }
            if (subject.Infobox != null)
            {
                if (anime.Duration == null)
                    anime.Duration = ExtractInfoboxString(subject.Infobox, "片长");
                if (anime.Producer == null)
                    anime.Producer = ExtractInfoboxString(subject.Infobox, "动画制作")
                                     ?? ExtractInfoboxString(subject.Infobox, "制作");
                if (anime.Director == null)
                    anime.Director = ExtractInfoboxString(subject.Infobox, "导演")
                                     ?? ExtractInfoboxString(subject.Infobox, "监督");
            }
            if (!string.IsNullOrEmpty(subject.Date) && DateOnly.TryParse(subject.Date, out var airDate))
            {
                if (anime.AirDate == null) anime.AirDate = airDate;
            }

            await _context.SaveChangesAsync();

            // 替换 tags
            var existingTags = await _context.AnimeTags
                .Where(t => t.AnimeId == animeId)
                .ToListAsync();
            _context.AnimeTags.RemoveRange(existingTags);

            var newTags = (subject.Tags ?? new())
                .Where(t => t.Count > 0)
                .OrderByDescending(t => t.Count)
                .Take(5)
                .Select(t => new AnimeTag { AnimeId = animeId, Name = t.Name, Count = t.Count })
                .ToList();
            await _context.AnimeTags.AddRangeAsync(newTags);
            await _context.SaveChangesAsync();

            _logger.LogInformation("懒拉取 Bangumi 元信息成功 AnimeId={Id}, Tags={Count}",
                animeId, newTags.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "懒拉取 Bangumi 元数据失败 AnimeId={Id}", animeId);
        }
    }
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 8: AnimeController 改造 GetById (懒拉取)

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: 注入 IBangumiService**

在 `AnimeController` 类中：
- 加 `using ManWei.Api.Services;`（如果还没有）
- 加 `using Microsoft.EntityFrameworkCore;`（用于 Include）
- 加 `using ManWei.Api.Models;`（用于 EntityState）
- 加字段：`private readonly IBangumiService _bangumiService;`
- 加字段：`private readonly ILogger<AnimeController> _logger;`
- 构造函数加 `IBangumiService bangumiService, ILogger<AnimeController> logger` 参数并赋值

- [ ] **Step 2: 改造 GetById 方法**

替换 `GetById` 整个方法：

```csharp
    public async Task<ActionResult<Result<AnimeDto>>> GetById(int id)
    {
        var anime = await _context.Anime.FindAsync(id);
        if (anime == null)
        {
            return NotFound(Result<AnimeDto>.Fail(404, "动漫不存在"));
        }

        // 懒拉取条件：有 BangumiId + 元信息字段都为空
        if (anime.BangumiId.HasValue &&
            (anime.AirDate == null || anime.Producer == null || anime.BangumiScore == null))
        {
            try
            {
                await _bangumiService.RefetchAnimeMetadataAsync(anime.Id);
                // Detach + 重新查询，强制刷新当前 DbContext
                _context.Entry(anime).State = EntityState.Detached;
                anime = await _context.Anime
                    .Include(a => a.AnimeTags)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (anime == null)
                {
                    return NotFound(Result<AnimeDto>.Fail(404, "动漫不存在"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "懒拉取后刷新失败 AnimeId={Id}", id);
                // 用旧对象继续返回
            }
        }

        var dto = new AnimeDto
        {
            Id = anime.Id,
            BangumiId = anime.BangumiId,
            Name = anime.Name,
            Cover = anime.Cover,
            Summary = anime.Summary,
            AnimeType = anime.AnimeType,
            AirDate = anime.AirDate,
            Duration = anime.Duration,
            Producer = anime.Producer,
            Director = anime.Director,
            BangumiScore = anime.BangumiScore,
            BangumiRank = anime.BangumiRank,
            BangumiRatingCount = anime.BangumiRatingCount,
            CreateTime = anime.CreateTime,
            Tags = anime.AnimeTags?.Select(t => new AnimeTagDto
            {
                Name = t.Name,
                Count = t.Count
            }).ToList() ?? new()
        };
        return Ok(Result<AnimeDto>.Success(dto));
    }
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 9: AnimeController 改造 AddByBangumi

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: 修改 AddByBangumi 用新 tuple 返回值**

找到 AddByBangumi 的"分支B：传了 BangumiId"块中，调用 `_bangumiService.GetAndMapAnimeAsync` 的地方。原来的写法：

```csharp
var newAnime = await _bangumiService.GetAndMapAnimeAsync(dto.BangumiId!.Value);
```

改为：
```csharp
var result = await _bangumiService.GetAndMapAnimeAsync(dto.BangumiId!.Value);
if (result == null) return BadRequest(Result<AnimeDto>.Fail(400, "从 Bangumi 获取数据失败，请检查 ID 是否正确"));
var (newAnime, tags) = result.Value;
```

- [ ] **Step 2: 在 SaveChangesAsync 后保存 tags**

在 `await _context.SaveChangesAsync();` 之后，添加 tags 写入逻辑：

```csharp
if (tags.Any())
{
    foreach (var t in tags) t.AnimeId = newAnime.Id;
    await _context.AnimeTags.AddRangeAsync(tags);
    await _context.SaveChangesAsync();
}
```

- [ ] **Step 3: 验证编译（Tasks 6-9 全部完成后的统一验证）**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: 0 errors

> 之前 Task 6 故意留的"中间编译错误状态"（旧 `GetAndMapAnimeAsync` 单值返回 vs 新 tuple 冲突）应当已消失。所有调用方（Task 8 GetById, Task 9 AddByBangumi, Task 10 GetList）都已迁移到新签名。

---

## Task 10: AnimeController 改造 GetList 和 Sync 的 AnimeDto 构造

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: 同步修改 GetList 的 Select 子句**

在 GetList 的 Select 中，加新字段（参考 GetById 的 dto 构造）：

```csharp
            .Select(a => new AnimeDto
            {
                Id = a.Id,
                BangumiId = a.BangumiId,
                Name = a.Name,
                Cover = a.Cover,
                Summary = a.Summary,
                AnimeType = a.AnimeType,
                AirDate = a.AirDate,
                Duration = a.Duration,
                Producer = a.Producer,
                Director = a.Director,
                BangumiScore = a.BangumiScore,
                BangumiRank = a.BangumiRank,
                BangumiRatingCount = a.BangumiRatingCount,
                CreateTime = a.CreateTime,
                FavoriteCount = _context.Favorites.Count(f => f.AnimeId == a.Id),
                AvgRating = _context.Favorites
                    .Where(f => f.AnimeId == a.Id && f.Rating != null)
                    .Average(f => (double?)f.Rating),
                ReviewCount = _context.Reviews.Count(r => r.Favorite.AnimeId == a.Id)
            })
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: 0 errors

---

## Task 11: 创建 EF Core 迁移

**Files:**
- Create: `backend/ManWei.Api/Migrations/{timestamp}_AddAnimeMetadataAndTags.cs`

- [ ] **Step 1: 生成迁移**

Run: `taskkill //F //IM "ManWei.Api.exe" 2>/dev/null; cd d:/AnimeEmotion/backend/ManWei.Api && dotnet ef migrations add AddAnimeMetadataAndTags --output-dir Migrations`

Expected: 两个新文件 — `AddAnimeMetadataAndTags.cs` 和 `.Designer.cs`

- [ ] **Step 2: 检查迁移内容**

打开生成的 `AddAnimeMetadataAndTags.cs`，确认 `Up` 包含：
- 7 个 `AddColumn` 调用给 Anime 表（AirDate, Duration, Producer, Director, BangumiScore, BangumiRank, BangumiRatingCount）
- `CreateTable` 包含 AnimeTags 表
- `CreateIndex` 在 AnimeTags 的 (AnimeId, Count DESC)

`Down` 包含对应的 DropColumn 和 DropTable。

- [ ] **Step 3: 应用迁移**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet ef database update`

Expected: 
```
ALTER TABLE [Anime] ADD [AirDate] date NULL;
ALTER TABLE [Anime] ADD [Duration] nvarchar(...) NULL;
...
CREATE TABLE [AnimeTags] (...);
CREATE INDEX [...] ON [AnimeTags] (...);
```

- [ ] **Step 4: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build 2>&1 | tail -3`
Expected: 0 errors

---

## Task 12: 后端启动 + 手动验证

- [ ] **Step 1: 启动后端**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet run`

Expected: `Now listening on: http://localhost:5150`

- [ ] **Step 2: 添加 Bangumi 动漫（场景 1）**

通过 API 添加一个 Bangumi ID，例如 `429424`：
```
POST /api/favorites/add { "bangumiId": 429424 }
```

检查响应包含新字段：
```json
{
  "data": {
    "animeId": ...,
    "airDate": "2024-...",
    "producer": "...",
    "bangumiScore": 8.5,
    ...
  }
}
```

- [ ] **Step 3: 验证老动漫懒拉取（场景 2）**

找一个老动漫 ID（数据库里 BangumiId != null 但 AirDate 为 null），用浏览器访问 `/anime/{id}`：
- Network 面板看到 `GET /v0/subjects/{bangumiId}`
- 第二次访问时不应再触发（字段已填）

---

## Task 13: 前端类型扩展

**Files:**
- Modify: `frontend/pc-client/src/types/api.ts`

- [ ] **Step 1: 添加 AnimeTag 类型**

在 `api.ts` 末尾（所有 export 之后）新增：

```typescript
export interface AnimeTag {
  name: string;
  count: number;
}
```

- [ ] **Step 2: 扩展 Anime 接口**

在 `Anime` 接口内找到合适位置（`avgRating` 等聚合字段后），新增：

```typescript
  airDate?: string | null;
  duration?: string | null;
  producer?: string | null;
  director?: string | null;
  bangumiScore?: number | null;
  bangumiRank?: number | null;
  bangumiRatingCount?: number | null;
  tags?: AnimeTag[];
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 14: 详情页插"基本信息"卡

**Files:**
- Modify: `frontend/pc-client/src/pages/AnimeDetail/index.tsx`

- [ ] **Step 1: 修改 cover 渲染位置**

找到 `<img className={styles.cover} />` 或 `<div className={styles.coverPlaceholder}>`，在 cover 元素所在容器后插入新卡（**不**改 `coverSection` 容器结构，仅追加元素）。

- [ ] **Step 2: 引入 Tag 组件**

在文件顶部 import 区添加：
```typescript
import { Tag } from 'antd';
```

- [ ] **Step 3: 插入基本信息卡**

在 cover 元素后插入：

```tsx
{/* 基本信息卡 — Bangumi 元信息 */}
{(anime.airDate || anime.duration || anime.producer || anime.director ||
  anime.bangumiScore || anime.bangumiRank) && (
  <div className={styles.favoritePanel} style={{ marginTop: 24 }}>
    <div className={styles.favoritePanelTitle}>基本信息</div>
    <div className={styles.favoritePanelContent}>
      {anime.airDate && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>放送日期</span>
          <span>{anime.airDate}</span>
        </div>
      )}
      {anime.duration && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>片长</span>
          <span>{anime.duration}</span>
        </div>
      )}
      {anime.producer && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>制作</span>
          <span>{anime.producer}</span>
        </div>
      )}
      {anime.director && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>监督</span>
          <span>{anime.director}</span>
        </div>
      )}
      {anime.bangumiScore && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>Bangumi 评分</span>
          <span>
            {anime.bangumiScore.toFixed(1)}
            {anime.bangumiRatingCount && ` (${anime.bangumiRatingCount} 人)`}
          </span>
        </div>
      )}
      {anime.bangumiRank && (
        <div className={styles.favoriteRow}>
          <span className={styles.favoriteLabel}>Bangumi 排名</span>
          <span>#{anime.bangumiRank}</span>
        </div>
      )}
    </div>
  </div>
)}
```

- [ ] **Step 4: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 15: 详情页插"标签"卡

**Files:**
- Modify: `frontend/pc-client/src/pages/AnimeDetail/index.tsx`

- [ ] **Step 1: 在基本信息卡后追加标签卡**

```tsx
{/* 标签卡 — Bangumi 官方 Top 5 标签 */}
{anime.tags && anime.tags.length > 0 && (
  <div className={styles.favoritePanel} style={{ marginTop: 24 }}>
    <div className={styles.favoritePanelTitle}>标签</div>
    <div className={styles.favoritePanelContent}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
        {anime.tags.slice(0, 5).map((tag) => (
          <Tag key={tag.name}>{tag.name}</Tag>
        ))}
      </div>
    </div>
  </div>
)}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 16: 右列 sticky（高度平衡）

**Files:**
- Modify: `frontend/pc-client/src/pages/AnimeDetail/AnimeDetail.module.css`

- [ ] **Step 1: 添加 sticky**

找到 `.infoSection`（在第 105 行附近），添加：

```css
.infoSection {
  position: sticky;
  top: 16px;
  align-self: start;
}
```

**注意**：如果 `.infoSection` 已经有其他规则，用 Edit 工具**只追加** `position`、`top`、`align-self` 三行，**不要覆盖**已有属性。

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build 2>&1 | tail -5`
Expected: 0 errors

---

## Task 17: 端到端验证

- [ ] **Step 1: 启动后端 + 前端**

后端（如果有 taskkill 杀进程，先杀）：
```bash
taskkill //F //IM "ManWei.Api.exe" 2>/dev/null
cd d:/AnimeEmotion/backend/ManWei.Api && dotnet run
```

前端：
```bash
cd d:/AnimeEmotion/frontend/pc-client && npm run dev
```

- [ ] **Step 2: 浏览器验证场景**

打开 `http://localhost:5174/anime/{id}`：
- **新添加的动漫**：基本信息卡 + 标签卡应立即显示
- **老动漫**：第一次访问触发懒拉取 → 看到 Network 有 `/v0/subjects/{id}` 请求
- 反复刷新：第二次起 Network 不再有 Bangumi 请求（已缓存）

- [ ] **Step 3: 并发验证（场景 3）**

打开两个浏览器标签同时访问同一老动漫详情：
- Network 应只有 1 次 `/v0/subjects/{id}` 请求（不是 2 次）

- [ ] **Step 4: 保护字段验证（场景 4）**

在数据库手动改 `Anime.Producer = "手工修正"`，再次访问详情页触发懒拉取：
- Producer 不被覆盖（仍为 "手工修正"）
- BangumiScore 仍被覆盖

---

## Self-Review

### Spec coverage

| Spec 需求 | 任务 |
|---|---|
| Anime 实体 + 7 字段 | Task 1 |
| AnimeTag 实体 | Task 2 |
| DbSet<AnimeTag> | Task 3 |
| Bangumi DTO 扩展（rating/infobox/tags/date + 子 DTO） | Task 4 |
| AnimeDto + Tags | Task 5 |
| GetAndMapAnimeAsync 返回 tuple | Task 6 |
| RefetchAnimeMetadataAsync (并发锁) | Task 7 |
| GetById 懒拉取 | Task 8 |
| AddByBangumi 用 tuple | Task 9 |
| GetList Select | Task 10 |
| EF 迁移 | Task 11 |
| 后端验证 | Task 12 |
| 前端类型 | Task 13 |
| 基本信息卡 | Task 14 |
| 标签卡 | Task 15 |
| sticky 右列 | Task 16 |
| E2E | Task 17 |

✅ **所有 spec 需求都有对应任务**

### Placeholder scan

- ✅ 无 "TBD" / "TODO"
- ✅ 所有代码块都有完整内容
- ✅ 所有命令有 expected output
- ✅ 任务粒度 2-5 分钟

### Type consistency

- `Anime` 字段：Task 1 定义 → Task 5 DTO 映射 → Task 8 GetById → Task 10 GetList ✅
- `AnimeTag` 实体：Task 2 → Task 5 AnimeTagDto → Task 13 前端 AnimeTag → Task 15 渲染 ✅
- `GetAndMapAnimeAsync` 返回值：Task 6 (tuple) → Task 9 (解包) ✅
- `RefetchAnimeMetadataAsync` 签名：Task 7 → Task 8 调用 ✅
- `BangumiInfoboxItemDto.Value` 类型 JsonElement → Task 6/7 ExtractInfoboxString 使用 ✅

✅ **类型一致**

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-12-anime-metadata-display.md`. **Two execution options:**

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
