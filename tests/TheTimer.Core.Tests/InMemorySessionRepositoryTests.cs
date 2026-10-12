using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class InMemorySessionRepositoryTests
{
    private static readonly TimeZoneInfo Plus3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");

    private static TimeSession Make(string name, DateTimeOffset start) =>
        new(Guid.NewGuid(), name, null, start, start.AddMinutes(10), TimeSpan.FromMinutes(10));

    [Fact]
    public void GetByDay_UsesGivenTimeZone()
    {
        var repo = new InMemorySessionRepository();
        // 22:30 UTC on Oct 11 is 01:30 on Oct 12 in UTC+3.
        var lateUtc = Make("late", new DateTimeOffset(2026, 10, 11, 22, 30, 0, TimeSpan.Zero));
        // 20:59 UTC on Oct 11 is 23:59 on Oct 11 in UTC+3.
        var beforeMidnight = Make("before", new DateTimeOffset(2026, 10, 11, 20, 59, 0, TimeSpan.Zero));
        repo.Add(lateUtc);
        repo.Add(beforeMidnight);

        var oct12Local = repo.GetByDay(new DateOnly(2026, 10, 12), Plus3);
        var oct11Local = repo.GetByDay(new DateOnly(2026, 10, 11), Plus3);
        var oct11Utc = repo.GetByDay(new DateOnly(2026, 10, 11), TimeZoneInfo.Utc);
        var oct12Utc = repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc);

        Assert.Equal([lateUtc], oct12Local);
        Assert.Equal([beforeMidnight], oct11Local);
        Assert.Equal(2, oct11Utc.Count);
        Assert.Empty(oct12Utc);
    }

    [Fact]
    public void GetByDay_MidnightBoundary_UsesLocalDay()
    {
        var repo = new InMemorySessionRepository();
        // 21:00:00 UTC is exactly local midnight (+03:00) at the start of Mar 2.
        var atMidnight = Make("midnight", new DateTimeOffset(2026, 3, 1, 21, 0, 0, TimeSpan.Zero));
        // 20:59:59 UTC is 23:59:59 on Mar 1 in +03:00.
        var justBefore = Make("before", new DateTimeOffset(2026, 3, 1, 20, 59, 59, TimeSpan.Zero));
        repo.Add(atMidnight);
        repo.Add(justBefore);

        var mar2Local = repo.GetByDay(new DateOnly(2026, 3, 2), Plus3);
        var mar1Local = repo.GetByDay(new DateOnly(2026, 3, 1), Plus3);

        Assert.Equal([atMidnight], mar2Local);
        Assert.Equal([justBefore], mar1Local);
    }

    [Fact]
    public void GetByDay_OrdersByStartDescending()
    {
        var repo = new InMemorySessionRepository();
        var a = Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
        var b = Make("b", new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        var c = Make("c", new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        repo.Add(a);
        repo.Add(b);
        repo.Add(c);

        var result = repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc);

        Assert.Equal([b, c, a], result);
    }

    [Fact]
    public void GetByDay_EmptyRepository_ReturnsEmpty()
    {
        Assert.Empty(new InMemorySessionRepository().GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Delete_ExistingId_RemovesAndReturnsTrue()
    {
        var repo = new InMemorySessionRepository();
        var s = Make("a", new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero));
        repo.Add(s);

        Assert.True(repo.Delete(s.Id));
        Assert.Empty(repo.GetByDay(new DateOnly(2026, 10, 12), TimeZoneInfo.Utc));
    }

    [Fact]
    public void Delete_UnknownId_ReturnsFalse()
    {
        Assert.False(new InMemorySessionRepository().Delete(Guid.NewGuid()));
    }
}
