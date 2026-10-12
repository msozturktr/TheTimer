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
    private readonly IActiveSessionStore _activeStore;
    private readonly TimeProvider _timeProvider;
    private readonly INotifier _notifier;
    private readonly PomodoroCycle _cycle = new();
    private readonly DispatcherTimer _ticker;
    private readonly DispatcherTimer _dayWatcher;
    private static readonly CultureInfo TrCulture = new("tr-TR");
    private bool _runningBreak;
    private bool _backgroundNoticeShown;
    private bool _storeErrorNotified;
    private ActiveSessionRecord? _pendingRecord;
    private DateOnly _loadedDay;

    public MainWindowViewModel(SessionTimer timer, ISessionRepository repository, IActiveSessionStore activeStore, TimeProvider timeProvider, INotifier notifier)
    {
        _timer = timer;
        _repository = repository;
        _activeStore = activeStore;
        _timeProvider = timeProvider;
        _notifier = notifier;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += (_, _) => OnTick();
        _timer.StateChanged += (_, _) => OnTimerStateChanged();

        _dayWatcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _dayWatcher.Tick += (_, _) =>
        {
            TouchActiveSession();
            if (CurrentDay() != _loadedDay)
                RefreshTodaySessions();
        };
        _dayWatcher.Start();

        RefreshTodaySessions();
        UpdatePhaseText();
        UpdateElapsed();
        LoadPendingRecovery();
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

    [ObservableProperty]
    public partial string TrayToolTip { get; set; } = "TheTimer";

    [ObservableProperty]
    public partial bool HasPendingRecovery { get; set; }

    [ObservableProperty]
    public partial bool CanResumePending { get; set; }

    [ObservableProperty]
    public partial string RecoveryText { get; set; } = string.Empty;

    public ObservableCollection<SessionItemViewModel> TodaySessions { get; } = [];

    public ObservableCollection<TaskTotalItemViewModel> TaskTotals { get; } = [];

    internal static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    private bool IsBreakPhase => Mode == TimerMode.Pomodoro && _cycle.CurrentPhase != PomodoroPhase.Work;

    private bool CanStart() =>
        !HasPendingRecovery && _timer.State == TimerState.Idle && (IsBreakPhase || !string.IsNullOrWhiteSpace(TaskName));

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
            SaveSession(session);
            RefreshTodaySessions();
        }
    }

    /// <summary>True when closing the window should keep a running/paused session alive in the tray.</summary>
    public bool ShouldHideOnClose()
    {
        if (_timer.State == TimerState.Idle)
            return false;

        if (!_backgroundNoticeShown)
        {
            _backgroundNoticeShown = true;
            Notify("TheTimer arka planda", "Zamanlayıcı tepside çalışmaya devam ediyor");
        }

        return true;
    }

    /// <summary>Refreshes the crash-recovery heartbeat; call on shutdown paths where the session stays active.</summary>
    public void TouchActiveSession()
    {
        if (_timer.State == TimerState.Idle)
            return;

        try
        {
            _activeStore.Touch(_timeProvider.GetUtcNow());
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to touch active session: {ex}");
        }
    }

    private void LoadPendingRecovery()
    {
        try
        {
            _pendingRecord = _activeStore.Load();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load active session: {ex}");
            _pendingRecord = null;
        }

        if (_pendingRecord is not { } record)
            return;

        var lastSeen = TimeZoneInfo.ConvertTime(record.LastSeen, TimeZoneInfo.Local);
        var task = record.IsBreak ? "Mola" : record.Timer.TaskName;
        RecoveryText = $"Yarım kalan oturum: {task} · {FormatDuration(SessionRecovery.RecoveredElapsed(record))} " +
                       $"({lastSeen:HH:mm})";
        CanResumePending = SessionRecovery.CanResume(record);
        HasPendingRecovery = true;
        StartCommand.NotifyCanExecuteChanged();
    }

    private void ClosePendingRecovery()
    {
        _pendingRecord = null;
        HasPendingRecovery = false;
        StartCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ResumePending()
    {
        if (_pendingRecord is not { } record || !SessionRecovery.CanResume(record))
            return;

        var snapshot = SessionRecovery.ToResumeSnapshot(record, _timeProvider.GetUtcNow());
        // Close first so the Start gate no longer matters; the restore itself re-saves the record.
        ClosePendingRecovery();

        Mode = Enum.TryParse<TimerMode>(record.Mode, out var mode) ? mode : TimerMode.Stopwatch;
        _cycle.Restore(record.PomodoroPhase, record.PomodoroCompletedWorkCount);
        _runningBreak = record.IsBreak;
        if (!record.IsBreak)
        {
            TaskName = snapshot.TaskName;
            Tag = snapshot.Tag;
        }

        UpdatePhaseText();
        _timer.Restore(snapshot);
    }

    [RelayCommand]
    private void SavePending()
    {
        if (_pendingRecord is not { } record)
            return;

        if (!record.IsBreak)
        {
            // Keep the recovery record if saving fails, so the session can be saved on a later run.
            if (!SaveSession(SessionRecovery.ToFinishedSession(record)))
                return;
            RefreshTodaySessions();
        }

        ClearActiveStore();
        ClosePendingRecovery();
    }

    [RelayCommand]
    private void DiscardPending()
    {
        ClearActiveStore();
        ClosePendingRecovery();
    }

    private void ClearActiveStore()
    {
        try
        {
            _activeStore.Clear();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to clear active session: {ex}");
        }
    }

    private void PersistActiveSession()
    {
        try
        {
            if (_timer.CreateSnapshot() is { } snapshot)
                _activeStore.Save(new ActiveSessionRecord(snapshot, _timeProvider.GetUtcNow(), Mode.ToString(),
                    _runningBreak, _cycle.CurrentPhase, _cycle.CompletedWorkCount));
            else
                _activeStore.Clear();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to persist active session: {ex}");
            if (!_storeErrorNotified)
            {
                _storeErrorNotified = true;
                Notify("Oturum yedeklenemedi", "Çökme durumunda oturum kurtarılamayabilir");
            }
        }
    }

    /// <summary>Stops the active session (if any) through the regular Stop path.</summary>
    public void StopActiveSession()
    {
        if (StopCommand.CanExecute(null))
            StopCommand.Execute(null);
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
                SaveSession(session);
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
            SaveSession(session);
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

    private bool SaveSession(TimeSession session)
    {
        try
        {
            _repository.Add(session);
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save session: {ex}");
            Notify("Oturum kaydedilemedi", $"{session.TaskName} · {FormatDuration(session.Duration)}");
            return false;
        }
    }

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
        PersistActiveSession();

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

        UpdateTrayToolTip();
    }

    private void UpdateTrayToolTip()
    {
        if (_timer.State == TimerState.Idle)
        {
            TrayToolTip = "TheTimer";
            return;
        }

        var task = _runningBreak ? "Mola" : TaskName;
        TrayToolTip = $"TheTimer — {StateText} {ElapsedText} · {task}";
    }

    private DateOnly CurrentDay() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime);

    [RelayCommand]
    private void DeleteSession(Guid id)
    {
        try
        {
            _repository.Delete(id);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to delete session {id}: {ex}");
            Notify("Oturum silinemedi", "Veritabanı hatası");
        }
        RefreshTodaySessions();
    }

    private void RefreshTodaySessions()
    {
        var tz = TimeZoneInfo.Local;
        _loadedDay = CurrentDay();
        IReadOnlyList<TimeSession> sessions;
        try
        {
            sessions = _repository.GetByDay(_loadedDay, tz);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load sessions: {ex}");
            sessions = Array.Empty<TimeSession>();
        }

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
