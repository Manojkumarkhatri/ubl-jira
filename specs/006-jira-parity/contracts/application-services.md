# Contract: Application Services (Jira Parity Pass)

**Feature**: [spec.md](../spec.md) | **Data model**: [data-model.md](../data-model.md)

New and changed service methods. Every space-scoped method calls `IProjectAccess.RequireAsync` first; people
outside a space get `NotFound`. Methods return `Result` / `Result<T>`, as in Phases 1 to 4.

## Identity module

| Method | Change | Notes |
|--------|--------|-------|
| `IAccountService.GetProfileAsync` | changed | `MyProfile.SidebarCollapsed` |
| `IAccountService.SetSidebarCollapsedAsync(bool collapsed)` | **new** | the signed-in person only; `Forbidden` when nobody active is signed in |

## Projects module

| Method | Change | Notes |
|--------|--------|-------|
| `IProjectService.CreateBlankAsync(name, key, description, SpaceSetup setup)` | **new** | `SpaceSetup(IReadOnlyList<NewColumn> Columns, WorkTypeSet WorkTypes)` for the Blank template (Kanban), beside the unchanged `CreateAsync(…, template)`; refusals as data-model.md (FR-014–FR-016) |
| `IProjectService.SetWorkTypesAsync(spaceKey, WorkTypeSet set, int expectedDetailsVersion)` | **new** | Manage right; `NoStandardType`, `WorkTypeInUse`, `TemplateSwitchNotAllowed`, `Conflict`; audits `ProjectWorkTypesChanged` (FR-017) |
| `IProjectService.GetAsync` | changed | `ProjectDetails.WorkTypes`, and per type the number of items using it (for the settings screen) |
| `IProjectService.ListAsync` | changed | `ProjectSummary.IsStarred` |
| `ProjectAccessInfo` | changed | `WorkTypes` |
| `IProjectPlanSetup.TypeCountsAsync(projectId)` | **new** (Work module implements) | live and deleted items per type |
| `ISpaceNavigationService.GetSidebarAsync()` | **new** | `SidebarView(Starred, RecentSpaces, Spaces, IsAdministrator)`; only what the caller can see now; the sidebar adds recent items (`ISearchService.RecentAsync`) and saved filters (`ISavedFilterService`) itself (FR-021) |
| `ISpaceNavigationService.StarAsync(spaceKey)` / `UnstarAsync(spaceKey)` | **new** | View right; idempotent (FR-021b) |
| `ISpaceNavigationService.RecordVisitAsync(spaceKey)` | **new** | View right; keeps the 20 latest per person |
| `ISpaceNavigationService.StarredAsync()` | **new** | the For you "Starred" tab |

## Work module

| Method | Change | Notes |
|--------|--------|-------|
| `IWorkItemService.CreateAsync(spaceKey, NewWorkItem item)` | changed | every optional field of data-model.md in one transaction and one change; Sub-task with `ParentKey`; Stage needs Manage (FR-003–FR-008) |
| `IWorkItemService.UpdateAsync(key, WorkItemEdit.Color(EpicColor), version)` | changed | contributors; epics only (FR-025) |
| `ICreateWorkService.SpacesAsync()` | **new** | `CreateSpaces(IReadOnlyList<CreateSpaceOption> Spaces, string? DefaultKey)`; spaces the caller can contribute to, by name; the option lists the types offered to the caller (FR-002, FR-003) |
| `ICreateWorkService.OptionsAsync(spaceKey)` | **new** | Contribute right; `CreateOptions(Types, Columns, Assignees, Containers, Sprints, Template)` (FR-004) |
| `ICreateWorkService.FindParentsAsync(spaceKey, WorkItemType type, string text)` | **new** | Contribute right; up to 20: epics or stages for standard items and milestones, stories, tasks and bugs for sub-tasks (FR-005) |
| `IBoardService.GetAsync` | changed | sub-task cards (FR-027a); `CardView.ParentKey`, `ContainerView.Color`; columns shown on Scrum boards without an active sprint (FR-013) |
| `IBoardService.MoveCardAsync(key, toColumnId, placement, version, LaneChange? lane)` | changed | Contribute right; lane change and column move in one change; `SubtaskFollowsParent`, `NotAssignable`, `StageLocked`, `Conflict`; a sub-task card changes column only; `CardPlacement.InPlace` keeps the card's place (a "Lane" chosen from "Move to") (FR-027, FR-027a) |
| `IBoardService.CreateInlineAsync` | changed | refuses types the space has turned off (`TypeNotAllowed`) |
| `ISpaceSummaryService.GetAsync(spaceKey)` | **new** | View right; `SpaceSummary(Last7Days, StatusOverview, RecentActivity, Priorities, Types, Workload, Containers)` (FR-022) |
| `ISearchService.WorkedOnAsync(PageRequest page)` | **new** | items the caller created, changed or commented on in the last 30 days, newest first, 50 a page, only in spaces they can see (FR-022a); beside `RecentAsync` ("Viewed") |
| `ISearchService.RecentAsync` | unchanged | the "Viewed" tab |
| `IMyTasksService` | unchanged | the "Assigned to me" tab |
| `SearchQuery.Resolved` | **new** | a date range on `ResolvedAt`, for the summary's "completed" count |

## Web

| Component or service | Change |
|----------------------|--------|
| `SidebarState` (scoped) | **new**: tells the sidebar to reload after a star, an unstar or a visit |
| `CreateWorkLauncher` (scoped) | **new**: opens the Create dialog from the header, and later from other places, with an optional space |
