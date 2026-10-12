namespace TheTimer.Core.Sessions;

/// <summary>Pure rules for turning a leftover <see cref="ActiveSessionRecord"/> into a session. Downtime after LastSeen is never counted.</summary>
public static class SessionRecovery
{
    public static TimeSpan RecoveredElapsed(ActiveSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var timer = record.Timer;
        if (timer.SegmentStart is not { } segmentStart)
            return timer.Accumulated;

        var segment = record.LastSeen - segmentStart;
        return segment > TimeSpan.Zero ? timer.Accumulated + segment : timer.Accumulated;
    }

    /// <summary>True when the session can be continued; a session that already reached its target can only be saved or discarded.</summary>
    public static bool CanResume(ActiveSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Timer.Target is not { } target || RecoveredElapsed(record) < target;
    }

    public static TimeSession ToFinishedSession(ActiveSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var timer = record.Timer;
        var elapsed = RecoveredElapsed(record);
        var endedAt = record.LastSeen;
        var duration = elapsed;

        if (timer.Target is { } target && elapsed >= target)
        {
            duration = target;
            if (timer.SegmentStart is { } segmentStart)
                endedAt = segmentStart + (target - timer.Accumulated);
        }

        return new TimeSession(Guid.NewGuid(), timer.TaskName, timer.Tag, timer.StartedAt, endedAt, duration);
    }

    /// <summary>A paused snapshot holding the recovered time, so the user resumes explicitly.</summary>
    public static TimerSnapshot ToResumeSnapshot(ActiveSessionRecord record, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!CanResume(record))
            throw new InvalidOperationException("Session already reached its target and cannot be resumed.");

        return record.Timer with { Accumulated = RecoveredElapsed(record), SegmentStart = null };
    }
}
