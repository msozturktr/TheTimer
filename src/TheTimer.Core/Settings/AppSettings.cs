namespace TheTimer.Core.Settings;

/// <summary>User preferences persisted as JSON next to the database.</summary>
public sealed record AppSettings
{
    public bool SoundEnabled { get; init; } = true;

    /// <summary>Id of the completion sound (file name without extension under Assets/Sounds).</summary>
    public string Sound { get; init; } = "chime";
}
