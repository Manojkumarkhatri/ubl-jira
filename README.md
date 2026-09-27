# U-PMS (UBL Project Management System)

U-PMS is a Jira-like project management system for a single organization, serving both IT teams
(agile: Scrum and Kanban) and the PMO (structured, phase-based projects) on one hierarchy:
Portfolio → Project → work items (epics or phases, stories, tasks, bugs, milestones, sub-tasks). It
covers projects, work items, backlog and sprints, an interactive timeline, boards, project and
portfolio dashboards, collaboration, search, and user administration.

**Status**: Phases 1 and 2 are built, with automated tests for every acceptance scenario.

- **Phase 1 (core Kanban project)**: sign-in and accounts, projects, the Kanban board, the task details drawer
  and column customization.
- **Phase 2 (project views and team)**:
  - members-only projects, with Project Admin, Member and Viewer roles;
  - task assignees and start and due dates, with overdue marks;
  - "My tasks";
  - "Only my tasks" and assignee filters on the board;
  - the List and Timeline views beside the board.

The [Phase 2 validation record](specs/003-project-views-and-team/validation.md) covers the upgrade of a Phase 1
database, every quickstart step and the performance run. Before the pilot, a few things still need a person;
see [Before the pilot](#before-the-pilot).

The project follows [Spec Kit](https://github.com/github/spec-kit) spec-driven development: every feature is
specified, clarified, planned, and broken into tasks before any code is written.

## Stack

.NET 10 (LTS) · ASP.NET Core Blazor Web App (Interactive Server) · SQL Server with EF Core 10 ·
ASP.NET Core Identity (built-in accounts; company SSO can be added later) · xUnit, bUnit,
Testcontainers, Playwright. One deployable modular monolith; see the
[Phase 1 plan](specs/002-kanban-project-core/plan.md) for details.

## Roadmap

| Phase | Scope | Spec |
|-------|-------|------|
| **1. Core Kanban project** | Sign-in from Day 1; project creation (name, key, description); Kanban board (To Do, In Progress, Done) with inline "What needs to be done?"; task details drawer (title, description, priority, sub-tasks, comments, history); customizable columns | [specs/002-kanban-project-core](specs/002-kanban-project-core/spec.md) |
| **2. Views and project team** | Members-only projects with Project Admin, Member and Viewer roles; task assignees, start and due dates, "My tasks"; List and Timeline views beside the board | [specs/003-project-views-and-team](specs/003-project-views-and-team/spec.md) |
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
| [Tasks](specs/002-kanban-project-core/tasks.md) | 119 test-first tasks, with implementation notes at the end |
| [Quickstart](specs/002-kanban-project-core/quickstart.md) | How to run and validate Phase 1 |
| [Deployment](docs/operations/deployment.md) · [Backups and restore](docs/operations/backup-restore.md) | Running U-PMS in production |
| [Security review](docs/security/phase1-asvs-review.md) · [Screen-reader review](docs/accessibility/phase1-screen-reader-review.md) | OWASP ASVS Level 2 review; accessibility checks |
| [Pilot plan](docs/pilot/phase1-pilot-plan.md) | How the 2-week pilot measures SC-001, SC-003 and SC-011 |
| [Design prototype](design/prototype/index.html) | Clickable prototype with demo data: open the file in any browser |

## Phase 2 documents

| Document | Purpose |
|----------|---------|
| [Spec](specs/003-project-views-and-team/spec.md) | What Phase 2 does and why: 4 stories, 43 requirements, the decisions of 2026-09-27 |
| [Plan](specs/003-project-views-and-team/plan.md) · [Research](specs/003-project-views-and-team/research.md) · [Data model](specs/003-project-views-and-team/data-model.md) | Design: additive changes to Phase 1 |
| [Contracts](specs/003-project-views-and-team/contracts/) | Application services, the Phase 2 permission matrix, UI routes |
| [Tasks](specs/003-project-views-and-team/tasks.md) | 70 test-first tasks, with implementation notes at the end |
| [Quickstart](specs/003-project-views-and-team/quickstart.md) · [Validation](specs/003-project-views-and-team/validation.md) | How to upgrade from Phase 1 and validate Phase 2; the record of doing it, with the performance results |
| [Security review](docs/security/phase2-asvs-review.md) · [Screen-reader script](docs/accessibility/phase1-screen-reader-review.md#phase-2-setup) | OWASP ASVS Level 2 review of the Phase 2 changes; the manual pass for the new screens |
| [Pilot plan](docs/pilot/phase1-pilot-plan.md) | Now also measures Phase 2's SC-001, SC-007 and SC-008 |

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
Make at least one colleague an administrator there too ("Make administrator"), so accounts never depend on
one person. A project's creator becomes its Project Admin and adds the team from the project's **Members**
page.

**Upgrading a Phase 1 database**: the same `dotnet ef database update` applies the Phase 2 migrations. Each
project's owner becomes its Project Admin, and everyone who created, changed or commented on its work becomes a
Member, with one audit event each. Everyone else no longer sees the project. See the
[Phase 2 quickstart](specs/003-project-views-and-team/quickstart.md), step 1.

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
`dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"` (the Phase 2
permission matrix: every action for every kind of caller). Acceptance tests are named after their scenarios:
`US1_AS1_…` for Phase 1 and `P2_US1_AS1_…` for Phase 2.

**Quality gates** (as in CI): `dotnet format --verify-no-changes`, `dotnet build -c Release` (warnings are
errors; NuGet audit fails on high and critical advisories) and
`dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web`.

**Performance (SC-002, opt-in)**: `dotnet test --project tests/Upms.Performance.Tests` seeds 500,000 work items
in a SQL Server container and runs 300 simulated users for 2 minutes; `tools/Upms.Seed` fills any database with
the same data. See the quickstart for the settings.

## Before the pilot

- [ ] Manual screen-reader pass with NVDA and Edge on the Phase 1 and Phase 2 screens
      ([script](docs/accessibility/phase1-screen-reader-review.md)).
- [ ] Restore drill on the production backups ([runbook](docs/operations/backup-restore.md)).
- [ ] Decide on multi-factor sign-in for administrators ([ASVS open item O1](docs/security/phase1-asvs-review.md#open-items)).
- [ ] Decide whether project names must stay unique across the organization, since the rule lets anyone creating
      a project learn that a name is taken ([Phase 2 open item P2-O1](docs/security/phase2-asvs-review.md#open-items)).

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`: for Phase 3 when its turn
  comes
- `/speckit-analyze`: cross-check a phase's spec, plan and tasks before building
- `/speckit-implement`: build a phase from its `tasks.md`
