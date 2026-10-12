using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class SessionSummaryTests
{
    private static TimeSession Make(string name, int minutes) =>
        new(Guid.NewGuid(), name, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(minutes), TimeSpan.FromMinutes(minutes));

    [Fact]
    public void ByTask_GroupsCaseInsensitively_KeepsFirstName()
    {
        var result = SessionSummary.ByTask([Make("Kod", 10), Make("kod", 5), Make("KOD", 1)]);

        var item = Assert.Single(result);
        Assert.Equal("Kod", item.TaskName);
        Assert.Equal(TimeSpan.FromMinutes(16), item.Total);
        Assert.Equal(3, item.SessionCount);
    }

    [Fact]
    public void ByTask_OrdersByTotalDescThenName()
    {
        var result = SessionSummary.ByTask([Make("b", 5), Make("c", 20), Make("a", 5)]);

        Assert.Equal(["c", "a", "b"], result.Select(t => t.TaskName));
    }

    [Fact]
    public void Total_SumsDurations()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), SessionSummary.Total([Make("a", 10), Make("b", 5)]));
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyAndZero()
    {
        Assert.Empty(SessionSummary.ByTask([]));
        Assert.Equal(TimeSpan.Zero, SessionSummary.Total([]));
    }
}
