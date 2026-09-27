# Feature Specification: Core Kanban Project (Phase 1)

**Feature Branch**: `claude/practical-lamport-l47ei8` (spec folder `specs/002-kanban-project-core`)

**Created**: 2026-09-27

**Status**: Draft

**Input**: User description: "Phase 1 Focus (Core Kanban Project): Project Creation (Name, Key,
Description). Standard Kanban Board (To Do, In Progress, Done) with inline task creation ('What needs
to be done?'). Task Details Drawer (Title, Description, Priority, Sub-tasks, Comments). Ability to
customize board columns." Context: basic sign-in from Day 1 was agreed earlier, with user
administration screens later. Phase 2 (Board/List/Timeline views, project members and task assignees)
and Phase 3 (portfolio rollups, cross-project dashboards, Scrum and PMO stage-gate templates) will get
their own specs.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Kanban project and track tasks on its board (Priority: P1)

A team lead signs in, creates a project by giving it a name, a short key and a description, and lands
on its Kanban board with three columns: To Do, In Progress and Done. Anyone on the team types a task
straight into a column ("What needs to be done?") and presses Enter; the card appears with a key such
as `WEB-1`. As work moves, people drag cards between columns and reorder them to show what matters
most; keyboard users do the same through a "Move to" action.

**Why this priority**: This is the smallest thing that is genuinely useful: a team can stop tracking
work in spreadsheets and chat as soon as it exists. Everything else in Phase 1 enriches this loop.

**Independent Test**: Sign in, create project "Website Revamp" with key `WEB`, add five tasks inline
across the columns, drag two cards to other columns, reorder a column, and move one card using only the
keyboard. After reloading, the board is exactly as it was left, and another signed-in user sees the same
board.

**Acceptance Scenarios**:

1. **Given** a person who is not signed in, **When** they open any U-PMS page, **Then** they are asked
   to sign in first and see no project information.
2. **Given** an administrator has added an account with a temporary password, **When** that person
   signs in for the first time, **Then** they must choose a new password before they can continue.
3. **Given** a signed-in user, **When** they create a project named "Website Revamp" with a
   description, **Then** a key is suggested from the name (for example `WR`), they can change it (for
   example to `WEB`) before saving, and the new project opens on a board with the columns To Do, In
   Progress and Done.
4. **Given** the key `WEB` is already in use, **When** someone tries to create another project with key
   `WEB`, **Then** creation is refused with a clear message, and the details they entered are kept.
5. **Given** the `WEB` board, **When** a user types "Design the home page" into a column's "What needs
   to be done?" box and presses Enter, **Then** card `WEB-1` appears at the bottom of that column, and
   the box is immediately ready for the next task.
6. **Given** a card in To Do, **When** a user drags it to In Progress, **Then** the task's status
   becomes In Progress, and everyone sees it there.
7. **Given** several cards in a column, **When** a user drags one card above another, **Then** the new
   order is kept, and everyone sees the same order.
8. **Given** a card has keyboard focus, **When** the user opens its "Move to" action and chooses Done,
   **Then** the result is the same as dragging the card to Done.
9. **Given** another user moved a card after the board was loaded, **When** the user drags that card,
   **Then** they are told the card changed, and the board shows its current position instead of
   overwriting it.
10. **Given** a task was moved to Done 20 days ago, **When** the board opens, **Then** that task is not
    shown in the Done column unless the user chooses to show all completed tasks.

---

### User Story 2 - Work on a task in the details drawer (Priority: P2)

Selecting a card opens a details drawer beside the board. There the team edits the title and
description, sets the priority, breaks the task into sub-tasks, and discusses it in comments. Every
change is recorded, so anyone can see who changed what, and when.

**Why this priority**: A title alone is rarely enough for real work. Details, priority, a breakdown
into sub-tasks and a discussion make a task actionable. This story builds directly on the board.

**Independent Test**: Open `WEB-1`, change its title, write a description, set priority High, add three
sub-tasks and complete one, post two comments and edit one. Close and reopen the drawer: everything is
saved, the card shows the High priority and "1/3" sub-task progress, and the history lists each change.

**Acceptance Scenarios**:

1. **Given** the board, **When** a user selects card `WEB-1`, **Then** its details drawer opens beside
   the board showing the key, title, status, priority, description, sub-tasks, comments and history,
   while the board stays visible behind it.
2. **Given** the drawer is open, **When** the user changes the title and confirms, **Then** the new
   title is saved, the save is confirmed visibly, and the card shows the new title.
3. **Given** the drawer, **When** the user writes a description over several lines and saves it,
   **Then** the line breaks are kept, and web links in it can be opened.
4. **Given** the drawer, **When** the user sets the priority to High, **Then** the card shows the High
   priority indicator.
5. **Given** the drawer, **When** the user changes the status to Done, **Then** the card moves to the
   Done column.
6. **Given** `WEB-1`, **When** the user adds three sub-tasks and marks one of them done, **Then** each
   sub-task receives its own key, the drawer lists them with their statuses, and the card shows "1/3".
7. **Given** a sub-task in the list, **When** the user opens it, **Then** its own drawer opens with a
   link back to `WEB-1`, and sub-tasks never appear as separate cards on the board.
8. **Given** the drawer, **When** the user posts a comment, **Then** it appears with their name and the
   time; they can edit it (it is then marked as edited) or delete it (a "comment deleted" placeholder
   remains), and they cannot edit or delete anyone else's comments.
9. **Given** several changes were made to `WEB-1`, **When** the user opens its history, **Then** every
   change is listed in time order with who made it, when, what changed, and the old and new values.
10. **Given** two users edit the same task's description, **When** the second saves after the first,
    **Then** the second is told the task changed, sees the latest version, and still has their own text.
11. **Given** the task's creator, the project owner or an administrator, **When** they delete the task
    after confirming, **Then** the task and its sub-tasks disappear from the board, and an administrator
    can restore them.

---

### User Story 3 - Customize the board's columns (Priority: P3)

Teams work differently, so the project owner shapes the board to match: adding columns such as "In
Review" or "Blocked", renaming and reordering them, setting work-in-progress limits, and removing
columns the team does not need, without ever losing a task.

**Why this priority**: The default three columns let a team start immediately; customization lets
each board reflect how that team really works. It changes the board's structure, so it follows the core
loop of stories 1 and 2.

**Independent Test**: As the project owner, add "In Review" (an in-progress column) between In
Progress and Done, rename "To Do" to "Backlog", set a limit of 3 on In Progress, move a column, then
delete "In Review" and send its tasks to Done. No task is lost, and a signed-in user who is not the
owner cannot change the columns.

**Acceptance Scenarios**:

1. **Given** the project owner opens the board's column settings, **When** they add a column named "In
   Review" of type "in progress" after In Progress, **Then** it appears on the board in that position
   for everyone.
2. **Given** a column, **When** the owner renames "To Do" to "Backlog", **Then** the new name is shown
   on the board and in every task's status, and the tasks stay where they are.
3. **Given** the board, **When** the owner moves a column to another position by dragging it or with the
   "Move left" and "Move right" actions, **Then** the new column order is kept for everyone.
4. **Given** the owner set a limit of 3 on In Progress, **When** a fourth card is placed in that column,
   **Then** the column is visibly marked as over its limit (showing 4 of 3), and the move is still
   allowed.
5. **Given** "In Review" holds two tasks, **When** the owner deletes that column and chooses Done as the
   destination, **Then** both tasks move to Done, each move is recorded in the task's history, and the
   column disappears.
6. **Given** the board has only one column of type "done", **When** the owner tries to delete it or
   change its type, **Then** the action is refused with an explanation; the same applies to the last
   column of type "to do".
7. **Given** a column name already used on the board (ignoring letter case), **When** the owner tries to
   add or rename another column to that name, **Then** the change is refused with a clear message.
8. **Given** a signed-in user who is neither the project owner nor an administrator, **When** they look
   at the board, **Then** the column settings are not offered, attempts made by other means are
   refused, and they can still create, edit and move tasks.

---

### Edge Cases

- **Deleting a column with tasks**: the owner must pick a destination column first; its tasks,
  including sub-tasks, move there, and moving into a "done" column marks them completed.
- **Keeping the board workable**: a board always keeps at least one "to do" column and one "done"
  column, and holds at most 10 columns.
- **Changing a column's type**: allowed only while the column is empty, so no task silently changes
  between not started, in progress and done.
- **Over the limit**: exceeding a column's work-in-progress limit shows a warning but never blocks a
  move or a new task.
- **Where new tasks land**: tasks created inline start in the column where they were typed; sub-tasks
  created from the drawer start in the leftmost "to do" column.
- **Completing a task with open sub-tasks**: allowed, but the user is warned and shown the sub-tasks
  that are still open.
- **Sub-task depth**: sub-tasks cannot have sub-tasks of their own.
- **Deleting a task with sub-tasks**: the confirmation shows how many sub-tasks will be deleted with it.
- **Simultaneous creation**: two tasks created at the same moment in one project never get the same key,
  and keys are never reused, even after a task is deleted.
- **Two owners editing columns at once**: the second change is refused if it conflicts, and the owner
  sees the board's latest column setup.
- **Empty or overlong titles**: pressing Enter in an empty "What needs to be done?" box creates nothing;
  a title over 255 characters is refused, and the typed text is kept.
- **Deleted task links**: opening a link to a deleted task shows "not found" to regular users;
  administrators can find and restore it.
- **Deactivated users**: cannot sign in; their names remain on the tasks, comments and history they
  created.
- **Owner deactivated**: administrators can still change the project's details and columns, so no
  project is left without someone who can manage it.
- **Session about to expire**: the user is warned at least 2 minutes before an idle session ends and can
  choose to stay signed in.
- **Long columns**: a column with many cards scrolls on its own, without slowing down the rest of the
  board.
- **Empty states**: no projects, an empty column, no sub-tasks and no comments each show a helpful
  message and the next action to take (for example "Create project").

## Requirements *(mandatory)*

### Functional Requirements

**Sign-in and security (from Day 1)**

- **FR-001**: System MUST require every user to sign in with an account managed within the application
  before any content is shown; there is no public self-registration.
- **FR-002**: System MUST let the first administrator account be created during first-time setup, and
  that setup path MUST stop being available once an administrator exists.
- **FR-003**: Administrators MUST be able to add user accounts (display name, username, email address)
  that start with a temporary password; users MUST replace a temporary password at their next sign-in.
- **FR-004**: Administrators MUST be able to reset a user's password to a new temporary one and to
  deactivate and reactivate accounts; deactivated users cannot sign in, and their names stay on their
  past work. This minimal account management is the only administration screen in Phase 1.
- **FR-005**: System MUST require passwords of at least 12 characters that do not contain the username,
  and MUST lock an account for 15 minutes after 5 consecutive failed sign-in attempts.
- **FR-006**: System MUST end sessions after 30 minutes of inactivity, warning the user at least 2
  minutes beforehand with an option to stay signed in.
- **FR-007**: Users MUST be able to change their own password, display name and time zone.
- **FR-008**: System MUST support two organization roles: Administrator (manages accounts and has full
  rights in every project) and User.
- **FR-009**: System MUST check every permission on the server for every request, whatever the screen
  shows; requests for tasks or projects that do not exist or were deleted MUST get a "not found"
  response.
- **FR-010**: System MUST record security events (successful and failed sign-ins, lockouts, password
  changes and resets, account creation, deactivation and reactivation) in an audit log from Day 1; a
  screen for reviewing it comes in a later phase.

**Projects**

- **FR-011**: Any signed-in user MUST be able to create a project with a unique name of 1–80
  characters, a unique key (2–10 uppercase letters or digits, starting with a letter, suggested from the
  name and editable until the project is created), and an optional description of up to 2,000
  characters. The creator becomes the project owner.
- **FR-012**: A project's key MUST NOT change after the project is created.
- **FR-013**: Users MUST see a list of all projects showing each project's name, key, owner and number
  of open tasks, sorted by name; opening a project MUST show its board.
- **FR-014**: The project owner and administrators MUST be able to edit the project's name and
  description.
- **FR-015**: In Phase 1, every signed-in, active user MUST be able to view every project and create,
  edit, move, comment on and delete their own tasks in it; changing a project's details and its columns
  MUST be limited to the project owner and administrators.

**Board**

- **FR-016**: Every new project MUST start with a board of three columns: To Do (type "to do"), In
  Progress (type "in progress") and Done (type "done").
- **FR-017**: The board MUST show the project's columns in order, each with its cards and a card count;
  a card shows the task's key, title, priority and, when the task has sub-tasks, how many are done out
  of the total (for example "1/3").
- **FR-018**: Every column MUST offer a "What needs to be done?" box: typing a title and pressing Enter
  MUST create a task at the bottom of that column and leave the box ready for the next title; an empty
  box MUST create nothing.
- **FR-019**: Users MUST be able to move a card to another column (changing the task's status) and to
  change its position within a column, by drag-and-drop or by an equivalent keyboard "Move to" action
  that offers every column and the top or bottom of a column.
- **FR-020**: The order of cards MUST be the same for everyone.
- **FR-021**: Columns of type "done" MUST show tasks completed in the last 14 days by default, with an
  option to show all completed tasks.
- **FR-022**: System MUST detect when a card was changed by someone else after the board was loaded; the
  move MUST NOT silently overwrite that change, and the board MUST show the card's current state.
- **FR-023**: Selecting a card MUST open the task's details drawer beside the board, and every task MUST
  have a link that opens its project's board with that task's drawer open.

**Tasks and the details drawer**

- **FR-024**: Each task MUST have a unique key made of the project key and the next number in that
  project (for example `WEB-42`); keys MUST never be reused.
- **FR-025**: Each task MUST have a required title of 1–255 characters, an optional plain-text
  description of up to 32,000 characters (line breaks kept, web links clickable when displayed), a
  status (one of the project's columns), a priority (Highest, High, Medium, Low or Lowest; default
  Medium), its creator, and created, updated and completed times.
- **FR-026**: The drawer MUST let users edit the title, description, priority and status in place, with
  each saved change confirmed visibly and reflected on the board.
- **FR-027**: Moving a task into a column of type "done" MUST record its completed time; moving it out
  of such a column MUST clear it.
- **FR-028**: Users MUST be able to add sub-tasks to a task from its drawer by typing a title; a
  sub-task is itself a task with its own key, status, priority, description, comments and history, is
  listed in its parent's drawer with its status and a link, is never shown as a separate card, and
  cannot have sub-tasks of its own; the list lets users change each sub-task's status, including a
  one-step "mark done" that moves it to the leftmost column of type "done".
- **FR-029**: Moving a task into a "done" column while it has open sub-tasks MUST be allowed, with a
  warning that lists the open sub-tasks.
- **FR-030**: Users MUST be able to add plain-text comments of up to 32,000 characters to a task;
  comments are listed oldest first with their author and time; authors MUST be able to edit their own
  comments (then marked as edited) and delete them (a "comment deleted" placeholder remains), and no one
  can edit or delete someone else's comments.
- **FR-031**: Each task MUST show a complete, time-ordered history of changes (who, when, what, old
  value, new value), covering creation, title, description, priority and status changes (including
  moves caused by deleting a column), sub-tasks added, comments added, edited and deleted, and deletion
  or restoration; no one can edit or remove history entries.
- **FR-032**: System MUST detect when someone else changed a task after the user loaded it; the user's
  save MUST NOT silently overwrite that change, and the user MUST see the latest values while keeping
  their own unsaved input.
- **FR-033**: The task's creator, the project owner and administrators MUST be able to delete a task
  after a confirmation that shows how many sub-tasks will be deleted with it; deleted tasks disappear
  from the board but are retained, and administrators MUST be able to list a project's deleted tasks and
  restore them.

**Customizing columns**

- **FR-034**: The project owner and administrators MUST be able to add a column with a name of 1–30
  characters, unique on that board regardless of letter case, a type ("to do", "in progress" or "done"),
  and a position; a board holds at most 10 columns.
- **FR-035**: The project owner and administrators MUST be able to rename columns and change their
  order, by dragging or with "Move left" and "Move right" actions.
- **FR-036**: The project owner and administrators MUST be able to set or remove a work-in-progress
  limit (1–99) on any column; when a column holds more cards than its limit, it MUST be marked as over
  its limit, showing the count against the limit, without blocking any move or new task.
- **FR-037**: Deleting a column that holds tasks (including sub-tasks) MUST require choosing a
  destination column, move those tasks there with each move recorded in the task's history, and then
  remove the column.
- **FR-038**: A board MUST always keep at least one column of type "to do" and one of type "done";
  deleting or retyping the last one MUST be refused with an explanation.
- **FR-039**: A column's type MUST be changeable only while the column is empty.
- **FR-040**: Tasks created inline MUST start in the column where they were typed; sub-tasks MUST start
  in the leftmost column of type "to do".
- **FR-041**: Column changes MUST apply for everyone, and conflicting column changes made at the same
  time MUST be detected rather than silently overwritten.

**Across the product**

- **FR-042**: Every screen MUST be fully usable with a keyboard alone, including moving cards and
  reordering columns, and MUST meet WCAG 2.2 Level AA.
- **FR-043**: Times MUST be shown in the viewer's time zone (defaulting to the organization's time
  zone).
- **FR-044**: User-written text (titles, descriptions, comments, names) MUST be displayed safely, so
  that it can never run code or alter the page for other users.

### Key Entities *(include if feature involves data)*

- **User**: a person with an account. Display name, username, email address, time zone, organization
  role (Administrator or User), and status (active or deactivated).
- **Project**: a body of work. Name, unique permanent key, description, owner (its creator), and its
  board columns and tasks.
- **Board Column**: one column of a project's board, which is also a status a task can have. Name, type
  ("to do", "in progress" or "done"), position, and an optional work-in-progress limit.
- **Task**: a unit of work in a project. Key, title, description, status (its column), priority,
  position within its column, optional parent task (for sub-tasks), creator, created, updated and
  completed times, and deleted state. A task is *open* while its column's type is not "done". Tasks are
  the first kind of work item; other kinds (such as epics or milestones) arrive with later templates.
- **Comment**: plain text on a task, with its author, times, and edited or deleted state.
- **Change Record**: one entry in a task's history: the task, who, when, what changed, the old value and
  the new value.
- **Audit Event**: a security-relevant event: its type, who acted, who or what it affected, when, and
  details.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of first-time users can create a project and add their first three tasks in
  under 3 minutes without help.
- **SC-002**: With 500,000 tasks in the system and 300 people using it at the same time, 95% of board
  loads (up to 500 visible cards), inline task creations, card moves and drawer openings complete within
  1 second.
- **SC-003**: A project owner can add a column, rename another, reorder them and set a work-in-progress
  limit in under 2 minutes.
- **SC-004**: 100% of task changes, including moves caused by deleting a column, appear in the task's
  history with who made them, when, and the old and new values.
- **SC-005**: Zero silent overwrites: in concurrent-editing tests of card moves, task edits and column
  changes, every conflicting change is shown to the person who saved second.
- **SC-006**: Zero tasks are lost when columns are added, deleted or reordered: every task remains on
  the board or among the project's deleted tasks.
- **SC-007**: Across acceptance and security testing, there are zero cases of a person who is not signed
  in seeing any project data, or of a user other than the project owner or an administrator changing a
  project's details or columns.
- **SC-008**: Automated accessibility checks report zero WCAG 2.2 AA violations on the project list,
  board, drawer and column settings, and every acceptance scenario can be completed using only a
  keyboard.
- **SC-009**: The system is available at least 99.5% of each calendar month.
- **SC-010**: After any failure, no more than 1 hour of saved work is lost and service is restored
  within 4 hours, as shown by a restore drill before go-live.
- **SC-011**: Within a 2-week pilot, at least one team runs all of its daily work on a U-PMS Kanban
  board, and at least 80% of its members rate the board and drawer "easy" or "very easy" to use.

## Assumptions

- **Roadmap**: this spec covers Phase 1 only. Phase 2 adds Board, List and Timeline views of the same
  project, project members and task assignees; Phase 3 adds portfolio rollups, cross-project
  dashboards, and Scrum and PMO stage-gate templates. Each phase gets its own spec. The earlier MVP spec
  (`specs/001-issue-tracker-mvp`) is kept as the product vision and requirement backlog for those
  phases.
- **Open workspace in Phase 1**: because project members arrive in Phase 2, every signed-in user can see
  and work in every project. This suits a pilot with non-confidential work; project membership must be
  in place before confidential projects are tracked. The project owner (its creator) becomes the
  project's first administrator when membership arrives.
- **Sign-in from Day 1**, as agreed on 2026-09-27. Minimal account management (add, reset password,
  deactivate, reactivate) is included so a pilot can onboard and offboard people safely; roles
  management, the audit log screen and organization settings come later.
- The tool serves one organization, and accounts are created by administrators. Users sign in with
  accounts managed inside the application; company single sign-on is a later feature.
- Temporary passwords are given to users through an existing secure company channel; Phase 1 sends no
  email.
- Descriptions and comments are plain text in Phase 1; text formatting comes in a later phase.
- Phase 1 has no assignees, due dates, labels, or work item types other than tasks and sub-tasks.
- Changes made by others appear when the board is refreshed or when the user next acts on the item; live
  updates of open screens are not required.
- Security defaults follow common industry practice: passwords of at least 12 characters, a 15-minute
  lockout after 5 failed attempts, and a 30-minute idle timeout.
- Reliability follows the standard level chosen on 2026-09-26: 99.5% monthly availability, at most 1
  hour of saved work lost, and service restored within 4 hours.
- Expected scale: up to 2,000 named users, 300 of them active at the same time, and 500,000 tasks.
- The interface is in English. Current versions of Microsoft Edge, Google Chrome and Mozilla Firefox on
  desktop are the primary targets, and screens stay usable on phones down to 360 pixels wide.
- Tasks, comments, history and audit events are kept indefinitely; there is no automatic purge.
- The clickable design prototype (`design/prototype/index.html`) illustrates the board and drawer
  experience; where it differs from this spec, this spec takes precedence for Phase 1.
- Out of scope for Phase 1 (candidates for later phases): project members and roles, task assignees,
  List and Timeline views, search and filters, labels, due dates, other work item types, sprints,
  portfolios, dashboards, templates other than Kanban, notifications and email, mentions, attachments,
  text formatting, archiving projects, board configuration history, the audit log screen, organization
  settings, company single sign-on, import and export, integrations and a public programming interface,
  automation rules, and native mobile apps.
