using TheTimer.Core.Planning;

namespace TheTimer.Core.Tests;

public class PlanFormatTests
{
    private static readonly DateOnly D = new(2026, 10, 12);

    [Fact]
    public void Describes_common_recurrences()
    {
        Assert.Equal("Her gün", PlanFormat.DescribeRecurrence(new PlanRecurrence(D, null, WeekDays.EveryDay)));
        Assert.Equal("Hafta içi", PlanFormat.DescribeRecurrence(new PlanRecurrence(D, null, WeekDays.Weekdays)));
        Assert.Equal("Hafta sonu", PlanFormat.DescribeRecurrence(new PlanRecurrence(D, null, WeekDays.Weekend)));
    }

    [Fact]
    public void Describes_custom_days_in_week_order_with_end_date()
    {
        var rec = new PlanRecurrence(D, new DateOnly(2026, 12, 31),
            WeekDays.Friday | WeekDays.Monday | WeekDays.Wednesday);
        Assert.Equal("Pzt, Çar, Cum · 31 Ara 2026 tarihine kadar", PlanFormat.DescribeRecurrence(rec));
    }

    [Theory]
    [InlineData(15, "+0:15")]
    [InlineData(-30, "−0:30")]
    [InlineData(0, "0:00")]
    [InlineData(135, "+2:15")]
    public void Formats_signed_durations(int minutes, string expected) =>
        Assert.Equal(expected, PlanFormat.FormatSigned(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Formats_hours_beyond_a_day_and_minutes()
    {
        Assert.Equal("26:05", PlanFormat.FormatHm(TimeSpan.FromMinutes(26 * 60 + 5)));
        Assert.Equal("45 dk", PlanFormat.FormatMinutes(TimeSpan.FromMinutes(44.6)));
    }

    [Fact]
    public void Day_label_of_single_day() => Assert.Equal("Cmt", PlanFormat.DayLabel(WeekDays.Saturday));
}
