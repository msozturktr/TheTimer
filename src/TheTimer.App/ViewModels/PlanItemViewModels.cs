using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.Core.Planning;

namespace TheTimer.App.ViewModels;

/// <summary>One row of the plan list.</summary>
public sealed class PlanOccurrenceItemViewModel
{
    public PlanOccurrenceItemViewModel(
        PlanOccurrence occurrence, TimeSpan actual, bool canStart,
        Action<PlanOccurrence> start, Action<PlanOccurrence> edit, Action<PlanOccurrence> delete)
    {
        Occurrence = occurrence;
        var block = occurrence.Block;
        TimeRange = $"{block.Start:HH:mm}–{block.End:HH:mm}";
        TaskName = block.TaskName;
        Tag = block.Tag;
        IsRecurring = block.Recurrence is not null;
        RecurrenceTip = block.Recurrence is { } rec ? PlanFormat.DescribeRecurrence(rec) : string.Empty;
        HasOverlap = occurrence.HasOverlap;
        ActualText = $"{PlanFormat.FormatMinutes(actual)} / {PlanFormat.FormatMinutes(occurrence.PlannedDuration)}";
        CanStart = canStart;
        StartCommand = new RelayCommand(() => start(occurrence), () => canStart);
        EditCommand = new RelayCommand(() => edit(occurrence));
        DeleteCommand = new RelayCommand(() => delete(occurrence));
    }

    public PlanOccurrence Occurrence { get; }

    public string TimeRange { get; }

    public string TaskName { get; }

    public string? Tag { get; }

    public bool HasTag => Tag is not null;

    public bool IsRecurring { get; }

    public string RecurrenceTip { get; }

    public bool HasOverlap { get; }

    public string ActualText { get; }

    public bool CanStart { get; }

    public ICommand StartCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }
}

/// <summary>One row of the per-task planned vs actual table.</summary>
public sealed class TaskComparisonItemViewModel
{
    public TaskComparisonItemViewModel(TaskPlanComparison comparison)
    {
        TaskName = comparison.TaskName;
        PlannedText = PlanFormat.FormatHm(comparison.Planned);
        ActualText = PlanFormat.FormatHm(comparison.Actual);
        DifferenceText = PlanFormat.FormatSigned(comparison.Difference);
        IsPositive = DifferenceText.StartsWith('+');
        IsNegative = DifferenceText.StartsWith('−');
    }

    public string TaskName { get; }

    public string PlannedText { get; }

    public string ActualText { get; }

    public string DifferenceText { get; }

    /// <summary>More time spent than planned.</summary>
    public bool IsPositive { get; }

    /// <summary>Less time spent than planned.</summary>
    public bool IsNegative { get; }
}

/// <summary>A weekday toggle in the custom-recurrence editor.</summary>
public sealed partial class DayToggleViewModel : ObservableObject
{
    public DayToggleViewModel(WeekDays day)
    {
        Day = day;
        Label = PlanFormat.DayLabel(day);
    }

    public WeekDays Day { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
