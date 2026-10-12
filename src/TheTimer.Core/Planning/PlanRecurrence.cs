namespace TheTimer.Core.Planning;

/// <summary>Weekly recurrence: every selected weekday from <see cref="StartDate"/> to <see cref="EndDate"/> (inclusive, open-ended when null).</summary>
public sealed record PlanRecurrence
{
    public PlanRecurrence(DateOnly startDate, DateOnly? endDate, WeekDays days)
    {
        if (days == WeekDays.None || (days & ~WeekDays.EveryDay) != 0)
            throw new ArgumentException("Days must be a non-empty combination of weekdays.", nameof(days));
        if (endDate is { } end && end < startDate)
            throw new ArgumentException("EndDate must not be before StartDate.", nameof(endDate));

        StartDate = startDate;
        EndDate = endDate;
        Days = days;
    }

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public WeekDays Days { get; init; }

    public bool OccursOn(DateOnly day) =>
        day >= StartDate && (EndDate is not { } end || day <= end) && Days.Includes(day.DayOfWeek);
}
