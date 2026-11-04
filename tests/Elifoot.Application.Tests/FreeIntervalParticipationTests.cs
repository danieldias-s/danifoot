using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class FreeIntervalParticipationTests
{
    private static async Task<(GameWorld World, MultiplayerRoundOrchestrator Service, HumanCoach A, HumanCoach B)> Room()
    {
        var repository = new InMemoryWorldRepository();
        var service = new MultiplayerRoundOrchestrator(repository, NullMatchRoomNotificationPublisher.Instance);
        var world = await service.CreateWorldAsync(new("Interval participation QA", TotalClubs: 128));
        world.RoundNumber = 1;
        var match = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world)[0];
        var a = (await service.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "Home", "home", match.HomeClubId)).Coach!;
        var b = (await service.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "Away", "away", match.AwayClubId)).Coach!;
        foreach (var coach in new[] { a, b })
        {
            var club = world.Clubs.First(c => c.Id == coach.CurrentClubId);
            var started = await service.SubmitTacticsAsync(new(world.Id, coach.Id, club.PreferredFormation,
                club.PreferredStarterIds, club.PreferredBenchIds, $"start-{coach.Id}", world.RoundVersion));
            Assert.True(started.Success, started.Error);
        }
        world.CurrentMatchMinute = 44;
        var players = world.Players.ToDictionary(p => p.Id);
        foreach (var fixture in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world))
        {
            PlayerStatisticsEngine.CaptureLineup(fixture, players);
            fixture.CurrentMinute = fixture.LastSimulatedMinute = 44;
        }
        return (world, service, a, b);
    }

    private static void MakeObserver(GameWorld world, HumanCoach coach, string kind)
    {
        world.Clubs.First(c => c.Id == coach.CurrentClubId).HumanCoachId = null;
        coach.CurrentClubId = null;
        var fixtures = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world);
        if (kind == "absent")
        {
            var playing = fixtures.SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).ToHashSet();
            var club = world.Clubs.First(c => !playing.Contains(c.Id) && c.HumanCoachId == null);
            coach.CurrentClubId = club.Id;
            club.HumanCoachId = coach.Id;
        }
        else if (kind == "finished")
        {
            var fixture = fixtures.First(m => !m.IsFinished &&
                world.Clubs.First(c => c.Id == m.HomeClubId).HumanCoachId == null &&
                world.Clubs.First(c => c.Id == m.AwayClubId).HumanCoachId == null);
            coach.CurrentClubId = fixture.HomeClubId;
            world.Clubs.First(c => c.Id == fixture.HomeClubId).HumanCoachId = coach.Id;
            fixture.IsFinished = true;
        }
    }

    private static async Task<LiveRoundSnapshotDto> ReachInterval(GameWorld world, MultiplayerRoundOrchestrator service)
    {
        var snapshot = (await service.AdvanceClockTickAsync(world.Id))!;
        // Real penalties at 45' must be decided before the interval boundary.
        for (int i = 0; snapshot.PendingPenalty is { } penalty && i < 10; i++)
        {
            var decision = await service.ChoosePenaltyTakerAsync(new(world.Id, penalty.CoachId, penalty.DecisionId,
                penalty.EligiblePlayers[0].PlayerId, $"penalty-{penalty.DecisionId}"));
            Assert.True(decision.Success, decision.Error);
            snapshot = decision.Snapshot!;
        }
        Assert.Null(snapshot.PendingPenalty);
        Assert.Equal(45, snapshot.CurrentMinute);
        return snapshot;
    }

    [Theory]
    [InlineData("unemployed")]
    [InlineData("absent")]
    [InlineData("finished")]
    public async Task OnlyObserversSkipTheIntervalAndContinueAtMinute46(string kind)
    {
        var (world, service, a, b) = await Room();
        MakeObserver(world, a, kind);
        MakeObserver(world, b, kind);
        var snapshot = await ReachInterval(world, service);
        Assert.Equal(RoundState.RUNNING_SECOND_HALF, snapshot.RoundState);
        Assert.Null(snapshot.ActivePauseWindow);
        Assert.True(world.PauseWindows.Last().IsClosed);
        var next = (await service.AdvanceClockTickAsync(world.Id))!;
        Assert.Equal(46, next.CurrentMinute);
        Assert.All(MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world), m =>
        {
            Assert.Equal(0, m.HomeUsedPauseWindows);
            Assert.Equal(0, m.AwayUsedPauseWindows);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoManualHumanDecisionsSkipTheInterval(bool autoMode)
    {
        var (world, service, a, b) = await Room();
        if (autoMode)
        {
            a.IsAutoMode = b.IsAutoMode = true;
        }
        else
        {
            MakeObserver(world, a, "unemployed");
            MakeObserver(world, b, "unemployed");
            world.HumanCoaches.Clear();
        }
        var snapshot = await ReachInterval(world, service);
        Assert.Equal(RoundState.RUNNING_SECOND_HALF, snapshot.RoundState);
        Assert.Null(snapshot.ActivePauseWindow);
    }

    [Theory]
    [InlineData("unemployed")]
    [InlineData("absent")]
    [InlineData("finished")]
    public async Task ObserverWaitsOnlyForTheHumanWhoCanEdit(string kind)
    {
        var (world, service, a, b) = await Room();
        MakeObserver(world, b, kind);
        var snapshot = await ReachInterval(world, service);
        Assert.Equal(RoundState.HALFTIME_PAUSED, snapshot.RoundState);
        Assert.Contains(b.Id, snapshot.ActivePauseWindow!.ConfirmedCoachIds);
        Assert.DoesNotContain(a.Id, snapshot.ActivePauseWindow.ConfirmedCoachIds);
        Assert.False(await service.ForceExpirePauseWindowIfTimedOutAsync(world.Id));
        var observer = await service.ConfirmContinueMatchAsync(new(world.Id, b.Id, "observer"));
        Assert.True(observer.Success, observer.Error);
        Assert.False(observer.Resumed);
        var player = await service.ConfirmContinueMatchAsync(new(world.Id, a.Id, "player"));
        Assert.True(player.Success, player.Error);
        Assert.True(player.Resumed);
        Assert.Equal(RoundState.RUNNING_SECOND_HALF, player.Snapshot!.RoundState);
    }

    [Fact]
    public async Task TwoPlayingHumansStillBothConfirmBeforeTheSecondHalf()
    {
        var (world, service, a, b) = await Room();
        Assert.Equal(RoundState.HALFTIME_PAUSED, (await ReachInterval(world, service)).RoundState);
        Assert.False((await service.ConfirmContinueMatchAsync(new(world.Id, a.Id, "first"))).Resumed);
        Assert.False(await service.ForceExpirePauseWindowIfTimedOutAsync(world.Id));
        Assert.True((await service.ConfirmContinueMatchAsync(new(world.Id, b.Id, "second"))).Resumed);
    }

    [Fact]
    public async Task AlreadyOpenIntervalWithOnlyObserversResumesWithoutWaitingForTimeout()
    {
        var (world, service, a, b) = await Room();
        await ReachInterval(world, service);
        var window = world.PauseWindows.Last();
        Assert.True(window.ExpiresAtUtc > DateTime.UtcNow);
        MakeObserver(world, a, "unemployed");
        MakeObserver(world, b, "unemployed");
        Assert.True(await service.ForceExpirePauseWindowIfTimedOutAsync(world.Id));
        Assert.Null(world.ActivePauseWindowId);
        Assert.Equal(RoundState.RUNNING_SECOND_HALF, world.RoundState);
    }
}
