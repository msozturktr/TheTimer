namespace TheTimer.Core.Planning;

public static class PlanReminder
{
    /// <summary>Occurrences whose start is in (<paramref name="afterExclusive"/>, <paramref name="untilInclusive"/>], earliest first.</summary>
    public static IReadOnlyList<PlanOccurrence> DueBetween(
        IEnumerable<PlanOccurrence> occurrences, DateTimeOffset afterExclusive, DateTimeOffset untilInclusive)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        return occurrences
            .Where(o => o.StartsAt > afterExclusive && o.StartsAt <= untilInclusive)
            .OrderBy(o => o.StartsAt)
            .ToList();
    }
}
