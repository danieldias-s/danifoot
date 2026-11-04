using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class AutomaticRoundPreparationTests
{
    [Fact]
    public async Task ConcurrentPreparationsAdvanceExactlyOnceAndLateRequestCannotSkipAnotherRound()
    {
        var repo = new InMemoryWorldRepository();
        var service = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        var world = await service.CreateWorldAsync(new CreateWorldRequest("Auto preparation", TotalClubs: 32));
        world.RoundState = RoundState.RESULTS;
        var season = world.SeasonNumber;
        var round = world.RoundNumber;
        await repo.SaveWorldAsync(world);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            service.AdvanceToNextRoundPreparationAsync(world.Id, expectedSeasonNumber: season, expectedRoundNumber: round)));
        var prepared = (await repo.GetWorldAsync(world.Id))!;
        Assert.Equal(round + 1, prepared.RoundNumber);
        Assert.Equal(RoundState.PREPARING, prepared.RoundState);
        // The next round later ends. A delayed request for the previous round must do nothing.
        prepared.RoundState = RoundState.RESULTS;
        await repo.SaveWorldAsync(prepared);
        await service.AdvanceToNextRoundPreparationAsync(world.Id, expectedSeasonNumber: season, expectedRoundNumber: round);
        var afterLateRequest = (await repo.GetWorldAsync(world.Id))!;
        Assert.Equal(round + 1, afterLateRequest.RoundNumber);
        Assert.Equal(RoundState.RESULTS, afterLateRequest.RoundState);
    }

    [Fact]
    public async Task PreparationRejectsPreviousSeasonAndStillSupportsExplicitLegacyCalls()
    {
        var repo = new InMemoryWorldRepository();
        var service = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        var world = await service.CreateWorldAsync(new CreateWorldRequest("Season guard", TotalClubs: 32));
        world.RoundState = RoundState.RESULTS;
        var round = world.RoundNumber;
        await repo.SaveWorldAsync(world);
        await service.AdvanceToNextRoundPreparationAsync(world.Id, expectedSeasonNumber: world.SeasonNumber - 1, expectedRoundNumber: round);
        Assert.Equal(round, (await repo.GetWorldAsync(world.Id))!.RoundNumber);
        await service.AdvanceToNextRoundPreparationAsync(world.Id);
        Assert.Equal(round + 1, (await repo.GetWorldAsync(world.Id))!.RoundNumber);
    }
}
