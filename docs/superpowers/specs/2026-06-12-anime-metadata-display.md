# 动漫详情页 Bangumi 元信息补全 — 设计文档

**Date:** 2026-06-12
**Status:** Approved (待用户复核)
**Author:** brainstorming session

## Context

PC 客户端动漫详情页（`/anime/{id}`）封底下有大量空白，且除了封面、名称、追番状态、评分外没有展示动漫的"基本信息"和"题材标签"。

用户希望：
1. 在封底下补全**基本信息**：放送日期、片长、制作公司、监督、Bangumi 官方评分、排名
2. 在封底下加 **Bangumi 官方标签云**（Top 5 静态展示），帮助用户一眼判断题材

当前所有数据源是 Bangumi API `/v0/subjects/{id}`：
- 顶层字段 `date` (结构化 `"2008-04-06"`)
- `rating.score` / `rating.rank` / `rating.total`
- `infobox` (数字字符串 key 的字典，值为 `{key, value}` 对象)
- `tags` (`{name, count}[]`)

## Goals

1. 添加 Bangumi 元信息拉取逻辑，在 AddByBangumi 同步拉取，老数据通过详情页懒拉取补齐
2. 前端详情页在封底下展示"基本信息"卡 + "标签"卡，复用现有视觉语言
3. 内存级并发锁防多用户同时拉同一动漫
4. 区分"保护字段"（用户可能改过，不覆盖）和"覆盖字段"（纯 Bangumi 同步数据，每次覆盖）

## Non-Goals

- 不做"按标签筛选动漫"交互（标签点击无行为）
- 不展示老 `EmotionTag` 用户情感标签在 Bangumi 标签云里
- 不做 Bangumi 官方人员的 cast / 制作人员头像展示
- 不做"相似动漫推荐"
- 不批量回填老数据（用懒拉取按需补齐）

## Data Model

### Anime 实体新增字段

```csharp
public class Anime
{
    // ... 现有字段
    public DateOnly? AirDate { get; set; }             // 放送日期 (from Bangumi date)
    public string? Duration { get; set; }              // 片长 (from infobox "片长")
    public string? Producer { get; set; }              // 制作公司 (from infobox "动画制作" / "制作")
    public string? Director { get; set; }              // 监督/导演 (from infobox "导演" / "监督")
    public double? BangumiScore { get; set; }          // 官方评分 (from rating.score)
    public int? BangumiRank { get; set; }              // 排名 (from rating.rank)
    public int? BangumiRatingCount { get; set; }       // 评分人数 (from rating.total)
}
```

### 新表 AnimeTag (Bangumi 官方题材标签)

```csharp
public class AnimeTag
{
    public int Id { get; set; }
    public int AnimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }   // Bangumi 标签热度权重
    public Anime Anime { get; set; } = null!;
}
```

索引：`(AnimeId, Count DESC)`。

**注意**：现有 `EmotionTag` 实体（用户情感标签）保留不变，新表是另存的"动漫题材标签"，互不干扰。

## Backend Changes

### 1. BangumiService 扩展 DTO

`BangumiSubjectDto.cs` 扩展：
```csharp
[JsonPropertyName("rating")]
public BangumiRatingDto? Rating { get; set; }

[JsonPropertyName("infobox")]
public Dictionary<string, BangumiInfoboxItemDto>? Infobox { get; set; }

[JsonPropertyName("tags")]
public List<BangumiTagItemDto> Tags { get; set; } = new();

[JsonPropertyName("date")]
public string? Date { get; set; }
```

子 DTO：
```csharp
public class BangumiRatingDto
{
    [JsonPropertyName("rank")] public int? Rank { get; set; }
    [JsonPropertyName("total")] public int? Total { get; set; }
    [JsonPropertyName("score")] public double? Score { get; set; }
    [JsonPropertyName("count")] public Dictionary<string, int>? Count { get; set; }
}

public class BangumiInfoboxItemDto
{
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
}

public class BangumiTagItemDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("count")] public int Count { get; set; }
}
```

### 2. BangumiService 改造 `GetAndMapAnimeAsync`

**重构思路**：让 `GetAndMapAnimeAsync` 返回 `Anime` 实体 + tags 列表（共享一次 Bangumi HTTP 请求），避免 `AddByBangumi` 重复拉取。修改返回类型为 `(Anime, List<AnimeTag>)`。

```csharp
public async Task<(Anime? anime, List<AnimeTag> tags)?> GetAndMapAnimeAsync(int bangumiId)
{
    if (!await _rateLimiter.WaitForTokenAsync()) return null;
    
    try
    {
        var url = $"/v0/subjects/{bangumiId}";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        
        var content = await response.Content.ReadAsStringAsync();
        var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);
        if (subject == null) return null;
        
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
        
        // 2) 解析 date
        if (!string.IsNullOrEmpty(subject.Date) && DateOnly.TryParse(subject.Date, out var airDate))
            anime.AirDate = airDate;
        
        // 3) 解析 infobox
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
            .Select(t => new AnimeTag { AnimeId = 0, Name = t.Name, Count = t.Count })  // animeId 在 caller 处填
            .ToList();
        
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

**不新增 `GetAnimeTagsAsync`**：原方法会重复调用 `/v0/subjects/{bangumiId}`，但 `GetAndMapAnimeAsync` 已经拉过完整 subject（含 tags）。合并到一次请求，caller 一次拿到 anime + tags。

**兼容性影响**：
- `RefetchAnimeMetadataAsync` 也用同样逻辑：拉到 subject 后**同时**算 tags 替换（不再调 `GetAnimeTagsAsync`）
- `Sync` 接口同样只需调用 `GetAndMapAnimeAsync` 一次

### 4. BangumiService 新增 `RefetchAnimeMetadataAsync` (懒拉取)

```csharp
private static readonly ConcurrentDictionary<int, Task> _pendingFetches = new();

public async Task RefetchAnimeMetadataAsync(int animeId)
{
    // 1) 并发锁：同 animeId 同时只有一个 Task 在跑
    var task = _pendingFetches.GetOrAdd(animeId, _ => DoRefetchAsync(animeId));
    try
    {
        await task;
    }
    finally
    {
        // 2) KeyValuePair 重载：只删 key+value 匹配的，防止误删其他线程新写任务
        _pendingFetches.TryRemove(new KeyValuePair<int, Task>(animeId, task));
    }
}

private async Task DoRefetchAsync(int animeId)
{
    var anime = await _context.Anime.FindAsync(animeId);
    if (anime == null || anime.BangumiId == null) return;
    
    var bangumiId = anime.BangumiId.Value;
    
    try
    {
        // 拉 Bangumi 完整 subject
        var url = $"/v0/subjects/{bangumiId}";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return;
        
        var content = await response.Content.ReadAsStringAsync();
        var subject = JsonSerializer.Deserialize<BangumiSubjectDto>(content, _jsonOptions);
        if (subject == null) return;
        
        // 字段更新策略：保护字段不覆盖，纯 Bangumi 数据每次覆盖
        if (subject.Rating != null)
        {
            anime.BangumiScore = subject.Rating.Score;
            anime.BangumiRank = subject.Rating.Rank;
            anime.BangumiRatingCount = subject.Rating.Total;
        }
        if (subject.Infobox != null)
        {
            // 保护：仅当原值为 null 时填充
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
        var existingTags = await _context.AnimeTags.Where(t => t.AnimeId == animeId).ToListAsync();
        _context.AnimeTags.RemoveRange(existingTags);
        var newTags = (subject.Tags ?? new())
            .Where(t => t.Count > 0)
            .OrderByDescending(t => t.Count)
            .Take(5)
            .Select(t => new AnimeTag { AnimeId = animeId, Name = t.Name, Count = t.Count })
            .ToList();
        await _context.AnimeTags.AddRangeAsync(newTags);
        await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "懒拉取 Bangumi 元数据失败 AnimeId={Id}", animeId);
        // 不抛异常 — 静默失败，下次访问再重试
    }
}
```

**关键设计**：
- 并发锁用 `GetOrAdd` + `KeyValuePair` 重载删除，防误删
- 异常会被 `finally` 清掉 Faulted Task，下次请求能重试
- 失败静默，不影响详情页响应
- 保护字段（Duration/Producer/Director/AirDate）只在原值为 null 时填充

### 5. AnimeController.GetById 懒拉取触发

```csharp
public async Task<ActionResult<Result<AnimeDto>>> GetById(int id)
{
    var anime = await _context.Anime.FindAsync(id);
    if (anime == null) return NotFound(...);
    
    // 懒拉取条件：有 BangumiId + 元信息字段都为空
    if (anime.BangumiId.HasValue && 
        (anime.AirDate == null || anime.Producer == null || anime.BangumiScore == null))
    {
        try
        {
            await _bangumiService.RefetchAnimeMetadataAsync(anime.Id);
            // 用 Detach + 重新 FindAsync 强制刷新当前 DbContext
            _context.Entry(anime).State = EntityState.Detached;
            anime = await _context.Anime
                .Include(a => a.AnimeTags)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (anime == null) return NotFound(...);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "懒拉取后刷新失败 AnimeId={Id}", id);
            // 用旧对象继续
        }
    }
    
    // 构造 DTO 返回
    var dto = new AnimeDto { /* ... */, Tags = anime.AnimeTags?.ToList() ?? new() };
    return Ok(Result<AnimeDto>.Success(dto));
}
```

**场景 11 修复**：Detach + 重新 FindAsync 替代 `ReloadAsync`，**整个 refetch 块包 try-catch**，刷新失败时返回旧数据，下次访问再次触发懒拉取。

### 6. AnimeController.Sync 改造

Sync 接口（管理员手动刷新）也调用 `RefetchAnimeMetadataAsync`，但因 Sync 是管理员行为，可以强制覆盖：
- 不在 RefetchAnimeMetadataAsync 里加"管理员覆盖"参数
- 改为 Sync 接口内**直接调底层 `DoRefetchAsync`** 强制覆盖（或者新增一个 `RefetchAnimeMetadataForceAsync`）

**简化决策**：Sync 接口用 `GetAndMapAnimeAsync`（重载后返回 tags）走添加路径，调用时**强制覆盖**所有字段；不走 Refetch。

### 7. AddByBangumi 改造

**重构点**：`GetAndMapAnimeAsync` 已返回 `(Anime, List<AnimeTag>)`，tags 不用再单独拉。`GetEpisodesTotalAsync`（episodes 是另一个 endpoint `/v0/episodes`）需要单独拉，但和 tags 无关。

```csharp
// 旧代码调 GetAndMapAnimeAsync 拿到 anime；现在改用重载后的签名
var (mappedAnime, tags) = await _bangumiService.GetAndMapAnimeAsync(dto.BangumiId!.Value);
// 仍然走相同的添加收藏分支 B 流程
newAnime = mappedAnime;
_context.Anime.Add(newAnime);
await _context.SaveChangesAsync();

try
{
    // 1) 拉总集数（独立 endpoint）
    var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(dto.BangumiId!.Value);
    if (totalEpisodes.HasValue)
    {
        newAnime.TotalEpisodes = totalEpisodes.Value;
    }
    
    // 2) 写入 tags（来自 GetAndMapAnimeAsync 同一次响应，不需再发请求）
    if (tags.Any())
    {
        foreach (var t in tags) t.AnimeId = newAnime.Id;  // 补回 AnimeId
        await _context.AnimeTags.AddRangeAsync(tags);
    }
    
    await _context.SaveChangesAsync();
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "添加收藏后拉取 Bangumi 扩展数据失败 BangumiId={BangumiId}", dto.BangumiId);
}
```

**总调用次数对比**：
- 旧方案：1 次 `/v0/subjects`（GetAndMapAnimeAsync）+ 1 次 `/v0/subjects`（GetAnimeTagsAsync 重复拉）+ 1 次 `/v0/episodes` = **3 次**
- 新方案：1 次 `/v0/subjects`（GetAndMapAnimeAsync 含 tags）+ 1 次 `/v0/episodes` = **2 次**

**总 Bangumi 调用数减少 1/3**。

### 8. EF Core 迁移

新建 `2026xxxx_AddAnimeMetadataAndTags` 迁移：
- `Anime` 表加 7 个列（AirDate, Duration, Producer, Director, BangumiScore, BangumiRank, BangumiRatingCount）
- 新建 `AnimeTags` 表 + `(AnimeId, Count DESC)` 索引
- 不设置唯一索引（按 CLAUDE.md 规范，nullable 字段不参与 unique index）

## Frontend Changes

### 1. AnimeDto 类型扩展

`types/api.ts`：
```typescript
export interface Anime {
  // ... 现有字段
  airDate?: string | null;            // "2008-04-06"
  duration?: string | null;
  producer?: string | null;
  director?: string | null;
  bangumiScore?: number | null;
  bangumiRank?: number | null;
  bangumiRatingCount?: number | null;
  tags?: AnimeTag[];
}

export interface AnimeTag {
  name: string;
  count: number;
}
```

### 2. 详情页布局

**位置**：左列（`coverSection`）封面图片下方插入"基本信息"卡 + "标签"卡。

**复用现有视觉语言**：
- 使用 `styles.favoritePanel` / `favoritePanelTitle` / `favoritePanelContent` / `favoriteRow` / `favoriteLabel`（**0 个新 CSS 类**）
- Ant Design `<Tag>` 默认样式（**不**动态字号）
- 不引入 emoji 图标
- 不另起新背景色

**JSX 片段**：
```tsx
{/* 左列：封面 + 基本信息 + 标签 */}
<div className={styles.coverSection}>
  {favorite.animeCover ? (
    <img src={...} className={styles.cover} />
  ) : (
    <div className={styles.coverPlaceholder}>暂无封面</div>
  )}
  
  {/* 基本信息卡 */}
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
  
  {/* 标签卡 */}
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
</div>
```

### 3. 高度平衡（方案 A 暂定）

右列（`infoSection`）加 `position: sticky; top: 16px`，让短简介作品在用户滚左列详情时**右列陪同滚动**。

**测试后调整**：如果方案 A 效果不理想（用户反馈短简介仍难看），切换为其他方案。

## Data Flow

### 添加新收藏
```
User → POST /favorites/add {bangumiId}
  ↓
Controller: BangumiService.GetAndMapAnimeAsync(bangumiId)
  ↓ 返回 (Anime, List<AnimeTag>)，含 6 个新元字段 + tags
Controller: 单独 GetEpisodesTotalAsync
  ↓ 保存 TotalEpisodes
保存 AnimeTags（来自上面的返回值，不再发请求）
返回 FavoriteDto
```

### 访问老动漫详情
```
User → GET /anime/{id}
  ↓
Controller: 查 Anime
  ↓ BangumiId != null && 元字段都空
Controller: RefetchAnimeMetadataAsync(animeId)
  ↓ 并发锁 + 拉 Bangumi + 保护字段填充 + tags 替换
Controller: Detach + 重新 FindAsync
  ↓
返回 AnimeDto (含 tags)
```

## Error Handling

| 场景 | 处理 |
|---|---|
| 拉 Bangumi 限流 | 返回 null/空，日志 warning，添加/懒拉取仍成功 |
| 拉 Bangumi 4xx/5xx | 返回 null/空，不抛 |
| 拉 Bangumi 异常 | catch 后日志，不冒泡 |
| 懒拉取 Reload 失败 | Detach + 重新 FindAsync 失败时用旧对象返回，**下次访问再触发** |
| 懒拉取 Faulted Task 残留 | `finally` 块 `TryRemove` 清掉，下次请求重新触发 |
| `dto.Date` 解析失败 | 静默跳过，AirDate 保持 null |
| `infobox` value 是数组 | `ValueKind != String` 时跳过，不写入 |

## Testing

### 单元 / 集成
- 后端 `dotnet build` 通过
- 前端 `npm run build` 通过
- 添加新 Bangumi 动漫 → 数据库有 7 个新字段 + AnimeTags 5 条
- 访问老动漫详情页 → 触发懒拉取 → 字段自动填充

### E2E 边界场景
| # | 场景 | 预期 |
|---|---|---|
| 1 | 新添加 Bangumi 动漫 | 详情页立刻显示 airDate/producer/score/tags |
| 2 | 老动漫打开详情页 | 触发懒拉取 → UI 立即显示（Detach + FindAsync） |
| 3 | 同一动漫 5 个并发请求 | 1 次 Bangumi 请求（5 个 Task 合并） |
| 4 | 用户手动改 Producer | 懒拉取不覆盖，但 Score/Rank 覆盖 |
| 5 | 拉取失败 | 不抛异常，字段保持 null，日志 |
| 6 | `dto.Date` = "2008-04-06" | 解析为 DateOnly(2008, 4, 6) |
| 7 | `tags` 数组有 count<=0 | 过滤掉 |
| 8 | `infobox` value 是数组 | 跳过，不写入 |
| 9 | 老动漫无 BangumiId | 不触发懒拉取 |
| 10 | 反复刷新同一动漫 | 第二次起秒进 |
| 11 | ReloadAsync 失败 | 整个块 try-catch，返回旧数据，下次访问再触发 |
| 12 | 懒拉取抛异常 | Faulted Task 被 finally 清理，下次能重试 |

## Files to Modify

**Backend（创建）：**
- `backend/ManWei.Api/Models/AnimeTag.cs` — 新增实体
- `backend/ManWei.Api/Migrations/2026xxxxxx_AddAnimeMetadataAndTags.cs` — 新迁移

> **注**：所有 Bangumi 子 DTO（`BangumiRatingDto` / `BangumiInfoboxItemDto` / `BangumiTagItemDto`）**统一合并写入 `BangumiSubjectDto.cs`** 一个文件，避免拆散。

**Backend（修改）：**
- `backend/ManWei.Api/Models/Anime.cs` — 加 7 个字段
- `backend/ManWei.Api/DTOs/AnimeDto.cs` — AnimeDto 加 7 个字段 + Tags 列表
- `backend/ManWei.Api/DTOs/BangumiSubjectDto.cs` — 扩展 rating/infobox/tags/date
- `backend/ManWei.Api/Services/BangumiService.cs` — 改造 GetAndMapAnimeAsync（返回 tuple）+ 新增 RefetchAnimeMetadataAsync（并发锁）
- `backend/ManWei.Api/Controllers/AnimeController.cs` — GetById 懒拉取 + AddByBangumi 从 tuple 返回值取 tags

**Frontend（修改）：**
- `frontend/pc-client/src/types/api.ts` — Anime 加 7 字段 + AnimeTag 类型
- `frontend/pc-client/src/pages/AnimeDetail/index.tsx` — 左列插入"基本信息" + "标签"卡
- `frontend/pc-client/src/pages/AnimeDetail/AnimeDetail.module.css` — 仅加 `position: sticky` 给右列

## Open Questions

无（已全部确认）。
