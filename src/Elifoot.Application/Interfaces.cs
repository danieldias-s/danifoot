using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public interface IWorldRepository
{
    Task<GameWorld?> GetWorldAsync(Guid worldId, CancellationToken ct = default);
    Task<GameWorld?> GetWorldByInviteCodeAsync(string inviteCode, CancellationToken ct = default);
    Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default);
    Task<List<GameWorld>> ListWorldsForClockAsync(CancellationToken ct = default)
        => ListWorldsAsync(ct);
    async IAsyncEnumerable<GameWorld> EnumerateWorldsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var world in await ListWorldsAsync(ct)) yield return world;
    }
    Task SaveWorldAsync(GameWorld world, CancellationToken ct = default);

    // A persistent implementation must resolve the next read from storage if the outcome is unknown.
    Task SavePauseTransitionAsync(GameWorld world, long previousVersion)
        => SaveWorldAsync(world, CancellationToken.None);

    Task<GameWorld?> GetWorldForRoundCheckpointAsync(Guid worldId, CancellationToken ct = default)
        => GetWorldAsync(worldId, ct);

    Task SaveRoundCheckpointAsync(GameWorld world, bool retry)
        => SaveWorldAsync(world, CancellationToken.None);
}

public sealed class RoundCheckpointPendingException() : IOException("A rodada ainda está sendo gravada.");

public interface IMatchRoomNotificationPublisher
{
    Task PublishReadyCounterUpdatedAsync(Guid worldId, ReadyCounterDto counter, CancellationToken ct = default);
    Task PublishRoundStateChangedAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default);
    Task PublishClockTickAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default);
    Task PublishGlobalPauseAsync(Guid worldId, PauseWindowStateDto pauseWindow, LiveRoundSnapshotDto snapshot, CancellationToken ct = default);
    Task PublishMarketUpdatedAsync(Guid worldId, string summaryMessage, CancellationToken ct = default);
    Task PublishWorldDataChangedAsync(WorldDataChange change, CancellationToken ct = default)
        => PublishMarketUpdatedAsync(change.WorldId, "Dados da sala atualizados.", ct);
    Task PublishRoundCheckpointPendingAsync(Guid worldId, long version, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class NullMatchRoomNotificationPublisher : IMatchRoomNotificationPublisher
{
    public static readonly NullMatchRoomNotificationPublisher Instance = new();
    public Task PublishReadyCounterUpdatedAsync(Guid worldId, ReadyCounterDto counter, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishRoundStateChangedAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishClockTickAsync(Guid worldId, LiveRoundSnapshotDto snapshot, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishGlobalPauseAsync(Guid worldId, PauseWindowStateDto pauseWindow, LiveRoundSnapshotDto snapshot, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishMarketUpdatedAsync(Guid worldId, string summaryMessage, CancellationToken ct = default) => Task.CompletedTask;
}
