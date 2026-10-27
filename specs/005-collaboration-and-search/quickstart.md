# Quickstart & Validation Guide: Collaboration and Search (Phase 4)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

This guide shows how to upgrade a Phase 3 installation and validate Phase 4 by hand and with the automated
suites. It assumes the Phase 1–3 setup (`specs/002-…`, `specs/003-…`, `specs/004-…/quickstart.md`).

## 1. Local services: mail and malware checks

Phase 4 needs a mail server and a malware scanner (research R10, R14, R23). For development, `docker-compose.yml`
provides both:

```bash
docker compose up -d sqlserver clamav smtp4dev
```

- `clamav` listens on port 3310. It loads its signatures on first start, which takes a few minutes; until then,
  uploads stay "being checked".
- `smtp4dev` accepts mail on port 2525 and shows it at `http://localhost:5025`.

Settings (user secrets in development; a secret store in production):

```bash
dotnet user-secrets set "Email:Smtp:Host" "localhost" --project src/Upms.Web
dotnet user-secrets set "Email:Smtp:Port" "2525" --project src/Upms.Web
dotnet user-secrets set "Email:From" "U-PMS <upms@example.com>" --project src/Upms.Web
dotnet user-secrets set "Malware:ClamAv:Host" "localhost" --project src/Upms.Web
dotnet user-secrets set "Malware:ClamAv:Port" "3310" --project src/Upms.Web
```

**Expected**: at startup the log names the mail server and the scanner. Without `Email:Smtp:Host`, it warns that
no email will be sent. Without a scanner, it warns that uploads will stay "being checked".

## 2. Upgrade an existing Phase 3 database (SC-008)

Before upgrading, note a few items in `WEB` with descriptions and comments, including line breaks and links.

```bash
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web
```

**Expected**:
- The six Phase 4 migrations are applied.
- Every noted description and comment reads exactly as before: the same text, line breaks and clickable links.
- Each item's creator, assignee and commenters now watch it.
- Within a few minutes the log reports that the search index is complete, and searching for a word from an old
  comment finds its item.

## 3. Validate each user story by hand

Use the Phase 3 accounts: **sara** (Project Admin of `WEB`, `MOB` and `CBU`), **amina** and **omar** (Members),
**bilal** (Viewer) and **farah** (approver of a `CBU` gate). Two browsers make the notifications easy to see. The
scenario numbers refer to spec.md.

### US1: Discuss work with formatting and mentions

1. As amina, open `WEB-1` and write a comment with bold, a bulleted list and a link, and mention bilal with "@bi".
   Post it: the formatting shows, and amina now watches `WEB-1` (scenarios 1, 5).
2. As bilal: within a minute the bell shows 1; the panel says "Amina Khan mentioned you on WEB-1 …"; opening it
   opens `WEB-1` and marks it read (scenarios 6, 12, 13).
3. Edit `WEB-1`'s description: a heading, a numbered list, a code block and a table with a header row (use the
   table menu to add a row and a column). Save; the history records it (scenario 2). Paste a table from a
   spreadsheet: its rows and columns stay (FR-001).
4. Paste `<script>alert(1)</script>` and an `<img onerror>` into a comment: they show as text; nothing runs
   (scenario 4).
5. As omar, "Watch" `WEB-1`. As bilal, move `WEB-1` to Done: amina and omar are notified, bilal is not (scenarios
   8, 9). As amina, assign `WEB-3` to bilal: bilal is notified and watches it; bilal assigning himself is not
   (scenario 10).
6. Mention bilal in a new comment on `WEB-1`, which he watches: one notification, not two (scenario 11).
7. As sara, remove bilal from `WEB`: bilal's `WEB` notifications disappear from his panel and count (scenario 14).
8. In `CBU`, request approval of a gate: farah is notified; approving it notifies the requester (scenario 15).

### US2: Attach files and screenshots

1. As amina, attach a 2 MB PNG to `WEB-1` with "Attach files", and drag a PDF onto the drawer: both show "Being
   checked", then a preview and "Download" (scenarios 1, 2).
2. Paste a screenshot into a comment and post it: it appears in the text once checked (scenario 3).
3. Try a 30 MB file and a `.exe`: both are refused with the reason (scenario 4).
4. Attach the standard antivirus test file (EICAR) saved as `eicar.txt`: it becomes "Blocked"; amina is notified;
   the audit log records `MalwareDetected` (scenario 5).
5. As amina, remove her file; as sara, remove another; as the administrator, delete one permanently: history and
   audit log record each (scenarios 6, 7).
6. As bilal (Viewer), preview and download, with no way to add or remove (scenario 8). Copy a download link,
   remove bilal from `WEB`, and open the link: 404 (scenario 9).

### US3: Find any work item fast

1. Type `mob-12` in the search box and press Enter: `MOB-12` opens. Type `MOB-9999`: "No work item MOB-9999 was
   found." (scenarios 1, 2). Focus the empty box: recently viewed items are listed (scenario 3).
2. Search "refund": results from every project you can access, newest update first; a project you cannot
   access never appears (scenarios 4, 7).
3. Add the filters status type "To do", assignee "Unassigned" and priority "High", then sort by due date
   (scenarios 5, 6). Save as "Unassigned refunds"; rename it; run it later (scenario 8).
4. As amina, share a filter with `WEB`: bilal sees it under "Shared with me" and can run it, not change it; stop
   sharing and it disappears (scenarios 10, 11).

### US4: Tag work with labels and filter boards quickly

1. As amina, add `launch` to `WEB-1`; in `MOB`, type "lau" on `MOB-3` and pick the suggestion; add `Launch` to
   another item: it becomes `launch` (scenarios 1–3).
2. Labels show on the board, the list and search results; filter the list by label (scenarios 4, 6).
3. On the `MOB` board, turn on the quick filters Bug and the epic "Card refunds": only matching cards, with
   "matching of total" per column; copy the address into another tab: same filters (scenario 7).
4. As bilal, labels show but cannot be changed (scenario 8).

### US5: Get emails about the work that matters to you

1. With bilal's default choice, assign him `WEB-4`, mention him, and comment on an item he watches: smtp4dev shows
   two emails (assignment and mention) with key, title, what happened and a link, and no comment text (scenarios
   1, 2, 4, 5).
2. Set omar's choice to "All": the comment also emails him. Set farah's to "None": no email (scenarios 4, 6).
3. Stop smtp4dev, assign bilal an item (the save is instant), start smtp4dev again: the email arrives within
   about a minute (scenario 8).

## 4. Run the automated tests

```bash
dotnet test --project tests/Upms.Domain.Tests
dotnet test --project tests/Upms.Application.Tests
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"      # Phase 4 matrix
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*CollaborationUpgradeTests"  # SC-008
dotnet test --project tests/Upms.Application.Tests -- --filter-trait "Category=ExternalServices"   # ClamAV and smtp4dev containers
dotnet test --project tests/Upms.Web.Tests
dotnet test --project tests/Upms.Architecture.Tests
dotnet test --project tests/Upms.E2E.Tests                     # journeys + axe at 1280 px and 360 px
```

**Expected**: all green. Phase 4 acceptance tests are named `P4_US1_AS…` to `P4_US5_AS…`. The cross-site scripting
set produces no script (SC-007), and the antivirus test file is always blocked (SC-003).

## 5. Quality gates (same as CI)

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
cd src/Upms.Web/Editor && npm ci && npm run build && npm audit --audit-level=high && git diff --exit-code ../wwwroot/js/editor.js
```

The last line checks that the committed editor bundle matches its sources and that its packages have no high or
critical advisories (research R2).

## 6. Performance check (SC-002)

As in Phases 1–3, with the larger seed (research R26): 2,000,000 comments, labels, watchers, attachments,
notifications and saved filters. The report adds key jumps, word searches (as a member and as an
administrator), saved filter runs, the notification panel and count, comments with mentions, and board loads
with quick filters.

**Expected**: with 300 simulated users on about 500,000 work items, p95 ≤ 1 second for every measured action,
Phase 1–3 actions included.

## 7. Usability measures (pilot)

The pilot plan (`docs/pilot/phase1-pilot-plan.md`) gains the Phase 4 measures:
- at least 90% of users find a given work item, by key or by words, within 30 seconds without help (SC-010);
- at least 80% rate commenting, mentions, notifications and attachments easy (SC-011);
- teams stop sharing screenshots of tracked work through chat or email.
