using System.Globalization;

namespace Upms.Web.Components.Pages.Timeline;

public enum TimelineZoom
{
    Weeks,
    Months,
    Quarters,
}

/// <summary>Where dates fall on the timeline's track (Phase 2 FR-034, research R12): 36 pixels per day in weeks, 8 in
/// months and 3 in quarters. The header follows Jira's: in weeks, the weeks above and every day's number below; in
/// months and quarters, one row of month or quarter names. The range covers every scheduled date and today, with room on
/// each side so the chart fills the screen; weeks start on Monday.</summary>
public sealed class TimelineScale
{
    private TimelineScale(TimelineZoom zoom, DateOnly start, DateOnly end, DateOnly today)
    {
        Zoom = zoom;
        Start = start;
        End = end;
        Today = today;
        Periods = BuildPeriods();
        Days = zoom == TimelineZoom.Weeks ? BuildDays() : [];
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

    /// <summary>Weeks, months or quarters: the header's names, the grid's lines and the shaded current one.</summary>
    public IReadOnlyList<TimelinePeriod> Periods { get; }

    /// <summary>In weeks, every day with its number under the week.</summary>
    public IReadOnlyList<TimelineDay> Days { get; }

    /// <summary>The weeks view has two header rows.</summary>
    public bool HasDayRow => Zoom == TimelineZoom.Weeks;

    public static double PixelsPerDayFor(TimelineZoom zoom) => zoom switch
    {
        TimelineZoom.Weeks => 36,
        TimelineZoom.Months => 8,
        _ => 3,
    };

    public static TimelineScale For(TimelineZoom zoom, IEnumerable<DateOnly> dates, DateOnly today)
    {
        var all = dates.Append(today).ToList();
        var (before, after) = zoom switch
        {
            TimelineZoom.Weeks => (6, 10),
            TimelineZoom.Months => (3, 6),
            _ => (2, 4),
        };
        var start = PeriodStart(zoom, all.Min());
        var end = PeriodStart(zoom, all.Max());
        for (var i = 0; i < before; i++)
        {
            start = PeriodStart(zoom, start.AddDays(-1));
        }

        for (var i = 0; i < after; i++)
        {
            end = NextPeriod(zoom, end);
        }

        return new TimelineScale(zoom, start, NextPeriod(zoom, end).AddDays(-1), today);
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

    private List<TimelinePeriod> BuildPeriods()
    {
        var periods = new List<TimelinePeriod>();
        for (var period = Start; period <= End; period = NextPeriod(Zoom, period))
        {
            var last = NextPeriod(Zoom, period).AddDays(-1);
            periods.Add(new TimelinePeriod(Label(period, last), Left(period), WidthOf(period, last), Today >= period && Today <= last));
        }

        return periods;
    }

    /// <summary>"Sep / Oct" or "Oct" for a week; "October", or "January '27" outside this year; "October - December".</summary>
    private string Label(DateOnly first, DateOnly last)
    {
        var year = first.Year == Today.Year ? "" : $" '{first.Year % 100:00}";
        return Zoom switch
        {
            TimelineZoom.Weeks when first.Month != last.Month =>
                $"{first.ToString("MMM", CultureInfo.InvariantCulture)} / {last.ToString("MMM", CultureInfo.InvariantCulture)}",
            TimelineZoom.Weeks => first.ToString("MMM", CultureInfo.InvariantCulture),
            TimelineZoom.Months => first.ToString("MMMM", CultureInfo.InvariantCulture) + year,
            _ => $"{first.ToString("MMMM", CultureInfo.InvariantCulture)} - {last.ToString("MMMM", CultureInfo.InvariantCulture)}{year}",
        };
    }

    private List<TimelineDay> BuildDays()
    {
        var days = new List<TimelineDay>();
        for (var day = Start; day <= End; day = day.AddDays(1))
        {
            days.Add(new TimelineDay(day.Day, Left(day), day == Today, day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
        }

        return days;
    }
}

/// <param name="Left">From the start of the track, in pixels.</param>
/// <param name="IsCurrent">Holds today; Jira shades it.</param>
public sealed record TimelinePeriod(string Label, double Left, double Width, bool IsCurrent);

public sealed record TimelineDay(int Number, double Left, bool IsToday, bool IsWeekend);
