# Feature Specification: Templates and Planning (Phase 3)

**Feature Branch**: `phase-3` (spec folder `specs/004-templates-and-planning`)

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "Start Phase 3: portfolio rollups, cross-project dashboards, Scrum and PMO
stage-gate templates, dependencies, milestones and other work item types. Ask me about scope before
deciding." The scope questions of 2026-09-28 (see Clarifications) split that list. This spec covers the
templates (Kanban, Scrum and Stage-gate), the new work item types, milestones and dependencies. Portfolios,
portfolio rollups and dashboards move to Phase 4. This spec builds on Phase 1
(`specs/002-kanban-project-core`) and Phase 2 (`specs/003-project-views-and-team`); their behavior continues
unless a requirement below changes it.

## Clarifications

### Session 2026-09-28

- Q: Phase 3 as listed is about seven user stories. How should it be delivered? → A: Split it, templates
  first. Phase 3 covers the Scrum and Stage-gate templates, the new work item types, milestones and
  dependencies. Phase 4 covers portfolios, rollups and dashboards, which can then report on real stages,
  milestones and sprints.
- Q: What should a PMO stage gate do? → A: No preference, so the recommended option applies. Each stage
  ends in a gate, and named approvers approve it or send it back with a note. The note is recorded in
  the history and the audit log. The next stage cannot start until the gate is approved.
- Q: Where can dependencies link work items? → A: Only within one project, as the backlog planned.
- Q: Which cross-project views should the dashboard work include? → A: One dashboard per project and one
  per portfolio, with health updates, as in the backlog. This belongs to Phase 4 and is recorded here so
  that Phase 4 starts from it.
- Q: When a gate has several approvers, what makes it approved? → A: The same as Jira's approvals. Each
  gate sets how many of its approvers must approve, from one to all of them; the default is all. A single
  "send back" ends the request at once.
- Q: Who can be named as a gate approver? → A: Any active user in the organization, as with Jira's
  approver field. Naming someone who is not a member of the project also adds them as a Viewer, and the
  audit log records it.
- Q: Can the person who requests approval of a gate also approve it? → A: Do what Jira does. Jira Cloud's
  approval step has an "Exclude approvers from" setting (for example, the reporter), so each gate has a
  setting that excludes the requester from approving. It is on by default, and Project Admins can turn
  it off.
- Q: Who can create, start, complete and delete sprints? → A: Project Admins (and administrators), as
  with Jira's "Manage Sprints" permission. Members still rank the backlog and move items between the
  backlog and sprints.
- Q: Should Kanban and Scrum projects get Jira-style releases (versions with a release date) in Phase 3?
  → A: No, in a later phase. Phase 3 keeps its four stories, and agile teams use epics until then.

## User Scenarios & Testing *(mandatory)*

Terms used in this spec:

- **Work item**: anything tracked in a project. Phases 1 and 2 had only tasks and sub-tasks; Phase 3 adds
  more types.
- **Template**: how a project works. **Kanban** is a continuous flow on a board, as in Phases 1 and 2.
  **Scrum** adds a backlog and sprints. **Stage-gate** suits PMO projects: work moves through stages, and
  an approval gate separates each stage from the next. Kanban and Scrum projects are **agile projects**.
- **Container**: an **Epic** (agile projects) or a **Stage** (Stage-gate projects) that groups related
  work. **Standard items** are Stories, Tasks and Bugs (agile projects) or Tasks and Milestones
  (Stage-gate projects). **Sub-tasks** belong to a standard item, as in Phase 1.
- **Column type**, **open** and **contributor**: as in Phases 1 and 2. A column's type is "to do", "in
  progress" or "done". An item is open while its column's type is not "done". Contributors are a
  project's Project Admins and Members, and the organization's administrators.
- **Gate**: the approval at the end of a stage. Its **approvers** are the people named to approve it.
- **Current stage**: the first stage, in stage order, whose gate is not yet approved. A stage after the
  current stage is **locked**: its work can be planned but not started.
- **Dependency**: "B waits for A" means B should not start until A is finished (finish-to-start).

### User Story 1 - Choose a template and organize work by type (Priority: P1)

A person creating a project now chooses how the team works: Kanban (as today), Scrum for teams that plan
in sprints, or Stage-gate for PMO projects that move through approved stages. The project starts with
columns that fit its template. Agile teams record epics, stories, tasks and bugs. PMO teams record stages,
tasks and milestones. Epics and stages group related work and show how much of it is done. Every existing
project carries on as a Kanban project, exactly as before.

**Why this priority**: Every other Phase 3 story builds on templates and types. On their own, they let
agile teams tell bugs from stories and group work into epics. They also let the PMO lay out a project by
stage, with milestones.

**Independent Test**: Create the Scrum project `MOB` and the Stage-gate project `CBU`. `MOB` starts with To
Do, In Progress, In Review and Done. `CBU` starts with Not Started, In Progress, On Hold and Completed, and
has the stages Initiation, Planning, Execution and Closure. In `MOB`, create the epic "Card refunds" with a
story and a bug in it. Change the bug into a story, then complete one of the two: the epic shows "1 of 2
done (50%)". In `CBU`, add a task and the milestone "Planning sign-off" to Planning. The existing project
`WEB` is a Kanban project, with its tasks, columns and team unchanged.

**Acceptance Scenarios**:

1. **Given** a signed-in user creating a project, **When** the form opens, **Then** they choose Kanban,
   Scrum or Stage-gate, each with a one-line description, and Kanban is preselected.
2. **Given** a user creates `MOB` with the Scrum template, **When** its board opens, **Then** its columns
   are To Do, In Progress, In Review and Done; **and given** a user creates `CBU` with the Stage-gate
   template, **Then** its columns are Not Started, In Progress, On Hold and Completed, and it has four
   stages in this order: Initiation, Planning, Execution and Closure.
3. **Given** `WEB` was created before Phase 3, **When** Phase 3 is installed, **Then** `WEB` is a Kanban
   project. Its tasks are of type Task and its sub-tasks of type Sub-task, and its columns, keys, members,
   assignees, dates and history are unchanged.
4. **Given** the `MOB` list, **When** a Member types "Card refunds", chooses Epic and presses Enter,
   **Then** the epic `MOB-1` is created. It appears in the list and on the timeline, but not as a card on
   the board.
5. **Given** the story `MOB-2` and the bug `MOB-3`, **When** a Member puts both in epic `MOB-1` from their
   drawers and moves `MOB-2` to Done, **Then** the epic's drawer lists both items and shows "1 of 2 done
   (50%)".
6. **Given** the bug `MOB-3`, **When** a Member changes its type to Story, **Then** the change is
   confirmed and recorded in its history; **and** the drawers of epics, stages, milestones and sub-tasks
   offer no type change.
7. **Given** `CBU`, **When** a Member creates the milestone "Planning sign-off" in the stage Planning,
   dated 30 October, **Then** it has that single date and no start date, it cannot get sub-tasks, and it
   is not shown as a card on the board.
8. **Given** the `MOB` list, **When** a member filters by type Bug and epic "Card refunds", **Then** only
   matching items are listed, each row shows its type and epic, and the filters are kept in the page's
   address.
9. **Given** the Kanban project `WEB`, **When** its Project Admin switches it to Scrum, **Then** its open
   stories, tasks and bugs appear in the backlog in board order: column by column from left to right,
   keeping their order within each column; **and** no project offers a switch to or from Stage-gate.
10. **Given** epic `MOB-1` holds two items, **When** its creator deletes it after confirming, **Then**
    both items remain, no longer in an epic, and each item's history records the change.

---

### User Story 2 - Plan and run sprints (Priority: P2)

A Scrum team keeps one ordered backlog of estimated stories, tasks and bugs. At planning time the team
moves the top items into a sprint, and the Project Admin (for example the Scrum Master) creates it and
starts it with dates and a goal. The board then holds only the sprint's work. At the end the Project
Admin completes the sprint: unfinished work goes back to the backlog or into the next sprint. The sprint
report shows what was committed, added, removed and completed.

**Why this priority**: Many IT teams plan in time-boxes. Sprints are the capability those teams ask for
first after the board, and they build on the types and estimates of story 1.

**Independent Test**: In `MOB`, with 15 estimated items in the backlog, move one to the top and create
"MOB Sprint 1". Move 5 items into it with the mouse and 1 with the keyboard, then start it for two weeks.
The board shows only those 6 items. Mark 4 of them Done and add 1 more item during the sprint. Complete
the sprint, sending the 3 open items back to the backlog. The report shows 6 committed, 1 added, 4
completed and 3 not completed, each with its estimate total.

**Acceptance Scenarios**:

1. **Given** a Scrum project, **When** a member opens its Backlog, **Then** they see the active and
   planned sprints, each with its items, item count and estimate total. Below them is the backlog: every
   open story, task and bug that is in no sprint, in priority order. Epics and sub-tasks are not listed
   on their own.
2. **Given** the backlog, **When** a Member moves an item to the top by dragging it or with "Move to top",
   **Then** everyone sees the new order, and the board uses the same order within its columns.
3. **Given** `MOB` has no sprints yet, **When** a Project Admin creates a sprint, **Then** it is named "MOB
   Sprint 1", and Members can move items between the backlog and the sprint by dragging or with "Move
   to".
4. **Given** a story with two sub-tasks, **When** it is moved into a sprint, **Then** its sub-tasks move
   with it; **and** a sub-task cannot be moved on its own.
5. **Given** a planned sprint, **When** a Project Admin starts it with a start date of 5 October and a goal,
   **Then** the end date defaults to 19 October, the sprint becomes active, and the board shows only its
   items.
6. **Given** a sprint is active, **When** a Project Admin tries to start another sprint in the project,
   **Then** they are told that only one sprint can be active at a time, and nothing changes.
7. **Given** the active sprint has 4 items done and 3 open, **When** a Project Admin completes it and
   chooses the backlog for the open items, **Then** the 3 items return to the backlog, the 4 stay in the
   completed sprint, and the completed sprint can no longer change.
8. **Given** a completed sprint, **When** a member opens its report, **Then** it lists the items, with
   their estimate totals, that were committed at the start, added during the sprint, removed during the
   sprint, completed, and not completed.
9. **Given** a Scrum project with no active sprint, **When** a member opens its board, **Then** the board
   says there is no active sprint and links to the backlog.
10. **Given** a Viewer of `MOB`, **When** they open the backlog or a sprint report, **Then** they can read
    everything but cannot reorder the backlog, move items or manage sprints; **and given** a Member,
    **Then** they can reorder the backlog and move items into and out of sprints, but any attempt to
    create, rename, start, complete or delete a sprint is refused.

---

### User Story 3 - Pass stage gates (Priority: P3)

A PMO project moves through its stages one gate at a time. The Project Admin names each gate's approvers
from anyone in the organization, for example the sponsor and the PMO lead. Work in any stage can be
planned at any time, but it can start only once the previous gate is approved. When a stage's work is
done, a team member requests approval.
Each approver finds the request on their "My tasks" page and approves it or sends it back with a note.
When enough approvers have approved (all of them, unless the Project Admin set a smaller number), the
next stage opens. Every step is recorded.

**Why this priority**: Formal approval between stages is what PMO governance adds to task tracking, and
it replaces sign-off by email. It builds on the stages of story 1.

**Independent Test**: In `CBU`, name farah (the sponsor, from outside the project) and omar (a Member) as
approvers of the Initiation gate; farah becomes a Viewer. A task in Planning cannot be moved to In Progress.
Request approval of the Initiation gate; farah approves it and omar sends it back with a note. Request
approval again, and both approve. Planning is now unlocked and its task can start, the Initiation stage can
be completed, and the stage's history and the audit log show each step.

**Acceptance Scenarios**:

1. **Given** the Stages view of `CBU`, **When** its Project Admin sara names farah (the sponsor, not yet a
   member) and omar (a Member) as approvers of the Initiation gate, **Then** both are listed as its
   approvers, farah becomes a Viewer of `CBU`, and the audit log records that. The view shows Initiation
   as the current stage, and Planning, Execution and Closure as locked.
2. **Given** Planning is locked, **When** amina, a Member, tries to move a task in Planning to In
   Progress, on the board or in the drawer, **Then** it is refused with a message that the Initiation
   gate must be approved first; **and** amina can still edit, assign and schedule that task.
3. **Given** Initiation still has one open task, **When** amina requests approval of its gate, **Then**
   amina is warned and shown the open task. After confirming, the gate awaits a decision, and it appears
   on farah's and omar's "My tasks" pages as waiting for them.
4. **Given** the gate awaits a decision and needs both approvals (the default), **When** farah approves
   it, **Then** it still waits for omar; **and When** omar approves it too, **Then** the gate is
   approved, Planning is unlocked, and its task can be moved to In Progress. **Given** instead that sara
   had set the gate to need one approval, **Then** farah's approval alone approves it.
5. **Given** the gate awaits a decision, **When** omar sends it back, **Then** a note is required; with
   the note, the gate shows "Sent back" and the note, and amina can request approval again.
6. **Given** the gate awaits a decision, **When** sara, who is a Project Admin but not an approver, or an
   administrator tries to approve it, **Then** it is refused.
7. **Given** Initiation's gate is not approved, **When** anyone tries to move the Initiation stage to
   Completed, **Then** it is refused; **and** once the gate is approved, the move is allowed.
8. **Given** the gate awaits a decision, **When** sara tries to change its approvers, **Then** sara is
   told to withdraw the request first. After the request is withdrawn, the decisions recorded so far are
   cleared (they stay in the history), and the approvers can be changed.
9. **Given** an approved gate, **When** anyone tries to withdraw the approval or change its approvers,
   **Then** it is refused.
10. **Given** the steps above, **When** an administrator reviews the stage's history and the audit log,
    **Then** each change of approvers, request, withdrawal and decision (with its note), and the
    approval itself, is listed with who and when.
11. **Given** the Initiation gate has the approvers omar and farah, needs both approvals, and excludes the
    requester (the defaults), **When** omar requests its approval, **Then** the request is refused,
    because the gate could not get two approvals without omar; **and When** sara lowers the number
    needed to one, **Then** omar can request approval, and only farah can decide.

---

### User Story 4 - Plan with epics, stages, milestones and dependencies on the timeline (Priority: P4)

Project leads see the whole plan on the timeline. Epics or stages are bars with their progress and their
items beneath them. Milestones are markers, and in Stage-gate projects each gate shows at the end of its
stage. People record that one piece of work waits for another. The timeline draws these dependencies as
arrows and flags any that the dates break. People then decide what to move; nothing is rescheduled
automatically. Scrum projects also see their sprints in a lane.

**Why this priority**: It turns the Phase 2 timeline into a plan that PMO and agile leads can present and
steer by. It comes last because it shows the containers, milestones, gates and sprints that stories 1–3
create.

**Independent Test**: In `CBU`, schedule the four stages, a milestone and six tasks, and make `CBU-12`
wait for `CBU-11`. The timeline shows the stages with their progress and gate markers, the milestone as a
marker, and an arrow from `CBU-11` to `CBU-12`. Drag the end of `CBU-11` past the start of `CBU-12`: the
dependency is flagged late on the timeline and in both drawers, and no other date changes. Move the
milestone two days later with the keyboard.

**Acceptance Scenarios**:

1. **Given** a project with dated epics or stages, **When** its timeline opens, **Then** each container
   is a bar with its progress and can be expanded to show its items beneath it. Items in no container
   follow the containers. In Stage-gate projects, stages are shown in stage order.
2. **Given** the milestone "Planning sign-off" on 30 October, **When** the timeline shows, **Then** it is
   a marker on that date with its key and title; **and When** a contributor moves it two days later by
   dragging it, or with the arrow keys and Enter, **Then** its date becomes 1 November and its history
   records the change. A milestone cannot be resized.
3. **Given** the stages of `CBU` are scheduled, **When** the timeline shows, **Then** each stage's gate
   appears as a marker on the stage's due date, labelled with the gate's status (for example "Gate
   approved").
4. **Given** an epic without dates, **When** a contributor chooses "Schedule", **Then** it gets a start
   date of today and a due date 27 days later (four weeks).
5. **Given** the tasks `CBU-11` and `CBU-12`, **When** a contributor records that `CBU-12` waits for
   `CBU-11`, from a drawer or from the menu of `CBU-12`'s bar, **Then** both drawers list the dependency,
   both histories record it, and the timeline draws an arrow from the end of `CBU-11` to the start of
   `CBU-12`.
6. **Given** `CBU-12` starts on 10 November, **When** the due date of `CBU-11` is moved to 12 November,
   **Then** the dependency is flagged late, with a label, on the timeline and in both drawers, and no
   date changes automatically; **and When** `CBU-11` is completed, or its due date is moved back to 10
   November or earlier, **Then** the flag clears.
7. **Given** `CBU-12` waits for `CBU-11`, **When** someone tries to make `CBU-11` wait for `CBU-12`,
   directly or through other items, **Then** it is refused with an explanation that it would create a
   loop.
8. **Given** the Scrum project `MOB` has a completed sprint and an active sprint, **When** its timeline
   opens, **Then** a sprint lane shows both sprints from their start dates to their end dates.
9. **Given** a Viewer, **When** they open the timeline, **Then** they see containers, milestones, gates,
   sprints and dependencies, but cannot move anything or add or remove dependencies.
10. **Given** `CBU-11` has dependencies, **When** it is deleted, **Then** its dependencies are removed,
    and each linked item's history records the removal.

---

### Edge Cases

- **Types that cannot change**: an item can switch only between Story, Task and Bug, and only in agile
  projects. Epics, stages, milestones and sub-tasks keep their type; a new item is created instead.
- **Containers completed with open items**: moving an epic to a "done" column is allowed, with a warning
  that lists its open items. A stage also needs its gate approved first.
- **Switching Scrum back to Kanban**: allowed only while the project has no active or planned sprint.
  Completed sprints and their reports stay readable. Switching Kanban to Scrum leaves the board empty
  until a sprint starts, and the board says so.
- **Items created on a Scrum board**: they join the active sprint. Items created in the list, the backlog
  or a container's drawer go to the bottom of the backlog.
- **Reopened items**: an item completed in a sprint that is later reopened returns to the backlog, and
  the completed sprint's report does not change.
- **Deleted items in a sprint**: they count as removed in the sprint's report. If restored, they return
  to the backlog.
- **Sprint past its end date**: it stays active, marked as overdue, until someone completes it.
- **Deleting a planned sprint**: its items return to the backlog in their priority order.
- **Two people start or complete sprints at the same moment**: one succeeds, and the other is told what
  changed and sees the current sprints.
- **Unestimated items**: they count as 0 in estimate totals, and sprints show how many items have no
  estimate.
- **Locked stages**: work in a locked stage stays in "to do" columns. An item that is already started or
  done cannot be moved into a locked stage, and its sub-tasks are locked with it. Items in no stage are
  never locked. The lock applies to everyone, administrators included.
- **Deleting a column**: when the column holds items of a locked stage, those items can only be sent to
  another "to do" column.
- **Approver who loses access**: if an approver is removed from the project, or their account is
  deactivated, while the gate awaits a decision, the request cannot complete. The Project Admin withdraws
  it, names new approvers and requests approval again. Approvers already named on gates that have not
  been requested are shown as unable to decide until replaced.
- **Approving your own request**: by default, a requester who is also an approver cannot decide on that
  request. The request is refused when the other approvers could not reach the number needed. When a
  Project Admin turns the exclusion off for a gate, the requester decides like any other approver.
- **Stage order**: new stages go after the current stage. Locked stages can be reordered among
  themselves. Stages with approved gates, and the current stage, keep their places.
- **Deleting stages**: a stage whose gate is approved cannot be deleted. Deleting the current stage
  (after confirming) ends any pending request and unlocks the next stage. Its items remain in the project,
  in no stage, and every change is recorded in the history.
- **Restoring a deleted stage**: it returns as the last stage, with its gate not requested, so restoring
  never locks work that has already started.
- **All stages approved**: nothing is locked. A stage added later becomes the current stage.
- **Stage without dates**: its gate has no marker on the timeline but still works in the Stages view.
- **Dependencies without dates**: a dependency is kept but not drawn or flagged until both items have the
  dates it needs.
- **Hidden items**: when one end of a dependency is in a collapsed container or hidden as completed, no
  arrow is drawn, and both drawers still list the dependency.
- **Finished work**: a dependency is never flagged late once the item it waits for is in a "done" column.
- **Dependencies inside the hierarchy**: an item cannot wait for its own container, parent or sub-task,
  or be waited for by them.
- **Restoring a deleted item**: its dependencies do not come back. They can be added again.
- **Moving an item between containers**: allowed within the project; into a locked stage, only while the
  item is in a "to do" column.
- **Empty states**: an empty backlog, no sprints, no gate approvers, no gates waiting for me, no
  dependencies and no scheduled containers each show a helpful message and the next action, such as
  "Create sprint", "Name approvers" or "Schedule".

## Requirements *(mandatory)*

### Functional Requirements

**Templates**

- **FR-001**: Every project MUST have exactly one template: Kanban, Scrum or Stage-gate. The person who
  creates a project MUST choose its template in the creation form, with Kanban preselected.
- **FR-002**: A new project's columns MUST come from its template. Kanban: To Do, In Progress and Done (as
  in Phase 1). Scrum: To Do, In Progress, In Review (type "in progress") and Done. Stage-gate: Not
  Started (type "to do"), In Progress, On Hold (type "in progress") and Completed (type "done"). Project
  Admins can then customize the columns as in Phase 1.
- **FR-003**: A new Stage-gate project MUST start with four stages in this order: Initiation, Planning,
  Execution and Closure. Each stage has its own gate, with no approvers and no dates yet.
- **FR-004**: When Phase 3 is installed, every existing project MUST become a Kanban project. Its tasks
  MUST become work items of type Task and its sub-tasks of type Sub-task. Nothing else about the project
  or its work may change.
- **FR-005**: Project Admins and administrators MUST be able to switch a Kanban project to Scrum at any
  time, and a Scrum project to Kanban while it has no active or planned sprint. Switching to or from
  Stage-gate MUST NOT be possible. A switch keeps the project's columns and work. Switching to Scrum puts
  the open stories, tasks and bugs in the backlog in board order: column by column from left to right,
  keeping their order within each column. Each switch MUST be recorded in the audit log.
- **FR-006**: The project's template MUST be shown in the project's header and in the project list.

**Work item types**

- **FR-007**: The work item types MUST depend on the template. Kanban and Scrum projects offer Epic,
  Story, Task, Bug and Sub-task. Stage-gate projects offer Stage, Task, Milestone and Sub-task.
- **FR-008**: Epics and stages are containers and MUST NOT have a parent. Stories, tasks, bugs and
  milestones are standard items; each MAY belong to one container in its project. Every sub-task MUST
  belong to exactly one standard item other than a milestone, in the same project. Sub-tasks cannot have
  sub-tasks (as in Phase 1).
- **FR-009**: Every work item MUST show its type wherever it appears: board cards, list rows, the drawer,
  the timeline, the backlog and "My tasks". The type is shown as a symbol with its name available as
  text, not by color alone.
- **FR-010**: Contributors MUST be able to choose the type when they create a work item. The "What needs
  to be done?" box MUST offer every type that can be created there, defaulting to Story in Scrum projects
  and Task otherwise. On the board it offers only standard items other than milestones. Only Project
  Admins and administrators can create stages (FR-039). Sub-tasks are still created from their parent's
  drawer.
- **FR-011**: In agile projects, contributors MUST be able to change a work item's type between Story,
  Task and Bug. Any other type change MUST NOT be offered and MUST be refused.
- **FR-012**: A milestone MUST have a single date (its due date) and no start date, estimate or
  sub-tasks.
- **FR-013**: Boards MUST NOT show containers or milestones as cards.
- **FR-014**: In agile projects, stories, tasks and bugs MUST have an optional estimate: a number from 0
  to 999 with at most one decimal place (story points, or any unit the team chooses). Contributors edit it
  in the drawer. When an estimate is set, board cards and backlog rows show it.

**Epics and stages**

- **FR-015**: From a standard item's drawer, contributors MUST be able to put the item in a container,
  move it to another one, or take it out. From a container's drawer, they MUST be able to add a new item
  to the container by typing its title; the item starts in the project's leftmost "to do" column.
- **FR-016**: A container's drawer MUST list its items with their key, type, title, status, assignee and
  due date, and show the container's progress.
- **FR-017**: A container's progress MUST be the share of its standard items, not counting milestones,
  that are in a "done" column. It is shown as a count and a percentage (for example "3 of 4 done
  (75%)"). A container with no such items shows 0%, and a container that is itself in a "done" column
  shows 100%. U-PMS uses this definition of progress everywhere.
- **FR-018**: Moving a container into a "done" column while it holds open items MUST be allowed, with a
  warning that lists them. For stages, FR-034 also applies.
- **FR-019**: Deleting a container MUST keep its items in the project, in no container, and record the
  change in each item's history. Epics can be deleted by their creator, by Project Admins and by
  administrators; stages only by Project Admins and administrators, and never once their gate is approved.
- **FR-020**: The List view MUST show every type of work item, including containers and milestones. It
  MUST add a type column and an epic or stage column, with filters by type and by epic or stage
  (including "none"). These filters MUST be kept in the list's address, like the Phase 2 filters.

**Backlog and sprints (Scrum projects)**

- **FR-021**: Each Scrum project MUST have a Backlog view. It lists the planned and active sprints with
  their items, and below them the backlog: every open story, task and bug that is in no sprint, in the
  project's priority order. Epics are not listed, and sub-tasks travel with their parent. Each row shows
  the item's key, type, title, epic, assignee, estimate and status. Contributors can add an item to the
  bottom of the backlog through a "What needs to be done?" box.
- **FR-022**: Contributors MUST be able to reorder the backlog by drag-and-drop or with "Move to top",
  "Move to bottom", "Move up" and "Move down". The order MUST be the same for everyone, and the Scrum board
  MUST use the same order within its columns.
- **FR-023**: Project Admins and administrators MUST be able to create sprints, named "<key> Sprint <n>" by
  default (for example "MOB Sprint 3"), with an optional goal of up to 500 characters. They MUST be able to
  rename a planned or active sprint and change its goal. They MUST be able to delete a planned sprint, and
  its items then return to the backlog.
- **FR-024**: Contributors MUST be able to move stories, tasks and bugs between the backlog and any
  planned or active sprint, and from one sprint to another, by drag-and-drop or with a "Move to" action.
  Sub-tasks MUST always be in the same sprint as their parent. Epics are never in a sprint.
- **FR-025**: Project Admins and administrators MUST be able to start a planned sprint with a start date, an
  end date and an optional goal. The end date defaults to two weeks after the start date and can never be
  earlier than the start date. At most one sprint per project can be active; starting a second one MUST be
  refused with an explanation.
- **FR-026**: Each planned and active sprint MUST show its number of items, its estimate total, and how
  many of its items have no estimate. The active sprint MUST also show its completed and remaining
  totals.
- **FR-027**: The board of a Scrum project MUST show only the active sprint's items, including all of
  its done items, with no 14-day limit. Without an active sprint, the board MUST say so and link to the
  backlog. Items created on the Scrum board join the active sprint. Items created anywhere else go to the
  bottom of the backlog.
- **FR-028**: Project Admins and administrators MUST be able to complete the active sprint and choose where
  its open items go: the backlog or a planned sprint. Items in a "done" column stay in the completed sprint.
  After completion, no item can be moved into or out of that sprint.
- **FR-029**: Each completed sprint MUST have a report listing the items, with their estimate totals, that
  were committed at the start, added during the sprint, removed during the sprint (moved out or deleted),
  completed, and not completed at the end. Each estimate is taken when the item was counted: at the
  start, when it was added or removed, or at completion. The report MUST NOT change after the sprint is
  completed. The Backlog view MUST link to the reports of past sprints.
- **FR-030**: In Scrum projects, an item's drawer MUST show its sprint, and its history MUST record each
  time it joins or leaves a sprint.

**Stage gates (Stage-gate projects)**

- **FR-031**: A Stage-gate project's stages MUST be kept in one order, and each stage MUST end in a gate.
  A Stages view MUST list the stages in order. For each stage it shows the dates, the progress, whether
  the stage is current, approved or locked, the gate's status (Not requested, Awaiting decision,
  Approved or Sent back), and the approvers with their decisions.
- **FR-032**: A stage MUST be locked while any earlier stage's gate is not approved; the first stage is
  never locked. While a stage is locked, neither the stage nor its items nor their sub-tasks can be moved
  out of a "to do" column. Such moves MUST be refused with a message that names the gate that must be
  approved first. Everything else stays allowed, including creating, editing, assigning, scheduling and
  commenting. Items in no stage are never locked. The lock applies to everyone, administrators included.
- **FR-033**: An item that is not in a "to do" column MUST NOT be moved into a locked stage. When a column
  holding items of a locked stage is deleted, those items MUST go to another "to do" column.
- **FR-034**: A stage MUST NOT be moved into a "done" column until its gate is approved.
- **FR-035**: Project Admins and administrators MUST be able to name one or more approvers for each gate,
  choosing any active user. Naming someone who is not a member of the project MUST also add them as a
  Viewer, recorded in the audit log like any other membership change. They MUST also be able to set how many
  approvals the gate needs, from one to the number of approvers; the default is all of them. They MUST
  also be able to choose whether the person who requests approval is excluded from approving it; the
  default is excluded. Approvers, the number needed and the exclusion can be changed only while the gate
  does not await a decision and is not approved.
- **FR-036**: Contributors MUST be able to request approval of the current stage's gate, once the gate has
  at least one approver and every approver can still decide (an active member of the project). When the
  requester is excluded from approving and is one of the approvers, the other approvers must be enough to
  reach the number needed; otherwise the request MUST be refused with an explanation. If the stage still has
  open items, they MUST be warned and shown those items, and they may continue. The gate then awaits a
  decision. Contributors MUST be able to withdraw a pending request, which clears the decisions recorded for
  it.
- **FR-037**: While a gate awaits a decision, each of its approvers, and nobody else, MUST be able to
  approve it or send it back once, except the requester when the gate excludes them, with a note of up to
  1,000 characters. The note is required when sending a gate back. A Viewer who is an approver MUST be able
  to record this decision; it is the only change a Viewer can make.
- **FR-038**: The gate MUST become approved as soon as it has the number of approvals it needs, and the
  next stage MUST unlock at once. Until then, a single "send back" MUST end the request and set the gate
  to "Sent back" with the note; contributors can then request approval again. An approved gate is
  final: it cannot be withdrawn, and its approvers and decisions cannot change.
- **FR-039**: Only Project Admins and administrators MUST be able to add, reorder and delete stages. New
  stages go after the current stage (at the end, by default). Locked stages can be reordered among
  themselves. Stages with approved gates, and the current stage, keep their places.
- **FR-040**: Every gate event MUST be recorded in the stage's history and in the audit log, with who
  acted and when: approvers, the number needed or the requester exclusion changed, approval requested,
  request withdrawn, each decision with its note, and the gate approved.
- **FR-041**: "My tasks" MUST list the user's open assigned work items of every type. It MUST also list
  the gates that await the user's decision, in projects the user can see, with the project, the stage,
  who requested approval and when. Selecting a gate MUST open it.

**Timeline, milestones and dependencies**

- **FR-042**: The timeline MUST show each container as a bar from its start date to its due date, with
  its progress. Each container can be expanded and collapsed. Expanded, it shows its scheduled items as
  bars beneath it and lists its unscheduled items, as Phase 2 does for sub-tasks. A container without
  dates keeps its row, without a bar. Stages are shown in stage order, and epics in the Phase 2 order (by
  first date, then due date, then key). Items in no container follow the containers, as in Phase 2.
- **FR-043**: A milestone MUST show as a marker on its date, with its key and title, not as a bar.
  Contributors MUST be able to move a milestone to another date by dragging it, or with the keyboard: the
  arrow keys move it one day at a time, Enter saves and Escape cancels. A milestone cannot be resized.
- **FR-044**: In Stage-gate projects, each stage's gate MUST show as a marker on the stage's due date,
  labelled with the gate's status.
- **FR-045**: Containers and milestones without dates MUST offer "Schedule" on the timeline. It gives a
  container a start date of today and a due date 27 days later, and a milestone a date of today. Other
  items keep the Phase 2 rule.
- **FR-046**: The timeline of a Scrum project MUST show a read-only lane with its active and completed
  sprints, each as a bar from its start date to its end date.
- **FR-047**: Contributors MUST be able to add a dependency ("B waits for A") between two work items of
  the same project, and remove it. They can do this from either item's drawer, or from a bar's menu on
  the timeline, finding the other item by key or title. Dependencies MUST NOT link items in different
  projects.
- **FR-048**: A dependency MUST be refused, with an explanation, if it links an item to itself, repeats an
  existing dependency, links an item to its own container, parent or sub-task, or would create a loop,
  directly or through other items.
- **FR-049**: Each item's drawer MUST list the items it waits for and the items waiting for it, with their
  key, title, status and dates, and whether each dependency is late.
- **FR-050**: The timeline MUST draw each dependency whose two items are shown as an arrow from the end of
  the item waited for to the start of the waiting item.
- **FR-051**: A dependency MUST be flagged late when the waiting item is scheduled to start before the due
  date of the item it waits for, while that item is still open. The waiting item's start is its start
  date, or its due date when it has no start date; a milestone's is its date. A dependency missing any of
  these dates is neither drawn nor flagged. Late dependencies MUST be marked with a label or symbol, not
  color alone, on the timeline and in both drawers. Dates MUST NOT change automatically.
- **FR-052**: Adding or removing a dependency MUST be recorded in both items' history. Deleting an item
  MUST remove its dependencies and record that in each linked item's history; restoring the item does
  not restore them.

**Across the product**

- **FR-053**: Every change that is new in Phase 3 MUST be recorded in the item's history, with who, when,
  and the old and new values. This covers type, container, estimate, sprint membership, backlog order,
  dependencies and gate events.
- **FR-054**: Phase 3 changes MUST follow the same conflict rules as other edits: a change made by
  someone else since the screen was loaded MUST NOT be silently overwritten, and the user MUST see the
  current state. This covers backlog reordering, sprint moves, starting and completing sprints, stage
  order, approver changes, gate requests and decisions, and dependencies.
- **FR-055**: Permissions MUST follow Phase 2. Viewers can see everything new (backlog, sprint reports,
  stages, gates and dependencies) and can change nothing, except a gate decision under FR-037.
  Contributors can do everything this spec allows contributors to do. Switching templates, managing
  sprints (creating, renaming, starting, completing and deleting them), managing stages and setting up
  gates are limited to Project Admins and administrators, as with Jira's "Manage Sprints" permission.
  Every check MUST be made on the server for every request.
- **FR-056**: Every new or changed screen MUST be fully usable with a keyboard alone, meet WCAG 2.2 Level
  AA, and stay usable on screens 360 pixels wide. Every drag action MUST have a keyboard or menu
  equivalent.
- **FR-057**: The requirements of Phases 1 and 2 MUST continue to apply, except where this spec replaces
  or extends them:
  - Phase 1 FR-016 (the three starting columns) is replaced by FR-002.
  - Phase 1 FR-021 (done columns show 14 days) does not apply to Scrum boards (FR-027).
  - Phase 1 FR-018 (inline creation), FR-025 (task fields) and FR-031 (history) are extended by FR-010,
    FR-007 to FR-014, and FR-053.
  - Phase 2 FR-003 (Viewers change nothing) is replaced by FR-055, which lets approvers who are Viewers
    record gate decisions.
  - Phase 2 FR-015 (the project list) is extended by FR-006, FR-025 ("My tasks") by FR-041, FR-028 to
    FR-030 (the list) by FR-020, and FR-034 to FR-040 (the timeline) by FR-042 to FR-046.

### Key Entities *(include if feature involves data)*

- **Project** (extended): gains a template (Kanban, Scrum or Stage-gate). Every existing project is
  Kanban.
- **Work Item** (formerly Task): gains a type, an optional container, an optional estimate (agile
  standard items), and a sprint (Scrum projects). A milestone keeps a single date.
- **Work Item Type**: Epic, Stage, Story, Task, Bug, Milestone or Sub-task. Each type has a level
  (container, standard item or sub-task) and the templates that offer it.
- **Sprint**: a time-box in a Scrum project. It has a name, a goal, a state (planned, active or
  completed), start and end dates, and a completion time. It also keeps a record of the items committed
  at the start, added, removed and completed, with their estimates.
- **Stage order**: the order of a Stage-gate project's stages. It decides which stage is current and
  which are locked.
- **Gate**: the approval at the end of one stage. It has a status (Not requested, Awaiting decision,
  Approved or Sent back), its approvers, the number of approvals it needs, and whether the requester is
  excluded from approving.
- **Gate Request and Decision**: one request for approval, with who asked and when, and each approver's
  decision (approved or sent back) with its note and time.
- **Dependency**: "B waits for A" between two work items of the same project, with who created it and
  when. Whether it is late is worked out from the two items' dates and A's status.
- **Change Record** (extended): also covers type, container, estimate, sprint and backlog order changes,
  dependencies, and gate events.
- **Audit Event** (extended): also covers template switches and gate events.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Scrum team can create a sprint, fill it with 20 items from the backlog, and start it in
  under 10 minutes.
- **SC-002**: With 500,000 work items in the system and 300 people using it at the same time, 95% of the
  following complete within 1 second: backlog loads (up to 1,000 open items), backlog reorders and
  sprint moves, sprint report and Stages view loads, gate requests and decisions, dependency changes, and
  timeline loads (up to 500 scheduled items and 200 dependencies).
- **SC-003**: A project manager can lay out a Stage-gate plan of 4 stages, 30 tasks, 4 milestones and 10
  dependencies, and name each gate's approvers, in under 45 minutes.
- **SC-004**: At least 90% of approvers can find a gate waiting for them and record their decision within
  1 minute of signing in, without help.
- **SC-005**: Across acceptance and security testing, there are zero cases of work in a locked stage
  leaving a "to do" column, a stage completed before its gate is approved, or a gate decision recorded by
  anyone other than that gate's approvers, including a requester the gate excludes.
- **SC-006**: In 100% of checks, each sprint report's items and totals match the items that were in the
  sprint at its start, added, removed and completed, and the report is unchanged after the sprint is
  completed.
- **SC-007**: In 100% of checks, a dependency is flagged late on the timeline and in both drawers exactly
  when FR-051 says it is late, and no date ever changes because of a dependency.
- **SC-008**: 100% of type, container, estimate, sprint, backlog order, dependency and gate changes appear
  in the history with who made them, when, and the old and new values. 100% of template switches and gate
  events also appear in the audit log.
- **SC-009**: Zero silent overwrites: in concurrent tests of backlog reordering, sprint changes, stage
  and approver changes, gate decisions and dependencies, every conflicting change is shown to the person
  who saved second.
- **SC-010**: After Phase 3 is installed, 100% of existing projects are Kanban projects, with their tasks,
  sub-tasks, columns, members, assignees, dates and history unchanged.
- **SC-011**: Automated accessibility checks report zero WCAG 2.2 AA violations on the project creation
  form, the Backlog view, sprint reports, the Stages view, the changed timeline, board, list and drawer,
  and "My tasks". Every acceptance scenario can be completed with the keyboard alone.
- **SC-012**: Within a 4-week pilot, at least one Scrum team completes two sprints, and one PMO project
  passes at least one gate, entirely in U-PMS. At least 80% of the pilot users rate the backlog, sprint
  and gate screens "easy" or "very easy" to use.

## Assumptions

- **Builds on Phases 1 and 2**: sign-in, projects, customizable columns, the drawer, comments, history,
  members-only projects with Project Admin, Member and Viewer roles, assignees, dates, "My tasks" and the
  Board, List and Timeline views work as specified in `specs/002-kanban-project-core` and
  `specs/003-project-views-and-team`.
- **Phase 4** (decision of 2026-09-28): portfolios, portfolio rollups, the portfolio timeline, and one
  dashboard per project and per portfolio with health updates. Phase 3 adds nothing for portfolios.
- **One set of columns per project**: every type in a project uses the project's columns. There are no
  separate workflows, custom types or custom fields per type.
- **Default stages**: Initiation, Planning, Execution and Closure follow a common PMO lifecycle. Each
  project can rename, add, remove and reorder its stages within the rules of FR-039.
- **Gate approvals work like Jira's approvals** (decision of 2026-09-28): each gate needs a set number of
  approvals, all of its approvers by default, and one "send back" ends the request. As with Jira
  Cloud's "Exclude approvers from" setting, the person who requested approval cannot approve it unless
  the gate allows it.
- **Approvers can come from anywhere in the organization** (decision of 2026-09-28): naming someone
  outside the project makes them a Viewer, so only members ever see a project. This includes
  administrators, who otherwise see every project without being listed as members.
- **No notifications** (in-app or email) in Phase 3: approvers find gates waiting for them on "My tasks",
  and teams find their sprint on the board and the backlog.
- **Estimates** are story points or any consistent unit the team chooses. Only stories, tasks and bugs
  have them; sub-tasks do not, so nothing is counted twice. There is no time tracking.
- **Milestones belong to Stage-gate projects**: agile projects group work with epics. Jira-style releases
  (versions with a release date) are the agile equivalent and come in a later phase (decision of
  2026-09-28).
- **Dependencies** are finish-to-start and within one project. Their dates only raise flags: there is no
  automatic scheduling, lag, critical path or baseline.
- **Container dates are their own**: an epic's or stage's dates are set by people. They are not worked
  out from its items, and they do not limit its items' dates.
- **Screen names stay**: "My tasks", "Only my tasks" and "What needs to be done?" keep their Phase 2
  names and now cover every type, so pilot users need no retraining.
- **Views remember nothing between visits**, as in Phase 2: opening a project still shows its board, and
  each view's address can be bookmarked.
- **Scale** stays at Phase 2's: up to 2,000 named users (300 active at once) and 500,000 work items.
  A Scrum backlog holds up to 1,000 open items, and a project's timeline shows up to 500 scheduled items
  and 200 dependencies at a time.
- Out of scope for Phase 3 (candidates for later phases): portfolios, rollups and dashboards (Phase 4);
  health updates; burndown and velocity charts; releases and versions; custom workflows, types and
  fields; cross-project dependencies and other link types (relates to, duplicates); automatic
  scheduling, lag, critical path and baselines; gate checklists, gate documents and delegated approval;
  budgets, cost, effort and resource planning; risk, issue and decision logs; notifications and email;
  labels; time tracking; bulk editing; import and export; and live updates of open screens.
