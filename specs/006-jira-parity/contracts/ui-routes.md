# Contract: UI Routes and Screens (Jira Parity Pass)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

Every space route of Phases 1–4 moves from `/projects/…` to `/spaces/…`. The old addresses lead to the new ones,
keeping the rest of the path and the query string (research R2). Every screen meets WCAG 2.2 AA, works with a
keyboard alone and stays usable at 360 px (FR-028). A non-member opening any space route sees "Not found".

## Routes

| Route | Screen | Who | Requirements |
|-------|--------|-----|--------------|
| `/` | leads to `/for-you` | signed in | FR-023 |
| `/for-you`, `/for-you/worked-on`, `/viewed`, `/assigned`, `/starred` | **New**: "For you", with four tabs (links with `aria-current`); `worked-on` is the default; `assigned` keeps the Phase 2 "My tasks" filters and gates waiting | signed in | FR-022a |
| `/spaces` | **Renamed** from `/projects`: the spaces list, with a star button per space (`aria-pressed`, "Star WEB" / "Unstar WEB") and **Create space** | signed in | FR-014, FR-021b |
| `/spaces/{key}` | leads to `/spaces/{key}/board` | members, Admin | — |
| `/spaces/{key}/summary` | **New**: the Summary page (below) | members, Admin | FR-022 |
| `/spaces/{key}/board` | **Changed**: Jira-style cards, sub-task cards, "Group by" and lanes, "+" and column menus (below) | members, Admin | FR-010–FR-013, FR-024–FR-027a |
| `/spaces/{key}/list`, `/timeline`, `/backlog`, `/sprints/{n}`, `/stages`, `/members`, `/deleted` | **Renamed**; epic tags in their color on list rows and timeline bars | members, Admin (deleted: Admin) | FR-019, FR-025 |
| `/spaces/{key}/settings` | **Renamed** "Space settings"; gains **Work types** (Kanban, Scrum and blank spaces): four checkboxes, each with the number of items using it, and "Save work types" | Space admin, Admin | FR-017 |
| `/search` | **Changed**: the query string's `project` becomes `space` (`project` is still read); gains `resolved` (`from..to`) | signed in | FR-022 |
| `/projects`, `/projects/{*rest}`, `/my-tasks` | **Old**: replaced by `/spaces`, `/spaces/{rest}` and `/for-you/assigned`, with the same query string | — | FR-020 |

## Every page

- **Header**: the sidebar button ("Sidebar", `aria-expanded`), U-PMS (to `/for-you`), the search box, **Create**
  (a primary button, shown to people who can contribute to at least one space), the bell and the user menu.
- **Sidebar** (`nav` "Main"): For you; Recent; Starred; Spaces; Filters; Accounts (administrators). Recent,
  Starred, Spaces and Filters are disclosure buttons (`aria-expanded`) over lists of links. "Spaces" is open by
  default. The current place has `aria-current="page"`. At 768 px and narrower, the sidebar is a modal panel
  opened by the sidebar button. Focus stays in the panel, and Escape closes it.
- **Space header** (every space page): the space name (`h1`), the template, a star button, tabs (`nav` "Space":
  Summary, Backlog, Board, List, Timeline, Stages, as the template has them) and a "More" menu button ("…",
  `aria-haspopup="menu"`): Members; Space settings; Deleted items.

## Create dialog

- Opened by **Create**. A modal dialog titled "Create". (Jira's one-key "c" shortcut is left out: WCAG 2.1.4 would
  need a way to turn it off.)
  Its fields, in order:
  - **Space** (select);
  - **Work type** (select, with icons and names);
  - **Summary** (required; focused when the dialog opens);
  - **Description** (formatted-text editor);
  - **Status**;
  - **Assignee** ("Assign to me" link);
  - **Priority**;
  - **Labels** (combobox);
  - **Parent** (searchable combobox: "Epic", "Stage" or "Parent" by type);
  - **Sprint** (Scrum);
  - **Start date** and **Due date** (one **Date** for milestones);
  - **Estimate** (agile stories, tasks and bugs).
- A field the chosen type does not have is not shown.
- Footer: a "Create another" checkbox, then "Cancel" and "Create".
- Errors appear beside their field and in an alert at the top, and the typed values stay.
- Closing with typed text asks "Discard this work item?" ("Discard" / "Keep editing").
- After creating: a status message "WEB-7 created" with a "View WEB-7" link, announced politely.

## Board

- **Cards**:
  - the summary (the drawer link) first;
  - the epic tag (its color, with "Epic: Card refunds" for screen readers) and the labels;
  - a footer with:
    - the type icon (and its name as text);
    - the key, with "Sub-task of WEB-2" for sub-tasks;
    - the priority, the estimate and the sub-task progress ("2 of 5 sub-tasks done");
    - the due date;
    - the assignee's avatar (their initials, with their name).
- **Group by** (a select: None, Epic or Stage, Assignee, Sub-tasks) beside the quick filters; query string
  `group`.
- **Lanes**: a header button per lane (`aria-expanded`), with the lane's name and its count, over the columns. "No
  epic", "No stage", "Unassigned" or "Everything else" comes last. Past 20 lanes, the rest start collapsed.
- **Moving between lanes**:
  - drag a card into another lane's column;
  - or use "Move to", which gains "Lane" (Epic, Stage or Assignee lanes) beside "Column" and "Position".
- **Columns** (Space admins, Admin):
  - "+" after the last column ("Add column") opens a small form: the name and the status type, with "Add" and
    "Cancel".
  - Each column header has a menu button ("Column actions for In Progress") with:
    - Rename;
    - Set limit or Clear limit;
    - Move left and Move right;
    - Change status type (only when the column is empty; otherwise disabled, with "Only empty columns can change
      type.");
    - Delete (opens the Phase 1 dialog, choosing where the cards go).

## Create space gallery

- **Create space** opens a dialog with two steps:
  1. **Choose a template**: a `radiogroup` of four cards, Kanban, Scrum, Stage-gate and Blank, each with a
     description. A preview beside the cards (below them at 360 px) shows the selected template's columns, work
     types and views. Then "Use template".
  2. **Space details**:
     - the name, the key (suggested), and the description;
     - for Blank also:
       - **Columns**: a list of rows, each with a name, a status type, "Move up", "Move down" and "Remove";
         "Add column" adds a row; the list starts with To do, In progress and Done;
       - **Work types**: checkboxes for Epic, Story, Task and Bug; Sub-task is always on.

     Then "Back" and "Create space".
- After creating: the new space's board.

## Summary page

- Four count cards for the last 7 days and the next 7 days:
  - "N completed in the last 7 days" (`/search?space=KEY&resolved=…`);
  - "N updated" (`updated=…`);
  - "N created" (`created=…`);
  - "N due soon" (`due=…`).
- **Status overview**: To do, In progress and Done, as a bar with numbers and a table, never color alone.
- **Recent activity**: the 20 latest changes in the space, each a sentence with who, what, which item and when,
  linking to the item.
- **Priority breakdown** and **Types of work**: counts with bars and links.
- **Team workload**: open items per assignee and "Unassigned", as counts and the share of all open items.
- **Epic progress** (or **Stage progress**): each with its color, the done-of-total count and a progress bar
  (`role="progressbar"` with its value), linking to its drawer.
- Each section is a `section` with a heading. Empty sections say so ("Nothing due soon.").

## Screen text

| Situation | Text |
|-----------|------|
| Created | "WEB-7 created" · "View WEB-7" |
| Discard | "Discard this work item?" · "Discard" · "Keep editing" |
| Sub-task without parent | "Choose the item this sub-task belongs to." |
| Parent gone | "The parent item no longer exists." |
| Right lost | "You can no longer add work to this space." |
| Type turned off | "This space no longer uses Bugs." |
| Work type in use | "Bugs are still used by 4 items in this space." |
| No standard type | "Choose at least one of Story, Task or Bug." |
| Sub-task lane move | "A sub-task follows its parent's epic." |
| Column type locked | "Only empty columns can change type." |
| Role names | "Space admin", "Member", "Viewer" |
