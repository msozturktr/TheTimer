using TheTimer.Core.Sessions;

namespace TheTimer.Core.Planning;

public sealed record TaskPlanComparison(string TaskName, TimeSpan Planned, TimeSpan Actual)
{
    public TimeSpan Difference => Actual - Planned;
}

public static class PlanComparison
{
    /// <summary>
    /// Time actually spent inside the occurrence: for each session of the same task, the overlap of its
    /// wall-clock span with the occurrence, scaled by Duration / span to discount pauses.
    /// </summary>
    public static TimeSpan ActualForOccurrence(PlanOccurrence occurrence, IEnumerable<TimeSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(sessions);

        var ticks = 0L;
        foreach (var session in sessions)
        {
            if (!SameTask(session.TaskName, occurrence.Block.TaskName))
                continue;
            var span = (session.EndedAt - session.StartedAt).Ticks;
            if (span <= 0)
                continue;
            var overlapStart = session.StartedAt > occurrence.StartsAt ? session.StartedAt : occurrence.StartsAt;
            var overlapEnd = session.EndedAt < occurrence.EndsAt ? session.EndedAt : occurrence.EndsAt;
            var overlap = (overlapEnd - overlapStart).Ticks;
            if (overlap <= 0)
                continue;
            ticks += (long)Math.Round(overlap * ((double)session.Duration.Ticks / span));
        }

        return TimeSpan.FromTicks(ticks);
    }

    /// <summary>Planned vs actual per task (case-insensitive); whole sessions count, not clipped to the plan.</summary>
    public static IReadOnlyList<TaskPlanComparison> ByTask(
        IEnumerable<PlanOccurrence> occurrences, IEnumerable<TimeSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(sessions);

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var planned = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var actual = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // Plan names are registered first so they win as display names.
        foreach (var o in occurrences)
        {
            var name = o.Block.TaskName.Trim();
            names.TryAdd(name, name);
            planned[name] = planned.GetValueOrDefault(name) + (o.EndsAt - o.StartsAt).Ticks;
        }

        foreach (var s in sessions)
        {
            var name = s.TaskName.Trim();
            names.TryAdd(name, name);
            actual[name] = actual.GetValueOrDefault(name) + s.Duration.Ticks;
        }

        return names
            .Select(kv => new TaskPlanComparison(
                kv.Value,
                TimeSpan.FromTicks(planned.GetValueOrDefault(kv.Key)),
                TimeSpan.FromTicks(actual.GetValueOrDefault(kv.Key))))
            .OrderByDescending(t => t.Planned)
            .ThenByDescending(t => t.Actual)
            .ThenBy(t => t.TaskName, StringComparer.Ordinal)
            .ToList();
    }

    private static bool SameTask(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
