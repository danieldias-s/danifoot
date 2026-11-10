using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class AssistRankingsTests
{
    // Expected hashes were generated from main before V3, with explicit fixed lineups.
    [Theory]
    [InlineData(199801, "3BBA97C1DA923234E130A008F15FA71130223E704B08A0FFD36FC107FD162EF0")]
    [InlineData(199802, "1C313DCA1ED977E289F81EBD4963F4E1A41D37C8D3DF5A48FF06FB7B8B66D919")]
    [InlineData(199803, "FF7F8272171ED59169B79D7CFC74789CA931A1103EAED73DF7384256672A471F")]
    [InlineData(199804, "BF03AEA7B696C7192729257331E87F04624A8C3FF49C92984C60030817FF4D65")]
    [InlineData(199805, "FE741A7A62CB5A3C0333E5F64DC31AB8B68430FFD8E65DE6CE7B32C02679C906")]
    [InlineData(199806, "B4947D4D06D60A72F4DD76BBAB2331174FB1F04D4CAA0548DCAA458ED90F830F")]
    [InlineData(199807, "BE3BD2FC6F0DF9BD5322D205A2781517ABD800F9B260E46552BA4D6E80C074C6")]
    [InlineData(199808, "030FDB792121C0E1C2B6D235696A82681E7B6F9A10BFAB5523E1990CB89A1931")]
    [InlineData(199809, "99C68C5744B751477D321EF216EDCF501A251175F1304EE3943BF4645D6A5221")]
    [InlineData(199810, "A30B89BE1CE772CAEE1A1181EAFCE26F5C0EBFCFBAC0BEB5D464BBB2E8D9C66E")]
    [InlineData(199811, "EB0E071CC5278836EB0BFEAC49914DAF48F56CF89A2F050E50233BBE59613855")]
    [InlineData(199812, "4E6B986E11CA8A7E1EC59CA2F6891FA9705DA93A92491F134E3418FD20274821")]
    [InlineData(199813, "198C08E27001B7E90F5E54E0641B619F1DA65797FB3703D7A9D49BDF44592D02")]
    [InlineData(199814, "4D22A77B891792F121C1ECFE19838143AA5F42912EB8DE8FE46A4D659B26881C")]
    [InlineData(199815, "96442C6270941EE77A9AF92BEEAD530CA0CE394C26300ECCF55A4A2A6121C009")]
    [InlineData(199816, "760B3B7B73C23D84C8579052D664D361F51315417FB00BC70AD2F6D7317CD25C")]
    [InlineData(199817, "5C23B4D5BA2B2951B4BA5FA4E94252D70EFF677A13005395AB2EBA6C89BFC820")]
    [InlineData(199818, "AAD34D9EA15BE36C0912C01070E0C2C86F2E5DA1B0BA6A210F5A37E0EA7AF3C6")]
    [InlineData(199819, "A5882441DC4A02AE3F735DDD3143CFF219912FC2CABAD19B3B86955AB0C944ED")]
    [InlineData(199820, "BBB4E47FDDF75B4F4E641614A86D9B3A0C013E2EA1BC5F842944298E9BF6B1B5")]
    public void FixedLineupsPreservePreV3MatchPhysicsAndAssistObservation(int seed, string expected)
        => Assert.Equal(expected, AssistOutcomeProbe.FixedLineupHash(seed));

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

    [Fact]
    public void PlayerPanelStatisticsAndHistoryProvideExactNineClassicMetricsAndPreserveDivisionHistoryAndInjuryOccurrences()
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        w.PlayerStatisticsVersion = 9;
        w.SeasonNumber = 3;
        var club = w.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var player = w.Players.First(p => p.ClubId == club.Id);

        // Season 1 in 4th Division: 40 matches, 20 goals, 10 assists, 1 red, 1 injury
        var matchS1 = Guid.NewGuid();
        var bucketS1 = new PlayerCompetitionStatistics
        {
            PlayerId = player.Id,
            ClubId = club.Id,
            Position = player.Position,
            Season = 1,
            CompetitionType = CompetitionType.League,
            Division = DivisionTier.QuartaDivisao,
            MatchesPlayed = 40,
            GoalsScored = 20,
            Assists = 10,
            RedCards = 1,
            Injuries = 1,
            HasIncompleteAssists = false
        };
        bucketS1.AppliedMatchIds.Add(matchS1);
        w.PlayerStatistics.Add(bucketS1);

        // Season 2 in 3rd Division: 44 matches, 15 goals, 8 assists, 1 red, 0 injuries
        w.PlayerStatistics.Add(new PlayerCompetitionStatistics
        {
            PlayerId = player.Id,
            ClubId = club.Id,
            Position = player.Position,
            Season = 2,
            CompetitionType = CompetitionType.League,
            Division = DivisionTier.TerceiraDivisao,
            MatchesPlayed = 44,
            GoalsScored = 15,
            Assists = 8,
            RedCards = 1,
            Injuries = 0,
            HasIncompleteAssists = false
        });

        // Season 3 back in 4th Division + Cup: 40 league (17 goals, 8 assists) + 4 cup (4 goals, 0 assists)
        var matchS3 = Guid.NewGuid();
        var bucketS3 = new PlayerCompetitionStatistics
        {
            PlayerId = player.Id,
            ClubId = club.Id,
            Position = player.Position,
            Season = 3,
            CompetitionType = CompetitionType.League,
            Division = DivisionTier.QuartaDivisao,
            MatchesPlayed = 40,
            GoalsScored = 17,
            Assists = 8,
            RedCards = 1,
            Injuries = 1,
            HasIncompleteAssists = false
        };
        bucketS3.AppliedMatchIds.Add(matchS3);
        w.PlayerStatistics.Add(bucketS3);
        w.PlayerStatistics.Add(new PlayerCompetitionStatistics
        {
            PlayerId = player.Id,
            ClubId = club.Id,
            Position = player.Position,
            Season = 3,
            CompetitionType = CompetitionType.CupQuarterFinal,
            Division = null,
            MatchesPlayed = 4,
            GoalsScored = 4,
            Assists = 0,
            RedCards = 0,
            Injuries = 0,
            HasIncompleteAssists = false
        });

        // Set missed rounds/days to large numbers to verify they are NOT treated as injury count
        player.InjuryRoundsRemaining = 9;
        player.InjuryDaysRemaining = 21;
        player.ForceInjuries.Add(new() { MatchId = matchS1, Season = 1, Round = 5 });
        player.ForceInjuries.Add(new() { MatchId = matchS3, Season = 3, Round = 12 });

        var panelInDiv4 = PlayerStatisticsEngine.GetPanelStatistics(w, player);
        Assert.Equal(128, panelInDiv4.CareerMatches);
        Assert.Equal(21, panelInDiv4.SeasonGoals);
        Assert.Equal(8, panelInDiv4.SeasonAssists);
        Assert.Equal(DivisionTier.QuartaDivisao, panelInDiv4.CurrentDivisionTier);
        Assert.Equal("4ª Divisão", panelInDiv4.CurrentDivisionDisplay);
        Assert.Equal(37, panelInDiv4.CurrentDivisionGoals); // 20 (S1) + 17 (S3)
        Assert.Equal(18, panelInDiv4.CurrentDivisionAssists); // 10 (S1) + 8 (S3)
        Assert.Equal(56, panelInDiv4.CareerGoals); // 20 + 15 + 17 + 4
        Assert.Equal(26, panelInDiv4.CareerAssists); // 10 + 8 + 8
        Assert.Equal(3, panelInDiv4.CareerRedCards);
        Assert.Equal(2, panelInDiv4.CareerInjuries); // 2 episodes, NOT 9 missed rounds

        // When club changes to 3rd Division, 3rd Division totals are shown and 4th Division totals remain preserved
        club.DivisionTier = DivisionTier.TerceiraDivisao;
        var panelInDiv3 = PlayerStatisticsEngine.GetPanelStatistics(w, player);
        Assert.Equal(DivisionTier.TerceiraDivisao, panelInDiv3.CurrentDivisionTier);
        Assert.Equal("3ª Divisão", panelInDiv3.CurrentDivisionDisplay);
        Assert.Equal(15, panelInDiv3.CurrentDivisionGoals);
        Assert.Equal(8, panelInDiv3.CurrentDivisionAssists);
        Assert.Equal(37, panelInDiv3.DivisionGoals[nameof(DivisionTier.QuartaDivisao)]);
        Assert.Equal(18, panelInDiv3.DivisionAssists[nameof(DivisionTier.QuartaDivisao)]);

        // Returning to 4th Division restores the accumulated 4th Division totals
        club.DivisionTier = DivisionTier.QuartaDivisao;
        var history = PlayerStatisticsEngine.GetHistory(w, player.Id)!;
        Assert.Equal(128, history.CareerMatches);
        Assert.Equal(21, history.SeasonGoals);
        Assert.Equal(8, history.SeasonAssists);
        Assert.Equal(37, history.CurrentDivisionGoals);
        Assert.Equal(18, history.CurrentDivisionAssists);
        Assert.Equal(56, history.CareerGoals);
        Assert.Equal(26, history.CareerAssists);
        Assert.Equal(3, history.CareerRedCards);
        Assert.Equal(2, history.CareerInjuries);
    }
}
