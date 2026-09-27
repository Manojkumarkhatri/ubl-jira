# U-PMS (UBL Project Management System)

U-PMS is a Jira-like project management system for a single organization, serving both IT teams
(agile: Scrum and Kanban) and the PMO (structured, phase-based projects) on one hierarchy:
Portfolio → Project → work items (epics or phases, stories, tasks, bugs, milestones, sub-tasks). It
covers projects, work items, backlog and sprints, an interactive timeline, boards, project and
portfolio dashboards, collaboration, search, and user administration.

**Status**: Phase 1 (core Kanban project) is specified and planned; implementation not started. The
project follows
[Spec Kit](https://github.com/github/spec-kit) spec-driven development: every feature is specified,
clarified, planned, and broken into tasks before any code is written.

## Stack

.NET 10 (LTS) · ASP.NET Core Blazor Web App (Interactive Server) · SQL Server with EF Core 10 ·
ASP.NET Core Identity (built-in accounts; company SSO can be added later) · xUnit, bUnit,
Testcontainers, Playwright. One deployable modular monolith; see the
[Phase 1 plan](specs/002-kanban-project-core/plan.md) for details.

## Roadmap

| Phase | Scope | Spec |
|-------|-------|------|
| **1. Core Kanban project** | Sign-in from Day 1; project creation (name, key, description); Kanban board (To Do, In Progress, Done) with inline "What needs to be done?"; task details drawer (title, description, priority, sub-tasks, comments, history); customizable columns | [specs/002-kanban-project-core](specs/002-kanban-project-core/spec.md) |
| 2. Views and project team | Board, List and Timeline views of the same project; project members and task assignees | to be specified |
| 3. Enterprise and portfolio | Portfolio rollups, cross-project dashboards, Scrum and PMO stage-gate templates | to be specified |

The earlier MVP spec, [specs/001-issue-tracker-mvp](specs/001-issue-tracker-mvp/spec.md), is kept as
the product vision and requirement backlog for Phases 2 and 3.

## Phase 1 documents

| Document | Purpose |
|----------|---------|
| [Constitution](.specify/memory/constitution.md) | Non-negotiable project principles and quality gates |
| [Spec](specs/002-kanban-project-core/spec.md) | What Phase 1 does and why: 3 stories, 44 requirements |
| [Plan](specs/002-kanban-project-core/plan.md) | Architecture, constitution check, source layout |
| [Research](specs/002-kanban-project-core/research.md) | Technology decisions, including how Phases 2–3 extend Phase 1 |
| [Data model](specs/002-kanban-project-core/data-model.md) | Tables, rules, and the planned additive changes for later phases |
| [Contracts](specs/002-kanban-project-core/contracts/) | Application services, permissions, UI routes, HTTP endpoints |
| [Quickstart](specs/002-kanban-project-core/quickstart.md) | How to run and validate Phase 1 once built |
| [Design prototype](design/prototype/index.html) | Clickable prototype with demo data: open the file in any browser |

## Design prototype

`design/prototype/index.html` is a single self-contained page (no install, no server). Open it in
Edge, Chrome or Firefox to click through project creation, the Summary page, Timeline, Backlog and
sprints, the board, the issue list, and the issue detail panel. It runs on demo data stored in your
browser; use the avatar menu to switch demo users or reset the data. It shows more than Phase 1
(Scrum backlog, timeline, summary), so treat it as a picture of the whole roadmap.

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-tasks`, then `/speckit-analyze`: break Phase 1 into test-first tasks and cross-check them
- `/speckit-implement`: build Phase 1 from its `tasks.md`
- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`: for Phase 2 and Phase 3 when their turn
  comes

Local setup and test commands will live in the
[quickstart](specs/001-issue-tracker-mvp/quickstart.md) once the code exists.
