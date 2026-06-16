# Tech Debt Backlog

> 集中记录在 code review 中发现但未在本轮修复的问题。便于后续按优先级清理。

## 评分规则

来源于 [`.claude/skills/code-review`](../.claude/skills/code-review/SKILL.md) 评分量表：
- 75+：已纳入正式 review（见对应 PR 评审记录）
- 40-74：记录在此 backlog，可追溯但不必立即修
- < 40：散落在代码注释里，不集中记录

---

## BackfillEpisodes 接口相关（[AnimeController.cs:230-300](../backend/ManWei.Api/Controllers/AnimeController.cs#L230-L300)）

本轮已修复（2026-06-12）：
- 评分 75：0 vs null 语义不一致（`Sync` 用 `is null or 0` 守卫；`Backfill` 只过滤 `null`，导致 `TotalEpisodes=0` 行永久卡住）
- 评分 50：UNIQUE 索引迁移 `20260608031125_AddUniqueBangumiIdIndex` 的 `Up()` / `Down()` 方法体为空（修复迁移 `20260612000000_FixAddUniqueBangumiIdIndex`）

### TD-1：Outer try/catch 是死代码（评分 50）

**位置**：[AnimeController.cs:260-286](../backend/ManWei.Api/Controllers/AnimeController.cs#L260-L286) + [BangumiService.cs:211-244](../backend/ManWei.Api/Services/BangumiService.cs#L211-L244)

**问题**：`GetEpisodesTotalAsync` 内部 `try/catch (Exception ex)` 已吞掉所有网络/反序列化/HTTP 异常并返回 `null`；所以 controller 的 `catch` 永远不会为 Bangumi 相关失败触发。
- `Failed` 计数把"返回 null（限速或无数据）"和"抛异常"混在一起
- 错误信息永远是 `"返回 null（限速或无数据）"`，看不到真正的异常类型
- 注释 `GetEpisodesTotalAsync 内部已识别限速拒绝...` 已经知道这事，但代码仍保留了死 try/catch

**建议修复方案**（任选一）：
1. 删掉 controller 的 try/catch，让 null 分支统一处理，并在服务层把异常细分（`_rateLimited` enum 单独返回）
2. 把 service 改成 `Task<(int? Total, FailureReason Reason)>` 显式区分限速 / 网络错误 / 真无数据

**Why not fixed now**：影响范围仅 admin 接口的诊断体验，不影响功能。

### TD-2：Outer try/catch / 0 计入 Updated 语义模糊（评分 60）

**位置**：[AnimeController.cs:263-268](../backend/ManWei.Api/Controllers/AnimeController.cs#L263-L268)

**问题**：当 `total.HasValue` 为 true（包括 `0`）时，写入并 `Updated++`。
- `Anime.cs` 文档化 0 与 null 等价（"未拉取到"）
- 但这里把 0 算"成功"
- `Updated` 计数被 0 行膨胀；`UpdatedAnimeIds` 里出现"未真正填充"的 ID
- 与 TD-1 合并修更佳

**建议修复方案**：在写入前加 `if (total.Value > 0)`，把 0 计入 `Failed`（reason = "Bangumi reports 0 episodes"）。

### TD-3：Errors 截断后没 truncated 标志（评分 50）

**位置**：[AnimeController.cs:443](../backend/ManWei.Api/Controllers/AnimeController.cs#L443)

**问题**：`Failed` 字段是总数，`Errors` 列表只取前 20 条；admin 响应里看不出有更多失败被丢弃。

**建议修复方案**：
- 方案 A：DTO 加 `bool ErrorsTruncated`
- 方案 B：聚合失败原因（`{"rate_limited": 5, "404": 3, "exception": 12}`）替代逐条罗列

### TD-4：ToList 注释"避免长事务"误导（评分 50）

**位置**：[AnimeController.cs:247](../backend/ManWei.Api/Controllers/AnimeController.cs#L247)

**问题**：注释写"避免长事务期间 DbContext 跟踪过多实体"，但代码里没有显式 transaction。真实原因是要避免 deferred execution N+1 和 DbContext 生命周期问题。

**建议修复方案**：改注释为"避免每次循环重新执行 LINQ 查询（deferred execution / N+1）+ 提前物化 list 让迭代与 DbContext 生命周期解耦"。

### TD-5：CancellationToken 未传递（评分 40）

**位置**：[AnimeController.cs:238](../backend/ManWei.Api/Controllers/AnimeController.cs#L238) 调用 [BangumiService.GetEpisodesTotalAsync:211](../backend/ManWei.Api/Services/BangumiService.cs#L211)

**问题**：
- action 签名无 `CancellationToken ct = default`
- 不接 `HttpContext.RequestAborted`
- 客户端断开后，循环继续跑完所有候选（每行 ~500ms 限速），浪费 Bangumi 配额
- `BangumiRateLimiter.WaitForTokenAsync` 已经支持 `CancellationToken`，但调用链都没传

**建议修复方案**：
- action 签名加 `CancellationToken ct = default`
- 给 `GetEpisodesTotalAsync` 加 `CancellationToken ct = default` 参数并向下传递（包括 `_httpClient.GetAsync(url, ct)`）

**Why not fixed now**：admin 触发频次低；连接断开是小概率；服务有 idempotency，重跑成本可控。

---

## 跨接口（不限于 BackfillEpisodes）

### TD-6：BangumiService.GetEpisodesTotalAsync 内部吞异常（评分 — 与 TD-1 同源）

参考 TD-1。修复 TD-1 时一并处理。

---

## 已修复（本轮）

| 日期 | 问题 | 修复 |
|------|------|------|
| 2026-06-12 | UNIQUE 迁移方法体为空 | 新增 `20260612000000_FixAddUniqueBangumiIdIndex` |
| 2026-06-12 | 0 vs null 语义不一致 | `AnimeController.cs` 过滤条件改为 `is null or 0`；`Anime.cs` 文档化 |
| 2026-06-12 | 验证 Backfill 端到端：发现 2 行 `TotalEpisodes=0` 被新过滤条件捞起并回填成功（ID 22, 23） | — |
