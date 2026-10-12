using TheTimer.Core.Pomodoro;

namespace TheTimer.Core.Sessions;

/// <summary>
/// An active session persisted for crash recovery. <see cref="LastSeen"/> is the last heartbeat;
/// <see cref="Mode"/> is an app-defined label (e.g. "Stopwatch", "Countdown", "Pomodoro").
/// </summary>
public sealed record ActiveSessionRecord(
    TimerSnapshot Timer,
    DateTimeOffset LastSeen,
    string Mode,
    bool IsBreak,
    PomodoroPhase PomodoroPhase,
    int PomodoroCompletedWorkCount);
