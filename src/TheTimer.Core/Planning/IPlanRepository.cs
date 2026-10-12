namespace TheTimer.Core.Planning;

public interface IPlanRepository
{
    void Add(PlanBlock block);

    /// <summary>Replaces the stored block with the same id; throws <see cref="InvalidOperationException"/> if unknown.</summary>
    void Update(PlanBlock block);

    /// <summary>Removes the block and its exceptions; returns true if one was removed.</summary>
    bool Delete(Guid id);

    PlanBlock? Get(Guid id);

    /// <summary>One-offs dated within the range plus recurring blocks whose [StartDate, EndDate] intersects it.</summary>
    IReadOnlyList<PlanBlock> GetBlocksForRange(DateOnly from, DateOnly toInclusive);

    /// <summary>Skips one occurrence of a recurring block; idempotent. Throws if the block is unknown.</summary>
    void AddException(Guid blockId, DateOnly day);

    IReadOnlySet<(Guid BlockId, DateOnly Day)> GetExceptions(DateOnly from, DateOnly toInclusive);
}
