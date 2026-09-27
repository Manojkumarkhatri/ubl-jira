# Phase 1 screen-reader review

**Status**: automated pre-check done for the Phase 1 and Phase 2 screens (2026-09-27); **the manual pass with
NVDA and Microsoft Edge is still to be done by a person** before the pilot (Phase 1 tasks.md T112, Phase 2
tasks.md T070; constitution VI). This page is the script for that pass and the place to record it.

## 1. Automated pre-check (done)

What was checked, on the sign-in page, project list, board, task drawer and column settings:

- **axe-core** scans (WCAG 2.0/2.1/2.2 A and AA) on every Phase 1 screen, at 1280 px and at 360 px, with dialogs
  open: zero violations (`tests/Upms.E2E.Tests`, SC-008).
- **Keyboard-only journeys** for each user story, reaching every control with Tab and checking where focus goes
  after each action (`ResponsiveAndKeyboardTests`).
- **Accessibility tree review** of each screen (Playwright ARIA snapshots) for names, roles, states and
  landmarks.

Findings from the tree review, all fixed:

| # | Screen | Finding | Fix |
|---|--------|---------|-----|
| A1 | Column settings | Each row's buttons were all named "Move left", "Move right", "Rename", "Delete", and every limit field "Limit": ambiguous in NVDA's elements list (Insert+F7). | Names include the column: "Move left: In Review", "Limit for In Progress". |
| A2 | Drawer | The header's "Delete" did not say what it deletes. | Named "Delete WEB-1". |
| A3 | Column settings, board | After "Move left/right" or "Move to", focus was lost (the moved row or card is re-inserted). | Focus returns to the same button or to the moved card. |
| A4 | Column settings | "Rename" left focus on the button instead of the new name box. | Focus moves to the name box; after saving or cancelling it returns to "Rename". |
| A5 | Drawer | While the drawer is open the rest of the page is inert, so page-level announcements were not heard. | The drawer has its own status line ("Title saved", "Priority changed to High"). |

What the tree review confirmed:

- Landmarks: skip link, header, `main`; the board is a region named "Board" with one region per column (named by
  the column), each holding a list of cards; each card is an article named by its title.
- The drawer is a modal dialog named "KEY: title"; focus moves to its heading on open and back to the card on
  close; Esc closes it unless a text field holds typed text.
- Every form field has a visible or visually hidden label; errors are linked with `aria-describedby` and
  announced (`role="alert"`); saves and moves are announced through polite status regions.

### Phase 2 screens (members, list, timeline, "My tasks", drawer changes)

- **axe-core** scans at 1280 px and 360 px of the members screen (with the person search open), the board with
  its filters, the drawer with assignee and dates, the List view with filters and chips, the timeline for a
  contributor and for a Viewer, and "My tasks": zero violations (`P2_US1`–`P2_US4` journeys,
  `ResponsiveAndKeyboardTests`, SC-009).
- **Keyboard-only journeys** for each Phase 2 story: add a member, "Assign to me" in the drawer, open a task from
  "My tasks", filter the list, and move and schedule tasks on the timeline (`ResponsiveAndKeyboardTests`).
- **Accessibility tree review** of the same screens, and a scripted run of every quickstart step in real
  browsers (Phase 2 validation.md).

Findings, all fixed:

| # | Screen | Finding | Fix |
|---|--------|---------|-----|
| A6 | My tasks | Each project's link was named only by its key, "(WEB)", which says nothing in NVDA's elements list. | The link is the project's name and key: "Website Revamp (WEB)". |
| A7 | Timeline, list, board, My tasks | After the drawer closed, focus was put back on its task a second time, a moment later. A person who had already moved on, for example to another bar, lost their place, and their next Enter acted on the wrong task. | Focus goes back once, when the drawer closes; later only if focus was lost. |

What the tree review confirmed:

- The project header is a navigation region named "Project" (Board, List, Timeline, Members, and Project
  settings for Project Admins), with the current view marked.
- Members: a region "Add member" with the search box "Find a person", a group "Choose a person" of radio buttons
  named by name and user name, a status line ("1 person found."), and a table "Members of …, Project Admins
  first" whose controls are named per person ("Role of Bilal Ahmed", "Remove Bilal Ahmed").
- Board: a group "Filter cards" with the toggle button "Only my tasks" and the "Assignee" list. Cards read
  "Assigned to …" (the initials are hidden from screen readers), the due date with "Overdue" in words, and "no
  longer on the project" where it applies.
- List: a region "Filters", a list "Active filters" with "Remove filter …" buttons, the count as a status line,
  and a table whose sortable headers are buttons with the sort order exposed.
- Timeline: a group "Scale" of toggle buttons, "Today", "Hide completed" (toggle), and a region "Timeline" where
  each task has a link and a bar button named with key, title, dates, status and assignee. The bar is described
  by the keyboard help. "Show sub-tasks of …" buttons are expandable, and the unscheduled tasks are a
  complementary region with "Schedule KEY" buttons.
- My tasks: one region per project named by its heading, each with a table "Your open tasks in …, soonest due
  first".

## 2. Manual pass with NVDA and Edge (to do)

**Setup**: Windows 10/11, current Microsoft Edge, current NVDA with default settings (browse mode on), a test
account and a project with at least 3 columns, 6 cards, one card with sub-tasks and comments. Screen at 100%
zoom. Run each script with the keyboard only.

For each step, listen for the expected announcement. Record anything missing, wrong, repeated or confusing in
the table in section 3.

### Sign-in and layout

1. Open the sign-in page. Expect: page title "Sign in · U-PMS", heading level 1 "Sign in".
2. Tab through: "User name, edit", "Password, edit, protected", "Show password, toggle button, not pressed",
   "Sign in, button".
3. Sign in with a wrong password. Expect the error message to be read.
4. Sign in correctly. On the project list press H: "Projects, heading level 1"; press D: landmarks
   (banner, main). Press Insert+F7 → Links: project names are listed.

### User story 1: project and board

1. "Create project", Enter. Expect: "Create project, dialog", focus in "Name, edit".
2. Type a name; Tab to "Key, edit": the suggested key is read; Tab on to the hint and description.
3. Create. Expect the board page title and heading.
4. Press D (landmarks) → "Board, region"; inside it, each column is a region named by the column.
5. Press H: column headings; after each heading "cards: 3" (or "cards: 4 of 3" and "Over limit").
6. Tab to "What needs to be done? New task in To Do, edit", type a title, Enter. Expect "Created WEB-7: …".
7. Tab to a card's "Move WEB-7 to…" button, Enter; choose "In Progress: bottom". Expect "Moved WEB-7 to In
   Progress." and focus on the card's title link in its new column.

### User story 2: task drawer

1. On a card title link press Enter. Expect "WEB-1: Design the home page, dialog", then the heading.
2. Tab: "Delete WEB-1, button" (if allowed), "Close task details, button", the title button, "Edit description,
   button", sub-task links ("WEB-8 Wireframes, link"), "Status of WEB-8, combo box", "Mark done: WEB-9, button",
   "New sub-task title, edit", "Add a comment, edit", "Comment, button", comment "Edit"/"Delete" buttons (own
   comments only), "History, collapsed" (Enter expands it), "Status, combo box", "Priority, combo box".
3. Change the title with Enter, type, Enter. Expect "Title saved".
4. Change the priority with the arrow keys. Expect "Priority changed to High".
5. Add a sub-task and a comment. Expect "Sub-task added" and "Comment added".
6. Press Esc. Expect the dialog to close and focus on the card link.

### User story 3: column settings

1. Open Project settings; press H to "Columns, heading level 2"; the list is "Board columns, from left to right".
2. Tab through a row: "Type of To Do, combo box, unavailable" with the hint "Only an empty column can change
   type", "Limit for To Do, spin button", "Move left: To Do, button, unavailable", "Move right: To Do, button",
   "Rename: To Do, button", "Delete: To Do, button".
3. Add a column with the form. Expect "Added the column …".
4. "Move left" on the new column. Expect the status message and focus still on "Move left" of that column.
5. "Delete" a column with items. Expect "Delete In Review?, dialog", the destination list "Move its work items
   (2) to, combo box"; pressing "Delete column" without choosing reads the error.

### Phase 2: setup

Use a project in which the tester is Project Admin. It needs at least three members (one a Viewer), six tasks
of which four have dates (one only a due date), one task with sub-tasks, and two tasks with no dates. At least
one task should be assigned to the tester in a second project.

### Phase 2: members (user story 1)

1. From the project header ("Project, navigation"), open Members. Expect "Members, heading level 1" and, for a
   Project Admin, the region "Add member".
2. In "Find a person, search box" type two letters of a colleague's name. Expect "1 person found." (or the
   number found) and the group "Choose a person" with radio buttons read as name and user name.
3. Choose a person with Space; Tab to "Role, combo box, Member" and to "Add member, button". Expect "Carla
   Mendes added as Member." and focus back in the search box.
4. In the table "Members of …, Project Admins first", move by cell (Ctrl+Alt+arrows). Each row reads the name
   ("(you)" on the tester's row), the user name, "Role of …, combo box", the date added and "Remove …, button".
5. Change a member's role with the arrow keys. Expect "… is now a Viewer."
6. "Remove …", then "Yes, remove". Expect "… removed from the project." Removing yourself as the only Project
   Admin reads the alert "This is the project's last active Project Admin. Make someone else a Project Admin
   first."

### Phase 2: board and drawer changes (user story 2)

1. On the board, read a card: its title, priority and key, "Assigned to …", and the due date with "Overdue" when
   it has passed.
2. In the group "Filter cards", press "Only my tasks, toggle button, not pressed". Expect it to read "pressed"
   and each column heading to be followed by the matching count, for example "cards: 2 of 5 match the
   filter".
3. Open a task. In the details, Tab to "Assignee, combo box" (the options are "Unassigned", the people who can
   work on the project, and your own name with "(me)"), then to "Assign to me, button". Expect "Assigned to
   you".
4. Type a start date and a due date. Expect "Start date saved" and "Due date saved". Type a due date before the
   start date: expect "The due date cannot be before the start date." to be read, and what you typed kept.

### Phase 2: list (user story 3)

1. Open List. Press D: the region "Filters" holds "Words, search box", "Search, button" and the combo boxes
   Status, Status type, Priority, Assignee and Due.
2. Choose "Status type: In progress". Expect the count to be read (for example "4 tasks") and the list "Active
   filters" to hold "Remove filter Status type: In progress, button". Then "Clear filters, button".
3. In the table "Tasks and sub-tasks of …", Tab to the "Due" header button and press Enter. Expect the header to
   read as sorted ascending; press Enter again for descending.
4. Enter on a row's task link opens the drawer. Esc closes it and focus is back on the same link.

### Phase 2: timeline (user story 4)

1. Open Timeline. Expect the group "Scale" ("Months, toggle button, pressed"), "Today, button" and "Hide
   completed, toggle button, not pressed".
2. Tab into the region "Timeline". Each task gives a link ("RMP-3 Design, link") and then its bar ("RMP-3 Design,
   6 Oct 2026 to 17 Oct 2026, To do, unassigned, button") with the keyboard help as its description.
3. On a bar, press Left. **Check whether NVDA passes the key to the page**: in browse mode it may move the reading
   cursor instead. If so, press NVDA+Space for focus mode and record it in section 3. Expect "RMP-3: 5 Oct 2026
   to 16 Oct 2026. Enter saves, Escape cancels." Press Enter and expect "RMP-3 now runs from 5 Oct 2026 to
   16 Oct 2026." with focus still on the bar.
4. Shift+Right changes only the due date and Ctrl+Left only the start date, each read the same way. Escape reads
   "Change to RMP-3 cancelled." Enter with no change opens the task.
5. "Show sub-tasks of …, button, collapsed": Enter expands it, and the sub-task bars follow.
6. In "Unscheduled, complementary", press "Schedule RMP-6". Expect "RMP-6 now runs from … to …." and focus on
   the new bar.
7. With a second browser, change a task's dates and then move its bar here. Expect the alert "… was changed by
   someone else. The timeline now shows its current dates."

### Phase 2: My tasks (user story 2)

1. In the header ("Main, navigation"), open "My tasks". Expect "My tasks, heading level 1" and the count ("3
   open tasks are assigned to you.").
2. Press H: one heading level 2 per project, each a link named by the project ("Website Revamp (WEB)"), followed
   by the table "Your open tasks in …, soonest due first".
3. Read a row: the task link ("WEB-4 Plan the launch"), status, priority ("Medium priority") and due date, with
   "Overdue" where it applies, or "No due date".
4. Open a task from its link, set its status to Done and close the drawer. Expect the row to be gone and focus on
   the "My tasks" heading.

### Idle warning

1. Leave the board idle for 28 minutes (or lower the timeout on a test system). Expect the warning dialog to be
   announced with its "Stay signed in" button focused.

## 3. Record of the manual pass

| Date | Tester | NVDA / Edge versions | Screen and step | Problem heard | Severity (blocker / serious / minor) | Fixed in |
|------|--------|----------------------|-----------------|---------------|--------------------------------------|----------|
| | | | | | | |

**Done when** every step above was run and every blocker and serious problem is fixed or has an agreed plan.
