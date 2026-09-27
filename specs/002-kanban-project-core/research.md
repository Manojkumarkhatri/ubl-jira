# Phase 0 Research: Core Kanban Project (Phase 1)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-27

Every Technical Context item in the plan is resolved below; no `NEEDS CLARIFICATION` remains. Platform
decisions carried over from the earlier product plan (`specs/001-issue-tracker-mvp/research.md`) are
restated briefly so this document stands alone; decisions specific to Phase 1 are marked **New**.
Package versions are "latest stable compatible with .NET 10", pinned in `Directory.Packages.props`.

## Platform

### R1. Runtime and language

- **Decision**: .NET 10 (LTS, supported until November 2028), C# 14, SDK pinned in `global.json`.
- **Rationale**: required by the constitution; the longest support window for a system of record.
- **Alternatives considered**: .NET 9 (shorter support).

### R2. UI hosting model

- **Decision**: ASP.NET Core **Blazor Web App** with the **Interactive Server** render mode applied
  globally; the ASP.NET Core Identity account pages stay static server-rendered
  (`[ExcludeFromInteractiveRouting]`) because they set the authentication cookie.
- **Rationale**: an internal app on a low-latency network; UI events call application services
  in-process, so Phase 1 needs no public HTTP API to secure or version; fast first load.
- **Alternatives considered**: WebAssembly (needs a public API and duplicated authorization); MVC or
  Razor Pages (weaker for drag-and-drop boards and in-place editing).

### R3. Component library

- **Decision**: Microsoft Fluent UI Blazor for inputs, dialogs, menus and the drawer shell; the board,
  its columns and cards are custom components.
- **Rationale**: accessible, Microsoft-maintained components (constitution VI) without building
  comboboxes, dialogs and menus by hand.
- **Alternatives considered**: MudBlazor (more custom accessibility work); no library (large effort).

### R4. Solution structure and modules

- **Decision**: one deployable app from four projects (`Upms.Domain`, `Upms.Application`,
  `Upms.Infrastructure`, `Upms.Web`). Phase 1 has three modules, each foldered inside every layer:
  - **Identity**: accounts, first-run setup, audit log, organization settings.
  - **Projects**: projects and their board columns (the project's workflow).
  - **Work**: work items (tasks and sub-tasks), comments, history, the board read model.

  A module uses another only through its `Contracts` namespace; ArchUnitNET tests enforce this.
- **Rationale**: constitution V (one app, explicit boundaries). Later phases add modules (Views and
  Membership in Phase 2; Planning and Portfolio in Phase 3) instead of reshaping these.
- **Alternatives considered**: a single project with folders (boundaries not enforceable).

### R5. Data access

- **Decision**: EF Core 10 on SQL Server, one `AppDbContext`, per-module entity configurations,
  migrations in `Upms.Infrastructure`; no generic repositories, mediator or object-mapping libraries.
- **Rationale**: the least code for one database; EF Core covers row versions, query filters and
  UTC `datetimeoffset`.
- **Alternatives considered**: Dapper (hand-written SQL everywhere).

## Security

### R6. Sign-in, sessions and minimal account management

- **Decision**: ASP.NET Core Identity with cookie authentication.
  - Password policy: at least 12 characters, no character-class rules, plus a validator that rejects
    passwords containing the username (FR-005).
  - Lockout: 5 failed attempts lock the account for 15 minutes (FR-005).
  - Temporary passwords: 16 characters from a cryptographic random source, shown once to the
    administrator; `MustChangePassword` forces a change before any other page (FR-003, FR-004). The
    guard runs as middleware for HTTP requests and in the router for interactive navigation.
  - Minimal account screen: list, add, reset password, deactivate, reactivate (FR-003, FR-004).
    Deactivation rotates the user's security stamp.
  - Revalidation: the authentication state is revalidated every minute (security stamp and active
    flag), so a deactivated user's open session ends within a minute.
  - Idle timeout (FR-006): a `CircuitHandler` tracks inbound activity on the server; a small script
    shows the warning 2 minutes before expiry; "Stay signed in" calls `POST /account/keepalive` to
    renew the sliding cookie. The timeout (30 minutes) is stored in organization settings so a later
    phase can expose it without a schema change.
  - Audit: a derived `SignInManager` and the account service write security events to an append-only
    audit table (FR-010).
  - SSO readiness: company single sign-on can later be added as an external login linked to the same
    user records.
- **Rationale**: built-in, well-reviewed mechanisms that meet constitution III from Day 1.
- **Alternatives considered**: a separate identity server (unnecessary infrastructure); Windows
  authentication (built-in accounts were chosen).

### R7. Authorization: one policy point, Phase 1 rules (**New**)

- **Decision**: a fallback policy requires an authenticated, active user on every page and endpoint
  except sign-in, setup and health. Every application-service method first calls the Projects
  contract `IProjectAccess`, which returns the caller's rights on a project:

  | Right | Phase 1 rule |
  |-------|--------------|
  | `View`, `Contribute` (create, edit, move, comment) | any active signed-in user |
  | `DeleteOwnWorkItem` | the item's creator |
  | `Manage` (project details, columns, delete any item) | the project owner (its creator) or an Administrator |
  | `Restore` deleted items | Administrators |

  Phase 2 replaces the implementation of `IProjectAccess` with a membership lookup (Project Admin,
  Member, Viewer); no caller changes. Unknown or deleted projects and items return `NotFound`.
- **Rationale**: implements the spec's open workspace (FR-015) while keeping server-side checks on
  every read and write (constitution III), so Phase 2 changes one class, not every service.
- **Alternatives considered**: no checks until Phase 2 (would scatter authorization later); claims in
  the cookie (stale when rights change).

### R8. First-run setup

- **Decision**: `/setup` creates the first Administrator only while none exists, requires a one-time
  setup token from configuration (`Setup:Token`), and is closed permanently afterwards (FR-002).
- **Rationale**: prevents someone else on the network from claiming a fresh installation.
- **Alternatives considered**: a seeded administrator password in configuration.

### R9. Security hardening

- **Decision**: HTTPS with HSTS; a strict Content Security Policy (scripts from self only); anti-forgery
  on forms; the Blazor hub rejects cross-origin WebSocket requests (Origin must match
  `App:PublicBaseUrl`); rate limiting on sign-in (10 per minute per IP) and setup; `NuGetAudit` fails
  the build on high or critical advisories; secrets only from environment variables, user secrets
  (development) or the company secret store.
- **Rationale**: constitution III, defense in depth for company information.
- **Alternatives considered**: network perimeter only.

### R10. Plain-text rendering without an HTML sanitizer (**New**)

- **Decision**: titles, descriptions and comments are stored and displayed as plain text. A small
  `PlainText` component splits text into text and link segments (only `http` and `https` URLs) and
  renders them as normal Blazor markup, which is HTML-encoded by default; line breaks are kept with
  `white-space: pre-wrap`; links get `rel="noopener noreferrer"` and open in a new tab. No raw HTML
  (`MarkupString`) is ever rendered from user input.
- **Rationale**: FR-025, FR-030 and FR-044 with no rich-text or sanitizer dependency; script injection
  is impossible by construction. Plain text is also valid input for a Markdown renderer if formatting
  is added in a later phase, so no data migration is needed.
- **Alternatives considered**: Markdown with a sanitizer now (two libraries and an editor, which the
  spec defers).

## Board and work items

### R11. Board columns are the project's workflow (**New**)

- **Decision**: each board column is a `ProjectStatus` row: name, category (`ToDo`, `InProgress`,
  `Done`), position and optional work-in-progress limit. Work items point to a status. A new project
  gets three statuses (To Do / `ToDo`, In Progress / `InProgress`, Done / `Done`). The project entity
  enforces the rules in one place:
  - 1–10 columns; names 1–30 characters, unique per project ignoring case (FR-034);
  - at least one `ToDo` and one `Done` column always remain (FR-038);
  - a column's category changes only while it holds no work items, including deleted ones (FR-039);
  - deleting a column requires a destination; all its work items (sub-tasks and soft-deleted items
    included) move there first, each move recorded in history with the note "column deleted"
    (FR-037); the status row is then removed.
  - Every structural change increments `Project.BoardVersion`; column commands carry the version the
    user loaded, and a mismatch returns `Conflict` with the current columns (FR-041).
- **Rationale**: one mechanism serves the Kanban board now and the Scrum and stage-gate templates later
  (they are different default status sets with the same categories); categories make "done", progress
  and future portfolio rollups comparable across templates.
- **Alternatives considered**: a separate board-column entity mapped to statuses (only needed when one
  column shows several statuses, which Phase 1 does not require); fixed statuses (fails FR-034–FR-039).

### R12. One work-item table for tasks and sub-tasks (**New**)

- **Decision**: tasks and sub-tasks are rows of one `WorkItems` table with a `Type` (`Task`,
  `Subtask`) whose hierarchy level is fixed by the type (task = standard level, sub-task = one below).
  A sub-task must have a parent task in the same project and cannot have children (FR-028). Sub-tasks
  use the project's statuses, are excluded from the board query, and their done/total counts appear
  on the parent's card (FR-017). The UI calls every work item a "task".
- **Rationale**: the unified hierarchy agreed for the product: later templates add types (Epic,
  Story, Bug, Phase, Milestone) as new `Type` values with their levels, and portfolios sit above
  projects, all without restructuring this table.
- **Alternatives considered**: a separate checklist table for sub-tasks (sub-tasks could not have
  their own comments, history or status, and would need migrating later).

### R13. Work item keys

- **Decision**: `Project.NextItemNumber` is incremented atomically in the creation transaction
  (`UPDATE … SET NextItemNumber += 1 OUTPUT deleted.NextItemNumber`); the item stores its `Number` and
  immutable `Key` (for example `WEB-42`) under unique indexes (FR-024).
- **Rationale**: concurrent creations serialize on the project row; a rolled-back transaction rolls
  back the counter, so keys are never duplicated or reused.
- **Alternatives considered**: `MAX(Number)+1` (race conditions).

### R14. Card order

- **Decision**: each work item has a `Rank` string from a fractional-indexing algorithm (base-62 keys
  between two neighbours), stored as `varchar(64)` with a binary collation. A move writes one row; a
  background job rebalances a project if any key exceeds 48 characters. Inline creation ranks the new
  card after the last card of its column; new sub-tasks are ranked after their last sibling (FR-018,
  FR-020).
- **Rationale**: moving a card never renumbers a whole column.
- **Alternatives considered**: integer positions (mass updates on every move); decimal midpoints
  (precision runs out).

### R15. Drag and drop without custom JavaScript (**New**)

- **Decision**: cards use native HTML drag-and-drop events handled in Blazor (`draggable`,
  `@ondragstart`, `@ondragover:preventDefault`, `@ondrop`). Every card is a drop target meaning "insert
  before this card", and each column's footer means "append to the end", so no script is needed to
  compute drop positions. Column headers in the column settings use the same pattern for reordering.
  Keyboard users get a "Move to" menu (every column, top or bottom of a column) on each card and
  "Move left" and "Move right" on each column; results are announced through a polite live region
  (FR-019, FR-035, FR-042).
- **Rationale**: fewer moving parts; works identically for mouse and keyboard paths; touch devices use
  the menu.
- **Alternatives considered**: a JavaScript drag-and-drop library (extra dependency, and interop for
  every move).

### R16. Concurrency

- **Decision**: `rowversion` on `WorkItems`, `Comments` and `Projects`, plus `Project.BoardVersion`
  for column structure (R11). Commands carry the version the user loaded; on
  `DbUpdateConcurrencyException` or a version mismatch the service returns `Conflict` with the current
  state, and the UI shows it next to the user's unsaved input (FR-022, FR-032, FR-041).
- **Rationale**: optimistic locking fits low-contention editing and constitution IV.
- **Alternatives considered**: last write wins (forbidden by the constitution).

### R17. History

- **Decision**: work items change only through domain methods (`Rename`, `Describe`, `Prioritize`,
  `MoveTo`, `AddSubtask`, `Delete`, `Restore`, …), each appending `WorkItemChange` rows (who, when,
  field, old value, new value, optional note) in the same transaction; comment actions add
  `CommentAdded`, `CommentEdited` and `CommentDeleted` entries. The table is append-only, enforced by
  an `INSTEAD OF UPDATE, DELETE` trigger. A reorder within a column is recorded as a `Rank` change
  whose old and new values are the card's 1-based positions in the column, with a note such as "moved
  above WEB-3"; a move between columns is recorded as a status change; rank rebalancing (R14) keeps the
  order and records nothing (FR-031, constitution IV).
  Architecture tests assert that `WorkItem` properties have non-public setters.
- **Rationale**: constitution IV and SC-004, testable test-first.
- **Alternatives considered**: an EF interceptor diffing properties (loses intent, noisy).

### R18. Soft delete and restore

- **Decision**: work items and comments are soft-deleted (`IsDeleted`, `DeletedAt`, `DeletedById`)
  with EF global query filters. Deleting a task soft-deletes its sub-tasks in the same change set;
  administrators list a project's deleted tasks and restore them with their sub-tasks (FR-033).
  Comments show a "comment deleted" placeholder (FR-030).
- **Rationale**: constitution IV (no hard deletes through the application).
- **Alternatives considered**: hard delete with an archive table.

### R19. Board query and performance

- **Decision**: one `AsNoTracking` projection loads a project's columns and top-level, non-deleted work
  items ordered by `(StatusId, Rank)`, with sub-task done/total counts from a grouped aggregate; "done"
  columns include only items resolved in the last 14 days unless the user chooses "show all"
  (`?done=all`) (FR-021). Columns render their cards with Blazor `Virtualize` so a long column scrolls
  on its own. A filtered index on `(ProjectId, StatusId, Rank) WHERE IsDeleted = 0 AND ParentId IS NULL`
  serves the board, and `(ProjectId, ResolvedAt)` serves the 14-day window. Every list is paged
  (constitution performance baseline): projects, accounts and deleted tasks 50 per page with a total
  count; sub-tasks, comments and history in the drawer 50 at a time with "Show more".
- **Rationale**: SC-002 (board of up to 500 visible cards within 1 second at 500,000 work items).
- **Alternatives considered**: loading full entities (too much data per circuit); caching (not needed
  at this scale; would need justification under constitution V).

### R20. The details drawer

- **Decision**: the drawer is a dialog panel over the right side of the board, opened by selecting a
  card or through the URL `/projects/{key}/board?task={KEY-N}` (FR-023). It has dialog semantics: focus
  moves into it on open, `Esc` closes it, and focus returns to the card. Each field saves on its own
  with a visible confirmation (FR-026). Sub-tasks open in the same drawer with a link back to the
  parent.
- **Rationale**: matches the prototype and Jira's pattern; shareable links; accessible.
- **Alternatives considered**: a full-page issue view (loses board context).

## Operations and quality

### R21. Time handling

- **Decision**: timestamps are UTC `datetimeoffset` from an injected `TimeProvider`; users have an IANA
  time zone (default: the organization's); display converts at render time (FR-043).
- **Rationale**: correct across time zones; deterministic tests.

### R22. Accessibility

- **Decision**: Fluent UI components; landmarks and skip link; keyboard "Move to", "Move left" and
  "Move right" actions; drawer focus management; automated checks with Deque axe-core through
  `Deque.AxeCore.Playwright` on every Phase 1 screen; a manual screen-reader pass (NVDA with Edge)
  before the pilot (FR-042, SC-008).

### R23. Observability

- **Decision**: `ILogger` with the JSON console formatter; OpenTelemetry traces and metrics for
  ASP.NET Core and EF Core, exported through OTLP when configured; `/health/live` and `/health/ready`
  (database). Every log entry carries the trace ID as its correlation ID. Logs contain IDs, never task
  text, comments or credentials.

### R24. Testing strategy

- **Decision**:
  - `Upms.Domain.Tests` (xUnit v3): key rules, rank ordering, column rules, work item state and
    history. Written first.
  - `Upms.Application.Tests` (xUnit + Testcontainers SQL Server + Respawn): every application service
    against a real database, including the permission rules, concurrency and history.
  - `Upms.Web.Tests` (bUnit): board, card, inline creation, "Move to" menu, drawer, column settings,
    idle warning.
  - `Upms.E2E.Tests` (Playwright + axe): one journey per user story plus accessibility scans.
  - `Upms.Architecture.Tests` (ArchUnitNET): module boundaries and private setters.
  - `Upms.Performance.Tests` (opt-in): 500,000 seeded work items, 300 concurrent simulated users;
    p95 within 1 second for project list and board loads, inline create, card move, drawer open and
    saving an edit (SC-002, constitution performance baseline).
  - Test names begin with the scenario they prove (for example `US1_AS5_InlineCreateAddsCard`).
- **Rationale**: constitution II. Phase 1 needs only the standard SQL Server image (no full-text).

### R25. Deployment and reliability

- **Decision**: one build published as a Linux container image and as a folder for Windows Server/IIS
  (WebSockets enabled). Data protection keys are stored in the database and protected with a
  certificate. SQL Server backups: daily full, 6-hourly differential, 15-minute log backups, giving
  RPO ≤ 1 hour; a restore runbook targets RTO < 4 hours, drilled before the pilot (SC-009, SC-010).

## Forward compatibility

### R26. How Phases 2 and 3 extend Phase 1 without restructuring (**New**)

- **Decision**: every later capability is a new table or a new nullable column with a default, and no
  Phase 1 column changes meaning:

  | Later capability | Additive change |
  |------------------|-----------------|
  | Project members and roles (Phase 2) | new `ProjectMembers` table; data migration makes each project's owner its first Project Admin; `IProjectAccess` switches to membership |
  | Task assignees (Phase 2) | nullable `WorkItems.AssigneeId` |
  | List view (Phase 2) | new read model over `WorkItems`; no schema change |
  | Timeline view and dates (Phase 2) | nullable `WorkItems.StartDate` and `DueDate` |
  | Portfolios (Phase 3) | new `Portfolios` table; nullable `Projects.PortfolioId` |
  | Templates: Scrum, PMO stage-gate (Phase 3) | new `Projects.Template` column defaulting to `Kanban` for existing projects; each template seeds its own statuses with the existing categories |
  | More work item types (Phase 3) | new `Type` values (Epic, Story, Bug, Phase, Milestone) with their hierarchy levels; containers become valid parents |
  | Sprints (Phase 3) | new `Sprints` table; nullable `WorkItems.SprintId` |
  | Dependencies and stage gates (Phase 3) | new `Dependencies` and gate-approval tables |
  | Cross-project dashboards (Phase 3) | read models over status categories, which exist from Phase 1 |

- **Rationale**: the user's direction: start with the core Kanban project while keeping one unified
  Portfolio → Project → work item hierarchy for IT and PMO. Constitution V still holds: none of these
  columns or tables is created before its phase.
- **Alternatives considered**: creating the future columns now (speculative, violates YAGNI);
  a generic "custom fields" store (hard to query and report on).
