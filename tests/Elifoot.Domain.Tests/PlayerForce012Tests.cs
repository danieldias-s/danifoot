using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class PlayerForce012Tests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(9)]
    public void LowStrengthAdvancesOnePointWithoutJumpingToTen(int strength)
    {
        var (w, p) = SinglePlayer(strength);
        p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        Assert.Equal(strength + 1, p.Strength);
        var change = Assert.Single(w.PlayerStrengthHistory);
        Assert.Equal(strength, change.StrengthBefore);
        Assert.Equal(strength + 1, change.StrengthAfter);
        Assert.Equal("NORMAL_PROGRESS", change.ChangeReason);
        Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p), p.MarketPrice);
        Assert.Equal(MarketAndEconomyEngine.CalculateFairSalaryPerRound(p), p.SalaryPerRound);
    }

    [Fact]
    public void StrengthFiftyDoesNotOverflowOrEmitAnUnchangedEvent()
    {
        var (w, p) = SinglePlayer(50);
        p.SeasonGoals = 12;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        Assert.Equal(50, p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
        Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p), p.MarketPrice);
        Assert.Equal(MarketAndEconomyEngine.CalculateFairSalaryPerRound(p), p.SalaryPerRound);
    }

    [Fact]
    public void StrongPlayersStillDeclineUnderExplicitLegacyPolicy()
    {
        int declines = 0;
        for (int seed = 1; seed <= 200; seed++)
        {
            var (w, p) = SinglePlayer(50, seed);
            p.SeasonMatches = 10;
            PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
            Assert.InRange(p.Strength, 49, 50);
            if (p.Strength == 49)
            {
                declines++;
                var change = Assert.Single(w.PlayerStrengthHistory);
                Assert.Equal("NORMAL_DECLINE", change.ChangeReason);
                Assert.Equal("LEGACY_ABOVE_DIVISION_TARGET", change.Detail);
                Assert.Null(change.InjuryId);
            }
        }
        Assert.InRange(declines, 50, 150);
    }

    [Theory]
    [InlineData(PlayerPosition.G)] [InlineData(PlayerPosition.D)]
    [InlineData(PlayerPosition.M)] [InlineData(PlayerPosition.A)]
    public void ExistingPolicyHasNoAdditionalGoalkeeperGate(PlayerPosition position)
    {
        var (w, p) = SinglePlayer(1);
        p.Position = position;
        p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        Assert.Equal(2, p.Strength);
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao)] [InlineData(DivisionTier.SegundaDivisao)]
    [InlineData(DivisionTier.TerceiraDivisao)] [InlineData(DivisionTier.QuartaDivisao)]
    [InlineData(DivisionTier.Distrital)]
    public void DivisionTargetsAreNotHardGrowthCaps(DivisionTier division)
    {
        var (w, p) = SinglePlayer(48);
        w.Clubs[0].DivisionTier = division;
        p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        Assert.Equal(49, p.Strength);
        Assert.Equal(division, Assert.Single(w.PlayerStrengthHistory).Division);
    }

    [Fact]
    public void GrowthHistorySurvivesTransferPromotionAndRelegation()
    {
        var (w, p) = SinglePlayer(1);
        var original = w.Clubs[0];
        var next = new Club { Name = "Destino", DivisionTier = DivisionTier.TerceiraDivisao };
        w.Clubs.Add(next);
        p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        p.ClubId = next.Id;
        Assert.Equal(2, p.Strength);
        for (int season = 2; season <= 4; season++)
        {
            w.SeasonNumber = season;
            next.DivisionTier = (DivisionTier)(5 - season);
            PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        }
        w.SeasonNumber = 5;
        next.DivisionTier = DivisionTier.QuartaDivisao;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        original.Name = "Nome posterior";
        var history = PlayerStatisticsEngine.GetHistory(w, p.Id)!;
        Assert.Equal(6, p.Strength);
        Assert.Equal(next.Id, history.Player.CurrentClubId);
        Assert.Equal(5, history.StrengthHistory.Count);
        Assert.Equal("Origem", history.StrengthHistory.Last().ClubName);
        Assert.Equal(DivisionTier.QuartaDivisao, history.StrengthHistory.Last().Division);
        Assert.Contains(history.StrengthHistory, e => e.Division == DivisionTier.PrimeiraDivisao);
    }

    [Fact]
    public void SeasonalProcessingIsIdempotentAfterJsonReloadAndDoesNotTouchRankings()
    {
        var (w, p) = SinglePlayer(1);
        p.SeasonGoals = 6;
        string ranking = JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(w));
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        string snapshot = JsonSerializer.Serialize(w);
        var loaded = JsonSerializer.Deserialize<GameWorld>(snapshot)!;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(loaded);
        Assert.Equal(snapshot, JsonSerializer.Serialize(loaded));
        Assert.Equal(ranking, JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded)));
        Assert.Equal(1, loaded.PlayerStrengthHistoryStartSeason);
        Assert.Equal(22, loaded.PlayerStrengthHistoryStartRound);
        Assert.Equal(1, loaded.EvolvedPlayerStrengthSeasons.Single());
    }

    [Fact]
    public void ReplayWithSameSnapshotAndSeedProducesIdenticalEventsAndPrices()
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
        w.RoundNumber = 22;
        foreach (var p in w.Players.Take(30)) p.Strength = 1;
        string initial = JsonSerializer.Serialize(w);
        var a = JsonSerializer.Deserialize<GameWorld>(initial)!;
        var b = JsonSerializer.Deserialize<GameWorld>(initial)!;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(a);
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(b);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Fact]
    public void LegacySnapshotDoesNotInventEarlierStrengthEvents()
    {
        var (w, p) = SinglePlayer(1);
        w.SeasonNumber = 50;
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(w))!.AsObject();
        foreach (var key in new[] { "PlayerStrengthHistory", "EvolvedPlayerStrengthSeasons", "PlayerStrengthHistoryStartSeason", "PlayerStrengthHistoryStartRound" }) node.Remove(key);
        var loaded = JsonSerializer.Deserialize<GameWorld>(node.ToJsonString())!;
        var history = PlayerStatisticsEngine.GetHistory(loaded, p.Id)!;
        Assert.Empty(history.StrengthHistory);
        Assert.Null(history.StrengthHistoryStartSeason);
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(loaded);
        Assert.All(loaded.PlayerStrengthHistory, e => Assert.Equal(50, e.Season));
        Assert.Equal(50, loaded.PlayerStrengthHistoryStartSeason);
    }

    [Theory]
    [InlineData(0)] [InlineData(45)] [InlineData(80)] [InlineData(-1)]
    public void StartingBenchAndSubstitutionsDoNotChangeNominalStrength(int entryMinute)
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
        w.RoundNumber = 3;
        var m = Match(w);
        var p = w.Players.First(p => p.Id == (entryMinute == 0
            ? m.HomeActiveStarterIds.First(id => w.Players.Single(p => p.Id == id).Position == PlayerPosition.A)
            : m.HomeBenchIds.First(id => w.Players.Single(p => p.Id == id).Position == PlayerPosition.A)));
        p.Strength = 1;
        var players = w.Players.ToDictionary(p => p.Id);
        PlayerStatisticsEngine.CaptureLineup(m, players);
        var home = w.Clubs[0]; var away = w.Clubs[1];
        for (int minute = 1; minute <= 90; minute++)
        {
            m.CurrentMinute = minute;
            if (minute == entryMinute)
            {
                var outgoing = m.HomeActiveStarterIds.First(id => players[id].Position == PlayerPosition.A);
                var result = MatchSimulationEngine.ApplyInMatchAdjustments(m, home, true,
                    minute == 45 ? RoundState.HALFTIME_PAUSED : RoundState.GLOBAL_PAUSED,
                    home.Id, null, [new(outgoing, p.Id)], players, true);
                Assert.True(result.Success, result.ErrorMessage);
                Assert.Equal(1, p.Strength);
            }
            MatchSimulationEngine.SimulateSingleMinute(m, home, away, players,
                ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, w.Seed);
            Assert.Equal(1, p.Strength);
        }
        m.IsFinished = true;
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w, [m]);
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [m]);
        Assert.Equal(1, p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
        Assert.Equal(entryMinute != -1, w.PlayerStatistics.Any(s => s.PlayerId == p.Id && s.MatchesPlayed == 1));
    }

    [Fact]
    public void RealInjuryRecoveryAndReinjuryPreserveNominalStrengthInCurrentEngine()
    {
        var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
        w.RoundNumber = 3;
        var m = Match(w);
        var players = w.Players.ToDictionary(p => p.Id);
        var before = players.ToDictionary(k => k.Key, k => k.Value.Strength);
        foreach (var p in players.Values) p.Behavior = PlayerBehavior.FairPlay;
        var home = w.Clubs[0]; var away = w.Clubs[1];
        MatchEventRecord? injury = null;
        for (int seed = 1; seed <= 200 && injury == null; seed++)
        {
            m=Match(w); // A new trial has a new fixture; repeated minute ticks are now idempotent.
            for (int minute = 1; minute <= 90 && injury == null; minute++)
            {
                m.CurrentMinute = minute;
                injury = MatchSimulationEngine.SimulateSingleMinute(m, home, away, players,
                    ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, seed)
                    .FirstOrDefault(e => e.EventType == MatchEventType.Injury);
            }
        }
        Assert.NotNull(injury);
        var injured = players[injury.PrimaryPlayerId!.Value];
        Assert.InRange(injured.InjuryRoundsRemaining, 1, 3);
        Assert.False(injured.IsAvailableForMatch);
        Assert.Equal(before[injured.Id], injured.Strength);
        int duration = injured.InjuryRoundsRemaining;
        m.IsFinished = true;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [m]);
        Assert.Equal(duration, injured.InjuryRoundsRemaining);
        for (int round = 1; round <= duration; round++)
        {
            w.RoundNumber++;
            var next = new MatchFixture { HomeClubId = home.Id, AwayClubId = away.Id,
                SeasonNumber = 1, RoundNumber = w.RoundNumber, IsFinished = true, CurrentMinute = 90 };
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [next]);
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [next]);
            Assert.Equal(duration - round, injured.InjuryRoundsRemaining);
            Assert.Equal(before[injured.Id], injured.Strength);
        }
        Assert.True(injured.IsAvailableForMatch);
        // A second real injury to the same recovered player, with full valid lineups.
        MatchEventRecord? second = null;
        int observedInjuries=0,exposedMinutes=0;
        for (int seed = 1; seed <= 500 && second == null; seed++)
        {
            foreach (var p in players.Values) { p.InjuryRoundsRemaining = 0; p.SuspensionRoundsRemaining = 0; }
            var next = Match(w);
            var active = injured.ClubId == home.Id ? next.HomeActiveStarterIds : next.AwayActiveStarterIds;
            if (!active.Contains(injured.Id))
            {
                int index = active.FindIndex(id => players[id].Position == injured.Position);
                Assert.True(index >= 0);
                active[index] = injured.Id;
            }
            for (int minute = 1; minute <= 90 && second == null; minute++)
            {
                next.CurrentMinute = minute;
                if(active.Contains(injured.Id))exposedMinutes++;
                var frame=MatchSimulationEngine.SimulateSingleMinute(next, home, away, players,
                    ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, seed);
                observedInjuries+=frame.Count(e=>e.EventType==MatchEventType.Injury);
                second=frame.FirstOrDefault(e => e.EventType == MatchEventType.Injury && e.PrimaryPlayerId == injured.Id);
            }
        }
        Assert.True(second!=null,$"Target {injured.Id} role {injured.Position}: injuries={observedInjuries}, exposure={exposedMinutes}, club={injured.ClubId}, expected={home.Id}/{away.Id}");
        Assert.Equal(before[injured.Id], injured.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
    }

    [Fact]
    public void AiAndNormalLineupValidationExcludeAlreadyInjuredPlayers()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var squad = w.Players.Where(p => p.ClubId == w.Clubs[0].Id).ToList();
        var (oldStarters, oldBench) = AiManagerBrain.BuildStartersAndBench(squad, TacticalFormations.All["4-4-2"]);
        var injured = squad.Single(p => p.Id == oldStarters[0]);
        injured.InjuryRoundsRemaining = 3;
        var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, TacticalFormations.All["4-4-2"]);
        Assert.DoesNotContain(injured.Id, starters.Concat(bench));
        Assert.False(SquadValidator.ValidatePreMatchLineup("4-4-2", oldStarters, oldBench, squad, true).IsValid);
    }

    [Fact]
    public void TransferToDistrictDoesNotClampAStrongPlayer()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var seller = w.Clubs[0];
        var buyer = w.Clubs.First(c => c.DivisionTier == DivisionTier.Distrital);
        var p = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        p.Strength = 50;
        buyer.CashBalance = 10_000_000m;
        var result = MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, p.Id,
            1_000_000m, "Controlled strong transfer", "force012-strong-transfer");
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(50, p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
        Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p), p.MarketPrice);
    }

    [Fact]
    public void SeasonalPolicyDoesNotMistakeInjuryForItsStrengthAdjustment()
    {
        var (w, p) = SinglePlayer(1);
        p.InjuryRoundsRemaining = 3;
        p.SeasonGoals = 6;
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
        var change = Assert.Single(w.PlayerStrengthHistory);
        Assert.Equal("NORMAL_PROGRESS", change.ChangeReason);
        Assert.Null(change.InjuryId);
        Assert.Equal(2, p.Strength);
        Assert.Equal(3, p.InjuryRoundsRemaining);
    }

    [Fact]
    public void RealGoalkeeperInjuryDoesNotReduceNominalStrengthInCurrentEngine()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var players = w.Players.ToDictionary(p => p.Id);
        var strengthsBefore = w.Players.ToDictionary(p => p.Id, p => p.Strength);
        foreach (var p in players.Values) p.Behavior = PlayerBehavior.FairPlay;
        MatchEventRecord? injury = null;
        for (int seed = 1; seed <= 300 && injury == null; seed++)
        {
            foreach (var p in players.Values) { p.InjuryRoundsRemaining = 0; p.SuspensionRoundsRemaining = 0; }
            var m = Match(w);
            for (int minute = 1; minute <= 90 && injury == null; minute++)
            {
                m.CurrentMinute = minute;
                injury = MatchSimulationEngine.SimulateSingleMinute(m, w.Clubs[0], w.Clubs[1], players,
                    ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, seed)
                    .FirstOrDefault(e => e.EventType == MatchEventType.Injury
                        && players[e.PrimaryPlayerId!.Value].Position == PlayerPosition.G);
            }
        }
        Assert.NotNull(injury);
        var keeper = players[injury.PrimaryPlayerId!.Value];
        Assert.False(keeper.IsAvailableForMatch);
        Assert.Equal(strengthsBefore[keeper.Id], keeper.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
    }

    internal static (GameWorld World, Player Player) SinglePlayer(int strength, int seed = 199812)
    {
        var club = new Club { Name = "Origem", DivisionTier = DivisionTier.QuartaDivisao };
        var p = new Player { Name = "Força um", ClubId = club.Id, Position = PlayerPosition.A, Strength = strength };
        return (new GameWorld { Seed = seed, RoundNumber = 22, Clubs = [club], Players = [p], PlayerStatisticsVersion = 9 }, p);
    }

    private static MatchFixture Match(GameWorld w)
    {
        var homeSquad = w.Players.Where(p => p.ClubId == w.Clubs[0].Id).ToList();
        var awaySquad = w.Players.Where(p => p.ClubId == w.Clubs[1].Id).ToList();
        var (hs, hb) = AiManagerBrain.BuildStartersAndBench(homeSquad, TacticalFormations.All["4-4-2"]);
        var (aws, awb) = AiManagerBrain.BuildStartersAndBench(awaySquad, TacticalFormations.All["4-4-2"]);
        return new MatchFixture { GameWorldId = w.Id, SeasonNumber = w.SeasonNumber, RoundNumber = w.RoundNumber,
            HomeClubId = w.Clubs[0].Id, AwayClubId = w.Clubs[1].Id, DeterministicMatchIndex = 1,
            HomeFormation = "4-4-2", AwayFormation = "4-4-2", HomeActiveStarterIds = hs, HomeBenchIds = hb,
            AwayActiveStarterIds = aws, AwayBenchIds = awb, DivisionTier = w.Clubs[0].DivisionTier };
    }
}
