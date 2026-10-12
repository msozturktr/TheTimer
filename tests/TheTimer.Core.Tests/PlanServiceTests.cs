using TheTimer.Core.Planning;

namespace TheTimer.Core.Tests;

public class PlanServiceTests
{
    private static readonly TimeZoneInfo Plus3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");

    private static readonly DateOnly Mon = new(2026, 10, 12);
    private static readonly TimeOnly H9 = new(9, 0);
    private static readonly TimeOnly H10 = new(10, 0);
    private static readonly TimeOnly H11 = new(11, 0);

    private readonly InMemoryPlanRepository _repo = new();
    private readonly PlanService _service;

    public PlanServiceTests() => _service = new PlanService(_repo);

    private IReadOnlyList<PlanOccurrence> Week() => _service.GetOccurrences(Mon, Mon.AddDays(6), Plus3);

    private PlanOccurrence OccOn(PlanBlock b, DateOnly day) =>
        _service.GetOccurrences(day, Plus3).Single(o => o.Block.Id == b.Id);

    [Fact]
    public void OneOff_AppearsOnlyOnItsDay_WithResolvedInstants()
    {
        _service.AddOneOff(Mon, H9, H10, "Write", "work");

        Assert.Empty(_service.GetOccurrences(Mon.AddDays(1), Plus3));
        var occ = Assert.Single(_service.GetOccurrences(Mon, Plus3));
        Assert.Equal(Mon, occ.Day);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 6, 0, 0, TimeSpan.Zero), occ.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 7, 0, 0, TimeSpan.Zero), occ.EndsAt);
        Assert.False(occ.IsRecurring);
        Assert.False(occ.HasOverlap);
    }

    [Fact]
    public void Recurring_ExpandsOnMatchingDays()
    {
        _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.Monday | WeekDays.Wednesday), H9, H10, "Gym", null);

        var days = Week().Select(o => o.Day).ToList();

        Assert.Equal([Mon, Mon.AddDays(2)], days);
        Assert.All(Week(), o => Assert.True(o.IsRecurring));
    }

    [Fact]
    public void Recurring_RespectsEndDate()
    {
        _service.AddRecurring(new PlanRecurrence(Mon, Mon.AddDays(2), WeekDays.EveryDay), H9, H10, "x", null);

        Assert.Equal(3, Week().Count);
    }

    [Fact]
    public void Exception_RemovesThatDayOnly()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "x", null);
        _repo.AddException(b.Id, Mon.AddDays(1));

        var days = Week().Select(o => o.Day).ToList();

        Assert.Equal(6, days.Count);
        Assert.DoesNotContain(Mon.AddDays(1), days);
    }

    [Fact]
    public void Ordering_ByStartThenName()
    {
        _service.AddOneOff(Mon, H10, H11, "B", null);
        _service.AddOneOff(Mon, H9, H10, "Z", null);
        _service.AddOneOff(Mon, H9, H10, "A", null);
        _service.AddOneOff(Mon.AddDays(1), H9, H10, "Next", null);

        var names = Week().Select(o => o.Block.TaskName).ToList();

        Assert.Equal(["A", "Z", "B", "Next"], names);
    }

    [Fact]
    public void Overlap_FlagsBothAndIgnoresTouching()
    {
        _service.AddOneOff(Mon, H9, new TimeOnly(10, 30), "A", null);
        _service.AddOneOff(Mon, H10, H11, "B", null);       // overlaps A
        _service.AddOneOff(Mon, H11, new TimeOnly(12, 0), "C", null); // touches B only
        _service.AddOneOff(Mon.AddDays(1), H9, H10, "D", null);
        _service.AddOneOff(Mon.AddDays(1), H10, H11, "E", null); // touching only

        var byName = Week().ToDictionary(o => o.Block.TaskName, o => o.HasOverlap);

        Assert.True(byName["A"]);
        Assert.True(byName["B"]);
        Assert.False(byName["C"]);
        Assert.False(byName["D"]);
        Assert.False(byName["E"]);
    }

    [Fact]
    public void Overlap_BetweenOneOffAndRecurring()
    {
        _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H11, "S", null);
        _service.AddOneOff(Mon.AddDays(2), H10, H11, "O", null);

        var week = Week();

        Assert.All(week.Where(o => o.Day == Mon.AddDays(2)), o => Assert.True(o.HasOverlap));
        Assert.All(week.Where(o => o.Day != Mon.AddDays(2)), o => Assert.False(o.HasOverlap));
    }

    [Fact]
    public void InvertedRange_ReturnsEmpty()
    {
        Assert.Empty(_service.GetOccurrences(Mon, Mon.AddDays(-1), Plus3));
    }

    // --- Delete ---

    [Fact]
    public void Delete_OneOff_RemovesBlock_ScopeIgnored()
    {
        var b = _service.AddOneOff(Mon, H9, H10, "x", null);

        _service.DeleteOccurrence(OccOn(b, Mon), PlanEditScope.ThisDayOnly);

        Assert.Null(_repo.Get(b.Id));
    }

    [Fact]
    public void Delete_Recurring_ThisDayOnly_AddsException()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "x", null);

        _service.DeleteOccurrence(OccOn(b, Mon.AddDays(2)), PlanEditScope.ThisDayOnly);

        Assert.NotNull(_repo.Get(b.Id));
        Assert.Equal(6, Week().Count);
        Assert.DoesNotContain(Week(), o => o.Day == Mon.AddDays(2));
    }

    [Fact]
    public void Delete_Recurring_Following_EndsSeriesDayBefore()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "x", null);

        _service.DeleteOccurrence(OccOn(b, Mon.AddDays(3)), PlanEditScope.ThisAndFollowing);

        Assert.Equal(Mon.AddDays(2), _repo.Get(b.Id)!.Recurrence!.EndDate);
        Assert.Equal(3, Week().Count);
    }

    [Fact]
    public void Delete_Recurring_Following_OnStartDate_DeletesBlock()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "x", null);
        _repo.AddException(b.Id, Mon.AddDays(1));

        _service.DeleteOccurrence(OccOn(b, Mon), PlanEditScope.ThisAndFollowing);

        Assert.Null(_repo.Get(b.Id));
        Assert.Empty(Week());
        Assert.Empty(_repo.GetExceptions(Mon, Mon.AddDays(10)));
    }

    // --- Update ---

    [Fact]
    public void Update_OneOff_InPlace()
    {
        var b = _service.AddOneOff(Mon, H9, H10, "old", "t");

        _service.UpdateOccurrence(OccOn(b, Mon), H10, H11, "new", null, PlanEditScope.ThisAndFollowing);

        var got = _repo.Get(b.Id)!;
        Assert.Equal(("new", null, H10, H11, (DateOnly?)Mon), (got.TaskName, got.Tag, got.Start, got.End, got.Date));
    }

    [Fact]
    public void Update_Recurring_ThisDayOnly_ExceptionPlusOneOff()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "old", null);

        _service.UpdateOccurrence(OccOn(b, Mon.AddDays(1)), H10, H11, "new", "t", PlanEditScope.ThisDayOnly);

        var day = _service.GetOccurrences(Mon.AddDays(1), Plus3);
        var only = Assert.Single(day);
        Assert.Equal("new", only.Block.TaskName);
        Assert.False(only.IsRecurring);
        Assert.Equal(H10, only.Block.Start);
        Assert.Equal("old", Assert.Single(_service.GetOccurrences(Mon.AddDays(2), Plus3)).Block.TaskName);
        Assert.Equal(Mon, _repo.Get(b.Id)!.Recurrence!.StartDate);
    }

    [Fact]
    public void Update_Recurring_Following_OnStartDate_UpdatesInPlace()
    {
        var rec = new PlanRecurrence(Mon, Mon.AddDays(20), WeekDays.Weekdays);
        var b = _service.AddRecurring(rec, H9, H10, "old", null);

        _service.UpdateOccurrence(OccOn(b, Mon), H10, H11, "new", null, PlanEditScope.ThisAndFollowing);

        var got = _repo.Get(b.Id)!;
        Assert.Equal("new", got.TaskName);
        Assert.Equal(rec, got.Recurrence);
        Assert.Single(_repo.GetBlocksForRange(Mon, Mon.AddDays(30)));
    }

    [Fact]
    public void Update_Recurring_Following_SplitsSeries()
    {
        var end = Mon.AddDays(30);
        var b = _service.AddRecurring(new PlanRecurrence(Mon, end, WeekDays.Weekdays), H9, H10, "old", null);
        _repo.AddException(b.Id, Mon.AddDays(8)); // a skipped day after the split point

        _service.UpdateOccurrence(OccOn(b, Mon.AddDays(3)), H10, H11, "new", "t", PlanEditScope.ThisAndFollowing);

        var old = _repo.Get(b.Id)!;
        Assert.Equal(Mon.AddDays(2), old.Recurrence!.EndDate);
        Assert.Equal("old", old.TaskName);
        var blocks = _repo.GetBlocksForRange(Mon, end);
        var added = Assert.Single(blocks, x => x.Id != b.Id);
        Assert.Equal(new PlanRecurrence(Mon.AddDays(3), end, WeekDays.Weekdays), added.Recurrence);
        Assert.Equal(("new", "t", H10, H11), (added.TaskName, added.Tag, added.Start, added.End));

        var all = _service.GetOccurrences(Mon, end, Plus3);
        Assert.All(all.Where(o => o.Day < Mon.AddDays(3)), o => Assert.Equal("old", o.Block.TaskName));
        Assert.All(all.Where(o => o.Day >= Mon.AddDays(3)), o => Assert.Equal("new", o.Block.TaskName));
        Assert.DoesNotContain(all, o => o.Day == Mon.AddDays(8)); // exception carried over
    }

    [Fact]
    public void Update_InvalidValues_ThrowsAndWritesNothing()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "old", null);

        Assert.Throws<ArgumentException>(() =>
            _service.UpdateOccurrence(OccOn(b, Mon.AddDays(2)), H11, H10, "new", null, PlanEditScope.ThisAndFollowing));

        Assert.Null(_repo.Get(b.Id)!.Recurrence!.EndDate);
        Assert.Single(_repo.GetBlocksForRange(Mon, Mon.AddDays(10)));
    }

    // --- UpdateRecurrence ---

    [Fact]
    public void UpdateRecurrence_ChangesWholeSeries()
    {
        var b = _service.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.EveryDay), H9, H10, "x", null);

        _service.UpdateRecurrence(b.Id, new PlanRecurrence(Mon, Mon.AddDays(6), WeekDays.Weekend));

        Assert.Equal([Mon.AddDays(5), Mon.AddDays(6)], Week().Select(o => o.Day).ToList());
    }

    [Fact]
    public void UpdateRecurrence_UnknownOrOneOff_Throws()
    {
        var one = _service.AddOneOff(Mon, H9, H10, "x", null);
        var rec = new PlanRecurrence(Mon, null, WeekDays.Monday);

        Assert.Throws<InvalidOperationException>(() => _service.UpdateRecurrence(Guid.NewGuid(), rec));
        Assert.Throws<InvalidOperationException>(() => _service.UpdateRecurrence(one.Id, rec));
    }
}
