using TheTimer.Core.Planning;
using TheTimer.Core.Sessions;

namespace TheTimer.Core.Reports;

public sealed record DayTotal(DateOnly Day, TimeSpan Actual, TimeSpan Planned);

public sealed record WeeklyReportData(
    DateOnly WeekStart,
    IReadOnlyList<DayTotal> Days,
    IReadOnlyList<TaskPlanComparison> Tasks,
    TimeSpan TotalActual,
    TimeSpan TotalPlanned);

public static class WeeklyReport
{
    /// <summary>The Monday of the week containing <paramref name="anyDay"/>.</summary>
    public static DateOnly WeekStart(DateOnly anyDay) =>
        anyDay.AddDays(-(((int)anyDay.DayOfWeek + 6) % 7));

    /// <summary>Builds the Monday-to-Sunday report; sessions (by local start day) and occurrences outside the week are ignored.</summary>
    public static WeeklyReportData Build(
        DateOnly anyDayInWeek, IEnumerable<TimeSession> sessions, IEnumerable<PlanOccurrence> occurrences, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(timeZone);

        var start = WeekStart(anyDayInWeek);
        var end = start.AddDays(6);

        var weekSessions = sessions
            .Select(s => (Session: s, Day: SessionDayFilter.LocalDay(s, timeZone)))
            .Where(x => x.Day >= start && x.Day <= end)
            .ToList();
        var weekOccurrences = occurrences.Where(o => o.Day >= start && o.Day <= end).ToList();

        var days = Enumerable.Range(0, 7)
            .Select(i =>
            {
                var day = start.AddDays(i);
                var actual = weekSessions.Where(x => x.Day == day).Sum(x => x.Session.Duration.Ticks);
                var planned = weekOccurrences.Where(o => o.Day == day).Sum(o => (o.EndsAt - o.StartsAt).Ticks);
                return new DayTotal(day, TimeSpan.FromTicks(actual), TimeSpan.FromTicks(planned));
            })
            .ToList();

        return new WeeklyReportData(
            start,
            days,
            PlanComparison.ByTask(weekOccurrences, weekSessions.Select(x => x.Session)),
            TimeSpan.FromTicks(days.Sum(d => d.Actual.Ticks)),
            TimeSpan.FromTicks(days.Sum(d => d.Planned.Ticks)));
    }
}

/// <summary>Convenience: loads a week from the repositories and builds the report.</summary>
public sealed class ReportService
{
    private readonly ISessionRepository _sessions;
    private readonly PlanService _plan;

    public ReportService(ISessionRepository sessions, PlanService plan)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(plan);
        _sessions = sessions;
        _plan = plan;
    }

    public WeeklyReportData GetWeek(DateOnly anyDay, TimeZoneInfo timeZone)
    {
        var start = WeeklyReport.WeekStart(anyDay);
        var end = start.AddDays(6);
        return WeeklyReport.Build(
            anyDay,
            _sessions.GetByRange(start, end, timeZone),
            _plan.GetOccurrences(start, end, timeZone),
            timeZone);
    }
}
