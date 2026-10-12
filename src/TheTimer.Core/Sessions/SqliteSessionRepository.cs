using TheTimer.Core.Storage;

namespace TheTimer.Core.Sessions;

/// <summary>
/// SQLite-backed session store. Instants are stored as UTC ticks and read back with a zero offset;
/// <see cref="DateTimeOffset"/> equality compares instants, so round-tripped sessions are equal.
/// </summary>
public sealed class SqliteSessionRepository : ISessionRepository
{
    private readonly SqliteDatabase _db;

    public SqliteSessionRepository(SqliteDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public void Add(TimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO sessions (id, task_name, tag, started_at_utc_ticks, ended_at_utc_ticks, duration_ticks) " +
            "VALUES ($id, $task, $tag, $started, $ended, $duration)";
        command.Parameters.AddWithValue("$id", session.Id.ToString());
        command.Parameters.AddWithValue("$task", session.TaskName);
        command.Parameters.AddWithValue("$tag", (object?)session.Tag ?? DBNull.Value);
        command.Parameters.AddWithValue("$started", session.StartedAt.UtcTicks);
        command.Parameters.AddWithValue("$ended", session.EndedAt.UtcTicks);
        command.Parameters.AddWithValue("$duration", session.Duration.Ticks);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TimeSession> GetByDay(DateOnly day, TimeZoneInfo timeZone) =>
        Query(day, day, timeZone).OrderByDescending(s => s.StartedAt).ToList();

    public IReadOnlyList<TimeSession> GetByRange(DateOnly from, DateOnly toInclusive, TimeZoneInfo timeZone) =>
        Query(from, toInclusive, timeZone).OrderBy(s => s.StartedAt).ToList();

    private List<TimeSession> Query(DateOnly from, DateOnly toInclusive, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var result = new List<TimeSession>();
        if (toInclusive < from)
            return result;

        // Coarse range: local range +/- 1 day, converted to UTC ticks. The padding makes it DST-safe;
        // the exact filter is applied in memory with the same predicate as the in-memory repository.
        var rangeStart = ZoneTime.ToUtcTicksClamped(from.ToDateTime(TimeOnly.MinValue).AddDays(-1), timeZone);
        var rangeEnd = ZoneTime.ToUtcTicksClamped(toInclusive.ToDateTime(TimeOnly.MinValue).AddDays(2), timeZone);

        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, task_name, tag, started_at_utc_ticks, ended_at_utc_ticks, duration_ticks " +
            "FROM sessions WHERE started_at_utc_ticks >= $from AND started_at_utc_ticks < $to";
        command.Parameters.AddWithValue("$from", rangeStart);
        command.Parameters.AddWithValue("$to", rangeEnd);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var session = new TimeSession(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                new DateTimeOffset(reader.GetInt64(3), TimeSpan.Zero),
                new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero),
                TimeSpan.FromTicks(reader.GetInt64(5)));
            if (SessionDayFilter.IsInRange(session, from, toInclusive, timeZone))
                result.Add(session);
        }

        return result;
    }

    public bool Delete(Guid id)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }
}
