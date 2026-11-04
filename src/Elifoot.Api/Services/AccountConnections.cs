using System.Collections.Concurrent;

namespace Elifoot.Api.Services;

/// Server-side revocation also closes live room transports, not just HTTP cookies.
public sealed class AccountConnections
{
    private readonly ConcurrentDictionary<string, (string Session, Action Abort)> _connections = new();
    public void Bind(string connection, string session, Action abort) => _connections[connection] = (session, abort);
    public void Remove(string connection) => _connections.TryRemove(connection, out _);
    public void Revoke(string session) {
        foreach (var entry in _connections.Where(x => x.Value.Session == session)) entry.Value.Abort();
    }
}
