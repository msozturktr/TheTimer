using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TheTimer.Core;
using TheTimer.Core.Planning;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

/// <summary>One arc on the 24-hour dome; times are minutes since local midnight (0..1440).</summary>
public readonly record struct DomeSegment(double StartMinute, double EndMinute, bool IsLive = false);

/// <summary>Everything the dome draws: saved + live sessions, planned blocks and the now marker.</summary>
public sealed record TodayArcData(
    IReadOnlyList<DomeSegment> Sessions, IReadOnlyList<DomeSegment> Planned, double NowMinute, string NowText)
{
    public static TodayArcData Empty { get; } = new([], [], 0, "00:00");
}

/// <summary>
/// Feeds the Bugün dome. Refreshed when sessions change, once a second only while a session runs,
/// and on a slow clock so the now marker follows the minute hand.
/// </summary>
public sealed class TodayArcViewModel : ObservableObject
{
    private readonly ISessionRepository _repository;
    private readonly PlanService _planService;
    private readonly SessionTimer _timer;
    private readonly TimeProvider _timeProvider;
    private readonly Func<bool> _isBreak;
    private readonly DispatcherTimer _liveTimer;
    private readonly DispatcherTimer _clockTimer;
    private long _shownMinute = -1;
    private TodayArcData _data = TodayArcData.Empty;

    public TodayArcViewModel(
        ISessionRepository repository, PlanService planService, SessionTimer timer, TimeProvider timeProvider, Func<bool>? isBreak = null)
    {
        _repository = repository;
        _planService = planService;
        _timer = timer;
        _timeProvider = timeProvider;
        _isBreak = isBreak ?? (() => false);

        _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveTimer.Tick += (_, _) => Refresh();
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += (_, _) =>
        {
            if (CurrentMinute() != _shownMinute)
                Refresh();
        };
        _clockTimer.Start();
        _timer.StateChanged += (_, _) =>
        {
            if (_timer.State == TimerState.Running)
                _liveTimer.Start();
            else
                _liveTimer.Stop();
            Refresh();
        };
        Refresh();
    }

    public TodayArcData Data
    {
        get => _data;
        private set => SetProperty(ref _data, value);
    }

    private long CurrentMinute() => _timeProvider.GetUtcNow().ToUnixTimeSeconds() / 60;

    public void Refresh()
    {
        var tz = TimeZoneInfo.Local;
        var now = _timeProvider.GetUtcNow();
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, tz).DateTime);
        var dayStart = ZoneTime.ToInstant(day, TimeOnly.MinValue, tz);

        double Minutes(DateTimeOffset instant) => Math.Clamp((instant - dayStart).TotalMinutes, 0, 1440);

        var sessions = new List<DomeSegment>();
        var planned = new List<DomeSegment>();
        try
        {
            foreach (var s in _repository.GetByDay(day, tz))
                sessions.Add(new DomeSegment(Minutes(s.StartedAt), Minutes(s.EndedAt)));
            foreach (var o in _planService.GetOccurrences(day, tz))
                planned.Add(new DomeSegment(Minutes(o.StartsAt), Minutes(o.EndsAt)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Failed to load dome data: {ex}");
        }

        if (_timer.State != TimerState.Idle && !_isBreak() && _timer.StartedAt is { } started)
        {
            var end = _timer.State == TimerState.Running ? now : started + _timer.Elapsed;
            sessions.Add(new DomeSegment(Minutes(started), Minutes(end), IsLive: true));
        }

        var local = TimeZoneInfo.ConvertTime(now, tz);
        _shownMinute = CurrentMinute();
        Data = new TodayArcData(sessions, planned, Minutes(now), $"{local:HH:mm}");
    }
}
