namespace TheTimer.Core.Sessions;

/// <summary>The single definition of "belongs to a local day", shared by all repositories.</summary>
internal static class SessionDayFilter
{
    public static DateOnly LocalDay(TimeSession session, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.StartedAt, timeZone).DateTime);

    public static bool IsOnDay(TimeSession session, DateOnly day, TimeZoneInfo timeZone) =>
        LocalDay(session, timeZone) == day;

    public static bool IsInRange(TimeSession session, DateOnly from, DateOnly toInclusive, TimeZoneInfo timeZone)
    {
        var local = LocalDay(session, timeZone);
        return local >= from && local <= toInclusive;
    }
}
