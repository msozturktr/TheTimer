namespace TheTimer.Core.Sessions;

public interface ISessionRepository
{
    void Add(TimeSession session);

    /// <summary>Sessions whose start, converted to <paramref name="timeZone"/>, falls on <paramref name="day"/>; newest first.</summary>
    IReadOnlyList<TimeSession> GetByDay(DateOnly day, TimeZoneInfo timeZone);

    /// <summary>Removes the session with the given id; returns true if one was removed.</summary>
    bool Delete(Guid id);
}
