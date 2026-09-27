# U-PMS (UBL Project Management System)

U-PMS is a Jira-like project management system for a single organization, serving both IT teams
(agile: Scrum and Kanban) and the PMO (structured, phase-based projects) on one hierarchy:
Portfolio → Project → work items (epics or phases, stories, tasks, bugs, milestones, sub-tasks). It
covers projects, work items, backlog and sprints, an interactive timeline, boards, project and
portfolio dashboards, collaboration, search, and user administration.

**Status**: Phase 1 (core Kanban project) is built: sign-in and accounts, projects, the Kanban board,
the task details drawer and column customization, with automated tests for every acceptance scenario.
Before the pilot, the manual screen-reader pass and the restore drill still need a person; see
[Before the pilot](#before-the-pilot). The project follows
[Spec Kit](https://github.com/github/spec-kit) spec-driven development: every feature is specified,
clarified, planned, and broken into tasks before any code is written.

## Stack

.NET 10 (LTS) · ASP.NET Core Blazor Web App (Interactive Server) · SQL Server with EF Core 10 ·
ASP.NET Core Identity (built-in accounts; company SSO can be added later) · xUnit, bUnit,
Testcontainers, Playwright. One deployable modular monolith; see the
[Phase 1 plan](specs/002-kanban-project-core/plan.md) for details.

## Roadmap

| Phase | Scope | Spec |
|-------|-------|------|
| **1. Core Kanban project** | Sign-in from Day 1; project creation (name, key, description); Kanban board (To Do, In Progress, Done) with inline "What needs to be done?"; task details drawer (title, description, priority, sub-tasks, comments, history); customizable columns | [specs/002-kanban-project-core](specs/002-kanban-project-core/spec.md) |
| 2. Views and project team | Board, List and Timeline views of the same project; project members and task assignees | to be specified |
| 3. Enterprise and portfolio | Portfolio rollups, cross-project dashboards, Scrum and PMO stage-gate templates | to be specified |

The earlier MVP spec, [specs/001-issue-tracker-mvp](specs/001-issue-tracker-mvp/spec.md), is kept as
the product vision and requirement backlog for Phases 2 and 3.

## Phase 1 documents

| Document | Purpose |
|----------|---------|
| [Constitution](.specify/memory/constitution.md) | Non-negotiable project principles and quality gates |
| [Spec](specs/002-kanban-project-core/spec.md) | What Phase 1 does and why: 3 stories, 44 requirements |
| [Plan](specs/002-kanban-project-core/plan.md) | Architecture, constitution check, source layout |
| [Research](specs/002-kanban-project-core/research.md) | Technology decisions, including how Phases 2–3 extend Phase 1 |
| [Data model](specs/002-kanban-project-core/data-model.md) | Tables, rules, and the planned additive changes for later phases |
| [Contracts](specs/002-kanban-project-core/contracts/) | Application services, permissions, UI routes, HTTP endpoints |
| [Tasks](specs/002-kanban-project-core/tasks.md) | 114 test-first tasks, with implementation notes at the end |
| [Quickstart](specs/002-kanban-project-core/quickstart.md) | How to run and validate Phase 1 |
| [Deployment](docs/operations/deployment.md) · [Backups and restore](docs/operations/backup-restore.md) | Running U-PMS in production |
| [Security review](docs/security/phase1-asvs-review.md) · [Screen-reader review](docs/accessibility/phase1-screen-reader-review.md) | OWASP ASVS Level 2 review; accessibility checks |
| [Pilot plan](docs/pilot/phase1-pilot-plan.md) | How the 2-week pilot measures SC-001, SC-003 and SC-011 |
| [Design prototype](design/prototype/index.html) | Clickable prototype with demo data: open the file in any browser |

## Design prototype

`design/prototype/index.html` is a single self-contained page (no install, no server). Open it in
Edge, Chrome or Firefox to click through project creation, the Summary page, Timeline, Backlog and
sprints, the board, the issue list, and the issue detail panel. It runs on demo data stored in your
browser; use the avatar menu to switch demo users or reset the data. It shows more than Phase 1
(Scrum backlog, timeline, summary), so treat it as a picture of the whole roadmap.

## Run it locally

You need the .NET 10 SDK (pinned in `global.json`) and Docker. Full steps, including how to validate each
user story by hand, are in the [quickstart](specs/002-kanban-project-core/quickstart.md).

```bash
export MSSQL_SA_PASSWORD='<a strong local password>'
docker compose up -d sqlserver                     # SQL Server 2022 on localhost:1433

cd src/Upms.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=Upms;User Id=sa;Password=<same password>;TrustServerCertificate=True"
dotnet user-secrets set "Setup:Token" "<any long random string>"
cd ../..

dotnet tool restore
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web                  # then open https://localhost:5001/setup
```

On `/setup`, enter the setup token and create the first administrator, then add users at `/admin/users`.

## Tests

```bash
dotnet test --project tests/Upms.Domain.Tests          # domain rules (no database)
dotnet test --project tests/Upms.Application.Tests     # services on a real SQL Server (Testcontainers)
dotnet test --project tests/Upms.Web.Tests             # components (bUnit) and host security
dotnet test --project tests/Upms.Architecture.Tests    # module boundaries
dotnet test --project tests/Upms.E2E.Tests             # browser journeys and axe accessibility scans
```

The database and browser tests start their own SQL Server container, so only a running Docker engine is
needed. The end-to-end tests need Playwright's Chromium once:
`pwsh tests/Upms.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`.

To run one class or one test, pass a filter to the test platform, for example
`dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"`.

**Quality gates** (as in CI): `dotnet format --verify-no-changes`, `dotnet build -c Release` (warnings are
errors; NuGet audit fails on high and critical advisories) and
`dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web`.

**Performance (SC-002, opt-in)**: `dotnet test --project tests/Upms.Performance.Tests` seeds 500,000 work items
in a SQL Server container and runs 300 simulated users for 2 minutes; `tools/Upms.Seed` fills any database with
the same data. See the quickstart for the settings.

## Before the pilot

- [ ] Manual screen-reader pass with NVDA and Edge ([script](docs/accessibility/phase1-screen-reader-review.md)).
- [ ] Restore drill on the production backups ([runbook](docs/operations/backup-restore.md)).
- [ ] Decide on multi-factor sign-in for administrators ([ASVS open item O1](docs/security/phase1-asvs-review.md#open-items)).

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`: for Phase 2 and Phase 3
  when their turn comes
- `/speckit-analyze`: cross-check a phase's spec, plan and tasks before building
- `/speckit-implement`: build a phase from its `tasks.md`
