using System.Diagnostics;

namespace TheTimer.App.Services;

/// <summary>Plays the freedesktop "complete" sound through whichever player the system has; never throws.</summary>
public static class CompletionSound
{
    private const string SoundFile = "/usr/share/sounds/freedesktop/stereo/complete.oga";

    private static readonly (string Command, string Args)[] Players =
    [
        ("canberra-gtk-play", "--id=complete"),
        ("pw-play", SoundFile),
        ("paplay", SoundFile),
    ];

    public static void Play()
    {
        foreach (var (command, args) in Players)
        {
            if (args.StartsWith('/') && !File.Exists(args))
                continue;
            try
            {
                using var process = Process.Start(new ProcessStartInfo(command, args) { UseShellExecute = false });
                if (process is not null)
                    return;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Sound player '{command}' failed: {ex.Message}");
            }
        }
    }
}
