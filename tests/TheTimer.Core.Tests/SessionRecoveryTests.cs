using TheTimer.Core.Pomodoro;
using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SessionRecoveryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static ActiveSessionRecord Record(TimeSpan accumulated, DateTimeOffset? segmentStart, DateTimeOffset lastSeen, TimeSpan? target = null) =>
        new(new TimerSnapshot("Work", "tag", T0, accumulated, segmentStart, target), lastSeen, "Stopwatch", false, PomodoroPhase.Work, 0);

    [Fact]
    public void RecoveredElapsed_Running_AddsSegmentUpToLastSeen() =>
        Assert.Equal(TimeSpan.FromMinutes(15),
            SessionRecovery.RecoveredElapsed(Record(TimeSpan.FromMinutes(5), T0.AddMinutes(5), T0.AddMinutes(15))));

    [Fact]
    public void RecoveredElapsed_Paused_IsAccumulated() =>
        Assert.Equal(TimeSpan.FromMinutes(5),
            SessionRecovery.RecoveredElapsed(Record(TimeSpan.FromMinutes(5), null, T0.AddHours(3))));

    [Fact]
    public void RecoveredElapsed_LastSeenBeforeSegmentStart_ClampsToAccumulated() =>
        Assert.Equal(TimeSpan.FromMinutes(5),
            SessionRecovery.RecoveredElapsed(Record(TimeSpan.FromMinutes(5), T0.AddMinutes(10), T0.AddMinutes(8))));

    [Fact]
    public void ToFinishedSession_Running_EndsAtLastSeen()
    {
        var session = SessionRecovery.ToFinishedSession(Record(TimeSpan.FromMinutes(5), T0.AddMinutes(5), T0.AddMinutes(15)));

        Assert.Equal("Work", session.TaskName);
        Assert.Equal("tag", session.Tag);
        Assert.Equal(T0, session.StartedAt);
        Assert.Equal(T0.AddMinutes(15), session.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(15), session.Duration);
    }

    [Fact]
    public void ToFinishedSession_Paused_EndsAtLastSeen()
    {
        var session = SessionRecovery.ToFinishedSession(Record(TimeSpan.FromMinutes(5), null, T0.AddMinutes(30)));

        Assert.Equal(T0.AddMinutes(30), session.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), session.Duration);
    }

    [Fact]
    public void ToFinishedSession_RunningPastTarget_CapsAtTargetInstant()
    {
        // 5 min accumulated, segment from +5, target 20 min -> reached at +20 though last seen +40.
        var session = SessionRecovery.ToFinishedSession(
            Record(TimeSpan.FromMinutes(5), T0.AddMinutes(5), T0.AddMinutes(40), TimeSpan.FromMinutes(20)));

        Assert.Equal(TimeSpan.FromMinutes(20), session.Duration);
        Assert.Equal(T0.AddMinutes(20), session.EndedAt);
    }

    [Fact]
    public void ToFinishedSession_PausedAtOrPastTarget_CapsAndUsesLastSeen()
    {
        var session = SessionRecovery.ToFinishedSession(
            Record(TimeSpan.FromMinutes(25), null, T0.AddMinutes(60), TimeSpan.FromMinutes(20)));

        Assert.Equal(TimeSpan.FromMinutes(20), session.Duration);
        Assert.Equal(T0.AddMinutes(60), session.EndedAt);
    }

    [Fact]
    public void CanResume_DependsOnTarget()
    {
        Assert.True(SessionRecovery.CanResume(Record(TimeSpan.FromMinutes(5), null, T0)));
        Assert.True(SessionRecovery.CanResume(Record(TimeSpan.FromMinutes(5), null, T0, TimeSpan.FromMinutes(10))));
        Assert.False(SessionRecovery.CanResume(Record(TimeSpan.FromMinutes(10), null, T0, TimeSpan.FromMinutes(10))));
        Assert.False(SessionRecovery.CanResume(
            Record(TimeSpan.FromMinutes(5), T0, T0.AddMinutes(10), TimeSpan.FromMinutes(10))));
    }

    [Fact]
    public void ToResumeSnapshot_IsPausedWithRecoveredTime()
    {
        var record = Record(TimeSpan.FromMinutes(5), T0.AddMinutes(5), T0.AddMinutes(12), TimeSpan.FromMinutes(30));

        var snapshot = SessionRecovery.ToResumeSnapshot(record, T0.AddHours(5));

        Assert.Null(snapshot.SegmentStart);
        Assert.Equal(TimeSpan.FromMinutes(12), snapshot.Accumulated);
        Assert.Equal(record.Timer.StartedAt, snapshot.StartedAt);
        Assert.Equal(record.Timer.Target, snapshot.Target);

        var timer = new SessionTimer(new ManualTimeProvider(T0.AddHours(5)));
        timer.Restore(snapshot);
        Assert.Equal(TimerState.Paused, timer.State);
        Assert.Equal(TimeSpan.FromMinutes(12), timer.Elapsed);
    }

    [Fact]
    public void ToResumeSnapshot_TargetReached_Throws() =>
        Assert.Throws<InvalidOperationException>(() => SessionRecovery.ToResumeSnapshot(
            Record(TimeSpan.FromMinutes(10), null, T0, TimeSpan.FromMinutes(10)), T0));
}
