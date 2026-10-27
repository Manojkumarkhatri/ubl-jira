# Quickstart & Validation Guide: Jira Parity Pass

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

This guide shows how to upgrade a Phase 4 installation and validate the Jira parity pass by hand and with the
automated suites. It assumes the Phase 1–4 setup (`specs/002-…` to `specs/005-…/quickstart.md`). Sign-in through
LDAP and email through the company's SMTP server stay for UAT; in development the app signs in as
`DevSignIn:UserName`.

## 1. Upgrade an existing Phase 4 database

```powershell
docker compose up -d sqlserver
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web
```

**Expected**:
- The three migrations are applied: `WorkTypesAndActivityIndex`, `SpaceNavigation` and `EpicColors`.
- Every existing Kanban and Scrum space has Epic, Story, Task and Bug turned on.
- Every existing epic has a color.
- No work item's history changed (history counts per item are the same before and after).

## 2. Validate each user story by hand

Use the Phase 3–4 accounts: **sara** (Space admin of `WEB`, `MOB` and `CBU`), **amina** and **omar** (Members),
**bilal** (Viewer). The scenario numbers refer to spec.md.

### US1: Create any work item from anywhere

1. As amina, on "For you", press **Create**. The dialog opens on `WEB` (the last space amina created in), and the
   focus is in Summary (scenario 1).
2. Choose "Epic", type "Card refunds" and create. "WEB-n created" appears with a link (scenario 6).
3. Create a Story "Refund to card" with parent "Card refunds", assigned to omar, priority High, label `payments`,
   and "Create another" ticked. The dialog stays open with Story and the same parent (scenarios 4, 7).
4. Choose "Sub-task" without a parent: "Choose the item this sub-task belongs to." Choose the story, then create
   (scenario 3).
5. In `MOB` (Scrum), create a Task in the planned sprint: it appears in that sprint on the backlog (scenario 5).
6. As bilal (Viewer everywhere): no **Create** button (scenario 8).
7. Type a summary and press Escape: "Discard this work item?" (scenario 9).
8. Open the story's history: one change records every value set at creation.

### US2: Change the board's columns on the board

1. As sara, on the `WEB` board, press "+", add "Review" (In progress); it appears last (scenario 1).
2. From its menu: Rename to "Code review", Set limit 3, Move left (scenario 2).
3. Move two cards into it, then try "Change status type": disabled, "Only empty columns can change type."
   (scenario 3).
4. Delete it, moving its cards to "In Progress" (scenario 4).
5. As amina: no "+" and no column menus (scenario 5).
6. Have sara rename a column in one browser while another browser renames it from the board: the second gets the
   conflict message and the current columns (scenario 6).

### US3: Create a space from a template gallery

1. On Spaces, press **Create space**; select each template card and read its preview (scenarios 1, 2).
2. Choose **Blank**, name it "Data Platform" (key `DATA`). Rename "In progress" to "Building", add "Validating"
   (In progress), choose Epic, Task and Bug, then create (scenarios 3, 4).
3. The `DATA` board shows To do, Building, Validating, Done. **Create** offers Epic, Task, Bug and Sub-task in
   `DATA` (scenario 5).
4. In `DATA`'s Space settings, turn off Bug after creating one bug: "Bugs are still used by 1 items in this
   space."; delete the bug as an administrator and try again; it is accepted (scenario 6).
5. As bilal, `DATA` cannot be found (scenario 7).

### US4: Find your way like in Jira

1. Every page shows the sidebar: For you, Recent, Starred, Spaces, Filters (and Accounts for administrators)
   (scenario 3).
2. Star `WEB` on Spaces: it appears under Starred and first under Spaces (scenario 6).
3. Collapse the sidebar, reload: it stays collapsed. At 360 px it opens as a panel from the header (scenario 4).
4. Open `WEB`: tabs Summary, Board, List, Timeline; "…" holds Members, Space settings, Deleted items
   (scenario 5).
5. Open Summary: the 7-day counts, status overview, recent activity, priorities, types of work, team workload and
   epic progress; "N updated" opens the matching search (scenario 7).
6. Open "For you": Worked on, Viewed, Assigned to me, Starred (scenario 8).
7. Open an old bookmark `/projects/WEB/board?task=WEB-1`: the address becomes `/spaces/WEB/board?task=WEB-1`, and
   the drawer opens (scenario 2).
8. No page says "project" (scenario 1).

### US5: Read the board like in Jira

1. On the `MOB` board, cards show their epic tag in its color, the avatar, priority, estimate, labels and sub-task
   progress; sub-tasks are cards with "Sub-task of MOB-n" (scenarios 1, 6).
2. Change "Card refunds"'s color in its drawer: every card, list row and timeline bar of its items follows
   (scenario 2).
3. Group by Epic, then Assignee, then Sub-tasks, then None; the address keeps `group` (scenario 3).
4. Group by Epic and drag a card into another epic's lane: its epic changes, and its history says so. Try with a
   sub-task: "A sub-task follows its parent's epic." (scenario 5).
5. Group by Assignee and use "Move to" › Lane with the keyboard to reassign a card (scenario 5).

## 3. Automated validation

```powershell
dotnet test --project tests/Upms.Domain.Tests
dotnet test --project tests/Upms.Architecture.Tests          # includes WordingTests
dotnet test --project tests/Upms.Web.Tests
dotnet test --project tests/Upms.Application.Tests -- --filter-not-trait "Category=ExternalServices"
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"
dotnet test --project tests/Upms.E2E.Tests                    # JP_US1–JP_US5 and every Phase 1–4 journey
dotnet format --verify-no-changes
dotnet build -c Release
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
```

**Expected**: every suite passes; Phase 1–4 tests changed only in wording (SC-005); `PermissionMatrixTests` reads
this feature's matrix; axe reports no violations on the new screens at both widths (SC-007).

## 4. Performance (opt-in)

```powershell
dotnet test --project tests/Upms.Performance.Tests   # seeds 500,000 work items in its own container
```

**Expected**: p95 ≤ 1 s at 300 users for the Create form's options, the summary, the four "For you" tabs, the
sidebar, the board with sub-task cards, and a lane move (SC-006), and for every Phase 1–4 action.
