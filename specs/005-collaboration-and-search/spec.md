# Feature Specification: Collaboration and Search (Phase 4)

**Feature Branch**: `phase-4` (spec folder `specs/005-collaboration-and-search`)

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Phase 4 of U-PMS: collaboration and search." The owner re-scoped the roadmap on
2026-10-01 (see Clarifications): Phase 4 is now collaboration and search, so that U-PMS feels like Jira sooner
for its target teams (software development, project management and technical teams such as Data & AI).
Portfolios, portfolio rollups, and the project and portfolio dashboards with health updates move to Phase 5.
The scope is:
- formatted text in descriptions and comments;
- @mentions;
- watchers;
- in-app notifications;
- email, with each person choosing all, only events aimed at them, or none;
- attachments checked for malware;
- labels and board quick filters;
- a search box on every screen, a search page across projects, and saved filters.

The details come from the product backlog (`specs/001-issue-tracker-mvp`, user stories 6 and 7 and FR-030,
FR-053, FR-060 to FR-074). Where Jira has an equivalent, U-PMS behaves like Jira. This spec builds on Phase 1
(`specs/002-kanban-project-core`), Phase 2 (`specs/003-project-views-and-team`) and Phase 3
(`specs/004-templates-and-planning`); their behavior continues unless a requirement below changes it.

## Clarifications

### Session 2026-10-01

- Q: Phase 4 was planned as portfolios and dashboards. Should collaboration and search come first, to close the
  biggest gaps with Jira for software and project teams? → A: Yes. Phase 4 is collaboration and search;
  portfolios, portfolio rollups, and the project and portfolio dashboards with health updates move to Phase 5.
- Q: Which files can people attach, and how large may each be? → A: Images, PDF, Word, Excel and PowerPoint
  documents, and text, log and CSV files, as in the backlog, plus technical files for software and Data & AI
  teams (JSON, XML, YAML, Markdown, SQL and Jupyter notebooks), up to 25 MB each.
- Q: Which events should send email? → A: Every event that notifies in the app, as in Jira's default, with a
  choice for each person: all, only the events aimed at them (assignments, mentions, gate approvals and their
  own gate requests and uploads), or none. The default is only the events aimed at them.
- Q: Should saved filters stay private, or can people share them? → A: Private by default. The owner can share
  a filter with the members of one or more of their projects, who can run it but not change it. Everyone sees
  only results from projects they can access.
- Q: Should descriptions and comments support tables? → A: Simple tables: rows and columns, an optional header
  row, and the same formatting inside cells; people add and remove rows and columns, and pasted tables keep
  their rows and columns. No merged cells or colors.

## User Scenarios & Testing *(mandatory)*

Terms used in this spec:

- **Work item**, **container**, **template**, **contributor**, **Viewer** and **open**: as in Phases 1 to 3.
  Contributors are a project's Project Admins and Members, and the organization's administrators.
- **Can see an item**: the person is an active member of the item's project (in any role) or an administrator,
  and the item is not deleted.
- **Formatted text**: text with headings, bold, italic, strikethrough, inline code, code blocks, bulleted and
  numbered lists, quotes, links and simple tables.
- **Mention**: a reference to a person in formatted text, written by typing "@" and choosing them. It shows as
  their display name.
- **Watcher**: a person who follows an item so that they hear about its comments and status changes.
- **Notification**: a message in the person's notification panel, opened from a bell on every screen.
- **Malware check**: the scan every uploaded file goes through before anyone can download it.
- **Label**: a short free-form word or phrase without spaces, such as `release-blocker`, attached to work items.
- **Saved filter**: a named search that belongs to one person, can be run again later, and can be shared with
  the members of a project.

### User Story 1 - Discuss work with formatting and mentions, and hear about it (Priority: P1)

Team members discuss work where it happens. They write descriptions and comments with headings, lists, code
blocks and links, and they pull colleagues in by mentioning them. The people involved in an item follow it
automatically, and anyone can choose to follow items they care about. A bell on every screen shows how many
unread notifications a person has, and each notification opens the item it is about. Nobody is told about
their own actions.

**Why this priority**: Keeping decisions next to the work is the main reason teams adopt a tracker like Jira.
Without mentions and notifications, people must keep checking items or move the discussion to chat.

**Independent Test**: In `WEB`, Amina comments on `WEB-1` with a bold phrase, a bulleted list and a link, and
mentions Bilal. Bilal's bell shows one unread notification, and opening it opens `WEB-1`. Amina now watches
`WEB-1`. Omar starts watching `WEB-1`. When Bilal moves `WEB-1` to Done, Amina and Omar are notified and Bilal
is not.

**Acceptance Scenarios**:

1. **Given** a contributor is viewing `WEB-1`, **When** they post a comment with a bold phrase, a bulleted
   list and a link, using the toolbar or keyboard shortcuts, **Then** the comment shows its author, time and
   formatting, and the author now watches `WEB-1`.
2. **Given** a contributor edits the description of `WEB-1`, **When** they add a heading, a numbered list, a
   code block and a table with a header row, and save, **Then** the description shows that formatting, the
   code block keeps its spacing, and the history records the change.
3. **Given** descriptions and comments written before Phase 4, **When** Phase 4 is installed, **Then** they
   read exactly as before: the same text, the same line breaks and the same clickable links.
4. **Given** a person pastes text that contains markup or a script, **When** it is saved and shown, **Then** it
   appears as text or as supported formatting only; nothing in it runs, and nothing is loaded from other
   sites.
5. **Given** a contributor types "@bi" in a comment on `WEB-1`, **When** suggestions appear, **Then** they
   list only active people who are members of `WEB`, in any role, matching by display name or user name.
6. **Given** a comment on `WEB-1` mentions Bilal, **When** it is posted, **Then** Bilal gets a notification
   such as "Amina Khan mentioned you on WEB-1 Plan the launch", his unread count grows by one, and opening
   the notification opens `WEB-1`.
7. **Given** a description already mentions Bilal, **When** someone edits it to add a mention of Omar,
   **Then** only Omar is notified.
8. **Given** `WEB-1`, **When** it is created, assigned or commented on, **Then** its creator, its assignee and
   each commenter watch it; **and** anyone who can see it can start or stop watching it, and can see how
   many people watch it and who they are.
9. **Given** Omar watches `WEB-1`, **When** someone else comments on it or changes its status, **Then** Omar
   is notified; the person who made the change is not.
10. **Given** Amina assigns `WEB-3` to Bilal, **When** the assignment is saved, **Then** Bilal is notified and
    now watches `WEB-3`; **and** when Bilal assigns an item to himself, he is not notified.
11. **Given** Bilal watches `WEB-1` and is mentioned in a new comment on it, **When** the comment is posted,
    **Then** Bilal gets one notification (the mention), not two.
12. **Given** Bilal has unread notifications, **When** he opens the panel, **Then** he sees the newest first,
    each with who did what, on which item and when. He can open one (which marks it read), mark one as read
    or unread, and mark all as read.
13. **Given** Bilal has the app open on any screen, **When** someone mentions him, **Then** his unread count
    updates within one minute, without reloading the page.
14. **Given** Bilal is removed from `WEB`, **When** things happen on `WEB` items he watched, **Then** he gets no
    notifications about them, and earlier notifications about `WEB` items no longer open them.
15. **Given** the Stage-gate project `CBU`, **When** approval of a gate is requested, **Then** each of its
    approvers who may decide is notified; **and when** the gate is approved or sent back, **Then** the person
    who requested it is notified.

---

### User Story 2 - Attach files and screenshots to work items (Priority: P2)

People attach the screenshots, logs, documents, spreadsheets and notebooks that explain the work. They can pick
files, drag them onto an item, or paste a screenshot straight into a description or comment. Every file is checked
for malware before anyone can download it. Images show a preview.

**Why this priority**: Bug reports, designs and project documents depend on files. Teams coming from Jira paste
screenshots all day. Without attachments, the files end up in email and chat, away from the work.

**Independent Test**: Amina attaches a 2 MB screenshot to `WEB-1` and pastes another into a comment. Both show
as "being checked", then as previews. Bilal, a Viewer, opens and downloads them. A 30 MB file and a `.exe`
file are refused with the reason. The standard antivirus test file is blocked: it can never be downloaded,
Amina is told, and the audit log records the detection.

**Acceptance Scenarios**:

1. **Given** a contributor is viewing `WEB-1`, **When** they attach a 2 MB PNG by choosing it or dragging it
   onto the item, **Then** it is listed with its file name, size, uploader and time, and shown as "being
   checked" until the malware check passes.
2. **Given** a file has passed the check, **When** anyone who can see `WEB-1` opens the list, **Then** images
   show a preview that can be enlarged, and every file can be downloaded.
3. **Given** a contributor pastes or drops an image into a description or comment, **When** they save it,
   **Then** the image is added to the item's attachments and shown in the text once it has passed the check,
   with its file name as its text alternative.
4. **Given** a contributor tries to attach a 30 MB file or a program file (such as `.exe`), **When** they
   upload it, **Then** it is refused with a message that states the 25 MB limit or the allowed file types.
5. **Given** an uploaded file contains malware, **When** the check finds it, **Then** it is shown as blocked
   and can never be downloaded, the uploader is notified, and the detection is recorded in the audit log.
6. **Given** the uploader of an attachment, a Project Admin or an administrator, **When** they remove it after
   confirming, **Then** it disappears from the item, the item's history records the removal, and the file is
   kept for audit.
7. **Given** an administrator, **When** they permanently delete an attachment (for example one uploaded with
   sensitive data by mistake), **Then** the file is destroyed, and the audit log records who deleted which
   file from which item.
8. **Given** Bilal, a Viewer of `WEB`, **When** he views `WEB-1`, **Then** he can preview and download its
   attachments but is offered no way to add or remove any.
9. **Given** someone has a download link to an attachment, **When** they use it after being removed from the
   project or deactivated, **Then** the file is not given to them.
10. **Given** `WEB-1` has attachments, **When** it is deleted and later restored, **Then** its attachments are
    unreachable while it is deleted and come back with it.

---

### User Story 3 - Find any work item fast (Priority: P3)

People jump straight to an item by typing its key into the search box at the top of every screen, or pick
from the items they viewed recently. They search across every project they can access by words and filters,
and save useful searches to run again or to share with their team. They never see anything from projects
they cannot access.

**Why this priority**: With thousands of items across many projects, finding things fast matters every day.
Each project's list covers one project at a time; teams that work across projects need one place to look.

**Independent Test**: With items about "refund" in `WEB`, `MOB` and `PAY`, where the user cannot access `PAY`,
type `MOB-12` in the search box and press Enter: `MOB-12` opens. Search for "refund": items from `WEB` and `MOB`
are listed, most recently updated first, and nothing from `PAY`. Filter to status type "To do", unassigned,
with priority "High", save the search as "Unassigned refunds", and run it again later to see current
results.

**Acceptance Scenarios**:

1. **Given** the user can see `MOB-12`, **When** they type "mob-12" into the search box and press Enter,
   **Then** `MOB-12` opens.
2. **Given** `MOB-99` does not exist or the user cannot see it, **When** they type it and press Enter,
   **Then** they are told no work item `MOB-99` was found, with the same message in both cases.
3. **Given** the user has opened items before, **When** they move to the empty search box, **Then** it lists
   the 10 items they viewed most recently that they can still see, and they can open one with the keyboard
   or the mouse.
4. **Given** items that mention "refund" in their title, description or comments, **When** the user searches
   for "refund", **Then** the search page lists matching items from every project they can access (including
   words that start with "refund", such as "refunds"), 50 at a time, most recently updated first, with the
   number of results.
5. **Given** search results, **When** the user filters by project, type, status type, priority, assignee
   (including "me" and "unassigned"), creator, label, and created, updated or due date ranges, **Then** the
   results narrow accordingly, each active filter is shown and can be removed on its own, and the filters are
   kept in the page's address.
6. **Given** search results, **When** the user sorts them by last update, creation, key, priority or due
   date, **Then** they are shown in that order.
7. **Given** a project the user cannot access contains items about "refund", **When** they search for
   "refund", **Then** none of those items appear and nothing reveals that they exist; deleted items never
   appear either.
8. **Given** a filtered search, **When** the user saves it as "Unassigned refunds", **Then** it is listed among
   their saved filters, running it later shows the current results, and they can rename it, update it with
   the current criteria or delete it. By default no one else can see it.
9. **Given** search results, **When** the user opens one, **Then** it opens in the details drawer over the
   results, and closing the drawer puts focus back on that result.
10. **Given** Amina owns the filter "Open payment bugs", **When** she shares it with the members of `WEB`,
    **Then** every member of `WEB` finds it among the filters shared with them, with Amina's name, and can run
    it but not change it; **and** each of them sees only results from projects they can access.
11. **Given** Amina shared a filter with `WEB`, **When** she stops sharing it, or is no longer a member of
    `WEB`, **Then** `WEB`'s members no longer see it.

---

### User Story 4 - Tag work with labels and filter boards quickly (Priority: P4)

Teams tag work with labels such as `release-blocker`, `tech-debt` or `data-quality`, reusing the labels they
already have. Labels show on cards and rows. Teams narrow the board in one click by label, type or epic, and
the list and search by label.

**Why this priority**: Labels are how Jira teams slice work across epics and sprints. Quick filters make a
busy board usable in daily stand-ups. Both are small and build on story 3's filters.

**Independent Test**: In `WEB`, add the label `launch` to `WEB-1`; in `MOB`, typing "lau" on `MOB-3` suggests
`launch`. Both items are then found by the label `launch`. On the `MOB` board, the quick filters "Bug" and
the epic "Card refunds" leave only matching cards, and the column counts show how many match.

**Acceptance Scenarios**:

1. **Given** a contributor is viewing `WEB-1`, **When** they type a label and press Enter or a space, **Then**
   the label is added; a label cannot contain spaces.
2. **Given** labels already used in projects the contributor can see, **When** they type part of one,
   **Then** matching labels are suggested; labels used only in projects they cannot see are never suggested.
3. **Given** `WEB-1` has the label `launch`, **When** someone adds `Launch` to `MOB-3`, **Then** it matches the
   existing label regardless of letter case and is shown the same way on both items.
4. **Given** items have labels, **When** they are shown on the board, in the list or in search results,
   **Then** their labels are shown with them.
5. **Given** a contributor removes a label from `WEB-1`, **When** it is saved, **Then** the label disappears
   from `WEB-1` and the item's history records the change.
6. **Given** the project list, **When** a member filters by one or more labels, **Then** only items with any of
   those labels are listed, and the filter is kept in the address.
7. **Given** the `MOB` board, **When** a member turns on the quick filters for the type Bug and the epic "Card
   refunds", **Then** only matching cards are shown, each column shows how many of its cards match, and the
   quick filters combine with "Only my tasks" and the assignee filter. Stage-gate boards offer stages in place
   of epics.
8. **Given** Bilal, a Viewer, **When** he views an item, **Then** he sees its labels and can use every filter,
   but is offered no way to add or remove labels.

---

### User Story 5 - Get emails about the work that matters to you (Priority: P5)

People who are not looking at U-PMS still hear about the things that need them, through the company mail
server. Each person chooses how much email they get: every event they are notified of in the app, only the
events aimed at them (such as being assigned an item, mentioned, or asked to approve a gate), or none. The
email says what happened and links to the item, but never contains the item's text.

**Why this priority**: Email reaches people who do not have U-PMS open, such as approvers and occasional
contributors. In-app notifications (story 1) cover everyone else, so email comes last.

**Independent Test**: Bilal keeps the default choice, "only events aimed at me". Amina assigns him `WEB-4`,
mentions him on `WEB-1`, and comments on `WEB-2`, which he watches: he receives two emails (the assignment and
the mention), each with the key, title, what happened, who did it and a link, and no description or comment
text. Omar chooses "all" and also receives an email about the comment. Farah chooses "none" and gets only
in-app notifications. With the mail server stopped, Amina's actions complete at once, and the emails arrive
after the mail server is back.

**Acceptance Scenarios**:

1. **Given** Bilal keeps the default email choice, "only events aimed at me", **When** someone else assigns
   him `WEB-4`, **Then** he receives an email with the project, the key and title, "Amina Khan assigned this
   to you", and a link to the item.
2. **Given** the same choice, **When** someone else mentions him, **Then** he receives an email that says who
   mentioned him on which item, with a link, but not the text around the mention.
3. **Given** Farah is an approver of the Initiation gate in `CBU` and gets email for events aimed at her,
   **When** approval of that gate is requested, **Then** she receives an email that names the stage and the
   requester, with a link to the Stages view.
4. **Given** Bilal keeps "only events aimed at me" and watches `WEB-2`, **When** someone else comments on it or
   changes its status, **Then** he gets an in-app notification but no email; **and given** Omar chooses
   "all", **Then** Omar also receives an email about it.
5. **Given** any of these emails, **When** it is sent, **Then** it never contains the description, comment
   text or attachments.
6. **Given** Farah chooses "none" in her profile, **When** she is assigned, mentioned or asked to approve,
   **Then** she receives no email, and her in-app notifications continue.
7. **Given** Bilal assigns an item to himself or mentions himself, **When** it is saved, **Then** he receives no
   email.
8. **Given** the mail server is unavailable, **When** someone assigns Bilal an item, **Then** the assignment
   completes at once, and the email is sent when the mail server is reachable again; emails not delivered
   within 24 hours are given up and recorded.
9. **Given** a deactivated account, **When** an event would email it, **Then** no email is sent.
10. **Given** an email's link, **When** Bilal opens it, **Then** he is asked to sign in if needed and the item
    opens; if he can no longer see it, he is told it was not found.

---

### Edge Cases

- **Mentions of people who later leave**: a mention keeps showing the person's name after they are removed from
  the project or deactivated, but they are not notified of later changes, and they are no longer suggested.
- **Mentions pasted or typed by hand**: text that looks like "@name" but was not chosen from the suggestions is
  plain text and notifies no one.
- **Editing mentions**: saving a description or comment again notifies only people mentioned for the first
  time in that text; removing a mention notifies no one.
- **Deleted comments**: deleting a comment leaves the "comment deleted" placeholder, as in Phase 1, and sends no
  notifications; notifications about it open the item.
- **Notifications about deleted items**: they stay in the panel; opening one says the item was not found. If the
  item is restored, they open it again.
- **Busy items**: every comment and status change on a watched item is a separate notification; notifications
  are not grouped in Phase 4.
- **Text too long**: formatted text is limited to 32,000 characters of text, as before. A longer description or
  comment is refused with the reason, and what was typed is kept.
- **Unsupported formatting**: pasted colors, fonts, merged table cells and embedded content are reduced to
  supported formatting and plain text; pasted tables keep their rows and columns; pasted images become
  attachments (story 2).
- **Links**: only web (http and https) and email links are clickable; they open in a new tab and never give the
  opened page any control over U-PMS.
- **Malware check unavailable or slow**: new files stay "being checked" and cannot be downloaded; they are
  checked once the scanning capability is back. Nothing is downloadable without a completed check.
- **Upload interrupted**: nothing is saved, and the person is told to try again.
- **Same file name twice**: allowed; files are told apart by their uploader and time.
- **Attachments on sub-tasks, epics, stages and milestones**: allowed, as on any work item.
- **Search words**: matching ignores letter case and matches words that start with the searched text. Symbols
  in the search are treated as plain text. An empty search with no filters lists recently updated items.
- **Saved filter for a project the person later loses**: running it leaves that project's items out; a filter
  value that no longer applies (such as that project) is shown as unavailable and can be removed.
- **Labels**: up to 50 characters each and up to 20 labels on one item. The first spelling used anywhere in the
  organization is kept for everyone (for example `launch`), whatever case others type.
- **Quick filters and hidden completed work**: quick filters apply to the cards the board shows, including when
  "Show all completed" is on.
- **Email address**: emails go to the address on the account at the time they are sent.
- **Time zones**: notification and email times are shown in the viewer's time zone, as elsewhere.

## Requirements *(mandatory)*

### Functional Requirements

**Formatted text (story 1)**

- **FR-001**: Descriptions and comments MUST support formatted text: headings, bold, italic, strikethrough,
  inline code, code blocks, bulleted and numbered lists, quotes, links and simple tables. Formatting MUST be
  available from a toolbar, from keyboard shortcuts and from typed shortcuts in the style of Jira (such as `*`
  or `-` at the start of a line for a list). A simple table has rows and columns, an optional header row, and
  the same formatting inside its cells (except other tables); people MUST be able to add and remove rows and
  columns, and tables pasted from spreadsheets or web pages MUST keep their rows and columns, with merged cells
  split and colors dropped.
- **FR-002**: Formatted text MUST be limited to 32,000 characters of text per description or comment; longer
  text is refused with the reason, and what was typed is kept.
- **FR-003**: Formatted text MUST be displayed safely: nothing in it runs, nothing is loaded from other sites,
  and only http, https and email links are clickable. Pasted content MUST be reduced to the supported
  formatting and plain text.
- **FR-004**: Descriptions and comments written before Phase 4 MUST keep their text, line breaks and clickable
  links.
- **FR-005**: The editor MUST be fully usable with the keyboard alone and with screen readers: the toolbar is
  reachable and labelled, formatting is exposed to assistive technologies, and a table's header row is read as
  the header of its columns.
- **FR-006**: Comments keep their Phase 1 rules: authors can edit their own comments (then marked as edited) and
  delete them (a "comment deleted" placeholder remains); no one can edit or delete another person's comment.

**Mentions (story 1)**

- **FR-007**: Contributors MUST be able to mention people in descriptions and comments by typing "@" and
  choosing from suggestions that match the display name or user name.
- **FR-008**: Suggestions MUST list only active members of the item's project (Project Admins, Members and
  Viewers); no one else can be mentioned or notified through a mention.
- **FR-009**: Saving a description or comment MUST notify each person mentioned in it for the first time;
  people mentioned in an earlier version of the same text are not notified again.
- **FR-010**: A mention MUST keep showing the person's display name, even after they leave the project or are
  deactivated.

**Watchers (story 1)**

- **FR-011**: An item's creator, its assignee (when assigned) and each person who comments on it MUST start
  watching it automatically.
- **FR-012**: Anyone who can see an item MUST be able to start and stop watching it, and see how many people
  watch it and who they are.
- **FR-013**: Project Admins and administrators MUST be able to add and remove watchers, choosing from the
  project's active members.
- **FR-014**: People who can no longer see an item (removed from the project, deactivated, or the item deleted)
  MUST NOT be notified about it; their watching resumes only if they can see it again.

**In-app notifications (stories 1 and 2)**

- **FR-015**: The system MUST notify a person in the app when someone else:
  - assigns them an item;
  - mentions them;
  - comments on an item they watch;
  - changes the status of an item they watch;
  - requests approval of a gate they approve and may decide on;
  - approves or sends back a gate request they made.
- **FR-016**: The system MUST notify an uploader when one of their files is blocked by the malware check.
- **FR-017**: The system MUST NOT notify anyone about their own actions, and one action MUST produce at most one
  notification per person; a mention takes precedence over the other reasons.
- **FR-018**: A bell on every screen MUST show the number of unread notifications, and the number MUST update
  within one minute while the person has the app open, without reloading the page.
- **FR-019**: The notification panel MUST list notifications newest first, each saying who did what, on which
  item and when. People MUST be able to open a notification (which marks it read and opens the item), mark it
  read or unread, and mark all as read.
- **FR-020**: Opening a notification about an item the person can no longer see MUST say that the item was not
  found, and MUST NOT reveal the item's content.
- **FR-021**: Notifications MUST be kept for 90 days, then removed.
- **FR-022**: Notifications MUST belong only to their recipient; no one else can see, open or change them.

**Attachments (story 2)**

- **FR-023**: Contributors MUST be able to attach files to any work item by choosing them, dragging them onto
  the item, or pasting or dropping images into a description or comment. Several files can be attached at
  once.
- **FR-024**: Attachments MUST be images (PNG, JPEG, GIF, WebP), PDF, Word, Excel and PowerPoint documents,
  plain-text, log and CSV files, or technical files (JSON, XML, YAML, Markdown, SQL and Jupyter notebooks), of
  up to 25 MB each. Any other type, or a larger file, MUST be refused with a message that states the limit or
  the allowed types. Technical files are downloaded, never shown or run in the browser.
- **FR-025**: Every uploaded file MUST pass the malware check before anyone can download or preview it. Until
  then it is shown as "being checked". A file that fails is blocked permanently, its uploader is notified, and
  the detection is recorded in the audit log.
- **FR-026**: Attachments MUST be listed on the item with their file name, size, uploader, upload time and
  check status. Images that passed the check MUST show a preview that can be enlarged, and images placed in
  formatted text MUST be shown there, with their file name as their text alternative.
- **FR-027**: Anyone who can see an item MUST be able to download its attachments that passed the check.
  Every download MUST check that the person can see the item at that moment.
- **FR-028**: The uploader (while they can contribute), Project Admins and administrators MUST be able to
  remove an attachment after confirming. A removed attachment disappears from the item, is recorded in the
  item's history, and is kept for audit.
- **FR-029**: Only administrators MUST be able to delete an attachment permanently; the file is destroyed and
  the audit log records who deleted which file from which item.
- **FR-030**: The attachments of a deleted item MUST be unreachable while it is deleted and come back when it
  is restored.
- **FR-031**: Adding and removing attachments MUST be recorded in the item's history.

**Search (story 3)**

- **FR-032**: A search box on every screen MUST open a work item when its key is typed (regardless of letter
  case) and confirmed. A key that does not exist and a key the person cannot see MUST give the same "not
  found" answer.
- **FR-033**: When the empty search box has focus, it MUST list the 10 items the person viewed most recently
  that they can still see.
- **FR-034**: Searching words MUST open a search page that lists items from every project the person can
  access whose title, description or comments contain words starting with the searched text, regardless of
  letter case.
- **FR-035**: The search page MUST filter by project, type, status type, priority, assignee (including "me"
  and "unassigned"), creator, label, and created, updated and due date ranges; each active filter MUST be shown
  and removable on its own, and the words and filters MUST be kept in the page's address.
- **FR-036**: Results MUST be shown 50 at a time with the number of results, sorted by last update (newest
  first) by default, and sortable by creation, key, priority and due date. Each result MUST show its key, type,
  title, project, status, assignee, labels and last update.
- **FR-037**: Search results, counts and suggestions MUST include only items the person can see; deleted items
  and items of projects they cannot access MUST never appear or be counted.
- **FR-038**: Opening a result MUST open the item's details drawer over the results; closing it MUST return
  focus to that result.
- **FR-039**: People MUST be able to save a search, with its words, filters and sort, as a named filter, and
  later run, rename, update or delete it. Filter names MUST be unique for each owner. A saved filter is private
  until its owner shares it.
- **FR-040**: The owner of a saved filter MUST be able to share it with the members of one or more projects
  they are a member of, and stop sharing it. Those members MUST find it among the filters shared with them,
  with the owner's name, and be able to run it but not change it. Sharing with a project ends when the owner
  is no longer a member of it or is deactivated.
- **FR-041**: Running a saved filter, one's own or a shared one, MUST show current results limited to the
  items the person running it can see. Filter values that no longer apply to that person (such as a project
  they cannot access) MUST be shown as unavailable and left out of the search.

**Labels and quick filters (story 4)**

- **FR-042**: Contributors MUST be able to add and remove labels on any work item. A label has no spaces or commas, has
  up to 50 characters, and an item has up to 20 labels.
- **FR-043**: Labels MUST be matched regardless of letter case across the organization; the first spelling used
  is kept for everyone.
- **FR-044**: While typing a label, matching labels used in projects the person can see MUST be suggested.
  Labels used only in projects they cannot see MUST never be suggested or revealed.
- **FR-045**: Labels MUST be shown on board cards, list rows, search results and in the item's details.
- **FR-046**: The project list and the search page MUST filter by one or more labels (items with any of them).
- **FR-047**: Boards MUST offer quick filters by label, by type and by epic (by stage on Stage-gate boards).
  They combine with "Only my tasks" and the assignee filter, each column shows how many of its cards match, and
  the chosen quick filters are kept in the page's address.
- **FR-048**: Adding and removing labels MUST be recorded in the item's history.

**Email (story 5)**

- **FR-049**: The system MUST email a person, through the company mail server, about the events they are
  notified of in the app (FR-015 and FR-016), following their email choice:
  - **all**: every such event;
  - **only events aimed at me** (the default): being assigned an item, being mentioned, being asked to approve
    a gate, a decision on their own gate request, and their own upload being blocked;
  - **none**: no email.
- **FR-050**: Each email MUST contain the project, the item key and title (or the stage and gate), what
  happened, who did it and a link. It MUST NOT contain descriptions, comment text or attachments.
- **FR-051**: Each person MUST be able to change their email choice in their profile at any time. Deactivated
  accounts receive no email.
- **FR-052**: Sending email MUST NOT block or delay the action that triggered it. When the mail server is
  unavailable, emails MUST be retried for 24 hours; emails still not delivered are given up and recorded for
  administrators.
- **FR-053**: An email's link MUST open the item (or the Stages view) after sign-in, with the same "not found"
  answer as elsewhere if the person can no longer see it.

**Across stories**

- **FR-054**: Every Phase 4 action MUST be checked on the server, whatever the screen shows, following the
  Phase 4 permission table: contributors write formatted text, mention, attach and label; anyone who can see an
  item watches it and downloads its checked files; Project Admins and administrators manage watchers and
  remove any attachment; only administrators delete attachments permanently; notifications belong to their
  recipient only; saved filters are changed only by their owner and run only by the owner and the members of
  the projects they are shared with.
- **FR-055**: The audit log MUST record malware detections, permanent attachment deletions and emails given up.
- **FR-056**: Every new and changed screen (the editor, mention suggestions, the notification panel,
  attachments and previews, the search box and page, saved filters, labels and quick filters) MUST meet WCAG
  2.2 AA, work with the keyboard alone, and work at 360 px wide.

### Key Entities *(include if feature involves data)*

- **Formatted text** (changed): descriptions and comments now hold formatted text, including mentions and
  references to attached images. Earlier plain text is kept as it was.
- **Mention**: a person referred to in a description or comment, and whether they have been notified for that
  text.
- **Watch**: a person following a work item; started automatically (creator, assignee, commenter) or by hand.
- **Notification**: a message to one person about one event on one item or gate. Who did what, when, whether
  it is read, and when it expires (after 90 days).
- **Email message**: an email waiting to be sent or sent, with its recipient, the event it is about, delivery
  attempts and outcome.
- **Attachment**: a file on a work item. File name, type, size, uploader, upload time, check status (being
  checked, passed or blocked), and removed or permanently deleted state.
- **Label**: a case-insensitive name shared across the organization, and the work items that carry it.
- **Saved filter**: a named search (words, filters and sort) owned by one person, and the projects it is
  shared with.
- **Recent view**: the last time a person opened a work item, for the search box's recent items.
- **Work item change** (extended): history now also records attachments added and removed, and labels added
  and removed.
- **Audit event** (extended): also covers malware detections, permanent attachment deletions and emails given
  up.
- **Account** (extended): the person's email choice: all, only events aimed at them (the default), or none.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A mentioned or newly assigned person sees the notification in their unread count within 1
  minute, without reloading, in 95% of cases.
- **SC-002**: With 500,000 work items, 2,000,000 comments and 300 people using the system at the same time, 95%
  of these complete within 1 second: opening an item by key, searching words across all accessible projects,
  running a saved filter, opening the notification panel, posting a comment with mentions, and applying board
  quick filters.
- **SC-003**: No uploaded file can be downloaded or previewed before it has passed the malware check, and the
  industry-standard antivirus test file is always blocked, in 100% of checks.
- **SC-004**: Across acceptance and security testing, there are zero cases of a notification, email, search
  result, count, recent item, mention suggestion or label suggestion revealing an item, person or label from a
  project the recipient cannot access.
- **SC-005**: In 100% of checks, no one is notified or emailed about their own actions, and no action produces
  more than one notification for the same person.
- **SC-006**: When the mail server is unavailable for up to 24 hours, the actions that trigger emails are not
  slowed, and 100% of the queued emails are delivered after it returns.
- **SC-007**: A set of at least 100 known cross-site scripting examples, pasted into descriptions and comments,
  produces no running script and no content loaded from other sites in 100% of cases.
- **SC-008**: After Phase 4 is installed, 100% of existing descriptions and comments read the same: the same
  text, line breaks and clickable links.
- **SC-009**: Automated accessibility checks report zero WCAG 2.2 AA violations on the new and changed screens,
  and every acceptance scenario can be completed with the keyboard alone.
- **SC-010**: In the pilot, at least 90% of users find a given work item, by key or by words, within 30 seconds
  and without help.
- **SC-011**: In the pilot, at least 80% of users rate commenting, mentions, notifications and attachments
  "easy" or "very easy", and the teams stop using chat or email to share screenshots for tracked work.

## Assumptions

- **Company services**: the organization provides a mail server that U-PMS can send email through, and a
  malware-scanning capability (the organization's antivirus service, or one deployed alongside U-PMS) that can
  check uploaded files. Every account already has an email address (Phase 1).
- **File storage**: the organization provides storage for up to 100,000 attachments of up to 25 MB each (2.5
  TB at most, far less in practice); there are no per-project quotas in Phase 4.
- **Jira parity, with two deliberate differences**: label suggestions come only from projects the person can
  see, where Jira suggests every label on the site, so that label names from restricted projects stay private;
  and saved filters are shared with project members only, where Jira can also share them with groups, the
  whole organization or the public. Both can be widened later.
- **Who is suggested for mentions**: members of the item's project only. Administrators who are not members
  can still see the item but are not suggested, so that mentions do not reach people outside the team.
- **Notification events**: the events of FR-015 follow the backlog, plus the Phase 3 gate events, which PMO
  teams rely on. Other changes (such as priority or dates) do not notify or email in Phase 4. Email follows
  the same events, filtered by each person's choice (FR-049); the default keeps inboxes quiet for people who
  watch many items.
- **Search**: words are matched on how they start, in any letter case, as Jira's quick search does; there is no
  query language (JQL), no spelling correction and no ranking by relevance beyond the chosen sort.
- **Previews**: images are previewed; other files are downloaded to be opened.
- **Scale**: as in Phases 2 and 3: up to 2,000 named users (300 active at once) and 500,000 work items, plus up
  to 2,000,000 comments and 100,000 attachments.
- **Screen names stay**: "My tasks", "Only my tasks" and "What needs to be done?" keep their names.
- Out of scope for Phase 4 (candidates for later phases):
  - portfolios, rollups, dashboards and health updates (Phase 5);
  - sharing saved filters beyond project members, filter subscriptions and a query language;
  - notification settings per event, grouping of notifications, and email for events that do not notify in the app;
  - replying by email, and chat integrations;
  - merged table cells, colors and emoji pickers in formatted text;
  - previews of non-image files, and attachment versions;
  - comment threads and reactions;
  - real-time co-editing and live updates of other screens (only the unread count updates live);
  - bulk editing of labels;
  - releases and versions, burndown and velocity charts, custom workflows and fields, time tracking.
