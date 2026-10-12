using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SessionTimerTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly SessionTimer _timer;

    public SessionTimerTests() => _timer = new SessionTimer(_time);

    [Fact]
    public void NewTimer_IsIdle()
    {
        Assert.Equal(TimerState.Idle, _timer.State);
        Assert.Null(_timer.TaskName);
        Assert.Null(_timer.StartedAt);
        Assert.Equal(TimeSpan.Zero, _timer.Elapsed);
    }

    [Fact]
    public void Elapsed_TracksRunningTime()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal(TimerState.Running, _timer.State);
        Assert.Equal(TimeSpan.FromSeconds(90), _timer.Elapsed);
    }

    [Fact]
    public void Pause_FreezesElapsed()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromSeconds(10));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimerState.Paused, _timer.State);
        Assert.Equal(TimeSpan.FromSeconds(10), _timer.Elapsed);
    }

    [Fact]
    public void Resume_ContinuesFromFrozenValue()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromSeconds(10));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));
        _timer.Resume();
        _time.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal(TimeSpan.FromSeconds(17), _timer.Elapsed);
    }

    [Fact]
    public void Stop_AfterMultiplePauses_DurationExcludesPauses()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromMinutes(1));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(10));
        _timer.Resume();
        _time.Advance(TimeSpan.FromMinutes(2));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(20));
        _timer.Resume();
        _time.Advance(TimeSpan.FromMinutes(3));

        var session = _timer.Stop();

        Assert.Equal(TimeSpan.FromMinutes(6), session.Duration);
    }

    [Fact]
    public void Stop_AfterPause_DurationExcludesPausedTime()
    {
        _timer.Start("Work", null);
        _time.Advance(TimeSpan.FromMinutes(10));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));
        _timer.Resume();
        _time.Advance(TimeSpan.FromMinutes(20));

        var session = _timer.Stop();

        Assert.Equal(TimeSpan.FromMinutes(30), session.Duration);
        Assert.Equal(session.Duration, session.EndedAt - session.StartedAt - TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Stop_ReturnsSessionAndResetsToIdle()
    {
        var start = _time.GetUtcNow();
        _timer.Start("Work", "dev");
        _time.Advance(TimeSpan.FromMinutes(5));
        _timer.Pause();
        _time.Advance(TimeSpan.FromMinutes(5));

        var session = _timer.Stop();

        Assert.Equal("Work", session.TaskName);
        Assert.Equal("dev", session.Tag);
        Assert.Equal(start, session.StartedAt);
        Assert.Equal(start + TimeSpan.FromMinutes(10), session.EndedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), session.Duration);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(TimerState.Idle, _timer.State);
        Assert.Equal(TimeSpan.Zero, _timer.Elapsed);
        Assert.Null(_timer.TaskName);
        Assert.Null(_timer.Tag);
        Assert.Null(_timer.StartedAt);
    }

    [Fact]
    public void Start_NormalizesTaskNameAndTag()
    {
        _timer.Start("  Work  ", "   ");
        Assert.Equal("Work", _timer.TaskName);
        Assert.Null(_timer.Tag);
    }

    [Fact]
    public void Start_TrimsTag()
    {
        _timer.Start("Work", "  dev ");
        Assert.Equal("dev", _timer.Tag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Start_BlankTaskName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => _timer.Start(name!, null));
        Assert.Equal(TimerState.Idle, _timer.State);
    }

    [Fact]
    public void InvalidTransitions_FromIdle_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => _timer.Pause());
        Assert.Throws<InvalidOperationException>(() => _timer.Resume());
        Assert.Throws<InvalidOperationException>(() => _timer.Stop());
    }

    [Fact]
    public void InvalidTransitions_FromRunning_Throw()
    {
        _timer.Start("Work", null);
        Assert.Throws<InvalidOperationException>(() => _timer.Start("Other", null));
        Assert.Throws<InvalidOperationException>(() => _timer.Resume());
    }

    [Fact]
    public void InvalidTransitions_FromPaused_Throw()
    {
        _timer.Start("Work", null);
        _timer.Pause();
        Assert.Throws<InvalidOperationException>(() => _timer.Start("Other", null));
        Assert.Throws<InvalidOperationException>(() => _timer.Pause());
    }

    [Fact]
    public void StateChanged_RaisedOnEachTransition()
    {
        var states = new List<TimerState>();
        _timer.StateChanged += (_, _) => states.Add(_timer.State);

        _timer.Start("Work", null);
        _timer.Pause();
        _timer.Resume();
        _timer.Stop();

        Assert.Equal(
            [TimerState.Running, TimerState.Paused, TimerState.Running, TimerState.Idle],
            states);
    }

    [Fact]
    public void StateChanged_NotRaisedOnFailedTransition()
    {
        var count = 0;
        _timer.StateChanged += (_, _) => count++;
        Assert.Throws<InvalidOperationException>(() => _timer.Pause());
        Assert.Throws<ArgumentException>(() => _timer.Start(" ", null));
        Assert.Equal(0, count);
    }
}
