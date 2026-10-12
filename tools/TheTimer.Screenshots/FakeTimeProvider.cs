namespace TheTimer.Screenshots;

/// <summary>Settable clock pinned to a zone so screenshots are deterministic.</summary>
public sealed class FakeTimeProvider(DateTimeOffset start, TimeZoneInfo zone) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => zone;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class NoOpNotifier : TheTimer.Core.Notifications.INotifier
{
    public Task NotifyAsync(string title, string body, CancellationToken ct = default) => Task.CompletedTask;
}
