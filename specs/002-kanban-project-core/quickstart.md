# Quickstart & Validation Guide: Core Kanban Project (Phase 1)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

How to run Phase 1 locally and prove each user story works end to end. The projects and files named
here are created during implementation (see `tasks.md`); this guide defines how they are validated.

## Prerequisites

- .NET 10 SDK (version pinned in `global.json`)
- Docker (Docker Desktop, or Docker Engine with Compose) for SQL Server
- A current Microsoft Edge, Chrome or Firefox

## 1. Start SQL Server

```bash
export MSSQL_SA_PASSWORD='<local-sa-password>'   # or put it in a local .env file (never committed)
docker compose up -d sqlserver      # standard SQL Server 2022 image; no other services in Phase 1
docker compose ps                   # sqlserver: healthy
```

## 2. Configure secrets (development only)

```bash
cd src/Upms.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=Upms;User Id=sa;Password=<local-sa-password>;TrustServerCertificate=True"
dotnet user-secrets set "Setup:Token" "<any-long-random-string>"
dotnet user-secrets set "App:PublicBaseUrl" "https://localhost:5001"
```

Production reads these from environment variables or the company secret store; nothing secret is
committed.

## 3. Create the database and run

```bash
dotnet tool restore
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web
```

Open `https://localhost:5001/setup`, enter the setup token and create the first administrator
(FR-002). Afterwards `/setup` returns 404.

## 4. Validate each user story manually

The steps mirror the acceptance scenarios in [spec.md](./spec.md); the automated tests in step 5 cover
the same scenarios.

### US1: Create a Kanban project and track tasks on its board

1. As the administrator, open `/admin/users` and add **amina** and **bilal**; note the temporary
   passwords. Sign out.
2. Open any page: you are sent to sign-in (scenario 1). Sign in as amina: you must choose a new
   password (scenario 2).
3. `/projects` → **Create project**: type "Website Revamp"; the key `WR` is suggested; change it to
   `WEB` and create. The board opens with To Do, In Progress and Done (scenario 3).
4. Try to create another project with key `WEB`: refused, input kept (scenario 4).
5. In To Do, type "Design the home page" in "What needs to be done?" and press Enter: `WEB-1` appears
   and the box is ready for the next title. Add four more tasks across the columns (scenario 5).
6. Drag a card to In Progress; drag a card above another; move a card with the keyboard (Tab to the
   card, open "Move to", choose Done) (scenarios 6–8).
7. Sign in as bilal in a second browser: the board looks identical. Move a card there, then drag the
   same card in amina's browser: amina is told the card changed (scenario 9).

### US2: Work on a task in the details drawer

1. Select `WEB-1`: the drawer opens beside the board (scenario 1). Copy the browser URL
   (`…/board?task=WEB-1`) and open it in bilal's browser: the same drawer opens.
2. Change the title, write a two-line description containing a link, set priority High (scenarios
   2–4). The card shows the new title and the High indicator.
3. Add three sub-tasks and use "mark done" on one: the card shows "1/3"; sub-tasks are not cards
   (scenarios 6, 7).
4. Change the status to Done while two sub-tasks are open: allowed, with a warning (scenario 5, FR-029).
5. Post two comments, edit one, delete the other; as bilal, confirm you cannot edit amina's comment
   (scenario 8).
6. Open the history: every change is listed with who, when, old and new values (scenario 9).
7. Edit the description in both browsers and save both: the second sees the conflict with their text
   kept (scenario 10).
8. As amina (the creator), delete `WEB-1`: the confirmation mentions its 3 sub-tasks; as the
   administrator, restore it from `/projects/WEB/deleted` (scenario 11).

### US3: Customize the board's columns

1. As amina (owner), open `/projects/WEB/settings`: add "In Review" (type "in progress") after In
   Progress; rename To Do to "Backlog"; move a column; set a limit of 3 on In Progress (scenarios
   1–3).
2. Put a fourth card in In Progress: the column shows 4 of 3 but the move succeeds (scenario 4).
3. Delete "In Review" while it holds two tasks, choosing Done as the destination: both tasks move and
   their histories note "column deleted" (scenario 5).
4. Try deleting the only "done" column or retyping the only "to do" column: refused (scenario 6).
5. Try adding a column named "backlog": refused as a duplicate (scenario 7).
6. As bilal (not the owner), confirm the settings are not offered and a direct visit is refused, while
   cards can still be moved (scenario 8).

## 5. Run the automated tests

```bash
dotnet test                                                   # unit, integration, component, architecture
dotnet test --filter "FullyQualifiedName~US3_"                 # one story's acceptance tests
pwsh tests/Upms.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install   # once: browsers for Playwright
dotnet test tests/Upms.E2E.Tests                               # journeys + axe accessibility scans
```

Integration and end-to-end tests start their own SQL Server container through Testcontainers; only a
running Docker engine is needed.

**Expected**: all green; test names start with the scenario they prove (for example
`US1_AS9_StaleMoveIsRejected`); zero WCAG 2.2 AA violations (SC-008).

## 6. Quality gates (same as CI)

```bash
dotnet format --verify-no-changes
dotnet build -c Release       # warnings are errors; NuGet audit fails on high or critical advisories
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
```

## 7. Performance check (SC-002)

```bash
dotnet run --project tools/Upms.Seed -- --users 2000 --projects 200 --tasks 500000
dotnet test tests/Upms.Performance.Tests --filter "Category=Performance"
```

**Expected**: with 300 simulated concurrent users, p95 ≤ 1 second for project list and board loads (up
to 500 visible cards), inline creation, card move, drawer open and saving a task edit.

## 8. Restore drill (SC-009, SC-010)

Follow `docs/operations/backup-restore.md` (created during implementation): restore the latest backups
to a fresh SQL Server, point a new app instance at it, and confirm that no more than 1 hour of changes
is missing and the restore took under 4 hours. Do this once before the pilot.
