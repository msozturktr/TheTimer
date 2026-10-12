using TheTimer.Core.Planning;
using TheTimer.Core.Reports;
using TheTimer.Core.Sessions;

namespace TheTimer.Core.Tests;

public class PlanReportTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero); // 09:00 at +03:00
    private static readonly TimeZoneInfo Plus3 =
        TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
    private static readonly DateOnly Mon = new(2026, 10, 12);

    private static PlanOccurrence Occ(string task, DateTimeOffset start, int minutes, DateOnly? day = null)
    {
        var block = new PlanBlock(Guid.NewGuid(), task, null, new TimeOnly(9, 0), new TimeOnly(10, 0), Mon, null);
        return new PlanOccurrence(block, day ?? DateOnly.FromDateTime(start.UtcDateTime), start, start.AddMinutes(minutes), false);
    }

    private static TimeSession Sess(string task, DateTimeOffset start, int spanMinutes, int activeMinutes) =>
        new(Guid.NewGuid(), task, null, start, start.AddMinutes(spanMinutes), TimeSpan.FromMinutes(activeMinutes));

    // --- PlanReminder ---

    [Fact]
    public void Reminder_BoundariesAreExclusiveInclusive()
    {
        var a = Occ("a", T0, 30);
        var b = Occ("b", T0.AddMinutes(10), 30);
        var c = Occ("c", T0.AddMinutes(20), 30);
        var all = new[] { c, a, b };

        Assert.Equal([b, c], PlanReminder.DueBetween(all, T0, T0.AddMinutes(20)));      // after exclusive, until inclusive
        Assert.Equal([b], PlanReminder.DueBetween(all, T0, T0.AddMinutes(19)));
        Assert.Equal([a, b], PlanReminder.DueBetween(all, T0.AddTicks(-1), T0.AddMinutes(10)));
        Assert.Empty(PlanReminder.DueBetween(all, T0.AddMinutes(20), T0.AddMinutes(30)));
        Assert.Empty(PlanReminder.DueBetween(all, T0.AddMinutes(5), T0.AddMinutes(5)));
    }

    // --- ActualForOccurrence ---

    [Fact]
    public void Actual_FullyInside_UsesDuration()
    {
        var occ = Occ("Work", T0, 60);
        var s = Sess("Work", T0.AddMinutes(10), 20, 20);

        Assert.Equal(TimeSpan.FromMinutes(20), PlanComparison.ActualForOccurrence(occ, [s]));
    }

    [Fact]
    public void Actual_PartialOverlap_ClippedAndScaledByPauses()
    {
        var occ = Occ("Work", T0, 60);
        // Span 40 min starting 20 min before the block; 20 min inside; only 30 of 40 min active -> 0.75 scale.
        var s = Sess("Work", T0.AddMinutes(-20), 40, 30);

        Assert.Equal(TimeSpan.FromMinutes(15), PlanComparison.ActualForOccurrence(occ, [s]));
    }

    [Fact]
    public void Actual_CaseInsensitiveTrimmed_IgnoresOtherTasksAndDisjoint()
    {
        var occ = Occ("Work", T0, 60);
        var match = Sess(" work ", T0, 10, 10);
        var other = Sess("Play", T0, 10, 10);
        var before = Sess("Work", T0.AddMinutes(-30), 30, 30); // ends exactly at start: zero overlap
        var after = Sess("Work", T0.AddMinutes(60), 10, 10);

        Assert.Equal(TimeSpan.FromMinutes(10), PlanComparison.ActualForOccurrence(occ, [match, other, before, after]));
    }

    [Fact]
    public void Actual_ZeroLengthSession_CountsZero()
    {
        var occ = Occ("Work", T0, 60);
        var s = new TimeSession(Guid.NewGuid(), "Work", null, T0.AddMinutes(5), T0.AddMinutes(5), TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.Zero, PlanComparison.ActualForOccurrence(occ, [s]));
    }

    [Fact]
    public void Actual_SumsSessions()
    {
        var occ = Occ("Work", T0, 60);

        var total = PlanComparison.ActualForOccurrence(occ, [Sess("Work", T0, 10, 10), Sess("WORK", T0.AddMinutes(30), 10, 5)]);

        Assert.Equal(TimeSpan.FromMinutes(15), total);
    }

    // --- ByTask ---

    [Fact]
    public void ByTask_UnionCaseInsensitive_PlanNameWins_Ordered()
    {
        var occs = new[] { Occ("Code", T0, 60), Occ("code", T0.AddHours(2), 30), Occ("Read", T0, 30) };
        var sessions = new[]
        {
            Sess("CODE", T0, 100, 100),
            Sess("Extra", T0, 20, 20),
            Sess("Extra2", T0, 20, 10),
            Sess("Read", T0, 30, 40),
        };

        var result = PlanComparison.ByTask(occs, sessions);

        Assert.Equal(["Code", "Read", "Extra", "Extra2"], result.Select(r => r.TaskName).ToList());
        Assert.Equal(TimeSpan.FromMinutes(90), result[0].Planned);
        Assert.Equal(TimeSpan.FromMinutes(100), result[0].Actual);
        Assert.Equal(TimeSpan.FromMinutes(10), result[0].Difference);
        Assert.Equal(TimeSpan.FromMinutes(30), result[1].Planned);
        Assert.Equal(TimeSpan.FromMinutes(40), result[1].Actual);
        Assert.Equal(TimeSpan.Zero, result[2].Planned);
        Assert.Equal(result[2].Actual, result[2].Difference);
    }

    [Fact]
    public void ByTask_SessionOnlyTask_UsesSessionSpelling_TieBreaksByName()
    {
        var result = PlanComparison.ByTask([], [Sess("b", T0, 10, 10), Sess("A", T0, 10, 10), Sess("a", T0, 5, 5)]);

        Assert.Equal(["A", "b"], result.Select(r => r.TaskName).ToList());
        Assert.Equal(TimeSpan.FromMinutes(15), result[0].Actual);
    }

    [Fact]
    public void ByTask_Empty()
    {
        Assert.Empty(PlanComparison.ByTask([], []));
    }

    // --- WeeklyReport ---

    [Theory]
    [InlineData(2026, 10, 12, 2026, 10, 12)] // Monday
    [InlineData(2026, 10, 14, 2026, 10, 12)]
    [InlineData(2026, 10, 18, 2026, 10, 12)] // Sunday -> previous Monday
    [InlineData(2026, 10, 19, 2026, 10, 19)]
    public void WeekStart_IsMonday(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), WeeklyReport.WeekStart(new DateOnly(y, m, d)));
    }

    [Fact]
    public void Build_SevenDays_TotalsAndTasks()
    {
        var monday9 = new DateTimeOffset(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);
        var wed9 = monday9.AddDays(2);
        var sessions = new[]
        {
            Sess("Code", monday9, 60, 50),
            Sess("Code", wed9, 30, 30),
            Sess("Misc", new DateTimeOffset(2026, 10, 18, 20, 0, 0, TimeSpan.Zero), 10, 10), // Sunday 23:00 local
        };
        var occs = new[] { Occ("Code", monday9, 120, Mon), Occ("Code", wed9, 60, Mon.AddDays(2)) };

        var report = WeeklyReport.Build(new DateOnly(2026, 10, 18), sessions, occs, Plus3);

        Assert.Equal(Mon, report.WeekStart);
        Assert.Equal(7, report.Days.Count);
        Assert.Equal(Enumerable.Range(0, 7).Select(i => Mon.AddDays(i)), report.Days.Select(d => d.Day));
        Assert.Equal(TimeSpan.FromMinutes(50), report.Days[0].Actual);
        Assert.Equal(TimeSpan.FromMinutes(120), report.Days[0].Planned);
        Assert.Equal(TimeSpan.FromMinutes(30), report.Days[2].Actual);
        Assert.Equal(TimeSpan.FromMinutes(10), report.Days[6].Actual);
        Assert.Equal(TimeSpan.Zero, report.Days[6].Planned);
        Assert.Equal(TimeSpan.FromMinutes(90), report.TotalActual);
        Assert.Equal(TimeSpan.FromMinutes(180), report.TotalPlanned);
        Assert.Equal(["Code", "Misc"], report.Tasks.Select(t => t.TaskName).ToList());
    }

    [Fact]
    public void Build_AttributesByLocalStartDay_AndIgnoresOutOfWeek()
    {
        // 22:00 UTC Sunday Oct 11 is 01:00 Monday Oct 12 at +03:00 -> in week; 21:30 UTC Sunday Oct 18 is Monday Oct 19 local -> out.
        var inWeek = Sess("In", new DateTimeOffset(2026, 10, 11, 22, 0, 0, TimeSpan.Zero), 10, 10);
        var outWeek = Sess("Out", new DateTimeOffset(2026, 10, 18, 21, 30, 0, TimeSpan.Zero), 10, 10);
        var before = Sess("Before", new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero), 10, 10);
        var outOcc = Occ("Out", T0.AddDays(10), 60, Mon.AddDays(10));

        var report = WeeklyReport.Build(Mon, [inWeek, outWeek, before], [outOcc], Plus3);

        Assert.Equal(TimeSpan.FromMinutes(10), report.Days[0].Actual);
        Assert.Equal(TimeSpan.FromMinutes(10), report.TotalActual);
        Assert.Equal(TimeSpan.Zero, report.TotalPlanned);
        Assert.Equal("In", Assert.Single(report.Tasks).TaskName);
    }

    [Fact]
    public void Build_Empty_HasSevenZeroDays()
    {
        var report = WeeklyReport.Build(Mon, [], [], Plus3);

        Assert.Equal(7, report.Days.Count);
        Assert.All(report.Days, d => Assert.Equal(TimeSpan.Zero, d.Actual + d.Planned));
        Assert.Empty(report.Tasks);
    }

    [Fact]
    public void ReportService_LoadsWeekFromRepositories()
    {
        var sessions = new InMemorySessionRepository();
        sessions.Add(Sess("Code", new DateTimeOffset(2026, 10, 13, 7, 0, 0, TimeSpan.Zero), 30, 30));
        sessions.Add(Sess("Code", new DateTimeOffset(2026, 10, 20, 7, 0, 0, TimeSpan.Zero), 30, 30));
        var plan = new PlanService(new InMemoryPlanRepository());
        plan.AddRecurring(new PlanRecurrence(Mon, null, WeekDays.Weekdays), new TimeOnly(9, 0), new TimeOnly(10, 0), "Code", null);

        var report = new ReportService(sessions, plan).GetWeek(Mon.AddDays(3), Plus3);

        Assert.Equal(TimeSpan.FromMinutes(30), report.TotalActual);
        Assert.Equal(TimeSpan.FromHours(5), report.TotalPlanned);
    }
}
