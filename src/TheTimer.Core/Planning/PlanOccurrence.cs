namespace TheTimer.Core.Planning;

/// <summary>One concrete appearance of a <see cref="PlanBlock"/> on a local day, resolved to instants.</summary>
public sealed record PlanOccurrence(
    PlanBlock Block, DateOnly Day, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool HasOverlap)
{
    public bool IsRecurring => Block.Recurrence is not null;

    public TimeSpan PlannedDuration => EndsAt - StartsAt;
}
