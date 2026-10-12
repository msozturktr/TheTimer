namespace TheTimer.Core.Pomodoro;

/// <summary>Pure Pomodoro phase sequence; owns no clock.</summary>
public sealed class PomodoroCycle
{
    public PomodoroCycle(PomodoroSettings? settings = null)
    {
        Settings = settings ?? new PomodoroSettings();
    }

    public PomodoroSettings Settings { get; }

    public PomodoroPhase CurrentPhase { get; private set; } = PomodoroPhase.Work;

    public int CompletedWorkCount { get; private set; }

    public TimeSpan CurrentDuration => CurrentPhase switch
    {
        PomodoroPhase.Work => Settings.Work,
        PomodoroPhase.ShortBreak => Settings.ShortBreak,
        _ => Settings.LongBreak,
    };

    public void Advance()
    {
        if (CurrentPhase == PomodoroPhase.Work)
        {
            CompletedWorkCount++;
            CurrentPhase = CompletedWorkCount % Settings.LongBreakEvery == 0
                ? PomodoroPhase.LongBreak
                : PomodoroPhase.ShortBreak;
        }
        else
        {
            CurrentPhase = PomodoroPhase.Work;
        }
    }

    public void Reset()
    {
        CurrentPhase = PomodoroPhase.Work;
        CompletedWorkCount = 0;
    }
}
