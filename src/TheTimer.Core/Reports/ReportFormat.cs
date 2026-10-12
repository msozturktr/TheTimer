using System.Globalization;

namespace TheTimer.Core.Reports;

/// <summary>Pure text formatting for the weekly report (no UI types).</summary>
public static class ReportFormat
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] ShortDayNames = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];

    /// <summary>Short Turkish weekday label (Pzt..Paz) for the given day.</summary>
    public static string ShortDayName(DateOnly day) => ShortDayNames[((int)day.DayOfWeek + 6) % 7];

    /// <summary>"5–11 Ekim 2026", "28 Eylül – 4 Ekim 2026" or "28 Aralık 2026 – 3 Ocak 2027".</summary>
    public static string WeekRange(DateOnly weekStart, CultureInfo? culture = null)
    {
        culture ??= Turkish;
        var end = weekStart.AddDays(6);
        if (weekStart.Year != end.Year)
            return $"{Day(weekStart, "d MMMM yyyy", culture)} – {Day(end, "d MMMM yyyy", culture)}";
        if (weekStart.Month != end.Month)
            return $"{Day(weekStart, "d MMMM", culture)} – {Day(end, "d MMMM yyyy", culture)}";
        return $"{weekStart.Day}–{Day(end, "d MMMM yyyy", culture)}";
    }

    /// <summary>"h:mm" rounded to the nearest minute (negative values lose their sign).</summary>
    public static string Hm(TimeSpan value)
    {
        var minutes = (int)Math.Round(Math.Abs(value.TotalMinutes), MidpointRounding.AwayFromZero);
        return $"{minutes / 60}:{minutes % 60:00}";
    }

    /// <summary>"+0:15", "−0:30" (U+2212) or "0:00" when the rounded difference is zero.</summary>
    public static string SignedDuration(TimeSpan value)
    {
        var text = Hm(value);
        if (text == "0:00")
            return text;
        return (value < TimeSpan.Zero ? "−" : "+") + text;
    }

    /// <summary>"%85" when something was planned; null otherwise.</summary>
    public static string? Percentage(TimeSpan actual, TimeSpan planned)
    {
        if (planned <= TimeSpan.Zero)
            return null;
        var percent = (int)Math.Round(actual.TotalSeconds / planned.TotalSeconds * 100, MidpointRounding.AwayFromZero);
        return $"%{percent}";
    }

    private static string Day(DateOnly day, string format, CultureInfo culture) =>
        day.ToString(format, culture);
}
