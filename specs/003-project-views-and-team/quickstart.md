# Quickstart & Validation Guide: Project Views and Team (Phase 2)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

How to upgrade a Phase 1 installation to Phase 2 and prove each user story works end to end. Setup,
secrets and running the app are exactly as in the Phase 1 guide
(`specs/002-kanban-project-core/quickstart.md`, steps 1–3); this guide starts from a running Phase 1 app.

## 1. Upgrade an existing Phase 1 database (SC-006)

Before upgrading, note one pilot project, its owner, one person who created tasks in it, one who only
commented, and one user who never worked in it.

```bash
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web
```

**Expected**: the owner sees the project as **Project Admin**, the creator and the commenter as
**Member**; the other user no longer sees it, and a direct link to one of its tasks shows "Not found".
An administrator still sees every project ("Administrator access"). The audit log has one `MemberAdded`
event per membership, with the source "Phase 2 upgrade".

## 2. Validate each user story manually

Use four accounts besides the administrator: **owen** (creates the project), **amina**, **bilal** and
**carla**. The scenario numbers refer to spec.md.

### US1: Control who sees and works in a project

1. As owen, create project "Website Revamp" (`WEB`): owen is its only member, as Project Admin
   (scenario 1). Open **Members** from the project header.
2. **Add member**: find amina by typing part of her name and add her as Member; add bilal as Viewer.
   The list shows all three with their roles (scenario 2).
3. As bilal: the board, list, timeline and drawers are visible but offer no way to change anything
   (no "What needs to be done?", no dragging, read-only drawer) (scenario 3).
4. As carla: `WEB` is not in her project list and `/projects/WEB/board?task=WEB-1` shows "Not found"
   (scenario 4).
5. With amina's board open, remove her as owen; her next action is refused and `WEB` leaves her list
   (scenario 5). Add her back as Member.
6. Make bilal a Member: without signing in again he can add a task (scenario 6).
7. As owen, try to remove yourself or change your own role: refused, "make someone else a Project Admin
   first" (scenario 7).
8. As the administrator (not a member), open `WEB`: full rights, including Members and settings
   (scenario 8).

### US2: Assign and schedule tasks, and find my work

1. In the drawer of `WEB-1`, choose amina as assignee; the card shows her initials and the history the
   change (scenario 1). As bilal, use **Assign to me** on an unassigned task (scenario 2).
2. Open the assignee choice: only active Project Admins and Members, plus "Unassigned" (scenario 3).
3. In the drawer of `WEB-3`, set start 1 October and due 10 October; then try a due date before the start
   date: refused, input kept (scenario 4).
4. Give `WEB-1` a due date of yesterday: "Overdue" on the card, in the list and on My tasks; move it to
   Done and the mark disappears (scenario 5).
5. As amina, turn on **Only my tasks**: only her cards remain and columns show "matching of total"
   (scenario 6).
6. Assign amina a task in a second project; her **My tasks** page lists her open tasks from both
   projects, soonest due first, without completed ones (scenario 7). Remove her from the second project:
   those tasks leave her My tasks and show her there as "no longer on the project" (scenario 8).

### US3: Browse a project as a list

1. Create 60 tasks (or use the seed tool). **List**: 50 rows with the total, newest key first; sub-tasks
   show their parent (scenario 1).
2. Sort by due date, then again to reverse it (scenario 2). Filter status type "in progress" and
   assignee "Me"; remove one chip; **Clear filters** (scenario 3).
3. Copy the address of a filtered, sorted list into bilal's browser: same list (scenario 4).
4. Open a row, change the priority in the drawer: the row follows (scenario 5). Add a task with "What
   needs to be done?": it appears in the first "to do" column (scenario 6). A filter matching nothing
   shows "Clear filters" (scenario 7).

### US4: Plan on a timeline

1. Date four of six tasks (one with a due date only). **Timeline**, months: four bars (the one-date task
   as a one-day bar), today line, two unscheduled tasks; switch to weeks and quarters (scenarios 1, 2).
2. Drag a bar two weeks later: both dates move 14 days, recorded as one change (the history lists the start
   and the due date with the same time) (scenario 3). Drag another bar's right end: only its due date
   changes; it cannot pass the start (scenario 4).
3. Tab to a bar, press Left, then Enter: one day earlier, saved once; Shift and Ctrl change one date;
   Escape cancels (scenario 5).
4. **Schedule** an unscheduled task: today to six days later (scenario 6). Expand a task with sub-tasks
   (scenario 7).
5. Change a task's dates in bilal's browser, then drag the same bar in amina's: she is told it changed
   and the bar shows the new dates (scenario 8). As a Viewer, bars do not move (scenario 9).

## 3. Run the automated tests

```bash
dotnet test --project tests/Upms.Domain.Tests
dotnet test --project tests/Upms.Application.Tests
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"   # Phase 2 matrix
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*MembershipUpgradeTests"  # SC-006
dotnet test --project tests/Upms.Web.Tests
dotnet test --project tests/Upms.Architecture.Tests
dotnet test --project tests/Upms.E2E.Tests                     # journeys + axe scans at 1280 px and 360 px
```

**Expected**: all green; Phase 2 acceptance tests are named `P2_US1_AS…` to `P2_US4_AS…`; zero WCAG 2.2 AA
violations on the new and changed screens (SC-009).

## 4. Quality gates (same as CI)

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
```

## 5. Performance check (SC-002)

As in Phase 1 (seeded database or container). The seed now creates teams, assignees and dates, and the
report adds list loads (sorted and filtered), timeline loads, "My tasks" loads, and saving an assignee,
dates and a membership change.

**Expected**: with 300 simulated concurrent users on about 500,000 work items, p95 ≤ 1 second for every
measured action.
