using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using TheTimer.Core.Notifications;
using TheTimer.Core.Pomodoro;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public enum TimerMode
{
    Stopwatch,
    Countdown,
    Pomodoro,
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SessionTimer _timer;
    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;
    private readonly PomodoroCycle _cycle = new();
    private readonly DispatcherTimer _ticker;
    private readonly DispatcherTimer _dayWatcher;
    private static readonly CultureInfo TrCulture = new("tr-TR");
    private bool _runningBreak;
    private DateOnly _loadedDay;

    public MainWindowViewModel(SessionTimer timer, ISessionRepository repository, TimeProvider timeProvider, INotifier notifier)
    {
        _timer = timer;
        _repository = repository;
        _timeProvider = timeProvider;
        _notifier = notifier;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += (_, _) => OnTick();
        _timer.StateChanged += (_, _) => OnTimerStateChanged();

        _dayWatcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _dayWatcher.Tick += (_, _) =>
        {
            if (CurrentDay() != _loadedDay)
                RefreshTodaySessions();
        };
        _dayWatcher.Start();

        RefreshTodaySessions();
        UpdatePhaseText();
        UpdateElapsed();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string TaskName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TimerMode Mode { get; set; } = TimerMode.Stopwatch;

    [ObservableProperty]
    public partial decimal? CountdownMinutes { get; set; } = 25;

    [ObservableProperty]
    public partial string PhaseText { get; set; } = string.Empty;

    public bool IsStopwatchMode
    {
        get => Mode == TimerMode.Stopwatch;
        set { if (value) Mode = TimerMode.Stopwatch; }
    }

    public bool IsCountdownMode
    {
        get => Mode == TimerMode.Countdown;
        set { if (value) Mode = TimerMode.Countdown; }
    }

    public bool IsPomodoroMode
    {
        get => Mode == TimerMode.Pomodoro;
        set { if (value) Mode = TimerMode.Pomodoro; }
    }

    [ObservableProperty]
    public partial string? Tag { get; set; }

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = FormatDuration(TimeSpan.Zero);

    [ObservableProperty]
    public partial string StateText { get; set; } = "Hazır";

    [ObservableProperty]
    public partial bool IsEditable { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial string DateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = FormatDuration(TimeSpan.Zero);

    [ObservableProperty]
    public partial bool HasSessions { get; set; }

    public ObservableCollection<SessionItemViewModel> TodaySessions { get; } = [];

    public ObservableCollection<TaskTotalItemViewModel> TaskTotals { get; } = [];

    internal static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    private bool IsBreakPhase => Mode == TimerMode.Pomodoro && _cycle.CurrentPhase != PomodoroPhase.Work;

    private bool CanStart() =>
        _timer.State == TimerState.Idle && (IsBreakPhase || !string.IsNullOrWhiteSpace(TaskName));

    private bool CanPause() => _timer.State == TimerState.Running;

    private bool CanResume() => _timer.State == TimerState.Paused;

    private bool CanStop() => _timer.State != TimerState.Idle;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        switch (Mode)
        {
            case TimerMode.Countdown:
                _runningBreak = false;
                _timer.Start(TaskName, Tag, CountdownTarget());
                break;
            case TimerMode.Pomodoro when IsBreakPhase:
                _runningBreak = true;
                _timer.Start("Mola", null, _cycle.CurrentDuration);
                break;
            case TimerMode.Pomodoro:
                _runningBreak = false;
                _timer.Start(TaskName, Tag, _cycle.CurrentDuration);
                break;
            default:
                _runningBreak = false;
                _timer.Start(TaskName, Tag);
                break;
        }
    }

    private bool CanReset() => _timer.State == TimerState.Idle && Mode == TimerMode.Pomodoro;

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void ResetPomodoro()
    {
        _cycle.Reset();
        UpdatePhaseText();
        UpdateElapsed();
        StartCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause() => _timer.Pause();

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => _timer.Resume();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        var session = _timer.Stop();
        if (!_runningBreak)
        {
            _repository.Add(session);
            RefreshTodaySessions();
        }
    }

    partial void OnModeChanged(TimerMode value)
    {
        OnPropertyChanged(nameof(IsStopwatchMode));
        OnPropertyChanged(nameof(IsCountdownMode));
        OnPropertyChanged(nameof(IsPomodoroMode));
        UpdatePhaseText();
        UpdateElapsed();
        StartCommand.NotifyCanExecuteChanged();
        ResetPomodoroCommand.NotifyCanExecuteChanged();
    }

    partial void OnCountdownMinutesChanged(decimal? value) => UpdateElapsed();

    private TimeSpan CountdownTarget() =>
        TimeSpan.FromMinutes((double)Math.Clamp(CountdownMinutes ?? 25, 1, 600));

    private void OnTick()
    {
        if (_timer.CheckTarget() is { } session)
            HandleCompletion(session);
        UpdateElapsed();
        UpdateTotal();
    }

    private void HandleCompletion(TimeSession session)
    {
        if (Mode == TimerMode.Pomodoro)
        {
            var wasBreak = _runningBreak;
            if (!wasBreak)
            {
                _repository.Add(session);
                RefreshTodaySessions();
            }

            _cycle.Advance();
            UpdatePhaseText();
            if (wasBreak)
                Notify("Mola bitti", "Çalışmaya dönme zamanı");
            else
                Notify("Çalışma bitti", $"Mola zamanı: {(int)_cycle.CurrentDuration.TotalMinutes} dk");
        }
        else
        {
            _repository.Add(session);
            RefreshTodaySessions();
            Notify("Süre doldu", $"{session.TaskName} tamamlandı");
        }

        StartCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePhaseText() => PhaseText = Mode != TimerMode.Pomodoro ? string.Empty : _cycle.CurrentPhase switch
    {
        PomodoroPhase.Work =>
            $"Çalışma ({_cycle.CompletedWorkCount % _cycle.Settings.LongBreakEvery + 1}/{_cycle.Settings.LongBreakEvery})",
        PomodoroPhase.ShortBreak => "Kısa mola",
        _ => "Uzun mola",
    };

    private void Notify(string title, string body) => _ = NotifySafeAsync(title, body);

    private async Task NotifySafeAsync(string title, string body)
    {
        try
        {
            await _notifier.NotifyAsync(title, body).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Notification failed: {ex}");
        }
    }

    private void OnTimerStateChanged()
    {
        StateText = _timer.State switch
        {
            TimerState.Running => "Çalışıyor",
            TimerState.Paused => "Duraklatıldı",
            _ => "Hazır",
        };
        IsEditable = _timer.State == TimerState.Idle;
        IsPaused = _timer.State == TimerState.Paused;

        if (_timer.State == TimerState.Running)
            _ticker.Start();
        else
            _ticker.Stop();

        UpdateElapsed();
        UpdateTotal();
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        ResetPomodoroCommand.NotifyCanExecuteChanged();
    }

    private void UpdateElapsed()
    {
        if (_timer.State != TimerState.Idle)
            ElapsedText = FormatDuration(_timer.Remaining ?? _timer.Elapsed);
        else
            ElapsedText = FormatDuration(Mode switch
            {
                TimerMode.Countdown => CountdownTarget(),
                TimerMode.Pomodoro => _cycle.CurrentDuration,
                _ => TimeSpan.Zero,
            });
    }

    private DateOnly CurrentDay() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime);

    [RelayCommand]
    private void DeleteSession(Guid id)
    {
        _repository.Delete(id);
        RefreshTodaySessions();
    }

    private void RefreshTodaySessions()
    {
        var tz = TimeZoneInfo.Local;
        _loadedDay = CurrentDay();
        var sessions = _repository.GetByDay(_loadedDay, tz);

        DateText = _loadedDay.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy dddd", TrCulture);
        TodaySessions.Clear();
        foreach (var session in sessions)
            TodaySessions.Add(new SessionItemViewModel(session, tz, DeleteSessionCommand));
        TaskTotals.Clear();
        foreach (var total in SessionSummary.ByTask(sessions))
            TaskTotals.Add(new TaskTotalItemViewModel(total));
        HasSessions = sessions.Count > 0;
        _savedTotal = SessionSummary.Total(sessions);
        UpdateTotal();
    }

    private TimeSpan _savedTotal;

    private void UpdateTotal()
    {
        var total = _savedTotal;
        if (_timer.State != TimerState.Idle && !_runningBreak && _timer.StartedAt is { } startedAt
            && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(startedAt, TimeZoneInfo.Local).DateTime) == _loadedDay)
            total += _timer.Elapsed;
        TotalText = $"Toplam: {FormatDuration(total)}";
    }
}
