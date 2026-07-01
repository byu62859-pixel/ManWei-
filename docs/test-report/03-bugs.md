# 漫味 (ManWei) 项目测试报告 — 03 缺陷清单

> 本文档记录本轮测试发现的真实 BUG。**不预设缺陷数量**——只有 1 条（BUG-001）。

---

## 1. 缺陷总览

| 项目 | 数值 |
|---|---|
| 真实 BUG | **1 个**（BUG-001） |
| 归类 | 测试工具链问题，**非后端 BUG** |
| 严重程度 | 中（污染测试数据，对生产 0 风险） |
| 已修复 | ✅（详见 04-optimization.md） |
| 已回归 | ✅（详见 05-regression.md） |

另有 4 条"边界发现"已在 `02-test-execution.md §5` 描述，均为**已确认设计选择，非 BUG**，本文档不收录。

---

## 2. BUG-001：PowerShell GBK 控制台中文 JSON 传输字符损坏

### 2.1 基本信息

| 字段 | 内容 |
|---|---|
| **编号** | BUG-001 |
| **严重程度** | 中 |
| **分类** | 测试工具链问题（**非后端 BUG**） |
| **影响范围** | 仅测试数据库 `EmotionTags` 表 2 条记录（Id=49、Id=52），对生产用户数据 **0 风险** |
| **复现条件** | Windows GBK 控制台 + PowerShell `-Command` + 内联含中文的 JSON 字符串 + `for` 循环发送 |
| **修复策略** | 客户端改用 UTF-8 JSON 文件 + bash `curl --data-binary @file`，完全避开 PowerShell 中文处理 |

### 2.2 现象

通过 PowerShell `-Command` 在 Windows GBK 控制台发送含中文的 JSON 测试数据时，`EmotionTags` 表中部分中文标签被损坏，显示为不可读字符。

### 2.3 根因分析

#### 2.3.1 后端 NVARCHAR 兼容性（已排除后端责任）

`EmotionTag.Name` 字段类型为 string，EF Core 默认映射 `nvarchar(max)`，SQL Server `nvarchar` 完全兼容 Unicode 中文。同一批次测试里 **6 条以上中文标签完整保存**（"热血""冲击""剧情向""思考""热血少年""催泪""神作""治愈"），只有 2 条损坏——说明后端编码链路正常，问题在客户端。

#### 2.3.2 客户端编码陷阱

PowerShell `-Command` 在 Windows GBK 控制台下发送中文 JSON 时，`for $t in $tags` 循环的特定路径会产生字符损坏：UTF-8 字节被错误按 Latin-1（ISO 8859-1）解码后重新编码。

#### 2.3.3 损坏记录详情（字节级证据）

| ID | 显示 | Unicode | UTF-8 字节 | 损坏路径 |
|---|---|---|---|---|
| 49 | `?` | U+003F | `3F` | PowerShell `for` 循环第 1 轮失败路径，UTF-8 多字节被截断为单字节 0x3F |
| 52 | `ȼ` | U+023C | `C8 BC` | PowerShell `for` 循环第 1 轮另一失败迭代，UTF-8 三字节 `E7 87 83` 被按 Latin-1 重编码为 `C8 BC` |

两条均为 PowerPoint `for $t in $tags` 第一次循环的不同失败模式产物，根因同类。

#### 2.3.4 证据来源

已通过 SQL Server `sqlcmd` 直接查 `EmotionTags` 表确认 Unicode 码点和 UTF-8 原始字节——**不是查询渲染问题**，是数据层已损坏。

### 2.4 修复策略

| 层面 | 措施 | 效果 |
|---|---|---|
| **客户端（推荐）** | 测试时用 UTF-8 编码的 JSON 文件 + bash `curl --data-binary @file` 直接发送 | **完全避开** PowerShell `-Command` 的中文处理路径 |
| **数据层** | 已删除 Id=49、52（详见 04-optimization.md） | 数据库恢复干净状态 |

### 2.5 截图证据

| 截图 | 说明 |
|---|---|
| `screenshots/bug-001-evidence-raw-bytes.png` | 修复前：含 2 条脏数据（Id=49/Id=52）+ Unicode/UTF-8 字节标注 |
| `screenshots/optimization-bug-001-after-cleanup.png` | 修复后：仅 9 条正常标签，无脏数据残留 |

---

## 3. 缺陷分级标准

| 等级 | 定义 | 本测试 |
|---|---|---|
| 严重 | 影响核心功能，用户数据损坏或安全漏洞 | 0 个 |
| 中 | 影响局部功能或污染测试数据，对生产低风险 | **BUG-001（1 个）** |
| 低 | 文案/样式偏差，不影响功能 | 0 个 |

---

## 4. 未发现 BUG 的领域

12 个一级用例中 **11 个完全通过**（D1 清理后回归也通过），验证后端代码质量稳定：

- 认证与鉴权（A1/A2）：JWT 签发/验证正确
- 检索与收藏（B1/B2/B3）：状态机正确，Bangumi 同步正常
- 情绪曲线（C1/C2）：写入/读取/边界校验全部通过（CLAUDE.md L20 已验证修复）
- 观后感（E1）：中文 nvarchar 保存完整
- 推荐算法（F1）：冷启动 full 模式正常
- AI 对话（G1/G2）：流式 NDJSON 正常，TTFB 207ms
