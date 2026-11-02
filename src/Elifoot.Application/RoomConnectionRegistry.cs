using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

/// <summary>Live transport presence, scoped to one server instance. Membership and careers remain persisted separately.</summary>
public sealed class RoomConnectionRegistry
{
    public sealed record Member(Guid WorldId, Guid CoachId);
    private readonly object _gate = new();
    private readonly Dictionary<string, Member> _connections = new(StringComparer.Ordinal);
    public Member? Find(string connectionId) { lock (_gate) return _connections.GetValueOrDefault(connectionId); }
    public void Bind(string connectionId, Guid worldId, Guid coachId) { lock (_gate) _connections[connectionId] = new(worldId, coachId); }
    public bool Remove(string connectionId, Guid worldId, Guid coachId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out var member) || member.WorldId != worldId || member.CoachId != coachId) return false;
            return _connections.Remove(connectionId);
        }
    }
    public HashSet<Guid> ConnectedCoaches(Guid worldId)
    { lock (_gate) return _connections.Values.Where(m => m.WorldId == worldId).Select(m => m.CoachId).ToHashSet(); }
    public bool IsConnected(Guid worldId, Guid coachId) => ConnectedCoaches(worldId).Contains(coachId);
    public ReadyCounterDto WithPresence(ReadyCounterDto counter)
    {
        var online = ConnectedCoaches(counter.GameWorldId);
        return counter with { Participants = counter.Participants.Select(p => p with { IsConnected = online.Contains(p.CoachId) }).ToList() };
    }
    public LiveRoundSnapshotDto WithPresence(LiveRoundSnapshotDto snapshot) => snapshot with { ReadyCounter = WithPresence(snapshot.ReadyCounter) };
}
