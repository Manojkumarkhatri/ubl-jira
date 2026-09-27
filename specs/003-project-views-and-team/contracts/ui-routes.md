# Contract: UI Routes and Screens (Phase 2)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

Phase 1 routes (`specs/002-kanban-project-core/contracts/ui-routes.md`) stay; this contract lists the new
and changed ones. Every screen meets WCAG 2.2 AA, works with a keyboard alone and stays usable at 360 px
(FR-041). A non-member opening any project route sees "Not found" (FR-002).

| Route | Screen | Who | Requirements |
|-------|--------|-----|--------------|
| `/projects` | **Changed**: the projects the user can see, with a "Your role" column (Project Admin, Member, Viewer, or "Administrator access") instead of the owner | signed in | FR-015 |
| `/projects/{key}/board` | **Changed**: cards show the assignee's initials (name as text) and the due date with an "Overdue" mark; filters "Only my tasks" and "Assignee" (people on the board, "Unassigned"); columns show "3 of 7" while filtered; Viewers get no "What needs to be done?" boxes, no dragging and no "Move to" | members, Admin | FR-003, FR-021, FR-022 |
| `/projects/{key}/list` | **New**: the List view (key, title, parent key, status, priority, assignee, start, due, updated); sortable headers; filters (status, status type, priority, assignee incl. "Me" and "Unassigned", due: "Overdue", "Due in the next 7 days", "No due date"; words); active filters as removable chips and "Clear filters"; 50 per page with the total; "What needs to be done?" for contributors. Query string: `sort`, `dir`, `column`, `type`, `priority`, `assignee` (`me`, `none` or IDs), `due` (`overdue`, `week`, `none`), `q`, `page`; lists are comma-separated, default values are left out, and values that mean nothing are ignored | members, Admin | FR-027–FR-033 |
| `/projects/{key}/timeline` | **New**: bars on a weeks / months / quarters scale (`?scale=`), today line and "Today" button, "Hide completed" (`?completed=hide`; months is the default scale), expandable rows with sub-tasks, "Unscheduled" list (50 at a time) with "Schedule"; drag to move or resize; keyboard rescheduling (below) | members, Admin | FR-034–FR-040 |
| `/projects/{key}/members` | **New**: the team (name, user name, role, "deactivated" mark, "you"); for Project Admins and Admins: "Add member" (search by name, user name or email; choose a role), a role choice per member, "Remove" with confirmation; conflict notice with the current team | members, Admin (changes: Project Admin, Admin) | FR-008–FR-013 |
| `/my-tasks` | **New**: "My tasks": open tasks assigned to the user, grouped by project, soonest due first, overdue marked, 50 per page (the page number is kept by the screen, not the address); rows open the drawer | signed in | FR-025, FR-026 |
| `…?task={KEY-N}` | The details drawer opens over the board, list, timeline or "My tasks". **Changed**: assignee choice with "Assign to me" and "Unassigned", start and due dates, overdue mark, assignee and due date in the sub-task list; read-only for Viewers (no edits, comment box, sub-task box or delete) | members, Admin | FR-003, FR-017, FR-019, FR-021 |
| `/projects/{key}/settings` | **Changed**: Project Admins instead of the owner | Project Admin, Admin | FR-005 |

## Project header (board, list, timeline, members)

Breadcrumb (Projects / KEY), project name, view tabs **Board · List · Timeline** (links with
`aria-current="page"` on the current view), then "Members", "Project settings" (Project Admins and Admins)
and "Deleted tasks" (Admins). Opening a project from the list still shows its board (FR-027).

## Global layout

The header gains a **My tasks** link next to **Projects**.

## Timeline keyboard (FR-038)

Each bar is one tab stop, named for the task, for example "WEB-12 Design the home page, 3 Oct to 9 Oct,
In progress, assigned to Amina Khan".

| Key | Effect |
|-----|--------|
| Left / Right | move the bar one day earlier or later (both dates) |
| Shift + Left / Right | change only the due date by one day |
| Ctrl + Left / Right | change only the start date by one day |
| Enter | save the pending change as one edit; with nothing pending, open the task's drawer |
| Escape | cancel the pending change |

While a change is pending the bar shows its new position and the live region announces the dates, for
example "Starts 5 Oct, due 11 Oct. Enter to save, Escape to cancel." The due date never moves before the
start date. The drawer's date fields remain a second way to reschedule.

## Empty states

No members but the Project Admin ("Add member"); nothing assigned ("Tasks assigned to you in any project
appear here"); no matching tasks ("Clear filters"); no scheduled tasks ("Schedule" on the unscheduled
tasks); no unscheduled tasks (nothing shown).
