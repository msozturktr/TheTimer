using Microsoft.Data.Sqlite;
using TheTimer.Core.Sessions;
using TheTimer.Core.Storage;

namespace TheTimer.Core.Tests;

public abstract class SessionRangeTestsBase : IDisposable
{
    protected static readonly TimeZoneInfo Plus3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");

    protected abstract ISessionRepository Create();

    public virtual void Dispose() { }

    private static TimeSession Make(string name, DateTimeOffset start) =>
        new(Guid.NewGuid(), name, null, start, start.AddMinutes(10), TimeSpan.FromMinutes(10));

    [Fact]
    public void GetByRange_InclusiveLocalDays_OldestFirst()
    {
        var repo = Create();
        // Local (UTC+3) days: Oct 11 23:59, Oct 12 00:00, Oct 14 23:30, Oct 15 00:00.
        var beforeRange = Make("before", new DateTimeOffset(2026, 10, 11, 20, 59, 0, TimeSpan.Zero));
        var firstDay = Make("first", new DateTimeOffset(2026, 10, 11, 21, 0, 0, TimeSpan.Zero));
        var lastDay = Make("last", new DateTimeOffset(2026, 10, 14, 20, 30, 0, TimeSpan.Zero));
        var afterRange = Make("after", new DateTimeOffset(2026, 10, 14, 21, 0, 0, TimeSpan.Zero));
        var middle = Make("middle", new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
        foreach (var s in new[] { lastDay, afterRange, middle, beforeRange, firstDay })
            repo.Add(s);

        var result = repo.GetByRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 14), Plus3);

        Assert.Equal([firstDay, middle, lastDay], result);
    }

    [Fact]
    public void GetByRange_SingleDayMatchesGetByDay()
    {
        var repo = Create();
        repo.Add(Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)));
        repo.Add(Make("b", new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero)));
        var day = new DateOnly(2026, 10, 12);

        Assert.Equal(repo.GetByDay(day, TimeZoneInfo.Utc), repo.GetByRange(day, day, TimeZoneInfo.Utc));
    }

    [Fact]
    public void GetByRange_InvertedOrEmpty_ReturnsEmpty()
    {
        var repo = Create();
        repo.Add(Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero)));

        Assert.Empty(repo.GetByRange(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
        Assert.Empty(repo.GetByRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), TimeZoneInfo.Utc));
    }
}

public class InMemorySessionRangeTests : SessionRangeTestsBase
{
    protected override ISessionRepository Create() => new InMemorySessionRepository();
}

public class SqliteSessionRangeTests : SessionRangeTestsBase
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "thetimer-tests-" + Guid.NewGuid().ToString("N"));

    protected override ISessionRepository Create() =>
        new SqliteSessionRepository(new SqliteDatabase(Path.Combine(_directory, "r.db")));

    public override void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
