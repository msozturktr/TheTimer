using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.App.Controls.Plan;
using TheTimer.Core.Planning;

namespace TheTimer.App.ViewModels;

/// <summary>One block capsule of the day ribbon.</summary>
public sealed partial class PlanOccurrenceItemViewModel : ObservableObject, ITimelineItem
{
    public PlanOccurrenceItemViewModel(
        PlanOccurrence occurrence, TimeSpan actual, bool canStart,
        Action<PlanOccurrence> start, Action<PlanOccurrence> edit, Action<PlanOccurrence> delete)
    {
        Occurrence = occurrence;
        var block = occurrence.Block;
        StartMinutes = block.Start.ToTimeSpan().TotalMinutes;
        EndMinutes = block.End.ToTimeSpan().TotalMinutes;
        TimeRange = $"{block.Start:HH:mm} – {block.End:HH:mm}";
        TimeRangeTight = $"{block.Start:HH:mm}–{block.End:HH:mm}";
        StartOnly = $"{block.Start:HH:mm}";
        TaskName = block.TaskName;
        Tag = block.Tag;
        TagDisplay = PlanItemFormat.TagDisplay(block.Tag);
        TagStyle = TagPalette.Style(block.Tag);
        IsRecurring = block.Recurrence is not null;
        RecurrenceTip = block.Recurrence is { } rec ? PlanFormat.DescribeRecurrence(rec) : string.Empty;
        RecurrenceChip = block.Recurrence is { } r ? PlanFormat.DescribeRecurrence(r with { EndDate = null }) : string.Empty;
        HasOverlap = occurrence.HasOverlap;

        var planned = occurrence.PlannedDuration;
        ActualText = $"{PlanFormat.FormatHm(actual)} / {PlanFormat.FormatHm(planned)}";
        IsIdle = actual < TimeSpan.FromMinutes(1);
        Fraction = planned > TimeSpan.Zero ? Math.Min(actual / planned, 1) : 0;
        var over = actual - planned;
        HasOverflow = over >= TimeSpan.FromMinutes(1);
        OverflowText = HasOverflow ? $"+{PlanFormat.FormatHm(over)} fazla" : string.Empty;
        IsCompact = (EndMinutes - StartMinutes) < 50;

        CanStart = canStart;
        StartCommand = new RelayCommand(() => start(occurrence), () => canStart);
        EditCommand = new RelayCommand(() => edit(occurrence));
        DeleteCommand = new RelayCommand(() => delete(occurrence));
    }

    public PlanOccurrence Occurrence { get; }

    public double StartMinutes { get; }

    public double EndMinutes { get; }

    public int Column { get; internal set; }

    public int ColumnCount { get; internal set; } = 1;

    /// <summary>Overlapping blocks share the width, so they drop the chips row and the actual/planned text.</summary>
    public bool IsDense => !IsCompact && ColumnCount > 1;

    public bool ShowRatio => !IsSqueezed && !IsDense;

    public bool ShowDenseChip => HasOverlap && !IsSqueezed;

    public bool ShowDenseIcon => HasOverlap && IsSqueezed;

    public bool ShowDenseTag => HasTag && !IsSqueezed;

    public bool IsFull => !IsCompact && ColumnCount <= 1;

    public string TimeRange { get; }

    /// <summary>"13:30–15:00" without spaces, for narrow side-by-side columns.</summary>
    public string TimeRangeTight { get; }

    private string StartOnly { get; }

    /// <summary>Time label of a side-by-side block: the range, or only the start while the editor squeezes the ribbon.</summary>
    public string DenseTime => IsSqueezed ? StartOnly : TimeRangeTight;

    public bool ShowCompactRepeat => IsRecurring && !IsSqueezed;

    public string TaskName { get; }

    public string? Tag { get; }

    public string TagDisplay { get; }

    public TagStyle TagStyle { get; }

    public bool HasTag => Tag is not null;

    public bool IsRecurring { get; }

    public string RecurrenceTip { get; }

    /// <summary>Short recurrence label for the chip (no end date), e.g. "Hafta içi".</summary>
    public string RecurrenceChip { get; }

    public bool HasOverlap { get; }

    /// <summary>"1:45 / 2:00" (actual / planned).</summary>
    public string ActualText { get; }

    /// <summary>Nothing tracked yet (renders faint instead of amber).</summary>
    public bool IsIdle { get; }

    /// <summary>Actual / planned for the progress bar, capped at 1.</summary>
    public double Fraction { get; }

    public bool HasOverflow { get; }

    public string OverflowText { get; }

    /// <summary>Short block: rendered as a single line.</summary>
    public bool IsCompact { get; }

    public bool CanStart { get; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    /// <summary>The editor card is open, so the ribbon is narrower: drop secondary text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRatio), nameof(ShowDenseChip), nameof(ShowDenseIcon), nameof(ShowDenseTag), nameof(ShowCompactRepeat), nameof(DenseTime))]
    public partial bool IsSqueezed { get; set; }

    public ICommand StartCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }
}

/// <summary>One row of the per-task planned vs actual table.</summary>
public sealed class TaskComparisonItemViewModel
{
    public TaskComparisonItemViewModel(TaskPlanComparison comparison, string? tag)
    {
        TaskName = comparison.TaskName;
        TagDisplay = PlanItemFormat.TagDisplay(tag);
        TagStyle = TagPalette.Style(tag);
        HasTag = !string.IsNullOrEmpty(tag);
        PlannedText = PlanFormat.FormatHm(comparison.Planned);
        ActualText = PlanFormat.FormatHm(comparison.Actual);
        DifferenceText = PlanFormat.FormatSigned(comparison.Difference);
        IsPositive = DifferenceText.StartsWith('+');
        IsNegative = DifferenceText.StartsWith('−');
        IsNeutral = !IsPositive && !IsNegative;
    }

    public string TaskName { get; }

    public string TagDisplay { get; }

    public TagStyle TagStyle { get; }

    public bool HasTag { get; }

    public string PlannedText { get; }

    public string ActualText { get; }

    public string DifferenceText { get; }

    /// <summary>More time spent than planned.</summary>
    public bool IsPositive { get; }

    /// <summary>Less time spent than planned.</summary>
    public bool IsNegative { get; }

    public bool IsNeutral { get; }
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

/// <summary>A quick-pick tag chip in the editor.</summary>
public sealed class TagChipViewModel(string tag)
{
    public string Tag { get; } = tag;

    public string Display { get; } = PlanItemFormat.TagDisplay(tag);

    public TagStyle Style { get; } = TagPalette.Style(tag);
}

internal static class PlanItemFormat
{
    /// <summary>"#yazım" for "yazım"; a tag already starting with '#' is kept; empty for no tag.</summary>
    public static string TagDisplay(string? tag) =>
        string.IsNullOrWhiteSpace(tag) ? string.Empty : tag.StartsWith('#') ? tag : "#" + tag;
}
