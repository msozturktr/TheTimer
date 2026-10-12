namespace TheTimer.Core.Sessions;

public interface ISessionRepository
{
    void Add(TimeSession session);

    /// <summary>Sessions whose start, converted to <paramref name="timeZone"/>, falls on <paramref name="day"/>; newest first.</summary>
    IReadOnlyList<TimeSession> GetByDay(DateOnly day, TimeZoneInfo timeZone);

    /// <summary>Sessions whose local start day is within [<paramref name="from"/>, <paramref name="toInclusive"/>]; oldest first.</summary>
    IReadOnlyList<TimeSession> GetByRange(DateOnly from, DateOnly toInclusive, TimeZoneInfo timeZone);

    /// <summary>Removes the session with the given id; returns true if one was removed.</summary>
    bool Delete(Guid id);
}
