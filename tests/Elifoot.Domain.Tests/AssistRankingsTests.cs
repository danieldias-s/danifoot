using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class AssistRankingsTests
{
    [Theory]
    [InlineData(199801, "18BAF93C53EA58432CEB5A67B9425FC14D944782BB41DFEE39D273969F120B59")]
    [InlineData(199802, "528B2FA73E2395CBB16E702681D41711EB097926637138F9E056BD1A434FA353")]
    [InlineData(199803, "5A42261530D4236B3C86F45195E78442BBF14C056B2DC7F5EDFD5E5A3160AAAD")]
    [InlineData(199804, "D48A62E50C9BBA5495CA833477CF60E81F509858A0CC61BEF743EFA197CC169A")]
    [InlineData(199805, "26F6201C8D743B2C78F54F70F638477F4FC570239176A7C402B75B09AA5BE1EB")]
    [InlineData(199806, "7C6DD5AF9503CDE43624EAC26A24A85291C54676EB862016B32514C94C9CCDE9")]
    [InlineData(199807, "679C95C9F3A7BD025CF79AEA0D3A58113FA35389DEA4420CBF670EA0FBED7305")]
    [InlineData(199808, "3101ED9932D2020C67BA4BCA33DE22D6751922C8B43277968B9FDA943523610E")]
    [InlineData(199809, "048A93578FF81CF261326E337735FAAB7254B183D9E18391162BCCBAC9356258")]
    [InlineData(199810, "2F4AA809756FDFD729251AA00FF67CEBA3DB05708A2EC559B95CF261F5E6EFE7")]
    [InlineData(199811, "A21EE80BE9198039521B9EE037107CE3C826CBBDC33E5ADC0B4006E3C77C785F")]
    [InlineData(199812, "5519779B39AE04D53C8B59B71BA6444AB89F1FEC70DC6200A3C35D98E21E96DE")]
    [InlineData(199813, "337E3634D584E389B030107E288D520637B53A0BD610736D251423F7A0CFECA2")]
    [InlineData(199814, "5A2DFCB6BB23FE7E4F8E210418E63646730C5778D46F6AC743E76E2B44C5836C")]
    [InlineData(199815, "76FB0280DA04C53794F4A283B6F8BFBCAD89CDCB7D70F75B8A6060C8E3B4B05B")]
    [InlineData(199816, "C0D0F68C30E1A4577E87C763AAC8D35910D389BCD7FFC4FC4DF9E7E69A291DDF")]
    [InlineData(199817, "AC7FAE7035136C230739EAA4C39BAFB5727D25DDDAA3A13F4114E8FF3288383C")]
    [InlineData(199818, "FC753A616D4F8723DD1BD16313D0A9A58C0CF8E611BA9A0257C7835496C4C953")]
    [InlineData(199819, "DD2986E2E2F87E4019B6E28322664B9BC906E805BBE3326F060B70EA98B0F498")]
    [InlineData(199820, "598FF812AE3D7C2ABF2F4E6DCE522302E44B1C0E9A180C5F7F9DE7E8E8C87A17")]
    public void RecordingAssistsPreservesThePreChangeSportingOutcome(int seed, string expected)
        => Assert.Equal(expected, AssistOutcomeProbe.Hash(seed));

    [Theory]
    [InlineData("scorers")]
    [InlineData("assists")]
    public void EveryPositionAccumulatesOnlyItsHistoricalDivisionAcrossSeasonsAndTransfers(string category)
    {
        var w = WorldSeedFactory.CreateSeededWorld(); w.PlayerStatisticsVersion = 9; w.SeasonNumber = 3;
        var selected = new[] { PlayerPosition.G, PlayerPosition.D, PlayerPosition.M, PlayerPosition.A }
            .Select(pos => w.Players.First(p => p.Position == pos)).ToList();
        foreach (var p in selected)
        {
            foreach (var (season, division, count, competition) in new[] {
                (1, DivisionTier.QuartaDivisao, 2, CompetitionType.League),
                (2, DivisionTier.QuartaDivisao, 3, CompetitionType.League),
                (3, DivisionTier.PrimeiraDivisao, 7, CompetitionType.League),
                (3, DivisionTier.PrimeiraDivisao, 11, CompetitionType.CupFinal) })
                w.PlayerStatistics.Add(new() {PlayerId=p.Id,Position=p.Position,ClubId=p.ClubId!.Value,
                    Season=season,Division=division,CompetitionType=competition,MatchesPlayed=5,
                    GoalsScored=count,Assists=count,HasIncompleteAssists=false});
            p.ClubId = w.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao).Id;
        }
        int Count(PlayerRankingRowDto row) => category == "assists" ? row.Assists : row.GoalsScored;
        var historical = PlayerStatisticsEngine.GetRankings(w,category,"division4","career");
        Assert.Equal(0,historical.MinimumMatches); Assert.Equal(4,historical.Rows.Count);
        Assert.Equal(new[] {PlayerPosition.G,PlayerPosition.D,PlayerPosition.M,PlayerPosition.A}.Order(),historical.Rows.Select(r=>r.Position).Order());
        Assert.All(historical.Rows,r=>Assert.Equal(5,Count(r)));
        Assert.All(PlayerStatisticsEngine.GetRankings(w,category,"division4","season",2).Rows,r=>Assert.Equal(3,Count(r)));
        Assert.Empty(PlayerStatisticsEngine.GetRankings(w,category,"division4","current").Rows);
        Assert.All(PlayerStatisticsEngine.GetRankings(w,category,"division1","career").Rows,r=>Assert.Equal(7,Count(r)));
        Assert.All(PlayerStatisticsEngine.GetRankings(w,category,"cup","career").Rows,r=>Assert.Equal(11,Count(r)));
        Assert.All(PlayerStatisticsEngine.GetRankings(w,category,"general","career").Rows,r=>Assert.Equal(23,Count(r)));
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        Assert.All(PlayerStatisticsEngine.GetRankings(restored,category,"division4","career").Rows,r=>Assert.Equal(5,Count(r)));
    }

    [Fact]
    public void AssistsFollowScorerTieBreaksWithoutTwentyMatchMinimum()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.PlayerStatisticsVersion=9;
        var ps=w.Players.Take(3).ToArray();
        for(int i=0;i<3;i++) w.PlayerStatistics.Add(new(){PlayerId=ps[i].Id,Season=1,CompetitionType=CompetitionType.League,
            Division=DivisionTier.PrimeiraDivisao,MatchesPlayed=i==0?3:2,Assists=4,HasIncompleteAssists=false});
        var ranking=PlayerStatisticsEngine.GetRankings(w,"assists");
        Assert.Equal(0,ranking.MinimumMatches);Assert.Empty(ranking.NotEligible);
        Assert.Equal(ps.Skip(1).Select(p=>p.Id).Order().Append(ps[0].Id),ranking.Rows.Select(r=>r.PlayerId));
    }

    [Fact]
    public void OfficialGoalCreditsOnlyAnOnPitchTeammateOnceAndNeverOwnGoalsOrLegacyGuesses()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.PlayerStatisticsVersion=9;
        var home=w.Clubs[0];var away=w.Clubs[1];
        var hs=w.Players.Where(p=>p.ClubId==home.Id).ToList();var aws=w.Players.Where(p=>p.ClubId==away.Id).ToList();
        var m=new MatchFixture{GameWorldId=w.Id,HomeClubId=home.Id,AwayClubId=away.Id,SeasonNumber=1,RoundNumber=1,
            CompetitionType=CompetitionType.League,DivisionTier=DivisionTier.QuartaDivisao,CurrentMinute=1,
            HomeActiveStarterIds=hs.Take(11).Select(p=>p.Id).ToList(),AwayActiveStarterIds=aws.Take(11).Select(p=>p.Id).ToList()};
        PlayerStatisticsEngine.CaptureLineup(m,w.Players.ToDictionary(p=>p.Id));
        var scorer=m.HomeActiveStarterIds[1];var assister=m.HomeActiveStarterIds[0];
        m.Events.Add(new(){Minute=10,EventType=MatchEventType.Goal,ClubId=home.Id,PrimaryPlayerId=scorer,AssistPlayerId=assister,HasAssistCoverage=true});
        m.Events.Add(new(){Minute=20,EventType=MatchEventType.Goal,ClubId=home.Id,PrimaryPlayerId=scorer,AssistPlayerId=assister,IsOwnGoal=true,HasAssistCoverage=true});
        m.Events.Add(new(){Minute=30,EventType=MatchEventType.Goal,ClubId=home.Id,PrimaryPlayerId=scorer});
        m.CurrentMinute=90;m.IsFinished=true;w.Fixtures.Add(m);
        PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);
        Assert.Equal(1,Assert.Single(PlayerStatisticsEngine.GetRankings(w,"assists").Rows).Assists);
        Assert.Contains(PlayerStatisticsEngine.GetRankings(w,"assists").CoverageWarnings,s=>s.Contains("não foram reconstruídas"));
    }
}
