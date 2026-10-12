using Microsoft.Data.Sqlite;
using TheTimer.Core.Pomodoro;
using TheTimer.Core.Sessions;
using TheTimer.Core.Storage;

namespace TheTimer.Core.Tests;

public class ActiveSessionStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "thetimer-tests-" + Guid.NewGuid().ToString("N"));

    private string DbPath => Path.Combine(_directory, "active.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static ActiveSessionRecord Running() => new(
        new TimerSnapshot("Work", "tag", T0, TimeSpan.FromTicks(1234567), T0.AddMinutes(2), TimeSpan.FromMinutes(25)),
        T0.AddMinutes(5), "Pomodoro", true, PomodoroPhase.ShortBreak, 3);

    private static ActiveSessionRecord Paused() => new(
        new TimerSnapshot("Other", null, T0, TimeSpan.FromMinutes(7), null, null),
        T0.AddMinutes(9), "Stopwatch", false, PomodoroPhase.Work, 0);

    private static long ScalarLong(string path, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    [Fact]
    public void Sqlite_MigratesV1Database_KeepsSessions()
    {
        Directory.CreateDirectory(_directory);
        var id = Guid.NewGuid();
        using (var connection = new SqliteConnection($"Data Source={DbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE sessions (id TEXT PRIMARY KEY, task_name TEXT NOT NULL, tag TEXT NULL, " +
                "started_at_utc_ticks INTEGER NOT NULL, ended_at_utc_ticks INTEGER NOT NULL, duration_ticks INTEGER NOT NULL); " +
                "CREATE INDEX ix_sessions_started_at ON sessions (started_at_utc_ticks); " +
                $"INSERT INTO sessions VALUES ('{id}', 'Old', NULL, {T0.UtcTicks}, {T0.AddMinutes(10).UtcTicks}, {TimeSpan.FromMinutes(9).Ticks}); " +
                "PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }

        var db = new SqliteDatabase(DbPath);
        var repository = new SqliteSessionRepository(db);

        Assert.Equal(3, ScalarLong(DbPath, "PRAGMA user_version"));
        var sessions = repository.GetByDay(DateOnly.FromDateTime(T0.UtcDateTime), TimeZoneInfo.Utc);
        Assert.Equal(id, Assert.Single(sessions).Id);
        Assert.Null(new SqliteActiveSessionStore(db).Load());
    }

    [Fact]
    public void Sqlite_SaveLoad_RoundTrips()
    {
        var store = new SqliteActiveSessionStore(new SqliteDatabase(DbPath));

        store.Save(Running());
        Assert.Equal(Running(), store.Load());

        store.Save(Paused());
        Assert.Equal(Paused(), store.Load());
    }

    [Fact]
    public void Sqlite_Save_Upserts_SingleRow()
    {
        var store = new SqliteActiveSessionStore(new SqliteDatabase(DbPath));

        store.Save(Running());
        store.Save(Paused());

        Assert.Equal(1, ScalarLong(DbPath, "SELECT COUNT(*) FROM active_session"));
    }

    [Fact]
    public void Sqlite_Touch_UpdatesOnlyLastSeen()
    {
        var store = new SqliteActiveSessionStore(new SqliteDatabase(DbPath));
        store.Save(Running());

        store.Touch(T0.AddMinutes(30));

        Assert.Equal(Running() with { LastSeen = T0.AddMinutes(30) }, store.Load());
    }

    [Fact]
    public void Sqlite_Touch_WithNothingStored_IsNoOp()
    {
        var store = new SqliteActiveSessionStore(new SqliteDatabase(DbPath));

        store.Touch(T0);

        Assert.Null(store.Load());
        Assert.Equal(0, ScalarLong(DbPath, "SELECT COUNT(*) FROM active_session"));
    }

    [Fact]
    public void Sqlite_Clear_RemovesRecord()
    {
        var store = new SqliteActiveSessionStore(new SqliteDatabase(DbPath));
        store.Save(Running());

        store.Clear();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Sqlite_RecordSurvivesReopen()
    {
        new SqliteActiveSessionStore(new SqliteDatabase(DbPath)).Save(Running());
        SqliteConnection.ClearAllPools();

        Assert.Equal(Running(), new SqliteActiveSessionStore(new SqliteDatabase(DbPath)).Load());
    }

    [Fact]
    public void InMemory_SaveLoadTouchClear()
    {
        var store = new InMemoryActiveSessionStore();
        Assert.Null(store.Load());
        store.Touch(T0);
        Assert.Null(store.Load());

        store.Save(Running());
        Assert.Equal(Running(), store.Load());

        store.Touch(T0.AddHours(1));
        Assert.Equal(T0.AddHours(1), store.Load()!.LastSeen);

        store.Clear();
        Assert.Null(store.Load());
    }
}
