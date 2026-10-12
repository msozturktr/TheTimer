using Microsoft.Data.Sqlite;

namespace TheTimer.Core.Storage;

/// <summary>
/// Owns the SQLite file: connection string, WAL mode and all schema migrations (<c>PRAGMA user_version</c>).
/// Repositories share one instance and call <see cref="Open"/> per operation.
/// </summary>
public sealed class SqliteDatabase
{
    /// <summary>Schema version this build understands. Add a step to <see cref="Migrate"/> when bumping it.</summary>
    internal const int CurrentSchemaVersion = 3;

    private readonly string _connectionString;

    public SqliteDatabase(string databasePath)
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

    /// <summary>Opens a new connection (foreign keys enforced). The caller disposes it.</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // Per-connection setting; pooled connections are reset, so set it every time.
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON";
        command.ExecuteNonQuery();
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
                case 2:
                    Execute(connection, transaction,
                        "CREATE TABLE active_session (" +
                        "id INTEGER PRIMARY KEY CHECK (id = 1), " +
                        "task_name TEXT NOT NULL, " +
                        "tag TEXT NULL, " +
                        "started_at_utc_ticks INTEGER NOT NULL, " +
                        "accumulated_ticks INTEGER NOT NULL, " +
                        "segment_start_utc_ticks INTEGER NULL, " +
                        "target_ticks INTEGER NULL, " +
                        "last_seen_utc_ticks INTEGER NOT NULL, " +
                        "mode TEXT NOT NULL, " +
                        "is_break INTEGER NOT NULL, " +
                        "pomodoro_phase INTEGER NOT NULL, " +
                        "pomodoro_completed INTEGER NOT NULL)");
                    break;
                case 3:
                    Execute(connection, transaction,
                        "CREATE TABLE plan_blocks (" +
                        "id TEXT PRIMARY KEY, " +
                        "task_name TEXT NOT NULL, " +
                        "tag TEXT NULL, " +
                        "start_minute INTEGER NOT NULL, " +
                        "end_minute INTEGER NOT NULL, " +
                        "date_day INTEGER NULL, " +
                        "rec_start_day INTEGER NULL, " +
                        "rec_end_day INTEGER NULL, " +
                        "rec_days INTEGER NULL)");
                    Execute(connection, transaction, "CREATE INDEX ix_plan_blocks_date_day ON plan_blocks (date_day)");
                    Execute(connection, transaction, "CREATE INDEX ix_plan_blocks_rec_start_day ON plan_blocks (rec_start_day)");
                    Execute(connection, transaction,
                        "CREATE TABLE plan_block_exceptions (" +
                        "block_id TEXT NOT NULL REFERENCES plan_blocks(id) ON DELETE CASCADE, " +
                        "day INTEGER NOT NULL, " +
                        "PRIMARY KEY (block_id, day))");
                    break;
                // case 4: add the next migration step here and bump CurrentSchemaVersion.
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
