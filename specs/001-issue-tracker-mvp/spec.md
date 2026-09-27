# Feature Specification: U-PMS MVP

**Feature Branch**: `claude/practical-lamport-l47ei8` (spec folder `specs/001-issue-tracker-mvp`)

**Created**: 2026-09-26

**Last Updated**: 2026-09-27 (re-specified after the prototype review)

**Status**: Draft

**Input**: User description: "I am planning to build a software like jira". Follow-up answers: it is
for one company/team; projects and issues (type, status, priority, assignee, search) are the core; the
rest of the MVP scope is left to the team's judgment ("no preference"); plan with Spec Kit before
building. Prototype review (2026-09-27): the product is named U-PMS (UBL Project Management System);
basic sign-in from Day 1, with user administration screens later; add a timeline with interactive bars
across weeks, months and quarters; deliver modules in the order Projects → Tasks → Backlog → Timeline
→ Board & Dashboards; keep one unified, flexible hierarchy (Portfolio/Project → Tasks/Issues) that
serves both IT (agile) and PMO (structured) work.

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

### Session 2026-09-27

- Q: Should sign-in exist from the first release, or arrive with user administration? → A: Basic
  sign-in from Day 1 for security compliance; user administration screens come later.
- Q: In what order should the modules be delivered? → A: Projects → Tasks → Backlog → Timeline →
  Board & Dashboards.
- Q: Should U-PMS have a timeline? → A: Yes: interactive bars across weeks, months and quarters, as in
  the prototype.
- Q: What must the underlying structure support? → A: One unified, flexible hierarchy (Portfolio/Project
  → Tasks/Issues) that handles both agile (IT) and structured (PMO) work.
- Q: What should structured (PMO-style) projects support in the first release? → A: Phases, milestones
  and finish-to-start dependencies, shown Gantt-style on the timeline; late dependencies are flagged
  and nothing is rescheduled automatically.
- Q: How should portfolios work? → A: One level (Portfolio → Project). Each project belongs to at most
  one portfolio; portfolio managers can see every project in their portfolio and create new projects
  in it.

## User Scenarios & Testing *(mandatory)*

Terms used in this spec:

- **Work item**: any unit of work in a project. Agile teams usually call these issues; PMO teams call
  them tasks. One model serves both.
- **Template**: how a project works. **Scrum** and **Kanban** are agile templates; **Structured** is a
  phase-based (waterfall-style) template for PMO projects. The template sets the project's workflow,
  its work item types, and its views.
- **Hierarchy**: Portfolio → Project → container → standard item → sub-task. Containers are **Epics**
  in agile projects and **Phases** in structured projects. Standard items are Stories, Tasks and Bugs
  (agile) or Tasks and Milestones (structured).
- **Status category**: every status belongs to one of three categories (To Do, In Progress, Done), so
  progress and dashboards compare agile and structured projects on equal terms.
- **Contributor**: a project member with the Project Admin or Member role (Viewers are read-only).

The stories below are in delivery order. Stories 1–5 are the core modules agreed after the prototype
review; stories 6–8 follow them.

### User Story 1 - Projects: set up portfolios and projects (Priority: P1)

After a one-time setup creates the first administrator, the administrator signs in and gives
colleagues accounts. They create a portfolio (for example "Digital Channels") and name its portfolio
manager, and projects are created in it with the template that fits the team: Scrum or Kanban for
agile IT teams, Structured for phase-based PMO projects. Each project gets a short key (for example
`MA`), a lead, and members with roles. Everyone signs in and sees exactly the projects they are allowed
to see.

**Why this priority**: Everything in U-PMS lives in a project, and projects live in portfolios. This
story creates the secure, organized structure every later module builds on, for IT and PMO alike, and
puts sign-in and access control in place from Day 1. On its own it already gives the PMO a single,
access-controlled register of every project by portfolio, with its lead, template and team.

**Independent Test**: From an empty system, complete first-time setup, sign in as the administrator,
add three users, create portfolio "Digital Channels" with one of them as portfolio manager, and create
a Scrum project and a Structured project in it with different members. Each member sees only their
project; the portfolio manager sees both, read-only; the third user sees neither.

**Acceptance Scenarios**:

1. **Given** a newly installed system, **When** first-time setup is completed, **Then** an
   administrator account exists and setup can never be run again.
2. **Given** an administrator has added an account with a temporary password, **When** that person
   signs in for the first time, **Then** they must choose a new password before they can continue.
3. **Given** someone enters a wrong password 5 times in a row, **When** they try again within 15
   minutes, **Then** sign-in is refused even with the correct password.
4. **Given** a signed-in user has been inactive for 28 minutes, **When** the warning appears and they
   choose to stay signed in, **Then** their session continues; if they do nothing, they are signed out
   at 30 minutes.
5. **Given** an administrator, **When** they create portfolio "Digital Channels" and make Sara its
   portfolio manager, **Then** the portfolio appears in the portfolio lists of the administrator and
   Sara.
6. **Given** Sara manages "Digital Channels", **When** she creates project "Mobile App" with the Scrum
   template and project "Core Banking Upgrade" with the Structured template in that portfolio,
   **Then** both appear in the portfolio, each with a key suggested from its name (for example `MA`
   and `CBU`) that can be changed before the project is created.
7. **Given** a project, **When** its Project Admin adds one colleague as a Member and another as a
   Viewer, **Then** both see the project in their project list, and only the Member can change work
   in it.
8. **Given** a signed-in user who is not a member of "Mobile App" and does not manage its portfolio,
   **When** they open a direct link to the project, **Then** they see "not found", and the project
   never appears in their lists.
9. **Given** Sara manages "Digital Channels" but is not a member of "Core Banking Upgrade", **When**
   she opens that project, **Then** she can see everything in it but cannot change anything.

---

### User Story 2 - Tasks: define and track work items (Priority: P2)

Team members break work down into work items. Agile teams use epics, stories, tasks, bugs and
sub-tasks; PMO teams use phases, tasks, milestones and sub-tasks. Every item gets a key (for example
`MA-12`), a status from its project's workflow, a priority, an assignee, dates and an estimate. People
open any item in a detail panel to edit it in place and read its full history, and they browse each
project's list with filters.

**Why this priority**: The work item is the heart of the product. The same model holds an agile
backlog and a PMO plan, which is what keeps IT and PMO in one system.

**Independent Test**: In the Scrum project, create an epic with a story and a sub-task; in the
Structured project, create a phase with two tasks and a milestone. Move items through each project's
statuses, edit fields in the detail panel, filter the list, and confirm the History tab lists every
change. A Viewer can read the items but not change them.

**Acceptance Scenarios**:

1. **Given** a Member of "Mobile App" (key `MA`), **When** they create a Story "Allow card refunds",
   **Then** it becomes `MA-1` with status To Do, priority Medium, and that Member as reporter.
2. **Given** the Structured project "Core Banking Upgrade" (key `CBU`), **When** a Member creates phase
   "Planning", a task "Finalize vendor contract" in it, and milestone "Planning sign-off" on a chosen
   date, **Then** the task starts as Not Started, and the milestone has that single date and no
   duration.
3. **Given** `MA-1` exists, **When** a Member adds a sub-task to it, **Then** the sub-task receives the
   next key and is listed under `MA-1`.
4. **Given** an agile work item, **When** it moves To Do → In Progress → In Review → Done, **Then** each
   change is saved and reaching Done records a resolved time; **and given** a structured task, **When**
   it moves between Not Started, In Progress, On Hold and Completed, **Then** reaching Completed
   records a resolved time.
5. **Given** a work item in a Done-category status (Done or Completed), **When** it is moved back to
   another status, **Then** it is treated as reopened and its resolved time is cleared.
6. **Given** the detail panel of `MA-1`, **When** a Member changes its assignee, priority and due date,
   **Then** each change is confirmed visibly and appears in the History tab with who, when, the old
   value and the new value.
7. **Given** a project with 30 work items, **When** a Member filters the list by status "In Progress"
   and assignee "me", **Then** only matching items are listed and the result count is shown.
8. **Given** two Members have `MA-1` open, **When** both change its summary and the second saves after
   the first, **Then** the second is told the item changed, sees the latest value, and still has their
   own typed text to reapply.
9. **Given** a Viewer of the project, **When** they open a work item, **Then** they can read everything
   but cannot change it.
10. **Given** a Project Admin deletes a story that has sub-tasks, **When** others look for them,
    **Then** the story and its sub-tasks are gone from every view, and an administrator can restore
    them.

---

### User Story 3 - Backlog: prioritize work and run sprints (Priority: P3)

A Scrum team keeps an ordered backlog, plans a sprint by moving the top items into it, starts the
sprint with dates and a goal, and at the end completes it: unfinished work goes back to the backlog
or into the next sprint, and the team reviews what was completed. Until the board arrives (story 5),
sprint work is updated from the list and the detail panel.

**Why this priority**: Time-boxed planning is how many IT teams work and a signature capability of
Jira-like tools. It builds directly on work items (story 2).

**Independent Test**: In the Scrum project with 15 estimated backlog items, reorder the backlog,
create a sprint, move 5 items into it and start it for two weeks. Mark 3 of them Done, complete the
sprint sending the other 2 back to the backlog, and read the sprint report.

**Acceptance Scenarios**:

1. **Given** a Scrum project, **When** a member opens the backlog, **Then** they see the active and
   planned sprints with their items, item counts and story-point totals, followed by the backlog:
   every open item not in a sprint, in priority order, with epics and sub-tasks not listed on their
   own.
2. **Given** the backlog, **When** a Member moves an item to the top by dragging it or with the "Move to
   top" action, **Then** the new order is kept for everyone.
3. **Given** a Scrum project, **When** a Member creates a sprint, **Then** it gets a default name such
   as "MA Sprint 1", and items can be moved between the backlog and the sprint.
4. **Given** a planned sprint containing items, **When** a Member starts it with a start date, an end
   date and a goal, **Then** the sprint becomes active.
5. **Given** a sprint is already active, **When** a Member tries to start another sprint in the same
   project, **Then** they are told only one sprint can be active at a time, and nothing changes.
6. **Given** the active sprint has 3 items in Done and 2 that are not, **When** a Member completes it
   and chooses to move unfinished items to the backlog, **Then** the 2 items return to the backlog and
   the sprint becomes read-only.
7. **Given** a completed sprint, **When** a member opens its report, **Then** it shows the items and
   story points committed at the start, added during the sprint, removed during the sprint, completed,
   and not completed.
8. **Given** an item with sub-tasks, **When** it is moved into a sprint, **Then** its sub-tasks move
   with it.

---

### User Story 4 - Timeline: plan across weeks, months and quarters (Priority: P4)

Project leads and the PMO plan and communicate over time. Each project's timeline shows its epics or
phases as bars across weeks, months or quarters, with milestones as markers and dependencies as arrows
between work items. People drag a bar to reschedule it and drag its ends to change dates, and they
expand an epic or phase to see its work items. Portfolio managers see a portfolio timeline with one
bar per project.

**Why this priority**: The timeline is how PMO plans and reports, and how agile leads share roadmaps.
It builds on work items (story 2) and sprints (story 3).

**Independent Test**: In the Structured project, schedule three phases and a milestone, add a
dependency between two tasks, drag one phase a month later and extend another; the dates, the history
and the late-dependency flag update accordingly. The portfolio manager's portfolio timeline shows both
projects.

**Acceptance Scenarios**:

1. **Given** a project with dated epics or phases, **When** its timeline opens, **Then** each appears
   as a bar from its start date to its due date with its progress and a marker for today, and the scale
   can be switched between weeks, months and quarters.
2. **Given** a phase bar, **When** a Member drags it two weeks later, **Then** its start and due dates
   both move by 14 days, and the change is recorded in its history.
3. **Given** a bar, **When** a Member drags its right end, **Then** only the due date changes, and a bar
   can never end before it starts.
4. **Given** a bar has keyboard focus, **When** a Member uses the arrow keys, **Then** the bar moves by
   a day at a time (or only its end changes, with a modifier key), with the same result as dragging.
5. **Given** a phase with tasks, **When** it is expanded, **Then** its work items appear as bars; in a
   Scrum project, items without their own dates show their sprint's dates in a visibly different style.
6. **Given** milestone "Planning sign-off", **When** the timeline is shown, **Then** it appears as a
   marker on its date and can be dragged to another date.
7. **Given** a Member makes task B depend on task A (B cannot start until A is finished), **When** A's
   due date is moved later than B's start date, **Then** the dependency is flagged as late on the
   timeline and in both items' details, and neither date changes automatically.
8. **Given** an epic or phase without dates, **When** a Member chooses "Schedule", **Then** it gets a
   default four-week range starting today, which they can then adjust.
9. **Given** portfolio "Digital Channels", **When** its manager opens the portfolio timeline, **Then**
   each project appears as a bar from its earliest to its latest scheduled date, expandable to its
   epics or phases and milestones.
10. **Given** a Scrum project, **When** its timeline is shown, **Then** a lane shows its completed,
    active and planned sprints.

---

### User Story 5 - Board & Dashboards: run daily work and follow progress (Priority: P5)

Teams run their daily work on a board with a column per status, moving cards as work progresses.
Project leads post short health updates, everyone follows progress on a project dashboard, and
portfolio managers see the health, progress and upcoming milestones of all their projects on one
portfolio dashboard, replacing manual status spreadsheets.

**Why this priority**: Boards and dashboards turn what stories 1–4 capture into daily visibility for
teams and management. Their value depends on real work items, sprints and dates already existing.

**Independent Test**: Move cards on the Scrum sprint board and on the Structured project's board; post
an "At risk" health update on the Structured project; confirm the project dashboard and the portfolio
dashboard show the right counts, health and next milestone.

**Acceptance Scenarios**:

1. **Given** each template, **When** a member opens the project board, **Then** it shows one column per
   workflow status: the Scrum board holds the active sprint's items, the Kanban board holds open items
   plus those done in the last 14 days, and the Structured board holds open tasks and sub-tasks plus
   those completed in the last 14 days; epics, phases and milestones are not shown as cards.
2. **Given** a card in one column, **When** a Member drags it to another column or uses its "Move to"
   action from the keyboard, **Then** the item's status changes and the change is recorded in its
   history.
3. **Given** several cards in a column, **When** a Member drags one above another, **Then** the new
   order is kept and every member sees the same order.
4. **Given** the board is open, **When** a member turns on "Only my items" or filters by assignee, type,
   label, epic or phase, or words, **Then** only matching cards remain and the column counts update.
5. **Given** another user changed an item after the board was loaded, **When** a Member drags that
   card, **Then** they are told the card changed, and the board shows its current state instead of
   overwriting it.
6. **Given** a project, **When** a member opens its dashboard, **Then** it shows how many items were
   completed, updated and created in the last 7 days, how many are due in the next 7 days and how many
   are overdue, breakdowns by status category, priority and type, progress per epic or phase, recent
   activity, and the latest health update; selecting a count opens the list of the items it counts.
7. **Given** a Project Admin, **When** they post a health update marked "At risk" with a short note,
   **Then** it appears on the project dashboard and the portfolio dashboard with its author and date,
   and a health update older than 14 days is marked as out of date.
8. **Given** portfolio "Digital Channels", **When** its manager opens the portfolio dashboard, **Then**
   each project shows its template, lead, latest health, progress, open and overdue counts, next
   milestone (structured) or active sprint end date (Scrum), and overall date range, with portfolio
   totals, and the list can be filtered by health.
9. **Given** a user who can see only some of a portfolio's projects, **When** they open the portfolio
   dashboard or timeline, **Then** only the projects they may see are shown and counted.

---

### User Story 6 - Collaboration: discuss, notify and attach (Priority: P6)

Team members discuss work where it happens: they comment on work items, attach screenshots and
documents, mention colleagues to pull them in, and follow the items they care about. The app tells
them when they are assigned or mentioned, or when something happens on an item they watch, and
assignments and mentions also reach them by email.

**Why this priority**: Keeping decisions next to the work, instead of scattered across chat and email,
is a major reason teams adopt a tracker. The core modules are already useful without it.

**Independent Test**: Member A comments on a work item, mentions member B, and attaches a screenshot.
B sees an unread notification that opens the item and also receives an email about the mention. The
screenshot can be previewed and downloaded once it has passed the malware check. A edits the comment,
which is then marked as edited, and the item's history lists the comment and attachment activity.

**Acceptance Scenarios**:

1. **Given** a member is viewing `MA-1`, **When** they post a comment with formatted text (bold, a
   list, a link), **Then** the comment appears with its author, time and formatting, and the author now
   watches `MA-1`.
2. **Given** a member writes a comment that mentions a project member named Sara, **When** it is
   posted, **Then** Sara receives a notification that opens `MA-1`, and her unread count increases by
   one.
3. **Given** Sara watches `MA-1`, **When** another member changes its status, **Then** Sara is notified,
   and the member who made the change is not.
4. **Given** a comment's author edits it, **When** others view the item, **Then** the comment shows as
   edited and the edit is recorded in the history.
5. **Given** a comment's author deletes it, **When** others view the item, **Then** a "comment deleted"
   placeholder is shown and the deletion remains in the history.
6. **Given** a member types a mention, **When** suggestions appear, **Then** only people who can access
   the project are suggested, and no one else can be notified through a mention.
7. **Given** Sara has notification emails switched on, **When** another member assigns her an item or
   mentions her, **Then** she also receives an email with the item key, summary, what happened and a
   link; if she has switched emails off, she gets only the in-app notification.
8. **Given** a member is viewing `MA-1`, **When** they attach a 2 MB screenshot, **Then** it is listed
   with its file name, size, uploader and time, and once it passes the malware check, anyone who can see
   `MA-1` can preview and download it.
9. **Given** a member tries to attach a 15 MB file or a program file (such as `.exe`), **When** they
   upload it, **Then** the upload is refused with a message stating the size limit or the allowed file
   types.
10. **Given** an uploaded file is found to contain malware, **When** the check completes, **Then** the
    file can never be downloaded, the uploader is told, and the detection is recorded in the audit log.

---

### User Story 7 - Search: find work quickly (Priority: P7)

Anyone can jump straight to a work item by typing its key, search across every project they can
access using words and filters (including by portfolio), save useful searches to rerun later, and
start the day on a "My Work" page that shows what is assigned to them.

**Why this priority**: Once there are thousands of items across many projects, finding things fast
matters. Each project's filterable list (story 2) covers the basic need until then.

**Independent Test**: With items in three projects, one of which the user cannot access, search for
"refund", narrow the results by status, assignee and portfolio, save the search and rerun it. Type a
key into the search box to open that item directly. My Work lists the user's open assigned items, and
no result ever comes from the inaccessible project.

**Acceptance Scenarios**:

1. **Given** the user can access `MA-12`, **When** they type "MA-12" into the search box and confirm,
   **Then** `MA-12` opens directly.
2. **Given** items mentioning "refund" in their summary, description or comments across several
   projects, **When** the user searches for "refund", **Then** matching items from every project they
   can access are listed, most recently updated first.
3. **Given** search results, **When** the user adds the filters portfolio = Digital Channels, status
   category = To Do and assignee = unassigned, **Then** the results narrow accordingly, and each active
   filter is visible and can be removed on its own.
4. **Given** a filtered search, **When** the user saves it as "Unassigned channel work", **Then** it
   appears in their saved filters, and running it later shows current results.
5. **Given** a project the user cannot access contains items mentioning "refund", **When** they search
   for "refund", **Then** none of that project's items appear.
6. **Given** the user signs in, **When** the My Work page opens, **Then** it lists their open assigned
   items grouped by project, and the items they viewed most recently.

---

### User Story 8 - User administration: manage accounts and security (Priority: P8)

Administrators manage the organization's accounts: they deactivate people who leave, reset forgotten
passwords, and grant or remove administrator rights. They review an audit log of security events,
which has been recorded since Day 1, and adjust organization settings such as the idle timeout and the
default time zone.

**Why this priority**: The full account lifecycle is needed before organization-wide rollout. Sign-in,
the minimal "add user" form and security logging already exist from story 1, so the administration
screens can come last, as agreed.

**Independent Test**: An administrator deactivates a user, who then cannot sign in but still appears
on their past work; resets another user's password; makes a third user an administrator; changes the
idle timeout; and finds each of these events, plus earlier failed sign-ins, in the audit log.

**Acceptance Scenarios**:

1. **Given** an active user, **When** an administrator deactivates them, **Then** the user can no
   longer sign in, is no longer offered as an assignee, and still appears (marked as deactivated) on
   earlier work items and comments.
2. **Given** a user has forgotten their password, **When** an administrator resets it, **Then** the user
   is given a new temporary password and must replace it at their next sign-in.
3. **Given** only one active administrator remains, **When** someone tries to deactivate that account
   or remove its administrator role, **Then** the action is refused.
4. **Given** failed sign-ins and a lockout happened last week, **When** an administrator filters the
   audit log by that user and date range, **Then** the failures and the lockout are listed with who,
   what and when.
5. **Given** an administrator changes the idle timeout to 15 minutes, **When** users are next inactive,
   **Then** the warning and sign-out follow the new timeout.
6. **Given** a user who is not an administrator, **When** they try to open any administration screen,
   **Then** access is refused.

---

### Edge Cases

- **Deactivated or removed assignee**: the item keeps showing that person, marked as deactivated or no
  longer a member; they cannot be chosen as a new assignee, and the item can be reassigned.
- **Removed project member**: loses access immediately, including on screens they already have open
  (their next action is refused).
- **Project moved to another portfolio**: the old portfolio's managers lose their access to it at once,
  the new portfolio's managers gain it, and both portfolios' dashboards and timelines update.
- **Simultaneous creation**: two items created at the same moment in one project never get the same
  key, and keys are never reused, even after an item is deleted.
- **Deleting items**: deleting a story, task or bug also deletes its sub-tasks, after a confirmation
  that shows how many; deleting an epic or phase removes the link from its items, which remain;
  deleting an item removes its dependencies, and the change is recorded on the linked items.
- **Changing work item type**: an item can switch between Story, Task and Bug in agile projects;
  converting to or from Epic, Phase, Milestone or Sub-task is not supported (a new item is created
  instead).
- **Container or parent completed with open items**: moving it to a Done-category status is allowed,
  but the user is warned and shown the items that are still open.
- **Changing template**: a project can switch between Scrum and Kanban while no sprint is active;
  switching to or from Structured is not supported because workflows and item types differ.
- **Dependency loops**: a dependency that would create a loop (A depends on B, which depends on A,
  directly or through other items) is refused with an explanation.
- **Undated dependencies**: a dependency between items that are not both scheduled is kept but not
  drawn or flagged until both have dates.
- **Milestones**: a milestone has exactly one date; on the timeline it can be moved but not resized,
  and it cannot have sub-tasks.
- **Sprint membership**: sub-tasks always belong to the same sprint as their parent, and an item counts
  as completed in a sprint according to its own status, not its sub-tasks'.
- **Mixed portfolios**: portfolios that mix agile and structured projects roll up by status category,
  so "On Hold" counts as In Progress and "Completed" counts as Done.
- **Out-of-date health**: if a project's latest health update is older than 14 days, dashboards show it
  as out of date rather than hiding it.
- **Empty portfolio**: a portfolio without projects shows an empty dashboard and timeline with a
  "Create project" action for those allowed to create projects.
- **Archived project**: becomes read-only for everyone, is hidden from lists, dashboards, timelines and
  search by default, and its items stay reachable by direct link for its members.
- **Deleted item**: direct links show "not found" to regular users; administrators can find and restore
  it.
- **Session about to expire**: the user is warned at least 2 minutes before an idle session ends and can
  choose to stay signed in; after it ends they must sign in again.
- **Invalid input**: a missing or overlong summary, a duplicate project key or portfolio name, or an end
  date before a start date is rejected with a clear message, and everything else the user typed is
  kept.
- **Labels that differ only by letter case**: "Backend" and "backend" are treated as the same label.
- **Malware check unavailable or slow**: newly uploaded files stay marked "being checked" and cannot be
  downloaded until the check completes; everything else keeps working.
- **Interrupted upload**: no partial file is attached, and the user can retry.
- **Mail server unavailable**: the action that triggered an email still succeeds immediately, in-app
  notifications are unaffected, and the email is sent once the mail server is reachable again.
- **Deactivated users**: receive no emails and cannot download attachments, even through old links.
- **Sensitive file uploaded by mistake**: an administrator can delete the file permanently, and the
  deletion itself is recorded in the audit log.
- **Empty states**: no projects, no items, no search results, no notifications and no scheduled items
  each show a helpful message and the next action to take (for example "Create project" or "Clear
  filters").

## Requirements *(mandatory)*

### Functional Requirements

**Sign-in and security (from Day 1, all modules)**

- **FR-001**: System MUST require every user to sign in with an account managed within the application
  before any content is shown; there is no public self-registration.
- **FR-002**: System MUST let the first administrator account be created during first-time setup, and
  that setup path MUST stop being available once an administrator exists.
- **FR-003**: Administrators MUST be able to add user accounts (display name, username, email address)
  that start with a temporary password; users MUST replace a temporary password at their next sign-in.
  This minimal form is the only account administration available before story 8.
- **FR-004**: System MUST require passwords of at least 12 characters that do not contain the username,
  and MUST lock an account for 15 minutes after 5 consecutive failed sign-in attempts.
- **FR-005**: System MUST end sessions after 30 minutes of inactivity (or the timeout set under
  FR-077), warning the user at least 2 minutes beforehand with an option to stay signed in.
- **FR-006**: Users MUST be able to change their own password, display name and time zone.
- **FR-007**: System MUST support two organization roles: Administrator (manages accounts, portfolios
  and projects, and can access every project with full rights) and User.
- **FR-008**: System MUST enforce every permission on every request, whatever the screen shows; a user
  without access to a portfolio, project or work item MUST get a "not found" response that does not
  reveal whether it exists.
- **FR-009**: System MUST record security events (successful and failed sign-ins, lockouts, password
  changes and resets, account creation, deactivation and reactivation, role and membership changes,
  portfolio manager changes, malware detections, and permanent attachment deletions) in an audit log
  from Day 1; the screen for reviewing it arrives with story 8.

**Portfolios and projects (story 1)**

- **FR-010**: Administrators MUST be able to create portfolios with a unique name and an optional
  description, rename them, assign or remove one or more portfolio managers, and delete a portfolio
  that has no projects.
- **FR-011**: Each project MUST belong to at most one portfolio. Administrators can create projects
  in any portfolio or in none; portfolio managers can create projects in their own portfolios; only
  administrators can move a project between portfolios or take it out of one.
- **FR-012**: A project MUST have a unique name, a unique key (2–10 uppercase letters or digits,
  starting with a letter, suggested from the name and editable until the project is created), an
  optional description, a lead, and a template: Scrum, Kanban or Structured. The key MUST NOT change
  after creation.
- **FR-013**: The template MUST determine the project's workflow, its work item types and its views:
  Scrum and Kanban use the agile workflow and types; Structured uses the structured workflow and
  types; the backlog and sprints exist only in Scrum projects.
- **FR-014**: Each project member MUST hold exactly one project role: Project Admin (manages the
  project's details, members and roles, and can delete items), Member (creates and works on items), or
  Viewer (read-only). The project lead is a Project Admin.
- **FR-015**: Project Admins MUST be able to edit the project's name, description and lead, add and
  remove members, change roles, and switch the template between Scrum and Kanban while no sprint is
  active.
- **FR-016**: Portfolio managers MUST be able to view every project in their portfolio, including its
  work items, timeline, board and dashboard, without changing anything unless they also hold a
  contributor role in that project.
- **FR-017**: Users MUST see a list of the projects they can access, filterable by portfolio;
  administrators and portfolio managers MUST also see a portfolio list, and each portfolio MUST have a
  page listing its projects.
- **FR-018**: Administrators MUST be able to archive and restore projects; archived projects are
  read-only and are hidden from lists, dashboards, timelines and search unless the user chooses to
  include them.

**Work items (story 2)**

- **FR-019**: System MUST provide work item types by template. Scrum and Kanban: Epic (container),
  Story, Task and Bug (standard items), and Sub-task. Structured: Phase (container), Task and Milestone
  (standard items), and Sub-task.
- **FR-020**: Containers MUST NOT have a parent; standard items MAY belong to one container in the same
  project; every sub-task MUST belong to exactly one standard item other than a milestone in the same
  project; milestones MUST NOT have sub-tasks.
- **FR-021**: System MUST give each work item a unique key made of the project key and the next number
  in that project (for example `MA-42`); keys MUST never be reused.
- **FR-022**: Each work item MUST have a required summary of up to 255 characters, an optional formatted
  description (headings, bold, italics, lists, links, code) of up to 32,000 characters, a status, a
  priority (Highest, High, Medium, Low, Lowest; default Medium), an optional assignee who is an active
  contributor in the project (with a one-click "Assign to me"), a reporter (its creator), zero or more
  labels, optional start and due dates, an optional estimate (0–999, at most one decimal place), and
  created, updated and resolved times. A milestone has a single date (its due date) and no start date
  or estimate.
- **FR-023**: System MUST provide two workflows. Agile: To Do, In Progress, In Review, Done. Structured:
  Not Started, In Progress, On Hold, Completed. Every status MUST belong to a status category (To Do,
  In Progress or Done); new items start in the first status, and contributors can move an item from any
  status to any other status of its workflow.
- **FR-024**: Moving a work item into a Done-category status MUST set its resolved time; moving it out
  of that category MUST clear it.
- **FR-025**: Opening a work item MUST show a detail panel where contributors can edit every field in
  place, with each saved change confirmed visibly; the panel shows the item's parent, its sub-tasks or
  child items, and (from story 4) its dependencies.
- **FR-026**: Each work item MUST show a complete, time-ordered history of changes (who, when, which
  field, old value, new value); no user can edit or remove history entries.
- **FR-027**: System MUST detect when someone else changed a work item after the user loaded it; the
  user's save MUST NOT silently overwrite that change, and the user MUST see the latest values while
  keeping their own unsaved input.
- **FR-028**: Project Admins and administrators MUST be able to delete a work item after confirming;
  deleted items disappear from every view but are retained, and administrators MUST be able to list and
  restore them.
- **FR-029**: Each project MUST have a work item list, shown in pages of 50, that can be sorted by key,
  summary, type, status, priority, assignee, start date, due date and last update, and filtered by
  type, status, status category, priority, assignee (including "me" and "unassigned"), label, epic or
  phase, created, updated, resolved and due date ranges, "overdue", and words in the summary or
  description.
- **FR-030**: Labels MUST be free-form, shared across the organization, matched regardless of letter
  case, and suggested from existing labels while typing.

**Backlog and sprints (story 3, Scrum projects)**

- **FR-031**: System MUST provide a backlog that lists, in priority order, the open items that are not
  in any sprint (sub-tasks travel with their parent and epics are excluded), together with the planned
  and active sprints and their items.
- **FR-032**: Contributors MUST be able to reorder the backlog by drag-and-drop or with "Move to top",
  "Move to bottom", "Move up" and "Move down" actions; the backlog and the board share one priority
  order.
- **FR-033**: Contributors MUST be able to create sprints (with a name defaulting to the project key
  plus a sequence number, for example "MA Sprint 3", and an optional goal) and move items between the
  backlog and any planned or active sprint.
- **FR-034**: Contributors MUST be able to start a planned sprint with a start date and an end date
  (defaulting to two weeks after the start); at most one sprint per project can be active.
- **FR-035**: Each planned and active sprint MUST show its item count and total story points; the active
  sprint MUST also show completed versus remaining story points.
- **FR-036**: Contributors MUST be able to complete the active sprint, choosing whether its unfinished
  items move to the backlog or to a planned sprint; completed sprints are read-only.
- **FR-037**: Each completed sprint MUST have a report showing the items and story points committed at
  the start, added during the sprint, removed during the sprint, completed, and not completed.
- **FR-038**: Sub-tasks MUST always be in the same sprint as their parent.

**Timeline (story 4)**

- **FR-039**: Each project MUST have a timeline that shows its containers (epics or phases) as bars from
  start date to due date and its milestones as markers on their date, on a scale that can be switched
  between weeks, months and quarters and that covers every scheduled item, with a marker for today and
  a way to jump back to today.
- **FR-040**: Expanding a container on the timeline MUST show its items as bars; in Scrum projects,
  items without their own dates MUST show their sprint's dates in a visibly different style; items
  with no dates at all are listed without a bar.
- **FR-041**: Contributors MUST be able to move a bar (shifting its start and due dates by the same
  number of whole days) or drag either end (changing only that date), and to move a milestone to
  another date; a due date can never be earlier than its start date; every drag action MUST have a
  keyboard equivalent.
- **FR-042**: Each container on the timeline MUST show its progress: the share of its standard items,
  excluding milestones, that are in a Done-category status (0% for a container without items, or 100%
  once the container itself is done). This definition of progress is used everywhere in U-PMS.
- **FR-043**: Contributors MUST be able to create a container directly on the timeline; containers
  without dates MUST offer a "Schedule" action that sets a four-week range starting today.
- **FR-044**: The timeline of a Scrum project MUST show a lane with its completed, active and planned
  sprints.
- **FR-045**: Contributors MUST be able to add and remove finish-to-start dependencies ("B cannot start
  until A is finished") between work items in the same project, from the timeline or the detail panel;
  the timeline MUST draw each dependency between the two items.
- **FR-046**: A dependency MUST be flagged as late, on the timeline and in both items' details, when the
  dependent item is scheduled to start (or, for a milestone, falls) before the other item's due date;
  dates MUST never change automatically, and dependencies that would create a loop MUST be refused.
- **FR-047**: Each portfolio MUST have a timeline that shows every project the viewer may see as a bar
  from the earliest start date to the latest due date of its scheduled items, expandable to its
  containers and milestones; the portfolio timeline is read-only.
- **FR-048**: The timeline MUST let users find containers by words in their summary and hide completed
  containers.
- **FR-049**: Every date change made on a timeline MUST be recorded in the item's history and follow the
  same permission and conflict rules as any other edit.

**Board (story 5)**

- **FR-050**: Each project MUST have a board with one column per status of its workflow, showing each
  item as a card with its key, summary, type, priority, assignee and estimate, and a card count per
  column.
- **FR-051**: The Scrum board MUST hold only the active sprint's items; the Kanban and Structured boards
  MUST hold all open items plus those that reached a Done-category status in the last 14 days;
  containers and milestones MUST NOT appear as cards.
- **FR-052**: Contributors MUST be able to change an item's status by moving its card to another column,
  and to change its position within a column, by drag-and-drop or by an equivalent keyboard and menu
  action.
- **FR-053**: The board MUST offer quick filters for "Only my items", assignee, type, label, epic or
  phase, and words in the summary.
- **FR-054**: Opening a card MUST show the item's detail panel without leaving the board.

**Dashboards (story 5)**

- **FR-055**: Each project MUST have a dashboard showing the number of items completed, updated and
  created in the last 7 days, due in the next 7 days, and overdue (open with a due date in the past);
  breakdowns by status category, priority and type; progress per container; the 10 most recent
  changes; and the latest health update.
- **FR-056**: Selecting a count on a project dashboard MUST open the project's list filtered to exactly
  the items that count includes.
- **FR-057**: Project Admins MUST be able to post a health update for their project: a health value (On
  track, At risk or Off track) and a note of up to 1,000 characters; earlier updates MUST be kept as a
  readable history, and a latest update older than 14 days MUST be marked as out of date.
- **FR-058**: Each portfolio MUST have a dashboard that lists every project the viewer may see with its
  template, lead, latest health and its date, progress (as defined in FR-042, across the whole
  project), open and overdue counts, next upcoming milestone (structured
  projects) or active sprint end date (Scrum projects), and overall date range; it MUST show portfolio
  totals, allow filtering by health, and link to each project's dashboard.
- **FR-059**: Dashboards MUST show every value as text as well as graphically, and MUST NOT rely on
  color alone (health values carry a label and a symbol).

**Collaboration (story 6)**

- **FR-060**: Contributors MUST be able to comment on work items with formatted text of up to 32,000
  characters; authors MUST be able to edit their own comments (shown as edited) and delete them (a
  "comment deleted" placeholder remains); comment edits and deletions appear in the item's history.
- **FR-061**: Users MUST be able to mention people who can access the project in descriptions and
  comments, and mentioned people MUST be notified.
- **FR-062**: The reporter, the assignee and anyone who comments MUST automatically watch an item;
  anyone who can see an item MUST be able to start or stop watching it.
- **FR-063**: System MUST notify users in the app when someone else assigns them an item, mentions them,
  or comments on or changes the status of an item they watch; users MUST see an unread count, open the
  related item from a notification, and mark notifications as read. No one is notified about their own
  actions.
- **FR-064**: System MUST also send an email when someone else assigns a user an item or mentions them;
  the email MUST contain the item key, summary, what happened and a link to the item, but not the
  description or comment text, and each user MUST be able to switch these emails off.
- **FR-065**: A failure to send email MUST NOT block or delay the action that triggered it; undelivered
  emails MUST be retried for at least 24 hours.
- **FR-066**: Contributors MUST be able to attach files to a work item: images (PNG, JPEG, GIF, WebP),
  PDF, Word, Excel and PowerPoint documents, and plain-text, log and CSV files, up to 10 MB each; any
  other file type, or a larger file, MUST be refused with a message explaining why.
- **FR-067**: Every uploaded file MUST be checked for malware before anyone can download it; until the
  check passes the file is shown as "being checked", and a file that fails is permanently blocked from
  download and its uploader is told.
- **FR-068**: Attachments MUST be listed on the item with file name, size, uploader and upload time;
  images MUST show a preview; anyone who can see the item MUST be able to download files that passed
  the check.
- **FR-069**: The uploader, Project Admins and administrators MUST be able to remove an attachment
  (recorded in the item's history and retained for audit); permanent deletion of an attachment, for
  example one uploaded with sensitive data by mistake, MUST be limited to administrators.

**Search and personal views (story 7)**

- **FR-070**: Users MUST be able to open a work item directly by entering its key in a search box
  available on every screen.
- **FR-071**: Users MUST be able to search all projects they can access by words in the summary,
  description or comments, combined with filters for portfolio, project, template, type, status,
  status category, priority, assignee (including "me" and "unassigned"), reporter, label, sprint, epic
  or phase, and created, updated and due date ranges; results MUST be sortable, shown in pages of 50,
  and ordered by most recent update by default.
- **FR-072**: Search results MUST include only items from projects the user can access, and MUST exclude
  archived projects unless the user chooses to include them.
- **FR-073**: Users MUST be able to save a search as a named private filter, and later run, rename or
  delete it.
- **FR-074**: System MUST provide a "My Work" page, shown after sign-in, that lists the user's open
  assigned items grouped by project and the 10 items they viewed most recently.

**User administration (story 8)**

- **FR-075**: Administrators MUST be able to list and search user accounts, deactivate and reactivate
  them, reset a user's password to a new temporary one, and grant or remove the Administrator role; the
  last active administrator cannot be deactivated or lose that role.
- **FR-076**: Administrators MUST be able to review the audit log, filtered by date range, user and
  event type.
- **FR-077**: Administrators MUST be able to change organization settings: the idle timeout (5–480
  minutes) and the default time zone.

**Across the product**

- **FR-078**: Every screen MUST be fully usable with a keyboard alone and MUST meet WCAG 2.2 Level AA.
- **FR-079**: Times MUST be shown in the viewer's chosen time zone (defaulting to the organization's
  time zone); start dates, due dates and milestone dates are calendar dates without a time.
- **FR-080**: User-written text (descriptions, comments, notes, names) MUST be displayed safely, so that
  it can never run code or alter the page for other users.

### Key Entities *(include if feature involves data)*

- **User**: a person with an account. Display name, username, email address, time zone, organization
  role (Administrator or User), status (active or deactivated), and whether they receive notification
  emails.
- **Portfolio**: a group of related projects, for example a business area or program of work. Name,
  description, portfolio managers, and its projects. Portfolios are one level deep.
- **Project**: a body of work run by one team. Name, unique permanent key, description, lead,
  template, optional portfolio, archived state, and members.
- **Project Membership**: links a user to a project with one project role (Project Admin, Member or
  Viewer).
- **Template**: Scrum, Kanban or Structured. Decides the project's workflow, its available work item
  types, and whether it has a backlog and sprints.
- **Workflow and Status**: an ordered list of statuses; each status belongs to a status category (To
  Do, In Progress, Done). The agile and structured workflows are built in.
- **Work Item Type**: a kind of work item with its level in the hierarchy (container, standard item,
  or sub-task) and the templates that offer it: Epic, Phase, Story, Task, Bug, Milestone, Sub-task.
  A milestone carries a single date.
- **Work Item**: a unit of work in a project. Key, type, summary, description, status, priority,
  assignee, reporter, labels, start and due dates, estimate, optional parent, position in the
  project's priority order, sprint, created, updated and resolved times, and deleted state. An item is
  *open* when its status is not in the Done category.
- **Dependency**: a finish-to-start link from one work item to another in the same project, with who
  created it and when; whether it is late is worked out from the two items' dates.
- **Sprint**: a time-box in a Scrum project. Name, goal, state (planned, active or completed), start and
  end dates, completion time, and a record of which items it held at the start, which were added or
  removed, and which were completed.
- **Health Update**: a project's health (On track, At risk or Off track) with a note, its author and
  time; the latest one is the project's current health.
- **Label**: an organization-wide tag attached to work items.
- **Comment**: formatted text on a work item, with its author, times, and edited or deleted state.
- **Attachment**: a file on a work item, with its file name, type, size, uploader, upload time,
  malware-check status (being checked, passed or blocked), and removed state.
- **Change Record**: one entry in a work item's history: the item, who, when, which field, the old value
  and the new value.
- **Watch**: a user following a work item.
- **Notification**: a message to one user about an event on a work item (assignment, mention, comment
  or status change), with a read or unread state and, for assignments and mentions, whether it was also
  sent by email.
- **Saved Filter**: a named, private set of search criteria owned by one user.
- **Audit Event**: a security-relevant event: its type, who acted, who or what it affected, when, and
  details.
- **Organization Settings**: the default time zone and the idle timeout.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of first-time users can sign in and create a complete work item (summary,
  type, priority, assignee) in under 2 minutes without training.
- **SC-002**: With 500,000 work items and 300 people using the system at the same time, 95% of work item
  views, board loads, dashboard loads, status changes and filtered searches complete within 1 second.
- **SC-003**: A project timeline with 100 epics or phases and 2,000 child items appears within 2
  seconds, and a dragged bar shows its saved new dates within 1 second.
- **SC-004**: A team can create a sprint, fill it with 20 items from the backlog, and start it in under
  10 minutes.
- **SC-005**: A project manager can lay out a structured plan of 4 phases, 30 tasks, 4 milestones and 10
  dependencies on the timeline in under 45 minutes.
- **SC-006**: A portfolio manager can answer "which projects are at risk, and which milestones fall this
  month?" from the portfolio dashboard in under 1 minute, without any spreadsheet.
- **SC-007**: 100% of work item changes, including dates changed on the timeline, appear in the item's
  history with who made them, when, and the old and new values.
- **SC-008**: Across acceptance and security testing, there are zero cases of a user seeing or changing
  anything they are not allowed to, including portfolio managers changing projects in which they hold
  no contributor role.
- **SC-009**: Zero silent overwrites: in concurrent-editing tests, every conflicting change is shown to
  the user who saved second.
- **SC-010**: Every count on project and portfolio dashboards matches the number of items in the
  corresponding filtered list in 100% of checks.
- **SC-011**: In 90% of attempts, users find a known work item (by its key or by words from its summary)
  in under 10 seconds.
- **SC-012**: Automated accessibility checks report zero WCAG 2.2 AA violations on the primary screens,
  and every acceptance scenario above can be completed using only a keyboard.
- **SC-013**: No uploaded file can be downloaded before it has passed the malware check, and the
  industry-standard harmless antivirus test file is always blocked.
- **SC-014**: The system is available at least 99.5% of each calendar month.
- **SC-015**: After any failure, no more than 1 hour of saved work is lost and service is restored
  within 4 hours, as shown by a restore drill before go-live and at least twice a year after that.
- **SC-016**: Within a 4-week pilot, at least one agile IT team and one PMO-managed structured project
  run all of their planning and tracking in U-PMS, and at least 80% of pilot users rate it "easy" or
  "very easy" to use.

## Assumptions

- The tool serves one organization. Everyone who uses it is an employee or contractor of that
  organization, and accounts are created by administrators.
- Users sign in with accounts managed inside the application (username and password). Company single
  sign-on is planned as a later feature and is out of scope here.
- So that teams can sign in before story 8, administrators get one minimal "add user" form from story
  1 (FR-003); every other account screen arrives with story 8, which must be delivered before
  organization-wide rollout so that leavers can be deactivated.
- Security events are recorded from Day 1 even though the audit log screen arrives with story 8.
- Temporary passwords are never sent by email; administrators give them to users through an existing
  secure company channel.
- The organization provides a mail server the system can send email through, and a malware-scanning
  capability (the organization's antivirus service, or one deployed alongside the system) that can
  check uploaded files.
- Reliability follows the standard level chosen on 2026-09-26 (SC-014, SC-015); it can be met without
  duplicate servers and raised later if needed.
- Security defaults follow common industry practice: passwords of at least 12 characters, a 15-minute
  lockout after 5 failed attempts, and a 30-minute idle timeout.
- Projects are visible only to their members, their portfolio's managers (read-only) and
  administrators; there are no organization-wide public projects.
- The two built-in workflows and the fixed type sets per template are enough for the first teams. The
  hierarchy is kept flexible (types carry a hierarchy level, statuses carry a category) so that custom
  workflows, statuses, types and fields can be added later without restructuring existing data.
- Dependencies are finish-to-start and within one project. Cross-project dependencies, other link types
  (relates to, duplicates), automatic scheduling, critical path and baselines are later features.
- Dashboards have fixed layouts. Configurable dashboards and further charts (such as burndown and
  velocity) are later features.
- Estimates are story points or any consistent unit the team chooses; there is no time tracking.
- Changes made by others appear when a screen is refreshed or when the user next acts on the item; live
  updates of open screens are not required.
- Expected scale: up to 2,000 named users, 300 of them active at the same time, 50 portfolios, 300
  projects and 500,000 work items.
- The interface is in English. Current versions of Microsoft Edge, Google Chrome and Mozilla Firefox on
  desktop are the primary targets, and screens stay usable on phones down to 360 pixels wide.
- Work items, comments, history, health updates and audit events are kept indefinitely; there is no
  automatic purge.
- The clickable design prototype reviewed with the team (`design/prototype/index.html`) illustrates the
  intended experience for stories 1–5; where it differs from this spec, this spec takes precedence.
- Stories 6–8 keep their earlier relative order (Collaboration, Search, User administration) after the
  five core modules.
- Out of scope for this MVP (candidates for later features): self-service password reset; emails for
  events other than assignments and mentions; company single sign-on; custom workflows, statuses,
  fields and work item types; nested portfolios or programs; cross-project dependencies and other link
  types; automatic scheduling, critical path and baselines; phase-gate approvals; budgets, cost, effort
  and resource capacity planning; risk, assumption, issue and decision logs; configurable dashboards;
  time tracking; import and export; integrations with other tools (source control, CI, chat) and a
  public programming interface; automation rules; native mobile apps; and multiple organizations.
