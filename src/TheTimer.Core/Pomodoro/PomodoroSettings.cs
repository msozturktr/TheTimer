namespace TheTimer.Core.Pomodoro;

/// <summary>Pomodoro durations; every value must be positive.</summary>
public sealed record PomodoroSettings
{
    private readonly TimeSpan _work = TimeSpan.FromMinutes(25);
    private readonly TimeSpan _shortBreak = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _longBreak = TimeSpan.FromMinutes(15);
    private readonly int _longBreakEvery = 4;

    public TimeSpan Work
    {
        get => _work;
        init => _work = Positive(value);
    }

    public TimeSpan ShortBreak
    {
        get => _shortBreak;
        init => _shortBreak = Positive(value);
    }

    public TimeSpan LongBreak
    {
        get => _longBreak;
        init => _longBreak = Positive(value);
    }

    /// <summary>A long break follows every N-th completed work phase.</summary>
    public int LongBreakEvery
    {
        get => _longBreakEvery;
        init => _longBreakEvery = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "LongBreakEvery must be greater than zero.");
    }

    private static TimeSpan Positive(TimeSpan value) =>
        value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), "Duration must be greater than zero.");
}
