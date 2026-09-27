using System.Globalization;

namespace Upms.Web.Components.Pages.Timeline;

public enum TimelineZoom
{
    Weeks,
    Months,
    Quarters,
}

/// <summary>Where dates fall on the timeline's track (Phase 2 FR-034, research R12): 36 pixels per day in weeks, 8 in
/// months and 3 in quarters. The range covers every scheduled date and today, with one whole week, month or quarter
/// of room on each side; weeks start on Monday.</summary>
public sealed class TimelineScale
{
    private TimelineScale(TimelineZoom zoom, DateOnly start, DateOnly end, DateOnly today)
    {
        Zoom = zoom;
        Start = start;
        End = end;
        Today = today;
        Headings = BuildHeadings();
    }

    public TimelineZoom Zoom { get; }

    public DateOnly Start { get; }

    /// <summary>The last day shown (inclusive).</summary>
    public DateOnly End { get; }

    public DateOnly Today { get; }

    public double PixelsPerDay => PixelsPerDayFor(Zoom);

    public double Width => (End.DayNumber - Start.DayNumber + 1) * PixelsPerDay;

    /// <summary>The today line, in the middle of today.</summary>
    public double TodayLeft => Left(Today) + PixelsPerDay / 2;

    public IReadOnlyList<TimelineHeading> Headings { get; }

    public static double PixelsPerDayFor(TimelineZoom zoom) => zoom switch
    {
        TimelineZoom.Weeks => 36,
        TimelineZoom.Months => 8,
        _ => 3,
    };

    public static TimelineScale For(TimelineZoom zoom, IEnumerable<DateOnly> dates, DateOnly today)
    {
        var all = dates.Append(today).ToList();
        var start = PeriodStart(zoom, PeriodStart(zoom, all.Min()).AddDays(-1));
        var end = PeriodEnd(zoom, PeriodEnd(zoom, all.Max()).AddDays(1));
        return new TimelineScale(zoom, start, end, today);
    }

    /// <summary>The distance from the start of the track to the start of the day.</summary>
    public double Left(DateOnly date) => (date.DayNumber - Start.DayNumber) * PixelsPerDay;

    /// <summary>From the start of the first day to the end of the last; a one-date task is one day wide.</summary>
    public double WidthOf(DateOnly first, DateOnly last) => (last.DayNumber - first.DayNumber + 1) * PixelsPerDay;

    /// <summary>Whole days for a distance dragged, to the nearest day.</summary>
    public int DaysFor(double pixels) => (int)Math.Round(pixels / PixelsPerDay, MidpointRounding.AwayFromZero);

    private static DateOnly PeriodStart(TimelineZoom zoom, DateOnly date) => zoom switch
    {
        TimelineZoom.Weeks => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        TimelineZoom.Months => new DateOnly(date.Year, date.Month, 1),
        _ => new DateOnly(date.Year, ((date.Month - 1) / 3 * 3) + 1, 1),
    };

    private static DateOnly NextPeriod(TimelineZoom zoom, DateOnly periodStart) => zoom switch
    {
        TimelineZoom.Weeks => periodStart.AddDays(7),
        TimelineZoom.Months => periodStart.AddMonths(1),
        _ => periodStart.AddMonths(3),
    };

    private static DateOnly PeriodEnd(TimelineZoom zoom, DateOnly date) => NextPeriod(zoom, PeriodStart(zoom, date)).AddDays(-1);

    private List<TimelineHeading> BuildHeadings()
    {
        var headings = new List<TimelineHeading>();
        for (var period = Start; period <= End; period = NextPeriod(Zoom, period))
        {
            var label = Zoom switch
            {
                TimelineZoom.Weeks => period.ToString("d MMM", CultureInfo.InvariantCulture),
                TimelineZoom.Months => period.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                _ => $"Q{((period.Month - 1) / 3) + 1} {period.Year}",
            };
            headings.Add(new TimelineHeading(label, Left(period), WidthOf(period, PeriodEnd(Zoom, period))));
        }

        return headings;
    }
}

/// <param name="Left">From the start of the track, in pixels.</param>
public sealed record TimelineHeading(string Label, double Left, double Width);
