using System.Globalization;

namespace TheTimer.Core.Planning;

/// <summary>Pure Turkish display formatting for plan data (kept out of the UI so it is testable).</summary>
public static class PlanFormat
{
    private static readonly CultureInfo Tr = new("tr-TR");

    private static readonly string[] MonthShort =
        ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

    private static readonly (WeekDays Day, string Label)[] DayLabels =
    [
        (WeekDays.Monday, "Pzt"), (WeekDays.Tuesday, "Sal"), (WeekDays.Wednesday, "Çar"),
        (WeekDays.Thursday, "Per"), (WeekDays.Friday, "Cum"), (WeekDays.Saturday, "Cmt"), (WeekDays.Sunday, "Paz"),
    ];

    /// <summary>Short Turkish weekday label, e.g. "Pzt".</summary>
    public static string DayLabel(WeekDays day) =>
        DayLabels.FirstOrDefault(d => d.Day == day).Label ?? string.Empty;

    /// <summary>e.g. "Hafta içi", "Her gün", "Pzt, Çar, Cum", optionally "· 31 Ara 2026 tarihine kadar".</summary>
    public static string DescribeRecurrence(PlanRecurrence recurrence)
    {
        ArgumentNullException.ThrowIfNull(recurrence);
        var days = recurrence.Days switch
        {
            WeekDays.EveryDay => "Her gün",
            WeekDays.Weekdays => "Hafta içi",
            WeekDays.Weekend => "Hafta sonu",
            _ => string.Join(", ", DayLabels.Where(d => (recurrence.Days & d.Day) != 0).Select(d => d.Label)),
        };
        if (recurrence.EndDate is not { } end)
            return days;
        return $"{days} · {end.Day} {MonthShort[end.Month - 1]} {end.Year} tarihine kadar";
    }

    /// <summary>Total hours and minutes as "h:mm" (sign ignored); minutes are floored.</summary>
    public static string FormatHm(TimeSpan value)
    {
        var abs = value.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"{(int)abs.TotalHours}:{abs.Minutes:00}");
    }

    /// <summary>"+0:15" / "−0:30" (U+2212 minus); "0:00" when there is no difference at minute resolution.</summary>
    public static string FormatSigned(TimeSpan value)
    {
        var text = FormatHm(value);
        if (text == "0:00")
            return text;
        return (value < TimeSpan.Zero ? "−" : "+") + text;
    }

    /// <summary>Whole minutes, rounded: "45 dk".</summary>
    public static string FormatMinutes(TimeSpan value) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(value.TotalMinutes)} dk");

    /// <summary>Long Turkish date, e.g. "12 Ekim 2026 Pazartesi".</summary>
    public static string LongDate(DateOnly day) => day.ToString("d MMMM yyyy dddd", Tr);
}
