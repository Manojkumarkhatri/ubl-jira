# Contract: UI Routes and Screens (Phase 4)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

Phase 1–3 routes (`specs/002-…`, `specs/003-…`, `specs/004-…/contracts/ui-routes.md`) stay; this contract lists the
new and changed ones. Every screen meets WCAG 2.2 AA, works with a keyboard alone and stays usable at 360 px
(FR-056). A non-member opening any project route sees "Not found".

| Route | Screen | Who | Requirements |
|-------|--------|-----|--------------|
| Every page: **header** | **Changed**: a **search box** (combobox "Search or type a key"; empty and focused, it lists up to 10 recently viewed items; a key plus Enter opens the item or says "No work item KEY was found"; words plus Enter open `/search?q=…`) and a **notification bell** (button "Notifications, N unread"; opens a panel: newest first, 20 at a time, "Show more", "Mark all as read", each entry with who, what, which item and when, a read/unread toggle, and a link that marks it read and opens the item's drawer; Escape closes the panel and returns focus to the bell; the count refreshes every 30 seconds) | signed in | FR-018–FR-020, FR-032, FR-033 |
| `/search` | **New**: words and filters (project, type, status type, priority, assignee with "Me" and "Unassigned", creator, label, and created, updated and due date ranges), each active filter as a removable chip; sort (last update, created, key, priority, due date); the result count; results 50 at a time with key, type, title, project, status, assignee, labels and last update; `?task={KEY-N}` opens the drawer over the results; **Saved filters** beside the results: "My filters" and "Shared with me" (with the owner's name), and for the current search "Save as filter"; for one's own filter "Rename", "Update with this search", "Share" (choose projects one is a member of) and "Delete". Query string: `q`, `project`, `kind`, `category`, `priority`, `assignee` (`me`, `none` or ids), `creator`, `label`, `created`, `updated` and `due` (`from..to`), `sort`, `desc`, `page`, `filter` (a saved filter's id) | signed in; results only from projects the caller can see | FR-034–FR-041 |
| `/projects/{key}/board` | **Changed**: cards show their labels; **quick filters** above the columns: label (choose one or more), type, epic (stage on Stage-gate boards), as toggle buttons with `aria-pressed`, beside "Only my tasks" and the assignee filter; columns show "matching of total"; query string adds `labels`, `kinds`, `containers` and `assignee` | members, Admin | FR-045, FR-047 |
| `/projects/{key}/list` | **Changed**: a "Labels" column and a label filter (items with any of the chosen labels); query string adds `labels` | members, Admin | FR-045, FR-046 |
| `…?task={KEY-N}` | **Changed drawer**: the **description** and each **comment** use the formatted-text editor (toolbar, shortcuts, "@" suggestions, tables, pasted images) and show formatted text read-only for Viewers; a **Watch** toggle ("Watch" or "Stop watching") with the watcher count, opening the list of watchers (Project Admins: "Add watcher" and "Remove"); **Attachments**: "Attach files" (file input), a drop zone on the drawer, and a list of files with name, size, uploader, time and status ("Being checked", "Blocked", or a preview for images and "Download"), with "Remove" where allowed (administrators also "Delete permanently", with a confirmation); **Labels**: chips with "Remove label X" and an "Add label" combobox with suggestions (contributors) | members, Admin | FR-001–FR-013, FR-023–FR-031, FR-042–FR-045 |
| `/account/profile` | **Changed**: an **Email notifications** radio group: "All notifications", "Only notifications aimed at me" (default), "None" | signed in | FR-049, FR-051 |

## The formatted-text editor (shared component `RichTextEditor`)

- The editable area is a labelled `textbox` (`aria-multiline`). The toolbar is one tab stop with arrow keys
  between buttons:
  - Bold, Italic, Strikethrough, Code, Link;
  - Heading 1–3, Bulleted list, Numbered list, Quote, Code block;
  - Table, with a table menu: add a row above or below, add a column left or right, delete a row or column,
    delete the table, header row on or off.
- Keyboard and typed shortcuts are those of research R5. Escape then Tab always leaves the editor.
- "@" opens a suggestion `listbox` (arrow keys, Enter to choose, Escape to close). Chosen mentions show the
  person's name.
- Pasted or dropped images upload as attachments and appear in the text once checked, with a "being checked"
  placeholder until then. Pasted tables keep their rows and columns. Other pasted content keeps only supported
  formatting.
- "Save" and "Cancel" as before. A refused save shows the reason and keeps the text. The 32,000-character limit
  shows a count near the limit.

## Screen text

| Situation | Text |
|-----------|------|
| Notification (mention) | "Amina Khan mentioned you on WEB-1 Plan the launch" |
| Notification (assignment) | "Amina Khan assigned WEB-4 Book the venue to you" |
| Notification (comment / status) | "Amina Khan commented on WEB-1 …" / "Amina Khan moved WEB-1 … to Done" |
| Notification (gate) | "Sara Malik requested approval of the Initiation gate in CBU" / "The Initiation gate in CBU was approved" or "… was sent back" |
| Notification (blocked upload) | "report.pdf on WEB-1 was blocked by the malware check" |
| Key not found | "No work item MOB-99 was found." |
| File refused | "Files can be up to 25 MB." / "This type of file can't be attached. Allowed: images, PDF, Word, Excel, PowerPoint, text, log, CSV, JSON, XML, YAML, Markdown, SQL and Jupyter notebooks." / "This file's content doesn't match its type." |
| Label refused | "A label can't contain spaces or commas and can have up to 50 characters." / "An item can have up to 20 labels." |
| Filter name taken | "You already have a filter called Unassigned refunds." |
