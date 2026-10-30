using Elifoot.Domain;

namespace Elifoot.Application;

/// <summary>Materializes player queries under the same world lock as match and market mutations.</summary>
public sealed class PlayerRankingsApplicationService(IWorldRepository repository)
{
    public async Task<T?> ReadWorldAsync<T>(Guid worldId, Func<GameWorld, T> read,
        CancellationToken ct = default) where T : class
    {
        var gate = MultiplayerRoundOrchestrator.GetLock(worldId);
        await gate.WaitAsync(ct);
        try
        {
            var world = await repository.GetWorldAsync(worldId, ct);
            return world == null ? null : read(world);
        }
        finally
        {
            gate.Release();
        }
    }
}
