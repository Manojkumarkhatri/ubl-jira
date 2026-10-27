using Upms.Domain.Common;
using Upms.Domain.Work;

namespace Upms.Domain.Tests.Work;

/// <summary>Epic colors, as in Jira: given in turn when an epic is created, and changeable (Jira parity pass FR-025; research
/// R15).</summary>
public sealed class EpicColorTests
{
    private static readonly StatusRef ToDo = new(1, "To Do", StatusCategory.ToDo);
    private static readonly Guid Amina = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private static ChangeContext By(Guid actor, int minutes = 0) => ChangeContext.New(actor, T0.AddMinutes(minutes));

    private static WorkItem New(WorkItemType type, int number) =>
        WorkItem.Create(10, "MOB", number, type, "Card refunds", ToDo, Rank.First(), ProjectTemplate.Scrum, By(Amina)).Value!;

    [Theory]
    [InlineData(1, EpicColor.Purple)]
    [InlineData(2, EpicColor.Blue)]
    [InlineData(8, EpicColor.Gray)]
    [InlineData(9, EpicColor.Purple)]
    [InlineData(15, EpicColor.Red)]
    public void A_new_epic_takes_the_next_color_in_turn(int number, EpicColor color)
    {
        Assert.Equal(color, New(WorkItemType.Epic, number).Color);
    }

    [Theory]
    [InlineData(WorkItemType.Story)]
    [InlineData(WorkItemType.Task)]
    [InlineData(WorkItemType.Bug)]
    public void Other_types_have_no_color(WorkItemType type)
    {
        Assert.Null(New(type, 3).Color);
    }

    [Fact]
    public void JP_US5_AS2_A_changed_color_is_recorded_with_its_old_and_new_names()
    {
        var epic = New(WorkItemType.Epic, 1);

        Assert.Null(epic.SetColor(EpicColor.Teal, By(Amina, 5)));

        Assert.Equal(EpicColor.Teal, epic.Color);
        Assert.Equal(T0.AddMinutes(5), epic.UpdatedAt);
        var change = epic.Changes.Last();
        Assert.Equal((WorkItemField.Color, "Purple", "Teal"), (change.Field, change.OldValue, change.NewValue));
    }

    [Fact]
    public void The_same_color_records_nothing()
    {
        var epic = New(WorkItemType.Epic, 2);

        Assert.Null(epic.SetColor(EpicColor.Blue, By(Amina, 5)));

        Assert.Single(epic.Changes);
        Assert.Equal(T0, epic.UpdatedAt);
    }

    [Fact]
    public void Only_an_epic_has_a_color()
    {
        var story = New(WorkItemType.Story, 3);

        var error = story.SetColor(EpicColor.Red, By(Amina));

        Assert.Equal(WorkItem.ColorNotAllowedCode, error!.Code);
        Assert.Null(story.Color);
        Assert.Single(story.Changes);
    }

    [Fact]
    public void The_palette_has_eight_named_colors_in_order()
    {
        Assert.Equal(["Purple", "Blue", "Teal", "Green", "Yellow", "Orange", "Red", "Gray"], Enum.GetNames<EpicColor>());
        Assert.Equal(EpicColor.Green, EpicColors.ForNumber(4));
    }
}
