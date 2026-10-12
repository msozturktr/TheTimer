using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SessionTimer _timer;
    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly DispatcherTimer _ticker;

    public MainWindowViewModel(SessionTimer timer, ISessionRepository repository, TimeProvider timeProvider)
    {
        _timer = timer;
        _repository = repository;
        _timeProvider = timeProvider;

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += (_, _) => UpdateElapsed();
        _timer.StateChanged += (_, _) => OnTimerStateChanged();

        RefreshTodaySessions();
        UpdateElapsed();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial string TaskName { get; set; } = string.Empty;

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

    public ObservableCollection<SessionItemViewModel> TodaySessions { get; } = [];

    internal static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    private bool CanStart() => _timer.State == TimerState.Idle && !string.IsNullOrWhiteSpace(TaskName);

    private bool CanPause() => _timer.State == TimerState.Running;

    private bool CanResume() => _timer.State == TimerState.Paused;

    private bool CanStop() => _timer.State != TimerState.Idle;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start() => _timer.Start(TaskName, Tag);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause() => _timer.Pause();

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => _timer.Resume();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _repository.Add(_timer.Stop());
        RefreshTodaySessions();
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
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void UpdateElapsed() => ElapsedText = FormatDuration(_timer.Elapsed);

    private void RefreshTodaySessions()
    {
        var tz = TimeZoneInfo.Local;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), tz).DateTime);
        TodaySessions.Clear();
        foreach (var session in _repository.GetByDay(today, tz))
            TodaySessions.Add(new SessionItemViewModel(session, tz));
    }
}
