using Upms.Web.Components.Pages.Timeline;

namespace Upms.Web.Tests.Timeline;

/// <summary>Where dates fall on the timeline (Phase 2 research R12), with Jira's header: weeks above the day numbers, or
/// one row of months or quarters, the current one marked. "Today" is Sunday 27 Sep 2026.</summary>
public sealed class TimelineScaleTests
{
    private static readonly DateOnly Today = new(2026, 9, 27);

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    [Theory]
    [InlineData(TimelineZoom.Weeks, 36)]
    [InlineData(TimelineZoom.Months, 8)]
    [InlineData(TimelineZoom.Quarters, 3)]
    public void Each_scale_has_its_pixels_per_day(TimelineZoom zoom, double pixels)
    {
        Assert.Equal(pixels, TimelineScale.For(zoom, [], Today).PixelsPerDay);
    }

    [Fact]
    public void In_months_the_range_covers_every_date_and_today_with_room_to_fill_the_screen()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [D(10, 5), D(11, 20)], Today);

        Assert.Equal((D(6, 1), D(5, 31, 2027)), (scale.Start, scale.End));
        Assert.Equal(365 * 8, scale.Width);
        Assert.Equal(["June", "July", "August", "September", "October", "November", "December", "January '27", "February '27",
            "March '27", "April '27", "May '27"], scale.Periods.Select(p => p.Label));
        Assert.Equal([0, 240, 488, 736], scale.Periods.Take(4).Select(p => p.Left));
        Assert.Equal("September", Assert.Single(scale.Periods, p => p.IsCurrent).Label);
        Assert.False(scale.HasDayRow);
    }

    [Fact]
    public void In_weeks_the_range_runs_from_Monday_to_Sunday_with_every_day_numbered()
    {
        var scale = TimelineScale.For(TimelineZoom.Weeks, [D(10, 7)], Today);

        Assert.Equal((D(8, 10), D(12, 20)), (scale.Start, scale.End));
        Assert.Equal(DayOfWeek.Monday, scale.Start.DayOfWeek);
        Assert.All(scale.Periods, p => Assert.Equal(7 * 36, p.Width));
        Assert.Equal(["Aug", "Aug", "Aug", "Aug / Sep", "Sep"], scale.Periods.Take(5).Select(p => p.Label));
        Assert.Equal(D(9, 21), scale.Start.AddDays((int)(Assert.Single(scale.Periods, p => p.IsCurrent).Left / 36)));
        Assert.True(scale.HasDayRow);
        Assert.Equal(133, scale.Days.Count);
        Assert.Equal(27, Assert.Single(scale.Days, d => d.IsToday).Number);
    }

    [Fact]
    public void In_quarters_the_range_is_whole_quarters_named_by_their_months()
    {
        var scale = TimelineScale.For(TimelineZoom.Quarters, [D(10, 5)], Today);

        Assert.Equal((D(1, 1), D(12, 31, 2027)), (scale.Start, scale.End));
        Assert.Equal(["January - March", "April - June", "July - September", "October - December", "January - March '27",
            "April - June '27", "July - September '27", "October - December '27"], scale.Periods.Select(p => p.Label));
        Assert.Equal("July - September", Assert.Single(scale.Periods, p => p.IsCurrent).Label);
    }

    [Fact]
    public void Bars_are_placed_from_the_start_of_their_first_day_to_the_end_of_their_last()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [D(10, 5)], Today);

        Assert.Equal(0, scale.Left(D(6, 1)));
        Assert.Equal(8, scale.Left(D(6, 2)));
        Assert.Equal(6 * 8, scale.WidthOf(D(10, 5), D(10, 10)));
        Assert.Equal(8, scale.WidthOf(D(10, 5), D(10, 5))); // a one-date task is one day wide
    }

    [Fact]
    public void The_today_line_is_in_the_middle_of_today()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [], Today);

        Assert.Equal(118 * 8 + 4, scale.TodayLeft);
    }

    [Fact]
    public void With_nothing_scheduled_the_range_is_around_today()
    {
        var scale = TimelineScale.For(TimelineZoom.Weeks, [], Today);

        Assert.Equal((D(8, 10), D(12, 6)), (scale.Start, scale.End));
        Assert.InRange(Today, scale.Start, scale.End);
    }

    [Fact]
    public void Day_offsets_come_from_pixels()
    {
        var scale = TimelineScale.For(TimelineZoom.Weeks, [], Today);

        Assert.Equal(14, scale.DaysFor(14 * 36 + 10));
        Assert.Equal(-3, scale.DaysFor(-3 * 36 - 5));
        Assert.Equal(0, scale.DaysFor(17));
    }
}
