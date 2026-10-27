# Research: Templates and Planning (Phase 3)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Phase 3 keeps every decision of Phase 1 (`specs/002-kanban-project-core/research.md`, R1–R26) and Phase 2
(`specs/003-project-views-and-team/research.md`, R1–R16). It follows the additive path of Phase 1 R26: new
tables, new nullable columns and new enum values; no existing column changes meaning. The Technical Context
had no open questions; the decisions below settle how each Phase 3 capability fits the existing code.

## Where the new parts live

### R1. Sprints, gates and dependencies belong to the Work module

- **Decision**: the template is a property of the project (Projects module). Everything else is new
  behavior of work items and lives in the Work module: types, containers, estimates, sprints, the sprint
  report, stages with their gates, and dependencies. No new module is added.
- **Rationale**: every one of these reads or changes work items in the same transaction as the item
  itself: a status move must check the stage lock; a sprint move changes the item and its history; a
  dependency joins two items. A separate "Planning" module would need contracts for almost every Work
  operation (constitution V: simplicity). The module rule still holds: the Work module asks the Projects
  module only through `Upms.Application.Projects.Contracts`.
- **Alternatives considered**: a Planning module for sprints, gates and dependencies (many cross-module
  calls inside one transaction, for no isolation benefit); putting gates in the Projects module (gates
  belong to stages, which are work items).

## Templates and types

### R2. The template is a project column, shared through the common kernel

- **Decision**: `Projects.Template` (`Kanban`, `Scrum` or `StageGate`), not null, default `Kanban`, so the
  migration makes every existing project a Kanban project (FR-004). The enum `ProjectTemplate` lives in
  `Upms.Domain.Common`, beside `StatusCategory`, because the Work module needs it and may not use the
  Projects domain (architecture rule). `Project.Create` takes the template and seeds its columns (FR-002):
  Kanban as today; Scrum adds "In Review" (in progress) before "Done"; Stage-gate uses "Not Started",
  "In Progress", "On Hold" (in progress) and "Completed". `ProjectAccessInfo` and `BoardInfo` gain the
  template, so every Work service knows it without another query.
- **Rationale**: one column, read with the access check the services already make.
- **Alternatives considered**: a templates table with configurable workflows (custom workflows are out of
  scope); inferring the template from the columns (columns are customizable).

### R3. Template switches and default stages cross the module line through one Work contract

- **Decision**: a new Work contract, `IProjectPlanSetup`, used by the Projects module inside its own
  transaction:
  - `CreateDefaultStagesAsync` creates the four stages of a new Stage-gate project (Initiation, Planning,
    Execution, Closure) as work items with their gates (FR-003). They take the first four keys.
  - `HasOpenSprintsAsync` answers whether a Scrum project has an active or planned sprint, which blocks a
    switch to Kanban (FR-005, `SprintsOpen`).
  - `PrepareForScrumAsync` gives the project's cards one project-wide order when it becomes Scrum (R8).

  `Project.SwitchTemplate` allows only Kanban to Scrum and Scrum to Kanban (`TemplateSwitchNotAllowed`),
  uses the project's `DetailsVersion` for conflicts (the switch is on the settings screen), and the switch
  is written to the audit log (R17).
- **Rationale**: keeps the Projects module ignorant of work items while the switch stays one transaction.
- **Alternatives considered**: having the Work module read `Projects.Template` directly (breaks the module
  rule); seeding stages lazily on first visit (a project would exist without its stages).

### R4. Types carry a level; cards are top-level stories, tasks and bugs

- **Decision**: `WorkItemType` gains `Epic`, `Story`, `Bug`, `Stage` and `Milestone` (the column is already
  a 12-character string). A static `WorkItemTypes` table in the domain states each type's level (container,
  standard or sub-task), the templates that offer it, whether it is a **card** (Story, Task, Bug) and which
  types it can change into (Story, Task and Bug among themselves, agile projects only). `WorkItem.Create`
  replaces `CreateTask` for every type and refuses types the template does not offer (`TypeNotAllowed`);
  `WorkItem.ChangeType` records a `Type` history row and refuses anything else
  (`TypeChangeNotAllowed`) (FR-007–FR-011). Only cards appear on boards (FR-013): the board, the card
  ranker and the board index add `Type IN ('Task','Story','Bug')` to their "top-level" condition.
  Epics and milestones never need a card order and keep the rank `a0`; a stage's rank is its place in the
  stage order (R12).
- **Rationale**: one table of rules is easy to test and to read; the existing `ParentId IS NULL` meaning
  "top level" is kept, and "card" is one extra condition.
- **Alternatives considered**: separate tables per type (every view would need unions); a type-to-template
  table in the database (the sets are fixed in this phase).

### R5. Containers link through a new `ContainerId`, not `ParentId`

- **Decision**: `WorkItems.ContainerId` (nullable, a foreign key to `WorkItems`) holds a standard item's
  epic or stage. `ParentId` keeps meaning "the task of a sub-task". The domain checks that the container
  is an epic or stage of the same project and that only standard items have one (`InvalidContainer`);
  every change records a `Container` history row with the old and new container keys and titles (FR-015,
  FR-053). Deleting a container clears `ContainerId` on its items in the same change set, with a history
  row on each (FR-019); restoring the container does not re-link them.
- **Rationale**: `ParentId IS NULL` means "card" in the board index, the ranker and the rebalancer;
  reusing `ParentId` for containers would hide every item in an epic from the board.
- **Alternatives considered**: reusing `ParentId` with a type check everywhere (many queries to change and
  easy to get wrong); a link table (one container per item needs no table).

### R6. Milestones and estimates are columns with database checks

- **Decision**: a milestone uses `DueDate` as its date; `StartDate` and `Estimate` stay empty and it gets no
  sub-tasks, checked by the domain and by the check constraint `CK_WorkItems_Milestone` (FR-012).
  `WorkItems.Estimate` is `decimal(4,1)`, from 0 to 999.9 (`CK_WorkItems_Estimate`), allowed only on
  stories, tasks and bugs of agile projects; `WorkItem.SetEstimate` records an `Estimate` history row
  (FR-014).
- **Rationale**: the rules are small and fixed; the database guards them as it guards the dates.
- **Alternatives considered**: a separate milestone date column (the due date already means "the date it
  must be done"); estimates as integers (the spec allows one decimal place).

### R7. Progress is computed on read

- **Decision**: a container's progress counts its live standard items that are not milestones, and how
  many of them are resolved (`ResolvedAt` is set exactly while an item is in a "done" column, Phase 1
  FR-027); a container that is itself resolved shows 100% (FR-017). The count comes from a filtered index
  on `ContainerId`.
- **Rationale**: no stored counter to keep in step with moves, deletions and restores.
- **Alternatives considered**: a stored count per container (every status change would update two rows
  and could conflict with edits of the container).

## Order and concurrency

### R8. Scrum projects rank their cards across the whole project

- **Decision**: in Kanban and Stage-gate projects `Rank` stays the order within a column (Phase 1 R14). In
  Scrum projects it is one order over all of the project's cards, so the backlog, each sprint and each
  board column read the same order (FR-022). The card ranker takes a **scope** (column, or project for
  Scrum) and always places an item by a key between the target and its nearest smaller key in that scope,
  or after the largest or before the smallest key; it never asks for a key between two equal keys. In
  Scrum projects these lookups read `IX_WorkItems_Backlog`, which covers every live card by project and
  rank (the backlog itself filters open cards). The
  rebalancer renumbers a whole Scrum project at once. `PrepareForScrumAsync` (R3) renumbers the project's
  cards, deleted ones included, column by column from left to right, keeping each column's order (FR-005);
  like rebalancing it records nothing, because no position on the board changes. A switch back to Kanban
  needs no work: a project-wide order is also a valid order within each column. A card restored in a Scrum
  project gets a new key at the bottom of the backlog.
- **Rationale**: Jira keeps one rank for the backlog and the board; the existing fractional keys support
  this with one row written per move.
- **Alternatives considered**: a second `BacklogRank` column (the board and the backlog would drift apart,
  against FR-022); making every project's rank project-wide (a data migration over 500,000 rows with no
  benefit to Kanban and Stage-gate projects).

### R9. One planning lock per project serializes structural changes

- **Decision**: an application lock per project (`upms:plan:{projectId}`), taken inside the transaction like
  the administrators lock of Phase 1 (`sp_getapplock`, released on commit or rollback, 15-second timeout).
  It is taken by the changes whose rules span several rows: adding a dependency (the loop check), creating,
  starting, completing and deleting sprints (one active sprint, sprint numbers), and every stage and gate
  change (stage order, requests, withdrawals, decisions, approval counts). Edits of single items keep
  their row versions (Phase 1 R16). A filtered unique index on active sprints backs the "one active
  sprint" rule in the database as well.
- **Rationale**: these rules read several rows and write others, which row versions alone cannot protect.
  Serializing per project costs nothing noticeable, because such changes are rare. Two approvers deciding
  at the same moment are both accepted, one after the other, instead of one being told to retry.
- **Alternatives considered**: a version counter on the project for each area (the Work module may not
  write `Projects`, and simultaneous approvals would conflict); serializable transactions (deadlock-prone
  and harder to reason about).

## Backlog and sprints

### R10. Sprints are a table; cards carry their sprint; the backlog is derived

- **Decision**: a `Sprints` table (project, number, name, goal, state, start and end dates, started and
  completed times, row version). Names default to "<key> Sprint <n>" with `n` one more than the project's
  highest sprint number (under the planning lock) (FR-023). `WorkItems.SprintId` is set only on cards;
  a sub-task is always in its parent's sprint, so it needs no column of its own (FR-024). Moving a card
  into or out of a sprint records a `Sprint` history row with the sprint names (FR-030). The **backlog** is
  every open card whose sprint is empty or completed, so an item reopened after its sprint ended returns to
  the backlog with no extra write (spec edge case). Deleting a card that is in a planned or active sprint
  clears its sprint in the same change set, so a restored card returns to the backlog. Starting a sprint
  checks its dates (the Phase 2 date rules, end not before start, default end 14 days after the start)
  and that no other sprint is active (`SprintAlreadyActive`, FR-025). Completing it moves its open cards to
  the backlog or to a planned sprint (FR-028). Completed sprints refuse every change (`SprintCompleted`).
  Creating, renaming, starting, completing and deleting sprints need the `Manage` right (decision of
  2026-09-28); moving cards between the backlog and sprints needs `Contribute`.
- **Rationale**: the spec's rules fall out of one nullable column and a derived definition; no background
  job or trigger is needed.
- **Alternatives considered**: storing sprint membership for sub-tasks too (two rows to keep in step); a
  membership table (an item is in at most one current sprint).

### R11. The sprint report is read from an append-only scope log

- **Decision**: `SprintScopeEvents` records, with the item's estimate at that moment: `Committed` for each
  card in the sprint when it starts; `Added` and `Removed` for cards moved in or out while it is active
  (deletion counts as removed); and `Completed` or `NotCompleted` for each card when it ends. The report
  lists each group with its items and estimate total (FR-029, SC-006). Rows are never changed, so the
  report cannot change after completion.
- **Rationale**: Jira builds its sprint report from change history; a dedicated log gives the same answer
  with simple queries and is easy to test.
- **Alternatives considered**: reconstructing the report from `WorkItemChanges` (harder queries, and
  estimates would have to be replayed); a frozen report snapshot only at completion (loses what was added
  and removed).

### R12. The Scrum board and creation follow the sprint

- **Decision**: a Scrum board reads the cards of the active sprint, all of its done cards included
  (FR-027), from `IX_WorkItems_Board`, which now also includes `SprintId`, `Type` and `Estimate`. With no
  active sprint the board shows an empty state with a link to the backlog. "What needs to be done?" on a
  Scrum board adds the card to the active sprint (an `Added` event); cards created anywhere else go to
  the bottom of the backlog. The default type is Story in Scrum projects and Task elsewhere (FR-010).
- **Rationale**: the spec's behavior with one extra filter on an existing covering index.
- **Alternatives considered**: a separate sprint board index (the board index already has the project and
  column keys).

## Stage gates

### R13. Stage order is the stage items' rank

- **Decision**: stages are work items of type Stage, ordered by their `Rank` among the project's live
  stages (then by ID). New stages go at the end by default, or after any stage from the current one on,
  whether they are added in the Stages view, from the list's "What needs to be done?" box or with a new
  Stage-gate project; each of these paths creates the stage's gate with it. A restored stage gets a key
  after the last stage and its gate returns to "Not requested", so it never locks started work;
  moving uses "Move up" and "Move down" and the same key placement as cards; changes record a `Rank`
  history row with positions, like card moves. The **current stage** is the first stage, in this order,
  whose gate is not approved; stages after it are **locked** (FR-031, FR-032). Stages with approved gates
  and the current stage keep their places; locked stages can be reordered among themselves (FR-039).
- **Rationale**: stages are containers, which otherwise need no rank; fractional keys give single-row
  moves and no position renumbering under a unique index.
- **Alternatives considered**: a `Position` column on the gate (renumbering every move conflicts with a
  unique index row by row); ordering by dates (stages may be undated and dates do not define the gate
  sequence).

### R14. Gates, requests and decisions

- **Decision**: one `StageGates` row per stage: approvals needed (null means all approvers), whether the
  requester is excluded (default yes), the state (Not requested, Awaiting decision, Approved, Sent back) and
  the current request, with a row version. `GateApprovers` lists the approvers. `GateRequests` records each
  request with its outcome (pending, approved, sent back, withdrawn, or cancelled when the stage is
  deleted). `GateDecisions` records each decision with its note, one per approver and request. The domain
  aggregate `StageGate` enforces the rules (FR-035–FR-038):
  - approvers, the number needed and the exclusion change only while no request is pending and the gate is
    not approved (`GateRequestPending`, `GateApproved`);
  - a request needs the current stage (`StageLocked`), at least one approver (`NoApprovers`), every
    approver still an active member (`ApproverCannotDecide`), and, when the requester is excluded and is an
    approver, enough other approvers to reach the number needed (`NotEnoughApprovers`);
  - a decision comes from an approver who has not decided on this request (`AlreadyDecided`) and is not
    the excluded requester; "send back" needs a note (validation);
  - the gate is approved when the approvals reach the number needed; one "send back" ends the request;
    an approved gate never changes again.

  Requests and decisions carry the request number the person saw, so a decision on a withdrawn or ended
  request is a `Conflict` that returns the current gate. Every gate change takes the planning lock (R9).
- **Rationale**: the rules follow Jira Service Management's approvals (decisions of 2026-09-28); an
  append-only decision log keeps the governance trail complete (constitution IV).
- **Alternatives considered**: storing decisions as history rows only (hard to query "waiting for me");
  one table with a JSON list of approvers (unqueryable for "My tasks").

### R15. The stage lock is one check on every path that changes a status

- **Decision**: an internal `StageLocks` reader answers, for a set of items, which locked stage holds them
  (through the item's container, or its parent's container for a sub-task). Every path that changes a
  status uses it before calling `WorkItem.Move`: the board, the drawer, "mark done" on a sub-task, and the
  column deletion of the Projects module (through `IWorkItemStatusMover`, which now also counts items of
  locked stages so that a column holding them can only send them to a "to do" column). A move out of a "to
  do" column is refused with `StageLocked`, naming the gate to approve (FR-032). Putting an item that is
  not in a "to do" column into a locked stage is refused too (FR-033). A stage cannot move into a "done"
  column before its gate is approved (`GateNotApproved`, FR-034). The lock applies to administrators too.
- **Rationale**: four call sites exist today; one reader and a test per path keep them in step (SC-005).
- **Alternatives considered**: a database trigger (business rules and messages would live in SQL); checking
  only in the user interface (constitution III requires server-side enforcement).

### R16. Approvers may come from anywhere; the Projects module adds them as Viewers

- **Decision**: the approver picker searches all active accounts through `IUserDirectory.SearchActiveAsync`
  (Phase 2 R5). When a chosen person is not a member, the Work module calls a new `IProjectTeam` method,
  `AddViewerAsync`, in the same transaction; the Projects module adds them as a Viewer, increments
  `MembersVersion` and writes `MemberAdded` to the audit log (Phase 2 FR-012). Recording a decision needs
  only the `View` right plus being an approver of the gate, so a Viewer can decide and still change
  nothing else (FR-037, FR-055). `TeamMemberInfo` gains `IsActive`, so the gate can tell which approvers
  can still decide.
- **Rationale**: the owner chose Jira's behavior (any user can be an approver) while keeping U-PMS's rule
  that only members see a project (decision of 2026-09-28).
- **Alternatives considered**: letting non-members see only the gate (a second visibility rule, against
  Phase 2 FR-002); giving approvers the `Contribute` right (they would gain more than a decision).

### R17. Gate history, the audit log, and "My tasks"

- **Decision**: each gate event adds a history row to the stage: `GateSetup` (approvers, number needed,
  exclusion), `GateRequested`, `GateWithdrawn`, `GateDecision` (approved or sent back, with the note) and
  `GateApproved` (FR-040, FR-053). The same events go to the audit log through a new Identity contract,
  `IPlanningAuditLog`, with its own `PlanningAuditEvent` enum mapped to new `AuditEventType` values, as
  Phase 2 did for memberships (Phase 2 R6); template switches use it too (FR-005). "My tasks" gains a
  section of gates that await the caller's decision: pending requests where the caller is an approver who
  has not decided and is not the excluded requester, in projects they can still see (FR-041).
- **Rationale**: the stage's history answers "what happened to this gate"; the audit log answers "who
  approved what" across projects.
- **Alternatives considered**: writing Identity's `AuditEventType` from the Work module (breaks the module
  rule, as found in Phase 2).

## Dependencies and the timeline

### R18. Dependencies: a link table, with the loop check under the planning lock

- **Decision**: a `Dependencies` table (project, the item waited for, the waiting item, who created it,
  when), unique per pair, indexed by each end and by project. Adding a dependency takes the planning lock,
  loads the project's links and refuses a link to the item itself, a repeat, a link within the item's own
  hierarchy (container, parent or sub-task), or one that would close a loop (a breadth-first search from
  the waiting item) (FR-047, FR-048). Both items get a history row (`DependencyAdded`, `DependencyRemoved`)
  (FR-052). Removing a dependency deletes the row, because the history of both items is the permanent
  record. Deleting an item deletes its links and records that on each linked item; restoring does not
  bring them back.
- **Rationale**: a project holds at most a few thousand links, so the whole graph fits in one small read;
  the lock prevents two opposite links being added at the same moment.
- **Alternatives considered**: a recursive SQL query for the loop check (no benefit at this size);
  soft-deleting links (the history already keeps the record).

### R19. Late flags are computed on read

- **Decision**: a dependency is late when the item waited for is open, both needed dates exist, and the
  waiting item's start (its start date, or its due date when it has none; a milestone's date) is before
  the due date of the item waited for (FR-051). The drawer, the timeline and the dependency list compute
  it from the dates they already read. No date is ever changed because of a dependency (SC-007).
- **Rationale**: a stored flag would go stale with every date or status change.
- **Alternatives considered**: storing a flag updated on every date change (two rows to update per edit).

### R20. The timeline groups by container and draws markers and arrows

- **Decision**: the Phase 2 timeline (positioned HTML bars, `timeline.js` for dragging, keyboard handling in
  Blazor) is extended:
  - **Rows** become groups: each container (stages in stage order, epics by first date, due date and key)
    is a bar with its progress, expandable to its items as Phase 2 expands sub-tasks; items in no
    container follow. A container without dates keeps its row, without a bar (FR-042).
  - **Milestones** are diamond markers; the keyboard moves them a day at a time, and Shift or Ctrl does
    nothing, because a milestone cannot be resized (FR-043).
  - **Gates** are markers on each stage's due date, labelled with their state (FR-044).
  - **The sprint lane** shows active and completed sprints as read-only bars (FR-046).
  - **Arrows** are drawn in one SVG layer under the bars, from each dependency's dates and row positions,
    for dependencies whose two items are shown, up to 200; the layer is hidden from assistive technology
    because each bar's accessible name already says what it waits for and whether that is late (FR-050).
  - **Actions**: each row's label gets an actions menu (open, schedule, add dependency), so dependencies
    can be added with the keyboard from the timeline (FR-047).
  - "Schedule" gives containers four weeks from today and milestones today (FR-045).
- **Rationale**: the same approach as Phase 2 (constitution V rules out a Gantt library); SVG lines are the
  simplest way to join two boxes.
- **Alternatives considered**: a Gantt component library (a new dependency with poor accessibility);
  drawing arrows with rotated HTML elements (fragile at different scales).

## Screens

### R21. The Backlog view and the sprint report

- **Decision**: `/projects/{key}/backlog` shows the active sprint, then the planned sprints, then the
  backlog, each as a list with key, type, title, epic, assignee, estimate and status. Sprint headers show
  the counts and totals of FR-026 and, for Project Admins, the sprint actions. The backlog shows 100 items
  at a time with "Show more". Mouse users drag rows (native drag and drop, as the board, Phase 1 R15);
  every row also has a "Move" menu: Top, Bottom, Up, Down, to any planned or active sprint, or to the
  backlog. Sprint dialogs (start, complete) are the existing modal dialog. The report is
  `/projects/{key}/sprints/{number}`, a set of tables with totals (FR-029).
- **Rationale**: the same controls and patterns as the board and list; the menu is the keyboard
  alternative to every drag (constitution VI).
- **Alternatives considered**: rendering every backlog item at once (slow with 1,000 items on a server-side
  circuit); a separate sprint planning screen (the backlog already is one).

### R22. The Stages view

- **Decision**: `/projects/{key}/stages` lists the stages in order. Each stage shows its dates, progress,
  place (approved, current or locked) and its gate: state, approvers and their decisions, the number
  needed and the exclusion setting. Project Admins can add stages, move locked stages, and set up gates
  (a person search over all active accounts, a number field and a checkbox). Contributors can request
  approval of the current stage, after a warning that lists its open items (FR-036), and withdraw a
  pending request. Approvers see "Approve" and "Send back" with a note box. A gate opened from "My tasks"
  scrolls to and focuses its stage.
- **Rationale**: one place for the whole governance flow; the stage's drawer links to it.
- **Alternatives considered**: putting the gate in the drawer (too much for one panel on a phone).

### R23. Changed screens

- **Decision**:
  - **Create project**: a radio group of the three templates, each with a one-line description (FR-001).
  - **Project header**: the view tabs depend on the template: Kanban shows Board, List and Timeline; Scrum
    adds Backlog; Stage-gate adds Stages. Opening a project still shows its board.
  - **Project list and settings**: a template column; the settings screen offers the Kanban/Scrum switch
    to Project Admins (FR-005, FR-006).
  - **Board**: cards show a type symbol and the estimate; the "What needs to be done?" box has a type
    choice; the Scrum board shows the sprint's name, goal and dates.
  - **List**: type and epic or stage columns and filters, kept in the address as `kind=` and `container=`
    (a key, or `none`); the create box offers every type the caller may create (FR-020). `type=` keeps its
    Phase 2 meaning, the status type, so that saved addresses still work.
  - **Drawer**: type (changeable between Story, Task and Bug), epic or stage, estimate, sprint, the
    container's items and progress, "Waits for" and "Blocks" lists with late marks and add and remove
    actions, and, for a stage, its gate state with a link to the Stages view.
  - **My tasks**: every type, and a "Gates waiting for you" section first.
- **Rationale**: each change extends a Phase 2 component; no screen is replaced.
- **Alternatives considered**: a separate drawer per type (the Phase 2 lesson: one drawer everywhere).

## Quality

### R24. The upgrade changes nothing visible

- **Decision**: the migrations add the new tables and columns; `Projects.Template` defaults to `Kanban` and
  existing work items keep their `Task` and `Subtask` types, so nothing else changes (FR-004). A migration
  test, like Phase 2's upgrade test, migrates a database to the last Phase 2 migration, loads pilot-style
  data, applies the Phase 3 migrations, and checks that every project is Kanban with its items, columns,
  members, assignees, dates and history unchanged (SC-010).
- **Rationale**: the additive path of Phase 1 R26 needs no data movement.
- **Alternatives considered**: none.

### R25. Performance: seeded Scrum and Stage-gate projects and new load scenarios

- **Decision**: `tools/Upms.Seed` makes about a third of the projects Scrum (with epics, estimates, three
  completed sprints, one active and one planned) and a fifth Stage-gate (four stages with gates in every
  state, milestones and about 50 dependencies each). The SC-002 suite adds backlog loads (up to 1,000 open
  items), backlog reorders and sprint moves, sprint report and Stages view loads, gate requests and
  decisions, dependency changes, and timeline loads with containers, milestones and dependencies. New
  indexes: `IX_WorkItems_Backlog` (project, rank over live cards), `IX_WorkItems_Sprint` (sprint, rank),
  `IX_WorkItems_Container_Live`, and the included columns listed in the data model. Indexes are tuned until
  p95 ≤ 1 s holds (SC-002).
- **Rationale**: the method of Phases 1 and 2, extended to the new screens.
- **Alternatives considered**: none; the constitution requires the baseline for every screen.

### R26. Tests: the same pyramid, a new matrix, and a migration test

- **Decision**: domain tests for types, containers, estimates, milestones, sprints, the gate aggregate,
  the stage lock and the loop check; service tests on SQL Server for every story, including each status
  path under the stage lock and simultaneous gate decisions; the upgrade test of R24; bUnit tests for the
  backlog, the Stages view, the timeline's markers and keyboard, and the drawer's new sections; Playwright
  journeys per story with axe scans at 1280 px and 360 px. Acceptance tests are named `P3_US1_AS1_…`. The
  data-driven permission matrix now reads `specs/004-templates-and-planning/contracts/permissions.md`, which
  adds an **Approver** column (a Viewer named as an approver) (SC-005, SC-011). Every Phase 3 change is
  checked for its history row and, for template switches and gate events, its audit event (SC-008); each
  versioned or locked change has a concurrent-edit test (SC-009). The usability outcomes (SC-001, SC-003,
  SC-004, SC-012) are timed in the pilot, which the pilot plan gains for Phase 3.
- **Rationale**: constitution II; the matrix stays tied to the current contract.
- **Alternatives considered**: keeping the Phase 2 matrix (it would not test gates or sprints).
