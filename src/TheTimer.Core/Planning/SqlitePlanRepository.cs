using Microsoft.Data.Sqlite;
using TheTimer.Core.Storage;

namespace TheTimer.Core.Planning;

/// <summary>SQLite plan store. Days are stored as <see cref="DateOnly.DayNumber"/>, times as minutes since midnight.</summary>
public sealed class SqlitePlanRepository : IPlanRepository
{
    private const string Columns =
        "id, task_name, tag, start_minute, end_minute, date_day, rec_start_day, rec_end_day, rec_days";

    private readonly SqliteDatabase _db;

    public SqlitePlanRepository(SqliteDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public void Add(PlanBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO plan_blocks ({Columns}) " +
            "VALUES ($id, $task, $tag, $start, $end, $date, $recStart, $recEnd, $recDays)";
        Bind(command, block);
        command.ExecuteNonQuery();
    }

    public void Update(PlanBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE plan_blocks SET task_name = $task, tag = $tag, start_minute = $start, end_minute = $end, " +
            "date_day = $date, rec_start_day = $recStart, rec_end_day = $recEnd, rec_days = $recDays WHERE id = $id";
        Bind(command, block);
        if (command.ExecuteNonQuery() == 0)
            throw new InvalidOperationException($"Unknown plan block {block.Id}.");
    }

    public bool Delete(Guid id)
    {
        using var connection = _db.Open();
        using var transaction = connection.BeginTransaction();
        // Explicit delete so exceptions go even if foreign keys were not enforced on this connection.
        using (var exceptions = connection.CreateCommand())
        {
            exceptions.Transaction = transaction;
            exceptions.CommandText = "DELETE FROM plan_block_exceptions WHERE block_id = $id";
            exceptions.Parameters.AddWithValue("$id", id.ToString());
            exceptions.ExecuteNonQuery();
        }

        bool removed;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM plan_blocks WHERE id = $id";
            command.Parameters.AddWithValue("$id", id.ToString());
            removed = command.ExecuteNonQuery() > 0;
        }

        transaction.Commit();
        return removed;
    }

    public PlanBlock? Get(Guid id)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM plan_blocks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<PlanBlock> GetBlocksForRange(DateOnly from, DateOnly toInclusive)
    {
        var result = new List<PlanBlock>();
        if (toInclusive < from)
            return result;

        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {Columns} FROM plan_blocks WHERE " +
            "(date_day IS NOT NULL AND date_day >= $from AND date_day <= $to) OR " +
            "(rec_start_day IS NOT NULL AND rec_start_day <= $to AND (rec_end_day IS NULL OR rec_end_day >= $from))";
        command.Parameters.AddWithValue("$from", from.DayNumber);
        command.Parameters.AddWithValue("$to", toInclusive.DayNumber);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(Read(reader));
        return result;
    }

    public void AddException(Guid blockId, DateOnly day)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO plan_block_exceptions (block_id, day) VALUES ($id, $day)";
        command.Parameters.AddWithValue("$id", blockId.ToString());
        command.Parameters.AddWithValue("$day", day.DayNumber);
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException($"Unknown plan block {blockId}.", ex);
        }
    }

    public IReadOnlySet<(Guid BlockId, DateOnly Day)> GetExceptions(DateOnly from, DateOnly toInclusive)
    {
        var result = new HashSet<(Guid, DateOnly)>();
        if (toInclusive < from)
            return result;

        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT block_id, day FROM plan_block_exceptions WHERE day >= $from AND day <= $to";
        command.Parameters.AddWithValue("$from", from.DayNumber);
        command.Parameters.AddWithValue("$to", toInclusive.DayNumber);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add((Guid.Parse(reader.GetString(0)), DateOnly.FromDayNumber((int)reader.GetInt64(1))));
        return result;
    }

    private static void Bind(SqliteCommand command, PlanBlock block)
    {
        var rec = block.Recurrence;
        command.Parameters.AddWithValue("$id", block.Id.ToString());
        command.Parameters.AddWithValue("$task", block.TaskName);
        command.Parameters.AddWithValue("$tag", (object?)block.Tag ?? DBNull.Value);
        command.Parameters.AddWithValue("$start", (int)(block.Start.Ticks / TimeSpan.TicksPerMinute));
        command.Parameters.AddWithValue("$end", (int)(block.End.Ticks / TimeSpan.TicksPerMinute));
        command.Parameters.AddWithValue("$date", block.Date is { } d ? d.DayNumber : DBNull.Value);
        command.Parameters.AddWithValue("$recStart", rec is null ? DBNull.Value : rec.StartDate.DayNumber);
        command.Parameters.AddWithValue("$recEnd", rec?.EndDate is { } e ? e.DayNumber : DBNull.Value);
        command.Parameters.AddWithValue("$recDays", rec is null ? DBNull.Value : (int)rec.Days);
    }

    private static PlanBlock Read(SqliteDataReader reader)
    {
        PlanRecurrence? recurrence = reader.IsDBNull(6)
            ? null
            : new PlanRecurrence(
                DateOnly.FromDayNumber((int)reader.GetInt64(6)),
                reader.IsDBNull(7) ? null : DateOnly.FromDayNumber((int)reader.GetInt64(7)),
                (WeekDays)reader.GetInt32(8));
        return new PlanBlock(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            new TimeOnly(reader.GetInt64(3) * TimeSpan.TicksPerMinute),
            new TimeOnly(reader.GetInt64(4) * TimeSpan.TicksPerMinute),
            reader.IsDBNull(5) ? null : DateOnly.FromDayNumber((int)reader.GetInt64(5)),
            recurrence);
    }
}
