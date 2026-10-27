# Contract: UI Routes and Screens (Phase 3)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

Phase 1 and Phase 2 routes (`specs/002-kanban-project-core/contracts/ui-routes.md`,
`specs/003-project-views-and-team/contracts/ui-routes.md`) stay; this contract lists the new and changed
ones. Every screen meets WCAG 2.2 AA, works with a keyboard alone and stays usable at 360 px (FR-056). A
non-member opening any project route sees "Not found". A route that does not fit the project's template
(for example the backlog of a Kanban project) also shows "Not found".

| Route | Screen | Who | Requirements |
|-------|--------|-----|--------------|
| `/projects` | **Changed**: a "Template" column (Kanban, Scrum, Stage-gate); the create-project dialog asks for the template (radio group, Kanban preselected, one line on each) | signed in | FR-001, FR-006 |
| `/projects/{key}/board` | **Changed**: cards show a type symbol (name as text) and the estimate; "What needs to be done?" has a type choice (Story, Task, Bug; Task only in Stage-gate projects). **Scrum**: only the active sprint's cards, with the sprint's name, goal and dates above the columns and "Complete sprint" for Project Admins; with no active sprint, an empty state linking to the backlog | members, Admin | FR-009, FR-010, FR-013, FR-027 |
| `/projects/{key}/backlog` | **New (Scrum)**: the active sprint, the planned sprints, then the backlog (100 at a time, "Show more"); rows show key, type, title, epic, assignee, estimate and status; sprint headers show items, estimate total, unestimated items, and completed and remaining totals for the active sprint; drag rows to reorder or move; each row's **Move** menu: Top, Bottom, Up, Down, to a sprint, to the backlog; "What needs to be done?" at the end of the backlog; Project Admins: "Create sprint", and per sprint "Edit", "Start", "Complete" and "Delete" (planned); links to past sprint reports | members, Admin | FR-021–FR-026, FR-028 |
| `/projects/{key}/sprints/{number}` | **New (Scrum)**: the sprint report of a completed sprint: name, goal, dates, and tables of items committed, added, removed, completed and not completed, each with its estimate total | members, Admin | FR-029 |
| `/projects/{key}/stages` | **New (Stage-gate)**: stages in order, each with dates, progress, its place ("Approved", "Current", "Locked"), and its gate: state, approvers with their decisions, "N of M approvals needed", the requester exclusion. Project Admins: "Add stage", "Move up" and "Move down" (locked stages), "Set up gate" (search people across the organization, number needed, "The person who requests approval can't approve"). Contributors: "Request approval" (current stage; confirms open items) and "Withdraw request". Approvers: "Approve" and "Send back" with a note (required to send back). `?stage={KEY-N}` focuses a stage | members, Admin (changes as in permissions.md) | FR-031–FR-040 |
| `/projects/{key}/list` | **Changed**: type and "Epic or stage" columns; filters by type and by epic or stage (including "None"); query string adds `kind` (comma-separated types; `type` keeps its Phase 2 meaning, the status type) and `container` (keys, or `none`); "What needs to be done?" has a type choice (every type the caller may create) | members, Admin | FR-020 |
| `/projects/{key}/timeline` | **Changed**: groups per epic or stage (bar with progress, expandable), milestones as diamond markers, gate markers on stage due dates labelled with their state, dependency arrows with late marks, a sprint lane in Scrum projects; each row's label has an **Actions** menu (Open, Schedule, Add dependency) | members, Admin | FR-042–FR-047, FR-050, FR-051 |
| `/projects/{key}/settings` | **Changed**: Project Admins of Kanban and Scrum projects can switch between the two, with a confirmation that explains the effect; refused switches explain why | Project Admin, Admin | FR-005 |
| `/my-tasks` | **Changed**: every type (with its symbol); a first section **Gates waiting for you**: project, stage, requested by and when, each linking to `/projects/{key}/stages?stage={KEY-N}` | signed in | FR-041 |
| `…?task={KEY-N}` | **Changed drawer**: type (a choice between Story, Task and Bug where allowed), "Epic" or "Stage" choice, estimate (agile cards), sprint (Scrum, read-only), a locked-stage notice naming the gate; for containers, their items with progress and "Add item"; **Dependencies**: "Waits for" and "Blocks" lists with late marks, "Add dependency" (find by key or title) and "Remove"; for stages, the gate state with a link to the Stages view | members, Admin | FR-011, FR-014–FR-016, FR-030, FR-049 |

## Project header

The template's name is shown next to the project name (FR-006). View tabs depend on the template, each a
link with `aria-current="page"` on the current view:

| Template | Tabs |
|----------|------|
| Kanban | Board · List · Timeline |
| Scrum | Backlog · Board · List · Timeline |
| Stage-gate | Board · List · Timeline · Stages |

Opening a project still shows its board. "Members", "Project settings" and "Deleted tasks" stay as in
Phase 2.

## Keyboard

- **Backlog**: each row is one tab stop with a **Move** menu button; the menu's items say where the card
  goes ("Move to MOB Sprint 2"). After a move, focus stays on the moved row, and the live region
  announces its new place ("MOB-7 moved to MOB Sprint 2, position 3 of 6").
- **Timeline**: bars keep the Phase 2 keys. A milestone marker moves one day per arrow key; Shift and Ctrl
  do nothing, because a milestone has one date. A container bar moves and resizes like a task bar.
- **Stages view**: "Move up" and "Move down" are buttons on each locked stage; focus follows the stage.
- **Dialogs** (start sprint, complete sprint, set up gate, send back, add dependency): the Phase 1 modal
  dialog, with focus returned to the button that opened it.

## Empty states

No sprints ("Create sprint" for Project Admins); an empty backlog ("What needs to be done?"); no active
sprint on a Scrum board ("Plan a sprint in the backlog"); no approvers on a gate ("Set up gate"); no gates
waiting ("Gates that need your decision appear here", shown only when the user is an approver somewhere);
no dependencies ("Add dependency"); no scheduled epics or stages ("Schedule").
