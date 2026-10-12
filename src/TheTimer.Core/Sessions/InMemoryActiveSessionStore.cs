namespace TheTimer.Core.Sessions;

public sealed class InMemoryActiveSessionStore : IActiveSessionStore
{
    private ActiveSessionRecord? _record;

    public void Save(ActiveSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _record = record;
    }

    public ActiveSessionRecord? Load() => _record;

    public void Clear() => _record = null;

    public void Touch(DateTimeOffset lastSeen) => _record = _record is null ? null : _record with { LastSeen = lastSeen };
}
