using Upms.Web.Components.Pages.Timeline;

namespace Upms.Web.Tests.Timeline;

/// <summary>Where dates fall on the timeline (Phase 2 research R12). "Today" is Sunday 27 Sep 2026.</summary>
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
    public void In_months_the_range_covers_every_date_and_today_with_a_month_of_room_on_each_side()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [D(10, 5), D(11, 20)], Today);

        Assert.Equal((D(8, 1), D(12, 31)), (scale.Start, scale.End));
        Assert.Equal(153 * 8, scale.Width);
        Assert.Equal(["Aug 2026", "Sep 2026", "Oct 2026", "Nov 2026", "Dec 2026"], scale.Headings.Select(h => h.Label));
        Assert.Equal([0, 248, 488, 736, 976], scale.Headings.Select(h => h.Left));
        Assert.Equal([248, 240, 248, 240, 248], scale.Headings.Select(h => h.Width));
    }

    [Fact]
    public void In_weeks_the_range_runs_from_Monday_to_Sunday()
    {
        var scale = TimelineScale.For(TimelineZoom.Weeks, [D(10, 7)], Today);

        Assert.Equal((D(9, 14), D(10, 18)), (scale.Start, scale.End));
        Assert.Equal(DayOfWeek.Monday, scale.Start.DayOfWeek);
        Assert.Equal(["14 Sep", "21 Sep", "28 Sep", "5 Oct", "12 Oct"], scale.Headings.Select(h => h.Label));
        Assert.All(scale.Headings, h => Assert.Equal(7 * 36, h.Width));
    }

    [Fact]
    public void In_quarters_the_range_is_whole_quarters()
    {
        var scale = TimelineScale.For(TimelineZoom.Quarters, [D(10, 5)], Today);

        Assert.Equal((D(4, 1), D(3, 31, 2027)), (scale.Start, scale.End));
        Assert.Equal(["Q2 2026", "Q3 2026", "Q4 2026", "Q1 2027"], scale.Headings.Select(h => h.Label));
    }

    [Fact]
    public void Bars_are_placed_from_the_start_of_their_first_day_to_the_end_of_their_last()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [D(10, 5)], Today);

        Assert.Equal(0, scale.Left(D(8, 1)));
        Assert.Equal(8, scale.Left(D(8, 2)));
        Assert.Equal(6 * 8, scale.WidthOf(D(10, 5), D(10, 10)));
        Assert.Equal(8, scale.WidthOf(D(10, 5), D(10, 5))); // a one-date task is one day wide
    }

    [Fact]
    public void The_today_line_is_in_the_middle_of_today()
    {
        var scale = TimelineScale.For(TimelineZoom.Months, [], Today);

        Assert.Equal(57 * 8 + 4, scale.TodayLeft);
    }

    [Fact]
    public void With_nothing_scheduled_the_range_is_around_today()
    {
        var scale = TimelineScale.For(TimelineZoom.Weeks, [], Today);

        Assert.Equal((D(9, 14), D(10, 4)), (scale.Start, scale.End));
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
