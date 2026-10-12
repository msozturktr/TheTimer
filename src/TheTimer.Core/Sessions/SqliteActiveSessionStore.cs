using TheTimer.Core.Pomodoro;
using TheTimer.Core.Storage;

namespace TheTimer.Core.Sessions;

/// <summary>SQLite-backed single-row active session store (table <c>active_session</c>).</summary>
public sealed class SqliteActiveSessionStore : IActiveSessionStore
{
    private readonly SqliteDatabase _db;

    public SqliteActiveSessionStore(SqliteDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public void Save(ActiveSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var timer = record.Timer;
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO active_session (id, task_name, tag, started_at_utc_ticks, accumulated_ticks, " +
            "segment_start_utc_ticks, target_ticks, last_seen_utc_ticks, mode, is_break, pomodoro_phase, pomodoro_completed) " +
            "VALUES (1, $task, $tag, $started, $accumulated, $segment, $target, $lastSeen, $mode, $isBreak, $phase, $completed) " +
            "ON CONFLICT(id) DO UPDATE SET task_name = excluded.task_name, tag = excluded.tag, " +
            "started_at_utc_ticks = excluded.started_at_utc_ticks, accumulated_ticks = excluded.accumulated_ticks, " +
            "segment_start_utc_ticks = excluded.segment_start_utc_ticks, target_ticks = excluded.target_ticks, " +
            "last_seen_utc_ticks = excluded.last_seen_utc_ticks, mode = excluded.mode, is_break = excluded.is_break, " +
            "pomodoro_phase = excluded.pomodoro_phase, pomodoro_completed = excluded.pomodoro_completed";
        command.Parameters.AddWithValue("$task", timer.TaskName);
        command.Parameters.AddWithValue("$tag", (object?)timer.Tag ?? DBNull.Value);
        command.Parameters.AddWithValue("$started", timer.StartedAt.UtcTicks);
        command.Parameters.AddWithValue("$accumulated", timer.Accumulated.Ticks);
        command.Parameters.AddWithValue("$segment", timer.SegmentStart is { } s ? s.UtcTicks : DBNull.Value);
        command.Parameters.AddWithValue("$target", timer.Target is { } t ? t.Ticks : DBNull.Value);
        command.Parameters.AddWithValue("$lastSeen", record.LastSeen.UtcTicks);
        command.Parameters.AddWithValue("$mode", record.Mode);
        command.Parameters.AddWithValue("$isBreak", record.IsBreak ? 1 : 0);
        command.Parameters.AddWithValue("$phase", (int)record.PomodoroPhase);
        command.Parameters.AddWithValue("$completed", record.PomodoroCompletedWorkCount);
        command.ExecuteNonQuery();
    }

    public ActiveSessionRecord? Load()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT task_name, tag, started_at_utc_ticks, accumulated_ticks, segment_start_utc_ticks, target_ticks, " +
            "last_seen_utc_ticks, mode, is_break, pomodoro_phase, pomodoro_completed FROM active_session WHERE id = 1";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        var timer = new TimerSnapshot(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero),
            TimeSpan.FromTicks(reader.GetInt64(3)),
            reader.IsDBNull(4) ? null : new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero),
            reader.IsDBNull(5) ? null : TimeSpan.FromTicks(reader.GetInt64(5)));
        return new ActiveSessionRecord(
            timer,
            new DateTimeOffset(reader.GetInt64(6), TimeSpan.Zero),
            reader.GetString(7),
            reader.GetInt64(8) != 0,
            (PomodoroPhase)reader.GetInt32(9),
            reader.GetInt32(10));
    }

    public void Clear()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM active_session";
        command.ExecuteNonQuery();
    }

    public void Touch(DateTimeOffset lastSeen)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE active_session SET last_seen_utc_ticks = $lastSeen WHERE id = 1";
        command.Parameters.AddWithValue("$lastSeen", lastSeen.UtcTicks);
        command.ExecuteNonQuery();
    }
}
