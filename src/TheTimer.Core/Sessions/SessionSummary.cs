namespace TheTimer.Core.Sessions;

/// <summary>Total time spent on one task (names compared case-insensitively).</summary>
public sealed record TaskTotal(string TaskName, TimeSpan Total, int SessionCount);

public static class SessionSummary
{
    /// <summary>Groups by task name; the first occurrence's spelling is kept. Largest total first, then name.</summary>
    public static IReadOnlyList<TaskTotal> ByTask(IEnumerable<TimeSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        return sessions
            .GroupBy(s => s.TaskName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TaskTotal(g.First().TaskName, TimeSpan.FromTicks(g.Sum(s => s.Duration.Ticks)), g.Count()))
            .OrderByDescending(t => t.Total)
            .ThenBy(t => t.TaskName, StringComparer.Ordinal)
            .ToList();
    }

    public static TimeSpan Total(IEnumerable<TimeSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        return TimeSpan.FromTicks(sessions.Sum(s => s.Duration.Ticks));
    }
}
