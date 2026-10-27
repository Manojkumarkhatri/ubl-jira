# Research: Jira Parity Pass

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This pass keeps every decision of Phases 1 to 4 (`specs/002-…` to `specs/005-…/research.md`). It stays additive:
new tables, new columns, new enum values and new indexes; no column changes meaning. Behavior follows today's Jira
Cloud wherever Jira has an equivalent (owner decision of 2026-10-07, spec Clarifications). Five areas needed real
choices: how "spaces" replace "projects" without breaking anything, the layout, the one Create form, how the
board gains lanes and sub-task cards, and how the new summary and "For you" pages stay fast.

## Spaces instead of projects

### R1. Only the words change; the code and the data keep "project"

- **Decision**: every text people read says "space", "spaces", "space key" and "Space admin": screens, page
  titles, messages from the Domain and Application layers, notifications and emails. Code identifiers, tables,
  enum values (`ProjectRole.ProjectAdmin`), error codes (`DuplicateProjectKey`), log messages and audit event
  names keep "project". Role names reach the screen through one helper, `RoleNames.Display(ProjectRole)`, which
  returns "Space admin", "Member" or "Viewer".
- **Rationale**: the spec says only the word changes (Assumptions). Renaming types, tables and error codes would
  touch every file and migration for no user benefit. It would also break the upgrade path and the audit trail's
  meaning.
- **Alternatives considered**: renaming everything (large, risky, no user value); keeping "project" in messages
  from the services (people would still see "project" in refusals, against SC-004).

### R2. New addresses, and one page that sends old addresses to them

- **Decision**: space pages move to `/spaces`, `/spaces/{key}/summary`, `/spaces/{key}/board`, `/list`,
  `/timeline`, `/backlog`, `/sprints/{n}`, `/stages`, `/members`, `/settings` and `/deleted`. "My tasks" becomes
  `/for-you/assigned`, and `/` opens `/for-you`. One component, `LegacyAddress.razor`, carries the routes
  `/projects`, `/projects/{*rest}` and `/my-tasks`. It replaces the old address with the new one, keeping the
  rest of the path and the query string (`?task=WEB-7`, filters), through `NavigationManager.NavigateTo(…,
  replace: true)`. During prerendering that call becomes an HTTP redirect, so bookmarks, old emails and
  notifications all land on the new page. In-app navigation is handled by the router without a server round
  trip. `EmailComposer` and every link built in code use the new addresses.
- **Rationale**: one mechanism covers full page loads and in-app navigation. Server middleware alone would miss
  in-app links, and Blazor's router would show "Not found" for an address it does not know.
- **Alternatives considered**: two `@page` routes on every page (old addresses would stay in the address bar
  and in shared links); server middleware only (see above).

### R3. A guard against "project" coming back

- **Decision**: a test in `Upms.Architecture.Tests`, `WordingTests`, reads the markup text of every `.razor`
  file, plus the string literals in `Upms.Domain`, `Upms.Application` and `Upms.Web` that become screen text,
  messages or emails. It fails on the whole words "project" or "projects" (any case). It skips identifiers,
  comments, `[LoggerMessage]` texts, error-code constants and a short, commented allowlist. The browser tests
  add a check on every page they visit: the visible text never contains those words (SC-004).
- **Rationale**: about 140 markup lines and 50 messages say "project" today. Without a guard, new screens and
  messages would bring the word back.
- **Alternatives considered**: a manual review only (does not last); localization resources (U-PMS is
  English-only, and moving every string into resources is a large change for a word swap).

## Layout

### R4. Today's Jira Cloud layout: a sidebar on every page, a space's views as tabs

- **Decision**: `MainLayout` gains `Sidebar.razor`, a `nav` labelled "Main", beside the page:
  - **For you**: the For you page;
  - **Recent**: a disclosure listing the 10 items and 5 spaces opened last;
  - **Starred**: a disclosure listing starred spaces;
  - **Spaces**: a disclosure listing starred spaces, then the ones opened last, up to 8, and "View all spaces";
  - **Filters**: a disclosure listing the person's own saved filters and those shared with them, up to 10 each,
    and "View all filters" (`/search`);
  - **Accounts**: administrators only.

  The header keeps only the sidebar button, the U-PMS name (to For you), the search box, **Create**, the bell and
  the user menu. Inside a space, `SpaceHeader.razor` (which replaces `ProjectHeader`) shows the space's name, its
  template, a star button and tabs. The tabs are links in a `nav` labelled "Space", with `aria-current` on the
  current one. A "…" menu button holds Members, Space settings and Deleted items.
- **Rationale**: these are Jira Cloud's placements (spec Clarifications). Disclosures, not hover menus, open with
  Enter and Space and read correctly in screen readers (constitution VI).
- **Alternatives considered**: hover fly-outs as in Jira (hard to use with a keyboard and on touch); keeping the
  header links (not Jira's layout).

### R5. The collapsed sidebar is saved on the person; on phones it is a panel

- **Decision**: a new column, `AspNetUsers.SidebarCollapsed` (bit, default 0), arrives with the profile that
  the header already loads during prerendering, so the first paint is already right. The sidebar button
  (`aria-expanded`, `aria-controls`) saves the choice through `IAccountService.SetSidebarCollapsedAsync`. Below
  768 px the sidebar is hidden and the same button opens it as a modal panel. Focus stays inside the panel,
  Escape closes it, and focus returns to the button; the saved choice does not apply at that width.
- **Rationale**: the spec wants the choice remembered per person (FR-021). A cookie or `localStorage` would be per
  browser, and `localStorage` is readable only after the first render, which makes the layout jump.
- **Alternatives considered**: `localStorage` (layout jump, per browser); a cookie (per browser).

### R6. Starred and recently opened spaces live in the Projects module

- **Decision**: two new tables, both keyed by (UserId, ProjectId):
  - `StarredProjects` (with `StarredAt`);
  - `ProjectVisits` (with `VisitedAt`). Only the 20 latest visits per person are kept: older ones are deleted
    when a visit is recorded.

  `SpaceHeader` records a visit once per page load, not on every render. It does so through
  `ISpaceNavigationService.RecordVisitAsync`, an `UPDATE`, then an `INSERT` when no row changed, under
  `UPDLOCK, HOLDLOCK`. Every read filters by `IProjectAccess.VisibleProjectIdsAsync`, so a space someone loses
  access to drops out of their sidebar at once (spec Edge Cases); its rows stay, harmlessly.
- **Rationale**: stars and visits belong to spaces, which the Projects module owns. Filtering at read time has
  no cleanup job and cannot leak.
- **Alternatives considered**: deleting rows when a member is removed (more coupling, and administrators lose
  access differently); a single "favorites" table with a kind column (YAGNI).

### R7. The sidebar loads in one call

- **Decision**: `ISpaceNavigationService.GetSidebarAsync()` (Projects) returns the starred spaces and the spaces
  opened last. The sidebar component adds the 10 recent items (`ISearchService.RecentAsync`) and the saved
  filters (`ISavedFilterService`) from the Work module's existing services, so no module calls another for it.
  The sidebar loads these once per page load. It reloads after a star or unstar and when a space page records a visit
  (a scoped `SidebarState` notifies it).
- **Rationale**: one round trip per page. Stars and recent spaces are small: at most 20 visits per person and a
  few stars.
- **Alternatives considered**: loading each section when it is opened (more round trips, and the "Spaces" section
  is open by default).

## Summary and For you

### R8. The Summary page: grouped queries over one covering index

- **Decision**: `ISpaceSummaryService.GetAsync(spaceKey)` (Work module) returns Jira's sections:
  - the last 7 days' completed (`ResolvedAt`), updated (`UpdatedAt`) and created (`CreatedAt`) counts, and the
    count of open items due in the next 7 days (the viewer's today, `ViewerToday`);
  - the status overview (by status type);
  - the priority breakdown and the types of work;
  - the team workload (open items per assignee, and unassigned);
  - the epic (or stage) progress (Phase 3's `Progress` rule);
  - the recent activity (the 20 latest history entries of the space's items in the last 30 days).

  Each section is one grouped SQL query that reads a range of a new filtered index, `IX_WorkItems_Summary
  (ProjectId) INCLUDE (Type, Priority, StatusId, AssigneeId, ContainerId, ParentId, CreatedAt, UpdatedAt,
  ResolvedAt, DueDate) WHERE IsDeleted = 0`. Recent activity reads a new index, `IX_WorkItemChanges_OccurredAt
  (OccurredAt) INCLUDE (WorkItemId, ActorId, Field)`, backwards, joined to the space's live items. History
  sentences reuse Phase 4's `HistoryList` wording. Counts link to `/search` with the space and the matching
  filter. Search gains a `resolved` date range for the "completed" link. Epic rows open the epic's drawer.
- **Rationale**: "updated" uses `UpdatedAt`, as the search page's "updated" filter does, so a count and the list
  it opens always agree. One covering index answers every count without touching the table.
- **Alternatives considered**: loading the space's items into memory and grouping there (megabytes per request at
  300 users); a `ProjectId` column on `WorkItemChanges` (it would need a backfill of an append-only table).

### R9. "For you": Jira's four tabs

- **Decision**: `/for-you/{tab}` with the tabs `worked-on` (the default, as in Jira), `viewed`, `assigned` and
  `starred`, as links with `aria-current`.
  - **Worked on**: the items the person created, changed or commented on in the last 30 days, newest first, 50
    at a time. It reads `WorkItemChanges` by actor through a new index, `IX_WorkItemChanges_Actor (ActorId,
    OccurredAt) INCLUDE (WorkItemId, Field)`, grouped by item and filtered to the spaces the person can see.
  - **Viewed**: the existing recent views (Phase 4 R16).
  - **Assigned to me**: the Phase 2 "My tasks" content, unchanged, moved into a component.
  - **Starred**: the starred spaces with their star buttons.
- **Rationale**: comments and label changes are written to the history, so the history is the single record of
  "worked on". The index makes it a range read per person.
- **Alternatives considered**: a separate activity table (duplicates history); using only `CreatedById` and
  `AssigneeId` (misses changes and comments).

## The Create form

### R10. One dialog in the layout, fed by one Work service

- **Decision**: `CreateWorkDialog.razor` lives once in `MainLayout` and opens over the page, so the address does
  not change (FR-001). Its data comes from `ICreateWorkService` (Work module):
  - `SpacesAsync()`: the spaces the caller can contribute to, with their template and work types, and the
    default space (the current one, else the last space the caller created an item in, from
    `IX_WorkItemChanges_Actor` with `Field = Created`, else the first by name);
  - `OptionsAsync(spaceKey)`: the types offered to the caller, the columns, the people who can be assigned, the
    epics or stages, and the active and planned sprints;
  - `FindParentsAsync(spaceKey, type, text)`: up to 20 parents by key or title, through the Phase 4 search words.

  The description uses `RichTextEditor`. Labels use a combobox that keeps them until the item exists. Closing
  with typed text opens a confirmation ("Discard this work item?"). A successful create shows "WEB-7 created",
  with a link, in a status message announced by `LiveAnnouncer`.
- **Rationale**: Jira's Create opens over the current screen. One service keeps the form to a few round trips:
  spaces on opening, options on choosing a space, parents while typing.
- **Alternatives considered**: a `/create` page (loses the page underneath); reusing each drawer section (one
  round trip per field, and the sections need an existing item).

### R11. Creating with every field: one transaction, one change

- **Decision**: `NewWorkItem` gains optional fields:
  - `ParentKey`, `Description` (editor JSON), `StatusId`, `AssigneeId`, `Priority`, `Labels`, `SprintId`,
    `StartDate`, `DueDate` and `Estimate`.

  `IWorkItemService.CreateAsync` applies them with the same domain methods and internal helpers as the drawer's
  edits, inside one transaction and one `ChangeContext`, so the history shows one change:
  - sub-tasks through `parent.AddSubtask` when `ParentKey` is given;
  - the assignee rules (`NotAssignable`), dates, estimate and labels;
  - the sprint join, with its scope event;
  - the stage lock for a status that is not "to do" (`StageLocks.CheckPlaceInAsync`);
  - mentions in the description and the assignment, which raise notifications through `INotificationCenter`;
  - the creator, and an assignee other than the creator, become watchers (Phase 4 FR-011).

  Refusals keep the form's text:
  - `TypeNotAllowed`: "This space no longer uses Bugs.";
  - `InvalidContainer`: "The parent item no longer exists.";
  - `Forbidden`: "You can no longer add work to this space."

  The list's, backlog's and board's inline create boxes keep their calls (FR-009).
- **Rationale**: Phase 1 to 4 rules stay in one place. The spec asks for one change in history (FR-006).
- **Alternatives considered**: creating the item and then calling `UpdateAsync` per field (many changes in history,
  many notifications, and a half-made item when one field is refused).

## Spaces, templates and work types

### R12. Columns on the board reuse the column service

- **Decision**: the board gains `AddColumnButton.razor` ("+" after the last column) and `ColumnMenu.razor` (a menu
  button in each column header) for people with `CanManageColumns`. Both call the existing `IBoardColumnService`
  with the board's `BoardVersion`, and then reload the board. Refusals and conflicts show the service's message,
  and the board shows the current columns. A Scrum board without an active sprint now shows its columns, empty,
  under its "No active sprint" message (FR-013).
- **Rationale**: the rules, audit and conflict handling exist already (Phase 1 FR-034–FR-041); only the place
  changes.
- **Alternatives considered**: a second column service for the board (duplicate rules).

### R13. The Blank template is a Kanban space with its own columns and work types

- **Decision**: the gallery offers Kanban, Scrum, Stage-gate and Blank. Blank creates a **Kanban** space (no new
  template value) with the columns and work types the creator chose. `Project.Create` gains an overload that
  takes a column list, checked with the existing column rules:
  - 1 to 10 columns;
  - unique names;
  - at least one To do and one Done;
  - the name rules.

  The gallery's previews come from one static description per template (`TemplateCatalog`): columns, work types
  and views. The gallery is a `radiogroup` of cards, and the preview is announced when a card is selected.
- **Rationale**: the spec says a blank space behaves like a Kanban space (FR-017). A new template value would
  have to be handled in every template switch and every view.
- **Alternatives considered**: a `Custom` template value (touches every switch on template); creating a Kanban
  space and then editing its columns (several steps, and an in-between state).

### R14. Work types per space: a set of flags on the space

- **Decision**: `Projects.WorkTypes` (int, not null, default 15) holds a `WorkTypeSet` flags enum: Epic 1, Story 2,
  Task 4, Bug 8. Sub-task is always on. Stage-gate spaces ignore the set.
  - `ProjectAccessInfo` carries the set, so every service knows it without another query.
  - `WorkItemTypes.IsOffered(type, template, set)` replaces the two-argument check. It is used by `WorkItem.Create`,
    `WorkItemService`, `BoardService`, the inline create boxes and the Create form.
  - `Project.SetWorkTypes(set, typesInUse)` refuses an empty Story/Task/Bug choice (`NoStandardType`). It refuses
    turning off a type that live or deleted items use (`WorkTypeInUse`, with the count). The count comes from the
    Work module through the existing contract `IProjectPlanSetup`, which gains `TypeCountsAsync`.
  - Each change bumps `DetailsVersion` and writes the audit event `ProjectWorkTypesChanged`. A switch between
    Kanban and Scrum keeps the set.
- **Rationale**: four booleans fit one column, and the access record already flows everywhere.
- **Alternatives considered**: a child table of types (joins for no gain); per-template fixed types (the owner
  chose per-space types).

## The board

### R15. Jira-style cards and epic colors

- **Decision**: `TaskCard.razor` shows the summary first. Then, in a footer row: the type icon, the key (with the
  parent's key for a sub-task), the priority, the estimate, the sub-task progress ("2/5"), the due date and the
  assignee's avatar. The epic tag and the labels sit above the footer. The avatar shows the person's initials in a
  circle whose color comes from their id, with their name as the accessible name.

  Epics get a color: `WorkItems.Color` (tinyint, null for other types), one of 8 named colors (Purple, Blue,
  Teal, Green, Yellow, Orange, Red, Gray). Each has text and background tokens that meet 4.5:1 in the light and
  dark themes.
  - A new epic takes `(Number − 1) mod 8`.
  - The migration gives existing epics the same color by the same formula.
  - `WorkItemEdit.Color` (contributors) changes it and writes a `Color` history entry.
  - Cards, list rows, timeline bars and search results use the tag. Stage tags keep a neutral style.
- **Rationale**: Jira assigns epic colors automatically and lets people change them. A fixed palette keeps the
  contrast checked.
- **Alternatives considered**: any color from a picker (contrast cannot be guaranteed); colors for stages too
  (not asked, and stages are already ordered and named).

### R16. Swimlanes are grouped in the browser, from the cards already loaded

- **Decision**: `Group by` (None, Epic or Stage, Assignee, Sub-tasks) is a select in the board toolbar, kept in the
  address as `group=epic|assignee|subtasks` with the quick filters (`BoardFilter`, using the `_requested`
  pattern). `BoardLanes.cs` groups the cards the board already loaded:
  - by container;
  - by assignee;
  - by parent: each parent with sub-tasks forms a lane holding its sub-task cards, and the others go to
    "Everything else".

  Lanes are in key order ("No epic", "Unassigned" or "Everything else" last). Each lane header is a button with
  `aria-expanded` and the count. Past 20 lanes, the rest start collapsed.
- **Rationale**: grouping needs no extra query and keeps the board at one read (SC-006).
- **Alternatives considered**: a server query per lane (more round trips); collapsed lanes in the address (noise in
  shared links).

### R17. Moving into another lane changes the epic, stage or assignee in the same change

- **Decision**: `IBoardService.MoveCardAsync` gains an optional `LaneChange`:
  - `Container(string? key)`;
  - `Assignee(Guid? userId)`.

  In one transaction and one `ChangeContext` it applies the lane change, with the drawer's rules, then the
  column move:
  - `PlaceIn` and `StageLocks.CheckPlaceInAsync` for a container, and `NotAssignable` for a person;
  - `StageLocks.CheckMoveAsync` for the column move.

  A sub-task cannot change epic or stage (`SubtaskFollowsParent`: "A sub-task follows its parent's epic."). In
  Sub-tasks lanes no lane change is offered. A refusal leaves the card where it was, with the message.

  - Drag and drop: each lane-and-column cell is a drop target.
  - Keyboard: the "Move to" menu gains a "Lane" list next to "Column" and "Position".
  - Assignment notifications and watchers follow the drawer's rules.
- **Rationale**: Jira's team-managed boards change these fields when a card changes lane (spec Clarifications).
  One change keeps the history and notifications coherent.
- **Alternatives considered**: two calls, assign then move (two changes, and a half-done move on refusal).

### R18. Sub-tasks are cards on the board

- **Decision**: the board query also reads the live sub-tasks of the cards it shows. On Scrum boards, that means
  the sub-tasks of cards in the active sprint; on Kanban and Stage-gate boards, those of live top-level cards. The
  "done in the last 14 days" rule applies to them.
  - A sub-task card shows its parent's key and takes its parent's epic or stage for tags, lanes and the
    "epics" quick filter.
  - The type quick filter gains "Sub-task".
  - Sub-tasks count toward column limits, as in Jira's issue count.
  - In a column they follow their parent's rank, then their own. Dropping one changes its column; it is not
    reordered among other cards.
  - The stage lock uses the parent's stage, as it already does (`StageLocks.BlockingGateAsync`).
- **Rationale**: Jira boards show sub-tasks as cards (spec Clarifications). The board already reads the
  space's sub-tasks for its progress counts, so the extra cost is small.
- **Alternatives considered**: a board option to hide sub-tasks (Jira's later setting; not needed now); free
  reordering of sub-tasks among cards (sub-task ranks are ordered within their parent).

## Cross-cutting

### R19. Permissions

- **Decision**: a new matrix, `contracts/permissions.md`, supersedes Phase 4's, with "space" wording and these
  new rows:
  - create from the Create form;
  - columns from the board (the same row as the settings);
  - turning work types on and off;
  - changing an epic's color;
  - moving a card into another lane;
  - starring a space;
  - seeing the Summary page, "For you" and the sidebar.

  `PermissionMatrixTests` reads the new file.
- **Rationale**: constitution III and the convention in CLAUDE.md.

### R20. Testing

- **Decision**:
  - Acceptance tests are named `JP_US{n}_AS{m}_…`, with browser journeys `JP_US1`–`JP_US5`.
  - Domain tests: the column-list overload, `WorkTypeSet` rules, epic color assignment, lane grouping (bUnit-free
    logic in `BoardLanes`).
  - Application tests on SQL Server for each service.
  - An upgrade test from the last Phase 4 migration: existing spaces get all four work types, existing epics
    their colors, and the old addresses redirect.
  - `WordingTests` (R3).
  - Every existing test that checks wording is updated, only for the wording (SC-005).
  - Performance: the seed adds stars, visits and epic colors. The SC-002 run adds the Create form options, the
    summary, the four "For you" tabs, the sidebar, a board with sub-task cards, and a lane move.
- **Rationale**: constitution II; the lessons in CLAUDE.md (browser tests use their own users and keys).

### R21. Migrations

- **Decision**: three additive migrations:
  1. `WorkTypesAndActivityIndex` (foundation): `Projects.WorkTypes` and `IX_WorkItemChanges_Actor`;
  2. `SpaceNavigation` (User Story 4): `StarredProjects`, `ProjectVisits`, `AspNetUsers.SidebarCollapsed`,
     `IX_WorkItemChanges_OccurredAt` and `IX_WorkItems_Summary`;
  3. `EpicColors` (User Story 5): `WorkItems.Color`, with the backfill of existing epics.

  `WorkItemChanges` stays append-only: indexes only, no data change.
- **Rationale**: one migration per story that needs one, as in Phase 4. Each is reviewed with its story.
