---

description: "Task list for the Issue Tracker MVP (specs/001-issue-tracker-mvp)"
---

# Tasks: Issue Tracker MVP

**Input**: Design documents from `specs/001-issue-tracker-mvp/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable) requires tests for domain
rules and application services to be written first and to fail before implementation, and every
acceptance scenario to be automated. Each story phase therefore starts with its tests. Test method
names begin with the scenario they prove (for example `US1_AS3_StoryGetsFirstKey`).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): `src/UblJira.Domain/`, `src/UblJira.Application/`,
`src/UblJira.Infrastructure/`, `src/UblJira.Web/`, `tests/UblJira.*.Tests/`, `tools/`, `docs/`.
Inside each project, code is foldered by module: Identity, Projects, Issues, Collaboration, Planning,
Search. Contracts referenced below live in [contracts/](./contracts/).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create `UblJira.slnx` and the projects from plan.md: `src/UblJira.Domain`, `src/UblJira.Application`, `src/UblJira.Infrastructure` (class libraries, `net10.0`); `src/UblJira.Web` (Blazor Web App with Individual Accounts and the Interactive Server render mode applied globally, e.g. `dotnet new blazor --auth Individual --interactivity Server --all-interactive`); `tools/UblJira.Seed` (console); and xUnit v3 test projects `tests/UblJira.Domain.Tests`, `tests/UblJira.Application.Tests`, `tests/UblJira.Web.Tests`, `tests/UblJira.E2E.Tests`, `tests/UblJira.Architecture.Tests`, `tests/UblJira.Performance.Tests`. References: Web → Infrastructure → Application → Domain; each test project references the layer it tests
- [ ] T002 [P] Pin the .NET 10 SDK in `global.json` (`rollForward: latestFeature`)
- [ ] T003 [P] Create `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `NuGetAudit=true`, `NuGetAuditMode=all`, and `WarningsAsErrors` including `NU1903;NU1904` so high and critical advisories fail the build (constitution CI gates)
- [ ] T004 [P] Create `Directory.Packages.props` (central package management) with the packages named in plan.md Technical Context and Complexity Tracking: EF Core 10 SqlServer and Design, Identity EF Core, DataProtection EF Core, `Microsoft.FluentUI.AspNetCore.Components`, Markdig, HtmlSanitizer, MailKit, OpenTelemetry (Extensions.Hosting, Instrumentation.AspNetCore, Instrumentation.EntityFrameworkCore, Instrumentation.Http, Exporter.OpenTelemetryProtocol), EF Core health checks; test packages `xunit.v3`, `bunit`, `Testcontainers.MsSql`, `Testcontainers`, `Respawn`, `Microsoft.Playwright`, `Deque.AxeCore.Playwright`, `TngTech.ArchUnitNET.xUnit`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.AspNetCore.Mvc.Testing`
- [ ] T005 [P] Add `.editorconfig` with the C# style and naming rules that `dotnet format` enforces
- [ ] T006 [P] Add `.config/dotnet-tools.json` with `dotnet-ef` matching EF Core 10
- [ ] T007 [P] Create `docker/sqlserver-fts/Dockerfile`: `mcr.microsoft.com/mssql/server:2022-latest` plus the `mssql-server-fts` package (research R14)
- [ ] T008 Create `docker-compose.yml` with `sqlserver` (built from `docker/sqlserver-fts`, port 1433), `clamav` (`clamav/clamav`, port 3310) and `mailpit` (`axllent/mailpit`, SMTP 1025, UI 8025), each with a health check (quickstart step 1)
- [ ] T009 [P] Add a .NET `.gitignore` (`bin/`, `obj/`, `TestResults/`, `*.user`, Playwright traces)
- [ ] T010 [P] Create `.github/workflows/ci.yml`: restore; `dotnet format --verify-no-changes`; `dotnet build -c Release`; `dotnet test` (Docker available for Testcontainers); `dotnet ef migrations has-pending-model-changes`; a Playwright end-to-end job that installs browsers; test-result upload
- [ ] T011 [P] Create the production `Dockerfile` for `src/UblJira.Web`: multi-stage SDK build, `aspnet:10.0` runtime, non-root user, no secrets in the image (research R25)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented:
persistence, identity and sign-in, the authorization framework, security middleware, observability,
the layout shell, and the test fixtures.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for the foundation (write first, must fail)

- [ ] T012 [P] Write `tests/UblJira.Architecture.Tests/ModuleBoundaryTests.cs`: Domain references neither EF Core nor ASP.NET Core; a module (Identity, Projects, Issues, Collaboration, Planning, Search) uses another module only through its `*.Contracts` namespace; Web references Infrastructure types only from `Program.cs` (research R4)
- [ ] T013 [P] Write `tests/UblJira.Domain.Tests/Projects/ProjectTests.cs`: key must match "`^[A-Z][A-Z0-9]{1,9}$`" and is immutable; name "required, unique (case-insensitive)" and at most 80 chars; archive and restore; board style change refused while a sprint is Active; issue numbers start at 1 (FR-012, FR-013)
- [ ] T014 [P] Write `tests/UblJira.Application.Tests/Identity/SetupServiceTests.cs`: setup is open only while no active Administrator exists and `SetupCompletedAt` is null; a wrong token returns `InvalidSetupToken`; after success every call returns `SetupClosed` (FR-002)
- [ ] T015 [P] Write `tests/UblJira.Application.Tests/Identity/SignInPolicyTests.cs`: passwords shorter than 12 characters or containing the username are rejected; 5 consecutive failures lock the account for 15 minutes; inactive users cannot sign in; `SignInSucceeded`, `SignInFailed` and `LockedOut` audit events are written and never contain passwords (FR-001, FR-004, FR-011)
- [ ] T016 [P] Write `tests/UblJira.Application.Tests/Projects/ProjectAccessTests.cs` for `IProjectAccess`, covering every role (Administrator, ProjectAdmin, Member, Viewer, non-member, inactive user) × permission (View, Contribute, Administer): non-member → `NotFound`; insufficient role → `Forbidden`; archived project → `ProjectArchived` for Contribute and Administer; membership is read fresh on each call (contracts/permissions.md, FR-008–FR-010)
- [ ] T017 [P] Write `tests/UblJira.Application.Tests/Identity/AccountServiceTests.cs`: display name "required, 1–100 chars"; time zone "valid IANA ID; null = organization default"; password change enforces the policy and writes `PasswordChanged` (FR-006)
- [ ] T018 [P] Write `tests/UblJira.Web.Tests/Security/HostSecurityTests.cs` (WebApplicationFactory): anonymous requests redirect to `/Account/Login` except sign-in, setup and health; a user with `MustChangePassword` is redirected to `/Account/ChangePassword`; security headers from contracts/http-endpoints.md are present; cross-origin `/_blazor` requests are rejected; the login rate limit (10 per minute per IP) applies (FR-001, FR-003, constitution III)
- [ ] T019 [P] Write `tests/UblJira.Web.Tests/Security/IdleSessionWarningTests.cs` (bUnit): the warning appears 2 minutes before the configured idle timeout; "Stay signed in" calls `POST /account/keepalive`; at expiry the user is sent to sign-in (FR-005)

### Implementation for the foundation

- [ ] T020 [P] Implement result types in `src/UblJira.Application/Common/Results/` (`ErrorKind`, `AppError`, `Result`, `Result<T>`, `Page<T>` with a fixed page size of 50) and the rule-violation code constants listed in contracts/application-services.md
- [ ] T021 [P] Define `ICurrentUser` (UserId, IsAdministrator, IsActive) and `IAppDbContext` in `src/UblJira.Application/Common/`
- [ ] T022 [P] Create enums `src/UblJira.Domain/Identity/OrganizationRole.cs` (`Administrator`, `User`), `src/UblJira.Domain/Identity/AuditEventType.cs` (values exactly as listed in data-model.md), `src/UblJira.Domain/Projects/ProjectRole.cs` (`ProjectAdmin`, `Member`, `Viewer`), `src/UblJira.Domain/Projects/BoardStyle.cs` (`Kanban`, `Scrum`)
- [ ] T023 [P] Create `User` in `src/UblJira.Domain/Identity/User.cs` extending `IdentityUser<Guid>`: UserName "3–64 chars of letters, digits, `.`, `-`, `_`"; Email "required, unique, valid email"; `DisplayName` "nvarchar(100), required, 1–100 chars"; `TimeZoneId` "varchar(64) null, valid IANA ID; null = organization default"; `OrganizationRole`; `IsActive` "default 1"; `DeactivatedAt`; `MustChangePassword`; `EmailNotificationsEnabled` "default 1"; `CreatedAt`; `LastSignInAt`
- [ ] T024 [P] Create `AuditEvent` and `OrganizationSettings` in `src/UblJira.Domain/Identity/` per data-model.md (`Target` "nvarchar(200)", `Details` "nvarchar(2000)" JSON, `SourceIp` "varchar(45) null"; `IdleTimeoutMinutes` "5–480, default 30"; `SetupCompletedAt`; `RowVersion`)
- [ ] T025 Create `Project` and `ProjectMember` in `src/UblJira.Domain/Projects/`: `Key` "varchar(10), required, unique, `^[A-Z][A-Z0-9]{1,9}$`, immutable"; `Name` "nvarchar(80), required, unique (case-insensitive)"; `Description` "nvarchar(2000) null"; `BoardStyle`; `NextIssueNumber` "starts at 1"; `IsArchived`, `ArchivedAt`; `RowVersion`; each member holds exactly one `Role` (make T013 pass)
- [ ] T026 Implement `AppDbContext` in `src/UblJira.Infrastructure/Persistence/AppDbContext.cs`: `IdentityDbContext<User, IdentityRole<Guid>, Guid>`, implements `IAppDbContext`, stores data protection keys, stores enums as varchar names and timestamps as UTC `datetimeoffset`
- [ ] T027 Add EF configurations in `src/UblJira.Infrastructure/Persistence/Configurations/Identity/` and `.../Projects/`: AuditEvent indexes `(OccurredAt DESC)`, `(SubjectUserId, OccurredAt)`, `(ActorUserId, OccurredAt)`, `(EventType, OccurredAt)`; OrganizationSettings seed row `Id = 1` (`DefaultTimeZoneId` "UTC", `IdleTimeoutMinutes` 30); Project unique `Key` and unique `Name` with rowversion; ProjectMember PK `(ProjectId, UserId)` and index `(UserId)`
- [ ] T028 Create migration `InitialIdentityAndProjects` in `src/UblJira.Infrastructure/Persistence/Migrations/`, including the `INSTEAD OF UPDATE, DELETE` trigger that makes `AuditEvents` append-only (research R12)
- [ ] T029 Build the integration-test fixture in `tests/UblJira.Application.Tests/Fixtures/`: `SqlServerFixture.cs` (Testcontainers image built from `docker/sqlserver-fts`, migrations applied once, Respawn reset between tests that keeps `__EFMigrationsHistory` and `OrganizationSettings`), `TestCurrentUser.cs`, `ServiceHarness.cs` (Application + Infrastructure DI with `FakeTimeProvider`), and builders for users and projects
- [ ] T030 [P] Implement `IAuditLog` (contract `src/UblJira.Application/Identity/Contracts/IAuditLog.cs`, implementation `src/UblJira.Infrastructure/Identity/AuditLog.cs`) recording actor, subject, project, target, JSON details and source IP, never passwords or tokens
- [ ] T031 Configure Identity in `src/UblJira.Infrastructure/Identity/IdentitySetup.cs`: `RequiredLength = 12`, no character-class rules, `RequireUniqueEmail`, lockout `MaxFailedAccessAttempts = 5` and `DefaultLockoutTimeSpan = 15 minutes` with `AllowedForNewUsers = true`; authentication cookie HttpOnly, Secure, SameSite=Lax, sliding expiration
- [ ] T032 [P] Implement `UsernamePasswordValidator` in `src/UblJira.Infrastructure/Identity/UsernamePasswordValidator.cs` (rejects passwords containing the username, case-insensitive)
- [ ] T033 Implement `AuditingSignInManager` in `src/UblJira.Infrastructure/Identity/AuditingSignInManager.cs`: refuses inactive users, updates `LastSignInAt`, writes sign-in success, failure and lockout audit events (make T015 pass)
- [ ] T034 Implement `ProjectAccess` (`IProjectAccess`, contract in `src/UblJira.Application/Projects/Contracts/IProjectAccess.cs`) in `src/UblJira.Application/Projects/ProjectAccess.cs`, reading organization role and membership from the database on every call (make T016 pass)
- [ ] T035 Implement `SetupService` in `src/UblJira.Application/Identity/SetupService.cs` with the `Setup:Token` check and `SetupCompletedAt` closing (make T014 pass)
- [ ] T036 Implement `AccountService` (profile and password) in `src/UblJira.Application/Identity/AccountService.cs` and `OrganizationSettingsService.GetAsync` in `src/UblJira.Application/Identity/OrganizationSettingsService.cs` (make T017 pass)
- [ ] T037 Implement `CurrentUser` in `src/UblJira.Web/Security/CurrentUser.cs` (from the authentication state; loads active flag and organization role once per scope) and `UserRevalidatingAuthenticationStateProvider` in `src/UblJira.Web/Security/UserRevalidatingAuthenticationStateProvider.cs` (revalidates security stamp and `IsActive` every 1 minute)
- [ ] T038 Wire the host in `src/UblJira.Web/Program.cs`: Fluent UI; Interactive Server components; Identity; fallback authorization policy requiring an authenticated, active user; antiforgery; data protection keys in the database, protected with a certificate when `DataProtection:CertificatePath` is set; `TimeProvider.System`; HTTPS redirection and HSTS
- [ ] T039 Adapt the Identity pages in `src/UblJira.Web/Components/Account/` (static SSR with `[ExcludeFromInteractiveRouting]`): keep Login, Logout and ChangePassword; delete registration, external-login, two-factor and passkey pages (out of scope); add `Setup.razor` at `/setup` returning 404 once setup is closed
- [ ] T040 Implement the forced password change: `src/UblJira.Web/Security/MustChangePasswordMiddleware.cs` for HTTP requests and a guard in `src/UblJira.Web/Components/Routes.razor` for interactive navigation (FR-003)
- [ ] T041 Implement the idle timeout (FR-005): `src/UblJira.Web/Security/IdleCircuitHandler.cs` (tracks inbound circuit activity and signs out after `IdleTimeoutMinutes`), `src/UblJira.Web/wwwroot/js/idle-monitor.js`, `src/UblJira.Web/Components/Shared/IdleSessionWarning.razor` (warning 2 minutes before, "Stay signed in"), `src/UblJira.Web/Endpoints/KeepAliveEndpoint.cs` (`POST /account/keepalive` → 204) (make T019 pass)
- [ ] T042 [P] Implement hardening in `src/UblJira.Web/Security/`: `SecurityHeadersMiddleware.cs` (HSTS, CSP `default-src 'self'`, nosniff, `X-Frame-Options: DENY`, Referrer-Policy), `BlazorOriginCheckMiddleware.cs` (rejects `/_blazor` requests whose Origin differs from `App:PublicBaseUrl`), `RateLimitingSetup.cs` (login 10/min per IP, setup 5/min per IP, keepalive 6/min per user) per contracts/http-endpoints.md (make T018 pass together with T038, T040)
- [ ] T043 [P] Implement `src/UblJira.Web/Observability/ObservabilitySetup.cs` (JSON console logging; OpenTelemetry traces and metrics for ASP.NET Core, EF Core and HttpClient, exported via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; no issue text in logs) and `src/UblJira.Web/Observability/HealthEndpoints.cs` (`/health/live`; `/health/ready` with the database as critical) (research R23)
- [ ] T044 Build the layout shell in `src/UblJira.Web/Components/Layout/MainLayout.razor` and `src/UblJira.Web/Components/Layout/NavMenu.razor`: header with project switcher and user menu, skip link, landmark regions, a live region for announcements, and responsive styles down to 360 px in `src/UblJira.Web/wwwroot/app.css` (constitution VI)
- [ ] T045 [P] Implement `src/UblJira.Application/Common/TimeZoneResolver.cs` (user time zone, else the organization default) and `src/UblJira.Web/Components/Shared/LocalTime.razor` (FR-051)
- [ ] T046 [P] Create `src/UblJira.Web/Components/Pages/Profile/Profile.razor` at `/account/profile` (display name, time zone, link to change password) (FR-006)
- [ ] T047 [P] Create `tests/UblJira.Web.Tests/BunitTestBase.cs` (Fluent UI services, fake authentication state, fake application services)
- [ ] T048 Build the end-to-end fixture in `tests/UblJira.E2E.Tests/Fixtures/`: `AppFixture.cs` (Testcontainers SQL Server with full-text, ClamAV and Mailpit; starts the app on Kestrel; completes `/setup`), `PlaywrightFixture.cs`, `AxeAssertions.cs` (fails on any WCAG 2.2 AA violation), `MailpitClient.cs` (reads caught email through the Mailpit API)

**Checkpoint**: Foundation ready. The app starts, the first administrator can be created, sign-in with
lockout and the idle timeout work, and all foundation tests pass.

---

## Phase 3: User Story 1 - Track issues in a project (Priority: P1) 🎯 MVP

**Goal**: An administrator creates accounts and a project; members create issues of all five types
with unique keys, edit fields, assign, move them through the workflow, and filter the project's issue
list. Non-members cannot see the project.

**Independent Test**: From an empty system, the administrator creates two users and project `PAY`;
a member creates a story with a sub-task, assigns it and moves it to Done; the list and issue pages
show correct keys, fields and statuses; a non-member gets "not found" (spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [ ] T049 [P] [US1] Write `tests/UblJira.Domain.Tests/Issues/IssueWorkflowTests.cs`: any status → any other status; entering Done sets `ResolvedAt`; leaving Done clears it; moving a parent to Done with open sub-tasks succeeds with a warning that lists them (FR-021, FR-022)
- [ ] T050 [P] [US1] Write `tests/UblJira.Domain.Tests/Issues/IssueHierarchyTests.cs`: "Epic: ParentId must be null"; Story, Task and Bug parent optional and only "an Epic in the same project"; Sub-task parent required and "a Story, Task, or Bug in the same project"; type changes "allowed only among Story, Task, and Bug" (FR-020)
- [ ] T051 [P] [US1] Write `tests/UblJira.Domain.Tests/Issues/IssueFieldRulesTests.cs`: summary "required, trimmed, 1–255 chars"; description "≤ 32,000 chars"; estimate "0–999, at most one decimal place"; label names "1–50 chars; letters, digits, `-`, `_`, `.`; no spaces" and case-insensitive equality (FR-017, FR-019, FR-027)
- [ ] T052 [P] [US1] Write `tests/UblJira.Domain.Tests/Issues/IssueChangeRecordingTests.cs`: every mutating method appends `IssueChange` rows with actor, time, field and old and new display values, sharing one `ChangeSetId` per action (FR-037, SC-004)
- [ ] T053 [P] [US1] Write `tests/UblJira.Domain.Tests/Common/RankTests.cs`: keys generated between, before and after neighbours sort ordinally; 1,000 inserts into the same gap stay ordered; keys longer than 48 characters report `NeedsRebalance` (research R10)
- [ ] T054 [P] [US1] Write `tests/UblJira.Application.Tests/Identity/UserCreationTests.cs`: `US1_AS1` Administrator creates a user and receives a 16-character temporary password once, with `MustChangePassword = true` and a `UserCreated` audit event; `DuplicateUserName` and `DuplicateEmail`; non-administrator → `Forbidden` (FR-003)
- [ ] T055 [P] [US1] Write `tests/UblJira.Application.Tests/Projects/ProjectCreationTests.cs`: `US1_AS2` project PAY is listed for the administrator and its two members only; `DuplicateProjectKey`, `DuplicateProjectName`, invalid key; non-administrator → `Forbidden`; adding a member writes `ProjectMemberAdded`; editing name, description and board style (FR-012, FR-014, FR-016)
- [ ] T056 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/IssueCreationTests.cs`: `US1_AS3` a Story becomes `PAY-1`, To Do, Medium, reporter = creator; `US1_AS4` a Sub-task becomes `PAY-2` under `PAY-1`; 50 concurrent creations produce 50 unique consecutive keys; keys are never reused after deletion (FR-017, FR-018)
- [ ] T057 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/IssueEditingTests.cs`: `US1_AS5` status and assignee change; `US1_AS6` reopening clears the resolved time; `US1_AS9` a stale `expectedVersion` returns `Conflict` carrying the current values; the assignee must be an active contributor (`AssigneeNotContributor`); every edit writes history (FR-019, FR-023, FR-024)
- [ ] T058 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/ProjectIssueListTests.cs`: `US1_AS7` filters by type, status, priority, assignee (including "me" and "unassigned"), label and full-text words; sorting by every listed column; pages of 50 with the total count (FR-026)
- [ ] T059 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/IssueAccessTests.cs`: `US1_AS8` a non-member gets `NotFound` for get, list, edit and history; a Viewer gets `Forbidden` for every write (FR-010)
- [ ] T060 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/IssueDeletionTests.cs`: Project Admin deletes after a preview; sub-tasks are deleted with their parent; Epic children lose the Epic link, recorded in their history; deleted issues vanish from lists; an Administrator lists and restores them; a Member gets `Forbidden` (FR-025)
- [ ] T061 [P] [US1] Write `tests/UblJira.Application.Tests/Issues/HistoryAppendOnlyTests.cs`: direct SQL `UPDATE` or `DELETE` on `IssueChanges` fails (constitution IV)
- [ ] T062 [P] [US1] Write `tests/UblJira.Application.Tests/Text/MarkdownRendererTests.cs`: headings, bold, italics, lists, links and code render; script tags, event-handler attributes, `javascript:` links and raw HTML are removed; links get `rel="noopener noreferrer nofollow"` (FR-052)
- [ ] T063 [P] [US1] Write bUnit tests `tests/UblJira.Web.Tests/Issues/InlineFieldEditorTests.cs` and `tests/UblJira.Web.Tests/Issues/ConflictBannerTests.cs`: a saved change is confirmed visibly; a validation error keeps the typed input; the conflict banner shows the latest values and keeps the user's text (FR-023, FR-024)
- [ ] T064 [P] [US1] Write `tests/UblJira.E2E.Tests/US1_TrackIssuesTests.cs`: the US1 Independent Test end to end, plus axe scans of the project list, issue list and issue page

### Implementation for User Story 1

- [ ] T065 [P] [US1] Create enums in `src/UblJira.Domain/Issues/`: `IssueType` (`Epic`, `Story`, `Task`, `Bug`, `Subtask`), `IssueStatus` (`ToDo`, `InProgress`, `InReview`, `Done`), `Priority` (`Highest`, `High`, `Medium`, `Low`, `Lowest`)
- [ ] T066 [P] [US1] Implement `Rank` in `src/UblJira.Domain/Common/Rank.cs` (base-62 fractional indexing with `Between`, `Before`, `After`, and `NeedsRebalance` above 48 characters) (make T053 pass)
- [ ] T067 [P] [US1] Create `IssueChange` in `src/UblJira.Domain/Issues/IssueChange.cs` with `Field` "varchar(30)" taking exactly the values listed in data-model.md and `OldValue`/`NewValue` "nvarchar(max) null" display snapshots
- [ ] T068 [P] [US1] Create `Label` and `IssueLabel` in `src/UblJira.Domain/Issues/Label.cs` (`Name` "nvarchar(50)"; `NormalizedName` "upper-invariant; unique")
- [ ] T069 [US1] Create `Issue` in `src/UblJira.Domain/Issues/Issue.cs` with the data-model.md fields: `Key` "varchar(21); unique; `{Project.Key}-{Number}`; immutable", `Summary` "nvarchar(255)", `Description` "nvarchar(max) null, Markdown, ≤ 32,000 chars", `Priority` "default `Medium`", `Status` "default `ToDo`", `AssigneeId`, `ReporterId` "creator; not editable", `ParentId`, `SprintId` (no foreign key until the Planning migration), `Rank`, `DueDate` "date null", `Estimate` "decimal(4,1) null", `CreatedAt`, `UpdatedAt`, `ResolvedAt`, soft-delete fields, `RowVersion`; non-public setters; methods `Create`, `UpdateSummary`, `UpdateDescription`, `ChangeType`, `ChangePriority`, `SetLabels`, `SetDueDate`, `SetEstimate`, `SetParent`, `ChangeStatus`, `Assign`, `Rerank`, `MarkDeleted`, `Restore` that enforce the rules and record `IssueChange` (make T049, T050, T051, T052 pass)
- [ ] T070 [US1] Add EF configurations in `src/UblJira.Infrastructure/Persistence/Configurations/Issues/`: Issue unique `(ProjectId, Number)` and `(Key)`; indexes `(ProjectId, Status, Rank)`, `(ProjectId, SprintId, Rank)`, `(ProjectId, UpdatedAt DESC)`, `(AssigneeId, Status)` including `ProjectId`, `(ParentId)`, `(ProjectId, ResolvedAt)`; `Rank` as `varchar(64)` with `Latin1_General_BIN2`; rowversion; `IsDeleted` query filter; IssueChange index `(IssueId, OccurredAt)`; Label unique `NormalizedName`; IssueLabel PK `(IssueId, LabelId)` and index `(LabelId, IssueId)`
- [ ] T071 [US1] Create migration `Issues` in `src/UblJira.Infrastructure/Persistence/Migrations/` with the `IssueChanges` append-only trigger, a full-text catalog and the full-text index on `Issues(Summary, Description)` (make T061 pass)
- [ ] T072 [P] [US1] Implement `IssueNumberAllocator` in `src/UblJira.Infrastructure/Persistence/IssueNumberAllocator.cs` (`UPDATE Projects SET NextIssueNumber += 1 OUTPUT deleted.NextIssueNumber` inside the current transaction), interface `src/UblJira.Application/Issues/IIssueNumberAllocator.cs`
- [ ] T073 [P] [US1] Implement `TemporaryPasswordGenerator` in `src/UblJira.Infrastructure/Identity/TemporaryPasswordGenerator.cs` (16 characters from a cryptographic random source; satisfies the password policy)
- [ ] T074 [P] [US1] Implement `MarkdownRenderer` in `src/UblJira.Infrastructure/Text/MarkdownRenderer.cs` (Markdig with raw HTML disabled, then HtmlSanitizer allowlist; link schemes `http`, `https`, `mailto`) (make T062 pass)
- [ ] T075 [P] [US1] Implement `FullTextQuery` in `src/UblJira.Infrastructure/Search/FullTextQuery.cs` (turns user words into a safe `CONTAINS` condition with prefix terms and escaped quotes)
- [ ] T076 [US1] Implement `UserAdminService.CreateUserAsync` and `ListUsersAsync` in `src/UblJira.Application/Identity/UserAdminService.cs` (make T054 pass)
- [ ] T077 [US1] Implement `ProjectService` (`CreateAsync`, `ListAccessibleAsync`, `GetAsync`, `UpdateAsync` for name, description and board style) in `src/UblJira.Application/Projects/ProjectService.cs` and `ProjectMembershipService` (`ListMembersAsync`, `AddMemberAsync` with role, `SuggestAssigneesAsync` returning active contributors) in `src/UblJira.Application/Projects/ProjectMembershipService.cs` (make T055 pass)
- [ ] T078 [US1] Implement `src/UblJira.Application/Issues/IssueCommands.cs` (`CreateAsync`, `UpdateAsync`, `ChangeStatusAsync`, `AssignAsync`, `PreviewDeleteAsync`, `DeleteAsync`, `RestoreAsync`) with `IProjectAccess` checks first, `Conflict` on concurrency, and new issues ranked at the bottom (make T056, T057, T060 pass)
- [ ] T079 [US1] Implement `src/UblJira.Application/Issues/IssueQueries.cs` (`GetAsync`, `ListProjectIssuesAsync` using `FullTextQuery` for words, `ListDeletedAsync`) with `AsNoTracking` projections (make T058, T059 pass)
- [ ] T080 [P] [US1] Implement `LabelService.SuggestAsync` in `src/UblJira.Application/Issues/LabelService.cs` (prefix match on `NormalizedName`, top 10)
- [ ] T081 [P] [US1] Create shared components in `src/UblJira.Web/Components/Shared/`: `InlineFieldEditor.razor`, `MarkdownEditor.razor` (accessible textarea, formatting toolbar, Preview tab), `UserPicker.razor`, `LabelPicker.razor`, `ConflictBanner.razor`, `ErrorSummary.razor`, `EmptyState.razor` (make T063 pass)
- [ ] T082 [P] [US1] Create `src/UblJira.Web/Components/Pages/Admin/Users.razor` at `/admin/users`: list and create users; show the temporary password once, with a copy button
- [ ] T083 [P] [US1] Create in `src/UblJira.Web/Components/Pages/Projects/`: `ProjectList.razor` (`/projects`), `CreateProject.razor` (`/projects/new`, Administrator), `ProjectSettings.razor` (`/projects/{key}/settings`: details, and add member with a role)
- [ ] T084 [US1] Create `src/UblJira.Web/Components/Pages/Issues/ProjectIssues.razor` at `/projects/{key}/issues`: filters kept in the query string, sortable columns, pages of 50, result count, empty state
- [ ] T085 [US1] Create `src/UblJira.Web/Components/Pages/Issues/CreateIssueDialog.razor`: type, summary, description, priority, assignee with "Assign to me", labels, due date, estimate, parent; validation keeps the input
- [ ] T086 [US1] Create `src/UblJira.Web/Components/Pages/Issues/IssuePage.razor` at `/browse/{IssueKey}`: inline field editors, status picker with the open-sub-task warning, sub-task list with "Create sub-task", delete with preview (Project Admin, Administrator), and the not-found page for `NotFound`
- [ ] T087 [P] [US1] Create `src/UblJira.Web/Components/Pages/Admin/DeletedIssues.razor` at `/admin/deleted-issues` (list and restore)
- [ ] T088 [US1] Add `tests/UblJira.Architecture.Tests/EntityEncapsulationTests.cs` asserting `Issue` properties have non-public setters (research R12), then make T064 pass end to end

**Checkpoint**: User Story 1 is fully functional and testable on its own. This is the MVP.

---

## Phase 4: User Story 2 - Work visually on a board (Priority: P2)

**Goal**: A board per project with a column per status; cards move by drag-and-drop or keyboard;
order is shared; quick filters; a card opens in a side panel.

**Independent Test**: With issues in every status, drag a card To Do → In Progress, reorder a column,
move a card by keyboard, filter to "Only my issues"; after reload, statuses and order persist (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [ ] T089 [P] [US2] Write `tests/UblJira.Application.Tests/Planning/BoardQueryTests.cs`: `US2_AS1` four columns with counts and no Epic cards; `US2_AS5` Done shows only issues resolved in the last 14 days; `US2_AS6` "Only my issues"; quick filters by type, label, Epic and words (FR-028, FR-029, FR-031)
- [ ] T090 [P] [US2] Write `tests/UblJira.Application.Tests/Planning/MoveCardTests.cs`: `US2_AS2` the status change is recorded in history; `US2_AS4` the new order persists for every user; `US2_AS7` a stale version returns `Conflict` with the current status; placements Before, After, Top, Bottom, Up, Down; Viewer → `Forbidden` (FR-030)
- [ ] T091 [P] [US2] Write `tests/UblJira.Application.Tests/Planning/RankRebalanceTests.cs`: a project with keys over 48 characters is rebalanced without changing the order
- [ ] T092 [P] [US2] Write bUnit tests `tests/UblJira.Web.Tests/Board/MoveToMenuTests.cs` and `tests/UblJira.Web.Tests/Board/BoardColumnTests.cs`: `US2_AS3` the keyboard "Move to" action issues the same call as drag-and-drop; focus returns to the moved card; counts render
- [ ] T093 [P] [US2] Write `tests/UblJira.E2E.Tests/US2_BoardTests.cs`: the US2 Independent Test (drag, reorder, keyboard move with `M`, "Only my issues", state after reload) plus an axe scan of the board

### Implementation for User Story 2

- [ ] T094 [US2] Implement `RankPlacementResolver` in `src/UblJira.Application/Issues/RankPlacementResolver.cs` (turns a `RankPlacement` into a new `Rank` within a column or backlog scope)
- [ ] T095 [US2] Implement `IIssuePlanning` (contract `src/UblJira.Application/Issues/Contracts/IIssuePlanning.cs`) in `src/UblJira.Application/Issues/IssuePlanning.cs`: `MoveAsync` (status and rank in one change set) and `CardsAsync` for the Kanban scope with `BoardFilter`
- [ ] T096 [US2] Implement `BoardService` in `src/UblJira.Application/Planning/BoardService.cs` (make T089, T090 pass)
- [ ] T097 [P] [US2] Implement `RankRebalanceWorker` in `src/UblJira.Infrastructure/Workers/RankRebalanceWorker.cs` (make T091 pass)
- [ ] T098 [US2] Create in `src/UblJira.Web/Components/Pages/Board/`: `BoardPage.razor` (`/projects/{key}/board`), `BoardColumn.razor` (with `Virtualize`), `BoardCard.razor`, `BoardQuickFilters.razor`, `MoveToMenu.razor`, `IssueSidePanel.razor` reusing the issue-page editors (FR-032) (make T092 pass)
- [ ] T099 [US2] Add `src/UblJira.Web/wwwroot/js/drag-drop.js` (drop-position calculation) and screen-reader announcements for moves, then make T093 pass

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Collaborate on issues (Priority: P3)

**Goal**: Comments, mentions, watches, in-app notifications with email for assignments and mentions,
attachments with malware scanning, and a readable change history.

**Independent Test**: Member A comments, mentions B and attaches a screenshot; B gets an unread
notification and an email; the screenshot becomes downloadable after the malware check; A's edit is
marked; history lists everything (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [ ] T100 [P] [US3] Write `tests/UblJira.Application.Tests/Collaboration/CommentTests.cs`: `US3_AS1` a formatted comment appears and its author starts watching; `US3_AS4` editing marks it edited and records history; `US3_AS5` deleting leaves a placeholder and history; only the author can edit or delete; Viewer → `Forbidden`; body "1–32,000 chars" (FR-033, FR-035)
- [ ] T101 [P] [US3] Write `tests/UblJira.Application.Tests/Collaboration/MentionTests.cs`: `US3_AS2` a mention creates a notification and raises the unread count; `US3_AS7` suggestions and notifications only reach users who can access the project; unresolvable mentions notify no one; mentions in descriptions also notify (FR-034)
- [ ] T102 [P] [US3] Write `tests/UblJira.Application.Tests/Collaboration/NotificationTests.cs`: `US3_AS3` a watcher is notified of a status change and the actor is not; assignment notifications; reporter and assignee auto-watch; mark read and mark all read (FR-035, FR-036)
- [ ] T103 [P] [US3] Write `tests/UblJira.Application.Tests/Collaboration/EmailOutboxTests.cs`: `US3_AS8` emails are queued only for Assigned and Mentioned, not when switched off and not for deactivated users; content has the key, summary, event and link but no description or comment text; an SMTP failure never fails the action; retries back off; messages become `Expired` after 24 hours using `FakeTimeProvider` (FR-057, FR-058)
- [ ] T104 [P] [US3] Write `tests/UblJira.Application.Tests/Issues/AttachmentTests.cs`: `US3_AS9` an upload is listed as Pending with name, size, uploader and time; `US3_AS10` a 15 MB file and an `.exe` are refused (`FileTooLarge`, `FileTypeNotAllowed`); an extension that does not match the file signature is refused; removal by uploader, Project Admin or Administrator; purge by Administrator only, with an `AttachmentPurged` audit event; download only when `Clean` (FR-053–FR-056)
- [ ] T105 [P] [US3] Write `tests/UblJira.Application.Tests/Issues/ClamAvScannerTests.cs` against a ClamAV Testcontainer: a clean file → Clean; `US3_AS11` the EICAR test file (assembled at runtime so the repository holds no signature) → Infected; an unreachable scanner → Unavailable (SC-010)
- [ ] T106 [P] [US3] Write `tests/UblJira.Application.Tests/Issues/AttachmentScanWorkerTests.cs`: Pending → Clean or Blocked; Blocked writes `MalwareDetected` and notifies the uploader; Unavailable retries after 1 minute, doubling up to 30 minutes (FR-054)
- [ ] T107 [P] [US3] Write `tests/UblJira.Application.Tests/Issues/IssueHistoryQueryTests.cs`: `US3_AS6` history in time order with who, when, field, old and new values, including comment and attachment events (FR-037)
- [ ] T108 [P] [US3] Write `tests/UblJira.Web.Tests/Endpoints/AttachmentDownloadTests.cs` (WebApplicationFactory): 404 for no access, Pending, Blocked, removed, purged, or a deactivated user; `Content-Disposition`, `nosniff` and `Content-Security-Policy: sandbox` headers; preview only for images (contracts/http-endpoints.md)
- [ ] T109 [P] [US3] Write bUnit tests in `tests/UblJira.Web.Tests/Collaboration/`: `CommentThreadTests.cs`, `MentionSuggestionsTests.cs`, `AttachmentListTests.cs`, `NotificationBellTests.cs`
- [ ] T110 [P] [US3] Write `tests/UblJira.E2E.Tests/US3_CollaborateTests.cs`: the US3 Independent Test, including the email in Mailpit and the malware block, plus axe scans of the issue page and notifications

### Implementation for User Story 3

- [ ] T111 [P] [US3] Create `Comment`, `Watch`, `Notification` and `EmailOutboxMessage` in `src/UblJira.Domain/Collaboration/` per data-model.md (`Comment.Body` "Markdown, 1–32,000 chars"; `Notification.Type` `Assigned`, `Mentioned`, `Commented`, `StatusChanged`; `Notification.Text` "nvarchar(300)"; outbox `Status` `Pending`, `Sent`, `Expired`; `Subject` "nvarchar(200)")
- [ ] T112 [P] [US3] Create `Attachment` and `AttachmentContent` in `src/UblJira.Domain/Issues/` (`FileName` "nvarchar(255)"; `SizeBytes` "1 – 10,485,760"; `ScanStatus` `Pending`, `Clean`, `Blocked`; `Sha256` "binary(32)"; removal and purge fields; the scan state machine from data-model.md)
- [ ] T113 [US3] Add EF configurations in `src/UblJira.Infrastructure/Persistence/Configurations/Collaboration/` and `src/UblJira.Infrastructure/Persistence/Configurations/Issues/AttachmentConfiguration.cs` with the data-model.md indexes, and migration `Collaboration` including the full-text index on `Comments(Body)`
- [ ] T114 [P] [US3] Implement `MentionParser` in `src/UblJira.Application/Collaboration/MentionParser.cs` (`@username` tokens resolved only to users who can view the project)
- [ ] T115 [US3] Implement `IIssueEventPublisher` (contract `src/UblJira.Application/Collaboration/Contracts/IIssueEventPublisher.cs`) in `src/UblJira.Application/Collaboration/IssueEventPublisher.cs`, creating notifications for targets and watchers except the actor and outbox rows for Assigned and Mentioned per contracts/email-notifications.md; call it from `IssueCommands` for assignment, status change and description mentions, and auto-watch the reporter and assignee
- [ ] T116 [US3] Implement `CommentService`, `MentionService`, `WatchService` and `NotificationService` in `src/UblJira.Application/Collaboration/` (make T100, T101, T102 pass)
- [ ] T117 [US3] Implement `IssueQueries.GetHistoryAsync` in `src/UblJira.Application/Issues/IssueQueries.cs` (make T107 pass)
- [ ] T118 [P] [US3] Implement `EmailTemplates` in `src/UblJira.Infrastructure/Email/EmailTemplates.cs` (Assigned and Mentioned per contract; summary truncated to 120 characters in the subject; CR/LF stripped from interpolated values) and `MailKitEmailSender` (`IEmailSender`) in `src/UblJira.Infrastructure/Email/MailKitEmailSender.cs`
- [ ] T119 [US3] Implement `EmailOutboxWorker` in `src/UblJira.Infrastructure/Email/EmailOutboxWorker.cs` (claims rows with `UPDLOCK, READPAST`; exponential backoff; `Expired` after 24 hours) (make T103 pass)
- [ ] T120 [P] [US3] Implement `FileSignatureValidator` in `src/UblJira.Infrastructure/Attachments/FileSignatureValidator.cs` (allowed: `.png`, `.jpg`/`.jpeg`, `.gif`, `.webp`, `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.txt`, `.log`, `.csv`; the extension must match the verified signature) and `DbAttachmentStore` (`IAttachmentStore`) in `src/UblJira.Infrastructure/Attachments/DbAttachmentStore.cs`
- [ ] T121 [US3] Implement `AttachmentService` in `src/UblJira.Application/Issues/AttachmentService.cs` (make T104 pass)
- [ ] T122 [P] [US3] Implement `ClamAvScanner` (`IMalwareScanner`, clamd `INSTREAM` over TCP, host and port from `Scanner:ClamAv`) in `src/UblJira.Infrastructure/Scanning/ClamAvScanner.cs` (make T105 pass)
- [ ] T123 [US3] Implement `AttachmentScanWorker` in `src/UblJira.Infrastructure/Scanning/AttachmentScanWorker.cs` (make T106 pass)
- [ ] T124 [US3] Implement `src/UblJira.Web/Endpoints/AttachmentEndpoints.cs` (`GET /attachments/{id}` and `GET /attachments/{id}/preview`) and add ClamAV and SMTP checks that report "degraded" to `src/UblJira.Web/Observability/HealthEndpoints.cs` (make T108 pass)
- [ ] T125 [US3] Create in `src/UblJira.Web/Components/Pages/Issues/`: `CommentThread.razor`, `MentionSuggestions.razor`, `AttachmentList.razor`, `AttachmentUpload.razor` (`InputFile` with a 10 MB limit and a "being checked" state), `HistoryTab.razor`, `WatchToggle.razor`, and add them to `IssuePage.razor` (make T109 pass)
- [ ] T126 [US3] Create `src/UblJira.Web/Components/Layout/NotificationBell.razor` (unread count refreshed on navigation and every 60 seconds) and `src/UblJira.Web/Components/Pages/Notifications/Notifications.razor` at `/notifications`
- [ ] T127 [US3] Add the "notification emails" switch to `src/UblJira.Web/Components/Pages/Profile/Profile.razor` and `AccountService.UpdateProfileAsync`, then make T110 pass

**Checkpoint**: User Stories 1–3 work independently.

---

## Phase 6: User Story 4 - Plan and run sprints (Priority: P4)

**Goal**: Sprint-based projects get a ranked backlog, sprints that can be planned, started (one at a
time) and completed, and a sprint report.

**Independent Test**: In a sprint-based project with 15 estimated issues: reorder the backlog, create
a sprint with 5 issues, start it, finish 3, complete the sprint sending 2 to the backlog, and view
the report (spec US4).

### Tests for User Story 4 (write first, must fail) ⚠️

- [ ] T128 [P] [US4] Write `tests/UblJira.Domain.Tests/Planning/SprintTests.cs`: `Planned → Active → Completed` only; starting requires dates with "`EndDate > StartDate`" and defaults the end to "start + 14 days"; completed sprints are read-only; default name "`{Key} Sprint {Sequence}`" (FR-040, FR-041, FR-043)
- [ ] T129 [P] [US4] Write `tests/UblJira.Application.Tests/Planning/BacklogTests.cs`: `US4_AS1` active and planned sprints with issue counts and story points, followed by open issues without a sprint, excluding Epics and Sub-tasks, in rank order; `US4_AS2` "Move to top" persists; moving issues between backlog and sprints; a Kanban project returns `NotScrumProject` (FR-038, FR-039, FR-042)
- [ ] T130 [P] [US4] Write `tests/UblJira.Application.Tests/Planning/SprintLifecycleTests.cs`: `US4_AS3` starting a sprint makes the board show only its issues; `US4_AS4` a second start returns `SprintAlreadyActive`, including under a concurrent race (filtered unique index); `US4_AS5` completion moves unfinished issues to the backlog or a planned sprint; `US4_AS7` sub-tasks follow their parent; changing the board style during an active sprint returns `BoardStyleLockedDuringSprint` (FR-014, FR-041, FR-043)
- [ ] T131 [P] [US4] Write `tests/UblJira.Application.Tests/Planning/SprintReportTests.cs`: `US4_AS6` issues and story points committed at start, added, removed, completed and not completed (FR-044)
- [ ] T132 [P] [US4] Write bUnit tests `tests/UblJira.Web.Tests/Planning/BacklogListTests.cs` and `tests/UblJira.Web.Tests/Planning/CompleteSprintDialogTests.cs` (keyboard move actions; choice of destination for unfinished issues)
- [ ] T133 [P] [US4] Write `tests/UblJira.E2E.Tests/US4_SprintsTests.cs`: the US4 Independent Test plus axe scans of the backlog and sprint report

### Implementation for User Story 4

- [ ] T134 [P] [US4] Create `Sprint` and `SprintIssueEvent` in `src/UblJira.Domain/Planning/` (`Name` "nvarchar(60)"; `Goal` "nvarchar(500) null"; `State` `Planned`, `Active`, `Completed`; `StartDate`, `EndDate` "date null"; event types `CommittedAtStart`, `Added`, `Removed`, `Completed`, `NotCompleted` with `Estimate` "decimal(4,1) null") (make T128 pass)
- [ ] T135 [US4] Add EF configurations in `src/UblJira.Infrastructure/Persistence/Configurations/Planning/` (unique `(ProjectId, Sequence)`; filtered unique index `(ProjectId) WHERE State = 'Active'`) and migration `Planning`, which also adds the `Issues.SprintId → Sprints` foreign key
- [ ] T136 [US4] Extend `src/UblJira.Application/Issues/IssuePlanning.cs` with `SetSprintAsync` (sub-tasks follow their parent; history records Sprint changes) and the active-sprint card scope
- [ ] T137 [US4] Implement `SprintService` in `src/UblJira.Application/Planning/SprintService.cs` (create, start, preview completion, complete, report; writes `SprintIssueEvent` rows) (make T130, T131 pass)
- [ ] T138 [US4] Implement `BacklogService` in `src/UblJira.Application/Planning/BacklogService.cs` (records Added and Removed events while a sprint is active) and add the Scrum scope with a "No active sprint" empty state to `src/UblJira.Application/Planning/BoardService.cs` (make T129 pass)
- [ ] T139 [US4] Enforce `BoardStyleLockedDuringSprint` in `ProjectService.UpdateAsync` in `src/UblJira.Application/Projects/ProjectService.cs`
- [ ] T140 [US4] Create in `src/UblJira.Web/Components/Pages/Backlog/`: `BacklogPage.razor` (`/projects/{key}/backlog`), `SprintPanel.razor`, `BacklogItem.razor` (Move to top, bottom, up, down), `StartSprintDialog.razor`, `CompleteSprintDialog.razor`; and `src/UblJira.Web/Components/Pages/Sprints/SprintReport.razor` at `/projects/{key}/sprints/{sprintId}`; show the Backlog tab only for Scrum projects (make T132, T133 pass)

**Checkpoint**: User Stories 1–4 work independently.

---

## Phase 7: User Story 5 - Find work quickly (Priority: P5)

**Goal**: Open issues by key from anywhere, search all accessible projects with words and filters,
save filters, and land on My Work.

**Independent Test**: Search "refund" across three projects (one inaccessible), filter, save and rerun
the filter; jump to an issue by key; My Work lists assigned open issues; nothing leaks from the
inaccessible project (spec US5).

### Tests for User Story 5 (write first, must fail) ⚠️

- [ ] T141 [P] [US5] Write `tests/UblJira.Application.Tests/Search/IssueSearchTests.cs`: `US5_AS1` key resolution only for accessible issues; `US5_AS2` words across summary, description and comments, most recently updated first; `US5_AS3` combined filters including reporter, sprint, Epic and created, updated and due date ranges; `US5_AS5` no results from inaccessible projects; archived projects excluded unless included; pages of 50 (FR-045–FR-047)
- [ ] T142 [P] [US5] Write `tests/UblJira.Application.Tests/Search/SavedFilterTests.cs`: `US5_AS4` save, run, rename, delete; `DuplicateFilterName`; another user's filter → `NotFound` (FR-048)
- [ ] T143 [P] [US5] Write `tests/UblJira.Application.Tests/Search/MyWorkTests.cs`: `US5_AS6` open assigned issues grouped by project; the 10 most recently viewed issues, with older views pruned (FR-049)
- [ ] T144 [P] [US5] Write bUnit tests `tests/UblJira.Web.Tests/Search/GlobalSearchBoxTests.cs` and `tests/UblJira.Web.Tests/Search/SearchFiltersTests.cs` (a key navigates to the issue; each filter chip can be removed on its own)
- [ ] T145 [P] [US5] Write `tests/UblJira.E2E.Tests/US5_SearchTests.cs`: the US5 Independent Test plus axe scans of search and My Work

### Implementation for User Story 5

- [ ] T146 [P] [US5] Create `SavedFilter` (`Name` "nvarchar(80)", "unique per owner"; `CriteriaJson` "nvarchar(4000)") and `RecentIssueView` (PK `(UserId, IssueId)`, `ViewedAt`) in `src/UblJira.Domain/Search/`
- [ ] T147 [US5] Add EF configurations in `src/UblJira.Infrastructure/Persistence/Configurations/Search/` (index `(UserId, ViewedAt DESC)`) and migration `Search`
- [ ] T148 [US5] Implement `IRecentViews` (contract `src/UblJira.Application/Search/Contracts/IRecentViews.cs`) in `src/UblJira.Application/Search/RecentViews.cs` (keeps the latest 10 per user) and call it from `IssueQueries.GetAsync`
- [ ] T149 [US5] Implement `IssueSearchService` in `src/UblJira.Application/Search/IssueSearchService.cs` (full-text on issues plus `EXISTS` on comments; filters; sort; paging) and `IssueSearchCriteriaSerializer` in `src/UblJira.Application/Search/IssueSearchCriteriaSerializer.cs` (make T141 pass)
- [ ] T150 [US5] Implement `SavedFilterService` and `MyWorkService` in `src/UblJira.Application/Search/` (make T142, T143 pass)
- [ ] T151 [US5] Create `src/UblJira.Web/Components/Layout/GlobalSearchBox.razor` (in the header), `src/UblJira.Web/Components/Pages/Search/SearchPage.razor` (`/search`, criteria in the query string, removable chips, save), `src/UblJira.Web/Components/Pages/Search/SavedFilters.razor` (`/filters`), and `src/UblJira.Web/Components/Pages/MyWork/MyWork.razor` at `/`, replacing the redirect (make T144, T145 pass)

**Checkpoint**: User Stories 1–5 work independently.

---

## Phase 8: User Story 6 - Manage users and project access (Priority: P6)

**Goal**: Full account lifecycle for Administrators, project-level role management for Project
Admins, archiving, organization settings, and the audit log.

**Independent Test**: Deactivate a user (cannot sign in, still shown on history), reset a password,
promote a Project Admin who adds a read-only Viewer; the audit log shows every event (spec US6).

### Tests for User Story 6 (write first, must fail) ⚠️

- [ ] T152 [P] [US6] Write `tests/UblJira.Application.Tests/Identity/UserLifecycleTests.cs`: `US6_AS1` deactivation blocks sign-in, rotates the security stamp, removes the user from assignee suggestions and keeps their name on history; reactivation; `US6_AS2` reset gives a temporary password and forces a change; `US6_AS7` `LastAdministrator` on deactivate and on demote; role changes audited (FR-007)
- [ ] T153 [P] [US6] Write `tests/UblJira.Application.Tests/Projects/MembershipManagementTests.cs`: `US6_AS3` a Project Admin adds a Viewer who can read but gets `Forbidden` on every write; `US6_AS4` a Member cannot manage members or delete issues; role changes and removals are audited; a removed member gets `NotFound` on the next call; assignments survive removal (FR-009, FR-014)
- [ ] T154 [P] [US6] Write `tests/UblJira.Application.Tests/Identity/AuditLogQueryTests.cs`: `US6_AS5` failed sign-ins and the lockout are listed; `US6_AS6` filters by user, date range and event type; non-administrator → `Forbidden` (FR-011)
- [ ] T155 [P] [US6] Write `tests/UblJira.Application.Tests/Projects/ProjectArchiveTests.cs`: Administrator-only archive and restore; archived projects are read-only (`ProjectArchived`) and hidden from lists and search unless included; members can still open issue links (FR-015)
- [ ] T156 [P] [US6] Write `tests/UblJira.Application.Tests/Identity/OrganizationSettingsTests.cs`: idle timeout "5–480"; default time zone must be a valid IANA ID; `SettingsChanged` audit event; non-administrator → `Forbidden` (FR-005, FR-051)
- [ ] T157 [P] [US6] Write the data-driven `tests/UblJira.Application.Tests/Security/PermissionMatrixTests.cs`, exercising every row of `specs/001-issue-tracker-mvp/contracts/permissions.md` against the real services for each role (SC-005)
- [ ] T158 [P] [US6] Write bUnit tests `tests/UblJira.Web.Tests/Admin/UserDetailsTests.cs` and `tests/UblJira.Web.Tests/Admin/MembersEditorTests.cs`
- [ ] T159 [P] [US6] Write `tests/UblJira.E2E.Tests/US6_ManageAccessTests.cs`: the US6 Independent Test plus axe scans of the admin screens

### Implementation for User Story 6

- [ ] T160 [US6] Implement `DeactivateAsync`, `ReactivateAsync`, `ResetPasswordAsync` and `SetOrganizationRoleAsync` in `src/UblJira.Application/Identity/UserAdminService.cs` (last-administrator check in a serializable transaction; security stamp rotation) (make T152 pass)
- [ ] T161 [US6] Implement `ChangeRoleAsync` and `RemoveMemberAsync` in `src/UblJira.Application/Projects/ProjectMembershipService.cs` and allow Project Admins to manage members (make T153 pass)
- [ ] T162 [P] [US6] Implement `AuditLogQuery` in `src/UblJira.Application/Identity/AuditLogQuery.cs` (make T154 pass)
- [ ] T163 [US6] Implement `ProjectService.ArchiveAsync` and `RestoreAsync` with `ProjectArchived`/`ProjectRestored` audit events in `src/UblJira.Application/Projects/ProjectService.cs` (make T155 pass)
- [ ] T164 [US6] Implement `OrganizationSettingsService.UpdateAsync` in `src/UblJira.Application/Identity/OrganizationSettingsService.cs` (make T156 pass)
- [ ] T165 [US6] Create in `src/UblJira.Web/Components/Pages/Admin/`: `UserDetails.razor` (`/admin/users/{id}`: deactivate, reactivate, reset password, organization role), `AuditLog.razor` (`/admin/audit`), `Settings.razor` (`/admin/settings`); add archive and restore actions to `src/UblJira.Web/Components/Pages/Projects/ProjectList.razor`
- [ ] T166 [US6] Add the members editor for Project Admins (change role, remove) to `src/UblJira.Web/Components/Pages/Projects/ProjectSettings.razor`, then make T157, T158, T159 pass

**Checkpoint**: All six user stories work independently.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T167 [P] Build the seed tool in `tools/UblJira.Seed/Program.cs` (`--users`, `--projects`, `--issues`; realistic mix of types, statuses, comments and history; bulk inserts)
- [ ] T168 Write `tests/UblJira.Performance.Tests/Sc002LoadTests.cs` (category `Performance`): 300 concurrent simulated users against the seeded 500,000-issue database, asserting p95 ≤ 1 s for opening an issue, loading a board, changing a status and running a filtered search (SC-002); tune queries and indexes until it passes
- [ ] T169 [P] Add `tests/UblJira.E2E.Tests/PageTimingTests.cs` measuring full page loads of the issue page, board and search on seeded data
- [ ] T170 [P] Add `tests/UblJira.E2E.Tests/ResponsiveLayoutTests.cs`: primary screens usable at 360 px wide, and each story's main path completed with the keyboard only (FR-050, SC-008)
- [ ] T171 [P] Write `docs/operations/deployment.md` (Linux container and Windows Server/IIS with WebSockets; configuration keys; data protection certificate; TLS; TDE recommendation)
- [ ] T172 [P] Write `docs/operations/backup-restore.md` (daily full, 6-hourly differential and 15-minute log backups; restore runbook; drill record template) for SC-011 and SC-012
- [ ] T173 [P] Write `docs/operations/monitoring.md` (health endpoints, OpenTelemetry signals, suggested alerts for readiness, outbox backlog, pending scans and sign-in failures)
- [ ] T174 [P] Write `docs/pilot/pilot-plan.md` defining how SC-001, SC-003, SC-007 and SC-009 are measured during the 4-week pilot (timed tasks and a survey)
- [ ] T175 Review security against OWASP ASVS Level 2, record it in `docs/security/asvs-review.md` and fix findings (constitution III)
- [ ] T176 Do a manual screen-reader pass (NVDA with Edge) on the primary screens, record it in `docs/accessibility/screen-reader-review.md` and fix findings (constitution VI)
- [ ] T177 Update `README.md` with the architecture overview, local setup (linking quickstart.md) and test commands
- [ ] T178 Run every step of `specs/001-issue-tracker-mvp/quickstart.md`, including the reliability drill, and record the results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - Recommended order is priority order (P1 → P6); see story dependencies below for what can overlap
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Needs US1 (issues, ranks, issue editors for the side panel).
- **User Story 3 (P3)**: Needs US1 (issues, history recording). Independent of US2.
- **User Story 4 (P4)**: Needs US2 (board scopes, rank placement) and US1.
- **User Story 5 (P5)**: Needs US1. Comment search needs US3 and the sprint filter needs US4; if US5
  is built earlier, those two filters are added when those stories land.
- **User Story 6 (P6)**: Needs US1 only; can run in parallel with US2–US5.

```text
Setup → Foundational → US1 ─┬─ US2 ── US4 ─┐
                            ├─ US3 ────────┼─ US5 → Polish
                            └─ US6 ────────┘
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Entities before configurations and migrations; migrations before services
- Services before UI components; UI before the end-to-end test is expected to pass
- Story complete (checkpoint green) before moving to the next priority

### Parallel Opportunities

- Setup: T002, T003, T004, T005, T006, T007, T009, T010, T011 in parallel after T001
- Foundational: all eight test tasks in parallel; then T020, T021, T022, T023, T024 in parallel
- Each story: all test tasks marked [P] in parallel; entity tasks marked [P] in parallel
- After US1: US2, US3 and US6 can proceed in parallel with separate developers

---

## Parallel Example: User Story 1

```bash
# Write all US1 tests together (they must fail first):
Task: "Domain tests in tests/UblJira.Domain.Tests/Issues/IssueWorkflowTests.cs"
Task: "Domain tests in tests/UblJira.Domain.Tests/Issues/IssueHierarchyTests.cs"
Task: "Integration tests in tests/UblJira.Application.Tests/Issues/IssueCreationTests.cs"
Task: "Integration tests in tests/UblJira.Application.Tests/Issues/ProjectIssueListTests.cs"

# Then create the independent domain pieces together:
Task: "Create enums in src/UblJira.Domain/Issues/"
Task: "Implement Rank in src/UblJira.Domain/Common/Rank.cs"
Task: "Create IssueChange in src/UblJira.Domain/Issues/IssueChange.cs"
Task: "Create Label and IssueLabel in src/UblJira.Domain/Issues/Label.cs"
```

## Parallel Example: User Story 3

```bash
# Tests together:
Task: "CommentTests.cs", "MentionTests.cs", "EmailOutboxTests.cs", "AttachmentTests.cs", "ClamAvScannerTests.cs"

# Independent infrastructure together:
Task: "EmailTemplates + MailKitEmailSender in src/UblJira.Infrastructure/Email/"
Task: "FileSignatureValidator + DbAttachmentStore in src/UblJira.Infrastructure/Attachments/"
Task: "ClamAvScanner in src/UblJira.Infrastructure/Scanning/ClamAvScanner.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run the US1 tests and quickstart.md section 4 (US1)
5. Demo to a pilot team

### Incremental Delivery

1. Setup + Foundational → sign-in, setup, security baseline
2. US1 → track issues (MVP) → demo
3. US2 → board → demo
4. US3 → comments, notifications, email, attachments → start the pilot (SC-009)
5. US4 → sprints; US5 → search and My Work; US6 → full access management
6. Polish → performance, operations docs, security and accessibility reviews, go-live drill

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. Developer A: US1, then US2, then US4
3. Developer B: joins after US1 for US3, then US5
4. Developer C: joins after US1 for US6, then Polish documentation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group; reference task IDs in pull requests (constitution, Traceability)
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
