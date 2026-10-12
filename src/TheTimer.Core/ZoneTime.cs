namespace TheTimer.Core;

/// <summary>The single local-wall-clock to instant conversion, shared by repositories and planning.</summary>
public static class ZoneTime
{
    /// <summary>
    /// Resolves a local wall-clock time in <paramref name="timeZone"/>. Times in a DST gap are shifted forward
    /// (resolved with the pre-gap offset, so they land after the gap); ambiguous times take the standard offset.
    /// </summary>
    public static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        return new DateTimeOffset(ToUtcTicksClamped(local, timeZone), TimeSpan.Zero);
    }

    public static DateTimeOffset ToInstant(DateOnly day, TimeOnly time, TimeZoneInfo timeZone) =>
        ToInstant(day.ToDateTime(time), timeZone);

    /// <summary>Same resolution as <see cref="ToInstant(DateTime, TimeZoneInfo)"/>, as UTC ticks clamped to the valid range.</summary>
    public static long ToUtcTicksClamped(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // For invalid (gap) and ambiguous local times the zone returns its standard offset.
        var offset = timeZone.GetUtcOffset(unspecified);
        var ticks = unspecified.Ticks - offset.Ticks;
        return Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
    }
}
