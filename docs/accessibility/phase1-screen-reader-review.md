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

### Phase 3 screens (create project, backlog, sprints, Stages, timeline plan, drawer sections)

- **Component tests** check the names that screen readers read: the type in words beside its symbol, the Move group of
  each backlog row with Escape, each gate's state and each stage's place in words, each timeline bar's name with its
  dates, progress and what it waits for (with "(late)"), the arrows hidden from screen readers, and the timeline's
  Actions group with Escape (`BacklogPageTests`, `StagesPageTests`, `TimelinePlanPageTests`, `DependencyListTests`).
- **axe-core** scans at 1280 px and 360 px of the create-project dialog, the backlog, the sprint dialogs and report, the
  Stages view and its dialogs, "My tasks" with a gate waiting, and the timeline with containers, markers and arrows are
  written in the `P3_US1`–`P3_US4` journeys. **They have not run yet**: the machine used to build Phase 3 has no
  Playwright browser. Run them before the manual pass below.

### Phase 4 screens (editor, notifications, attachments, search, labels, email choice)

- **Component tests** check the names that screen readers read: the editor's labelled text box and its toolbar as one
  tab stop with arrow keys, the table tools, the "@" suggestions as a list box with the active option, the bell's
  unread count in words and its panel, the attachment list with each file's state in words and the preview dialog,
  the search box as a combo box with recent items, the search page's removable filter chips and saved-filter panel,
  the label combo box and "Remove label …" buttons, the board's quick filters as toggle buttons, and the profile's
  email choice as a radio group (`RichTextEditorTests`, `NotificationBellTests`, `AttachmentsSectionTests`,
  `SearchBoxTests`, `SearchPageTests`, `LabelsSectionTests`, `BoardQuickFilterTests`, `ProfileTests`).
- **axe-core** scans at 1280 px and 360 px of the editor with its suggestions, the notification panel, the attachment
  list and a preview, the search box with recent items, the search page with the saved-filter panel, the label combo
  box, a quick-filtered board and the profile are written in the `P4_US1`–`P4_US5` journeys. Run them before the
  manual pass below.

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
2. Choose "Status type: In progress". Expect the count to be read (for example "4 items") and the list "Active
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

### Phase 3 screens: setup

Use three projects, with the tester as Project Admin in each:

- **Kanban**: at least one epic with two items in it.
- **Scrum**: ten estimated stories in the backlog, one completed sprint and one planned sprint.
- **Stage-gate**: its four stages. The first stage's gate has two approvers, the tester and a colleague. The second
  stage holds a task.

In the Stage-gate project, schedule the stages and three tasks, add a milestone, and make one task wait for another.
A second account, named as an approver, is needed for the gate steps.

### Phase 3: create project and drawer (user story 1)

1. "Create project": after the name and key, expect the group "Template" with three radio buttons ("Kanban", "Scrum",
   "Stage-gate"), each with its description.
2. On a board, read a card: its type is read in words ("Bug", "Story") before the title; the symbol is not read.
3. Open an epic. Expect "Type, combo box, Epic", then the section "Items in this epic" with a progress bar named by its
   text (for example "1 of 2 done (50%)") and a box to add an item. Open a story: the "Estimate" field and the "Epic"
   combo box.
4. In the List view, the Filters region also holds "Type" and "Epic" (or "Stage"). A chosen value is read in the list
   "Active filters" as "Remove filter Type: Bug, button".

### Phase 3: backlog and sprints (user story 2)

1. Open Backlog from the project header. Expect one heading level 2 per sprint, then "Backlog". Each sprint reads its
   totals (items and points) and its dates or state.
2. Tab to a row: its link (the title), then "Move MOB-7 to…, button, collapsed". Enter expands the group "Move MOB-7":
   "Move to top", "Move to bottom", "Move up", "Move down" and "Move to MOB Sprint 2". Choose one and expect "MOB-7 moved
   to MOB Sprint 2, position 3 of 6." with focus back on the row's Move button. Escape closes the group without moving.
3. "Create sprint": expect "Created MOB Sprint 3.". "Start MOB Sprint 3, button" opens the dialog "Start MOB Sprint 3"
   with the start date, the end date (two weeks later) and the goal, each labelled.
4. On the board, the active sprint's banner is a heading with its name, then its dates ("Overdue" once its end has
   passed) and, for Project Admins, "Complete sprint, button". The dialog "Complete MOB Sprint 3" says how many items
   are done and open, and asks where the open items go.
5. From "Past sprints", open a report. Expect the sprint's name as a heading, then four tables whose captions name
   them ("Added", "Removed", "Completed" and "Not completed"), each with a line that explains it.

### Phase 3: stages and gates (user story 3)

1. Open Stages. Expect one heading level 2 per stage, each followed by its place ("Current", "Locked" or "Approved"),
   its dates and progress, the list "Approvers" and the gate's state in words.
2. "Set up the Initiation gate, button" opens the dialog "Set up the Initiation gate". Type in the person search: the
   list "People found" names each person with their user name, and the search's description says that people outside
   the project become Viewers. "Remove Omar Ali, button" removes an approver. Expect "Saved the Initiation gate."
3. On a locked stage's task, try to start it (drawer status or board Move). Expect the alert "This work cannot start
   until the Initiation gate is approved." The drawer also reads "This work can start once the Initiation gate is
   approved."
4. "Request approval", as the colleague: the dialog "Request approval of the Initiation gate?" lists the stage's open
   items first.
5. As the approver, open "My tasks": the region "Gates waiting for you" lists "Initiation gate, link" with the project and
   who requested it. Follow it: focus lands on the stage. "Send back the Initiation gate, button" opens a dialog whose
   note is required; sending without one reads the error. Then "Approve the Initiation gate". Expect the stage's place
   to read "Approved" and the next stage "Current".

### Phase 3: timeline plan (user story 4)

1. Open Timeline in the Stage-gate project. Each stage's row reads "Hide the items of CBU-1, button, expanded", the
   stage link, "0% done", and "Actions for CBU-1, button, collapsed". Its bar reads "CBU-1 Initiation, 1 Oct 2026 to 14
   Oct 2026, 0% done, To do, unassigned". The gate marker's text follows ("Gate approved").
2. Collapse a stage with Enter: its items are no longer read, and the button reads "Show the items of …, collapsed".
3. The milestone reads "CBU-5 Planning sign-off, milestone on 28 Oct 2026, To do, unassigned, button". Press Right twice
   (Shift+Right does the same): expect "CBU-5: due 30 Oct 2026. Enter saves, Escape cancels.". Enter reads "CBU-5 is now
   due 30 Oct 2026."
4. A waiting task's bar ends with "waits for CBU-10", or "waits for CBU-10 (late)". The arrows themselves are not read.
5. "Actions for CBU-11": Enter expands a group of buttons ("Open", "Add dependency", and "Schedule" for items without
   dates). Escape closes it with focus back on the button. "Add dependency" opens the dialog "Add a dependency to
   CBU-11": the combo box "This item" ("waits for…" or "blocks…"), the search box "Find the other item", and the list
   "Items found". A refused link reads its reason (for example the loop). A saved one reads "CBU-11 now waits for
   CBU-10."
6. In a Scrum project, the lane "Sprints" reads each sprint's name, state, and dates.
7. In a drawer, the section "Dependencies" has the headings "Waits for" and "Blocks". Each item reads its key, title,
   status and dates, "Late" where it applies, and "Remove the dependency: CBU-11 waits for CBU-10, button".

### Phase 4: formatted text, mentions and watchers (user story 1)

1. In a drawer, Tab to "Edit description, button" and press Enter. Expect the description's text box ("edit, multi
   line") and then the toolbar as one tab stop: "Formatting: Description, toolbar", the group "Text" and "Bold, toggle
   button, not pressed"; Right moves through the tools and the groups "Blocks" and "Table menu".
2. Press Ctrl+B, type a word, press Ctrl+B. Expect "Bold, toggle button, pressed" while on, and the word read as bold
   later in browse mode (NVDA reads "bold" with formatting announcements on).
3. Type "@bi". Expect "People to mention, list" and the first option read, for example "Bilal Ahmed, bilal, 1 of 1".
   Enter inserts the mention; once saved, the comment reads "@Bilal Ahmed".
4. Insert a table from the toolbar. Inside it, the tools of the "Table menu" group are enabled and read their names;
   outside it they read "unavailable".
5. "Watch, button" toggles to "Stop watching". The count reads "2 watchers, button, collapsed" and opens the list.

### Phase 4: notifications (user story 1)

1. In the header, the bell reads "Notifications, 2 unread, button, collapsed". Enter opens the panel: "Notifications,
   dialog", "Mark all as read, button", then each notification as a link with its sentence ("Amina Khan mentioned you
   on WEB-1 Plan the launch"), its time, and "Mark as read: …, button" (or "Mark as unread").
2. Escape closes the panel with focus back on the bell. Opening a notification opens the item's drawer.

### Phase 4: attachments (user story 2)

1. The drawer's "Attachments" section reads "Attach files, button". After choosing a file, expect "report.pdf attached.
   It is being checked for malware.", then the file's row with its size, uploader and time.
2. A file being checked reads "Being checked" and has no link. A checked image reads "Enlarge home.png, button";
   Enter opens the dialog "home.png" with the image's name as its text, and Escape closes it with focus back.
3. "Download report.pdf, link" downloads; "Remove report.pdf, button" asks to confirm and reads the result.

### Phase 4: search and saved filters (user story 3)

1. On any page, Tab to "Search or type a key, combo box, collapsed". With it empty, expect it to expand and read
   "Recently viewed, list" with each item ("WEB-4 Book the venue, Website Revamp"); Down and Enter open one.
2. Type "mob-99" and Enter: expect "No work item MOB-99 was found." Type words and Enter: the search page opens and reads
   "3 results".
3. On the search page, the filters are labelled ("Space", "Type", "Status type"…); each chosen filter adds a chip
   read as "Remove filter Status type: To do, button". The results table reads its caption "Search results".
4. In "Saved filters, complementary", "Save this search as, edit" and "Save as filter" save it; the status reads
   "Saved as Unassigned refunds.". "Rename", "Update with this search", "Share" (a group of checkboxes, one per project)
   and "Delete" (with "Delete filter" and "Keep it") read their results. ("Share" lists spaces since the Jira parity pass.)

### Phase 4: labels and quick filters (user story 4)

1. In a drawer, "Labels, heading level 3", each label followed by "Remove label launch, button", and "Add label, combo
   box". Typing "lau" reads "Labels in use, list" and "launch, 1 of 1"; Enter adds it and reads "Added the label
   launch.". Viewers hear the labels only.
2. On the board, after "Only my tasks" and "Assignee", the groups "Types", "Epics" (or "Stages") and "Labels" read
   their toggle buttons, for example "Bug, toggle button, not pressed". Pressing one reads "Showing 2 of 9 cards.",
   and each column's count reads "1 of 3 match the filter".

### Phase 4: email choice (user story 5)

1. On the profile, "Email notifications, heading level 2", then the radio group: "All notifications, radio button, not
   checked, 1 of 3", "Only notifications aimed at me, radio button, checked", "None". Each reads its hint.
2. Choose "None" with the arrow keys and press "Save email choice". Expect "Email choice saved."

### Jira parity pass: layout, sidebar and "For you" (user story 4)

1. On any page, the landmarks read "banner", "Main, navigation" (the sidebar) and "main". The sidebar's buttons read
   "Recent, button, collapsed", "Starred, button, collapsed", "Spaces, button, expanded" and "Filters, button,
   collapsed"; Enter opens one, and its links follow. "For you" is a link.
2. In the header, "Sidebar, button, expanded" hides the sidebar and reads "collapsed"; reload: it is still collapsed.
3. At 360 px, "Menu, button, has popup dialog" opens the "Menu" dialog with the same list; Escape closes it and focus
   returns to the button.
4. On a space page, "Star WEB, toggle button, not pressed" stars the space ("pressed"). The tabs read as links in the
   "Space, navigation" ("Board, current page"). "More space pages, button, collapsed" opens a menu: Down and Up move
   between "Members", "Space settings" and "Deleted items"; Escape returns to the button.
5. On "Summary", the counts read as links ("3 created in the last 7 days"); each section is a heading level 2 with a
   table or a list ("Work items by status type" caption); the epic progress bars read "Card refunds, 40% done".
6. On "For you", the tabs "Worked on", "Viewed", "Assigned to me" and "Starred" read as links in the "For you"
   navigation, with the current one marked.

### Jira parity pass: Create form, gallery and columns (user stories 1 to 3)

1. "Create, button" in the header opens the "Create" dialog with focus on "Summary *". Each field reads its label;
   "Description" is the formatted text editor; "Parent" reads as a combo box with its suggestions as a list.
2. Leave "Summary" empty and press "Create": the error reads "Enter a title." and focus moves to the field. With
   "Create another" checked, a created item reads "Created WEB-12: …" and focus returns to "Summary".
3. Escape with text typed asks "Discard this work item?" with "Discard" and "Keep editing".
4. "Create space" opens the gallery: the templates read as radio buttons ("Scrum, radio button, 2 of 4") and the
   preview reads "Preview of the Scrum template, region". "Use template" moves to the details step with focus on "Name".
5. On the board as a Space admin, each column header has "Column actions for In Progress, button"; its menu reads
   "Rename", "Set limit", "Move left", "Move right", "Change status type" and "Delete" as menu items. "Add column" at
   the end opens its dialog.

### Jira parity pass: cards, lanes and epic colors (user story 5)

1. A card reads its summary first (the link), then "Epic: Card refunds", its labels, "Story", the key ("Sub-task of
   WEB-2" for a sub-task), "High priority", "3 estimate", "2/5 sub-tasks done", "Due 9 Oct" and "Assigned to Sara Khan".
2. "Group by, combo box" offers None, Epic (Stage), Assignee and Sub-tasks. With lanes, each lane header reads
   "WEB-1 Card refunds, 2 cards, button, expanded"; Enter collapses it.
3. On a card, "Move WEB-4 to…" opens the move list; in epic or person lanes it ends with "Lane: Sara Khan"; choosing it
   reads "Moved WEB-4 to Sara Khan, To Do.". In Sub-tasks lanes there is no "Lane" choice.
4. In an epic's drawer, "Epic color, radio group" reads "Purple, radio button, checked, 1 of 8"; arrow keys choose
   another and the status reads "Color changed to Teal".

### Idle warning

1. Leave the board idle for 28 minutes (or lower the timeout on a test system). Expect the warning dialog to be
   announced with its "Stay signed in" button focused.

## 3. Record of the manual pass

| Date | Tester | NVDA / Edge versions | Screen and step | Problem heard | Severity (blocker / serious / minor) | Fixed in |
|------|--------|----------------------|-----------------|---------------|--------------------------------------|----------|
| | | | | | | |

**Done when** every step above was run and every blocker and serious problem is fixed or has an agreed plan.
