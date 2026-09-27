# U-PMS: notes for Claude Code

U-PMS (UBL Project Management System) is a Jira-like project management app for one organization. Phase 1 (the
core Kanban project) and Phase 2 (project views and team) are built and validated. **Phase 3 (enterprise and
portfolio) is next and has not been specified yet.** README.md has the roadmap and status.

## How work is done here

- **Spec-driven, with Spec Kit.** Every phase is specified, clarified, planned and broken into test-first tasks
  before any code is written. The commands are skills in `.claude/skills/`:
  - `/speckit-specify`, `/speckit-clarify`, `/speckit-plan` and `/speckit-tasks` produce a phase's documents;
  - `/speckit-analyze` checks them against each other;
  - `/speckit-implement` builds the phase from its tasks.
- **The constitution is binding** (`.specify/memory/constitution.md`). Its principles:
  - I spec-driven, incremental delivery;
  - II test-first quality (non-negotiable);
  - III secure by default (OWASP ASVS Level 2);
  - IV auditable, consistent data;
  - V simplicity: one modular monolith;
  - VI accessible, responsive experience (WCAG 2.2 AA, keyboard only, screen readers).
- **Each phase has a folder `specs/NNN-name/`.** It holds the spec, plan, research, data model, contracts,
  quickstart, tasks (with implementation notes at the end) and a validation record. Phase 1 is
  `specs/002-kanban-project-core/`; Phase 2 is `specs/003-project-views-and-team/`.
- **Phase 3 scope, as agreed so far:**
  - portfolio rollups;
  - cross-project dashboards;
  - Scrum and PMO stage-gate templates;
  - dependencies, milestones and other work item types.

  `specs/001-issue-tracker-mvp/` is the product vision and requirement backlog to draw from. Ask the owner
  before settling scope questions.

## Stack and layout

.NET 10 (the SDK is pinned in `global.json`), C# 14, Blazor Web App (Interactive Server), EF Core 10 on SQL
Server 2022, ASP.NET Core Identity. One modular monolith with three modules: Identity, Projects and Work.

- `src/Upms.Domain`: entities and rules, with no EF Core or ASP.NET types.
- `src/Upms.Application`: services per module. Modules call each other only through `*.Contracts` interfaces,
  and `tests/Upms.Architecture.Tests` enforces that.
- `src/Upms.Infrastructure`: EF Core (`Persistence/Configurations`, `Persistence/Migrations`) and Identity.
- `src/Upms.Web`: Blazor components (`Components/Pages/...`), host security and small scripts in `wwwroot/js`.
- `tools/Upms.Seed`: bulk data for the performance suite.

## Commands (PowerShell, from the repository root)

```powershell
docker compose up -d sqlserver          # needs a .env file with MSSQL_SA_PASSWORD (never committed)
dotnet run --project src/Upms.Web       # https://localhost:5001; migrates the database in Development

dotnet test --project tests/Upms.Domain.Tests
dotnet test --project tests/Upms.Application.Tests      # Testcontainers: Docker must be running
dotnet test --project tests/Upms.Web.Tests              # bUnit components and host security
dotnet test --project tests/Upms.Architecture.Tests
dotnet test --project tests/Upms.E2E.Tests              # Playwright + axe; install Chromium once (README)
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"

dotnet format --verify-no-changes
dotnet build -c Release                 # warnings are errors
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet ef migrations add <Name> --project src/Upms.Infrastructure --startup-project src/Upms.Web --output-dir Persistence/Migrations
```

Before each commit, run the three gates and every test suite. The performance suite
(`tests/Upms.Performance.Tests`) is opt-in and slow; run it when queries or indexes change.

## Conventions

- Services return `Result` / `Result<T>`. A failure carries an `AppError`, made with `AppError.NotFound`,
  `Forbidden`, `Validation`, `Rule` or `Conflict`. Domain rules return a `DomainError`, and `ToAppError()` keeps
  its code and field.
- Every project-scoped service method calls `IProjectAccess.RequireAsync` before any other work. People outside a
  project get `NotFound`, exactly as for a project that does not exist.
- New actions need rows in the phase's `contracts/permissions.md`. `PermissionMatrixTests` reads that table and
  fails for rows without operations.
- Concurrency: work items carry a row version and teams a `MembersVersion`, and a conflict returns the current
  state to show. History (`WorkItemChanges`) and `AuditEvents` are append-only.
- Web services are registered per action (`AddOperationScoped`), not per circuit.
- Acceptance tests are named after their scenarios (`US1_AS3_…` in Phase 1, `P2_US1_AS3_…` in Phase 2) and are
  written before the code.
- Match the surrounding code and its comment density. Documentation and screen text use plain, short sentences.

## Lessons from Phases 1 and 2

- **Blazor Server round trips.** A new address (query string) reaches the page only after a round trip through
  the browser, and a save only after a round trip to the server. In pages:
  - build each change on the last one asked for, as `ProjectListView` (`_requested`) and `TimelinePage`
    (`_requestedView`) do;
  - ignore late answers for addresses that are no longer current;
  - run saves one at a time;
  - put focus back only if it was lost (`upms.focusByIdIfLost`).

  `tests/Upms.Web.Tests/SlowNavigationManager.cs` reproduces the delay in bUnit.
- **Browser tests.**
  - Wait for in-app address changes with `WaitForPathAsync`, which polls; `WaitForURLAsync` can miss a change.
  - Wait for a suggested value before typing over it.
  - The axe helper waits for animations to finish.
- **"Open" and "today".** An open work item has `ResolvedAt IS NULL`. "Today" is the viewer's (`ViewerToday`);
  the organization's time zone is UTC unless changed.
- **Performance (SC-002).** Every action's p95 must stay within 1 second at 300 users and about 500,000 work
  items. Phase 2's highest was 211 ms.
