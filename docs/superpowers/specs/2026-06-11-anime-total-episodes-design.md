# 动漫总集数获取与进度上限限制 — 设计文档

**Date:** 2026-06-11
**Status:** Approved (待用户复核)
**Author:** brainstorming session

## Context

用户反馈 PC 客户端动漫详情页与收藏页的"进度"输入控件没有上限限制，可以输入任意大的数字（如 999）。从产品角度，Progress（已看集数）应该受限于动漫的总集数。

当前代码现状：
- `Anime` 实体（`backend/ManWei.Api/Models/Anime.cs`）没有 `TotalEpisodes` 字段
- `Favorite.Progress`（`backend/ManWei.Api/Models/Favorite.cs`）是 `int`，没有上限校验
- 详情页 `AnimeDetail/index.tsx` 的 `InputNumber` min=0，无 max
- 收藏页 `ProgressModal.tsx` 的 `InputNumber` 无 max，`Slider` max=500
- `BangumiService` 已有 `SearchAsync` / `GetAndMapAnimeAsync` / `GetAnimeBatchAsync`，但没有 episodes 拉取

Bangumi API 提供了 `GET /v0/episodes?subject_id={id}&type=0` 用于获取本篇集数，响应格式：
```json
{ "total": 24, "limit": 100, "offset": 0, "data": [...] }
```

## Goals

1. **数据来源自动**：用户通过 BangumiId 添加收藏时，后端自动从 Bangumi 拉取本篇总集数
2. **缓存到本地**：写入 `Anime.TotalEpisodes`，避免每次重复请求
3. **进度受限**：前端输入框 + Slider 上限 = `TotalEpisodes`，后端校验
4. **优雅降级**：拉取失败/老数据无 `TotalEpisodes` 时，默认上限 500（与当前 ProgressModal 一致）
5. **失败不阻断**：集数拉取失败不影响"添加收藏"成功

## Non-Goals

- 不展示每集详情列表（用户选择"仅展示总集数"）
- 不做"每集勾选看过"功能
- 不支持用户手动修正总集数（首版仅 Bangumi 自动拉取）
- 不为老数据批量回填（依赖未来新添加的收藏逐步覆盖）

## 方案选择

**采用方案 A**：从 Bangumi `GET /v0/episodes?subject_id={id}&type=0` 响应取 `total` 字段，作为总集数。

**理由**：
- 用户明确选择"自动从 Bangumi 拉取"+"只取总集数"+"添加收藏时同步拉取"
- `total` 字段是 Bangumi 官方为 type=0 提供的计数，一次请求即可获得
- 比解析 `data` 数组再 `max(ep)` 更简单、可靠
- 后续如需"逐集勾选"，可扩展为拉 `data` 全量

## Architecture

### 数据层

**`Anime` 实体新增字段**（`backend/ManWei.Api/Models/Anime.cs`）：
```csharp
/// <summary>
/// 总集数（从 Bangumi 拉取，null=未拉取到/老数据）
/// </summary>
public int? TotalEpisodes { get; set; }
```

**EF Core 迁移**：`AddAnimeTotalEpisodes`
- 列类型：`int NULL`
- 默认值：NULL
- 不设置唯一索引（可空字段不参与唯一索引，按项目 CLAUDE.md 规范）

### Service 层

**`IBangumiService` 新增方法**（`backend/ManWei.Api/Services/BangumiService.cs`）：

```csharp
/// <summary>
/// 获取本篇总集数
/// </summary>
/// <param name="bangumiId">Bangumi 条目 ID</param>
/// <returns>本篇集数；失败/未发布返回 null</returns>
Task<int?> GetEpisodesTotalAsync(int bangumiId);
```

**实现**：
```csharp
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
        // BangumiSubjectListDto 可复用，total 字段已在 schema 内
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

**新增 DTO**（`backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs`）：
```csharp
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

### Controller 层

**`FavoritesController.AddByBangumi` 修改**（`backend/ManWei.Api/Controllers/FavoriteController.cs`）：

在「分支B：传了 BangumiId」的 newAnime 保存到数据库后，新增：
```csharp
// 拉取总集数（拉取失败不影响添加收藏成功）
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
```

`AnimeController.Sync` 同步路径也加同样的拉取：

```csharp
// 已存在则更新（包括 TotalEpisodes）
existing.Name = anime.Name;
existing.Summary = anime.Summary;
existing.Cover = anime.Cover;
existing.AnimeType = anime.AnimeType;

try
{
    var totalEpisodes = await _bangumiService.GetEpisodesTotalAsync(bangumiId);
    if (totalEpisodes.HasValue)
    {
        // 策略：仅在 TotalEpisodes 为 null 或 0 时才覆盖
        // 原因：管理员手动设置过总集数、或 Bangumi 对老条目返回 0 时，
        // 避免被新拉到的"未知"数据覆盖。
        // 连载动漫：管理员可手动调 Sync 后端接口刷新，或者后续加"刷新总集数"按钮。
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
```

**Sync 覆盖策略说明**：
- **不无条件覆盖**：因为 Sync 会被用于"刷新动漫信息"（连载动漫每周集数可能变），但**目前不实现自动周期刷新**
- **仅在 null/0 时覆盖**：首次同步或老数据兜底场景
- 后续如需"连载刷新总集数"，建议另开 `/anime/{id}/refresh-episodes` 端点 + 按钮，避免无脑覆盖

`AnimeController.Sync` 还需要在 DTO 映射处补充 `TotalEpisodes = existing.TotalEpisodes` 字段（Sync 接口原本就不返回 DTO 详细字段，新增即可）。

**Sync 与 `AddByBangumi` 的差异**：
- `AddByBangumi`：首次添加，**无脑写入** TotalEpisodes（因为 newAnime 刚创建，必然是 null）
- `Sync`：可能是更新，**仅在 null/0 时写入**

**`FavoritesController.Update` 添加 Progress 校验**（`backend/ManWei.Api/Controllers/FavoriteController.cs`）：

```csharp
// 更新进度
if (dto.Progress.HasValue)
{
    if (dto.Progress.Value < 0)
        return BadRequest(Result<FavoriteDto>.Fail(400, "进度不能为负数"));

    // 进度上限 = 动漫总集数（null 时默认 500）
    var maxProgress = favorite.Anime?.TotalEpisodes ?? 500;
    if (dto.Progress.Value > maxProgress)
        return BadRequest(Result<FavoriteDto>.Fail(400,
            $"进度不能超过总集数 {maxProgress} 集"));

    favorite.Progress = dto.Progress.Value;
}
```

### DTO 层

**`AnimeDto` 新增字段**（`backend/ManWei.Api/DTOs/AnimeDto.cs`）：
```csharp
public int? TotalEpisodes { get; set; }
```
在所有 `AnimeDto` 实例化点补充赋值。

**`FavoriteDto` 新增字段**（`backend/ManWei.Api/DTOs/FavoriteDto.cs`）：
```csharp
public int? AnimeTotalEpisodes { get; set; }
```
便于前端无需再次请求动漫详情。

### 前端

**`types/api.ts` 类型扩展**：
- `Anime` 接口加 `totalEpisodes?: number | null`
- `FavoriteDto` 加 `animeTotalEpisodes?: number | null`

**`AnimeDetail/index.tsx` 详情页**：
- `InputNumber` 进度控件：max = `anime.totalEpisodes ?? 500`
- 显示"X / {total} 集"

**`Favorites/components/ProgressModal.tsx`**：
- 接受新 prop `maxProgress: number`（从父组件传入）
- `InputNumber` max = `maxProgress`
- `Slider` max = `maxProgress`
- `Favorites/index.tsx` 调用时传入 `favorite.animeTotalEpisodes ?? 500`

**`Favorites/components/FavoriteCard.tsx`**：
- 显示"进度: X / {animeTotalEpisodes ?? '?'} 集"（如果 status=1）

## Data Flow

### 添加收藏
```
用户填关键词 → POST /favorites/add { bangumiId }
    ↓
Controller: BangumiService.GetAndMapAnimeAsync(bangumiId)
    ↓
Controller: BangumiService.GetEpisodesTotalAsync(bangumiId)  ← 新增
    ↓
写入 Anime.TotalEpisodes
    ↓
返回 FavoriteDto (含 animeTotalEpisodes)
    ↓
前端展示进度上限
```

### 更新进度
```
用户拖动 Slider 或改 InputNumber
    ↓
PUT /favorites/{id} { progress: N }
    ↓
后端校验: 0 <= N <= (Anime.TotalEpisodes ?? 500)
    ↓
返回 200 或 400
```

## Error Handling

| 场景 | 处理 |
|---|---|
| Bangumi episodes 拉取被限流 | 返回 null，log warning，添加收藏仍成功 |
| Bangumi 返回 404 | 返回 null，添加收藏仍成功 |
| Bangumi 返回非 200 | 返回 null，log warning，添加收藏仍成功 |
| Bangumi `total` = 0（未发布） | 返回 0，前端 max=0，用户不能输入正数；或 fallback 到 500？ |
| Bangumi 返回 200 但 data=空、total=0（如只有 SP、无本篇） | TotalEpisodes 写入 0，前后端都按 500 兜底（按 `is > 0` 判断） |
| 老数据 TotalEpisodes=null | 前后端默认上限 500 |
| 用户输入 Progress > max | 前端 InputNumber 限制；后端二次校验返回 400 |

**关于 `total=0` 的决策**：
- 方案 X：`TotalEpisodes=0` 时也允许用户输入 0（不开放输入）
- 方案 Y：兜底 500
- **采用方案 Y**：`TotalEpisodes <= 0` 视为"未知"，按 500 兜底
- 原因 1：前端 max=0 时 InputNumber 体验差（用户无法输入任何值）
- 原因 2：data=空 + total=0 表示"条目存在但还没本篇集数"（如只有 SP），不应阻塞用户收藏

**`is > 0` 兜底表达式**（在 Controller 与前端通用）：
```csharp
var maxProgress = favorite.Anime?.TotalEpisodes is > 0 ? favorite.Anime.TotalEpisodes.Value : 500;
```
- `is > 0` 同时处理 null（false → 500）、0（false → 500）、负数（false → 500，正常情况不会发生）

## Testing

### 单元测试
- `GetEpisodesTotalAsync`：mock HttpClient，验证 total 字段解析
- 解析 0/null/非 200 响应

### 集成测试
- 添加收藏后 `Anime.TotalEpisodes` 写入正确
- 更新 Progress 超出 max 返回 400
- 更新 Progress 在合法范围返回 200

### E2E
- 浏览器添加收藏，进度输入框 max 实时更新
- 输入超过 max 的值，前端截断
- 强制改请求 body 越界，后端拒绝

## Files to Modify

**Backend：**
- `backend/ManWei.Api/Models/Anime.cs` — 加字段
- `backend/ManWei.Api/Data/AppDbContext.cs` — 无需改（EF 自动）
- `backend/ManWei.Api/Migrations/{timestamp}_AddAnimeTotalEpisodes.cs` — 新增
- `backend/ManWei.Api/Services/BangumiService.cs` — 新增 `GetEpisodesTotalAsync`
- `backend/ManWei.Api/DTOs/BangumiEpisodeListDto.cs` — 新增文件
- `backend/ManWei.Api/Controllers/FavoriteController.cs` — `AddByBangumi` / `Update` / DTO 映射
- `backend/ManWei.Api/Controllers/AnimeController.cs` — `Sync` 方法 + DTO 映射（Sync 需注入 `ILogger<AnimeController>`）
- `backend/ManWei.Api/DTOs/AnimeDto.cs` — 加字段
- `backend/ManWei.Api/DTOs/FavoriteDto.cs` — 加字段

**Frontend：**
- `frontend/pc-client/src/types/api.ts` — 类型扩展
- `frontend/pc-client/src/pages/AnimeDetail/index.tsx` — 进度输入 max
- `frontend/pc-client/src/pages/Favorites/components/ProgressModal.tsx` — max 参数化
- `frontend/pc-client/src/pages/Favorites/index.tsx` — 传入 maxProgress
- `frontend/pc-client/src/pages/Favorites/components/FavoriteCard.tsx` — 显示总集数

## Rollout

1. 部署后端 migration + 新字段
2. 用户**新添加**的收藏会立即有 TotalEpisodes
3. **老数据**的动漫 TotalEpisodes=null，使用兜底 500
4. 如有大量老动漫需要回填，可后续加"后台批量回填脚本"（不在本 spec 范围）

## Open Questions

无（用户已确认全部关键决策）
