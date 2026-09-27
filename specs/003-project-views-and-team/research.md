# Research: Project Views and Team (Phase 2)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Phase 2 keeps every Phase 1 decision (`specs/002-kanban-project-core/research.md`, R1–R26) and extends the
design along the additive path Phase 1 prepared in its R26. The Technical Context had no open questions;
the decisions below settle how each Phase 2 capability fits the existing modules.

## Team and access

### R1. Project membership is part of the Project aggregate

- **Decision**: a new `ProjectMembers` table (project, user, role, added by, added at), owned by the
  `Project` aggregate like its statuses. `Project` gains `MembersVersion`, a concurrency token incremented
  by every membership change, just as `BoardVersion` guards the columns. The aggregate enforces the rules:
  one membership per person and project; the creator becomes the first Project Admin; a change that
  would reduce the number of Project Admins with active accounts to zero is refused (`LastProjectAdmin`).
  The domain cannot see account status (Identity module), so the service passes in the set of active
  member IDs read through `IUserDirectory`.
- **Rationale**: the same pattern as the board columns (R11 of Phase 1), already familiar to the code and
  tests. One version for the whole team gives both conflict detection (FR-013) and the last Project Admin
  guarantee: two Project Admins removing each other read the same version, so only one save succeeds.
- **Alternatives considered**: a separate aggregate with a row version per membership plus a per-project
  lock for the last-admin rule (two mechanisms instead of one); storing roles in a JSON column on
  `Projects` (unqueryable for the project list and "My tasks").

### R2. `IProjectAccess` switches to membership, without caller changes

- **Decision**: the single policy point (Phase 1 R7) keeps its interface and rights. Its rules become:

  | Right | Phase 2 rule |
  |-------|--------------|
  | `View` | any member (Viewer, Member, Project Admin) or an administrator |
  | `Contribute` (create, edit, move, assign, schedule, comment) | Member or Project Admin, or an administrator |
  | `DeleteOwnWorkItem` | a contributor who created the item |
  | `Manage` (details, columns, members, delete any item) | Project Admin or an administrator |
  | `Restore` deleted items | administrators |

  A caller who is neither a member nor an administrator gets `NotFound`, exactly like an unknown project
  (FR-002); a member who lacks a right gets `Forbidden`. `ProjectAccessInfo` gains the caller's
  `ProjectRole` (null for an administrator who is not a member) and `CanContribute`. The role is read from
  `ProjectMembers` on every call, so removals and role changes apply at the next action (FR-011).
- **Rationale**: Phase 1 built for exactly this swap. Returning `NotFound` to non-members hides whether
  a project exists.
- **Alternatives considered**: `Forbidden` for non-members (reveals existence); caching roles per circuit
  (stale after a removal).

### R3. The upgrade backfills memberships in the migration, and audits them

- **Decision**: the `ProjectMembers` migration creates the table, then inserts in one set-based
  statement each project's owner as Project Admin and, as Members, every other person who created a work
  item in the project (including deleted ones), acted in its work items' history, or wrote a comment on
  them (FR-014). Each inserted membership is also written to `AuditEvents` as `MemberAdded` with no actor
  and the source "Phase 2 upgrade". The inserts skip existing rows, so re-running is harmless. A dedicated
  test migrates a database to the last Phase 1 migration, loads pilot-style data with SQL, applies the
  Phase 2 migrations and checks the memberships (SC-006).
- **Rationale**: the data needed (owners, creators, history actors, comment authors) is all in the
  database; doing it inside the migration makes the upgrade one step with no window in which projects
  have no members. Auditing keeps the membership trail complete (constitution III).
- **Alternatives considered**: a one-off console command after deployment (a window in which only
  administrators see projects); granting everyone access to existing projects (rejected by the user on
  2026-09-27).

### R4. The project list joins memberships

- **Decision**: `ProjectService.ListAsync` lists the projects the caller is a member of, with their role;
  administrators get every project, with their role where they are members and "administrator access"
  elsewhere. An index on `ProjectMembers(UserId)` including `Role` serves the lookup. The count of open
  tasks is unchanged. The owner column is replaced by the caller's role (FR-015).
- **Rationale**: one indexed join; paging and sorting by name stay as in Phase 1.
- **Alternatives considered**: filtering in memory after reading every project (does not scale with the
  number of projects).

### R5. Adding members: people search through the Identity contract

- **Decision**: `IUserDirectory` (Identity contract) gains `SearchActiveAsync(term, take)`, which finds
  active accounts by display name, user name or email address (prefix and contains matching, at most 20),
  and `UserDisplay` gains `UserName`. The members screen offers the matches that are not yet members; the
  service checks again that the chosen person is active and not a member.
- **Rationale**: the Projects module must not read the Identity tables directly (constitution V).
- **Alternatives considered**: a full user list in a drop-down (2,000 names); free-typed user names
  (error-prone).

### R6. Membership changes are audited through an Identity contract

- **Decision**: new `AuditEventType` values `MemberAdded`, `MemberRemoved` and `MemberRoleChanged`, written
  in the same transaction as the change, with the member as subject, the project key as target and the
  roles in the details (FR-012). The Projects module writes them through a small contract of the Identity
  module, `IMembershipAuditLog` with its own `MembershipChange` enum, which maps to `IAuditLog`.
- **Rationale**: the constitution requires membership changes in the audit log. `IAuditLog` takes the
  Identity module's `AuditEventType`, which the Projects module may not use (constitution V; the
  architecture tests caught the first version), so the contract names only the three membership changes.
- **Alternatives considered**: a separate project activity table (duplicates the audit log); moving
  `AuditEventType` into a contract (would expose every security event type to every module).

## Assignees and dates

### R7. Assignee column with validation through a Projects contract

- **Decision**: `WorkItems.AssigneeId` (nullable, foreign key to users). A new Projects contract,
  `IProjectTeam`, answers the Work module's questions: the team with roles and account status (for the
  drawer's choices and for marking assignees who can no longer work on the project), whether a person can
  be assigned (active Project Admin or Member), and which of a set of projects a person can see (for "My
  tasks"). `WorkItem.Assign` records the change with display-name snapshots as old and new values, like
  status names in Phase 1. Assigning is part of `Contribute` (FR-004, FR-016, FR-017, FR-024).
- **Rationale**: keeps membership knowledge in the Projects module and history readable after renames.
- **Alternatives considered**: querying `ProjectMembers` from the Work module (breaks the module rule);
  storing user IDs in history (unreadable once accounts change).

### R8. Start and due dates are `date` columns changed as one edit

- **Decision**: `WorkItems.StartDate` and `DueDate` are nullable `date` columns (`DateOnly`), with a check
  constraint that the due date is not before the start date and both lie in 2000–2099 (FR-018).
  `WorkItem.Schedule(start, due)` validates the same rules, records `StartDate` and `DueDate` history rows
  (ISO values, shown as "1 Oct 2026"), and changes `UpdatedAt` and the row version, so concurrent edits
  are detected (FR-023). The drawer and the timeline send both dates in one `WorkItemEdit.Dates`, so a bar
  moved on the timeline is one change set.
- **Rationale**: calendar dates have no time zone (FR-042); one edit for both dates avoids a half-applied
  move and gives one history entry per drag.
- **Alternatives considered**: `datetimeoffset` at midnight (time zone errors); separate edits for each date
  (two conflicts and two change sets per drag).

### R9. "Today" is the viewer's

- **Decision**: overdue marks and the timeline's today marker use the viewer's today, computed in the web
  layer from the viewer's time zone (`ViewerTimeZone`, Phase 1) and passed to the list and "My tasks"
  services where a filter depends on it ("overdue", "due in the next 7 days"). A task is overdue when it
  is open and its due date is before today (FR-020).
- **Rationale**: the same calendar date means the same thing to everyone, while "today" differs by time
  zone; passing it in keeps the services deterministic and testable.
- **Alternatives considered**: UTC today (wrong for users far from UTC).

## Views

### R10. Board: covering index, client-side filters, read-only for Viewers

- **Decision**: `CardView` gains the assignee (ID, display name, initials, whether they can still work) and
  the due date; `IX_WorkItems_Board` adds `AssigneeId` and `DueDate` to its included columns so a
  500-card board is still read from the index. "Only my tasks" and the assignee filter run in the board
  component over the cards it already has, so filtering is instant; each column shows "matching of
  total" while its work-in-progress marker keeps counting all cards (FR-021, FR-022). `BoardView` gains
  `CanContribute`: Viewers get no "What needs to be done?" boxes, no dragging and no "Move to" (FR-003).
- **Rationale**: no extra round trip per filter change; the index keeps SC-002.
- **Alternatives considered**: server-side filtering (a round trip for each toggle, and column counts that
  no longer describe the whole column).

### R11. List view: server-side filtering, sorting and paging with the state in the address

- **Decision**: `IWorkItemListService.ListAsync(projectKey, query, today)` filters in SQL by column,
  status type, priority, assignee ("me", "unassigned" or people), due ("overdue", "next 7 days", "no due
  date") and words in the title or description, sorts by key, title, priority, start date, due date or last
  update in SQL (priority through its rank order, undated tasks last), and returns 50 rows with the total.
  Sorting by status (column position, then board order) or by assignee (display name) depends on other
  modules' data, so for those two the service orders the matching IDs in memory (a project holds at most a
  few thousand tasks) and then reads the page. A new covering index `IX_WorkItems_Project_Live`
  (`ProjectId`, `Number`) over live items includes the list and timeline columns. The page keeps the query
  in its address (`?sort=due&dir=asc&assignee=me&due=overdue&q=…&page=2`), so a list can be bookmarked,
  shared and reloaded (FR-028–FR-031).
- **Rationale**: correct counts and paging at any size; shareable state without saved filters.
- **Alternatives considered**: loading every task into the browser (breaks the paging baseline);
  joining the users table from the Work module (breaks the module rule).

### R12. Timeline: positioned HTML bars, a small pointer script, keyboard in Blazor

- **Decision**: the timeline is plain HTML and CSS: one row per top-level task that is scheduled or has
  scheduled sub-tasks, a sticky label column (key and title, a button that opens the drawer), and a track
  where each bar is absolutely positioned from its dates (weeks 36 px per day, months 8 px, quarters 3 px),
  with week, month or quarter headings and a today line. Expanding a row shows its sub-tasks: scheduled
  ones as bars, unscheduled ones listed. Top-level tasks with no dates and no scheduled sub-tasks are in
  the "Unscheduled" list beside the track, 50 at a time, each with "Schedule" (today to six days later).
  Dragging uses a small script (`wwwroot/js/timeline.js`, pointer events) that previews the move and, on
  release, calls the component once with the day offsets; the component saves through
  `ITimelineService.RescheduleAsync`, which uses the same domain method and conflict check as the drawer.
  Keyboard: each bar is one tab stop (a button named with the task, dates, status and assignee); arrow
  keys move the pending bar by a day, Shift with an arrow changes only the due date, Ctrl with an arrow
  only the start date, Enter saves the pending change (or opens the drawer when nothing is pending) and
  Escape cancels; each adjustment is announced through the live region (FR-034–FR-040).
- **Rationale**: constitution V rules out a charting or Gantt library; bars are simple boxes. Alt is
  avoided because Alt+Left means "back" in browsers.
- **Alternatives considered**: an SVG chart (harder to make keyboard-accessible); a Gantt component
  library (new dependency, poor accessibility); saving on every key press (one history row per day moved).

### R13. "My tasks": assignee index and a visibility check

- **Decision**: `IMyTasksService.ListAsync(page)` reads the caller's live, open, assigned tasks and
  sub-tasks through a filtered index `IX_WorkItems_Assignee_Live` (`AssigneeId`, `ProjectId`, including the
  row's display columns), keeps only projects the caller can see (`IProjectTeam`), and orders by project
  name, due date (undated last), priority and key, 50 at a time; the page groups consecutive rows by
  project (FR-025). The page opens the shared drawer with `?task=` (FR-026).
- **Rationale**: one indexed read per page, however many projects exist.
- **Alternatives considered**: iterating over the user's projects (one query per project).

### R14. One drawer everywhere, read-only for Viewers

- **Decision**: the Phase 1 `TaskDrawer` is reused on the list, timeline and "My tasks" pages through the
  same `?task=` query parameter. It gains an assignee choice (the project's active Project Admins and
  Members, "Unassigned", and "Assign to me" when the viewer can be assigned), start and due date fields
  with an overdue mark, and assignee and due date in the sub-task list. `WorkItemDetails` gains
  `CanContribute`; when it is false every field is read-only and the comment box, sub-task box and
  delete action are hidden (FR-003, FR-017, FR-019, FR-021).
- **Rationale**: one place to edit a task, the same keyboard and focus behavior on every page.
- **Alternatives considered**: a separate read-only viewer (two components to keep in step).

## Quality

### R15. Performance: seeded teams and extended load scenarios

- **Decision**: `tools/Upms.Seed` gives every project its owner as Project Admin, 4–20 Members (40 for the
  largest board) and 0–3 Viewers; about 70% of open tasks get an assignee from the team and about half of
  all tasks get dates.
  The SC-002 suite adds list loads (sorted and filtered), timeline loads, "My tasks" loads, and saving an
  assignee, dates and a membership change, with each simulated user acting only in projects they belong
  to. Indexes are tuned until p95 ≤ 1 s holds (SC-002).
- **Rationale**: the Phase 1 method, extended to the new screens.
- **Alternatives considered**: none; the constitution requires the performance baseline for every screen.

### R16. Tests: the Phase 1 pyramid plus a migration test

- **Decision**: domain tests for membership, assignment and date rules; service tests on SQL Server for
  the access rules, memberships, list filters and sorts, timeline reads and "My tasks"; the upgrade test
  of R3; bUnit tests for the members screen, list, timeline keyboard and the drawer's new fields;
  Playwright journeys per story with axe scans at 1280 px and 360 px; and the data-driven permission
  matrix, which now reads `specs/003-project-views-and-team/contracts/permissions.md` (SC-003, SC-009).
- **Rationale**: constitution II; the matrix stays tied to the current contract.
- **Alternatives considered**: keeping the matrix on the Phase 1 document (it would test superseded rules).
