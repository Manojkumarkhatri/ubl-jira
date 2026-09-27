# Implementation Plan: Core Kanban Project (Phase 1)

**Branch**: `claude/practical-lamport-l47ei8` (feature `002-kanban-project-core`) | **Date**: 2026-09-27 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-kanban-project-core/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Phase 1 delivers the core Kanban project: sign-in from Day 1 with minimal account management; project
creation (name, key, description); a board that starts with To Do, In Progress and Done, with inline
"What needs to be done?" creation and drag-and-drop (plus keyboard) card moves; a task details drawer
(title, description, priority, sub-tasks, comments, history); and owner-controlled column
customization (add, rename, reorder, work-in-progress limits, typed columns, safe deletion).

**Technical approach** (details in [research.md](./research.md)): a single ASP.NET Core **Blazor Web
App** on **.NET 10** with the **Interactive Server** render mode, calling application services
in-process; **SQL Server** through **EF Core 10**; **ASP.NET Core Identity** for accounts. Three
modules (Identity, Projects, Work) in a four-project modular monolith. Board columns are per-project
statuses with a category (to do, in progress, done); tasks and sub-tasks share one work-item table;
authorization goes through one policy point whose Phase 1 rule is the open workspace. Phase 1 adds no
search engine, email, malware scanner or rich-text library, and every Phase 2 and Phase 3 capability
is an additive change (research R26).

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS)

**Primary Dependencies**: ASP.NET Core 10 (Blazor Web App, Interactive Server), ASP.NET Core Identity
(EF Core stores), EF Core 10 (SQL Server provider), Microsoft Fluent UI Blazor components,
OpenTelemetry .NET

**Storage**: SQL Server 2022 or later (or Azure SQL Database); data protection keys in the database

**Testing**: xUnit v3, bUnit, Testcontainers (SQL Server), Respawn, Microsoft.Playwright with Deque
axe-core, ArchUnitNET, Microsoft.Extensions.TimeProvider.Testing; an opt-in performance suite

**Target Platform**: Linux container or Windows Server with IIS (WebSockets enabled), from one build;
current Edge, Chrome and Firefox on desktop; usable at 360 px width

**Project Type**: Web application (server-rendered Blazor, single deployable modular monolith)

**Performance Goals**: p95 ≤ 1 s for project list and board loads (up to 500 visible cards), inline
creation, card move, drawer open and saving an edit, at 500,000 work items and 300 concurrent users
(SC-002); every list is paged, 50 items at a time (constitution performance baseline)

**Constraints**: 99.5% monthly availability, RPO ≤ 1 h, RTO ≤ 4 h (SC-009, SC-010); WCAG 2.2 AA with
keyboard alternatives for every drag (FR-042); 30-minute idle timeout (FR-006); plain text only
(research R10); no OS-specific APIs (constitution, Portability)

**Scale/Scope**: 2,000 named users, 300 concurrent circuits, 500,000 work items, up to 10 columns per
board; about 10 screens ([contracts/ui-routes.md](./contracts/ui-routes.md))

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.1 (`.specify/memory/constitution.md`).

| # | Gate | Pre-research | Post-design evidence |
|---|------|:------------:|----------------------|
| I | Spec in `specs/002-…`, stories prioritized and independently testable, no technology in the spec; P1 alone is a usable increment | ✅ PASS | spec.md: 3 stories, checklist passes; US1 (project + board + inline tasks) is usable alone |
| II | Test-first domain rules and services; every acceptance scenario automated; real SQL Server in data tests | ✅ PASS | research R24: per-layer test projects, Testcontainers, scenario-named tests |
| III | Authenticated by default; server-side authorization on every read and write, based on role and project membership; Identity; safe rendering; CSRF; HTTPS; security audit log; dependency scanning | ⚠️ PASS with justification | R6–R10. Checks run on every call through `IProjectAccess` ([permissions.md](./contracts/permissions.md)); project membership arrives in Phase 2, so Phase 1 uses the open workspace; see **Complexity Tracking** |
| IV | Append-only history; soft delete; optimistic concurrency; UTC; reviewed migrations | ✅ PASS | R11, R16–R18; data-model.md: `WorkItemChanges` trigger, `rowversion`, `BoardVersion` |
| V | One app, one database, explicit module boundaries; extra dependencies justified; YAGNI | ✅ PASS | R4, R5, R26: three modules; no search engine, email, scanner or rich-text library; future columns not created early |
| VI | WCAG 2.2 AA; keyboard alternative for every drag; usable at 360 px; feedback without losing input | ✅ PASS | R15, R20, R22: "Move to", "Move left/right", drawer focus management, axe scans |
| — | Platform standards (.NET 10, Blazor Web App, SQL Server, Identity, xUnit/bUnit/Testcontainers/Playwright, performance baseline, observability, configuration, portability) | ✅ PASS | Technical Context; R23, R25 |
| — | Workflow gates (PR review, CI gates, Definition of Done, traceability) | ✅ PASS | quickstart.md step 6; tests named after scenarios |

**Result**: passes, with one justified deviation (principle III, open workspace), recorded below.
Re-checked after the design with the same outcome.

## Project Structure

### Documentation (this feature)

```text
specs/002-kanban-project-core/
├── spec.md              # Feature specification (/speckit-specify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── application-services.md
│   ├── permissions.md
│   ├── ui-routes.md
│   └── http-endpoints.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Upms.slnx
global.json                         # pins the .NET 10 SDK
Directory.Build.props               # nullable on, warnings as errors, analyzers, NuGetAudit
Directory.Packages.props            # central package versions
.editorconfig
.config/dotnet-tools.json           # dotnet-ef
docker-compose.yml                  # local SQL Server
Dockerfile                          # production image for Upms.Web
.github/workflows/ci.yml            # format, build, tests, audit, migrations check

src/
├── Upms.Domain/                    # entities and rules; no I/O
│   ├── Common/                     # Entity base, DomainError, Rank (fractional index)
│   ├── Identity/                   # User, OrganizationRole, AuditEvent, OrganizationSettings
│   ├── Projects/                   # Project (board rules), ProjectStatus, StatusCategory
│   └── Work/                       # WorkItem, WorkItemType, Priority, WorkItemChange, Comment
├── Upms.Application/               # use cases, permission checks, DTOs, module contracts
│   ├── Common/                     # Result/AppError, ICurrentUser, IAppDbContext
│   ├── Identity/                   # SetupService, AccountService, UserAdminService, Contracts/IAuditLog
│   ├── Projects/                   # ProjectService, BoardColumnService, KeySuggester,
│   │                               #   Contracts/IProjectAccess, Contracts/IProjectWorkflow
│   └── Work/                       # BoardService, WorkItemService, CommentService,
│                                   #   Contracts/IWorkItemStatusMover
├── Upms.Infrastructure/            # EF Core, Identity integration
│   ├── Persistence/                # AppDbContext, Configurations/{Identity,Projects,Work},
│   │                               #   Migrations/, WorkItemNumberAllocator
│   ├── Identity/                   # AuditingSignInManager, UsernamePasswordValidator,
│   │                               #   TemporaryPasswordGenerator, AuditLog
│   └── Workers/                    # RankRebalanceWorker
└── Upms.Web/                       # Blazor Web App host and composition root
    ├── Program.cs
    ├── Components/                 # App.razor, Routes.razor, Layout/,
    │                               #   Shared/ (PlainText, ConflictBanner, EmptyState, IdleSessionWarning)
    ├── Components/Account/         # Login, Logout, ChangePassword, Setup (static SSR)
    ├── Components/Pages/
    │   ├── Projects/               # ProjectList, CreateProjectDialog
    │   ├── Board/                  # BoardPage, BoardColumn, TaskCard, InlineCreate, MoveToMenu
    │   ├── Drawer/                 # TaskDrawer, SubtaskList, CommentThread, HistoryList
    │   ├── Settings/               # ProjectDetails, BoardColumnsEditor, DeletedTasks
    │   ├── Admin/                  # Users
    │   └── Profile/                # Profile
    ├── Endpoints/                  # KeepAliveEndpoint, HealthEndpoints
    ├── Security/                   # CurrentUser, revalidation, MustChangePassword, IdleCircuitHandler,
    │                               #   security headers, Blazor origin check, rate limiting
    └── wwwroot/                    # app.css, js/idle-monitor.js

tools/
└── Upms.Seed/                      # realistic data volumes for the performance suite

tests/
├── Upms.Domain.Tests/              # unit: keys, ranks, board rules, work item rules and history
├── Upms.Application.Tests/         # integration: services on real SQL Server (Testcontainers + Respawn)
├── Upms.Web.Tests/                 # bUnit component tests; host security tests (WebApplicationFactory)
├── Upms.E2E.Tests/                 # Playwright journeys per story + axe scans
├── Upms.Architecture.Tests/        # ArchUnitNET: module boundaries, private setters
└── Upms.Performance.Tests/         # opt-in: SC-002

docs/
└── operations/                     # deployment (container and IIS), backup-restore runbook
```

**Structure Decision**: one web application (`Upms.Web`, one deployable) layered into Domain,
Application, Infrastructure and Web projects, with the Phase 1 modules (Identity, Projects, Work) as
folders and namespaces in each layer (research R4). Later phases add modules and additive schema
changes (research R26) rather than new projects or restructured tables.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle III: Phase 1 authorizes by role, project ownership and item authorship, not project membership (open workspace, FR-015) | The agreed roadmap puts project member management in Phase 2; the spec limits Phase 1 to a pilot with non-confidential work, and membership must exist before confidential projects are tracked | Building membership now pulls Phase 2 forward; skipping checks until Phase 2 would scatter authorization later. The chosen design still checks every read and write through `IProjectAccess` (research R7), so Phase 2 changes one implementation |
| Microsoft.FluentUI.AspNetCore.Components | Accessible inputs, dialogs, menus and the drawer shell (constitution VI, FR-042) | Hand-building accessible menus and dialogs is large and error-prone |
| OpenTelemetry .NET packages | Traces and metrics required by the constitution (Observability) | The built-in `Activity` and `Meter` APIs need an exporter; OpenTelemetry is the vendor-neutral standard |
| Test-only: Respawn, Deque.AxeCore.Playwright, ArchUnitNET (beyond the constitution's xUnit, bUnit, Testcontainers, Playwright) | Fast database reset between tests; automated WCAG checks (SC-008); enforcing module boundaries (constitution V) | Recreating databases per test is slow; manual accessibility checks are not repeatable; boundary rules by convention erode |
