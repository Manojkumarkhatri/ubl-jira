# Quickstart validation: Project Views and Team (Phase 2)

**Tasks**: tasks.md T065, T069 | **Guide**: [quickstart.md](./quickstart.md) | **Date**: 2026-09-27 |
**Branch**: `claude/practical-lamport-l47ei8`

Every step of the quickstart was run in a Linux container (4 vCPUs, 16 GB): .NET SDK 10.0.401, Docker 29.3 with
Compose, SQL Server 2022 (16.0.4295, `mcr.microsoft.com/mssql/server:2022-latest`), Chromium from Playwright
1.56. The app ran with `dotnet run` on `https://localhost:5001`, as in the Phase 1 guide.

## Summary

| Step | Result | Notes |
|------|--------|-------|
| 1. Upgrade a Phase 1 database (SC-006) | ✅ | A Phase 1 database built through the Phase 1 app's own screens was upgraded with `dotnet ef database update`. The migrations `ProjectMembers` and `AssigneesAndDates` were applied, all 5 checks passed, and exactly one "Phase 2 upgrade" audit event was written per new membership. See below. |
| 2. Validate each story by hand | ✅ after fixes | A scripted walkthrough of every step in section 2, with four browsers (owen, amina, bilal, carla) and the administrator. The final run on a freshly upgraded database passed all 23 steps in one go. Earlier runs found three timing bugs, all fixed with tests, and two wording problems in the guide (below). |
| 3. Automated tests | ✅ | 875 tests: Domain 126, Application 520, Web 197, Architecture 8, E2E 24 (journeys, keyboard-only paths and axe scans at 1280 px and 360 px). Phase 2 acceptance tests are named `P2_US1_AS…` to `P2_US4_AS…`. |
| 4. Quality gates | ✅ | `dotnet format --verify-no-changes` clean; `dotnet build -c Release` with no warnings; no pending model changes. |
| 5. Performance (SC-002) | ✅ | 300 simulated users on 498,935 work items: every action's p95 is at most 211 ms, against a 1,000 ms target, with no failures. No new index was needed beyond those planned. See below. |

## Step 1: upgrading a Phase 1 database

The Phase 1 app (commit `e4b1106`, before any Phase 2 code) created a new database and was driven through its
screens: the administrator added owen, cora, cole and olga; each replaced the temporary password; owen created
"Pilot Rollout" (`PIL`) and PIL-1; cora added PIL-2; cole only commented on PIL-1; olga created her own project
"Olga Notes" (`OLG`). Then, with the Phase 2 code, the quickstart's `dotnet ef database update` applied the two
Phase 2 migrations and the app was started.

| Check | Result |
|-------|--------|
| The owner, owen, sees Pilot Rollout as **Project Admin**; its Members page lists owen (Project Admin), cora and cole (Member) | ✅ |
| cora, who created a task, is a **Member** | ✅ |
| cole, who only commented, is a **Member** | ✅ |
| olga, who never worked in it, no longer sees it, and `/projects/PIL/board?task=PIL-1` shows "Not found"; she is Project Admin of her own project | ✅ |
| The administrator still sees every project, with "Administrator access" | ✅ |

Audit log after the upgrade (`AuditEvents`, events with no acting person):

| Event | Project | Member | Details |
|-------|---------|--------|---------|
| MemberAdded | PIL | owen | `{"Role":"ProjectAdmin","Source":"Phase 2 upgrade"}` |
| MemberAdded | OLG | olga | `{"Role":"ProjectAdmin","Source":"Phase 2 upgrade"}` |
| MemberAdded | PIL | cora | `{"Role":"Member","Source":"Phase 2 upgrade"}` |
| MemberAdded | PIL | cole | `{"Role":"Member","Source":"Phase 2 upgrade"}` |

## Step 2: stories checked by hand

Final run, on a database upgraded as in step 1 (owen from the Phase 1 data; amina, bilal and carla added by the
administrator):

| Check | Result |
|-------|--------|
| US1.1 owen creates Website Revamp (`WEB`) and is its only member, as Project Admin | ✅ |
| US1.2 "Add member": amina found by part of her name and added as Member, bilal as Viewer; three members listed | ✅ |
| US1.3 bilal, a Viewer, sees the board, list, timeline and a drawer but is offered no change: no "What needs to be done?", no dragging or "Move to", read-only drawer, no "Schedule" | ✅ |
| US1.4 carla does not see `WEB`, and a link to WEB-1 shows "Not found" | ✅ |
| US1.5 amina, removed while her board is open, is refused at her next action and `WEB` leaves her list; she is added back | ✅ |
| US1.6 bilal, made a Member, adds a task without signing in again | ✅ |
| US1.7 owen cannot remove himself or step down: "Make someone else a Project Admin first." | ✅ |
| US1.8 the administrator, not a member, has full rights in `WEB`, including Members and settings | ✅ |
| US2.1 owen assigns WEB-1 to amina (her initials on the card, the change in the history); bilal uses "Assign to me" on WEB-2 | ✅ |
| US2.2 the assignee choice offers only active Project Admins and Members, plus "Unassigned" | ✅ |
| US2.3 WEB-3 gets 1–10 October; a due date before the start is refused and kept in the field | ✅ |
| US2.4 WEB-1 due yesterday is "Overdue" on the card, in the list and on amina's My tasks; the mark goes once it is Done | ✅ |
| US2.5 amina's "Only my tasks" leaves only her two cards, and the columns show "matching of total" | ✅ |
| US2.6 amina's My tasks lists her open tasks from both projects, soonest due first; after she leaves "Branch Survey" its tasks leave her list and show her as "no longer on the project" | ✅ |
| US3.1 with 61 tasks the List view shows 50 rows and "61 tasks", newest key first; a sub-task shows its parent | ✅ |
| US3.2 sorting by due date and reversing it; "in progress" and "Me" filters as chips; one chip removed; "Clear filters" | ✅ |
| US3.3 the address of a filtered, sorted list shows bilal the same rows, filter and sort | ✅ |
| US3.4 a priority changed in a row's drawer shows in the row; a task added from the list starts in To Do; a search matching nothing offers "Clear filters" | ✅ |
| US4.1 four of six tasks dated: four bars (one a one-day bar), the today line and two unscheduled tasks; weeks and quarters | ✅ |
| US4.2 a bar dragged two weeks later moves both dates, recorded as one change; a dragged right end changes only the due date and cannot pass the start | ✅ |
| US4.3 Left then Enter moves a bar a day earlier, saved once and keeping focus; Shift changes only the due date; Ctrl then Escape changes nothing | ✅ |
| US4.4 "Schedule" gives today to six days later; a task with a scheduled and an unscheduled sub-task expands to show both | ✅ |
| US4.5 after bilal changes a task's dates, amina's drag of its bar is refused with "RMP-1 was changed by someone else…" and her bar shows the new dates; carla, a Viewer, cannot move bars | ✅ |

**Bugs found and fixed** (each fixed test-first; all three were about work reaching the page after a round
trip through the browser, which the in-process tests did not wait for):

1. **Timeline keys pressed while a change was being saved.** Keys pressed while a keyboard change was still
   being saved built on the task's old dates. A quick second Enter was sent with the old version and came back
   as a conflict ("changed by someone else"). Saves now go one at a time, in order, each with the version the
   one before it returned. Keys pressed meanwhile build on the dates being saved, and changes queued behind a
   save that fails are dropped (`TimelinePageTests.Keys_pressed_while_a_change_is_being_saved_build_on_it_and_the_next_save_waits_for_it`,
   `…A_change_queued_behind_a_save_that_fails_is_dropped`).
2. **List filters and timeline settings chosen in quick succession.** A new address reaches the page only
   after a round trip, so a second filter chosen before the first arrived was built on the old address and
   dropped the first. The same happened with the timeline's scale and "Hide completed", and with removing a
   chip. Also, a list answer for an older address could arrive after a newer one and replace it. Changes now
   build on the last one asked for, and late answers for old addresses are ignored (`ListAddressTimingTests`,
   `TimelineAddressTimingTests`).
3. **Focus taken back after the drawer closed.** On the timeline, the list, the board and My tasks, focus went
   back to the drawer's task a second time, after a round trip. A person who had already moved on, for example
   to another bar, lost focus, and their next Enter opened the wrong task. The browser already returns focus
   when the drawer closes, so the page now does it only if focus was lost (`upms.focusByIdIfLost`;
   `P2_US4_TimelineTests` moves another bar with the keyboard straight after closing the drawer).

**Guide corrections**: US2 step 3 now names WEB-3. Step 4 puts yesterday's due date on WEB-1, which would be
refused if WEB-1 had just been given a start date in October. US4 step 2 now says the drag is "recorded as one
change": the history shows the start and due date on two lines with the same time, not one line.

## Step 5: performance (SC-002)

Setup: `tools/Upms.Seed` defaults (2,000 users, 1,000 projects of 4–20 Members and up to 3 Viewers, the largest
with 40 Members; 498,935 work items, about 70% assigned and half with a due date). Then 300 simulated users ran
for 120 s after a 20-second warm-up, on the same 4-vCPU host as SQL Server. Every Phase 1 and Phase 2 action was
in the mix:

| Action | Calls | p50 ms | p95 ms | p99 ms | Target (p95) |
|--------|------:|-------:|-------:|-------:|-------:|
| Project list | 1,412 | 11 | 44 | 96 | ≤ 1,000 |
| My tasks load | 906 | 19 | 75 | 125 | ≤ 1,000 |
| Board load | 5,903 | 30 | 118 | 192 | ≤ 1,000 |
| List load (sorted and filtered) | 1,224 | 28 | 120 | 180 | ≤ 1,000 |
| Timeline load | 917 | 39 | 139 | 236 | ≤ 1,000 |
| Inline creation | 1,639 | 26 | 120 | 193 | ≤ 1,000 |
| Card move | 1,698 | 34 | 134 | 224 | ≤ 1,000 |
| Drawer open | 3,911 | 42 | 182 | 282 | ≤ 1,000 |
| Saving an edit | 1,368 | 51 | 195 | 338 | ≤ 1,000 |
| Assigning | 717 | 56 | 211 | 378 | ≤ 1,000 |
| Setting dates | 733 | 53 | 208 | 336 | ≤ 1,000 |
| Rescheduling on the timeline | 282 | 30 | 115 | 181 | ≤ 1,000 |
| Membership change | 102 | 37 | 121 | 161 | ≤ 1,000 |

No call failed. 35 edits met a conflict with another simulated user's change to the same task, which is the
intended answer (SC-005). The indexes planned for Phase 2 were enough, so no tuning was needed:

- the board index covers assignee and due date;
- `IX_WorkItems_Project_Live` serves the list and the timeline;
- `IX_WorkItems_Assignee_Live` serves My tasks;
- the member index on `(UserId)` includes the role.

An earlier run after US3, before the timeline existed, had every p95 under 170 ms.
