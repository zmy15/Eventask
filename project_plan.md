## 日程管理 Eventask

### 核心实体

- 用户
- 日历
	- 日历成员与管理权限
	- 包含的任务和日程项
- 日程项
	- 重复规则
	- 日期，地点，描述，附件
	- 提醒方式
- 任务
	- 重复规则
	- 日期，是否完成，描述，附件
- 附件
	- 上传到对象存储

### 后端

- 账号登录与注册
- 跨设备日程同步
- 存储日程的附件
- 在线查看日程
- 自动生成数据库备份
- 实现与他人共享日历

### 客户端

- 添加/删除日程或任务
- 使用AI自动识别和导入日程或任务（自然语言识别，图像OCR识别）
- 导入南邮课程表/考试安排
- 通用格式的数据导入导出
- 支持上传和下载附件
- 提醒待完成的日程
- 在线同步+离线使用，冲突合并策略，版本控制
- 查看和编辑其它人的共享日历
- 日/周/月视图


---

## 1. 目标与范围收敛

### MVP（必须交付）

- 账号：注册/登录/退出（JWT）
    
- 日历：默认主日历 + 多日历（可选）CRUD
    
- 日程项：创建/编辑/删除（支持事件与任务）
    
- 提醒：最小实现（应用内提醒，或本地通知的最小子集）
    
- 附件：上传/下载（对象存储）
    
- 同步：**delta 同步 + 版本冲突控制（409）**
    

### 加分项

- 共享日历（只读 Viewer + 可编辑 Editor）
    
- 导出：JSON/ICS，导入：JSON
    
- 冲突合并 UI：覆盖/放弃/另存副本
    
- 课程表/考试导入
    

---

## 2. 领域建模（建议统一 Event 与 Task）

为提升开发效率、减少重复逻辑，建议将 `Event` 与 `Task` 统一成一个实体：

### 核心实体（建议）

- **User**
    
- **Calendar**
    
- **CalendarMember**（共享权限）
    
- **ScheduleItem**（统一承载事件/任务）
    
- **RecurrenceRule**（可选：先做子集）
    
- **Reminder**（最小实现）
    
- **Attachment**
    
- **Tombstone / SoftDelete**（用于同步删除）
    

### 关键设计点

- `ScheduleItem.Type`: `Event | Task`
    
- `Event`：有 `StartAt/EndAt`，通常 `IsCompleted` 不使用
    
- `Task`：可只有 `DueAt`（或 `StartAt = DueAt`），`IsCompleted` 生效
    
- **同步字段：每个可同步实体都要有**
    
    - `Version`（int，服务端递增）
        
    - `UpdatedAt`（UTC 时间）
        
    - `IsDeleted` + `DeletedAt`（或 Tombstone 表）
        

---

## 3. EF Core 数据模型（字段级建议）

### User（可用 ASP.NET Core Identity）

- `Id`（Guid/string）
    
- `UserName/Email/PasswordHash/...`（Identity 管理）
    

### Calendar

- `Id` (Guid)
    
- `OwnerId`
    
- `Name`
    
- `Color`（可选）
    
- `Version`, `UpdatedAt`
    
- `IsDeleted`, `DeletedAt`
    

### CalendarMember

- `CalendarId`
    
- `UserId`
    
- `Role`：`Owner | Editor | Viewer`
    
- `Version`, `UpdatedAt`
    
- 唯一索引：`(CalendarId, UserId)`
    

### ScheduleItem

- `Id` (Guid)
    
- `CalendarId`
    
- `Type`（enum）
    
- `Title`
    
- `Description`
    
- `Location`
    
- `StartAt`（nullable）
    
- `EndAt`（nullable）
    
- `DueAt`（nullable）
    
- `AllDay`（bool）
    
- `IsCompleted`（bool，task 用）
    
- `CompletedAt`（nullable）
    
- `TimeZoneId`（可选：先固定用用户本地/UTC 简化）
    
- `Version`, `UpdatedAt`
    
- `IsDeleted`, `DeletedAt`
    

### RecurrenceRule

- `Id`

- ScheduleItemId
    
- `Freq`：Daily/Weekly/Monthly
    
- `Interval`（默认 1）
    
- `ByDay`（如 "MO,TU"；或单独表）
    
- `Until` / `Count`（二选一）
    
- `Version`, `UpdatedAt`

- 单次特例修改规则（用于实现调课等场景）
    

### Reminder

- `Id`
    
- `ScheduleItemId`
    
- `OffsetMinutes`（例如提前 10 分钟）
    
- `Channel`：InApp（先做这个），Email/Push 留扩展
    
- `Version`, `UpdatedAt`
    

### Attachment

- `Id`
    
- `ScheduleItemId`
    
- `FileName`
    
- `ContentType`
    
- `Size`
    
- `ObjectKey`（对象存储 key）
    
- `Sha256`（可选）
    
- `Version`, `UpdatedAt`
    
- 权限：通过 `ScheduleItem -> Calendar -> Member` 继承校验
    

---

## 4. 后端架构与接口（ASP.NET Core）

### 分层

- `Domain`：实体、枚举、领域规则。与客户端共用该部分代码，不应包含任何领域无关内容
    
- `ApiService`：Minimal APIs
    

### 认证

- ASP.NET Core Identity + JWT
    
- Desktop 登录后保存 refresh token（或简单短期 token + 重新登录，视时间）
    

### API 清单（MVP）

- Auth
    
    - `POST /auth/register`
        
    - `POST /auth/login` -> JWT
        
- Calendars
    
    - `GET /calendars`
        
    - `POST /calendars`
        
    - `PUT /calendars/{id}`
        
    - `DELETE /calendars/{id}`
        
- ScheduleItems
    
    - `GET /calendars/{id}/items?from=&to=`（用于列表/视图）
        
    - `POST /calendars/{id}/items`
        
    - `PUT /items/{id}`（带 expectedVersion）
        
    - `DELETE /items/{id}`（软删/墓碑）
        
- Attachments
    
    - `POST /items/{id}/attachments`（上传）
        
    - `GET /attachments/{id}/download`
        
- Sync（关键）
    
    - `POST /sync/pull`：客户端给 `lastSyncAt`（或 syncToken），服务端返回“变更集合 + 服务器时间戳”
        
    - `POST /sync/push`：客户端上传本地变更（新增/更新/删除），服务端逐条应用，冲突返回 409 + 最新实体
        

> 如果你想更简单：也可以只做 `push` 时逐条调用 item API，但**效率和实现复杂度并不会更低**；一个聚合的 `/sync/*` 更适合实验周展示“跨设备同步”。

---

## 5. 同步与冲突控制

### 乐观并发（核心机制）

- 客户端更新请求携带 `ExpectedVersion`
    
- 服务端检查：
    
    - `ExpectedVersion == CurrentVersion`：更新成功，`Version++`
        
    - 否则：`409 Conflict` + 返回服务器最新版本（含 `Version/UpdatedAt`）
        

### Delta 同步

- 客户端维护 `lastSyncAt`
    
- Pull：服务端返回 `UpdatedAt > lastSyncAt` 的所有变更（含已删除项的 tombstone/软删记录）
    
- Pull 响应同时返回 `serverNow`，客户端将其作为新的 `lastSyncAt`
    

### 删除同步

- 不建议物理删除；用 `IsDeleted/DeletedAt` 或 Tombstone 表
    
- Pull 时必须包含删除信息，否则另一端无法删除
    

### 冲突 UI（客户端最小策略）

- 收到 409：
    
    - 方案 A（最小）：提示三选一
        
        1. 用服务器版本覆盖本地
            
        2. 用本地版本强制覆盖（再发一次，ExpectedVersion=服务器当前版本）
            
        3. 另存为副本（新建一个 item）
            
- 方案 B（加分）：做字段级对比（标题/描述/时间等）给用户勾选合并
    

---

## 6. Avalonia 客户端与离线策略

### 本地存储

- SQLite + EF Core（与你后端同技术栈，开发效率最高）
    
- 本地表结构可与服务端 DTO/实体近似，但建议分开：
    
    - `LocalScheduleItem` 增加 `SyncState`：`Clean | Created | Updated | Deleted | Conflict`
        
    - 存储 `ServerId`（或直接用 Guid 统一生成，减少映射）
        

### 同步流程

- 启动或手动点击“同步”
    
    1. Push：提交本地 `Created/Updated/Deleted`
        
    2. Pull：拉取服务器自 `lastSyncAt` 的变更并合并到本地
        
    3. 清理：将成功提交的变更标为 `Clean`，更新 `lastSyncAt`
        

### 提醒

- 最后再考虑做
- 应用内提醒服务
    - 每分钟扫描未来 N 分钟内需要提醒的项
    - 弹窗/通知栏（若要系统通知，再逐平台做适配）
        
- 扩展：Windows Toast / macOS 通知 / Android 通知可列为后续计划
    

---

## 7. 附件与对象存储

### 选型

- Cloudflare R2等云平台的对象存储
    
### 最小实现方式

- `POST /items/{id}/attachments`：服务端接收 multipart，写入对象存储，保存元数据到 Attachment 表
    
- 下载：鉴权后由服务端返回预签名 URL
    

---

## 8. 共享日历


- 最后再考虑做，非必须

- `CalendarMember.Role` 三档：Owner/Editor/Viewer
    
- MVP 共享流程：
    
    - Owner 在客户端输入对方用户名/邮箱 -> 调用 `POST /calendars/{id}/members`
        
    - Viewer 只能读，Editor 可增删改 item，Owner 管理成员
        

---

## 9. 推荐的工程组织方式

- 共享项目：`Eventask.Domains`（领域模型，DTO、枚举、错误码），服务端与客户端共用，减少反复定义
    
- 客户端使用 HttpClient + Refit 快速生成 API 调用
    
- 服务端用最少的基础设施：
    
    - Identity + JWT
    - EF Core migrations
    - 最小化中间件（日志、异常处理、认证鉴权）


将功能分为 **P0 (必须交付)** 和 **P1 (加分项)**。

#### P0：必须交付（演示时的核心路径）

- **账号体系**：最简单的 JWT 登录/注册。
    
- **基础 CRUD**：创建、修改、删除日程/任务。
    
- **数据同步**：这是技术亮点，必须由你之前的 Networking/System 兴趣支撑。但不要做实时协作，做**离线优先的 Delta 同步**。
    
- **日历视图**：只做一个视图（推荐**月视图**或**列表视图**），周视图/日视图的 UI 布局算法很繁琐，容易卡壳。
    
- **南邮课表导入**：这是针对学校场景的“杀手级”功能，演示时效果最好，且逻辑相对独立，建议保留。
    

#### P1：加分项（有时间再做）

- **AI 识别**：听起来很酷，但调试耗时。建议只做一个简单的**正则表达式解析**（如识别“明天下午3点开会”），如果时间不够直接砍掉。
    
- **附件对象存储**：对于演示，存本地文件系统或 Base64 存库（虽然不规范但开发快）即可。如果必须上云，用 Cloudflare R2 或 MinIO。
    
- **共享日历**：涉及复杂的权限 ACL 控制，建议简化为“只读分享”或砍掉。

## 备注

### 课表导入

可以考虑使用油猴脚本等方式，让用户在浏览器主动打开教务系统的课程表，再通过脚本读取并生成能识别的JSON格式。