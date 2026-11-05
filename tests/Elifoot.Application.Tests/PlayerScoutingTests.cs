using System.Text.Json;
using Elifoot.Application;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class PlayerScoutingTests
{
    [Fact]
    public async Task SearchRequiresThreeLettersFindsPartialNamesAcrossAllClubsAndLeavesGameplayUntouched()
    {
        var repository = new InMemoryWorldRepository();
        var rounds = new MultiplayerRoundOrchestrator(repository, NullMatchRoomNotificationPublisher.Instance);
        var world = await rounds.CreateWorldAsync(new("Search QA"));
        var coach = (await rounds.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "Scout", "scout", joinUnemployed: true)).Coach!;
        var service = new ClubAndMarketApplicationService(repository, NullMatchRoomNotificationPublisher.Instance);
        world.Players[0].Name = "Daniel Dias";
        world.Players[1].Name = "Adán Silva";
        world.Players[2].Name = "Danilo Costa";
        world.Players[0].IsListedForTransfer = false;
        var before = JsonSerializer.Serialize(world);
        Assert.Empty((await service.GetPlayerScoutingAsync(world.Id, coach.Id, "Da"))!.Players);
        var found = (await service.GetPlayerScoutingAsync(world.Id, coach.Id, " dan "))!.Players;
        Assert.Contains(found, p => p.PlayerId == world.Players[0].Id);
        Assert.Contains(found, p => p.PlayerId == world.Players[1].Id);
        Assert.Contains(found, p => p.PlayerId == world.Players[2].Id);
        Assert.Contains((await service.GetPlayerScoutingAsync(world.Id, coach.Id, "Dániel Dias"))!.Players, p => p.PlayerId == world.Players[0].Id);
        Assert.Equal(before, JsonSerializer.Serialize(world));
    }

    [Fact]
    public async Task ObservationsArePersonalIdempotentPersistentAndFollowThePlayerAfterATransfer()
    {
        var repository = new InMemoryWorldRepository();
        var rounds = new MultiplayerRoundOrchestrator(repository, NullMatchRoomNotificationPublisher.Instance);
        var world = await rounds.CreateWorldAsync(new("Observed QA"));
        var a = (await rounds.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "A", "a", joinUnemployed: true)).Coach!;
        var b = (await rounds.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "B", "b", joinUnemployed: true)).Coach!;
        var service = new ClubAndMarketApplicationService(repository, NullMatchRoomNotificationPublisher.Instance);
        var player = world.Players[0];
        var cash = world.Clubs.Select(c => c.CashBalance).ToList();
        var version = world.RoundVersion;
        Assert.True((await service.SetPlayerObservationAsync(world.Id, a.Id, player.Id, true)).Success);
        Assert.True((await service.SetPlayerObservationAsync(world.Id, a.Id, player.Id, true)).Success);
        Assert.Single(a.ObservedPlayerIds);
        Assert.Empty((await service.GetPlayerScoutingAsync(world.Id, b.Id, observedOnly: true))!.Players);
        Assert.False((await service.SetPlayerObservationAsync(world.Id, Guid.NewGuid(), player.Id, true)).Success);
        Assert.False((await service.SetPlayerObservationAsync(world.Id, a.Id, Guid.NewGuid(), true)).Success);
        var restored = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        await repository.SaveWorldAsync(restored);
        restored.Players[0].ClubId = restored.Clubs.Last().Id;
        var saved = (await service.GetPlayerScoutingAsync(world.Id, a.Id, observedOnly: true))!;
        Assert.Equal(restored.Clubs.Last().Id, Assert.Single(saved.Players).ClubId);
        Assert.Equal(cash, restored.Clubs.Select(c => c.CashBalance).ToList());
        Assert.Equal(version, restored.RoundVersion);
        Assert.True((await service.SetPlayerObservationAsync(world.Id, a.Id, player.Id, false)).Success);
        Assert.Empty((await service.GetPlayerScoutingAsync(world.Id, a.Id, observedOnly: true))!.Players);
        Assert.Empty(JsonSerializer.Deserialize<HumanCoach>("{}")!.ObservedPlayerIds);
    }
}
