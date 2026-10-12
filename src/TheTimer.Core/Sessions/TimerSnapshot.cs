namespace TheTimer.Core.Sessions;

/// <summary>
/// Serializable state of an active <see cref="SessionTimer"/>.
/// <see cref="Accumulated"/> is running time before the current segment; <see cref="SegmentStart"/> is null when paused.
/// </summary>
public sealed record TimerSnapshot(
    string TaskName,
    string? Tag,
    DateTimeOffset StartedAt,
    TimeSpan Accumulated,
    DateTimeOffset? SegmentStart,
    TimeSpan? Target);
