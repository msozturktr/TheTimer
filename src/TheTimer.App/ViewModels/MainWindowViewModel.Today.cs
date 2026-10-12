using CommunityToolkit.Mvvm.ComponentModel;

namespace TheTimer.App.ViewModels;

/// <summary>Bugün tab extras: split date heading, header total and the dome data source.</summary>
public partial class MainWindowViewModel
{
    /// <summary>Feeds the 24-hour dome; set by <c>AppComposition</c>.</summary>
    public TodayArcViewModel? TodayArc { get; init; }

    [ObservableProperty]
    public partial string DateMainText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WeekdayText { get; set; } = string.Empty;

    /// <summary>Header total as h:mm, e.g. "4:37".</summary>
    [ObservableProperty]
    public partial string TotalShortText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string TotalCaptionText { get; set; } = "toplam · 0 oturum";

    /// <summary>True while a Pomodoro break is running (breaks are never saved and stay off the dome).</summary>
    internal bool IsRunningBreak => _runningBreak;

    internal static string FormatHm(TimeSpan value) => $"{(int)value.TotalHours}:{value.Minutes:00}";

    partial void OnDateTextChanged(string value)
    {
        // "12 Ekim 2026 Pazartesi" -> "12 Ekim 2026" + "Pazartesi"
        var cut = value.LastIndexOf(' ');
        DateMainText = cut > 0 ? value[..cut] : value;
        WeekdayText = cut > 0 ? value[(cut + 1)..] : string.Empty;
    }

    private void UpdateTodayHeader(TimeSpan total, bool includesRunning)
    {
        TotalShortText = FormatHm(total);
        var count = TodaySessions.Count + (includesRunning ? 1 : 0);
        TotalCaptionText = $"toplam · {count} oturum";
    }
}
