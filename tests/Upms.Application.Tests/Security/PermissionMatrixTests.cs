using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Notifications;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Security;

/// <summary>The columns of the matrix, in their order.</summary>
public enum Role
{
    Anonymous,
    NonMember,
    Viewer,

    /// <summary>A Viewer named as an approver of the gate in question (Phase 3).</summary>
    Approver,

    Member,
    Creator,
    ProjectAdmin,
    Admin,
}

/// <summary>Every cell of <c>specs/006-jira-parity/contracts/permissions.md</c> (the Jira parity pass matrix, which
/// supersedes Phase 4's), exercised against the real services (Phase 4 SC-004). The rows are read from the document
/// itself, so a row without operations here fails; rows whose operations arrive with a later user story are listed in
/// <see cref="Pending"/> and reported as skipped. The sign-in redirect (🔒) is the host's job and is tested in the web
/// tests; here an anonymous caller is refused.</summary>
public sealed class PermissionMatrixTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private const string NewPassword = "a brand new passphrase 77";

    private const string PendingOperation = "(waits for a later story)";

    /// <summary>Rows whose operations arrive with a later story of the Jira parity pass, and the task that fills them.</summary>
    private static readonly Dictionary<string, string> Pending = new(StringComparer.Ordinal)
    {
    };

    /// <summary>The operations that implement each row of the matrix.</summary>
    private static readonly Dictionary<string, (string Name, Operation Run)[]> Rows = new(StringComparer.Ordinal)
    {
        ["See the space, its summary, board, list, timeline, backlog, sprint reports, stages, drawers and member list"] =
        [
            ("open a board", (t, _) => t.Try<IBoardService, BoardView>(s => s.GetAsync("WEB", false, Ct))),
            ("open the list", (t, _) => t.Try<IWorkItemListService, WorkItemListView>(s => s.ListAsync("WEB", new WorkItemListQuery(), new DateOnly(2026, 9, 27), Ct))),
            ("open the timeline", (t, _) => t.Try<ITimelineService, TimelineView>(s => s.GetAsync("WEB", false, Ct))),
            ("open a task drawer", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s => s.GetAsync("WEB-1", Ct))),
            ("see the member list", (t, _) => t.Try<IProjectMemberService, TeamView>(s => s.GetTeamAsync("WEB", Ct))),
            ("open the backlog", (t, _) => t.Try<IBacklogService, BacklogView>(s => s.GetAsync("MOB", PageRequest.First, Ct))),
            ("open a sprint report", (t, _) => t.Try<ISprintService, SprintReport>(s => s.GetReportAsync("APP", 1, Ct))),
            ("open the stages", (t, _) => t.Try<IStageGateService, StagesView>(s => s.GetAsync("CBU", Ct))),
            ("open the summary", (t, _) => t.Try<ISpaceSummaryService, SpaceSummary>(s => s.GetAsync("WEB", new DateOnly(2026, 10, 7), Ct))),
        ],
        ["Reorder the backlog and move cards between the backlog and sprints"] =
        [
            ("reorder the backlog", (t, w) => t.Try<IBacklogService, BacklogRow>(s =>
                s.MoveAsync("MOB-2", BacklogTarget.ToBacklog, BacklogPlacement.AtTop, w.Scrum!.Card2Version, Ct))),
            ("move a card into a sprint", (t, w) => t.Try<IBacklogService, BacklogRow>(s =>
                s.MoveAsync("MOB-1", new BacklogTarget.Sprint(w.Scrum!.Planned.Id), BacklogPlacement.KeepRank, w.Scrum.Card1Version, Ct))),
        ],
        ["Create, rename, start, complete and delete sprints"] =
        [
            ("create a sprint", (t, _) => t.Try<ISprintService, SprintSection>(s => s.CreateAsync("MOB", Ct))),
            ("rename a sprint", (t, w) => t.Try<ISprintService, SprintSection>(s =>
                s.UpdateAsync(w.Scrum!.Planned.Id, "Refunds sprint", "Ship refunds", w.Scrum.Planned.Version, Ct))),
            ("start a sprint", (t, w) => t.Try<ISprintService, SprintSection>(s =>
                s.StartAsync(w.Scrum!.OtherPlanned.Id, new DateOnly(2026, 10, 5), null, null, w.Scrum.OtherPlanned.Version, Ct))),
            ("complete a sprint", (t, w) => t.Try<ISprintService, SprintSection>(s =>
                s.CompleteAsync(w.Scrum!.Active.Id, null, w.Scrum.Active.Version, Ct))),
            ("delete a sprint", (t, w) => t.Try<ISprintService>(s => s.DeleteAsync(w.Scrum!.Planned.Id, w.Scrum.Planned.Version, Ct))),
        ],
        ["Add, reorder and delete stages (from the stages page or the Create form)"] =
        [
            ("add a stage", (t, _) => t.Try<IStageGateService, StagesView>(s => s.AddStageAsync("CBU", "Transition", null, Ct))),
            ("reorder the stages", (t, w) => t.Try<IStageGateService, StagesView>(s => s.MoveStageAsync("CBU-4", -1, w.Gates!.ClosureVersion, Ct))),
            ("delete a stage", (t, _) => t.Try<IWorkItemService>(s => s.DeleteAsync("CBU-4", Ct))),
        ],
        ["Name gate approvers and set the approvals needed and the requester exclusion"] =
        [
            ("set up a gate", (t, w) => t.Try<IStageGateService, StageView>(s =>
                s.SetUpGateAsync("CBU-2", [w.Approver.Id], null, true, w.Gates!.PlanningGateVersion, Ct))),
        ],
        ["Request approval of the current stage's gate and withdraw the request"] =
        [
            ("request approval", (t, w) => t.Try<IStageGateService, StageView>(s => s.RequestApprovalAsync("CBU-1", w.Gates!.InitiationGateVersion, Ct))),
            ("withdraw the request", (t, _) => t.Try<IStageGateService, StageView>(s => s.WithdrawAsync("PMO-1", 1, Ct))),
        ],
        ["Approve a gate or send it back"] =
        [
            ("approve a gate", (t, _) => t.Try<IStageGateService, StageView>(s => s.DecideAsync("PMO-1", 1, GateDecisionKind.Approve, null, Ct))),
            ("send a gate back", (t, _) => t.Try<IStageGateService, StageView>(s =>
                s.DecideAsync("PMO-1", 1, GateDecisionKind.SendBack, "The budget is missing.", Ct))),
        ],
        ["Create a space from a template or the Blank template (becoming its first Space admin)"] =
        [
            ("create a project", async (t, _) =>
            {
                var error = await t.Try<IProjectService, string>(s => s.CreateAsync("Payments", "PAY", null, ProjectTemplate.Scrum, Ct));
                if (error is null)
                {
                    var created = (await t.CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("PAY", Ct))).ValueOrThrow();
                    Assert.True(created.CanManage, "The creator becomes the first Project Admin.");
                }

                return error;
            }),
            ("create a blank space", (t, _) => t.Try<IProjectService, string>(s => s.CreateBlankAsync("Data Platform", "DATA", null,
                new SpaceSetup([new NewColumn("To do", StatusCategory.ToDo), new NewColumn("Done", StatusCategory.Done)], WorkTypeSet.Task), Ct))),
        ],
        ["Turn a space's work types on and off"] =
        [
            ("turn off bugs", (t, _) => t.Try<IProjectService, ProjectDetails>(s =>
                s.SetWorkTypesAsync("WEB", WorkTypeSet.Epic | WorkTypeSet.Story | WorkTypeSet.Task, 1, Ct))), // WEB has an epic, no bugs
        ],
        ["Edit space name and description"] =
        [
            ("edit project details", (t, _) => t.Try<IProjectService, ProjectDetails>(s => s.UpdateDetailsAsync("WEB", "Website", "New text", 1, Ct))),
        ],
        ["Switch a space between Kanban and Scrum"] =
        [
            ("switch to Scrum", (t, _) => t.Try<IProjectService, ProjectDetails>(s => s.SwitchTemplateAsync("WEB", ProjectTemplate.Scrum, 1, Ct))),
        ],
        ["Add, rename, reorder, limit, retype or delete board columns (on the board or in space settings)"] =
        [
            ("add a column", (t, w) => t.Columns(s => s.AddAsync("WEB", "QA", StatusCategory.InProgress, 2, w.BoardVersion, Ct))),
            ("rename a column", (t, w) => t.Columns(s => s.RenameAsync("WEB", w.InProgress, "Doing", w.BoardVersion, Ct))),
            ("reorder the columns", (t, w) => t.Columns(s => s.MoveAsync("WEB", w.Done, 0, w.BoardVersion, Ct))),
            ("set a limit", (t, w) => t.Columns(s => s.SetWipLimitAsync("WEB", w.InProgress, 3, w.BoardVersion, Ct))),
            ("retype a column", (t, w) => t.Columns(s => s.ChangeCategoryAsync("WEB", w.InProgress, StatusCategory.ToDo, w.BoardVersion, Ct))),
            ("delete a column", (t, w) => t.Columns(s => s.DeleteAsync("WEB", w.InProgress, null, w.BoardVersion, Ct))),
        ],
        ["Add members, change their roles and remove them"] =
        [
            ("add a member", (t, w) => t.Try<IProjectMemberService, TeamView>(s => s.AddAsync("WEB", w.Newcomer.Id, ProjectRole.Member, w.MembersVersion, Ct))),
            ("change a member's role", (t, w) => t.Try<IProjectMemberService, TeamView>(s => s.ChangeRoleAsync("WEB", w.Other.Id, ProjectRole.Viewer, w.MembersVersion, Ct))),
            ("remove a member", (t, w) => t.Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", w.Other.Id, w.MembersVersion, Ct))),
        ],
        ["Create work items (cards, epics, milestones) and sub-tasks, inline or from the Create form"] =
        [
            ("create a task", (t, w) => t.Try<IBoardService, CardView>(s => s.CreateInlineAsync("WEB", w.ToDo, "New task", WorkItemType.Task, Ct))),
            ("create a sub-task", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s => s.AddSubtaskAsync("WEB-1", "New sub-task", Ct))),
            ("create an epic", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s => s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Epic, "Statements"), Ct))),
            ("create a milestone", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s =>
                s.CreateAsync("CBU", new NewWorkItem(WorkItemType.Milestone, "Planning sign-off", "CBU-2"), Ct))),
            ("create a story with every field from the Create form", (t, w) => t.Try<IWorkItemService, WorkItemDetails>(s =>
                s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Story, "Refund to card") { StatusId = w.InProgress, Priority = Domain.Work.Priority.High, Labels = ["payments"] }, Ct))),
            ("create a sub-task from the Create form", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s =>
                s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Subtask, "Call the hotel") { ParentKey = "WEB-1" }, Ct))),
        ],
        ["Get the Create form's spaces, options and parents"] =
        [
            ("get the space's options", (t, _) => t.Try<ICreateWorkService, CreateOptions>(s => s.OptionsAsync("WEB", Ct))),
            ("find parents", (t, _) => t.Try<ICreateWorkService, IReadOnlyList<ParentOption>>(s => s.FindParentsAsync("WEB", WorkItemType.Subtask, "", Ct))),
        ],
        ["Edit title, description, priority, status, type, estimate, epic or stage, and an epic's color; move and reorder cards"] =
        [
            ("edit the title", (t, w) => t.Edit(new WorkItemEdit.Title("Design the landing page"), w.Task1Version)),
            ("edit the description", (t, w) => t.Edit(new WorkItemEdit.Description(Documents.Text("Hero first.")), w.Task1Version)),
            ("edit the priority", (t, w) => t.Edit(new WorkItemEdit.Priority(Domain.Work.Priority.High), w.Task1Version)),
            ("edit the status", (t, w) => t.Edit(new WorkItemEdit.Status(w.InProgress), w.Task1Version)),
            ("change the type", (t, w) => t.Edit(new WorkItemEdit.Type(WorkItemType.Story), w.Task1Version)),
            ("set the estimate", (t, w) => t.Edit(new WorkItemEdit.Estimate(3), w.Task1Version)),
            ("put it in an epic", (t, w) => t.Edit(new WorkItemEdit.Container(w.EpicKey), w.Task1Version)),
            ("move a card", (t, w) => t.Try<IBoardService, CardView>(s => s.MoveCardAsync("WEB-1", w.InProgress, CardPlacement.AtEnd, w.Task1Version, null, Ct))),
            ("reorder a card", (t, w) => t.Try<IBoardService, CardView>(s => s.MoveCardAsync("WEB-2", w.ToDo, CardPlacement.AtTop, w.Task2Version, null, Ct))),
            ("change an epic's color", async (t, w) =>
            {
                var epic = await t.CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync(w.EpicKey, Ct));
                return epic.Error ?? await t.Try<IWorkItemService, WorkItemDetails>(s =>
                    s.UpdateAsync(w.EpicKey, new WorkItemEdit.Color(EpicColor.Teal), epic.Value!.Version, Ct));
            }),
        ],
        ["Move a card into another epic's, stage's or person's lane on the board"] =
        [
            ("move a card into an epic's lane", (t, w) => t.Try<IBoardService, CardView>(s =>
                s.MoveCardAsync("WEB-1", w.ToDo, CardPlacement.AtEnd, w.Task1Version, new LaneChange.Container(w.EpicKey), Ct))),
            ("move a card into a person's lane", (t, w) => t.Try<IBoardService, CardView>(s =>
                s.MoveCardAsync("WEB-2", w.InProgress, CardPlacement.AtEnd, w.Task2Version, new LaneChange.Assignee(w.Other.Id), Ct))),
        ],
        ["Assign work items and set their dates (drawer or timeline)"] =
        [
            ("assign a task", (t, w) => t.Edit(new WorkItemEdit.Assignee(w.Other.Id), w.Task1Version)),
            ("set a task's dates", (t, w) => t.Edit(new WorkItemEdit.Dates(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 10)), w.Task1Version)),
            ("reschedule on the timeline", (t, w) => t.Try<ITimelineService, TimelineItem>(s =>
                s.RescheduleAsync("WEB-1", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 12), w.Task1Version, Ct))),
        ],
        ["Add and remove dependencies"] =
        [
            ("add a dependency", (t, _) => t.Try<IDependencyService, DependencyList>(s => s.AddAsync("WEB-2", "WEB-1", Ct))),
            ("remove a dependency", (t, _) => t.Try<IDependencyService, DependencyList>(s => s.RemoveAsync("WEB-1", "WEB-4", Ct))),
        ],
        ["Add comments"] =
        [
            ("add a comment", (t, _) => t.Try<ICommentService, CommentView>(s => s.AddAsync("WEB-1", Documents.Text("Looks good"), Ct))),
        ],
        ["Edit or delete a comment"] =
        [
            ("edit the comment", (t, w) => t.Try<ICommentService, CommentView>(s => s.EditAsync(w.CommentId, Documents.Text("Second thoughts"), w.CommentVersion, Ct))),
            ("delete the comment", (t, w) => t.Try<ICommentService>(s => s.DeleteAsync(w.CommentId, Ct))),
        ],
        ["Find people to mention"] =
        [
            ("find people to mention", (t, _) => t.Try<IMentionService, IReadOnlyList<PersonView>>(s => s.SuggestAsync("WEB-1", "u", Ct))),
            ("find people to mention in a new item", (t, _) => t.Try<IMentionService, IReadOnlyList<PersonView>>(s => s.SuggestInSpaceAsync("WEB", "u", Ct))),
        ],
        ["Watch and stop watching an item, and see its watchers"] =
        [
            ("watch an item", (t, _) => t.Try<IWatcherService, WatchersView>(s => s.WatchAsync("WEB-1", Ct))),
            ("stop watching an item", (t, _) => t.Try<IWatcherService, WatchersView>(s => s.StopWatchingAsync("WEB-1", Ct))),
            ("see the watchers", (t, _) => t.Try<IWatcherService, WatchersView>(s => s.GetAsync("WEB-1", Ct))),
        ],
        ["Add and remove other people as watchers"] =
        [
            ("add a watcher", (t, w) => t.Try<IWatcherService, WatchersView>(s => s.AddAsync("WEB-1", w.Other.Id, Ct))),
            ("remove a watcher", (t, w) => t.Try<IWatcherService, WatchersView>(s => s.RemoveAsync("WEB-1", w.Creator.Id, Ct))),
        ],
        ["See, open and mark one's own notifications"] =
        [
            ("see own notifications", (t, _) => t.Try<INotificationService, Page<NotificationView>>(s => s.ListAsync(PageRequest.First, Ct))),
            ("mark own notifications as read", (t, _) => t.Try<INotificationService>(s => s.MarkAllReadAsync(Ct))),
        ],
        ["Attach files to a work item"] =
        [
            ("attach a file", (t, _) => t.Try<IAttachmentService, AttachmentView>(s =>
                s.AddAsync("WEB-1", "notes.txt", 5, new MemoryStream("Notes"u8.ToArray()), Ct))),
        ],
        ["Download and preview attachments that passed the check"] =
        [
            ("download a file", (t, w) => t.Try<IAttachmentService, AttachmentFile>(s => s.OpenAsync(w.FileId, false, Ct))),
            ("preview an image", (t, w) => t.Try<IAttachmentService, AttachmentFile>(s => s.OpenAsync(w.ImageId, true, Ct))),
        ],
        ["Remove an attachment"] =
        [
            ("remove a file", (t, w) => t.Try<IAttachmentService>(s => s.RemoveAsync(w.FileId, Ct))),
        ],
        ["Delete an attachment permanently"] =
        [
            ("delete a file permanently", (t, w) => t.Try<IAttachmentService>(s => s.PurgeAsync(w.FileId, Ct))),
        ],
        ["Add and remove labels, and get label suggestions"] =
        [
            ("add a label", (t, _) => t.Try<ILabelService, IReadOnlyList<string>>(s => s.AddAsync("WEB-1", "launch", Ct))),
            ("remove a label", (t, _) => t.Try<ILabelService, IReadOnlyList<string>>(s => s.RemoveAsync("WEB-1", "design", Ct))),
            ("get label suggestions", (t, _) => t.Try<ILabelService, IReadOnlyList<string>>(s => s.SuggestAsync("WEB", "de", Ct))),
        ],
        ["Share a saved filter with the space"] =
        [
            ("share own filter with WEB", (t, _) => t.OwnFilterAsync(f => t.Try<ISavedFilterService, SavedFilterView>(s => s.ShareAsync(f.Id, ["WEB"], Ct)))),
        ],
        ["Run a saved filter shared with the space"] =
        [
            ("run a filter shared with WEB", async (t, w) =>
                await t.Try<ISavedFilterService, SavedFilterView>(s => s.GetAsync(w.SharedFilterId, Ct))
                ?? await t.Try<ISearchService, SearchResults>(s => s.SearchAsync(new SearchQuery(null, ["WEB"]), PageRequest.First, Ct))),
        ],
        ["Search, open work items by key, and see recently viewed items (only in spaces one can see)"] =
        [
            ("search for words", (t, _) => t.Try<ISearchService, SearchResults>(s => s.SearchAsync(new SearchQuery("design"), PageRequest.First, Ct))),
            ("see recently viewed items", (t, _) => t.Try<ISearchService, IReadOnlyList<SearchResultRow>>(s => s.RecentAsync(Ct))),
            ("jump to a key", async (t, _) =>
            {
                // Anyone signed in may jump; a key they cannot see is "not found", like one that does not exist, which is
                // an answer rather than a refusal (what each person finds is Rule8's test).
                var error = await t.Try<ISearchService, string>(s => s.ResolveKeyAsync("WEB-1", Ct));
                return error?.Code == ErrorCodes.NotFound ? null : error;
            }),
        ],
        ["Save, run, rename, update and delete one's own filters"] =
        [
            ("save a filter", (t, _) => t.Try<ISavedFilterService, SavedFilterView>(s => s.SaveAsync("My open work", "assignee=me", Ct))),
            ("run own filter", (t, _) => t.OwnFilterAsync(f => t.Try<ISavedFilterService, SavedFilterView>(s => s.GetAsync(f.Id, Ct)))),
            ("rename own filter", (t, _) => t.OwnFilterAsync(f => t.Try<ISavedFilterService, SavedFilterView>(s => s.RenameAsync(f.Id, "Renamed", f.Version, Ct)))),
            ("update own filter", (t, _) => t.OwnFilterAsync(f => t.Try<ISavedFilterService, SavedFilterView>(s => s.UpdateAsync(f.Id, "q=copy", f.Version, Ct)))),
            ("delete own filter", (t, _) => t.OwnFilterAsync(f => t.Try<ISavedFilterService>(s => s.DeleteAsync(f.Id, Ct)))),
        ],
        ["Delete a work item other than a stage (with its sub-tasks)"] =
        [
            ("delete the task", (t, _) => t.Try<IWorkItemService>(s => s.DeleteAsync("WEB-1", Ct))),
        ],
        ["List archived work items"] =
        [
            ("list deleted tasks", (t, _) => t.Try<IWorkItemService, Page<DeletedItemView>>(s => s.ListDeletedAsync("WEB", PageRequest.First, Ct))),
        ],
        ["Restore archived work items"] =
        [
            ("restore a deleted task", (t, _) => t.Try<IWorkItemService>(s => s.RestoreAsync("WEB-3", Ct))),
        ],
        ["See \"For you\" (worked on, viewed, assigned to me with gates waiting, starred) and the sidebar"] =
        [
            ("see my tasks", (t, _) => t.Try<IMyTasksService, Page<MyTaskRow>>(s => s.ListAsync(PageRequest.First, Ct))),
            ("see what I worked on", (t, _) => t.Try<ISearchService, Page<SearchResultRow>>(s => s.WorkedOnAsync(PageRequest.First, Ct))),
            ("see what I viewed", (t, _) => t.Try<ISearchService, IReadOnlyList<SearchResultRow>>(s => s.RecentAsync(Ct))),
            ("see my starred spaces", (t, _) => t.Try<ISpaceNavigationService, IReadOnlyList<SpaceLink>>(s => s.StarredAsync(Ct))),
            ("see the sidebar", (t, _) => t.Try<ISpaceNavigationService, SidebarView>(s => s.GetSidebarAsync(Ct))),
        ],
        ["Star and unstar the space, and have it recorded as opened"] =
        [
            ("star the space", (t, _) => t.Try<ISpaceNavigationService>(s => s.StarAsync("WEB", Ct))),
            ("unstar the space", (t, _) => t.Try<ISpaceNavigationService>(s => s.UnstarAsync("WEB", Ct))),
            ("record the space as opened", (t, _) => t.Try<ISpaceNavigationService>(s => s.RecordVisitAsync("WEB", Ct))),
        ],
        ["Manage accounts (list, add, reset password, deactivate, reactivate)"] =
        [
            ("list accounts", (t, _) => t.Try<IUserAdminService, Page<UserSummary>>(s => s.ListUsersAsync(null, PageRequest.First, Ct))),
            ("add an account", (t, _) => t.Try<IUserAdminService, CreatedUser>(s => s.AddUserAsync("newbie", "New Colleague", "newbie@example.com", Ct))),
            ("reset a password", (t, w) => t.Try<IUserAdminService, string>(s => s.ResetPasswordAsync(w.Other.Id, Ct))),
            ("deactivate an account", (t, w) => t.Try<IUserAdminService>(s => s.DeactivateAsync(w.Other.Id, Ct))),
            ("reactivate an account", (t, w) => t.Try<IUserAdminService>(s => s.ReactivateAsync(w.Deactivated.Id, Ct))),
        ],
        ["Give or remove the Administrator role"] =
        [
            ("make someone an administrator", (t, w) => t.Try<IUserAdminService>(s => s.ChangeRoleAsync(w.Other.Id, OrganizationRole.Administrator, Ct))),
            ("remove the Administrator role", (t, w) => t.Try<IUserAdminService>(s => s.ChangeRoleAsync(w.SecondAdmin.Id, OrganizationRole.User, Ct))),
        ],
        ["Choose one's own email choice and sidebar layout"] =
        [
            ("choose own email choice", (t, _) => t.Try<IAccountService, MyProfile>(s => s.SetEmailChoiceAsync(EmailChoice.All, Ct))),
            ("collapse own sidebar", (t, _) => t.Try<IAccountService, MyProfile>(s => s.SetSidebarCollapsedAsync(true, Ct))),
        ],
        ["Change own password, display name and time zone"] =
        [
            ("change own password", (t, _) => t.Try<IAccountService>(s => s.ChangePasswordAsync(TestData.DefaultPassword, NewPassword, Ct))),
            ("change own display name and time zone", (t, _) => t.Try<IAccountService>(s => s.UpdateProfileAsync("New Name", "Asia/Karachi", Ct))),
        ],
    };

    /// <summary>The operations that need the Scrum projects of <see cref="ScrumWorld"/>; the other cells skip building them.</summary>
    private static readonly HashSet<string> ScrumOperations = new(StringComparer.Ordinal)
    {
        "open the backlog", "open a sprint report", "reorder the backlog", "move a card into a sprint", "create a sprint",
        "rename a sprint", "start a sprint", "complete a sprint", "delete a sprint",
    };

    /// <summary>The operations that need the gates of <see cref="GateWorld"/>.</summary>
    private static readonly HashSet<string> GateOperations = new(StringComparer.Ordinal)
    {
        "reorder the stages", "set up a gate", "request approval", "withdraw the request", "approve a gate", "send a gate back",
    };

    private delegate Task<AppError?> Operation(PermissionMatrixTests test, World world);

    /// <summary>(row, operation, role, cell) for every cell of the document's table.</summary>
    public static TheoryData<string, string, Role, string> Cells()
    {
        var data = new TheoryData<string, string, Role, string>();
        foreach (var (action, cells) in ReadMatrix())
        {
            if (Pending.ContainsKey(action))
            {
                Assert.False(Rows.ContainsKey(action), $"The row \"{action}\" has operations; take it out of Pending.");
                data.Add(action, PendingOperation, Role.Admin, cells[(int)Role.Admin]);
                continue;
            }

            Assert.True(Rows.TryGetValue(action, out var operations), $"No test operations for the permissions.md row \"{action}\".");
            foreach (var name in operations.Select(o => o.Name))
            {
                foreach (var role in Enum.GetValues<Role>())
                {
                    data.Add(action, name, role, cells[(int)role]);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task P4_SC004_Every_cell_of_the_permission_matrix_is_enforced(string action, string operation, Role role, string cell)
    {
        if (operation == PendingOperation)
        {
            Assert.Skip($"The row \"{action}\" is filled by {Pending[action]}.");
        }

        var world = await SetUpAsync(scrum: ScrumOperations.Contains(operation), gates: GateOperations.Contains(operation));
        ActAs(world.Caller(role));

        var error = await Rows[action].Single(o => o.Name == operation).Run(this, world);

        if (cell.StartsWith('✅'))
        {
            Assert.True(error is null, $"{role} should be allowed to {operation}, but got {error?.Kind}: {error?.Message}");
        }
        else if (cell.StartsWith("🚫", StringComparison.Ordinal))
        {
            Assert.True(error?.Kind == ErrorKind.NotFound, $"{role} should be told the project does not exist when trying to {operation}, but got {error?.Kind.ToString() ?? "success"}.");
        }
        else
        {
            Assert.True(error is not null, $"{role} should be refused to {operation}, but it succeeded.");
            // Only the author may change a comment: other contributors, even Project Admins and Administrators, get
            // CommentNotOwned; Viewers are refused before that, as they cannot contribute at all.
            var refusedAsNotOwner = action == "Edit or delete a comment" && role is Role.Member or Role.ProjectAdmin or Role.Admin;

            // Every refused download is "not found", so nobody learns whether a file exists (Phase 4 rule 3).
            var refusedAsNotFound = action == "Download and preview attachments that passed the check";
            Assert.Equal(refusedAsNotOwner ? ErrorCodes.CommentNotOwned : refusedAsNotFound ? ErrorCodes.NotFound : ErrorCodes.Forbidden, error.Code);
        }
    }

    [Theory]
    [InlineData("open a board")]
    [InlineData("create a task")]
    [InlineData("add a comment")]
    [InlineData("see the member list")]
    [InlineData("change own display name and time zone")]
    public async Task Rule1_A_deactivated_user_is_refused_everything(string operation)
    {
        var world = await SetUpAsync();
        ActAs(world.Deactivated);

        var error = await Rows.Values.SelectMany(o => o).Single(o => o.Name == operation).Run(this, world);

        Assert.Equal(ErrorKind.Forbidden, error?.Kind);
    }

    [Fact]
    public async Task Rule3_Unknown_or_deleted_projects_tasks_and_comments_are_not_found()
    {
        var world = await SetUpAsync();
        ActAs(world.Owner);

        Assert.Equal(ErrorKind.NotFound, (await Try<IBoardService, BoardView>(s => s.GetAsync("NOPE", false, Ct)))?.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Try<IWorkItemService, WorkItemDetails>(s => s.GetAsync("WEB-99", Ct)))?.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Try<IWorkItemService, WorkItemDetails>(s => s.GetAsync("WEB-3", Ct)))?.Kind); // deleted
        Assert.Equal(ErrorKind.NotFound, (await Try<ICommentService, CommentView>(s => s.EditAsync(999_999, "x", [1], Ct)))?.Kind);
    }

    [Fact]
    public async Task Rule4_The_last_active_Project_Admin_can_neither_leave_nor_step_down()
    {
        var world = await SetUpAsync();
        ActAs(world.Owner);

        Assert.Equal(ErrorCodes.LastProjectAdmin, (await Try<IProjectMemberService, TeamView>(s =>
            s.RemoveAsync("WEB", world.Owner.Id, world.MembersVersion, Ct)))?.Code);
        Assert.Equal(ErrorCodes.LastProjectAdmin, (await Try<IProjectMemberService, TeamView>(s =>
            s.ChangeRoleAsync("WEB", world.Owner.Id, ProjectRole.Member, world.MembersVersion, Ct)))?.Code);
    }

    [Fact]
    public async Task Rule6_A_removal_applies_to_the_persons_next_request()
    {
        var world = await SetUpAsync();
        ActAs(world.Other);
        Assert.Null(await Try<IBoardService, BoardView>(s => s.GetAsync("WEB", false, Ct)));

        ActAs(world.Owner);
        Assert.Null(await Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", world.Other.Id, world.MembersVersion, Ct)));

        ActAs(world.Other);
        Assert.Equal(ErrorKind.NotFound, (await Try<IBoardService, BoardView>(s => s.GetAsync("WEB", false, Ct)))?.Kind);
        Assert.Equal(ErrorKind.NotFound, (await Try<IWorkItemService, WorkItemDetails>(s => s.GetAsync("WEB-1", Ct)))?.Kind);
    }

    [Fact]
    public async Task Rule5_Only_active_Project_Admins_and_Members_can_be_assigned_and_stay_assigned_after_leaving()
    {
        var world = await SetUpAsync();
        ActAs(world.Owner);

        foreach (var person in new[] { world.Viewer, world.Outsider, world.Deactivated, world.Admin })
        {
            Assert.Equal(ErrorCodes.NotAssignable, (await Edit(new WorkItemEdit.Assignee(person.Id), world.Task1Version))?.Code);
        }

        Assert.Null(await Edit(new WorkItemEdit.Assignee(world.Other.Id), world.Task1Version));
        Assert.Null(await Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", world.Other.Id, world.MembersVersion, Ct)));
        var details = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync("WEB-1", Ct))).ValueOrThrow();
        Assert.Equal((world.Other.Id, false), (details.Assignee!.UserId, details.Assignee.CanWork));
    }

    [Fact]
    public async Task Rule7_My_tasks_lists_only_projects_the_caller_can_still_see()
    {
        var world = await SetUpAsync();
        ActAs(world.Owner);
        Assert.Null(await Edit(new WorkItemEdit.Assignee(world.Other.Id), world.Task1Version));

        ActAs(world.Other);
        Assert.Equal(["WEB-1"], (await CallAsync<IMyTasksService, Result<Page<MyTaskRow>>>(s => s.ListAsync(PageRequest.First, Ct))).ValueOrThrow().Items.Select(r => r.Key));

        ActAs(world.Owner);
        Assert.Null(await Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", world.Other.Id, world.MembersVersion, Ct)));

        ActAs(world.Other);
        Assert.Empty((await CallAsync<IMyTasksService, Result<Page<MyTaskRow>>>(s => s.ListAsync(PageRequest.First, Ct))).ValueOrThrow().Items);
    }

    [Fact]
    public async Task P4_Rule7_Only_the_owner_changes_a_filter_and_sharing_lasts_while_the_owner_is_a_member()
    {
        var world = await SetUpAsync();
        foreach (var someoneElse in new[] { world.Owner, world.Admin })
        {
            ActAs(someoneElse);
            var shared = (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.GetAsync(world.SharedFilterId, Ct))).ValueOrThrow();
            Assert.Equal(ErrorKind.Forbidden, (await Try<ISavedFilterService, SavedFilterView>(s =>
                s.RenameAsync(world.SharedFilterId, "Taken over", shared.Version, Ct)))?.Kind);
            Assert.Equal(ErrorKind.Forbidden, (await Try<ISavedFilterService>(s => s.DeleteAsync(world.SharedFilterId, Ct)))?.Kind);
        }

        ActAs(world.Owner);
        Assert.Null(await Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", world.Creator.Id, world.MembersVersion, Ct)));

        ActAs(world.Other);
        Assert.Equal(ErrorKind.NotFound, (await Try<ISavedFilterService, SavedFilterView>(s => s.GetAsync(world.SharedFilterId, Ct)))?.Kind);
    }

    [Fact]
    public async Task P4_Rule8_Search_finds_only_what_the_caller_can_see()
    {
        var world = await SetUpAsync();
        ActAs(world.Outsider);
        Assert.Equal("No work item WEB-1 was found.", (await Try<ISearchService, string>(s => s.ResolveKeyAsync("web-1", Ct)))?.Message);
        Assert.Equal("No work item WEB-99 was found.", (await Try<ISearchService, string>(s => s.ResolveKeyAsync("WEB-99", Ct)))?.Message);
        Assert.Empty((await CallAsync<ISearchService, Result<SearchResults>>(s =>
            s.SearchAsync(new SearchQuery("design"), PageRequest.First, Ct))).ValueOrThrow().Rows.Items);

        foreach (var reader in new[] { world.Viewer, world.Admin })
        {
            ActAs(reader);
            Assert.Equal("WEB-1", (await CallAsync<ISearchService, Result<string>>(s => s.ResolveKeyAsync("web-1", Ct))).ValueOrThrow());
            Assert.Equal(["WEB-1"], (await CallAsync<ISearchService, Result<SearchResults>>(s =>
                s.SearchAsync(new SearchQuery("design"), PageRequest.First, Ct))).ValueOrThrow().Rows.Items.Select(r => r.Key));
        }

        ActAs(world.Owner);
        Assert.Null(await Try<IProjectMemberService, TeamView>(s => s.RemoveAsync("WEB", world.Viewer.Id, world.MembersVersion, Ct)));

        ActAs(world.Viewer);
        Assert.Equal(ErrorCodes.NotFound, (await Try<ISearchService, string>(s => s.ResolveKeyAsync("WEB-1", Ct)))?.Code);
    }

    private static IEnumerable<(string Action, string[] Cells)> ReadMatrix()
    {
        var lines = File.ReadAllLines(PermissionsDocument());
        var header = Array.FindIndex(lines, l => l.StartsWith("| Action |", StringComparison.Ordinal));
        Assert.True(header >= 0, "The permission table was not found in permissions.md.");
        var columns = Split(lines[header]);
        Assert.Equal(["Action", "Anonymous", "Non-member", "Viewer", "Approver", "Member", "Creator", "Space admin", "Admin"],
            columns.Select(c => c.Replace("*", "", StringComparison.Ordinal)));
        foreach (var line in lines.Skip(header + 2).TakeWhile(l => l.StartsWith('|')))
        {
            var cells = Split(line);
            yield return (cells[0], cells[1..]);
        }
    }

    private static string[] Split(string line) => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static string PermissionsDocument()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "specs", "006-jira-parity", "contracts", "permissions.md");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("specs/006-jira-parity/contracts/permissions.md was not found above the test output.");
    }

    /// <summary>Project Admin "owen" created the Kanban project WEB and the Stage-gate project CBU (stages CBU-1 to CBU-4)
    /// with the same team: Member "cara" created WEB-1, WEB-2, a comment, two checked attachments and the label
    /// "design" on WEB-1, and shares a saved filter with WEB; "uma" is another Member; "vera" is a Viewer and "abe" a Viewer who approves gates;
    /// "nora" is not a member; owen created the epic WEB-4; the Administrator "ada" (not a member) deleted WEB-3; "grace"
    /// is a second Administrator; "dora" is a deactivated Member; "nick" can be added to the team. With <paramref name="scrum"/>, owen also has the Scrum projects of
    /// <see cref="ScrumWorld"/> with the same team.</summary>
    private async Task<World> SetUpAsync(bool scrum = false, bool gates = false)
    {
        var owner = await Data.UserAsync("owen");
        var creator = await Data.UserAsync("cara");
        var other = await Data.UserAsync("uma");
        var viewer = await Data.UserAsync("vera");
        var approver = await Data.UserAsync("abe");
        var outsider = await Data.UserAsync("nora");
        var newcomer = await Data.UserAsync("nick");
        var admin = await Data.AdministratorAsync("ada");
        var secondAdmin = await Data.AdministratorAsync("grace");
        var deactivated = await Data.UserAsync("dora", isActive: false);

        ActAs(owner);
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Website Revamp", "WEB", null, ProjectTemplate.Kanban, Ct))).ValueOrThrow();
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Core Banking Upgrade", "CBU", null, ProjectTemplate.StageGate, Ct))).ValueOrThrow();
        foreach (var key in new[] { "WEB", "CBU" })
        {
            await Data.MembersAsync(key, ProjectRole.Member, creator, other, deactivated);
            await Data.MembersAsync(key, ProjectRole.Viewer, viewer, approver);
        }

        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();
        var (toDo, inProgress, done) = (board.Columns[0].Id, board.Columns[1].Id, board.Columns[2].Id);
        var membersVersion = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct))).ValueOrThrow().MembersVersion;

        ActAs(creator);
        foreach (var title in new[] { "Design the home page", "Write the copy", "Old idea" })
        {
            (await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", toDo, title, WorkItemType.Task, Ct))).ValueOrThrow();
        }

        var comment = (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Documents.Text("First thoughts"), Ct))).ValueOrThrow();

        // cara attaches a document and an image to WEB-1; both have passed their malware check (Phase 4).
        var fileId = (await CallAsync<IAttachmentService, Result<AttachmentView>>(s =>
            s.AddAsync("WEB-1", "brief.pdf", 9, new MemoryStream("%PDF-1.7\n"u8.ToArray()), Ct))).ValueOrThrow().Id;
        var imageId = (await CallAsync<IAttachmentService, Result<AttachmentView>>(s =>
            s.AddAsync("WEB-1", "shot.png", 8, new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), Ct))).ValueOrThrow().Id;
        await using (var scope = Harness.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            foreach (var attachment in await db.Attachments.ToListAsync(Ct))
            {
                attachment.Pass(Harness.Time.GetUtcNow());
            }

            await db.SaveChangesAsync(Ct);
        }

        // cara labels WEB-1 "design" (Phase 4).
        (await CallAsync<ILabelService, Result<IReadOnlyList<string>>>(s => s.AddAsync("WEB-1", "design", Ct))).ValueOrThrow();

        // cara shares a filter with WEB (Phase 4).
        var sharedFilter = (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.SaveAsync("Open in WEB", "project=WEB", Ct))).ValueOrThrow();
        (await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.ShareAsync(sharedFilter.Id, ["WEB"], Ct))).ValueOrThrow();

        ActAs(owner);
        var epic = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("WEB", new NewWorkItem(WorkItemType.Epic, "Card refunds"), Ct))).ValueOrThrow();
        (await CallAsync<IDependencyService, Result<DependencyList>>(s => s.AddAsync("WEB-1", epic.Key, Ct))).ValueOrThrow();

        var scrumWorld = scrum ? await ScrumWorldAsync(creator, other, deactivated, viewer, approver) : null;
        var gateWorld = gates ? await GateWorldAsync(creator, other, deactivated, viewer, approver) : null;

        ActAs(admin);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-3", Ct))).IsSuccess);
        var task1 = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync("WEB-1", Ct))).ValueOrThrow();
        var task2 = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync("WEB-2", Ct))).ValueOrThrow();
        ActAs(null);
        return new World(owner, creator, other, viewer, outsider, newcomer, admin, secondAdmin, deactivated, board.BoardVersion,
            membersVersion, toDo, inProgress, done, task1.Version, task2.Version, comment.Id, comment.Version, approver, epic.Key, fileId, imageId,
            sharedFilter.Id)
        {
            Scrum = scrumWorld,
            Gates = gateWorld,
        };
    }

    /// <summary>Gates in owen's Stage-gate projects, with WEB's team: CBU-1's gate has abe (a Viewer) as its approver and
    /// can be requested; PMO-1's gate has abe as its approver and owen's request 1 pending. Called as owen.</summary>
    private async Task<GateWorld> GateWorldAsync(User creator, User other, User deactivated, User viewer, User approver)
    {
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("PMO Office", "PMO", null, ProjectTemplate.StageGate, Ct))).ValueOrThrow();
        await Data.MembersAsync("PMO", ProjectRole.Member, creator, other, deactivated);
        await Data.MembersAsync("PMO", ProjectRole.Viewer, viewer, approver);

        async Task<StageView> StageAsync(string projectKey, string stageKey) =>
            (await CallAsync<IStageGateService, Result<StagesView>>(s => s.GetAsync(projectKey, Ct))).ValueOrThrow().Stages.Single(s => s.Key == stageKey);

        async Task<StageView> SetUpAsync(string projectKey, string stageKey)
        {
            var version = (await StageAsync(projectKey, stageKey)).Gate.Version;
            return (await CallAsync<IStageGateService, Result<StageView>>(s =>
                s.SetUpGateAsync(stageKey, [approver.Id], null, true, version, Ct))).ValueOrThrow();
        }

        var initiation = await SetUpAsync("CBU", "CBU-1");
        var pmo = await SetUpAsync("PMO", "PMO-1");
        (await CallAsync<IStageGateService, Result<StageView>>(s => s.RequestApprovalAsync("PMO-1", pmo.Gate.Version, Ct))).ValueOrThrow();
        var planning = await StageAsync("CBU", "CBU-2");
        var closure = await StageAsync("CBU", "CBU-4");
        return new GateWorld(initiation.Gate.Version, planning.Gate.Version, closure.StageVersion);
    }

    /// <summary>Owen's Scrum projects, with WEB's team: MOB with the backlog cards MOB-1 and MOB-2, the active sprint 1 and
    /// the planned sprint 2; APP with the completed sprint 1 (its report) and the planned sprint 2, which can start
    /// because nothing else in APP is active. Called as owen.</summary>
    private async Task<ScrumWorld> ScrumWorldAsync(User creator, User other, User deactivated, User viewer, User approver)
    {
        foreach (var (name, key) in new[] { ("Mobile App", "MOB"), ("Mobile Payments", "APP") })
        {
            (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync(name, key, null, ProjectTemplate.Scrum, Ct))).ValueOrThrow();
            await Data.MembersAsync(key, ProjectRole.Member, creator, other, deactivated);
            await Data.MembersAsync(key, ProjectRole.Viewer, viewer, approver);
        }

        async Task<byte[]> StoryAsync(string title) => (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s =>
            s.CreateAsync("MOB", new NewWorkItem(WorkItemType.Story, title), Ct))).ValueOrThrow().Version;

        async Task<SprintSection> SprintAsync(string key) =>
            (await CallAsync<ISprintService, Result<SprintSection>>(s => s.CreateAsync(key, Ct))).ValueOrThrow();

        async Task<SprintSection> StartAsync(SprintSection sprint) => (await CallAsync<ISprintService, Result<SprintSection>>(s =>
            s.StartAsync(sprint.Id, new DateOnly(2026, 9, 21), null, null, sprint.Version, Ct))).ValueOrThrow();

        var card1 = await StoryAsync("Refund to card");
        var card2 = await StoryAsync("Refund history");
        var active = await StartAsync(await SprintAsync("MOB"));
        var planned = await SprintAsync("MOB");
        var done = await StartAsync(await SprintAsync("APP"));
        (await CallAsync<ISprintService, Result<SprintSection>>(s => s.CompleteAsync(done.Id, null, done.Version, Ct))).ValueOrThrow();
        var otherPlanned = await SprintAsync("APP");
        return new ScrumWorld(card1, card2, (active.Id, active.Version), (planned.Id, planned.Version), (otherPlanned.Id, otherPlanned.Version));
    }

    private async Task<AppError?> Try<TService, TValue>(Func<TService, Task<Result<TValue>>> call)
        where TService : notnull => (await CallAsync(call)).Error;

    private async Task<AppError?> Try<TService>(Func<TService, Task<Result>> call)
        where TService : notnull => (await CallAsync(call)).Error;

    private Task<AppError?> Columns(Func<IBoardColumnService, Task<Result<BoardColumnsView>>> call) => Try(call);

    private Task<AppError?> Edit(WorkItemEdit edit, byte[] version) =>
        Try<IWorkItemService, WorkItemDetails>(s => s.UpdateAsync("WEB-1", edit, version, Ct));

    /// <summary>Saves a filter of the caller's own, then changes it; a caller who cannot save is refused there.</summary>
    private async Task<AppError?> OwnFilterAsync(Func<SavedFilterView, Task<AppError?>> change)
    {
        var saved = await CallAsync<ISavedFilterService, Result<SavedFilterView>>(s => s.SaveAsync("My open work", "assignee=me", Ct));
        return saved.Error ?? await change(saved.Value!);
    }

    private sealed record World(User Owner, User Creator, User Other, User Viewer, User Outsider, User Newcomer, User Admin,
        User SecondAdmin, User Deactivated, int BoardVersion, int MembersVersion, long ToDo, long InProgress, long Done,
        byte[] Task1Version, byte[] Task2Version, long CommentId, byte[] CommentVersion, User Approver, string EpicKey, long FileId, long ImageId,
        long SharedFilterId)
    {
        public ScrumWorld? Scrum { get; init; }

        public GateWorld? Gates { get; init; }

        public User? Caller(Role role) => role switch
        {
            Role.NonMember => Outsider,
            Role.Viewer => Viewer,
            Role.Approver => Approver,
            Role.Member => Other,
            Role.Creator => Creator,
            Role.ProjectAdmin => Owner,
            Role.Admin => Admin,
            _ => null,
        };
    }

    private sealed record ScrumWorld(byte[] Card1Version, byte[] Card2Version, (long Id, byte[] Version) Active,
        (long Id, byte[] Version) Planned, (long Id, byte[] Version) OtherPlanned);

    private sealed record GateWorld(byte[] InitiationGateVersion, byte[] PlanningGateVersion, byte[] ClosureVersion);
}
