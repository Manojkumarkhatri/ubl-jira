# Implementation Plan: Jira Parity Pass

**Branch**: `phase-3` (feature `006-jira-parity`) | **Date**: 2026-10-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-jira-parity/spec.md`

## Summary

Before Phase 5, U-PMS should look and behave like today's Jira Cloud for its target teams:

- "projects" become "spaces" on every screen, with old addresses still working;
- a **Create** button on every screen opens one form for any work type, with every field and "Create another";
- Space admins add, rename, limit, move, retype and delete columns on the board itself;
- **Create space** is a template gallery with previews, plus a Blank template with chosen columns and work
  types; any Kanban, Scrum or blank space can turn its work types on and off;
- the layout:
  - a left sidebar on every page (For you, Recent, Starred, Spaces, Filters);
  - tabs for a space's views;
  - a Summary page;
  - a "For you" page;
- the board:
  - Jira-style cards with colored epic tags and avatars;
  - sub-tasks as cards;
  - swimlanes by epic, assignee or parent, where moving a card into another lane changes its epic or assignee.

**Technical approach** (details in [research.md](./research.md)): the same stack, no new dependency, no new
module, three additive migrations.

- **Words, not code** (R1–R3): screen text and messages say "space"; code, tables and error codes keep
  "project". One redirecting page serves the old addresses, and an architecture test stops "project" coming back.
- **Layout** (R4–R7): a `Sidebar` in `MainLayout` loaded in one call; stars and visits in two small Projects
  tables; the collapsed choice on the user; `SpaceHeader` with tabs and a "…" menu.
- **Create form** (R10, R11): one dialog in the layout, fed by `ICreateWorkService`. `IWorkItemService.CreateAsync`
  takes every field and applies the drawer's rules in one transaction and one history change.
- **Spaces** (R12–R14): board column controls reuse `IBoardColumnService`. Blank is a Kanban space created with
  its own columns. A `WorkTypeSet` on the space flows through `ProjectAccessInfo` to every check.
- **Board** (R15–R18): cards redesigned; epic colors from a fixed palette with checked contrast; lanes grouped in
  the component from the cards already loaded; `MoveCardAsync` takes a `LaneChange`; sub-tasks join the board
  query.
- **Summary and For you** (R8, R9): grouped queries over one covering index, and history read by actor and by
  time through two new indexes.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS), as Phases 1–4

**Primary Dependencies**: unchanged (ASP.NET Core 10 Blazor Web App with Interactive Server, ASP.NET Core
Identity, EF Core 10 SQL Server provider, OpenTelemetry, MailKit, the TipTap editor bundle). No new package.

**Storage**: SQL Server 2022 or later. Three additive migrations:
- `WorkTypesAndActivityIndex`;
- `SpaceNavigation`;
- `EpicColors`.

They add one column each on `AspNetUsers`, `Projects` and `WorkItems`, two tables and three indexes
([data-model.md](./data-model.md)).

**Testing**:
- The same stack: xUnit v3, bUnit, Testcontainers (SQL Server), Respawn, Playwright with axe-core, ArchUnitNET.
- New: `WordingTests` (architecture), `JP_US1`–`JP_US5` browser journeys, an upgrade test from the last Phase 4
  migration, the new permission matrix, and new SC-002 load scenarios.

**Target Platform**: unchanged (Linux container or Windows Server/IIS; current Edge, Chrome and Firefox; usable
at 360 px)

**Project Type**: web application (the same single deployable modular monolith)

**Performance Goals**: p95 ≤ 1 s at 500,000 work items and 300 concurrent users for every Phase 1–4 action, plus:
- the Create form's spaces and options;
- the Summary page;
- the four "For you" tabs;
- the sidebar;
- a board with sub-task cards;
- a lane move (SC-006).

**Constraints**:
- Phase 1–4 constraints hold: availability, RPO/RTO, WCAG 2.2 AA with keyboard alternatives, portability, and
  "not found" for non-members.
- Spaces stay members-only (FR-018).
- No page or message says "project" (SC-004).
- Phase 1–4 tests change only in wording (SC-005).
- History stays append-only: the new indexes add no data change to `WorkItemChanges`.

**Scale/Scope**: as Phase 4.
- New screens: the Summary page, "For you", the Create dialog, the Create space gallery and the sidebar.
- Changed screens: the header, every space page (tabs), the board (cards, lanes, column controls), space settings
  (work types), the list and the timeline (epic colors), and the search page (`resolved`).

See [contracts/ui-routes.md](./contracts/ui-routes.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.1 (`.specify/memory/constitution.md`).

| # | Gate | Pre-research | Post-design evidence |
|---|------|:------------:|----------------------|
| I | Spec in `specs/006-…`, stories prioritized and independently testable, no technology in the spec; P1 alone is a usable increment | ✅ PASS | spec.md: 5 stories, checklist 16/16, six clarifications recorded (one owner rule: follow Jira wherever it has an answer); US1 (the Create form) is usable alone |
| II | Test-first rules and services; every acceptance scenario automated; real SQL Server | ✅ PASS | R20: domain tests for column lists, work-type sets, epic colors and lane grouping; service tests on SQL Server per story; `JP_` acceptance tests and browser journeys written before the code; upgrade test; new matrix |
| III | Server-side authorization on every read and write; encoded output; CSRF protection; audit of security-relevant events | ✅ PASS | R6, R11, R17, R19: every new method calls `IProjectAccess`; the Create form's lists and the sidebar, summary and For you show only what the caller can see now; lane moves check every drawer rule; work-type changes audited; spaces stay members-only |
| IV | Append-only history for every change; no hard deletes in normal use; optimistic concurrency; UTC; reviewed migrations | ✅ PASS | R11, R15, R17, R21: creation with all fields and lane moves are one change each in history; epic color changes recorded; work types guarded by `DetailsVersion`, columns by `BoardVersion`; indexes only on `WorkItemChanges`; three additive migrations |
| V | One app, one database, module boundaries; every new dependency justified; YAGNI | ✅ PASS | No new dependency or module. Stars and visits in Projects; summary, For you and Create form in Work; the sidebar choice in Identity; Projects asks Work for type counts through the existing `IProjectPlanSetup` contract; Blank is a Kanban space, not a new template |
| VI | WCAG 2.2 AA; keyboard alternative for every drag; 360 px; feedback without losing input | ✅ PASS | R4, R5, R10, R16, R17: disclosures instead of hover menus, a modal panel at phone width, "Move to › Lane" for lane drags, menu buttons for columns, a radiogroup gallery with announced previews, a Create form that keeps text on refusal, no one-key shortcut (WCAG 2.1.4), epic colors with 4.5:1 contrast in both themes and text beside every color |
| — | Platform standards (performance baseline, observability, configuration, portability) | ✅ PASS | R8, R9, R20: covering and history indexes, load scenarios for every new read and the lane move; no new settings |
| — | Workflow gates (CI, Definition of Done, traceability) | ✅ PASS | quickstart.md section 3; README, CLAUDE.md and the pilot plan updated |

**Result**: passes with no exceptions; Complexity Tracking is empty. Re-checked after the design with the same
outcome.

## Project Structure

### Documentation (this feature)

```text
specs/006-jira-parity/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-services.md
│   ├── permissions.md   # supersedes the Phase 4 matrix; read by PermissionMatrixTests
│   └── ui-routes.md
└── tasks.md             # /speckit-tasks output
```

### Source Code (additions and changes to the Phase 4 tree)

```text
src/
├── Upms.Domain/
│   ├── Common/WorkTypeSet.cs                   # new flags enum (research R14)
│   ├── Identity/User.cs, AuditEventType.cs     # + SidebarCollapsed; ProjectWorkTypesChanged
│   ├── Projects/Project.cs, ProjectRole.cs     # + WorkTypes, column-list Create, SetWorkTypes; RoleNames
│   │           StarredProject.cs, ProjectVisit.cs   # new (research R6)
│   └── Work/WorkItem.cs, WorkItemTypes.cs, WorkItemField.cs, EpicColor.cs   # Color; IsOffered with the set
├── Upms.Application/
│   ├── Identity/AccountService.cs              # sidebar choice
│   ├── Projects/ProjectService.cs, ProjectAccess.cs        # Blank setup, work types; WorkTypes in access info
│   ├── Projects/ISpaceNavigationService.cs, SpaceNavigationService.cs   # new: sidebar, stars, visits
│   ├── Projects/TemplateCatalog.cs              # new: gallery previews (research R13)
│   └── Work/
│       ├── WorkItemService.cs                   # create with every field; epic color
│       ├── ICreateWorkService.cs, CreateWorkService.cs     # new (research R10)
│       ├── BoardService.cs                      # sub-task cards, lane moves, colors
│       ├── ISpaceSummaryService.cs, SpaceSummaryService.cs # new (research R8)
│       ├── IForYouService.cs, ForYouService.cs  # new: worked on (research R9)
│       ├── SearchService.cs                     # resolved range
│       └── ProjectPlanSetup.cs                  # TypeCountsAsync
├── Upms.Infrastructure/
│   └── Persistence/Configurations/..., Migrations/*_WorkTypesAndActivityIndex, *_SpaceNavigation, *_EpicColors
└── Upms.Web/
    ├── Components/Layout/MainLayout.razor, Header.razor, Sidebar.razor (new), SidebarState.cs (new)
    ├── Components/Layout/CreateWorkDialog.razor, CreateWorkLauncher.cs     # new (research R10)
    ├── Components/Shared/SpaceHeader.razor (replaces ProjectHeader), StarButton.razor, Avatar.razor,
    │                     EpicTag.razor, LegacyAddress.razor             # new
    ├── Components/Pages/ForYou/ForYouPage.razor, WorkedOnTab.razor, AssignedTab.razor   # new; My tasks moves in
    ├── Components/Pages/Summary/SummaryPage.razor                                       # new
    ├── Components/Pages/Spaces/SpaceList.razor, CreateSpaceDialog.razor, TemplateGallery.razor,
    │                           BlankSetup.razor                                         # renamed and new
    ├── Components/Pages/Board/BoardPage.razor, TaskCard.razor, BoardColumn.razor, MoveToMenu.razor,
    │                         BoardLanes.cs, LaneHeader.razor, ColumnMenu.razor, AddColumnButton.razor
    ├── Components/Pages/Settings/SpaceSettings.razor, WorkTypesEditor.razor (new)
    ├── Components/Pages/**                     # @page /spaces/…, wording
    └── wwwroot/app.css                          # sidebar, tabs, cards, lanes, epic palette (light and dark)

tools/Upms.Seed/                                 # stars, visits, epic colors, sub-tasks on boards (R20)
tests/
├── Upms.Domain.Tests/                           # work-type sets, column lists, epic colors, lane grouping
├── Upms.Application.Tests/                      # services per story, JiraParityUpgradeTests, the new matrix
├── Upms.Web.Tests/                              # dialog, sidebar, gallery, column menu, lanes, cards, wording updates
├── Upms.E2E.Tests/                              # JP_US1–JP_US5 + axe; Phase 1–4 journeys on the new addresses
├── Upms.Architecture.Tests/                     # WordingTests
└── Upms.Performance.Tests/                      # new actions in the SC-002 run
```

**Structure Decision**: no new module.
- **Projects** owns spaces, their work types, stars and visits, the gallery's template descriptions and the
  sidebar's space sections.
- **Work** owns the Create form's data and the creation itself, the summary, "For you", the board's lanes and
  sub-task cards, and epic colors. It implements `IProjectPlanSetup.TypeCountsAsync` for the Projects module.
- **Identity** owns the sidebar choice.

The sidebar reads recent items and saved filters through Work's existing read services, from the Web layer,
not across modules.

## Complexity Tracking

No constitution exceptions: no new dependency, module or infrastructure.
