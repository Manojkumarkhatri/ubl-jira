# UBL Jira

A Jira-like issue and project tracker for a single organization: projects, issues (epics, stories,
tasks, bugs, sub-tasks), a drag-and-drop board, comments with mentions, attachments, notifications,
sprints, search, and access administration.

**Status**: planning complete, implementation not started. The project follows
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
| [Spec](specs/001-issue-tracker-mvp/spec.md) | What the MVP does and why: 6 prioritized user stories, 58 requirements |
| [Plan](specs/001-issue-tracker-mvp/plan.md) | Architecture, constitution check, source layout |
| [Research](specs/001-issue-tracker-mvp/research.md) | Technology decisions with rationale and alternatives |
| [Data model](specs/001-issue-tracker-mvp/data-model.md) | Tables, fields, rules, state machines |
| [Contracts](specs/001-issue-tracker-mvp/contracts/) | Application services, permissions, endpoints, routes, emails |
| [Tasks](specs/001-issue-tracker-mvp/tasks.md) | 178 test-first tasks, ordered by user story |
| [Quickstart](specs/001-issue-tracker-mvp/quickstart.md) | How to run and validate the app once built |

## Working with Spec Kit

The Spec Kit commands are installed as Claude Code skills in `.claude/skills/`:

- `/speckit-implement`: build the tasks in `tasks.md` (start with Phases 1–3 for the MVP)
- `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-analyze`: for
  the next feature (for example attachments on comments, or company SSO)

Local setup and test commands will live in the
[quickstart](specs/001-issue-tracker-mvp/quickstart.md) once the code exists.
