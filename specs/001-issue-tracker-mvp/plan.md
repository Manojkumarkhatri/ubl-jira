# Implementation Plan: Issue Tracker MVP

**Branch**: `claude/practical-lamport-l47ei8` (feature `001-issue-tracker-mvp`) | **Date**: 2026-09-27 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-issue-tracker-mvp/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Build a Jira-like issue tracker for one organization: projects with members and roles; issues (Epic,
Story, Task, Bug, Sub-task) with a fixed four-status workflow, keys like `PAY-42`, history, and
attachments; a drag-and-drop board; comments, mentions, watches, in-app and email notifications;
backlog and sprints with a sprint report; cross-project search, saved filters, and a My Work page;
and administration of accounts, project access, and an audit log. Six user stories are delivered in
priority order, and each is usable on its own.

**Technical approach** (details in [research.md](./research.md)): a single ASP.NET Core **Blazor Web
App** on **.NET 10** using the **Interactive Server** render mode, so UI code calls application
services in-process and there is no public API to secure. **SQL Server** through **EF Core 10** holds
all data, including attachment bytes; **SQL Server Full-Text Search** powers word search.
**ASP.NET Core Identity** provides built-in accounts, lockout, and sessions, with room to add company
SSO later. Authorization runs in every application-service call against live project membership.
Issue changes are written as append-only history in the same transaction. Emails go through a
transactional outbox to the company SMTP relay; uploads are held until a pluggable scanner (ClamAV by
default) marks them clean. The code is a **modular monolith** (Identity, Projects, Issues,
Collaboration, Planning, Search) with boundaries enforced by architecture tests.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS)

**Primary Dependencies**: ASP.NET Core 10 (Blazor Web App, Interactive Server; minimal APIs for file
downloads), ASP.NET Core Identity, EF Core 10 (SQL Server provider), Microsoft Fluent UI Blazor
components, Markdig, HtmlSanitizer, MailKit, OpenTelemetry .NET; external services: company SMTP
relay and ClamAV `clamd`

**Storage**: SQL Server 2022 or later (or Azure SQL Database) with Full-Text Search; attachments in a
dedicated `varbinary(max)` table behind `IAttachmentStore`; data protection keys in the database

**Testing**: xUnit v3, bUnit, Testcontainers (SQL Server with Full-Text Search, ClamAV), Respawn,
Microsoft.Playwright with Deque axe-core, ArchUnitNET; an opt-in performance suite

**Target Platform**: Linux container or Windows Server with IIS (WebSockets enabled), from the same
build; current Edge, Chrome, and Firefox on desktop, usable at 360 px width

**Project Type**: Web application (server-rendered Blazor, single deployable modular monolith)

**Performance Goals**: 95% of issue views, board loads, status changes, and filtered searches within
1 s at 500,000 issues and 300 concurrent users (SC-002)

**Constraints**: 99.5% monthly availability, RPO ≤ 1 h, RTO ≤ 4 h (SC-011, SC-012); files ≤ 10 MB and
malware-checked before download (FR-053, FR-054); WCAG 2.2 AA (FR-050); 30-minute idle timeout
(FR-005); no OS-specific APIs (constitution, Portability); email never blocks user actions (FR-058)

**Scale/Scope**: 2,000 named users, 300 concurrent circuits, 500,000 issues (about 1.5 M comments, 5 M
history rows, 100,000 attachments ≈ 100 GB); 6 modules; about 25 screens
([contracts/ui-routes.md](./contracts/ui-routes.md))

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.0 (`.specify/memory/constitution.md`).

| # | Gate (from the constitution) | Pre-research | Post-design evidence |
|---|------------------------------|:------------:|----------------------|
| I | Spec exists in `specs/001-…`, clarified, stories prioritized and independently testable; no tech in the spec | ✅ PASS | spec.md: 6 prioritized stories, 3 clarifications recorded, checklist 16/16 |
| II | Test-first for domain rules and services; every acceptance scenario automated; data tests on real SQL Server; regression tests for defects | ✅ PASS | research R24: test projects per layer, Testcontainers SQL Server, scenario-named tests (`US1_AS3_…`); quickstart step 5 |
| III | Authenticated by default; server-side authorization on every read and write; Identity for credentials; input validation, safe rendering, CSRF; HTTPS; security audit log; dependency scanning | ✅ PASS | R6–R8, R15, R26; [permissions.md](./contracts/permissions.md) matrix with `NotFound`/`Forbidden` semantics; AuditEvent table; NuGetAudit gate |
| IV | Append-only issue history; soft delete; optimistic concurrency; UTC; reviewed EF migrations | ✅ PASS | R11–R13, R21; data-model.md: `IssueChanges`/`AuditEvents` triggers, `rowversion`, `datetimeoffset` |
| V | One deployable app and one database; module boundaries; extra infrastructure and dependencies justified; YAGNI | ⚠️ PASS with justifications | R4, R5, R19; ClamAV service, Full-Text Search, and third-party packages justified in **Complexity Tracking**; no broker, cache, or separate search engine |
| VI | WCAG 2.2 AA with keyboard alternatives for drag-and-drop; responsive to 360 px; feedback and no lost input | ✅ PASS | R3, R22; "Move to" menus, axe scans (SC-008), conflict banner keeps input |
| — | Platform standards: .NET 10, Blazor Web App, SQL Server 2022+/Azure SQL, Identity, xUnit/bUnit/Testcontainers/Playwright, performance baseline, observability, configuration, portability | ✅ PASS | Technical Context above; R23 (OpenTelemetry, health checks), R25 (container and IIS from one build) |
| — | Workflow gates: PR review, CI (warnings as errors, format, tests, vulnerability audit, migrations in sync), Definition of Done, traceability | ✅ PASS | quickstart step 6 lists the CI commands; tests named after acceptance scenarios; tasks reference FR IDs |

**Result**: no unjustified violations. Gate passed before Phase 0 and re-checked after Phase 1 with
the same outcome. The design added no new infrastructure beyond what research had justified.

## Project Structure

### Documentation (this feature)

```text
specs/001-issue-tracker-mvp/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── application-services.md
│   ├── permissions.md
│   ├── http-endpoints.md
│   ├── ui-routes.md
│   └── email-notifications.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
UblJira.slnx
global.json                         # pins the .NET 10 SDK
Directory.Build.props               # nullable on, warnings as errors, analyzers, NuGetAudit
Directory.Packages.props            # central package versions
.editorconfig
.config/dotnet-tools.json           # dotnet-ef
docker-compose.yml                  # local sqlserver (FTS), clamav, mailpit
docker/sqlserver-fts/Dockerfile     # SQL Server image with Full-Text Search
Dockerfile                          # production container image for UblJira.Web
.github/workflows/ci.yml            # build, format, tests, audit, migrations check

src/
├── UblJira.Domain/                 # entities, value objects, domain rules; no I/O
│   ├── Common/                     # Entity base, domain errors, IssueKey, Rank (fractional index)
│   ├── Identity/                   # User roles, AuditEventType
│   ├── Projects/                   # Project, ProjectMember, ProjectRole, BoardStyle
│   ├── Issues/                     # Issue (workflow, hierarchy), Label, IssueChange, Attachment
│   ├── Collaboration/              # Comment, Watch, Notification, EmailOutboxMessage
│   ├── Planning/                   # Sprint, SprintIssueEvent
│   └── Search/                     # SavedFilter, RecentIssueView
├── UblJira.Application/            # use cases; permission checks; DTOs; module contracts
│   ├── Common/                     # Result/AppError, Page<T>, ICurrentUser, IAppDbContext, TimeProvider use
│   ├── Identity/                   # Setup, Account, UserAdmin, AuditLog, OrganizationSettings
│   ├── Projects/                   # ProjectService, MembershipService, Contracts/IProjectAccess
│   ├── Issues/                     # IssueService, LabelService, AttachmentService, Contracts/IIssuePlanning
│   ├── Collaboration/              # Comments, Mentions, Watches, Notifications, Contracts/IIssueEventPublisher
│   ├── Planning/                   # BoardService, BacklogService, SprintService
│   └── Search/                     # IssueSearchService, SavedFilterService, MyWorkService, Contracts/IRecentViews
├── UblJira.Infrastructure/         # EF Core, Identity stores, email, scanning, workers
│   ├── Persistence/                # AppDbContext, Configurations/<Module>/, Migrations/, query filters
│   ├── Identity/                   # AuditingSignInManager, UsernamePasswordValidator, revalidation
│   ├── Search/                     # Full-text query composition
│   ├── Attachments/                # DbAttachmentStore, FileSignatureValidator
│   ├── Scanning/                   # ClamAvScanner (INSTREAM client), AttachmentScanWorker
│   ├── Email/                      # MailKitEmailSender, EmailOutboxWorker, templates
│   └── Workers/                    # RankRebalanceWorker
└── UblJira.Web/                    # Blazor Web App host and composition root
    ├── Program.cs
    ├── Components/                 # App.razor, Routes.razor, Layout/, Shared/ (editor, pickers, dialogs)
    ├── Components/Account/         # Identity pages (static SSR), Setup, ChangePassword
    ├── Components/Pages/           # MyWork/, Projects/, Issues/, Board/, Backlog/, Sprints/, Search/,
    │                               #   Notifications/, Admin/, Profile/
    ├── Endpoints/                  # AttachmentEndpoints, KeepAliveEndpoint, HealthEndpoints
    ├── Security/                   # headers/CSP, origin check, idle-timeout CircuitHandler, rate limits
    └── wwwroot/                    # app.css, js/idle-monitor.js, js/drag-drop.js

tools/
└── UblJira.Seed/                   # generates realistic data volumes for performance tests

tests/
├── UblJira.Domain.Tests/           # unit: workflow, hierarchy, keys, ranks, sprint rules
├── UblJira.Application.Tests/      # integration: services on real SQL Server (Testcontainers + Respawn)
├── UblJira.Web.Tests/              # bUnit component tests
├── UblJira.E2E.Tests/              # Playwright journeys per story + axe accessibility scans
├── UblJira.Architecture.Tests/     # ArchUnitNET: module boundaries, private setters
└── UblJira.Performance.Tests/      # opt-in: 300 concurrent users on the seeded database (SC-002)

docs/
└── operations/                     # deployment (container and IIS), backup-restore runbook, monitoring
```

**Structure Decision**: A single web application (one deployable, `UblJira.Web`) layered into
Domain, Application, Infrastructure, and Web projects, with the six feature modules as folders and
namespaces inside each layer (research R4). There is no separate frontend project: Blazor components
live in `UblJira.Web` and call `UblJira.Application` services directly. Tests mirror the layers,
plus architecture and performance suites.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Constitution V requires every additional runtime component and third-party dependency to be justified
here.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| ClamAV `clamd` service (extra runtime component) | FR-054 and SC-010: every upload must be malware-checked before download | .NET has no built-in scanner; Windows Defender (AMSI) is Windows-only, which breaks portability; cloud scanning APIs send internal files outside the organization |
| SQL Server Full-Text Search (optional database component; custom dev/test image) | FR-046 word search over descriptions and comments within 1 s at 500,000 issues (SC-002) | `LIKE '%term%'` scans cannot meet SC-002; Elasticsearch/OpenSearch is a separate cluster to run and secure |
| Company SMTP relay (external dependency) | FR-057 email notifications (clarification 2026-09-26) | Required by the clarified scope; the outbox keeps it off the critical path (FR-058) |
| Microsoft.FluentUI.AspNetCore.Components | Accessible inputs, dialogs, menus, grids (constitution VI, FR-050) | Hand-building accessible comboboxes and grids is large and error-prone |
| Markdig + HtmlSanitizer (Ganss.Xss) | Formatted text (FR-017, FR-033) rendered safely (FR-052) | .NET has no built-in Markdown renderer or HTML sanitizer |
| MailKit | SMTP delivery (FR-057) | Microsoft marks `System.Net.Mail.SmtpClient` as not recommended for new development |
| OpenTelemetry .NET packages | Traces and metrics required by the constitution (Observability) | The built-in `Activity`/`Meter` APIs need an exporter; OpenTelemetry is the vendor-neutral standard |
| Test-only: Respawn, Deque.AxeCore.Playwright, ArchUnitNET (in addition to the constitution's xUnit, bUnit, Testcontainers, Playwright) | Fast database reset between integration tests; automated WCAG checks (SC-008); enforcing module boundaries (constitution V) | Recreating databases per test is too slow; manual accessibility checks are not repeatable; boundary rules by convention alone erode |
