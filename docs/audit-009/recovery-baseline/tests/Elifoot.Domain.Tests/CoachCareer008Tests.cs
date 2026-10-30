using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class CoachCareer008Tests
{
    public static (GameWorld W, HumanCoach H, Club Club, MatchFixture Match) Scenario(int morale = 70)
    {
        var w = WorldSeedFactory.CreateSeededWorld(); w.RoundNumber = 3;
        var club = w.Clubs[24]; var opponent = w.Clubs[25];
        var old = w.AiManagers.Single(a => a.Id == club.AiManagerId); old.CurrentClubId = null; club.AiManagerId = null;
        var h = new HumanCoach { Id = CoachCareerEngine.StableId("008-human"), GameWorldId = w.Id, Nickname = "Carreira008", CurrentClubId = club.Id, Career = new() { Morale = morale } };
        h.EmploymentHistory.Add(new() { ClubId = club.Id, ClubName = club.Name, StartSeason = 1, StartRound = 1 });
        club.HumanCoachId = h.Id; w.HumanCoaches.Add(h);
        club.Reputation = opponent.Reputation = 50;
        foreach (var p in w.Players.Where(p => p.ClubId == club.Id || p.ClubId == opponent.Id)) p.Strength = 30;
        var m = new MatchFixture { GameWorldId = w.Id, HomeClubId = club.Id, AwayClubId = opponent.Id,
            CompetitionType = CompetitionType.League, DivisionTier = DivisionTier.QuartaDivisao, SeasonNumber = 1, RoundNumber = 3, CurrentMinute = 90, IsFinished = true };
        w.Fixtures.Add(m); CoachCareerEngine.CaptureLeadership(w, m);
        return (w, h, club, m);
    }

    private static void Result(GameWorld w, MatchFixture m, int home, int away)
    {
        m.HomeGoals = home; m.AwayGoals = away;
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w, [m]);
    }

    [Theory]
    [InlineData(60,0,1,51,"AMARELA",false)]
    [InlineData(35,0,1,26,"VERMELHA",false)]
    [InlineData(20,0,1,11,"VERMELHA",true)]
    [InlineData(20,1,0,29,"VERMELHA",false)]
    [InlineData(55,1,0,64,"VERDE",false)]
    [InlineData(20,0,0,21,"VERMELHA",false)]
    public void MoraleBandsAndPreMatchRedDismissal(int before,int home,int away,int expected,string band,bool dismissed)
    {
        var (w,h,c,m)=Scenario(before);Result(w,m,home,away);
        Assert.Equal(expected,h.Career.Morale);Assert.Equal(band,w.CoachMoraleRules.Band(h.Career.Morale));Assert.Equal(dismissed,h.IsUnemployed);
        Assert.Single(h.Career.MoraleHistory);
        if(dismissed) {Assert.Null(c.HumanCoachId);Assert.NotNull(c.AiManagerId);Assert.Equal(1,h.Dismissals);}
        var points=h.CoachPoints;var dismissals=h.Dismissals;
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,[m]);
        if(dismissed) CompetitionAndSeasonEngine.DismissHumanCoach(w,h,c,"duplicate");
        if(dismissed) Assert.Equal(dismissals,h.Dismissals);
        Assert.Equal(points,h.CoachPoints);
    }

    [Fact]
    public void StrongFavoriteDemandsMoreThanUnderdog_AndRecentPoorFormMatters()
    {
        var (w,h,c,m)=Scenario();var opponent=w.Clubs[25];
        foreach(var p in w.Players.Where(p=>p.ClubId==c.Id))p.Strength=48;
        foreach(var p in w.Players.Where(p=>p.ClubId==opponent.Id))p.Strength=12;
        Result(w,m,0,1);var favorite=h.Career.Morale;
        var (v,k,d,n)=Scenario();foreach(var p in v.Players.Where(p=>p.ClubId==d.Id))p.Strength=12;
        foreach(var p in v.Players.Where(p=>p.ClubId==v.Clubs[25].Id))p.Strength=48;
        Result(v,n,0,1);Assert.True(k.Career.Morale>favorite);
        Assert.Equal(1,h.Career.WinlessMatches);
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao,4,2)] [InlineData(DivisionTier.SegundaDivisao,3,1.5)]
    [InlineData(DivisionTier.TerceiraDivisao,2,1)] [InlineData(DivisionTier.QuartaDivisao,1,.5)]
    public void ExactDecimalDivisionPoints(DivisionTier tier,int win,double draw)
    {
        Assert.Equal((decimal)win,CoachCareerEngine.LeaguePoints(tier,true,false));
        Assert.Equal((decimal)draw,CoachCareerEngine.LeaguePoints(tier,false,true));
        Assert.Equal(0m,CoachCareerEngine.LeaguePoints(tier,false,false));
    }

    [Theory]
    [InlineData(CompetitionType.CupRoundOf64,3)] [InlineData(CompetitionType.CupRoundOf32,4)]
    [InlineData(CompetitionType.CupRoundOf16,5)] [InlineData(CompetitionType.CupQuarterFinal,6)]
    [InlineData(CompetitionType.CupSemiFinal,7)] [InlineData(CompetitionType.CupFinal,8)]
    public void CupPhaseWinAndPenaltiesCreditOnce(CompetitionType phase,int points)
    {
        var(w,h,_,m)=Scenario();m.CompetitionType=phase;m.EnteredExtraTime=true;m.HomePenaltyGoals=4;m.AwayPenaltyGoals=3;m.WinnerClubId=m.HomeClubId;
        Result(w,m,0,0);Result(w,m,0,0);
        Assert.Equal((decimal)points,h.CoachPoints);Assert.Equal(1,h.TotalWins);Assert.Equal(0,h.TotalDraws);
        Assert.Equal(phase==CompetitionType.CupFinal?1:0,h.CupTitles);
        Assert.Equal(0m,CoachCareerEngine.CupPhasePoints(CompetitionType.CupPreliminary1));
    }

    [Fact]
    public void SixCupWinsEqual33AndTitleIsSeparate_NoByeTitle()
    {
        var(w,h,_,m)=Scenario();
        foreach(var phase in new[]{CompetitionType.CupRoundOf64,CompetitionType.CupRoundOf32,CompetitionType.CupRoundOf16,CompetitionType.CupQuarterFinal,CompetitionType.CupSemiFinal,CompetitionType.CupFinal})
        {
            var game=new MatchFixture { HomeClubId=m.HomeClubId,AwayClubId=m.AwayClubId,SeasonNumber=1,RoundNumber=3,CompetitionType=phase,CurrentMinute=90,IsFinished=true,WinnerClubId=m.HomeClubId };
            CoachCareerEngine.CaptureLeadership(w,game);Result(w,game,1,0);
        }
        Assert.Equal(33m,h.CoachPoints);Assert.Equal(1,h.CupTitles);Assert.Single(w.CoachTitles);
        var empty=WorldSeedFactory.CreateSeededWorld();CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(empty);
        Assert.Equal(0,empty.AiManagers.Sum(a=>a.CupTitles));
    }

    [Fact]
    public void CreditBelongsToKickoffCoachEvenAfterClubChanges_AndReplayReloadIsIdempotent()
    {
        var(w,h,c,m)=Scenario();h.CurrentClubId=null;c.HumanCoachId=null;
        var ai=w.AiManagers.First(a=>a.CurrentClubId==null);ai.CurrentClubId=c.Id;c.AiManagerId=ai.Id;
        Result(w,m,0,0);Assert.Equal(.5m,h.CoachPoints);Assert.Equal(0m,ai.CoachPoints);
        var loaded=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var before=JsonSerializer.Serialize(loaded);var game=loaded.Fixtures.Single(f=>f.Id==m.Id);
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(loaded,[game]);
        Assert.Equal(before,JsonSerializer.Serialize(loaded));
    }

    [Fact]
    public void EveryCompletedDivisionAwardsOneTitleToItsClosingMatchCoach()
    {
        var(w,h,_,manual)=Scenario();w.Fixtures.Remove(manual);
        var games=w.Fixtures.Where(m=>m.CompetitionType==CompetitionType.League).ToList();
        foreach(var g in games){CoachCareerEngine.CaptureLeadership(w,g);g.IsFinished=true;g.CurrentMinute=90;g.HomeGoals=g.AwayGoals=0;}
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,games);
        CoachCareerEngine.AwardCompletedLeagues(w);CoachCareerEngine.AwardCompletedLeagues(w);
        Assert.Equal(4,w.CoachTitles.Count(t=>t.TitleType=="LEAGUE"));Assert.Equal(1,h.LeagueTitles);
        Assert.Contains(w.CoachTitles,t=>t.Division==DivisionTier.QuartaDivisao&&t.CoachId==h.Id);
    }

    [Fact]
    public void RankingIsLexicographicAndTiesAreStableAcrossReloadAndUnemployment()
    {
        var w=WorldSeedFactory.CreateSeededWorld();
        var people=new[]{new HumanCoach{Id=CoachCareerEngine.StableId("A"),Nickname="A",LeagueTitles=1,CoachPoints=10},
            new HumanCoach{Id=CoachCareerEngine.StableId("B"),Nickname="B",CupTitles=10,CoachPoints=5000},
            new HumanCoach{Id=CoachCareerEngine.StableId("C"),Nickname="C",LeagueTitles=2,CupTitles=1,CoachPoints=50},
            new HumanCoach{Id=CoachCareerEngine.StableId("D"),Nickname="D",LeagueTitles=2,CupTitles=1,CoachPoints=60}};
        w.HumanCoaches.AddRange(people);
        var ranks=CompetitionAndSeasonEngine.GetClassicCoachRanking(w);
        Assert.True(ranks.FindIndex(r=>r.Name=="A")<ranks.FindIndex(r=>r.Name=="B"));
        Assert.True(ranks.FindIndex(r=>r.Name=="D")<ranks.FindIndex(r=>r.Name=="C"));
        Assert.All(people,p=>Assert.True(p.IsUnemployed));
        var loaded=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        Assert.Equal(ranks.Select(r=>r.CoachId),CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded).Select(r=>r.CoachId));
        people[1].CupTitles=people[0].CupTitles=1;people[1].LeagueTitles=people[0].LeagueTitles=1;people[0].CoachPoints=1;people[1].CoachPoints=999999;
        people[0].CupTitles=2;ranks=CompetitionAndSeasonEngine.GetClassicCoachRanking(w);
        Assert.True(ranks.FindIndex(r=>r.Name=="A")<ranks.FindIndex(r=>r.Name=="B"));
    }

    [Fact]
    public void UnemployedRecoveryIsGradualOncePerRoundAndNeverDeletesCareer()
    {
        var(w,h,c,_)=Scenario(20);h.CoachPoints=12.5m;h.LeagueTitles=2;
        Assert.True(CoachCareerEngine.Dismiss(w,h.Id,c,"RED_MORALE_LOSS"));
        CoachCareerEngine.RecoverUnemployed(w);Assert.Equal(20,h.Career.Morale);
        for(int r=4;r<=10;r++){w.RoundNumber=r;CoachCareerEngine.RecoverUnemployed(w);var morale=h.Career.Morale;CoachCareerEngine.RecoverUnemployed(w);Assert.Equal(morale,h.Career.Morale);}
        Assert.Equal(60,h.Career.Morale);Assert.Equal(12.5m,h.CoachPoints);Assert.Equal(2,h.LeagueTitles);
    }

    [Theory] [InlineData(20,false)] [InlineData(50,false)] [InlineData(70,true)]
    public void OnlyGreenHumanReceivesOpportunity(int morale,bool invited)
    {
        var(w,h,c,_)=Scenario(morale);h.CurrentClubId=null;c.HumanCoachId=null;
        foreach(var a in w.AiManagers)a.Career.Morale=0;
        var target=w.Clubs[28];CoachCareerEngine.OpenVacancy(w,target);CoachCareerEngine.ProcessVacancies(w);
        Assert.Equal(invited,w.JobInvitations.Any(i=>i.CoachId==h.Id&&!i.IsRejected&&!i.IsExpired));
        Assert.Contains("CANDIDATE_NOT_ELIGIBLE",CoachCareerEngine.EvaluateOpportunity(w,new(h),w.Clubs[0]).Reason);
    }

    [Fact]
    public void EliteLeaderRejectsLowerProjectButSmallLeaderCanAcceptGreatStructure()
    {
        var(w,h,old,_)=Scenario();old.HumanCoachId=null;
        var source=w.Clubs[0];source.HumanCoachId=h.Id;h.CurrentClubId=source.Id;
        source.Reputation=95;source.CashBalance=20_000_000;source.Stadium.BuiltCapacity=60000;source.LeagueWins=10;source.LeaguePlayed=10;
        Assert.False(CoachCareerEngine.EvaluateOpportunity(w,new(h),w.Clubs[28]).Eligible);
        source.Reputation=35;source.CashBalance=100_000;source.Stadium.BuiltCapacity=5000;
        var target=w.Clubs[1];target.Reputation=98;target.CashBalance=40_000_000;target.Stadium.BuiltCapacity=80000;
        foreach(var p in w.Players.Where(p=>p.ClubId==source.Id))p.Strength=20;
        foreach(var p in w.Players.Where(p=>p.ClubId==target.Id))p.Strength=48;
        Assert.True(CoachCareerEngine.EvaluateOpportunity(w,new(h),target).Eligible);
    }

    [Fact]
    public void SearchContinuesAfterAiRefusalAndPreservesPersonalityOnAcceptedMove()
    {
        var w=WorldSeedFactory.CreateSeededWorld();foreach(var a in w.AiManagers)a.Career.Morale=0;
        var elite=w.AiManagers.Single(a=>a.Id==w.Clubs[0].AiManagerId);elite.Career.Morale=90;elite.LeagueTitles=99;
        var candidate=w.AiManagers.Single(a=>a.Id==w.Clubs[31].AiManagerId);candidate.Career.Morale=80;
        var target=w.Clubs[24];target.CashBalance=30_000_000;target.Reputation=98;target.Stadium.BuiltCapacity=80000;
        var source=w.Clubs[31];source.CashBalance=100_000;source.Reputation=18;source.Stadium.BuiltCapacity=5000;
        var personality=(candidate.Identity,candidate.Pragmatism,candidate.Adaptability,candidate.FinancialAggressiveness,candidate.YouthPatience);
        var prefs=candidate.PreferredFormations.ToArray();
        CoachCareerEngine.OpenVacancy(w,target);var vacancy=target.CareerVacancyId;CoachCareerEngine.ProcessVacancies(w);
        Assert.Contains(w.JobInvitations,i=>i.CoachId==elite.Id&&i.IsRejected);
        Assert.Contains(w.JobInvitations,i=>i.CoachId==candidate.Id&&i.IsAccepted);
        Assert.Equal(target.Id,candidate.CurrentClubId);Assert.NotNull(source.AiManagerId);Assert.NotEqual(candidate.Id,source.AiManagerId);
        Assert.Equal(personality,(candidate.Identity,candidate.Pragmatism,candidate.Adaptability,candidate.FinancialAggressiveness,candidate.YouthPatience));
        Assert.Equal(prefs,candidate.PreferredFormations);var offers=w.JobInvitations.Count(i=>i.VacancyId==vacancy);
        CoachCareerEngine.ProcessVacancies(w);Assert.Equal(offers,w.JobInvitations.Count(i=>i.VacancyId==vacancy));
    }

    [Fact]
    public void DistrictRelegationDismissesRegardlessOfMorale_AndPromotedClubIsEligible()
    {
        var(w,h,c,_)=Scenario(100);foreach(var other in w.Clubs.Where(x=>x.DivisionTier==DivisionTier.QuartaDivisao&&x.Id!=c.Id))other.LeagueWins=10;
        h.LeagueTitles=1;h.CoachPoints=99.5m;
        var history=CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);
        Assert.Contains(c.Id,history.RelegatedClubIds);Assert.Equal(DivisionTier.Distrital,c.DivisionTier);Assert.True(h.IsUnemployed);
        Assert.Equal(1,h.Dismissals);Assert.Equal(1,h.LeagueTitles);Assert.Equal(99.5m,h.CoachPoints);Assert.NotNull(c.AiManagerId);
        Assert.Contains(w.Clubs,x=>history.PromotedClubIds.Contains(x.Id)&&x.DivisionTier==DivisionTier.QuartaDivisao&&x.HumanCoachId==null);
    }

    [Fact]
    public void LegacyMigrationPreservesAggregatesAndMarksFinishedGamesWithoutInventingHistory()
    {
        var(w,h,club,m)=Scenario();h.CoachPoints=42;h.LeagueTitles=3;h.CupTitles=2;w.CareerRulesVersion=0;m.CareerResultsApplied=false;
        h.CurrentClubId=null;club.HumanCoachId=null;
        var replacement=w.AiManagers.First(a=>a.CurrentClubId==null);replacement.CurrentClubId=club.Id;club.AiManagerId=replacement.Id;
        var oldJob=h.EmploymentHistory.Last();oldJob.EndSeason=1;oldJob.EndRound=3;oldJob.ReasonForLeaving="Vínculo legado encerrado";
        var invitation=new JobInvitation { GameWorldId=w.Id,CoachId=h.Id,ClubId=w.Clubs[28].Id,CreatedAtSeason=1,CreatedAtRound=3 };
        w.JobInvitations.Add(invitation);
        var legacy=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(w))!.AsObject();
        legacy.Remove("CareerRulesVersion");legacy.Remove("CoachMoraleRules");legacy.Remove("CoachTitles");
        foreach(var c in legacy["HumanCoaches"]!.AsArray())c!.AsObject().Remove("Career");
        foreach(var c in legacy["AiManagers"]!.AsArray())c!.AsObject().Remove("Career");
        foreach(var i in legacy["JobInvitations"]!.AsArray()) {i!.AsObject().Remove("VacancyId");i.AsObject().Remove("ExpiresAtOrdinal");}
        var loaded=JsonSerializer.Deserialize<GameWorld>(legacy.ToJsonString())!;CoachCareerEngine.EnsureMigrated(loaded);
        var coach=loaded.HumanCoaches.Single();Assert.Equal(42m,coach.CoachPoints);Assert.Equal(3,coach.LeagueTitles);Assert.Equal(2,coach.CupTitles);
        Assert.True(loaded.Fixtures.Single(g=>g.Id==m.Id).CareerResultsApplied);Assert.Empty(coach.Career.MoraleHistory);
        CoachCareerEngine.EnsureMigrated(loaded);Assert.Single(loaded.AuditLogs,a=>a.ActionType=="CAREER_MIGRATED"&&a.Description.Contains("008"));
        Assert.True(loaded.JobInvitations.Single().ExpiresAtOrdinal>CoachCareerEngine.Ordinal(loaded));
        var accept=CoachCareerEngine.Respond(loaded,coach.Id,invitation.Id,true);Assert.True(accept.Success,accept.Error);
        Assert.Equal(w.Clubs[28].Id,coach.CurrentClubId);Assert.Equal(42m,coach.CoachPoints);Assert.Equal(3,coach.LeagueTitles);
    }

    [Fact]
    public void DismissedAiCannotImmediatelyReappointItselfAsInterim()
    {
        var w=WorldSeedFactory.CreateSeededWorld();var club=w.Clubs[0];var id=club.AiManagerId!.Value;
        Assert.True(CoachCareerEngine.Dismiss(w,id,club,"AI_RELEGATION"));
        Assert.NotEqual(id,club.AiManagerId);Assert.Null(w.AiManagers.Single(a=>a.Id==id).CurrentClubId);
    }

    [Fact]
    public void MoraleUsesCompletedRoundTableRegardlessOfFixtureProcessingOrder()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.RoundNumber=3;
        foreach(var c in w.Clubs.Take(8)){c.Reputation=50;c.LeaguePlayed=2;}
        foreach(var p in w.Players.Where(p=>w.Clubs.Take(8).Any(c=>c.Id==p.ClubId)))p.Strength=30;
        var games=new List<MatchFixture>();
        for(int i=0;i<3;i++)
        {
            var m=new MatchFixture{HomeClubId=w.Clubs[i*2].Id,AwayClubId=w.Clubs[i*2+1].Id,IsFinished=true,
                CompetitionType=CompetitionType.League,DivisionTier=DivisionTier.PrimeiraDivisao,SeasonNumber=1,RoundNumber=3,CurrentMinute=90,
                HomeGoals=i==0?0:1,AwayGoals=i==0?1:0};
            CoachCareerEngine.CaptureLeadership(w,m);games.Add(m);
        }
        var copy=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var reverse=JsonSerializer.Deserialize<List<MatchFixture>>(JsonSerializer.Serialize(games))!;reverse.Reverse();
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,games);
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(copy,reverse);
        foreach(var a in w.AiManagers)Assert.Equal(a.Career.Morale,copy.AiManagers.Single(x=>x.Id==a.Id).Career.Morale);
    }

    [Fact]
    public void FinishedByeWithNoPlayedMinuteCannotAwardPointsOrCupTitle()
    {
        var(w,h,_,m)=Scenario();m.CurrentMinute=0;m.CompetitionType=CompetitionType.CupFinal;m.WinnerClubId=m.HomeClubId;
        Result(w,m,0,0);Assert.Equal(0m,h.CoachPoints);Assert.Equal(0,h.CupTitles);Assert.Empty(w.CoachTitles);
    }
}
