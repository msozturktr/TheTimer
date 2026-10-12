using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.App.Controls.Report;
using TheTimer.Core.Planning;
using TheTimer.Core.Reports;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

/// <summary>One row of the per-task plan vs. actual list.</summary>
public sealed class ReportTaskItemViewModel
{
    // Existing Astrolab accent tokens (Cyan, Lavender, Mint, Amber, Rose, Coral); the dot colour is stable per task name.
    private static readonly Color[] Palette =
    [
        Color.Parse("#7DD3FC"), Color.Parse("#A78BFA"), Color.Parse("#5EEAD4"),
        Color.Parse("#FFB547"), Color.Parse("#FF6B81"), Color.Parse("#FF7A59"),
    ];

    public ReportTaskItemViewModel(TaskPlanComparison task, double scaleHours, string? tag = null)
    {
        TaskName = task.TaskName;
        PlannedText = ReportFormat.Hm(task.Planned);
        ActualText = ReportFormat.Hm(task.Actual);
        DifferenceText = ReportFormat.SignedDuration(task.Difference);
        IsPositive = DifferenceText.StartsWith('+');
        IsNegative = DifferenceText.StartsWith('−');
        IsNeutral = !IsPositive && !IsNegative;
        DotColor = string.IsNullOrWhiteSpace(tag) ? Palette[StableHash(task.TaskName.ToLowerInvariant()) % Palette.Length] : TagPalette.For(tag);
        PlannedFraction = scaleHours > 0 ? task.Planned.TotalHours / scaleHours : 0;
        ActualFraction = scaleHours > 0 ? task.Actual.TotalHours / scaleHours : 0;

        if (task.Planned <= TimeSpan.Zero)
        {
            CaptionText = "Plansız";
        }
        else
        {
            var ratio = task.Actual.TotalSeconds / task.Planned.TotalSeconds;
            if (ratio > 1.005)
            {
                CaptionText = $"+%{(int)Math.Round((ratio - 1) * 100, MidpointRounding.AwayFromZero)} aşıldı";
                IsCaptionPositive = true;
            }
            else
            {
                CaptionText = $"%{(int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero)} tamamlandı";
                IsCaptionPositive = ratio >= 0.995;
            }
        }
    }

    public string TaskName { get; }

    public string PlannedText { get; }

    public string ActualText { get; }

    public string DifferenceText { get; }

    public bool IsPositive { get; }

    public bool IsNegative { get; }

    public bool IsNeutral { get; }

    public Color DotColor { get; }

    public double PlannedFraction { get; }

    public double ActualFraction { get; }

    public string CaptionText { get; }

    public bool IsCaptionPositive { get; }

    // string.GetHashCode is randomised per process; colours must not flicker between runs.
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text)
            hash = unchecked(hash * 31 + ch);
        return hash & int.MaxValue;
    }
}

public partial class ReportViewModel : ViewModelBase
{
    private static readonly string[] LongDayNames =
        ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];

    private readonly ReportService _service;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionRepository? _sessions;
    private readonly PlanService? _plan;
    private DateOnly _weekStart;

    public ReportViewModel(ReportService service, TimeProvider timeProvider,
        ISessionRepository? sessions = null, PlanService? plan = null)
    {
        _service = service;
        _timeProvider = timeProvider;
        _sessions = sessions;
        _plan = plan;
        _weekStart = WeeklyReport.WeekStart(Today());
        Refresh();
    }

    [ObservableProperty]
    public partial string WeekText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial bool IsCurrentWeek { get; set; } = true;

    [ObservableProperty]
    public partial string ActualTotalText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string PlannedTotalText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string SessionsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BlocksText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPlan { get; set; }

    [ObservableProperty]
    public partial string PercentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PercentSubText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double PercentValue { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ConstellationDay> ChartDays { get; set; } = [];

    [ObservableProperty]
    public partial bool HasBestDay { get; set; }

    [ObservableProperty]
    public partial string BestDayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BestDayHours { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BestDayCaption { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTasks { get; set; }

    [ObservableProperty]
    public partial string TasksFooterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FooterPercentText { get; set; } = string.Empty;

    public ObservableCollection<ReportTaskItemViewModel> Tasks { get; } = [];

    /// <summary>Reloads the shown week from the repositories.</summary>
    public void Refresh()
    {
        var today = Today();
        var zone = _timeProvider.LocalTimeZone;
        WeeklyReportData data;
        var sessionCount = 0;
        var blockCount = 0;
        IReadOnlyList<TimeSession> weekSessions = [];
        try
        {
            data = _service.GetWeek(_weekStart, zone);
            var end = data.WeekStart.AddDays(6);
            if (_sessions is not null)
            {
                weekSessions = _sessions.GetByRange(data.WeekStart, end, zone);
                sessionCount = weekSessions.Count;
            }
            if (_plan is not null)
                blockCount = _plan.GetOccurrences(data.WeekStart, end, zone).Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to build weekly report: {ex}");
            return;
        }

        WeekText = ReportFormat.WeekRange(data.WeekStart);
        IsCurrentWeek = data.WeekStart == WeeklyReport.WeekStart(today);
        var weeks = (data.WeekStart.DayNumber - WeeklyReport.WeekStart(today).DayNumber) / 7;
        WeekNavLabel = weeks switch
        {
            0 => "Bu hafta",
            -1 => "Geçen hafta",
            1 => "Gelecek hafta",
            < 0 => $"{-weeks} hafta önce",
            _ => $"{weeks} hafta sonra",
        };
        OnPropertyChanged(nameof(WeekNavLabel));

        ActualTotalText = ReportFormat.Hm(data.TotalActual);
        PlannedTotalText = ReportFormat.Hm(data.TotalPlanned);
        SessionsText = $"{sessionCount} odak oturumu";
        BlocksText = $"{blockCount} blok";
        HasPlan = data.TotalPlanned > TimeSpan.Zero;
        var percent = ReportFormat.Percentage(data.TotalActual, data.TotalPlanned);
        PercentText = percent ?? "—";
        PercentSubText = percent is null ? "Plan yok" : $"{ReportFormat.Hm(data.TotalActual)} / {ReportFormat.Hm(data.TotalPlanned)}";
        PercentValue = HasPlan ? data.TotalActual.TotalSeconds / data.TotalPlanned.TotalSeconds : 0;
        FooterPercentText = percent is null ? string.Empty : $"Gerçekleşme: {percent}";

        ChartDays = data.Days.Select(d => new ConstellationDay(
            $"{ReportFormat.ShortDayName(d.Day).ToUpperInvariant()} {d.Day.Day}",
            d.Actual.TotalHours,
            d.Planned.TotalHours,
            d.Day == today,
            ReportFormat.Hm(d.Actual),
            ReportFormat.Hm(d.Planned),
            $"{LongName(d.Day)} {d.Day.Day}\nGerçekleşen {ReportFormat.Hm(d.Actual)} / Planlanan {ReportFormat.Hm(d.Planned)}"))
            .ToList();

        var best = data.Days.MaxBy(d => d.Actual);
        HasBestDay = best is not null && best.Actual > TimeSpan.Zero;
        if (best is not null && HasBestDay)
        {
            BestDayName = LongName(best.Day);
            BestDayHours = ReportFormat.Hm(best.Actual);
            BestDayCaption = best.Planned > TimeSpan.Zero
                ? $"Planın %{(int)Math.Round(best.Actual.TotalSeconds / best.Planned.TotalSeconds * 100, MidpointRounding.AwayFromZero)}'si"
                : "Plan yok";
        }
        else
        {
            BestDayName = "—";
            BestDayHours = "0:00";
            BestDayCaption = "Henüz kayıt yok";
        }

        var scale = data.Tasks.Count == 0
            ? 1.0
            : Math.Max(0.1, data.Tasks.Max(t => Math.Max(t.Planned.TotalHours, t.Actual.TotalHours)));
        Tasks.Clear();
        foreach (var task in data.Tasks)
            Tasks.Add(new ReportTaskItemViewModel(task, scale, TagOfLatestSession(weekSessions, task.TaskName)));
        HasTasks = data.Tasks.Count > 0;
        TasksFooterText = $"Toplam {data.Tasks.Count} görev · {sessionCount} oturum";

        HasData = data.TotalActual > TimeSpan.Zero || data.TotalPlanned > TimeSpan.Zero || data.Tasks.Count > 0;
    }

    /// <summary>Tag of the task's most recent session in the week (case-insensitive name match), if any.</summary>
    private static string? TagOfLatestSession(IEnumerable<TimeSession> sessions, string taskName) =>
        sessions
            .Where(s => !string.IsNullOrWhiteSpace(s.Tag)
                        && string.Equals(s.TaskName.Trim(), taskName.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.Tag)
            .FirstOrDefault();

    [RelayCommand]
    private void PreviousWeek()
    {
        _weekStart = _weekStart.AddDays(-7);
        Refresh();
    }

    [RelayCommand]
    private void NextWeek()
    {
        _weekStart = _weekStart.AddDays(7);
        Refresh();
    }

    /// <summary>Relative name of the shown week; the button always jumps back to this week.</summary>
    public string WeekNavLabel { get; private set; } = "Bu hafta";

    [RelayCommand]
    private void ThisWeek()
    {
        _weekStart = WeeklyReport.WeekStart(Today());
        Refresh();
    }

    private static string LongName(DateOnly day) => LongDayNames[((int)day.DayOfWeek + 6) % 7];

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
}
