using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Identity;
using Upms.Application.Identity.Contracts;
using Upms.Application.Tests.Fixtures;
using Upms.Domain.Common;
using Upms.Domain.Identity;

namespace Upms.Application.Tests.Identity;

/// <summary>Each person's email choice (Phase 4 FR-051, research R11): "aimed at me" by default, changed only by its owner,
/// and read by the Notifications module with the address on the account.</summary>
public sealed class EmailChoiceTests(SqlServerFixture fixture) : IntegrationTest(fixture)
{
    private User _amina = null!;
    private User _bilal = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _amina = await Data.UserAsync("amina");
        _bilal = await Data.UserAsync("bilal");
        ActAs(_amina);
    }

    private Task<Result<MyProfile>> ChooseAsync(EmailChoice choice) =>
        CallAsync<IAccountService, Result<MyProfile>>(s => s.SetEmailChoiceAsync(choice, Ct));

    [Fact]
    public async Task P4_FR051_New_accounts_get_the_emails_aimed_at_them()
    {
        var profile = (await CallAsync<IAccountService, Result<MyProfile>>(s => s.GetProfileAsync(Ct))).ValueOrThrow();

        Assert.Equal(EmailChoice.AimedAtMe, profile.EmailChoice);
    }

    [Fact]
    public async Task P4_FR051_The_choice_changes_only_the_callers_account()
    {
        Assert.Equal(EmailChoice.None, (await ChooseAsync(EmailChoice.None)).ValueOrThrow().EmailChoice);
        Assert.Equal(EmailChoice.All, (await ChooseAsync(EmailChoice.All)).ValueOrThrow().EmailChoice);

        var choices = await QueryAsync(db => db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.EmailNotifications, Ct));
        Assert.Equal((EmailChoice.All, EmailChoice.AimedAtMe), (choices[_amina.Id], choices[_bilal.Id]));
    }

    [Fact]
    public async Task An_account_created_with_None_keeps_it_and_recipients_carry_address_and_choice()
    {
        var farah = new User { UserName = "farah", Email = "farah@example.com", DisplayName = "Farah Tester", EmailNotifications = EmailChoice.None };
        await using (var scope = Harness.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<User>>().CreateAsync(farah, TestData.DefaultPassword)).Succeeded);
        }

        var recipients = await CallAsync<IUserDirectory, IReadOnlyDictionary<Guid, RecipientInfo>>(s => s.GetRecipientsAsync([_amina.Id, farah.Id], Ct));

        Assert.Equal(new RecipientInfo(farah.Id, "Farah Tester", "farah@example.com", true, EmailChoice.None), recipients[farah.Id]);
        Assert.Equal(EmailChoice.AimedAtMe, recipients[_amina.Id].EmailChoice);
    }

    [Fact]
    public async Task Unknown_choices_and_signed_out_callers_are_refused()
    {
        Assert.Equal(ErrorKind.Validation, (await ChooseAsync((EmailChoice)7)).Error!.Kind);

        ActAs(null);
        Assert.Equal(ErrorKind.Forbidden, (await ChooseAsync(EmailChoice.None)).Error!.Kind);
    }
}
