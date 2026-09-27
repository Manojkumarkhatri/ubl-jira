using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Projects;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Projects;

namespace Upms.Application.Tests.Security;

public enum Role
{
    Anonymous,
    NonMember,
    Viewer,
    Member,
    Creator,
    ProjectAdmin,
    Admin,
}

/// <summary>Every cell of <c>specs/003-project-views-and-team/contracts/permissions.md</c> (the Phase 2 matrix, which
/// supersedes Phase 1's), exercised against the real services (Phase 2 SC-003). The rows are read from the document
/// itself, so a row without operations here fails. The sign-in redirect (🔒) is the host's job and is tested in the web
/// tests; here an anonymous caller is refused.</summary>
public sealed class PermissionMatrixTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private const string NewPassword = "a brand new passphrase 77";

    /// <summary>Rows whose operations arrive with Phase 2 User Story 2 (tasks.md T048); their cells are skipped until then.</summary>
    private static readonly HashSet<string> PendingUntilUs2 = new(StringComparer.Ordinal)
    {
        "Assign tasks and set their dates (drawer or timeline)",
        "See \"My tasks\" (own open assigned tasks in projects they can see)",
    };

    private const string PendingOperation = "(arrives with US2)";

    /// <summary>The operations that implement each row of the matrix.</summary>
    private static readonly Dictionary<string, (string Name, Operation Run)[]> Rows = new(StringComparer.Ordinal)
    {
        ["See the project, its board, list, timeline, task drawers and member list"] =
        [
            ("open a board", (t, _) => t.Try<IBoardService, BoardView>(s => s.GetAsync("WEB", false, Ct))),
            ("open a task drawer", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s => s.GetAsync("WEB-1", Ct))),
            ("see the member list", (t, _) => t.Try<IProjectMemberService, TeamView>(s => s.GetTeamAsync("WEB", Ct))),
        ],
        ["Create a project (becoming its first Project Admin)"] =
        [
            ("create a project", async (t, _) =>
            {
                var error = await t.Try<IProjectService, string>(s => s.CreateAsync("Payments", "PAY", null, Ct));
                if (error is null)
                {
                    var created = (await t.CallAsync<IProjectService, Result<ProjectDetails>>(s => s.GetAsync("PAY", Ct))).ValueOrThrow();
                    Assert.True(created.CanManage, "The creator becomes the first Project Admin.");
                }

                return error;
            }),
        ],
        ["Edit project name and description"] =
        [
            ("edit project details", (t, _) => t.Try<IProjectService, ProjectDetails>(s => s.UpdateDetailsAsync("WEB", "Website", "New text", 1, Ct))),
        ],
        ["Add, rename, reorder, limit, retype or delete board columns"] =
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
        ["Create tasks (on the board or the list) and sub-tasks"] =
        [
            ("create a task", (t, w) => t.Try<IBoardService, CardView>(s => s.CreateInlineAsync("WEB", w.ToDo, "New task", Ct))),
            ("create a sub-task", (t, _) => t.Try<IWorkItemService, WorkItemDetails>(s => s.AddSubtaskAsync("WEB-1", "New sub-task", Ct))),
        ],
        ["Edit title, description, priority, status; move and reorder cards"] =
        [
            ("edit the title", (t, w) => t.Edit(new WorkItemEdit.Title("Design the landing page"), w.Task1Version)),
            ("edit the description", (t, w) => t.Edit(new WorkItemEdit.Description("Hero first."), w.Task1Version)),
            ("edit the priority", (t, w) => t.Edit(new WorkItemEdit.Priority(Domain.Work.Priority.High), w.Task1Version)),
            ("edit the status", (t, w) => t.Edit(new WorkItemEdit.Status(w.InProgress), w.Task1Version)),
            ("move a card", (t, w) => t.Try<IBoardService, CardView>(s => s.MoveCardAsync("WEB-1", w.InProgress, CardPlacement.AtEnd, w.Task1Version, Ct))),
            ("reorder a card", (t, w) => t.Try<IBoardService, CardView>(s => s.MoveCardAsync("WEB-2", w.ToDo, CardPlacement.AtTop, w.Task2Version, Ct))),
        ],
        ["Add comments"] =
        [
            ("add a comment", (t, _) => t.Try<ICommentService, CommentView>(s => s.AddAsync("WEB-1", "Looks good", Ct))),
        ],
        ["Edit or delete a comment"] =
        [
            ("edit the comment", (t, w) => t.Try<ICommentService, CommentView>(s => s.EditAsync(w.CommentId, "Second thoughts", w.CommentVersion, Ct))),
            ("delete the comment", (t, w) => t.Try<ICommentService>(s => s.DeleteAsync(w.CommentId, Ct))),
        ],
        ["Delete a task (with its sub-tasks)"] =
        [
            ("delete the task", (t, _) => t.Try<IWorkItemService>(s => s.DeleteAsync("WEB-1", Ct))),
        ],
        ["List and restore deleted tasks"] =
        [
            ("list deleted tasks", (t, _) => t.Try<IWorkItemService, Page<DeletedItemView>>(s => s.ListDeletedAsync("WEB", PageRequest.First, Ct))),
            ("restore a deleted task", (t, _) => t.Try<IWorkItemService>(s => s.RestoreAsync("WEB-3", Ct))),
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
        ["Change own password, display name and time zone"] =
        [
            ("change own password", (t, _) => t.Try<IAccountService>(s => s.ChangePasswordAsync(TestData.DefaultPassword, NewPassword, Ct))),
            ("change own display name and time zone", (t, _) => t.Try<IAccountService>(s => s.UpdateProfileAsync("New Name", "Asia/Karachi", Ct))),
        ],
    };

    private delegate Task<AppError?> Operation(PermissionMatrixTests test, World world);

    /// <summary>(row, operation, role, cell) for every cell of the document's table.</summary>
    public static TheoryData<string, string, Role, string> Cells()
    {
        var data = new TheoryData<string, string, Role, string>();
        foreach (var (action, cells) in ReadMatrix())
        {
            var names = Rows.TryGetValue(action, out var operations)
                ? operations.Select(o => o.Name).ToList()
                : PendingUntilUs2.Contains(action) ? [PendingOperation] : null;
            Assert.True(names is not null, $"No test operations for the permissions.md row \"{action}\".");
            foreach (var name in names!)
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
    public async Task P2_SC003_Every_cell_of_the_permission_matrix_is_enforced(string action, string operation, Role role, string cell)
    {
        if (operation == PendingOperation)
        {
            Assert.Skip("The operations for this row arrive with Phase 2 User Story 2.");
        }

        var world = await SetUpAsync();
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
            Assert.Equal(refusedAsNotOwner ? ErrorCodes.CommentNotOwned : ErrorCodes.Forbidden, error.Code);
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

    private static IEnumerable<(string Action, string[] Cells)> ReadMatrix()
    {
        var lines = File.ReadAllLines(PermissionsDocument());
        var header = Array.FindIndex(lines, l => l.StartsWith("| Action |", StringComparison.Ordinal));
        Assert.True(header >= 0, "The permission table was not found in permissions.md.");
        var columns = Split(lines[header]);
        Assert.Equal(["Action", "Anonymous", "Non-member", "Viewer", "Member", "Creator", "Project Admin", "Admin"],
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
            var path = Path.Combine(dir.FullName, "specs", "003-project-views-and-team", "contracts", "permissions.md");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("specs/003-project-views-and-team/contracts/permissions.md was not found above the test output.");
    }

    /// <summary>Project Admin "owen" created WEB; Member "cara" created WEB-1, WEB-2 and a comment on WEB-1; "uma" is
    /// another Member and "vera" a Viewer; "nora" is not a member; the Administrator "ada" (not a member) deleted
    /// WEB-3; "grace" is a second Administrator; "dora" is a deactivated Member; "nick" can be added to the team.</summary>
    private async Task<World> SetUpAsync()
    {
        var owner = await Data.UserAsync("owen");
        var creator = await Data.UserAsync("cara");
        var other = await Data.UserAsync("uma");
        var viewer = await Data.UserAsync("vera");
        var outsider = await Data.UserAsync("nora");
        var newcomer = await Data.UserAsync("nick");
        var admin = await Data.AdministratorAsync("ada");
        var secondAdmin = await Data.AdministratorAsync("grace");
        var deactivated = await Data.UserAsync("dora", isActive: false);

        ActAs(owner);
        (await CallAsync<IProjectService, Result<string>>(s => s.CreateAsync("Website Revamp", "WEB", null, Ct))).ValueOrThrow();
        await Data.MembersAsync("WEB", ProjectRole.Member, creator, other, deactivated);
        await Data.MembersAsync("WEB", ProjectRole.Viewer, viewer);
        var board = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("WEB", false, Ct))).ValueOrThrow();
        var (toDo, inProgress, done) = (board.Columns[0].Id, board.Columns[1].Id, board.Columns[2].Id);
        var membersVersion = (await CallAsync<IProjectMemberService, Result<TeamView>>(s => s.GetTeamAsync("WEB", Ct))).ValueOrThrow().MembersVersion;

        ActAs(creator);
        foreach (var title in new[] { "Design the home page", "Write the copy", "Old idea" })
        {
            (await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", toDo, title, Ct))).ValueOrThrow();
        }

        var comment = (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", "First thoughts", Ct))).ValueOrThrow();

        ActAs(admin);
        Assert.True((await CallAsync<IWorkItemService, Result>(s => s.DeleteAsync("WEB-3", Ct))).IsSuccess);
        var task1 = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync("WEB-1", Ct))).ValueOrThrow();
        var task2 = (await CallAsync<IWorkItemService, Result<WorkItemDetails>>(s => s.GetAsync("WEB-2", Ct))).ValueOrThrow();
        ActAs(null);
        return new World(owner, creator, other, viewer, outsider, newcomer, admin, secondAdmin, deactivated, board.BoardVersion,
            membersVersion, toDo, inProgress, done, task1.Version, task2.Version, comment.Id, comment.Version);
    }

    private async Task<AppError?> Try<TService, TValue>(Func<TService, Task<Result<TValue>>> call)
        where TService : notnull => (await CallAsync(call)).Error;

    private async Task<AppError?> Try<TService>(Func<TService, Task<Result>> call)
        where TService : notnull => (await CallAsync(call)).Error;

    private Task<AppError?> Columns(Func<IBoardColumnService, Task<Result<BoardColumnsView>>> call) => Try(call);

    private Task<AppError?> Edit(WorkItemEdit edit, byte[] version) =>
        Try<IWorkItemService, WorkItemDetails>(s => s.UpdateAsync("WEB-1", edit, version, Ct));

    private sealed record World(User Owner, User Creator, User Other, User Viewer, User Outsider, User Newcomer, User Admin,
        User SecondAdmin, User Deactivated, int BoardVersion, int MembersVersion, long ToDo, long InProgress, long Done,
        byte[] Task1Version, byte[] Task2Version, long CommentId, byte[] CommentVersion)
    {
        public User? Caller(Role role) => role switch
        {
            Role.NonMember => Outsider,
            Role.Viewer => Viewer,
            Role.Member => Other,
            Role.Creator => Creator,
            Role.ProjectAdmin => Owner,
            Role.Admin => Admin,
            _ => null,
        };
    }
}
