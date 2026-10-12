using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SessionSnapshotTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void CreateSnapshot_Idle_ReturnsNull() =>
        Assert.Null(new SessionTimer(_time).CreateSnapshot());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_Running_RoundTrips(bool withTarget)
    {
        var source = new SessionTimer(_time);
        source.Start("Work", "tag", withTarget ? TimeSpan.FromMinutes(25) : null);
        _time.Advance(TimeSpan.FromMinutes(3));
        source.Pause();
        _time.Advance(TimeSpan.FromMinutes(1));
        source.Resume();
        var snapshot = source.CreateSnapshot()!;

        Assert.NotNull(snapshot.SegmentStart);
        Assert.Equal(TimeSpan.FromMinutes(3), snapshot.Accumulated);

        var restored = new SessionTimer(_time);
        restored.Restore(snapshot);
        Assert.Equal(TimerState.Running, restored.State);
        Assert.Equal(snapshot, restored.CreateSnapshot());
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(5), restored.Elapsed);
        Assert.Equal(withTarget ? TimeSpan.FromMinutes(25) : null, restored.Target);
    }

    [Fact]
    public void Snapshot_Paused_RoundTrips()
    {
        var source = new SessionTimer(_time);
        source.Start("Work", null, TimeSpan.FromMinutes(10));
        _time.Advance(TimeSpan.FromMinutes(4));
        source.Pause();
        var snapshot = source.CreateSnapshot()!;

        Assert.Null(snapshot.SegmentStart);

        var restored = new SessionTimer(_time);
        var raised = 0;
        restored.StateChanged += (_, _) => raised++;
        restored.Restore(snapshot);
        Assert.Equal(1, raised);
        Assert.Equal(TimerState.Paused, restored.State);
        Assert.Equal(TimeSpan.FromMinutes(4), restored.Elapsed);
        Assert.Equal(snapshot, restored.CreateSnapshot());
    }

    [Fact]
    public void Restore_WhenNotIdle_Throws()
    {
        var timer = new SessionTimer(_time);
        timer.Start("Work", null);
        var snapshot = timer.CreateSnapshot()!;

        Assert.Throws<InvalidOperationException>(() => timer.Restore(snapshot));
    }

    [Fact]
    public void Restore_InvalidInput_Throws()
    {
        var timer = new SessionTimer(_time);
        var now = _time.GetUtcNow();

        Assert.Throws<ArgumentNullException>(() => timer.Restore(null!));
        Assert.Throws<ArgumentException>(() => timer.Restore(new TimerSnapshot(" ", null, now, TimeSpan.Zero, null, null)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timer.Restore(new TimerSnapshot("a", null, now, TimeSpan.FromSeconds(-1), null, null)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            timer.Restore(new TimerSnapshot("a", null, now, TimeSpan.Zero, null, TimeSpan.Zero)));
        Assert.Equal(TimerState.Idle, timer.State);
    }
}
