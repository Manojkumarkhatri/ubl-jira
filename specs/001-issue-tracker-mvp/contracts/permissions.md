# Contract: Permission Matrix

**Feature**: [spec.md](../spec.md) | Implements FR-008, FR-009, FR-010, FR-015, FR-025, FR-056 and
constitution principle III. Every cell is enforced in the application service (research R7) and
covered by an automated test (SC-005).

**Legend**: ✅ allowed · ❌ `Forbidden` (the user can see the resource but lacks the role) · 🚫
`NotFound` (the user cannot see the resource, so its existence is not revealed) · "own" = only
their own item.

Columns: **Admin** = organization Administrator (full rights in every project); **PA** = Project
Admin; **Mbr** = Member; **Viewer**; **Other** = signed-in user who is not a member.

| Action | Admin | PA | Mbr | Viewer | Other |
|--------|:-----:|:--:|:---:|:------:|:-----:|
| See project in list, open project, issues, board, backlog, sprint reports | ✅ | ✅ | ✅ | ✅ | 🚫 |
| Read issue details, comments, history; download clean attachments | ✅ | ✅ | ✅ | ✅ | 🚫 |
| Watch / unwatch an issue | ✅ | ✅ | ✅ | ✅ | 🚫 |
| Create project | ✅ | ❌ | ❌ | ❌ | ❌ |
| Edit project name, description, board style | ✅ | ✅ | ❌ | ❌ | 🚫 |
| Add / remove members, change project roles | ✅ | ✅ | ❌ | ❌ | 🚫 |
| Archive / restore project | ✅ | ❌ | ❌ | ❌ | 🚫 |
| Create issue; edit fields; change status; assign; rank; move card | ✅ | ✅ | ✅ | ❌ | 🚫 |
| Delete issue (soft) | ✅ | ✅ | ❌ | ❌ | 🚫 |
| List and restore deleted issues | ✅ | ❌ | ❌ | ❌ | ❌ |
| Add comment | ✅ | ✅ | ✅ | ❌ | 🚫 |
| Edit / delete a comment | own | own | own | ❌ | 🚫 |
| Upload attachment | ✅ | ✅ | ✅ | ❌ | 🚫 |
| Remove attachment | ✅ | ✅ | own | ❌ | 🚫 |
| Permanently delete (purge) attachment | ✅ | ❌ | ❌ | ❌ | ❌ |
| Create / start / complete sprints; move issues between backlog and sprints | ✅ | ✅ | ✅ | ❌ | 🚫 |
| Search (results limited to accessible projects) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Saved filters, notifications, My Work, own profile and password | own | own | own | own | own |
| Manage users (create, deactivate, reactivate, reset password, organization role) | ✅ | ❌ | ❌ | ❌ | ❌ |
| View audit log; change organization settings | ✅ | ❌ | ❌ | ❌ | ❌ |

## Additional rules

1. **Archived projects** are read-only for everyone: all write actions return the rule violation
   `ProjectArchived`, including for Administrators, until the project is restored.
2. **Assignees** must be active users with the PA or Mbr role in the project at the moment of
   assignment (`AssigneeNotContributor`).
3. **Comment authors** who lose contributor access can no longer edit their comments.
4. **Last Administrator**: deactivating or demoting the last active Administrator returns
   `LastAdministrator` (FR-007).
5. **Deactivated users** cannot sign in; open sessions end within one minute (research R6).
6. **Mentions and notifications** only target users who can see the project (US3 scenario 7).
7. Anonymous requests reach only the sign-in, first-run setup, and health endpoints
   ([http-endpoints.md](./http-endpoints.md)).
