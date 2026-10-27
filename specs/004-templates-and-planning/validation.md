# Quickstart validation: Templates and Planning (Phase 3)

**Tasks**: tasks.md T095, T099 | **Guide**: [quickstart.md](./quickstart.md) | **Date**: 2026-09-30 |
**Branch**: `phase-3`

The checks ran on Windows 11 Pro with:
- .NET SDK 10.0.401;
- Docker Desktop 29.8, with 3.76 GB for containers;
- SQL Server 2022 (16.0.4295, `mcr.microsoft.com/mssql/server:2022-latest`).

The app ran with `dotnet run` on `https://localhost:5001` against its own database, `UpmsP3Validation`. The
walkthrough used the browser built into the Claude desktop app. Playwright's Chromium is not installed on this
machine, so the end-to-end suite has not run yet (see "Still to do").

## Summary

| Step | Result | Notes |
|------|--------|-------|
| 1. Upgrade a Phase 2 database (SC-010) | ✅ | The Phase 2 app (commit `eaea9fd`) built the data through its own screens on a new database. The Phase 3 app then applied its four migrations at startup, and everything checked was unchanged. See below. `TemplatesUpgradeTests` checks the same in the automated suite. |
| 2. Validate each story by hand | ⚠️ partly | US1 steps 1–6 and 8 passed after four wording fixes (below). US1 step 7 and US2 to US4 are still to do. The walkthrough stopped because the browser pane cannot click while the desktop app's window is hidden. |
| 3. Automated tests | ✅ except E2E | See the list below the table. |
| 4. Quality gates | ✅ | `dotnet format --verify-no-changes` is clean. It runs on a copy with LF line endings, because this checkout converts to CRLF. `dotnet build -c Release` has no warnings. `has-pending-model-changes` reports none. |
| 5. Performance (SC-002) | ⏳ | See "Step 5". |

Automated tests (step 3):
- Domain: 253.
- Application: 910, as the full suite before the last US4 changes. Since then, the US4 service classes (28) and
  the Phase 3 permission matrix (562 cells and rules, none skipped) passed again.
- Web: 326.
- Architecture: 15.
- End to end: the `P3_US1`–`P3_US4` journeys and the Phase 3 responsive and keyboard tests are written and build,
  but have not run.

## Step 1: upgrading a Phase 2 database

Before the upgrade, the Phase 2 app built this data through its screens:
- **Accounts**: the administrator, plus sara, amina, omar, bilal and farah, added on the Accounts page.
- **Project**: Website Revamp (`WEB`), with WEB-1 to WEB-3 on its board.
- **WEB-1**: assigned to Amina Khan, dated 5 to 10 October, with sub-task WEB-4 and 5 history entries.
- **Team**: the administrator (Project Admin), amina (Member) and bilal (Viewer).

After `dotnet run` of the Phase 3 app (Development applies migrations at startup):

| Check | Result |
|-------|--------|
| The migrations `TemplatesAndTypes`, `Sprints`, `StageGates` and `Dependencies` are applied, after Phase 2's | ✅ |
| The project list shows `WEB` with the template **Kanban** | ✅ |
| WEB-1 to WEB-3 are type Task and WEB-4 a Sub-task of WEB-1; WEB-1 keeps its assignee, its dates and its 5 history entries | ✅ |
| WEB-1's drawer offers the new Type, Epic, Estimate and Dependencies fields | ✅ |

## Step 2: stories checked by hand

| Check | Result |
|-------|--------|
| US1.1 The create form offers Kanban (preselected), Scrum and Stage-gate. `MOB` (Scrum) has To Do, In Progress, In Review and Done, with Backlog in its header. `CBU` (Stage-gate) has Not Started, In Progress, On Hold and Completed, and its Stages view lists Initiation (current), Planning, Execution and Closure (locked, "Waits for the Initiation gate"). amina and omar are Members and bilal is a Viewer of both | ✅ |
| US1.2 `WEB` checked as in step 1 | ✅ |
| US1.3 In the `MOB` list, "Card refunds" with type Epic becomes `MOB-1`. The list's type choice offers Task, Story, Bug and Epic | ✅ |
| US1.4 A story and a bug are put in the epic from their drawers, and the story is marked Done. The epic's drawer shows "1 of 2 done (50%)" and no type choice. The bug changed to a Story; its history says "changed the type from Bug to Story" | ✅ |
| US1.5 Milestone `CBU-5` "Planning sign-off" in Planning, dated 30 October: one "Date" field, no start date, no sub-tasks, and no card on the board | ✅ after fix 1 |
| US1.6 Type and epic filters show as chips, are kept in the address (`?kind=story&container=MOB-1`), and come back after a reload | ✅ |
| US1.8 Deleting the epic keeps its two items, in no epic; each has the history entry "took the story out of MOB-1 Card refunds" | ✅ after fix 2 |
| US2.1 The backlog lists the open stories in order. MOB-4's two sub-tasks show as "2 sub-tasks" on its row, not as rows. **Move → Move to top** puts MOB-9 first, keeps focus on its Move button, and announces the new place | ✅ after fix 4 |

**Fixed during the walkthrough** (each test first):
1. **A milestone's date**: saving it said "Due date saved", although the field is "Date". It now says "Date saved"
   (`TypeAndContainerTests.A_milestone_s_date_is_saved_as_its_date`).
2. **Deleting an epic or stage**: the confirmation said "It has no sub-tasks" and "Delete task". It now says "Its 2
   items stay in the project, in no epic." and the button is "Delete epic"
   (`TypeAndContainerTests.P3_US1_AS10_Deleting_an_epic_says_its_items_stay`).
3. **The List's count**: it said "3 tasks" for an epic, a story and a bug. It now says "3 items". The Phase 2 tests
   were updated to match.
4. **Moving a row within its list**: the backlog announced "MOB-9 moved to the backlog, position 1 of 8." for a row
   that was already there. It now says "MOB-9 moved to position 1 of 8 in the backlog."

**Noted, not changed**:
- History entries name the item by its current type: a bug later changed to a story reads "created the story".
- When an epic is deleted, its items' history says they were taken out of it, but not that it was deleted.

## Step 5: performance (SC-002)

Pending: the full run with 300 simulated users on about 500,000 work items. An earlier small run (50,000 work items,
40 users, 30 s), with sprints and gates seeded, passed with every p95 under 300 ms.

## Still to do

- **The rest of the walkthrough**: US1 step 7 (switch `WEB` to Scrum and back, as its Project Admin), then US2
  steps 1 (dragging) to 4, US3 and US4.
- **The end-to-end suite** (journeys, keyboard-only paths and axe scans at 1280 px and 360 px) once Playwright's
  Chromium is installed:

  ```
  pwsh tests/Upms.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
  ```
- **The manual screen-reader pass** (T100).
