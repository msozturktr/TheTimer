namespace TheTimer.Core.Sessions;

/// <summary>The single definition of "belongs to a local day", shared by all repositories.</summary>
internal static class SessionDayFilter
{
    public static bool IsOnDay(TimeSession session, DateOnly day, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.StartedAt, timeZone).DateTime) == day;
}
