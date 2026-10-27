# Contract: Permission Matrix (Phase 3)

> **Superseded by Phase 4**: once Phase 4 is installed, the matrix in
> `specs/005-collaboration-and-search/contracts/permissions.md` applies; it adds comments with mentions,
> watchers, notifications, attachments, labels, search and saved filters, and the email choice.

**Feature**: [spec.md](../spec.md) | Implements FR-005, FR-010, FR-019, FR-023–FR-028, FR-035–FR-041,
FR-047 and FR-055 on top of the Phase 2 rules, through the single policy point `IProjectAccess` plus the
gate's approver list (research R14, R16). This matrix supersedes the Phase 2 matrix
(`specs/003-project-views-and-team/contracts/permissions.md`). Every cell is enforced in the application
services and covered by an automated test (`PermissionMatrixTests`, SC-005).

**Legend**: ✅ allowed · ❌ refused (`Forbidden`; `CommentNotOwned` for someone else's comment) ·
🚫 hidden (`NotFound`, as if the project did not exist) · 🔒 redirected to sign-in.

Columns: **Anonymous**; **Non-member** = an active, signed-in user who is not a member of the project and
not an administrator; **Viewer**, **Member**, **Project Admin** = members with that project role;
**Approver** = a Viewer named as an approver of the gate in question (and not its excluded requester);
**Creator** = a Member who created the work item (or comment); **Admin** = organization Administrator
(full rights in every project, whether or not a member). Only the Approver is an approver of the gate in
the decision row; everyone else in that row is not.

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
| Delete a work item other than a stage (with its sub-tasks) | 🔒 | 🚫 | ❌ | ❌ | ❌ | ✅ (own) | ✅ | ✅ |
| List and restore deleted work items | 🔒 | 🚫 | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| See "My tasks" (own open assigned work items, and gates waiting for them) | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Manage accounts (list, add, reset password, deactivate, reactivate) | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Give or remove the Administrator role | 🔒 | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Change own password, display name and time zone | 🔒 | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

## Rules

1. **Deactivated users**, **the last active Administrator**, **unknown or deleted items**, **the last active
   Project Admin**, **assignees**, **role changes** and **"My tasks"** follow Phase 2 rules 1–7
   (`specs/003-project-views-and-team/contracts/permissions.md`).
2. **Stages** are added, reordered and deleted only by Project Admins and administrators; the "Delete a work
   item" row does not apply to them. A stage whose gate is approved can never be deleted (`GateApproved`).
3. **Gate decisions** come only from the gate's named approvers, whatever their project role, and never
   from a requester the gate excludes; administrators and Project Admins who are not named are refused
   like anyone else (FR-037). Being an approver grants nothing else: an approver who is a Viewer can change
   nothing but their own decision.
4. **Naming an approver** who is not a member of the project adds them as a Viewer, recorded in the audit
   log as `MemberAdded` (research R16).
5. **The stage lock** applies to everyone, administrators included: no item of a locked stage leaves a "to
   do" column (`StageLocked`), and no stage enters a "done" column before its gate is approved
   (`GateNotApproved`) (FR-032–FR-034).
6. **Completed sprints** refuse every change, whoever asks (`SprintCompleted`).
7. **Refusals for rules**, rather than rights (rules 2, 5, 6 and the gate rules of research R14), return
   their rule code; the matrix's ❌ cells are refusals for rights and return `Forbidden`.
