# Contract: UI Routes and Screens

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

Routes are stable URLs users can bookmark and share. Filters are kept in the query string so a filtered
list or search can be shared or reloaded. Every screen meets WCAG 2.2 AA and works with a keyboard
alone (FR-050). All routes except sign-in and setup require a signed-in user.

| Route | Screen | Who | Story / requirements |
|-------|--------|-----|----------------------|
| `/setup` | First-run administrator setup | anonymous, until setup is complete | FR-002 |
| `/Account/Login`, `/Account/ChangePassword` | Sign in; forced password change | anyone | US1 scenario 1, FR-001, FR-003, FR-004 |
| `/account/profile` | Display name, time zone, notification emails, change password | self | FR-006, FR-057 |
| `/` | **My Work**: open assigned issues by project, 10 recently viewed. Until US5 ships, redirects to `/projects` | signed in | US5, FR-049 |
| `/projects` | Project list (accessible projects; "show archived" toggle) | signed in | FR-015, FR-016 |
| `/projects/new` | Create project (name, key, description, board style) | Admin | US1 scenario 2, FR-012 |
| `/projects/{key}` | Redirects to the board | viewer+ | |
| `/projects/{key}/issues` | Issue list with sort, filters, 50 per page, "Create issue" | viewer+ (create: contributor) | US1 scenarios 3, 7, FR-026 |
| `/projects/{key}/board` | Board: columns, cards, quick filters, card side panel, "Move to" menu | viewer+ (move: contributor) | US2, FR-028–FR-032 |
| `/projects/{key}/backlog` | Backlog and sprint planning (Scrum projects only) | viewer+ (edit: contributor) | US4, FR-038–FR-043 |
| `/projects/{key}/sprints/{sprintId}` | Sprint report (completed sprints) | viewer+ | US4 scenario 6, FR-044 |
| `/projects/{key}/settings` | Project details and members with roles | PA, Admin | US6 scenario 3, FR-014 |
| `/browse/{issueKey}` | **Issue page**: fields (inline edit), description, sub-tasks, attachments, comments, history tab, watch toggle | viewer+ (edit: contributor) | US1, US3, FR-017–FR-025, FR-033–FR-037, FR-053–FR-056 |
| `/search?q=…&project=…&status=…` | Search with combinable filters; save as filter | signed in | US5 scenarios 2–5, FR-046–FR-048 |
| `/filters` | My saved filters (run, rename, delete) | self | FR-048 |
| `/notifications` | Notification list; mark read | self | US3 scenarios 2–3, FR-036 |
| `/admin/users`, `/admin/users/{id}` | Users: create, deactivate, reactivate, reset password, role | Admin | US6 scenarios 1, 2, 7, FR-003, FR-007 |
| `/admin/audit` | Audit log with date, user, and event-type filters | Admin | US6 scenarios 5, 6, FR-011 |
| `/admin/deleted-issues` | Deleted issues; restore | Admin | FR-025 |
| `/admin/settings` | Organization time zone and idle timeout | Admin | FR-005, FR-051 |

## Global layout

- Header: product name, project switcher, **search box** (issue key → opens the issue; other text →
  `/search`), "Create issue" button, notification bell with unread count, user menu.
- Skip link to main content; landmark regions; visible focus; announcements for async results.
- Idle-session warning dialog 2 minutes before the timeout, with "Stay signed in" (FR-005).
- Conflict banner pattern: "This issue was changed by *name* at *time*. Review the latest values;
  your unsaved text is kept below." (FR-024).
- Empty states with a next action on every list (edge case "Empty states").
