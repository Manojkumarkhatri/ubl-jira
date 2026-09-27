# Contract: Permission Matrix (Phase 2)

**Feature**: [spec.md](../spec.md) | Implements FR-002–FR-011, FR-016, FR-025 and Phase 1's account rules
through the single policy point `IProjectAccess` (research R2). This matrix supersedes the Phase 1 matrix
(`specs/002-kanban-project-core/contracts/permissions.md`). Every cell is enforced in the application
services and covered by an automated test (`PermissionMatrixTests`, SC-003).

**Legend**: ✅ allowed · ❌ refused (`Forbidden`; `CommentNotOwned` for someone else's comment) ·
🚫 hidden (`NotFound`, as if the project did not exist) · 🔒 redirected to sign-in.

Columns: **Anonymous**; **Non-member** = an active, signed-in user who is not a member of the project and
not an administrator; **Viewer**, **Member**, **Project Admin** = members with that project role;
**Creator** = a Member who created the work item (or comment); **Admin** = organization Administrator
(full rights in every project, whether or not a member).

| Action | Anonymous | Non-member | Viewer | Member | Creator | Project Admin | Admin |
|--------|:---------:|:----------:|:------:|:------:|:-------:|:-------------:|:-----:|
| See the project, its board, list, timeline, task drawers and member list | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create a project (becoming its first Project Admin) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Edit project name and description | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add, rename, reorder, limit, retype or delete board columns | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add members, change their roles and remove them | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ | ✅ |
| Create tasks (on the board or the list) and sub-tasks | 🔒 | 🚫 | ❌ | ✅ | ✅ | ✅ | ✅ |
| Edit title, description, priority, status; move and reorder cards | 🔒 | 🚫 | ❌ | ✅ | ✅ | ✅ | ✅ |
| Assign tasks and set their dates (drawer or timeline) | 🔒 | 🚫 | ❌ | ✅ | ✅ | ✅ | ✅ |
| Add comments | 🔒 | 🚫 | ❌ | ✅ | ✅ | ✅ | ✅ |
| Edit or delete a comment | 🔒 | 🚫 | ❌ | ❌ | ✅ (own) | ❌ | ❌ |
| Delete a task (with its sub-tasks) | 🔒 | 🚫 | ❌ | ❌ | ✅ (own) | ✅ | ✅ |
| List and restore deleted tasks | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ |
| See "My tasks" (own open assigned tasks in projects they can see) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage accounts (list, add, reset password, deactivate, reactivate) | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Give or remove the Administrator role | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Change own password, display name and time zone | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

## Rules

1. **Deactivated users** cannot sign in; open sessions end within one minute (Phase 1 research R6).
2. **The last active Administrator** can neither be deactivated nor lose the Administrator role (Phase 1
   FR-008).
3. **Unknown or deleted** projects, tasks and comments return `NotFound`; so does every project-scoped
   request from a non-member, so a non-member cannot tell whether a project exists (FR-002).
4. **The last active Project Admin** of a project can neither be removed nor given another role
   (`LastProjectAdmin`); membership changes carry the team's version, so changes made at the same moment
   cannot leave a project without an active Project Admin (FR-010, FR-013).
5. **Assignees** must be active Project Admins or Members when assigned (`NotAssignable`, FR-016); a
   person who later leaves, becomes a Viewer or is deactivated stays on their tasks until reassigned
   (FR-024).
6. **Role changes and removals** apply to the person's next request, including on screens already open;
   no sign-in is needed to gain new rights (FR-011).
7. **"My tasks"** lists only tasks in projects the caller can still see (FR-025).
