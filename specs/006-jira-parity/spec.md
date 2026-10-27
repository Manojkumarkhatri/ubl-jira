# Feature Specification: Jira Parity Pass (before Phase 5)

**Feature Branch**: `phase-3` (current working branch; spec folder `specs/006-jira-parity`)

**Created**: 2026-10-07

**Status**: Draft

**Input**: User description: "Jira parity pass for U-PMS before Phase 5." After trying the Phase 4 build on
2026-10-07, the owner found that what is built works but does not feel like Jira. Before Phase 5 (portfolio and
dashboards), U-PMS should look and behave like Jira for its target teams (software development, PMO and technical
teams such as Data & AI). The scope is:
- "projects" become "spaces" on every screen, as in Jira Cloud;
- a "Create" button in the header that opens a full form for any work type;
- board columns managed on the board itself, as in Jira team-managed spaces;
- a Jira-style "Create space" flow: a template gallery with previews, including a new blank template where the
  creator chooses the columns and work types;
- a Jira-like layout in U-PMS's own style: Jira Cloud's left sidebar and space tabs, Jira-style cards, swimlanes,
  and a simple space summary page.

Where Jira has an equivalent, U-PMS behaves like Jira. Everything built in Phases 1 to 4 keeps working
(`specs/002-kanban-project-core` to `specs/005-collaboration-and-search`); their behavior continues unless a
requirement below changes it. Sign-in through the company's LDAP server and email through its SMTP server are
for UAT and out of scope here.

## Clarifications

### Session 2026-10-07

- Q: Which Jira behaviors should this pass add before Phase 5? → A: All four: a global "Create" button, columns
  managed on the board, a Jira-style "Create space" gallery with a blank template, and a Jira-like layout
  (sidebar, cards, swimlanes).
- Q: Jira Cloud now calls projects "spaces". What should U-PMS call them? → A: "Spaces", on every screen.
- Q: How close to Jira should the look be? → A: Jira's layout, placement and behavior, in U-PMS's own colors,
  icons and branding; not a copy of Atlassian's design.
- Q: Should spaces get Jira's access levels (open, limited, private), or stay visible to members only? → A:
  Members only, as in Phase 2 (Jira's "Private"); creating a space has no access step. Access levels may come in a
  later phase.
- Q: Which Jira layout should U-PMS copy: today's Jira Cloud (one left sidebar on every page, a space's views as
  tabs along the top) or classic Jira (a sidebar inside each space)? → A: Today's Jira Cloud: a left sidebar on every
  page with Your work, Recent, Starred, Spaces, Filters and Accounts; inside a space, its views are tabs, and
  Members, Space settings and Deleted items are in a "…" menu. Anyone can star a space.
- Q: Should a Space admin be able to change which work types a Kanban or Scrum space uses, or only in Blank
  spaces? → A: In any Kanban, Scrum or Blank space: Epic, Story, Task and Bug can be turned on or off in the space
  settings, keeping at least one of Story, Task or Bug; Sub-task is always on; Stage-gate spaces keep their types.
- Q: When the board is grouped into lanes, should moving a card to another lane change its epic or assignee? →
  The owner answered: "Don't ask question what ever jira has just go with that." So wherever Jira has an answer,
  this pass follows Jira, and these points follow it:
  - moving a card to another epic's, stage's or person's lane changes its epic, stage or assignee, as on Jira
    team-managed boards;
  - the sidebar's first entry is Jira's "For you" (formerly "My tasks"), with Jira's tabs: Worked on, Viewed,
    Assigned to me and Starred;
  - the Summary page has Jira's summary sections;
  - sub-tasks appear on the board as their own cards, and "Group by" offers Jira's choices: None, Assignee, Epic
    and Sub-tasks.

## User Scenarios & Testing *(mandatory)*

Terms used in this spec:

- **Space**: what Phases 1 to 4 called a project. Its key, members, roles, template, columns and work are the
  same; only the word changes. The roles are **Space admin** (formerly Project Admin), **Member** and **Viewer**.
- **Work type**: Epic, Story, Task, Bug, Sub-task (Kanban, Scrum and blank spaces), and Stage, Task, Milestone and
  Sub-task (Stage-gate spaces), as in Phase 3.
- **Contributor**, **can see**, **container** and **open**: as in Phases 1 to 4.
- **Column**: a board column, which is also a status with a status type (To do, In progress or Done), as in
  Phase 1.

### User Story 1 - Create any work item from anywhere (Priority: P1)

From any screen, a contributor presses "Create" in the header and fills in one form, as in Jira: the space, the
work type, a summary and the other fields that type has. Epics, sub-tasks, stages and milestones are created the
same way as tasks, without hunting for the right page.

**Why this priority**: The owner could not find how to create an epic or a sub-task. One form for every work
type, always in the same place, is the most visible Jira habit and closes the biggest gap.

**Independent Test**: On the "For you" page, press "Create", choose the space `WEB` and "Epic", type "Card
refunds" and create it; press "Create" again, choose "Story", type "Refund to card", choose the parent "WEB-1
Card refunds", assign it, set its priority and labels, tick "Create another" and create it; in the form, still
open, choose "Sub-task" and the parent "WEB-2 Refund to card", and create it. Every item appears on the board,
list and timeline as expected.

**Acceptance Scenarios**:

1. **Given** a contributor on any screen, **When** they press "Create" in the header, **Then** a form opens with
   the space (the current space when there is one, otherwise the last space they created in), the work type, the
   summary and the description, with the focus on the summary.
2. **Given** the form, **When** the contributor chooses a space, **Then** the work type list offers only the
   types that space uses, and the other fields follow the type: status (the space's columns), assignee, priority,
   labels, parent, sprint, start and due dates, and estimate where the type and template have them.
3. **Given** the work type "Sub-task", **When** the contributor tries to create it without a parent, **Then**
   it is refused with "Choose the item this sub-task belongs to."; the parent list offers the space's stories,
   tasks and bugs.
4. **Given** a story, task or bug, **When** the contributor chooses a parent, **Then** the list offers the
   space's epics (stages in a Stage-gate space), and the item is created in it.
5. **Given** a Scrum space with a sprint, **When** the contributor chooses that sprint, **Then** the item is
   created in the sprint; without a choice it goes to the bottom of the backlog, as in Phase 3.
6. **Given** a filled form, **When** the contributor presses "Create", **Then** the item is created with all the
   values, its history records them, the watchers and notifications of Phase 4 follow, and a message "WEB-7
   created" offers a link to open it.
7. **Given** "Create another" is ticked, **When** an item is created, **Then** the form stays open with the
   same space, type and parent, and an empty summary and description.
8. **Given** a person who is a Viewer in every space, **When** they look at the header, **Then** the "Create"
   button is not offered; a contributor sees only the spaces they can contribute to, and only Space admins and
   administrators are offered "Stage".
9. **Given** a form with typed text, **When** the contributor closes it, **Then** they are asked whether to
   discard it.

---

### User Story 2 - Change the board's columns on the board (Priority: P2)

A Space admin changes the board's columns where they see them, as in a Jira team-managed space: "+" after the
last column adds one, and each column's menu renames it, sets its limit, moves it, changes its status type or
deletes it.

**Why this priority**: The owner thought the columns were fixed. The editor exists in the space's settings, but
Jira users expect to change columns on the board. It reuses the column rules already built.

**Independent Test**: On the `WEB` board, add a column "Review" of type In progress after "In Progress" with
"+", rename it "Code review" from its menu, set its limit to 3, move it left, and delete it while it holds two
cards, moving them to "In Progress". The board and the space settings show each change.

**Acceptance Scenarios**:

1. **Given** a Space admin on a board, **When** they press "+" after the last column, **Then** they give a name
   and a status type, and the new column appears at the end.
2. **Given** a column's menu, **When** a Space admin chooses rename, set or clear the limit, move left or right,
   **Then** the board shows the change at once.
3. **Given** an empty column, **When** a Space admin changes its status type, **Then** it changes; for a column
   with cards the choice is not offered and the menu says why.
4. **Given** a column with cards, **When** a Space admin deletes it, **Then** they choose the column the cards
   move to, as in the space settings; the last To do column and the last Done column cannot be deleted.
5. **Given** a Member or Viewer, **When** they look at the board, **Then** they see no "+" and no column menu.
6. **Given** any refusal (a name already used, more than 10 columns, a stage lock, a change made meanwhile by
   someone else), **When** it happens on the board, **Then** the board shows the same reason as the space
   settings and the current columns.

---

### User Story 3 - Create a space from a template gallery (Priority: P3)

Anyone starts a space as in Jira: they browse a gallery of templates, see what each gives them, and choose one.
The new blank template lets them choose their own columns and work types.

**Why this priority**: Starting a space is the first thing a new team does. A gallery with previews explains the
templates, and the blank template serves teams whose way of working fits none of them.

**Independent Test**: Press "Create space", look at the preview of each template, choose "Blank", name the
space "Data Platform" (key `DATA`), keep "To do", rename "In progress" to "Building", add "Validating" of type In
progress, keep "Done", choose the work types Epic, Task and Bug, and create it. The board shows the four
columns, and the Create form offers only Epic, Task, Bug and Sub-task in `DATA`.

**Acceptance Scenarios**:

1. **Given** anyone signed in, **When** they press "Create space" on the Spaces page, **Then** a gallery shows
   Kanban, Scrum, Stage-gate and Blank, each with a short description.
2. **Given** a template in the gallery, **When** they select it, **Then** a preview shows its columns, its work
   types and its views (board, backlog, timeline, stages) before they continue with "Use template".
3. **Given** a chosen template, **When** they continue, **Then** they enter the name, the key (suggested from
   the name, as today) and a description, and create the space; they become its Space admin.
4. **Given** the Blank template, **When** they set it up, **Then** they start from "To do", "In progress" and
   "Done", can add, rename, reorder and remove columns (1 to 10 columns, at least one To do and one Done), and
   choose its work types among Epic, Story, Task and Bug (at least one of Story, Task or Bug); Sub-task is always
   available.
5. **Given** a blank space, **When** people work in it, **Then** it behaves like a Kanban space with only the
   chosen work types.
6. **Given** a Kanban, Scrum or blank space, **When** a Space admin turns a work type on or off in the space
   settings, **Then** the Create form and the inline create boxes offer only the types that are on; turning off a
   type that items still use is refused with "Bugs are still used by 4 items in this space."
7. **Given** a new space, **When** someone who is neither its member nor an administrator looks for it, **Then**
   they cannot find or open it, as in Phase 2; the creator adds members from the space's Members page.

---

### User Story 4 - Find your way like in Jira (Priority: P4)

People move around U-PMS as in today's Jira Cloud: "spaces" everywhere, a left sidebar on every page that
leads to their work, recent and starred things, spaces and filters, tabs for a space's views, and a Summary page
that shows how the space is doing.

**Why this priority**: The layout is what makes U-PMS feel like Jira day to day. It changes no rule, so it comes
after the missing actions.

**Independent Test**: Star the `WEB` space from the Spaces page; the sidebar shows it under Starred and under
Spaces; open it from there: its tabs are Summary, Board, List and Timeline, and its "…" menu offers Members, Space
settings and Deleted items; collapse the sidebar and reload; open Summary and see the last 7 days' counts, the
status overview, the recent activity and the other sections; open "For you" and its four tabs; every screen says
"space", never "project"; an old bookmark to `/projects/WEB/board` opens the board.

**Acceptance Scenarios**:

1. **Given** any screen, **When** people read it, **Then** it says "space" and "Space admin" where it used to say
   "project" and "Project Admin", including page titles, menus, messages, notifications and emails.
2. **Given** an address from Phases 1 to 4 (a bookmark, a notification or an email link), **When** someone
   opens it, **Then** it opens the same page at its new address.
3. **Given** any page, **When** people look at the left sidebar, **Then** it offers "For you" (formerly "My
   tasks"), "Recent" (the 10 items and 5 spaces they opened last), "Starred" (the spaces they starred), "Spaces"
   (their starred spaces, then the ones they opened last, up to 8, and "View all spaces"), "Filters" (the search
   page and their saved filters) and "Accounts" (administrators); the current place is marked.
4. **Given** the sidebar, **When** someone collapses it, **Then** it stays collapsed on their next visit; on a
   phone it opens as a menu from the header.
5. **Given** a space, **When** someone opens any of its pages, **Then** the space's name and template are shown
   with tabs for the views it has: Summary, Backlog (Scrum), Board, List, Timeline and Stages (Stage-gate); the
   current tab is marked; a "…" menu offers Members, Space settings (Space admins and administrators) and Deleted
   items (administrators).
6. **Given** a space they can see, **When** someone stars it from its page or from the Spaces page, **Then** it
   appears under Starred and first under Spaces in their sidebar; unstarring removes it; only they see their stars.
7. **Given** the Summary page, **When** a member opens it, **Then** it shows, as Jira's summary does: how many
   items were completed, updated and created in the last 7 days and how many are due in the next 7 days; a
   status overview (items by status type); the recent activity (the latest changes from the items' history);
   the priority breakdown; the types of work; the team workload (open items per assignee, and unassigned); and
   the epic progress (stage progress in Stage-gate spaces); each count opens the search page with those items.
8. **Given** the "For you" page, **When** someone opens it, **Then** it has Jira's tabs: "Worked on" (items they
   created, changed or commented on in the last 30 days), "Viewed" (the items they opened last), "Assigned to me"
   (what "My tasks" showed, with its filters) and "Starred" (their starred spaces).
9. **Given** the header, **When** people look at it, **Then** it offers the button to collapse or open the
   sidebar, the U-PMS name (leading to For you), the search box, the Create button, the bell and the user menu.

---

### User Story 5 - Read the board like in Jira (Priority: P5)

Cards carry what Jira cards carry, sub-tasks are cards too, and the board can be grouped into swimlanes by epic,
assignee or parent, as on a Jira team-managed board.

**Why this priority**: It polishes daily use of the board. The information is already in U-PMS; this shows it
the way Jira users expect.

**Independent Test**: On the `MOB` board, cards show their type icon, summary, key, a colored tag with their epic
"Card refunds", the assignee's avatar, priority, estimate, labels and sub-task progress, and sub-tasks show their
parent's key; choose "Group by Epic": cards appear in a lane per epic, with "No epic" last, each lane with its
count; drag a card into another epic's lane and see its epic change; choose "Group by Assignee", then
"Sub-tasks", then "None".

**Acceptance Scenarios**:

1. **Given** a card, **When** it is shown, **Then** it has its summary first, then its type icon, key,
   priority, estimate, sub-task progress, due date, labels, epic or stage tag and the assignee's avatar, each
   also in words for screen readers.
2. **Given** an epic, **When** its tag is shown on cards, the list and the timeline, **Then** it has the same
   color everywhere; a contributor can change the color in the epic's drawer from a set of 8.
3. **Given** a board, **When** someone chooses "Group by Epic" (or "Stage"), "Assignee" or "Sub-tasks",
   **Then** cards are shown in a collapsible lane per epic, stage, person or parent item (each parent that has
   sub-tasks, with its sub-tasks in the lane), with "No epic", "No stage", "Unassigned" or "Everything else"
   last, each lane with its card count, and the choice is kept in the address like the quick filters.
4. **Given** swimlanes, **When** someone moves a card between columns in its lane, **Then** the same rules apply
   as without lanes; the quick filters and "Only my tasks" still apply.
5. **Given** Epic, Stage or Assignee lanes, **When** a contributor moves a card into another lane, by drag and
   drop or with the keyboard, **Then** its epic, stage or assignee changes to the lane's (cleared in "No epic",
   "No stage" or "Unassigned"), with the drawer's rules: only people who can be assigned, and the stage lock; a
   sub-task follows its parent's epic or stage, so it cannot be moved into another epic or stage lane; in
   Sub-tasks lanes, cards move only within their lane.
6. **Given** a sub-task, **When** the board is shown, **Then** it is a card in its status's column, with its
   parent's key, and moves between columns like any card; it takes its parent's epic or stage lane.

---

### Edge Cases

- A person opens the Create form, and meanwhile loses the right to contribute to the chosen space: creating is
  refused with "You can no longer add work to this space.", and the typed text stays.
- The chosen parent is deleted or moved to another space while the form is open: creating is refused with "The
  parent item no longer exists."; the person chooses another.
- A Space admin deletes a column from the board while a Member is moving a card into it: the move is refused as a
  change made meanwhile, and the Member's board shows the current columns.
- A Space admin turns off a work type in a space while someone has the Create form open with that type:
  creating is refused with "This space no longer uses Bugs."
- Two people create spaces with the same key at once: one gets "The key DATA is already taken.", as today.
- A person is removed from a space they starred or opened lately: it disappears from their Starred, Recent and
  Spaces lists, and so do its items.
- The Summary page of a space with no work shows zero counts and "Nothing due soon." rather than empty boxes.
- An epic is deleted: its tag disappears from cards, and its lane disappears; its items join "No epic".
- A space has more than 50 epics: the "Group by Epic" lanes are collapsed past the first 20, and each lane still
  shows its count.
- A card is moved into another person's lane, but that person can no longer be assigned (removed from the space
  or deactivated): the move is refused with the drawer's message, and the card returns to its lane.
- Someone moves a sub-task's parent into another epic's lane: the sub-tasks follow it to that lane.
- A sub-task's parent is in a locked stage: the sub-task card cannot leave its "to do" column, with the stage
  lock's message, as in the drawer.

## Requirements *(mandatory)*

### Functional Requirements

**Create form (User Story 1)**

- **FR-001**: The header MUST offer "Create" on every screen to people who can contribute to at least one space,
  opening a form that keeps the address of the screen underneath.
- **FR-002**: The form MUST offer the spaces the person can contribute to, defaulting to the current space, or
  else the last space they created an item in, or else the first in alphabetical order.
- **FR-003**: The form MUST offer only the work types the chosen space uses; "Stage" only to Space admins and
  administrators; "Sub-task" in every space.
- **FR-004**: The form MUST show the fields the chosen type has: summary (required), description (formatted
  text, as in Phase 4), status (the space's columns; default the first To do column), assignee (people who can
  be assigned, as in Phase 2), priority (default Medium), labels (as in Phase 4), parent, sprint (Scrum stories,
  tasks and bugs), start and due dates (one date for milestones), and estimate (Scrum and Kanban stories, tasks
  and bugs).
- **FR-005**: The parent MUST be the space's epics for stories, tasks and bugs (stages in Stage-gate spaces, also
  for milestones), and the space's stories, tasks and bugs for sub-tasks, which require one; the list MUST be
  searchable by key or title.
- **FR-006**: Creating MUST apply every Phase 1 to 4 rule the separate actions apply today (title length,
  dates, estimate, labels, stage lock, sprint state, assignee rules, mentions, watchers, notifications, search
  words) and record each value in the item's history in one change.
- **FR-007**: After creating, the person MUST see "KEY created" with a link that opens the item; with "Create
  another", the form MUST stay open with the same space, type and parent.
- **FR-008**: Closing the form with typed text MUST ask before discarding it.
- **FR-009**: The board's, list's and backlog's inline create boxes MUST remain.

**Columns on the board (User Story 2)**

- **FR-010**: Space admins and administrators MUST be able to add a column from the board with "+" after the last
  column, giving its name and status type.
- **FR-011**: Each column on the board MUST have a menu for Space admins and administrators to rename it, set or
  clear its limit, move it left or right, change its status type when it is empty, and delete it, choosing where
  its cards go.
- **FR-012**: Board changes to columns MUST follow the rules, refusals and audit of the space settings' column
  editor (Phase 1 FR-034 to FR-040 and the Phase 3 stage lock), which stays.
- **FR-013**: Scrum boards without an active sprint MUST still show the columns, empty, so that they can be
  managed there.

**Create space (User Story 3)**

- **FR-014**: "Create space" MUST open a gallery of the templates Kanban, Scrum, Stage-gate and Blank, each with
  a description, and a preview of its columns, work types and views.
- **FR-015**: After the template, the creator MUST enter the name, the key and an optional description, as in
  Phase 3, and becomes the space's Space admin.
- **FR-016**: The Blank template MUST let the creator set 1 to 10 columns (names unique, at least one To do and
  one Done, each with a status type), starting from "To do", "In progress" and "Done", and choose the work types
  among Epic, Story, Task and Bug, at least one of Story, Task or Bug; Sub-task is always on.
- **FR-017**: A blank space MUST behave like a Kanban space limited to its work types. In every Kanban, Scrum and
  blank space, Space admins and administrators MUST be able to turn Epic, Story, Task and Bug on or off in the
  space settings, keeping at least one of Story, Task or Bug, and MUST be refused turning off a type that a live
  or deleted item of the space uses; Sub-task is always on, and Stage-gate spaces keep their fixed types. Existing
  Kanban and Scrum spaces start with all four on.
- **FR-018**: A space MUST stay visible only to its members and to administrators, as in Phase 2 (Jira's
  "Private"); creating a space MUST NOT offer an access choice.

**Navigation and wording (User Story 4)**

- **FR-019**: Every screen, message, notification and email MUST say "space", "spaces", "space key" and "Space
  admin" where Phases 1 to 4 said "project", "projects", "project key" and "Project Admin".
- **FR-020**: Every address of Phases 1 to 4 MUST keep working by leading to the same page at its new address.
- **FR-021**: Every page MUST show a left sidebar with For you, Recent, Starred, Spaces, Filters and Accounts
  (administrators), as listed in User Story 4, scenario 3, marking the current place; it MUST collapse, remember
  that per person, and open as a menu on phones. Recent, Starred and Spaces MUST list only spaces and items the
  person can still see.
- **FR-021a**: Each space's pages MUST show the space's name and template, tabs for its views (Summary, Backlog
  for Scrum, Board, List, Timeline, Stages for Stage-gate) and a "…" menu with Members, Space settings (Space
  admins and administrators) and Deleted items (administrators).
- **FR-021b**: Anyone MUST be able to star and unstar the spaces they can see, from the space and from the
  Spaces page; stars are personal.
- **FR-022**: Each space MUST have a Summary page with Jira's summary sections, as listed in User Story 4,
  scenario 7: the last 7 days' completed, updated and created counts and the count due in the next 7 days (in the
  viewer's time zone), status overview, recent activity (the 20 latest changes of the last 30 days), priority
  breakdown, types of work, team workload and epic (or stage) progress; each count MUST lead to the search page
  showing exactly those items of the space.
- **FR-022a**: The "For you" page MUST have the tabs Worked on (items the person created, changed or commented on
  in the last 30 days, newest first), Viewed (the 10 items they opened last), Assigned to me (the Phase 2 "My
  tasks" page, unchanged) and Starred (their starred spaces); every tab MUST show only items and spaces they can
  still see.
- **FR-023**: The header MUST offer the sidebar button, the U-PMS name (leading to For you), the search box,
  "Create", the bell and the user menu; the links that were in the header move to the sidebar.

**Cards and swimlanes (User Story 5)**

- **FR-024**: Cards MUST show the summary, type icon, key, priority, estimate, sub-task progress, due date,
  labels, the epic or stage tag and the assignee's avatar, each also in words for screen readers.
- **FR-025**: Each epic MUST have a color, chosen automatically when it is created and changeable by
  contributors from a set of 8; its tag MUST use it on cards, rows and the timeline.
- **FR-026**: Boards MUST offer "Group by": None, Epic (Stage on Stage-gate boards), Assignee and Sub-tasks, as
  collapsible lanes with counts, "No epic", "No stage", "Unassigned" or "Everything else" last, kept in the address
  with the quick filters.
- **FR-027**: In lanes, cards MUST move between columns by drag and drop and by keyboard, with the Phase 1 to 4
  rules. In Epic, Stage and Assignee lanes, moving a card to another lane MUST change its epic, stage or assignee
  (cleared in the "No …" and "Unassigned" lanes) with the drawer's rules and history, in one change with any
  column change; a sub-task MUST NOT move to another epic or stage lane; in Sub-tasks lanes cards stay in their
  lane.
- **FR-027a**: Sub-tasks MUST appear on the board as their own cards in their status's column, showing their
  parent's key, with the same move rules as other cards (including the stage lock of their parent's stage);
  they count toward column limits, and the quick filters apply to them.

**Across the stories**

- **FR-028**: Every new or changed screen MUST meet WCAG 2.2 AA, work with the keyboard alone and at 360 px, as in
  Phases 1 to 4: the Create form, the column menu, the gallery, the sidebar, the space tabs, the Summary
  page, the "For you" page and the lanes (moving a card between lanes included).
- **FR-029**: Permissions MUST stay as in the Phase 4 matrix; the new actions (create from the form, manage
  columns from the board, a space's work types, epic color, starring a space, moving a card between lanes) MUST
  be added to it.

### Key Entities

- **Space**: what was a project. Gains its **work types**: the ones that are on, among Epic, Story, Task and Bug
  (Kanban, Scrum and blank spaces; Sub-task always), or the fixed Stage-gate types. Who can see it stays as in
  Phase 2: its members and administrators.
- **Epic color**: a color from a set of 8 on each epic.
- **Personal layout choices**: whether a person keeps the sidebar collapsed.
- **Starred space**: a person and a space they starred; personal, removed when they can no longer see the space.
- **Recently opened space**: the spaces a person opened last, for Recent and Spaces in the sidebar (work items
  opened last already exist since Phase 4).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A first-time user creates an epic, a story in it and a sub-task of the story from the Create
  button within 3 minutes, without help, in at least 9 out of 10 pilot sessions.
- **SC-002**: A Space admin adds, renames and deletes a column on the board, each in no more than 3 actions.
- **SC-003**: A user creates a blank space with their own columns and work types in under 2 minutes.
- **SC-004**: No screen, message, notification or email says "project" for a space (checked automatically on
  every page and message).
- **SC-005**: Every acceptance test of Phases 1 to 4 still passes, with only the wording changed.
- **SC-006**: Opening the Create form, the Summary page, the "For you" tabs, the sidebar and a board grouped by
  epic (with sub-tasks as cards), and moving a card between lanes, each respond within 1 second
  at the 95th percentile, with 300 users and about 500,000 work items (as Phase 1 SC-002).
- **SC-007**: Automated accessibility checks report zero WCAG 2.2 AA violations on the new and changed screens,
  and every acceptance scenario can be completed with the keyboard alone.
- **SC-008**: In the pilot, at least 80% of users who know Jira rate finding their way and creating work as
  "like Jira" or "easier than Jira".

## Assumptions

- Only words change for spaces: keys, members, roles, templates, data and permissions stay as they are, and the
  internal name may stay "project".
- New addresses use "spaces" (for example `/spaces/WEB/board`); the old ones lead there.
- Blank spaces have no sprints or gates; like Kanban spaces they can switch to Scrum, keeping their work types.
- Stage and Milestone stay Stage-gate types; blank spaces do not offer them.
- The Summary page is the one view per space described here; richer dashboards and portfolios are Phase 5.
- Jira features outside the five stories (workflows with transitions, custom fields, issue links other than
  dependencies, forms, automation) are not part of this pass.
- Sign-in through LDAP and email through the company's SMTP server are done at UAT (owner decision of
  2026-10-07); until then the development sign-in and the switched-off email remain.
