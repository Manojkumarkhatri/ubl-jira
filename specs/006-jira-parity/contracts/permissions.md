# Contract: Permission Matrix (Jira Parity Pass)

**Feature**: [spec.md](../spec.md) | Implements FR-003, FR-010–FR-012, FR-017, FR-018, FR-021–FR-022a, FR-025,
FR-027 and FR-029 on top of the Phase 4 rules, through the single policy point `IProjectAccess` (research R19).
This matrix supersedes the Phase 4 matrix (`specs/005-collaboration-and-search/contracts/permissions.md`). Every
cell is enforced in the application services or endpoints and covered by an automated test
(`PermissionMatrixTests`).

**Legend**: ✅ allowed · ❌ refused (`Forbidden`; `CommentNotOwned` for someone else's comment) ·
🚫 hidden (`NotFound`, as if the space did not exist) · 🔒 redirected to sign-in (or 401 for the attachment
endpoints).

Columns: **Anonymous**; **Non-member** = an active, signed-in user who is not a member of the space and not an
administrator; **Viewer**, **Member**, **Space admin** = members with that space role; **Approver** = a Viewer
named as an approver of the gate in question (and not its excluded requester); **Creator** = a Member who
created the work item, the comment, the attachment (its uploader) or the saved filter; **Admin** =
organization Administrator (full rights in every space, whether or not a member). Only the Approver is an
approver of the gate in the decision row; everyone else in that row is not.

| Action | Anonymous | Non-member | Viewer | Approver | Member | Creator | Space admin | Admin |
|--------|:---------:|:----------:|:------:|:--------:|:------:|:-------:|:-----------:|:-----:|
| See the space, its summary, board, list, timeline, backlog, sprint reports, stages, drawers and member list | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Create a space from a template or the Blank template (becoming its first Space admin) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Edit space name and description | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Switch a space between Kanban and Scrum | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Turn a space's work types on and off | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add, rename, reorder, limit, retype or delete board columns (on the board or in space settings) | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add members, change their roles and remove them | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Create work items (cards, epics, milestones) and sub-tasks, inline or from the Create form | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Get the Create form's spaces, options and parents | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Edit title, description, priority, status, type, estimate, epic or stage, and an epic's color; move and reorder cards | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Move a card into another epic's, stage's or person's lane on the board | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Assign work items and set their dates (drawer or timeline) | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Add and remove dependencies | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Reorder the backlog and move cards between the backlog and sprints | 🔒 | 🚫 | ❌ | ❌ | ✅ | ✅ | ✅ | ✅ |
| Create, rename, start, complete and delete sprints | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Add, reorder and delete stages (from the stages page or the Create form) | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
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
| Share a saved filter with the space | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Run a saved filter shared with the space | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Star and unstar the space, and have it recorded as opened | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Delete a work item other than a stage (with its sub-tasks) | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ (own) | ✅ | ✅ |
| List archived work items | 🔒 | 🚫 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Restore archived work items | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| See "For you" (worked on, viewed, assigned to me with gates waiting, starred) and the sidebar | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Search, open work items by key, and see recently viewed items (only in spaces one can see) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| See, open and mark one's own notifications | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Save, run, rename, update and delete one's own filters | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Choose one's own email choice and sidebar layout | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage accounts (list, add, reset password, deactivate, reactivate) | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Give or remove the Administrator role | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Change own password, display name and time zone | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

## Rules

1. **Phase 4 rules 1–8** (`specs/005-collaboration-and-search/contracts/permissions.md`) still hold, with
   "space" for "project".
2. **Spaces stay members-only** (FR-018): no space is visible to non-members other than administrators, whatever
   its template.
3. **The Create form** offers only spaces where the caller can contribute, and only the types the space has
   turned on. "Stage" is offered only with the Manage right. The service checks again when creating, so a right
   or type lost meanwhile is refused (spec Edge Cases).
4. **Lane moves** check every right and rule the drawer would for the same change (assignee, epic or stage, status,
   stage lock), in one change. A sub-task never changes epic or stage this way (`SubtaskFollowsParent`).
5. **The sidebar, "For you" and the summary** show only spaces and items the caller can see now. Stars and visits
   of a space the caller can no longer see are kept but never shown.
6. **Archived work items** (2026-10-20, as in Jira): anyone who sees the space reads its archived work items; only
   administrators restore them.
