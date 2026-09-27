# Feature Specification: Issue Tracker MVP

**Feature Branch**: `claude/practical-lamport-l47ei8` (spec folder `specs/001-issue-tracker-mvp`)

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "I am planning to build a software like jira". Follow-up answers: it is
for one company/team; projects and issues (type, status, priority, assignee, search) are the core; the
rest of the MVP scope is left to the team's judgment ("no preference"); plan with Spec Kit before
building.

## Clarifications

### Session 2026-09-26

- Q: Should people be able to attach files, such as screenshots, to issues in the first release? → A:
  Yes, images and documents (screenshots, PDF, Office documents, text and log files), up to 10 MB each,
  checked for malware before anyone can download them.
- Q: How much downtime and data loss can the organization accept if the tracker fails? → A: No
  preference; the recommended standard level applies: 99.5% monthly availability, at most 1 hour of
  saved work lost, and service restored within 4 hours.
- Q: Should the tracker also send email notifications, or only show them inside the app? → A: In-app
  and email: assignments and mentions are also emailed through the company mail server, and each user
  can switch these emails off.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Track issues in a project (Priority: P1)

An administrator sets up the tracker for a team: they create accounts for the team members and a
project (for example "Payments", key `PAY`) with those people as members. Team members sign in, record
work as issues (epics, stories, tasks, bugs, and sub-tasks), describe it, set priority and estimates,
assign owners, and move each issue through the workflow: To Do → In Progress → In Review → Done. Anyone
in the project can browse the project's issue list, filter it, and open any issue to see its details.

**Why this priority**: This is the heart of a Jira-like tool: a shared, trustworthy record of who is
doing what. Every other capability (boards, sprints, notifications, search) builds on it, and with this
story alone a team can stop tracking work in spreadsheets and email threads.

**Independent Test**: Starting from an empty system, the initial administrator creates two user
accounts and a project with both as members. One member signs in, creates a story with a sub-task,
assigns it to the other member, and moves it to Done. The issue list and issue pages show the right
keys, fields, and statuses, and a user who is not a member cannot see the project at all.

**Acceptance Scenarios**:

1. **Given** an administrator has created an account with a temporary password, **When** the new user
   signs in for the first time, **Then** they must choose a new password before they can continue.
2. **Given** an administrator is signed in, **When** they create a project named "Payments" with key
   `PAY` and add two members, **Then** the project appears in the project lists of the administrator
   and both members, and of no one else.
3. **Given** a member of project PAY, **When** they create a Story with the summary "Allow card
   refunds", **Then** the issue is created as `PAY-1` with status To Do, priority Medium, and that
   member as reporter.
4. **Given** `PAY-1` exists, **When** a member creates a Sub-task under it, **Then** the sub-task
   receives the next key (`PAY-2`) and is listed on `PAY-1`'s page as its sub-task.
5. **Given** `PAY-1` is in To Do, **When** a member moves it to In Progress and assigns it to a
   teammate, **Then** the issue shows the new status, the new assignee, and an updated "last changed"
   time.
6. **Given** an issue in Done, **When** a member moves it back to In Progress, **Then** the issue is
   treated as reopened and its resolved time is cleared.
7. **Given** a project with 30 issues in various states, **When** a member filters the issue list by
   status "In Progress" and assignee "me", **Then** only matching issues are listed and the result
   count is shown.
8. **Given** a signed-in user who is not a member of PAY, **When** they open a direct link to `PAY-1`,
   **Then** they see a "not found" page, and PAY never appears in their project list.
9. **Given** two members have `PAY-1` open, **When** both change its summary and the second saves after
   the first, **Then** the second member is told the issue changed, sees the latest value, and still
   has their own typed text to reapply.

---

### User Story 2 - Work visually on a board (Priority: P2)

A team member opens the project board to see all current work at a glance, as cards arranged in one
column per status. They drag cards between columns to update status, reorder cards within a column to
show what matters most, filter the board to their own work, and open a card to read or edit it without
leaving the board. People who don't use a mouse can do all of this from the keyboard.

**Why this priority**: The board is where most teams run their daily stand-up, and it is the most-used
screen after the issue itself. It turns the issue list into a shared picture of progress.

**Independent Test**: With a project that has issues in every status, a member opens the board, drags a
card from To Do to In Progress, reorders cards in a column, moves another card using only the keyboard,
and filters to "Only my issues". After reloading the page, statuses and order are as they left them.

**Acceptance Scenarios**:

1. **Given** a project with issues in every status, **When** a member opens the board, **Then** they
   see the columns To Do, In Progress, In Review, and Done, each with its cards and a card count, and
   Epics are not shown as cards.
2. **Given** a card in To Do, **When** the member drags it to In Progress, **Then** the issue's status
   becomes In Progress and the change is recorded in the issue's history.
3. **Given** a card has keyboard focus, **When** the member uses the card's "Move to" action and picks
   In Review, **Then** the outcome is identical to dragging the card there.
4. **Given** several cards in To Do, **When** the member drags one card above another, **Then** the
   new order is kept and every project member sees the same order.
5. **Given** one issue resolved 3 days ago and another resolved 20 days ago, **When** the board is
   shown, **Then** only the issue resolved 3 days ago appears in the Done column.
6. **Given** the board is open, **When** the member turns on "Only my issues", **Then** only cards
   assigned to them remain and the column counts update.
7. **Given** another user changed an issue's status after the board was loaded, **When** the member
   drags that card, **Then** they are told the card changed and the board shows its current status
   instead of overwriting it.

---

### User Story 3 - Collaborate on issues (Priority: P3)

Team members discuss work where it happens: they comment on issues, attach screenshots and documents,
mention colleagues to pull them in, and follow the issues they care about. The app tells them when they
are assigned or mentioned, or when something happens on an issue they watch, and assignments and
mentions also reach them by email. Anyone in the project can read the full history of what changed on
an issue.

**Why this priority**: Keeping decisions next to the work, instead of scattered across chat and email,
is a major reason teams adopt a tracker, and the change history gives accountability. The tracker is
still useful without it, so it follows the board.

**Independent Test**: Member A comments on an issue, mentions member B, and attaches a screenshot. B
sees an unread notification that opens the issue and also receives an email about the mention. The
screenshot can be previewed and downloaded once it has passed the malware check. A edits the comment,
which is then marked as edited. The issue's history lists the earlier status change, the assignment,
the attachment, and the comment activity, with who did each and when.

**Acceptance Scenarios**:

1. **Given** a member is viewing `PAY-1`, **When** they post a comment with formatted text (bold, a
   list, a link), **Then** the comment appears with its author, time, and formatting, and the author
   now watches `PAY-1`.
2. **Given** a member writes a comment that mentions a project member named Sara, **When** it is
   posted, **Then** Sara receives a notification that opens `PAY-1`, and her unread count increases by
   one.
3. **Given** Sara watches `PAY-1`, **When** another member changes its status, **Then** Sara is
   notified, and the member who made the change is not.
4. **Given** a comment's author edits it, **When** others view the issue, **Then** the comment shows as
   edited and the edit is recorded in the history.
5. **Given** a comment's author deletes it, **When** others view the issue, **Then** a "comment
   deleted" placeholder is shown and the deletion remains in the history.
6. **Given** `PAY-1` has been changed several times, **When** a member opens its history, **Then**
   every change is listed in time order with who made it, when, the field, the old value, and the new
   value.
7. **Given** a member types a mention, **When** suggestions appear, **Then** only people who can access
   the project are suggested, and no one else can be notified through a mention.
8. **Given** Sara has notification emails switched on, **When** another member assigns her an issue or
   mentions her, **Then** she also receives an email with the issue key, summary, what happened, and a
   link to the issue; if she has switched emails off, she gets only the in-app notification.
9. **Given** a member is viewing `PAY-1`, **When** they attach a 2 MB screenshot, **Then** it is listed
   on the issue with its file name, size, uploader, and time, and once it passes the malware check,
   anyone who can see `PAY-1` can preview and download it.
10. **Given** a member tries to attach a 15 MB file or a program file (such as `.exe`), **When** they
    upload it, **Then** the upload is refused with a message stating the size limit or the allowed
    file types.
11. **Given** an uploaded file is found to contain malware, **When** the check completes, **Then** the
    file can never be downloaded, the uploader is told, and the detection is recorded in the audit log.

---

### User Story 4 - Plan and run sprints (Priority: P4)

A team that works in sprints keeps an ordered backlog, plans a sprint by moving the top items into it,
starts the sprint with dates and a goal, works through it on the board, and at the end completes the
sprint: unfinished work is carried to the backlog or the next sprint, and the team reviews what was
completed.

**Why this priority**: Time-boxed planning is how many software teams work and is a signature
capability of Jira-like tools, but teams already get value from stories 1–3 with continuous-flow work.

**Independent Test**: In a sprint-based project with 15 estimated backlog issues, a member reorders the
backlog, creates a sprint, moves 5 issues into it, and starts it for two weeks. Three of those issues
are moved to Done on the board. The member completes the sprint, sends the other two back to the
backlog, and views the sprint report.

**Acceptance Scenarios**:

1. **Given** a sprint-based project, **When** a member opens the backlog, **Then** they see the active
   and planned sprints with their issues, issue counts, and story-point totals, followed by the
   backlog: every open issue not in a sprint, in priority order, with Epics and Sub-tasks not listed
   on their own.
2. **Given** the backlog, **When** the member moves an issue to the top (by dragging or with the "Move
   to top" action), **Then** the new order is kept for everyone.
3. **Given** a planned sprint containing issues, **When** the member starts it with a start date, an
   end date, and a goal, **Then** the sprint becomes active and the board shows only that sprint's
   issues.
4. **Given** a sprint is already active, **When** a member tries to start another sprint in the same
   project, **Then** they are told only one sprint can be active at a time, and nothing changes.
5. **Given** the active sprint has 3 issues in Done and 2 that are not, **When** the member completes
   the sprint and chooses to move unfinished issues to the backlog, **Then** the 2 issues return to
   the backlog and the sprint becomes read-only.
6. **Given** a completed sprint, **When** a member opens its sprint report, **Then** it shows the
   issues and story points committed at the start, added during the sprint, removed during the sprint,
   completed, and not completed.
7. **Given** an issue with sub-tasks, **When** it is moved into a sprint, **Then** its sub-tasks move
   with it.

---

### User Story 5 - Find work quickly (Priority: P5)

Anyone can jump straight to an issue by typing its key, search across all the projects they can access
using words and filters, save useful searches to rerun later, and start the day on a "My Work" page
that shows what is assigned to them.

**Why this priority**: Once there are thousands of issues across many projects, finding things fast
matters a lot. Early on, each project's filterable issue list (story 1) covers the basic need, so this
comes later.

**Independent Test**: With issues in three projects, one of which the user is not a member of, the user
searches for "refund", narrows the results by status and assignee, saves the search, and reruns it.
They type an issue key into the search box to open that issue directly. My Work lists their open
assigned issues, and no result ever comes from the project they cannot access.

**Acceptance Scenarios**:

1. **Given** the user can access `PAY-12`, **When** they type "PAY-12" into the search box and confirm,
   **Then** `PAY-12` opens directly.
2. **Given** issues mentioning "refund" in their summary, description, or comments across several
   projects, **When** the user searches for "refund", **Then** matching issues from every project they
   can access are listed, most recently updated first.
3. **Given** search results, **When** the user adds the filters project = PAY, status = To Do, and
   assignee = unassigned, **Then** the results narrow accordingly, and each active filter is visible
   and can be removed on its own.
4. **Given** a filtered search, **When** the user saves it as "Unassigned PAY work", **Then** it
   appears in their saved filters, and running it later shows current results.
5. **Given** a project the user is not a member of contains issues mentioning "refund", **When** they
   search for "refund", **Then** none of that project's issues appear.
6. **Given** the user signs in, **When** the My Work page opens, **Then** it lists their open assigned
   issues grouped by project, and the issues they viewed most recently.

---

### User Story 6 - Manage users and project access (Priority: P6)

Administrators manage the organization's accounts: they deactivate people who leave, reset forgotten
passwords, and grant or remove administrator rights. Project Admins manage their own project's members
and roles (Project Admin, Member, Viewer) without needing a system administrator. Administrators can
review an audit log of security events.

**Why this priority**: This is needed to roll the tool out safely beyond a pilot team. Story 1 already
covers the basics (administrator-created accounts and project members), so the full access lifecycle
can follow.

**Independent Test**: An administrator deactivates a user, who can then no longer sign in but still
appears on their past issues; resets another user's password; and makes a member the Project Admin of
PAY. That Project Admin adds a Viewer, who can read PAY's issues but not change anything. The audit log
shows each of these events.

**Acceptance Scenarios**:

1. **Given** an active user, **When** an administrator deactivates them, **Then** the user can no
   longer sign in, is no longer offered as an assignee, and still appears (marked as deactivated) on
   earlier issues and comments.
2. **Given** a user has forgotten their password, **When** an administrator resets it, **Then** the
   user is given a new temporary password and must replace it at their next sign-in.
3. **Given** a Project Admin of PAY, **When** they add a user as a Viewer, **Then** that user can see
   PAY's issues, board, comments, and history, but cannot create, edit, move, comment on, or delete
   issues.
4. **Given** a Member who is not a Project Admin, **When** they try to manage members or delete an
   issue, **Then** those actions are not offered, and attempts made by other means are refused.
5. **Given** a user enters a wrong password 5 times in a row, **When** they try again within 15
   minutes, **Then** sign-in is refused even with the correct password, and both the failures and the
   lockout appear in the audit log.
6. **Given** several role changes and deactivations have happened, **When** an administrator filters
   the audit log by user and date range, **Then** the matching events are listed with who acted, on
   whom, when, and what changed.
7. **Given** only one active administrator remains, **When** someone tries to deactivate that account
   or remove its administrator role, **Then** the action is refused.

---

### Edge Cases

- **Deactivated or removed assignee**: the issue keeps showing that person, marked as deactivated or
  no longer a member; they cannot be chosen as a new assignee, and the issue can be reassigned.
- **Removed project member**: loses access to the project immediately, including on screens they
  already have open (their next action is refused).
- **Simultaneous issue creation**: two issues created at the same moment in one project never get the
  same key, and keys are never reused, even after an issue is deleted.
- **Deleting a parent issue**: its sub-tasks are deleted with it, after a confirmation that shows how
  many; deleting an Epic removes the Epic link from its child issues, which remain.
- **Changing issue type**: an issue can switch freely between Story, Task, and Bug; converting to or
  from Epic or Sub-task is not supported in the MVP (the user creates a new issue instead).
- **Parent completed with open sub-tasks**: moving a parent to Done is allowed, but the user is warned
  and shown the sub-tasks that are still open.
- **Sprint membership**: sub-tasks always belong to the same sprint as their parent, and an issue
  counts as completed in a sprint according to its own status, not its sub-tasks'.
- **Changing board style**: a project can switch between continuous-flow and sprint-based only while
  no sprint is active.
- **Archived project**: becomes read-only for everyone, is hidden from project lists and search by
  default, and its issues stay reachable by direct link for its members.
- **Deleted issue**: direct links show "not found" to regular users; administrators can find and
  restore it.
- **Session about to expire**: the user is warned at least 2 minutes before an idle session ends and
  can choose to stay signed in; after it ends they must sign in again.
- **Invalid input**: a missing or overlong summary, a duplicate project key, or an end date before a
  start date is rejected with a clear message, and everything else the user typed is kept.
- **Labels that differ only by letter case**: "Backend" and "backend" are treated as the same label.
- **Malware check unavailable or slow**: newly uploaded files stay marked "being checked" and cannot
  be downloaded until the check completes; everything else keeps working.
- **Interrupted upload**: no partial file is attached, and the user can retry.
- **Mail server unavailable**: the action that triggered an email still succeeds immediately, in-app
  notifications are unaffected, and the email is sent once the mail server is reachable again.
- **Deactivated users**: receive no emails and cannot download attachments, even through old links.
- **Sensitive file uploaded by mistake**: an Administrator can delete the file permanently, and the
  deletion itself is recorded in the audit log.
- **Empty states**: no projects, no issues, no search results, and no notifications each show a
  helpful message and the next action to take (for example "Create issue" or "Clear filters").

## Requirements *(mandatory)*

### Functional Requirements

**Accounts, sign-in, and roles**

- **FR-001**: System MUST require every user to sign in with an account managed within the application
  before any content is shown; there is no public self-registration.
- **FR-002**: System MUST let the first administrator account be created during first-time setup, and
  that setup path MUST stop being available once an administrator exists.
- **FR-003**: Administrators MUST be able to create user accounts with a display name, username, email
  address, and temporary password; users MUST replace a temporary password at their next sign-in.
- **FR-004**: System MUST require passwords of at least 12 characters that do not contain the username,
  and MUST lock an account for 15 minutes after 5 consecutive failed sign-in attempts.
- **FR-005**: System MUST end sessions after 30 minutes of inactivity (a value administrators can
  change), warning the user at least 2 minutes beforehand with an option to stay signed in.
- **FR-006**: Users MUST be able to change their own password, display name, and time zone.
- **FR-007**: Administrators MUST be able to deactivate and reactivate accounts, reset a user's password
  to a new temporary one, and grant or remove the Administrator role; the last active Administrator
  cannot be deactivated or lose that role.
- **FR-008**: System MUST support two organization roles: Administrator (manages accounts and projects
  and can access every project with full rights) and User (can access only the projects they belong
  to).
- **FR-009**: Each project member MUST hold exactly one project role: Project Admin (manages the
  project's settings and members and can delete issues), Member (creates and works on issues), or
  Viewer (read-only). Members and Project Admins are called *contributors* below.
- **FR-010**: System MUST enforce every permission on every request, whatever the screen shows; a user
  without access to a project or issue MUST get a "not found" response that does not reveal whether it
  exists.
- **FR-011**: System MUST record security events (successful and failed sign-ins, lockouts, password
  changes and resets, account creation, deactivation and reactivation, role and membership changes,
  malware detections, and permanent attachment deletions) in an audit log that Administrators can
  filter by date range, user, and event type.

**Projects**

- **FR-012**: Administrators MUST be able to create projects with a unique name, a unique key (2–10
  uppercase letters or digits, starting with a letter), an optional description, and a board style:
  continuous flow (Kanban) or sprint-based (Scrum).
- **FR-013**: A project's key MUST NOT change after the project is created.
- **FR-014**: Project Admins MUST be able to edit their project's name, description, and board style,
  and add members, remove members, or change members' roles.
- **FR-015**: Administrators MUST be able to archive and restore projects; archived projects are
  read-only and are hidden from project lists and search unless the user chooses to include them.
- **FR-016**: Users MUST see a list of the projects they can access.

**Issues**

- **FR-017**: Contributors MUST be able to create issues of type Epic, Story, Task, Bug, or Sub-task,
  with a required summary of up to 255 characters and an optional formatted description (headings,
  bold, italics, lists, links, code) of up to 32,000 characters.
- **FR-018**: System MUST give each issue a unique key made of the project key and the next number in
  that project (for example `PAY-42`); keys MUST never be reused.
- **FR-019**: Each issue MUST have a status; a priority (Highest, High, Medium, Low, Lowest; default
  Medium); an optional assignee, who must be an active contributor in the project (with a one-click
  "Assign to me"); a reporter (its creator); zero or more labels; an optional due date; an optional
  estimate in story points (0–999, at most one decimal place); and created, updated, and resolved
  times.
- **FR-020**: Epics MUST NOT have a parent; Stories, Tasks, and Bugs MAY have one Epic parent in the
  same project; every Sub-task MUST have exactly one parent that is a Story, Task, or Bug in the same
  project.
- **FR-021**: Every issue MUST follow the workflow To Do → In Progress → In Review → Done: new issues
  start in To Do, and contributors can move an issue from any status to any other status.
- **FR-022**: Moving an issue into Done MUST set its resolved time; moving it out of Done MUST clear
  it.
- **FR-023**: Contributors MUST be able to edit any issue field from the issue page, and each saved
  change MUST be confirmed visibly.
- **FR-024**: System MUST detect when someone else changed an issue after the user loaded it; the
  user's save MUST NOT silently overwrite that change, and the user MUST see the latest values while
  keeping their own unsaved input.
- **FR-025**: Project Admins and Administrators MUST be able to delete an issue after confirming;
  deleted issues disappear from every view but are retained, and Administrators MUST be able to list
  and restore them.
- **FR-026**: Each project MUST have an issue list, shown in pages of 50, that can be sorted by key,
  summary, type, status, priority, assignee, due date, and last update, and filtered by type, status,
  priority, assignee (including "me" and "unassigned"), label, and words in the summary or
  description.
- **FR-027**: Labels MUST be free-form, shared across the organization, matched regardless of letter
  case, and suggested from existing labels while typing.

**Board**

- **FR-028**: Each project MUST have a board with one column per status, showing each issue as a card
  with its key, summary, type, priority, assignee, and estimate, and showing a card count per column.
- **FR-029**: On a continuous-flow board, cards MUST include every open issue except Epics, and the
  Done column MUST show only issues resolved in the last 14 days; on a sprint-based board, cards MUST
  include only the active sprint's issues.
- **FR-030**: Contributors MUST be able to change an issue's status by moving its card to another
  column, and change its position within a column, either by drag-and-drop or by an equivalent
  keyboard and menu action.
- **FR-031**: The board MUST offer quick filters for assignee (including "Only my issues"), type,
  label, Epic, and words in the summary.
- **FR-032**: Opening a card MUST show the full issue, editable as on the issue page, without leaving
  the board.

**Collaboration**

- **FR-033**: Contributors MUST be able to comment on issues with formatted text of up to 32,000
  characters; authors MUST be able to edit their own comments (shown as edited) and delete them (a
  "comment deleted" placeholder remains).
- **FR-034**: Users MUST be able to mention people who can access the project in descriptions and
  comments, and mentioned people MUST be notified.
- **FR-035**: The reporter, the assignee, and anyone who comments MUST automatically watch an issue;
  anyone who can see an issue MUST be able to start or stop watching it.
- **FR-036**: System MUST notify users in the app when someone else assigns them an issue, mentions
  them, or comments on or changes the status of an issue they watch; users MUST see an unread count,
  open the related issue from a notification, and mark notifications as read. No one is notified about
  their own actions.
- **FR-037**: Each issue MUST show a complete, time-ordered history of changes (who, when, which
  field, old value, new value), including comment edits and deletions; no user can edit or remove
  history entries.
- **FR-053**: Contributors MUST be able to attach files to an issue: images (PNG, JPEG, GIF, WebP),
  PDF, Word, Excel, and PowerPoint documents, and plain-text, log, and CSV files, up to 10 MB each;
  any other file type, or a larger file, MUST be refused with a message explaining why.
- **FR-054**: Every uploaded file MUST be checked for malware before anyone can download it; until the
  check passes the file is shown as "being checked", and a file that fails is permanently blocked from
  download and its uploader is told.
- **FR-055**: Attachments MUST be listed on the issue with file name, size, uploader, and upload time;
  images MUST show a preview; anyone who can see the issue MUST be able to download files that passed
  the check.
- **FR-056**: The uploader, Project Admins, and Administrators MUST be able to remove an attachment
  (recorded in the issue history and retained for audit); permanent deletion of an attachment, for
  example one uploaded with sensitive data by mistake, MUST be limited to Administrators.
- **FR-057**: System MUST also send an email when someone else assigns a user an issue or mentions
  them; the email MUST contain the issue key, summary, what happened, and a link to the issue, but not
  the description or comment text, and each user MUST be able to switch these emails off.
- **FR-058**: A failure to send email MUST NOT block or delay the action that triggered it; undelivered
  emails MUST be retried for at least 24 hours.

**Sprints and backlog** (sprint-based projects)

- **FR-038**: System MUST provide a backlog that lists, in priority order, the open issues that are not
  in any sprint (Sub-tasks travel with their parent and Epics are excluded), together with the planned
  and active sprints and their issues.
- **FR-039**: Contributors MUST be able to reorder the backlog by drag-and-drop or with "Move to top",
  "Move to bottom", "Move up", and "Move down" actions; the board and the backlog share one priority
  order.
- **FR-040**: Contributors MUST be able to create sprints (with a name defaulting to the project key
  plus a sequence number, for example "PAY Sprint 3", and an optional goal) and move issues between
  the backlog and any planned or active sprint.
- **FR-041**: Contributors MUST be able to start a planned sprint with a start date and an end date
  (defaulting to two weeks after the start); at most one sprint per project can be active.
- **FR-042**: Each planned and active sprint MUST show its issue count and total story points; the
  active sprint MUST also show completed versus remaining story points.
- **FR-043**: Contributors MUST be able to complete the active sprint, choosing whether its unfinished
  issues move to the backlog or to a planned sprint; completed sprints are read-only.
- **FR-044**: Each completed sprint MUST have a report showing the issues and story points committed at
  the start, added during the sprint, removed during the sprint, completed, and not completed.

**Search and personal views**

- **FR-045**: Users MUST be able to open an issue directly by entering its key in a search box
  available on every screen.
- **FR-046**: Users MUST be able to search all projects they can access by words in the summary,
  description, or comments, combined with filters for project, type, status, priority, assignee
  (including "me" and "unassigned"), reporter, label, sprint, Epic, and created, updated, and due date
  ranges; results MUST be sortable, shown in pages of 50, and ordered by most recent update by default.
- **FR-047**: Search results MUST include only issues from projects the user can access, and MUST
  exclude archived projects unless the user chooses to include them.
- **FR-048**: Users MUST be able to save a search as a named private filter, and later run, rename, or
  delete it.
- **FR-049**: System MUST provide a "My Work" page, shown after sign-in, that lists the user's open
  assigned issues grouped by project and the 10 issues they viewed most recently.

**Across the product**

- **FR-050**: Every screen MUST be fully usable with a keyboard alone and MUST meet WCAG 2.2 Level AA.
- **FR-051**: Times MUST be shown in the viewer's chosen time zone (defaulting to an organization time
  zone set by an administrator); due dates are calendar dates without a time.
- **FR-052**: User-written text (descriptions, comments, names) MUST be displayed safely, so that it
  can never run code or alter the page for other users.

### Key Entities *(include if feature involves data)*

- **User**: a person with an account. Display name, username, email address, time zone, organization
  role (Administrator or User), status (active or deactivated), and whether they receive notification
  emails.
- **Project**: a body of work such as a product or team. Name, unique permanent key, description,
  board style (continuous flow or sprint-based), and archived state. Has members.
- **Project Membership**: links a user to a project with one project role (Project Admin, Member, or
  Viewer).
- **Issue**: a unit of work. Key, type (Epic, Story, Task, Bug, Sub-task), summary, description,
  status, priority, assignee, reporter, labels, due date, estimate, optional parent, position in the
  project's priority order, sprint, created, updated, and resolved times, and deleted state. An issue
  is *open* when its status is anything other than Done.
- **Workflow Status**: the four fixed statuses (To Do, In Progress, In Review, Done), in board order.
- **Label**: an organization-wide tag attached to issues.
- **Comment**: formatted text on an issue, with its author, times, and edited or deleted state.
- **Attachment**: a file on an issue, with its file name, type, size, uploader, upload time,
  malware-check status (being checked, passed, or blocked), and removed state.
- **Change Record**: one entry in an issue's history: the issue, who, when, which field, the old value,
  and the new value.
- **Watch**: a user following an issue.
- **Notification**: a message to one user about an event on an issue (assignment, mention, comment,
  or status change), with a read or unread state and, for assignments and mentions, whether it was
  also sent by email.
- **Sprint**: a time-box in one project. Name, goal, state (planned, active, or completed), start and
  end dates, completion time, and a record of which issues it held at the start, which were added or
  removed, and which were completed.
- **Saved Filter**: a named, private set of search criteria owned by one user.
- **Audit Event**: a security-relevant event: its type, who acted, who or what it affected, when, and
  details.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of first-time users can sign in and create a complete issue (summary, type,
  priority, assignee) in under 2 minutes without training.
- **SC-002**: With 500,000 issues and 300 people using the system at the same time, 95% of issue page
  loads, board loads, status changes, and filtered searches complete within 1 second.
- **SC-003**: A team can create a sprint, fill it with 20 issues from the backlog, and start it in
  under 10 minutes.
- **SC-004**: 100% of issue changes appear in the issue's history with who made them, when, and the
  old and new values.
- **SC-005**: Across acceptance and security testing, there are zero cases of a user seeing or changing
  anything in a project they do not belong to, or performing an action their role does not allow.
- **SC-006**: Zero silent overwrites: in concurrent-editing tests, every conflicting change is shown to
  the user who saved second.
- **SC-007**: In 90% of attempts, users find a known issue (by its key or by words from its summary) in
  under 10 seconds.
- **SC-008**: Automated accessibility checks report zero WCAG 2.2 AA violations on the primary screens,
  and every acceptance scenario above can be completed using only a keyboard.
- **SC-009**: Within a 4-week pilot, at least one team runs all of its planning, daily tracking, and
  discussion in the tool, and at least 80% of pilot users rate it "easy" or "very easy" to use.
- **SC-010**: No uploaded file can be downloaded before it has passed the malware check, and the
  industry-standard harmless antivirus test file is always blocked.
- **SC-011**: The system is available at least 99.5% of each calendar month.
- **SC-012**: After any failure, no more than 1 hour of saved work is lost and service is restored
  within 4 hours, as shown by a restore drill before go-live and at least twice a year after that.

## Assumptions

- The tool serves one organization. Everyone who uses it is an employee or contractor of that
  organization, and accounts are created by administrators.
- Users sign in with accounts managed inside the application (username and password). Company single
  sign-on is planned as a later feature and is out of scope here.
- Temporary passwords are never sent by email; administrators give them to users through an existing
  secure company channel.
- The organization provides a mail server the system can send email through, and a malware-scanning
  capability (the organization's antivirus service, or one deployed alongside the system) that can
  check uploaded files.
- Reliability follows the standard level chosen during clarification (SC-011, SC-012); it can be met
  without duplicate servers and raised later if needed.
- Security defaults follow common industry practice: passwords of at least 12 characters, a 15-minute
  lockout after 5 failed attempts, and a 30-minute idle timeout.
- Projects are private to their members and to Administrators (least privilege); there are no
  organization-wide public projects.
- One fixed workflow (To Do, In Progress, In Review, Done) with free movement between statuses, and
  five fixed issue types, are enough for the first teams.
- Estimates use story points; there is no time tracking.
- Changes made by others appear when a screen is refreshed or when the user next acts on the item;
  live updates of open screens are not required.
- Expected scale: up to 2,000 named users, 300 of them active at the same time, and 500,000 issues.
- The interface is in English. Current versions of Microsoft Edge, Google Chrome, and Mozilla Firefox
  on desktop are the primary targets, and screens stay usable on phones down to 360 pixels wide.
- Issues, comments, history, and audit events are kept indefinitely; there is no automatic purge.
- Out of scope for this MVP (candidates for later features): self-service password reset; emails for
  events other than assignments and mentions; company single sign-on; custom workflows, statuses,
  fields, and issue types; links between issues (blocks, relates to, duplicates); time tracking;
  dashboards and charts beyond the sprint report (such as burndown and velocity); import and export;
  integrations
  with other tools (source control, CI, chat) and a public programming interface; automation rules;
  native mobile apps; and multiple organizations.
