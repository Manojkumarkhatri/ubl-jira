# Contract: UI Routes and Screens (Phase 1)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

All routes except sign-in and setup require a signed-in user. Every screen meets WCAG 2.2 AA and works
with a keyboard alone (FR-042).

| Route | Screen | Who | Requirements |
|-------|--------|-----|--------------|
| `/setup` | First-run administrator setup | anonymous, until setup is complete | FR-002 |
| `/Account/Login`, `/Account/ChangePassword` | Sign in; forced and voluntary password change | anyone | FR-001, FR-003, FR-005 |
| `/account/profile` | Display name, time zone, change password | self | FR-007, FR-043 |
| `/` | Redirects to `/projects` | signed in | |
| `/projects` | Project list, 50 per page, with "Create project" (dialog: name, key suggested from the name, description) | signed in | FR-011, FR-013 |
| `/projects/{key}/board` | Kanban board: columns, cards, card counts, over-limit markers, "What needs to be done?" per column, "Move to" menu, "Show all completed" toggle (`?done=all`) | signed in | FR-016–FR-022 |
| `/projects/{key}/board?task={KEY-N}` | The board with that task's details drawer open (title, status, priority, description, sub-tasks, comments, history, delete); sub-tasks, comments and history load 50 at a time with "Show more" | signed in | FR-023–FR-033 |
| `/projects/{key}/settings` | Project details (name, description) and board columns (add, rename, drag or move left/right, type, work-in-progress limit, delete with destination) | owner, Admin | FR-014, FR-034–FR-041 |
| `/projects/{key}/deleted` | Deleted tasks, 50 per page, with Restore | Admin | FR-033 |
| `/admin/users` | Accounts: list and search (50 per page), add (temporary password shown once), reset password, deactivate, reactivate | Admin | FR-003, FR-004 |

## Global layout

- Header: product name, "Projects" link, user menu (profile, sign out); skip link; landmarks; a polite
  live region announcing moves and saves.
- Idle-session warning 2 minutes before the timeout, with "Stay signed in" (FR-006).
- Conflict banner: "This task was changed by *name* at *time*. Review the latest values; your unsaved
  text is kept below." (FR-022, FR-032).
- Empty states with a next action: no projects ("Create project"), empty column (the "What needs to be
  done?" box), no sub-tasks, no comments.
