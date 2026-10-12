using System.Windows.Input;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public sealed class SessionItemViewModel
{
    public SessionItemViewModel(TimeSession session, TimeZoneInfo timeZone, ICommand deleteCommand)
    {
        Id = session.Id;
        DeleteCommand = deleteCommand;
        TaskName = session.TaskName;
        Tag = session.Tag;
        var start = TimeZoneInfo.ConvertTime(session.StartedAt, timeZone);
        var end = TimeZoneInfo.ConvertTime(session.EndedAt, timeZone);
        TimeRange = $"{start:HH:mm}–{end:HH:mm}";
        DurationText = MainWindowViewModel.FormatDuration(session.Duration);
    }

    public Guid Id { get; }

    public ICommand DeleteCommand { get; }

    public string TaskName { get; }

    public string? Tag { get; }

    public bool HasTag => Tag is not null;

    public string TimeRange { get; }

    public string DurationText { get; }
}
