using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using TheTimer.App.Controls.Timer;
using TheTimer.Core.Pomodoro;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

/// <summary>A finished session shown in the "Son oturumlar" list of the timer tab.</summary>
public sealed record RecentSessionItem(string TaskName, string? TagText, string DurationText)
{
    public bool HasTag => TagText is not null;
}

// Timer tab (dial, side panel) presentation state; the timing logic itself lives in MainWindowViewModel.cs.
public partial class MainWindowViewModel
{
    private List<DaySegment> _finishedSegments = [];
    private int _ribbonMinute = -1;

    [ObservableProperty]
    public partial double DialProgress { get; set; }

    [ObservableProperty]
    public partial int DialHours { get; set; }

    [ObservableProperty]
    public partial DialKind DialKind { get; set; }

    [ObservableProperty]
    public partial bool DialActive { get; set; }

    [ObservableProperty]
    public partial double DialTargetMinutes { get; set; }

    [ObservableProperty]
    public partial int MoonCount { get; set; }

    [ObservableProperty]
    public partial int MoonsCompleted { get; set; }

    [ObservableProperty]
    public partial bool MoonCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsTimerRunning { get; set; }

    [ObservableProperty]
    public partial bool ShowHoursChip { get; set; }

    [ObservableProperty]
    public partial string HoursChipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BigTimeText { get; set; } = "00:00";

    [ObservableProperty]
    public partial string OverlineText { get; set; } = "KRONOMETRE";

    [ObservableProperty]
    public partial string DisplayTaskName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TagPillText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTagPill { get; set; }

    [ObservableProperty]
    public partial string CaptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PrimaryTip { get; set; } = "Başlat";

    [ObservableProperty]
    public partial bool ShowReset { get; set; }

    [ObservableProperty]
    public partial string ClockText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DateShortText { get; set; } = string.Empty;

    /// <summary>Total focused time today (saved sessions plus the running one).</summary>
    [ObservableProperty]
    public partial TimeSpan TotalToday { get; set; }

    [ObservableProperty]
    public partial string TotalFocusText { get; set; } = "0";

    [ObservableProperty]
    public partial string TotalFocusUnit { get; set; } = "dk";

    [ObservableProperty]
    public partial IReadOnlyList<DaySegment> RibbonSegments { get; set; } = [];

    [ObservableProperty]
    public partial double RibbonNowMinute { get; set; } = -1;

    public ObservableCollection<RecentSessionItem> RecentSessions { get; } = [];

    public bool HasRecentSessions => RecentSessions.Count > 0;

    partial void OnTotalTodayChanged(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            TotalFocusText = $"{(int)value.TotalHours}:{value.Minutes:00}";
            TotalFocusUnit = "saat";
        }
        else
        {
            TotalFocusText = ((int)value.TotalMinutes).ToString(TrCulture);
            TotalFocusUnit = "dk";
        }
    }

    private bool CanPrimary() => _timer.State switch
    {
        TimerState.Idle => StartCommand.CanExecute(null),
        TimerState.Running => PauseCommand.CanExecute(null),
        _ => ResumeCommand.CanExecute(null),
    };

    /// <summary>The single hero button: start when idle, pause when running, resume when paused.</summary>
    [RelayCommand(CanExecute = nameof(CanPrimary))]
    private void Primary()
    {
        switch (_timer.State)
        {
            case TimerState.Idle:
                StartCommand.Execute(null);
                break;
            case TimerState.Running:
                PauseCommand.Execute(null);
                break;
            default:
                ResumeCommand.Execute(null);
                break;
        }
    }

    private void InitTimerView()
    {
        StartCommand.CanExecuteChanged += (_, _) => PrimaryCommand.NotifyCanExecuteChanged();
        PauseCommand.CanExecuteChanged += (_, _) => PrimaryCommand.NotifyCanExecuteChanged();
        ResumeCommand.CanExecuteChanged += (_, _) => PrimaryCommand.NotifyCanExecuteChanged();
        SessionsChanged += (_, _) => ReloadDayPanel();
        UpdateClock();
    }

    private DateTimeOffset LocalNow() => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local);

    private void UpdateClock()
    {
        var now = LocalNow();
        ClockText = $"{now:HH:mm} · {now.ToString("ddd", TrCulture)}";
        UpdateRibbon();
    }

    private void ReloadDayPanel()
    {
        var tz = TimeZoneInfo.Local;
        IReadOnlyList<TimeSession> sessions;
        try
        {
            sessions = _repository.GetByDay(_loadedDay, tz);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load day panel: {ex}");
            sessions = [];
        }

        DateShortText = _loadedDay.ToDateTime(TimeOnly.MinValue).ToString("d MMMM, dddd", TrCulture);
        _finishedSegments = sessions
            .Select(s => new DaySegment(MinuteOfDay(s.StartedAt, tz), MinuteOfDay(s.EndedAt, tz)))
            .ToList();
        RecentSessions.Clear();
        foreach (var s in sessions.OrderByDescending(s => s.StartedAt).Take(4))
        {
            var tag = string.IsNullOrWhiteSpace(s.Tag) ? null : "#" + s.Tag.TrimStart('#');
            RecentSessions.Add(new RecentSessionItem(s.TaskName, tag, $"{(int)s.Duration.TotalHours}:{s.Duration.Minutes:00}"));
        }

        OnPropertyChanged(nameof(HasRecentSessions));
        _ribbonMinute = -1;
        UpdateRibbon();
    }

    private static double MinuteOfDay(DateTimeOffset value, TimeZoneInfo tz) =>
        Math.Clamp(TimeZoneInfo.ConvertTime(value, tz).TimeOfDay.TotalMinutes, 0, 1440);

    /// <summary>Rebuilds the day ribbon at most once per minute (plus on session changes).</summary>
    private void UpdateRibbon()
    {
        var now = LocalNow();
        var minute = now.Hour * 60 + now.Minute;
        if (minute == _ribbonMinute)
            return;
        _ribbonMinute = minute;

        var segments = new List<DaySegment>(_finishedSegments);
        if (_timer.State != TimerState.Idle && !_runningBreak && _timer.StartedAt is { } started)
            segments.Add(new DaySegment(MinuteOfDay(started, TimeZoneInfo.Local), minute));
        RibbonSegments = segments;
        RibbonNowMinute = now.TimeOfDay.TotalMinutes;
    }

    private TimeSpan IdleDisplay() => Mode switch
    {
        TimerMode.Countdown => CountdownTarget(),
        TimerMode.Pomodoro => _cycle.CurrentDuration,
        _ => TimeSpan.Zero,
    };

    private void UpdateDial()
    {
        var state = _timer.State;
        var idle = state == TimerState.Idle;
        var target = _timer.Target;

        DialKind = Mode switch
        {
            TimerMode.Countdown => DialKind.Countdown,
            TimerMode.Pomodoro => DialKind.Pomodoro,
            _ => DialKind.Stopwatch,
        };
        DialActive = !idle;
        IsTimerRunning = state == TimerState.Running;

        BigTimeText = FormatClock(idle ? IdleDisplay() : _timer.Remaining ?? _timer.Elapsed);

        var hours = 0;
        var progress = 0.0;
        if (!idle)
        {
            var elapsed = _timer.Elapsed;
            if (target is { } t && t > TimeSpan.Zero)
                progress = Math.Clamp(elapsed / t, 0, 1);
            else
            {
                progress = elapsed.TotalSeconds % 3600 / 3600;
                hours = (int)elapsed.TotalHours;
            }
        }

        DialProgress = progress;
        DialHours = hours;
        HoursChipText = $"{hours} sa";
        ShowHoursChip = !idle && target is null && hours > 0;
        DialTargetMinutes = (idle ? IdleDisplay() : target ?? TimeSpan.Zero).TotalMinutes;

        var every = _cycle.Settings.LongBreakEvery;
        var pomodoro = Mode == TimerMode.Pomodoro;
        MoonCount = pomodoro ? every : 0;
        MoonsCompleted = _cycle.CurrentPhase == PomodoroPhase.LongBreak ? every : _cycle.CompletedWorkCount % every;
        MoonCurrent = _cycle.CurrentPhase == PomodoroPhase.Work;

        var overline = Mode switch
        {
            TimerMode.Countdown => "GERİ SAYIM",
            TimerMode.Pomodoro => _cycle.CurrentPhase switch
            {
                PomodoroPhase.Work => $"ÇALIŞMA · {_cycle.CompletedWorkCount % every + 1}/{every}",
                PomodoroPhase.ShortBreak => "KISA MOLA",
                _ => "UZUN MOLA",
            },
            _ => "KRONOMETRE",
        };
        OverlineText = state == TimerState.Paused ? overline + " · DURAKLATILDI" : overline;

        DisplayTaskName = IsBreakPhase ? "Mola" : TaskName;
        TagPillText = IsBreakPhase || string.IsNullOrWhiteSpace(Tag) ? string.Empty : "#" + Tag.Trim().TrimStart('#');
        HasTagPill = TagPillText.Length > 0;

        PrimaryTip = state switch
        {
            TimerState.Running => "Duraklat",
            TimerState.Paused => "Devam",
            _ => "Başlat",
        };
        ShowReset = pomodoro && idle;
        CaptionText = BuildCaption(state, pomodoro);
        UpdateRibbon();
        PrimaryCommand.NotifyCanExecuteChanged();
    }

    private string BuildCaption(TimerState state, bool pomodoro)
    {
        string text;
        if (state == TimerState.Idle)
            text = IsBreakPhase ? "Molaya başla" : "Görev adını yaz ve başlat";
        else if (state == TimerState.Paused)
            text = "Duraklatıldı";
        else if (_timer.Remaining is { } remaining)
        {
            text = $"Bitiş {LocalNow() + remaining:HH:mm}";
            if (pomodoro)
            {
                var settings = _cycle.Settings;
                text += _cycle.CurrentPhase != PomodoroPhase.Work
                    ? $" · Sonraki: Çalışma {(int)settings.Work.TotalMinutes} dk"
                    : (_cycle.CompletedWorkCount + 1) % settings.LongBreakEvery == 0
                        ? $" · Sonraki: Uzun mola {(int)settings.LongBreak.TotalMinutes} dk"
                        : $" · Sonraki: Kısa mola {(int)settings.ShortBreak.TotalMinutes} dk";
            }
        }
        else
            text = _timer.StartedAt is { } started
                ? $"Başlangıç {TimeZoneInfo.ConvertTime(started, TimeZoneInfo.Local):HH:mm}"
                : string.Empty;

        return text.ToUpper(TrCulture);
    }

    /// <summary>MM:SS under one hour, H:MM:SS from one hour.</summary>
    internal static string FormatClock(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{value.Minutes:00}:{value.Seconds:00}";
}
