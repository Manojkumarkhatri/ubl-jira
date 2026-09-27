# Contract: Permission Matrix (Phase 1)

> **Superseded in Phase 2**: once Phase 2 is installed, projects are members-only and the rules are those
> of [specs/003-project-views-and-team/contracts/permissions.md](../../003-project-views-and-team/contracts/permissions.md).
> This matrix records the Phase 1 rules.

**Feature**: [spec.md](../spec.md) | Implements FR-001, FR-004, FR-008, FR-009, FR-014, FR-015, FR-033,
FR-034–FR-039 through the single policy point `IProjectAccess` (research R7). Every cell is enforced in
the application service and covered by an automated test (SC-007).

**Legend**: ✅ allowed · ❌ refused (`Forbidden`) · 🔒 redirected to sign-in.

Columns: **Anonymous**; **User** = any active, signed-in user; **Creator** = the user who created the
work item (or comment); **Owner** = the project's owner (its creator); **Admin** = organization
Administrator (full rights in every project).

| Action | Anonymous | User | Creator | Owner | Admin |
|--------|:---------:|:----:|:-------:|:-----:|:-----:|
| See the project list, any board, any task drawer | 🔒 | ✅ | ✅ | ✅ | ✅ |
| Create a project (becoming its owner) | 🔒 | ✅ | ✅ | ✅ | ✅ |
| Edit project name and description | 🔒 | ❌ | ❌ | ✅ | ✅ |
| Add, rename, reorder, limit, retype or delete board columns | 🔒 | ❌ | ❌ | ✅ | ✅ |
| Create tasks (inline) and sub-tasks | 🔒 | ✅ | ✅ | ✅ | ✅ |
| Edit title, description, priority, status; move and reorder cards | 🔒 | ✅ | ✅ | ✅ | ✅ |
| Add comments | 🔒 | ✅ | ✅ | ✅ | ✅ |
| Edit or delete a comment | 🔒 | ❌ | ✅ (own) | ❌ | ❌ |
| Delete a task (with its sub-tasks) | 🔒 | ❌ | ✅ (own) | ✅ | ✅ |
| List and restore deleted tasks | 🔒 | ❌ | ❌ | ❌ | ✅ |
| Manage accounts (list, add, reset password, deactivate, reactivate) | 🔒 | ❌ | ❌ | ❌ | ✅ |
| Give or remove the Administrator role | 🔒 | ❌ | ❌ | ❌ | ✅ |
| Change own password, display name and time zone | 🔒 | ✅ | ✅ | ✅ | ✅ |

## Rules

1. **Deactivated users** cannot sign in; open sessions end within one minute (research R6).
2. **The last active Administrator** can neither be deactivated nor lose the Administrator role, and only an
   active account can be given it. Changes to who is an active administrator are made one at a time, so two
   administrators acting on each other at the same moment cannot leave none (FR-008).
3. **Unknown or deleted** projects, tasks and comments return `NotFound` (FR-009); deleted tasks are
   visible only in the Admin's deleted-task list.
4. **Phase 2 change**: the *User* and *Owner* columns are replaced by project roles (Project Admin,
   Member, Viewer) when membership arrives; only the `IProjectAccess` implementation changes.
