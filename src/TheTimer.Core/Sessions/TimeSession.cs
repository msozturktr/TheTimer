namespace TheTimer.Core.Sessions;

/// <summary>A finished timing session. <see cref="Duration"/> is net active time (pauses excluded).</summary>
public sealed record TimeSession(
    Guid Id,
    string TaskName,
    string? Tag,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Duration);
