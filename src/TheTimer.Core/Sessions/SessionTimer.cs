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

    /// <summary>Raised when a timer with a target completes via <see cref="CheckTarget"/>.</summary>
    public event EventHandler<TimeSession>? TargetReached;

    public TimerState State { get; private set; } = TimerState.Idle;

    public string? TaskName { get; private set; }

    public string? Tag { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public TimeSpan? Target { get; private set; }

    public TimeSpan? Remaining => State == TimerState.Idle || Target is null
        ? null
        : TimeSpan.FromTicks(Math.Max(0, (Target.Value - Elapsed).Ticks));

    public TimeSpan Elapsed => State switch
    {
        TimerState.Running => _accumulated + (_timeProvider.GetUtcNow() - _segmentStart),
        TimerState.Paused => _accumulated,
        _ => TimeSpan.Zero,
    };

    public void Start(string taskName, string? tag, TimeSpan? target = null)
    {
        if (State != TimerState.Idle)
            throw new InvalidOperationException("Timer is already started.");
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Task name must not be empty.", nameof(taskName));
        if (target is { } t && t <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(target), "Target must be greater than zero.");

        var now = _timeProvider.GetUtcNow();
        TaskName = taskName.Trim();
        Tag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
        StartedAt = now;
        Target = target;
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

        Reset();
        OnStateChanged();
        return session;
    }

    /// <summary>
    /// Finishes the session exactly at the target if it has been reached; otherwise changes nothing.
    /// Paused timers never complete.
    /// </summary>
    public TimeSession? CheckTarget()
    {
        if (State != TimerState.Running || Target is not { } target || Elapsed < target)
            return null;

        var endedAt = _segmentStart + (target - _accumulated);
        var session = new TimeSession(Guid.NewGuid(), TaskName!, Tag, StartedAt!.Value, endedAt, target);

        Reset();
        OnStateChanged();
        TargetReached?.Invoke(this, session);
        return session;
    }

    private void Reset()
    {
        TaskName = null;
        Tag = null;
        StartedAt = null;
        Target = null;
        _accumulated = TimeSpan.Zero;
        State = TimerState.Idle;
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
