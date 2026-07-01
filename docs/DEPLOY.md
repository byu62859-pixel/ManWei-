# 漫味 ManWei 部署复盘

> 2026-07-01 部署到腾讯云 Ubuntu 22.04 + Docker 26.1.3，全程记录。

---

## 〇、部署前状态

| 维度 | 本地 | 服务器 |
|---|---|---|
| OS | Windows 11 | Ubuntu 22.04 LTS |
| 数据库 | SQL Server（Windows 集成身份验证） | SQL Server 2022 容器 |
| 后端 | `dotnet run` 开发模式 | Docker 容器 `dotnet ManWei.Api.dll` |
| 前端 | `npm run dev` Vite 开发服务器 | nginx 静态文件 |
| 端口 | localhost:5150 / 5173 / 5174 | 公网 80/443 → nginx 反代 |
| 密钥 | appsettings.json 占位符 | .env 环境变量注入 |
| 数据 | 本地 ManWeiDB（136 动漫 / 12 用户） | 空库 → RESTORE 还原 |

---

## 一、部署架构

```
公网用户 ──HTTPS──▶ nginx (alpine) :80/:443
                     │
                     ├── /api/*      ──▶ api (.NET 8) :8080
                     │                   ├── manwei_uploads 卷
                     │                   └── 环境变量注入密钥
                     │
                     ├── /admin/*    ──▶ 静态文件 (/app/projects/pc-admin-dist)
                     └── /           ──▶ 静态文件 (/app/projects/pc-client-dist)

api ──TCP──────────▶ sqlserver (2022) :1433
                       └── manwei_db_data 卷
```

---

## 二、分阶段执行记录

### 阶段 0：服务器基础准备

| 操作 | 结果 |
|---|---|
| 装 trash-cli / git / curl / openssl | ✅ |
| UFW 防火墙开 22/80/443 | ✅ |
| 创建 `/app/projects/` | ✅ |

**踩坑**：腾讯云有**双重防火墙**——OS 内 UFW + 腾讯云控制台安全组。阶段 0 只开了 UFW，443 端口到阶段 5 才发现腾讯云控制台没放行，导致 HTTPS 不通。

---

### 阶段 1：本地构建前端 + 推 dist/

| 操作 | 结果 |
|---|---|
| `npm ci && npm run build`（pc-admin + pc-client） | ✅ |
| `scp` 推送 dist/ 到 `/app/projects/pc-admin-dist` | ✅ |

**踩坑**：2 GB 内存入门型实例不能直接在服务器 `npm run build`（会 OOM），必须在本地构建再推送。

**踩坑**：`scp` 推 root@IP 被拒——腾讯云默认禁用 root 密码登录；改用 ubuntu@IP 成功。

**踩坑**：`/app/projects/` 属主是 root:root，ubuntu 用户无写权限；`chown -R ubuntu:ubuntu` 修复。

---

### 阶段 2：服务器拉代码 + 写配置文件

| 操作 | 结果 |
|---|---|
| `git clone` 到 `/app/projects/manwei` | ✅ |
| 创建 `.env`（密钥不入库） | ✅ |
| 创建 `deploy/Dockerfile.api` | ✅ |
| 创建 `deploy/nginx.conf` | ✅ |
| 创建 `docker-compose.yml` | ✅ |

**踩坑**：SSH 终端里用 `cat > file << 'EOF'` heredoc 粘贴长文本时，**多行内容被截断**（后半段丢失）。改为本地 Write 工具创建文件 + `scp` 推送。

**踩坑**：`.env` 的 `$SA_PASSWORD` 通过 `docker compose config` 调试时**明文打印到终端**——`docker compose config` 会展开所有 `${}` 占位符。教训：调试 compose 配置时用 `--services` 只看服务列表，**不要用 `config` 看完整配置**。

**踩坑**：`docker-compose.yml` 里 `healthcheck.test` 用了 `$$SA_PASSWORD`，但 docker compose 解析后容器内 shell **拿不到这个环境变量**（healthcheck 执行环境与容器 environment 不同）。改为 `["CMD-SHELL", "..."]` 数组形式 + 显式传 `-e SA_PASSWORD` 修复。

---

### 阶段 3：启动 sqlserver 容器

| 操作 | 结果 |
|---|---|
| `docker compose up -d sqlserver` | ✅（镜像下载 ~34 分钟） |
| SA 密码验证 | ✅（sqlcmd 需要 `-C` 信任自签证书） |

**踩坑**：SQL Server 2022 容器首次启动需要 10-15 分钟初始化系统数据库（master/model/msdb/tempdb），期间 `docker compose ps` 显示 `(health: starting)` 而非 `(unhealthy)`。

**踩坑**：sqlcmd 连接容器内 SQL Server **必须加 `-C`**（TrustServerCertificate），否则报 SSL 握手错误。

---

### 阶段 4：启动 api 容器 + 数据库自动建库

| 操作 | 结果 |
|---|---|
| `docker compose up -d --build api` | ✅ |
| EF Core EnsureCreatedAsync 自动建库 | ✅ 8 张表 |
| ManualMigration.RunAsync | ✅ |

**结论**：不需要手动 CREATE DATABASE 或跑 DDL 脚本——`Program.cs` 启动时自动执行 `EnsureCreatedAsync` + `ManualMigration`。

---

### 阶段 5：启动 nginx + HTTPS

| 操作 | 结果 |
|---|---|
| 自签 SSL 证书 | ✅ |
| nginx 容器启动 | ✅ |
| 腾讯云控制台**放开 443 端口** | ⚠️ 关键遗漏 |

**踩坑（关键）**：阶段 0 只开了 UFW 防火墙，但**腾讯云控制台安全组默认只放 22/80/ICMP**，443 没放行。浏览器长时间报 `ERR_CONNECTION_TIMED_OUT` / `ERR_SSL_PROTOCOL_ERROR`。到腾讯云控制台手动添加 443 规则后立即通。

**踩坑**：PowerShell 5.1 的 `curl` / `Invoke-WebRequest` 与自签证书 + HTTP/2 不兼容（TLS 握手失败）。用 `[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }` 跳过验证。

---

### 阶段 5.6：修复前端适配生产环境

| 问题 | 根因 | 修复 |
|---|---|---|
| 管理端 JS 返回 `text/plain` | alpine nginx 默认不加载 mime.types | `include /etc/nginx/mime.types;` |
| 管理端空白页 | `createWebHistory()` 无 base | `createWebHistory('/admin/')` |
| 登录 POST `localhost:5150` | axios baseURL 硬编码 | `baseURL: '/'` |
| 头像 URL 硬编码 | `getAvatarUrl` 里 `localhost:5150` | `baseURL = ''` |
| `/api/api/Auth/login` 404 | baseURL 双重 /api 前缀 | baseURL 改为 `/` 而非 `/api` |

---

### 阶段 7：数据迁移

| 操作 | 结果 |
|---|---|
| 本地 SSMS 备份 → 8.2 MB .bak | ✅ |
| 腾讯云 WebShell SFTP 上传 | ✅（绕过 scp 密码限制） |
| `docker cp` 到 sqlserver 容器 | ✅ |
| `RESTORE DATABASE WITH MOVE, REPLACE` | ✅ 994 pages / 0.168s |
| 136 动漫 + 12 用户 + 31 收藏入库 | ✅ |
| 头像文件从本地迁移 | ❌ 卷里是空的（头像不包含在 .bak 里）|
| 前端上传新头像 | ✅ 200 OK |

**踩坑**：`RESTORE DATABASE` 需要 `WITH MOVE` 把 Windows 路径映射到 Linux 容器路径（`D:\...\ManWeiDB.mdf` → `/var/opt/mssql/data/ManWeiDB.mdf`）。

**踩坑**：`RESTORE` 前必须先 `ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE`——否则报"数据库已存在"。

---

## 三、踩坑清单（按严重程度排序）

| 严重度 | 问题 | 原因 | 解决 | 避免方法 |
|---|---|---|---|---|
| 🔴 致命 | 443 端口不通 | 腾讯云双重防火墙 | 控制台安全组加 443 | 部署前检查**云控制台**安全组规则 |
| 🔴 致命 | nginx 返回 MIME `text/plain` | alpine nginx 无 mime.types | `include /etc/nginx/mime.types;` | nginx.conf 模板里始终加这一行 |
| 🔴 致命 | 管理端空白页 | Vue Router 无 base | `createWebHistory('/admin/')` | 子路径部署时同步改 router base |
| 🟠 严重 | axios 硬编码 localhost:5150 | 前端 dev 配置未区分环境 | `baseURL: '/'` | 用 Vite 环境变量区分 dev/prod |
| 🟠 严重 | 头像文件迁移丢失 | .bak 只含数据库不含文件 | 前端重新上传 | 文件与数据库分开备份 |
| 🟡 中等 | heredoc 多行截断 | SSH 终端缓冲区限制 | 改用 scp 推送文件 | 长配置文件用 scp 而非 heredoc |
| 🟡 中等 | docker compose config 泄漏密码 | `${}` 展开 | 不用 `config` debug | 用 `config --services` |
| 🟡 中等 | sqlserver healthcheck 失败 | `${SA_PASSWORD}` 不传 | `["CMD-SHELL", "..."]` + `-e` | 数组形式 + 显式传环境变量 |
| 🟢 低 | scp Permission denied | ubuntu 用户无写权限 | `chown -R ubuntu:ubuntu` | clone 前先 chown |
| 🟢 低 | PowerShell 5.1 curl 不兼容 | TLS/HTTP2 协商失败 | 浏览器直接测试 | |

---

## 四、关键经验

### 1. 分步启动优于一键启动

`docker compose up -d` 一次性启动所有容器，出错时难以定位。改为：
1. 先起 sqlserver → 验证 SA 密码
2. 再起 api → 验证自动建库
3. 最后起 nginx → 验证 HTTPS

### 2. 前端构建不在服务器做

2 GB 入门型实例 + React 19 + TS + 4000+ 模块 = OOM 高风险。本地构建 + scp 推送是最稳妥方案。

### 3. 腾讯云是双重防火墙

OS 内 UFW + 控制台安全组。部署前**必须先检查控制台**，否则浪费几十分钟排查"为什么端口不通"。

### 4. 子路径部署的双 base 规则

`vite build --base=/admin/` 只修静态资源路径。Vue Router 也需要 `createWebHistory('/admin/')`。缺一个 = 空白页。

### 5. alpine nginx 的 mime.types 陷阱

nginx:alpine 镜像**不会自动 `include mime.types`**——必须在 http 块显式写。否则所有 .js 返回 text/plain，浏览器拒绝加载 ES Module。

### 6. 数据库 .bak 是最快的迁移方式

`RESTORE WITH MOVE, REPLACE` 一条命令，0.064 秒恢复 994 页。比 SQL 脚本导入快几个数量级。

### 7. 文件（头像）和数据库分开备份

.bak 只备份数据库。用户上传的头像在 `wwwroot/uploads/`，需要单独备份。部署后卷是空的——在服务器端重新上传。

---

## 五、下次部署需要的文件清单

| 文件 | 说明 | 是否入库 |
|---|---|---|
| `deploy/Dockerfile.api` | 多阶段构建 | ✅ |
| `deploy/nginx.conf` | nginx 反代 + mime.types | ✅ |
| `docker-compose.yml` | 三容器编排（无密钥） | ✅ |
| `.env` | 生产密钥 | ❌（绝对不入库） |
| `deploy/certs/` | SSL 证书 | ❌ (.gitignore 已忽略) |
| `frontend/pc-admin/dist/` | 本地构建产物 | ❌ (.gitignore 已忽略) |
| `frontend/pc-client/dist/` | 同上 | ❌ |

---

## 六、日常运维命令速查

```bash
# 查看所有容器状态
cd /app/projects/manwei && sudo docker compose --env-file .env ps

# 查看 api 日志
sudo docker compose --env-file .env logs --tail=50 api

# 重启 api（代码更新后）
sudo docker compose --env-file .env up -d --build api

# 重载 nginx（配置更新后）
sudo docker compose --env-file .env restart nginx

# 数据库紧急连接
SA_PWD=$(grep SA_PASSWORD .env | cut -d= -f2)
sudo docker exec -e SA_PASSWORD="$SA_PWD" manwei-sqlserver \
  /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PWD" -d ManWeiDB

# 磁盘空间
df -h && sudo docker system df

# 容器资源
sudo docker stats --no-stream
```
