# Implementation Plan: Project Views and Team (Phase 2)

**Branch**: `claude/practical-lamport-l47ei8` (feature `003-project-views-and-team`) | **Date**: 2026-09-27 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-project-views-and-team/spec.md`

## Summary

Phase 2 turns the Phase 1 open workspace into members-only projects and adds the views teams asked for:
project teams with Project Admin, Member and Viewer roles (with pilot projects migrated to their owners
and contributors); task assignees and start and due dates, with overdue marks, board filters and a
cross-project "My tasks" page; a sortable, filterable List view whose state lives in its address; and a
Timeline view with drag-and-drop and keyboard rescheduling.

**Technical approach** (details in [research.md](./research.md)): the same Blazor Web App, modules and
test stack as Phase 1 (`specs/002-kanban-project-core/plan.md`). Every change is additive, as Phase 1's
research R26 planned: a `ProjectMembers` table owned by the `Project` aggregate with a `MembersVersion`
concurrency token; `IProjectAccess` switches its implementation to membership (non-members get "not
found"); nullable `AssigneeId`, `StartDate` and `DueDate` columns on `WorkItems`; new read services for
the list, the timeline and "My tasks"; a new `IProjectTeam` contract so the Work module can ask about
membership without reading Projects tables. The timeline is plain HTML and CSS with one small pointer
script; no new libraries or runtime infrastructure.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS), as Phase 1

**Primary Dependencies**: unchanged from Phase 1 (ASP.NET Core 10 Blazor Web App with Interactive Server,
ASP.NET Core Identity, EF Core 10 SQL Server provider, OpenTelemetry); no new packages

**Storage**: SQL Server 2022 or later; two additive migrations (team with its upgrade backfill; assignees,
dates and indexes)

**Testing**: xUnit v3, bUnit, Testcontainers (SQL Server), Respawn, Playwright with axe-core, ArchUnitNET;
a migration test for the upgrade; the opt-in performance suite extended

**Target Platform**: unchanged (Linux container or Windows Server/IIS; current Edge, Chrome, Firefox;
usable at 360 px)

**Project Type**: web application (the same single deployable modular monolith)

**Performance Goals**: p95 ≤ 1 s at 500,000 work items and 300 concurrent users for the Phase 1 actions
plus list loads (sorted and filtered), timeline loads (up to 500 scheduled tasks), "My tasks" loads, and
saving assignees, dates and membership changes (SC-002); every list paged 50 at a time

**Constraints**: Phase 1 constraints hold (availability, RPO/RTO, WCAG 2.2 AA with keyboard alternatives,
plain text, portability); non-members must not learn that a project exists (FR-002); the upgrade must
keep every pilot contributor's access (SC-006)

**Scale/Scope**: as Phase 1, plus a timeline of up to 500 scheduled tasks and teams of up to a few hundred
members; three new screens (list, timeline, members) and "My tasks" ([contracts/ui-routes.md](./contracts/ui-routes.md))

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.1 (`.specify/memory/constitution.md`).

| # | Gate | Pre-research | Post-design evidence |
|---|------|:------------:|----------------------|
| I | Spec in `specs/003-…`, stories prioritized and independently testable, no technology in the spec; P1 alone is a usable increment | ✅ PASS | spec.md: 4 stories, checklist 16/16, clarifications recorded; US1 (teams and members-only projects) is usable alone |
| II | Test-first rules and services; every acceptance scenario automated; real SQL Server | ✅ PASS | R16: domain, service, component and browser tests per story; migration test for the upgrade; the permission matrix reads the Phase 2 contract |
| III | Server-side authorization on every read and write **based on role and project membership**; audit of role and membership changes | ✅ PASS | R2, R6: `IProjectAccess` now checks membership on every call; non-members get `NotFound`; membership changes and the upgrade are audited. The Phase 1 deviation (open workspace) is **resolved** |
| IV | Append-only history for assignment and dates; optimistic concurrency; UTC; reviewed migrations | ✅ PASS | R1, R8: `Assignee`, `StartDate`, `DueDate` history rows; row version and `MembersVersion` conflicts; dates are calendar dates, times stay UTC; two migrations |
| V | One app, one database, module boundaries; no unjustified dependencies; YAGNI | ✅ PASS | R5, R7, R12: new contracts (`IProjectTeam`, `IUserDirectory.SearchActiveAsync`) instead of cross-module table reads; no Gantt or chart library; no saved filters, notifications or project visibility setting |
| VI | WCAG 2.2 AA; keyboard alternative for every drag; 360 px; feedback without losing input | ✅ PASS | R12: keyboard rescheduling with announcements plus the drawer's date fields; list and members screens are native controls; axe scans at both widths |
| — | Platform standards (performance baseline, observability, configuration, portability) | ✅ PASS | R15: extended performance suite and indexes; refusals keep the Phase 1 log event 4030 |
| — | Workflow gates (CI, Definition of Done, traceability) | ✅ PASS | quickstart.md steps 3–5; README and quickstart updated per story |

**Result**: passes with no deviations. Re-checked after the design with the same outcome.

## Project Structure

### Documentation (this feature)

```text
specs/003-project-views-and-team/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-services.md
│   ├── permissions.md   # supersedes the Phase 1 matrix; read by PermissionMatrixTests
│   └── ui-routes.md
└── tasks.md             # /speckit-tasks output
```

### Source Code (additions and changes to the Phase 1 tree)

```text
src/
├── Upms.Domain/
│   ├── Identity/AuditEventType.cs              # + MemberAdded, MemberRemoved, MemberRoleChanged
│   ├── Projects/ProjectRole.cs                 # new
│   ├── Projects/ProjectMember.cs               # new (owned by Project)
│   ├── Projects/Project.cs                     # + Members, MembersVersion, team rules
│   └── Work/WorkItem.cs, WorkItemField.cs      # + AssigneeId, StartDate, DueDate, Assign, Schedule
├── Upms.Application/
│   ├── Identity/Contracts/IUserDirectory.cs    # + SearchActiveAsync, UserDisplay.UserName
│   ├── Projects/Contracts/IProjectAccess.cs    # ProjectAccessInfo + Role, CanContribute
│   ├── Projects/Contracts/IProjectTeam.cs      # new contract for the Work module
│   ├── Projects/ProjectAccess.cs               # membership rules
│   ├── Projects/ProjectTeam.cs                 # new
│   ├── Projects/IProjectMemberService.cs, ProjectMemberService.cs   # new
│   ├── Projects/ProjectService.cs              # list by membership, creator as Project Admin
│   ├── Work/IWorkItemService.cs, WorkItemService.cs, WorkItemReads.cs  # assignee, dates, CanContribute
│   ├── Work/IBoardService.cs, BoardService.cs  # card assignee and due date, CanContribute
│   ├── Work/IWorkItemListService.cs, WorkItemListService.cs          # new
│   ├── Work/ITimelineService.cs, TimelineService.cs                  # new
│   └── Work/IMyTasksService.cs, MyTasksService.cs                    # new
├── Upms.Infrastructure/
│   └── Persistence/
│       ├── Configurations/Projects/ProjectMemberConfiguration.cs     # new
│       ├── Configurations/Work/WorkItemConfiguration.cs              # columns, check constraint, indexes
│       └── Migrations/*_ProjectMembers.cs, *_AssigneesAndDates.cs    # new
└── Upms.Web/
    ├── Components/Layout/                      # "My tasks" link
    ├── Components/Shared/ProjectHeader.razor   # new: breadcrumb, view tabs, Members, settings
    ├── Components/Shared/AssigneeBadge.razor, DueDate.razor          # new
    ├── Components/Pages/Board/                 # filters, card assignee and due date, read-only
    ├── Components/Pages/Drawer/                # assignee, dates, read-only mode
    ├── Components/Pages/List/ProjectListView.razor                   # new
    ├── Components/Pages/Timeline/TimelinePage.razor, TimelineBar.razor, TimelineScale.cs   # new
    ├── Components/Pages/Members/MembersPage.razor                    # new
    ├── Components/Pages/MyTasks/MyTasksPage.razor                    # new
    ├── Components/Pages/Projects/ProjectList.razor                   # role column
    └── wwwroot/js/timeline.js                  # new: pointer dragging only

tools/Upms.Seed/                                # teams, assignees, dates
tests/
├── Upms.Domain.Tests/                          # team, assignment and date rules
├── Upms.Application.Tests/                     # access, members, assignee/dates, list, timeline,
│                                               #   My tasks, MembershipUpgradeTests, Phase 2 matrix
├── Upms.Web.Tests/                             # members, list, timeline keyboard, drawer, board filters
├── Upms.E2E.Tests/                             # P2_US1–P2_US4 journeys + axe
├── Upms.Architecture.Tests/                    # unchanged rules cover the new contracts
└── Upms.Performance.Tests/                     # new actions in the SC-002 run
```

**Structure Decision**: no new projects or modules. The team belongs to the Projects module (it decides
who may do what in a project); assignees, dates and the three read services belong to the Work module,
which asks the Projects module about membership only through `IProjectTeam` and `IProjectAccess`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. The Phase 1 deviation (authorization without project membership) ends with this phase.
