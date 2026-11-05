using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class History017CTests
{
    [Fact] public void LegacyPersistedSeasonMovementAndSoldAuctionAreRecoveredWithoutInventingTheFallbackCupChampion()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.History=new();w.SeasonNumber=4;w.RoundNumber=3;w.Fixtures.Clear();
        var c=w.Clubs[0];var p=w.Players[0];var auction=Guid.NewGuid();
        w.SeasonHistories.Add(new(){SeasonNumber=1,FirstDivisionChampionClubId=c.Id,CupChampionClubId=c.Id,PromotedClubIds=[w.Clubs[1].Id]});
        w.Economy.AuctionHistory.Add(new(auction,p.Id,AuctionReason.Voluntary,c.Id,w.Clubs[1].Id,2,3,45000,"SOLD"));
        w.Transactions.Add(new(){PlayerId=p.Id,BuyerClubId=w.Clubs[1].Id,SellerClubId=c.Id,Amount=-45000,SeasonNumber=2,RoundNumber=3,IdempotencyKey=$"auction:{auction}:buy"});
        HistoryEngine.Observe(w);HistoryEngine.Observe(w);
        var h=HistoryEngine.GetClubHistory(w,c.Id)!;Assert.Contains(h.Titles,t=>t.Season==1&&t.Competition=="LEAGUE");Assert.DoesNotContain(h.Titles,t=>t.Competition=="CUP");
        Assert.Single(w.History.Transfers);Assert.Equal(auction,w.History.Transfers.Single().Key);
        Assert.True(HistoryEngine.GetClubHistory(w,w.Clubs[1].Id)!.Seasons.Single(s=>s.Season==1).Promoted);
    }
    [Fact] public void KnockoutResultsUseTheRecordedWinnerAndDoNotInventGoalDifferencesFromShootouts()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();
        var match=new HistoryMatchDto(Guid.NewGuid(),1,4,CompetitionType.CupFinal,null,a,"A",0,b,"B",0,a);
        var home=HistoryEngine.Totals([match],a);var away=HistoryEngine.Totals([match],b);
        Assert.Equal(1,home.Wins);Assert.Equal(1,away.Losses);Assert.Equal(0,home.GoalsFor);Assert.Equal(0,away.GoalDifference);
    }
    [Fact] public void NewClubAndPlayerHaveAnHonestEmptyHistoryAndQueriesDoNotChangeGameplay()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);var c=w.Clubs[0];
        var before=JsonSerializer.Serialize(w);
        var h=HistoryEngine.GetClubHistory(w,c.Id)!;
        Assert.Equal(0,h.Total.Games);Assert.Empty(h.Titles);Assert.Null(h.Seasons.Single().FinalPosition);
        var p=PlayerStatisticsEngine.GetHistory(w,w.Players[0].Id)!;
        Assert.Equal(0,p.Career.MatchesPlayed);Assert.Empty(p.Transfers);Assert.Null(p.SeasonStrengths.Single().FinalStrength);
        Assert.Equal(before,JsonSerializer.Serialize(w));
    }
    [Fact] public void TransferIdentityAndCareerSurviveMultipleClubsAndSnapshotRestart()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.Auctions.Clear();
        var a=w.Clubs[0];var b=w.Clubs[1];var c=w.Clubs[2];b.CashBalance=c.CashBalance=20_000_000;
        var p=w.Players.Last(p=>p.ClubId==a.Id&&p.Position==PlayerPosition.A);var id=p.Id;
        w.PlayerStatistics.Add(new(){PlayerId=id,ClubId=a.Id,ClubName=a.Name,Season=1,CompetitionType=CompetitionType.League,Division=a.DivisionTier,MatchesPlayed=3,GoalsScored=5,Assists=2,HasIncompleteAssists=false});
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,a.Id,id,1000,"QA","first").Success);HistoryEngine.Observe(w);
        w.RoundNumber++;
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,c.Id,b.Id,id,2000,"QA","second").Success);HistoryEngine.Observe(w);HistoryEngine.Observe(w);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;HistoryEngine.Observe(restored);
        var h=PlayerStatisticsEngine.GetHistory(restored,id)!;
        Assert.Equal(c.Id,h.Player.CurrentClubId);Assert.Equal(5,h.Career.GoalsScored);Assert.Equal(2,h.Career.Assists);
        Assert.Equal(2,h.Transfers.Count);Assert.Equal(2,h.Transfers.Select(t=>t.Id).Distinct().Count());
        Assert.Contains(h.ClubCareer,s=>s.ClubId==a.Id);Assert.Contains(h.ClubCareer,s=>s.ClubId==b.Id);Assert.Contains(h.ClubCareer,s=>s.ClubId==c.Id);
        Assert.Equal(h.Career.GoalsScored,h.SeasonTotals.Sum(s=>s.Goals));Assert.Equal(h.Career.MatchesPlayed,h.SeasonTotals.Sum(s=>s.Games));
        Assert.Single(HistoryEngine.GetClubHistory(restored,a.Id)!.Transfers);
    }
    [Fact] public void FinishedMatchesFreezeActualKickoffCoachAndRecordOnlyPlayedScores()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);var m=w.Fixtures.First();
        var c=w.Clubs.Single(c=>c.Id==m.HomeClubId);var old=c.AiManagerId!.Value;
        CoachCareerEngine.CaptureLeadership(w,m);m.IsFinished=true;m.CurrentMinute=90;m.HomeGoals=4;m.AwayGoals=1;
        HistoryEngine.Observe(w);w.RoundNumber++;
        Assert.True(CoachCareerEngine.Dismiss(w,old,c,"AI_RELEGATION"));HistoryEngine.Observe(w);
        var h=HistoryEngine.GetClubHistory(w,c.Id)!;
        Assert.Equal(1,h.Total.Games);Assert.Equal(1,h.Total.Wins);Assert.Equal(4,h.Total.GoalsFor);
        Assert.Equal(m.Id,h.Records.BiggestWin!.MatchId);
        var coach=Assert.Single(h.Coaches,c=>c.CoachId==old);Assert.Equal(1,coach.Results.Games);Assert.Equal("AI_RELEGATION",coach.ExitReason);
        Assert.Equal(0,h.Coaches.Where(c=>c.CoachId!=old).Sum(c=>c.Results.Games));
        var future=w.Fixtures.First(f=>f.Id!=m.Id);future.HomeGoals=99;HistoryEngine.Observe(w);Assert.Single(w.History.Matches);
    }
    [Fact] public void ClosingASeasonStoresEveryDivisionBeforeMovementWithoutInventingAnUnplayedCupTitle()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);int strength=w.Players[0].Strength;
        foreach(var m in w.Fixtures.Where(f=>f.CompetitionType==CompetitionType.League)) {m.IsFinished=true;m.CurrentMinute=90;m.HomeGoals=1;m.AwayGoals=0;}
        var tables=Enumerable.Range(1,4).Select(i=>CompetitionAndSeasonEngine.GetDivisionStandings(w,(DivisionTier)i)).ToArray();
        var champion=tables[3][0].ClubId;var entry=new SeasonHistoryEntry{SeasonNumber=1,PromotedClubIds=[champion],RelegatedClubIds=[tables[0][7].ClubId]};
        var p=w.Players.First(p=>p.ClubId==champion);
        w.PlayerStatistics.Add(new(){PlayerId=p.Id,ClubId=champion,ClubName=HistoryEngine.ClubName(w,champion),Position=p.Position,Season=1,CompetitionType=CompetitionType.League,Division=DivisionTier.QuartaDivisao,MatchesPlayed=14,GoalsScored=12});
        w.Players[0].Strength=Math.Max(1,strength-1);HistoryEngine.CloseSeason(w,entry,tables);HistoryEngine.CloseSeason(w,entry,tables);
        var h=HistoryEngine.GetClubHistory(w,champion)!;
        Assert.Equal(DivisionTier.QuartaDivisao,h.Seasons.Single().Division);Assert.True(h.Seasons.Single().Promoted);Assert.Equal(1,h.Seasons.Single().FinalPosition);
        Assert.Equal(8,w.History.Titles.Count);Assert.DoesNotContain(w.History.Titles,t=>t.Competition=="CUP");
        var player=PlayerStatisticsEngine.GetHistory(w,p.Id)!;Assert.Contains(player.ClubTitlesWithParticipation,t=>t.ClubId==champion);
        Assert.Contains(player.ScoringRecognitions,s=>s.Scope=="division4"&&s.Goals==12);
        Assert.Equal(w.Players[0].Strength,PlayerStatisticsEngine.GetHistory(w,w.Players[0].Id)!.SeasonStrengths.Single().FinalStrength);
        Assert.Equal(strength,PlayerStatisticsEngine.GetHistory(w,w.Players[0].Id)!.SeasonStrengths.Single().InitialStrength);
    }
    [Fact] public void OldSaveKeepsReliableFactsButDoesNotInventMissingStrengthSeasonsPlacementsOrTransactions()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.History=new();w.SeasonNumber=4;w.RoundNumber=6;w.Fixtures.Clear();
        var p=w.Players[0];w.PlayerStatistics.Add(new(){PlayerId=p.Id,ClubId=p.ClubId!.Value,ClubName="Antigo clube",Season=2,CompetitionType=CompetitionType.League,Division=DivisionTier.QuartaDivisao,MatchesPlayed=7,GoalsScored=2});
        HistoryEngine.Observe(w);var h=HistoryEngine.GetClubHistory(w,p.ClubId.Value)!;
        Assert.NotEmpty(h.CoverageWarnings);Assert.DoesNotContain(h.Seasons,s=>s.Season==1||s.Season==3);Assert.All(h.Seasons,s=>Assert.Null(s.FinalPosition));
        var player=PlayerStatisticsEngine.GetHistory(w,p.Id)!;Assert.Null(player.SeasonStrengths.Single().InitialStrength);Assert.Equal(2,player.Career.GoalsScored);
        Assert.Empty(player.Transfers);Assert.Empty(player.ClubTitlesWithParticipation);Assert.Null(h.Records.LongestWinningRun);
    }
}
