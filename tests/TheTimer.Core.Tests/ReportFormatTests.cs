using TheTimer.Core.Reports;

namespace TheTimer.Core.Tests;

public class ReportFormatTests
{
    [Fact]
    public void WeekRange_SameMonth() =>
        Assert.Equal("5–11 Ekim 2026", ReportFormat.WeekRange(new DateOnly(2026, 10, 5)));

    [Fact]
    public void WeekRange_TwoMonths() =>
        Assert.Equal("28 Eylül – 4 Ekim 2026", ReportFormat.WeekRange(new DateOnly(2026, 9, 28)));

    [Fact]
    public void WeekRange_TwoYears() =>
        Assert.Equal("28 Aralık 2026 – 3 Ocak 2027", ReportFormat.WeekRange(new DateOnly(2026, 12, 28)));

    [Theory]
    [InlineData(15, "+0:15")]
    [InlineData(-30, "−0:30")]
    [InlineData(0, "0:00")]
    [InlineData(135, "+2:15")]
    public void SignedDuration_FormatsSign(int minutes, string expected) =>
        Assert.Equal(expected, ReportFormat.SignedDuration(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Hm_RoundsToMinute()
    {
        Assert.Equal("2:15", ReportFormat.Hm(TimeSpan.FromMinutes(135)));
        Assert.Equal("0:01", ReportFormat.Hm(TimeSpan.FromSeconds(40)));
        Assert.Equal("26:00", ReportFormat.Hm(TimeSpan.FromHours(26)));
    }

    [Fact]
    public void Percentage_NullWithoutPlan() =>
        Assert.Null(ReportFormat.Percentage(TimeSpan.FromHours(1), TimeSpan.Zero));

    [Fact]
    public void Percentage_Rounds() =>
        Assert.Equal("%85", ReportFormat.Percentage(TimeSpan.FromMinutes(51), TimeSpan.FromMinutes(60)));

    [Fact]
    public void ShortDayName_MondayFirst()
    {
        Assert.Equal("Pzt", ReportFormat.ShortDayName(new DateOnly(2026, 10, 12)));
        Assert.Equal("Paz", ReportFormat.ShortDayName(new DateOnly(2026, 10, 18)));
    }
}
