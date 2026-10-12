using System.Diagnostics;
using Tmds.DBus.Protocol;
using TheTimer.Core.Notifications;

namespace TheTimer.App.Services;

/// <summary>Desktop notifications via the freedesktop <c>org.freedesktop.Notifications</c> D-Bus service.</summary>
public sealed class FreedesktopNotifier : INotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string Path = "/org/freedesktop/Notifications";

    public async Task NotifyAsync(string title, string body, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var connection = DBusConnection.Session;
            await connection.CallMethodAsync(CreateNotifyMessage(connection, title, body))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Notification failed: {ex}");
        }
    }

    private static MessageBuffer CreateNotifyMessage(DBusConnection connection, string title, string body)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: Service,
            path: Path,
            @interface: Service,
            member: "Notify",
            signature: "susssasa{sv}i");
        writer.WriteString("TheTimer");
        writer.WriteUInt32(0);
        writer.WriteString(string.Empty);
        writer.WriteString(title);
        writer.WriteString(body);
        writer.WriteArray(Array.Empty<string>());
        writer.WriteDictionary(new Dictionary<string, VariantValue>());
        writer.WriteInt32(-1);
        return writer.CreateMessage();
    }
}
