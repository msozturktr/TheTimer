using Avalonia.Media;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public sealed class TaskTotalItemViewModel
{
    public TaskTotalItemViewModel(TaskTotal total, string? tag = null, TimeSpan dayTotal = default)
    {
        TaskName = total.TaskName;
        CountText = $"{total.SessionCount} oturum";
        TotalText = MainWindowViewModel.FormatDuration(total.Total);
        ShortTotalText = MainWindowViewModel.FormatHm(total.Total);
        Fraction = dayTotal > TimeSpan.Zero ? Math.Clamp(total.Total / dayTotal, 0, 1) : 0;
        PercentText = $"{Math.Round(Fraction * 100):0}%";
        TagText = string.IsNullOrWhiteSpace(tag) ? string.Empty : "#" + tag;
        TagBrush = TagPalette.Solid(tag);
    }

    public string TaskName { get; }

    public string CountText { get; }

    public string TotalText { get; }

    public string ShortTotalText { get; }

    /// <summary>Share of the day's tracked time, 0..1.</summary>
    public double Fraction { get; }

    public string PercentText { get; }

    public string TagText { get; }

    public IBrush TagBrush { get; }
}
