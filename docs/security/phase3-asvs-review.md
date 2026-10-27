# Phase 3 security review against OWASP ASVS 4.0.3, Level 2

**Scope**: what Phase 3 added or changed, as built on branch `phase-3`, 2026-09-30 (tasks.md T096):
- templates and the switch between Kanban and Scrum;
- the new work item types, epics and stages, and estimates;
- the backlog and sprints;
- stages, gates and their approvers, and the stage lock;
- dependencies;
- the timeline's plan: groups, markers, the sprint lane, arrows and the Actions menu;
- "Gates waiting for you".

Sign-in, sessions, cryptography and the host configuration did not change. They keep their Phase 1 status
([phase1-asvs-review.md](phase1-asvs-review.md)), and the Phase 2 items keep theirs
([phase2-asvs-review.md](phase2-asvs-review.md)).

**Method**: requirement-by-requirement review of the code and tests. It focused on:
- who may manage sprints and stages;
- who may decide a gate;
- the stage lock on every path that changes a status;
- the audit of approvers added as Viewers;
- leaks through the new searches, the backlog and "Gates waiting for you";
- what the timeline takes from the browser.

Each finding got a fix and a test (constitution III). Evidence is the automated tests named below.

**Result**: 1 finding fixed during the review. 1 new open item needs a product decision. The Phase 1 and Phase 2 open
items are unchanged.

## Finding fixed during the review

| # | ASVS | Finding | Fix | Test |
|---|------|---------|-----|------|
| F1 | 5.1.3, 11.1.4 | The timeline took the drag mode ("start", "end" or "move") from the browser. A milestone has no handles, but a changed page could send "start" for one. The page then asked for a start date, which the domain refused ("a milestone has one date"), so nothing wrong was saved, but the person saw an error. A drag sent for an item without dates would have asked to save "no dates" again. | A milestone only moves, whatever mode is sent, and a drag of an item without dates is ignored. | `TimelinePlanPageTests.A_milestone_dragged_by_either_end_only_moves_its_date` |

## Access control (V4)

| Check | Result | Evidence |
|-------|--------|----------|
| Every project-scoped call is checked by `IProjectAccess` before any other work: the backlog, sprints and reports, the Stages view and gates, dependencies and the item search, the timeline, and the drawer's new sections. | ✅ | `PermissionMatrixTests`: every action of [permissions.md](../../specs/004-templates-and-planning/contracts/permissions.md) for 8 kinds of caller (anonymous, non-member, Viewer, Approver, Member, creator, Project Admin, administrator), run against the real services and database. |
| Sprint management (create, rename, start, complete, delete) needs the Manage right: Project Admins and administrators. Members and Viewers are refused; Viewers read the backlog and reports. | ✅ | `SprintServiceTests.P3_US2_AS10_Only_Project_Admins_and_administrators_manage_sprints`, `…Viewers_read_sprint_reports`; `BacklogServiceTests.P3_US2_AS10_Viewers_read_the_backlog_and_Members_move_cards`. |
| Stage management (add, reorder, delete) and gate setup need Manage. Requesting approval and withdrawing need Contribute. | ✅ | `StageGateServiceTests.Members_cannot_manage_stages_or_gates_and_Viewers_cannot_request`. |
| Only a gate's named approvers decide it. That includes Viewers named as approvers, and excludes Project Admins and administrators who are not approvers. The requester is excluded when the gate says so. A decision from anyone else is `Forbidden`; from outside the project it is "not found". | ✅ | `StageGateServiceTests.P3_US3_AS6_Project_Admins_and_administrators_who_are_not_approvers_cannot_decide`, `…P3_US3_AS11_The_requester_cannot_approve_their_own_request`; the matrix's Approver column. |
| The stage lock applies on every path that changes a status, for everyone, administrators included. The paths are: board moves, the drawer's status, "mark done", sub-tasks through their parent's stage, putting started work into a locked stage, completing a stage before its gate, and deleting a column that holds locked work. A column changes its status type only when it is empty, so that cannot unlock work either. | ✅ | `StageLockTests` (all 9 tests), including `P3_US3_AS2_Work_in_a_locked_stage_cannot_leave_to_do_on_the_board_or_in_the_drawer`, `…P3_FR033_Started_work_cannot_be_put_into_a_locked_stage` and `…P3_FR033_Deleting_a_column_that_holds_locked_work_sends_it_only_to_a_to_do_column`. |
| Dependencies link items of one project only. Both keys are read, the caller's right is checked in the first item's project, and the second must be in the same project; otherwise "not found". The loop check runs under the planning lock, so two opposite links added at the same moment leave one. | ✅ | `DependencyServiceTests.Items_of_another_project_cannot_be_linked`, `…P3_US4_AS7_Two_opposite_links_added_at_the_same_moment_leave_one`, `…P3_US4_AS9_Viewers_cannot_add_or_remove_dependencies`. |
| A backlog move checks the Contribute right on the card's project and finds the target sprint only among that project's sprints. | ✅ | `BacklogService.MoveAsync`; `BacklogServiceTests.P3_US2_AS3_Cards_move_into_between_and_out_of_sprints_with_history`. |
| The timeline's calls from the browser (`OnBarDragged`) act only on bars shown on the page. Offsets are bounded (Phase 2 F1), and a milestone only moves (F1 above). The service checks the right again. | 🔧 | `TimelinePageTests.An_offset_no_drag_can_produce_is_ignored`, `TimelinePlanPageTests.A_milestone_dragged_by_either_end_only_moves_its_date`, `…P3_US4_AS9_Viewers_move_nothing_and_add_no_dependencies`. |

## Leaks (V4, V8)

| Check | Result | Evidence |
|-------|--------|----------|
| "Gates waiting for you" lists only gates in projects the approver can see now. After a removal, the gate leaves the list. | ✅ | `StageGateServiceTests.P3_FR041_Gates_waiting_leave_My_tasks_when_the_approver_can_no_longer_see_the_project` |
| The approver search needs Manage and finds active accounts across the organization, as the spec asks (FR-038), at most 20. Each result has the display name, the user name and whether the person is a member. The email address is never returned. | ✅ | `StageGateService.FindPeopleAsync` returns `ApproverOption(UserId, DisplayName, UserName, IsMember)`; `StageGateServiceTests.People_are_found_across_the_organization`. |
| The dependency search needs the View right and searches only that project's live items, by key or words in the title, at most 20. The words match literally. | ✅ | `DependencyServiceTests.Items_are_found_by_key_or_words_in_the_title`. |
| The backlog, sprint reports and the Stages view read only the project the caller can see. Deleted items stay out (the query filter on `WorkItems`). | ✅ | The matrix's 🚫 cells for non-members. |

## Audit (V7)

- **Template switches and gate events** each write an audit event in the same transaction as the change: setup,
  request, withdrawal, each decision with its note, and approval (`PlanningAuditEvent`).
- **Approvers from outside the project** are added as Viewers with a `MemberAdded` event that says so
  (`As = "Gate approver"`). Tests: `ProjectTeamTests.P3_US3_AS1_An_approver_from_outside_the_project_becomes_a_Viewer_and_it_is_audited`,
  `StageGateServiceTests.P3_US3_AS10_Every_step_is_in_the_stage_s_history_and_the_audit_log`.
- **Sprint changes, dependencies and the plan's dates** are recorded in the items' history, as the spec asks, not in
  the audit log.
- **Append-only tables**: gate decisions and sprint scope events are append-only, like history and the audit log.
  Update and delete are refused by trigger.

## Other requirement areas

Status: ✅ met · 🔧 met after the fix above · ⚠️ open (see below). Areas Phase 3 did not touch are not repeated.

| Area | Status | Evidence |
|------|--------|----------|
| **V1 Architecture** | ✅ | Sprints, gates and dependencies live in the Work module. It reaches membership only through `IProjectTeam` and people only through the Identity contracts (`ModuleBoundaryTests`). The new entities keep their state private (`EntityEncapsulationTests`). |
| **V5 Validation and encoding** | 🔧 | Limits checked in the domain: sprint names (60 characters) and goals (500); gate notes (1,000, required to send back); estimates (0 to 999.9, one decimal place); approvals needed (1 to the number of approvers). Database constraints back them: `CK_Sprints_Dates`, one active sprint per project (`IX_Sprints_OneActive`), `CK_Dependencies_NotSelf` and one link per pair. All text is rendered through Razor. `timeline.js` builds the arrows as SVG elements from computed numbers and the bars' own IDs, never as HTML from data. Drag modes are bounded (F1). |
| **V11 Business logic** | ⚠️ | Concurrency: gates carry a version and sprints a version; a stale change returns the current state. Two sprints started at once leave one active; two approvers deciding at once are both kept. Limits: 20 search results, 100 backlog rows a page, 500 timeline rows, 200 arrows. The number of approvers on one gate has no limit (P3-O1). |
| **V13 API** | ✅ | No new HTTP endpoints. The only calls from the browser are the timeline's drag (above) and the backlog's drag-and-drop, which calls the same service as the Move menu. |
| **V14 Configuration** | ✅ | No new packages or third-party scripts. `timeline.js` is served by the site itself (CSP `script-src 'self'`). |

## Open items

| # | ASVS | Item | Risk | Recommendation |
|---|------|------|------|----------------|
| **P3-O1** | 11.1.4 | A gate can have any number of approvers. Each must be an active account, and people outside the project are added as Viewers. A Project Admin, or a changed page, could name everyone in the organization and so add them all to the project as Viewers. | Low: only Project Admins can do it, it is audited per person, and it can be undone from Members. | **Product decision**: set a limit, for example 20 approvers per gate, as the people search already shows at most 20. |
| P2-O1 | V4, V8 | Project names and keys are unique across the organization (Phase 2 review). | As in the Phase 2 review. | Unchanged. |
| O1–O4 | 2.2.3, 2.3.1, 2.7–2.8, 3.3.4, **4.3.1 (L2)** | Phase 1's open items: no multi-factor sign-in, no list of one's sessions, no notice of password changes, no expiry for temporary passwords. | As in the Phase 1 review. | Decide O1, a Level 2 requirement, before rollout beyond the pilot. |

## Re-running the checks

```bash
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests" --filter-class "*StageLockTests" --filter-class "*StageGateServiceTests" --filter-class "*SprintServiceTests" --filter-class "*BacklogServiceTests" --filter-class "*DependencyServiceTests" --filter-class "*ProjectTeamTests"
dotnet test --project tests/Upms.Web.Tests -- --filter-class "*TimelinePageTests" --filter-class "*TimelinePlanPageTests"
dotnet test --project tests/Upms.Architecture.Tests
```
