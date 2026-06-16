# PC用户端 · 前端约束

> 本文档记录前端专属的设计约束和项目规范

---

## 一、项目概述

**目录**: `frontend/pc-client`
**技术栈**: React 18 + TypeScript + Vite
**用途**: 漫味追番管理PC端，对标微信小程序，功能增强在数据中心

---

## 二、设计约束

### 2.1 色彩系统
```css
--color-bg: #F7F6F3;           /* 背景：极淡米白 */
--color-text: #1C1C1E;         /* 主文字：深墨色 */
--color-text-secondary: #6B6B6B; /* 次文字 */
--color-accent: #D4A574;        /* 强调色：琥珀橙 */
--color-border: #E8E4DE;       /* 边框 */
```

### 2.2 布局原则
- **流动布局**: 非固定式，内容区最大宽度 1200px
- **克制留白**: 呼吸感，不填满
- **无固定侧边栏**: 导航项横向排列或可折叠

### 2.3 禁止事项
- 禁止圆角卡片 + 左 border accent 组合
- 禁止紫色渐变
- 禁止 emoji 作图标
- 禁止装饰性 icon 堆砌

---

## 三、页面清单

| 页面 | 路由 | 状态 |
|------|------|------|
| 登录 | `/login` | ✅ 已完成 |
| 首页 | `/` | ✅ 已完成 |
| 动漫详情 | `/anime/:id` | ✅ 已完成 |
| 收藏页 | `/favorites` | ✅ 已完成 |
| 观后感 | `/reviews` | ✅ 已完成 |
| 个人中心 | `/profile` | ✅ 已完成 |
| 数据中心 | `/data` | ✅ 已完成 |

---

## 四、目录结构

```
src/
├── components/          # 通用组件
├── pages/              # 页面
│   └── Login/          # 已完成
├── services/
│   └── request.ts      # Axios实例，已配置token拦截
├── stores/
│   └── authStore.ts    # 认证状态
└── types/
    └── api.ts         # API响应类型
```

---

## 五、已配置项

| 项目 | 配置 |
|------|------|
| Vite proxy | `/api`、`/uploads` → `http://localhost:5150` |
| 端口 | 5173 |
| Zustand | authStore 已建立 |
| Axios | 请求拦截器自动注入 token |

---

## 六、开发进度

1. **已完成**: 登录、首页、动漫详情、收藏页、观后感、个人中心、数据中心
