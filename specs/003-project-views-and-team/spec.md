# Feature Specification: Project Views and Team (Phase 2)

**Feature Branch**: `claude/practical-lamport-l47ei8` (spec folder `specs/003-project-views-and-team`)

**Created**: 2026-09-27

**Status**: Draft

**Input**: User description: "Phase 2 of U-PMS: Project views and team. Board, List and Timeline views
of the same project; project member management with roles (Project Admin, Member, Viewer) so that only
members and administrators see a project; task assignees chosen from the project's members; start and
due dates on tasks; and a 'My tasks' page listing the signed-in person's open assigned tasks across
projects." Phase 3 (portfolio rollups, cross-project dashboards, Scrum and PMO stage-gate templates,
dependencies, milestones and other work item types) stays out of scope. This spec builds on Phase 1
(`specs/002-kanban-project-core`); Phase 1 behavior continues unless a requirement below changes it.

## Clarifications

### Session 2026-09-27

- Q: Should Phase 2 also include the account-security follow-ups from the Phase 1 security review
  (two-step sign-in for administrators, a list of signed-in sessions, expiring temporary passwords)? →
  A: No. Phase 2 covers views, project members and assignees; the security items come later.
- Q: Tasks get assignees but Phase 2 has no notifications. Should there be a "My tasks" page across all
  projects? → A: Yes: one page listing the person's open assigned tasks from every project.
- Q: When project membership arrives, who keeps access to the projects created during the pilot? → A:
  Each project's owner becomes its Project Admin, and everyone who created, changed or commented on its
  work becomes a Member. Anyone else is added by a Project Admin.

## User Scenarios & Testing *(mandatory)*

Terms used in this spec:

- **Project role**: what a member may do in one project. **Project Admin** manages the project and its
  team; **Member** works on tasks; **Viewer** can only look.
- **Contributor**: a Project Admin or Member of the project, or an organization Administrator. Only
  contributors change work; only Project Admins and Members can be assigned tasks.
- **Status type**: the type of the column a task is in: "to do", "in progress" or "done" (Phase 1).
- **Open task**: a task or sub-task whose column's type is not "done" (as in Phase 1).
- **Scheduled task**: a task with a start date, a due date, or both.

### User Story 1 - Control who sees and works in a project (Priority: P1)

A project's Project Admin builds its team: they add colleagues as Members who work on tasks or as
Viewers who follow along, change roles as people's involvement changes, and remove people who leave the
work. From then on the project, its board and its tasks are visible only to its members and to the
organization's administrators. Projects created during the Phase 1 pilot keep working for the people
who used them: each owner becomes the Project Admin, and everyone who worked on the project becomes a
Member.

**Why this priority**: Phase 1 is an open workspace, suitable only for non-confidential work. Membership
lets teams track confidential work, and it defines who can be assigned tasks in User Story 2. On its
own it already makes U-PMS usable beyond the pilot.

**Independent Test**: As the Project Admin of `WEB`, add amina as a Member and bilal as a Viewer. Amina
can create and move tasks; bilal sees the board, list and task details but cannot change anything;
carla, who is not a member, does not see `WEB` in her project list and gets "not found" from a direct
link to `WEB-1`. Remove amina: her next action on the board she already has open is refused.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they create a project, **Then** they become its first Project
   Admin and the only member of its team.
2. **Given** the Project Admin of `WEB`, **When** they add amina as a Member and bilal as a Viewer,
   **Then** both see `WEB` in their project list with their role, and the member list shows all three
   people with their roles.
3. **Given** bilal is a Viewer of `WEB`, **When** he opens its board, list, timeline or a task, **Then**
   he sees everything but is offered no way to change anything, and any attempt to change something is
   refused.
4. **Given** carla is not a member of `WEB` and not an administrator, **When** she opens her project list
   or a direct link to `WEB` or `WEB-1`, **Then** `WEB` is not listed and the link shows "not found".
5. **Given** amina is a Member with the `WEB` board open, **When** the Project Admin removes her from the
   project, **Then** her next action on that board is refused, and `WEB` disappears from her lists.
6. **Given** the Project Admin changes bilal's role from Viewer to Member, **When** bilal next acts,
   **Then** he can create and edit tasks without signing in again.
7. **Given** a project with a single active Project Admin, **When** that person tries to remove
   themselves or change their own role, **Then** it is refused with a message asking them to make
   someone else a Project Admin first.
8. **Given** an organization administrator who is not a member of `WEB`, **When** they open the project
   list, **Then** `WEB` is listed and they can view, change and manage it with full rights.
9. **Given** a project created during the pilot, owned by owen, in which cara created tasks and dan only
   commented, **When** Phase 2 is installed, **Then** owen is its Project Admin, cara and dan are
   Members, and a user who never worked in it no longer sees it.

---

### User Story 2 - Assign and schedule tasks, and find my work (Priority: P2)

Team members say who is doing what and by when. In a task's details drawer they choose an assignee
from the project's team (or "Assign to me") and set a start date and a due date. Cards on the board
show who has each task and when it is due, with overdue tasks clearly marked, and the board can be
filtered to "Only my tasks" or to one person's tasks. Each person's "My tasks" page gathers the open
tasks assigned to them across all their projects, soonest due first.

**Why this priority**: Knowing who owns a task and when it is due is the next thing teams ask for after
a shared board. The dates also feed the List and Timeline views (stories 3 and 4). With no
notifications in Phase 2, "My tasks" is how people find their work.

**Independent Test**: In `WEB`, assign `WEB-1` to amina with a due date of yesterday and `WEB-2` to
bilal (a Member) due next week; amina assigns `WEB-3` to herself. The cards show the assignees and due
dates, `WEB-1` marked overdue. "Only my tasks" shows amina `WEB-1` and `WEB-3`. Her "My tasks" page
lists them, `WEB-1` first, together with a task assigned to her in another project.

**Acceptance Scenarios**:

1. **Given** the drawer of `WEB-1`, **When** a contributor chooses amina as the assignee, **Then** the
   change is confirmed, the card shows amina, and the history records the old and new assignee.
2. **Given** a Member viewing an unassigned task, **When** they choose "Assign to me", **Then** they
   become its assignee in one step.
3. **Given** the assignee list in the drawer, **When** it opens, **Then** it offers only the project's
   active Project Admins and Members, plus "Unassigned"; Viewers, people outside the project and
   deactivated accounts are not offered.
4. **Given** a task, **When** a contributor sets a start date of 1 October and a due date of 10 October,
   **Then** both are saved and shown on the card and in the history; **and When** they try to set a due
   date earlier than the start date, **Then** the change is refused with a clear message and what they
   entered is kept.
5. **Given** an open task whose due date was yesterday, **When** anyone views it on the board, in the
   list or on My tasks, **Then** it is marked overdue with a label, not by color alone; **and given** the
   same task is moved to Done, **Then** it is no longer marked overdue.
6. **Given** the board, **When** amina turns on "Only my tasks", **Then** only cards assigned to her
   remain, each column shows how many of its cards match, and turning the filter off brings every card
   back.
7. **Given** amina has open tasks assigned to her in `WEB` and `PAY` and a completed one in `WEB`,
   **When** she opens "My tasks", **Then** it lists the open ones grouped by project, soonest due first
   with undated tasks last, and not the completed one.
8. **Given** amina is removed from `PAY`, **When** she opens "My tasks", **Then** her `PAY` tasks are no
   longer listed, and in `PAY` those tasks still show her as assignee, marked as no longer on the
   project, until someone reassigns them.

---

### User Story 3 - Browse a project as a list (Priority: P3)

Besides the board, every project has a List view: a table of all its tasks and sub-tasks that people
sort and filter to answer questions such as "what is overdue?", "what is unassigned?" or "what did
bilal work on?". A filtered list can be bookmarked or shared as a link. Rows open the same details
drawer as the board, and new tasks can be added from the list.

**Why this priority**: A board is ideal for daily flow but poor at answering questions across many
tasks or finished work. The list builds on the assignees and dates of story 2.

**Independent Test**: In a project with 60 tasks, open the List view: the first 50 are shown with the
total count. Sort by due date, filter to "overdue" and assignee "unassigned", copy the address into a
new tab: the same filtered, sorted list appears. Open a row, change its priority in the drawer, and see
the row update.

**Acceptance Scenarios**:

1. **Given** a project, **When** a member switches from Board to List, **Then** the list shows the
   project's tasks and sub-tasks with key, title, status, priority, assignee, start date, due date and
   last update, newest first, in pages of 50 with the total count; each sub-task shows its parent's key.
2. **Given** the list, **When** a member sorts by due date, **Then** the tasks are ordered by due date
   (undated tasks last), and selecting the column again reverses the order.
3. **Given** the list, **When** a member filters by status type "in progress" and assignee "me",
   **Then** only matching tasks are listed, the count updates, each active filter is shown and can be
   removed on its own, and "Clear filters" shows everything again.
4. **Given** a filtered and sorted list, **When** its address is opened in another browser by another
   member, **Then** they see the same filters and order applied to the current data.
5. **Given** the list, **When** a member selects a row, **Then** the task's details drawer opens without
   leaving the list, and a change made there is reflected in the row.
6. **Given** a contributor on the list, **When** they type a title into "What needs to be done?" and
   press Enter, **Then** a new task is created in the project's leftmost "to do" column and appears in
   the list.
7. **Given** a filter that matches nothing, **When** the list shows, **Then** it says that no task
   matches and offers "Clear filters".

---

### User Story 4 - Plan on a timeline (Priority: P4)

Project leads see and adjust the plan over time. The Timeline view shows the project's scheduled tasks
as bars across weeks, months or quarters, with a marker for today. People drag a bar to reschedule a
task, drag its ends to change its start or due date, or do the same with the keyboard. Tasks without
dates wait in an "Unscheduled" list with a one-step "Schedule" action. Expanding a task shows its
sub-tasks.

**Why this priority**: The timeline turns the dates of story 2 into a plan that can be read and changed
at a glance, the view PMO-minded users expect. It is the most elaborate view and depends on dates
already being in place.

**Independent Test**: In a project with six tasks, four of them dated, open the Timeline in months:
four bars and a today marker appear, and two tasks are listed as unscheduled. Drag one bar two weeks
later, drag another's right end three days later, move a third one day earlier with the keyboard, and
schedule an unscheduled task. Each task's dates and history reflect exactly these changes.

**Acceptance Scenarios**:

1. **Given** a project with dated tasks, **When** a member opens the Timeline, **Then** each scheduled
   task appears as a bar from its start date to its due date with its key, title, assignee and status
   type, a marker shows today, and the scale can be switched between weeks, months and quarters.
2. **Given** a task with only a due date, **When** the timeline shows, **Then** it appears as a one-day
   bar on that date.
3. **Given** a bar, **When** a contributor drags it two weeks later, **Then** its start and due dates
   both move 14 days later, and the change is recorded in the task's history as one edit.
4. **Given** a bar, **When** a contributor drags its right end, **Then** only the due date changes, and
   the end can never be dragged before the start.
5. **Given** a bar has keyboard focus, **When** a contributor presses the arrow keys and then Enter,
   **Then** the bar moves one day per key press and the change is saved once; with the modifier keys
   only the due date or only the start date changes; Escape cancels without saving.
6. **Given** a task without dates, **When** a contributor chooses "Schedule" in the Unscheduled list,
   **Then** it gets a start date of today and a due date six days later and appears on the timeline.
7. **Given** a task with sub-tasks, **When** a member expands it, **Then** its scheduled sub-tasks appear
   as bars beneath it and its unscheduled sub-tasks are listed beneath it.
8. **Given** another user changed a task's dates after the timeline was loaded, **When** a contributor
   drags that bar, **Then** they are told the task changed, and the bar shows the task's current dates
   instead of overwriting them.
9. **Given** a Viewer, **When** they open the timeline, **Then** they can read it and open task details
   but cannot move bars or schedule tasks.

---

### Edge Cases

- **Last Project Admin**: a project always keeps at least one Project Admin with an active account;
  removing or changing the role of the last one is refused, even when two Project Admins act on each
  other at the same moment. If every Project Admin's account is later deactivated, the organization's
  administrators can still manage the project and appoint a new Project Admin.
- **Adding someone twice**: adding a person who is already a member is refused with a message that
  names their current role.
- **Deactivated members**: stay in the member list, marked as deactivated, and can be removed; they
  cannot be added to other projects or chosen as assignees until reactivated.
- **Removed member with screens open**: their next action is refused and they are told the project is
  no longer available to them; nothing they had typed is saved.
- **Assignee who can no longer work on the project** (removed, made a Viewer or deactivated): their
  tasks keep showing them, marked accordingly, until reassigned; they are never offered as a new
  assignee. Choosing someone who was removed after the drawer was opened is refused, and the drawer
  shows the current choices.
- **Role changed while working**: a Member made a Viewer mid-edit has the save refused; a Viewer made a
  Member can act at their next action without signing in again.
- **Invalid dates**: a due date before the start date, or a date outside 2000–2099, is refused with a
  clear message, and the rest of what the user entered is kept.
- **One-date tasks**: a task with only a start date or only a due date shows as a one-day bar on that
  date; it is "scheduled" but is overdue only if its due date has passed.
- **Moving a bar across many months**: the bar keeps its length; the timeline scrolls to follow it, and
  the scale can be changed without losing the change in progress.
- **Today and time zones**: dates are the same calendar dates for every viewer; what counts as "today"
  (for overdue marks and the today marker) follows each viewer's time zone.
- **Completed tasks**: are not overdue; they stay on the timeline (with a "hide completed" option) and
  in the list, but leave "My tasks".
- **Deleted tasks**: disappear from every view, including "My tasks" and the timeline; if restored, they
  return with their assignee and dates.
- **Filters and work-in-progress limits**: a filtered board column shows how many of its cards match,
  while its work-in-progress limit keeps counting all of its cards.
- **Moving a card while the board is filtered**: allowed; a card placed next to a visible card keeps
  that position for everyone, even though other cards are hidden.
- **Empty states**: an empty member list (only the Project Admin), no tasks assigned ("My tasks"), no
  matching tasks (list), and no scheduled tasks (timeline) each show a helpful message and the next
  action, such as "Add member", "Clear filters" or "Schedule".
- **Pilot projects**: projects whose owner's account is deactivated keep that person as their (inactive)
  Project Admin, and administrators appoint a new one.

## Requirements *(mandatory)*

### Functional Requirements

**Project members and roles**

- **FR-001**: Each project MUST have a team of members, each holding exactly one project role: Project
  Admin, Member or Viewer.
- **FR-002**: Only a project's members and the organization's administrators MUST be able to see the
  project anywhere it could appear (project list, board, list, timeline, task links, member list and
  "My tasks"); for everyone else the project and its tasks MUST respond as if they did not exist ("not
  found").
- **FR-003**: Viewers MUST be able to see everything in the project (board, list, timeline, task
  details, comments, history and the member list) and MUST NOT be able to change anything.
- **FR-004**: Members MUST be able to do what Phase 1 allowed any signed-in user to do in a project:
  create, edit, move and comment on tasks and sub-tasks, edit and delete their own comments, and delete
  tasks they created; and, new in Phase 2, assign and schedule tasks.
- **FR-005**: Project Admins MUST be able to do everything Members can, and also change the project's
  details and columns, delete any task in the project, and manage its members: the rights Phase 1 gave
  the project owner.
- **FR-006**: The organization's administrators MUST keep full rights in every project, whether or not
  they are members, and MUST see every project in their project list.
- **FR-007**: The person who creates a project MUST become its first Project Admin.
- **FR-008**: Project Admins and administrators MUST be able to add any active user to the project with
  a chosen role, finding them by display name, user name or email address; change a member's role; and
  remove a member after confirming.
- **FR-009**: Every member of a project MUST be able to see its member list, showing each member's
  display name, user name, role, and whether their account is deactivated.
- **FR-010**: A project MUST always keep at least one Project Admin with an active account: removing
  the last such person, or giving them another role, MUST be refused with an explanation.
- **FR-011**: Membership changes MUST apply at once: a removed member's next action MUST be refused,
  including on screens they already have open, and a person whose role changed MUST get their new
  rights at their next action without signing in again.
- **FR-012**: Membership changes (member added, removed, or role changed) MUST be recorded in the audit
  log with who made the change, the project, the person affected, and the old and new roles.
- **FR-013**: Conflicting membership changes made at the same time MUST be detected rather than silently
  overwritten, and changes made at the same moment MUST NOT leave a project without an active Project
  Admin.
- **FR-014**: When Phase 2 is installed, each existing project's owner MUST become its Project Admin,
  and every other person who created a task or sub-task in it, made a change recorded in one of its
  tasks' history, or wrote a comment on one of its tasks MUST become a Member; nobody else gains access.
- **FR-015**: The project list MUST show only the projects the user can see, with each project's name,
  key, the user's role in it (or that they have access as an administrator), and its number of open
  tasks, sorted by name. This replaces the owner column of Phase 1's project list.

**Assigning and scheduling tasks**

- **FR-016**: A task or sub-task MUST have at most one assignee, who MUST be an active Project Admin or
  Member of the project when the assignment is made.
- **FR-017**: Contributors MUST be able to choose a task's assignee from the project's active Project
  Admins and Members, set it back to unassigned, or, if they can be assigned themselves, use a one-step
  "Assign to me", all from the task's details drawer.
- **FR-018**: Every task and sub-task MUST have an optional start date and an optional due date:
  calendar dates without a time, between 1 January 2000 and 31 December 2099, with the due date never
  earlier than the start date.
- **FR-019**: Contributors MUST be able to set, change and clear a task's start and due dates in its
  details drawer, with each saved change confirmed visibly and reflected on the board, list, timeline
  and "My tasks".
- **FR-020**: An open task whose due date is earlier than today MUST be marked as overdue wherever it is
  shown, with a label or symbol and not by color alone.
- **FR-021**: Board cards MUST also show the task's assignee (their initials, with the full name
  available as text) and its due date, marked overdue when it applies; the sub-task list in a task's
  drawer MUST show each sub-task's assignee and due date.
- **FR-022**: The board MUST offer an "Only my tasks" filter and an assignee filter that includes
  "Unassigned"; while a filter is on, each column MUST show how many of its cards match, and
  work-in-progress limits MUST still count all of a column's cards.
- **FR-023**: Assignee and date changes MUST be recorded in the task's history (who, when, old value and
  new value) and MUST follow the same conflict detection as other task edits.
- **FR-024**: When an assignee is removed from the project, made a Viewer or deactivated, the tasks
  assigned to them MUST keep showing them, marked as no longer able to work on the project, until
  someone reassigns those tasks; such people MUST NOT be offered as new assignees.

**My tasks**

- **FR-025**: Every signed-in user MUST have a "My tasks" page, reachable from every screen, that lists
  the open tasks and sub-tasks assigned to them in the projects they can see, grouped by project, with
  each task's key, title, status, priority and due date (marked overdue when it applies) and, for a
  sub-task, its parent's key; within a project, tasks are ordered by due date (earliest first, undated
  last), then by priority; the page shows the total count and at most 50 tasks at a time.
- **FR-026**: Selecting a task on "My tasks" MUST open its details drawer; when the drawer closes, the
  page MUST reflect any change made there (for example, a completed task leaves the list).

**Project views and the list**

- **FR-027**: Every project MUST offer a Board, a List and a Timeline view, switchable from the project's
  header; each view MUST have its own address that can be bookmarked or shared, and opening a project
  MUST still show its board.
- **FR-028**: The List view MUST show the project's tasks and sub-tasks in a table with key, title,
  status, priority, assignee, start date, due date and last update, and each sub-task's parent key; it
  MUST be shown in pages of 50 with the total count.
- **FR-029**: The list MUST be sortable by each of its columns, ascending or descending, with the newest
  key first by default; undated tasks sort last when sorting by a date.
- **FR-030**: The list MUST be filterable by status, status type, priority, assignee (including "me"
  and "unassigned"), due date ("overdue", "due in the next 7 days" or "no due date") and words in the
  title or description; active filters MUST be visible, each removable on its own, with a "Clear
  filters" action.
- **FR-031**: The list's sort order and filters MUST be kept in its address, so that a filtered list can
  be bookmarked, shared with other members and reloaded.
- **FR-032**: Selecting a row MUST open the task's details drawer without leaving the list, and changes
  made in the drawer MUST be reflected in the list.
- **FR-033**: Contributors MUST be able to add a task from the list through a "What needs to be done?"
  box; the new task starts in the project's leftmost column of type "to do".

**Timeline**

- **FR-034**: The Timeline view MUST show the project's scheduled tasks as bars from start date to due
  date on a scale that can be switched between weeks, months and quarters and that covers every
  scheduled task, with a marker for today and a way to return to today; a task with only one date MUST
  show as a one-day bar on that date.
- **FR-035**: Each bar MUST show the task's key and title, its assignee, and its status type as a label
  or symbol (not color alone); rows MUST be ordered by their first date (the start date, or the due date
  when there is no start date), then by due date, then by key; a task with sub-tasks MUST be expandable
  to show its sub-tasks beneath it.
- **FR-036**: Tasks without dates MUST be listed beside the timeline as unscheduled, each with a
  "Schedule" action that sets its start date to today and its due date six days later.
- **FR-037**: Contributors MUST be able to move a bar, shifting its start and due dates by the same
  number of days, or drag either end to change only that date; the due date MUST never move before the
  start date.
- **FR-038**: Every timeline change MUST have a keyboard equivalent: with a bar focused, the arrow keys
  move it by one day, with modifier keys they change only its due date or only its start date, Enter
  saves the change as one edit and Escape cancels it; the dates being set MUST be announced while
  adjusting.
- **FR-039**: Timeline changes MUST be recorded in the task's history and follow the same permission and
  conflict rules as any other task edit; when a task was changed by someone else since the timeline was
  loaded, the change MUST NOT overwrite it, the user MUST be told, and the bar MUST show the task's
  current dates.
- **FR-040**: Selecting a bar or an unscheduled task MUST open the task's details drawer, and users MUST
  be able to hide completed tasks on the timeline (they are shown by default).

**Across the product**

- **FR-041**: Every new or changed screen MUST be fully usable with a keyboard alone, meet WCAG 2.2 Level
  AA, and stay usable on screens 360 pixels wide (the timeline scrolls sideways within its own area).
- **FR-042**: Start and due dates MUST be the same calendar dates for every viewer; "today", for overdue
  marks and the timeline's today marker, MUST follow the viewer's time zone.
- **FR-043**: Phase 1 requirements MUST continue to apply, except where this spec replaces them: Phase 1
  FR-011 (the creator becomes the first Project Admin, FR-007), FR-013 (the project list, FR-015), FR-014,
  FR-033 and FR-034–FR-036 (the owner's rights belong to Project Admins, FR-005), and FR-015 (the open
  workspace ends, FR-002).

### Key Entities *(include if feature involves data)*

- **Project Member**: a person's membership in one project: the person, the project, their project role
  (Project Admin, Member or Viewer), and who added them and when. A person holds at most one membership
  per project.
- **Task** (extended from Phase 1): gains an optional assignee (a project member who could work on it
  when assigned), an optional start date and an optional due date. A task is *overdue* when it is open
  and its due date is before today.
- **Change Record** (extended): also covers assignee, start date and due date changes, including those
  made on the timeline.
- **Audit Event** (extended): also covers members being added and removed and project roles being
  changed.
- **Project** (changed): its Phase 1 owner is no longer a separate role; Project Admins hold the owner's
  rights.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Project Admin can add three colleagues with different roles to a project in under 2
  minutes.
- **SC-002**: With 500,000 tasks in the system and 300 people using it at the same time, 95% of project
  list, board and List view loads (including sorted and filtered pages), timeline loads (up to 500
  scheduled tasks), "My tasks" loads, and saved assignee, date and membership changes complete within 1
  second.
- **SC-003**: Across acceptance and security testing, there are zero cases of a person who is not a
  member (and not an administrator) seeing any of a project's data, including through task links,
  "My tasks" or filters, and zero cases of a Viewer changing anything.
- **SC-004**: 100% of assignee and date changes appear in the task's history, and 100% of membership
  changes appear in the audit log, with who made them, when, and the old and new values.
- **SC-005**: Zero silent overwrites: in concurrent-editing tests of assignee and date edits, timeline
  moves and membership changes, every conflicting change is shown to the person who saved second.
- **SC-006**: After Phase 2 is installed, 100% of the people who worked on a pilot project keep access to
  it, and no one else gains access.
- **SC-007**: At least 90% of pilot users can find every open task assigned to them within 30 seconds of
  signing in, without help.
- **SC-008**: A contributor can move a task two weeks later on the timeline in under 15 seconds, with a
  mouse or with the keyboard alone.
- **SC-009**: Automated accessibility checks report zero WCAG 2.2 AA violations on the member list, the
  List view, the timeline, "My tasks" and the changed board and drawer, and every acceptance scenario can
  be completed using only a keyboard.

## Assumptions

- **Builds on Phase 1**: sign-in, accounts, projects, boards, the details drawer, comments, history and
  column customization work as specified in `specs/002-kanban-project-core`, including the second
  administrator added on 2026-09-27.
- **Members-only projects**: every project is visible only to its members and the organization's
  administrators; there is no "visible to everyone" setting. People who only need to follow a project
  are added as Viewers.
- **Only existing accounts become members**: administrators create accounts as in Phase 1; there are no
  invitations by email.
- **Administrators are not listed as members** unless added: their access to every project is implicit.
  An administrator who is not a member cannot be assigned tasks in that project.
- **One assignee per task**; multiple assignees, groups and workload views come later, if at all.
- **Calendar dates**: start and due dates are plain calendar dates; there are no working-day calendars,
  holidays or time-of-day deadlines, so moving a bar by 7 days moves it 7 calendar days.
- **No notifications** (in-app or email) in Phase 2: "My tasks", "Only my tasks" and the list filters are
  how people find their work.
- **Account-security follow-ups deferred**: two-step sign-in for administrators, a list of signed-in
  sessions and expiring temporary passwords are not part of Phase 2 (decision of 2026-09-27); they stay
  open in the Phase 1 security review.
- **Timeline content**: Phase 2 has only tasks and sub-tasks, so the timeline shows those; epics,
  phases, milestones and dependencies arrive with the Phase 3 templates.
- **Views remember nothing between visits**: opening a project shows its board; each view's address
  (including the list's filters) can be bookmarked instead.
- **Scale** stays at Phase 1's: up to 2,000 named users (300 active at once) and 500,000 tasks; a
  project's timeline shows up to 500 scheduled tasks at a time.
- **"Worked on" for the upgrade** means creating a task or sub-task, making any change recorded in a
  task's history, or writing a comment in that project, including by people whose accounts are now
  deactivated (they regain access only if an administrator reactivates them).
- Out of scope for Phase 2 (candidates for later phases): notifications and email, mentions and
  watchers, search across projects and saved searches, dashboards, portfolios, Scrum and PMO templates,
  sprints, epics, phases, milestones and dependencies, estimates and time tracking, labels, multiple
  assignees and groups, bulk editing, import and export, live updates of open screens, a "visible to
  everyone" project setting, and the account-security follow-ups listed above.
