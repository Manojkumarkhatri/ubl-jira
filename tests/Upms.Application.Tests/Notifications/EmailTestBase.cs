using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Notifications;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Notifications;

/// <summary>WEB with its owner Amina and the task WEB-1 "Plan the launch", a mail server that works until told otherwise,
/// and Members with each email choice: Bilal keeps the default ("aimed at me"), Omar chose "All" and Farah "None" (Phase 4
/// User Story 5).</summary>
public abstract class EmailTestBase(SqlServerFixture fixture) : NotificationTestBase(fixture)
{
    protected const string BaseUrl = "https://upms.example";

    protected FakeEmailTransport Mail { get; } = new();

    protected User Bilal { get; private set; } = null!;

    protected User Omar { get; private set; } = null!;

    protected User Farah { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        Bilal = await MemberAsync("bilal");
        Omar = await MemberAsync("omar");
        Farah = await MemberAsync("farah");
        await ChooseAsync(Omar, EmailChoice.All);
        await ChooseAsync(Farah, EmailChoice.None);
        ActAs(Owner);
        await CreateAsync(WorkItemType.Task, "Plan the launch");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IEmailTransport>(Mail);
        services.Configure<EmailOptions>(o =>
        {
            o.FromAddress = "upms@bank.example";
            o.PublicBaseUrl = BaseUrl;
        });
    }

    protected async Task ChooseAsync(User user, EmailChoice choice)
    {
        ActAs(user);
        (await CallAsync<IAccountService, Result<MyProfile>>(s => s.SetEmailChoiceAsync(choice, Ct))).ValueOrThrow();
    }

    /// <summary>Every queued email with its recipient and reason, oldest first.</summary>
    protected Task<List<(Guid Recipient, NotificationKind Kind, EmailState State)>> EmailsAsync() =>
        QueryAsync(async db => (await db.EmailMessages.AsNoTracking().Include(e => e.Notification).OrderBy(e => e.Id).ToListAsync(Ct))
            .ConvertAll(e => (e.RecipientId, e.Notification!.Kind, e.State)));

    protected Task<int> DispatchAsync() => CallAsync<IEmailDispatcher, int>(s => s.SendDueAsync(Ct));
}
