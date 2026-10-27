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
- **Phase 3 (templates and planning)**: in progress on the `phase-3` branch
  ([tasks](specs/004-templates-and-planning/tasks.md)). All four stories are built; the browser tests, the full
  performance run and the manual checks are still to do ([validation](specs/004-templates-and-planning/validation.md)):
  - a template per project (Kanban, Scrum or Stage-gate), with the columns that fit it, and a switch between
    Kanban and Scrum;
  - work item types: epics, stories and bugs in Kanban and Scrum projects, stages and milestones in Stage-gate
    projects, beside tasks and sub-tasks;
  - epics and stages that group work and show its progress, and estimates on stories, tasks and bugs;
  - for Scrum projects, an ordered backlog, sprints that Project Admins create, start and complete, a board that
    shows the active sprint, and a report for each completed sprint;
  - for Stage-gate projects, a Stages view where each stage ends in a gate: approvers named from anywhere in the
    organization approve it or send it back, work in later stages cannot start until it is approved, and "My tasks"
    lists the gates waiting for you;
  - dependencies ("waits for") between items of one project, added from the drawer or the timeline, with late ones
    marked and no date ever moved;
  - a timeline that plans: epics and stages as groups with their progress, milestones as markers, gates on the
    stages' end dates, a sprint lane for Scrum projects, and arrows between items that wait for each other.
- **Phase 4 (collaboration and search)**: in progress ([tasks](specs/005-collaboration-and-search/tasks.md)), built
  story by story. Done so far:
  - formatted descriptions and comments (headings, lists, code, links and simple tables) in an editor with a toolbar
    and keyboard shortcuts, with "@" mentions of project members;
  - watchers: the creator, assignee and commenters watch an item, and anyone who can see it can start or stop;
  - a notification bell on every screen for mentions, assignments, comments and status changes on watched items, and
    gate requests and decisions, never about one's own actions;
  - attachments up to 25 MB (images, PDF, Office, text and technical files such as logs, JSON, SQL and notebooks), from
    the file chooser, by dropping them on a task or by pasting screenshots into the text, each checked for malware by
    ClamAV before anyone can open it;
  - a search box on every screen that opens an item by its key and lists the items you opened last, and a search page
    across every project you can see, by words in titles, descriptions and comments and by filters, with saved
    filters you can rerun, rename, update, delete and share with a project;
  - labels shared across projects, matched regardless of letter case and suggested while typing, shown on cards,
    rows and results, with label filters in the list and search, and board quick filters by label, type and epic or
    stage;
  - email through the company mail server for what each person chose (everything, only what is aimed at them, the
    default, or nothing), sent in the background and retried for 24 hours, without descriptions or comment text.
- **Jira parity pass (before Phase 5)**: in progress ([tasks](specs/006-jira-parity/tasks.md)). After trying the
  Phase 4 build, the owner asked for U-PMS to look and behave like today's Jira Cloud. All five stories are built;
  the full browser and performance runs, the quickstart and the manual screen reader check are still to do:
  - **spaces** instead of projects on every screen, message and email, with old bookmarks still working;
  - a **Create** button on every screen: one form for any work type the space uses (epic, story, task, bug,
    sub-task, stage, milestone) with its status, assignee, priority, labels, parent, sprint, dates and estimate,
    and "Create another";
  - **columns managed on the board**: "+" adds one, and each column's menu renames it, sets its limit, moves it,
    changes its status type or deletes it;
  - **Create space** as a template gallery with previews, including a **Blank** template where you choose the
    columns and work types; work types can be turned on and off later in Space settings;
  - **Jira's layout**: a collapsible sidebar (For you, Recent, Starred, Spaces, Filters), starred spaces, the
    space's tabs and "…" menu, a **Summary** page per space, and **For you** (Worked on, Viewed, Assigned to me,
    Starred);
  - **Jira-style cards** with colored epic tags and avatars, **sub-tasks as cards**, and **swimlanes** by epic (or
    stage), assignee or sub-tasks, where moving a card to another lane changes its epic, stage or assignee.

The [Phase 2 validation record](specs/003-project-views-and-team/validation.md) covers the upgrade of a Phase 1
database, every quickstart step and the performance run. Before the pilot, a few things still need a person;
see [Before the pilot](#before-the-pilot).

The project follows [Spec Kit](https://github.com/github/spec-kit) spec-driven development: every feature is
specified, clarified, planned, and broken into tasks before any code is written.

## Stack

.NET 10 (LTS) · ASP.NET Core Blazor Web App (Interactive Server) · SQL Server with EF Core 10 ·
ASP.NET Core Identity (built-in accounts; company SSO can be added later) · since Phase 4, a TipTap editor bundle
(built with Node.js and committed), ClamAV for the malware check and MailKit for email · xUnit, bUnit,
Testcontainers, Playwright. One deployable modular monolith; see the
[Phase 1 plan](specs/002-kanban-project-core/plan.md) and the [Phase 4 plan](specs/005-collaboration-and-search/plan.md)
for details.

## Roadmap

| Phase | Scope | Spec |
|-------|-------|------|
| **1. Core Kanban project** | Sign-in from Day 1; project creation (name, key, description); Kanban board (To Do, In Progress, Done) with inline "What needs to be done?"; task details drawer (title, description, priority, sub-tasks, comments, history); customizable columns | [specs/002-kanban-project-core](specs/002-kanban-project-core/spec.md) |
| **2. Views and project team** | Members-only projects with Project Admin, Member and Viewer roles; task assignees, start and due dates, "My tasks"; List and Timeline views beside the board | [specs/003-project-views-and-team](specs/003-project-views-and-team/spec.md) |
| 3. Templates and planning | Scrum and Stage-gate templates beside Kanban; epics, stories, bugs, stages and milestones; backlog and sprints; stage-gate approvals; dependencies on the timeline | [specs/004-templates-and-planning](specs/004-templates-and-planning/spec.md) (draft) |
| 4. Collaboration and search | Formatted descriptions and comments, @mentions, watchers, in-app notifications, email with a per-person choice, attachments checked for malware, labels and board quick filters, search across projects with saved filters | [specs/005-collaboration-and-search](specs/005-collaboration-and-search/spec.md) (draft) |
| Jira parity pass | Spaces; the Create form; columns on the board; template gallery and Blank spaces; sidebar, Summary and "For you"; Jira-style cards, sub-task cards and swimlanes | [specs/006-jira-parity](specs/006-jira-parity/spec.md) (draft) |
| 5. Portfolio and dashboards | Portfolios and portfolio rollups; one dashboard per project and per portfolio, with health updates | to be specified |

The earlier MVP spec, [specs/001-issue-tracker-mvp](specs/001-issue-tracker-mvp/spec.md), is kept as
the product vision and requirement backlog for the later phases.

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

## Phase 3 documents

| Document | Purpose |
|----------|---------|
| [Spec](specs/004-templates-and-planning/spec.md) | What Phase 3 does and why: 4 stories, 57 requirements, the decisions of 2026-09-28 |
| [Plan](specs/004-templates-and-planning/plan.md) · [Research](specs/004-templates-and-planning/research.md) · [Data model](specs/004-templates-and-planning/data-model.md) | Design: templates, types, sprints, stage gates and dependencies, all additive |
| [Contracts](specs/004-templates-and-planning/contracts/) | Application services, the Phase 3 permission matrix (with the Approver column), UI routes |
| [Tasks](specs/004-templates-and-planning/tasks.md) | 100 test-first tasks, with implementation notes at the end |
| [Quickstart](specs/004-templates-and-planning/quickstart.md) · [Validation](specs/004-templates-and-planning/validation.md) | How to upgrade from Phase 2 and validate Phase 3; what has been checked so far |
| [Security review](docs/security/phase3-asvs-review.md) · [Screen-reader script](docs/accessibility/phase1-screen-reader-review.md#phase-3-screens-setup) | OWASP ASVS Level 2 review of the Phase 3 changes; the manual pass for the new screens |
| [Pilot plan](docs/pilot/phase1-pilot-plan.md) | Now also measures Phase 3's SC-001, SC-003, SC-004 and SC-012 |

## Phase 4 documents

| Document | Purpose |
|----------|---------|
| [Spec](specs/005-collaboration-and-search/spec.md) | What Phase 4 does and why: 5 stories, 56 requirements, the decisions of 2026-10-01 |
| [Plan](specs/005-collaboration-and-search/plan.md) · [Research](specs/005-collaboration-and-search/research.md) · [Data model](specs/005-collaboration-and-search/data-model.md) | Design: the Notifications module, formatted text, files in the database, search in SQL Server, all additive |
| [Contracts](specs/005-collaboration-and-search/contracts/) | Application services, the Phase 4 permission matrix, the attachment endpoints, UI routes |
| [Tasks](specs/005-collaboration-and-search/tasks.md) | 104 test-first tasks, with implementation notes at the end |
| [Quickstart](specs/005-collaboration-and-search/quickstart.md) | How to upgrade from Phase 3 and validate Phase 4 |

## Jira parity pass documents

| Document | Purpose |
|----------|---------|
| [Spec](specs/006-jira-parity/spec.md) | What the pass does and why: 5 stories, the owner's decisions of 2026-10-07 (follow Jira wherever it has an answer) |
| [Plan](specs/006-jira-parity/plan.md) · [Research](specs/006-jira-parity/research.md) · [Data model](specs/006-jira-parity/data-model.md) | Design: words not code, Jira Cloud's layout, one Create form, work types per space, lanes and sub-task cards; three additive migrations |
| [Contracts](specs/006-jira-parity/contracts/) | Application services, the permission matrix (superseding Phase 4's), UI routes |
| [Tasks](specs/006-jira-parity/tasks.md) | 98 test-first tasks |
| [Quickstart](specs/006-jira-parity/quickstart.md) | How to upgrade from Phase 4 and validate the pass |

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

**Phase 4 services** (optional locally): `docker compose up -d clamav smtp4dev` starts ClamAV (uploads stay "being
checked" without it) and smtp4dev, a mail server that keeps every email and shows them at http://localhost:5025. Set
`Malware:ClamAv:Host` to `localhost`, and `Email:Smtp:Host`, `Email:Smtp:Port` (`2525`), `Email:Smtp:Security` (`None`)
and `Email:FromAddress` with `dotnet user-secrets`. The editor bundle `wwwroot/js/editor.js` is committed; rebuild it
after changing `src/Upms.Web/Editor` with `npm ci && npm run build` there (Node.js 24). See the
[Phase 4 quickstart](specs/005-collaboration-and-search/quickstart.md).

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
`dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"` (the Phase 4
permission matrix: every action for every kind of caller). Acceptance tests are named after their scenarios:
`US1_AS1_…` for Phase 1, `P2_US1_AS1_…` for Phase 2, `P3_US1_AS1_…` for Phase 3 and `P4_US1_AS1_…` for Phase 4.

**External services**: the tests against a real ClamAV daemon and a real mail server pull their images and run in
their own CI job. Leave them out with `-- --filter-not-trait "Category=ExternalServices"`, or run only them with
`-- --filter-trait "Category=ExternalServices"`.

**Quality gates** (as in CI): `dotnet format --verify-no-changes`, `dotnet build -c Release` (warnings are
errors; NuGet audit fails on high and critical advisories) and
`dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web`.

**Performance (SC-002, opt-in)**: `dotnet test --project tests/Upms.Performance.Tests` seeds 500,000 work items
in a SQL Server container and runs 300 simulated users for 2 minutes; `tools/Upms.Seed` fills any database with
the same data. See the quickstart for the settings.

## Before the pilot

- [ ] Manual screen-reader pass with NVDA and Edge on the Phase 1 to 4 screens
      ([script](docs/accessibility/phase1-screen-reader-review.md)).
- [ ] Restore drill on the production backups ([runbook](docs/operations/backup-restore.md)).
- [ ] Decide on multi-factor sign-in for administrators ([ASVS open item O1](docs/security/phase1-asvs-review.md#open-items)).
- [ ] Decide whether project names must stay unique across the organization, since the rule lets anyone creating
      a project learn that a name is taken ([Phase 2 open item P2-O1](docs/security/phase2-asvs-review.md#open-items)).
- [ ] Decide on a limit to the number of approvers per gate, since people outside the project who are named become
      Viewers ([Phase 3 open item P3-O1](docs/security/phase3-asvs-review.md#open-items)).
- [ ] Run the browser tests, with their axe scans, for Phases 3 and 4 (they need Playwright's Chromium, above).
- [ ] Connect sign-in to the company's LDAP server (provided for UAT), and remove `DevSignIn:UserName`, a development-only
      switch that signs everyone in without a password.
- [ ] Set up ClamAV and the company mail server, with the mail password in the secret store
      ([deployment](docs/operations/deployment.md)), and review the [Phase 4 security review](docs/security/phase4-asvs-review.md).

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`: produce a phase's documents
  (Phase 3's are in `specs/004-templates-and-planning/`)
- `/speckit-analyze`: cross-check a phase's spec, plan and tasks before building
- `/speckit-implement`: build a phase from its `tasks.md`
