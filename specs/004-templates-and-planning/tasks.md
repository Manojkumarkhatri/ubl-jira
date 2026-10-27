---

description: "Task list for Phase 3: Templates and Planning (specs/004-templates-and-planning)"
---

# Tasks: Templates and Planning (Phase 3)

**Input**: Design documents from `specs/004-templates-and-planning/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable): tests for domain rules and
application services are written first and fail before implementation, and every acceptance scenario is
automated. Phase 3 test methods begin with `P3_` and the scenario they prove (for example
`P3_US3_AS2_LockedStageRefusesStart`), so they are not confused with the `US1_AS…` and `P2_US1_AS…` tests of
earlier phases.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4 of this spec)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): the existing tree (`src/Upms.Domain/`, `src/Upms.Application/`,
`src/Upms.Infrastructure/`, `src/Upms.Web/`, `tests/Upms.*.Tests/`, `tools/`, `docs/`), foldered by module
(Identity, Projects, Work). Contracts referenced below live in [contracts/](./contracts/); research decisions
are cited as R1–R26 of [research.md](./research.md).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Point the documentation at Phase 3 before code changes start

- [X] T001 [P] Add a "Phase 3 documents" table (spec, plan, research, data model, contracts, tasks, quickstart of `specs/004-templates-and-planning`) and a Phase 3 line under **Status** to `README.md`
- [X] T002 [P] Add a note at the top of `specs/003-project-views-and-team/contracts/permissions.md` that the matrix is superseded by `specs/004-templates-and-planning/contracts/permissions.md` once Phase 3 is installed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The template enum, the per-project planning lock, the planning audit contract and the template-aware
project header that every story uses

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 [P] Write `tests/Upms.Application.Tests/Work/PlanLockTests.cs`: a second transaction asking for the lock of the same project waits until the first commits, and one for another project does not wait; the lock is released on rollback; taking it outside a transaction throws (research R9)
- [X] T004 [P] Write `tests/Upms.Application.Tests/Identity/PlanningAuditLogTests.cs`: each `PlanningAuditEvent` is written as the matching `AuditEventType` (`ProjectTemplateChanged`, `GateSetupChanged`, `GateApprovalRequested`, `GateRequestWithdrawn`, `GateDecisionRecorded`, `GateApproved`) with the current user as actor, the given target and serialized details, in the caller's transaction (research R17)
- [X] T005 [P] Extend `tests/Upms.Web.Tests/Shared/ProjectHeaderTests.cs`: the template's name ("Kanban", "Scrum", "Stage-gate") is shown next to the project name (FR-006); Board, List and Timeline tabs for every template, with `aria-current="page"` on the current one
- [X] T006 Create `src/Upms.Domain/Common/ProjectTemplate.cs` (`Kanban`, `Scrum`, `StageGate`), documented as shared by the Projects and Work modules like `StatusCategory` (research R2)
- [X] T007 Create `src/Upms.Application/Work/IPlanLock.cs` and `src/Upms.Infrastructure/Persistence/PlanLock.cs`: `sp_getapplock` on resource `upms:plan:{projectId}`, `@LockOwner = N'Transaction'`, `@LockTimeout = 15000`, failing when the lock is not granted, as `src/Upms.Infrastructure/Identity/AdministratorLock.cs` does; register it in `src/Upms.Infrastructure/InfrastructureServiceCollectionExtensions.cs` (make T003 pass)
- [X] T008 [P] Add the six values to `src/Upms.Domain/Identity/AuditEventType.cs`, and create `src/Upms.Application/Identity/Contracts/IPlanningAuditLog.cs` (with `PlanningAuditEvent`) and `src/Upms.Application/Identity/PlanningAuditLog.cs` mapping to `IAuditLog`, like `MembershipAuditLog`; register it in `src/Upms.Application/ApplicationServiceCollectionExtensions.cs` (make T004 pass)
- [X] T009 Give `src/Upms.Web/Components/Shared/ProjectHeader.razor` a `Template` parameter (default `Kanban`) that shows the template's name next to the project name and builds the view tabs from a list, so later stories add Backlog (Scrum) and Stages (Stage-gate) (make T005 pass)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Choose a template and organize work by type (Priority: P1) 🎯 MVP

**Goal**: Kanban, Scrum and Stage-gate templates with their own starting columns; epics, stories, bugs,
stages and milestones beside tasks and sub-tasks; containers with progress; estimates; existing projects
unchanged as Kanban.

**Independent Test**: Create Scrum project `MOB` and Stage-gate project `CBU` with their columns (and
`CBU`'s four stages); in `MOB` build epic "Card refunds" with a story and a bug, change the bug to a story,
complete one: "1 of 2 done (50%)"; in `CBU` add a task and milestone "Planning sign-off" to Planning; `WEB`
is Kanban and unchanged (spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T010 [P] [US1] Write `tests/Upms.Domain.Tests/Projects/ProjectTemplateTests.cs`: `Project.Create` seeds the columns of data-model.md per template (Kanban "To Do (to do), In Progress (in progress), Done (done)"; Scrum adds "In Review (in progress)" before Done; Stage-gate "Not Started (to do), In Progress (in progress), On Hold (in progress), Completed (done)"); `SwitchTemplate` allows Kanban→Scrum and Scrum→Kanban when `hasOpenSprints` is false, refuses Scrum→Kanban with open sprints (`SprintsOpen`) and any switch to or from Stage-gate (`TemplateSwitchNotAllowed`), keeps the columns and increments `DetailsVersion` (FR-002, FR-005)
- [X] T011 [P] [US1] Write `tests/Upms.Domain.Tests/Work/WorkItemTypesTests.cs`: the type table of data-model.md (level, templates, card, estimate, allowed changes); `WorkItem.Create` refuses a type the template does not offer (`TypeNotAllowed`); `ChangeType` records a `Type` history row for Story↔Task↔Bug in agile projects and refuses every other change (`TypeChangeNotAllowed`); `PlaceIn` accepts only a live epic or stage of the same project for a standard item and records `Container` with old and new "KEY Title" (`InvalidContainer` otherwise, including for containers and sub-tasks); `SetEstimate` accepts "0 to 999.9" with at most one decimal place, records `Estimate`, and is refused for containers, milestones, sub-tasks and Stage-gate tasks; a milestone refuses a start date (`InvalidDates`), an estimate and sub-tasks (`NoSubtasks`); `AddSubtask` refuses epics and stages (`NoSubtasks`) (FR-007–FR-012, FR-014, FR-015)
- [X] T012 [P] [US1] Extend `tests/Upms.Application.Tests/Projects/ProjectServiceTests.cs`: `P3_US1_AS1` `CreateAsync` with each template; `P3_US1_AS2` Scrum columns, and a Stage-gate project with stages Initiation, Planning, Execution and Closure as work items `KEY-1` to `KEY-4` in that order; `ProjectSummary` and `ProjectDetails` carry `Template` (FR-006); `P3_US1_AS9` `SwitchTemplateAsync` Kanban→Scrum orders the open cards column by column, keeping each column's order, writes `ProjectTemplateChanged` with from and to, returns `Conflict` on a stale `DetailsVersion`, `TemplateSwitchNotAllowed` for Stage-gate, and `Forbidden` for Members (FR-001–FR-006)
- [X] T013 [P] [US1] Write `tests/Upms.Application.Tests/Work/WorkItemTypesServiceTests.cs`: `P3_US1_AS4` `IWorkItemService.CreateAsync` makes epic `MOB-1` in the leftmost "to do" column and the board does not show it; `P3_US1_AS5` putting a story and a bug in the epic through `WorkItemEdit.Container` and completing the story gives the epic's details `Progress(1, 2)` and its items; `P3_US1_AS6` `WorkItemEdit.Type` Bug→Story with history, and `TypeChangeNotAllowed` for an epic; `P3_US1_AS7` a milestone in stage Planning dated 30 October with no start date, refused sub-tasks and not on the board; `P3_US1_AS10` deleting the epic keeps its items with `ContainerId` cleared and a `Container` history row each; moving a container with open items to a "done" column warns with their keys (FR-018); `WorkItemEdit.Estimate`; a Member creating a stage gets `Forbidden`; `ListContainersAsync`; a stale version returns `Conflict` (FR-010–FR-019, FR-053, FR-054)
- [X] T014 [P] [US1] Write `tests/Upms.Application.Tests/Work/ScrumRankTests.cs`: in a Scrum project a card created in any column is placed after the project's largest key; moving before a card uses the nearest smaller key in the whole project, so no two keys are equal after interleaved moves; the rebalancer renumbers a whole Scrum project and keeps the order; `PrepareForScrumAsync` gives a switched project one order column by column (deleted cards included) and records no history; a card restored in a Scrum project goes to the bottom; Kanban projects keep per-column ranks (research R8)
- [X] T015 [P] [US1] Extend `tests/Upms.Application.Tests/Work/WorkItemListServiceTests.cs`: `P3_US1_AS8` every type is listed; filters `Types`, `ContainerKeys` and `NoContainer` combine with the Phase 2 filters; rows carry `Type`, `Container` and `Estimate`; sorting by `ListSort.Type` (FR-020)
- [X] T016 [P] [US1] Extend `tests/Upms.Application.Tests/Work/BoardQueryTests.cs` and `tests/Upms.Application.Tests/Work/InlineCreateTests.cs`: boards show only stories, tasks and bugs (not epics, stages, milestones); cards carry `Type` and `Estimate`; `CreateInlineAsync` with a type creates it (Story, Task, Bug in agile projects; Task only in Stage-gate, `TypeNotAllowed` otherwise); `BoardView.Template` (FR-009, FR-010, FR-013)
- [X] T017 [P] [US1] Extend `tests/Upms.Application.Tests/Work/MyTasksServiceTests.cs`: open assigned items of every type are listed with their `Type` (FR-041, Phase 2 FR-025)
- [X] T018 [P] [US1] Write `tests/Upms.Application.Tests/Projects/TemplatesUpgradeTests.cs` (own database): migrate to the last Phase 2 migration, insert pilot-style data with SQL (a project with customized columns, tasks, sub-tasks, a deleted task, assignees, dates, members and history), apply the Phase 3 migrations, then assert `P3_US1_AS3`: the project's template is `Kanban`, its items are `Task` and `Subtask` with no container, estimate or sprint, and its columns, keys, members, assignees, dates and history are unchanged (FR-004, SC-010)
- [X] T019 [P] [US1] Update `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs` to read `specs/004-templates-and-planning/contracts/permissions.md` with the columns Anonymous, Non-member, Viewer, Approver, Member, Creator, Project Admin, Admin; the world gains Scrum project `MOB` and Stage-gate project `CBU` with the same team; operations for every row that exists after US1 (creating a project with a template, switching Kanban and Scrum, creating epics and milestones, editing type, estimate and container); the rows for sprints, stages, gates and dependencies are listed in explicit `PendingUntilUs2`, `PendingUntilUs3` and `PendingUntilUs4` sets whose cells are reported as skipped until T059, T079 and T093 fill them (SC-005)
- [X] T020 [P] [US1] Extend `tests/Upms.Web.Tests/Projects/CreateProjectDialogTests.cs` and `tests/Upms.Web.Tests/MyTasks/MyTasksPageTests.cs`, and add `tests/Upms.Web.Tests/Projects/ProjectListTests.cs`: the dialog's template radio group (Kanban preselected, one line on each, keyboard operable) sends the chosen template; the project list shows a "Template" column; "My tasks" rows show the type symbol with its name as text (FR-001, FR-006, FR-009)
- [X] T021 [P] [US1] Write `tests/Upms.Web.Tests/Drawer/TypeAndContainerTests.cs`: the type choice offers only `TypeChoices` and saves `WorkItemEdit.Type`; the "Epic" or "Stage" choice lists `ListContainersAsync` and "None"; the estimate field saves one decimal and shows the validation message while keeping the input; a container's drawer shows its items with progress ("1 of 2 done (50%)") and an "Add item" box; history lines for type, container and estimate changes; all read-only without `CanContribute`
- [X] T022 [P] [US1] Extend `tests/Upms.Web.Tests/Board/BoardPageTests.cs`, `tests/Upms.Web.Tests/Board/InlineCreateTests.cs`, `tests/Upms.Web.Tests/List/ListStateTests.cs` and `tests/Upms.Web.Tests/List/ProjectListViewTests.cs`: cards show the type symbol with its name as text and the estimate; the create box's type choice; the list's type and "Epic or stage" columns and filters, their chips, and `type` and `container` (keys or `none`) in the address, round-tripped by `ListState` (FR-009, FR-010, FR-020)
- [X] T023 [P] [US1] Extend `tests/Upms.Web.Tests/Settings/` with `ProjectSettingsTemplateTests.cs`: Project Admins of Kanban and Scrum projects see the switch with a confirmation that explains the effect; refusals (`SprintsOpen`, `Conflict`) are explained; Stage-gate projects and Members see no switch (FR-005)
- [ ] T024 [P] [US1] Write `tests/Upms.E2E.Tests/P3_US1_TemplatesAndTypesTests.cs`: the US1 Independent Test end to end, plus axe scans of the create-project dialog, the board with type symbols, the list with the new filters and the drawer of an epic at 1280 px and 360 px

### Implementation for User Story 1

- [X] T025 [US1] Extend `src/Upms.Domain/Projects/Project.cs` with `Template` "varchar(10); `Kanban`, `Scrum` or `StageGate`; default `Kanban`", a template argument in `Create` that seeds the columns of data-model.md, and `SwitchTemplate(target, hasOpenSprints, now)` with the codes `TemplateSwitchNotAllowed` and `SprintsOpen` (make T010 pass)
- [X] T026 [P] [US1] Add `Epic`, `Story`, `Bug`, `Stage` and `Milestone` to `src/Upms.Domain/Work/WorkItemType.cs` and create `src/Upms.Domain/Work/WorkItemTypes.cs` with each type's level, templates, card flag and allowed changes (research R4)
- [X] T027 [US1] Extend `src/Upms.Domain/Work/WorkItem.cs`: `ContainerId` "only on standard items; must be a live epic or stage of the same project", `Estimate` "0 to 999.9; only on stories, tasks and bugs of agile projects", `Create` for every type (keeping `CreateTask` as a wrapper), `ChangeType`, `PlaceIn`, `SetEstimate`, the milestone rules and `NoSubtasks`; add `Type`, `Container` and `Estimate` to `src/Upms.Domain/Work/WorkItemField.cs` (make T011 pass)
- [X] T028 [US1] Update `src/Upms.Infrastructure/Persistence/Configurations/Projects/ProjectConfiguration.cs` (`Template` varchar(10), default `Kanban`) and `src/Upms.Infrastructure/Persistence/Configurations/Work/WorkItemConfiguration.cs` (`ContainerId` FK restrict, `Estimate` decimal(4,1), `CK_WorkItems_Estimate` "Estimate IS NULL OR Estimate BETWEEN 0 AND 999.9", `CK_WorkItems_Milestone` "Type <> 'Milestone' OR (StartDate IS NULL AND Estimate IS NULL AND ParentId IS NULL)", `IX_WorkItems_Board` filtered to "Type IN ('Task','Story','Bug')" including `Type` and `Estimate`, `IX_WorkItems_Container_Live`, and the new included columns of `IX_WorkItems_Project_Live` and `IX_WorkItems_Assignee_Live`); create migration `TemplatesAndTypes` in `src/Upms.Infrastructure/Persistence/Migrations/` (make T018 pass)
- [X] T029 [US1] Add `TypeNotAllowed`, `TypeChangeNotAllowed`, `InvalidContainer`, `NoSubtasks` and `TemplateSwitchNotAllowed` to `src/Upms.Application/Common/Results/ErrorCodes.cs`; add `Template` to `ProjectAccessInfo` (`src/Upms.Application/Projects/Contracts/IProjectAccess.cs`, `ProjectAccess.cs`) and to `BoardInfo` (`src/Upms.Application/Projects/Contracts/IProjectWorkflow.cs`, `ProjectWorkflow.cs`)
- [X] T030 [US1] Give `src/Upms.Application/Work/CardRanker.cs` a rank scope (the column in Kanban and Stage-gate projects, the whole project in Scrum projects) that always places between a key and its nearest smaller key in the scope, filter "top level" to cards (`Type IN (Task, Story, Bug)`), make `src/Upms.Application/Work/RankRebalancer.cs` renumber whole Scrum projects, and give cards restored in Scrum projects a key at the bottom (make T014 pass, except `PrepareForScrumAsync`)
- [X] T031 [US1] Create `src/Upms.Application/Work/Contracts/IProjectPlanSetup.cs` and `src/Upms.Application/Work/ProjectPlanSetup.cs`: `CreateDefaultStagesAsync` creates the stages Initiation, Planning, Execution and Closure in the leftmost "to do" column with ranks in that order; `HasOpenSprintsAsync` returns false until T055; `PrepareForScrumAsync` renumbers the project's cards column by column, deleted ones included, without history (research R3, R8); register it
- [X] T032 [US1] Update `src/Upms.Application/Projects/IProjectService.cs` and `ProjectService.cs`: `CreateAsync` takes the template and calls `CreateDefaultStagesAsync` for Stage-gate in the same transaction; new `SwitchTemplateAsync` (Manage) calls `HasOpenSprintsAsync` and `PrepareForScrumAsync` and writes `TemplateChanged` through `IPlanningAuditLog`; `ProjectSummary` and `ProjectDetails` gain `Template` (make T012 pass)
- [X] T033 [US1] Extend `src/Upms.Application/Work/IWorkItemService.cs`, `WorkItemService.cs` and `WorkItemReads.cs`: `CreateAsync(projectKey, NewWorkItem)` (stages need Manage and get a rank after the last stage; epics and milestones get the rank `a0`), `ListContainersAsync`, the edits `Type`, `Container` and `Estimate`, the container warning of FR-018, the details additions of contracts/application-services.md (`Template`, `TypeChoices`, `Container`, `Estimate`, `AsContainer` with `Progress` and items), `NoSubtasks` in `AddSubtaskAsync`, and container deletion releasing its items with history (make T013 pass)
- [X] T034 [US1] Update `src/Upms.Application/Work/IBoardService.cs` and `BoardService.cs`: cards only, `CardView.Type` and `Estimate`, `BoardView.Template`, and `CreateInlineAsync` with a type (make T016 pass)
- [X] T035 [US1] Update `src/Upms.Application/Work/IWorkItemListService.cs` and `WorkItemListService.cs` (every type; `Types`, `ContainerKeys`, `NoContainer` filters; `Type`, `Container`, `Estimate` columns; `ListSort.Type`; `WorkItemListView.Template`) and `src/Upms.Application/Work/IMyTasksService.cs` and `MyTasksService.cs` (`MyTaskRow.Type`, every type); add `Template` to `TimelineView` (`ITimelineService.cs`, `TimelineService.cs`) and to `TeamView` (`src/Upms.Application/Projects/IProjectMemberService.cs`, `ProjectMemberService.cs`) for the project header (make T015 and T017 pass)
- [X] T036 [P] [US1] Create `src/Upms.Web/Components/Shared/TypeIcon.razor` (a symbol per type with the type's name as text) and show it on the rows of `src/Upms.Web/Components/Pages/MyTasks/MyTasksPage.razor` (FR-009); add the template radio group to `src/Upms.Web/Components/Pages/Projects/CreateProjectDialog.razor` and the "Template" column to `ProjectList.razor` (make T020 pass)
- [X] T037 [US1] Pass the project's template from each view model to `ProjectHeader` in `src/Upms.Web/Components/Pages/Board/BoardPage.razor`, `List/ProjectListView.razor`, `Timeline/TimelinePage.razor`, `Members/MembersPage.razor` and `Settings/ProjectSettings.razor`
- [X] T038 [US1] Show `TypeIcon` and the estimate in `src/Upms.Web/Components/Pages/Board/TaskCard.razor`, and add the type choice (default Story in Scrum projects, Task otherwise) to `src/Upms.Web/Components/Pages/Board/InlineCreate.razor` (make the board part of T022 pass)
- [X] T039 [US1] Add `type` and `container` to `src/Upms.Web/Components/Pages/List/ListState.cs`, and the type and "Epic or stage" columns, filters, chips and a type choice in "What needs to be done?" to `src/Upms.Web/Components/Pages/List/ProjectListView.razor`, building on the last change asked for as Phase 2 does (make the list part of T022 pass)
- [X] T040 [US1] Add the type choice, "Epic" or "Stage" choice, estimate field and history labels to `src/Upms.Web/Components/Pages/Drawer/TaskDrawer.razor` and `HistoryList.razor`, and create `src/Upms.Web/Components/Pages/Drawer/ContainerItems.razor` (items, progress, "Add item") (make T021 pass)
- [X] T041 [US1] Add the Kanban/Scrum switch with its confirmation to `src/Upms.Web/Components/Pages/Settings/ProjectSettings.razor` (make T023 pass)
- [X] T042 [US1] Seed templates in `tools/Upms.Seed/Seeder.cs` (about a third of projects Scrum with epics, stories, bugs and estimates; a fifth Stage-gate with their four stages, tasks placed in stages and milestones), and add a type filter to the list loads measured by `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R25)
- [ ] T043 [US1] Make T019 and T024 pass end to end, update the Phase 1 and 2 tests that create projects or tasks through the changed signatures (`CreateAsync` with a template, `CreateInlineAsync` with a type) in the Application, Web and E2E suites, and add templates and work item types to the Phase 3 line of `README.md`

**Checkpoint**: User Story 1 is fully functional and testable on its own. This is the MVP: teams choose a
template and organize work by type.

---

## Phase 4: User Story 2 - Plan and run sprints (Priority: P2)

**Goal**: An ordered backlog with estimates, sprints managed by Project Admins, a Scrum board that shows the
active sprint, and sprint reports.

**Independent Test**: In `MOB` with 15 estimated items, reorder the backlog, create "MOB Sprint 1", move 6
items in (5 by mouse, 1 by keyboard), start it for two weeks; the board shows those 6; complete 4, add 1,
complete the sprint sending 3 to the backlog; the report shows 6 committed, 1 added, 4 completed, 3 not
completed with totals (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T044 [P] [US2] Write `tests/Upms.Domain.Tests/Work/SprintTests.cs`: a new sprint is `Planned` named "<key> Sprint <n>"; names are "required, 1–60 characters" and goals at most 500 characters; `Start` needs `Planned`, dates in "2000–2099; end not before start", and defaults the end to 14 days after the start; `Complete` needs `Active`; only `Planned` sprints can be deleted; a `Completed` sprint refuses every change (`SprintCompleted`); `WorkItem.JoinSprint` records a `Sprint` history row with the sprint names, only on cards, and is refused for sub-tasks, epics and completed sprints (FR-023–FR-025, FR-028, FR-030)
- [X] T045 [P] [US2] Write `tests/Upms.Application.Tests/Work/BacklogServiceTests.cs`: `P3_US2_AS1` the view lists the active and planned sprints with items, counts, estimate totals and unestimated counts (completed and remaining totals for the active sprint), then the backlog of open cards with no sprint or a completed one, by rank, 100 at a time, without epics or sub-tasks; `P3_US2_AS2` `MoveAsync` with Top, Bottom, Up, Down and Before keeps one order that the board uses; `P3_US2_AS3` moving into and out of sprints (Keep); `P3_US2_AS4` a story's sub-tasks follow it and a sub-task cannot be moved; an item reopened after its sprint completed is back in the backlog; `P3_US1_AS9` (completed here, where the backlog exists) a Kanban project switched to Scrum lists its open cards in the backlog column by column, keeping each column's order; `SprintCompleted` for completed sprints; a stale version returns `Conflict`; `P3_US2_AS10` Viewers get `Forbidden` and Members may move (FR-021, FR-022, FR-024, FR-026)
- [X] T046 [P] [US2] Write `tests/Upms.Application.Tests/Work/SprintServiceTests.cs`: `P3_US2_AS3` `CreateAsync` names "MOB Sprint 1", then "MOB Sprint 2"; `P3_US2_AS5` `StartAsync` with 5 October and a goal gives 19 October and writes a `Committed` event per card with its estimate; `P3_US2_AS6` a second start is refused (`SprintAlreadyActive`), also when two starts race; `P3_US2_AS7` `CompleteAsync` moves open cards to the backlog or a planned sprint with history, keeps done cards and writes `Completed` and `NotCompleted` events; `P3_US2_AS8` `GetReportAsync` groups committed, added, removed (including a deleted card), completed and not completed with totals, and does not change after later edits; rename, goal and delete rules; `P3_US2_AS10` Members, Viewers and non-members are refused (`Forbidden`, `NotFound`); stale versions return `Conflict` (FR-023, FR-025, FR-028, FR-029)
- [X] T047 [P] [US2] Extend `tests/Upms.Application.Tests/Work/BoardQueryTests.cs`, `tests/Upms.Application.Tests/Work/DeleteRestoreTests.cs` and `tests/Upms.Application.Tests/Projects/ProjectServiceTests.cs`: `P3_US2_AS9` a Scrum board shows the active sprint's cards with every done card and `Sprint` is null without one; a card created on a Scrum board joins the active sprint with an `Added` event; deleting a card in an active sprint clears its sprint and writes `Removed`, and restoring it returns it to the backlog; switching Scrum→Kanban with an active or planned sprint returns `SprintsOpen` (FR-005, FR-027)
- [X] T048 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/Backlog/BacklogPageTests.cs`: sections for the active sprint, planned sprints and backlog with rows (key, type, title, epic, assignee, estimate, status) and totals; the **Move** menu's items and announcements with focus kept on the moved row; dragging sends `Before`; "Show more"; "What needs to be done?" at the end of the backlog; sprint actions and dialogs (create, edit, start with the proposed end date, complete with a destination, delete) only for Project Admins; empty states; read-only for Viewers
- [X] T049 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/Sprints/SprintReportPageTests.cs` and extend `tests/Upms.Web.Tests/Board/BoardPageTests.cs` and `tests/Upms.Web.Tests/Drawer/TaskDrawerTests.cs`: the report's five tables with totals and deleted items marked; the Scrum board's sprint name, goal and dates, "Complete sprint" for Project Admins, and the empty state linking to the backlog; the drawer's read-only sprint; the header's Backlog tab for Scrum projects only
- [ ] T050 [P] [US2] Write `tests/Upms.E2E.Tests/P3_US2_SprintsTests.cs`: the US2 Independent Test end to end, plus axe scans of the backlog, the sprint dialogs, the Scrum board and the report at 1280 px and 360 px

### Implementation for User Story 2

- [X] T051 [US2] Create `src/Upms.Domain/Work/Sprint.cs` (fields of data-model.md; `Name` "nvarchar(60), required, 1–60 characters; default "<key> Sprint <n>"", `Goal` "nvarchar(500) null", `State` `Planned`/`Active`/`Completed`) and `src/Upms.Domain/Work/SprintScopeEvent.cs` (`Kind` `Committed`, `Added`, `Removed`, `Completed`, `NotCompleted`; `Estimate` "the item's estimate at that moment"); add `SprintId` and `JoinSprint` to `src/Upms.Domain/Work/WorkItem.cs` and `Sprint` to `WorkItemField.cs` (make T044 pass)
- [X] T052 [US2] Add `src/Upms.Infrastructure/Persistence/Configurations/Work/SprintConfiguration.cs` (unique `(ProjectId, Number)`, unique filtered `(ProjectId) WHERE State = 'Active'`, check `EndDate >= StartDate`, row version) and `SprintScopeEventConfiguration.cs` (index `(SprintId, Kind)`), `SprintId` FK and the indexes `IX_WorkItems_Backlog` ("ProjectId, Rank; live, top-level, cards", including `ResolvedAt`, so it also serves the Scrum ranker) and `IX_WorkItems_Sprint` of data-model.md in `WorkItemConfiguration.cs` (with `SprintId` added to `IX_WorkItems_Board`), `DbSet`s in `IAppDbContext` and `AppDbContext`; create migration `Sprints` whose `Up` also makes `SprintScopeEvents` append-only with `AppendOnlyTable.CreateTrigger`
- [X] T053 [US2] Add `SprintsOpen`, `SprintAlreadyActive` and `SprintCompleted` to `ErrorCodes.cs`; implement `src/Upms.Application/Work/ISprintService.cs` and `SprintService.cs` (Manage; the planning lock for create, start, complete and delete; scope events; the report), registered in `ApplicationServiceCollectionExtensions.cs` and as operation-scoped in `src/Upms.Web/Security/WebServiceCollectionExtensions.cs` (make T046 pass)
- [X] T054 [US2] Implement `src/Upms.Application/Work/IBacklogService.cs` and `BacklogService.cs` (the view with 100-item pages; `MoveAsync` with `BacklogTarget` and `BacklogPlacement` through the project-scoped ranker; `Added` and `Removed` events for the active sprint), registered as above (make T045 pass)
- [X] T055 [US2] Make `BoardService` show the active sprint on Scrum boards with `ActiveSprintInfo` and `CanManageSprints` and add inline-created cards to it; make `WorkItemService.DeleteAsync` and `RestoreAsync` apply the sprint rules of data-model.md; make `ProjectPlanSetup.HasOpenSprintsAsync` read sprints; add `SprintRef` to the drawer details (make T047 pass)
- [X] T056 [US2] Create `src/Upms.Web/Components/Pages/Backlog/BacklogPage.razor` at `/projects/{key}/backlog` with `BacklogRow.razor`, `SprintSection.razor` and `SprintDialogs.razor`: sections, the **Move** menu, native drag and drop, "Show more", the create box, sprint dialogs for Project Admins, announcements through the live region, and one save at a time as the Phase 2 pages do; add the Backlog tab for Scrum projects to `ProjectHeader` (make T048 pass)
- [X] T057 [US2] Create `src/Upms.Web/Components/Pages/Sprints/SprintReportPage.razor` at `/projects/{key}/sprints/{number}`; add the sprint header, "Complete sprint" and the empty state to `src/Upms.Web/Components/Pages/Board/BoardPage.razor`; show the sprint in `TaskDrawer.razor` (make T049 pass)
- [X] T058 [US2] Seed sprints in `tools/Upms.Seed/Seeder.cs` (three completed, one active and one planned per Scrum project, with scope events), and add backlog loads, backlog reorders, sprint moves and report loads to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R25)
- [ ] T059 [US2] Fill the `PendingUntilUs2` rows of `PermissionMatrixTests` (reorder the backlog and move cards; create, rename, start, complete and delete sprints), add `Sprint` and `SprintScopeEvent` to `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs`, make T050 pass end to end, and add the backlog and sprints to the Phase 3 line of `README.md`

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Pass stage gates (Priority: P3)

**Goal**: Stages in order, each ending in a gate that named approvers from anywhere in the organization
approve or send back; locked stages whose work cannot start; "Gates waiting for you" on "My tasks"; every
step in the history and the audit log.

**Independent Test**: In `CBU`, name farah (from outside the project) and omar as approvers of the Initiation
gate; a Planning task cannot start; request approval; farah approves, omar sends it back with a note;
request again, both approve; Planning unlocks, Initiation can be completed, and the history and audit log
show each step (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T060 [P] [US3] Write `tests/Upms.Domain.Tests/Work/StageGateTests.cs`: the state diagram of data-model.md; setup changes only in `NotRequested` or `SentBack` (`GateRequestPending`, `GateApproved`); `RequiredApprovals` "null means all approvers; otherwise 1 to the number of approvers"; a request needs the current stage (`StageLocked`), approvers (`NoApprovers`), every approver able to decide (`ApproverCannotDecide`) and enough approvers besides an excluded requester (`NotEnoughApprovers`); decisions from approvers only, not from the excluded requester, once per request (`AlreadyDecided`), with a note "required for `SendBack`" of at most 1,000 characters; approval when the approvals reach the number needed; one "send back" ends the request; withdrawal clears the request's decisions from the count; an approved gate never changes (FR-035–FR-038)
- [X] T061 [P] [US3] Write `tests/Upms.Domain.Tests/Work/StageOrderTests.cs`: from stages ordered by `(Rank, Id)` and their gate states, the current stage is the first not approved and later stages are locked; all approved means none locked; new stages may go only after the current stage; approved stages and the current stage cannot move (`StageOrderFixed`); locked stages move among themselves (FR-031, FR-032, FR-039)
- [X] T062 [P] [US3] Write `tests/Upms.Application.Tests/Work/StageGateServiceTests.cs`: `P3_US3_AS1` `SetUpGateAsync` adds farah (not a member) as a Viewer with a `MemberAdded` audit event, and the view shows Initiation current and the rest locked; `P3_US3_AS3` `PreviewRequestAsync` lists open items and the request appears for both approvers; `P3_US3_AS4` two approvals needed, then one needed; `P3_US3_AS5` a note is required to send back; `P3_US3_AS6` Project Admins and administrators who are not approvers get `Forbidden`; `P3_US3_AS8` changing approvers while pending is refused, and withdrawing keeps earlier decisions in the history; `P3_US3_AS9` an approved gate refuses withdrawal and changes; `P3_US3_AS10` every step is in the stage's history and the audit log; `P3_US3_AS11` the requester exclusion; two approvers deciding at the same moment are both accepted; a decision on a withdrawn request returns `Conflict` with the current gate; adding, moving and deleting stages (`StageOrderFixed`, `GateApproved`, deleting the current stage cancels its request); a stage created from the list's create box gets a gate and goes after the last stage; restoring a deleted stage puts it after the last stage with its gate `NotRequested`, never locking started work; Members cannot manage stages or gates (FR-031, FR-035–FR-040)
- [X] T063 [P] [US3] Write `tests/Upms.Application.Tests/Work/StageLockTests.cs`: `P3_US3_AS2` a task (and its sub-task) in locked Planning cannot leave a "to do" column through the board, the drawer's status or "mark done", for Members and administrators alike (`StageLocked` naming the Initiation gate), while title, assignee and dates can still change; a started item cannot be put into a locked stage (FR-033); `P3_US3_AS7` a stage cannot move into a "done" column before its gate is approved (`GateNotApproved`) and can afterwards; items in no stage are never locked (FR-032–FR-034)
- [X] T064 [P] [US3] Extend `tests/Upms.Application.Tests/Projects/ProjectMemberServiceTests.cs` and `tests/Upms.Application.Tests/Work/MyTasksServiceTests.cs`: `IProjectTeam.AddViewerAsync` adds an active non-member as Viewer with `MemberAdded` and returns false for a member; `TeamMemberInfo.IsActive`; `ListGatesAsync` lists pending requests awaiting the caller's decision, not those they decided, not an excluded requester's own, and not in projects they can no longer see (FR-041)
- [X] T065 [P] [US3] Extend `tests/Upms.Application.Tests/Projects/BoardColumnServiceTests.cs`: deleting a column that holds items of a locked stage is refused with `StageLocked` unless their destination is a "to do" column (FR-033)
- [X] T066 [P] [US3] Write bUnit tests `tests/Upms.Web.Tests/Stages/StagesPageTests.cs`: stages with dates, progress and place labels; the gate panel with state, approvers, decisions and "N of M approvals needed"; the setup dialog (people search across the organization, number needed, exclusion checkbox) for Project Admins only; "Request approval" confirming open items; "Withdraw request"; "Approve" and "Send back" for approvers, with the note required; "Move up" and "Move down" on locked stages with focus following; `?stage=` focuses a stage; conflicts reload the view; read-only for Viewers who are not approvers; the header's Stages tab for Stage-gate projects only
- [X] T067 [P] [US3] Extend `tests/Upms.Web.Tests/MyTasks/MyTasksPageTests.cs` and `tests/Upms.Web.Tests/Drawer/TaskDrawerTests.cs`: "Gates waiting for you" with links to `/projects/{key}/stages?stage={KEY-N}`; the drawer's locked-stage notice naming the gate, the gate summary on a stage with a link to the Stages view, and history labels for the gate events
- [ ] T068 [P] [US3] Write `tests/Upms.E2E.Tests/P3_US3_StageGatesTests.cs`: the US3 Independent Test end to end with farah, omar, amina and sara, plus axe scans of the Stages view, the setup and send-back dialogs and "My tasks" at 1280 px and 360 px

### Implementation for User Story 3

- [X] T069 [US3] Create `src/Upms.Domain/Work/StageGate.cs`, `GateRequest.cs` and `GateDecision.cs` (the aggregate of research R14; `State` `NotRequested`/`AwaitingDecision`/`Approved`/`SentBack`; `Outcome` `Pending`/`Approved`/`SentBack`/`Withdrawn`/`Cancelled`; `Decision` `Approve`/`SendBack`; `Note` "nvarchar(1000) null; required for `SendBack`") and `src/Upms.Domain/Work/StageOrder.cs`; add `GateSetup`, `GateRequested`, `GateWithdrawn`, `GateDecision` and `GateApproved` to `WorkItemField.cs` (make T060 and T061 pass)
- [X] T070 [US3] Add `StageGateConfiguration.cs` (`StageId` unique, `ExcludeRequester` default 1, row version), `GateApproverConfiguration.cs` (key `(GateId, UserId)`), `GateRequestConfiguration.cs` (unique `(GateId, Number)`, `IX_GateRequests_Pending` filtered on `Outcome = 'Pending'`) and `GateDecisionConfiguration.cs` (unique `(RequestId, ApproverId)`) in `src/Upms.Infrastructure/Persistence/Configurations/Work/`, `DbSet`s, and migration `StageGates` whose `Up` makes `GateDecisions` append-only and inserts a `NotRequested` gate for every existing stage
- [X] T071 [US3] Add `AddViewerAsync` and `TeamMemberInfo.IsActive` to `src/Upms.Application/Projects/Contracts/IProjectTeam.cs` and `src/Upms.Application/Projects/ProjectTeam.cs` (Viewer membership, `MembersVersion` + 1, `MemberAdded` through `IMembershipAuditLog`, `AccountDeactivated` for inactive accounts) (make the team part of T064 pass)
- [X] T072 [US3] Create `src/Upms.Application/Work/StageLocks.cs` and use it on every status path: `BoardService.MoveCardAsync`, `WorkItemService` (status edit, `MarkSubtaskDoneAsync`, container edit), and `WorkItemStatusMover`, which gains `CountInLockedStagesAsync` in `src/Upms.Application/Work/Contracts/IWorkItemStatusMover.cs` for `src/Upms.Application/Projects/BoardColumnService.cs`; add `StageLocked` and `GateNotApproved` to `ErrorCodes.cs` (make T063 and T065 pass)
- [X] T073 [US3] Implement `src/Upms.Application/Work/IStageGateService.cs` and `StageGateService.cs` (the view; stage add and move; people search through `IUserDirectory.SearchActiveAsync`; gate setup adding non-members with `AddViewerAsync`; preview, request, withdraw and decide under the planning lock; history rows and `IPlanningAuditLog` events), add the remaining gate codes of contracts/application-services.md to `ErrorCodes.cs`, create a gate on every path that creates a stage (`ProjectPlanSetup.CreateDefaultStagesAsync`, `AddStageAsync`, `WorkItemService.CreateAsync`), apply the stage rules in `WorkItemService.DeleteAsync`, and in `RestoreAsync` place a restored stage after the last stage with its gate `NotRequested`; register the service as above (make T062 pass)
- [X] T074 [US3] Add `ListGatesAsync` and `WaitingGateRow` to `src/Upms.Application/Work/IMyTasksService.cs` and `MyTasksService.cs`, reading `IX_GateRequests_Pending` and keeping visible projects through `IProjectTeam.VisibleAmongAsync` (make the "My tasks" part of T064 pass)
- [X] T075 [US3] Create `src/Upms.Web/Components/Pages/Stages/StagesPage.razor` at `/projects/{key}/stages` with `GatePanel.razor` and `GateSetupDialog.razor`, and add the Stages tab for Stage-gate projects to `ProjectHeader` (make T066 pass)
- [X] T076 [US3] Add "Gates waiting for you" to `src/Upms.Web/Components/Pages/MyTasks/MyTasksPage.razor`, and the locked-stage notice, the gate summary and the gate history labels to `TaskDrawer.razor` and `HistoryList.razor` (make T067 pass)
- [X] T077 [US3] Seed gates in `tools/Upms.Seed/Seeder.cs` (approvers from each team, gates in every state, decisions with notes), and add Stages view loads, gate requests and gate decisions to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R25)
- [X] T078 [US3] Add `StageGate`, `GateRequest` and `GateDecision` to `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs`
- [ ] T079 [US3] Fill the `PendingUntilUs3` rows of `PermissionMatrixTests` (stages, gate setup, request and withdraw, and the decision row with its Approver column), make T068 pass end to end, and add stage gates to the Phase 3 line of `README.md`

**Checkpoint**: User Stories 1, 2 and 3 all work independently.

---

## Phase 6: User Story 4 - Plan with epics, stages, milestones and dependencies on the timeline (Priority: P4)

**Goal**: The timeline shows containers with progress, milestones and gate markers, a sprint lane, and
dependency arrows with late flags; dependencies are added and removed from the drawer and the timeline.

**Independent Test**: In `CBU`, schedule the stages, a milestone and six tasks, and make `CBU-12` wait for
`CBU-11`; stages show progress and gate markers, the milestone a marker, and an arrow joins the tasks; drag
`CBU-11` past `CBU-12`'s start: flagged late, no other date changes; move the milestone two days with the
keyboard (spec US4).

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T080 [P] [US4] Write `tests/Upms.Domain.Tests/Work/DependencyGraphTests.cs`: `DependencyToSelf`, `DuplicateDependency`, `DependencyInHierarchy` (container and item, parent and sub-task, either way) and `DependencyLoop` (direct and through three items); the late rule of data-model.md: late only while the item waited for is open and the waiting item's start (start date, else due date; a milestone's date) is before its due date; not late on equal dates; not flagged when a date is missing (FR-048, FR-051)
- [X] T081 [P] [US4] Write `tests/Upms.Application.Tests/Work/DependencyServiceTests.cs`: `P3_US4_AS5` adding records `DependencyAdded` on both items and both drawers list it; `P3_US4_AS6` the drawers' late marks follow date and status changes and no date ever changes; `P3_US4_AS7` a loop is refused, including two opposite links added at the same moment (one succeeds); an item of another project is `NotFound`; `P3_US4_AS9` Viewers get `Forbidden`; removing records `DependencyRemoved`; `P3_US4_AS10` deleting an item removes its links (and its sub-tasks') with history on the linked items, and restoring it does not bring them back; `FindItemsAsync` by key or title, at most 20 (FR-047–FR-052)
- [X] T082 [P] [US4] Extend `tests/Upms.Application.Tests/Work/TimelineServiceTests.cs`: `P3_US4_AS1` groups with containers first (stages in stage order, epics by first date, due date and key), each with progress, rows and unscheduled items, then items in no container; an undated container keeps its group; `P3_US4_AS2` a milestone is scheduled by its date and `RescheduleAsync` refuses a start date for it (`InvalidDates`); `P3_US4_AS3` gate markers on stage due dates with their state; `P3_US4_AS8` the sprint lane lists active and completed sprints with dates; `P3_US4_AS6` `TimelineDependency.IsLate`; at most 500 rows and 200 dependencies, both ends shown; "hide completed" (FR-042–FR-046, FR-050)
- [X] T083 [P] [US4] Extend `tests/Upms.Web.Tests/Timeline/TimelinePageTests.cs`: container bars with progress that expand and collapse; milestone markers that move one day per arrow key, save with Enter, cancel with Escape, and ignore Shift and Ctrl; gate markers labelled with their state; the sprint lane; the arrows layer is `aria-hidden` while each bar's accessible name says what it waits for and whether that is late; each row's **Actions** menu (Open, Add dependency, and `P3_US4_AS4` Schedule: a start date of today and a due date 27 days later for a container, today for a milestone); `P3_US4_AS9` Viewers get no moves and no "Add dependency" (FR-042–FR-047)
- [X] T084 [P] [US4] Write `tests/Upms.Web.Tests/Drawer/DependencyListTests.cs`: "Waits for" and "Blocks" with key, title, status, dates and a "Late" label; the "Add dependency" dialog finds items by key or title and shows refusals (loop, repeat, hierarchy) with the typed text kept; "Remove" with its announcement; read-only for Viewers (FR-049)
- [ ] T085 [P] [US4] Write `tests/Upms.E2E.Tests/P3_US4_TimelineTests.cs`: the US4 Independent Test end to end, the `MOB` sprint lane and an epic scheduled from the Actions menu, plus axe scans of the timeline with containers, markers and arrows and of the dependency dialog at 1280 px and 360 px

### Implementation for User Story 4

- [X] T086 [US4] Create `src/Upms.Domain/Work/Dependency.cs` (`PredecessorId` "the item waited for", `SuccessorId` "the waiting item") and `src/Upms.Domain/Work/DependencyGraph.cs` (loop, hierarchy and late rules); add `DependencyAdded` and `DependencyRemoved` to `WorkItemField.cs` (make T080 pass)
- [X] T087 [US4] Add `src/Upms.Infrastructure/Persistence/Configurations/Work/DependencyConfiguration.cs` (unique `(PredecessorId, SuccessorId)`, index `(SuccessorId)`, `IX_Dependencies_Project` including `PredecessorId` and `SuccessorId`, check `PredecessorId <> SuccessorId`), the `DbSet`, and migration `Dependencies`
- [X] T088 [US4] Add the dependency codes to `ErrorCodes.cs`; implement `src/Upms.Application/Work/IDependencyService.cs` and `DependencyService.cs` (Contribute; the planning lock around the loop check; history on both items), make `WorkItemService.DeleteAsync` remove links with history, and add `DependencyList` to the drawer details; register the service as above (make T081 pass)
- [X] T089 [US4] Rework `src/Upms.Application/Work/ITimelineService.cs` and `TimelineService.cs` into groups with progress, gate markers, the sprint lane and dependencies (limits of 500 rows and 200 dependencies), with `Type` and waited-for keys on `TimelineItem`, and the milestone rule in `RescheduleAsync` (make T082 pass)
- [X] T090 [US4] Update `src/Upms.Web/Components/Pages/Timeline/TimelinePage.razor` and `TimelineBar.razor`, and create `TimelineMarker.razor` (milestones and gates), `SprintLane.razor`, `DependencyArrows.razor` (one SVG layer, `aria-hidden`) and the row **Actions** menu; extend `src/Upms.Web/wwwroot/js/timeline.js` to drag milestone markers without resizing, keeping saves one at a time and building on the last change asked for (make T083 pass)
- [X] T091 [US4] Create `src/Upms.Web/Components/Pages/Drawer/DependencyList.razor` with the "Add dependency" dialog, use it in `TaskDrawer.razor`, and add the dependency history labels to `HistoryList.razor` (make T084 pass)
- [ ] T092 [US4] Seed about 50 dependencies per Stage-gate project in `tools/Upms.Seed/Seeder.cs`, and add dependency changes and timeline loads with containers, milestones and dependencies to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R25)
- [ ] T093 [US4] Fill the `PendingUntilUs4` row of `PermissionMatrixTests` (add and remove dependencies), add `Dependency` to `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs`, make T085 pass end to end, and add timeline planning and dependencies to the Phase 3 line of `README.md`

**Checkpoint**: All four user stories work independently.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T094 [P] Extend `tests/Upms.E2E.Tests/ResponsiveAndKeyboardTests.cs` to the create-project dialog, the backlog, the sprint report, the Stages view, the changed timeline and the drawer's new sections: usable at 360 px, and each story's main path completes with the keyboard only (FR-056, SC-011)
- [ ] T095 Run the SC-002 suite with every Phase 1, 2 and 3 action at 300 users and about 500,000 work items, tune indexes until p95 ≤ 1 s, and record the results in `specs/004-templates-and-planning/validation.md` (SC-002)
- [ ] T096 [P] Review the Phase 3 changes against OWASP ASVS Level 2 in `docs/security/phase3-asvs-review.md` (sprint and stage management need Manage; decisions only from named approvers, including Viewers; the stage lock on every status path; approvers added as Viewers are audited; no leaks through "Gates waiting for you", people search, dependency search or the backlog; the timeline's drag offsets for milestones), and fix findings (constitution III)
- [ ] T097 [P] Add a "Phase 3 screens" section (backlog, sprint dialogs and report, Stages view, gate dialogs, timeline markers and arrows, drawer sections, "Gates waiting for you") to the manual screen-reader script in `docs/accessibility/phase1-screen-reader-review.md`
- [ ] T098 [P] Update `README.md` (status, features, test commands), `CLAUDE.md` (Phase 3 built; Phase 4 next) and `docs/pilot/phase1-pilot-plan.md` with how the pilot measures SC-001 (a 20-item sprint in under 10 minutes), SC-003 (a Stage-gate plan in under 45 minutes), SC-004 (a gate decided within 1 minute of signing in) and SC-012 (a Scrum team and a PMO project for 4 weeks, 80% rating the screens easy)
- [ ] T099 Run every step of `specs/004-templates-and-planning/quickstart.md`, including the upgrade of a Phase 2 database, and record the results in `specs/004-templates-and-planning/validation.md`
- [ ] T100 Do the manual screen-reader pass (NVDA with Edge) on the Phase 3 screens and fix findings (needs a person; constitution VI)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 comes first: templates, types, containers and the Scrum rank order underlie everything else
  - US2 and US3 both build on US1 and can then proceed in parallel
  - US4 builds on US1, and its sprint lane and gate markers on US2 and US3
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Needs US1 (Scrum template, card types, estimates, project-wide rank). Independent of US3.
- **User Story 3 (P3)**: Needs US1 (Stage-gate template and its stages). Independent of US2.
- **User Story 4 (P4)**: Needs US1 (containers, milestones); the sprint lane needs US2 and the gate markers US3.

```text
Setup → Foundational → US1 ─┬─ US2 ─┬─ US4 → Polish
                            └─ US3 ─┘
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Domain before persistence, persistence before services, services before screens
- The story's permission rows and end-to-end test pass last
- Commit after each task or logical group; push after each story's checkpoint

### Parallel Opportunities

- T001 and T002 in parallel; T003, T004 and T005 in parallel, then T007 and T008
- All test tasks of a story marked [P] in parallel (different files)
- In US1: T026 and T036 alongside the domain and service work
- After US1, US2 and US3 in parallel (different domain types, services, pages and tests)
- Polish: T094, T096, T097 and T098 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "T010 ProjectTemplateTests (domain)"
Task: "T011 WorkItemTypesTests (domain)"
Task: "T013 WorkItemTypesServiceTests"
Task: "T014 ScrumRankTests"
Task: "T018 TemplatesUpgradeTests"
Task: "T021 TypeAndContainerTests (bUnit)"

# Then independent implementation pieces:
Task: "T026 WorkItemType values and WorkItemTypes table"
Task: "T036 TypeIcon, create dialog and project list"
```

## Parallel Example: User Stories 2 and 3

```bash
# After the US1 checkpoint, two people can work at once:
Developer A: T044–T050 (tests) → T051 → T052 → T053 → T054 → T055 → T056 → T057 → T058 → T059   (sprints)
Developer B: T060–T068 (tests) → T069 → T070 → T071 → T072 → T073 → T074 → T075 → T076 → T077 → T078 → T079   (gates)
# Both touch ErrorCodes.cs, WorkItemService.cs, TaskDrawer.razor and ProjectHeader.razor: merge those edits with care.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup and Phase 2: Foundational
2. Complete Phase 3: User Story 1 (templates, types, containers, estimates, the upgrade)
3. **STOP and VALIDATE**: run the US1 tests and the upgrade test; teams can now choose a template and organize
   work by type
4. Deploy or demo if ready

### Incremental Delivery

1. Setup + Foundational → lock, audit contract and header ready
2. US1 → templates and types (MVP) → demo
3. US2 → backlog and sprints → demo to the Scrum pilot team
4. US3 → stage gates → demo to the PMO
5. US4 → timeline planning and dependencies → demo
6. Polish → performance, security and accessibility reviews, quickstart run

### Parallel Team Strategy

With two developers: both on US1 (tests in parallel), then one takes US2 and the other US3; both finish
with US4.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps each task to a user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate the story on its own

## Implementation Notes

Decisions made while implementing are recorded here so the documents match the code.

- **The list's type filter is `kind=`** (T039): Phase 2 already uses `type=` for the status type, and saved
  addresses must keep working. `contracts/ui-routes.md` and research R23 say so.
- **Names in the code** (T033): the record the contract calls `ContainerRef` is `ContainerView` in the
  application layer, because the domain already has a `ContainerRef` (the container's id, project and label, used
  by `WorkItem.PlaceIn`). The non-generic `Result` gained `ThrowIfFailed()` for tests.
- **"Top" and "end" of a Scrum column** (T030): a Scrum project has one order for the whole project, so "top of
  the column" puts the card just before the column's first card, and "end of the column" just after its last card.
  A card moved into an empty column keeps its place in the project's order. Tests move cards with the version the
  drawer shows, because the Scrum board shows only the sprint once User Story 2 is built.
- **Stage-gate milestones in the drawer** (T040, found while writing the browser test): the drawer shows a milestone's one
  date as "Date" and offers no start date. A stage's or epic's drawer lists its first 50 items; more link to the
  List filtered by that container.
- **Seed** (T042): of every 15 projects, 5 are Scrum (an epic per 40 cards; 55% stories, 25% tasks, 20% bugs; 60%
  in an epic; 70% estimated) and 3 are Stage-gate (four stages, the first completed and the second in progress,
  one or two milestones each, 85% of tasks in a stage). The largest board stays Kanban. The SC-002 run adds two
  list loads: open bugs, and work in no epic or stage. A run with 50,000 work items and 30 users passed, with
  every p95 under 300 ms; the full run comes with User Story 2.
- **Sprint codes and names in the code** (T051, T053): besides the contract's `SprintAlreadyActive` and
  `SprintCompleted`, completing a sprint that has not started returns `SprintNotActive`, and starting or deleting a
  sprint that has started returns `SprintNotPlanned`. `BacklogRow.Epic` is a `ContainerView`, as elsewhere. The
  views carry what the page header needs: `BacklogView` has `CanRestoreDeleted` and the statuses, and `SprintReport` has
  the project name, `CanManage` and `CanRestoreDeleted`. `ActiveSprintInfo` lists the planned sprints that the open
  cards can go to (`SprintOption`), and the drawer's sprint is a `SprintView`.
- **Status changes keep a Scrum card's place** (T055, found by `BacklogServiceTests`): a status change from the drawer
  used to put the card at the bottom of its new column, which in a Scrum project moved it down the backlog and the
  sprint. Scrum cards now keep their rank, as the board and Jira do.
- **Backlog components** (T056): the row is `BacklogItem.razor` and the sprint heading `SprintHeader.razor`, because
  `BacklogRow` and `SprintSection` are already the names of the view records. `SprintDialogs.razor` serves the backlog
  and the Scrum board's "Complete sprint". A Scrum board shows all of its sprint's done cards, so it has no "Show all
  completed" link.
- **Permission matrix** (T059): the Scrum rows run against two more projects, built only for their cells: MOB (backlog
  cards, an active and a planned sprint) and APP (a completed sprint for its report, and a planned sprint that can
  start, since a project has one active sprint at most).
- **Seed** (T058): each Scrum project has three completed two-week sprints of done cards, the active sprint (started a
  week ago, with the top 12 open cards and 4 done ones), a planned sprint with the next 10 open cards, and the scope
  log of each. Simulated users in Scrum projects also load the backlog, reorder it, move cards between it and the
  planned sprint, and read past sprint reports.
- **Gate codes and names in the code** (T069, T073): the gate aggregate refuses a decision from someone who may not
  decide with `NotAnApprover`, which the service returns as `Forbidden`, and a decision or withdrawal on a request that
  has ended with `GateRequestEnded`, which it returns as `Conflict` with the current stage. `ApproverOption` (in the Work
  module) stands for the contract's `PersonOption`, which belongs to the Projects module. `StageView` gains `WaitsFor`
  (the stage whose gate a locked stage waits for), and the drawer's details gain `Gate` (a `GateSummary`) and
  `LockedUntil`. `IProjectTeam.AddViewerAsync` returns a `Result<bool>`, so an inactive account is refused with
  `AccountDeactivated`.
- **The requester exclusion's default** (T070): `ExcludeRequester` defaults to 1 in the database only. A model default
  would make EF Core skip writing `false` on insert.
- **Stage and gate helpers** (T072, T073): `StageLocks` answers which gate locks an item and is called on every status
  path and by column deletion; `StageGateRules` gives each new stage its gate, keeps approved stages from being deleted,
  and cancels or restarts a gate when its stage is deleted or restored. Deleting and restoring a stage take the planning
  lock.
- **History sentences** (T076): the drawer now also words sprint moves ("moved the story into MOB Sprint 1"), which
  User Story 2 had left as "changed Sprint".
- **Permission matrix** (T079): the gate rows run against the CBU gate that abe (a Viewer) approves, and a second
  Stage-gate project, PMO, whose gate has a pending request for the withdraw and decision cells.
- **Seed** (T077): each Stage-gate project has Initiation and Planning approved, Execution current (awaiting a
  decision, or sent back with a note in every other project) and Closure locked, with two approvers per gate and one
  approval needed. Seeded tasks stay out of the locked stage, so the load test's card moves are not refused. Simulated
  users in Stage-gate projects load the Stages view, request approval or withdraw it, and approvers send the current
  gate back.
- **Dependencies in the code** (T086, T088, T091): `DependencyGraph` sits in `Dependency.cs` beside the entity. The
  drawer's section is `DependencyLinks.razor`, because `DependencyList` is already the name of its view record, and
  adding a link is `AddDependencyDialog.razor`, which the timeline's Actions menu opens too. `DependencyReads` holds
  what the drawer and `WorkItemService.DeleteAsync` share: the lists and the removal of a deleted item's links, with
  history on each linked item.
- **The US4 browser test** (T085): it builds its own Scrum project, `MPL`, with a completed and an active sprint, rather
  than relying on `MOB` from the US2 test, since the tests share one database and may run in any order.
- **Timeline components** (T090): the markers, the sprint lane and the arrows layer are part of `TimelinePage.razor` and
  `TimelineBar.razor` rather than three more components, because each is a few lines that share the page's scale.
  Every top-level row, in every template, has an Actions menu (Open, Schedule when it has no dates, Add dependency);
  Viewers get only Open. `timeline.js` draws the arrows after each render (`upms.timeline.drawArrows`).
- **Late marks follow a move** (T090, found while writing the browser test): moving a bar that waits for another, or
  is waited for, reads the timeline again, so the server works out which links are late. Other moves still update
  only their own bar.
- **Milestone drags** (T096, security review F1): a milestone has no handles, but the page no longer trusts the drag
  mode the browser sends for it: a milestone only moves. A drag of an item without dates is ignored.
- **Seed** (T092): each Stage-gate project has about 50 dependencies between its tasks, each later task waiting for an
  earlier one so none makes a loop, with history on both items. Simulated users in Stage-gate projects also add a
  dependency and remove it on their next turn, keeping the same rule, and the timeline load now covers the groups,
  milestones and arrows.
- **Wording found in the quickstart walkthrough** (T099): a milestone's date saves as "Date saved"; deleting an epic or
  stage says its items stay, in no epic or stage, and the button names the type ("Delete epic"); the List's count
  says "items", not "tasks", since it holds every type (the Phase 2 tests now expect "61 items"); a backlog row moved
  within its own list is announced as "MOB-9 moved to position 1 of 8 in the backlog."
