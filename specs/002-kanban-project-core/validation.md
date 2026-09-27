# Quickstart validation: Core Kanban Project (Phase 1)

**Task**: tasks.md T114 | **Guide**: [quickstart.md](./quickstart.md) | **Date**: 2026-09-27 |
**Branch**: `claude/practical-lamport-l47ei8`

Every step of the quickstart was run on a clean checkout in a Linux container (4 vCPUs, 16 GB): .NET SDK
10.0.401, Docker Engine with Compose v5, SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`),
Chromium from Playwright 1.56.

## Summary

| Step | Result | Notes |
|------|--------|-------|
| 1. Start SQL Server | ✅ | `docker compose up -d sqlserver`: healthy after about 10 seconds. |
| 2. Configure secrets | ✅ | `dotnet user-secrets set` for the connection string, setup token and public URL. |
| 3. Create the database and run | ✅ after a fix | `dotnet ef database update` first failed ("Login failed for user ''"): the EF design-time factory read only the `ConnectionStrings__Default` environment variable, not the user secrets from step 2. **Fixed**: it now reads Upms.Web's user secrets, with the environment variable taking precedence. Five migrations applied; `dotnet run` served `https://localhost:5001`; `/setup` created the first administrator and then returned 404. |
| 4. Validate each story by hand | ✅ | Scripted walkthrough of every step in section 4 (below): 20 of 20 checks pass. One UX improvement came out of it (below). |
| 5. Automated tests | ✅ | 497 tests: Domain 98, Application 288, Web 87, Architecture 7, E2E 17 (journeys and axe scans). |
| 6. Quality gates | ✅ | `dotnet format --verify-no-changes` clean; `dotnet build -c Release` with no warnings; no pending model changes. |
| 7. Performance (SC-002) | ✅ after tuning | See below. |
| 8. Restore drill (SC-009, SC-010) | ✅ locally | Point-in-time restore of the quickstart database into a new database, checked by a second app instance. See below; the drill on production backups is still due before the pilot. |

## Step 4: stories checked by hand

The walkthrough drove two or three browsers (administrator, amina, bilal) through the steps of the quickstart
exactly as written:

| Check | Result |
|-------|--------|
| Setup creates the first administrator; `/setup` then returns 404 | ✅ |
| US1.1 The administrator adds amina and bilal and sees each temporary password once | ✅ |
| US1.2 Anonymous visits go to sign-in; a temporary password must be replaced first | ✅ |
| US1.3 "Website Revamp" suggests `WR`; created as `WEB` with To Do, In Progress, Done | ✅ |
| US1.4 A second project with key `WEB` is refused and the input kept | ✅ |
| US1.5 Inline creation gives `WEB-1`, then four more tasks | ✅ |
| US1.6–8 Drag to a column, drag above a card, keyboard "Move to" Done | ✅ |
| US1.7, US1.9 Bilal sees the same board; amina's stale drag of the card bilal moved is reported as a conflict | ✅ |
| US2.1 The drawer opens from the card, and from its link in bilal's browser | ✅ |
| US2.2–4 Title, two-line description with a working link, priority High; the card follows | ✅ |
| US2.3 Three sub-tasks, one done: the card shows 1/3; sub-tasks are not cards | ✅ |
| US2.4 Done with two open sub-tasks is allowed, with a warning | ✅ |
| US2.5 Two comments, one edited ("(edited)"), one deleted (placeholder); bilal cannot change them | ✅ |
| US2.6 History lists the changes with old and new values | ✅ |
| US2.7 Concurrent description edits: the second sees the conflict and keeps their text | ✅ |
| US2.8 The creator deletes WEB-1 (confirmation mentions its 3 sub-tasks); the administrator restores it | ✅ |
| US3.1 "In Review" added after In Progress; To Do renamed Backlog; a column moved; limit 3 on In Progress | ✅ |
| US3.2 A fourth card in In Progress shows "4 of 3" and "Over limit"; the move succeeds | ✅ |
| US3.3 Deleting "In Review" with two tasks moves them to Done; their history notes "column deleted" | ✅ |
| US3.4–6 The only "done" column cannot be deleted and the only "to do" column cannot be retyped (its type is locked while it holds tasks); "backlog" is refused as a duplicate; bilal is not offered settings, a direct visit shows "Not found", and he can still move cards | ✅ |

**Improvement made**: when deleting the only "done" (or "to do") column, the dialog first asked where to move
its work items and refused only after that. It now explains straight away that the column cannot be deleted
(test `BoardColumnsEditorTests.US3_AS6_Deleting_the_only_done_column_is_explained_without_asking_for_a_destination`).

## Step 7: performance

Setup: `tools/Upms.Seed` defaults (2,000 users, 1,000 projects, 498,935 work items, 1.5 million history rows,
198,078 comments; seeded in 88 seconds). Then 300 simulated users ran for 120 s after a 20-second warm-up, on the
same 4-vCPU host as SQL Server.

| Action (p95, ms) | First run | After the index migration | Target |
|------------------|----------:|--------------------------:|-------:|
| Project list | 5,138 | 109 | ≤ 1,000 |
| Board load (up to about 500 cards) | 2,385 | 111 | ≤ 1,000 |
| Inline creation | 2,271 | 123 | ≤ 1,000 |
| Card move | 3,226 | 129 | ≤ 1,000 |
| Drawer open | 4,914 | 173 | ≤ 1,000 |
| Saving an edit | 5,449 | 199 | ≤ 1,000 |

The first run was limited by one query: the project list's open-work count scanned the whole `WorkItems`
table (about 400 ms of CPU per call). Migration `PerformanceIndexes` adds filtered indexes for live work items
per status and for sub-task counts, and makes the board index covering. Throughput rose from 72 to 174 calls per
second, with no failures.

## Step 8: restore drill (local)

Run against the quickstart database `Upms`, in the full recovery model, following
[backup-restore.md](../../docs/operations/backup-restore.md):

| Event (UTC) | Detail |
|-------------|--------|
| 05:40:16 | Full backup (794 pages, 0.06 s) |
| 05:40:17.49 | amina creates "Drill marker A" (WEB-14) |
| 05:40:17 | Target time noted; log backup 1 |
| 05:40:23.97 | amina creates "Drill marker B" (WEB-15) |
| 05:40:24 | Log backup 2; failure declared |
| | Restore into `Upms_Drill`: full `WITH NORECOVERY`, log 1 `WITH NORECOVERY`, log 2 `WITH STOPAT = '05:40:18', RECOVERY`: 1.2 s |
| | `DBCC CHECKDB`: no errors |
| | A second app instance on `Upms_Drill` was ready in 8 s; amina signed in with her password, and the board showed "Drill marker A" and not "Drill marker B" (made after the target time), as intended |

**Result**: pass. The restore returned the database to the chosen moment with no loss before it, far inside
the 1-hour RPO and 4-hour RTO. Two lessons are now in the runbook's spirit: compute the `STOPAT` time with a tool
that does not read "+1" as a time zone (our first attempt did), and start a checking instance from a published
build or in Development (an unpublished build in Production does not serve static files).

**Still to do before the pilot**: the same drill on the production server with its real backup files, recorded
in the runbook's drill record.
