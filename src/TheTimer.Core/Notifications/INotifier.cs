namespace TheTimer.Core.Notifications;

/// <summary>Shows a desktop notification. Implementations must not throw on delivery failure.</summary>
public interface INotifier
{
    Task NotifyAsync(string title, string body, CancellationToken ct = default);
}
