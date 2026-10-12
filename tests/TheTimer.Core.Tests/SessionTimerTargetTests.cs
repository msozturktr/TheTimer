using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SessionTimerTargetTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly SessionTimer _timer;

    public SessionTimerTargetTests() => _timer = new SessionTimer(_time);

    [Fact]
    public void Remaining_IsNullWhenIdleOrNoTarget()
    {
        Assert.Null(_timer.Remaining);
        _timer.Start("Work", null);
        Assert.Null(_timer.Target);
        Assert.Null(_timer.Remaining);
    }

    [Fact]
    public void Remaining_CountsDownAndClampsAtZero()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(10));
        Assert.Equal(TimeSpan.FromMinutes(10), _timer.Remaining);
        _time.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(TimeSpan.FromMinutes(6), _timer.Remaining);
        _time.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(TimeSpan.Zero, _timer.Remaining);
    }

    [Fact]
    public void Remaining_FreezesWhilePaused()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(10));
        _time.Advance(TimeSpan.FromMinutes(3));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(7), _timer.Remaining);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Start_InvalidTarget_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _timer.Start("Work", null, TimeSpan.FromSeconds(seconds)));
        Assert.Equal(TimerState.Idle, _timer.State);
    }

    [Fact]
    public void CheckTarget_BeforeTarget_ReturnsNullAndKeepsRunning()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.Null(_timer.CheckTarget());
        Assert.Equal(TimerState.Running, _timer.State);
    }

    [Fact]
    public void CheckTarget_AtTarget_FinishesSession()
    {
        _timer.Start("Work", "tag", TimeSpan.FromMinutes(1));
        var start = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromMinutes(1));

        var session = _timer.CheckTarget();

        Assert.NotNull(session);
        Assert.Equal("Work", session.TaskName);
        Assert.Equal("tag", session.Tag);
        Assert.Equal(TimeSpan.FromMinutes(1), session.Duration);
        Assert.Equal(start + TimeSpan.FromMinutes(1), session.EndedAt);
        Assert.Equal(TimerState.Idle, _timer.State);
        Assert.Null(_timer.Target);
    }

    [Fact]
    public void CheckTarget_CheckedLate_EndsExactlyAtTarget()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(1));
        var start = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromSeconds(63));

        var session = _timer.CheckTarget();

        Assert.NotNull(session);
        Assert.Equal(TimeSpan.FromMinutes(1), session.Duration);
        Assert.Equal(start + TimeSpan.FromMinutes(1), session.EndedAt);
    }

    [Fact]
    public void CheckTarget_AfterPauseResume_AccountsForAccumulatedTime()
    {
        _timer.Start("Work", null, TimeSpan.FromSeconds(100));
        _time.Advance(TimeSpan.FromSeconds(40));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));
        _timer.Resume();
        var resumedAt = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromSeconds(63));

        var session = _timer.CheckTarget();

        Assert.NotNull(session);
        Assert.Equal(TimeSpan.FromSeconds(100), session.Duration);
        Assert.Equal(resumedAt + TimeSpan.FromSeconds(60), session.EndedAt);
    }

    [Fact]
    public void CheckTarget_WhilePaused_NeverCompletes()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromSeconds(30));
        _timer.Pause();
        _time.Advance(TimeSpan.FromHours(1));
        Assert.Null(_timer.CheckTarget());
        Assert.Equal(TimerState.Paused, _timer.State);
    }

    [Fact]
    public void CheckTarget_WithoutTarget_ReturnsNull()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromHours(5));
        Assert.Null(_timer.CheckTarget());
        Assert.Equal(TimerState.Running, _timer.State);
    }

    [Fact]
    public void TargetReached_RaisedOnce()
    {
        var raised = new List<TimeSession>();
        _timer.TargetReached += (_, s) => raised.Add(s);
        _timer.Start("Work", null, TimeSpan.FromSeconds(10));
        _time.Advance(TimeSpan.FromSeconds(11));

        var session = _timer.CheckTarget();
        Assert.Null(_timer.CheckTarget());

        Assert.Single(raised);
        Assert.Same(session, raised[0]);
    }

    [Fact]
    public void Stop_EarlyWithTarget_UsesActualDuration()
    {
        _timer.Start("Work", null, TimeSpan.FromMinutes(10));
        _time.Advance(TimeSpan.FromMinutes(2));
        var session = _timer.Stop();
        Assert.Equal(TimeSpan.FromMinutes(2), session.Duration);
        Assert.Null(_timer.Target);
    }
}
