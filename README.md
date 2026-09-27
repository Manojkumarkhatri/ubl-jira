# U-PMS (UBL Project Management System)

U-PMS is a Jira-like project management system for a single organization, serving both IT teams
(agile: Scrum and Kanban) and the PMO (structured, phase-based projects) on one hierarchy:
Portfolio → Project → work items (epics or phases, stories, tasks, bugs, milestones, sub-tasks). It
covers projects, work items, backlog and sprints, an interactive timeline, boards, project and
portfolio dashboards, collaboration, search, and user administration.

**Status**: spec re-specified after the prototype review (2026-09-27); plan and tasks are being
regenerated to match; a clickable design prototype is available; implementation not started. The project follows
[Spec Kit](https://github.com/github/spec-kit) spec-driven development: every feature is specified,
clarified, planned, and broken into tasks before any code is written.

## Stack

.NET 10 (LTS) · ASP.NET Core Blazor Web App (Interactive Server) · SQL Server with EF Core 10 ·
ASP.NET Core Identity (built-in accounts; company SSO can be added later) · xUnit, bUnit,
Testcontainers, Playwright. One deployable modular monolith; see the
[plan](specs/001-issue-tracker-mvp/plan.md) for details.

## Where things are

| Document | Purpose |
|----------|---------|
| [Constitution](.specify/memory/constitution.md) | Non-negotiable project principles and quality gates |
| [Spec](specs/001-issue-tracker-mvp/spec.md) | What the MVP does and why: 8 stories in delivery order, 80 requirements |
| [Plan](specs/001-issue-tracker-mvp/plan.md) | Architecture, constitution check, source layout (out of date until regenerated) |
| [Research](specs/001-issue-tracker-mvp/research.md) | Technology decisions with rationale and alternatives |
| [Data model](specs/001-issue-tracker-mvp/data-model.md) | Tables, fields, rules, state machines |
| [Contracts](specs/001-issue-tracker-mvp/contracts/) | Application services, permissions, endpoints, routes, emails |
| [Tasks](specs/001-issue-tracker-mvp/tasks.md) | Test-first tasks by user story (out of date until regenerated from the new spec) |
| [Quickstart](specs/001-issue-tracker-mvp/quickstart.md) | How to run and validate the app once built |
| [Design prototype](design/prototype/index.html) | Clickable prototype of the main features with demo data: open the file in any browser |

## Delivery order

1. **Projects**: portfolios, projects (Scrum, Kanban or Structured), members and roles, with sign-in
   and security from Day 1
2. **Tasks**: work items, workflows, detail panel, history, lists
3. **Backlog**: ranked backlog and sprints (Scrum)
4. **Timeline**: bars across weeks, months and quarters; milestones; dependencies; portfolio timeline
5. **Board & Dashboards**: boards, health updates, project and portfolio dashboards
6. Collaboration, 7. Search, 8. User administration

## Design prototype

`design/prototype/index.html` is a single self-contained page (no install, no server). Open it in
Edge, Chrome or Firefox to click through project creation, the Summary page, Timeline, Backlog and
sprints, the board, the issue list, and the issue detail panel. It runs on demo data stored in your
browser; use the avatar menu to switch demo users or reset the data. There is no sign-in yet: users,
login and permissions are a later module.

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-implement`: build the tasks in `tasks.md` (start with Phases 1–3 for the MVP)
- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze`: for
  the next feature (for example attachments on comments, or company SSO)

Local setup and test commands will live in the
[quickstart](specs/001-issue-tracker-mvp/quickstart.md) once the code exists.
