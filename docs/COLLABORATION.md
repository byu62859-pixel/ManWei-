# 前后端协作规范

> 本文档定义前端与后端 API 的协作约定，确保联调顺利。

---

## 一、响应格式

所有接口统一使用 `Result<T>` 包装：

```json
{
  "code": 200,
  "message": "操作成功",
  "data": { ... },
  "isSuccess": true
}
```

| code | 含义 |
|------|------|
| 200 | 成功 |
| 400 | 参数错误 / 业务错误 |
| 401 | 未登录 / Token 过期 |
| 403 | 无权限 |
| 404 | 资源不存在 |
| 500 | 服务器错误 |

**前端取数路径**: `res.data.xxx`（注意是两层 `data`）

```typescript
const res = await request.get('/anime');
if (res.code === 200) {
  setList(res.data.items);
}
```

---

## 二、分页结果格式

分页接口统一返回 `PagedResult<T>`：

```json
{
  "items": [...],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

---

## 三、API 端点汇总

### 3.1 认证 `/api/auth`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| POST | `/api/auth/Register` | `Username`, `Password`, `NickName?` | 匿名 |
| POST | `/api/auth/Login` | `Username`, `Password` | 匿名 |
| POST | `/api/auth/wx-login` | `Code` | 匿名 |

**登录响应**：
```json
{
  "token": "JWT...",
  "expiresIn": 604800,
  "userId": 1,
  "nickName": "xxx",
  "role": "User"
}
```

---

### 3.2 动漫 `/api/anime`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/anime` | `Page`, `PageSize`, `Keyword?`, `Type?`, `TagName?` | 匿名 |
| GET | `/api/anime/{id}` | `id`(路径) | 匿名 |
| POST | `/api/anime/Sync/{bangumiId}` | `bangumiId`(路径) | 需登录 |
| POST | `/api/anime` | `Name`, `Cover?`, `Summary?`, `AnimeType?` | 需登录 |
| PUT | `/api/anime/{id}` | `id`(路径), body | 需登录 |
| DELETE | `/api/anime/{id}` | `id`(路径) | 需登录 |

**分页参数（PascalCase）**：
```typescript
request.get('/anime', { params: { Page: 1, PageSize: 20, Keyword: '海贼王' } });
```

---

### 3.3 收藏 `/api/favorites`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/favorites` | `Page`, `PageSize`, `Status?`, `TagName?`, `OrderBy?` | 需登录 |
| GET | `/api/favorites/check/{animeId}` | `animeId`(路径) | 需登录 |
| GET | `/api/favorites/{id}` | `id`(路径) | 需登录 |
| POST | `/api/favorites` | `{ AnimeId: number }` | 需登录 |
| PUT | `/api/favorites/{id}` | `id`(路径), `{ Status?, Progress?, Rating? }` | 需登录 |
| DELETE | `/api/favorites/{id}` | `id`(路径) | 需登录 |

**收藏状态值**：
- `0` = 想看
- `1` = 在看
- `2` = 看过

**评分范围**：`1-10`，传 `null` 取消评分

**进度**：`Progress >= 0`

---

### 3.4 情感标签 `/api/emotiontags`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/emotiontags?animeId=1` | `animeId`(必需) | 需登录 |
| GET | `/api/emotiontags/used` | - | 需登录 |
| POST | `/api/emotiontags` | `{ Name, AnimeId }` | 需登录 |
| DELETE | `/api/emotiontags/{id}` | `id`(路径) | 需登录 |

**TagName 查询时会 Trim**：
```typescript
// 查询时需 Trim
const tagName = query.TagName.trim();
```

---

### 3.5 情感曲线 `/api/emotioncurves`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/emotioncurves/{favoriteId}` | `favoriteId`(路径) | 需登录 |
| POST | `/api/emotioncurves` | `{ FavoriteId, Episode?, EmotionLevel }` | 需登录 |
| DELETE | `/api/emotioncurves/{favoriteId}/{episode}` | `favoriteId`, `episode`(路径) | 需登录 |

**EmotionLevel 范围**：`1-5`

**Episode 默认**：`1`

---

### 3.6 观后感 `/api/reviews`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/reviews` | `page`, `pageSize`（**小写**） | 需登录 |
| GET | `/api/reviews/{favoriteId}` | `favoriteId`(路径) | 需登录 |
| POST | `/api/reviews` | `{ FavoriteId, Content }` | 需登录 |
| DELETE | `/api/reviews/{favoriteId}` | `favoriteId`(路径) | 需登录 |

**⚠️ 注意**：此端点分页参数是**小写** `page`/`pageSize`，与其他端点不同！

---

### 3.7 用户 `/api/users`

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/users/me` | - | 需登录 |
| POST | `/api/users/me/avatar` | `multipart/form-data`：`file` | 需登录 |
| PUT | `/api/users/me/nickname` | `{ NickName }` | 需登录 |
| GET | `/api/users/me/stats` | - | 需登录 |

**NickName 会被 Trim**

**头像上传**：仅支持 JPG / PNG / WEBP，最大 2MB，返回 `UserDto.avatar` 静态访问路径。

---

### 3.8 数据中心 `/api/dashboard`（Admin）

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| GET | `/api/dashboard/stats` | - | Admin |
| GET | `/api/dashboard/today-overview` | - | Admin |
| GET | `/api/dashboard/user-growth` | `days?` | Admin |
| GET | `/api/dashboard/anime-rank` | `top?` | Admin |
| GET | `/api/dashboard/tag-rank` | - | Admin |

---

### 3.9 AI 对话

| 方法 | 路径 | 参数 | 授权 |
|------|------|------|------|
| POST | `/api/aiagent/chat` | `{ Message, History? }` | Admin |
| POST | `/api/wxaiagent/chat` | `{ Message, History? }` | 需登录 |

---

## 四、认证与授权

### 4.1 Token 传递

```typescript
// request.ts 拦截器自动注入
const token = localStorage.getItem('mw_token');
config.headers.Authorization = `Bearer ${token}`;
```

### 4.2 401 处理

```typescript
// request.ts 拦截器
if (error.response?.status === 401) {
  localStorage.removeItem('mw_token');
  window.location.href = '/login';
}
```

### 4.3 角色说明

| 角色 | 说明 |
|------|------|
| `User` | 普通用户 |
| `Admin` | 管理员 |

---

## 五、参数命名规范

### 5.1 统一规则

| 场景 | 规范 | 示例 |
|------|------|------|
| 查询参数（FromQuery） | PascalCase | `Page`, `PageSize`, `Keyword` |
| 路径参数 | PascalCase | `/anime/{id}` |
| 请求体（FromBody） | PascalCase | `{ "AnimeId": 1, "Name": "xxx" }` |

### 5.2 特殊情况

**ReviewsController 分页参数是小写** `page`/`pageSize`：

```typescript
// ❌ 错误（大写取不到）
request.get('/reviews', { params: { Page: 1, PageSize: 10 } });

// ✅ 正确（小写）
request.get('/reviews', { params: { page: 1, pageSize: 10 } });
```

**EmotionTags TagName 查询时会 Trim**，前端建议传入前也 Trim：

```typescript
params.append('TagName', tagName.trim());
```

---

## 六、踩坑记录

### 6.1 API 参数大小写

**现象**：分页请求 Page=2 但返回第一页数据
**原因**：axios params 对象 key 使用 PascalCase，ASP.NET Core 可绑定
**解决**：参考本文档 5.1 节，ReviewsController 用小写

### 6.2 EmotionTags.Name 空格污染

**现象**：标签查询不到
**原因**：存储时未 Trim，查询时需匹配原始空格
**解决**：写入时 `Trim()`，查询时 `t.Name == query.TagName.Trim()`

### 6.3 Rating null 排最后

**现象**：排序时 null 值不知道放哪
**解决**：SQL Server 的 `OrderByDescending(f => f.Rating == null).ThenByDescending(f => f.Rating)` 实现 NULLS LAST

### 6.4 deepSeek tool_call_id 字段丢失

**现象**：调用 AI 时报 400
**解决**：从原始 `JsonElement` 取 tool_call_id，不能用 `ChatMessage` 类重建

### 6.5 收藏页 wx:for 变量遮蔽（小程序）

**现象**：内外层 `wx:for` 数据错乱
**解决**：内外层 `wx:for` 必须显式命名 `wx:for-item` / `wx:for-index`

---

## 七、数据模型

### 7.1 UserDto

```typescript
interface User {
  id: number;
  openId: string;
  nickName: string | null;
  avatar: string | null;
  role: 'User' | 'Admin';
  isEnabled: boolean;
  createTime: string;
}
```

### 7.2 AnimeDto

```typescript
interface Anime {
  id: number;
  bangumiId: number | null;
  name: string;
  cover: string | null;
  summary: string | null;
  animeType: string;
  createTime: string;
  favoriteCount: number;  // 收藏人数
  avgRating: number | null;  // 平均评分 1-10
  reviewCount: number;  // 观后感数量
}
```

### 7.3 FavoriteDto

```typescript
interface Favorite {
  id: number;
  animeId: number;
  userId: number;
  status: 0 | 1 | 2;  // 想看/在看/看过
  progress: number;  // 已看集数
  rating: number | null;  // 评分 1-10
  animeName: string;
  animeCover: string | null;
  animeType: string;
  emotionTagNames: string[];
  createTime: string;
}
```

### 7.4 EmotionCurveDto

```typescript
interface EmotionCurve {
  episode: number;
  emotionLevel: number;  // 1-5
  createTime: string;
}
```

### 7.5 ReviewDto

```typescript
interface Review {
  favoriteId: number;
  content: string;
  createTime: string;
  updateTime: string;
}
```

---

## 八、联调检查清单

- [ ] 响应结构是 `res.data.xxx` 不是直接 `res.xxx`
- [ ] 分页参数大小写正确（`Page` vs `page`）
- [ ] 需要登录的接口确认 Token 已注入
- [ ] POST/PUT 请求体默认是 JSON；头像上传接口例外，使用 `multipart/form-data`
- [ ] 路径参数不要放在 params 里
- [ ] 空值参数用 `undefined` 而不是 `null`
