---

description: "Task list for Phase 4: Collaboration and Search (specs/005-collaboration-and-search)"
---

# Tasks: Collaboration and Search (Phase 4)

**Input**: Design documents from `specs/005-collaboration-and-search/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution principle II (Test-First, non-negotiable): tests for domain rules and
application services are written first and fail before implementation, and every acceptance scenario is
automated. Phase 4 test methods begin with `P4_` and the scenario they prove (for example
`P4_US1_AS11_OneNotificationPerPerson`), so they are not confused with the tests of earlier phases.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5 of this spec)
- Include exact file paths in descriptions

## Path Conventions

Per [plan.md](./plan.md): the existing tree (`src/Upms.Domain/`, `src/Upms.Application/`,
`src/Upms.Infrastructure/`, `src/Upms.Web/`, `tests/Upms.*.Tests/`, `tools/`, `docs/`), foldered by module
(Identity, Projects, Work, and the new Notifications). Contracts referenced below live in
[contracts/](./contracts/); research decisions are cited as R1–R27 of [research.md](./research.md).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Point the documentation at Phase 4, add the local services, and set up the editor bundle

- [X] T001 [P] Add a "Phase 4 documents" table (spec, plan, research, data model, contracts, tasks, quickstart of `specs/005-collaboration-and-search`) and a Phase 4 line under **Status** to `README.md`
- [X] T002 [P] Add a note at the top of `specs/004-templates-and-planning/contracts/permissions.md` that the matrix is superseded by `specs/005-collaboration-and-search/contracts/permissions.md` once Phase 4 is installed
- [X] T003 [P] Add the development services `clamav` (`clamav/clamav:stable`, port 3310, with a health check) and `smtp4dev` (`rnwood/smtp4dev`, SMTP on 2525 and its web inbox on 5025) to `docker-compose.yml`, as quickstart step 1 describes (research R23)
- [X] T004 Create the editor build in `src/Upms.Web/Editor/`: `package.json` with exact 3.x versions of `@tiptap/core`, `@tiptap/pm`, `@tiptap/starter-kit` (which includes Link in 3.x), the Table, Mention (with `@tiptap/suggestion`), Image and Placeholder extensions, plus `esbuild`, and `"engines": { "node": ">=24" }`; a committed `package-lock.json`; `build.mjs` bundling `src/editor.ts` into `../wwwroot/js/editor.js` (one minified classic script on `window.upms.editor`, no `eval`); a `.gitignore` for `node_modules`; a first `src/editor.ts` exposing `create(element, options)` and `destroy(handle)`. Exclude `Editor/**` from `src/Upms.Web/Upms.Web.csproj` content and publish, load `js/editor.js` in `src/Upms.Web/Components/App.razor`, and commit the built bundle (research R2)
- [X] T005 [P] Update `.github/workflows/ci.yml`: add an `editor` job (Node.js 24, `npm ci`, `npm run build`, `npm audit --audit-level=high`, and `git diff --exit-code src/Upms.Web/wwwroot/js/editor.js`, so the committed bundle always matches its sources); pass `--filter-not-trait "Category=ExternalServices"` to the main test step; and add an `external-services` job that runs `tests/Upms.Application.Tests` with `--filter-trait "Category=ExternalServices"` (ClamAV and smtp4dev containers), so the main job never waits on those images (plan: Complexity Tracking)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The Notifications module and its one contract, the visibility contracts every story uses, the content
audit contract, and the module boundary rule

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 [P] Extend `tests/Upms.Architecture.Tests/ModuleBoundaryTests.cs`: the modules are Identity, Projects, Work and Notifications; the Work module may use the Notifications module only through `Upms.Application.Notifications.Contracts`, and the Notifications module may use the others only through their `Contracts` namespaces (research R1)
- [X] T007 [P] Write `tests/Upms.Application.Tests/Projects/ProjectVisibilityTests.cs`: `IProjectAccess.VisibleProjectIdsAsync` returns a member's projects in every role, drops a project after removal, and returns null (all projects) for administrators; `IProjectTeam.FindMembersAsync` finds active members of any role by display name or user name, at most `take`, never deactivated people, non-members or administrators who are not members; `IProjectTeam.WhoCanSeeAsync` keeps members and administrators and drops everyone else
- [X] T008 [P] Write `tests/Upms.Application.Tests/Identity/ContentAuditLogTests.cs`: each `ContentAuditEvent` is written as the matching `AuditEventType` (`MalwareDetected`, `AttachmentPurged`, `EmailGivenUp`) with the current user (or the system) as actor, the given target and serialized details, in the caller's transaction (research R20)
- [X] T009 [P] Write `tests/Upms.Domain.Tests/Notifications/NotificationRulesTests.cs`: `RecipientRules` removes the actor, keeps one reason per person with the precedence Mentioned, then Assigned, then Commented or StatusChanged, and reports which kinds count as "aimed at me" per data-model.md's table (research R8, R11)
- [X] T010 [P] Write `tests/Upms.Application.Tests/Notifications/NotificationCenterTests.cs` and `NotificationServiceTests.cs`: `RaiseAsync` adds one row per remaining recipient to the caller's unit of work (nothing when the caller does not save), removes the actor and anyone `WhoCanSeeAsync` drops, and applies the precedence across the events of one action; `CountUnreadAsync` and `ListAsync` (newest first, 20 a page) return only the caller's notifications, leave out projects the caller can no longer access (notifications about deleted items stay listed), and show the item's current key and title and the actor's name; `MarkAsync` answers `NotFound` for someone else's notification; `MarkAllReadAsync` (FR-015–FR-022)
- [X] T011 [P] Write `tests/Upms.Application.Tests/Notifications/NotificationCleanupTests.cs`: the cleanup deletes notifications older than 90 days and keeps newer ones, using a fake time provider (FR-021)
- [X] T012 Create `src/Upms.Domain/Notifications/Notification.cs`, `NotificationKind.cs` and `RecipientRules.cs` (make T009 pass)
- [X] T013 Add `src/Upms.Infrastructure/Persistence/Configurations/Notifications/NotificationConfiguration.cs` (the indexes of data-model.md), `DbSet<Notification>` in `src/Upms.Application/Common/IAppDbContext.cs` and `src/Upms.Infrastructure/Persistence/AppDbContext.cs`, migration `Notifications` in `src/Upms.Infrastructure/Persistence/Migrations/`, and `Notification` in `tests/Upms.Architecture.Tests/EntityEncapsulationTests.cs`
- [X] T014 Add `VisibleProjectIdsAsync` to `src/Upms.Application/Projects/Contracts/IProjectAccess.cs` and `ProjectAccess.cs`, and `FindMembersAsync` and `WhoCanSeeAsync` to `src/Upms.Application/Projects/Contracts/IProjectTeam.cs` and `ProjectTeam.cs` (make T007 pass)
- [X] T015 [P] Add the three values to `src/Upms.Domain/Identity/AuditEventType.cs`, and create `src/Upms.Application/Identity/Contracts/IContentAuditLog.cs` (with `ContentAuditEvent`) and `src/Upms.Application/Identity/ContentAuditLog.cs`, like `PlanningAuditLog`; register it (make T008 pass)
- [X] T016 Create `src/Upms.Application/Notifications/Contracts/INotificationCenter.cs` (with `NotificationEvent`), `src/Upms.Application/Notifications/NotificationCenter.cs`, `INotificationService.cs` and `NotificationService.cs`; register them in `src/Upms.Application/ApplicationServiceCollectionExtensions.cs` and, operation-scoped, in the web host (make T006 and T010 pass)
- [X] T017 Create `src/Upms.Infrastructure/Workers/NotificationCleanupWorker.cs` (daily, like `RankRebalanceWorker`) and register it (make T011 pass)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Discuss work with formatting and mentions, and hear about it (Priority: P1) 🎯 MVP

**Goal**: Formatted descriptions and comments with tables, @mentions, watchers, and in-app notifications with a
bell on every screen; nobody is told about their own actions; existing text reads as before.

**Independent Test**: Amina comments on `WEB-1` with bold, a list and a link, and mentions Bilal; Bilal's bell
shows one unread notification that opens `WEB-1`; Amina watches `WEB-1`; Omar watches it too; when Bilal moves it
to Done, Amina and Omar are notified and Bilal is not (spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T018 [P] [US1] Write `tests/Upms.Domain.Tests/Work/FormattedTextTests.cs`: `FormattedText.Parse` keeps the allowlisted nodes and marks of data-model.md and drops other attributes; refuses unknown nodes, nested tables and tables over 50 rows or 20 columns (`UnsupportedFormatting`), and links other than http, https and mailto (`InvalidLink`); forces cell spans to 1; refuses over 32,000 characters of plain text or over 512 KB (`TextTooLong`); `PlainText` puts line breaks between blocks and table cells; `MentionIds` and `ImageAttachmentIds` list what the document holds; `FromPlainText` turns old text into paragraphs (FR-001–FR-004)
- [X] T019 [P] [US1] Write `tests/Upms.Web.Tests/Shared/FormattedTextRendererTests.cs` and `FormattedTextXssTests.cs`: every node and mark renders to the expected HTML; text is encoded; links get `rel="noopener noreferrer nofollow"` and `target="_blank"`; header rows render `<th scope="col">`; mentions show the person's current display name, or the stored name when unknown; plain text renders as `PlainText` does; a data file of at least 100 cross-site scripting samples (`tests/Upms.Web.Tests/Shared/xss-samples.json`: documents with script text, event attributes, `javascript:` and `data:` links, unknown nodes and marks, and HTML in text) is either refused by `Parse` or renders to HTML that, parsed with AngleSharp, has no script element, no event attribute, no non-http(s)/mailto link and no external source (FR-003, SC-007)
- [X] T020 [P] [US1] Extend `tests/Upms.Domain.Tests/Work/WorkItemEditingTests.cs` (comments and descriptions): a comment and a description take formatted text and keep its format and plain text; editing keeps the Phase 1 rules (only the author; deleted comments cannot be edited); the description's history rows hold plain text (FR-006)
- [X] T021 [P] [US1] Write `tests/Upms.Application.Tests/Work/FormattedDescriptionTests.cs`: `P4_US1_AS2` a description saved as a document with a heading, a list, a code block and a table, returned as `RichText`, recorded in history as plain text; refused documents (`UnsupportedFormatting`, `InvalidLink`, `TextTooLong`) change nothing; a stale version returns `Conflict`; mention ids of non-members become plain text; `P4_US1_AS7` saving again notifies only people mentioned for the first time; the List view's word filter searches `DescriptionText`, never the JSON (FR-001–FR-004, FR-007–FR-010)
- [X] T022 [P] [US1] Write `tests/Upms.Application.Tests/Notifications/WorkNotificationTests.cs`:
  - `P4_US1_AS1` posting a comment makes its author a watcher;
  - `P4_US1_AS6` a mention notifies the mentioned person;
  - `P4_US1_AS9` watchers hear about comments, and about status changes from the board, the drawer, "mark done" and column deletion, but the actor does not; deleting a comment notifies no one;
  - `P4_US1_AS10` assigning someone else notifies them and makes them watch; assigning oneself does not notify;
  - `P4_US1_AS11` a watcher who is mentioned gets one notification;
  - `P4_US1_AS14` someone removed from the project is no longer notified and no longer sees earlier notifications about it; their watch is kept, and once added back they are notified again (FR-014);
  - `P4_US1_AS15` a gate request notifies each approver who may decide (not an excluded requester), and a decision notifies the requester (FR-011–FR-017)
- [X] T023 [P] [US1] Write `tests/Upms.Application.Tests/Work/WatcherServiceTests.cs` and `MentionServiceTests.cs`: `P4_US1_AS8` the creator, assignee and commenters watch automatically; "Watch" and "Stop watching" are idempotent for anyone who can see the item; the count and list; Project Admins and administrators add and remove active members (`NotAMember` otherwise, `Forbidden` for Members and Viewers); `P4_US1_AS5` mention suggestions list active members of any role by display name or user name, at most 10, need Contribute, and answer `NotFound` to non-members (FR-008, FR-011–FR-013)
- [X] T024 [P] [US1] Write `tests/Upms.Application.Tests/Work/CollaborationUpgradeTests.cs` (own database): migrate to the last Phase 3 migration, insert pilot-style data with SQL (descriptions and comments with line breaks and links, a deleted comment, assignees, commenters), apply the Phase 4 migrations, then assert `P4_US1_AS3`: formats are plain, `DescriptionText` and `BodyText` equal the old text, and each item's creator, assignee and commenters watch it (FR-004, SC-008). Later stories extend this test
- [ ] T025 [P] [US1] Update `tests/Upms.Application.Tests/Security/PermissionMatrixTests.cs` to read `specs/005-collaboration-and-search/contracts/permissions.md`; the world gains watchers and notifications; operations for the rows that exist after US1 ("Find people to mention", "Watch and stop watching…", "Add and remove other people as watchers", "See, open and mark one's own notifications", and formatted text in the description and comment rows); the attachment, search, saved filter, label and email-choice rows are listed in explicit `PendingUntilUs2` to `PendingUntilUs5` sets reported as skipped until T046, T072, T082 and T096 fill them (SC-004)
- [X] T026 [P] [US1] Write `tests/Upms.Web.Tests/Shared/RichTextEditorTests.cs` (JavaScript interop mocked): the component creates the editor with the document, or with old plain text as paragraphs; the toolbar and editable area are labelled; "Save" sends the document's JSON; a refused save shows the reason and keeps the text; "@" suggestions call `IMentionService`; `FormattedTextView` shows formatted text read-only for Viewers (FR-001, FR-005, FR-007)
- [X] T027 [P] [US1] Write `tests/Upms.Web.Tests/Layout/NotificationBellTests.cs`: the button is named "Notifications, N unread"; the count refreshes every 30 seconds and when the panel opens or closes; the panel lists newest first with who, what, which item and when; opening one marks it read and navigates to `?task={KEY}`; mark read or unread; "Mark all as read"; "Show more"; Escape closes the panel and returns focus to the bell (FR-018, FR-019)
- [X] T028 [P] [US1] Write `tests/Upms.Web.Tests/Drawer/WatchersTests.cs` and extend `tests/Upms.Web.Tests/Drawer/TaskDrawerTests.cs`: the "Watch"/"Stop watching" toggle with the count, the watcher list, "Add watcher" and "Remove" for Project Admins; the description and comments use `RichTextEditor` and show `FormattedTextView`; history lines are unchanged (FR-012, FR-013)
- [ ] T029 [P] [US1] Write `tests/Upms.E2E.Tests/P4_US1_DiscussAndNotifyTests.cs`: the US1 Independent Test end to end with real typing (Ctrl+B, "- " for a list, Ctrl+K for a link, "@bi" for a mention, a table with a row added from the table menu), the bell in a second browser, watchers and the status-change notification; axe scans of the editor, the suggestion list and the notification panel at 1280 px and 360 px

### Implementation for User Story 1

- [X] T030 [US1] Create `src/Upms.Domain/Work/FormattedText.cs` and `TextFormat.cs` (parse, allowlist, limits, plain text, mention and image ids, `FromPlainText`) (make T018 pass)
- [X] T031 [US1] Extend `src/Upms.Domain/Work/Comment.cs` (`BodyFormat`, `BodyText`, formatted `Create` and `Edit`) and `src/Upms.Domain/Work/WorkItem.cs` (`DescriptionFormat`, `DescriptionText`, a formatted `Describe` with plain-text history); create `src/Upms.Domain/Work/Watch.cs` (make T020 pass)
- [X] T032 [US1] Update `src/Upms.Infrastructure/Persistence/Configurations/Work/WorkItemConfiguration.cs` and `CommentConfiguration.cs` (the new columns), add `WatchConfiguration.cs` (key and index of data-model.md), and create migration `FormattedTextAndWatchers`: the columns with `DescriptionText` and `BodyText` copied from the old text, and `WorkItemWatchers` backfilled from creators, assignees and commenters; add `Watch` to `EntityEncapsulationTests` (make T024 pass)
- [X] T033 [US1] Add `UnsupportedFormatting`, `InvalidLink` and `NotAMember` to `src/Upms.Application/Common/Results/ErrorCodes.cs`; add `RichText` to `src/Upms.Application/Work/IWorkItemService.cs`; make `WorkItemEdit.Description` carry a document; `WorkItemDetails.Description` becomes `RichText` and gains `Watchers`; `CommentView.Body` becomes `RichText`
- [X] T034 [US1] Update `src/Upms.Application/Work/WorkItemService.cs`, `WorkItemReads.cs` and `CommentService.cs`: parse documents, keep mentions of active members only, record history, add watchers (creator, commenter, new assignee), and raise `Mentioned`, `Commented` and `Assigned` through `INotificationCenter`; make the List view's word filter in `WorkItemListService.cs` read `DescriptionText` (make T021 pass)
- [X] T035 [US1] Raise `StatusChanged` for watchers on every status path: `src/Upms.Application/Work/BoardService.cs` (moves), `WorkItemService.cs` (drawer status and "mark done") and `WorkItemStatusMover.cs` (column deletion); raise `ApprovalRequested` and `GateDecided` in `src/Upms.Application/Work/StageGateService.cs` (make T022 pass)
- [X] T036 [US1] Create `src/Upms.Application/Work/IWatcherService.cs`, `WatcherService.cs`, `IMentionService.cs` and `MentionService.cs`, and register them, operation-scoped in the web host too (make T023 pass)
- [X] T037 [US1] Create `src/Upms.Web/Components/Shared/FormattedTextRenderer.cs` and `FormattedTextView.razor` (documents rendered by the server, plain text through `PlainText`) (make T019 pass)
- [X] T038 [US1] Implement the editor in `src/Upms.Web/Editor/src/editor.ts` and rebuild the bundle: StarterKit, Link, Table with its menu, Mention with a suggestion callback to .NET, the toolbar as one tab stop with arrow keys, the shortcuts and typed shortcuts of research R5, Tab between table cells, Escape then Tab to leave, and paste that keeps tables and drops other formatting; create `src/Upms.Web/Components/Shared/RichTextEditor.razor` wrapping it (make T026 pass)
- [X] T039 [US1] Use `RichTextEditor` and `FormattedTextView` for the description and comments in `src/Upms.Web/Components/Pages/Drawer/TaskDrawer.razor` and its comment list, and add `src/Upms.Web/Components/Pages/Drawer/WatchersSection.razor` (make T028 pass)
- [X] T040 [US1] Create `src/Upms.Web/Components/Layout/NotificationBell.razor` and `NotificationPanel.razor` (30-second `PeriodicTimer`, notification sentences of contracts/ui-routes.md) and add the bell to `Header.razor` (make T027 pass)
- [X] T041 [US1] Seed watchers and notifications in `tools/Upms.Seed/Seeder.cs`, and add the notification panel, the unread count and posting a comment with a mention to `tests/Upms.Performance.Tests/Sc002LoadTests.cs` (research R26)
- [ ] T042 [US1] Make T025 and T029 pass end to end, update the Phase 1–3 tests that pass plain descriptions or comments through the changed signatures, and add formatted text, mentions, watchers and notifications to the Phase 4 line of `README.md`

**Checkpoint**: User Story 1 is fully functional and testable on its own. This is the MVP: teams discuss work with
formatting and mentions, and hear about it.

---

## Phase 4: User Story 2 - Attach files and screenshots to work items (Priority: P2)

**Goal**: Attachments up to 25 MB of the allowed types, including pasted screenshots, checked for malware before
anyone can download them, with previews, removal and permanent deletion.

**Independent Test**: Amina attaches a 2 MB screenshot and pastes another into a comment; both show "being
checked", then previews; Bilal (Viewer) downloads them; a 30 MB file and a `.exe` are refused; the antivirus test
file is blocked, Amina is told, and the audit log records it (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T043 [P] [US2] Write `tests/Upms.Domain.Tests/Work/AttachmentRulesTests.cs`: the extensions of FR-024 and no others (`FileTypeNotAllowed`); 25 MB (`FileTooLarge`); file names lose paths and control characters and are cut to 255; the first bytes match PNG, JPEG, GIF, WebP, PDF, Office Open XML (ZIP with `[Content_Types].xml`) and legacy Office (compound file) types, and text and technical files are UTF-8 without NUL bytes (`FileContentMismatch` otherwise); images keep their content type and everything else is `application/octet-stream`; the state changes of data-model.md (FR-023–FR-029)
- [X] T044 [P] [US2] Write `tests/Upms.Application.Tests/Work/AttachmentServiceTests.cs`:
  - `P4_US2_AS1` `AddAsync` streams a file into the database as "being checked" and records `AttachmentAdded`;
  - `P4_US2_AS4` refused files store nothing;
  - `P4_US2_AS6` removal by the uploader, a Project Admin or an administrator, with history and bytes kept; `Forbidden` for others, including an uploader who became a Viewer;
  - `P4_US2_AS7` purge by administrators only, with the bytes deleted and `AttachmentPurged` audited;
  - `P4_US2_AS8` Viewers list without `CanRemove` and cannot add;
  - `P4_US2_AS9` `OpenAsync` answers `NotFound` unless the file passed, is neither removed nor purged, and the caller can see the item now;
  - `P4_US2_AS10` a deleted item's files are unreachable and come back when it is restored;
  - images in formatted text must be attachments of the same item (FR-023–FR-031)
- [X] T045 [P] [US2] Write `tests/Upms.Application.Tests/Work/AttachmentScanTests.cs` (fake scanner): the worker claims files "being checked" without two instances taking the same one; clean files pass; infected ones are blocked and quarantined (content kept, never served), `P4_US2_AS5` the uploader notified (`UploadBlocked`) and `MalwareDetected` audited; when the scanner is unavailable, files stay "being checked" and are retried after 1, 5 and then 15 minutes (FR-025, SC-003)
- [X] T046 [P] [US2] Fill the `PendingUntilUs2` rows of `PermissionMatrixTests` (attach, download and preview, remove, delete permanently), with downloads going through the endpoints (SC-004)
- [X] T047 [P] [US2] Write `tests/Upms.Application.Tests/External/ClamAvScannerTests.cs` (trait `Category=ExternalServices`, run by the CI `external-services` job only; a `clamav/clamav:stable` Testcontainer): the standard antivirus test file is found, a clean text file passes, and a stopped daemon reports unavailable (research R14)
- [X] T048 [P] [US2] Write `tests/Upms.Web.Tests/Security/AttachmentEndpointsTests.cs` (host test): `GET /attachments/{id}` and `/preview` return the file with the headers of contracts/http-endpoints.md; the preview serves only images; every refusal is a 404 with no details; anonymous requests get 401; the rate limit applies (FR-027)
- [X] T049 [P] [US2] Write `tests/Upms.Web.Tests/Drawer/AttachmentsSectionTests.cs` and extend `FormattedTextRendererTests.cs`: the list with name, size, uploader, time and status; previews for images, which can be enlarged in a dialog, and "Download" for the rest; "Remove" and "Delete permanently" by rights, with confirmations; the "Attach files" button; progress and refusals announced; images in formatted text point to the preview address once checked, and show "being checked" or "removed" otherwise (FR-023–FR-026)
- [ ] T050 [P] [US2] Write `tests/Upms.E2E.Tests/P4_US2_AttachmentsTests.cs` (the app with a fake scanner that blocks the antivirus test file): the US2 Independent Test end to end with the file chooser, drag-and-drop and a pasted screenshot; axe scans of the attachments list and a preview at 1280 px and 360 px

### Implementation for User Story 2

- [X] T051 [US2] Create `src/Upms.Domain/Work/Attachment.cs`, `AttachmentCheckStatus.cs` and `AttachmentRules.cs`, and add `AttachmentAdded` and `AttachmentRemoved` to `WorkItemField.cs` (make T043 pass)
- [X] T052 [US2] Add `AttachmentConfiguration.cs` and `AttachmentContentConfiguration.cs`, migration `Attachments`, and `src/Upms.Infrastructure/Persistence/AttachmentContentStore.cs` streaming content in and out with SqlClient (`SequentialAccess`); add `Attachment` to `EntityEncapsulationTests`
- [X] T053 [US2] Add `FileTypeNotAllowed`, `FileTooLarge`, `FileContentMismatch` and `AttachmentNotAvailable` to `ErrorCodes.cs`; create `src/Upms.Application/Work/IAttachmentService.cs`, `AttachmentService.cs` and `IMalwareScanner.cs`; add `Attachments` to `WorkItemDetails`; check image ids in formatted text; register the service (make T044 pass)
- [X] T054 [US2] Create `src/Upms.Infrastructure/Malware/ClamAvScanner.cs` (the `INSTREAM` protocol, `Malware:ClamAv` options), `src/Upms.Infrastructure/Workers/AttachmentScanWorker.cs`, and a degraded readiness entry for the scanner; log a warning when no scanner is configured (make T045 and T047 pass)
- [X] T055 [US2] Create `src/Upms.Web/Endpoints/AttachmentEndpoints.cs` with the two GET endpoints, their headers and rate limit, and map them in `Program.cs` (make T048 pass)
- [X] T056 [US2] Create `src/Upms.Web/wwwroot/js/attachments.js` (drag-and-drop on the drawer; files passed to .NET as `IJSStreamReference`) and `src/Upms.Web/Components/Pages/Drawer/AttachmentsSection.razor`; render images in `FormattedTextRenderer.cs`; add pasted and dropped images to `editor.ts` (upload through .NET, then insert the image node) and rebuild the bundle (make T049 pass)
- [ ] T057 [US2] Register a fake scanner in `tests/Upms.E2E.Tests/Fixtures/AppFixture.cs`, seed small pre-checked attachments in `tools/Upms.Seed/Seeder.cs`, make T046 and T050 pass end to end, and add attachments to the Phase 4 line of `README.md`

**Checkpoint**: User Stories 1 and 2 both work independently

---

## Phase 5: User Story 3 - Find any work item fast (Priority: P3)

**Goal**: A search box on every screen with key jumps and recent items, a search page across every accessible
project, and saved filters that can be shared with project members.

**Independent Test**: `MOB-12` opens from the search box; "refund" finds items in `WEB` and `MOB` but nothing in
`PAY`; filtered to status type "To do" and unassigned, saved as "Unassigned refunds", and run again later with
current results (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T058 [P] [US3] Write `tests/Upms.Domain.Tests/Work/SearchWordsTests.cs` and `SavedFilterTests.cs`: words are lower-cased (invariant culture), split on anything that is not a letter or digit, kept from 2 to 40 characters, and distinct; at most 10 search words; key-like text (`[A-Za-z0-9]{2,10}-\d+`) is recognized regardless of case; filter names are 1–80 characters and unique per owner by normalized name (research R15, R17)
- [X] T059 [P] [US3] Write `tests/Upms.Application.Tests/Work/SearchIndexTests.cs`: an item's words are rewritten in the same transaction when its title, description or comments change, and a deleted comment's words go; `SearchIndexWorker` fills items whose `SearchIndexedAt` is null, 500 at a time (research R15)
- [X] T060 [P] [US3] Write `tests/Upms.Application.Tests/Work/SearchServiceTests.cs`:
  - `P4_US3_AS1` `ResolveKeyAsync` regardless of case;
  - `P4_US3_AS2` an unknown key and a hidden key get the same `NotFound`;
  - `P4_US3_AS3` recent items: the 10 latest the caller can still see, recorded when the drawer opens and trimmed to 50;
  - `P4_US3_AS4` words match the start of words in titles, descriptions and live comments, across accessible projects, newest update first, 50 a page, with the count; several words must all match;
  - `P4_US3_AS5` the filters of `SearchQuery`;
  - `P4_US3_AS6` each sort;
  - `P4_US3_AS7` projects the caller cannot access and deleted items never appear or count, and administrators search every project (FR-032–FR-038)
- [X] T061 [P] [US3] Write `tests/Upms.Application.Tests/Work/SavedFilterServiceTests.cs`:
  - `P4_US3_AS8` save, list, rename, update and delete; `FilterNameTaken`; `Conflict` on a stale version; only the owner changes a filter;
  - `P4_US3_AS10` sharing with projects the owner is a member of (any project for administrators; `NotAMember` otherwise); members find it under "shared with me" with the owner's name and can run it but not change it;
  - `P4_US3_AS11` unsharing, the owner leaving the project and the owner's deactivation each end the sharing;
  - running any filter applies the runner's visibility and reports values that no longer apply as unavailable (FR-039–FR-041)
- [X] T062 [P] [US3] Write `tests/Upms.Web.Tests/Layout/SearchBoxTests.cs`: the combobox pattern; recent items when focused and empty; a key plus Enter opens the item or shows "No work item KEY was found."; words plus Enter open `/search?q=…` (FR-032, FR-033)
- [X] T063 [P] [US3] Write `tests/Upms.Web.Tests/Search/SearchPageTests.cs` and `SearchStateTests.cs`: filters as removable chips; the query string of contracts/ui-routes.md round-trips through `SearchState`; sorting and paging; a new filter builds on the last one asked for (`SlowNavigationManager`); `P4_US3_AS9` a result opens in the drawer and closing it returns focus to the result; the saved-filter panel ("My filters", "Shared with me", save, rename, update, share, delete) and unavailable values (FR-034–FR-041)
- [ ] T064 [P] [US3] Write `tests/Upms.E2E.Tests/P4_US3_SearchTests.cs`: the US3 Independent Test end to end, plus sharing a filter with `WEB`; axe scans of the search box with recent items, the search page and the saved-filter panel at 1280 px and 360 px

### Implementation for User Story 3

- [X] T065 [US3] Create `src/Upms.Domain/Work/SearchWords.cs` and `SavedFilter.cs` (make T058 pass)
- [X] T066 [US3] Add the configurations for `SearchTerms`, `SavedFilters`, `SavedFilterShares` and `RecentViews`, `WorkItems.SearchIndexedAt`, and migration `SearchAndSavedFilters`; add `SavedFilter` to `EntityEncapsulationTests`
- [X] T067 [US3] Create `src/Upms.Application/Work/SearchIndexer.cs` and call it from `WorkItemService.cs` and `CommentService.cs`; create `src/Upms.Infrastructure/Workers/SearchIndexWorker.cs` (make T059 pass)
- [X] T068 [US3] Create `src/Upms.Application/Work/ISearchService.cs` and `SearchService.cs`, and record recent views in `WorkItemService.GetAsync`; register it (make T060 pass)
- [X] T069 [US3] Add `FilterNameTaken` to `ErrorCodes.cs`; create `src/Upms.Application/Work/ISavedFilterService.cs` and `SavedFilterService.cs`; register it (make T061 pass)
- [X] T070 [US3] Create `src/Upms.Web/Components/Layout/SearchBox.razor` and add it to `Header.razor` (make T062 pass)
- [X] T071 [US3] Create `src/Upms.Web/Components/Pages/Search/SearchPage.razor`, `SearchState.cs` and `SavedFilters.razor` (make T063 pass)
- [ ] T072 [US3] Fill the search and saved-filter rows (`PendingUntilUs3`) of `PermissionMatrixTests`; extend `CollaborationUpgradeTests` (existing items are found once `SearchIndexWorker` has run); seed 2,000,000 comments with realistic words, search terms and saved filters with shares in `tools/Upms.Seed/Seeder.cs`; add key jumps, word searches (as a member and as an administrator) and saved-filter runs to `Sc002LoadTests.cs`; make T064 pass end to end; add search and saved filters to the Phase 4 line of `README.md`

**Checkpoint**: User Stories 1–3 work independently

---

## Phase 6: User Story 4 - Tag work with labels and filter boards quickly (Priority: P4)

**Goal**: Shared, case-insensitive labels with suggestions from visible projects, shown on cards, rows and results;
label filters in the list and search; board quick filters by label, type and epic or stage, kept in the address.

**Independent Test**: `launch` on `WEB-1` is suggested on `MOB-3`; both are found by the label; the `MOB` board's
quick filters for Bug and the epic "Card refunds" leave only matching cards with column counts (spec US4).

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T073 [P] [US4] Write `tests/Upms.Domain.Tests/Work/LabelTests.cs`: names of 1–50 characters with no whitespace or control characters (`InvalidLabel`); normalization ignores case (invariant culture); at most 20 labels per item (`TooManyLabels`) (FR-042, FR-043)
- [X] T074 [P] [US4] Write `tests/Upms.Application.Tests/Work/LabelServiceTests.cs`:
  - `P4_US4_AS1` adding a label;
  - `P4_US4_AS2` suggestions come only from labels used on live items in projects the caller can see, matching the start of the name, at most 10;
  - `P4_US4_AS3` a label in another case reuses the first spelling;
  - `P4_US4_AS5` removal with history;
  - `P4_US4_AS8` Viewers get `Forbidden`;
  - `InvalidLabel` and `TooManyLabels` (FR-042–FR-044, FR-048)
- [X] T075 [P] [US4] Extend `tests/Upms.Application.Tests/Work/WorkItemListServiceTests.cs`, `BoardQueryTests.cs` and `SearchServiceTests.cs`: `P4_US4_AS4` cards carry labels and their container, and rows and results carry labels; `P4_US4_AS6` the list and search filter by one or more labels (items with any of them) (FR-045, FR-046)
- [X] T076 [P] [US4] Write `tests/Upms.Web.Tests/Drawer/LabelsSectionTests.cs` and `tests/Upms.Web.Tests/Board/BoardQuickFilterTests.cs`, and extend `tests/Upms.Web.Tests/List/ListStateTests.cs` and `ProjectListViewTests.cs`:
  - the drawer's label combobox with suggestions, chips named "Remove label X", and read-only labels for Viewers;
  - `P4_US4_AS7` the board's quick filters as toggle buttons with `aria-pressed` (label, type, and epic, or stage on Stage-gate boards) combine with "Only my tasks" and the assignee filter, show "matching of total" per column, and round-trip through the address with the assignee filter;
  - the list's labels column and label filter (FR-045–FR-047)
- [ ] T077 [P] [US4] Write `tests/Upms.E2E.Tests/P4_US4_LabelsTests.cs`: the US4 Independent Test end to end; axe scans of the label combobox and a quick-filtered board at 1280 px and 360 px

### Implementation for User Story 4

- [X] T078 [US4] Create `src/Upms.Domain/Work/Label.cs` and add `LabelAdded` and `LabelRemoved` to `WorkItemField.cs` (make T073 pass)
- [X] T079 [US4] Add the configurations for `Labels` and `WorkItemLabels` and migration `Labels`; add `Label` to `EntityEncapsulationTests`
- [X] T080 [US4] Add `InvalidLabel` and `TooManyLabels` to `ErrorCodes.cs`; create `src/Upms.Application/Work/ILabelService.cs` and `LabelService.cs`; add `Labels` to `WorkItemDetails`, `CardView` (with `Container`), `WorkItemRow`, `SearchResultRow`, `WorkItemListQuery` and `SearchQuery`; register it (make T074 and T075 pass)
- [X] T081 [US4] Create `src/Upms.Web/Components/Pages/Drawer/LabelsSection.razor`; show labels on `src/Upms.Web/Components/Pages/Board/TaskCard.razor`, list rows and search results; add the quick filters to `BoardPage.razor` and move the assignee filter into the address; add the label column and filter to `ProjectListView.razor` and `ListState.cs`, and the label filter to `SearchPage.razor` (make T076 pass)
- [ ] T082 [US4] Fill the label rows (`PendingUntilUs4`) of `PermissionMatrixTests`; seed labels on 30% of items (about 200 labels) in `tools/Upms.Seed/Seeder.cs`; measure board loads with quick filters in `Sc002LoadTests.cs`; make T077 pass end to end; add labels and quick filters to the Phase 4 line of `README.md`

**Checkpoint**: User Stories 1–4 work independently

---

## Phase 7: User Story 5 - Get emails about the work that matters to you (Priority: P5)

**Goal**: Email through the company mail server for the events each person chose (all, only those aimed at them,
or none), never blocking the action, retried for 24 hours, without the item's text.

**Independent Test**: With the default choice, Bilal gets emails for an assignment and a mention but not for a
comment on a watched item; Omar ("All") also gets that one; Farah ("None") gets none; with the mail server stopped,
the actions complete at once and the emails arrive later (spec US5).

### Tests for User Story 5 (write first, must fail) ⚠️

- [X] T083 [P] [US5] Write `tests/Upms.Domain.Tests/Notifications/EmailRulesTests.cs`: which kinds email for each choice (data-model.md's table); the retry schedule (1, 2, 5, 10, 20, then every 30 minutes) and giving up 24 hours after creation; cancelling for deactivated recipients (FR-049, FR-052)
- [ ] T084 [P] [US5] Write `tests/Upms.Application.Tests/Notifications/EmailOutboxTests.cs`:
  - `P4_US5_AS1`–`AS3` assignments, mentions and approval requests queue an email for the default choice;
  - `P4_US5_AS4` "All" also queues comments and status changes on watched items;
  - `P4_US5_AS6` "None" queues nothing;
  - `P4_US5_AS7` one's own actions queue nothing;
  - nothing is queued when email is not configured;
  - `P4_US5_AS9` deactivated recipients are cancelled when sending (FR-049–FR-051)
- [ ] T085 [P] [US5] Write `tests/Upms.Application.Tests/Notifications/EmailDispatchTests.cs` (fake transport):
  - the worker sends due messages with content built at send time, to the address on the account at that moment;
  - `EmailComposer`'s subject and body carry the project, key and title (or stage and gate), what happened, who
    did it and the link, are HTML-encoded, and never contain description, comment text or attachments (FR-050);
  - failures are retried on the schedule;
  - `P4_US5_AS8` a message is given up after 24 hours, with `EmailGivenUp` audited;
  - two worker instances never send the same message;
  - the triggering action never waits for the mail server (FR-052, SC-006)
- [ ] T086 [P] [US5] Write `tests/Upms.Application.Tests/Identity/EmailChoiceTests.cs` and create `tests/Upms.Web.Tests/Profile/ProfileTests.cs`: `SetEmailChoiceAsync` changes only the caller's choice; the profile shows the three choices as a radio group with the current one selected (FR-051)
- [ ] T087 [P] [US5] Write `tests/Upms.Application.Tests/External/SmtpDeliveryTests.cs` (trait `Category=ExternalServices`, run by the CI `external-services` job only; an `rnwood/smtp4dev` Testcontainer): an email arrives with the right sender, subject and link; while the server is stopped the message is retried, and it is delivered once the server is back
- [ ] T088 [P] [US5] Write `tests/Upms.E2E.Tests/P4_US5_EmailTests.cs` (an smtp4dev container in the E2E fixture, read through its API): the US5 Independent Test end to end; `P4_US5_AS10` the email's link opens the item after sign-in; an axe scan of the profile at 1280 px and 360 px

### Implementation for User Story 5

- [X] T089 [US5] Create `src/Upms.Domain/Identity/EmailChoice.cs`, add `EmailNotifications` to `User.cs`, and create `src/Upms.Domain/Notifications/EmailMessage.cs` with the retry schedule and cancellation (make T083 pass)
- [ ] T090 [US5] Add `EmailMessageConfiguration.cs`, the `AspNetUsers.EmailNotifications` column (default `AimedAtMe`), migration `EmailOutbox`, and `EmailMessage` to `EntityEncapsulationTests`
- [ ] T091 [US5] Add `GetRecipientsAsync` to `src/Upms.Application/Identity/Contracts/IUserDirectory.cs` and `UserDirectory.cs`, `SetEmailChoiceAsync` to `IAccountService.cs` and `AccountService.cs`; create `src/Upms.Application/Notifications/EmailComposer.cs` and `IEmailTransport.cs`; make `NotificationCenter.cs` queue emails by choice when email is configured (make T084 and T086's service part pass)
- [ ] T092 [US5] Add MailKit to `Directory.Packages.props` and `src/Upms.Infrastructure/Upms.Infrastructure.csproj`; create `src/Upms.Infrastructure/Email/MailKitEmailTransport.cs` (`Email:Smtp` options), `src/Upms.Infrastructure/Workers/EmailDispatchWorker.cs`, a degraded readiness entry for the mail server, and a startup warning when email is not configured (make T085 and T087 pass)
- [ ] T093 [US5] Add the email choice to `src/Upms.Web/Components/Pages/Profile/Profile.razor` (make T086's page part pass)
- [ ] T094 [US5] Add an smtp4dev container and the email settings to `tests/Upms.E2E.Tests/Fixtures/AppFixture.cs`, and make T088 pass end to end
- [ ] T095 [US5] Extend `CollaborationUpgradeTests` (every account's choice is `AimedAtMe`); seed email choices and a few pending emails in `tools/Upms.Seed/Seeder.cs`
- [ ] T096 [US5] Fill the email-choice row (`PendingUntilUs5`) of `PermissionMatrixTests`, and add email to the Phase 4 line of `README.md`

**Checkpoint**: All user stories are independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T097 [P] Extend `tests/Upms.E2E.Tests/ResponsiveAndKeyboardTests.cs` to the editor (toolbar, table menu, suggestions), the bell and panel, the search box and page, attachments, labels and quick filters: usable at 360 px, and each story's main path completes with the keyboard only (FR-056, SC-009)
- [ ] T098 Run the SC-002 suite with every Phase 1–4 action at 300 users, about 500,000 work items and 2,000,000 comments, tune indexes until p95 ≤ 1 s, and record the results in `specs/005-collaboration-and-search/validation.md` (SC-002)
- [X] T099 [P] Review the Phase 4 changes against OWASP ASVS Level 2 in `docs/security/phase4-asvs-review.md` (the document validator and renderer with the cross-site scripting set; upload type sniffing and limits; download headers and per-request access checks; no leaks through mention and label suggestions, notifications, emails, search, recent items or shared filters; email header injection; the scanner failing closed; secrets for the mail server), and fix findings (constitution III)
- [X] T100 [P] Add a "Phase 4 screens" section (editor and table menu, mention suggestions, bell and panel, attachments and previews, search box and page, saved filters, labels, quick filters, email choice) to the manual screen-reader script in `docs/accessibility/phase1-screen-reader-review.md`
- [X] T101 [P] Document the new settings, services and storage in `docs/operations/deployment.md` and `docs/operations/backup-restore.md` (mail server and ClamAV settings as secrets, the database growth from attachments and search terms, the degraded readiness entries)
- [X] T102 [P] Update `README.md` (status, features, test commands including the ExternalServices group and the editor build), `CLAUDE.md` (Phase 4 built; Phase 5 next; the Notifications module, MailKit, the editor bundle and ClamAV in "Stack and layout") and `docs/pilot/phase1-pilot-plan.md` with how the pilot measures SC-010 (a work item found within 30 seconds) and SC-011 (80% rating collaboration easy)
- [ ] T103 Run every step of `specs/005-collaboration-and-search/quickstart.md`, including the upgrade of a Phase 3 database, and record the results in `specs/005-collaboration-and-search/validation.md`
- [ ] T104 Do the manual screen-reader pass (NVDA with Edge) on the Phase 4 screens and fix findings (needs a person; constitution VI)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - US1 comes first: formatted text, watchers and the notification events underlie the rest
  - US2, US3 and US4 build on US1 and can then proceed in parallel
  - US5 builds on the notification events of US1 (and sends the US2 blocked-upload event once US2 is in)
- **Polish (Final Phase)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on other stories.
- **User Story 2 (P2)**: Needs US1 (formatted text for pasted images; the notification center for blocked uploads).
- **User Story 3 (P3)**: Needs US1 (plain text of descriptions and comments for the search words). Independent of US2.
- **User Story 4 (P4)**: Needs US1 only; its label filter on the search page needs US3.
- **User Story 5 (P5)**: Needs US1 (the notification events). Independent of US2–US4.

```text
Setup → Foundational → US1 ─┬─ US2 ─┐
                            ├─ US3 ─┼─ Polish
                            ├─ US4 ─┤   (US4's search label filter after US3)
                            └─ US5 ─┘
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution II)
- Domain before persistence, persistence before services, services before screens
- The story's permission rows and end-to-end test pass last
- Commit after each task or logical group; push after each story's checkpoint

### Parallel Opportunities

- T001, T002, T003 and T005 in parallel; T004 on its own (it creates the editor build)
- T006–T011 (foundational tests) in parallel, then T012–T017
- All test tasks of a story marked [P] in parallel (different files)
- After US1: US2, US3, US4 and US5 in parallel (different domain types, services, pages and tests)
- Polish: T097, T099, T100, T101 and T102 in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first, together:
Task: "T018 FormattedTextTests (domain)"
Task: "T019 FormattedTextRendererTests and FormattedTextXssTests (web)"
Task: "T021 FormattedDescriptionTests"
Task: "T022 WorkNotificationTests"
Task: "T023 WatcherServiceTests and MentionServiceTests"
Task: "T024 CollaborationUpgradeTests"
Task: "T026 RichTextEditorTests (bUnit)"
Task: "T027 NotificationBellTests (bUnit)"

# Then independent implementation pieces:
Task: "T030 FormattedText (domain)"
Task: "T037 FormattedTextRenderer and FormattedTextView"
Task: "T038 editor.ts and RichTextEditor"
```

## Parallel Example: User Stories 2–5

```bash
# After the US1 checkpoint, several people can work at once:
Developer A: T043–T050 (tests) → T051 → T052 → T053 → T054 → T055 → T056 → T057   (attachments)
Developer B: T058–T064 (tests) → T065 → T066 → T067 → T068 → T069 → T070 → T071 → T072   (search)
Developer C: T073–T077 (tests) → T078 → T079 → T080 → T081 → T082   (labels; the search filter after B)
Developer D: T083–T088 (tests) → T089 → T090 → T091 → T092 → T093 → T094 → T095 → T096   (email)
# Several touch ErrorCodes.cs, WorkItemService.cs, TaskDrawer.razor, Header.razor and the seeder: merge with care.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup and Phase 2: Foundational
2. Complete Phase 3: User Story 1 (formatted text, mentions, watchers, notifications, the upgrade)
3. **STOP and VALIDATE**: run the US1 tests, the cross-site scripting set and the upgrade test; teams can now
   discuss work with formatting and mentions and hear about it
4. Deploy or demo if ready

### Incremental Delivery

1. Setup + Foundational → notifications module, visibility and audit contracts ready
2. US1 → formatting, mentions, watchers, notifications (MVP) → demo
3. US2 → attachments with malware checks → demo to the software teams (screenshots)
4. US3 → search and saved filters → demo
5. US4 → labels and quick filters → demo at a stand-up
6. US5 → email → demo to approvers and occasional users
7. Polish → performance, security and accessibility reviews, quickstart run

### Parallel Team Strategy

With two developers: both on US1 (tests in parallel), then one takes US2 and US5 and the other US3 and US4.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps each task to a user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate the story on its own
- Before building US2 and US5, confirm with IT the mail server settings and whether they provide ClamAV (plan.md)

## Implementation Notes

Decisions made while implementing are recorded here so the documents match the code.

### Setup and foundation (T001–T017)

- **CI** (T005): the main test step leaves out `Category=ExternalServices`; an `external-services` job runs those tests,
  and an `editor` job rebuilds the bundle with Node.js 24, runs `npm audit --audit-level=high` and checks the committed
  `wwwroot/js/editor.js` is unchanged. `.gitattributes` marks the bundle as generated.
- **`NotificationKind` lives in `Upms.Domain.Common`**, like `ProjectTemplate`, not in `Upms.Domain.Notifications`: the
  Work module names the kind in its events, and the module rules forbid another module's domain types.
- **A Work contract for titles**: `IWorkItemTitles` (`Upms.Application.Work.Contracts`) gives the Notifications module
  each item's current key and title, deleted items included, so the panel never reads the Work module's tables.
- **Contracts gained small fields**: `UserDisplay.IsAdministrator` (who can see a project, for `WhoCanSeeAsync`) and
  `TeamMemberInfo.UserName` (mention suggestions).
- **`INotificationService.ListAsync` and `MarkAllReadAsync` return `Result`**, refusing a signed-out or deactivated
  caller with `Forbidden`, as "My tasks" does, so the permission matrix can check them. `CountUnreadAsync` stays an
  `int` (0 for such callers).
- **One reason per person and item**: `INotificationCenter` groups an action's events by item, and asks who can see the
  project once per project, because deleting a column moves many items in one action.
- **Registration**: the web host's per-action proxies need an implementation type, so `INotificationService` and
  `INotificationCleanup` are registered as `NotificationService` each.

### User Story 1 (T018–T042)

- **Strict documents**: the description and comment services accept only the editor's JSON document. Phase 1–3 tests
  now send `Documents.Text(...)` (one paragraph per line). The domain keeps its plain-text overloads for seeding and
  upgrades.
- **Error codes**: `UnsupportedFormatting`, `InvalidLink` and `TextTooLong` carry the field (`Description` or `Body`), so
  they are validation errors that keep their codes.
- **Current names in mentions** (FR-010): the server replaces each mention's label with the person's current display
  name before returning text (`WorkItemReads.WithCurrentNamesAsync`); the renderer shows the label it is given.
- **Rendering**: headings render as `h4`–`h6`, below the drawer's own section headings; a header row becomes a `thead`
  with `th scope="col"`; empty paragraphs keep their line. The cross-site scripting set has 155 documents (script text,
  event attributes, unsafe links in many spellings, unknown nodes and marks, markup in labels and attributes).
- **The editor** (T004, T038): TipTap 3.31.4 with esbuild 0.28.2; the bundle is 457 KB and loads the first time an
  editor opens (`upms.loadEditor`), not on every page. Blazor renders the toolbar (one tab stop; table tools disabled
  outside a table, "Insert table" disabled inside one) and `editor.js` wires it, so pressing a button costs no round
  trip. Underline, horizontal rules and the trailing node are off, so the editor never produces what the server refuses.
  Save is an `EventCallback<EditorSave>`: the parent re-renders and sets `EditorSave.Error` to refuse.
- **The comment box** is an "Add a comment…" button that opens the editor, as in Jira, so the bundle loads only for
  people who write. The drawer's Escape guard now also keeps the drawer open while the editor holds text.
- **Watchers**: the creator watches items made on the board, in the list, as sub-tasks, as stages and as a new project's
  default stages. Status changes notify only for live items. A gate's requester hears its outcome (approved or sent back),
  not each approval on the way.
- **Matrix**: the rows of later stories are in the existing `Pending` dictionary (row → task), reported as skipped; the
  gate notification scenarios are in `GateNotificationTests.cs` (they need the Stage-gate test base).
- **Seed and load test** (T041): the seeder writes the plain-text copies, the watchers (creator, assignee, commenters) and
  notifications for the last 90 days; the load test adds "unread count", "notification panel" and "comment with a
  mention". The full run is pending, as for Phase 3.

### User Story 2 (T043–T057)

- **Upload**: the file is copied to a temporary file while its size and SHA-256 are taken, then its first bytes are
  checked against its extension; only then is a row written. Contents live in `AttachmentContents`, one row per file,
  streamed in and out with SqlClient so a 25 MB file is never held in memory.
- **Checking**: `AttachmentScanner` claims up to 20 files with `UPDATE TOP (20) … WITH (READPAST, ROWLOCK) OUTPUT`, so two
  instances never take the same file, and holds each for 5 minutes. A scanner that cannot answer leaves the file
  "being checked" and tries again after 1, 5 and then every 15 minutes. With no scanner configured, `NoMalwareScanner`
  answers "unavailable", so files are never released (it fails closed), a warning is logged and readiness is
  "Degraded".
- **Blocked files are quarantined**: the content is kept for the security team and never served. The uploader is told
  (`UploadBlocked`) and `MalwareDetected` is audited with no actor, as the worker found it. "Delete permanently" by an
  administrator is audited as `AttachmentPurged` with that administrator as actor.
- **Endpoints**: every refusal is the same empty 404 (status code pages are switched off for it). Responses carry
  `nosniff`, `Content-Disposition` and `default-src 'none'; sandbox`; the security headers middleware leaves a policy
  an endpoint set itself (`OwnPolicyKey`). The rate limiter now runs after authentication, so its per-user partition
  sees the user (120 requests a minute).
- **`AttachmentView.CanPurge`** tells the drawer whether to offer "Delete permanently", so the rule stays in the service.
- **ClamAV test** (T047): one `clamav/clamav:stable` container per test class (`ClamAvFixture`); it starts in about two
  minutes while the signatures load.
- **Browser tests** (T050): the app runs with `TestVirusScanner`, which blocks the antivirus test file and passes
  everything else.

### User Story 3 (T058–T072)

- **Every search call refuses a signed-out caller**: `ResolveKeyAsync`, `RecentAsync` and `SearchAsync` return `Result`
  and answer `Forbidden` to a signed-out or deactivated caller, so the permission matrix can check them. A key that
  is hidden or does not exist gets the same `NotFound` "No work item KEY was found.".
- **Recent items** are recorded each time a drawer opens (`WorkItemService.GetAsync`), one row per person and item,
  trimmed to the latest 50; the box shows the latest 10 the person can still see.
- **The index**: `SearchIndexer` rewrites an item's words in the caller's unit of work and never changes the item, so
  neither a comment nor the background worker changes the version an open drawer holds. Every path that creates an
  item indexes it (stages too). The worker keeps its place in a one-row table, `SearchIndexProgress` (migration
  `SearchIndexProgress`), and only indexes items without words: at first a per-item `SearchIndexedAt` column marked
  indexed items, but the browser tests showed that marking an item changed its version under an open drawer.
  Deleted items keep their words, so a restored item is found again; search leaves deleted items out.
- **Sorting by key** orders by the project key, then the item number, so `WEB-9` comes before `WEB-10`.
- **Saved filters keep one query string**: the search page's address without the page and the filter id. Opening
  `/search?filter={id}` alone puts the filter's criteria into the address (replacing it), so the address always
  says what is shown.
- **Sharing**: "Shared with me" lists filters shared with the projects one is a member of; anyone who can see the
  project, administrators included, can open and run such a filter. Sharing with a project one cannot see is
  `NotAMember` as a `NotFound` refusal, the same answer whether or not the project exists (matrix 🚫). A taken name
  is reported with the existing filter's name as its owner wrote it.
- **Browser tests**: the fixture's direct inserts index the new items at once, as `SearchIndexWorker` would within
  30 seconds.
- **Seed and load test** (T072): cards have five comments on average (about 2,000,000 at the baseline), with more
  varied text; the seeder writes every item's search words and marks it indexed; about a third of the people have one
  to three saved filters, one in five shared with a project they work on. The load test adds "key jump", "word
  search", "word search (administrator)" (one search in ten) and "saved filter run" (open the filter and run its
  search, measured together). The full run is pending, as for Phase 3.

### User Story 4 (T073–T082)

- **No commas in labels**: a label has no whitespace, control characters or commas, because the board, list and search
  addresses carry labels as comma-separated lists (FR-042 and the refusal message now say so).
- **One label row per name**: adding a label creates it, if no label has that name in any case, in one statement that
  holds its range lock, so two people adding the same new label share one row; the first spelling stays. Adding a
  label an item already has, or removing one it does not have, changes nothing.
- **History, not version**: adding and removing a label are recorded with `RecordActivity`, like attachments, so
  someone editing the item in another window is not told it changed.
- **Suggestions** need the right to contribute to the project being edited, but come from every project the person
  can see (live items only). The drawer leaves out labels the item already has. A space or Enter adds what is typed.
- **Board quick filters** run on the cards the board already shows. `BoardFilter` carries them in the address
  (`labels`, `kinds`, `containers`, `assignee`); "Only my tasks" is `assignee=me`. Each kind matches any of its values
  and the kinds combine. Opening a card keeps the filters in its link. Type toggles appear when the board can hold more
  than one type, so Stage-gate boards (tasks only) offer stages and labels.
- **List and search**: the list gains a "Labels" column and a label filter (`labels`), the search page a label field
  (`label`); labels typed are kept as typed and matched in any case.
- **Seed and load test** (T082): 200 labels, on about 30% of items (one to three each, a few labels on many items).
  The board load measured by SC-002 now includes each card's labels and epic or stage; the quick filters themselves
  need no server call.

### User Story 5 (T083–T096)

- **`EmailChoice` lives in `Upms.Domain.Common`**, like `NotificationKind`: the account (Identity) keeps it and the
  Notifications domain applies it, and the module rules forbid one domain reaching into the other.
- **Email off by default**: with no `Email:Smtp:Host`, the transport reports itself unconfigured, nothing is queued and
  the worker logs a warning once. `Email:FromAddress` and `Email:FromName` set the sender; links use
  `App:PublicBaseUrl` (also settable as `Email:PublicBaseUrl`). The SMTP password comes from a secret store.
- **Queued with the notification**: `NotificationCenter` adds an `EmailMessages` row for each new notification whose
  recipient is active and whose choice includes the kind (`RecipientRules.IsEmailed`). The email row points at its
  notification through a navigation, so both are saved together, and it is deleted with it (cascade).
- **Built when sent**: `EmailDispatcher` (called by `EmailDispatchWorker` every 15 seconds, until the queue is empty)
  claims 20 emails with `UPDATE TOP (20) … WITH (READPAST, ROWLOCK)` and a 2-minute lease. It reads the address, the
  choice, the project, the item's current key and title and the actor's name at that moment. Someone deactivated, who
  changed their choice to leave this kind out, or who can no longer see the project is not emailed (the email is
  cancelled).
- **Content** (`EmailComposer`): subject `[KEY-N] Title` (`[KEY] Stage gate` for gates), the sentence of the in-app
  notification addressed to "you", the project, the link and a line pointing to the profile, in plain text and
  encoded HTML. No description, comment text or attachment.
- **Retries**: after 1, 2, 5, 10 and 20 minutes, then every 30 minutes; a failure 24 hours after creation gives the
  email up and writes `EmailGivenUp` (target: the recipient's id; no actor). The last error is kept, cut to 500
  characters, never the content.
- **Clean-up**: the daily clean-up also deletes finished emails older than 30 days.
- **The email choice column** has a database default of `AimedAtMe`, so existing accounts get it; a sentinel no account
  can have makes EF always write the value, so an account created with "None" keeps it.
- **The upgrade test** now writes its Phase 3 accounts with SQL, as the Phase 3 schema has no email choice.
- **Browser tests**: the app runs with smtp4dev (`rnwood/smtp4dev:v3`) and a port chosen before it starts, so links in
  emails name it. The "mail server unreachable" case is covered by `SmtpDeliveryTests` (external) and the dispatch
  tests, not the browser test.
- **Seed**: one person in ten chose "None" and three in twenty "All"; the last day's assignments and mentions have
  emails, a few dozen still waiting.

### Owner decision of 2026-10-07: sign-in and email until UAT

- **Sign-in**: the organization will provide its LDAP server for UAT, and sign-in will move to it then. Until then a
  development-only switch, `DevSignIn:UserName`, signs every visitor in as that account without the sign-in page
  (`DevSignInMiddleware`). The app refuses to start with it outside Development (`DevSignInTests`). The password
  sign-in stays in the code and works again as soon as the setting is removed.
- **Email**: the organization will provide its SMTP server for UAT. Until then no `Email:Smtp:Host` is set, so email
  is off: nothing is queued or sent, and in-app notifications work as usual. The email code stays as built.
