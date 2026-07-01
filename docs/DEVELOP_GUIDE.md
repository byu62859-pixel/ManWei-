# 📦 漫味 ManWei 项目代码规范、存放规则与数据安全防御标准说明书

> **个人开发自留标准**：
> 线上环境无小事，慢就是快。保持仓库干净、严格执行分离配置、死守数据防删防误操作底线，是全栈独立开发者的基本素养。本文档为 v1.2 修订版，结构示例已对齐实际工程目录。

---

## 🟢 一、代码仓库存放规则（"纯源"白名单）

GitHub 仓库原则上**只能存放源代码和项目的元数据（如依赖描述配置文件）**。任何可以通过机器自动编译、生成、或者能动态下载的实体文件，统统禁止入库。

### 1. 核心模块存放细则

| 模块区分 | 允许存放的文件类型 / 文件夹 | 工程化作用说明 |
| :--- | :--- | :--- |
| **项目根目录** | `.gitignore` | 整个仓库的门神，定义哪些本地垃圾文件和临时文件不准提交。 |
| | `README.md`、`CLAUDE.md`、`LICENSE` | 项目说明文档、AI 协作上下文说明、开源许可证，全部正常入库。 |
| | `package.json` & `package-lock.json` | 根目录级依赖描述，只放声明，不放实体。 |
| | `ManWeiDB` | 数据库结构脚本，只允许存放 DDL 脚本，不允许存放数据快照或备份文件。 |
| **后端工程**<br>`backend/ManWei.Api/` | `Common/` | 公共工具类、扩展方法、通用常量等横切关注点代码。 |
| | `Controllers/` | API 接口层，所有 HTTP 端点定义。 |
| | `Data/` | DbContext 及数据访问相关代码。 |
| | `DTOs/` | 数据传输对象，定义接口的请求体与响应体结构。 |
| | `Migrations/` | EF Core 数据库迁移脚本，记录数据库结构变更历史，**必须入库，禁止删除**。 |
| | `Models/` | 数据实体层，对应数据库表结构。 |
| | `Properties/` | 项目属性配置（`launchSettings.json`），注意不含真实外网 IP 或敏感端口信息。 |
| | `Services/` | 业务逻辑层，核心业务处理。 |
| | `tests/api-tests.http` | HTTP 接口测试脚本，可正常入库（不含真实 Token 或密码）。 |
| | `appsettings.json` | **脱敏的基础配置文件**。仅写 Log 级别、通用端口等，数据库密码和私钥必须写虚拟占位符（如 `Password=YOUR_PASSWORD`）。 |
| | `ManWei.Api.csproj`、`ManWei.sln`、`Program.cs` | 项目文件、解决方案文件、程序入口，正常入库。 |
| **前端 - 管理端**<br>`frontend/pc-admin/` | `src/` | 管理端 Vue 3 组件、路由、状态管理、业务脚本。 |
| | `public/` | 静态公共资源（favicon 等）。 |
| | `index.html`、`vite.config.ts`、`eslint.config.js` | 构建入口与工程配置文件。 |
| | `package.json` & `package-lock.json` | 管理端依赖描述，只放声明。 |
| | `tsconfig.json` 系列、`README.md` | TypeScript 配置与项目说明。 |
| **前端 - 用户端**<br>`frontend/pc-client/` | `src/` | 用户端 Vue 3 组件、路由、状态管理、业务脚本。 |
| | `public/` | 静态公共资源。 |
| | `index.html`、`vite.config.ts`、`eslint.config.js` | 构建入口与工程配置文件。 |
| | `package.json` & `package-lock.json` | 用户端依赖描述，只放声明。 |
| | `tsconfig.json` 系列、`README.md` | TypeScript 配置与项目说明。 |
| **小程序**<br>`miniprogram/` | `images/`、`pages/`、`utils/` | 小程序页面、工具函数、本地图片资源。 |
| | `app.js`、`app.json`、`app.wxss` | 小程序全局入口文件。 |
| | `package.json` & `package-lock.json` | 小程序依赖描述。 |
| | `project.config.json` | 小程序公共项目配置，正常入库。 |
| **图标与图片资源**<br>`icon/`、`images/` | 全部图标与静态图片 | 项目 UI 图标资源，正常入库（注意单文件不超过 10MB）。 |
| **项目文档**<br>`docs/` | 全部 `.md` 文档、`assets/`、`test-report/` | 接口设计、前端文档、技术债、协作记录等，全部正常入库。 |
| **工具脚本**<br>`tools/` | 工具脚本源文件 | 自动化工具脚本，正常入库（不含硬编码密钥）。 |
| **自动化**<br>`.github/` | `workflows/` | CI/CD 自动化部署脚本（GitHub Actions）。 |

### 2. 实际项目仓库结构（对照当前工程）

```text
AnimeEmotion/                              # 仓库根目录
├── .github/
│   └── workflows/                         # CI/CD 自动化部署脚本
├── .gitignore                             # Git 忽略规则
├── README.md                              # 项目说明文档
├── CLAUDE.md                              # AI 协作上下文说明
├── LICENSE                                # 开源许可证
├── ManWeiDB                               # 数据库结构脚本（仅 DDL）
├── package.json                           # 根目录依赖描述
├── package-lock.json
│
├── backend/
│   └── ManWei.Api/                        # 后端工程 (ASP.NET Core)
│       ├── Common/                        # 公共工具类、扩展方法
│       ├── Controllers/                   # API 接口层
│       ├── Data/                          # DbContext、数据访问层
│       ├── DTOs/                          # 数据传输对象
│       ├── Migrations/                    # EF Core 迁移脚本（必须入库）
│       ├── Models/                        # 数据实体层
│       ├── Properties/                    # 项目属性配置
│       ├── Services/                      # 业务逻辑层
│       ├── tests/
│       │   └── api-tests.http             # 接口测试脚本
│       ├── wwwroot/
│       │   └── uploads/
│       │       └── avatars/               # ⛔ 用户上传头像，禁止入库
│       ├── appsettings.json               # 脱敏基础配置（占位符）
│       ├── appsettings.Development.json   # ⛔ 含真实密码，禁止入库
│       ├── ManWei.Api.csproj
│       ├── ManWei.Api.csproj.user         # ⛔ IDE 私人缓存，禁止入库
│       ├── ManWei.sln
│       ├── Program.cs
│       ├── bin/                           # ⛔ 编译产物，禁止入库
│       └── obj/                           # ⛔ 编译缓存，禁止入库
│
├── docs/                                  # 项目文档
│   ├── assets/
│   ├── superpowers/
│   ├── test-report/
│   ├── 接口设计.md
│   ├── 外部文件索引.md
│   ├── COLLABORATION.md
│   ├── FE.md
│   ├── PC用户端设计文档.md
│   ├── recommendation-impl-progress.md
│   ├── recommendation.md
│   └── TECH_DEBT.md
│
├── frontend/
│   ├── pc-admin/                          # 管理端 (Vue 3 + Vite)
│   │   ├── src/                           # 业务源码
│   │   ├── public/                        # 静态资源
│   │   ├── index.html
│   │   ├── vite.config.ts
│   │   ├── eslint.config.js
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── README.md
│   │   ├── dist/                          # ⛔ 编译产物，禁止入库
│   │   ├── node_modules/                  # ⛔ 实体依赖，禁止入库
│   │   └── .vscode/                       # ⛔ IDE 私人缓存，禁止入库
│   │
│   └── pc-client/                         # 用户端 (Vue 3 + Vite)
│       ├── src/                           # 业务源码
│       ├── public/                        # 静态资源
│       ├── index.html
│       ├── vite.config.ts
│       ├── eslint.config.js
│       ├── package.json
│       ├── tsconfig.json
│       ├── README.md
│       ├── dist/                          # ⛔ 编译产物，禁止入库
│       └── node_modules/                  # ⛔ 实体依赖，禁止入库
│
├── icon/                                  # 项目图标资源
│   ├── admin-favicon-256.png
│   ├── admin-favicon-master.png
│   ├── AI助手.png / .svg
│   ├── user-favicon-256.png
│   └── user-favicon-master.png
│
├── images/                                # 项目图片资源
│
├── miniprogram/                           # 微信小程序
│   ├── images/
│   ├── miniprogram_npm/
│   ├── pages/
│   ├── utils/
│   ├── app.js / app.json / app.wxss
│   ├── package.json
│   ├── project.config.json                # 公共配置，正常入库
│   ├── project.private.config.json        # ⛔ 个人私有配置，禁止入库
│   └── node_modules/                      # ⛔ 实体依赖，禁止入库
│
├── generated/                             # 自动生成代码（视情况加入 .gitignore）
└── tools/                                 # 工具脚本
```

---

## 🛑 二、绝对禁止入库清单（"防污染"黑名单）

| 污染类型 | 具体的文件夹 / 文件名 | 为什么不能入库（危害说明） |
| :--- | :--- | :--- |
| 前端实体依赖 | `frontend/pc-admin/node_modules/`<br>`frontend/pc-client/node_modules/`<br>`miniprogram/node_modules/` | 体积极其庞大，包含数万个碎片文件，导致 push/pull 极慢。拉下代码后在本地执行 `npm install` 动态生成即可。 |
| 后端编译产物 | `backend/ManWei.Api/bin/`<br>`backend/ManWei.Api/obj/` | .NET 编译时生成的二进制文件（`.dll`, `.exe`）和临时缓存，带有强烈的操作系统和本地平台属性，推上去会导致 Linux 服务器跨平台编译冲突。 |
| IDE 私人缓存 | `backend/.vs/`<br>`frontend/pc-admin/.vscode/`<br>`*.user`（如 `ManWei.Api.csproj.user`） | 本地开发工具记录的个人窗口排版、断点信息、历史文件缓存，无代码同步价值，且可能包含本地绝对路径等隐私信息。 |
| 前端编译产物 | `frontend/pc-admin/dist/`<br>`frontend/pc-client/dist/` | `npm run build` 打包出来的纯静态 HTML/JS/CSS，属于生产部署的"死代码"，Git 不需要追踪其变化历史。 |
| 绝对机密隐私 | `backend/ManWei.Api/appsettings.Development.json`<br>`.env`、`*.local`、任何含真实密码的文件 | 包含云服务器的真实 IP、数据库连接密码、第三方 API 私钥（Token）。一旦泄露，自动化爬虫会在秒级内扫描并攻击服务器。 |
| 用户上传文件 | `backend/ManWei.Api/wwwroot/uploads/avatars/` | 用户动态上传的头像图片，属于"运行时动态数据"，体积不可控，绝不属于"源代码"。 |
| 小程序私有配置 | `miniprogram/project.private.config.json` | 包含个人微信开发者 ID 等本地私有配置，不应推送至远端。 |
| 动态运行数据 | `*.log`、`TestResults/`、`coverage/` | 程序运行期间动态生成的日志、测试报告，属于运行时产物，不属于源代码。 |

### 🛡️ 适配当前项目的 `.gitignore` 模板

在项目根目录的 `.gitignore` 中确保包含以下所有规则：

```plaintext
# ==========================================
# 1. 针对 ASP.NET Core (C#) 后端
# ==========================================
backend/ManWei.Api/bin/
backend/ManWei.Api/obj/
backend/.vs/
*.user
*.suo
*.sln.docuser
*.pdb
TestResults/
coverage/

# 排除包含真实密码的本地配置文件
backend/ManWei.Api/appsettings.Development.json
backend/ManWei.Api/appsettings.Local.json
backend/ManWei.Api/appsettings.Production.json

# 用户上传的动态文件
backend/ManWei.Api/wwwroot/uploads/

# ==========================================
# 2. 针对 Vue 3 前端（管理端 + 用户端）
# ==========================================
frontend/pc-admin/node_modules/
frontend/pc-admin/dist/
frontend/pc-admin/.vscode/

frontend/pc-client/node_modules/
frontend/pc-client/dist/

# Vite 环境变量文件（容易塞入真实 API Key，务必忽略）
.env
.env.local
.env.*.local
*.local

# 日志文件
*.log
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# ==========================================
# 3. 针对微信小程序
# ==========================================
miniprogram/node_modules/
miniprogram/miniprogram_npm/
miniprogram/project.private.config.json

# ==========================================
# 4. 根目录 node_modules（tools 等工具脚本依赖）
# ==========================================
node_modules/

# ==========================================
# 5. IDE 缓存（全局兜底）
# ==========================================
.vs/
.vscode/
.idea/

# ==========================================
# 6. 操作系统缓存
# ==========================================
.DS_Store
Thumbs.db
```

---

## ⚡ 三、线上环境防删库与误删除防御规约

任何依赖"靠人肉集中注意力"来防止删库的方案，都是在给未来的重大事故埋地雷。必须从**代码、仓库、运维、备份**四个维度建立物理防火墙。

### 1. 代码开发层：全线推行"软删除（Soft Delete）"

【铁律】数据库中涉及核心业务数据（动漫数据、用户收藏、用户记录等）的表，一律禁止执行 SQL 的 `DELETE` 物理删除命令。

【规范】所有核心实体表必须在基类中包含以下两个字段：

- `IsDeleted (bool/bit)`：是否已被删除，默认为 `false`。
- `DeleteTime (DateTime?)`：删除操作发生的时间，默认为 `null`。

【执行】任何前端发起的"删除"请求，后端 API 内部逻辑一律改为 `Update` 操作，将 `IsDeleted` 置为 `true`。

【EF Core 全局过滤器机制】在 `Data/` 目录下的 `DbContext` 的 `OnModelCreating` 中配置全局查询过滤器，正常查询会自动过滤掉已删除的数据，发生误操作时可以直接在数据库将 `IsDeleted` 改回 `false` 实现一键恢复：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 全局拦截已软删除的数据，任何 LINQ 查询都会自动附带 !IsDeleted 条件
    modelBuilder.Entity<WwUserData>().HasQueryFilter(u => !u.IsDeleted);
    // 其他核心表依此类推...
}
```

> ⚠️ **唯一索引冲突陷阱**：
> `HasQueryFilter` 只对 LINQ 查询生效，对 `FromSqlRaw` / 存储过程等原生 SQL **不生效**，编写这类查询时需手动附加 `WHERE IsDeleted = 0`。
> 此外，若表中存在唯一约束（如邮箱、用户名），软删除后该字段依然被"占用"，会导致相同值无法重新写入。建议二选一：
> - 将唯一索引改为**过滤索引**（Filtered Index），仅对 `IsDeleted = 0` 的行生效；
> - 或在唯一约束的组合键中把 `IsDeleted` 一并纳入。

【前端双保险验证】前端任何删除按钮，必须弹出二次确认弹窗（如 Element Plus 的 `ElMessageBox.confirm`），且确认按钮必须做至少 1 秒的防连击限制（Debounce）。高危删除操作必须要求手动输入验证文本（如"确认删除"）。

### 2. 代码仓库层：敏感配置物理隔离与分支保护

【规范】仓库里的 `appsettings.json` 只写占位符。真实的生产环境数据库密码，在服务器启动后通过 Docker 环境变量动态注入，或存放在服务器本地不进 Git 的 `appsettings.Production.json` 中。

【分支保护】锁定 GitHub 的主分支（`main`），禁止直接 `git push` 到主分支。所有功能开发在 feature 分支完成后，通过 Pull Request 合并，合并前务必自我 Review，防止把带有机密信息或错误物理删除逻辑的代码推上线。

### 3. 生产运维层：Linux 终端与 Docker 容器物理隔离

【铁律】严禁在 Linux 远端终端执行原生的 `rm -rf` 命令。

【执行】在 Ubuntu 服务器上安装命令行回收站工具：

```bash
apt-get install trash-cli
```

以后在终端删除文件时，一律使用 `trash-put 文件或目录名`，它会把文件移入 Linux 回收站。如果发现误删，通过 `trash-restore` 命令救回。

> ⚠️ **回收站不等于免删除**：
> 移入回收站的文件依然占用磁盘空间，长期不清理会撑爆服务器磁盘。必须配合定时清理任务，通过 `crontab` 每天执行：
> ```bash
> trash-empty 30   # 清空 30 天前的回收站内容
> ```

【Docker 数据生命周期隔离】数据库容器（如 SQL Server）运行时，绝对不能将数据保存在容器内部。必须使用 `-v` 参数进行持久化具名挂载：

```bash
# 将 SQL Server 数据安全挂载到名为 manwei_db_data 的独立数据卷中
docker run -d --name sqlserver -v manwei_db_data:/var/opt/mssql ...
```

这样，就算容器被误删（`docker rm -f`）了，只要底层的 `manwei_db_data` 数据卷还在，重新拉一个新容器挂上去，数据立刻恢复。

### 4. 灾备层：数据卷 ≠ 备份，必须异地/离线留存

【关键提醒】Docker 具名卷只能防"容器被删"，挡不住以下几类灾难：

- 误执行 `docker volume rm` 或 `docker system prune -a --volumes`；
- 宿主机磁盘损坏、服务器被入侵、机房级故障；
- 云服务商账号被盗、实例被整体删除。

这些场景下，**卷和容器会一起消失**，因此必须建立独立于当前主机的备份链路：

- 通过定时任务（`cron` + SQL Server 的 `BACKUP DATABASE`）每日生成数据库快照；
- 快照文件加密后上传至**异地对象存储**（如阿里云 OSS、腾讯云 COS），与生产服务器物理隔离；
- 至少保留 **7~30 天**的滚动备份，并定期（如每月）做一次真实的**恢复演练**，确认备份文件真的可用，而不是只确认"备份任务成功跑了"。

---

## 🛠️ 四、Git 仓库清理与历史敏感信息修正

### 1. 日常清理：清除已入库的垃圾文件

如果仓库里已经混入了 `obj/`、`bin/` 或 `node_modules/` 等垃圾文件，在本地项目目录下依次执行以下三步：

```bash
# 1. 强行清除本地 Git 的缓存索引（不会删掉本地的任何物理源码）
git rm -r --cached .

# 2. 重新把所有文件加回来（Git 会自动读取最新的 .gitignore 并过滤掉垃圾文件）
git add .

# 3. 提交并推送到 GitHub 远端
git commit -m "chore: 依据工程标准清理仓库垃圾文件"
git push origin main
```

> ⚠️ **重要提醒**：上述三步只能让文件"以后不再被追踪"，**无法清除 Git 历史记录中已经提交过的内容**。如果曾经的某次提交中包含过真实密码或 `appsettings.Development.json`，任何人执行 `git log` 翻看历史提交依然能挖出来。

### 2. 历史敏感信息彻底清除（一旦真实泄露过，必须执行）

若确认历史提交中曾经出现过真实密码 / API Key / Token，必须做两件事，**缺一不可**：

**第一步：清除 Git 历史中的敏感文件**

推荐使用 [`git filter-repo`](https://github.com/newren/git-filter-repo) 或 [BFG Repo-Cleaner](https://rtyley.github.io/bfg-repo-cleaner/)：

```bash
# 示例：从整个历史中彻底删除曾经提交过的敏感配置文件
bfg --delete-files appsettings.Development.json

# 清理引用并强制垃圾回收
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# 强制推送覆盖远端历史
git push origin --force --all
```

**第二步：轮换（作废并重新生成）已经泄露过的凭证**

> 🚨 仅清除代码历史是不够的——泄露过的密码/密钥即使从 Git 历史中删掉，也可能已经被爬虫或第三方缓存记录。必须**立即修改数据库密码、吊销并重新生成 API Key/Token**，让旧凭证彻底失效，这一步比清理 Git 历史本身更紧急、更重要。

---

## ✅ 五、执行清单速查（Checklist）

| 类别 | 检查项 |
| :--- | :--- |
| 仓库结构 | `.gitignore` 是否已覆盖 `node_modules/`（三处）、`bin/`、`obj/`、`.env`、`appsettings.Development.json`、`project.private.config.json`、`wwwroot/uploads/` |
| 配置安全 | `appsettings.json` 中是否只有占位符，真实密码是否通过环境变量注入 |
| 迁移脚本 | `Migrations/` 目录是否正常追踪，未被误加入 `.gitignore` |
| 分支保护 | 主分支 `main` 是否已锁定，是否强制通过 PR 合并 |
| 软删除 | 核心表是否具备 `IsDeleted` / `DeleteTime` 字段，唯一索引是否已处理软删除冲突 |
| 前端删除交互 | 是否有二次确认弹窗、防连击限制、高危操作二次输入验证 |
| 运维防误删 | 服务器是否已安装 `trash-cli` 并配置定期清空任务 |
| 数据持久化 | 数据库容器是否使用具名卷 `-v manwei_db_data` 挂载 |
| 异地备份 | 是否有定时数据库快照 + 异地对象存储备份 + 定期恢复演练 |
| 历史泄露排查 | 若曾经泄露真实密码，是否已完成历史清理 **且** 完成凭证轮换 |

---

*本文档为漫味 ManWei 项目个人开发标准，随 GitHub 仓库一并维护，作为日常开发、自我 Code Review 与线上运维的统一对账依据。*
