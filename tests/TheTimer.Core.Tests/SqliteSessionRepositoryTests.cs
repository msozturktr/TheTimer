using Microsoft.Data.Sqlite;
using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SqliteSessionRepositoryTests : IDisposable
{
    private static readonly TimeZoneInfo Plus3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "thetimer-tests-" + Guid.NewGuid().ToString("N"));

    private string DbPath => Path.Combine(_directory, "nested", "test.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static TimeSession Make(string name, DateTimeOffset start, string? tag = null) =>
        new(Guid.NewGuid(), name, tag, start, start.AddMinutes(10), TimeSpan.FromMinutes(9) + TimeSpan.FromTicks(1234567));

    private static void SetUserVersion(string path, int version)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version}";
        command.ExecuteNonQuery();
    }

    private static long GetUserVersion(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Constructor_CreatesDirectoryAndDatabase()
    {
        _ = new SqliteSessionRepository(DbPath);

        Assert.True(File.Exists(DbPath));
    }

    [Fact]
    public void AddThenGetByDay_RoundTripsAllFieldsExactly()
    {
        var repo = new SqliteSessionRepository(DbPath);
        // Non-zero offset and sub-second ticks: only the instant must survive.
        var start = new DateTimeOffset(2026, 10, 12, 9, 15, 30, TimeSpan.FromHours(3)).AddTicks(4321);
        var withTag = Make("coding", start, "work");
        var noTag = Make("reading", start.AddHours(1));
        repo.Add(withTag);
        repo.Add(noTag);

        var result = repo.GetByDay(new DateOnly(2026, 10, 12), Plus3);

        Assert.Equal(2, result.Count);
        var gotNoTag = result[0];
        var gotTag = result[1];
        Assert.Equal(noTag.Id, gotNoTag.Id);
        Assert.Null(gotNoTag.Tag);
        Assert.Equal(withTag.Id, gotTag.Id);
        Assert.Equal("coding", gotTag.TaskName);
        Assert.Equal("work", gotTag.Tag);
        Assert.Equal(withTag.StartedAt.UtcTicks, gotTag.StartedAt.UtcTicks);
        Assert.Equal(withTag.EndedAt.UtcTicks, gotTag.EndedAt.UtcTicks);
        Assert.Equal(withTag.Duration, gotTag.Duration);
        Assert.Equal(TimeSpan.Zero, gotTag.StartedAt.Offset);
        // Record equality holds because DateTimeOffset equality compares instants.
        Assert.Equal(withTag, gotTag);
    }

    [Fact]
    public void Data_SurvivesNewRepositoryInstance()
    {
        var s = Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), "t");
        new SqliteSessionRepository(DbPath).Add(s);
        SqliteConnection.ClearAllPools();

        var result = new SqliteSessionRepository(DbPath).GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc);

        Assert.Equal([s], result);
    }

    [Fact]
    public void GetByDay_UsesGivenTimeZone()
    {
        var repo = new SqliteSessionRepository(DbPath);
        var lateUtc = Make("late", new DateTimeOffset(2026, 10, 11, 22, 30, 0, TimeSpan.Zero));
        var beforeMidnight = Make("before", new DateTimeOffset(2026, 10, 11, 20, 59, 0, TimeSpan.Zero));
        repo.Add(lateUtc);
        repo.Add(beforeMidnight);

        Assert.Equal([lateUtc], repo.GetByDay(new DateOnly(2026, 10, 12), Plus3));
        Assert.Equal([beforeMidnight], repo.GetByDay(new DateOnly(2026, 10, 11), Plus3));
        Assert.Equal(2, repo.GetByDay(new DateOnly(2026, 10, 11), TimeZoneInfo.Utc).Count);
        Assert.Empty(repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void GetByDay_MidnightBoundary_UsesLocalDay()
    {
        var repo = new SqliteSessionRepository(DbPath);
        var atMidnight = Make("midnight", new DateTimeOffset(2026, 3, 1, 21, 0, 0, TimeSpan.Zero));
        var justBefore = Make("before", new DateTimeOffset(2026, 3, 1, 20, 59, 59, TimeSpan.Zero));
        repo.Add(atMidnight);
        repo.Add(justBefore);

        Assert.Equal([atMidnight], repo.GetByDay(new DateOnly(2026, 3, 2), Plus3));
        Assert.Equal([justBefore], repo.GetByDay(new DateOnly(2026, 3, 1), Plus3));
    }

    [Fact]
    public void GetByDay_OrdersByStartDescending()
    {
        var repo = new SqliteSessionRepository(DbPath);
        var a = Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
        var b = Make("b", new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        var c = Make("c", new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        repo.Add(a);
        repo.Add(b);
        repo.Add(c);

        Assert.Equal([b, c, a], repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void GetByDay_Empty_ReturnsEmpty()
    {
        Assert.Empty(new SqliteSessionRepository(DbPath).GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Delete_ExistingId_RemovesAndReturnsTrue()
    {
        var repo = new SqliteSessionRepository(DbPath);
        var s = Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
        repo.Add(s);

        Assert.True(repo.Delete(s.Id));
        Assert.Empty(repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Delete_UnknownId_ReturnsFalse()
    {
        Assert.False(new SqliteSessionRepository(DbPath).Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Initialize_SetsUserVersionToCurrent()
    {
        _ = new SqliteSessionRepository(DbPath);
        SqliteConnection.ClearAllPools();

        Assert.Equal(2, GetUserVersion(DbPath));
    }

    [Fact]
    public void Initialize_NewerUserVersion_Throws()
    {
        _ = new SqliteSessionRepository(DbPath);
        SqliteConnection.ClearAllPools();
        SetUserVersion(DbPath, 99);
        SqliteConnection.ClearAllPools();

        Assert.Throws<InvalidOperationException>(() => new SqliteSessionRepository(DbPath));
    }
}
