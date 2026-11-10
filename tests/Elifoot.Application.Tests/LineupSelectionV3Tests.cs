using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class LineupSelectionV3Tests
{
    private static async Task<(MultiplayerRoundOrchestrator Service, GameWorld World, HumanCoach Coach, Club Club)> Setup(IWorldRepository? repo = null)
    {
        var service = new MultiplayerRoundOrchestrator(repo ?? new InMemoryWorldRepository(), NullMatchRoomNotificationPublisher.Instance);
        var world = await service.CreateWorldAsync(new("V3 A/M", MaxHumans: 2, TotalClubs: 32));
        var club = world.Clubs[0];
        var joined = await service.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "Human", "v3", club.Id);
        Assert.True(joined.Success);
        return (service, world, joined.Coach!, club);
    }

    [Theory]
    [InlineData(LineupSelectionMode.Automatic)]
    [InlineData(LineupSelectionMode.Best)]
    public async Task HumanSelectionIsAuthoritativePersistentAndConcurrentRetriesAreIdempotent(LineupSelectionMode mode)
    {
        var (service, w, coach, club) = await Setup();
        var squad = w.Players.Where(p => p.ClubId == club.Id).ToList();
        var expected = LineupSelectionEngine.Select(squad, TacticalFormations.All["4-4-2"], mode, club.PreferredStarterIds, w);
        var request = new SelectLineupRequest(coach.Id, "4-4-2", mode, null, "same-click", w.RoundVersion);
        long version = w.RoundVersion;
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.SelectLineupAsync(w.Id, request)));
        Assert.All(responses, r => Assert.True(r.Success, r.Error));
        Assert.Equal(version + 1, w.RoundVersion); Assert.Single(w.LineupSelectionReceipts);
        Assert.All(responses, r => Assert.Equal(expected.Starters, r.Selection!.StarterPlayerIds));
        Assert.Equal(expected.Bench, club.PreferredBenchIds); Assert.Equal(mode, club.LineupSelectionMode);
        var saved = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var savedClub = saved.Clubs.Single(c => c.Id == club.Id);
        Assert.Equal(mode, savedClub.LineupSelectionMode); Assert.Equal(expected.Starters, savedClub.PreferredStarterIds);
        var repo = new InMemoryWorldRepository(); await repo.SaveWorldAsync(saved);
        var restoredService = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        var replay = await restoredService.SelectLineupAsync(saved.Id, request);
        Assert.True(replay.Success, replay.Error); Assert.Equal(version + 1, saved.RoundVersion);
        Assert.False((await service.SelectLineupAsync(w.Id, request with { Mode = mode == LineupSelectionMode.Best ? LineupSelectionMode.Automatic : LineupSelectionMode.Best })).Success);
    }

    [Fact]
    public async Task AiUsesTheSameAutomaticSelectionIncludingAssistsAndIncumbents()
    {
        var (service, w, coach, club) = await Setup();
        var squad = w.Players.Where(p => p.ClubId == club.Id).ToList();
        foreach (var p in squad) { p.Strength = 40; p.IsStar = false; }
        var attacker = squad.Last(p => p.Position == PlayerPosition.A); attacker.Strength = 39;
        w.PlayerStatistics.Add(new() { PlayerId = attacker.Id, ClubId = club.Id, Season = w.SeasonNumber, Assists = 10 });
        var ai = new AiManager { PreferredFormations = [new("4-4-2", 100)] };
        var selected = AiManagerBrain.PlanAndSelectLineup(ai, club, squad, null, null, true, w.Seed, w.SeasonNumber, w.RoundNumber, w.AllowClassic505, w);
        var human = await service.SelectLineupAsync(w.Id, new(coach.Id, selected.Formation, LineupSelectionMode.Automatic, club.PreferredStarterIds.ToList(), "ai-human", w.RoundVersion));
        Assert.True(human.Success, human.Error);
        Assert.Equal(selected.Starters, human.Selection!.StarterPlayerIds); Assert.Equal(selected.Bench, human.Selection.BenchPlayerIds);
        Assert.Contains(attacker.Id, selected.Starters);
    }

    [Fact]
    public async Task UnmanagedAiClubDefaultsToAutomaticEvenIfItsFormerHumanUsedBest()
    {
        var (service, w, coach, club) = await Setup();
        var otherClub = w.Clubs[1];
        var other = (await service.JoinWorldAndAssignClubAsync(w.Id, Guid.NewGuid(), "Other", "other-v3", otherClub.Id)).Coach!;
        var fixture = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w).First(f =>
            f.HomeClubId != club.Id && f.AwayClubId != club.Id && f.HomeClubId != otherClub.Id && f.AwayClubId != otherClub.Id);
        var aiClub = w.Clubs.Single(c => c.Id == fixture.HomeClubId);
        aiClub.AiManagerId = null; aiClub.LineupSelectionMode = LineupSelectionMode.Best;
        var squad = w.Players.Where(p => p.ClubId == aiClub.Id).ToList();
        var attackers = squad.Where(p => p.Position == PlayerPosition.A).ToArray();
        Assert.True(attackers.Length >= 3);
        foreach (var p in attackers) { p.Strength = 40; p.SeasonGoals = 0; }
        attackers.Last().Strength = 35; attackers.Last().SeasonGoals = 7;
        var expected = LineupSelectionEngine.Select(squad, TacticalFormations.All["4-4-2"], LineupSelectionMode.Automatic, aiClub.PreferredStarterIds, w);
        Assert.Contains(attackers.Last().Id, expected.Starters);
        foreach (var (human, humanClub) in new[] { (coach, club), (other, otherClub) })
        {
            var result = await service.SubmitTacticsAsync(new(w.Id, human.Id, humanClub.PreferredFormation,
                humanClub.PreferredStarterIds.ToList(), humanClub.PreferredBenchIds.ToList(), "ready-" + human.Id, w.RoundVersion));
            Assert.True(result.Success, result.Error);
        }
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, w.RoundState);
        Assert.Equal(expected.Starters, fixture.HomeActiveStarterIds);
    }

    [Fact]
    public async Task SelectionRejectsStaleReadyRunningForeignAndMalformedCommandsWithoutChangingPreferences()
    {
        var (service, w, coach, club) = await Setup(); var original = club.PreferredStarterIds.ToList();
        var good = new SelectLineupRequest(coach.Id, "4-4-2", LineupSelectionMode.Automatic, null, "guard", w.RoundVersion);
        foreach (var bad in new[] {
            good with { CoachId = Guid.NewGuid() }, good with { ExpectedRoundVersion = w.RoundVersion - 1 },
            good with { Formation = "7-7-7" }, good with { Mode = (LineupSelectionMode)99 }, good with { IdempotencyKey = "" },
            good with { CurrentStarterPlayerIds = [w.Players.First(p => p.ClubId != club.Id).Id] },
            good with { CurrentStarterPlayerIds = [original[0], original[0]] } })
            Assert.False((await service.SelectLineupAsync(w.Id, bad)).Success);
        coach.IsReadyForRound = true; Assert.False((await service.SelectLineupAsync(w.Id, good)).Success);
        coach.IsReadyForRound = false; w.RoundState = RoundState.RUNNING_FIRST_HALF;
        Assert.False((await service.SelectLineupAsync(w.Id, good)).Success);
        Assert.Equal(original, club.PreferredStarterIds); Assert.Empty(w.LineupSelectionReceipts);
    }

    [Fact]
    public async Task RetryCompletesAFailedSaveWithoutReselectingOrAdvancingVersionAgain()
    {
        var repo = new FailingRepository(); var (service, w, coach, club) = await Setup(repo);
        var request = new SelectLineupRequest(coach.Id, "4-4-2", LineupSelectionMode.Best, null, "recover-save", w.RoundVersion);
        long version = w.RoundVersion; repo.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => service.SelectLineupAsync(w.Id, request));
        var retry = await service.SelectLineupAsync(w.Id, request);
        Assert.True(retry.Success, retry.Error); Assert.Equal(version + 1, w.RoundVersion);
        Assert.Equal(club.PreferredStarterIds, retry.Selection!.StarterPlayerIds);
        w.RoundNumber++; Assert.False((await service.SelectLineupAsync(w.Id, request)).Success);
    }

    private sealed class FailingRepository : IWorldRepository
    {
        private readonly InMemoryWorldRepository inner = new();
        public bool FailNext;
        public Task<GameWorld?> GetWorldAsync(Guid id, CancellationToken ct = default) => inner.GetWorldAsync(id, ct);
        public Task<GameWorld?> GetWorldByInviteCodeAsync(string code, CancellationToken ct = default) => inner.GetWorldByInviteCodeAsync(code, ct);
        public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default) => inner.ListWorldsAsync(ct);
        public Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
        {
            if (FailNext) { FailNext = false; throw new IOException("Injected save failure"); }
            return inner.SaveWorldAsync(world, ct);
        }
    }
}
