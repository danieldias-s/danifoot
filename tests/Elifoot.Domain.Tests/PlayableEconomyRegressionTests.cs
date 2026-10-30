using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class PlayableEconomyRegressionTests
{
    [Fact]
    public void ManagerKeepsPersonalityAndPreferencesWhenSeasonRotatesClubs()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var club = w.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        foreach (var other in w.Clubs.Where(c => c.DivisionTier == DivisionTier.PrimeiraDivisao && c.Id != club.Id)) other.LeagueWins = 10;
        var manager = w.AiManagers.Single(m => m.Id == club.AiManagerId);
        var identity = manager.Identity;
        var preferences = manager.PreferredFormations.Select(p => (p.Formation,p.WeightPercent)).ToArray();
        CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);
        Assert.NotEqual(club.Id, manager.CurrentClubId);
        Assert.Equal(identity, manager.Identity);
        Assert.Equal(preferences, manager.PreferredFormations.Select(p => (p.Formation,p.WeightPercent)).ToArray());
    }

    [Fact]
    public void SubstitutionLimitRejectsSixthInNormalTime_AndSeventhInExtraTime()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var club = w.Clubs[0];
        var squad = w.Players.Where(p => p.ClubId == club.Id).ToList();
        var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, TacticalFormations.All["4-4-2"]);
        var match = new MatchFixture { GameWorldId = w.Id, HomeClubId = club.Id, AwayClubId = w.Clubs[1].Id,
            HomeFormation = "4-4-2", HomeActiveStarterIds = starters.ToList(), HomeBenchIds = bench.ToList() };
        var players = w.Players.ToDictionary(p => p.Id);
        var firstFive = starters.Take(5).Zip(bench.Take(5), (o,i) => new InMatchSubstitutionDto(o,i)).ToList();
        var five = MatchSimulationEngine.ApplyInMatchAdjustments(match,club,true,RoundState.GLOBAL_PAUSED,Guid.NewGuid(),null,firstFive,players,true);
        Assert.True(five.Success);
        var sixth = new List<InMatchSubstitutionDto> { new(starters[5],bench[5]) };
        var rejected = MatchSimulationEngine.ApplyInMatchAdjustments(match,club,true,RoundState.HALFTIME_PAUSED,Guid.NewGuid(),null,sixth,players,true);
        Assert.False(rejected.Success);
        Assert.Equal(5,match.HomeUsedSubstitutions);
        match.EnteredExtraTime = true;
        var extra = MatchSimulationEngine.ApplyInMatchAdjustments(match,club,true,RoundState.EXTRA_TIME_BREAK,Guid.NewGuid(),null,sixth,players,true);
        Assert.True(extra.Success);
        var seventh = MatchSimulationEngine.ApplyInMatchAdjustments(match,club,true,RoundState.EXTRA_TIME_HALFTIME,Guid.NewGuid(),null,
            [new(starters[6],bench[6])],players,true);
        Assert.False(seventh.Success);
        Assert.Equal(6,match.HomeUsedSubstitutions);
    }

    [Fact]
    public void RoundSponsorship_IsIdempotent_AndFundsEliminatedDistrictClubs()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var club = w.Clubs.First(c => c.DivisionTier == DivisionTier.Distrital);
        var before = club.CashBalance;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, []);
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, []);
        Assert.Equal(before + 20_000m, club.CashBalance);
        Assert.Single(w.Transactions, t => t.ClubId == club.Id && t.Category == FinancialTransactionCategory.Sponsorship);
    }

    [Fact]
    public void AuctionCreatedAtSeasonEnd_ExpiresInNextSeason_WithoutGivingHumanUnwantedPlayers()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        foreach (var club in w.Clubs) club.HumanCoachId = Guid.NewGuid();
        var player = w.Players[0]; player.ClubId = null;
        var auction = new AuctionItem { GameWorldId = w.Id, PlayerId = player.Id,
            CreatedAtSeason = 1, CreatedAtRound = 22, ExpiresAtRound = 24, MinimumBid = 100_000 };
        w.Auctions.Add(auction); w.SeasonNumber = 2; w.RoundNumber = 1;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.True(auction.IsClosed);
        Assert.Null(player.ClubId);
    }
}
