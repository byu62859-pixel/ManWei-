# recommend_anime + search_anime 实施进度

> **进度说明**：本文件用于在跨会话中保持实施连续性。下一位 agent 应**先读此文件**，无需重新走公式确认。
> 
> **本文档因上一轮对话 context 用量达 70% 而创建，用于无损切换到新对话。算法设计阶段已 100% 完成，进入实施阶段。**
> 
> **当前状态**：算法公式已全部确认并写入本文档（§二），后端代码实施**未开始**。

---

## 一、Context（为什么做）

PC 端 AI 助手当前有 5 个 tool，其中 2 个是占位（`search_anime`、`query_global_emotion_tags`）。本次任务：

1. 替换 `search_anime` 桩为真实 Bangumi 关键词搜索
2. 新增 `recommend_anime` tool，提供**含情绪维度**的个性化推荐（论文方法章节用）
3. 新增独立 REST 接口 `GET /api/recommendations`（前端"为你推荐"模块用，复用同一 `RecommendAnimeService`）

预期产出：
- 5 个文件改/建（`PcAiTools.cs`、`PcAiAgentService.cs`、`BangumiService.cs` 不动、新建 `RecommendAnimeService.cs` + 子目录 5 文件 + `Models.cs`）
- 1 个新 Controller（`RecommendationsController.cs`）
- 1 个论文用说明文档（`docs/recommendation.md`）

---

## 二、完整公式（已确认版，无截断）

### 1. 用户标签画像（TF-IDF + max-pool + L2 归一）

对每个高评分番 `a ∈ H`，本地 `Anime.AnimeTags` 是 Bangumi Top-5 (Count 是该 tag 在该番的投票数)：

```
TF_a(t) = t.Count / Σ_{t' ∈ a.AnimeTags} t'.Count          # a 内归一
IDF(t)  = log( N / (1 + df(t)) )                            # N = 候选池大小, df(t) = 含 t 的候选数
U_tag(t) = max_{a ∈ H}  TF_a(t) · IDF(t)  if t ∈ a.Tags else 0
U_tag_norm(t) = U_tag(t) / ||U_tag||₂                       # L2 归一 → U_tag ∈ [0,1], ΣU_tag² = 1
```

**为什么用 max-pool 而非 sum**：用户的"最爱"标签往往来自单部神作而非多部均值；max 更能反映"被这部番打动的核心标签"。

### 2. 用户情绪画像

对每个高评分番 `a ∈ H` 的 EmotionRecord：

```
E_avg(a)  = mean(EmotionRecord.EmotionLevel where FavoriteId = f(a).Id)   # 1-5
E_std(a)  = std(同上)                                                       # ≥ 0
```

聚合为用户级画像：

```
E_avg_global = mean(E_avg(a)  over a ∈ H)
E_std_global = mean(E_std(a)  over a ∈ H)
```

**E_avg vs E_std 互补而非替代**：E_avg 高说明偏好"爽/燃"型（如战斗番）；E_std 高说明偏好"虐/起伏"型（如悲剧）。

### 3. 候选番打分（per-candidate, 范围 [0, 1.1]）

**步骤 1：tagOverlap**

```
tagVec_c(t) = (t.Count / Σ t'.Count)  if t ∈ c.Tags else 0     # 候选 tag TF
tagOverlap_c = Σ_{t ∈ c.Tags}  U_tag_norm(t) · tagVec_c(t)     # 点积, 范围 [0, 1]
```

**步骤 2：近邻查找（候选番无 EmotionRecord，需找 H 中最相似番代理）**

```
nearest_c = argmax_{a ∈ H}  Σ_{t ∈ c.Tags ∩ a.Tags}  U_tag_norm(t)
```

**关键决定：`tagSim(t) = U_tag_norm(t)`，直接复用步骤 1 算好的标签向量**。逻辑闭环，不引入新概念。

> Defensive 分支：若 H 非空但理论上找不到近邻（H 里每部番有 1 个 tag，c.Tags 至少 1 个），仍写 `emotionAffinity = 0.5` 作为兜底。**但实际上此分支永不触发**（H 非空时 `nearest_c` 必存在），论文不描述此兜底。

**步骤 3：emotionAffinity**

```
emo_avg_sim_c = 1 - |E_avg_global - E_avg(nearest_c)| / 4   # 范围 [0, 1]
emo_std_sim_c = 1 - |E_std_global - E_std(nearest_c)| / 2   # 范围 [0, 1]
emotionAffinity_c = 0.6 · emo_avg_sim_c + 0.4 · emo_std_sim_c
```

**步骤 4：qualityBoost**

```
qualityBoost_c = (c.BangumiScore ?? 6.5) / 10                  # 范围 [0, 1]
```

**步骤 5：综合分（两段式：归一化主分 + 质量加成）**

```
baseScore_c = 0.6 · tagOverlap_c + 0.4 · emotionAffinity_c   # 主分, 0.6+0.4=1.0 归一
score_c     = baseScore_c + 0.1 · qualityBoost_c              # 加成, 范围 [0, 1.1]
```

> **设计说明**：qualityBoost 是"加成项"而非同级权重。三权重相加 = 1.1 是有意为之：baseScore 反映个性化（0-1 之间），qualityBoost 是软下限兜底（防止无标签候选得 0 分）。论文中需明确这一段式定义，避免读者误以为三权重同级却凑不齐 1。

### 4. 冷启动 3 档（统一两段式写法，**已删除原计划的 type_only 档**）

| Mode | 触发条件 | baseScore 公式 | qualityBoost |
|---|---|---|---|
| `full` | 标签 + 情绪都有 | `0.6·tag + 0.4·emo` | `0.1` |
| `tag_only` | 标签有 / 情绪空 | `1.0·tag + 0·emo` | `0.1` |
| `popular` | `len(H) == 0` OR `len(candidates) == 0` | 跳过 baseScore，候选按 `BangumiScore` 降序 | — |

**判定逻辑（伪代码）**：

```python
def resolve_mode(high_rated, candidates):
    if len(high_rated) == 0:
        return 'popular'                       # 1) 无 H
    if len(candidates) == 0:
        return 'popular'                       # 2) 候选池空
    if not has_emotion_profile(high_rated):
        return 'tag_only'
    return 'full'
```

**`has_emotion_profile(H)` 精确定义**（避免边界模糊）：

```
has_emotion_profile = any a ∈ H such that a.EmotionRecords.Count >= 1
```

即 H 中**至少 1 部番**有 ≥1 条 EmotionRecord 就算"有情绪画像"。H 里所有番都没情绪记录时降级到 `tag_only`。

> **彻底弃用** "baseScore 全 0" 这个判定条件——浮点比较 + 0.5 兜底让这个分支实际上永不会触发。改用离散条件清晰可验证。

### 5. 高评分番集合定义

```
H = { f.Anime | f in Favorites(user) and f.Rating >= 8 and f.Status == 2 }
```

- 阈值 8 = Bangumi Score 同区间 1-10
- 8+ 约占 25%，既稀疏又有判别力

### 6. 候选池去重

- **统一用 `BangumiId`（int）做去重 key**
- 原因：BangumiId 是稳定的外部 ID，不受中日文标题差异、副标题、繁体简体影响
- 来源 A（本地）：`Anime.BangumiId`
- 来源 B（Bangumi 搜索）：`BangumiSubjectDto.Id` 字段
- 合并逻辑：先收集来源 A 候选 BangumiId 集合；来源 B 命中若 BangumiId 已存在于 A 集合，跳过；否则按 Bangumi hit 构造临时 `Anime`（不写库）

### 7. 排序输出

```
TopK = OrderByDescending(score) → Take(topK) → DTO 含 breakdown { tagOverlap, emotionAffinity, qualityBoost, nearestNeighbor, mode }
```

### 8. 权重依据（论文论证）

- **0.6 标签主导**：题材是"看什么番"的最强先验；数据最稠密（每番 5 标签）
- **0.4 情绪辅助**：漫味差异化维度（"情绪曲线"是本系统独有概念）；不能更高因数据稀疏
- **0.1 质量加成**：BangumiScore 防"无标签命中"得 0 分；不参与 baseScore 归一化，作为软下限

### 9. 备选方案对比（论文中讨论）

**标签权重**：
- ❌ Jaccard：忽略 Bangumi Count 权重；通用词无惩罚
- ✅ **TF-IDF**：抑制通用词；反映"热度质量"（本方案）
- BM25：参数敏感；tag 固定 5 收益小
- 嵌入向量（未来）：需要外部模型

**情绪权重**：
- 仅均值：丢失"虐/爽"区别
- ✅ **均值+波动**（本方案）：区分起伏/平稳
- 后期加权：需完整集数据；用户常只填关键集

---

## 三、文件改动清单

### Backend — 新建

| 文件 | 用途 |
|---|---|
| `Services/Recommendation/Models.cs` | RecommendRequest, RecommendResult, RecommendItem, ScoreBreakdown, EmotionProfile, UserTagProfile |
| `Services/Recommendation/TagProfileBuilder.cs` | 高评分番集合 + TF-IDF + max-pool + L2 归一 |
| `Services/Recommendation/EmotionProfileBuilder.cs` | avg/std 情绪画像 + `HasProfile(H)` 判定 |
| `Services/Recommendation/CandidatePoolBuilder.cs` | 本地 + Bangumi 双源并集 + BangumiId 去重 + 排除已收藏 |
| `Services/Recommendation/Scorer.cs` | 纯函数 `Score(c, ...)` |
| `Services/Recommendation/ReasonBuilder.cs` | 基于 breakdown 拼解释模板 |
| `Services/Recommendation/ColdStartResolver.cs` | 3 档离散判定 |
| `Services/RecommendAnimeService.cs` | 编排（注入 IRecommendAnimeService） |
| `Controllers/RecommendationsController.cs` | `GET /api/recommendations` 独立 REST 接口 |
| `docs/recommendation.md` | 论文用说明文档（按 Plan agent §七大纲） |

### Backend — 修改

| 文件 | 改动 |
|---|---|
| `Services/PcAiTools.cs` | `search_anime` schema 实化（参数：keyword required, limit 1-25 default 10）；新增 `recommend_anime` schema（参数：keyword, animeType, topK 1-20 default 5） |
| `Services/PcAiAgentService.cs` | 构造函数加 `IBangumiService` + `IRecommendAnimeService` 字段；`switch` L67 替换 search_anime 桩为 `await SearchAnimeAsync(args, ct)`；新增 `recommend_anime` case → `await RecommendAnimeAsync(args, ct)` |
| `Services/BangumiService.cs` | **不动**，`SearchAsync` 已满足 search_anime 需求 |
| `Program.cs` | DI 注册：`AddScoped<IRecommendAnimeService, RecommendAnimeService>()` |
| `Services/PcAiAgentService.cs` SystemPrompt | L36 措辞：search_anime 从"留桩"改为"已实现"；新增 recommend_anime 工具行 |

### Frontend — 暂未开始（后端完成后再做）

| 文件 | 用途 |
|---|---|
| `pages/Home/` 或 `pages/DataCenter/` | "为你推荐"模块（位置待定） |
| 复用 `components/RecommendAnimeCard/` | 卡片组件（封面、番名、reason、score 进度条） |
| `services/recommendations.ts` | 调 `GET /api/recommendations` |
| `types/api.ts` | `RecommendItem`, `RecommendResult` |
| `components/AiAssistantDrawer/index.tsx` | 内嵌简化版卡片（3-5 条） |

> 前端需遵循 FE.md：色彩系统 `--color-bg: #F7F6F3` 等；**禁止圆角卡片+左 border accent**；卡片风格与现有 `AnimeCard` 一致。

---

## 四、17 步实施步骤清单

| # | 步骤 | 状态 |
|---|---|---|
| 1 | 新建 DTO/Models — `Services/Recommendation/Models.cs`   | ✅ 已完成 |
| 2 | TagProfileBuilder — TF-IDF + max-pool + L2   | ✅ 已完成 |
| 3 | EmotionProfileBuilder — avg/std + `HasProfile(H)` 判定   | ✅ 已完成 |
| 4 | CandidatePoolBuilder — 双源 + BangumiId 去重 + 排除已收藏   | ✅ 已完成 |
| 5 | Scorer — 纯函数打分   | ✅ 已完成 |
| 6 | ColdStartResolver — 3 档离散判定   | ✅ 已完成 |
| 7 | ReasonBuilder — 模板化解释   | ✅ 已完成 |
| 8 | RecommendAnimeService — 编排（含 IRecommendAnimeService 接口）   | ✅ 已完成 |
| 9 | PcAiAgentService — 接入（构造函数加 2 个 service；switch 加 2 个 case）   | ✅ 已完成 |
| 10 | PcAiTools — 改 search_anime schema + 新增 recommend_anime   | ✅ 已完成 |
| 11 | RecommendationsController — `GET /api/recommendations`   | ✅ 已完成 |
| 12 | Program.cs — DI 注册 IRecommendAnimeService   | ✅ 已完成 |
| 13 | SystemPrompt 同步更新措辞   | ✅ 已完成 |
| 14 | `dotnet build` 验证通过   | ✅ 已完成 |
| 15 | `docs/recommendation.md` — 论文用说明文档   | ✅ 已完成 |
| 16 | **手动 curl 测试 3 档冷启动场景**   | ✅ 已完成 |
| 17 | **commit 1-2 个**   | ✅ 已完成 |

> 状态图例：✅ 已完成 / 🔄 进行中 / ⬜ 未开始
>
> **本轮（2026-06-17）实施完成**：所有 17 步已 ✅。Build 通过、3 档冷启动 curl 实测通过。详见 §四状态表。

---

## 五、关键设计决策记录

1. **公式两段式**：`baseScore = 0.6·tag + 0.4·emo`（归一） + `0.1·qualityBoost`（加成）—— 避免"三权重凑不齐 1"的视觉误解，论文中需明确说明 qualityBoost 是"加成项"非同级
2. **删除 type_only 档**：标签空的情况罕见，TypePreferenceBuilder 复杂度不划算；保留 3 档（full / tag_only / popular），未来真有需求再扩展
3. **popular 触发用离散条件**：`len(H) == 0` OR `len(candidates) == 0`；彻底弃用"baseScore 全 0"浮点比较
4. **`has_emotion_profile(H)` 精确定义**：H 中**至少 1 部番**有 ≥1 条 EmotionRecord
5. **`tagSim(t) = U_tag_norm(t)`**：近邻查找直接复用步骤 1 算好的标签向量，逻辑闭环不引入新概念
6. **BangumiId 去重**：本地 + Bangumi 搜索都可能返回同一部番，用稳定的外部 ID 去重（不用 Name 字符串匹配）
7. **`emotionAffinity = 0.5` 兜底保留但论文不描述**：H 非空时 `nearest_c` 必存在，理论永不触发；代码保留作 defensive programming
8. **`PcAiAgentService` 复用 `IBangumiService`**：构造函数新增参数；CLAUDE.md L30 已确认 Scoped 注入安全
9. **独立 REST 接口**：`GET /api/recommendations` 与 AI tool 共用 `RecommendAnimeService`；前端"为你推荐"模块直接调，独立于 AI 对话入口
10. **前端延后**：后端验证通过后再做"为你推荐"模块 + AI 内嵌卡片

---

## 六、数据流图

```
[AI tool path]
PcAiAgentService.ExecuteToolAsync("recommend_anime", args, ct)
    │
    ▼ RecommendAnimeAsync(args, ct)
    │  解析 args → RecommendRequest
    ▼
[Service] RecommendAnimeService.RecommendAsync(userId, req, ct)
    │
    ├──► TagProfileBuilder.BuildAsync(userId)        → UserTagProfile
    ├──► EmotionProfileBuilder.BuildAsync(userId)    → EmotionProfile (+ HasProfile flag)
    ├──► CandidatePoolBuilder.BuildAsync(req, userId)
    │       ├─ A. _context.Anime filter (Type, exclude Favorited)
    │       └─ B. _bangumiService.SearchAsync(keyword) + BangumiId 去重
    ├──► ColdStartResolver.Resolve(tag, emo)         → Mode (full / tag_only / popular)
    ├──► foreach candidate: Scorer.Score(c, ...)
    ├──► OrderByDescending → Take(topK)
    └──► ReasonBuilder.Build(item, breakdown, userTag)
         │
         ▼
   JSON: { mode, items: [{animeId, name, tags, score, breakdown, reason}] }

[REST path] (前端"为你推荐"模块)
GET /api/recommendations?keyword=&animeType=&topK=5
    │
    ▼ RecommendationsController.Get(req)
    │
    ▼ RecommendAnimeService.RecommendAsync(userId, req, ct)   ← 同一服务
    │
    ▼ 相同 JSON 输出
```

---

## 七、关键风险与缓解

| 风险 | 缓解 |
|---|---|
| 候选池 Bangumi 拉 tags 慢 | 复用 `RefetchAnimeMetadataAsync` 的 `_pendingFetches` 锁；并发 ≤ 5；失败静默 |
| 标签中英文不一致 | 构建时 `ToLower().Trim()`；不引入同义簇（留未来） |
| `H` 为空 | `ColdStartResolver` 返回 `popular` 模式（先于 Scorer 调用） |
| 候选池空 | 返回 `{"error":"no_candidates"}`，LLM 自然回复 |
| `H.Anime.AnimeTags` 加载 | EF `Include(a => a.AnimeTags)`；或 Select 投影到 dict |
| `BangumiService` 注入到 Scoped Service | 已确认 OK（CLAUDE.md L30 提示） |

---

## 八、验证

- `dotnet build` 通过
- 手动 curl：登录拿 JWT → POST `/api/pcaia/chat-stream` 触发含 `recommend_anime` 的消息；GET `/api/recommendations?topK=5` 验证独立接口
- 3 档冷启动场景各跑一遍（无 H / 有标签无情绪 / 完整）
- 前端 Drawer 触发"推荐类似番"类问题
- `docs/recommendation.md` 完整性：包含 §1 引言 / §2 相关工作 / §3 方法（含公式表+流程图） / §4 备选 / §5 实现 / §6 局限

---

## 九、给下一位 agent 的提示

1. **不要重新走公式确认流程**——本文件 §二 已完整记录最终版
2. **不要修改 `BangumiService.cs`**——`SearchAsync` 已满足 search_anime 需求
3. **commit 顺序建议**：
   - Commit 1：步骤 1-8（Recommendation 子目录 + RecommendAnimeService）
   - Commit 2：步骤 9-17（PcAiAgentService 接入 + Controller + DI + 文档 + 验证）
4. **写完代码后必须**：
   - `cd d:/AnimeEmotion/backend/ManWei.Api && taskkill //F //IM "ManWei.Api.exe" 2>nul || true && dotnet build`
   - 手动 curl 测 3 档冷启动
   - 更新本文档 §四状态表（✅）
5. **任何算法与本文档 §二不一致的地方，先回用户确认再改**
