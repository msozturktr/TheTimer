using Microsoft.Data.Sqlite;

namespace TheTimer.Core.Sessions;

/// <summary>
/// SQLite-backed session store. Instants are stored as UTC ticks and read back with a zero offset;
/// <see cref="DateTimeOffset"/> equality compares instants, so round-tripped sessions are equal.
/// </summary>
public sealed class SqliteSessionRepository : ISessionRepository
{
    /// <summary>Schema version this build understands. Add a step to <see cref="Migrate"/> when bumping it.</summary>
    private const int CurrentSchemaVersion = 1;

    private readonly string _connectionString;

    public SqliteSessionRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();

        Initialize();
    }

    public void Add(TimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var connection = Open();
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

    public IReadOnlyList<TimeSession> GetByDay(DateOnly day, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        // Coarse range: local day +/- 1 day, converted to UTC ticks. The padding makes it DST-safe;
        // the exact filter is applied in memory with the same predicate as the in-memory repository.
        var localStart = day.ToDateTime(TimeOnly.MinValue);
        var rangeStart = ToUtcTicksClamped(localStart.AddDays(-1), timeZone);
        var rangeEnd = ToUtcTicksClamped(localStart.AddDays(2), timeZone);

        var result = new List<TimeSession>();
        using var connection = Open();
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
            if (SessionDayFilter.IsOnDay(session, day, timeZone))
                result.Add(session);
        }

        return result.OrderByDescending(s => s.StartedAt).ToList();
    }

    public bool Delete(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    private static long ToUtcTicksClamped(DateTime unspecifiedLocal, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(unspecifiedLocal, DateTimeKind.Unspecified);
        // Invalid (DST gap) local times are shifted forward by the zone itself; ambiguous ones take the standard offset.
        var offset = timeZone.IsInvalidTime(unspecified)
            ? timeZone.GetUtcOffset(unspecified.AddHours(1))
            : timeZone.GetUtcOffset(unspecified);
        var ticks = unspecified.Ticks - offset.Ticks;
        return Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = Open();

        using (var wal = connection.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL";
            wal.ExecuteNonQuery();
        }

        Migrate(connection);
    }

    private static void Migrate(SqliteConnection connection)
    {
        var version = GetUserVersion(connection);
        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException(
                $"Database schema version {version} is newer than supported version {CurrentSchemaVersion}.");

        while (version < CurrentSchemaVersion)
        {
            using var transaction = connection.BeginTransaction();
            var next = version + 1;
            switch (next)
            {
                case 1:
                    Execute(connection, transaction,
                        "CREATE TABLE sessions (" +
                        "id TEXT PRIMARY KEY, " +
                        "task_name TEXT NOT NULL, " +
                        "tag TEXT NULL, " +
                        "started_at_utc_ticks INTEGER NOT NULL, " +
                        "ended_at_utc_ticks INTEGER NOT NULL, " +
                        "duration_ticks INTEGER NOT NULL)");
                    Execute(connection, transaction,
                        "CREATE INDEX ix_sessions_started_at ON sessions (started_at_utc_ticks)");
                    break;
                // case 2: add the next migration step here and bump CurrentSchemaVersion.
                default:
                    throw new InvalidOperationException($"No migration defined for schema version {next}.");
            }

            // PRAGMA does not accept parameters; the value is an int we control.
            Execute(connection, transaction, $"PRAGMA user_version = {next}");
            transaction.Commit();
            version = next;
        }
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
