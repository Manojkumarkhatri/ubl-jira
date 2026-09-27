# Pilot plan (Phases 1 and 2)

A two-week pilot with one or two real teams shows whether U-PMS is ready for wider use. It measures the success
criteria that need people rather than automated tests: three from Phase 1 and, when Phase 2 is installed, three
from Phase 2. Phase 2 numbers its criteria from SC-001 again, so they are written "P2 SC-…" here.

**Phase 1**

| Criterion | Target | How it is measured |
|-----------|--------|--------------------|
| **SC-001** | At least 90% of first-time users create a project and add their first three tasks in under 3 minutes without help | Timed task session on day 1 (below) |
| **SC-003** | A project owner adds a column, renames another, reorders them and sets a work-in-progress limit in under 2 minutes | Timed task session on day 1 (below) |
| **SC-011** | At least one team runs all of its daily work on a U-PMS board for the 2 weeks, and at least 80% of its members rate the board and drawer "easy" or "very easy" | Usage check during the pilot and the end-of-pilot survey (below) |

**Phase 2** ([spec](../../specs/003-project-views-and-team/spec.md))

| Criterion | Target | How it is measured |
|-----------|--------|--------------------|
| **P2 SC-001** | A Project Admin adds three colleagues with different roles to a project in under 2 minutes | Timed task session C (below) |
| **P2 SC-007** | At least 90% of pilot users find every open task assigned to them within 30 seconds of signing in, without help | Timed task session D (below) |
| **P2 SC-008** | A contributor moves a task two weeks later on the timeline in under 15 seconds, with a mouse or with the keyboard alone | Timed task session E (below) |

The other success criteria are covered by automated tests (Phase 1 SC-002 performance, SC-004 to SC-008;
Phase 2 SC-002 to SC-006 and SC-009, recorded in
[validation.md](../../specs/003-project-views-and-team/validation.md)), monitoring (Phase 1 SC-009) and the
restore drill (Phase 1 SC-010) before the pilot starts.

## Before the pilot

- [ ] Production-like environment deployed ([deployment.md](../operations/deployment.md)) with monitoring on
      `/health/ready`.
- [ ] Restore drill done and recorded ([backup-restore.md](../operations/backup-restore.md)).
- [ ] Pilot teams chosen: 1–2 teams of 5–12 people with ongoing work. With Phase 1 alone, choose
      non-confidential work: every signed-in user can see every project. With Phase 2, only a project's members
      and the administrators see it. Project names are still unique across the organization, so avoid
      confidential words in them ([security review, P2-O1](../security/phase2-asvs-review.md#open-items)).
- [ ] If the teams used Phase 1 first: Phase 2 installed on the same database
      ([quickstart](../../specs/003-project-views-and-team/quickstart.md), step 1), and each team lead checks their
      project's Members page. Owners become Project Admins and everyone who worked on the project becomes a
      Member (P2 SC-006).
- [ ] For session D: the facilitator assigns each participant three open tasks in two projects, one of them
      overdue, the day before.
- [ ] Accounts created; temporary passwords delivered through a secure channel.
- [ ] At least two administrators, so accounts can be managed when one is away.
- [ ] A facilitator (runs the timed sessions) and an observer (takes notes) named for each team.
- [ ] Participants told that the sessions time the product, not them, and that they may stop at any time.

## Day 1: timed task sessions

Each participant works alone at their own computer; the facilitator reads the task, starts a stopwatch when the
participant starts, and stops it when the result is on screen. The facilitator does not help. If the
participant asks for help or gives up, the task is recorded as "not completed without help".

### Session A: first project and tasks (SC-001), every participant, first use

> "Create a project for your team's work and add three tasks you are working on this week."

Completed when the board shows the new project with three cards. Record the time and whether help was needed.

**Pass**: at least 90% of participants complete it in under 3 minutes without help.

### Session B: shape the board (SC-003), each project owner

Using the project from session A:

> "Add a column called 'In Review' between In Progress and Done, rename 'To Do' to 'Backlog', move 'In Review'
> one place to the left, and limit 'In Progress' to 3 cards."

Completed when the settings page shows all four changes. Record the time.

**Pass**: every owner completes it in under 2 minutes.

### Session C: build the team (P2 SC-001), each Project Admin

Using the participant's project:

> "Add three colleagues to your project: one as a Project Admin, one as a Member and one as a Viewer."

Completed when the Members page lists all three with those roles. Record the time.

**Pass**: every Project Admin completes it in under 2 minutes.

### Session D: find my work (P2 SC-007), every participant

Start the stopwatch when the participant has signed in and sees the project list:

> "Find every open task that is assigned to you, in all your projects."

Completed when the participant has named or pointed to their three tasks, for example on "My tasks". Record
the time and whether help was needed.

**Pass**: at least 90% of participants find all three within 30 seconds without help.

### Session E: reschedule on the timeline (P2 SC-008), every contributor

Using a project with dated tasks:

> "On the timeline, move the task '…' two weeks later."

Run it once with the mouse and, for anyone who works with the keyboard, once with the keyboard alone. Completed
when the task's bar shows the new dates. Record each time.

**Pass**: every contributor completes it in under 15 seconds, with the mouse or with the keyboard.

### Recording sheet

| Participant (code) | Role | Session | Time (m:ss) | Without help? | Observations (hesitations, errors, comments) |
|--------------------|------|---------|-------------|---------------|-----------------------------------------------|
| P01 | member | A | | | |
| P01 | owner | B | | | |
| P01 | Project Admin | C | | | |
| P01 | member | D | | | |
| P01 | member | E (mouse / keyboard) | | | |

Use participant codes, not names, in the sheet.

## During the pilot (2 weeks)

- The team moves its daily work (stand-ups, planning, status changes, discussion) onto its U-PMS board. The
  team lead confirms this at the start and at the end of each week.
- The facilitator checks weekly, from the boards themselves, that tasks are being created, moved and commented
  on by the team (for example the number of cards moved and comments posted that week).
- Problems are reported in one agreed place (a channel or a form) with the time, the page and what happened.
  Blocking problems are fixed or worked around within one working day.
- Accessibility: at least one keyboard-only user and one screen-reader user (if the teams include one) are
  asked about their experience.

**SC-011 part 1 passes** when at least one team ran all of its daily work on its board for both weeks.

## End of the pilot: survey

Sent to every pilot participant on the last day; anonymous; five minutes.

1. How easy is it to use the **board** (creating, finding and moving tasks)? *Very easy / Easy / Neither /
   Difficult / Very difficult*
2. How easy is it to use the **task drawer** (details, sub-tasks, comments, history)? *same scale*
3. Did you use U-PMS for all of your team's daily work during the pilot? *Yes / Mostly / Partly / No*
4. What slowed you down or confused you? *free text*
5. What should we improve first? *free text*
6. Did you use the keyboard or a screen reader to work with U-PMS? If so, what worked and what did not?
   *free text*
7. With Phase 2: how easy was it to see your own work ("My tasks", the "Only my tasks" filter) and to plan
   with the List and Timeline views? *Very easy / Easy / Neither / Difficult / Very difficult*

**SC-011 part 2 passes** when at least 80% of the pilot team's respondents answer "Easy" or "Very easy" to
both question 1 and question 2.

## Results and decision

At the end of the pilot, the product owner summarizes:

| Criterion | Result | Pass? |
|-----------|--------|-------|
| SC-001 | _% of participants under 3 minutes without help (median time)_ | |
| SC-003 | _owners under 2 minutes (times)_ | |
| SC-011 | _team(s) fully on the board; % easy or very easy for board and drawer_ | |
| P2 SC-001 | _Project Admins under 2 minutes (times)_ | |
| P2 SC-007 | _% of participants within 30 seconds without help (median time)_ | |
| P2 SC-008 | _contributors under 15 seconds with the mouse, and with the keyboard (times)_ | |

together with the top problems from the report channel and survey questions 4–7. The decision options are:
roll out more widely, fix the listed problems and repeat the affected sessions, or change the plan for the next
phase.
