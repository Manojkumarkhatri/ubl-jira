# Research: Collaboration and Search (Phase 4)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Phase 4 keeps every decision of Phases 1 to 3 (`specs/002-…/research.md` R1–R26, `specs/003-…/research.md`
R1–R16, `specs/004-…/research.md` R1–R26) and stays additive: new tables, new columns and new enum values; no
existing column changes meaning. Four areas needed real choices: the editor and how formatted text is stored,
where files are kept and how they are checked, how email leaves the system, and how search stays fast across
projects. The rest follows the existing patterns.

## Where the new parts live

### R1. A Notifications module; everything else joins the Work module

- **Decision**: add a fourth module, **Notifications**, that owns notifications, the email outbox and their
  delivery. Formatted text, mentions, watchers, attachments, labels, search, saved filters and recent views
  belong to the **Work** module, which owns work items and comments. The Identity module gains the person's
  email choice. The Work module raises events through one contract, `INotificationCenter`
  (`Upms.Application.Notifications.Contracts`), inside its own transaction. The Notifications module reads
  people through `IUserDirectory` and visibility through `Upms.Application.Projects.Contracts`.
- **Rationale**: notifications and email have their own screens, retention, background delivery and failure
  handling, and they will serve later phases (dashboards, releases). Every other Phase 4 capability reads or
  changes work items in the same transaction as the item (a comment adds watchers and search words; an
  attachment changes the item's history), so it stays in Work, as Phase 3 decided for sprints and gates (Phase 3
  R1). One contract keeps the new boundary narrow; `ModuleBoundaryTests` gains the new module.
- **Alternatives considered**: notifications inside Work (mixes delivery concerns into the largest module);
  a separate Search module (search reads work items, comments and labels, all Work data, so every query would
  cross the boundary).

## Formatted text

### R2. The editor: TipTap (ProseMirror), bundled once and served from the site

- **Decision**: descriptions and comments use a WYSIWYG editor built on TipTap 3, the MIT-licensed toolkit over
  ProseMirror, with its free extensions: StarterKit (headings, bold, italic, strikethrough, code, code blocks,
  lists, quotes), Link, Table (with header rows), Mention and Image. A small source folder
  (`src/Upms.Web/Editor/`, with `package.json` and a lock file) is bundled by esbuild into one file,
  `wwwroot/js/editor.js`, which is committed, so the .NET build needs no Node.js. A Blazor component,
  `RichTextEditor`, wraps it through JavaScript interop.
- **Rationale**: the spec asks for what people know from Jira: formatting as you type, a toolbar, keyboard and
  typed shortcuts, tables whose rows and columns can be added and removed, tables pasted from spreadsheets,
  pasted screenshots, and "@" suggestions (FR-001, FR-007, FR-023). Atlassian's own editor is built on
  ProseMirror. Writing a safe, accessible WYSIWYG editor from scratch is far beyond the "small scripts" in
  `wwwroot/js`, and a Markdown text box would show syntax instead of formatting. TipTap needs no inline
  scripts or `eval`, so the Content Security Policy (`script-src 'self'`) is unchanged.
- **Alternatives considered**: a Markdown text box with a preview (not WYSIWYG, and tables and mentions are
  awkward); Quill (weak table support); Lexical (heavier, React-oriented); loading the editor from a CDN
  (breaks `script-src 'self'` and adds a runtime dependency on another site). Recorded in Complexity Tracking.

### R3. Formatted text is stored as a validated document, and the server renders it

- **Decision**: formatted text is stored as the editor's JSON document, never as HTML. On every save the
  server:
  - checks the document against an allowlist of node and mark types (paragraph, heading 1–3, bold, italic,
    strike, code, code block, bullet and ordered list, list item, blockquote, hard break, link, table with rows,
    header and plain cells, mention, image) and drops any other attribute;
  - accepts links only with `http`, `https` or `mailto` addresses;
  - forces table cells to span one row and one column;
  - keeps a mention only for an active member of the project, and an image only for an attachment of the same
    item;
  - extracts the plain text, limited to 32,000 characters (FR-002), and the document size, limited to 512 KB.

  For display, the server renders the document to HTML itself, encoding every text node. Links get
  `rel="noopener noreferrer nofollow"` and `target="_blank"`. Mentions show the person's current display name.
  Images point to the attachment's preview address, or show "being checked" or "removed".
- **Rationale**: rendering from a structured document with an allowlist is safe by construction, so no HTML
  sanitizer library is needed (FR-003, SC-007; constitution III). The document also gives the server what it
  needs without parsing HTML: mention ids for notifications (R6), image ids for attachments (R10), and plain
  text for length, history and search (R15). The client's paste cleanup only improves the editing experience;
  the server never trusts it.
- **Alternatives considered**: storing sanitized HTML (needs a sanitizer package and re-sanitizing on read);
  storing Markdown (lossy round trips with a WYSIWYG editor, and mentions and images need custom syntax).

### R4. Old plain text keeps its path; a format flag and a plain-text copy are added

- **Decision**: `WorkItems` gains `DescriptionFormat` (0 = plain, 1 = document) and `DescriptionText` (the plain
  text). `Comments` gains `BodyFormat` and `BodyText`. The migration sets the format to plain and copies
  `Description` into `DescriptionText` and `Body` into `BodyText`. Plain text keeps its Phase 1 rendering
  (`PlainText`: line breaks kept, links clickable), so nothing changes on screen until someone edits it
  (FR-004, SC-008). When plain text is opened in the editor, it becomes paragraphs; saving stores a document.
  The List view's word filter (Phase 2) now looks in `DescriptionText`, so it never matches JSON keys. History
  rows for description changes keep plain text, as before.
- **Rationale**: no existing value is rewritten, and every reader of plain text keeps working.
- **Alternatives considered**: converting every old text into a document in the migration (rewrites all data,
  and link detection belongs to the renderer); a second description column for documents (two sources of
  truth).

### R5. The editor is accessible and keyboard-first

- **Decision**:
  - The editable area has `role="textbox"`, `aria-multiline="true"` and a visible label.
  - The toolbar is a `role="toolbar"` with one tab stop and arrow keys between its buttons. Each button has a
    name and shows its shortcut, and toggles show their state with `aria-pressed`.
  - Shortcuts: Ctrl+B, I, Shift+S (strike), E (code) and K (link); Ctrl+Alt+1–3 (headings); Ctrl+Shift+7 and 8
    (lists).
  - Typed shortcuts: `*` or `-` and a space starts a list, `1.` a numbered list, `#` a heading, `>` a quote,
    and three backticks a code block.
  - Inside a table, Tab and Shift+Tab move between cells and a table menu adds or removes rows and columns.
    Escape then Tab always leaves the editor.
  - "@" opens a `listbox` of suggestions tied to the editor by `aria-activedescendant`.
  - Rendered tables use `<th scope="col">` for header rows (FR-005).
- **Rationale**: constitution VI and SC-009. These are TipTap's defaults where they exist; the toolbar,
  suggestion list and table menu are ours.
- **Alternatives considered**: TipTap's default of trapping Tab in lists (removed so that keyboard users can
  always leave).

## Mentions, watchers and notifications

### R6. Mentions are ids in the document; new ones are found by comparing versions

- **Decision**: a mention node stores the person's id and their name at the time. The suggestions come from
  `IProjectTeam.GetMembersAsync`: active members in any role, matched by display name or user name, at most 10
  (FR-008). On save, the service compares the mention ids in the new text with those in the previous version of
  the same description or comment. Only new ids are notified (FR-009). Ids that are not active members are
  turned into plain text.
- **Rationale**: ids survive renames, and the comparison needs no extra table. The rendered name is the
  current display name, and still shows after the person leaves (FR-010).
- **Alternatives considered**: a mentions table (no query needs it); matching "@name" text (breaks on renames
  and duplicate names).

### R7. Watchers are a table, filled by the services

- **Decision**: `WorkItemWatchers` (`WorkItemId`, `UserId`, `StartedAt`). The work item, comment and assignment
  services add the creator, commenters and new assignees in the same transaction (FR-011). "Watch" and "Stop
  watching" are for oneself; Project Admins and administrators can add and remove active members (FR-013). The
  migration adds the creators, assignees and commenters of existing items, so existing work behaves as the
  spec describes. Watching is a personal subscription, not a change to the item: it needs only the right to
  see the item, is not recorded in history, and leaves the item's row version alone, as in Jira. Rows are kept
  when someone loses access, so watching resumes if they can see the item again (FR-014).
- **Rationale**: one row per person and item; the watcher count and list are one indexed read.
- **Alternatives considered**: working out watchers from history on each event (slow, and "Stop watching" would
  need exceptions).

### R8. Notifications: recipients worked out once per action, visibility checked on read

- **Decision**: the Work module calls `INotificationCenter.Raise(event)` before saving, where the event names
  its kind, project, item or gate, actor and candidate recipients with their reasons:
  - mentions;
  - the new assignee;
  - watchers, for comments and status changes;
  - approvers who may decide, or the requester, for gate events;
  - the uploader, for a blocked file.

  The center removes the actor and anyone who cannot see the item now (FR-014, FR-017). It keeps one reason per
  person, with a mention first, then an assignment, then a watch. It adds one `Notifications` row per person in
  the same transaction, so a refused save notifies no one. Reading the panel and the count keeps only
  notifications whose project the person can still access (active member or administrator). So people removed
  from a project stop seeing its notifications altogether, which satisfies FR-020 and SC-004. Notifications
  about a deleted item stay listed, as the spec's edge case asks: opening one opens the drawer, which says the
  item was not found, and it works again if the item is restored. A notification shows the item's current key
  and title.
- **Rationale**: one place decides who hears about what, and the security rule is applied at read time as well
  as at write time.
- **Alternatives considered**: working out recipients after the save in a background job (a crash between
  the two loses notifications); keeping notifications about projects the person has left, only refusing to
  open them (lists titles they may no longer see).

### R9. The unread count refreshes every 30 seconds; old notifications are removed daily

- **Decision**: the bell in the page header shows the count from an indexed query (`RecipientId`, `ReadAt`). A
  `PeriodicTimer` in the circuit refreshes it every 30 seconds, and it also refreshes when the panel opens or
  closes (FR-018, SC-001). The panel lists 20 at a time, newest first, with "Show more", "Mark all as read", and
  read or unread per notification (FR-019). A background worker deletes notifications older than 90 days once a
  day (FR-021).
- **Rationale**: 300 circuits × 2 queries a minute is trivial, and works with several app instances. Pushing
  changes between instances would need a message bus (constitution V).
- **Alternatives considered**: an in-memory push when a notification is created (misses other instances);
  refreshing only on navigation (can miss the one-minute target).

## Email

### R10. An outbox table and a background sender; MailKit for SMTP

- **Decision**: when a notification is created and the recipient's email choice includes it (R11), the
  Notifications module adds an `EmailMessages` row in the same transaction (the outbox pattern). A background
  worker, `EmailDispatchWorker`, runs every 15 seconds:
  - it claims due messages with `UPDATE … WITH (READPAST, ROWLOCK)`, so several instances never send the same
    one;
  - it builds the text at send time from the notification: subject `[WEB-4] Plan the launch`, then who did
    what, the project and a link from `App:PublicBaseUrl`, in plain text and simple encoded HTML, without
    descriptions, comment text or attachments (FR-050);
  - it sends through the company mail server with MailKit, to the address on the account at that moment;
  - after a failure it retries after 1, 2, 5, 10 and 20 minutes, then every 30 minutes, until 24 hours have
    passed (FR-052, SC-006);
  - messages still not delivered are given up and recorded in the audit log (`EmailGivenUp`).

  Deactivated recipients are skipped. If `Email:Smtp:Host` is not configured, no email is queued and the app
  logs a warning at startup.
- **Rationale**: the action never waits for the mail server (FR-052). MailKit is the MIT-licensed library that
  Microsoft recommends in place of `System.Net.Mail.SmtpClient`, which the .NET documentation marks as not
  recommended for new development.
- **Alternatives considered**: sending during the request (slow, and lost when the server is down);
  `SmtpClient` (obsolete for new code); a message broker (constitution V).

### R11. The email choice lives on the account

- **Decision**: `Users.EmailNotifications` (`None`, `AimedAtMe`, `All`), default `AimedAtMe` (FR-049,
  FR-051). "Aimed at me" covers the reasons *assigned*, *mentioned*, *approval requested*, *gate decided* and
  *upload blocked*; "All" adds *comment* and *status change* on watched items. The profile page offers the
  three choices as a radio group. `IUserDirectory` gains a method that returns the choice, email address and
  active state for the recipients of one event.
- **Rationale**: the Identity module owns accounts, and the Notifications module only reads them through a
  contract.
- **Alternatives considered**: a settings table per event (per-event settings are out of scope).

## Attachments

### R12. Files are kept in the database, in their own table

- **Decision**: `Attachments` holds the metadata: item, file name, type, size, uploader, time, SHA-256, check
  status, removed and purged state. `AttachmentContents` holds the bytes as `varbinary(max)`, one row per
  attachment. Files are written and read as streams through SqlClient (`SequentialAccess`), never held whole in
  memory. Permanent deletion removes the content row and marks the metadata as purged (FR-029).
- **Rationale**: one application and one database (constitution V). Files follow the database's transactions,
  backups and restores, so a restore never finds items without their files. It works the same in a Linux
  container and on Windows. At 100,000 files and far fewer bytes than the 2.5 TB ceiling, SQL Server copes
  well, and a separate table keeps item queries from touching blob pages.
- **Alternatives considered**: a folder or blob store (a second thing to back up and keep consistent, and
  per-environment configuration); FILESTREAM (not available on SQL Server for Linux).

### R13. Uploads go over the circuit; downloads over two HTTP endpoints

- **Decision**:
  - **Uploads**: the drawer's "Attach" button (a real file input), drag-and-drop, and pasting or dropping
    images into the editor all read files in the browser. They pass them to .NET as `IJSStreamReference`
    (Blazor's JavaScript-to-.NET streaming) with a 25 MB limit.
  - **Upload checks**: `AttachmentService.AddAsync` checks the Contribute right, the extension allowlist and
    the size. It also checks the first bytes against the declared type (PNG, JPEG, GIF, WebP, PDF, the
    Office Open XML and legacy Office containers, and UTF-8 text without NUL bytes for text and technical
    files). It then streams the file into the database with the status "being checked" (FR-023–FR-025).
  - **Downloads**: `GET /attachments/{id}` returns the file as a download. `GET /attachments/{id}/preview`
    returns images inline, and only images.
  - **Download checks**: both endpoints check at that moment that the person can see the item, that the file
    passed its check, and that it was not removed, purged or deleted with its item; otherwise they return 404
    (FR-027, FR-030).
  - **Download headers**: `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none';
    sandbox`, `Cache-Control: private, no-store`, and `Content-Disposition` with the encoded file name.
    Technical and text files are always sent as `application/octet-stream` downloads, never shown in the
    browser.
- **Rationale**: uploads over the circuit reuse the circuit's authentication and antiforgery, with no new
  write endpoint. Downloads must be plain URLs for `<img>` and the browser's download, and are checked on every
  request. Content sniffing stops a script or an HTML page renamed `.png`.
- **Alternatives considered**: a multipart upload endpoint (new CSRF and rate-limit surface for no gain);
  serving previews from a separate domain (not needed with `sandbox` and `nosniff`).

### R14. Malware checks: ClamAV through its daemon protocol, run by a background worker

- **Decision**: `IMalwareScanner` with one implementation, `ClamAvScanner`. It streams the file to a `clamd`
  daemon over TCP with the `INSTREAM` command, a small documented protocol, written in about 80 lines with no
  package. `AttachmentScanWorker` claims files that are "being checked" (every 10 seconds, with `READPAST`):
  - **clean** files pass;
  - **infected** files are blocked and quarantined: their bytes stay in the database but are never served,
    previewed or scanned again, and their hash and the signature name are recorded. The uploader is notified,
    and the audit log records `MalwareDetected` (FR-025, SC-003). Only an administrator's purge destroys the
    bytes, audited as any purge (FR-029), as constitution IV requires of permanent deletion;
  - if the daemon is unreachable or answers with an error, the file stays "being checked" and is retried after
    1, 5 and then 15 minutes, indefinitely.

  Nothing is downloadable without a completed check. If no scanner is configured, files stay "being checked"
  and the app logs a warning: the check fails closed. The organization can run `clamav/clamav` beside U-PMS or
  point the setting at its own `clamd`. An ICAP adapter for other antivirus products can be added later behind
  the same interface.
- **Rationale**: ClamAV is free, runs on Linux and Windows, detects the standard antivirus test file, and its
  daemon protocol is simple and stable. Windows Defender's scanning interface is Windows-only (constitution:
  portability).
- **Alternatives considered**: scanning during the upload (slow, and fails the upload when the scanner is
  down); a ClamAV client package (a dependency for 80 lines); AMSI (Windows-only).

## Search, labels and filters

### R15. Search uses a word table in SQL Server, not full-text search

- **Decision**: `SearchTerms` (`WorkItemId`, `ProjectId`, `Term`) holds the distinct words of each item's
  title, its description's plain text and its comments' plain text. Words are lower-cased (invariant culture),
  split on anything that is not a letter or digit, cut to 40 characters, and kept only from 2 characters on.
  Whenever the title, the description or a comment changes, the service rewrites the item's words in the same
  transaction; deleted comments' words go.
  A word in the search matches terms that start with it (`Term LIKE @word + '%'`, an index seek). Several words
  must all match. The query also:
  - keeps only projects the person can access: their memberships, or no limit for administrators;
  - applies the other filters on `WorkItems`;
  - sorts and pages the results (FR-034–FR-037).

  A one-row table, `SearchIndexProgress`, records how far `SearchIndexWorker` has gone: it fills the table for
  existing items in batches of 500 after the upgrade, in order of their ids, and the quickstart says search is
  complete once it finishes. New items get their words in the same transaction as their text. The worker never
  writes to `WorkItems`, so it never changes the version an open drawer holds (a per-item `SearchIndexedAt` column
  did, and was replaced during validation). The List view keeps its Phase 2 "contains" filter on `DescriptionText` (R4).
- **Rationale**:
  - "Words starting with" is exactly a prefix seek.
  - The table works in the standard SQL Server container used by every test and in any edition, with no extra
    feature to install.
  - The access filter is a plain join.
  - The size is about 15 million rows (500,000 items × about 30 distinct words), plus comment words, well within
    SQL Server's comfort on the existing hardware.
- **Alternatives considered**: SQL Server full-text search (an optional component that the official Linux
  image lacks, so every test and developer environment would need a custom image, plus its own index
  population lag); `LIKE '%word%'` over every description and comment (scans; fine for one person's projects,
  too slow for administrators over 500,000 items); a search engine such as Elasticsearch (constitution V).

### R16. The search box: key jump, recent items, then the search page

- **Decision**:
  - **The box**: an accessible combobox in the page header (`role="combobox"` with a `listbox`). On focus with
    no text it lists the person's 10 most recently viewed items that they can still see (FR-033).
  - **Recent views**: `RecentViews` (`UserId`, `WorkItemId`, `ViewedAt`) is updated when the drawer opens, and
    trimmed to the latest 50 per person.
  - **Key jump**: text that looks like a key (`[A-Za-z0-9]{2,10}-\d+`) plus Enter opens the item, or answers
    "No work item KEY was found" in every refused case (FR-032).
  - **Words**: any other text plus Enter opens `/search?q=…`.
  - **The page**: filters and sort live in the address (as in the Phase 2 list), 50 results per page, and the
    drawer opens over the results (FR-035–FR-038).
- **Rationale**: this matches Jira's quick search and keeps one screen for results.
- **Alternatives considered**: showing search results inside the box (duplicates the results page; Jira moved
  away from it).

### R17. Saved filters store the search address; shares are rows checked on read

- **Decision**:
  - **Tables**: `SavedFilters` (`OwnerId`, `Name`, `NormalizedName`, `Query`, times and a row version), with
    names unique per owner (FR-039). `Query` is the canonical query string of the search page, so running a
    filter is navigating to `/search?filter={id}`, which loads its words, filters and sort.
    `SavedFilterShares` (`FilterId`, `ProjectId`) records sharing (FR-040).
  - **Sharing rules**: the owner may share only with projects they are a member of (administrators: any
    project). Filters shared with a person are those with a share in one of their projects, whose owner is
    active and still a member of that project (or still an administrator), checked through `IProjectTeam` on
    read. So sharing ends by itself when the owner leaves or is deactivated, with no clean-up job.
  - **Running a filter**: always applies the runner's own visibility, and marks values that no longer apply
    to them (such as a project they cannot access) as unavailable (FR-041).
- **Rationale**: one representation of a search, and sharing that can never reveal more than the runner may
  see.
- **Alternatives considered**: a structured criteria table (more schema for the same meaning); removing shares
  when memberships change (a cross-module event for no benefit).

### R18. Labels: one shared table and a link table; suggestions from visible projects

- **Decision**: `Labels` (`Id`, `Name`, `NormalizedName` unique, upper-cased invariant) and `WorkItemLabels`
  (`WorkItemId`, `LabelId`). Adding a label that matches an existing one in any case reuses it, so the first
  spelling is kept (FR-043).
  - **Names**: 1–50 characters with no whitespace, control characters or commas (commas separate labels in
    addresses); at most 20 labels per item
    (FR-042).
  - **Suggestions**: labels used on live items in the projects the person can access, whose normalized name
    starts with the typed text, at most 10 (FR-044). Unused labels are never suggested.
  - **History**: `LabelAdded` and `LabelRemoved`.
  - **Where labels show**: the board card, the list row and the search result carry the item's labels
    (FR-045).
- **Rationale**: a shared table gives case-insensitive matching across the organization, as the backlog asks.
  Building suggestions from visible projects keeps restricted label names private (SC-004).
- **Alternatives considered**: a label column of comma-separated text (no uniqueness, slow filters);
  suggesting from every label (reveals names from restricted projects, as Jira does).

### R19. Board quick filters work on the loaded cards and live in the address

- **Decision**: `CardView` gains the card's labels and its container (key and title). The board page filters
  the cards it already shows by label, type and epic or stage, together with the Phase 2 "Only my tasks" and
  assignee filter, and shows "matching of total" per column (FR-047). The quick filters and the assignee
  filter move into the address (`?labels=…&kinds=…&containers=…&assignee=…`), so a filtered board can be
  bookmarked and shared.
- **Rationale**: the board already loads every card it shows (at most 500), and Phase 2's filters work the same
  way, so filtering is instant and needs no new query.
- **Alternatives considered**: filtering on the server (a round trip per click for data the page already has).

## Across stories

### R20. History and the audit log

- **Decision**: `WorkItemField` gains `AttachmentAdded`, `AttachmentRemoved`, `LabelAdded` and `LabelRemoved`
  (FR-031, FR-048). Comment events keep their Phase 1 fields. A new Identity contract, `IContentAuditLog`,
  writes `MalwareDetected`, `AttachmentPurged` and `EmailGivenUp` (FR-055), as `IPlanningAuditLog` does for
  Phase 3.
- **Rationale**: the same append-only tables and the same contract pattern.

### R21. Permissions

- **Decision**: the Phase 4 matrix (`contracts/permissions.md`) supersedes Phase 3's.
  - **View**: watch and stop watching, see watchers, download checked files.
  - **Contribute**: write formatted text, mention, attach, add and remove labels.
  - **Manage**: manage watchers, remove anyone's attachment.
  - **Administrator only**: purge an attachment.
  - **Uploader**: remove their own attachment while they can contribute.
  - **Owner only**: notifications, saved filters and the email choice belong to their owner. Shared filters
    can be run by members of the projects they are shared with.

  Every rule is enforced in the services, and `PermissionMatrixTests` gains the new rows (FR-054).

### R22. Upgrade

- **Decision**: six additive migrations, one for the shared foundation and one per story, so each story can be
  built and shipped on its own:
  - `Notifications`: notifications (foundation);
  - `FormattedTextAndWatchers`: format and text columns filled by copying the old text, and watchers backfilled
    from creators, assignees and commenters (story 1);
  - `Attachments` (story 2);
  - `SearchAndSavedFilters`: search terms, `SearchIndexedAt`, saved filters, shares and recent views (story 3);
  - `Labels` (story 4);
  - `EmailOutbox`: the email outbox and the email choice (story 5).

  After the upgrade, every description and comment reads as before (SC-008). Search fills in within minutes
  (R15).

### R23. Configuration and local services

- **Decision**: new settings, from configuration providers only (constitution):
  - `Email:Smtp:Host`, `Port`, `UseStartTls`, `UserName`, `Password` (a secret), and `Email:From`;
  - `Malware:ClamAv:Host` and `Port`.

  `docker-compose.yml` gains two development services: `clamav` (`clamav/clamav:stable`, port 3310) and
  `smtp4dev` (`rnwood/smtp4dev`, SMTP on 2525 and a web inbox on 5025). The readiness check reports the scanner
  and mail server as degraded, not unhealthy, so the app keeps serving when they are down.

### R24. Security review points

- **Decision**: the Phase 4 review covers:
  - the document validator and renderer, against a set of at least 100 cross-site scripting samples (SC-007);
  - upload type sniffing, the 25 MB limit and file-name encoding;
  - download headers and the access check on every download;
  - no leaks through mention suggestions, label suggestions, notifications, emails, search, recent items or
    shared filters (SC-004);
  - email header injection (MailKit encodes headers; subjects come from titles);
  - the scanner failing closed.

  Logs never contain descriptions, comments or file contents (constitution).

### R25. Accessibility of the new screens

- **Decision**:
  - **The bell**: a button whose name includes the count ("Notifications, 3 unread"). The panel is a
    non-modal dialog with a list; Escape closes it and returns focus to the bell.
  - **The search box**: the combobox pattern of R16.
  - **Attachments**: a list whose buttons are named per file ("Download report.pdf", "Remove report.pdf").
    The drop zone has a keyboard alternative, the "Attach files" button. Upload and check progress is
    announced through the live region.
  - **Labels**: a combobox with suggestions, and removable label chips ("Remove label launch").
  - **Quick filters**: toggle buttons with `aria-pressed`, and column counts as text.

  axe checks at 1280 px and 360 px, and a keyboard-only path per story (FR-056, SC-009).

### R26. Performance: a bigger seed and new load scenarios

- **Decision**:
  - **Seed**: `tools/Upms.Seed` adds 2,000,000 comments with realistic words, labels on 30% of items (from about
    200 labels), watchers, 50,000 small attachments with pre-passed checks, notifications, saved filters and
    shares, and fills `SearchTerms`.
  - **New load scenarios**: key jump, word search across accessible projects (and as an administrator), running
    a saved filter, opening the notification panel, the unread count, posting a comment with a mention, and a
    board load with quick filters.
  - **Indexes**: `SearchTerms` (`Term`) including `ProjectId` and `WorkItemId`, with the primary key
    (`WorkItemId`, `Term`); `Notifications` (`RecipientId`, `ReadAt`, `OccurredAt`); `WorkItemWatchers`
    (`WorkItemId`); `WorkItemLabels` (`LabelId`); `RecentViews` (`UserId`, `ViewedAt`).
- **Note**: Phase 3's full-scale run (T095) is still open. It found and fixed two queries that scanned the work
  item table. Phase 4's run repeats the full Phase 1–3 mix as well.

### R27. Tests: the same pyramid, plus two service containers

- **Decision**:
  - **Domain tests**: the document validator and renderer (including the cross-site scripting set), the mention
    comparison, word splitting, label rules, file type sniffing and the notification precedence.
  - **Application tests** (real SQL Server): services per story, recipients and removal of the actor, email
    outbox retries with a fake transport, attachment streaming, search visibility across projects, sharing of
    saved filters, the upgrade from the last Phase 3 migration, and the Phase 4 permission matrix.
  - **Integration tests with real services**: one class runs a ClamAV container (the standard test file is
    blocked, a clean file passes). One runs an smtp4dev container (an email arrives with the right content,
    and is retried while the server is stopped). Both pull images, so they are marked and can be skipped
    offline.
  - **Web tests** (bUnit, JavaScript interop mocked): the editor component, the bell and panel, the search box
    and page, attachments, labels and quick filters.
  - **End-to-end tests** (Playwright and axe): each story's journey, with real typing, pasting and uploads.
- **Rationale**: constitution II; the real services are where the protocol risks are.
