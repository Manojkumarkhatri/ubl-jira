# Phase 1 screen-reader review

**Status**: automated pre-check done (2026-09-27); **the manual pass with NVDA and Microsoft Edge is still to
be done by a person** before the pilot (tasks.md T112, constitution VI). This page is the script for that pass
and the place to record it.

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

### Idle warning

1. Leave the board idle for 28 minutes (or lower the timeout on a test system). Expect the warning dialog to be
   announced with its "Stay signed in" button focused.

## 3. Record of the manual pass

| Date | Tester | NVDA / Edge versions | Screen and step | Problem heard | Severity (blocker / serious / minor) | Fixed in |
|------|--------|----------------------|-----------------|---------------|--------------------------------------|----------|
| | | | | | | |

**Done when** every step above was run and every blocker and serious problem is fixed or has an agreed plan.
