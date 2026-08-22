# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"Khoi Pro" is a full-stack project/task management system: an ASP.NET Core 9 Web API backend backed by SQL Server (EF Core), paired with a React 18 + Tailwind CSS frontend. Core domain: Users (admin/manager/member roles), Projects, Tasks (assignable, taggable, with status/priority), Tags, Attachments, Notifications, and email logging — plus a dashboard and report endpoints (overdue tasks, project summaries, team performance).

## Solution Structure

The real solution is `KhoiProjectManagement.sln`, containing three projects:

- **KhoiProjectManagementApi/** — the ASP.NET Core Web API (net9.0). Layout: `Controllers/`, `Services/` (interface + implementation per domain area, e.g. `ITaskService`/`TaskService`), `Data/ProjectManagementContext.cs` (EF Core `DbContext`), `Middleware/ErrorHandlingMiddleware.cs`, `BackgroundServices/OverdueTaskCheckerService.cs` (hosted service), `Extensions/ServiceCollectionExtensions.cs` (DI/auth/CORS wiring), `DatabaseSeeder.cs`.
- **KhoiProjectManagement.Models/** — shared entity classes (`User`, `Project`, `ProjectTask`, `Tag`, `Attachment`, `Notification`, `EmailLog`, join entities `ProjectUser`/`ProjectTag`/`TaskTag`) and `DTOs/` used across the API. Referenced by the API project.
- **KhoiProjectManagementApp/** — despite having its own `.csproj`/`Program.cs`, that ASP.NET stub is an empty, unused "Hello World" scaffold. The actual frontend is the React app living in the same folder (`src/`, `package.json`, `public/`) — treat this directory as a Create React App project, not a .NET project. It has its own `.sln` as well, which can be ignored.

**KhoiManagementApp/** at the repo root is a separate, unreferenced default ASP.NET Core MVC template (default `HomeController`, scaffolded Views, bundled Bootstrap/jQuery). It is not part of `KhoiProjectManagement.sln` and nothing else depends on it — leftover scaffolding, not active code.

## Commands

Backend (run from `KhoiProjectManagementApi/`):
- `dotnet build` — build the API
- `dotnet run` — run the API; on startup it calls `Database.EnsureCreatedAsync()` then `DatabaseSeeder.SeedAsync()` to create and seed the DB (no EF migrations exist yet — despite `Microsoft.EntityFrameworkCore.Tools`/`Design` being referenced, the schema is *not* managed via `dotnet ef migrations`; adding real migrations would require switching `Program.cs` off `EnsureCreatedAsync`). Swagger UI is available at `/swagger` in both Development and Production (see `Program.cs`).
- Connection string, JWT secret, and SMTP credentials live in `KhoiProjectManagementApi/appsettings.json`, which is committed to git with real-looking credentials — treat as sensitive, do not add more secrets here, and flag this to the user rather than propagating the pattern.

Frontend (run from `KhoiProjectManagementApp/`):
- `npm install`
- `npm start` — dev server (README says port 3000, though `package.json`'s `proxy` points at `http://localhost:905` and `.env.example`'s `REACT_APP_API_URL` points at `https://localhost:7001/api` — these ports are inconsistent across config files; check `Cors:AllowedOrigins` in the API's `appsettings.json`/`ServiceCollectionExtensions.cs` for the actual allowed origins before assuming a port).
- `npm run build` — production build
- `npm test` — Jest via react-scripts

There is no test project in the solution (no `*.Tests.csproj`), and no lint config beyond the CRA-default `eslintConfig` in `package.json`.

## Architecture Notes

- **Auth**: JWT bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`), issued/validated per `Jwt:Issuer`/`Jwt:Audience`/`Jwt:SecretKey` in config; passwords hashed with BCrypt. Controllers are `[Authorize]` by default (see `TasksController`).
- **Layering**: Controllers are thin — they delegate to an `IXxxService`/`XxxService` pair registered in `ServiceCollectionExtensions.AddApplicationServices`, which talk directly to `ProjectManagementContext` (no repository layer, no MediatR/CQRS).
- **Mapping**: AutoMapper and FluentValidation are referenced dependencies for DTO mapping/validation — check individual services/controllers for actual usage rather than assuming a project-wide convention.
- **EF Core relationships**: composite keys for the `ProjectUser`/`ProjectTag`/`TaskTag` join entities are configured in `ProjectManagementContext.OnModelCreating`, along with `Restrict`/`SetNull` delete behaviors for `Project.Creator`, `ProjectTask.AssignedTo`, and `Attachment.UploadedByUser`. Seed data (users, tags) is also defined there via `HasData`, separate from the runtime `DatabaseSeeder.SeedAsync`.
- **Background work**: `OverdueTaskCheckerService` runs as a hosted `BackgroundService` for overdue-task detection/notifications.
- **Email**: `EmailService`/`IEmailService` send via MailKit/MimeKit using the `Email:*` SMTP settings in config; sends are recorded via the `EmailLog` entity.
- **CORS**: must allow the React dev origin explicitly — the allowed-origins list is duplicated (with different port lists) between `appsettings.json`'s `Cors:AllowedOrigins` and the hardcoded fallback array in `ServiceCollectionExtensions.AddApplicationServices`; update both if changing ports.
- **Frontend structure**: `src/components/` is organized by concern — `Auth/` (login, route guard), `Layout/` (header, nav, the main `ProjectManagementSystem` shell), `Pages/` (Dashboard, Projects, Tasks, Team, Reports), `Models/` (modal dialogs), `Common/` (badges, spinners, error display). `contexts/AuthContext.js` holds auth state; `services/ApiService.js` centralizes API calls and JWT header injection.
