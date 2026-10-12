using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.Core.Planning;
using TheTimer.Core.Reports;

namespace TheTimer.App.ViewModels;

/// <summary>One day column of the weekly bar chart.</summary>
public sealed class ReportDayItemViewModel
{
    public ReportDayItemViewModel(DayTotal day, double scale, double chartHeight, bool isToday)
    {
        WeekdayText = ReportFormat.ShortDayName(day.Day);
        DayNumberText = day.Day.Day.ToString();
        ValueText = ReportFormat.Hm(day.Actual);
        ToolTipText = $"Gerçekleşen {ReportFormat.Hm(day.Actual)} / Planlanan {ReportFormat.Hm(day.Planned)}";
        ActualHeight = Math.Round(day.Actual.TotalHours / scale * chartHeight, 1);
        PlannedHeight = Math.Round(day.Planned.TotalHours / scale * chartHeight, 1);
        IsToday = isToday;
    }

    public string WeekdayText { get; }

    public string DayNumberText { get; }

    public string ValueText { get; }

    public string ToolTipText { get; }

    public double ActualHeight { get; }

    public double PlannedHeight { get; }

    public bool IsToday { get; }
}

/// <summary>One row of the per-task plan vs. actual table.</summary>
public sealed class ReportTaskItemViewModel
{
    public ReportTaskItemViewModel(TaskPlanComparison task)
    {
        TaskName = task.TaskName;
        PlannedText = ReportFormat.Hm(task.Planned);
        ActualText = ReportFormat.Hm(task.Actual);
        DifferenceText = ReportFormat.SignedDuration(task.Difference);
        IsPositive = DifferenceText.StartsWith('+');
        IsNegative = DifferenceText.StartsWith('−');
    }

    public string TaskName { get; }

    public string PlannedText { get; }

    public string ActualText { get; }

    public string DifferenceText { get; }

    public bool IsPositive { get; }

    public bool IsNegative { get; }
}

public partial class ReportViewModel : ViewModelBase
{
    public const double ChartHeight = 120;

    private readonly ReportService _service;
    private readonly TimeProvider _timeProvider;
    private DateOnly _weekStart;

    public ReportViewModel(ReportService service, TimeProvider timeProvider)
    {
        _service = service;
        _timeProvider = timeProvider;
        _weekStart = WeeklyReport.WeekStart(Today());
        Refresh();
    }

    [ObservableProperty]
    public partial string WeekText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial bool IsCurrentWeek { get; set; } = true;

    public ObservableCollection<ReportDayItemViewModel> Days { get; } = [];

    public ObservableCollection<ReportTaskItemViewModel> Tasks { get; } = [];

    /// <summary>Reloads the shown week from the repositories.</summary>
    public void Refresh()
    {
        var today = Today();
        WeeklyReportData data;
        try
        {
            data = _service.GetWeek(_weekStart, _timeProvider.LocalTimeZone);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to build weekly report: {ex}");
            return;
        }

        var scale = Math.Max(1.0, data.Days.Max(d => Math.Max(d.Actual.TotalHours, d.Planned.TotalHours)));

        WeekText = ReportFormat.WeekRange(data.WeekStart);
        IsCurrentWeek = data.WeekStart == WeeklyReport.WeekStart(today);

        var totals = $"Gerçekleşen: {ReportFormat.Hm(data.TotalActual)} · Planlanan: {ReportFormat.Hm(data.TotalPlanned)}";
        var percent = ReportFormat.Percentage(data.TotalActual, data.TotalPlanned);
        TotalsText = percent is null ? totals : $"{totals} · {percent}";

        Days.Clear();
        foreach (var day in data.Days)
            Days.Add(new ReportDayItemViewModel(day, scale, ChartHeight, day.Day == today));

        Tasks.Clear();
        foreach (var task in data.Tasks)
            Tasks.Add(new ReportTaskItemViewModel(task));

        HasData = data.TotalActual > TimeSpan.Zero || data.TotalPlanned > TimeSpan.Zero || data.Tasks.Count > 0;
    }

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

    [RelayCommand]
    private void ThisWeek()
    {
        _weekStart = WeeklyReport.WeekStart(Today());
        Refresh();
    }

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
}
