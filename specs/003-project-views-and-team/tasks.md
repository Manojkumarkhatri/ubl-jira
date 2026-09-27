---

description: "Task list for Phase 2: Project Views and Team (specs/003-project-views-and-team)"
---

# Tasks: Project Views and Team (Phase 2)

**Input**: Design documents from `specs/003-project-views-and-team/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable): tests for domain rules and
application services are written first and fail before implementation, and every acceptance scenario is
automated. Phase 2 test methods begin with `P2_` and the scenario they prove (for example
`P2_US1_AS5_RemovedMemberIsRefusedAtNextAction`), so they are not confused with Phase 1's `US1_AS…` tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4 of this spec)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): the Phase 1 tree (`src/Upms.Domain/`, `src/Upms.Application/`,
`src/Upms.Infrastructure/`, `src/Upms.Web/`, `tests/Upms.*.Tests/`, `tools/`, `docs/`), foldered by module
(Identity, Projects, Work). Contracts referenced below live in [contracts/](./contracts/).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Point the documentation at Phase 2 before code changes start

- [X] T001 [P] Add the Phase 2 documents to the documents table and status line of `README.md` (spec, plan, tasks, quickstart of `specs/003-project-views-and-team`)
- [X] T002 [P] Add a note at the top of `specs/002-kanban-project-core/contracts/permissions.md` that the matrix is superseded by `specs/003-project-views-and-team/contracts/permissions.md` once Phase 2 is installed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared project header that every Phase 2 screen uses

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Write `tests/Upms.Web.Tests/Shared/ProjectHeaderTests.cs`: breadcrumb "Projects / KEY", the project name as heading level 1, links for the views and actions it is given, `aria-current="page"` on the current view, actions hidden when not allowed
- [X] T004 Extract the board page's header into `src/Upms.Web/Components/Shared/ProjectHeader.razor` (breadcrumb, name, view links, and "Project settings" / "Deleted tasks" shown from flags) and use it in `src/Upms.Web/Components/Pages/Board/BoardPage.razor` and `src/Upms.Web/Components/Pages/Settings/ProjectSettings.razor` (make T003 pass; Phase 1 board tests stay green)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Control who sees and works in a project (Priority: P1) 🎯 MVP

**Goal**: Project teams with Project Admin, Member and Viewer roles; members-only projects; read-only
Viewers; pilot projects migrated to their owners and contributors.

**Independent Test**: As `WEB`'s Project Admin, add amina as Member and bilal as Viewer; amina works on
tasks, bilal only reads, carla (not a member) gets "not found"; removing amina refuses her next action
(spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T005 [P] [US1] Write `tests/Upms.Domain.Tests/Projects/ProjectTeamTests.cs`: `Project.Create` adds the creator as `ProjectAdmin`; `AddMember` refuses an existing member with `DuplicateMember` naming the current role; `ChangeMemberRole` and `RemoveMember` refuse to leave zero Project Admins with active accounts (`LastProjectAdmin`), allow it when another active Project Admin remains, and always allow changing or removing an inactive Project Admin; a change to the same role does nothing; every change increments `MembersVersion` (FR-007, FR-010)
- [X] T006 [P] [US1] Rewrite `tests/Upms.Application.Tests/Projects/ProjectAccessTests.cs` for the Phase 2 rules of research R2: non-members get `NotFound` for every right (as for unknown projects); Viewers have `View` only (`Forbidden` otherwise); Members `View`, `Contribute` and `DeleteOwnWorkItem`; Project Admins also `Manage`; administrators everything, member or not; `Restore` for administrators only; a removal or role change applies at the next call; `ProjectAccessInfo` carries `Role` and `CanContribute` (FR-002–FR-006, FR-011)
- [X] T007 [P] [US1] Write `tests/Upms.Application.Tests/Projects/ProjectMemberServiceTests.cs`: `P2_US1_AS2` adding a Member and a Viewer, the team listed Project Admins first then by name with active marks; `FindPeopleAsync` returns active accounts matching name, user name or email, excluding members, at most 20; `AccountDeactivated` for a deactivated account; `DuplicateMember`; role changes and removals; `P2_US1_AS7` the last active Project Admin cannot remove or retype themselves (`LastProjectAdmin`); a stale `MembersVersion` returns `Conflict` with the current team; two Project Admins removing each other at the same moment leave exactly one; Members and Viewers get `Forbidden` for changes; each change writes `MemberAdded`, `MemberRemoved` or `MemberRoleChanged` with the roles (FR-008–FR-013)
- [X] T008 [P] [US1] Extend `tests/Upms.Application.Tests/Projects/ProjectServiceTests.cs`: `P2_US1_AS1` the creator becomes the only member, as Project Admin; the list shows only the caller's projects with their role; `P2_US1_AS8` administrators see every project with "administrator access" where they are not members; `P2_US1_AS4` non-members get `NotFound` for project details (FR-002, FR-006, FR-007, FR-015)
- [X] T009 [P] [US1] Write `tests/Upms.Application.Tests/Projects/MembershipUpgradeTests.cs` (own database): migrate to the last Phase 1 migration, insert pilot data with SQL (a project owned by owen; tasks created by cara, including a deleted one; a history row by erin; a comment by dan; a deactivated contributor; an uninvolved user; a second project whose owner is deactivated), apply the Phase 2 migrations, then assert `P2_US1_AS9`: owen is Project Admin, the deactivated owner is their project's (inactive) Project Admin, cara, erin, dan and the deactivated contributor are Members, the uninvolved user is not a member, one `MemberAdded` audit event per membership with source "Phase 2 upgrade", and running the backfill again adds nothing (FR-014, SC-006)
- [X] T010 [P] [US1] Update `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs` to read `specs/003-project-views-and-team/contracts/permissions.md` with the columns Anonymous, Non-member, Viewer, Member, Creator, Project Admin, Admin: `NotFound` for 🚫 cells, `Forbidden` for ❌ (`CommentNotOwned` for members refused someone else's comment), operations for every row that exists after US1 (including team changes); the rows "Assign tasks and set their dates" and "See "My tasks"" are listed in an explicit `PendingUntilUs2` set whose cells are reported as skipped until T048 fills them; rules 3, 4 and 6 of the contract (SC-003)
- [X] T011 [P] [US1] Write bUnit tests `tests/Upms.Web.Tests/Members/MembersPageTests.cs`: the team with roles, "deactivated" and "you" marks; Members and Viewers see no controls; a Project Admin finds a person by typing, adds them with a role, changes a role and removes a member after confirming; `LastProjectAdmin` and conflict messages (the conflict reloads the team); focus returns to the changed row
- [X] T012 [P] [US1] Extend `tests/Upms.Web.Tests/Board/BoardPageTests.cs` and `tests/Upms.Web.Tests/Drawer/TaskDrawerTests.cs` for `P2_US1_AS3`: with `CanContribute` false the board has no "What needs to be done?" boxes, no draggable cards and no "Move to", and the drawer's fields are read-only with no comment box, sub-task box or delete action
- [X] T013 [P] [US1] Write `tests/Upms.E2E.Tests/P2_US1_ProjectTeamTests.cs`: the US1 Independent Test end to end (owen adds amina as Member and bilal as Viewer; bilal read-only; carla "Not found" from her list and a direct link; amina removed while her board is open is refused at her next action; bilal made a Member acts without signing in again; owen cannot remove himself; an administrator who is not a member has full rights), plus axe scans of the members screen at 1280 px and 360 px

### Implementation for User Story 1

- [X] T014 [P] [US1] Create `src/Upms.Domain/Projects/ProjectRole.cs` (`ProjectAdmin`, `Member`, `Viewer`) and `src/Upms.Domain/Projects/ProjectMember.cs` (`ProjectId`, `UserId`, `Role` "varchar(12)", `AddedAt`, `AddedById` "null for upgrade memberships", non-public setters)
- [X] T015 [US1] Extend `src/Upms.Domain/Projects/Project.cs` with the `Members` collection, `MembersVersion` "starts at 1; incremented by every membership change; concurrency token", the creator added as `ProjectAdmin` in `Create`, and `AddMember`, `ChangeMemberRole`, `RemoveMember` taking the set of active member IDs, with the codes `DuplicateMember` and `LastProjectAdmin` (make T005 pass)
- [X] T016 [P] [US1] Add `MemberAdded`, `MemberRemoved` and `MemberRoleChanged` to `src/Upms.Domain/Identity/AuditEventType.cs`
- [X] T017 [US1] Add `src/Upms.Infrastructure/Persistence/Configurations/Projects/ProjectMemberConfiguration.cs` (table `ProjectMembers`, unique `(ProjectId, UserId)`, index `(UserId)` including `Role`, FKs) and `MembersVersion` as a concurrency token in `ProjectConfiguration.cs`; create migration `ProjectMembers` in `src/Upms.Infrastructure/Persistence/Migrations/` whose `Up` also runs the upgrade of data-model.md (owners as `ProjectAdmin`; creators, history actors and comment authors as `Member`; one `MemberAdded` audit row each; skipping existing rows) (make T009 pass)
- [X] T018 [P] [US1] Extend `src/Upms.Application/Identity/Contracts/IUserDirectory.cs` and `src/Upms.Application/Identity/UserDirectory.cs` with `SearchActiveAsync(term, take)` and `UserDisplay.UserName` (research R5)
- [X] T019 [US1] Rewrite `src/Upms.Application/Projects/ProjectAccess.cs` for membership (research R2), extend `ProjectAccessInfo` in `src/Upms.Application/Projects/Contracts/IProjectAccess.cs` with `Role` and `CanContribute`, make `CanDeleteWorkItemAsync` allow administrators, Project Admins and creators who can still contribute, and update refusal messages in `WorkItemService` ("the task's creator, a Project Admin or an administrator") (make T006 pass)
- [X] T020 [US1] Implement `src/Upms.Application/Projects/IProjectMemberService.cs` and `src/Upms.Application/Projects/ProjectMemberService.cs` (team view, people search, add, change role, remove; active IDs from `IUserDirectory`; audit through `IMembershipAuditLog` (research R6) in the same transaction; `Conflict` on a stale `MembersVersion`), registered in `ApplicationServiceCollectionExtensions.cs` and as operation-scoped in `src/Upms.Web/Security/WebServiceCollectionExtensions.cs` (make T007 pass)
- [X] T021 [US1] Update `src/Upms.Application/Projects/ProjectService.cs` and `IProjectService.cs`: `ListAsync` by membership with `ProjectSummary(Key, Name, MyRole, AdministratorAccess, OpenItemCount)` (administrators see all); `CreateAsync` relies on the domain adding the creator as Project Admin (make T008 pass)
- [X] T022 [US1] Enforce `Contribute` plus authorship for editing and deleting comments in `src/Upms.Application/Work/CommentService.cs`, and expose `CanContribute` on `BoardView` (`src/Upms.Application/Work/IBoardService.cs`, `BoardService.cs`) and `WorkItemDetails` (`IWorkItemService.cs`, `WorkItemService.cs`)
- [X] T023 [US1] Update the Phase 1 tests that relied on the open workspace or on ownership (Application, Web and E2E suites): add the acting users as members with the intended role (Project Admin where they were the owner, Member where they were "any user"), and expect `NotFound` instead of `Forbidden` for non-members; add member builders to `tests/Upms.Application.Tests/Fixtures/TestData.cs` and `tests/Upms.E2E.Tests/Fixtures/AppFixture.cs`
- [X] T024 [US1] Create `src/Upms.Web/Components/Pages/Members/MembersPage.razor` at `/projects/{key}/members` (team list; for Project Admins and administrators: "Add member" with type-ahead search and role, a role choice per member, "Remove" with confirmation; conflict and rule messages; announcements) and add "Members" to `ProjectHeader` (make T011 pass)
- [X] T025 [US1] Make the board read-only without `CanContribute` in `src/Upms.Web/Components/Pages/Board/` (`BoardColumn.razor`, `TaskCard.razor`, `InlineCreate.razor`, `MoveToMenu.razor`) and the drawer read-only in `src/Upms.Web/Components/Pages/Drawer/` (`TaskDrawer.razor`, `SubtaskList.razor`, `CommentThread.razor`) (make T012 pass)
- [X] T026 [US1] Show "Your role" instead of the owner in `src/Upms.Web/Components/Pages/Projects/ProjectList.razor` (FR-015), and say "Project Admins" instead of "owner" in the settings and column screens
- [X] T027 [US1] Give seeded projects teams in `tools/Upms.Seed/Seeder.cs` (owner as Project Admin, 4–20 Members, 0–3 Viewers; work item creators and comment authors drawn from contributors), and make `tests/Upms.Performance.Tests/LoadDatabase.cs` and `Sc002LoadTests.cs` act only in projects the simulated user belongs to, with "membership change" as a measured action (research R15)
- [X] T028 [US1] Add `ProjectMember` to `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs`, then make T010 and T013 pass end to end

**Checkpoint**: User Story 1 is fully functional and testable on its own. This is the MVP: projects are members-only.

---

## Phase 4: User Story 2 - Assign and schedule tasks, and find my work (Priority: P2)

**Goal**: Assignees chosen from the team, start and due dates with overdue marks, board filters, and a
cross-project "My tasks" page.

**Independent Test**: Assign `WEB-1` to amina (due yesterday) and `WEB-2` to bilal; amina assigns
`WEB-3` to herself; cards show assignees and due dates with `WEB-1` overdue; "Only my tasks" and her "My
tasks" page list `WEB-1` and `WEB-3`, plus a task from another project (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T029 [P] [US2] Write `tests/Upms.Domain.Tests/Work/AssignAndScheduleTests.cs`: `Assign` records `Assignee` with old and new display names and updates `UpdatedAt`, and does nothing when unchanged; `Schedule` accepts either date alone, refuses a due date before the start date or a date outside "2000-01-01 to 2099-12-31" (`InvalidDates`), records `StartDate` and `DueDate` rows (ISO values) in one change set for the dates that changed, and clears dates (FR-018, FR-023)
- [X] T030 [P] [US2] Write `tests/Upms.Application.Tests/Work/AssigneeAndDatesTests.cs`: `P2_US2_AS1` assigning through `WorkItemEdit.Assignee` updates the task and its history; `P2_US2_AS2` "Assign to me"; `P2_US2_AS3` the choices are the active Project Admins and Members only, and choosing a Viewer, a non-member, a deactivated account or someone removed since the drawer opened returns `NotAssignable`; `P2_US2_AS4` `WorkItemEdit.Dates` saves both dates and `InvalidDates` changes nothing; a stale version returns `Conflict`; Viewers get `Forbidden`; an assignee who left the project is returned with `CanWork` false (FR-016–FR-019, FR-023, FR-024)
- [X] T031 [P] [US2] Write `tests/Upms.Application.Tests/Work/MyTasksServiceTests.cs`: `P2_US2_AS7` open tasks and sub-tasks assigned to the caller from every project they can see, ordered by project name, due date (undated last), priority and key, with parent keys, paged 50; done, deleted and other people's tasks excluded; `P2_US2_AS8` a project the caller was removed from is excluded (FR-025)
- [X] T032 [P] [US2] Extend `tests/Upms.Application.Tests/Work/BoardQueryTests.cs`: cards carry the assignee (name, initials, `CanWork`) and due date; `BoardView.ViewerId` is the caller (FR-021)
- [X] T033 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/Drawer/AssigneeAndDatesTests.cs`: the assignee choice lists the options and "Unassigned"; "Assign to me" is shown only when the viewer can be assigned; start and due date fields save through `WorkItemEdit.Dates`; `InvalidDates` shows the message and keeps the input; `P2_US2_AS5` "Overdue" is a text label; the sub-task list shows assignee and due date; history lines for assignee and date changes; `ViewerToday` returns the calendar date in the viewer's time zone (a viewer in UTC+5 at 20:00 UTC is already on the next day) (FR-042)
- [X] T034 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/Board/BoardFiltersTests.cs`: cards show initials with the full name as text and the due date with "Overdue"; `P2_US2_AS6` "Only my tasks" and the assignee filter (including "Unassigned") hide other cards, columns show "2 of 5" while the work-in-progress marker counts all cards, and turning the filter off shows everything; a card whose assignee can no longer work on the project shows "no longer on the project" (FR-024); dropping a card next to a visible card while filtered sends a placement before that card
- [X] T035 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/MyTasks/MyTasksPageTests.cs`: rows grouped by project with overdue marks and parent keys; the total count; empty state; selecting a row opens the drawer and closing it after completing the task removes the row (FR-026)
- [X] T036 [P] [US2] Write `tests/Upms.E2E.Tests/P2_US2_AssignAndScheduleTests.cs`: the US2 Independent Test end to end, plus axe scans of the board, the drawer and "My tasks" at 1280 px and 360 px

### Implementation for User Story 2

- [X] T037 [US2] Add `AssigneeId`, `StartDate`, `DueDate`, `Assign` and `Schedule` to `src/Upms.Domain/Work/WorkItem.cs` and `Assignee`, `StartDate`, `DueDate` to `src/Upms.Domain/Work/WorkItemField.cs` (make T029 pass)
- [X] T038 [US2] Update `src/Upms.Infrastructure/Persistence/Configurations/Work/WorkItemConfiguration.cs`: `StartDate`/`DueDate` as `date`, the `AssigneeId` FK, check constraint `CK_WorkItems_Dates`, `AssigneeId` and `DueDate` included in `IX_WorkItems_Board`, new `IX_WorkItems_Assignee_Live` and `IX_WorkItems_Project_Live` (data-model.md); create migration `AssigneesAndDates`
- [X] T039 [US2] Create the contract `src/Upms.Application/Projects/Contracts/IProjectTeam.cs` and `src/Upms.Application/Projects/ProjectTeam.cs` (`GetMembersAsync`, `CanBeAssignedAsync`, `VisibleAmongAsync`), registered in `ApplicationServiceCollectionExtensions.cs` (research R7)
- [X] T040 [US2] Implement the `Assignee` and `Dates` edits in `src/Upms.Application/Work/WorkItemService.cs` (codes `NotAssignable` and `InvalidDates` in `ErrorCodes.cs`), and `Assignee`, `StartDate`, `DueDate`, `AssigneeOptions` in `WorkItemDetails` and assignee and due date in `SubtaskView` (`IWorkItemService.cs`, `WorkItemReads.cs`) (make T030 pass)
- [X] T041 [US2] Add the assignee and due date to `CardView` and `ViewerId` to `BoardView` in `src/Upms.Application/Work/BoardService.cs` (make T032 pass)
- [X] T042 [US2] Implement `src/Upms.Application/Work/IMyTasksService.cs` and `MyTasksService.cs` (research R13), registered as operation-scoped in the web host (make T031 pass)
- [X] T043 [P] [US2] Create `src/Upms.Web/Components/Shared/AssigneeBadge.razor` (initials, name as text, "no longer on the project"), `src/Upms.Web/Components/Shared/DueDateText.razor` (date, "Overdue" label) and `src/Upms.Web/Components/Shared/ViewerToday.cs` (today in the viewer's time zone from `ViewerTimeZone` and `TimeProvider`, research R9)
- [X] T044 [US2] Add the assignee choice, "Assign to me", start and due date fields, the overdue mark and the sub-task list's assignee and due date to `src/Upms.Web/Components/Pages/Drawer/TaskDrawer.razor` and `SubtaskList.razor`, and assignee and date lines to `HistoryList.razor` (make T033 pass)
- [X] T045 [US2] Show the assignee and due date on `src/Upms.Web/Components/Pages/Board/TaskCard.razor`, and add "Only my tasks" and the assignee filter with "matching of total" column counts to `BoardPage.razor` and `BoardColumn.razor` (make T034 pass)
- [X] T046 [US2] Create `src/Upms.Web/Components/Pages/MyTasks/MyTasksPage.razor` at `/my-tasks` (grouped rows, pager, drawer through `?task=`) and add "My tasks" to the header in `src/Upms.Web/Components/Layout/MainLayout.razor` (make T035 pass)
- [X] T047 [US2] Seed assignees (about 70% of open tasks, from contributors) and dates (about half of all tasks) in `tools/Upms.Seed/Seeder.cs`, and add "My tasks load", "assigning" and "setting dates" to `tests/Upms.Performance.Tests/Sc002LoadTests.cs`; then make T036 pass end to end
- [X] T048 [US2] Add the operations for the rows "Assign tasks and set their dates" and "See "My tasks"" to `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs` and empty its `PendingUntilUs2` set (SC-003)

**Checkpoint**: User Stories 1 and 2 both work independently: teams, assignees, dates and "My tasks".

---

## Phase 5: User Story 3 - Browse a project as a list (Priority: P3)

**Goal**: A sortable, filterable List view of every task and sub-task, with its state in the address,
the shared drawer and "What needs to be done?".

**Independent Test**: With 60 tasks, the list shows 50 and the total; sort by due date, filter
"overdue" and "unassigned", open the address in another browser to get the same list, and edit a row in
the drawer (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [ ] T049 [P] [US3] Write `tests/Upms.Application.Tests/Work/WorkItemListServiceTests.cs`: `P2_US3_AS1` tasks and sub-tasks with parent keys, newest key first, 50 per page with the total; `P2_US3_AS2` every sort in both directions (status by column position then board order, assignee by display name, priority by rank, dates with undated last); `P2_US3_AS3` filters by column, status type, priority, "me", "unassigned", people, "overdue" and "due in the next 7 days" relative to the given today, "no due date", and words in the title or description, alone and combined; non-members get `NotFound` (FR-028–FR-030)
- [ ] T050 [P] [US3] Write bUnit tests `tests/Upms.Web.Tests/List/ProjectListViewTests.cs`: `P2_US3_AS4` the query string round-trips into the filters, sort and page and back; active filters as removable chips and "Clear filters"; sortable headers expose `aria-sort`; `P2_US3_AS5` a row opens the drawer and a change is reflected; `P2_US3_AS6` "What needs to be done?" creates in the first "to do" column (hidden for Viewers); `P2_US3_AS7` the empty state offers "Clear filters"; overdue rows carry the "Overdue" label (FR-020)
- [ ] T051 [P] [US3] Write `tests/Upms.E2E.Tests/P2_US3_ListViewTests.cs`: the US3 Independent Test end to end, including the shared address in a second member's browser, plus axe scans at 1280 px and 360 px

### Implementation for User Story 3

- [ ] T052 [US3] Implement `src/Upms.Application/Work/IWorkItemListService.cs` and `WorkItemListService.cs` (SQL filtering, sorting and paging; status and assignee sorts ordered in memory by ID, then the page read; research R11), registered as operation-scoped (make T049 pass)
- [ ] T053 [US3] Create `src/Upms.Web/Components/Pages/List/ProjectListView.razor` at `/projects/{key}/list` (filters, chips, sortable headers, pager, query-string state, "What needs to be done?", drawer through `?task=`) and add the "List" view link to `ProjectHeader` (make T050 pass)
- [ ] T054 [US3] Add "list load (sorted and filtered)" to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` and tune `IX_WorkItems_Project_Live` if needed; then make T051 pass end to end

**Checkpoint**: The List view works on its own on top of stories 1 and 2.

---

## Phase 6: User Story 4 - Plan on a timeline (Priority: P4)

**Goal**: A timeline of scheduled tasks with weeks, months and quarters, drag-and-drop and keyboard
rescheduling, an "Unscheduled" list with "Schedule", and expandable sub-tasks.

**Independent Test**: Six tasks, four dated: four bars and a today line, two unscheduled; drag one bar
two weeks later, drag another's end, move a third with the keyboard, schedule an unscheduled task; each
task's dates and history match (spec US4).

### Tests for User Story 4 (write first, must fail) ⚠️

- [ ] T055 [P] [US4] Write `tests/Upms.Application.Tests/Work/TimelineServiceTests.cs`: rows are top-level tasks that are scheduled or have scheduled sub-tasks, ordered by first date, due date and key; one-date tasks included; sub-tasks split into scheduled and unscheduled; "hide completed"; unscheduled tasks 50 at a time; `RescheduleAsync` moves both dates or one, returns `InvalidDates`, `Conflict` with the current item on a stale version, `Forbidden` for Viewers, and records one change set (FR-034–FR-039)
- [ ] T056 [P] [US4] Write `tests/Upms.Web.Tests/Timeline/TimelineScaleTests.cs`: pixels per day for weeks, months and quarters; the range covers every scheduled date and today; headings per week, month or quarter; positions and widths, including one-day bars
- [ ] T057 [P] [US4] Write bUnit tests `tests/Upms.Web.Tests/Timeline/TimelinePageTests.cs`: bars named with key, title, dates, status and assignee; `P2_US4_AS5` arrows, Shift and Ctrl adjust a pending change with an announcement, Enter saves once, Escape cancels, Enter with nothing pending opens the drawer; `P2_US4_AS6` "Schedule"; `P2_US4_AS7` expanding a row; `P2_US4_AS8` a conflict shows the message and the current dates; `P2_US4_AS9` Viewers cannot adjust or schedule; "Hide completed"; switching the scale keeps a pending change
- [ ] T058 [P] [US4] Write `tests/Upms.E2E.Tests/P2_US4_TimelineTests.cs`: the US4 Independent Test end to end, dragging a bar and a bar's end with the mouse and moving one with the keyboard, plus axe scans at 1280 px and 360 px

### Implementation for User Story 4

- [ ] T059 [US4] Implement `src/Upms.Application/Work/ITimelineService.cs` and `TimelineService.cs` (`GetAsync`, `ListUnscheduledAsync`, `RescheduleAsync` through `WorkItem.Schedule`), registered as operation-scoped (make T055 pass)
- [ ] T060 [P] [US4] Implement `src/Upms.Web/Components/Pages/Timeline/TimelineScale.cs` (make T056 pass)
- [ ] T061 [US4] Create `src/Upms.Web/Components/Pages/Timeline/TimelinePage.razor` at `/projects/{key}/timeline` and `TimelineBar.razor` (scale switch `?scale=`, today line and "Today", "Hide completed", expandable rows, "Unscheduled" list with "Schedule", keyboard rescheduling with live announcements, conflicts, drawer through `?task=`), and add the "Timeline" view link to `ProjectHeader` (make T057 pass)
- [ ] T062 [US4] Create `src/Upms.Web/wwwroot/js/timeline.js` (pointer dragging with a preview; one call to the component with the day offsets on release) and the timeline styles in `src/Upms.Web/wwwroot/app.css`
- [ ] T063 [US4] Add "timeline load" and "rescheduling" to `tests/Upms.Performance.Tests/Sc002LoadTests.cs`; then make T058 pass end to end

**Checkpoint**: All four user stories work independently.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T064 [P] Extend `tests/Upms.E2E.Tests/ResponsiveAndKeyboardTests.cs` to the members screen, the list, the timeline and "My tasks": usable at 360 px, and each story's main path completes with the keyboard only (FR-041, SC-009)
- [ ] T065 Run the SC-002 suite with every Phase 1 and Phase 2 action at 300 users and about 500,000 work items, tune indexes until p95 ≤ 1 s, and record the results in `specs/003-project-views-and-team/validation.md` (SC-002)
- [ ] T066 [P] Review the Phase 2 changes against OWASP ASVS Level 2 (access control on every project-scoped call and task key, no leaks through "My tasks", filters or people search, audit of membership changes) in `docs/security/phase2-asvs-review.md`, and fix findings (constitution III)
- [ ] T067 [P] Add a "Phase 2 screens" section (members, list, timeline, "My tasks", drawer changes) to the manual screen-reader script in `docs/accessibility/phase1-screen-reader-review.md`
- [ ] T068 [P] Update `README.md` (status, features, test commands) and `docs/pilot/phase1-pilot-plan.md` for Phase 2: members-only projects, "My tasks", and how the pilot measures SC-001 (adding three colleagues in under 2 minutes), SC-007 (finding one's tasks within 30 seconds) and SC-008 (rescheduling on the timeline in under 15 seconds)
- [ ] T069 Run every step of `specs/003-project-views-and-team/quickstart.md`, including the upgrade of a Phase 1 database, and record the results in `specs/003-project-views-and-team/validation.md`
- [ ] T070 Do the manual screen-reader pass (NVDA with Edge) on the Phase 2 screens and fix findings (needs a person; constitution VI)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 comes first: assignees are chosen from the team, and every view is members-only
  - US2 builds on US1; US3 and US4 both build on US2 (assignees and dates) and can then proceed in parallel
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Needs US1 (the team decides who can be assigned; "My tasks" respects visibility).
- **User Story 3 (P3)**: Needs US2 (assignee and date columns and filters). Independent of US4.
- **User Story 4 (P4)**: Needs US2 (dates). Independent of US3.

```text
Setup → Foundational → US1 → US2 ─┬─ US3 ─┐
                                  └─ US4 ─┴─ Polish
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Domain before persistence, persistence before services, services before screens
- The story's end-to-end test passes last
- Commit after each task or logical group; push after each story's checkpoint

### Parallel Opportunities

- T001 and T002 in parallel
- All test tasks of a story marked [P] in parallel (different files)
- In US1: T014, T016 and T018 in parallel; in US2: T043 alongside the service work
- After US2, US3 and US4 in parallel (different services, pages and tests)
- Polish: T064, T066, T067 and T068 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "T005 ProjectTeamTests (domain rules)"
Task: "T006 ProjectAccessTests (Phase 2 rules)"
Task: "T007 ProjectMemberServiceTests"
Task: "T009 MembershipUpgradeTests"
Task: "T011 MembersPageTests (bUnit)"

# Then independent implementation pieces:
Task: "T014 ProjectRole and ProjectMember"
Task: "T016 membership audit event types"
Task: "T018 IUserDirectory.SearchActiveAsync"
```

## Parallel Example: User Stories 3 and 4

```bash
# After the US2 checkpoint, two people can work at once:
Developer A: T049 → T052 → T050 → T053 → T051 → T054   (List view)
Developer B: T055 → T059 → T056 → T060 → T057 → T061 → T062 → T058 → T063   (Timeline)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup and Phase 2: Foundational
2. Complete Phase 3: User Story 1 (teams, members-only projects, the upgrade)
3. **STOP and VALIDATE**: run the US1 tests and the upgrade test; projects can now hold confidential work
4. Deploy or demo if ready

### Incremental Delivery

1. Setup + Foundational → header ready
2. US1 → members-only projects (MVP) → demo
3. US2 → assignees, dates, "My tasks" → demo
4. US3 → List view → demo
5. US4 → Timeline → demo
6. Polish → performance, security and accessibility reviews, quickstart run

### Parallel Team Strategy

With two developers: both on US1 (tests in parallel), then US2 together; then one takes US3 and the other
US4.

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

- **Project header** (T004): `Shared/ProjectHeader.razor` holds the breadcrumb and the project navigation
  (Board, Members, Project settings for Project Admins and administrators, Deleted tasks for
  administrators), with `aria-current="page"` on the current screen; List and Timeline join it in US3 and
  US4.
- **Membership audit** (T020, research R6): the Projects module writes the audit events through
  `IMembershipAuditLog` (Identity contracts) with its own `MembershipChange` enum, because the architecture
  rules keep the Identity module's `AuditEventType` out of other modules.
- **Read-only by default** (T022, T025): `BoardView.CanContribute` and `WorkItemDetails.CanContribute`
  default to `false`, so a screen that is not told otherwise offers no changes rather than changes that
  would be refused.
- **Removed while working** (T025, `P2_US1_AS5`): when a board action returns `NotFound` or `Forbidden`,
  the board reloads, so a person removed from the team sees "Not found" at once. The not-found text now
  reads "This page does not exist, was removed, or is not available to you." (FR-003: the same page for a
  missing project and one the person may not see).
- **Rows waiting for US2** (T010): the matrix rows "Assign tasks and set their dates" and "See "My
  tasks"" are in `PendingUntilUs2` and reported as skipped until T048.
- **Team contract** (T039): `TeamMemberInfo` carries `CanWork` instead of the role, so the Work module never
  touches the Projects domain's `ProjectRole` (module rule).
- **Open items for "My tasks"** (T038, T042): an item is open exactly while `ResolvedAt` is null (Phase 1
  FR-027 keeps the two in step), so "My tasks" filters without reading every project's columns, and
  `IX_WorkItems_Assignee_Live` includes `ResolvedAt`. The page's status names come from a new batch read,
  `IProjectWorkflow.StatusesAsync(projectIds)`. Project names live in the Projects module, so the service
  reads the sort keys of the caller's open assigned items, keeps the visible projects, orders in memory and
  then reads one page.
- **Board filters** (T045): "Only my tasks" is a shortcut for the assignee filter's "Me" (one filter
  state); the filter offers the people the cards are assigned to. While a filter is on, each column shows
  "matching of total" and its limit as "limit N".
- **Assignee and dates in the drawer** (T044): `NotAssignable` reloads the drawer so its choices are
  current; an assignee who can no longer work on the project stays in the list, disabled, as "(no longer
  on the project)". Both dates go in one edit, and refused dates stay in the fields with the reason.
- **Key suggestion race** (found by the browser tests, a Phase 1 screen): a suggested project key that
  arrived after the person had moved into the key box could be written into it while they typed ("KD" plus
  "KDR" became "KDKDR"). The create-project dialog no longer writes a suggestion while the key box has
  focus, and the browser journeys wait for the suggestion before typing a key, as a person would.
- **Accessibility scans wait for animations**: the drawer slides in over 0.18 s, and a scan during the
  slide measured half-transparent text; the axe helper now waits for running animations to finish.
- **Today in component tests**: the bUnit base registers the viewer's time zone and `ViewerToday` (UTC
  unless a test registers another account service).
- **Seeded teams** (T027, research R15): the largest project has 40 Members so that every tenth simulated
  user works on it as a different person. Simulated Project Admins add someone as a Viewer and later
  remove them, so the contributors the other simulated users act as stay in their teams.
