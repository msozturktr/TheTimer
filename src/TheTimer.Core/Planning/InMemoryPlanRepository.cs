namespace TheTimer.Core.Planning;

public sealed class InMemoryPlanRepository : IPlanRepository
{
    private readonly Dictionary<Guid, PlanBlock> _blocks = [];
    private readonly HashSet<(Guid BlockId, DateOnly Day)> _exceptions = [];

    public void Add(PlanBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!_blocks.TryAdd(block.Id, block))
            throw new InvalidOperationException($"A plan block with id {block.Id} already exists.");
    }

    public void Update(PlanBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!_blocks.ContainsKey(block.Id))
            throw new InvalidOperationException($"Unknown plan block {block.Id}.");
        _blocks[block.Id] = block;
    }

    public bool Delete(Guid id)
    {
        _exceptions.RemoveWhere(e => e.BlockId == id);
        return _blocks.Remove(id);
    }

    public PlanBlock? Get(Guid id) => _blocks.GetValueOrDefault(id);

    public IReadOnlyList<PlanBlock> GetBlocksForRange(DateOnly from, DateOnly toInclusive) =>
        _blocks.Values.Where(b => IsInRange(b, from, toInclusive)).ToList();

    public void AddException(Guid blockId, DateOnly day)
    {
        if (!_blocks.ContainsKey(blockId))
            throw new InvalidOperationException($"Unknown plan block {blockId}.");
        _exceptions.Add((blockId, day));
    }

    public IReadOnlySet<(Guid BlockId, DateOnly Day)> GetExceptions(DateOnly from, DateOnly toInclusive) =>
        _exceptions.Where(e => e.Day >= from && e.Day <= toInclusive).ToHashSet();

    private static bool IsInRange(PlanBlock block, DateOnly from, DateOnly toInclusive) =>
        block.Recurrence is { } r
            ? r.StartDate <= toInclusive && (r.EndDate is not { } end || end >= from)
            : block.Date >= from && block.Date <= toInclusive;
}
