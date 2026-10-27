# Implementation Plan: Collaboration and Search (Phase 4)

**Branch**: `phase-4` (feature `005-collaboration-and-search`) | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-collaboration-and-search/spec.md`

## Summary

Phase 4 brings the everyday collaboration of Jira to U-PMS:

- formatted descriptions and comments, with @mentions and simple tables;
- watchers and in-app notifications, with a bell on every screen;
- email through the company mail server, with each person choosing all, only what is aimed at them, or none;
- attachments up to 25 MB, including pasted screenshots and technical files, checked for malware before anyone
  can download them;
- shared labels and board quick filters;
- a search box that opens items by key, a search page across projects, and saved filters that can be shared with
  project members.

**Technical approach** (details in [research.md](./research.md)): the same Blazor Web App and test stack, one new
module, and additive migrations.

- **Editor and formatted text** (R2–R5): a WYSIWYG editor built on TipTap (ProseMirror), bundled once into
  `wwwroot/js/editor.js`. It saves a JSON document, which the server checks against an allowlist and renders to
  HTML itself, so no sanitizer is needed and nothing a user writes can run.
- **Notifications** (R1, R8–R11): a new **Notifications** module owns notifications and an email outbox. The
  Work module raises events through one contract, inside its own transaction. A background worker sends the
  email through the company mail server with MailKit, retrying for 24 hours.
- **Attachments** (R12–R14): files live in the database, in their own table, streamed in and out. Uploads go
  over the circuit; downloads go through two HTTP endpoints that check access on every request. A background
  worker checks each file with ClamAV through its daemon protocol, failing closed.
- **Search** (R15–R17): a word table in SQL Server gives "words starting with" as an index seek, filtered to the
  projects the person can see. Recent views, key jumps and saved filters with project sharing sit on top of it.
- **Labels and quick filters** (R18–R19): a shared label table and board filters that work on the cards already
  loaded.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS), as Phases 1–3; the editor bundle is built with Node.js 24 LTS and
esbuild at development time only

**Primary Dependencies**:
- Existing: ASP.NET Core 10 Blazor Web App with Interactive Server, ASP.NET Core Identity, EF Core 10 SQL Server
  provider, OpenTelemetry.
- New: **MailKit** (NuGet, MIT) for SMTP; **TipTap 3** with its free extensions and **ProseMirror** (npm, MIT),
  bundled with **esbuild** (development only). See Complexity Tracking.

**Storage**: SQL Server 2022 or later. Six additive migrations: notifications (foundation); formatted text and
watchers; attachments; search and saved filters; labels; the email outbox. Files are stored as `varbinary(max)` in
their own table.

**External services** (configured, not bundled):
- the company mail server (SMTP);
- a ClamAV daemon (`clamd`), run beside U-PMS or provided by IT.

Both are optional at startup: without them, email is not queued, and uploads stay "being checked" (fail closed).

**Testing**:
- As before: xUnit v3, bUnit, Testcontainers (SQL Server), Respawn, Playwright with axe-core, and ArchUnitNET with
  the new module.
- New: Testcontainers for ClamAV and smtp4dev in a small "ExternalServices" group; a cross-site scripting set
  for the formatted text renderer; an upgrade test from the last Phase 3 migration.

**Target Platform**: unchanged (Linux container or Windows Server/IIS; current Edge, Chrome and Firefox; usable
at 360 px)

**Project Type**: web application (the same single deployable modular monolith)

**Performance Goals**: p95 ≤ 1 s at 500,000 work items, 2,000,000 comments and 300 concurrent users for every
Phase 1–3 action, plus:
- opening an item by key;
- word search across accessible projects;
- running a saved filter;
- the notification panel and unread count;
- posting a comment with mentions;
- board loads with quick filters.

The unread count refreshes within 1 minute (SC-001, SC-002).

**Constraints**:
- Phase 1–3 constraints hold: availability, RPO/RTO, WCAG 2.2 AA with keyboard alternatives, portability, and
  "not found" for non-members.
- Nothing written by a user runs in the browser (SC-007).
- No file is downloadable before its check passes (SC-003).
- No suggestion, notification, email or search result reveals anything from a project the person cannot see
  (SC-004).
- Existing text reads the same after the upgrade (SC-008).

**Scale/Scope**: as Phase 3, plus up to 2,000,000 comments and 100,000 attachments. The phase adds one screen
(`/search`) and two header controls (search box and bell), and changes the drawer, the board, the list and the
profile ([contracts/ui-routes.md](./contracts/ui-routes.md)).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.0.1 (`.specify/memory/constitution.md`).

| # | Gate | Pre-research | Post-design evidence |
|---|------|:------------:|----------------------|
| I | Spec in `specs/005-…`, stories prioritized and independently testable, no technology in the spec; P1 alone is a usable increment | ✅ PASS | spec.md: 5 stories, checklist 16/16, five clarifications recorded; US1 (formatting, mentions, watchers, notifications) is usable alone |
| II | Test-first rules and services; every acceptance scenario automated; real SQL Server | ✅ PASS | R27: domain tests for the document validator and renderer (with a cross-site scripting set), mention comparison, word splitting, labels, file sniffing and notification precedence; service tests on SQL Server per story; real ClamAV and SMTP containers for the protocol risks; upgrade test; Phase 4 matrix |
| III | Server-side authorization on every read and write; encoded or sanitized user content; CSRF protection; audit of security-relevant events | ✅ PASS | R3, R13, R21, R24: documents checked against an allowlist and rendered by the server with encoding; uploads over the authenticated circuit; downloads checked on every request with `nosniff` and `sandbox`; visibility filters on every suggestion, notification, email, search and shared filter; audit of malware detections, permanent deletions and emails given up |
| IV | Append-only history for every change including links; no hard deletes in normal use; optimistic concurrency; UTC; reviewed migrations | ✅ PASS | R4, R12, R20, R22: history for attachments and labels (comments as before); removed attachments kept, permanent deletion administrator-only and audited; row versions on attachments and saved filters; six additive migrations, one per story plus the foundation |
| V | One app, one database, module boundaries; every new dependency or piece of infrastructure justified; YAGNI | ⚠️ JUSTIFIED | R1, R2, R10, R12, R14, R15: one new feature module behind one contract; files in the database; search in SQL Server with no search engine. MailKit, the TipTap editor bundle and the ClamAV daemon are justified below (Complexity Tracking) |
| VI | WCAG 2.2 AA; keyboard alternative for every drag; 360 px; feedback without losing input | ✅ PASS | R5, R25: accessible editor toolbar, shortcuts and suggestion list; "Attach files" button as the drop zone's alternative; bell, panel, search combobox and quick filters with names and states; failed saves keep the text; axe at both widths |
| — | Platform standards (performance baseline, observability, configuration, portability) | ✅ PASS | R23, R26: settings from configuration providers only; readiness reports the mail server and scanner as degraded; logs never contain text or file content; runs the same on Linux and Windows; larger seed and new load scenarios |
| — | Workflow gates (CI, Definition of Done, traceability) | ✅ PASS | quickstart.md steps 4–5, including the editor bundle check and `npm audit`; README and quickstart updated per story |

**Result**: passes. The three new dependencies are recorded in Complexity Tracking. Re-checked after the design
with the same outcome.

## Project Structure

### Documentation (this feature)

```text
specs/005-collaboration-and-search/
├── spec.md              # Feature specification (/speckit-specify, /speckit-clarify)
├── checklists/
│   └── requirements.md  # Spec quality checklist
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-services.md
│   ├── http-endpoints.md
│   ├── permissions.md   # supersedes the Phase 3 matrix; read by PermissionMatrixTests
│   └── ui-routes.md
└── tasks.md             # /speckit-tasks output
```

### Source Code (additions and changes to the Phase 3 tree)

```text
src/
├── Upms.Domain/
│   ├── Identity/User.cs, EmailChoice.cs, AuditEventType.cs   # + EmailNotifications; new audit values
│   ├── Notifications/Notification.cs, EmailMessage.cs,
│   │                 NotificationKind.cs, RecipientRules.cs  # new module (research R1, R8, R10)
│   └── Work/
│       ├── FormattedText.cs, TextFormat.cs     # new: document parsing and checks (research R3)
│       ├── WorkItem.cs, Comment.cs, WorkItemField.cs   # formatted text; new history fields
│       ├── Attachment.cs, AttachmentRules.cs   # new: states, allowlist, sniffing (research R12, R13)
│       ├── Label.cs                            # new: names and normalization (research R18)
│       ├── SearchWords.cs                      # new: word splitting (research R15)
│       └── SavedFilter.cs                      # new
├── Upms.Application/
│   ├── Identity/Contracts/IUserDirectory.cs, IContentAuditLog.cs   # recipients; content audit (new)
│   ├── Identity/AccountService.cs, ContentAuditLog.cs              # email choice
│   ├── Projects/Contracts/IProjectAccess.cs, IProjectTeam.cs       # visible project ids, member search, who can see
│   ├── Notifications/Contracts/INotificationCenter.cs              # new: the one contract for the Work module
│   ├── Notifications/NotificationCenter.cs, INotificationService.cs, NotificationService.cs,
│   │                 EmailComposer.cs, IEmailTransport.cs          # new
│   └── Work/
│       ├── WorkItemService.cs, CommentService.cs, BoardService.cs, WorkItemListService.cs   # formatted text,
│       │                                       #   events, watchers, labels on cards and rows, recent views
│       ├── IMentionService.cs, IWatcherService.cs, WatcherService.cs, MentionService.cs      # new
│       ├── IAttachmentService.cs, AttachmentService.cs, IMalwareScanner.cs                 # new
│       ├── ILabelService.cs, LabelService.cs                         # new
│       ├── ISearchService.cs, SearchService.cs, SearchIndexer.cs     # new
│       └── ISavedFilterService.cs, SavedFilterService.cs             # new
├── Upms.Infrastructure/
│   ├── Persistence/Configurations/...          # new tables and indexes; Notifications configurations
│   ├── Persistence/AttachmentContentStore.cs   # new: streaming in and out with SqlClient (research R12)
│   ├── Persistence/Migrations/*_Notifications, *_FormattedTextAndWatchers, *_Attachments,
│   │                     *_SearchAndSavedFilters, *_Labels, *_EmailOutbox.cs
│   ├── Email/MailKitEmailTransport.cs          # new (research R10)
│   ├── Malware/ClamAvScanner.cs                # new: clamd INSTREAM client (research R14)
│   └── Workers/EmailDispatchWorker.cs, AttachmentScanWorker.cs, SearchIndexWorker.cs,
│              NotificationCleanupWorker.cs     # new background workers
└── Upms.Web/
    ├── Editor/                                 # new: package.json, lock file, src/editor.ts, esbuild script
    ├── wwwroot/js/editor.js                    # new: the committed bundle
    ├── wwwroot/js/attachments.js               # new: drag-and-drop and file streaming to .NET
    ├── Endpoints/AttachmentEndpoints.cs        # new: GET /attachments/{id} and /preview
    ├── Components/Shared/RichTextEditor.razor, FormattedTextView.razor, FormattedTextRenderer.cs   # new
    ├── Components/Layout/Header.razor, SearchBox.razor, NotificationBell.razor, NotificationPanel.razor
    ├── Components/Pages/Search/SearchPage.razor, SavedFilters.razor                            # new
    ├── Components/Pages/Drawer/                # editor, WatchButton, Attachments, Labels sections
    ├── Components/Pages/Board/                 # labels on cards, quick filters in the address
    ├── Components/Pages/List/                  # labels column and filter
    └── Components/Pages/Profile/Profile.razor  # email choice

docker-compose.yml                              # + clamav and smtp4dev for development (research R23)
tools/Upms.Seed/                                # comments, labels, watchers, attachments, notifications, filters (R26)
tests/
├── Upms.Domain.Tests/                          # formatted text (with the cross-site scripting set), sniffing, words, labels
├── Upms.Application.Tests/                     # services per story, CollaborationUpgradeTests, Phase 4 matrix,
│                                               #   ExternalServices group (ClamAV, smtp4dev)
├── Upms.Web.Tests/                             # editor wrapper, bell and panel, search, attachments, labels, quick filters
├── Upms.E2E.Tests/                             # P4_US1–P4_US5 journeys + axe
├── Upms.Architecture.Tests/                    # the Notifications module in the boundary rules
└── Upms.Performance.Tests/                     # new actions in the SC-002 run
```

**Structure Decision**: one new module, **Notifications**, owns notifications, the email outbox and their
delivery (research R1). The Work module reaches it only through `Upms.Application.Notifications.Contracts`
(`INotificationCenter`). The Notifications module reads people through `IUserDirectory` and visibility through
`Upms.Application.Projects.Contracts`. Formatted text, mentions, watchers, attachments, labels, search and saved
filters belong to the Work module, which owns work items and comments. The email choice belongs to the Identity
module. `ModuleBoundaryTests` gains the new module.

## Complexity Tracking

| Addition | Why Needed | Simpler Alternative Rejected Because |
|----------|------------|-------------------------------------|
| **TipTap 3 and ProseMirror** (npm, MIT), bundled by **esbuild** from `src/Upms.Web/Editor` into a committed `wwwroot/js/editor.js`; Node.js 24 LTS at development time and in one CI job (bundle check and `npm audit`) | A WYSIWYG editor with typed shortcuts, tables with row and column editing, pasted spreadsheets and screenshots, and "@" suggestions (FR-001, FR-007, FR-023), as Jira users expect; Atlassian's own editor is built on ProseMirror | Writing a WYSIWYG editor from scratch is unsafe and far larger than this phase. A Markdown text box shows syntax, not formatting, and makes tables and mentions awkward. Loading the editor from a CDN breaks `script-src 'self'`. The .NET build stays Node-free because the bundle is committed |
| **MailKit** (NuGet, MIT) | Sending email through the company mail server with STARTTLS and authentication, with clear errors for retries (FR-049–FR-052) | `System.Net.Mail.SmtpClient` is marked by Microsoft as not recommended for new development and points to MailKit |
| **A ClamAV daemon** (`clamd`) as a configured external service; own ~80-line client for its `INSTREAM` protocol | The spec requires a malware check before any download (FR-025, SC-003) using the organization's antivirus or one deployed alongside | Windows Defender's scanning interface is Windows-only (portability). A ClamAV client package adds a dependency for a tiny protocol. Scanning in the upload request would fail uploads whenever the scanner is down |

The company mail server is required by the spec itself, not a design choice. `smtp4dev` and the ClamAV container
are development and test conveniences only.
