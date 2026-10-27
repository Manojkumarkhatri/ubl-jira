---

description: "Task list for the Jira parity pass (specs/006-jira-parity)"
---

# Tasks: Jira Parity Pass

**Input**: Design documents from `specs/006-jira-parity/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable): tests for domain rules and
application services are written first and fail before implementation, and every acceptance scenario is
automated. Test methods of this pass begin with `JP_` and the scenario they prove (for example
`JP_US1_AS3_SubtaskNeedsParent`), so they are not confused with the tests of earlier phases.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5 of this spec)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): the existing tree (`src/Upms.Domain/`, `src/Upms.Application/`,
`src/Upms.Infrastructure/`, `src/Upms.Web/`, `tests/Upms.*.Tests/`, `tools/`, `docs/`), foldered by module
(Identity, Projects, Work, Notifications). Code keeps the name "project"; only screen text says "space" (research
R1). Contracts referenced below live in [contracts/](./contracts/); research decisions are cited as R1–R21 of
[research.md](./research.md).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Point the documentation at this pass

- [X] T001 [P] Add a "Jira parity pass documents" table (spec, plan, research, data model, contracts, tasks, quickstart of `specs/006-jira-parity`) and a status line to `README.md`
- [X] T002 [P] Add a note at the top of `specs/005-collaboration-and-search/contracts/permissions.md` that the matrix is superseded by `specs/006-jira-parity/contracts/permissions.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: "Spaces" on every screen and address (spec US4 scenarios 1–2, needed first because every later screen
and test uses the new words), work types on every space (needed by the Create form and the gallery), the
history-by-actor index, and the new permission matrix

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests (write first, must fail) ⚠️

- [X] T003 [P] Write `tests/Upms.Domain.Tests/Common/WorkTypeSetTests.cs`: `WorkItemTypes.IsOffered(type, template, set)` offers Sub-task always; Epic, Story, Task and Bug in Kanban and Scrum only when their flag is on; Stage, Task, Milestone and Sub-task in Stage-gate whatever the set; `WorkTypeSet.All` is 15 (data-model.md, "WorkTypeSet"; research R14)
- [X] T004 [P] Write `tests/Upms.Web.Tests/Shared/LegacyAddressTests.cs` with `SlowNavigationManager`-free bUnit navigation: `/projects` → `/spaces`; `/projects/WEB/board?task=WEB-1&labels=a` → `/spaces/WEB/board?task=WEB-1&labels=a`; `/projects/WEB/sprints/3` → `/spaces/WEB/sprints/3`; every redirect replaces the history entry (`JP_US4_AS2_OldAddressesLeadToNewPages`; research R2)
- [X] T005 [P] Write `tests/Upms.Architecture.Tests/WordingTests.cs`: fails on the whole words "project" or "projects" (any case) in the markup text of every `.razor` file under `src/Upms.Web/Components` and in string literals of `src/Upms.Domain`, `src/Upms.Application` and `src/Upms.Web` that become screen text, messages or emails; skips identifiers, comments, `[LoggerMessage]` texts, `const string …Code`/`ErrorCodes` constants, `nameof`, route templates of `LegacyAddress.razor`, and a commented allowlist (`JP_US4_AS1_NoScreenSaysProject`; research R3)
- [X] T006 [P] Write `tests/Upms.Domain.Tests/Projects/RoleNamesTests.cs`: `RoleNames.Display` returns "Space admin", "Member" and "Viewer" (research R1)
- [X] T007 [P] Write `tests/Upms.Application.Tests/Projects/WorkTypesAccessTests.cs`: a new space has all four work types; `ProjectAccessInfo.WorkTypes` carries the space's set; `IWorkItemService.CreateAsync` and `IBoardService.CreateInlineAsync` refuse a type whose flag is off with `TypeNotAllowed` and the message "This space no longer uses Bugs." (spec Edge Cases; research R14)

### Implementation

- [X] T008 Create `src/Upms.Domain/Common/WorkTypeSet.cs` (flags: Epic 1, Story 2, Task 4, Bug 8, All 15) and add `IsOffered(WorkItemType, ProjectTemplate, WorkTypeSet)` to `src/Upms.Domain/Work/WorkItemTypes.cs`; keep the two-argument overload as `IsOffered(type, template, WorkTypeSet.All)` only until T012 has replaced its callers, then remove it (make T003 pass)
- [X] T009 Add `Project.WorkTypes` (default `WorkTypeSet.All`) to `src/Upms.Domain/Projects/Project.cs`, map it in `src/Upms.Infrastructure/Persistence/Configurations/Projects/ProjectConfiguration.cs` (int, not null, default 15), add the index `IX_WorkItemChanges_Actor (ActorId, OccurredAt) INCLUDE (WorkItemId, Field)` in `src/Upms.Infrastructure/Persistence/Configurations/Work/WorkItemChangeConfiguration.cs`, and add migration `WorkTypesAndActivityIndex` in `src/Upms.Infrastructure/Persistence/Migrations/` (indexes only on `WorkItemChanges`; research R21)
- [X] T010 Add `WorkTypes` to `ProjectAccessInfo` in `src/Upms.Application/Projects/Contracts/IProjectAccess.cs` and fill it in `src/Upms.Application/Projects/ProjectAccess.cs`; add `WorkTypes` to `BoardView` (`src/Upms.Application/Work/IBoardService.cs`) and to `BacklogView` in `src/Upms.Application/Work/IBacklogService.cs`
- [X] T011 Create `src/Upms.Domain/Projects/RoleNames.cs` (`Display(ProjectRole)`) and use it wherever a role name reaches the screen: `src/Upms.Web/Components/Pages/Members/MembersPage.razor`, the Projects list, `src/Upms.Application/Projects/ProjectMemberService.cs` messages, and `src/Upms.Application/Projects/ProjectAccess.cs` refusals (make T006 pass)
- [X] T012 Use the space's set in every work-type check: `WorkItem.Create` in `src/Upms.Domain/Work/WorkItem.cs` (takes the set), `src/Upms.Application/Work/WorkItemService.cs`, `src/Upms.Application/Work/BoardService.cs`, and the inline create type lists in `src/Upms.Web/Components/Pages/Board/BoardPage.razor`, `src/Upms.Web/Components/Pages/Backlog/BacklogPage.razor` and `src/Upms.Web/Components/Pages/List/ProjectListView.razor`; refusals say "This space no longer uses Bugs." (make T007 pass)
- [X] T013 Move every space page to `/spaces/…` (`@page` of `Projects/ProjectList.razor`, `Board/BoardPage.razor`, `List/ProjectListView.razor`, `Timeline/TimelinePage.razor`, `Backlog/BacklogPage.razor`, `Sprints/SprintReportPage.razor`, `Stages/StagesPage.razor`, `Members/MembersPage.razor`, `Settings/ProjectSettings.razor`, `Settings/DeletedTasks.razor`), every link built in `.razor` and `.cs` files (search `projects/`), `EmailComposer` in `src/Upms.Application/Notifications/EmailComposer.cs`, and `/` in `src/Upms.Web/Components/Pages/Home.razor`; add `src/Upms.Web/Components/Shared/LegacyAddress.razor` with `@page "/projects"` and `@page "/projects/{*Rest}"` (make T004 pass; research R2)
- [X] T014 Replace "project", "projects", "project key", "Project Admin" and "Project settings" with "space", "spaces", "space key", "Space admin" and "Space settings" in all screen text, page titles, messages, notifications and emails: every `.razor` file under `src/Upms.Web/Components`, `src/Upms.Web/Components/Layout/NotificationText.cs`, `src/Upms.Application/Notifications/EmailComposer.cs`, and every user-facing message in `src/Upms.Domain` and `src/Upms.Application` (`AppError.NotFound("project")` becomes "space"); code, tables, error codes and log messages keep "project" (make T005 pass; research R1, R3)
- [ ] T015 Update every existing test whose expectations name the old words or addresses, and only those expectations (SC-005): `tests/Upms.Web.Tests/**`, `tests/Upms.Application.Tests/**` (messages), and the browser tests with their helpers in `tests/Upms.E2E.Tests/Fixtures/` (addresses `/spaces/…`, "Create space", "Space admin", "Space settings")
- [X] T016 Copy the Phase 4 rows of `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs` to the renamed rows of `specs/006-jira-parity/contracts/permissions.md`, and point the test at that file; the rows that are new in this pass fail until their story adds operations (research R19)
- [ ] T017 Run the whole suite (domain, architecture, web, application, browser): everything from Phases 1–4 passes on the new words and addresses

**Checkpoint**: U-PMS says "space" everywhere, old addresses work, and every space has work types

---

## Phase 3: User Story 1 - Create any work item from anywhere (Priority: P1) 🎯 MVP

**Goal**: A **Create** button on every screen opens one form that creates any work type the space offers, with
every field, sub-tasks and stages included, in one change

**Independent Test**: spec US1 "Independent Test": from "My tasks" (later "For you"), create an epic, a story in
it with assignee, priority and labels using "Create another", and a sub-task of the story

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T018 [P] [US1] Write `tests/Upms.Application.Tests/Work/CreateWithFieldsTests.cs`:
  - `JP_US1_AS2…`: every field of data-model.md's `NewWorkItem` is saved; the history shows one change (one `ChangeSetId`) with each value;
  - `JP_US1_AS3_SubtaskNeedsParent`: `ParentRequired` without a parent; `InvalidParent` for a parent of another space or a deleted one ("The parent item no longer exists.");
  - `JP_US1_AS4…`: a story with an epic parent lands in it; in Stage-gate a task or milestone with a stage, and the stage lock refuses a status other than "to do" in a locked stage;
  - `JP_US1_AS5…`: a Scrum task with a planned sprint joins it, with a scope event; without one, it goes to the bottom of the backlog;
  - `JP_US1_AS6…`: the assignee gets the assignment notification and watches; mentions in the description notify; the creator watches; labels are added with their rules; the search finds the item at once;
  - `NotAssignable`, `InvalidDates`, the estimate rules and 21 labels are refused, and nothing is created;
  - "Stage" needs the Manage right;
  - a caller who became a Viewer meanwhile is refused with `Forbidden` ("You can no longer add work to this space.").
- [X] T019 [P] [US1] Write `tests/Upms.Application.Tests/Work/CreateWorkServiceTests.cs`:
  - `SpacesAsync` lists only spaces where the caller can contribute, by name, each with the types offered to the caller (no "Stage" without Manage; only flags that are on);
  - the default is the last space the caller created an item in (from history), else the first by name;
  - `OptionsAsync`: columns in order, assignable people, epics or stages (stages in stage order), active and planned sprints (Scrum), types;
  - `FindParentsAsync`: epics or stages for standard items and milestones, stories, tasks and bugs for sub-tasks; matching by key or title words; at most 20; never another space's items or deleted ones;
  - non-members get `NotFound` (research R10).
- [X] T020 [P] [US1] Write `tests/Upms.Web.Tests/Layout/CreateWorkDialogTests.cs` with a new `FakeCreateWorkService` and the existing `FakeWorkItemService` in `tests/Upms.Web.Tests/Fakes/`:
  - `JP_US1_AS1…`: opens with the current space (from the address) or the default, and the focus on Summary;
  - `JP_US1_AS2…`: changing the space reloads the options, and a stale answer for an earlier space is ignored (`SlowNavigationManager`-style delay); the fields follow the type (no estimate for epics, one date for milestones, sprint only in Scrum);
  - `JP_US1_AS7…`: "Create another" keeps the space, type and parent and clears the summary and description;
  - `JP_US1_AS9…`: Escape or Cancel with typed text asks "Discard this work item?", and "Keep editing" keeps the text;
  - a refusal shows the message beside the field and keeps every value;
  - success shows "WEB-7 created" with a link and announces it;
  - saves run one at a time.
- [X] T021 [P] [US1] Write `tests/Upms.Web.Tests/Layout/HeaderCreateButtonTests.cs`: **Create** is shown when the profile can contribute to at least one space and hidden for someone who is a Viewer everywhere (`JP_US1_AS8…`)
- [X] T022 [P] [US1] Write `tests/Upms.E2E.Tests/JP_US1_CreateFromAnywhereTests.cs`: the spec's independent test end to end (epic, story with "Create another", sub-task), each item checked on the board, list and timeline; a Viewer sees no **Create**; axe on the open dialog at 1280 and 360 px; own users and space keys per test (CLAUDE.md lessons)
- [X] T023 [P] [US1] Add operations for the matrix rows "Create work items … inline or from the Create form" (the Create form path) and "Get the Create form's spaces, options and parents" in `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`

### Implementation for User Story 1

- [X] T024 [US1] Add `ParentRequired` and `InvalidParent` to `src/Upms.Application/Common/Results/ErrorCodes.cs`; extend `NewWorkItem` in `src/Upms.Application/Work/IWorkItemService.cs` with `ParentKey`, `Description`, `StatusId`, `AssigneeId`, `Priority`, `Labels`, `SprintId`, `StartDate`, `DueDate`, `Estimate` (all optional)
- [X] T025 [US1] Extend `CreateAsync` in `src/Upms.Application/Work/WorkItemService.cs`: sub-tasks through `parent.AddSubtask`, then each field with the drawer's domain methods and helpers in one transaction and one `ChangeContext` (assignee rules, dates, estimate, sprint join with scope event in `SprintService`'s internal helper, labels through `LabelService`'s internal insert, stage lock via `StageLocks.CheckPlaceInAsync`, mentions and assignment through `INotificationCenter`, watchers for the creator and the assignee) (make T018 pass; research R11)
- [X] T026 [US1] Create `src/Upms.Application/Work/ICreateWorkService.cs` and `CreateWorkService.cs` (`SpacesAsync`, `OptionsAsync`, `FindParentsAsync`; the default space from `IX_WorkItemChanges_Actor` with `Field = Created`), register it with `AddOperationScoped` in `src/Upms.Application/ApplicationServiceCollectionExtensions.cs` (make T019 pass; research R10)
- [X] T027 [US1] Create `src/Upms.Web/Components/Layout/CreateWorkLauncher.cs` (scoped; `Open(string? spaceKey)`), `CreateWorkDialog.razor` (the fields of contracts/ui-routes.md "Create dialog", using `ModalDialog`, `RichTextEditor`, a parent combobox and a pending-labels combobox; `@bind` with `@bind:event="oninput"` for typed fields; one save at a time; a discard confirmation), render it once in `src/Upms.Web/Components/Layout/MainLayout.razor`, and add **Create** to `src/Upms.Web/Components/Layout/Header.razor`; styles in `src/Upms.Web/wwwroot/app.css` (make T020, T021 pass)
- [X] T028 [US1] Run T018–T023 and the browser suite; fix what fails

**Checkpoint**: User Story 1 is usable on its own: any work type, from anywhere, in one form

---

## Phase 4: User Story 2 - Change the board's columns on the board (Priority: P2)

**Goal**: Space admins add, rename, limit, move, retype and delete columns from the board, with the settings'
rules

**Independent Test**: spec US2 "Independent Test" on the `WEB` board

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T029 [P] [US2] Write `tests/Upms.Web.Tests/Board/BoardColumnControlsTests.cs` with fakes of `IBoardService` and `IBoardColumnService`:
  - `JP_US2_AS1…`: "+" adds a column with a name and a status type, at the end;
  - `JP_US2_AS2…`: Rename, Set limit, Clear limit, Move left and Move right call the service with the board version and show the result;
  - `JP_US2_AS3…`: "Change status type" is offered for empty columns and disabled with "Only empty columns can change type." otherwise;
  - `JP_US2_AS4…`: Delete asks where the cards go, and there is no Delete for the last To do or Done column;
  - `JP_US2_AS5…`: no "+" and no column menus without `CanManageColumns`;
  - `JP_US2_AS6…`: a refusal or `Conflict` shows the message and the current columns;
  - the menu works with the keyboard (menu button pattern) and returns focus to its button.
- [X] T030 [P] [US2] Add `JP_US2_FR013_ScrumBoardWithoutSprintShowsColumns` to `tests/Upms.Application.Tests/Work/BoardQueryTests.cs`: a Scrum board without an active sprint returns its columns with no cards
- [X] T031 [P] [US2] Write `tests/Upms.E2E.Tests/JP_US2_ColumnsOnBoardTests.cs`: the spec's independent test end to end, a Member sees no column controls, keyboard-only use of the column menu, axe at both widths
- [X] T032 [P] [US2] Map the renamed matrix row "Add, rename, reorder, limit, retype or delete board columns (on the board or in space settings)" to the existing operations in `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`

### Implementation for User Story 2

- [X] T033 [US2] Extract the delete-with-destination dialog of `src/Upms.Web/Components/Pages/Settings/BoardColumnsEditor.razor` into `src/Upms.Web/Components/Shared/DeleteColumnDialog.razor` and use it in both places
- [X] T034 [US2] Create `src/Upms.Web/Components/Pages/Board/AddColumnButton.razor` and `ColumnMenu.razor` (menu button, `aria-haspopup="menu"`, arrow keys, Escape) calling `IBoardColumnService` with `BoardView.BoardVersion`; put them in `BoardColumn.razor`'s header and after the last column in `BoardPage.razor`; reload the board after each change; styles in `src/Upms.Web/wwwroot/app.css` (make T029 pass; research R12)
- [X] T035 [US2] Show a Scrum board's columns without an active sprint: `src/Upms.Application/Work/BoardService.cs` and `BoardPage.razor` (columns under the "No active sprint" message) (make T030 pass; FR-013)
- [X] T036 [US2] Run T029–T032 and the browser suite; fix what fails

**Checkpoint**: Columns change where people see them; the settings editor still works

---

## Phase 5: User Story 3 - Create a space from a template gallery (Priority: P3)

**Goal**: A gallery with previews, a Blank template with chosen columns and work types, and work types that Space
admins turn on and off

**Independent Test**: spec US3 "Independent Test": a blank `DATA` space with four columns and Epic, Task and Bug

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T037 [P] [US3] Write `tests/Upms.Domain.Tests/Projects/ProjectBlankSetupTests.cs`: `Project.Create` with a column list keeps the order and status types; refuses 0 or 11 columns (`TooManyColumns`), duplicate names ignoring case (`DuplicateColumnName`), no To do (`LastToDoColumn`), no Done (`LastDoneColumn`), names over 30 characters; refuses work types without Story, Task or Bug (`NoStandardType`) (data-model.md; research R13)
- [X] T038 [P] [US3] Write `tests/Upms.Domain.Tests/Projects/ProjectWorkTypesTests.cs`: `SetWorkTypes` turns types on and off and bumps `DetailsVersion`; refuses `NoStandardType`; refuses turning off a used type with `WorkTypeInUse` and the message "Bugs are still used by 4 items in this space."; refuses Stage-gate spaces; `SwitchTemplate` keeps the set
- [X] T039 [P] [US3] Write `tests/Upms.Application.Tests/Projects/CreateSpaceTests.cs`:
  - `JP_US3_AS3…`: creating from each template makes the caller Space admin;
  - `JP_US3_AS4…`/`AS5…`: a Blank space is a Kanban space with exactly the chosen columns and work types;
  - `JP_US3_AS7…`: a non-member gets `NotFound`, and an administrator sees it;
  - two creates with the same key: one `DuplicateProjectKey` ("The key DATA is already taken.").
- [X] T040 [P] [US3] Write `tests/Upms.Application.Tests/Projects/WorkTypesTests.cs`:
  - `JP_US3_AS6…`: `SetWorkTypesAsync` for Space admins and administrators only;
  - `WorkTypeInUse` counts live and deleted items through `IProjectPlanSetup.TypeCountsAsync`;
  - `Conflict` on a stale `DetailsVersion`;
  - the audit event `ProjectWorkTypesChanged` with the before and after sets;
  - `GetAsync` returns the per-type counts;
  - the Create form and inline create follow the new set at once.
- [X] T041 [P] [US3] Write `tests/Upms.Web.Tests/Spaces/CreateSpaceDialogTests.cs`:
  - `JP_US3_AS1…`/`AS2…`: the gallery is a radiogroup of four cards, and selecting one shows its preview (columns, work types, views) from `TemplateCatalog`;
  - "Use template" leads to the details;
  - `JP_US3_AS4…`: Blank adds, renames, reorders and removes column rows, and checks work types, with Sub-task always on;
  - refusals show beside their field, and the typed values stay;
  - "Back" keeps the choices;
  - on success the dialog goes to the new space's board.
- [X] T042 [P] [US3] Write `tests/Upms.Web.Tests/Settings/WorkTypesEditorTests.cs`: four checkboxes with their item counts, "Save work types", the refusal message, hidden on Stage-gate spaces and for Members
- [X] T043 [P] [US3] Write `tests/Upms.E2E.Tests/JP_US3_CreateSpaceTests.cs`: the spec's independent test end to end, turning off a used type and back, keyboard-only use of the gallery, axe at both widths
- [X] T044 [P] [US3] Add operations for the matrix rows "Create a space from a template or the Blank template" (Blank path) and "Turn a space's work types on and off" in `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`

### Implementation for User Story 3

- [X] T045 [US3] Add the column-list `Create` overload and `SetWorkTypes` to `src/Upms.Domain/Projects/Project.cs`, the codes `NoStandardType` and `WorkTypeInUse` to `src/Upms.Application/Common/Results/ErrorCodes.cs`, and `ProjectWorkTypesChanged` to `src/Upms.Domain/Identity/AuditEventType.cs` (make T037, T038 pass)
- [X] T046 [US3] Add `TypeCountsAsync(projectId)` to the `IProjectPlanSetup` contract and implement it in `src/Upms.Application/Work/ProjectPlanSetup.cs` (live and deleted items per type)
- [X] T047 [US3] Extend `src/Upms.Application/Projects/IProjectService.cs` and `ProjectService.cs`: `CreateAsync(…, SpaceSetup? setup)`, `SetWorkTypesAsync`, `ProjectDetails.WorkTypes` with counts; create `src/Upms.Application/Projects/TemplateCatalog.cs` (each template's columns, work types and views) (make T039, T040 pass)
- [X] T048 [US3] Create `src/Upms.Web/Components/Pages/Spaces/CreateSpaceDialog.razor` (two steps), `TemplateGallery.razor` (radiogroup of cards with a preview) and `BlankSetup.razor` (column rows and work-type checkboxes); move `Projects/ProjectList.razor` to `Spaces/SpaceList.razor` and replace `CreateProjectDialog.razor`; styles in `src/Upms.Web/wwwroot/app.css` (make T041 pass)
- [X] T049 [US3] Create `src/Upms.Web/Components/Pages/Settings/WorkTypesEditor.razor` and add it to the space settings page (make T042 pass)
- [X] T050 [US3] Run T037–T044 and the browser suite; fix what fails

**Checkpoint**: Spaces start from a gallery, Blank spaces have their own columns and types, and any space can change
its types

---

## Phase 6: User Story 4 - Find your way like in Jira (Priority: P4)

**Goal**: Today's Jira Cloud layout: a sidebar on every page, space tabs with a "…" menu, stars, recent spaces, the
Summary page and "For you"

**Independent Test**: spec US4 "Independent Test"

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T051 [P] [US4] Write `tests/Upms.Application.Tests/Projects/SpaceNavigationTests.cs`:
  - `JP_US4_AS6…`: star and unstar are idempotent and personal;
  - visits keep the 20 latest per person;
  - `GetSidebarAsync` lists starred spaces, then spaces opened last (up to 8 in "Spaces", 5 in "Recent");
  - spaces the caller can no longer see drop out at once, while their rows stay (spec Edge Cases);
  - non-members get `NotFound` for star and visit;
  - `IsAdministrator`.
- [X] T052 [P] [US4] Write `tests/Upms.Application.Tests/Work/SpaceSummaryTests.cs` (`JP_US4_AS7…`), with a fake time provider and the viewer's time zone:
  - completed, updated and created in the last 7 days;
  - open items due in the next 7 days or overdue;
  - status overview by status type;
  - the 20 latest history entries of the space in the last 30 days, none from other spaces;
  - priority breakdown, types of work, team workload with "Unassigned";
  - epic (and stage) progress with the Phase 3 rule;
  - deleted items never counted;
  - an empty space gives zeros;
  - non-members get `NotFound`.
- [X] T053 [P] [US4] Write `tests/Upms.Application.Tests/Work/ForYouTests.cs`: `JP_US4_AS8…` "Worked on" lists items the caller created, changed or commented on in the last 30 days, newest first, once each, 50 a page, never items of spaces they cannot see or deleted items
- [X] T054 [P] [US4] Add to `tests/Upms.Application.Tests/Work/SearchServiceTests.cs`: the `Resolved` date range matches `ResolvedAt`; `project` and `space` in the address both filter the space (web test in T058)
- [X] T055 [P] [US4] Add to `tests/Upms.Application.Tests/Identity/AccountServiceTests.cs`: `SetSidebarCollapsedAsync` saves the choice and `GetProfileAsync` returns it
- [X] T056 [P] [US4] Write `tests/Upms.Web.Tests/Layout/SidebarTests.cs`:
  - `JP_US4_AS3…`: For you, Recent, Starred, Spaces (open by default), Filters, and Accounts for administrators only; disclosures with `aria-expanded`; the current place has `aria-current`;
  - `JP_US4_AS4…`: the button saves the collapsed choice, and the profile's choice applies on first render;
  - below 768 px the sidebar is a modal panel that keeps focus and closes on Escape;
  - it reloads after a star (`SidebarState`).
- [X] T057 [P] [US4] Write `tests/Upms.Web.Tests/Shared/SpaceHeaderTests.cs` (replacing `ProjectHeaderTests.cs`): `JP_US4_AS5…` tabs per template (Summary, Backlog for Scrum, Board, List, Timeline, Stages for Stage-gate) with `aria-current`; the "…" menu shows Members, Space settings (Manage) and Deleted items (administrators); the star button toggles `aria-pressed`; a visit is recorded once per page load
- [X] T058 [P] [US4] Write `tests/Upms.Web.Tests/Summary/SummaryPageTests.cs` and `tests/Upms.Web.Tests/ForYou/ForYouPageTests.cs`:
  - each Summary section has its heading, the counts link to `/search` with the right filters, and empty sections say so;
  - "For you" tabs are links following the address (`/for-you` shows Worked on), with the `_requested` pattern and stale answers ignored;
  - "Assigned to me" keeps the Phase 2 My tasks tests' behaviors (move those tests from `tests/Upms.Web.Tests/MyTasks/`);
  - "Starred" toggles stars;
  - `/my-tasks?…` leads to `/for-you/assigned?…`.
- [ ] T059 [P] [US4] Write `tests/Upms.E2E.Tests/JP_US4_FindYourWayTests.cs`:
  - the spec's independent test;
  - every page visited in the browser suite has no "project" in its visible text (a helper in `tests/Upms.E2E.Tests/Fixtures/`, called by every journey);
  - an old bookmark with `?task=`;
  - keyboard-only sidebar and tabs;
  - axe at 1280 and 360 px, sidebar open and collapsed.
- [X] T060 [P] [US4] Add operations for the matrix rows "Star and unstar the space…", "See 'For you' … and the sidebar", the summary in "See the space…", and "Choose one's own … sidebar layout" in `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`

### Implementation for User Story 4

- [X] T061 [US4] Create `src/Upms.Domain/Projects/StarredProject.cs` and `ProjectVisit.cs`; add `SidebarCollapsed` to `src/Upms.Domain/Identity/User.cs`; configurations in `src/Upms.Infrastructure/Persistence/Configurations/Projects/` and `Identity/UserConfiguration.cs`; `DbSet`s in `src/Upms.Application/Common/IAppDbContext.cs` and `src/Upms.Infrastructure/Persistence/AppDbContext.cs`; the indexes `IX_WorkItemChanges_OccurredAt` and `IX_WorkItems_Summary` (filtered `IsDeleted = 0`); migration `SpaceNavigation`; the new entities in `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs` (data-model.md; research R21)
- [X] T062 [US4] Create `src/Upms.Application/Projects/ISpaceNavigationService.cs` and `SpaceNavigationService.cs` (sidebar, star, unstar, record visit with `UPDLOCK, HOLDLOCK`, starred list), register it; add `SetSidebarCollapsedAsync` and `MyProfile.SidebarCollapsed` to `src/Upms.Application/Identity/IAccountService.cs` and `AccountService.cs`; `ProjectSummary.IsStarred` in `ProjectService.ListAsync` (make T051, T055 pass; research R5–R7)
- [X] T063 [US4] Create `src/Upms.Application/Work/ISpaceSummaryService.cs` and `SpaceSummaryService.cs` (one grouped query per section over `IX_WorkItems_Summary`; recent activity over `IX_WorkItemChanges_OccurredAt`, with the history sentences shared with the drawer's `HistoryList`), register it (make T052 pass; research R8)
- [X] T064 [US4] Create `src/Upms.Application/Work/IForYouService.cs` and `ForYouService.cs` ("Worked on" over `IX_WorkItemChanges_Actor`), register it; add `Resolved` to `SearchQuery` and its filter in `src/Upms.Application/Work/SearchService.cs` and the `space` address name (reading `project` too) in `src/Upms.Web/Components/Pages/Search/SearchPage.razor` and its state (make T053, T054 pass; research R9)
- [X] T065 [US4] Create `src/Upms.Web/Components/Layout/Sidebar.razor` and `SidebarState.cs` (scoped), change `MainLayout.razor` and `Header.razor` to the layout of contracts/ui-routes.md "Every page" (header links move to the sidebar), with styles for the sidebar, its collapsed state and the phone panel in `src/Upms.Web/wwwroot/app.css` (make T056 pass; research R4, R5)
- [X] T066 [US4] Create `src/Upms.Web/Components/Shared/SpaceHeader.razor` (name, template, `StarButton.razor`, tabs, "…" menu, visit recording) replacing `ProjectHeader.razor` on every space page, with a `Summary` value in `ProjectPage.cs` (make T057 pass)
- [X] T067 [US4] Create `src/Upms.Web/Components/Pages/Summary/SummaryPage.razor` (`/spaces/{key}/summary`) and `src/Upms.Web/Components/Pages/ForYou/ForYouPage.razor` with `WorkedOnTab.razor`, `ViewedTab.razor`, `AssignedTab.razor` (the content of `MyTasks/MyTasksPage.razor`, which is removed) and `StarredTab.razor`; add `@page "/my-tasks"` to `LegacyAddress.razor` leading to `/for-you/assigned`; star buttons on `Spaces/SpaceList.razor` (make T058 pass)
- [X] T068 [US4] Seed stars and visits for the load users in `tools/Upms.Seed/Seeder.cs`; add the summary, the four "For you" tabs and the sidebar to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R20)
- [ ] T069 [US4] Run T051–T060 and the browser suite; fix what fails

**Checkpoint**: U-PMS moves like today's Jira Cloud

---

## Phase 7: User Story 5 - Read the board like in Jira (Priority: P5)

**Goal**: Jira-style cards with colored epic tags, sub-tasks as cards, and swimlanes where moving a card into
another lane changes its epic, stage or assignee

**Independent Test**: spec US5 "Independent Test" on the `MOB` board

### Tests for User Story 5 (write first, must fail) ⚠️

- [X] T070 [P] [US5] Write `tests/Upms.Domain.Tests/Work/EpicColorTests.cs`: a new epic takes `(Number − 1) mod 8`; other types have no color; `SetColor` on a non-epic is `ColorNotAllowed`; a change writes a `Color` history entry with the old and new names; no entry when unchanged
- [X] T071 [P] [US5] Write `tests/Upms.Application.Tests/Work/BoardSubtaskCardsTests.cs`:
  - `JP_US5_AS6…`: sub-tasks of live top-level cards appear as cards in their status's column, with `ParentKey` and the parent's container;
  - on Scrum boards, only sub-tasks of cards in the active sprint;
  - the 14-day done window applies;
  - column card counts and over-limit include sub-tasks;
  - ordering follows the parent's rank, then the sub-task's;
  - moving a sub-task changes its column, and the stage lock of the parent's stage applies;
  - a deleted epic's items come back with no container, so they join "No epic" (spec Edge Cases).
- [X] T072 [P] [US5] Write `tests/Upms.Application.Tests/Work/LaneMoveTests.cs`:
  - `JP_US5_AS5…`: `MoveCardAsync` with `LaneChange.Container` changes the epic (or stage) and the column in one change (one `ChangeSetId`);
  - `LaneChange.Assignee` reassigns, with the assignment notification and the watcher;
  - `null` clears;
  - `SubtaskFollowsParent` for a sub-task's container change;
  - `NotAssignable` for someone removed or deactivated;
  - `StageLocked` for started work into a locked stage;
  - `Conflict` on a stale version;
  - Viewers are refused;
  - a refused move changes nothing.
- [X] T073 [P] [US5] Write `tests/Upms.Application.Tests/Work/EpicColorServiceTests.cs`: `UpdateAsync` with `WorkItemEdit.Color` for contributors (Viewers refused); `ContainerView.Color` reaches board cards, list rows, timeline bars and search results
- [X] T074 [P] [US5] Write `tests/Upms.Web.Tests/Board/BoardLanesTests.cs` (logic of `BoardLanes.cs`):
  - grouping by epic or stage, by assignee and by parent ("Sub-tasks");
  - lanes in key order, with "No epic", "No stage", "Unassigned" or "Everything else" last;
  - counts;
  - lanes past 20 start collapsed;
  - sub-task cards take their parent's epic lane;
  - the quick filters apply before grouping.
- [X] T075 [P] [US5] Write `tests/Upms.Web.Tests/Board/SwimlaneBoardTests.cs`:
  - `JP_US5_AS3…`: "Group by" follows the address (`group=epic|assignee|subtasks`), with the `_requested` pattern; lane headers toggle `aria-expanded`;
  - `JP_US5_AS4…`: moves within a lane send no `LaneChange`;
  - `JP_US5_AS5…`: a drop in another lane sends the lane change; "Move to" offers "Lane" for Epic, Stage and Assignee lanes but not for Sub-tasks lanes; a refusal returns the card with the message.
- [X] T076 [P] [US5] Write `tests/Upms.Web.Tests/Board/TaskCardTests.cs` for `JP_US5_AS1…`: the summary first; the type, key ("Sub-task of WEB-2"), priority, estimate, sub-task progress, due date, labels, epic tag and avatar, each with its text for screen readers; the epic tag's color class
- [X] T077 [P] [US5] Write `tests/Upms.Web.Tests/Drawer/EpicColorPickerTests.cs`: `JP_US5_AS2…` eight choices as a radiogroup with names, shown only for epics and to contributors, saving with the version
- [ ] T078 [P] [US5] Write `tests/Upms.E2E.Tests/JP_US5_BoardLikeJiraTests.cs`: the spec's independent test end to end, including a drag into another epic's lane and a keyboard "Move to › Lane" reassignment, axe at both widths in the light and dark themes
- [X] T079 [P] [US5] Add operations for the matrix rows "Move a card into another epic's, stage's or person's lane" and the epic color in "Edit title, … and an epic's color" in `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`

### Implementation for User Story 5

- [X] T080 [US5] Create `src/Upms.Domain/Work/EpicColor.cs`; add `Color` and `SetColor` to `src/Upms.Domain/Work/WorkItem.cs` (color at creation for epics), `Color` to `WorkItemField.cs`; map it (tinyint, null) in `WorkItemConfiguration.cs`; migration `EpicColors` with the backfill of existing epics (make T070 pass; research R15, R21)
- [X] T081 [US5] Add `ColorNotAllowed` and `SubtaskFollowsParent` to `ErrorCodes.cs`; `WorkItemEdit.Color` in `IWorkItemService.cs` and `WorkItemService.cs`; `ContainerView.Color` filled by the board, list, timeline and search reads (`BoardService.cs`, `WorkItemListService.cs`, `TimelineService.cs`, `SearchService.cs`); the history sentence for colors in `src/Upms.Web/Components/Pages/Drawer/HistoryList.razor` (make T073 pass)
- [X] T082 [US5] Read sub-task cards in `src/Upms.Application/Work/BoardService.cs` (`CardView.ParentKey`, parent's container, ordering, counts, done window), and let `MoveCardAsync` move a sub-task between columns (make T071 pass; research R18)
- [X] T083 [US5] Add `LaneChange` and the lane path of `MoveCardAsync` in `src/Upms.Application/Work/IBoardService.cs` and `BoardService.cs` (one transaction and one `ChangeContext`; container through `PlaceIn` and `StageLocks.CheckPlaceInAsync`, assignee through the drawer's assignee helper and notifications, then the column move) (make T072 pass; research R17)
- [X] T084 [US5] Create `src/Upms.Web/Components/Shared/Avatar.razor` and `EpicTag.razor`; redesign `src/Upms.Web/Components/Pages/Board/TaskCard.razor`; epic tags on list rows (`List/ProjectListView.razor`), timeline bars (`Timeline/TimelinePage.razor`) and search rows; the 8-color palette with light and dark tokens at 4.5:1 in `src/Upms.Web/wwwroot/app.css` (make T076 pass)
- [X] T085 [US5] Create `src/Upms.Web/Components/Pages/Board/BoardLanes.cs` and `LaneHeader.razor`; add `group` and the "Sub-task" kind to `BoardFilter.cs`; render lanes in `BoardPage.razor` with lane-and-column drop targets in `BoardColumn.razor`; add "Lane" to `MoveToMenu.razor` (make T074, T075 pass; research R16, R17)
- [X] T086 [US5] Create `src/Upms.Web/Components/Pages/Drawer/EpicColorPicker.razor` and add it to the epic's drawer in `TaskDrawer.razor` (make T077 pass)
- [X] T087 [US5] Seed epic colors and boards with sub-tasks in `tools/Upms.Seed/Seeder.cs`; add a board with sub-task cards and a lane move to `tests/Upms.Performance.Tests/Sc002LoadTests.cs`
- [ ] T088 [US5] Run T070–T079 and the browser suite; fix what fails

**Checkpoint**: All five stories work on their own and together

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T089 Write `tests/Upms.Application.Tests/Work/JiraParityUpgradeTests.cs`: from the last Phase 4 migration, with data inserted by SQL in the Phase 4 schema, the three migrations give every space all four work types and every epic its color, and leave every history row unchanged (count and content) (research R20, R21)
- [ ] T090 [P] Extend `tests/Upms.E2E.Tests/ResponsiveAndKeyboardTests.cs`: keyboard-only and 360 px checks for the sidebar panel, the Create dialog, the gallery, the column menu, lanes with "Move to › Lane", the Summary page and "For you" (FR-028, SC-007)
- [X] T091 [P] Update `docs/accessibility/phase1-screen-reader-review.md` with a "Jira parity pass" section (sidebar and panel, Create dialog, gallery, column menu, lanes, Summary, For you) for the manual screen reader check
- [X] T092 [P] Update `docs/pilot/phase1-pilot-plan.md`: a session for the new layout and the Create form, and the questions for SC-001, SC-003 and SC-008
- [X] T093 [P] Update `docs/operations/deployment.md` and `docs/operations/backup-restore.md`: the three migrations, the new tables, and the old addresses that keep working
- [X] T094 [P] Update `README.md` (features in Jira's words, "spaces", the new screens) and `CLAUDE.md` (layout, the Create form, lessons from this pass)
- [ ] T095 Run the performance suite (`tests/Upms.Performance.Tests`) and record the p95 of every new action (SC-006)
- [ ] T096 Run the three gates (`dotnet format --verify-no-changes` on an LF copy, `dotnet build -c Release`, `dotnet ef migrations has-pending-model-changes`) and every test suite, one database suite at a time (CLAUDE.md "Test machines")
- [ ] T097 Run quickstart.md by hand and record the results in `specs/006-jira-parity/validation.md`
- [ ] T098 Manual screen reader check of the new screens with NVDA (a person; T091's script)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories (every screen and test moves to
  "space" first; every check uses the work-type set)
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1, US2, US3 and US5 can proceed in parallel after the foundation
  - US4 replaces the header and the space header, which US1 (Create button) and US2/US5 (board) also touch:
    merge with care
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Starts after Foundational. Independent of US1.
- **User Story 3 (P3)**: Starts after Foundational. The Create form (US1) follows the work types it changes, through
  the foundation's set.
- **User Story 4 (P4)**: Starts after Foundational. Moves the **Create** button (US1) into the new header if US1 is
  in.
- **User Story 5 (P5)**: Starts after Foundational. Independent; its lanes sit beside US2's column controls on the
  board.

```text
Setup → Foundational ─┬─ US1 ─┐
                      ├─ US2 ─┤
                      ├─ US3 ─┼─ Polish
                      ├─ US4 ─┤   (US4's header after US1's Create button)
                      └─ US5 ─┘   (US5's lanes beside US2's column controls)
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Domain before persistence, persistence before services, services before screens
- The story's permission rows and end-to-end test pass last
- Commit only when the owner asks; push after each story's checkpoint when asked

### Parallel Opportunities

- T001 and T002 in parallel
- T003–T007 (foundational tests) in parallel, then T008–T017 in order (T013–T015 touch many files)
- All test tasks of a story marked [P] in parallel (different files)
- After the foundation: US1, US2, US3 and US5 in parallel; US4 after US1's header change
- Polish: T090–T094 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "T018 CreateWithFieldsTests"
Task: "T019 CreateWorkServiceTests"
Task: "T020 CreateWorkDialogTests (bUnit)"
Task: "T021 HeaderCreateButtonTests (bUnit)"
Task: "T022 JP_US1_CreateFromAnywhereTests (browser)"
Task: "T023 matrix operations"

# Then in order: T024 → T025 → T026 → T027 → T028
```

## Parallel Example: User Stories 2, 3 and 5

```bash
# After the foundation, several people can work at once:
Developer A: T029–T032 (tests) → T033 → T034 → T035 → T036   (columns on the board)
Developer B: T037–T044 (tests) → T045 → T046 → T047 → T048 → T049 → T050   (gallery and work types)
Developer C: T070–T079 (tests) → T080 → T081 → T082 → T083 → T084 → T085 → T086 → T087 → T088   (cards and lanes)
# A and C both touch BoardPage.razor and BoardColumn.razor; B and US1 both touch ErrorCodes.cs: merge with care.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational ("spaces" everywhere; work types)
3. Complete Phase 3: User Story 1 (the Create form)
4. **STOP and VALIDATE**: create an epic, a story and a sub-task from any screen
5. Show the owner

### Incremental Delivery

1. Setup + Foundational → "spaces" everywhere, old addresses still work
2. US1 → the Create form (MVP)
3. US2 → columns on the board
4. US3 → the gallery, Blank spaces, work types
5. US4 → the Jira Cloud layout, Summary, For you
6. US5 → Jira-style cards, sub-task cards, lanes
7. Polish → upgrade test, accessibility, performance, documents, validation

### Parallel Team Strategy

After the foundation, one person each on US1, US2, US3 and US5; US4 follows US1. Each story is tested and
validated on its own before merging.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps a task to its user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Code keeps "project"; only what people read says "space" (research R1)
- Browser tests: own users and space keys per test; `WaitForPathAsync` for in-app address changes; the "no
  project" check on every page (CLAUDE.md lessons)
- Every status change path calls `StageLocks`: the Create form's status, lane moves and sub-task card moves too
- Run one database suite at a time on a 4 GB Docker engine

---

## Implementation notes

Where the code differs from the wording of a task, and why.

- **Names in code.** Components and services keep "project" in their names, as research R1 decided for the code:
  `ProjectHeader.razor` (T066's `SpaceHeader.razor`), `ProjectList.razor` (`SpaceList.razor`),
  `ProjectHeaderTests.cs` (T057's `SpaceHeaderTests.cs`). Only what people read says "space";
  `tests/Upms.Architecture.Tests/WordingTests.cs` and the browser tests' "no project" check guard that.
- **Blank spaces (T047).** `IProjectService.CreateBlankAsync(name, key, description, SpaceSetup)` sits beside the
  existing `CreateAsync(…, template)` instead of one `CreateAsync` with an optional setup, so the template path is
  unchanged.
- **"Worked on" (T064).** It is `ISearchService.WorkedOnAsync`, next to `RecentAsync` ("Viewed"), instead of a new
  `IForYouService`: both read the same search rows and access rules.
- **"For you" (T067).** Each tab is its own page with its own address (`/for-you`, `/for-you/viewed`,
  `/for-you/assigned`, `/for-you/starred`) under a shared `ForYouTabs.razor`, as Jira's tabs are links. "Assigned to
  me" is the former My tasks page at its new address; `/my-tasks` leads there through `LegacyAddress.razor`.
- **Star button.** Its name is "Star KEY" in every state, with `aria-pressed` saying whether it is starred, so screen
  readers announce a toggle rather than a changing name.
- **Shared assignee and epic rules (T083).** The drawer's rules for the assignee and the epic or stage moved from
  `WorkItemService.UpdateAsync` into `src/Upms.Application/Work/ItemEdits.cs`, which the drawer and the board's lane
  moves both call, so a lane move cannot drift from the drawer.
- **Lane moves keep their order of checks (T083).** The rank is worked out before the lane change is applied, because
  renumbering a full column saves; then the lane, then the stage lock against the card's new epic or stage, then the
  column, all in one transaction and one change. A "Lane" chosen from "Move to" sends `CardPlacement.InPlace`, so the
  card keeps its place in its column.
- **Sub-task cards (T082).** They are read with one join on the cards the board can show, covered by
  `IX_WorkItems_Subtasks_Live`, which the `EpicColors` migration widens with the card fields (not listed in
  data-model.md's index table). Hidden done sub-tasks count in "Show all completed".
- **Epic tags everywhere (T081, T084).** `ContainerView.Color` reaches cards, list rows, backlog rows and search rows
  (`SearchResultRow.Container` is new). On the timeline an epic's own bar takes its color as a border, so the bar's
  text keeps its contrast over the progress fill.
- **Lanes (T085).** A lane exists only while it has cards (as Jira's "No epic" lane); the column headers sit above the
  lanes (`BoardColumn.HeaderOnly`) and each lane-and-column cell is `LaneCell.razor`. Lanes are `role="group"`, not
  regions, so a board of many lanes does not fill the landmark list.
