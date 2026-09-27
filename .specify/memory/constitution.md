# UBL Jira Constitution

## Core Principles

### I. Spec-Driven, Incremental Delivery

- Every feature MUST start as a specification in `specs/<###-feature-name>/`, produced with the
  Spec Kit workflow (specify → clarify → plan → tasks), before implementation begins.
- Specifications MUST describe user value and behavior (the *what* and *why*) and MUST NOT
  prescribe implementation technology; technology decisions belong in `plan.md`.
- Work MUST be sliced into prioritized user stories (P1, P2, …), each independently
  implementable, testable, and demonstrable. The P1 story alone MUST form a usable increment.
- Every code change MUST trace to a task in the feature's `tasks.md`; work outside the spec
  requires a spec amendment first.

Rationale: an issue tracker grows by accretion. Small, specified, independently shippable
increments keep scope under control and make every change reviewable against intent.

### II. Test-First Quality (NON-NEGOTIABLE)

- Domain rules (workflow transitions, permissions, sprint rules, issue keys) and application
  services MUST have automated tests written first; those tests MUST fail before the code that
  satisfies them is written (Red-Green-Refactor).
- Every acceptance scenario in a spec MUST be covered by at least one automated test (unit,
  integration, component, or end-to-end).
- Data access MUST be tested against a real SQL Server instance (for example, a container),
  never an in-memory substitute.
- Every defect fix MUST add a regression test that fails without the fix.
- The main branch MUST stay green: a failing build or test blocks merge.

Rationale: teams will rely on this tool as the system of record for their work. Regressions in
workflow or permission logic erode trust quickly and are cheap to prevent with tests.

### III. Secure by Default

- Every page and endpoint MUST require an authenticated user unless explicitly marked public
  (sign-in, password reset); the fallback authorization policy denies anonymous access.
- Authorization MUST be enforced on the server for every read and write, based on the user's
  role and project membership. Hiding UI elements is never sufficient.
- Credentials MUST be managed through ASP.NET Core Identity (salted hashing, lockout after
  repeated failures, secure password reset). Passwords and secrets MUST NOT appear in plain
  text, logs, or source control.
- All input MUST be validated server-side; user-authored content (descriptions, comments) MUST
  be encoded or sanitized before rendering; state-changing requests MUST be protected against
  cross-site request forgery.
- Transport MUST be HTTPS-only, with secure, HTTP-only authentication cookies.
- Security-relevant events (sign-in success and failure, lockout, role or membership changes,
  user deactivation) MUST be written to an audit log.
- Dependencies MUST be scanned for known vulnerabilities on every build; high or critical
  findings block release.

Rationale: the tracker will hold internal company information. A data leak or privilege
escalation is the most damaging failure this system can have.

### IV. Auditable, Consistent Data

- Every change to an issue (field edits, status transitions, assignment, comments, links) MUST
  be recorded in an append-only history capturing who, what, when, and old and new values.
- Issues, comments, and projects MUST NOT be hard-deleted through normal application use;
  removal is a soft delete or archive that stays auditable. Permanent deletion is an audited,
  administrator-only operation.
- Concurrent edits MUST be detected (optimistic concurrency) and MUST NOT silently overwrite
  another user's changes.
- Timestamps MUST be stored in UTC and displayed in the viewer's time zone.
- Schema changes MUST be made through versioned, reviewed EF Core migrations committed to source
  control; manual production schema edits are prohibited.

Rationale: an issue tracker is an accountability record. Teams must be able to trust who
changed what, and when.

### V. Simplicity: One Modular Monolith

- The system MUST ship as a single deployable ASP.NET Core application backed by a single SQL
  Server database.
- Code MUST be organized by feature module (for example Projects, Issues, Boards, Sprints,
  Identity) with explicit boundaries; a module uses another module's application interfaces,
  never its tables directly.
- Additional runtime infrastructure (message brokers, distributed caches, search engines,
  separate services, JavaScript SPA frameworks) and every new third-party dependency MUST be
  justified in the plan's Complexity Tracking table, stating why the built-in .NET option is
  insufficient.
- YAGNI: build only what the current spec requires. Configurability (custom workflows, custom
  fields) is added when a spec calls for it, not speculatively.

Rationale: a small team can run one application and one database reliably. Distributed
complexity waits until measured need justifies it.

### VI. Accessible, Responsive Experience

- User interfaces MUST meet WCAG 2.2 Level AA, including full keyboard operation; every
  drag-and-drop interaction MUST have a keyboard or menu alternative.
- Primary screens (issue view, board, backlog, search) MUST remain usable from 360 px wide
  viewports up to large desktop screens.
- Every user action MUST give visible feedback (progress, success, validation errors), and a
  failed validation MUST NOT discard what the user typed.

Rationale: everyone in the organization uses this tool all day, including people who rely on
assistive technology.

## Platform & Operational Standards

- **Runtime**: .NET 10 (LTS) and C#; ASP.NET Core with a Blazor Web App user interface.
- **Data**: SQL Server 2022 or later (or Azure SQL Database) through Entity Framework Core.
- **Identity**: ASP.NET Core Identity with built-in accounts. The identity model MUST allow
  adding company single sign-on (OpenID Connect, for example Microsoft Entra ID) later without
  migrating issue data.
- **Testing**: xUnit for unit and integration tests, bUnit for Blazor components, Testcontainers
  for SQL Server integration tests, and Playwright for end-to-end smoke tests.
- **Performance baseline**: at a scale of 2,000 named users (300 concurrently active) and
  500,000 issues, page loads and interactive actions (open issue, move card, save edit, filter)
  MUST complete within 1 second at the 95th percentile. Lists and search results MUST be
  paginated. Feature specs MAY set stricter targets.
- **Observability**: structured logging through `ILogger` with correlation IDs; health-check
  endpoints for the application and database; OpenTelemetry-compatible traces and metrics.
  Logs MUST NOT contain passwords, tokens, or issue descriptions and comments.
- **Configuration**: environment-specific settings and secrets come from configuration providers
  (environment variables, user secrets in development, a secret store in production), never
  from committed files.
- **Portability**: the application MUST NOT depend on OS-specific features, so the same
  published build runs in a Linux container or on Windows Server with IIS.

## Development Workflow & Quality Gates

- **Pull requests**: all changes land through a pull request with at least one approving
  review; the reviewer confirms the relevant plan's Constitution Check still holds.
- **CI gates** (all MUST pass before merge): build with nullable reference types enabled and
  warnings treated as errors; `dotnet format --verify-no-changes`; all automated tests;
  vulnerable-dependency check; EF Core check that the model and migrations are in sync.
- **Definition of Done**: acceptance scenarios pass as automated tests; accessibility checks
  pass for changed UI; `README.md` and the feature's `quickstart.md` are updated when setup or
  behavior changes; no `[NEEDS CLARIFICATION]` markers remain in the feature's spec.
- **Traceability**: pull request descriptions reference the feature folder and task IDs (for
  example `specs/001-issue-tracker-mvp/tasks.md` T014).

## Governance

- This constitution supersedes all other development practices in this repository; where
  guidance conflicts, the constitution wins.
- **Amendments**: proposed through a pull request that edits `.specify/memory/constitution.md`,
  states the rationale, and includes a Sync Impact Report. Amendments require approval from
  the project owner, and any amendment that invalidates an in-flight plan MUST include a
  migration note for that feature.
- **Versioning**: semantic versioning. MAJOR for removing or redefining a principle, MINOR for
  adding a principle or materially expanding guidance, PATCH for clarifications and wording.
- **Compliance**: every `plan.md` MUST pass the Constitution Check before Phase 0 research and
  again after design, with deviations justified in its Complexity Tracking table. Reviewers
  MUST reject changes that violate a principle without a recorded justification.
  `/speckit-analyze` findings that conflict with this constitution are critical.
- **Runtime guidance**: day-to-day development guidance lives in `README.md` and each feature's
  `quickstart.md`, and MUST stay consistent with this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-09-26 | **Last Amended**: 2026-09-26
