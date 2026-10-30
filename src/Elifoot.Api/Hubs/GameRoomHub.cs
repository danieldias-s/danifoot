using Elifoot.Application;
using Elifoot.Contracts;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Elifoot.Api.Hubs;

public sealed class GameRoomHub : Hub
{
    private static readonly ConcurrentDictionary<string, (Guid World, Guid Coach)> Connections = new();
    private readonly IWorldRepository _repository;

    public GameRoomHub(IWorldRepository repository)
    {
        _repository = repository;
    }

    public async Task<LiveRoundSnapshotDto?> JoinRoom(Guid worldId, Guid coachId)
    {
        string groupName = GetGroupName(worldId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var world = await _repository.GetWorldAsync(worldId);
        if (world == null) return null;

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach != null)
        {
            Connections[Context.ConnectionId] = (worldId, coachId);
            coach.IsConnected = true;
            await _repository.SaveWorldAsync(world);
        }

        var snapshot = MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world);
        await Clients.Group(groupName).SendAsync("ReadyCounterUpdated", snapshot.ReadyCounter);
        return snapshot;
    }

    public async Task LeaveRoom(Guid worldId, Guid coachId)
    {
        Connections.TryRemove(Context.ConnectionId, out _);
        string groupName = GetGroupName(worldId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        var world = await _repository.GetWorldAsync(worldId);
        if (world == null) return;

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach != null)
        {
            coach.IsConnected = Connections.Values.Any(c => c.World == worldId && c.Coach == coachId);
            await _repository.SaveWorldAsync(world);
            await Clients.Group(groupName).SendAsync("ReadyCounterUpdated", MultiplayerRoundOrchestrator.BuildReadyCounterDto(world));
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var member))
            await LeaveRoom(member.World, member.Coach);
        await base.OnDisconnectedAsync(exception);
    }

    public static string GetGroupName(Guid worldId) => $"world:{worldId}";
}

public sealed class SignalRMatchRoomNotificationPublisher : IMatchRoomNotificationPublisher
{
    private readonly IHubContext<GameRoomHub> _hubContext;

    public SignalRMatchRoomNotificationPublisher(IHubContext<GameRoomHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishReadyCounterUpdatedAsync(Guid worldId, ReadyCounterDto counter, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("ReadyCounterUpdated", counter, ct);

    public Task PublishRoundStateChangedAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("RoundStateChanged", snapshot, ct);

    public Task PublishClockTickAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("ClockTick", snapshot, ct);

    public Task PublishGlobalPauseAsync(Guid worldId, PauseWindowStateDto pauseWindow, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("GlobalPause", new { pauseWindow, snapshot }, ct);

    public Task PublishMarketUpdatedAsync(Guid worldId, string summaryMessage, CancellationToken ct = default)
        => _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId)).SendAsync("MarketUpdated", new { worldId, summaryMessage }, ct);
}
