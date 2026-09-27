using Microsoft.EntityFrameworkCore;
using Upms.Application.Common.Results;
using Upms.Application.Tests.Fixtures;
using Upms.Application.Work;
using Upms.Domain.Work;

namespace Upms.Application.Tests.Work;

/// <summary>"What needs to be done?" (FR-018, FR-024, FR-040).</summary>
public sealed class InlineCreateTests(SqlServerFixture fixture) : BoardTestBase(fixture)
{
    [Fact]
    public async Task US1_AS5_Typing_a_title_creates_the_next_key_at_the_bottom_of_that_column()
    {
        var first = await AddAsync(InProgress, "Design the home page");
        var second = await AddAsync(InProgress, "Write the copy");

        Assert.Equal(("WEB-1", "WEB-2"), (first.Key, second.Key));
        Assert.Equal(["WEB-1", "WEB-2"], await KeysInAsync(InProgress));
        Assert.Equal(InProgress, second.ColumnId);

        var history = await QueryAsync(db => db.WorkItemChanges.AsNoTracking().Where(c => c.Field == WorkItemField.Created).ToListAsync(Ct));
        Assert.Equal(2, history.Count);
        Assert.All(history, h => Assert.Equal(Owner.Id, h.ActorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_title_creates_nothing(string title)
    {
        var result = await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", ToDo, title, Ct));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(await KeysInAsync(ToDo));
    }

    [Fact]
    public async Task A_title_over_255_characters_is_refused()
    {
        var result = await CallAsync<IBoardService, Result<CardView>>(s =>
            s.CreateInlineAsync("WEB", ToDo, new string('x', 256), Ct));

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("Title", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public async Task Fifty_simultaneous_creations_get_fifty_unique_consecutive_keys()
    {
        var results = await Task.WhenAll(Enumerable.Range(1, 50).Select(i =>
            CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", ToDo, $"Task {i}", Ct))));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.Message));
        var numbers = results.Select(r => int.Parse(r.Value!.Key["WEB-".Length..], System.Globalization.CultureInfo.InvariantCulture))
            .Order().ToList();
        Assert.Equal(Enumerable.Range(1, 50), numbers);
    }

    [Fact]
    public async Task A_column_of_another_project_is_not_found()
    {
        await CallAsync<Upms.Application.Projects.IProjectService, Result<string>>(s => s.CreateAsync("Mobile App", "MOB", null, Ct));
        var mobileToDo = (await CallAsync<IBoardService, Result<BoardView>>(s => s.GetAsync("MOB", false, Ct))).Value!.Columns[0].Id;

        var result = await CallAsync<IBoardService, Result<CardView>>(s => s.CreateInlineAsync("WEB", mobileToDo, "Sneaky", Ct));

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }
}
