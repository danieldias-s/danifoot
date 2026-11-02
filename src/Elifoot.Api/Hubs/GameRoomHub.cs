using Elifoot.Application;
using Elifoot.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Elifoot.Api.Hubs;

public sealed class GameRoomHub : Hub
{
    private readonly IWorldRepository _repository;
    private readonly RoomConnectionRegistry _connections;
    public GameRoomHub(IWorldRepository repository, RoomConnectionRegistry connections)
    { _repository = repository; _connections = connections; }

    public async Task<LiveRoundSnapshotDto?> JoinRoom(Guid worldId, Guid coachId)
    {
        // Validate the target before detaching a valid current room.
        var validation = MultiplayerRoundOrchestrator.GetRoomLock(worldId);
        await validation.WaitAsync();
        try
        {
            var target = await _repository.GetWorldAsync(worldId);
            if (target == null || !target.HumanCoaches.Any(h => h.Id == coachId)) return null;
        }
        finally { validation.Release(); }
        var previous = _connections.Find(Context.ConnectionId);
        if (previous != null && (previous.WorldId != worldId || previous.CoachId != coachId))
            await LeaveRoom(previous.WorldId, previous.CoachId);
        var sem = MultiplayerRoundOrchestrator.GetRoomLock(worldId);
        await sem.WaitAsync();
        try
        {
            var world = await _repository.GetWorldAsync(worldId);
            var coach = world?.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            if (world == null || coach == null) return null;
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(worldId));
            _connections.Bind(Context.ConnectionId, worldId, coachId);
            coach.IsConnected = true;
            try { await _repository.SaveWorldAsync(world); }
            catch { _connections.Remove(Context.ConnectionId, worldId, coachId); coach.IsConnected = _connections.IsConnected(worldId, coachId); throw; }
            var snapshot = _connections.WithPresence(MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world));
            await Clients.Group(GetGroupName(worldId)).SendAsync("ReadyCounterUpdated", snapshot.ReadyCounter);
            return snapshot;
        }
        finally { sem.Release(); }
    }

    public async Task LeaveRoom(Guid worldId, Guid coachId)
    {
        var sem = MultiplayerRoundOrchestrator.GetRoomLock(worldId);
        await sem.WaitAsync();
        try
        {
            // A connection may leave only its own bound room and coach.
            if (!_connections.Remove(Context.ConnectionId, worldId, coachId)) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(worldId));
            var world = await _repository.GetWorldAsync(worldId);
            var coach = world?.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            if (world == null || coach == null) return;
            coach.IsConnected = _connections.IsConnected(worldId, coachId);
            await _repository.SaveWorldAsync(world);
            await Clients.Group(GetGroupName(worldId)).SendAsync("ReadyCounterUpdated", _connections.WithPresence(MultiplayerRoundOrchestrator.BuildReadyCounterDto(world)));
        }
        finally { sem.Release(); }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.Find(Context.ConnectionId) is { } member)
            await LeaveRoom(member.WorldId, member.CoachId);
        await base.OnDisconnectedAsync(exception);
    }
    public static string GetGroupName(Guid worldId) => $"world:{worldId}";
}

public sealed class SignalRMatchRoomNotificationPublisher : IMatchRoomNotificationPublisher
{
    private readonly IHubContext<GameRoomHub> _hubContext;

    private readonly RoomConnectionRegistry _connections;
    public SignalRMatchRoomNotificationPublisher(IHubContext<GameRoomHub> hubContext, RoomConnectionRegistry connections)
    { _hubContext = hubContext; _connections = connections; }

    public Task PublishReadyCounterUpdatedAsync(Guid worldId, ReadyCounterDto counter, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("ReadyCounterUpdated", _connections.WithPresence(counter), ct);

    public Task PublishRoundStateChangedAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("RoundStateChanged", _connections.WithPresence(snapshot), ct);

    public Task PublishClockTickAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("ClockTick", _connections.WithPresence(snapshot), ct);

    public Task PublishGlobalPauseAsync(Guid worldId, PauseWindowStateDto pauseWindow, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("GlobalPause", new { pauseWindow, snapshot = _connections.WithPresence(snapshot) }, ct);

    public Task PublishMarketUpdatedAsync(Guid worldId, string summaryMessage, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("MarketUpdated", new { worldId, summaryMessage }, ct);
}
