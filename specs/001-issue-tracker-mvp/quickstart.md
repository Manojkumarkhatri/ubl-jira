# Quickstart & Validation Guide: Issue Tracker MVP

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) |
**Contracts**: [contracts/](./contracts/)

How to run the app locally and prove each user story works end to end. The files and projects named
here are created during implementation (see `tasks.md`); this guide defines how they are validated.

## Prerequisites

- .NET 10 SDK (version pinned in `global.json`)
- Docker (Docker Desktop, or Docker Engine with Compose) for SQL Server, ClamAV, and Mailpit
- A current Microsoft Edge, Chrome, or Firefox

## 1. Start local dependencies

```bash
docker compose up -d        # sqlserver (with Full-Text Search), clamav, mailpit
docker compose ps           # all three healthy; ClamAV needs ~1–2 minutes to load signatures
```

| Service | Purpose | Local address |
|---------|---------|---------------|
| `sqlserver` | SQL Server with Full-Text Search (image built from `docker/sqlserver-fts/`) | `localhost,1433` |
| `clamav` | Malware scanner (`clamd`) | `localhost:3310` |
| `mailpit` | Catches outgoing email | SMTP `localhost:1025`, web UI `http://localhost:8025` |

## 2. Configure secrets (development only)

```bash
cd src/UblJira.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=UblJira;User Id=sa;Password=<local-sa-password>;TrustServerCertificate=True"
dotnet user-secrets set "Setup:Token" "<any-long-random-string>"
dotnet user-secrets set "App:PublicBaseUrl" "https://localhost:5001"
dotnet user-secrets set "Email:Smtp:Host" "localhost"
dotnet user-secrets set "Email:Smtp:Port" "1025"
dotnet user-secrets set "Scanner:ClamAv:Host" "localhost"
dotnet user-secrets set "Scanner:ClamAv:Port" "3310"
```

Production uses environment variables or the company secret store instead; nothing secret is
committed (constitution, Configuration).

## 3. Create the database and run

```bash
dotnet tool restore
dotnet ef database update --project src/UblJira.Infrastructure --startup-project src/UblJira.Web
dotnet run --project src/UblJira.Web
```

Open `https://localhost:5001/setup`, enter the setup token and create the first administrator
(FR-002). Afterwards `/setup` returns 404.

## 4. Validate each user story manually

Each walkthrough mirrors the acceptance scenarios in [spec.md](./spec.md). The automated tests in
step 5 cover the same scenarios; these steps are for demos and exploratory checks.

### US1 – Track issues in a project (P1)

1. As the administrator: `/admin/users` → create users **amina** and **bilal**; note the temporary
   passwords. `/projects/new` → project "Payments", key `PAY`, continuous flow; add both as Members.
2. Sign in as amina → forced to set a new password (scenario 1).
3. Create Story "Allow card refunds" → becomes `PAY-1`, To Do, Medium, reporter amina (scenario 3).
4. On `PAY-1`, add a Sub-task → `PAY-2` listed under `PAY-1` (scenario 4).
5. Move `PAY-1` to In Progress, assign to bilal (scenario 5); move to Done, then back → resolved time
   cleared (scenario 6).
6. Issue list: filter status = In Progress, assignee = me → only matching rows and a count
   (scenario 7).
7. Create user **carol** with no membership; as carol open `/browse/PAY-1` → "not found"
   (scenario 8).
8. Open `PAY-1` in two browsers as amina and bilal, edit the summary in both and save both → the
   second sees the conflict banner with their text kept (scenario 9).

**Expected**: all of the above hold, and every change appears in `PAY-1`'s history tab.

### US2 – Board (P2)

1. `/projects/PAY/board` → four columns with counts; no Epic cards (scenario 1).
2. Drag a card To Do → In Progress (scenario 2). With the keyboard: Tab to a card, press `M`, choose
   In Review (scenario 3).
3. Reorder two cards; reload in another browser → same order (scenario 4).
4. Turn on "Only my issues" → counts update (scenario 6).

### US3 – Collaborate (P3)

1. On `PAY-1`, comment with bold text, a list, and `@bilal` → bilal's bell shows 1 unread; Mailpit
   shows a "mentioned you" email with no comment text (scenarios 1, 2, 8; FR-057).
2. Attach a PNG screenshot → shows "being checked", then previewable (scenario 9).
3. Try a 15 MB file and an `.exe` → refused with a clear message (scenario 10).
4. Upload the EICAR anti-malware test file (from eicar.org; harmless) → stays blocked, the uploader
   is told, and the audit log shows `MalwareDetected` (scenario 11; SC-010).
5. Edit then delete the comment → "edited", then a "comment deleted" placeholder; the history lists
   both (scenarios 4–6).
6. Stop the mail container (`docker compose stop mailpit`), assign an issue → the action succeeds
   immediately; restart Mailpit → the email arrives (FR-058).

### US4 – Sprints (P4)

1. Create project "Mobile" (`MOB`, sprint-based) with 15 estimated issues.
2. `/projects/MOB/backlog` → reorder with drag and with "Move to top" (scenario 2); create a sprint,
   move 5 issues in, start it for two weeks → the board shows only those issues (scenario 3).
3. Try starting a second sprint → refused (scenario 4).
4. Mark 3 Done, complete the sprint, send the rest to the backlog → the sprint report shows
   committed/added/removed/completed/not completed (scenarios 5, 6).

### US5 – Find work (P5)

1. Type `PAY-1` in the header search → opens the issue (scenario 1).
2. Search "refund" → results from accessible projects only, newest first (scenarios 2, 5).
3. Add filters, save as "Unassigned PAY work", rerun from `/filters` (scenarios 3, 4).
4. Go to `/` → My Work shows assigned open issues and recent views (scenario 6).

### US6 – Manage access (P6)

1. Deactivate bilal → cannot sign in; still shown (deactivated) on his issues (scenario 1).
2. Reset amina's password → temporary password; forced change at next sign-in (scenario 2).
3. Make amina Project Admin of PAY; she adds carol as Viewer → carol can read but not edit
   (scenario 3).
4. Enter a wrong password 5 times → locked for 15 minutes; `/admin/audit` shows the failures and the
   lockout (scenario 5); filter by user and date (scenario 6).
5. Try to demote the only administrator → refused (scenario 7).

## 5. Run the automated tests

```bash
dotnet test                                             # all unit, integration, component, architecture tests
dotnet test --filter "FullyQualifiedName~US3_"           # one story's acceptance tests
pwsh tests/UblJira.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install   # once, installs browsers
dotnet test tests/UblJira.E2E.Tests                      # end-to-end journeys + axe accessibility scans
```

Integration and end-to-end tests start their own containers through Testcontainers, so step 1 is not
required for them, only a running Docker engine.

**Expected**: all green. Test names start with the scenario they prove (for example
`US1_AS8_NonMemberGetsNotFound`), and accessibility scans report zero WCAG 2.2 AA violations
(SC-008).

## 6. Quality gates (same as CI)

```bash
dotnet format --verify-no-changes
dotnet build -c Release                  # warnings are errors; NuGet audit fails on high/critical advisories
dotnet ef migrations has-pending-model-changes --project src/UblJira.Infrastructure --startup-project src/UblJira.Web
```

## 7. Performance validation (SC-002)

```bash
dotnet run --project tools/UblJira.Seed -- --users 2000 --projects 60 --issues 500000
dotnet test tests/UblJira.Performance.Tests --filter "Category=Performance"
```

**Expected**: with 300 simulated concurrent users, p95 ≤ 1 s for opening an issue, loading a board,
changing a status, and running a filtered search. Playwright timing checks on the same seeded data
confirm full page loads.

## 8. Reliability drill (SC-011, SC-012)

Follow `docs/operations/backup-restore.md` (created during implementation): restore the latest full,
differential, and log backups to a fresh SQL Server, point a new app instance at it, and confirm that
(a) no more than 1 hour of changes is missing and (b) the whole restore takes less than 4 hours.
Record the result; repeat before go-live and twice a year.
