# 动漫总集数获取与进度上限限制 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用户通过 BangumiId 添加收藏时自动拉取动漫总集数，缓存到 `Anime.TotalEpisodes`，前端进度输入框与后端校验以此为上限。

**Architecture:**
- 后端在 `Anime` 表新增 `TotalEpisodes int?` 字段
- `BangumiService` 新增 `GetEpisodesTotalAsync(bangumiId)` 调 Bangumi `GET /v0/episodes?subject_id={id}&type=0&limit=1`
- `FavoritesController.AddByBangumi` 拉取并写入 `Anime.TotalEpisodes`（失败不阻断）
- `FavoritesController.Update` 在更新 Progress 时校验 `Progress <= (TotalEpisodes ?? 500)`
- 前端 `AnimeDetail` 详情页 + `ProgressModal` 进度控件 max = `totalEpisodes ?? 500`

**Tech Stack:** ASP.NET Core 8 (C#), EF Core 8, React 18 + TypeScript + Ant Design, Bangumi API v0

**Spec:** [2026-06-11-anime-total-episodes-design.md](docs/superpowers/specs/2026-06-11-anime-total-episodes-design.md)

---

## File Structure

**Backend (created):**
- `backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs` — 拉取 episodes 的响应 DTO
- `backend/ManWei.Api/Migrations/20260611000000_AddAnimeTotalEpisodes.cs` — EF 迁移

**Backend (modified):**
- `backend/ManWei.Api/Models/Anime.cs` — 新增 `TotalEpisodes` 字段
- `backend/ManWei.Api/Services/BangumiService.cs` — 新增 `GetEpisodesTotalAsync`
- `backend/ManWei.Api/DTOs/AnimeDto.cs` — 新增 `TotalEpisodes`
- `backend/ManWei.Api/DTOs/FavoriteDto.cs` — 新增 `AnimeTotalEpisodes`
- `backend/ManWei.Api/Controllers/FavoriteController.cs` — `AddByBangumi` 拉取 + `Update` 校验 + DTO 映射
- `backend/ManWei.Api/Controllers/AnimeController.cs` — `Sync` 拉取 + DTO 映射

**Frontend (modified):**
- `frontend/pc-client/src/types/api.ts` — 类型扩展
- `frontend/pc-client/src/pages/AnimeDetail/index.tsx` — 进度输入 max
- `frontend/pc-client/src/pages/Favorites/components/ProgressModal.tsx` — max 参数化
- `frontend/pc-client/src/pages/Favorites/index.tsx` — 传入 maxProgress
- `frontend/pc-client/src/pages/Favorites/components/FavoriteCard.tsx` — 显示总集数

---

## Task 1: Anime 实体新增 TotalEpisodes 字段

**Files:**
- Modify: `backend/ManWei.Api/Models/Anime.cs`

- [ ] **Step 1: 修改 Anime.cs 加字段**

打开 `backend/ManWei.Api/Models/Anime.cs`，在 `AnimeType` 字段后、`CreateTime` 前新增：

```csharp
    public string AnimeType { get; set; } = "TV";
    /// <summary>
    /// 总集数（从 Bangumi 拉取，null=未拉取到/老数据/拉取失败）
    /// </summary>
    public int? TotalEpisodes { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
```

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
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功（无新增错误）

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Models/Anime.cs
git commit -m "feat(model): add Anime.TotalEpisodes nullable int"
```

---

## Task 2: 创建 EF Core 迁移

**Files:**
- Create: `backend/ManWei.Api/Migrations/20260611000000_AddAnimeTotalEpisodes.cs`
- Create: `backend/ManWei.Api/Migrations/20260611000000_AddAnimeTotalEpisodes.Designer.cs`

- [ ] **Step 1: 用 dotnet ef 工具生成迁移**

Run:
```bash
cd d:/AnimeEmotion/backend/ManWei.Api
dotnet ef migrations add AddAnimeTotalEpisodes --output-dir Migrations
```

Expected: 创建两个文件 `20260611000000_AddAnimeTotalEpisodes.cs` 和 `.Designer.cs`

- [ ] **Step 2: 检查迁移内容**

打开 `Migrations/20260611000000_AddAnimeTotalEpisodes.cs`，确认 `Up` 方法包含：

```csharp
migrationBuilder.AddColumn<int>(
    name: "TotalEpisodes",
    table: "Anime",
    type: "int",
    nullable: true);
```

`Down` 方法包含对应的 `DropColumn`。

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

```bash
git add backend/ManWei.Api/Migrations/
git commit -m "feat(migration): add Anime.TotalEpisodes column"
```

---

## Task 3: 新增 Bangumi episodes 响应 DTO

**Files:**
- Create: `backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs`

- [ ] **Step 1: 创建 DTO 文件**

创建 `backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs`：

```csharp
using System.Text.Json.Serialization;

namespace ManWei.Api.DTOs;

/// <summary>
/// Bangumi episodes 列表响应（仅用于获取 total 计数）
/// 完整字段见 https://bangumi.github.io/api/#/Episode
/// </summary>
public class BangumiEpisodeListDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("data")]
    public List<BangumiEpisodeDto> Data { get; set; } = new();
}

/// <summary>
/// Bangumi 单条 episode 数据
/// </summary>
public class BangumiEpisodeDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("ep")]
    public int? Ep { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs
git commit -m "feat(dto): add BangumiEpisodeListDto for episodes total"
```

---

## Task 4: BangumiService 新增 GetEpisodesTotalAsync 方法

**Files:**
- Modify: `backend/ManWei.Api/Services/BangumiService.cs`

- [ ] **Step 1: 在 IBangumiService 接口新增方法**

打开 `backend/ManWei.Api/Services/BangumiService.cs`，在 `IBangumiService` 接口的 `SearchAsync` 后新增：

```csharp
    /// <summary>
    /// 获取本篇总集数（type=0）
    /// </summary>
    /// <param name="bangumiId">Bangumi 条目 ID</param>
    /// <returns>本篇集数；失败/未发布返回 null</returns>
    Task<int?> GetEpisodesTotalAsync(int bangumiId);
```

- [ ] **Step 2: 在 BangumiService 类实现方法**

在 `BangumiService` 类的 `SearchAsync` 方法后、`MapPlatform` 私有方法前，新增实现：

```csharp
    /// <summary>
    /// 获取本篇总集数
    /// </summary>
    public async Task<int?> GetEpisodesTotalAsync(int bangumiId)
    {
        if (!await _rateLimiter.WaitForTokenAsync())
        {
            _logger.LogWarning("Bangumi 限流，episodes 拉取被拒 ID: {BangumiId}", bangumiId);
            return null;
        }

        try
        {
            // limit=1 仅用于节省带宽（不需要返回 data 列表，只取 total 字段）。
            // ⚠️ Bangumi 的 total 字段是全量计数（与 limit 无关），
            // 不是当前页的条目数，所以 limit=1 时 total 仍是全量本篇集数。
            // 未来若有人误以为 total 跟随 limit 变化而改大 limit，请注意这一点。
            var url = $"/v0/episodes?subject_id={bangumiId}&type=0&limit=1";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bangumi episodes 拉取失败: {StatusCode}, ID: {BangumiId}",
                    response.StatusCode, bangumiId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var listResponse = JsonSerializer.Deserialize<BangumiEpisodeListDto>(content, _jsonOptions);
            return listResponse?.Total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bangumi episodes 拉取异常 ID: {BangumiId}", bangumiId);
            return null;
        }
    }
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

```bash
git add backend/ManWei.Api/Services/BangumiService.cs
git commit -m "feat(bangumi): add GetEpisodesTotalAsync for total episodes"
```

---

## Task 5: AnimeDto 加 TotalEpisodes 字段

**Files:**
- Modify: `backend/ManWei.Api/DTOs/AnimeDto.cs`

- [ ] **Step 1: 添加字段**

打开 `backend/ManWei.Api/DTOs/AnimeDto.cs`，在 `AnimeType` 字段后新增：

```csharp
    public string AnimeType { get; set; } = "TV";
    public int? TotalEpisodes { get; set; }
```

完整 AnimeDto 应该是（参考现有结构）：
```csharp
namespace ManWei.Api.DTOs;

public class AnimeDto
{
    public int Id { get; set; }
    public int? BangumiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? Summary { get; set; }
    public string AnimeType { get; set; } = "TV";
    public int? TotalEpisodes { get; set; }
    public DateTime CreateTime { get; set; }
    public int FavoriteCount { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/DTOs/AnimeDto.cs
git commit -m "feat(dto): add TotalEpisodes to AnimeDto"
```

---

## Task 6: FavoriteDto 加 AnimeTotalEpisodes 字段

**Files:**
- Modify: `backend/ManWei.Api/DTOs/FavoriteDto.cs`

- [ ] **Step 1: 添加字段**

打开 `backend/ManWei.Api/DTOs/FavoriteDto.cs`，在 `AnimeType` 字段后新增：

```csharp
    public string AnimeType { get; set; } = string.Empty;
    /// <summary>
    /// 动漫总集数（用于限制 Progress 上限；null=未拉取到）
    /// </summary>
    public int? AnimeTotalEpisodes { get; set; }
```

完整 FavoriteDto 应该是：
```csharp
namespace ManWei.Api.DTOs;

public class FavoriteDto
{
    public int Id { get; set; }
    public int AnimeId { get; set; }
    public string AnimeName { get; set; } = string.Empty;
    public string? AnimeCover { get; set; }
    public string AnimeType { get; set; } = string.Empty;
    /// <summary>
    /// 动漫总集数（用于限制 Progress 上限；null=未拉取到）
    /// </summary>
    public int? AnimeTotalEpisodes { get; set; }
    public int Status { get; set; }
    public int Progress { get; set; }
    public int? Rating { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/DTOs/FavoriteDto.cs
git commit -m "feat(dto): add AnimeTotalEpisodes to FavoriteDto"
```

---

## Task 7: FavoritesController 注入 ILogger 备用

**Files:**
- Modify: `backend/ManWei.Api/Controllers/FavoriteController.cs`

- [ ] **Step 1: 添加 logger 字段与构造器参数**

打开 `backend/ManWei.Api/Controllers/FavoriteController.cs`，修改类字段和构造函数：

在类顶部字段区添加（紧挨 `private readonly IBangumiService _bangumiService;`）：

```csharp
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly ILogger<FavoritesController> _logger;
```

修改构造函数（替换原构造函数）：

```csharp
    public FavoritesController(AppDbContext context, IBangumiService bangumiService,
        ILogger<FavoritesController> logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _logger = logger;
    }
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/FavoriteController.cs
git commit -m "refactor(favorites): inject ILogger for episodes logging"
```

---

## Task 8: FavoritesController.AddByBangumi 拉取 TotalEpisodes

**Files:**
- Modify: `backend/ManWei.Api/Controllers/FavoriteController.cs`

- [ ] **Step 1: 在 AddByBangumi 写入 newAnime 后追加拉取**

打开 `FavoritesController.AddByBangumi`（第 419 行附近的 `_context.Anime.Add(newAnime); ...` 块后），找到 `_context.SaveChangesAsync()` 调用。

**当前代码**（参考结构）：
```csharp
                _context.Anime.Add(newAnime);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    existingAnime = await _context.Anime
                        .FirstOrDefaultAsync(a => a.BangumiId == dto.BangumiId);
                    if (existingAnime == null)
                        return StatusCode(503, Result<FavoriteDto>.Fail(503, "同步失败"));
                    targetAnimeId = existingAnime.Id;
                }
                targetAnimeId = newAnime.Id;
```

**修改为**：
```csharp
                _context.Anime.Add(newAnime);
                try
                {
                    await _context.SaveChangesAsync();

                    // 拉取本篇总集数（拉取失败不影响添加收藏成功）
                    // GetEpisodesTotalAsync 内部已 try/catch 返回 null；
                    // 但这里再加一层 try/catch 是为了防御性：万一 Service 未来
                    // 改动漏了 catch，不会让异常冒泡到外层影响"添加收藏"成功。
                    try
                    {
                        var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(dto.BangumiId!.Value);
                        if (totalEpisodes.HasValue)
                        {
                            newAnime.TotalEpisodes = totalEpisodes.Value;
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "拉取总集数异常，忽略 BangumiId={BangumiId}", dto.BangumiId);
                    }
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    existingAnime = await _context.Anime
                        .FirstOrDefaultAsync(a => a.BangumiId == dto.BangumiId);
                    if (existingAnime == null)
                        return StatusCode(503, Result<FavoriteDto>.Fail(503, "同步失败"));
                    targetAnimeId = existingAnime.Id;
                }
                targetAnimeId = newAnime.Id;
```

- [ ] **Step 2: 在 AddByBangumi 的 MapToDto 填充 AnimeTotalEpisodes**

找到 AddByBangumi 方法末尾的 `var resultDto = MapToDto(favorite);` 之前，修改为：

```csharp
        // 重新加载关联的 Anime 以获取 TotalEpisodes
        await _context.Entry(favorite).Reference(f => f.Anime).LoadAsync();
        var resultDto = MapToDto(favorite);
```

`MapToDto` 私有方法会读取 `favorite.Anime.TotalEpisodes`（在 Task 9 改）。

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

```bash
git add backend/ManWei.Api/Controllers/FavoriteController.cs
git commit -m "feat(favorites): fetch TotalEpisodes on add by bangumi"
```

---

## Task 9: FavoritesController.MapToDto 填 AnimeTotalEpisodes

**Files:**
- Modify: `backend/ManWei.Api/Controllers/FavoriteController.cs`

- [ ] **Step 1: 修改 MapToDto**

打开 `FavoritesController` 文件底部 `private static FavoriteDto MapToDto(Favorite favorite)` 方法（带 `static` 关键字），在 `AnimeType` 赋值后新增：

```csharp
    private static FavoriteDto MapToDto(Favorite favorite)
    {
        return new FavoriteDto
        {
            Id = favorite.Id,
            AnimeId = favorite.AnimeId,
            AnimeName = favorite.Anime?.Name ?? string.Empty,
            AnimeCover = favorite.Anime?.Cover,
            AnimeType = favorite.Anime?.AnimeType ?? string.Empty,
            AnimeTotalEpisodes = favorite.Anime?.TotalEpisodes,
            Status = favorite.Status,
            Progress = favorite.Progress,
            Rating = favorite.Rating,
            CreateTime = favorite.CreateTime,
            UpdateTime = favorite.UpdateTime
        };
    }
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/FavoriteController.cs
git commit -m "feat(favorites): map AnimeTotalEpisodes in MapToDto"
```

---

## Task 10: FavoritesController.Update 添加 Progress 上限校验

**Files:**
- Modify: `backend/ManWei.Api/Controllers/FavoriteController.cs`

- [ ] **Step 1: 修改 Update 方法的进度校验**

打开 `FavoritesController.Update`（第 270-276 行附近），找到进度校验代码块：

**当前代码**：
```csharp
        // 更新进度
        if (dto.Progress.HasValue)
        {
            if (dto.Progress.Value < 0)
                return BadRequest(Result<FavoriteDto>.Fail(400, "进度不能为负数"));

            favorite.Progress = dto.Progress.Value;
        }
```

**修改为**：
```csharp
        // 更新进度
        if (dto.Progress.HasValue)
        {
            if (dto.Progress.Value < 0)
                return BadRequest(Result<FavoriteDto>.Fail(400, "进度不能为负数"));

            // 进度上限 = 动漫总集数（null/0 视为未知，按 500 兜底）
            var maxProgress = favorite.Anime?.TotalEpisodes is > 0
                ? favorite.Anime.TotalEpisodes.Value
                : 500;
            if (dto.Progress.Value > maxProgress)
                return BadRequest(Result<FavoriteDto>.Fail(400,
                    $"进度不能超过总集数 {maxProgress} 集"));

            favorite.Progress = dto.Progress.Value;
        }
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/FavoriteController.cs
git commit -m "feat(favorites): validate Progress upper bound by TotalEpisodes"
```

---

## Task 11: FavoritesController.GetList/GetById DTO 映射补充

**Files:**
- Modify: `backend/ManWei.Api/Controllers/FavoriteController.cs`

- [ ] **Step 1: GetList Select 子句补充**

打开 `GetList` 方法（38-112 行），找到第 88-100 行的 Select 子句，在 `AnimeType = f.Anime.AnimeType,` 后新增：

```csharp
            .Select(f => new FavoriteDto
            {
                Id = f.Id,
                AnimeId = f.AnimeId,
                AnimeName = f.Anime.Name,
                AnimeCover = f.Anime.Cover,
                AnimeType = f.Anime.AnimeType,
                AnimeTotalEpisodes = f.Anime.TotalEpisodes,
                Status = f.Status,
                Progress = f.Progress,
                Rating = f.Rating,
                CreateTime = f.CreateTime,
                UpdateTime = f.UpdateTime
            })
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/FavoriteController.cs
git commit -m "feat(favorites): include AnimeTotalEpisodes in GetList"
```

---

## Task 12: AnimeController 注入 ILogger

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: 添加 logger 字段与构造器参数**

打开 `backend/ManWei.Api/Controllers/AnimeController.cs`，在类字段区添加：

```csharp
    private readonly AppDbContext _context;
    private readonly IBangumiService _bangumiService;
    private readonly ILogger<AnimeController> _logger;
```

修改构造函数：

```csharp
    public AnimeController(AppDbContext context, IBangumiService bangumiService,
        ILogger<AnimeController> logger)
    {
        _context = context;
        _bangumiService = bangumiService;
        _logger = logger;
    }
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/AnimeController.cs
git commit -m "refactor(anime): inject ILogger for sync episodes logging"
```

---

## Task 13: AnimeController.Sync 拉取 TotalEpisodes

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: 修改 Sync 方法**

打开 `AnimeController.Sync`（第 145-206 行）。**当前代码**：
```csharp
            existing.Name = anime.Name;
            existing.Summary = anime.Summary;
            existing.Cover = anime.Cover;
            existing.AnimeType = anime.AnimeType;

            await _context.SaveChangesAsync();

            var dto = new AnimeDto
            {
                Id = existing.Id,
                BangumiId = existing.BangumiId,
                Name = existing.Name,
                Cover = existing.Cover,
                Summary = existing.Summary,
                AnimeType = existing.AnimeType,
                CreateTime = existing.CreateTime
            };
```

**修改为**（在 SaveChangesAsync 之前加拉取逻辑，并在 DTO 里加字段）：
```csharp
            existing.Name = anime.Name;
            existing.Summary = anime.Summary;
            existing.Cover = anime.Cover;
            existing.AnimeType = anime.AnimeType;

            // 拉取总集数（覆盖策略：仅在 null/0 时覆盖）
            try
            {
                var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(bangumiId);
                if (totalEpisodes.HasValue)
                {
                    if (existing.TotalEpisodes is null or 0)
                    {
                        existing.TotalEpisodes = totalEpisodes.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync 拉取总集数异常 BangumiId={BangumiId}", bangumiId);
            }

            await _context.SaveChangesAsync();

            var dto = new AnimeDto
            {
                Id = existing.Id,
                BangumiId = existing.BangumiId,
                Name = existing.Name,
                Cover = existing.Cover,
                Summary = existing.Summary,
                AnimeType = existing.AnimeType,
                TotalEpisodes = existing.TotalEpisodes,
                CreateTime = existing.CreateTime
            };
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 3: 提交**

```bash
git add backend/ManWei.Api/Controllers/AnimeController.cs
git commit -m "feat(anime): fetch TotalEpisodes in Sync (only if null/0)"
```

---

## Task 14: AnimeController.GetList/GetById DTO 映射补充

**Files:**
- Modify: `backend/ManWei.Api/Controllers/AnimeController.cs`

- [ ] **Step 1: GetList Select 子句补充**

打开 `GetList`（37-97 行），第 70-86 行的 Select 子句，添加 `TotalEpisodes`：

```csharp
            .Select(a => new AnimeDto
            {
                Id = a.Id,
                BangumiId = a.BangumiId,
                Name = a.Name,
                Cover = a.Cover,
                Summary = a.Summary,
                AnimeType = a.AnimeType,
                TotalEpisodes = a.TotalEpisodes,
                CreateTime = a.CreateTime,
                FavoriteCount = _context.Favorites.Count(f => f.AnimeId == a.Id),
                AvgRating = _context.Favorites
                    .Where(f => f.AnimeId == a.Id && f.Rating != null)
                    .Average(f => (double?)f.Rating),
                ReviewCount = _context.Reviews.Count(r => r.Favorite.AnimeId == a.Id)
            })
```

- [ ] **Step 2: GetById 方法补充**

打开 `GetById`（107-134 行），第 117-131 行的 DTO 赋值，添加 `TotalEpisodes`：

```csharp
        var dto = new AnimeDto
        {
            Id = anime.Id,
            BangumiId = anime.BangumiId,
            Name = anime.Name,
            Cover = anime.Cover,
            Summary = anime.Summary,
            AnimeType = anime.AnimeType,
            TotalEpisodes = anime.TotalEpisodes,
            CreateTime = anime.CreateTime,
            FavoriteCount = _context.Favorites.Count(f => f.AnimeId == anime.Id),
            AvgRating = _context.Favorites
                .Where(f => f.AnimeId == anime.Id && f.Rating != null)
                .Average(f => (double?)f.Rating),
            ReviewCount = _context.Reviews.Count(r => r.Favorite.AnimeId == anime.Id)
        };
```

- [ ] **Step 3: 验证编译**

Run: `cd d:/AnimeEmotion/backend/ManWei.Api && dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

```bash
git add backend/ManWei.Api/Controllers/AnimeController.cs
git commit -m "feat(anime): include TotalEpisodes in GetList/GetById"
```

---

## Task 15: 端到端后端测试

**Files:**
- 测试：手动

- [ ] **Step 1: 启动后端**

Run:
```bash
cd d:/AnimeEmotion/backend/ManWei.Api
dotnet run
```

后端启动后，确认 migration 自动应用（如未应用，手动 `dotnet ef database update`）。

- [ ] **Step 2: 验证 Bangumi 拉取**

通过测试客户端（curl / Postman / 浏览器）调一个添加收藏的接口，确认：
- `Anime.TotalEpisodes` 字段被写入
- 失败时（如 Bangumi 限流）`TotalEpisodes=null`，但添加收藏成功

- [ ] **Step 3: 验证 Progress 校验**

调 `PUT /favorites/{id}` 接口：
- `progress <= total` → 200
- `progress > total` → 400 with message "进度不能超过总集数 X 集"
- `TotalEpisodes=null` → max=500，500 成功，501 失败

- [ ] **Step 4: 提交（如有修复）**

```bash
git add -A
git commit -m "test: verify backend episodes integration"
```

---

## Task 16: 前端类型扩展

**Files:**
- Modify: `frontend/pc-client/src/types/api.ts`

- [ ] **Step 1: 添加 totalEpisodes 字段**

打开 `frontend/pc-client/src/types/api.ts`，在 `Anime` 接口添加：

```typescript
export interface Anime {
  // ... 现有字段
  totalEpisodes?: number | null;
}
```

在 `FavoriteDto` 接口添加：

```typescript
export interface FavoriteDto {
  // ... 现有字段
  animeTotalEpisodes?: number | null;
}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build`
Expected: TypeScript 编译通过

- [ ] **Step 3: 提交**

```bash
git add frontend/pc-client/src/types/api.ts
git commit -m "feat(types): add totalEpisodes and animeTotalEpisodes"
```

---

## Task 17: AnimeDetail 详情页进度输入 max 限制

**Files:**
- Modify: `frontend/pc-client/src/pages/AnimeDetail/index.tsx`

- [ ] **Step 1: 修改 InputNumber**

打开 `frontend/pc-client/src/pages/AnimeDetail/index.tsx` 第 415-427 行附近：

**当前代码**：
```tsx
                  <div className={styles.favoriteRow}>
                    <span className={styles.favoriteLabel}>进度</span>
                    <Space>
                      <InputNumber
                        className={styles.episodeInput}
                        min={0}
                        value={favoriteCheck.progress || 0}
                        onChange={(value) => handleUpdateFavorite({ progress: value || 0 })}
                      />
                      <span className={styles.episodeSuffix}>集</span>
                    </Space>
                  </div>
```

**修改为**：
```tsx
                  <div className={styles.favoriteRow}>
                    <span className={styles.favoriteLabel}>进度</span>
                    <Space>
                      <InputNumber
                        className={styles.episodeInput}
                        min={0}
                        max={anime.totalEpisodes && anime.totalEpisodes > 0 ? anime.totalEpisodes : 500}
                        value={favoriteCheck.progress || 0}
                        onChange={(value) => handleUpdateFavorite({ progress: value || 0 })}
                      />
                      <span className={styles.episodeSuffix}>
                        / {anime.totalEpisodes && anime.totalEpisodes > 0 ? anime.totalEpisodes : '?'} 集
                      </span>
                    </Space>
                  </div>
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build`
Expected: 编译通过

- [ ] **Step 3: 提交**

```bash
git add frontend/pc-client/src/pages/AnimeDetail/index.tsx
git commit -m "feat(anime-detail): cap progress input by totalEpisodes"
```

---

## Task 18: ProgressModal max 参数化

**Files:**
- Modify: `frontend/pc-client/src/pages/Favorites/components/ProgressModal.tsx`

- [ ] **Step 1: 修改 ProgressModal**

打开 `frontend/pc-client/src/pages/Favorites/components/ProgressModal.tsx`，修改组件接口和实现：

**当前代码**：
```tsx
interface ProgressModalProps {
  visible: boolean;
  favoriteId: number;
  currentProgress: number;
  onClose: () => void;
}

export function ProgressModal({ visible, favoriteId, currentProgress, onClose }: ProgressModalProps) {
  const [progress, setProgress] = useState(currentProgress);
  // ...
        <InputNumber
            min={0}
            value={progress}
            onChange={(value) => setProgress(value || 0)}
            style={{ width: '100%' }}
          />
        // ...
        <Slider
            min={0}
            max={500}
            value={progress}
            onChange={setProgress}
          />
```

**修改为**：
```tsx
interface ProgressModalProps {
  visible: boolean;
  favoriteId: number;
  currentProgress: number;
  maxProgress?: number; // 新增：动漫总集数（null/0 时 fallback 500）
  onClose: () => void;
}

export function ProgressModal({ visible, favoriteId, currentProgress, maxProgress = 500, onClose }: ProgressModalProps) {
  const [progress, setProgress] = useState(currentProgress);
  // ...
        <InputNumber
            min={0}
            max={maxProgress}
            value={progress}
            onChange={(value) => setProgress(value || 0)}
            style={{ width: '100%' }}
          />
        // ...
        <Slider
            min={0}
            max={maxProgress}
            value={progress}
            onChange={setProgress}
          />
        <div style={{ marginTop: 8, fontSize: 12, color: '#6B6B6B' }}>
          最多 {maxProgress} 集
        </div>
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build`
Expected: 编译通过

- [ ] **Step 3: 提交**

```bash
git add frontend/pc-client/src/pages/Favorites/components/ProgressModal.tsx
git commit -m "feat(progress-modal): parameterize maxProgress prop"
```

---

## Task 19: Favorites 收藏页传入 maxProgress

**Files:**
- Modify: `frontend/pc-client/src/pages/Favorites/index.tsx`

- [ ] **Step 1: 修改 ProgressModal 调用处**

打开 `frontend/pc-client/src/pages/Favorites/index.tsx`，找到 ProgressModal 调用的地方（应该类似）：

**当前代码**（参考结构）：
```tsx
<ProgressModal
  visible={progressModal.visible}
  favoriteId={progressModal.favoriteId}
  currentProgress={progressModal.currentProgress}
  onClose={() => setProgressModal({ ... })}
/>
```

**修改为**（传入 animeTotalEpisodes）：
```tsx
<ProgressModal
  visible={progressModal.visible}
  favoriteId={progressModal.favoriteId}
  currentProgress={progressModal.currentProgress}
  maxProgress={
    progressModal.favoriteId
      ? (list.find(f => f.id === progressModal.favoriteId)?.animeTotalEpisodes ?? 500) > 0
        ? list.find(f => f.id === progressModal.favoriteId)?.animeTotalEpisodes ?? 500
        : 500
      : 500
  }
  onClose={() => setProgressModal({ ... })}
/>
```

或更简洁的 helper：
```tsx
const targetFavorite = list.find(f => f.id === progressModal.favoriteId);
const maxEpisodes = targetFavorite?.animeTotalEpisodes && targetFavorite.animeTotalEpisodes > 0
  ? targetFavorite.animeTotalEpisodes
  : 500;
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build`
Expected: 编译通过

- [ ] **Step 3: 提交**

```bash
git add frontend/pc-client/src/pages/Favorites/index.tsx
git commit -m "feat(favorites): pass animeTotalEpisodes to ProgressModal"
```

---

## Task 20: FavoriteCard 显示总集数

**Files:**
- Modify: `frontend/pc-client/src/pages/Favorites/components/FavoriteCard.tsx`

- [ ] **Step 1: 修改进度显示**

打开 `frontend/pc-client/src/pages/Favorites/components/FavoriteCard.tsx`，第 91-94 行：

**当前代码**：
```tsx
        {favorite.status === 1 && (
          <div className={styles.progress}><span>进度: {favorite.progress} 集</span></div>
        )}
```

**修改为**：
```tsx
        {favorite.status === 1 && (
          <div className={styles.progress}>
            <span>
              进度: {favorite.progress} / {favorite.animeTotalEpisodes && favorite.animeTotalEpisodes > 0 ? favorite.animeTotalEpisodes : '?'} 集
            </span>
          </div>
        )}
```

- [ ] **Step 2: 验证编译**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run build`
Expected: 编译通过

- [ ] **Step 3: 提交**

```bash
git add frontend/pc-client/src/pages/Favorites/components/FavoriteCard.tsx
git commit -m "feat(favorite-card): display total episodes in progress"
```

---

## Task 21: 端到端前端测试

**Files:**
- 测试：手动

- [ ] **Step 1: 启动前端**

Run: `cd d:/AnimeEmotion/frontend/pc-client && npm run dev`

- [ ] **Step 2: 验证详情页**

浏览器打开 `/anime/{id}`：
- 添加一个 BangumiId 收藏，触发 TotalEpisodes 拉取
- 进度输入框 max 与显示 "X / N 集" 正确
- 输入超过 N 的值被截断

- [ ] **Step 3: 验证收藏页**

打开 `/favorites`：
- 卡片显示 "进度: X / N 集"
- 点击更新进度弹窗，Slider/InputNumber max = N
- 强制改请求 body 越界，前端允许输入但后端拒绝（弹错）

- [ ] **Step 4: 提交（如有修复）**

```bash
git add -A
git commit -m "test: verify frontend episodes UI"
```

---

## Self-Review

### Spec coverage check

| Spec 需求 | 任务 |
|---|---|
| Anime 实体新增字段 | Task 1 |
| EF Core 迁移 | Task 2 |
| BangumiEpisodeListDto | Task 3 |
| BangumiService.GetEpisodesTotalAsync | Task 4 |
| AnimeDto.TotalEpisodes | Task 5 |
| FavoriteDto.AnimeTotalEpisodes | Task 6 |
| FavoritesController.AddByBangumi 拉取 | Task 7, 8 |
| FavoritesController.MapToDto | Task 9 |
| FavoritesController.Update 校验 | Task 10 |
| FavoritesController.GetList DTO | Task 11 |
| AnimeController 注入 ILogger | Task 12 |
| AnimeController.Sync 拉取 | Task 13 |
| AnimeController.GetList/GetById DTO | Task 14 |
| 后端测试 | Task 15 |
| 前端类型扩展 | Task 16 |
| 详情页 max | Task 17 |
| ProgressModal 参数化 | Task 18 |
| 收藏页传入 max | Task 19 |
| 收藏卡显示总集数 | Task 20 |
| 前端 E2E 测试 | Task 21 |

✅ **所有 spec 需求都有对应任务**

### Placeholder scan
- ✅ 无 "TBD" / "TODO"
- ✅ 所有代码步骤有完整代码
- ✅ 所有测试有具体命令和预期输出
- ✅ 无 "similar to" 偷懒

### Type consistency check
- `Anime.TotalEpisodes`: Task 1 (int?) → Task 2 (EF migration) → Task 5 (AnimeDto) ✅
- `FavoriteDto.AnimeTotalEpisodes`: Task 6 → Task 9 (MapToDto) → Task 11 (GetList Select) ✅
- `BangumiEpisodeListDto.Total`: Task 3 → Task 4 (Service) ✅
- `GetEpisodesTotalAsync(bangumiId)`: Task 4 → Task 8 (AddByBangumi) → Task 13 (Sync) ✅
- `maxProgress`: Task 10 (后端) → Task 18 (ProgressModal) → Task 19 (传入值) ✅
- `totalEpisodes` / `animeTotalEpisodes`: Task 16 (类型) → Task 17, 19, 20 (使用) ✅

✅ **类型一致**

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-11-anime-total-episodes.md`. **Two execution options:**

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
