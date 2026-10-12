namespace TheTimer.Core.Planning;

/// <summary>
/// A planned time block on a local day: either a one-off (<see cref="Date"/>) or a series (<see cref="Recurrence"/>).
/// Times are local wall-clock with minute precision and never cross midnight.
/// </summary>
public sealed record PlanBlock
{
    public PlanBlock(
        Guid id, string taskName, string? tag, TimeOnly start, TimeOnly end, DateOnly? date, PlanRecurrence? recurrence)
    {
        ArgumentNullException.ThrowIfNull(taskName);
        var name = taskName.Trim();
        if (name.Length == 0)
            throw new ArgumentException("Task name must not be empty.", nameof(taskName));
        if (start.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Start must have minute precision.", nameof(start));
        if (end.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("End must have minute precision.", nameof(end));
        if (end <= start)
            throw new ArgumentException("End must be after Start.", nameof(end));
        if ((date is null) == (recurrence is null))
            throw new ArgumentException("Exactly one of Date and Recurrence must be set.");

        Id = id;
        TaskName = name;
        Tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
        Start = start;
        End = end;
        Date = date;
        Recurrence = recurrence;
    }

    public Guid Id { get; init; }

    public string TaskName { get; init; }

    public string? Tag { get; init; }

    public TimeOnly Start { get; init; }

    public TimeOnly End { get; init; }

    public DateOnly? Date { get; init; }

    public PlanRecurrence? Recurrence { get; init; }

    public bool IsRecurring => Recurrence is not null;
}
