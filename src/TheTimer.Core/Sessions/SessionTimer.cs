namespace TheTimer.Core.Sessions;

/// <summary>Stopwatch state machine that produces a <see cref="TimeSession"/> on stop.</summary>
public sealed class SessionTimer
{
    private readonly TimeProvider _timeProvider;
    private TimeSpan _accumulated;
    private DateTimeOffset _segmentStart;

    public SessionTimer(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public event EventHandler? StateChanged;

    public TimerState State { get; private set; } = TimerState.Idle;

    public string? TaskName { get; private set; }

    public string? Tag { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public TimeSpan Elapsed => State switch
    {
        TimerState.Running => _accumulated + (_timeProvider.GetUtcNow() - _segmentStart),
        TimerState.Paused => _accumulated,
        _ => TimeSpan.Zero,
    };

    public void Start(string taskName, string? tag)
    {
        if (State != TimerState.Idle)
            throw new InvalidOperationException("Timer is already started.");
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name must not be empty.", nameof(taskName));

        var now = _timeProvider.GetUtcNow();
        TaskName = taskName.Trim();
        Tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
        StartedAt = now;
        _accumulated = TimeSpan.Zero;
        _segmentStart = now;
        State = TimerState.Running;
        OnStateChanged();
    }

    public void Pause()
    {
        if (State != TimerState.Running)
            throw new InvalidOperationException("Timer is not running.");

        _accumulated += _timeProvider.GetUtcNow() - _segmentStart;
        State = TimerState.Paused;
        OnStateChanged();
    }

    public void Resume()
    {
        if (State != TimerState.Paused)
            throw new InvalidOperationException("Timer is not paused.");

        _segmentStart = _timeProvider.GetUtcNow();
        State = TimerState.Running;
        OnStateChanged();
    }

    public TimeSession Stop()
    {
        if (State == TimerState.Idle)
            throw new InvalidOperationException("Timer is not started.");

        var now = _timeProvider.GetUtcNow();
        var duration = State == TimerState.Running ? _accumulated + (now - _segmentStart) : _accumulated;
        var session = new TimeSession(Guid.NewGuid(), TaskName!, Tag, StartedAt!.Value, now, duration);

        TaskName = null;
        Tag = null;
        StartedAt = null;
        _accumulated = TimeSpan.Zero;
        State = TimerState.Idle;
        OnStateChanged();
        return session;
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
