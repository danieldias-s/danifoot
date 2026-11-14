using Elifoot.Application;
using Elifoot.Contracts;
using Microsoft.AspNetCore.SignalR;
using Elifoot.Api.Services;
using System.Security.Claims;

namespace Elifoot.Api.Hubs;

public sealed class GameRoomHub : Hub
{
    private readonly IWorldRepository _repository;
    private readonly RoomConnectionRegistry _connections;
    private readonly IConfiguration _configuration;
    private readonly Elifoot.Infrastructure.WorldAuthority? _authority;
    private readonly RoundStream _stream;
    public GameRoomHub(IWorldRepository repository, RoomConnectionRegistry connections, IConfiguration configuration,
        RoundStream stream, Elifoot.Infrastructure.WorldAuthority? authority = null)
    { _repository = repository; _connections = connections; _configuration = configuration; _authority = authority; _stream = stream; }

    public override async Task OnConnectedAsync()
    {
        if (AccountLogin.Enabled(_configuration)) {
            var http = Context.GetHttpContext()!;
            var service = http.RequestServices.GetRequiredService<AccountSessionService>();
            var token = Context.User?.FindFirstValue("sid");
            if (token == null || await service.Session(token) == null) { Context.Abort(); return; }
            http.RequestServices.GetRequiredService<AccountConnections>().Bind(Context.ConnectionId, AccountSessionService.Hash(token), Context.Abort);
        }
        await base.OnConnectedAsync();
    }

    public Task<LiveRoundSnapshotDto?> JoinRoom(Guid worldId, Guid coachId) => Join(worldId, coachId, false);

    public Task<LiveRoundSnapshotDto?> JoinRoomV2(Guid worldId, Guid coachId) => Join(worldId, coachId, true);

    private async Task<LiveRoundSnapshotDto?> Join(Guid worldId, Guid coachId, bool incremental)
    {
        if (_authority is { IsActive: false }) { Context.Abort(); return null; }
        if (AccountLogin.Enabled(_configuration)) {
            var service = Context.GetHttpContext()!.RequestServices.GetRequiredService<AccountSessionService>();
            var token = Context.User?.FindFirstValue("sid");
            var session = token == null ? null : await service.Session(token);
            if (session == null || !await service.Owns(session.AccountId, coachId)) throw new HubException("A conta não controla este treinador.");
        }
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
        if (previous != null && (previous.WorldId != worldId || previous.CoachId != coachId || previous.Incremental != incremental))
            await LeaveRoom(previous.WorldId, previous.CoachId);
        var sem = MultiplayerRoundOrchestrator.GetRoomLock(worldId);
        await sem.WaitAsync();
        try
        {
            var world = await _repository.GetWorldAsync(worldId);
            var coach = world?.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            if (world == null || coach == null) return null;
            if (Context.ConnectionAborted.IsCancellationRequested) return null;
            var group = GetGroupName(worldId, incremental);
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            _connections.Bind(Context.ConnectionId, worldId, coachId, incremental);
            coach.IsConnected = true;
            try { await _repository.SaveWorldAsync(world); }
            catch
            {
                _connections.Remove(Context.ConnectionId, worldId, coachId);
                coach.IsConnected = _connections.IsConnected(worldId, coachId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
                if (!_connections.HasSubscribers(worldId, true)) _stream.Forget(worldId);
                throw;
            }
            var snapshot = _connections.WithPresence(MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world));
            await Clients.Groups(GetGroupName(worldId), GetGroupName(worldId, true)).SendAsync("ReadyCounterUpdated", snapshot.ReadyCounter);
            return _stream.Stamp(snapshot);
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
            var member = _connections.Find(Context.ConnectionId);
            if (member == null || !_connections.Remove(Context.ConnectionId, worldId, coachId)) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(worldId, member.Incremental));
            if (!_connections.HasSubscribers(worldId, true)) _stream.Forget(worldId);
            var world = await _repository.GetWorldForRoundCheckpointAsync(worldId);
            var coach = world?.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            if (world == null || coach == null) return;
            coach.IsConnected = _connections.IsConnected(worldId, coachId);
            if (world.RoundCheckpointNeedsSave || world.PendingWorldDataChange != null) return;
            await _repository.SaveWorldAsync(world);
            await Clients.Groups(GetGroupName(worldId), GetGroupName(worldId, true))
                .SendAsync("ReadyCounterUpdated", _connections.WithPresence(MultiplayerRoundOrchestrator.BuildReadyCounterDto(world)));
        }
        finally { sem.Release(); }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (AccountLogin.Enabled(_configuration)) Context.GetHttpContext()!.RequestServices.GetRequiredService<AccountConnections>().Remove(Context.ConnectionId);
        if (_connections.Find(Context.ConnectionId) is { } member)
            await LeaveRoom(member.WorldId, member.CoachId);
        await base.OnDisconnectedAsync(exception);
    }
    public static string GetGroupName(Guid worldId, bool incremental = false) =>
        incremental ? $"world-v2:{worldId}" : $"world:{worldId}";
}

public sealed class SignalRMatchRoomNotificationPublisher : IMatchRoomNotificationPublisher
{
    private readonly IHubContext<GameRoomHub> _hubContext;

    private readonly RoomConnectionRegistry _connections;
    private readonly RoundStream _stream;
    public SignalRMatchRoomNotificationPublisher(IHubContext<GameRoomHub> hubContext, RoomConnectionRegistry connections, RoundStream stream)
    { _hubContext = hubContext; _connections = connections; _stream = stream; }

    private IClientProxy Room(Guid id) => _hubContext.Clients.Groups(GameRoomHub.GetGroupName(id), GameRoomHub.GetGroupName(id, true));

    public Task PublishReadyCounterUpdatedAsync(Guid worldId, ReadyCounterDto counter, CancellationToken ct = default)
        => Room(worldId).SendAsync("ReadyCounterUpdated", _connections.WithPresence(counter), ct);

    public Task PublishRoundStateChangedAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => PublishSnapshot(worldId, "RoundStateChanged", snapshot, ct);

    public Task PublishClockTickAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => PublishSnapshot(worldId, "ClockTick", snapshot, ct);

    public Task PublishGlobalPauseAsync(Guid worldId, PauseWindowStateDto pauseWindow, LiveRoundSnapshotDto snapshot, CancellationToken ct = default)
        => PublishSnapshot(worldId, "GlobalPause", snapshot, ct, pauseWindow);

    private Task PublishSnapshot(Guid worldId, string legacyEvent, LiveRoundSnapshotDto snapshot, CancellationToken ct,
        PauseWindowStateDto? pauseWindow = null)
    {
        bool legacy = _connections.HasSubscribers(worldId, false);
        bool incremental = _connections.HasSubscribers(worldId, true);
        if (!legacy && !incremental) return Task.CompletedTask;

        var live = _connections.WithPresence(snapshot);
        var stamped = _stream.Stamp(live);
        var legacySend = legacy ? _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId))
            .SendAsync(legacyEvent, pauseWindow == null ? (object)stamped : new { pauseWindow, snapshot = stamped }, ct) : Task.CompletedTask;
        if (!incremental) return legacySend;

        // A confirmed result is a recovery point, even for clients that missed the final live ticks.
        var (eventName, payload) = _stream.Publish(live, legacyEvent == "ClockTick" &&
            live.RoundState is not (RoundState.RESULTS or RoundState.FINISHED));
        return Task.WhenAll(legacySend, _hubContext.Clients.Group(GameRoomHub.GetGroupName(worldId, true)).SendAsync(eventName, payload, ct));
    }

    public Task PublishMarketUpdatedAsync(Guid worldId, string summaryMessage, CancellationToken ct = default)
        => Room(worldId).SendAsync("MarketUpdated", new { worldId, summaryMessage }, ct);

    public Task PublishWorldDataChangedAsync(WorldDataChange change, CancellationToken ct = default)
        => Task.WhenAll(
            _hubContext.Clients.Group(GameRoomHub.GetGroupName(change.WorldId, true)).SendAsync("WorldDataChanged", change, ct),
            Room(change.WorldId).SendAsync("MarketUpdated",
                new { worldId = change.WorldId, scoped = true, summaryMessage = "Dados da sala atualizados." }, ct));

    public Task PublishRoundCheckpointPendingAsync(Guid worldId, long version, CancellationToken ct = default)
        => Room(worldId).SendAsync(
            "RoundCheckpointPending", new { worldId, version }, ct);
}
