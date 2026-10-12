using Microsoft.Data.Sqlite;
using TheTimer.Core.Planning;
using TheTimer.Core.Storage;

namespace TheTimer.Core.Tests;

public abstract class PlanRepositoryTestsBase : IDisposable
{
    protected static readonly DateOnly Mon = new(2026, 10, 12);

    protected abstract IPlanRepository Create();

    public virtual void Dispose() { }

    protected static PlanBlock OneOff(DateOnly day, string name = "a") =>
        new(Guid.NewGuid(), name, null, new TimeOnly(9, 0), new TimeOnly(10, 0), day, null);

    protected static PlanBlock Series(DateOnly start, DateOnly? end, string name = "s") =>
        new(Guid.NewGuid(), name, "tag", new TimeOnly(8, 15), new TimeOnly(23, 59), null,
            new PlanRecurrence(start, end, WeekDays.Monday | WeekDays.Friday));

    [Fact]
    public void AddThenGet_RoundTripsAllFields()
    {
        var repo = Create();
        var one = new PlanBlock(Guid.NewGuid(), "One", "t", new TimeOnly(0, 0), new TimeOnly(0, 1), Mon, null);
        var series = Series(Mon, Mon.AddDays(30));
        var open = Series(Mon, null, "open");
        repo.Add(one);
        repo.Add(series);
        repo.Add(open);

        Assert.Equal(one, repo.Get(one.Id));
        Assert.Equal(series, repo.Get(series.Id));
        Assert.Equal(open, repo.Get(open.Id));
        Assert.Null(repo.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Update_ReplacesBlock_UnknownThrows()
    {
        var repo = Create();
        var block = OneOff(Mon);
        repo.Add(block);
        var changed = block with { TaskName = "changed", Tag = "x" };

        repo.Update(changed);

        Assert.Equal(changed, repo.Get(block.Id));
        Assert.Throws<InvalidOperationException>(() => repo.Update(OneOff(Mon)));
    }

    [Fact]
    public void Update_CanSwitchRecurrenceBounds()
    {
        var repo = Create();
        var series = Series(Mon, null);
        repo.Add(series);
        var ended = series with { Recurrence = series.Recurrence! with { EndDate = Mon.AddDays(7) } };

        repo.Update(ended);

        Assert.Equal(Mon.AddDays(7), repo.Get(series.Id)!.Recurrence!.EndDate);
    }

    [Fact]
    public void Delete_ReturnsWhetherRemoved()
    {
        var repo = Create();
        var b = OneOff(Mon);
        repo.Add(b);

        Assert.True(repo.Delete(b.Id));
        Assert.False(repo.Delete(b.Id));
        Assert.Null(repo.Get(b.Id));
    }

    [Fact]
    public void GetBlocksForRange_OneOffsInclusiveBounds()
    {
        var repo = Create();
        var before = OneOff(Mon.AddDays(-1), "before");
        var first = OneOff(Mon, "first");
        var last = OneOff(Mon.AddDays(6), "last");
        var after = OneOff(Mon.AddDays(7), "after");
        foreach (var b in new[] { before, first, last, after })
            repo.Add(b);

        var names = repo.GetBlocksForRange(Mon, Mon.AddDays(6)).Select(b => b.TaskName).Order().ToList();

        Assert.Equal(["first", "last"], names);
    }

    [Fact]
    public void GetBlocksForRange_RecurringIntersection()
    {
        var repo = Create();
        var endsBefore = Series(Mon.AddDays(-30), Mon.AddDays(-1), "endsBefore");
        var endsOnFrom = Series(Mon.AddDays(-30), Mon, "endsOnFrom");
        var openStarted = Series(Mon.AddDays(-30), null, "openStarted");
        var startsOnTo = Series(Mon.AddDays(6), null, "startsOnTo");
        var startsAfter = Series(Mon.AddDays(7), null, "startsAfter");
        foreach (var b in new[] { endsBefore, endsOnFrom, openStarted, startsOnTo, startsAfter })
            repo.Add(b);

        var names = repo.GetBlocksForRange(Mon, Mon.AddDays(6)).Select(b => b.TaskName).Order().ToList();

        Assert.Equal(["endsOnFrom", "openStarted", "startsOnTo"], names);
    }

    [Fact]
    public void Exceptions_AddIsIdempotent_AndRangeFiltered()
    {
        var repo = Create();
        var s = Series(Mon, null);
        repo.Add(s);

        repo.AddException(s.Id, Mon);
        repo.AddException(s.Id, Mon);
        repo.AddException(s.Id, Mon.AddDays(10));

        Assert.Equal([(s.Id, Mon)], repo.GetExceptions(Mon, Mon.AddDays(6)));
        Assert.Equal(2, repo.GetExceptions(Mon, Mon.AddDays(10)).Count);
        Assert.Empty(repo.GetExceptions(Mon.AddDays(1), Mon.AddDays(9)));
    }

    [Fact]
    public void AddException_UnknownBlock_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Create().AddException(Guid.NewGuid(), Mon));
    }

    [Fact]
    public void Delete_RemovesExceptions()
    {
        var repo = Create();
        var s = Series(Mon, null);
        var other = Series(Mon, null, "other");
        repo.Add(s);
        repo.Add(other);
        repo.AddException(s.Id, Mon);
        repo.AddException(other.Id, Mon);

        repo.Delete(s.Id);

        Assert.Equal([(other.Id, Mon)], repo.GetExceptions(Mon, Mon));
    }
}

public class InMemoryPlanRepositoryTests : PlanRepositoryTestsBase
{
    protected override IPlanRepository Create() => new InMemoryPlanRepository();
}

public class SqlitePlanRepositoryTests : PlanRepositoryTestsBase
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "thetimer-tests-" + Guid.NewGuid().ToString("N"));

    private string DbPath => Path.Combine(_directory, "plan.db");

    protected override IPlanRepository Create() => new SqlitePlanRepository(new SqliteDatabase(DbPath));

    public override void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Data_SurvivesNewInstance()
    {
        var repo = Create();
        var s = Series(Mon, Mon.AddDays(20));
        repo.Add(s);
        repo.AddException(s.Id, Mon);
        SqliteConnection.ClearAllPools();

        var again = Create();

        Assert.Equal(s, again.Get(s.Id));
        Assert.Single(again.GetExceptions(Mon, Mon));
    }

    [Fact]
    public void ForeignKeysAreEnforced_AndCascadeDeletes()
    {
        var db = new SqliteDatabase(DbPath);
        var repo = new SqlitePlanRepository(db);
        var s = Series(Mon, null);
        repo.Add(s);
        repo.AddException(s.Id, Mon);

        using (var connection = db.Open())
        {
            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM plan_blocks"; // raw delete: relies on ON DELETE CASCADE
            delete.ExecuteNonQuery();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM plan_block_exceptions";
            Assert.Equal(0L, count.ExecuteScalar());
        }
    }

    [Fact]
    public void Migration_V2ToV3_KeepsSessionsAndActiveSession()
    {
        Directory.CreateDirectory(_directory);
        var id = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        using (var connection = new SqliteConnection($"Data Source={DbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE sessions (id TEXT PRIMARY KEY, task_name TEXT NOT NULL, tag TEXT NULL, " +
                "started_at_utc_ticks INTEGER NOT NULL, ended_at_utc_ticks INTEGER NOT NULL, duration_ticks INTEGER NOT NULL); " +
                "CREATE INDEX ix_sessions_started_at ON sessions (started_at_utc_ticks); " +
                "CREATE TABLE active_session (id INTEGER PRIMARY KEY CHECK (id = 1), task_name TEXT NOT NULL, tag TEXT NULL, " +
                "started_at_utc_ticks INTEGER NOT NULL, accumulated_ticks INTEGER NOT NULL, segment_start_utc_ticks INTEGER NULL, " +
                "target_ticks INTEGER NULL, last_seen_utc_ticks INTEGER NOT NULL, mode TEXT NOT NULL, is_break INTEGER NOT NULL, " +
                "pomodoro_phase INTEGER NOT NULL, pomodoro_completed INTEGER NOT NULL); " +
                $"INSERT INTO sessions VALUES ('{id}', 'Old', NULL, {t0.UtcTicks}, {t0.AddMinutes(10).UtcTicks}, {TimeSpan.FromMinutes(9).Ticks}); " +
                $"INSERT INTO active_session VALUES (1, 'Live', 'g', {t0.UtcTicks}, 600000000, NULL, NULL, {t0.AddMinutes(1).UtcTicks}, 'Stopwatch', 0, 0, 2); " +
                "PRAGMA user_version = 2;";
            command.ExecuteNonQuery();
        }

        var db = new SqliteDatabase(DbPath);

        var sessions = new TheTimer.Core.Sessions.SqliteSessionRepository(db).GetByDay(Mon, TimeZoneInfo.Utc);
        Assert.Equal(id, Assert.Single(sessions).Id);
        var active = new TheTimer.Core.Sessions.SqliteActiveSessionStore(db).Load();
        Assert.Equal("Live", active!.Timer.TaskName);
        Assert.Equal(2, active.PomodoroCompletedWorkCount);
        var plan = new SqlitePlanRepository(db);
        var block = OneOff(Mon);
        plan.Add(block);
        Assert.Equal(block, plan.Get(block.Id));
        SqliteConnection.ClearAllPools();
        using var check = new SqliteConnection($"Data Source={DbPath}");
        check.Open();
        using var version = check.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        Assert.Equal(3L, version.ExecuteScalar());
    }
}
