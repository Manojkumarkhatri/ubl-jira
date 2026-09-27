using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Upms.Application.Common;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>The append-only history (FR-031, SC-004, constitution IV).</summary>
public sealed class HistoryTests(SqlServerFixture fixture) : DrawerTestBase(fixture)
{
    [Fact]
    public async Task US2_AS9_Every_change_is_listed_in_time_order_with_who_when_what_and_values()
    {
        var card = await AddAsync(ToDo, "Design the home page");
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync(card.Key, new WorkItemEdit.Title("Design the landing page"));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await EditAsync(card.Key, new WorkItemEdit.Priority(Priority.Highest));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        ActAs(await Data.UserAsync("bilal"));
        await EditAsync(card.Key, new WorkItemEdit.Status(InProgress));
        Harness.Time.Advance(TimeSpan.FromMinutes(1));
        await AddSubtaskAsync(card.Key, "Wireframes");

        var history = (await HistoryAsync(card.Key)).Items;

        Assert.Equal([WorkItemField.Created, WorkItemField.Title, WorkItemField.Priority, WorkItemField.Status, WorkItemField.SubtaskAdded],
            history.Select(h => h.Field));
        Assert.Equal(history.Select(h => h.OccurredAt).Order(), history.Select(h => h.OccurredAt));
        Assert.Equal(("Design the home page", "Design the landing page"), (history[1].OldValue, history[1].NewValue));
        Assert.Equal(("Medium", "Highest"), (history[2].OldValue, history[2].NewValue));
        Assert.Equal(("To Do", "In Progress", "Bilal Tester"), (history[3].OldValue, history[3].NewValue, history[3].ActorName));
        Assert.Equal(Owner.DisplayName, history[0].ActorName);
    }

    [Fact]
    public async Task History_rows_cannot_be_updated_or_deleted_even_with_SQL()
    {
        await AddAsync(ToDo, "Design the home page");

        await Assert.ThrowsAsync<SqlException>(() => QueryAsync(db =>
            db.Database.ExecuteSqlRawAsync("UPDATE [WorkItemChanges] SET [NewValue] = N'forged'", Ct)));
        await Assert.ThrowsAsync<SqlException>(() => QueryAsync(db =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM [WorkItemChanges]", Ct)));
        Assert.Single((await HistoryAsync("WEB-1")).Items);
    }

    [Fact]
    public async Task History_is_read_50_entries_at_a_time()
    {
        var card = await AddAsync(ToDo, "Busy task");
        for (var i = 1; i <= 25; i++)
        {
            await EditAsync(card.Key, new WorkItemEdit.Priority(i % 2 == 0 ? Priority.High : Priority.Low));
            await EditAsync(card.Key, new WorkItemEdit.Title($"Busy task {i}"));
        }

        var first = await HistoryAsync(card.Key);
        var second = await HistoryAsync(card.Key, new PageRequest(2));

        Assert.Equal((50, 51), (first.Items.Count, first.TotalCount));
        Assert.Equal(WorkItemField.Created, first.Items[0].Field);
        Assert.Single(second.Items);
    }
}
