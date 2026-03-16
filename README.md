# Eventask

Eventask is a cross-platform calendar and task manager built on .NET 10. It includes a rich client app (desktop/mobile), a REST API backend, and supporting services for data storage and synchronization. The application provides calendar views (year/month/day), task and event management, natural language input, and special-day markers (rest/work days).

Eventask 是一个基于 .NET 10 的跨平台日历与任务管理系统，包含桌面/移动端客户端、REST API 后端以及数据存储与同步服务。应用提供年/月/日视图、日程与任务管理、自然语言输入、以及休/班特殊日期标识。

## Solution overview

Key projects in the solution:

- `Eventask.App`: Avalonia-based client UI and view models.
- `Eventask.Desktop`: Desktop host for the client.
- `Eventask.Android`: Android host for the client.
- `Eventask.ApiService`: ASP.NET Core API service with PostgreSQL persistence.
- `Eventask.ApiService.MigrationWorker`: Worker that runs database migrations.
- `Eventask.Domain`: Domain entities, DTOs, and request models.
- `Eventask.App.Tests`, `Eventask.Domain.Tests`, `Eventask.ApiService.Tests`: Unit tests.

解决方案包含的主要项目：

- `Eventask.App`：基于 Avalonia 的客户端 UI 与视图模型。
- `Eventask.Desktop`：桌面端宿主。
- `Eventask.Android`：Android 端宿主。
- `Eventask.ApiService`：ASP.NET Core API 服务，使用 PostgreSQL 持久化。
- `Eventask.ApiService.MigrationWorker`：数据库迁移 Worker。
- `Eventask.Domain`：领域实体、DTO 与请求模型。
- `Eventask.App.Tests` / `Eventask.Domain.Tests` / `Eventask.ApiService.Tests`：测试项目。

## Core features

- Calendar views: year, month, and day.
- Tasks and events with reminders and attachments.
- Search across schedule items.
- Natural language input for drafts.
- Special rest/work day markers driven by the backend.
- Multi-client hosts (desktop and Android) sharing the same client logic.

- 年/月/日视图。
- 带提醒与附件的任务与日程。
- 跨日程/任务搜索。
- 自然语言草稿输入。
- 由后端驱动的休/班特殊日期标识。
- 桌面与移动端共用同一客户端逻辑。

## Architecture

- **Client** (`Eventask.App`): MVVM with CommunityToolkit, Avalonia UI, and Refit API client.
- **API** (`Eventask.ApiService`): Minimal APIs with EF Core and PostgreSQL.
- **Domain** (`Eventask.Domain`): Entities, DTOs, and request contracts shared between client and server.

- **客户端**（`Eventask.App`）：MVVM + CommunityToolkit + Avalonia UI + Refit API 客户端。
- **后端**（`Eventask.ApiService`）：Minimal APIs + EF Core + PostgreSQL。
- **领域层**（`Eventask.Domain`）：客户端/服务端共享实体与契约。

## Configuration

Client app configuration is in platform-specific `appsettings.json` files:

- `Eventask.Desktop/appsettings.json`
- `Eventask.Android/appsettings.json`

API configuration uses `Eventask.ApiService/appsettings.json` (and environment variables for secrets).

客户端配置位于各平台的 `appsettings.json`：

- `Eventask.Desktop/appsettings.json`
- `Eventask.Android/appsettings.json`

后端配置使用 `Eventask.ApiService/appsettings.json`（敏感信息建议用环境变量）。

## Database

The API service uses PostgreSQL. The schema is managed with EF Core migrations.

To create or update the database schema:

- From Visual Studio Package Manager Console:
  - `Add-Migration <Name> -Project Eventask.ApiService -StartupProject Eventask.ApiService`
  - `Update-Database -Project Eventask.ApiService -StartupProject Eventask.ApiService`

Special rest/work days are stored in the `SpecialDays` table (`Date`, `Type`).

后端使用 PostgreSQL，数据库结构由 EF Core 迁移管理。

创建或更新数据库：

- Visual Studio 的 Package Manager Console：
  - `Add-Migration <Name> -Project Eventask.ApiService -StartupProject Eventask.ApiService`
  - `Update-Database -Project Eventask.ApiService -StartupProject Eventask.ApiService`

休/班特殊日期存储在 `SpecialDays` 表（`Date`、`Type`）。

## Running

Typical workflow:

1. Start the database (PostgreSQL).
2. Run `Eventask.ApiService` (API backend).
3. Run one of the client hosts:
   - `Eventask.Desktop` (desktop)
   - `Eventask.Android` (mobile)

The client uses `AppOptions.ApiBackendUrl` in its `appsettings.json` to connect to the API.

常见运行流程：

1. 启动数据库（PostgreSQL）。
2. 运行 `Eventask.ApiService`（API 后端）。
3. 运行客户端宿主：
   - `Eventask.Desktop`（桌面端）
   - `Eventask.Android`（移动端）

客户端通过 `appsettings.json` 中的 `AppOptions.ApiBackendUrl` 连接后端。

## Tests

Test projects include:

- `Eventask.App.Tests`
- `Eventask.Domain.Tests`
- `Eventask.ApiService.Tests`

Run tests from Visual Studio Test Explorer or with `dotnet test`.

测试项目：

- `Eventask.App.Tests`
- `Eventask.Domain.Tests`
- `Eventask.ApiService.Tests`

可使用 Visual Studio Test Explorer 或 `dotnet test` 运行。
