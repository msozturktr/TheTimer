using System.Windows.Input;
using Avalonia.Media;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public sealed class SessionItemViewModel
{
    private static readonly IBrush AmberNode = new SolidColorBrush(Color.Parse("#FFB547"));
    private static readonly IBrush CoralNode = new SolidColorBrush(Color.Parse("#FF7A59"));

    public SessionItemViewModel(TimeSession session, TimeZoneInfo timeZone, ICommand deleteCommand,
        bool isFirst = true, bool isLast = true)
    {
        Id = session.Id;
        DeleteCommand = deleteCommand;
        TaskName = session.TaskName;
        Tag = session.Tag;
        var start = TimeZoneInfo.ConvertTime(session.StartedAt, timeZone);
        var end = TimeZoneInfo.ConvertTime(session.EndedAt, timeZone);
        TimeRange = $"{start:HH:mm} – {end:HH:mm}";
        DurationText = MainWindowViewModel.FormatDuration(session.Duration);
        ShortDurationText = MainWindowViewModel.FormatHm(session.Duration);
        IsFirst = isFirst;
        IsLast = isLast;
        NodeBrush = isLast ? CoralNode : AmberNode;
        TagText = session.Tag is { Length: > 0 } t ? "#" + t : string.Empty;
        TagBrush = TagPalette.Solid(session.Tag);
        TagBackground = TagPalette.Tint(session.Tag, 0.12);
        TagBorder = TagPalette.Tint(session.Tag, 0.35);
    }

    public Guid Id { get; }

    public ICommand DeleteCommand { get; }

    public string TaskName { get; }

    public string? Tag { get; }

    public bool HasTag => Tag is not null;

    public string TimeRange { get; }

    public string DurationText { get; }

    public string ShortDurationText { get; }

    public bool IsFirst { get; }

    /// <summary>The latest session of the day (chronologically last); its chain node is coral.</summary>
    public bool IsLast { get; }

    public IBrush NodeBrush { get; }

    public string TagText { get; }

    public IBrush TagBrush { get; }

    public IBrush TagBackground { get; }

    public IBrush TagBorder { get; }
}
