# Contract: Permission Matrix (Phase 4)

> **Superseded by the Jira parity pass**: the matrix in `specs/006-jira-parity/contracts/permissions.md`
> applies; it says "space" for "project" and adds the Create form, columns on the board, work types, epic colors,
> lane moves, stars, the summary and "For you".

**Feature**: [spec.md](../spec.md) | Implements FR-008, FR-012, FR-013, FR-022, FR-023, FR-027–FR-029, FR-037,
FR-039–FR-044, FR-051 and FR-054 on top of the Phase 3 rules, through the single policy point `IProjectAccess`
(research R21). This matrix supersedes the Phase 3 matrix (`specs/004-templates-and-planning/contracts/
permissions.md`). Every cell is enforced in the application services or endpoints and covered by an automated
test (`PermissionMatrixTests`, SC-004).

**Legend**: ✅ allowed · ❌ refused (`Forbidden`; `CommentNotOwned` for someone else's comment) ·
🚫 hidden (`NotFound`, as if the project did not exist) · 🔒 redirected to sign-in (or 401 for the attachment
endpoints).

Columns: **Anonymous**; **Non-member** = an active, signed-in user who is not a member of the project and not an
administrator; **Viewer**, **Member**, **Project Admin** = members with that project role; **Approver** = a Viewer
named as an approver of the gate in question (and not its excluded requester); **Creator** = a Member who
created the work item, the comment, the attachment (its uploader) or the saved filter; **Admin** =
organization Administrator (full rights in every project, whether or not a member). Only the Approver is an
approver of the gate in the decision row; everyone else in that row is not.

| Action | Anonymous | Non-member | Viewer | Approver | Member | Creator | Project Admin | Admin |
|--------|:---------:|:----------:|:------:|:--------:|:------:|:-------:|:-------------:|:-----:|
| See the project, its board, list, timeline, backlog, sprint reports, stages, drawers and member list | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create a project with a template (becoming its first Project Admin) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Edit project name and description | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Switch a project between Kanban and Scrum | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add, rename, reorder, limit, retype or delete board columns | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add members, change their roles and remove them | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Create work items (cards, epics, milestones) and sub-tasks | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Edit title, description, priority, status, type, estimate, epic or stage; move and reorder cards | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Assign work items and set their dates (drawer or timeline) | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Add and remove dependencies | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Reorder the backlog and move cards between the backlog and sprints | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Create, rename, start, complete and delete sprints | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add, reorder and delete stages | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Name gate approvers and set the approvals needed and the requester exclusion | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Request approval of the current stage's gate and withdraw the request | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Approve a gate or send it back | 🔒 | 🚫 | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Add comments | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Edit or delete a comment | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ (own) | ❌ | ❌ |
| Find people to mention | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Watch and stop watching an item, and see its watchers | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Add and remove other people as watchers | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Attach files to a work item | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Download and preview attachments that passed the check | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Remove an attachment | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ (own) | ✅ | ✅ |
| Delete an attachment permanently | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Add and remove labels, and get label suggestions | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Share a saved filter with the project | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Run a saved filter shared with the project | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Delete a work item other than a stage (with its sub-tasks) | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ (own) | ✅ | ✅ |
| List and restore deleted work items | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| See "My tasks" (own open assigned work items, and gates waiting for them) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Search, open work items by key, and see recently viewed items (only in projects one can see) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| See, open and mark one's own notifications | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Save, run, rename, update and delete one's own filters | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Choose one's own email choice | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage accounts (list, add, reset password, deactivate, reactivate) | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Give or remove the Administrator role | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Change own password, display name and time zone | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

## Rules

1. **Phase 3 rules 1–7** (`specs/004-templates-and-planning/contracts/permissions.md`) still hold: deactivated
   users, the last active Administrator and Project Admin, unknown or deleted items, stages, gate decisions,
   approvers added as Viewers, the stage lock, completed sprints, and rule codes versus `Forbidden`.
2. **Removing an attachment** as its uploader needs the Contribute right at that moment: an uploader who became a
   Viewer is refused (`Forbidden`). Project Admins and administrators may remove any attachment.
3. **Downloads and previews** are checked on every request: the file passed its check, was neither removed nor
   purged, its item is not deleted, and the caller can see the item now. Any refusal returns 404, with no
   difference between a missing file and a hidden one (FR-027).
4. **Mentions** reach only active members of the item's project. Mention ids for anyone else are turned into plain
   text when saving, and notify no one (FR-008).
5. **Watchers added by a Project Admin** must be active members of the project (`NotAMember`).
6. **Notifications** are read, opened and marked only by their recipient. The list and the unread count leave
   out notifications about projects the recipient can no longer access. Notifications about deleted items stay
   listed; opening one says the item was not found (research R8).
7. **Saved filters** are changed only by their owner, whatever their role, administrators included. A filter can
   be shared only with projects the owner is a member of, or any project for an administrator (`NotAMember`, a
   `NotFound` refusal, so the answer is the same whether or not the project exists).
   Sharing counts only while the owner is active and still a member (or still an administrator). "Shared with me"
   lists filters shared with the projects one is a member of; anyone who can see the project, administrators
   included, can run the filter. Running any filter shows only items the runner can see (FR-040, FR-041).
8. **Suggestions and search** (mentions, labels, recent items, key jump, search results and counts) include only
   what the caller can see. A key the caller cannot see gets the same answer as a key that does not exist
   (FR-032, FR-037, FR-044, SC-004).
