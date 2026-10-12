using TheTimer.Core.Planning;

namespace TheTimer.Core.Tests;

public class PlanModelTests
{
    private static readonly DateOnly Mon = new(2026, 10, 12);

    [Fact]
    public void WeekDays_Composites()
    {
        Assert.Equal(WeekDays.Weekdays | WeekDays.Weekend, WeekDays.EveryDay);
        Assert.True(WeekDays.Weekdays.Includes(DayOfWeek.Friday));
        Assert.False(WeekDays.Weekdays.Includes(DayOfWeek.Saturday));
        Assert.True(WeekDays.Weekend.Includes(DayOfWeek.Sunday));
        Assert.False(WeekDays.None.Includes(DayOfWeek.Monday));
        foreach (var d in Enum.GetValues<DayOfWeek>())
            Assert.True(WeekDays.EveryDay.Includes(d));
    }

    [Fact]
    public void FromDayOfWeek_MapsEachDay()
    {
        Assert.Equal(WeekDays.Monday, WeekDaysExtensions.FromDayOfWeek(DayOfWeek.Monday));
        Assert.Equal(WeekDays.Sunday, WeekDaysExtensions.FromDayOfWeek(DayOfWeek.Sunday));
    }

    [Fact]
    public void Recurrence_OccursOn_RespectsBoundsAndMask()
    {
        var rec = new PlanRecurrence(Mon, Mon.AddDays(13), WeekDays.Monday | WeekDays.Wednesday);

        Assert.True(rec.OccursOn(Mon));
        Assert.False(rec.OccursOn(Mon.AddDays(1)));
        Assert.True(rec.OccursOn(Mon.AddDays(2)));
        Assert.True(rec.OccursOn(Mon.AddDays(7)));
        Assert.True(rec.OccursOn(Mon.AddDays(9)));
        Assert.False(rec.OccursOn(Mon.AddDays(14)));   // after end date
        Assert.False(rec.OccursOn(Mon.AddDays(-7)));   // before start date
    }

    [Fact]
    public void Recurrence_EndDateInclusive_AndOpenEnded()
    {
        var bounded = new PlanRecurrence(Mon, Mon, WeekDays.EveryDay);
        Assert.True(bounded.OccursOn(Mon));
        Assert.False(bounded.OccursOn(Mon.AddDays(1)));
        Assert.True(new PlanRecurrence(Mon, null, WeekDays.EveryDay).OccursOn(Mon.AddYears(5)));
    }

    [Fact]
    public void Recurrence_Validation()
    {
        Assert.Throws<ArgumentException>(() => new PlanRecurrence(Mon, null, WeekDays.None));
        Assert.Throws<ArgumentException>(() => new PlanRecurrence(Mon, null, (WeekDays)128));
        Assert.Throws<ArgumentException>(() => new PlanRecurrence(Mon, Mon.AddDays(-1), WeekDays.Monday));
    }

    [Fact]
    public void Block_NormalizesNameAndTag()
    {
        var b = new PlanBlock(Guid.NewGuid(), "  Deep work ", "   ", new TimeOnly(9, 0), new TimeOnly(10, 30), Mon, null);
        Assert.Equal("Deep work", b.TaskName);
        Assert.Null(b.Tag);
        Assert.Equal("x", new PlanBlock(Guid.NewGuid(), "a", " x ", new TimeOnly(9, 0), new TimeOnly(10, 0), Mon, null).Tag);
    }

    [Fact]
    public void Block_Validation()
    {
        var s = new TimeOnly(9, 0);
        var e = new TimeOnly(10, 0);
        var rec = new PlanRecurrence(Mon, null, WeekDays.Monday);
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "  ", null, s, e, Mon, null));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, e, s, Mon, null));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, s, s, Mon, null));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, s, e, null, null));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, s, e, Mon, rec));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, new TimeOnly(9, 0, 30), e, Mon, null));
        Assert.Throws<ArgumentException>(() => new PlanBlock(Guid.NewGuid(), "a", null, s, new TimeOnly(10, 0, 1), Mon, null));
        Assert.True(new PlanBlock(Guid.NewGuid(), "a", null, s, e, null, rec).IsRecurring);
    }

    [Fact]
    public void ZoneTime_FixedOffset()
    {
        var plus3 = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        var instant = ZoneTime.ToInstant(Mon, new TimeOnly(9, 0), plus3);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 6, 0, 0, TimeSpan.Zero), instant);
    }

    private static TimeZoneInfo? Berlin()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
        catch (TimeZoneNotFoundException) { return null; }
    }

    [Fact]
    public void ZoneTime_DstGap_ShiftsForward()
    {
        if (Berlin() is not { } berlin)
            return; // tz database unavailable
        // 2026-03-29 02:30 does not exist in Berlin (02:00 -> 03:00): resolves to 03:30 CEST = 01:30 UTC.
        var instant = ZoneTime.ToInstant(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), berlin);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), instant);
    }

    [Fact]
    public void ZoneTime_Ambiguous_UsesStandardOffset()
    {
        if (Berlin() is not { } berlin)
            return;
        // 2026-10-25 02:30 occurs twice; standard offset is +01:00 -> 01:30 UTC.
        var instant = ZoneTime.ToInstant(new DateOnly(2026, 10, 25), new TimeOnly(2, 30), berlin);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), instant);
    }

    [Fact]
    public void Occurrences_OnDstDay_ResolveInZone()
    {
        if (Berlin() is not { } berlin)
            return;
        var service = new PlanService(new InMemoryPlanRepository());
        service.AddOneOff(new DateOnly(2026, 3, 29), new TimeOnly(4, 0), new TimeOnly(5, 0), "x", null);

        var occ = Assert.Single(service.GetOccurrences(new DateOnly(2026, 3, 29), berlin));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 2, 0, 0, TimeSpan.Zero), occ.StartsAt); // 04:00 CEST
        Assert.Equal(TimeSpan.FromHours(1), occ.EndsAt - occ.StartsAt);
    }
}
