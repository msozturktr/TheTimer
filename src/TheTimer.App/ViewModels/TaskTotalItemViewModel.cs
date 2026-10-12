using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public sealed class TaskTotalItemViewModel
{
    public TaskTotalItemViewModel(TaskTotal total)
    {
        TaskName = total.TaskName;
        CountText = $"{total.SessionCount} oturum";
        TotalText = MainWindowViewModel.FormatDuration(total.Total);
    }

    public string TaskName { get; }

    public string CountText { get; }

    public string TotalText { get; }
}
