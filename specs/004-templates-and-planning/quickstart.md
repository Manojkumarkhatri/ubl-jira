# Quickstart & Validation Guide: Templates and Planning (Phase 3)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Contracts**: [contracts/](./contracts/)

How to upgrade a Phase 2 installation to Phase 3 and prove each user story works end to end. Setup, secrets
and running the app are as in the Phase 1 guide (`specs/002-kanban-project-core/quickstart.md`, steps 1–3);
this guide starts from a running Phase 2 app.

## 1. Upgrade an existing Phase 2 database (SC-010)

Before upgrading, note one project (`WEB`), a few of its tasks with sub-tasks, assignees and dates, its
columns and its team.

```bash
dotnet ef database update --project src/Upms.Infrastructure --startup-project src/Upms.Web
dotnet run --project src/Upms.Web
```

**Expected**: the project list shows `WEB` as **Kanban**. Its board, columns, tasks (type Task), sub-tasks,
assignees, dates, members and history are unchanged, and its header shows Board · List · Timeline.

## 2. Validate each user story manually

Use these accounts besides the administrator: **sara** (creates the projects), **amina** and **omar**
(Members), **bilal** (Viewer) and **farah** (not a member at first). The scenario numbers refer to
spec.md.

### US1: Choose a template and organize work by type

1. As sara, create "Mobile App" (`MOB`) with **Scrum** and "Core Banking Upgrade" (`CBU`) with
   **Stage-gate**; the form offers the three templates with Kanban preselected (scenario 1). `MOB` has To
   Do, In Progress, In Review, Done; `CBU` has Not Started, In Progress, On Hold, Completed and, under
   **Stages**, Initiation, Planning, Execution and Closure (scenario 2). Add amina and omar as Members and
   bilal as Viewer to both.
2. Check `WEB` as in step 1 (scenario 3).
3. In the `MOB` list, type "Card refunds", choose **Epic** and press Enter: `MOB-1` appears in the list,
   not on the board (scenario 4).
4. Create a story and a bug; put both in the epic from their drawers; move the story to Done: the epic's
   drawer shows "1 of 2 done (50%)" (scenario 5). Change the bug to a Story; the history records it; an
   epic's drawer offers no type change (scenario 6).
5. In `CBU`, create milestone "Planning sign-off" in stage Planning, dated 30 October: no start date, no
   sub-tasks, not on the board (scenario 7).
6. In the `MOB` list, filter type Bug and epic "Card refunds"; reload the address: same filters (scenario 8).
7. In `WEB`'s settings, switch to **Scrum**: the header gains Backlog, and the backlog lists its open tasks
   column by column (the Backlog view arrives with US2; before it, check the board is unchanged); Stage-gate
   is never offered (scenario 9). Switch back to Kanban.
8. Delete the epic: its two items stay, with no epic, each with a history entry (scenario 10).

### US2: Plan and run sprints

1. Seed or create 15 estimated items in `MOB`. **Backlog**: sprints first, then the backlog in order;
   epics and sub-tasks are not listed on their own (scenario 1). Move one item to the top by dragging and
   another with **Move → Top**; the board shows the same order in its columns once they are in a sprint
   (scenario 2).
2. As sara, **Create sprint**: "MOB Sprint 1". As amina, move 5 items in by dragging and 1 with **Move → to
   MOB Sprint 1** (scenario 3); a story with two sub-tasks brings them along (scenario 4).
3. As sara, **Start** it on 5 October with a goal: the end date proposes 19 October; the board now shows only
   those 6 cards (scenario 5). Try to start another sprint: refused (scenario 6). As amina, try to start or
   complete a sprint: refused (scenario 10).
4. Mark 4 cards Done; add 1 more card during the sprint. As sara, **Complete sprint**, sending the open
   ones to the backlog (scenario 7). Open its report: 6 committed, 1 added, 4 completed, 3 not completed,
   with totals (scenario 8). The board now says there is no active sprint (scenario 9). As bilal, the
   backlog and report are read-only (scenario 10).

### US3: Pass stage gates

1. In `CBU` **Stages**, as sara, **Set up gate** on Initiation: search for farah (not a member) and add
   omar; keep "all approvals" and the requester exclusion. farah becomes a Viewer (the audit log shows
   `MemberAdded`); Planning, Execution and Closure show "Locked" (scenario 1).
2. As amina, try to move a Planning task to In Progress on the board and in the drawer: refused, naming the
   Initiation gate; editing, assigning and scheduling it still work (scenario 2).
3. Leave one Initiation task open. As amina, **Request approval**: a warning lists the open task; confirm.
   farah's and omar's **My tasks** show the gate under "Gates waiting for you" (scenario 3).
4. As farah, **Approve**: the gate still waits for omar. As omar, **Send back** without a note (asked for
   one), then with a note: "Sent back" with the note (scenarios 4, 5). As sara (not an approver) or the
   administrator, there is no way to decide, and a request by other means is refused (scenario 6).
5. As amina, request again; as sara, try to change the approvers: told to withdraw first; withdraw, and
   the earlier decisions stay in the history (scenario 8). Request again; farah and omar approve: the gate
   is approved and Planning unlocks; its task moves to In Progress (scenario 4). Initiation can now move to
   Completed; before approval it could not (scenario 7). The approved gate offers no withdrawal or changes
   (scenario 9).
6. As the administrator, read Initiation's history and the audit log: every setup change, request,
   withdrawal, decision with note, and the approval (scenario 10).
7. On Planning's gate, name omar and farah, keep "all" and the exclusion: omar's request is refused; set
   one approval needed: omar can request and only farah can decide (scenario 11).

### US4: Plan on the timeline

1. In `CBU`, schedule the stages, milestone "Planning sign-off" and six tasks; make one task wait for
   another from its drawer (scenario 5). **Timeline**: stages in order as bars with progress, expandable;
   gate markers at their due dates with their state; the milestone as a marker; an arrow between the two
   tasks (scenarios 1, 3).
2. Move the milestone two days later by dragging, then with the arrow keys and Enter (scenario 2).
   **Schedule** an unscheduled epic in `MOB`: today to 27 days later (scenario 4).
3. Drag the end of the task waited for past the start of the waiting task: "Late" on the timeline and in
   both drawers, and no other date changed; complete it: the flag clears (scenario 6).
4. Try to make the first task wait for the second: refused as a loop (scenario 7). As bilal, nothing moves
   and no dependency can be added (scenario 9).
5. In `MOB`, the sprint lane shows the completed and the active sprint (scenario 8).
6. Delete a task that has dependencies: its links disappear, and the linked items' history says so
   (scenario 10).

## 3. Run the automated tests

```bash
dotnet test --project tests/Upms.Domain.Tests
dotnet test --project tests/Upms.Application.Tests
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests"   # Phase 3 matrix
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*TemplatesUpgradeTests"   # SC-010
dotnet test --project tests/Upms.Web.Tests
dotnet test --project tests/Upms.Architecture.Tests
dotnet test --project tests/Upms.E2E.Tests                     # journeys + axe scans at 1280 px and 360 px
```

**Expected**: all green; Phase 3 acceptance tests are named `P3_US1_AS…` to `P3_US4_AS…`; zero WCAG 2.2 AA
violations on the new and changed screens (SC-011).

## 4. Quality gates (same as CI)

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet ef migrations has-pending-model-changes --project src/Upms.Infrastructure --startup-project src/Upms.Web
```

## 5. Performance check (SC-002)

As in Phases 1 and 2 (seeded database or container). The seed now creates Scrum projects with epics,
estimates and sprints, and Stage-gate projects with stages, gates, milestones and dependencies. The report
adds backlog loads, backlog reorders and sprint moves, sprint report and Stages view loads, gate requests
and decisions, dependency changes, and timeline loads with containers and dependencies.

**Expected**: with 300 simulated concurrent users on about 500,000 work items, p95 ≤ 1 second for every
measured action.

## 6. Usability measures (pilot)

The pilot plan (`docs/pilot/phase1-pilot-plan.md`) gains the Phase 3 measures: a sprint of 20 items created,
filled and started in under 10 minutes (SC-001); a Stage-gate plan of 4 stages, 30 tasks, 4 milestones and 10
dependencies laid out in under 45 minutes (SC-003); approvers deciding a waiting gate within 1 minute of
signing in (SC-004); and a Scrum team and a PMO project running in U-PMS for 4 weeks, with at least 80% rating
the new screens easy (SC-012).
