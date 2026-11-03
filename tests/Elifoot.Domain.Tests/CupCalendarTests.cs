using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class CupCalendarTests
{
    [Fact]
    public void World128HasSevenLeagueLeagueCupBlocksAndEndsWithTheFinal()
    {
        var world=WorldSeedFactory.CreateSeededWorld();
        var schedule=CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(world);
        Assert.Equal(21,schedule.Count);
        Assert.Equal(21,world.TotalRoundsInSeason);
        for(int block=0;block<7;block++)
        {
            Assert.Equal(CompetitionType.League,schedule[block*3]);
            Assert.Equal(CompetitionType.League,schedule[block*3+1]);
            Assert.NotEqual(CompetitionType.League,schedule[block*3+2]);
        }
        Assert.Equal(CompetitionType.CupRoundOf128,schedule[2]);
        Assert.Equal(CompetitionType.CupFinal,schedule[^1]);
        Assert.Equal(16,world.Fixtures.Count(m=>m.RoundNumber==1));
    }

    [Theory]
    [InlineData(64)] [InlineData(128)] [InlineData(256)] [InlineData(512)] [InlineData(1024)]
    public void EverySurvivingClubPlaysExactlyOncePerCupStageAndEliminatedClubsNeverReturn(int size)
    {
        var world=WorldSeedFactory.CreateSeededWorld(totalClubs:size);
        var survivors=world.Clubs.Select(c=>c.Id).ToHashSet();
        int games=0;
        foreach(var round in Enumerable.Range(1,world.TotalRoundsInSeason)
            .Where(r=>CompetitionAndSeasonEngine.GetCompetitionTypeForRound(world,r)!=CompetitionType.League))
        {
            world.RoundNumber=round;
            CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world,round);
            var matches=world.Fixtures.Where(m=>m.SeasonNumber==1&&m.RoundNumber==round).ToList();
            var actual=matches.SelectMany(m=>new[]{m.HomeClubId,m.AwayClubId}).ToList();
            Assert.Equal(survivors.Count,actual.Count);
            Assert.Equal(actual.Count,actual.Distinct().Count());
            Assert.True(survivors.SetEquals(actual));
            CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world,round);
            Assert.Equal(matches.Count,world.Fixtures.Count(m=>m.RoundNumber==round));
            foreach(var match in matches) {match.IsFinished=true;match.WinnerClubId=match.HomeClubId;}
            survivors=matches.Select(m=>m.WinnerClubId!.Value).ToHashSet();
            games+=matches.Count;
        }
        Assert.Single(survivors);
        Assert.Equal(size-1,games);
        Assert.Equal(CompetitionType.CupFinal,CompetitionAndSeasonEngine.GetCompetitionTypeForRound(world,world.TotalRoundsInSeason));
    }

    [Fact]
    public void LegacyCustomWorldWith129ClubsStillGeneratesItsOpeningFixtures()
    {
        var world=WorldSeedFactory.CreateSeededWorld(totalClubs:129);
        Assert.False(world.LeagueFirstSchedule);
        Assert.Equal(CompetitionType.CupPreliminary1,CompetitionAndSeasonEngine.GetCompetitionTypeForRound(world,1));
        Assert.Equal(32,world.Fixtures.Count(m=>m.RoundNumber==1));
        Assert.Equal(CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(world),
            CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(129));
    }

    [Fact]
    public void NextCupStageRequiresThePreviousStageToBeFinished()
    {
        var world=WorldSeedFactory.CreateSeededWorld();
        CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world,3);
        Assert.Throws<InvalidOperationException>(()=>CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world,6));
        Assert.DoesNotContain(world.Fixtures,m=>m.RoundNumber==6);
    }

    [Fact]
    public void SaveWithoutScheduleFlagKeepsItsSeasonAndUsesNewCalendarAtNextInitialization()
    {
        var world=WorldSeedFactory.CreateSeededWorld();
        world.LeagueFirstSchedule=false;
        var oldSchedule=CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(world);
        var oldLeagueRounds=oldSchedule.Select((type,index)=>(type,index))
            .Where(row=>row.type==CompetitionType.League).Select(row=>row.index+1).ToArray();
        var groups=world.Fixtures.GroupBy(m=>m.RoundNumber).OrderBy(g=>g.Key).ToArray();
        for(int i=0;i<groups.Length;i++)foreach(var match in groups[i])match.RoundNumber=oldLeagueRounds[i];
        world.TotalRoundsInSeason=22;
        world.RoundNumber=2;
        var json=JsonNode.Parse(JsonSerializer.Serialize(world))!.AsObject();
        json.Remove(nameof(GameWorld.LeagueFirstSchedule));
        var loaded=JsonSerializer.Deserialize<GameWorld>(json.ToJsonString())!;
        Assert.False(loaded.LeagueFirstSchedule);
        Assert.Equal(CompetitionType.CupPreliminary2,CompetitionAndSeasonEngine.GetCompetitionTypeForRound(loaded,2));
        Assert.Equal(CompetitionType.League,CompetitionAndSeasonEngine.GetCompetitionTypeForRound(loaded,3));
        Assert.Equal(16,loaded.Fixtures.Count(m=>m.RoundNumber==3));
        var strengths=loaded.Players.Select(p=>p.Strength).ToArray();
        loaded.SeasonNumber++;
        CompetitionAndSeasonEngine.InitializeSeasonFixtures(loaded);
        Assert.True(loaded.LeagueFirstSchedule);
        Assert.Equal(21,loaded.TotalRoundsInSeason);
        Assert.Equal(CompetitionType.League,CompetitionAndSeasonEngine.GetCompetitionTypeForRound(loaded,1));
        Assert.Equal(strengths,loaded.Players.Select(p=>p.Strength));
    }

    [Fact]
    public void CupDismissalAndInjurySurviveReloadWhileParticipationHasNoForceEffect()
    {
        var (world,player)=Discipline012CTests.World();
        var match=Discipline012CTests.Fixture(world,3,CompetitionType.CupRoundOf128);
        Discipline012CTests.Card(world,match,player,MatchEventType.DirectRedCard,60,2);
        var injured=world.Players.First(p=>p.ClubId==player.ClubId&&p.Id!=player.Id);
        ForceInjury episode;
        int seed=0;
        do
        {
            injured.ForceInjuries.Clear();
            injured.InjuryDaysRemaining=0;
            episode=ClassicForceEvolutionEngine.RegisterInjury(match,world.Clubs.Single(c=>c.Id==injured.ClubId),injured,++seed);
        }while(episode.TotalLoss==0);
        Discipline012CTests.Finish(world,match);
        ClassicForceEvolutionEngine.ApplyFinishedRound(world,[match]);
        Assert.Equal(50,player.Strength);
        Assert.Equal(50,injured.Strength);
        Assert.Equal(2,player.SuspensionMatchesRemaining);
        Assert.Empty(world.PlayerStrengthHistory);
        var loaded=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        ClassicForceEvolutionEngine.AdvanceToDate(loaded,episode.EndDate);
        Assert.Equal(50-episode.TotalLoss,loaded.Players.Single(p=>p.Id==injured.Id).Strength);
        Assert.All(loaded.PlayerStrengthHistory,e=>Assert.Equal("INJURY_DAILY_LOSS",e.ChangeReason));
        Assert.Equal(2,loaded.Players.Single(p=>p.Id==player.Id).SuspensionMatchesRemaining);
        var league=Discipline012CTests.Fixture(loaded,4);
        Discipline012CTests.Finish(loaded,league);
        Assert.Equal(1,loaded.Players.Single(p=>p.Id==player.Id).SuspensionMatchesRemaining);
    }
}
