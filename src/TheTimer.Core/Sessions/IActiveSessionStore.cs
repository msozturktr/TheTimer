namespace TheTimer.Core.Sessions;

/// <summary>Holds at most one active-session record so a crash, kill or logout does not lose the session.</summary>
public interface IActiveSessionStore
{
    /// <summary>Stores the record, replacing any previous one.</summary>
    void Save(ActiveSessionRecord record);

    /// <summary>The stored record, or null when there is none.</summary>
    ActiveSessionRecord? Load();

    void Clear();

    /// <summary>Updates only the heartbeat of the stored record; does nothing if none is stored.</summary>
    void Touch(DateTimeOffset lastSeen);
}
