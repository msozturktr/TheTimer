namespace TheTimer.Core.Planning;

public enum PlanEditScope
{
    ThisDayOnly,
    ThisAndFollowing,
}

/// <summary>Plan editing rules and occurrence expansion on top of an <see cref="IPlanRepository"/>.</summary>
public sealed class PlanService
{
    private readonly IPlanRepository _repository;

    public PlanService(IPlanRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public IReadOnlyList<PlanOccurrence> GetOccurrences(DateOnly day, TimeZoneInfo timeZone) =>
        GetOccurrences(day, day, timeZone);

    /// <summary>Expanded occurrences (one-offs and series minus exceptions), ordered by start then task name.</summary>
    public IReadOnlyList<PlanOccurrence> GetOccurrences(DateOnly from, DateOnly toInclusive, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        if (toInclusive < from)
            return [];

        var blocks = _repository.GetBlocksForRange(from, toInclusive);
        var exceptions = _repository.GetExceptions(from, toInclusive);
        var result = new List<PlanOccurrence>();

        for (var day = from; day <= toInclusive; day = day.AddDays(1))
        {
            var dayItems = new List<PlanOccurrence>();
            foreach (var block in blocks)
            {
                var occurs = block.Recurrence is { } r
                    ? r.OccursOn(day) && !exceptions.Contains((block.Id, day))
                    : block.Date == day;
                if (!occurs)
                    continue;
                dayItems.Add(new PlanOccurrence(
                    block, day,
                    ZoneTime.ToInstant(day, block.Start, timeZone),
                    ZoneTime.ToInstant(day, block.End, timeZone),
                    false));
            }

            for (var i = 0; i < dayItems.Count; i++)
            {
                var a = dayItems[i];
                var overlaps = false;
                for (var j = 0; j < dayItems.Count && !overlaps; j++)
                    overlaps = i != j && a.StartsAt < dayItems[j].EndsAt && dayItems[j].StartsAt < a.EndsAt;
                result.Add(a with { HasOverlap = overlaps });
            }
        }

        return result
            .OrderBy(o => o.StartsAt)
            .ThenBy(o => o.Block.TaskName, StringComparer.Ordinal)
            .ToList();
    }

    public PlanBlock AddOneOff(DateOnly day, TimeOnly start, TimeOnly end, string task, string? tag)
    {
        var block = new PlanBlock(Guid.NewGuid(), task, tag, start, end, day, null);
        _repository.Add(block);
        return block;
    }

    public PlanBlock AddRecurring(PlanRecurrence recurrence, TimeOnly start, TimeOnly end, string task, string? tag)
    {
        ArgumentNullException.ThrowIfNull(recurrence);
        var block = new PlanBlock(Guid.NewGuid(), task, tag, start, end, null, recurrence);
        _repository.Add(block);
        return block;
    }

    public void DeleteOccurrence(PlanOccurrence occurrence, PlanEditScope scope)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        var block = occurrence.Block;
        if (block.Recurrence is not { } rec)
        {
            _repository.Delete(block.Id);
            return;
        }

        if (scope == PlanEditScope.ThisDayOnly)
            _repository.AddException(block.Id, occurrence.Day);
        else if (occurrence.Day <= rec.StartDate)
            _repository.Delete(block.Id);
        else
            _repository.Update(block with { Recurrence = rec with { EndDate = occurrence.Day.AddDays(-1) } });
    }

    public void UpdateOccurrence(
        PlanOccurrence occurrence, TimeOnly start, TimeOnly end, string task, string? tag, PlanEditScope scope)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        var block = occurrence.Block;
        // Validates the new values before anything is written.
        var updated = new PlanBlock(block.Id, task, tag, start, end, block.Date, block.Recurrence);

        if (block.Recurrence is not { } rec)
        {
            _repository.Update(updated);
            return;
        }

        if (scope == PlanEditScope.ThisDayOnly)
        {
            var single = new PlanBlock(Guid.NewGuid(), task, tag, start, end, occurrence.Day, null);
            _repository.AddException(block.Id, occurrence.Day);
            _repository.Add(single);
            return;
        }

        if (occurrence.Day <= rec.StartDate)
        {
            _repository.Update(updated);
            return;
        }

        // Split the series: the old block ends the day before, a new block carries the edit onwards.
        var following = new PlanBlock(
            Guid.NewGuid(), task, tag, start, end, null, new PlanRecurrence(occurrence.Day, rec.EndDate, rec.Days));
        _repository.Update(block with { Recurrence = rec with { EndDate = occurrence.Day.AddDays(-1) } });
        _repository.Add(following);
        // Skipped days after the split belong to the new series now.
        foreach (var (blockId, day) in _repository.GetExceptions(occurrence.Day, rec.EndDate ?? DateOnly.MaxValue))
            if (blockId == block.Id)
                _repository.AddException(following.Id, day);
    }

    /// <summary>Changes days / date range of a whole series.</summary>
    public void UpdateRecurrence(Guid blockId, PlanRecurrence newRecurrence)
    {
        ArgumentNullException.ThrowIfNull(newRecurrence);
        var block = _repository.Get(blockId)
            ?? throw new InvalidOperationException($"Unknown plan block {blockId}.");
        if (block.Recurrence is null)
            throw new InvalidOperationException("Only recurring blocks have a recurrence.");
        _repository.Update(block with { Recurrence = newRecurrence });
    }
}
