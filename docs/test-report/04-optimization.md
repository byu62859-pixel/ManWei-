# 漫味 (ManWei) 项目测试报告 — 04 系统优化

> 本文档记录基于 BUG-001 发现的测试环境优化措施。

---

## 1. 优化概述

本轮优化共 **2 项**，均围绕 BUG-001（PowerShell GBK 控制台中文 JSON 传输字符损坏）展开：

| 编号 | 优化项 | 类型 |
|---|---|---|
| 优化-1 | 测试数据清理（删除 2 条脏数据）| 数据修复 |
| 优化-2 | 测试工具链切换（PowerShell → bash curl + UTF-8 JSON 文件）| 流程改进 |

后端代码本身无 BUG，本轮优化不改动生产代码。

---

## 2. 优化项 1：测试数据清理

### 2.1 背景

BUG-001 根因为 PowerShell `-Command` 在 Windows GBK 控制台下中文 JSON 处理故障，导致 `EmotionTags` 表中 2 条测试记录被损坏（Id=49、Id=52）。

### 2.2 修复前状态

`EmotionTags` 表共 **11 条**测试标签：

| 类别 | 条数 | 内容 |
|---|---|---|
| 正常标签 | 9 条 | `ASCII_TAG`、`冲击`、`催泪`、`剧情向`、`热血`、`热血少年`、`神作`、`思考`、`治愈` |
| 损坏标签 | 2 条 | Id=49 显示 `?`（U+003F）；Id=52 显示 `ȼ`（U+023C） |

截图证据：`screenshots/bug-001-evidence-raw-bytes.png`

### 2.3 执行的清理操作

| 步骤 | 请求 | 响应 |
|---|---|---|
| 1 | `DELETE http://localhost:5150/api/EmotionTags/49` | 200 `删除成功` |
| 2 | `DELETE http://localhost:5150/api/EmotionTags/52` | 200 `删除成功` |

### 2.4 修复后状态

`EmotionTags` 表剩余 **9 条**正常标签：

- `GET /api/EmotionTags/used` → 200
- 返回：`["ASCII_TAG","冲击","催泪","剧情向","热血","热血少年","神作","思考","治愈"]`
- **无任何脏数据残留**

截图证据：`screenshots/optimization-bug-001-after-cleanup.png`

### 2.5 前后对比

| 指标 | 修复前 | 修复后 |
|---|---|---|
| 标签总数 | 11 | 9 |
| 损坏标签数 | 2（Id=49, Id=52）| 0 |
| 正常标签数 | 9 | 9（数据完整，未误删） |
| 脏数据残留 | 有 | 无 |
| 对收藏/情绪/推荐的影响 | 无（仅标签独立） | 无 |

---

## 3. 优化项 2：测试工具链改进

### 3.1 改进前流程

```
PowerShell -Command "Invoke-RestMethod ... -Body '{\"name\":\"燃\",\"animeId\":47}'"
                    ↓
              Windows GBK 控制台
                    ↓
          UTF-8 字节被按 Latin-1 错误解码/编码
                    ↓
         nvarchar 字段写入损坏的中文字符
```

### 3.2 改进后流程

```
UTF-8 编码的 JSON 文件（body.json）
         ↓
bash curl --data-binary @body.json \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5150/api/EmotionTags
         ↓
    绕过 PowerShell -Command 的中文处理
         ↓
    nvarchar 字段写入正确的中文字符
```

### 3.3 预防效果

- **完全避开** PowerShell `-Command` 在 Windows GBK 控制台下的中文处理路径
- 后续测试不会再产生同类脏数据
- 不影响后端代码，纯客户端工具链层面的改进

---

## 4. 回归测试结论

> 详细回归步骤与证据见 `05-regression.md`。

- 2 条 DELETE 操作后，`GET /api/EmotionTags/used` 立即返回 200，数据集为 9 条正常标签
- 推荐接口（Favorites）、收藏接口、情绪曲线接口未受清理操作影响，抽查结果正常
- 无新问题引入

---

## 5. 未在本轮执行的优化项

为保持诚实，以下工作**不在本轮测试范围**：

| 项目 | 说明 |
|---|---|
| 后端代码改动 | 后端代码本身无 BUG，无须改动 |
| 性能调优 | 非本测试目标；性能基线已采集（详见 02-test-execution.md），后续可按需优化 |
| D1.7 / AiAgent Admin 403 | 已确认为设计选择，不是缺陷，不"优化" |
