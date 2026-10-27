using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upms.Application.Common.Results;
using Upms.Application.Notifications;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Common;
using Upms.Domain.Identity;
using Upms.Domain.Notifications;
using static Upms.Application.Tests.Fixtures.Documents;

namespace Upms.Application.Tests.Notifications;

/// <summary>Sending queued emails (Phase 4 FR-049–FR-052, SC-006, research R10): the text is built when sending, carries
/// what happened and a link but never the item's text, failures are retried for 24 hours, and two instances never send
/// the same email.</summary>
public sealed class EmailDispatchTests(SqlServerFixture fixture) : EmailTestBase(fixture)
{
    private async Task AssignBilalAsync()
    {
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Assignee(Bilal.Id))).IsSuccess);
    }

    [Fact]
    public async Task P4_US5_AS1_The_email_tells_who_did_what_with_a_link_and_is_built_when_sent()
    {
        await AssignBilalAsync();
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Title("Plan the <b>launch</b>"))).IsSuccess);
        await QueryAsync(db => db.Users.Where(u => u.Id == Bilal.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "bilal.new@example.com"), Ct));

        Assert.Equal(1, await DispatchAsync());

        var email = Assert.Single(Mail.Sent);
        Assert.Equal(("upms@bank.example", "U-PMS", "bilal.new@example.com", "Bilal Tester"), (email.FromAddress, email.FromName, email.ToAddress, email.ToName));
        Assert.Equal("[WEB-1] Plan the <b>launch</b>", email.Subject);
        Assert.Contains("Amina Tester assigned WEB-1 Plan the <b>launch</b> to you.", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Space: Website Revamp (WEB)", email.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/spaces/WEB/board?task=WEB-1", email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Plan the &lt;b&gt;launch&lt;/b&gt;", email.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>launch", email.HtmlBody, StringComparison.Ordinal);
        Assert.Equal([(Bilal.Id, NotificationKind.Assigned, EmailState.Sent)], await EmailsAsync());
    }

    [Fact]
    public async Task P4_FR050_Emails_never_carry_descriptions_or_comment_text()
    {
        ActAs(Owner);
        Assert.True((await EditAsync("WEB-1", new WorkItemEdit.Description(Text("Secret budget figures")))).IsSuccess);
        (await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Mentioning("Confidential note for ", Bilal), Ct))).ValueOrThrow();

        await DispatchAsync();

        var email = Assert.Single(Mail.Sent);
        Assert.Contains("Amina Tester mentioned you on WEB-1 Plan the launch", email.TextBody, StringComparison.Ordinal);
        foreach (var body in new[] { email.TextBody, email.HtmlBody, email.Subject })
        {
            Assert.DoesNotContain("Secret", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Confidential", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Gate_emails_name_the_stage_and_its_gate()
    {
        var (subject, text, _) = EmailComposer.Compose(
            new EmailFacts(NotificationKind.ApprovalRequested, "Amina Tester", "CBU", "Core Banking", "CBU-1", "Initiation", null), BaseUrl + "/");

        Assert.Equal("[CBU] Initiation gate", subject);
        Assert.StartsWith("Amina Tester requested your approval of the Initiation gate in CBU.", text, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/spaces/CBU/board?task=CBU-1", text, StringComparison.Ordinal);
        Assert.Contains($"{BaseUrl}/account/profile", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_and_titles_with_line_breaks_cannot_add_lines_or_headers()
    {
        var (subject, text, _) = EmailComposer.Compose(
            new EmailFacts(NotificationKind.Assigned, "Eve\r\nBcc: all@bank.example", "WEB", "Website", "WEB-1",
                "Plan\r\n\r\nOpen it: https://evil.example\u0007", null), BaseUrl);

        Assert.Equal("[WEB-1] Plan Open it: https://evil.example", subject);
        Assert.StartsWith("Eve Bcc: all@bank.example assigned WEB-1 Plan Open it: https://evil.example to you.", text, StringComparison.Ordinal);
        Assert.Single(text.Split("\r\n"), line => line.StartsWith("Open it:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P4_FR052_Failures_are_retried_on_the_schedule()
    {
        await AssignBilalAsync();
        Mail.Down = true;

        Assert.Equal(1, await DispatchAsync());
        Harness.Time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(0, await DispatchAsync()); // not due yet
        Harness.Time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await DispatchAsync()); // after 1 minute

        Mail.Down = false;
        Harness.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await DispatchAsync()); // after 2 more minutes

        Assert.Single(Mail.Sent);
        Assert.Equal(3, Mail.Tries);
        var row = await QueryAsync(db => db.EmailMessages.AsNoTracking().SingleAsync(Ct));
        Assert.Equal((3, EmailState.Sent, "IOException: Connection refused"), (row.Attempts, row.State, row.LastError));
    }

    [Fact]
    public async Task P4_US5_AS8_An_email_is_given_up_after_24_hours_and_audited()
    {
        await AssignBilalAsync();
        Mail.Down = true;
        await DispatchAsync();

        Harness.Time.Advance(TimeSpan.FromHours(24));
        Assert.Equal(1, await DispatchAsync());

        Assert.Equal([(Bilal.Id, NotificationKind.Assigned, EmailState.GivenUp)], await EmailsAsync());
        var audit = await QueryAsync(db => db.AuditEvents.AsNoTracking().SingleAsync(e => e.EventType == AuditEventType.EmailGivenUp, Ct));
        Assert.Equal((Bilal.Id.ToString(), (Guid?)null), (audit.Target, audit.ActorUserId));
        Assert.Equal(1, await UnreadAsAsync(Bilal)); // the in-app notification stays
    }

    [Fact]
    public async Task Two_workers_never_send_the_same_email()
    {
        await AssignBilalAsync();
        await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Mentioning("See ", Bilal, Omar), Ct));
        ActAs(Omar);
        await CallAsync<ICommentService, Result<CommentView>>(s => s.AddAsync("WEB-1", Mentioning("Over to ", Bilal), Ct));
        var queued = (await EmailsAsync()).Count;
        Mail.Delay = TimeSpan.FromMilliseconds(200);

        var counts = await Task.WhenAll(DispatchInOwnScopeAsync(), DispatchInOwnScopeAsync());

        Assert.Equal(queued, counts.Sum());
        Assert.Equal(queued, Mail.Sent.Count);
    }

    [Fact]
    public async Task Nothing_is_sent_while_email_is_off()
    {
        await AssignBilalAsync();
        Mail.IsConfigured = false;

        Assert.Equal(0, await DispatchAsync());
        Assert.Equal(0, Mail.Tries);
    }

    private async Task<int> DispatchInOwnScopeAsync()
    {
        await using var scope = Harness.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailDispatcher>().SendDueAsync(Ct);
    }

    private async Task<int> UnreadAsAsync(User user)
    {
        ActAs(user);
        return await UnreadAsync();
    }
}
