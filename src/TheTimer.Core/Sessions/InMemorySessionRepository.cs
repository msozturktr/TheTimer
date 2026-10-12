namespace TheTimer.Core.Sessions;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly List<TimeSession> _sessions = [];

    public void Add(TimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions.Add(session);
    }

    public IReadOnlyList<TimeSession> GetByDay(DateOnly day, TimeZoneInfo timeZone) =>
        _sessions
            .Where(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.StartedAt, timeZone).DateTime) == day)
            .OrderByDescending(s => s.StartedAt)
            .ToList();
}
