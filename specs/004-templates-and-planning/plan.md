# Implementation Plan: Templates and Planning (Phase 3)

**Branch**: `phase-3` (feature `004-templates-and-planning`) | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-templates-and-planning/spec.md`

## Summary

Phase 3 gives each project a template and the planning tools that go with it:

- Kanban (every existing project), Scrum and Stage-gate templates, each starting with its own columns;
- epics, stories, bugs, stages and milestones beside tasks and sub-tasks, with estimates and progress;
- an ordered backlog, sprints and sprint reports in Scrum projects;
- stages that each end in a gate named approvers must approve before the next stage can start;
- finish-to-start dependencies within a project, drawn on the timeline and flagged when late.

**Technical approach** (details in [research.md](./research.md)): the same Blazor Web App, modules and test
stack as Phases 1 and 2. Every change is additive, as Phase 1's research R26 planned:

- a `Projects.Template` column, shared with the Work module through the common kernel;
- new work item types, with `ContainerId`, `Estimate` and `SprintId` columns on `WorkItems`;
- new tables for sprints and their report log, stage gates with requests and decisions, and dependencies.

Sprints, gates and dependencies live in the Work module. The Projects module reaches them through one new
contract, `IProjectPlanSetup`. Scrum projects rank their cards across the whole project, so the backlog and
the board share one order. A per-project application lock, as Phase 1 already uses for administrators,
guards the rules that span several rows: one active sprint, gate decisions and dependency loops. The
timeline keeps its plain HTML bars and gains an SVG layer for dependency arrows. There are no new packages
and no new infrastructure.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS), as Phases 1 and 2

**Primary Dependencies**: unchanged (ASP.NET Core 10 Blazor Web App with Interactive Server, ASP.NET Core
Identity, EF Core 10 SQL Server provider, OpenTelemetry); no new packages

**Storage**: SQL Server 2022 or later; four additive migrations (templates and types; sprints; stage
gates; dependencies), with check constraints and filtered indexes

**Testing**: xUnit v3, bUnit, Testcontainers (SQL Server), Respawn, Playwright with axe-core, ArchUnitNET;
an upgrade test from the last Phase 2 migration; the opt-in performance suite extended

**Target Platform**: unchanged (Linux container or Windows Server/IIS; current Edge, Chrome, Firefox;
usable at 360 px)

**Project Type**: web application (the same single deployable modular monolith)

**Performance Goals**: p95 ≤ 1 s at 500,000 work items and 300 concurrent users for every Phase 1 and 2
action, plus:

- backlog loads (up to 1,000 open items), backlog reorders and sprint moves;
- sprint report and Stages view loads;
- gate requests and decisions, and dependency changes;
- timeline loads (up to 500 scheduled items and 200 dependencies).

**Constraints**:

- Phase 1 and 2 constraints hold: availability, RPO/RTO, WCAG 2.2 AA with keyboard alternatives, plain
  text, portability, and "not found" for non-members.
- The stage lock applies to everyone and is enforced on every path that changes a status (SC-005).
- The upgrade changes nothing visible in existing projects (SC-010).

**Scale/Scope**: as Phase 2; about 300 projects, of which a third may be Scrum and a fifth Stage-gate. The
phase adds three new screens (backlog, sprint report, stages) and changes seven, plus the project header
([contracts/ui-routes.md](./contracts/ui-routes.md)).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.1 (`.specify/memory/constitution.md`).

| # | Gate | Pre-research | Post-design evidence |
|---|------|:------------:|----------------------|
| I | Spec in `specs/004-…`, stories prioritized and independently testable, no technology in the spec; P1 alone is a usable increment | ✅ PASS | spec.md: 4 stories, checklist 16/16, nine clarifications recorded; US1 (templates and types) is usable alone |
| II | Test-first rules and services; every acceptance scenario automated; real SQL Server | ✅ PASS | R26: domain tests for types, sprints, the gate aggregate, the stage lock and loops; service tests per status path; upgrade test; Phase 3 matrix with an Approver column |
| III | Server-side authorization on every read and write based on role and membership; audit of security-relevant changes | ✅ PASS | R14–R17: sprint and stage management need `Manage`; decisions need a named approver; the stage lock is checked in services, not in the UI; approvers added as Viewers are audited as memberships; template switches and gate events are audited |
| IV | Append-only history for every change including links; optimistic concurrency; UTC; reviewed migrations | ✅ PASS | R5, R10, R11, R14, R18: history rows for type, container, estimate, sprint, dependencies and gates; append-only `SprintScopeEvents` and `GateDecisions`; row versions plus the planning lock (R9); four migrations |
| V | One app, one database, module boundaries; no unjustified dependencies; YAGNI | ✅ PASS | R1, R3, R16, R17: no new module or package; the Projects and Work modules talk through `IProjectPlanSetup`, `IProjectTeam.AddViewerAsync` and `IPlanningAuditLog`; no Gantt library, custom workflows, releases or notifications |
| VI | WCAG 2.2 AA; keyboard alternative for every drag; 360 px; feedback without losing input | ✅ PASS | R20–R23: "Move" menus on backlog rows, keyboard milestones, timeline row actions, "Move up/down" for stages; arrows hidden from assistive technology with the same facts in accessible names; axe at both widths |
| — | Platform standards (performance baseline, observability, configuration, portability) | ✅ PASS | R25: seeded Scrum and Stage-gate projects, new load scenarios and indexes; refusals keep log event 4030 |
| — | Workflow gates (CI, Definition of Done, traceability) | ✅ PASS | quickstart.md steps 3–5; README and quickstart updated per story |

**Result**: passes with no deviations. Re-checked after the design with the same outcome.

## Project Structure

### Documentation (this feature)

```text
specs/004-templates-and-planning/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-services.md
│   ├── permissions.md   # supersedes the Phase 2 matrix; read by PermissionMatrixTests
│   └── ui-routes.md
└── tasks.md             # /speckit-tasks output
```

### Source Code (additions and changes to the Phase 2 tree)

```text
src/
├── Upms.Domain/
│   ├── Common/ProjectTemplate.cs               # new (research R2)
│   ├── Identity/AuditEventType.cs              # + template and gate events
│   ├── Projects/Project.cs                     # + Template, columns per template, SwitchTemplate
│   └── Work/
│       ├── WorkItemType.cs, WorkItemTypes.cs   # + Epic, Story, Bug, Stage, Milestone; levels and rules (new)
│       ├── WorkItem.cs, WorkItemField.cs       # + ContainerId, Estimate, SprintId; Create, ChangeType, PlaceIn,
│       │                                       #   SetEstimate, JoinSprint; new history fields
│       ├── Sprint.cs, SprintScopeEvent.cs      # new
│       ├── StageGate.cs, GateRequest.cs, GateDecision.cs   # new: the gate aggregate
│       └── Dependency.cs, DependencyGraph.cs   # new: links and the loop check
├── Upms.Application/
│   ├── Identity/Contracts/IPlanningAuditLog.cs, Identity/PlanningAuditLog.cs   # new (research R17)
│   ├── Projects/Contracts/IProjectAccess.cs, IProjectWorkflow.cs   # + Template
│   ├── Projects/Contracts/IProjectTeam.cs      # + AddViewerAsync, TeamMemberInfo.IsActive
│   ├── Projects/ProjectService.cs, ProjectTeam.cs, BoardColumnService.cs   # templates, viewers, locked stages
│   ├── Work/Contracts/IProjectPlanSetup.cs     # new (research R3)
│   ├── Work/Contracts/IWorkItemStatusMover.cs  # + CountInLockedStagesAsync
│   ├── Work/ProjectPlanSetup.cs, IPlanLock.cs, StageLocks.cs   # new
│   ├── Work/CardRanker.cs, RankRebalancer.cs   # rank scope: column or project (research R8)
│   ├── Work/IWorkItemService.cs, WorkItemService.cs, WorkItemReads.cs   # types, containers, estimates, dependencies
│   ├── Work/IBoardService.cs, BoardService.cs  # cards only, Scrum board, type choice
│   ├── Work/IBacklogService.cs, BacklogService.cs          # new
│   ├── Work/ISprintService.cs, SprintService.cs            # new
│   ├── Work/IStageGateService.cs, StageGateService.cs      # new
│   ├── Work/IDependencyService.cs, DependencyService.cs    # new
│   ├── Work/ITimelineService.cs, TimelineService.cs        # groups, markers, sprint lane, arrows
│   ├── Work/IWorkItemListService.cs, WorkItemListService.cs   # type and container filters
│   ├── Work/IMyTasksService.cs, MyTasksService.cs          # every type, gates waiting
│   └── Work/WorkItemStatusMover.cs             # stage lock on column deletion
├── Upms.Infrastructure/
│   └── Persistence/
│       ├── Configurations/Projects/ProjectConfiguration.cs                    # Template
│       ├── Configurations/Work/WorkItemConfiguration.cs                       # columns, checks, indexes
│       ├── Configurations/Work/Sprint*, StageGate*, Gate*, Dependency*Configuration.cs   # new
│       ├── PlanLock.cs                         # new: per-project sp_getapplock (research R9)
│       └── Migrations/*_Templates, *_Sprints, *_StageGates, *_Dependencies.cs   # new
└── Upms.Web/
    ├── Components/Shared/ProjectHeader.razor   # tabs by template
    ├── Components/Shared/TypeIcon.razor        # new: type symbol with its name as text
    ├── Components/Pages/Projects/              # template in the create dialog and the list
    ├── Components/Pages/Board/                 # type choice, card type and estimate, Scrum sprint header
    ├── Components/Pages/Backlog/BacklogPage.razor, BacklogRow.razor, SprintSection.razor,
    │                               SprintDialogs.razor   # new
    ├── Components/Pages/Sprints/SprintReportPage.razor    # new
    ├── Components/Pages/Stages/StagesPage.razor, GatePanel.razor, GateSetupDialog.razor   # new
    ├── Components/Pages/List/                  # type and container columns and filters
    ├── Components/Pages/Timeline/              # groups, markers, sprint lane, arrows, row actions
    ├── Components/Pages/Drawer/                # type, container, estimate, sprint, ContainerItems.razor,
    │                                           #   DependencyList.razor, gate summary
    ├── Components/Pages/MyTasks/MyTasksPage.razor          # gates waiting for you
    ├── Components/Pages/Settings/ProjectSettings.razor     # Kanban/Scrum switch
    └── wwwroot/js/timeline.js                  # milestone dragging

tools/Upms.Seed/                                # Scrum and Stage-gate projects (research R25)
tests/
├── Upms.Domain.Tests/                          # types, sprints, gates, stage lock, loops
├── Upms.Application.Tests/                     # services per story, TemplatesUpgradeTests, Phase 3 matrix
├── Upms.Web.Tests/                             # backlog, stages, timeline markers and keys, drawer sections
├── Upms.E2E.Tests/                             # P3_US1–P3_US4 journeys + axe
├── Upms.Architecture.Tests/                    # unchanged rules cover the new contracts
└── Upms.Performance.Tests/                     # new actions in the SC-002 run
```

**Structure Decision**: no new projects or modules. The template belongs to the Projects module, which
owns the project and its columns. Types, containers, sprints, gates and dependencies belong to the Work
module, which owns work items (research R1). The Projects module reaches them only through
`Upms.Application.Work.Contracts`. The Work module asks about templates, teams and viewers only through
`Upms.Application.Projects.Contracts`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. The per-project planning lock (research R9) reuses the SQL Server application lock that
Phase 1 introduced for administrators; it adds no infrastructure.
