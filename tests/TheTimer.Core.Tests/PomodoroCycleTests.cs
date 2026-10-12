using TheTimer.Core.Pomodoro;

namespace TheTimer.Core.Tests;

public class PomodoroCycleTests
{
    [Fact]
    public void NewCycle_StartsWithWork()
    {
        var cycle = new PomodoroCycle();
        Assert.Equal(PomodoroPhase.Work, cycle.CurrentPhase);
        Assert.Equal(0, cycle.CompletedWorkCount);
        Assert.Equal(TimeSpan.FromMinutes(25), cycle.CurrentDuration);
    }

    [Fact]
    public void Advance_FollowsDefaultSequence()
    {
        var cycle = new PomodoroCycle();
        PomodoroPhase[] expected =
        [
            PomodoroPhase.Work, PomodoroPhase.ShortBreak,
            PomodoroPhase.Work, PomodoroPhase.ShortBreak,
            PomodoroPhase.Work, PomodoroPhase.ShortBreak,
            PomodoroPhase.Work, PomodoroPhase.LongBreak,
            PomodoroPhase.Work,
        ];

        foreach (var phase in expected)
        {
            Assert.Equal(phase, cycle.CurrentPhase);
            cycle.Advance();
        }

        Assert.Equal(PomodoroPhase.ShortBreak, cycle.CurrentPhase);
        Assert.Equal(5, cycle.CompletedWorkCount);
    }

    [Fact]
    public void CurrentDuration_FollowsPhase()
    {
        var cycle = new PomodoroCycle();
        cycle.Advance();
        Assert.Equal(TimeSpan.FromMinutes(5), cycle.CurrentDuration);
        cycle.Advance();
        cycle.Advance();
        cycle.Advance();
        cycle.Advance();
        cycle.Advance();
        cycle.Advance();
        Assert.Equal(PomodoroPhase.LongBreak, cycle.CurrentPhase);
        Assert.Equal(TimeSpan.FromMinutes(15), cycle.CurrentDuration);
    }

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var cycle = new PomodoroCycle();
        for (var i = 0; i < 5; i++)
            cycle.Advance();
        cycle.Reset();
        Assert.Equal(PomodoroPhase.Work, cycle.CurrentPhase);
        Assert.Equal(0, cycle.CompletedWorkCount);
    }

    [Fact]
    public void CustomSettings_AreUsed()
    {
        var cycle = new PomodoroCycle(new PomodoroSettings
        {
            Work = TimeSpan.FromMinutes(10),
            ShortBreak = TimeSpan.FromMinutes(2),
            LongBreak = TimeSpan.FromMinutes(7),
            LongBreakEvery = 2,
        });
        Assert.Equal(TimeSpan.FromMinutes(10), cycle.CurrentDuration);
        cycle.Advance();
        Assert.Equal(PomodoroPhase.ShortBreak, cycle.CurrentPhase);
        Assert.Equal(TimeSpan.FromMinutes(2), cycle.CurrentDuration);
        cycle.Advance();
        cycle.Advance();
        Assert.Equal(PomodoroPhase.LongBreak, cycle.CurrentPhase);
        Assert.Equal(TimeSpan.FromMinutes(7), cycle.CurrentDuration);
    }

    [Fact]
    public void InvalidSettings_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PomodoroSettings { Work = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PomodoroSettings { ShortBreak = TimeSpan.FromMinutes(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PomodoroSettings { LongBreak = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PomodoroSettings { LongBreakEvery = 0 });
    }
}
