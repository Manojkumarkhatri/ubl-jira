# Phase 2 security review against OWASP ASVS 4.0.3, Level 2

**Scope**: what Phase 2 added or changed (project teams and members-only projects, assignees and dates, the List
and Timeline views, "My tasks", the board filters, the drawer changes, and the upgrade of Phase 1 data), as built
on branch `claude/practical-lamport-l47ei8`, 2026-09-27 (tasks.md T066). Sign-in, sessions, cryptography and the
host configuration did not change and keep their Phase 1 status ([phase1-asvs-review.md](phase1-asvs-review.md)).
**Method**: requirement-by-requirement review of the code and tests, focused on access control for every
project-scoped call and task key, leaks through "My tasks", the filters and the people search, and the audit of
team changes; a fix and a test for each finding (constitution III). Evidence is the automated tests named below.

**Result**: 1 finding fixed during the review. 1 new open item needs a product decision. The 4 Phase 1 open
items are still open: the decision of 27 Sep 2026 kept them out of Phase 2.

## Finding fixed during the review

| # | ASVS | Finding | Fix | Test |
|---|------|---------|-----|------|
| F1 | 5.1.3, 5.1.4 | The timeline took the day offset of a drag from the browser without a range check. An out-of-range value sent by a changed page raised an error in that page's own connection. Nothing was saved, and the service would have refused dates outside 2000–2099 anyway. | Offsets longer than the span of allowed dates (36,524 days) are ignored before any date is computed. | `TimelinePageTests.An_offset_no_drag_can_produce_is_ignored` |

## Access control (V4)

| Check | Result | Evidence |
|-------|--------|----------|
| Every project-scoped call is checked by `IProjectAccess` before any other work: board, list, timeline, drawer, comments, team, people search, team changes, assigning and dates. | ✅ | `PermissionMatrixTests`: 311 cells, every action of [permissions.md](../../specs/003-project-views-and-team/contracts/permissions.md) for 7 kinds of caller (anonymous, non-member, Viewer, Member, creator, Project Admin, administrator), run against the real services and database. |
| People outside the team learn nothing: every right answers "not found", as for a project that does not exist. | ✅ | `ProjectAccessTests.P2_US1_AS4_Non_members_are_told_the_project_does_not_exist` (every right); `P2_US1_ProjectTeamTests` (the list of projects and a direct task link). |
| Task keys: a call by key (drawer, edit, comment, reschedule) reads the item and then checks the caller's right in the item's own project. A key that does not exist and a key in a project the caller cannot see get the same "not found". | ✅ | The matrix's 🚫 cells for non-members; `TimelineService.RescheduleAsync` and `WorkItemService` read the item first and then check the right. |
| Rights are read from the database on every call, so a removal or a role change applies at the next action, without signing in again. | ✅ | `ProjectAccessTests.P2_US1_AS5_A_removal_applies_at_the_next_call`, `…AS6_A_role_change_applies_at_the_next_call`; end to end in `P2_US1_ProjectTeamTests`. |
| Viewers can read but not change anything. The server refuses their changes; the screens hide the controls but are not relied on. | ✅ | `ProjectAccessTests.P2_US1_AS3_Viewers_can_look_but_not_change_anything`, `AssigneeAndDatesTests.Viewers_are_offered_no_one_and_cannot_assign_or_schedule`, `TimelinePageTests.P2_US4_AS9_…`. |
| Team changes need Project Admin or administrator rights. The last active Project Admin can neither leave nor step down, even when two Project Admins remove each other at the same moment. | ✅ | `ProjectMemberServiceTests.Every_member_can_see_the_team_but_only_Project_Admins_and_administrators_change_it`, `…P2_US1_AS7_…`, `…Two_Project_Admins_removing_each_other_at_the_same_moment_leave_one`. |
| Assignees: only active Project Admins and Members of the task's project. The check is made again when saving, so someone removed after the drawer opened is refused. | ✅ | `AssigneeAndDatesTests.P2_US2_AS3_Only_active_Project_Admins_and_Members_are_offered_and_accepted`, `…P2_US2_AS3_Someone_removed_since_the_drawer_opened_cannot_be_assigned`. |
| "My tasks" shows only tasks in projects the person can see now; after a removal those tasks disappear from it. | ✅ | `MyTasksServiceTests.P2_US2_AS8_Tasks_in_a_project_the_person_was_removed_from_are_left_out`, `…Completed_deleted_and_other_peoples_tasks_are_left_out`. |
| List filters from a shared address are parsed into typed values: numbers, IDs and known names; values that mean nothing are ignored. At most 10 search words are used, and they match literally (`%` and `_` are not wildcards). An address with 5,000 values that mean nothing still gets the right answer. | ✅ | `ListStateTests`, `WorkItemListServiceTests.P2_US3_AS3_Words_are_found_in_titles_and_descriptions_regardless_of_case`, `…Thousands_of_filter_values_from_an_address_are_answered_as_if_only_the_meaningful_ones_were_given`. |
| The people search needs the Manage right and finds only active accounts that are not members yet, at most 20. Each result has the display name and user name. The email address can be searched on but is never returned. | ✅ | `ProjectMemberServiceTests.People_are_found_by_name_user_name_or_email_leaving_out_members_and_deactivated_accounts`, `…At_most_twenty_people_are_suggested`. |
| The timeline's one call from the browser into the page (`OnBarDragged`) acts only on bars shown on the page. The service checks the right again, and the offset is bounded (F1). | 🔧 | `TimelinePageTests.P2_US4_AS9_…`, `…An_offset_no_drag_can_produce_is_ignored`. |

## Audit of team changes (V7)

Adding a member, changing a role and removing a member each write an audit event (`MemberAdded`,
`MemberRoleChanged`, `MemberRemoved`) with the actor, the member, the project and the role or roles. The event is
written in the same transaction as the change, so there is never a change without its event
(`ProjectMemberServiceTests.Role_changes_and_removals_apply_and_are_audited`). The upgrade of Phase 1 data writes
one `MemberAdded` event per membership it creates, with the source "Phase 2 upgrade"
(`MembershipUpgradeTests.P2_US1_AS9_…`). The audit table stays append-only (update and delete refused by trigger).
Refusals of people outside a team are logged as access-control events (event 4030, IDs only).

## Other requirement areas

Status: ✅ met · 🔧 met after the fix above · ⚠️ open (see below). Areas Phase 2 did not touch are not repeated.

| Area | Status | Evidence |
|------|--------|----------|
| **V1 Architecture** | ✅ | Membership lives in the Projects module. The Work module reaches it only through `IProjectTeam` in `Projects.Contracts`, and the Web layer only through application services (`ModuleBoundaryTests`). |
| **V5 Validation and encoding** | 🔧 | Dates are checked in the domain (between 1 Jan 2000 and 31 Dec 2099; due not before start) and again by a database check constraint (`CK_WorkItems_Dates`). Roles are checked against the defined values. The new screens render all user text through Razor, which encodes it. `timeline.js` builds no HTML from data, and inline styles carry only computed numbers. Timeline offsets are bounded (F1). |
| **V8 Data protection** | ✅ | The List view keeps its filters, including search words, in the address, because the spec makes list addresses shareable (FR-030). Search words are not secrets, and pages keep `Cache-Control: no-store`. People search results never include email addresses. |
| **V11 Business logic** | ✅ | Optimistic concurrency covers team changes (team version) and dates (row version). A timeline conflict shows the current dates. Limits: 500 timeline rows, 50 rows a page (list, unscheduled tasks, "My tasks"), 20 people per search, 10 search words. |
| **V13 API** | ✅ | No new HTTP endpoints. The only new call from the browser is `OnBarDragged` (above). |
| **V14 Configuration** | ✅ | No new packages or third-party scripts. `timeline.js` is served by the site itself (CSP `script-src 'self'`). |

## Open items

| # | ASVS | Item | Risk | Recommendation |
|---|------|------|------|----------------|
| **P2-O1** | V4, V8 | Project names and keys are unique across the organization, so anyone creating a project learns that a name or key is taken ("Another project already has this name.", "The key … is already used by another project."), even for projects they cannot see. The key suggestion also skips keys that are taken. Nothing else about such a project is revealed. | Low: it confirms only an exact name or key that someone guessed. It matters only if project names themselves are confidential. | **Product decision**: if names need not be unique, drop the name rule (keys stay unique, as task keys depend on them). Otherwise accept the risk and tell pilot users not to put confidential words in project names. |
| O1–O4 | 2.2.3, 2.3.1, 2.7–2.8, 3.3.4, **4.3.1 (L2)** | Phase 1's open items: no multi-factor sign-in (including for administrators), no list of one's sessions, no notice of password changes, no expiry for temporary passwords. | As in the Phase 1 review. | Kept out of Phase 2 by the decision of 27 Sep 2026. Decide O1, a Level 2 requirement, before rollout beyond the pilot. |

## Re-running the checks

```bash
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests" --filter-class "*ProjectAccessTests" --filter-class "*ProjectMemberServiceTests" --filter-class "*MyTasksServiceTests" --filter-class "*MembershipUpgradeTests"
dotnet test --project tests/Upms.Web.Tests -- --filter-class "*TimelinePageTests" --filter-class "*ListStateTests"
dotnet test --project tests/Upms.E2E.Tests -- --filter-class "*P2_US1_ProjectTeamTests"
```
