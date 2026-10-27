# U-PMS: notes for Claude Code

U-PMS (UBL Project Management System) is a Jira-like project management app for one organization. Phase 1 (the
core Kanban project) and Phase 2 (project views and team) are built and validated. **Phase 3 (templates and
planning) and Phase 4 (collaboration and search) are built** on the branch `phase-3`. Their browser tests, full
performance runs and manual checks are still to do (`specs/004-templates-and-planning/validation.md`, and T097–T104 in
`specs/005-collaboration-and-search/tasks.md`). Before Phase 5 (portfolio and dashboards) comes a Jira parity pass
(`specs/006-jira-parity/`: spaces, a Create form, columns on the board, a template gallery, Jira's layout).
The Jira parity pass is built on the same branch (all five stories); its full browser and performance runs, quickstart
and manual screen reader check are still to do (T088–T098 in `specs/006-jira-parity/tasks.md`). README.md has the
roadmap and status.

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
  `specs/002-kanban-project-core/`; Phase 2 is `specs/003-project-views-and-team/`; Phase 3 is
  `specs/004-templates-and-planning/`; Phase 4 is `specs/005-collaboration-and-search/`; the Jira parity pass is
  `specs/006-jira-parity/`.
- **Target teams**: software development, project management (PMO) and technical teams such as Data & AI. Match
  Jira's behavior where Jira has an equivalent; Jira's business templates (HR, Marketing and the like) are not
  in scope.
- **Scope decisions of 2026-09-28** (recorded in the Phase 3 spec's Clarifications):
  - Phase 3 covers the Scrum and Stage-gate templates, the new work item types, milestones and dependencies;
  - a stage gate is approved by named approvers, and the next stage cannot start until it is;
  - dependencies link work items within one project only.
- **Scope decision of 2026-10-01** (recorded in the Phase 4 spec's Clarifications): Phase 4 is collaboration
  and search (formatted text, mentions, watchers, notifications, email, attachments, labels, search and saved
  filters); portfolios, portfolio rollups, and one dashboard per project and per portfolio with health updates
  move to Phase 5.

  `specs/001-issue-tracker-mvp/` is the product vision and requirement backlog to draw from. Ask the owner
  before settling scope questions.

## Stack and layout

.NET 10 (the SDK is pinned in `global.json`), C# 14, Blazor Web App (Interactive Server), EF Core 10 on SQL
Server 2022, ASP.NET Core Identity. One modular monolith with four modules: Identity, Projects, Work and (since Phase 4)
Notifications. Phase 4 adds a TipTap editor bundle (`src/Upms.Web/Editor`, built with Node.js 24 into the committed
`wwwroot/js/editor.js`), ClamAV for the malware check (`Malware:ClamAv`) and MailKit for email (`Email:Smtp`).

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
dotnet test --project tests/Upms.Application.Tests -- --filter-not-trait "Category=ExternalServices"   # without ClamAV and smtp4dev

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

## Lessons from Phases 1 to 4 and the Jira parity pass

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
  - All browser tests share one app and database: give each test its own users and project keys.
- **Append-only tables** (history, audit, sprint scope events, gate decisions) are created with
  `AppendOnlyTable.CreateTrigger` in their migration and `HasTrigger` in their configuration. The Application tests'
  `SqlServerFixture` truncates them before Respawn, so a new one must be added to its list.
- **Stage lock.** Every path that changes a status calls `StageLocks` (board moves, the drawer, "mark done", column
  deletion). A new path must too. Since the Jira parity pass that includes the Create form's status, lane moves and
  sub-task cards.
- **"Space" on screens, "project" in code.** Screen text, messages, emails and addresses say "space"; code and tables
  keep "project" (research R1). `tests/Upms.Architecture.Tests/WordingTests.cs` and the browser tests'
  `AssertSaysSpaceNotProjectAsync` (run with every axe check) fail on a new "project" on screen; even an element id
  like `project-name` counts. Old addresses (`/projects/…`, `/my-tasks`) go through `LegacyAddress.razor`.
- **One set of rules per field.** The assignee and epic or stage rules live in `ItemEdits` and are used by the drawer
  and the board's lane moves; a new path that changes them uses it too. Work out a card's rank before changing
  anything else: `RankRebalancer` saves through the same context.
- **EF projections.** A later `Where` or `Select` can see through anonymous types and member initializers
  (`new Row { … }`), not through a record's constructor; project into one of the first two when the query goes on.
- **The permission matrix** reads `specs/006-jira-parity/contracts/permissions.md`; its run takes about 35 minutes and
  its rows cannot be run alone. Give each new row operations that work on the matrix's sample spaces (for example,
  turn off a work type nobody uses there).
- **Formatting on Windows.** The checkout uses `core.autocrlf=true`, so `dotnet format --verify-no-changes` can
  report line endings. Run it on a copy with LF line endings to see real findings.
- **"Open" and "today".** An open work item has `ResolvedAt IS NULL`. "Today" is the viewer's (`ViewerToday`);
  the organization's time zone is UTC unless changed.
- **Performance (SC-002).** Every action's p95 must stay within 1 second at 300 users and about 500,000 work
  items. Phase 2's highest was 211 ms.
- **Background work** (attachment checks and email) claims rows with `UPDATE TOP (n) … WITH (READPAST,
  ROWLOCK) OUTPUT inserted.Id` and a lease, so several app instances never take the same row. Follow
  `AttachmentScanner` and `EmailDispatcher`.
- **Test machines.** Every database suite starts its own SQL Server container. On a 4 GB Docker engine, run one suite
  at a time and stop the dev container (`docker compose stop sqlserver`) first. While a suite runs, its DLLs are
  locked: compile the same test project elsewhere with `dotnet build … -o <folder>` to check it.
