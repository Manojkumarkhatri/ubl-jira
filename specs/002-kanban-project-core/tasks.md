---

description: "Task list for Phase 1: Core Kanban Project (specs/002-kanban-project-core)"
---

# Tasks: Core Kanban Project (Phase 1)

**Input**: Design documents from `specs/002-kanban-project-core/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable) requires tests for domain
rules and application services to be written first and to fail before implementation, and every
acceptance scenario to be automated. Each story phase therefore starts with its tests. Test method
names begin with the scenario they prove (for example `US1_AS5_InlineCreateAddsCardAtBottom`).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): `src/Upms.Domain/`, `src/Upms.Application/`, `src/Upms.Infrastructure/`,
`src/Upms.Web/`, `tests/Upms.*.Tests/`, `tools/`, `docs/`. Inside each project, code is foldered by
module: Identity, Projects, Work. Contracts referenced below live in [contracts/](./contracts/).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create `Upms.slnx` and the projects from plan.md: `src/Upms.Domain`, `src/Upms.Application`, `src/Upms.Infrastructure` (class libraries, `net10.0`); `src/Upms.Web` (Blazor Web App with Individual Accounts and Interactive Server applied globally, e.g. `dotnet new blazor --auth Individual --interactivity Server --all-interactive`); `tools/Upms.Seed` (console); xUnit v3 test projects `tests/Upms.Domain.Tests`, `tests/Upms.Application.Tests`, `tests/Upms.Web.Tests`, `tests/Upms.E2E.Tests`, `tests/Upms.Architecture.Tests`, `tests/Upms.Performance.Tests`. References: Web → Infrastructure → Application → Domain; each test project references the layer it tests
- [X] T002 [P] Pin the .NET 10 SDK in `global.json` (`rollForward: latestFeature`)
- [X] T003 [P] Create `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `NuGetAudit=true`, `NuGetAuditMode=all`, and `WarningsAsErrors` including `NU1903;NU1904` (high and critical advisories fail the build)
- [X] T004 [P] Create `Directory.Packages.props` (central package management) with only the Phase 1 packages from plan.md: EF Core 10 SqlServer and Design, Identity EF Core, DataProtection EF Core, OpenTelemetry (Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.SqlClient, Exporter.OpenTelemetryProtocol), EF Core health checks; test packages `xunit.v3`, `bunit`, `Testcontainers.MsSql`, `Respawn`, `Microsoft.Playwright`, `Deque.AxeCore.Playwright`, `TngTech.ArchUnitNET.xUnit`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.AspNetCore.Mvc.Testing` (no search, email, scanner or rich-text packages)
- [X] T005 [P] Add `.editorconfig` with the C# style and naming rules enforced by `dotnet format`
- [X] T006 [P] Add `.config/dotnet-tools.json` with `dotnet-ef` matching EF Core 10
- [X] T007 [P] Create `docker-compose.yml` with one `sqlserver` service (`mcr.microsoft.com/mssql/server:2022-latest`, port 1433, health check) (quickstart step 1)
- [X] T008 [P] Add a .NET `.gitignore` (`bin/`, `obj/`, `TestResults/`, `*.user`, Playwright traces)
- [X] T009 [P] Create `.github/workflows/ci.yml`: restore; `dotnet format --verify-no-changes`; `dotnet build -c Release`; `dotnet test` with Docker available for Testcontainers; `dotnet ef migrations has-pending-model-changes`; a Playwright job that installs browsers; test-result upload
- [X] T010 [P] Create the production `Dockerfile` for `src/Upms.Web`: multi-stage SDK build, `aspnet:10.0` runtime, non-root user, no secrets in the image (research R25)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented:
persistence, sign-in and minimal account management, the single authorization point, security
middleware, observability, the layout shell, and test fixtures.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for the foundation (write first, must fail)

- [X] T011 [P] Write `tests/Upms.Architecture.Tests/ModuleBoundaryTests.cs`: Domain references neither EF Core nor ASP.NET Core; the modules Identity, Projects and Work use each other only through their `*.Contracts` namespaces; Web references Infrastructure types only from `Program.cs` (research R4)
- [X] T012 [P] Write `tests/Upms.Application.Tests/Identity/SetupServiceTests.cs`: setup is open only while no active Administrator exists and `SetupCompletedAt` is null; a wrong token returns `InvalidSetupToken`; after success every call returns `SetupClosed` and a `SetupCompleted` audit event exists (FR-002)
- [X] T013 [P] Write `tests/Upms.Application.Tests/Identity/SignInPolicyTests.cs`: passwords shorter than 12 characters or containing the username are rejected; 5 consecutive failures lock the account for 15 minutes; deactivated users cannot sign in; `SignInSucceeded`, `SignInFailed` and `LockedOut` audit events are written without passwords (FR-001, FR-005, FR-010)
- [X] T014 [P] Write `tests/Upms.Application.Tests/Identity/UserAdminServiceTests.cs`: adding a user returns a 16-character temporary password once and sets `MustChangePassword = true`; `DuplicateUserName` and `DuplicateEmail`; the list is searchable and paged (50 per page); reset password issues a new temporary password; deactivate and reactivate work and rotate the security stamp; deactivating the last active Administrator returns `LastAdministrator`; non-administrators get `Forbidden`; each action writes its audit event (FR-003, FR-004, FR-010)
- [X] T015 [P] Write `tests/Upms.Application.Tests/Projects/ProjectAccessTests.cs` for `IProjectAccess` with the Phase 1 rules of research R7: any active user has `View` and `Contribute`; the item's creator has `DeleteOwnWorkItem`; the project owner and Administrators have `Manage`; only Administrators have `Restore`; Administrators have full rights in every project; unknown projects return `NotFound` (FR-008, FR-009, FR-015)
- [X] T016 [P] Write `tests/Upms.Application.Tests/Identity/AccountServiceTests.cs`: display name "required, 1–100 characters"; time zone "valid IANA ID; null = organization default"; password change enforces the policy and writes `PasswordChanged`; `IUserDirectory` returns display names for any user IDs, deactivated users included (FR-007, edge case "Deactivated users")
- [X] T017 [P] Write `tests/Upms.Web.Tests/Security/HostSecurityTests.cs` (WebApplicationFactory): anonymous requests are redirected to `/Account/Login` except sign-in, setup and health; a user with `MustChangePassword` is redirected to `/Account/ChangePassword`; the headers in contracts/http-endpoints.md are present; cross-origin `/_blazor` requests are rejected; the login rate limit (10 per minute per IP) applies (FR-001, FR-003, research R9)
- [X] T018 [P] Write `tests/Upms.Web.Tests/Security/IdleSessionWarningTests.cs` (bUnit): the warning appears 2 minutes before the 30-minute idle timeout; "Stay signed in" calls `POST /account/keepalive`; at expiry the user is sent to sign-in (FR-006)

### Implementation for the foundation

- [X] T019 [P] Implement result types in `src/Upms.Application/Common/Results/` (`ErrorKind`, `AppError`, `Result`, `Result<T>`) and the rule-violation code constants listed in contracts/application-services.md
- [X] T020 [P] Define `ICurrentUser` (UserId, IsAdministrator, IsActive) and `IAppDbContext` in `src/Upms.Application/Common/`
- [X] T021 [P] Create `src/Upms.Domain/Identity/OrganizationRole.cs` (`Administrator`, `User`; FR-008) and `src/Upms.Domain/Identity/AuditEventType.cs` (`SetupCompleted`, `SignInSucceeded`, `SignInFailed`, `LockedOut`, `PasswordChanged`, `PasswordReset`, `UserCreated`, `UserDeactivated`, `UserReactivated`)
- [X] T022 [P] Create `User` in `src/Upms.Domain/Identity/User.cs` extending `IdentityUser<Guid>`: UserName "3–64 letters, digits, `.`, `-`, `_`"; Email "required, unique, valid email"; `DisplayName` "nvarchar(100), required, 1–100 characters"; `TimeZoneId` "varchar(64) null, valid IANA ID; null = organization default"; `OrganizationRole`; `IsActive` "default 1"; `DeactivatedAt`; `MustChangePassword`; `CreatedAt`; `LastSignInAt`
- [X] T023 [P] Create `AuditEvent` and `OrganizationSettings` in `src/Upms.Domain/Identity/` per data-model.md (`Target` "nvarchar(200)", `Details` "nvarchar(2000)" JSON, `SourceIp` "varchar(45) null"; `DefaultTimeZoneId` "seeded `UTC`"; `IdleTimeoutMinutes` "seeded 30"; `SetupCompletedAt`; `RowVersion`)
- [X] T024 [P] Create `Project`, `ProjectStatus` and `StatusCategory` (`ToDo`, `InProgress`, `Done`) in `src/Upms.Domain/Projects/` with the data-model.md fields: `Key` "varchar(10), required, unique, `^[A-Z][A-Z0-9]{1,9}$`, immutable"; `Name` "nvarchar(80), required, 1–80 characters" with `NormalizedName` "upper-invariant; unique"; `Description` "nvarchar(2000) null"; `OwnerId`; `NextItemNumber` "starts at 1"; `BoardVersion` "starts at 1"; `RowVersion`; statuses with `Name` "1–30 characters", `NormalizedName`, `Category`, `Position` "0-based", `WipLimit` "1–99 when set"; a constructor (key, name, description, owner, creation time) that test builders use until `Project.Create` adds validation and the default columns in T065
- [X] T025 Implement `AppDbContext` in `src/Upms.Infrastructure/Persistence/AppDbContext.cs`: `IdentityDbContext<User, IdentityRole<Guid>, Guid>`, implements `IAppDbContext`, stores data protection keys, stores enums as varchar names and timestamps as UTC `datetimeoffset`
- [X] T026 Add EF configurations in `src/Upms.Infrastructure/Persistence/Configurations/Identity/` and `.../Projects/`: AuditEvent indexes `(OccurredAt DESC)` and `(SubjectUserId, OccurredAt)`; OrganizationSettings seed row `Id = 1`; Project unique `Key` and unique `NormalizedName` with rowversion; ProjectStatus unique `(ProjectId, NormalizedName)`
- [X] T027 Create migration `InitialIdentityAndProjects` in `src/Upms.Infrastructure/Persistence/Migrations/`, including the `INSTEAD OF UPDATE, DELETE` trigger that makes `AuditEvents` append-only
- [X] T028 Build the integration-test fixture in `tests/Upms.Application.Tests/Fixtures/`: `SqlServerFixture.cs` (Testcontainers `mcr.microsoft.com/mssql/server:2022-latest`, migrations applied once, Respawn reset between tests keeping `__EFMigrationsHistory` and `OrganizationSettings`, then resetting `OrganizationSettings.SetupCompletedAt` to null), `TestCurrentUser.cs`, `ServiceHarness.cs` (Application + Infrastructure DI with `FakeTimeProvider`), and builders for users and projects (projects through the T024 constructor, switched to `Project.Create` in T065; US1 adds a work-item builder with T066)
- [X] T029 [P] Implement `IAuditLog` (contract `src/Upms.Application/Identity/Contracts/IAuditLog.cs`, implementation `src/Upms.Infrastructure/Identity/AuditLog.cs`) recording actor, subject, target, JSON details and source IP, never passwords or tokens
- [X] T030 Configure Identity in `src/Upms.Infrastructure/Identity/IdentitySetup.cs`: `RequiredLength = 12`, no character-class rules, `RequireUniqueEmail`, lockout `MaxFailedAccessAttempts = 5` and `DefaultLockoutTimeSpan = 15 minutes` with `AllowedForNewUsers = true`; authentication cookie HttpOnly, Secure, SameSite=Lax, sliding expiration
- [X] T031 [P] Implement `UsernamePasswordValidator` in `src/Upms.Infrastructure/Identity/UsernamePasswordValidator.cs` (rejects passwords containing the username, case-insensitive)
- [X] T032 [P] Implement `TemporaryPasswordGenerator` in `src/Upms.Infrastructure/Identity/TemporaryPasswordGenerator.cs` (16 characters from a cryptographic random source; satisfies the password policy)
- [X] T033 Implement `AuditingSignInManager` in `src/Upms.Infrastructure/Identity/AuditingSignInManager.cs`: refuses deactivated users, updates `LastSignInAt`, writes sign-in success, failure and lockout audit events (make T013 pass)
- [X] T034 Implement `IProjectAccess` (contract `src/Upms.Application/Projects/Contracts/IProjectAccess.cs` with `ProjectRight`: `View`, `Contribute`, `DeleteOwnWorkItem`, `Manage`, `Restore`) in `src/Upms.Application/Projects/ProjectAccess.cs` with the Phase 1 rules, reading role and ownership from the database on every call (make T015 pass)
- [X] T035 Implement `SetupService` in `src/Upms.Application/Identity/SetupService.cs` with the `Setup:Token` check and `SetupCompletedAt` closing (make T012 pass)
- [X] T036 Implement `UserAdminService` (`ListUsersAsync` with search and 50 per page, `AddUserAsync`, `ResetPasswordAsync`, `DeactivateAsync`, `ReactivateAsync`) in `src/Upms.Application/Identity/UserAdminService.cs` (make T014 pass)
- [X] T037 Implement `AccountService` (`GetProfileAsync`, `UpdateProfileAsync(displayName, timeZoneId)`, `ChangePasswordAsync`) in `src/Upms.Application/Identity/AccountService.cs` a read-only `OrganizationSettingsReader` in `src/Upms.Application/Identity/OrganizationSettingsReader.cs`, and `IUserDirectory` (contract `src/Upms.Application/Identity/Contracts/IUserDirectory.cs`) in `src/Upms.Application/Identity/UserDirectory.cs` (make T016 pass)
- [X] T038 Implement `CurrentUser` in `src/Upms.Web/Security/CurrentUser.cs` (from the authentication state; loads the active flag and role once per scope) and `UserRevalidatingAuthenticationStateProvider` in `src/Upms.Web/Security/UserRevalidatingAuthenticationStateProvider.cs` (revalidates the security stamp and `IsActive` every minute)
- [X] T039 Wire the host in `src/Upms.Web/Program.cs`: Interactive Server components; Identity; fallback authorization policy requiring an authenticated, active user; anti-forgery; data protection keys in the database, protected with a certificate when `DataProtection:CertificatePath` is set; `TimeProvider.System`; HTTPS redirection and HSTS
- [X] T040 Adapt the Identity pages in `src/Upms.Web/Components/Account/` (static SSR with `[ExcludeFromInteractiveRouting]`): keep Login, Logout and ChangePassword; delete registration, external-login, two-factor and passkey pages; add `Setup.razor` at `/setup` returning 404 once setup is closed
- [X] T041 Implement the forced password change: `src/Upms.Web/Security/MustChangePasswordMiddleware.cs` for HTTP requests and a guard in `src/Upms.Web/Components/Routes.razor` for interactive navigation (FR-003)
- [X] T042 Implement the idle timeout (FR-006): `src/Upms.Web/Security/IdleCircuitHandler.cs` (tracks inbound circuit activity; signs out after `IdleTimeoutMinutes`), `src/Upms.Web/wwwroot/js/idle-monitor.js`, `src/Upms.Web/Components/Shared/IdleSessionWarning.razor` (warning 2 minutes before, "Stay signed in"), `src/Upms.Web/Endpoints/KeepAliveEndpoint.cs` (`POST /account/keepalive` → 204) (make T018 pass)
- [X] T043 [P] Implement hardening in `src/Upms.Web/Security/`: `SecurityHeadersMiddleware.cs` (HSTS, CSP `default-src 'self'`, nosniff, `X-Frame-Options: DENY`, Referrer-Policy), `BlazorOriginCheckMiddleware.cs` (rejects `/_blazor` requests whose Origin differs from `App:PublicBaseUrl`), `RateLimitingSetup.cs` (login 10/min per IP, setup 5/min per IP, keepalive 6/min per user) (make T017 pass together with T039 and T041)
- [X] T044 [P] Implement `src/Upms.Web/Observability/ObservabilitySetup.cs` (JSON console logging with the trace ID on every entry as the correlation ID; OpenTelemetry traces and metrics for ASP.NET Core and EF Core, exported through OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; no task text or credentials in logs) and `src/Upms.Web/Endpoints/HealthEndpoints.cs` (`/health/live`, `/health/ready` with the database) (research R23)
- [X] T045 Build the layout shell in `src/Upms.Web/Components/Layout/MainLayout.razor` and `src/Upms.Web/Components/Layout/Header.razor`: product name, "Projects" link, user menu (profile, sign out), skip link, landmarks, a polite live region for announcements, and responsive styles down to 360 px in `src/Upms.Web/wwwroot/app.css`
- [X] T046 [P] Implement `src/Upms.Application/Common/TimeZoneResolver.cs` (the user's time zone, else the organization default) and `src/Upms.Web/Components/Shared/LocalTime.razor` (FR-043)
- [X] T047 [P] Create `src/Upms.Web/Components/Pages/Profile/Profile.razor` at `/account/profile` (display name, time zone, link to change password) (FR-007)
- [X] T048 [P] Create `src/Upms.Web/Components/Pages/Admin/Users.razor` at `/admin/users`: list and search accounts, 50 per page; add (temporary password shown once, with a copy button); reset password; deactivate and reactivate (FR-003, FR-004)
- [X] T049 [P] Create `tests/Upms.Web.Tests/BunitTestBase.cs` (fake authentication state, fake application services, JS interop)
- [X] T050 Build the end-to-end fixture in `tests/Upms.E2E.Tests/Fixtures/`: `AppFixture.cs` (Testcontainers SQL Server; starts the app on Kestrel; completes `/setup`; helpers to add users), `PlaywrightFixture.cs`, `AxeAssertions.cs` (fails on any WCAG 2.2 AA violation)

**Checkpoint**: Foundation ready. The app starts, the first administrator can be created, accounts can
be added, sign-in with lockout and the idle timeout work, and all foundation tests pass.

---

## Phase 3: User Story 1 - Create a Kanban project and track tasks on its board (Priority: P1) 🎯 MVP

**Goal**: Signed-in users create a project (name, suggested key, description), land on a board with To
Do, In Progress and Done, add tasks inline with "What needs to be done?", and move and reorder cards by
drag-and-drop or keyboard.

**Independent Test**: Sign in, create "Website Revamp" with key `WEB`, add five tasks inline across the
columns, drag two cards, reorder a column, move one card with the keyboard; after reloading the board is
unchanged, and another signed-in user sees the same board (spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T051 [P] [US1] Write `tests/Upms.Domain.Tests/Common/RankTests.cs`: keys generated between, before and after neighbours sort ordinally; 1,000 inserts into the same gap stay ordered; keys longer than 48 characters report `NeedsRebalance` (research R14)
- [X] T052 [P] [US1] Write `tests/Upms.Domain.Tests/Projects/ProjectCreationTests.cs`: key must match "`^[A-Z][A-Z0-9]{1,9}$`" and never changes; name "1–80 characters"; description at most 2,000 characters; `Project.Create` seeds "To Do (`ToDo`, 0), In Progress (`InProgress`, 1), Done (`Done`, 2)" and records the creator as owner (FR-011, FR-012, FR-016)
- [X] T053 [P] [US1] Write `tests/Upms.Domain.Tests/Projects/ProjectKeySuggesterTests.cs`: "Website Revamp" → `WR`; a single word → its first letters; leading digits dropped; a taken key gets a numeric suffix; results always match the key pattern (FR-011, US1_AS3)
- [X] T054 [P] [US1] Write `tests/Upms.Domain.Tests/Work/WorkItemCreationAndMoveTests.cs`: title "required, trimmed, 1–255 characters"; priority "default `Medium`"; type `Task`; key `{Project.Key}-{Number}`; moving into a `Done` status sets `ResolvedAt` and leaving clears it; `Created` and `Status` changes are appended to history with old and new values; a reorder within the same column appends a `Rank` change with the old and new 1-based positions and a note such as "moved above WEB-3" (FR-024, FR-025, FR-027, FR-031, constitution IV)
- [X] T055 [P] [US1] Write `tests/Upms.Application.Tests/Projects/ProjectServiceTests.cs`: `US1_AS3` creating a project opens a board with three columns and the creator as owner; `US1_AS4` duplicate key → `DuplicateProjectKey` and duplicate name → `DuplicateProjectName`; invalid key → `InvalidProjectKey`; the list shows name, key, owner and open task count (sub-tasks included), sorted by name and paged 50 per page; details can be edited by the owner and Administrators only (FR-011–FR-014)
- [X] T056 [P] [US1] Write `tests/Upms.Application.Tests/Work/BoardQueryTests.cs`: columns come in position order with cards in rank order and card counts; `US1_AS10` done columns hold only tasks resolved in the last 14 days unless `showAllDone`; every user sees the same order (FR-017, FR-020, FR-021)
- [X] T057 [P] [US1] Write `tests/Upms.Application.Tests/Work/InlineCreateTests.cs`: `US1_AS5` creates `WEB-1` at the bottom of the chosen column; empty or whitespace titles create nothing; titles over 255 characters are refused; 50 concurrent creations produce 50 unique consecutive keys (FR-018, FR-024)
- [X] T058 [P] [US1] Write `tests/Upms.Application.Tests/Work/MoveCardTests.cs`: `US1_AS6` a move changes the status and is recorded in history; `US1_AS7` the new order persists for everyone and the reorder is recorded as a `Rank` change; placements `Before`, `End` and `Top`; `US1_AS9` a stale version returns `Conflict` with the card's current state; unknown keys return `NotFound` (FR-019, FR-020, FR-022)
- [X] T059 [P] [US1] Write `tests/Upms.Application.Tests/Work/RankRebalanceTests.cs`: a project with rank keys over 48 characters is rebalanced without changing card order
- [X] T060 [P] [US1] Write bUnit tests `tests/Upms.Web.Tests/Board/InlineCreateTests.cs`, `tests/Upms.Web.Tests/Board/MoveToMenuTests.cs` and `tests/Upms.Web.Tests/Board/BoardPageTests.cs`: after Enter the box clears and keeps focus; `Esc` cancels; `US1_AS8` "Move to" offers every column plus top and bottom and issues the same call as a drop; cards show key, title and priority; a conflict result shows the conflict message
- [X] T061 [P] [US1] Write `tests/Upms.E2E.Tests/US1_KanbanBoardTests.cs`: `US1_AS1` anonymous visitors are sent to sign-in; `US1_AS2` a temporary password must be replaced; then the US1 Independent Test end to end (suggested key, inline tasks, drags, keyboard move, reload, second user), plus axe scans of the project list and board

### Implementation for User Story 1

- [X] T062 [P] [US1] Implement `Rank` in `src/Upms.Domain/Common/Rank.cs` (base-62 fractional indexing with `Between`, `Before`, `After`, and `NeedsRebalance` above 48 characters) (make T051 pass)
- [X] T063 [P] [US1] Create `src/Upms.Domain/Work/WorkItemType.cs` (`Task`, `Subtask`) and `src/Upms.Domain/Work/Priority.cs` (`Highest`, `High`, `Medium`, `Low`, `Lowest`)
- [X] T064 [P] [US1] Create `WorkItemChange` in `src/Upms.Domain/Work/WorkItemChange.cs` with `Field` "varchar(30)" taking the values `Created`, `Title`, `Description`, `Priority`, `Status`, `Rank`, `SubtaskAdded`, `CommentAdded`, `CommentEdited`, `CommentDeleted`, `Deleted`, `Restored`; `OldValue`/`NewValue` "nvarchar(max) null"; `Note` "nvarchar(200) null"
- [X] T065 [US1] Add `Project.Create(name, key, description, ownerId)` seeding the three default statuses, and `ProjectKeySuggester` in `src/Upms.Domain/Projects/ProjectKeySuggester.cs` (make T052 and T053 pass)
- [X] T066 [US1] Create `WorkItem` in `src/Upms.Domain/Work/WorkItem.cs` with the data-model.md fields (`Key` "varchar(21); unique; `{Project.Key}-{Number}`; immutable", `Title` "nvarchar(255)", `Description` "nvarchar(max) null, plain text, at most 32,000 characters", `Priority` "default `Medium`", `StatusId`, `Rank`, `ParentId`, `CreatedById`, `CreatedAt`, `UpdatedAt`, `ResolvedAt`, soft-delete fields, `RowVersion`), non-public setters, and the methods `CreateTask` and `MoveTo(status, rank)` that record history (`Status` for a column change, `Rank` for a reorder); add the work-item builder to the test fixtures (make T054 pass)
- [X] T067 [US1] Add EF configurations in `src/Upms.Infrastructure/Persistence/Configurations/Work/`: WorkItem unique `(ProjectId, Number)` and `(Key)`; index `(ProjectId, StatusId, Rank)` filtered `IsDeleted = 0 AND ParentId IS NULL`; `(ParentId)`; `(ProjectId, ResolvedAt)`; `Rank` as `varchar(64)` with `Latin1_General_BIN2`; rowversion; `IsDeleted` query filter; WorkItemChange index `(WorkItemId, OccurredAt)`
- [X] T068 [US1] Create migration `Work` in `src/Upms.Infrastructure/Persistence/Migrations/` with the `INSTEAD OF UPDATE, DELETE` trigger that makes `WorkItemChanges` append-only
- [X] T069 [P] [US1] Implement `WorkItemNumberAllocator` in `src/Upms.Infrastructure/Persistence/WorkItemNumberAllocator.cs` (`UPDATE Projects SET NextItemNumber += 1 OUTPUT deleted.NextItemNumber` inside the current transaction), interface `src/Upms.Application/Work/IWorkItemNumberAllocator.cs`
- [X] T070 [US1] Implement `ProjectService` (`ListAsync` paged, with owner names from `IUserDirectory` and open counts from `IWorkItemCounts`; `SuggestKeyAsync`, `CreateAsync`, `GetAsync`, `UpdateDetailsAsync`) in `src/Upms.Application/Projects/ProjectService.cs`, `IWorkItemCounts` (contract `src/Upms.Application/Work/Contracts/IWorkItemCounts.cs`) in `src/Upms.Application/Work/WorkItemCounts.cs`, and `IProjectWorkflow` (`StatusesAsync`, `FirstToDoStatusAsync`) in `src/Upms.Application/Projects/ProjectWorkflow.cs` (make T055 pass)
- [X] T071 [US1] Implement `BoardService` (`GetAsync(projectKey, showAllDone)`, `CreateInlineAsync`, `MoveCardAsync`) in `src/Upms.Application/Work/BoardService.cs` with `AsNoTracking` projections, permission checks first, and `Conflict` on stale versions (make T056, T057 and T058 pass)
- [X] T072 [P] [US1] Implement `RankRebalanceWorker` in `src/Upms.Infrastructure/Workers/RankRebalanceWorker.cs` (make T059 pass)
- [X] T073 [US1] Create `src/Upms.Web/Components/Pages/Projects/ProjectList.razor` (`/projects`, 50 per page with a pager, with empty state) and `src/Upms.Web/Components/Pages/Projects/CreateProjectDialog.razor` (name, key suggested as the name is typed and editable, description; errors keep the input)
- [X] T074 [US1] Create in `src/Upms.Web/Components/Pages/Board/`: `BoardPage.razor` (`/projects/{key}/board`, "Show all completed" toggle `?done=all`), `BoardColumn.razor` (card count, `Virtualize`, footer drop target), `TaskCard.razor` (drop target "before this card"), `InlineCreate.razor` ("What needs to be done?"), `MoveToMenu.razor`; native drag-and-drop per research R15; moves announced in the live region; conflict banner on stale moves (make T060 pass)
- [X] T075 [US1] Create `src/Upms.Web/Components/Pages/Settings/ProjectSettings.razor` at `/projects/{key}/settings` with a Details section (name, description) shown to the owner and Administrators only (FR-014)
- [X] T076 [US1] Add `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs` asserting `WorkItem` properties have non-public setters (research R17), then make T061 pass end to end

**Checkpoint**: User Story 1 is fully functional and testable on its own. This is the MVP.

---

## Phase 4: User Story 2 - Work on a task in the details drawer (Priority: P2)

**Goal**: A drawer beside the board to edit title, description, priority and status; add and complete
sub-tasks; comment; read the history; and delete (with administrator restore).

**Independent Test**: Open `WEB-1`, change its title, write a description, set priority High, add three
sub-tasks and complete one, post two comments and edit one; after reopening, everything is saved, the
card shows High and "1/3", and the history lists each change (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T077 [P] [US2] Write `tests/Upms.Domain.Tests/Work/WorkItemEditingTests.cs`: `Rename` ("1–255 characters", trimmed), `Describe` ("at most 32,000 characters"), `Prioritize` each record history with old and new values; `AddSubtask` requires a `Task` parent in the same project and refuses sub-tasks of sub-tasks (`SubtaskDepth`); moving to `Done` with open sub-tasks returns a warning listing them; `Delete` soft-deletes the sub-tasks and `Restore` restores them (FR-026, FR-028, FR-029, FR-033)
- [X] T078 [P] [US2] Write `tests/Upms.Web.Tests/Shared/PlainTextTests.cs`: line breaks are kept; `http` and `https` URLs become links with `rel="noopener noreferrer"` opening in a new tab; HTML and `<script>` render as text; `javascript:` never becomes a link (FR-025, FR-044, research R10)
- [X] T079 [P] [US2] Write `tests/Upms.Application.Tests/Work/WorkItemServiceTests.cs`: `US2_AS1` details include key, title, status, priority, description, sub-tasks, comments and history (first pages of 50, with paged "Show more"); `US2_AS2` title, `US2_AS3` description (line breaks kept), `US2_AS4` priority and `US2_AS5` status edits save and show on the board; `US2_AS10` a stale version returns `Conflict` with the current values; unknown keys return `NotFound` (FR-025–FR-027, FR-032)
- [X] T080 [P] [US2] Write `tests/Upms.Application.Tests/Work/SubtaskTests.cs`: `US2_AS6` three sub-tasks get their own keys and start in the leftmost "to do" column; "mark done" moves one to the leftmost "done" column and the parent card shows "1/3"; `US2_AS7` sub-tasks never appear as board cards (FR-017, FR-028, FR-040)
- [X] T081 [P] [US2] Write `tests/Upms.Application.Tests/Work/CommentServiceTests.cs`: `US2_AS8` comments list oldest first with author and time, 50 at a time, and authors' names stay after they are deactivated; authors edit (`EditedAt` set) and delete (placeholder) their own; editing or deleting someone else's returns `CommentNotOwned`; body "1–32,000 characters"; history records `CommentAdded`, `CommentEdited`, `CommentDeleted` (FR-030)
- [X] T082 [P] [US2] Write `tests/Upms.Application.Tests/Work/HistoryTests.cs`: `US2_AS9` history is complete and time-ordered with who, when, what, old and new values, 50 entries at a time; direct SQL `UPDATE` or `DELETE` on `WorkItemChanges` fails (FR-031, constitution IV)
- [X] T083 [P] [US2] Write `tests/Upms.Application.Tests/Work/DeleteRestoreTests.cs`: `US2_AS11` the creator, the owner and Administrators can delete, others get `Forbidden`; the preview reports the sub-task count; deleted tasks leave the board and return `NotFound`; Administrators list them (50 per page) and restore them; keys are never reused after deletion (FR-024, FR-033)
- [X] T084 [P] [US2] Write bUnit tests `tests/Upms.Web.Tests/Drawer/TaskDrawerTests.cs`, `SubtaskListTests.cs` and `CommentThreadTests.cs`: each field saves with a visible confirmation; validation errors keep the input; the conflict banner keeps the user's text; focus moves into the drawer on open, `Esc` closes it and focus returns to the card (research R20)
- [X] T085 [P] [US2] Write `tests/Upms.E2E.Tests/US2_TaskDrawerTests.cs`: the US2 Independent Test end to end, including opening `…/board?task=WEB-1` from a second browser (FR-023), plus an axe scan of the drawer

### Implementation for User Story 2

- [X] T086 [US2] Extend `WorkItem` in `src/Upms.Domain/Work/WorkItem.cs` with `Rename`, `Describe`, `Prioritize`, `AddSubtask`, `Delete` and `Restore` (cascading to sub-tasks), all recording history (make T077 pass)
- [X] T087 [P] [US2] Create `Comment` in `src/Upms.Domain/Work/Comment.cs` (`Body` "plain text, 1–32,000 characters", `CreatedAt`, `EditedAt` "non-null → shown as edited", `IsDeleted`, `DeletedAt`, `RowVersion`), its configuration in `src/Upms.Infrastructure/Persistence/Configurations/Work/CommentConfiguration.cs` (index `(WorkItemId, CreatedAt)`), and migration `Comments`
- [X] T088 [P] [US2] Implement `src/Upms.Web/Components/Shared/PlainText.razor` per research R10, never rendering user text as raw markup (make T078 pass)
- [X] T089 [US2] Implement `WorkItemService` (`GetAsync`, `UpdateAsync`, `AddSubtaskAsync`, `MarkSubtaskDoneAsync`, `PreviewDeleteAsync`, `DeleteAsync`, and the paged `ListDeletedAsync`, `RestoreAsync`, `ListSubtasksAsync`, `GetHistoryAsync`) in `src/Upms.Application/Work/WorkItemService.cs` (make T079, T080, T082 and T083 pass)
- [X] T090 [US2] Implement `CommentService` in `src/Upms.Application/Work/CommentService.cs` (make T081 pass)
- [X] T091 [US2] Add sub-task done/total counts to the card projection in `src/Upms.Application/Work/BoardService.cs` and show them on `TaskCard.razor` (FR-017)
- [X] T092 [US2] Create in `src/Upms.Web/Components/Pages/Drawer/`: `TaskDrawer.razor` (dialog semantics; opened from a card or `?task={KEY-N}`; title, status, priority and description editors; delete with preview), `SubtaskList.razor` (add by title, status per sub-task, "mark done", open with a link back to the parent), `CommentThread.razor`, `HistoryList.razor`; use `PlainText` for description and comments; sub-tasks, comments and history load 50 at a time with "Show more" (make T084 pass)
- [X] T093 [US2] Create `src/Upms.Web/Components/Pages/Settings/DeletedTasks.razor` at `/projects/{key}/deleted` (Administrators: list 50 per page and restore), then make T085 pass

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Customize the board's columns (Priority: P3)

**Goal**: The project owner and Administrators add, rename, reorder, type, limit and delete columns,
without ever losing a task.

**Independent Test**: As the owner, add "In Review" after In Progress, rename To Do to "Backlog", set a
limit of 3 on In Progress, move a column, then delete "In Review" sending its tasks to Done; no task is
lost, and a non-owner cannot change columns (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T094 [P] [US3] Write `tests/Upms.Domain.Tests/Projects/BoardColumnRulesTests.cs`: names "1–30 characters", unique ignoring case (`DuplicateColumnName`); at most 10 columns (`TooManyColumns`); positions stay consecutive after moves; limits "1–99" or none; a column's category changes only while empty (`ColumnNotEmpty`); the last `ToDo` and last `Done` columns cannot be deleted or retyped (`LastToDoColumn`, `LastDoneColumn`); deleting a non-empty column requires a destination (`DestinationRequired`); every change increments `BoardVersion` (FR-034–FR-039, FR-041)
- [X] T095 [P] [US3] Write `tests/Upms.Application.Tests/Projects/BoardColumnServiceTests.cs`: `US3_AS1` an added column appears in position for everyone; `US3_AS2` renaming keeps tasks in place; `US3_AS3` a new order persists; `US3_AS5` deleting a column moves all its work items (sub-tasks and deleted items included) to the destination, records "column deleted" in each history, and marks them completed when the destination is a "done" column; `US3_AS6` last-column protection; `US3_AS7` duplicate names refused; a stale `BoardVersion` returns `Conflict` with the current columns; `US3_AS8` users other than the owner and Administrators get `Forbidden` (FR-034–FR-041, SC-006)
- [X] T096 [P] [US3] Write `tests/Upms.Application.Tests/Work/WipLimitTests.cs`: `US3_AS4` a column over its limit is reported with count and limit, and moves and inline creations into it still succeed (FR-036)
- [X] T097 [P] [US3] Write bUnit tests `tests/Upms.Web.Tests/Settings/BoardColumnsEditorTests.cs`: add, rename, move left and right, type (disabled with a hint when the column is not empty), limit, delete with a destination picker; errors keep the input; the settings link is hidden from users who cannot manage the project
- [X] T098 [P] [US3] Write `tests/Upms.E2E.Tests/US3_ColumnCustomizationTests.cs`: the US3 Independent Test end to end, including a column reorder by drag and by keyboard, plus an axe scan of the column settings

### Implementation for User Story 3

- [X] T099 [US3] Add `AddColumn`, `RenameColumn`, `MoveColumn`, `SetWipLimit`, `ChangeColumnCategory` and `RemoveColumn` to `Project` in `src/Upms.Domain/Projects/Project.cs`, enforcing the board rules of data-model.md and incrementing `BoardVersion` (make T094 pass)
- [X] T100 [US3] Implement `IWorkItemStatusMover` (contract `src/Upms.Application/Work/Contracts/IWorkItemStatusMover.cs`; `CountInStatusAsync` including deleted items; `MoveAllAsync` recording a `Status` change with the note "column deleted") in `src/Upms.Application/Work/WorkItemStatusMover.cs`
- [X] T101 [US3] Implement `BoardColumnService` (`GetAsync`, `AddAsync`, `RenameAsync`, `MoveAsync`, `SetWipLimitAsync`, `ChangeCategoryAsync`, `DeleteAsync`) in `src/Upms.Application/Projects/BoardColumnService.cs`, moving work items and removing the column in one transaction (make T095 pass)
- [X] T102 [US3] Add the over-limit flag to the column projection in `src/Upms.Application/Work/BoardService.cs` and show "count of limit" with a visible over-limit marker in `BoardColumn.razor` (make T096 pass)
- [X] T103 [US3] Create `src/Upms.Web/Components/Pages/Settings/BoardColumnsEditor.razor` and add it as the Columns section of `ProjectSettings.razor`: column list with drag reorder and "Move left" and "Move right", add dialog (name, type, position), inline rename, type selector, limit input, delete with destination picker, conflict banner (make T097 pass), then make T098 pass

**Checkpoint**: All three user stories work independently.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T104 Write the data-driven `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs`, exercising every row of `specs/002-kanban-project-core/contracts/permissions.md` against the real services for each role (SC-007)
- [X] T105 [P] Build `tools/Upms.Seed/Program.cs` (`--users`, `--projects`, `--tasks`; realistic mix of statuses, sub-tasks, comments and history; bulk inserts)
- [X] T106 Write `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (category `Performance`): 300 concurrent simulated users on 500,000 seeded work items; assert p95 ≤ 1 second for project list load, board load (up to 500 visible cards), inline creation, card move, drawer open and saving a task edit (SC-002, constitution performance baseline); tune queries and indexes until it passes
- [X] T107 [P] Add `tests/Upms.E2E.Tests/ResponsiveAndKeyboardTests.cs`: the project list, board, drawer and column settings are usable at 360 px wide, and each story's main path completes with the keyboard only (FR-042, SC-008)
- [X] T108 [P] Write `docs/operations/deployment.md` (Linux container and Windows Server/IIS with WebSockets; configuration keys; data protection certificate; TLS; uptime monitoring and alerting on `/health/ready` for SC-009)
- [X] T109 [P] Write `docs/operations/backup-restore.md` (daily full, 6-hourly differential and 15-minute log backups; restore runbook; drill record template) for SC-009 and SC-010
- [X] T110 [P] Write `docs/pilot/phase1-pilot-plan.md` defining how SC-001, SC-003 and SC-011 are measured in the 2-week pilot (timed tasks and a survey)
- [X] T111 Review Phase 1 against OWASP ASVS Level 2, record it in `docs/security/phase1-asvs-review.md`, and fix findings (constitution III)
- [ ] T112 Do a manual screen-reader pass (NVDA with Edge) on the Phase 1 screens, record it in `docs/accessibility/phase1-screen-reader-review.md`, and fix findings (constitution VI)
- [X] T113 Update `README.md` with local setup (linking quickstart.md) and test commands
- [ ] T114 Run every step of `specs/002-kanban-project-core/quickstart.md`, including the restore drill, and record the results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 comes first; US2 and US3 both build on US1 and can then proceed in parallel
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Needs US1 (work items, board, cards).
- **User Story 3 (P3)**: Needs US1 (projects, statuses, board). Independent of US2; if US3 is built
  first, column deletion simply has no sub-tasks or deleted items to move yet.

```text
Setup → Foundational → US1 ─┬─ US2 ─┐
                            └─ US3 ─┴─ Polish
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Domain types before configurations and migrations; migrations before services
- Services before UI components; UI before the end-to-end test is expected to pass
- Story complete (checkpoint green) before moving to the next priority

### Parallel Opportunities

- Setup: every task marked [P] after T001
- Foundational: all eight test tasks together; then T019, T020, T021, T022, T023, T024 together
- Each story: all its test tasks together; the domain tasks marked [P] together
- After US1: one developer on US2 and another on US3

---

## Parallel Example: User Story 1

```bash
# Write all US1 tests together (they must fail first):
Task: "RankTests.cs", "ProjectCreationTests.cs", "ProjectKeySuggesterTests.cs", "WorkItemCreationAndMoveTests.cs"
Task: "ProjectServiceTests.cs", "BoardQueryTests.cs", "InlineCreateTests.cs", "MoveCardTests.cs"

# Then the independent domain pieces together:
Task: "Rank in src/Upms.Domain/Common/Rank.cs"
Task: "WorkItemType and Priority in src/Upms.Domain/Work/"
Task: "WorkItemChange in src/Upms.Domain/Work/WorkItemChange.cs"
```

## Parallel Example: User Story 3

```bash
# Tests together:
Task: "BoardColumnRulesTests.cs", "BoardColumnServiceTests.cs", "WipLimitTests.cs", "BoardColumnsEditorTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run the US1 tests and quickstart.md section 4 (US1)
5. Demo to the pilot team

### Incremental Delivery

1. Setup + Foundational → sign-in, accounts, security baseline
2. US1 → project and board (MVP) → demo
3. US2 → details drawer, sub-tasks, comments, history → demo, start the pilot
4. US3 → column customization → demo
5. Polish → permission matrix, performance, operations and pilot docs, reviews, restore drill

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Developer A: US1, then US2
3. Developer B: joins after US1 for US3, then Polish documentation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group; reference task IDs in pull requests (constitution, Traceability)
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence

## Implementation Notes

Decisions made while implementing, recorded so the documents match the code:

- **Test runner** (T001): xUnit v3 4.x runs on Microsoft Testing Platform; `global.json` opts
  `dotnet test` into it. xUnit's `--filter` still accepts the `FullyQualifiedName~US1_` syntax.
- **UI controls** (T004, research R3): native HTML controls and `<dialog>` replace Fluent UI.
- **Callers** (T020): `ICurrentUser` exposes only the caller's ID; `ICallerContext` reads the active flag
  and role from the database on every call.
- **Shared status category** (T024): `StatusCategory` lives in `Upms.Domain.Common`, the shared
  kernel, because the Projects and Work domains both use it.
- **Project versions** (T024, T026): `Project.DetailsVersion` and `Project.BoardVersion` are the
  concurrency tokens instead of a rowversion. Task creation increments `NextItemNumber` in the same row,
  so a rowversion would report false conflicts.
- **Identity tables** (T025): `IdentityUserContext<User, Guid>` is used, without role tables;
  organization roles are the `OrganizationRole` column.
- **Per-action scopes** (T039): UI-facing services run each call in their own DI scope
  (`Security/OperationScope.cs`), so every user action gets a fresh database context.
- **Idle timeout** (T042): user input is reported by `idle-monitor.js` through the keep-alive endpoint
  (at most once a minute). The circuit's own traffic, such as render acknowledgements, is not user
  activity. `IdleCircuitHandler` counts opening a page as activity.
- **Forced password change in circuits** (T041): the guard is `Shared/EnforcePasswordChange.razor` in the
  main layout rather than in `Routes.razor`.
- **Status code pages**: only GET and HEAD requests are re-executed to the not-found page, so POST
  errors keep their status code.
- **Number allocator** (T069): `IWorkItemNumberAllocator` is a Projects contract
  (`Upms.Application.Projects.Contracts`) because the counter lives in the `Projects` table.
- **Key suggestions** (T065): numbers are ignored when forming initials ("HR-2026 onboarding" → HO), and
  a single word gives its first three letters.
- **Long columns** (T074): each column scrolls on its own; cards are rendered without `Virtualize`,
  which suits the 500-visible-card envelope. The performance suite (T106) confirms it.
- **Interactivity marker**: the main layout sets `data-interactive="true"` once the Blazor circuit
  has taken over from prerendering, so browser tests act only on live pages.
- **History for comments and sub-tasks** (T089, T090): adding a sub-task or adding, editing or deleting a
  comment writes a history row on the task without updating the task row, so `UpdatedAt` and the row
  version change only when the task's own fields change and open drawers see no false conflicts. The
  drawer's history shows "added a comment" without the text: comment text stays in the audit rows but is
  not shown again, so a deleted comment stays deleted.
- **Open sub-task warning** (T077, T079): the warning when a task becomes done with open sub-tasks is
  built by `WorkItemService` from the database and tested at the service level, not in the domain.
- **Drawer behaviour** (T092): the drawer is a native modal `<dialog>`. Esc closes it through the
  browser's own dialog handling, except while the focused text field holds typed text
  (`data-escape-guard`), so a comment or description being written is not lost. Links to a sub-task or
  back to its parent change `?task=` and show the other item in the same dialog. Opening or closing the
  drawer does not reload the board.
- **Deleted tasks link** (T093): the board shows "Deleted tasks" to administrators from
  `BoardView.CanRestoreDeleted`, not from a policy check on each render.
- **Policy checks in circuits**: `UserStatusAuthorizationHandler` reads the user's status in a scope of its
  own, because checks from several components of one circuit can overlap and a shared `DbContext` allows
  one query at a time.
- **Rate limits** are configuration (`RateLimiting:SignInPerMinute`, `SetupPerMinute`,
  `KeepAlivePerMinute`) with the contract's values as defaults; only the browser-test host raises the
  sign-in limit, because every test signs in from the same address.
- **Column changes** (T099, T101): `Project` identifies columns by instance, so the rules are testable
  before anything is saved; `BoardColumnService` finds the column by ID and passes it in. A change that
  alters nothing (same name, same position, same limit) does not increment `BoardVersion`.
- **Deleting a column** (T100, T101): `IWorkItemStatusMover.MoveAllAsync` stages the moves in the shared
  unit of work, so one save moves the work items and removes the column in one transaction. Tasks join the
  end of the destination column in their order; sub-tasks keep their place under their parent. The mover
  counts statuses in one grouped query (`CountInStatusesAsync`), which the settings screen also uses.
- **Races with a column deletion**: if a card is moved or created in a column at the moment it is deleted,
  the save is refused by the database and the user gets a conflict ("the latest board is shown") or "column
  not found" instead of an error page.
- **Column settings screen** (T103): the Columns section of the project settings lists the columns from left
  to right with type, limit, "Move left", "Move right", Rename and Delete; rows can also be dragged. New
  columns default to the position before the first "done" column.
- **Permission matrix** (T104): `PermissionMatrixTests` reads the table from `contracts/permissions.md`, so a row
  without test operations fails; 32 operations × 5 roles run against the real services. A signed-in user who is
  refused a comment change gets `CommentNotOwned`; every other refusal is `Forbidden`.
- **Seed data** (T105): `tools/Upms.Seed` bulk-copies work items, history and comments with pre-assigned IDs
  (sub-tasks reference their parents in the same batch) and keeps foreign keys trusted. The largest project gets
  1,200 tasks, so its board shows about 500 cards.
- **Performance** (T106): the first 300-user run missed SC-002 (p95 2.3–5.4 s) because the project list counted
  open work by scanning `WorkItems`. Migration `PerformanceIndexes` adds `IX_WorkItems_Status_Live`,
  `IX_WorkItems_Subtasks_Live` (both filtered to live rows) and covering columns on `IX_WorkItems_Board`. After it,
  on one 4-vCPU host running both SQL Server and the load, p95 was 109–199 ms for all six actions at 300 users and
  499,000 work items. The suite measures service calls in-process (the Blazor rendering on top is small).
- **Screen width and keyboard** (T107): hidden labels inside the board no longer widen the page at 360 px (the
  board region is a containing block); focus follows a card after "Move to", and a column after "Move left/right".
- **Security review** (T111): see `docs/security/phase1-asvs-review.md`: 13 findings fixed (among them the
  `__Host-` session cookie, a 12-hour session limit, server-side rejection of cookies after sign-out, a
  common-password check, `no-store` pages and access-refusal logging); 4 items are open, 2 of them Level 2
  requirements that need a decision (multi-factor for administrators, managing one's own sessions).
- **Screen-reader review** (T112): the automated pre-check is done and its findings fixed
  (`docs/accessibility/phase1-screen-reader-review.md`); the manual NVDA pass needs a person and is still open.
