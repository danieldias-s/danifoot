using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Xunit.Abstractions;

namespace Elifoot.Domain.Tests;

public class ClassicForce012CTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> CeilingRows => new[]
    {
        new object[] { DivisionTier.PrimeiraDivisao, new[] {50,50,50,46,44,42,40,39} },
        new object[] { DivisionTier.SegundaDivisao, new[] {38,37,36,34,32,30,28,27} },
        new object[] { DivisionTier.TerceiraDivisao, new[] {25,24,23,21,19,17,15,14} },
        new object[] { DivisionTier.QuartaDivisao, new[] {12,11,10,8,6,4,2,1} }
    };

    [Theory, MemberData(nameof(CeilingRows))]
    public void ApprovedCeilingsForAllEightPlaces(DivisionTier tier, int[] expected)
    {
        Assert.Equal(expected, Enumerable.Range(1, 8).Select(rank => ClassicForceEvolutionEngine.GrowthCeiling(tier, rank)));
    }

    [Fact]
    public void DurationHasExactCumulativeBoundariesAndLossMaximum()
    {
        var counts = new int[14];
        for (int roll = 0; roll < 200; roll++) counts[ClassicForceEvolutionEngine.DurationFromRoll(roll) - 2]++;
        Assert.Equal(new[] {26,26,24,24,22,20,18,12,8,6,5,4,3,2}, counts);
        Assert.Equal(160, counts.Take(7).Sum());
        Assert.Throws<ArgumentOutOfRangeException>(() => ClassicForceEvolutionEngine.DurationFromRoll(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClassicForceEvolutionEngine.DurationFromRoll(200));
        Assert.Equal(25, ClassicForceEvolutionEngine.MaximumLoss(50, 15));
        Assert.Equal(0, ClassicForceEvolutionEngine.MaximumLoss(1, 15));
        Assert.True(Enumerable.Range(2,14).Select(d => ClassicForceEvolutionEngine.MaximumLoss(50,d)).SequenceEqual(
            Enumerable.Range(2,14).Select(d => ClassicForceEvolutionEngine.MaximumLoss(50,d)).Order()));
    }

    [Fact]
    public void FourthDivisionFifthPlaceGrowsExactlyOnceWithUnusedAndSuspendedPlayers()
    {
        var (w,p,club)=World(5,DivisionTier.QuartaDivisao,5);
        var suspended=new Player { Id=Guid.NewGuid(),ClubId=club.Id,Strength=1,SuspensionMatchesRemaining=1 };
        var atLimit=new Player { Id=Guid.NewGuid(),ClubId=club.Id,Strength=6 };
        var strong=new Player { Id=Guid.NewGuid(),ClubId=club.Id,Strength=25 };
        w.Players.AddRange([suspended,atLimit,strong]);
        var round=PolicyFixture(w,p,club,false,CompetitionType.League);
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[round]);
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[round]);
        Assert.Equal(new[] { 6,2,6,25 },w.Players.Select(x=>x.Strength));
        Assert.Equal(1,suspended.SuspensionMatchesRemaining);
    }

    [Theory]
    [InlineData(2)] [InlineData(8)] [InlineData(15)]
    public void DailyLossIsGradualExactAndRecoversOnVirtualDate(int duration)
    {
        var (w,p,club) = World(50, DivisionTier.QuartaDivisao, 1);
        int total = ClassicForceEvolutionEngine.MaximumLoss(50,duration);
        var injury = Injury(w,p,duration,total);
        for (int day = 1; day <= duration; day++)
        {
            ClassicForceEvolutionEngine.AdvanceToDate(w, injury.StartDate.AddDays(day));
            Assert.Equal(50 - total * day / duration, p.Strength);
            Assert.Equal(duration-day,p.InjuryDaysRemaining);
            if (day < duration) Assert.False(p.IsAvailableForMatch);
        }
        Assert.True(p.IsAvailableForMatch);
        Assert.Equal(total,w.PlayerStrengthHistory.Sum(e=>e.DailyInjuryLoss));
        Assert.Equal(MarketAndEconomyEngine.CalculateFairValue(p),p.MarketPrice);
        Assert.Equal(8000m,p.SalaryPerRound); // The negotiated salary survives injury.
        Assert.Equal(MarketAndEconomyEngine.CalculateFairSalaryPerRound(p),p.ReferenceSalaryPerRound);
        Assert.All(w.PlayerStrengthHistory,e=> {Assert.Equal(injury.Id,e.InjuryId);Assert.Equal(duration,e.InjuryDuration);Assert.Equal(w.Id,e.WorldId);});
        Assert.True(w.PlayerStrengthHistory.Count > 1);
        var history = JsonSerializer.Serialize(w.PlayerStrengthHistory);
        ClassicForceEvolutionEngine.AdvanceToDate(w,injury.EndDate);
        Assert.Equal(history,JsonSerializer.Serialize(w.PlayerStrengthHistory));
    }

    [Fact]
    public void PartialAndZeroInjuryLossDoNotBecomeMaximumOrAutomaticRestoration()
    {
        var (w,p,_) = World(50,DivisionTier.PrimeiraDivisao,1);
        var injury=Injury(w,p,15,7);
        ClassicForceEvolutionEngine.AdvanceToDate(w,injury.EndDate);
        Assert.Equal(43,p.Strength);
        Assert.Equal(7,injury.ActualLoss);
        Assert.Equal(0,p.InjuryDaysRemaining);
        var second=Injury(w,p,2,0);
        ClassicForceEvolutionEngine.AdvanceToDate(w,second.EndDate);
        Assert.Equal(43,p.Strength);
        Assert.Equal(2,p.ForceInjuries.Count);
        Assert.Equal(7,w.PlayerStrengthHistory.Sum(e=>e.DailyInjuryLoss));
    }

    [Fact]
    public void StrengthOneNeverFallsBelowOneEvenAcrossAnInterruptedSave()
    {
        var (w,p,_) = World(1,DivisionTier.QuartaDivisao,8);
        var injury=Injury(w,p,15,0);
        ClassicForceEvolutionEngine.AdvanceToDate(w,injury.StartDate.AddDays(8));
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        ClassicForceEvolutionEngine.AdvanceToDate(restored,injury.EndDate);
        Assert.Equal(1,restored.Players.Single(x=>x.Id==p.Id).Strength);
        Assert.Empty(restored.PlayerStrengthHistory);
        Assert.Equal(15,restored.Players.Single(x=>x.Id==p.Id).ForceInjuries.Single().ProcessedDays);
    }

    [Theory]
    [InlineData(DivisionTier.QuartaDivisao,1,1,12)]
    [InlineData(DivisionTier.TerceiraDivisao,8,5,14)]
    [InlineData(DivisionTier.TerceiraDivisao,1,24,25)]
    [InlineData(DivisionTier.SegundaDivisao,1,37,38)]
    [InlineData(DivisionTier.PrimeiraDivisao,1,49,50)]
    [InlineData(DivisionTier.PrimeiraDivisao,2,49,50)]
    [InlineData(DivisionTier.PrimeiraDivisao,3,40,50)]
    public void NonParticipantCanReachApprovedCeiling(DivisionTier tier,int rank,int initial,int ceiling)
    {
        var (w,p,club)=World(initial,tier,rank);
        for(int i=0;i<220;i++)
        {
            w.RoundNumber=i+1;
            var m=PolicyFixture(w,p,club,false,CompetitionType.League);
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
            Assert.InRange(p.Strength,initial,ceiling);
        }
        Assert.Equal(ceiling,p.Strength);
        Assert.All(w.PlayerStrengthHistory,e=>Assert.Equal("RESERVE_PROGRESS",e.ChangeReason));
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao,8,48)]
    [InlineData(DivisionTier.QuartaDivisao,1,40)]
    [InlineData(DivisionTier.PrimeiraDivisao,1,50)]
    public void CeilingNeverClampsAStrongNonparticipant(DivisionTier tier,int rank,int strength)
    {
        var (w,p,club)=World(strength,tier,rank);
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[PolicyFixture(w,p,club,false,CompetitionType.League)]);
        Assert.Equal(strength,p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
    }

    [Theory]
    [InlineData(CompetitionType.CupPreliminary1)] [InlineData(CompetitionType.CupPreliminary2)]
    [InlineData(CompetitionType.CupRoundOf64)] [InlineData(CompetitionType.CupRoundOf32)]
    [InlineData(CompetitionType.CupRoundOf16)] [InlineData(CompetitionType.CupQuarterFinal)]
    [InlineData(CompetitionType.CupSemiFinal)] [InlineData(CompetitionType.CupFinal)]
    public void EveryCupStageUsesLastValidLeagueRankIncludingClubsWithNoFixture(CompetitionType competition)
    {
        var (w,p,club)=World(1,DivisionTier.PrimeiraDivisao,1);
        var byeClub=w.Clubs[2];
        w.ClassicForce.CompletedLeaguePositions[byeClub.Id]=8;
        foreach(var team in w.Clubs)team.LeaguePlayed=1;
        byeClub.LeagueWins=99; // An unofficial change must not replace the cached Cup reference.
        var idle=new Player { Id=Guid.NewGuid(),ClubId=byeClub.Id,Strength=38 };
        w.Players.Add(idle);
        var m=PolicyFixture(w,p,club,false,competition);
        m.CurrentMinute=120;
        m.HomePenaltyGoals=5;m.AwayPenaltyGoals=4;
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
        Assert.Equal(2,p.Strength);
        Assert.Equal(39,idle.Strength);
        Assert.All(w.PlayerStrengthHistory,e=>Assert.Equal("RESERVE_PROGRESS",e.ChangeReason));
    }

    [Theory]
    [InlineData(CompetitionType.League)] [InlineData(CompetitionType.CupFinal)]
    public void ParticipantCanMaintainOrLoseOneButNeverReceivesReserveGain(CompetitionType competition)
    {
        var (w,p,club)=World(30,DivisionTier.PrimeiraDivisao,1);
        int losses=0;
        for(int seed=1;seed<=1000;seed++)
        {
            w.Seed=seed;w.RoundNumber=seed;p.Strength=30;
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[PolicyFixture(w,p,club,true,competition)]);
            Assert.InRange(p.Strength,29,30);
            if(p.Strength==29)losses++;
        }
        Assert.InRange(losses,440,560);
        output.WriteLine($"{competition}: {losses} perdas e {1000-losses} manutenções em 1000 sorteios elegíveis.");
        Assert.All(w.PlayerStrengthHistory,e=>Assert.Equal("PLAYED_MATCH_DECLINE",e.ChangeReason));
    }

    [Fact]
    public void ActualMinutesDistinguishUnusedBenchLateEntryAndZeroMinuteEntry()
    {
        var (w,p,club)=World(30,DivisionTier.PrimeiraDivisao,1);
        var m=PolicyFixture(w,p,club,false,CompetitionType.League);
        var starter=Guid.NewGuid();m.StatisticsInitialLineup.Add(new(starter,club.Id,PlayerPosition.A,30,true));
        m.Events.Add(new MatchEventRecord { Minute=80,EventType=MatchEventType.Substitution,ClubId=club.Id,PrimaryPlayerId=p.Id,SecondaryPlayerId=starter });
        Assert.Equal(10,ClassicForceEvolutionEngine.ParticipationMinutes(m)[p.Id]);
        m.Events[0].Minute=45;
        Assert.Equal(45,ClassicForceEvolutionEngine.ParticipationMinutes(m)[p.Id]);
        m.Events[0].Minute=90;
        Assert.Equal(0,ClassicForceEvolutionEngine.ParticipationMinutes(m)[p.Id]);
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
        Assert.InRange(p.Strength,29,30);
        m.Events.Clear();
        Assert.False(ClassicForceEvolutionEngine.ParticipationMinutes(m).ContainsKey(p.Id));
    }

    [Fact]
    public void AnAppearanceInTwoEventsTakesOneDecisionAndTransferUsesCurrentDivision()
    {
        var (w,p,club)=World(25,DivisionTier.PrimeiraDivisao,1);
        var reserve=new Player { Id=Guid.NewGuid(),ClubId=club.Id,Strength=25 };
        w.Players.Add(reserve);
        var destination=w.Clubs[2];
        destination.DivisionTier=DivisionTier.QuartaDivisao;
        p.ClubId=reserve.ClubId=destination.Id;
        for (int round=1;round<=32;round++)
        {
            w.RoundNumber=round;
            p.Strength=25;
            var first=PolicyFixture(w,p,club,true,CompetitionType.League);
            var second=PolicyFixture(w,p,club,true,CompetitionType.League);
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[first,second]);
            Assert.InRange(p.Strength,24,25);
        }
        Assert.Equal(25,reserve.Strength);
        Assert.NotEmpty(w.PlayerStrengthHistory);
        Assert.All(w.PlayerStrengthHistory,e=>Assert.Equal(DivisionTier.QuartaDivisao,e.Division));
        Assert.All(w.PlayerStrengthHistory.GroupBy(e=>e.Round), group=>Assert.Single(group));
    }

    [Fact]
    public void LeagueUsesNewPositionAfterAllScoresAreApplied()
    {
        var world=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        world.RoundNumber=3;
        ClassicForceEvolutionEngine.PrepareRound(world);
        var tier=DivisionTier.QuartaDivisao;
        var clubs=world.Clubs.Where(c=>c.DivisionTier==tier).ToList();
        for (int i=0;i<clubs.Count;i++) { clubs[i].LeaguePlayed=9; clubs[i].LeagueWins=9-i; }
        var target=clubs[4];
        var fourth=clubs[3];
        world.ClassicForce.CompletedLeaguePositions[target.Id]=5;
        var reserve=world.Players.First(p=>p.ClubId==target.Id && p.Position==PlayerPosition.G);
        reserve.Strength=6;
        var round=world.Fixtures.Where(m=>m.SeasonNumber==world.SeasonNumber && m.RoundNumber==3).ToList();
        foreach(var match in round)
        {
            FinishFixture(world,match);
            if(match.HomeClubId==target.Id || match.AwayClubId==fourth.Id)match.HomeGoals=20;
            else if(match.AwayClubId==target.Id || match.HomeClubId==fourth.Id)match.AwayGoals=20;
        }
        Assert.DoesNotContain(round.SelectMany(m=>m.StatisticsInitialLineup),p=>p.IsStarter && p.PlayerId==reserve.Id);
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world,round);
        Assert.Equal((4,8),ClassicForceEvolutionEngine.ClubContext(world,target));
        Assert.Equal(7,reserve.Strength);
    }

    [Fact]
    public void APreviouslySettledLegacyRoundIsSkippedAndItsNextCupRoundUsesPersistedStandings()
    {
        var (w,p,club)=World(5,DivisionTier.QuartaDivisao,5);
        w.ForceEvolutionMode=ForceEvolutionMode.LegacySeasonal;
        w.ClassicForce=new();
        foreach(var c in w.Clubs)c.LeaguePlayed=1;
        var old=PolicyFixture(w,p,club,false,CompetitionType.CupPreliminary1);
        old.CareerResultsApplied=true;
        w.Fixtures.Add(old);
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,[old]);
        Assert.Equal(5,p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);

        w.RoundNumber++;
        var next=PolicyFixture(w,p,club,false,CompetitionType.CupPreliminary2);
        w.Fixtures.Add(next);
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,[next]);
        Assert.Equal(6,p.Strength);
        Assert.Equal(5,w.ClassicForce.CompletedLeaguePositions[club.Id]);
    }

    [Fact]
    public void SameInjuryEpisodeNeverAlsoChargesParticipationLoss()
    {
        var (w,p,club)=World(50,DivisionTier.PrimeiraDivisao,1);
        for(int seed=1;seed<=100;seed++)
        {
            w.Seed=seed;w.RoundNumber=seed;
            var m=PolicyFixture(w,p,club,true,CompetitionType.League);
            m.HomeInjuredInMatchIds.Add(p.Id);
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
        }
        Assert.Equal(50,p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao,1,50,25,true)]
    [InlineData(DivisionTier.QuartaDivisao,1,50,25,false)]
    [InlineData(DivisionTier.TerceiraDivisao,8,5,2,true)]
    public void InjuredNonparticipantStillReceivesEligibleLeagueGrowth(DivisionTier tier,int rank,int strength,int total,bool grows)
    {
        int trialsWithGrowth=0;
        for(int seed=1;seed<=40;seed++)
        {
            var (w,p,club)=World(strength,tier,rank);w.Seed=seed;
            var injury=Injury(w,p,15,total);
            for(int day=1;day<=15;day++)
            {
                ClassicForceEvolutionEngine.AdvanceToDate(w,injury.StartDate.AddDays(day));
                if(day is 3 or 7 or 10 or 14)
                {
                    w.RoundNumber=day;
                    ClassicForceEvolutionEngine.ApplyFinishedRound(w,[PolicyFixture(w,p,club,false,CompetitionType.League)]);
                }
            }
            Assert.Equal(total,w.PlayerStrengthHistory.Sum(e=>e.DailyInjuryLoss));
            int gains=w.PlayerStrengthHistory.Sum(e=>e.ReserveGrowth);
            Assert.Equal(strength-total+gains,p.Strength);
            if(gains>0)trialsWithGrowth++;
            if(!grows)Assert.Equal(0,gains);
        }
        if(grows)Assert.True(trialsWithGrowth>0);
        else Assert.Equal(0,trialsWithGrowth);
    }

    [Fact]
    public void ReprocessingAndReloadPreserveEventsAndDisableOldSeasonalPolicy()
    {
        var (w,p,club)=World(1,DivisionTier.PrimeiraDivisao,1);
        var m=PolicyFixture(w,p,club,false,CompetitionType.League);
        ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
        string state=JsonSerializer.Serialize(w);
        var restored=JsonSerializer.Deserialize<GameWorld>(state)!;
        ClassicForceEvolutionEngine.ApplyFinishedRound(restored,[m]);
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(restored);
        Assert.Equal(state,JsonSerializer.Serialize(restored));
        Assert.Empty(restored.EvolvedPlayerStrengthSeasons);
    }

    [Fact]
    public void OfficialRoundWaitsForAllResultsAndCapturesBothHalvesBeforePromotion()
    {
        var w=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        foreach(int playedBefore in new[]{6,13})
        {
            w.RoundNumber=Enumerable.Range(1,w.TotalRoundsInSeason)
                .Where(r=>CompetitionAndSeasonEngine.GetCompetitionTypeForRound(w,r)==CompetitionType.League).ElementAt(playedBefore);
            ClassicForceEvolutionEngine.PrepareRound(w);
            foreach(var club in w.Clubs.Where(c=>c.DivisionTier!=DivisionTier.Distrital))club.LeaguePlayed=playedBefore;
            var matches=w.Fixtures.Where(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber).ToList();
            foreach(var m in matches)FinishFixture(w,m);
            matches[^1].IsFinished=false;
            var before=w.Clubs.Select(c=>c.LeaguePlayed).ToArray();
            CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,matches.Take(1).ToList());
            Assert.Equal(before,w.Clubs.Select(c=>c.LeaguePlayed));
            matches[^1].IsFinished=true;
            CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,matches);
            CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,matches);
        }
        Assert.Equal(64,w.ClassicForce.TurnSnapshots.Count);
        Assert.All(w.ClassicForce.TurnSnapshots,s=>Assert.Equal(ClassicForceEvolutionEngine.GrowthCeiling(s.Division,s.LeaguePosition),s.GrowthCeiling));
        var snapshot=JsonSerializer.Serialize(w.ClassicForce.TurnSnapshots);
        var promotedClub=CompetitionAndSeasonEngine.GetDivisionStandings(w,DivisionTier.SegundaDivisao)[0].ClubId;
        var promotedPlayer=w.Players.First(p=>p.ClubId==promotedClub);promotedPlayer.Strength=50;
        var transitionInjury=Injury(w,promotedPlayer,2,1);
        CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);
        Assert.All(w.PlayerStrengthHistory.Where(e=>e.InjuryId==transitionInjury.Id),e=>{
            Assert.Equal(DivisionTier.PrimeiraDivisao,e.Division);Assert.Equal(4,e.LeaguePosition);Assert.Equal(46,e.GrowthCeiling);
        });
        Assert.Equal(49,promotedPlayer.Strength);
        Assert.Equal(snapshot,JsonSerializer.Serialize(w.ClassicForce.TurnSnapshots));
        Assert.Empty(w.ClassicForce.CompletedLeaguePositions);
        Assert.All(w.Clubs.Where(c=>c.DivisionTier!=DivisionTier.Distrital),c=>Assert.Equal(
            ClassicForceEvolutionEngine.GrowthCeiling(c.DivisionTier,4),ClassicForceEvolutionEngine.ClubContext(w,c).Ceiling));
    }

    [Fact]
    public void InjuryClockIgnoresWallTimeAndCannotAdvanceDuringLiveMatch()
    {
        var (w,p,_)=World(50,DivisionTier.PrimeiraDivisao,1);
        Injury(w,p,15,25);
        w.Fixtures.Add(new MatchFixture { SeasonNumber=w.SeasonNumber,CurrentMinute=20,IsFinished=false });
        Assert.Throws<InvalidOperationException>(()=>ClassicForceEvolutionEngine.AdvanceToDate(w,w.ClassicForce.GameDate.AddDays(1)));
        Assert.Equal(50,p.Strength);
    }

    [Fact]
    public void DurationAndLossLotteryCanReachMaximumAndAlsoChooseSmallerLosses()
    {
        var (w,p,club)=World(50,DivisionTier.PrimeiraDivisao,1);
        var m=PolicyFixture(w,p,club,true,CompetitionType.League);m.CurrentMinute=20;
        bool maximum=false,smaller=false;
        for(int seed=1;seed<=20000&&!maximum;seed++)
        {
            p.InjuryDaysRemaining=0;p.ForceInjuries.Clear();
            var episode=ClassicForceEvolutionEngine.RegisterInjury(m,club,p,seed);
            Assert.InRange(episode.TotalLoss,0,ClassicForceEvolutionEngine.MaximumLoss(50,episode.Duration));
            smaller|=episode.TotalLoss<ClassicForceEvolutionEngine.MaximumLoss(50,episode.Duration);
            maximum|=episode.Duration==15&&episode.TotalLoss==25;
            Assert.Equal(50,p.Strength);
        }
        Assert.True(maximum&&smaller);
    }

    [Fact]
    public void InjuryTransferKeepsInitialLossBudgetAndHistoricalClubs()
    {
        var w=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        var seller=w.Clubs.First(c=>c.DivisionTier==DivisionTier.PrimeiraDivisao);
        var buyer=w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao);
        var p=w.Players.First(p=>p.ClubId==seller.Id&&p.Position==PlayerPosition.A);p.Strength=50;
        var episode=Injury(w,p,15,25);
        ClassicForceEvolutionEngine.AdvanceToDate(w,episode.StartDate.AddDays(3));
        int beforeTransfer=p.Strength;
        var transfer=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,seller.Id,p.Id,100_000m,"012C test","012c-transfer");
        Assert.True(transfer.Success,transfer.ErrorMessage);
        Assert.Equal(beforeTransfer,p.Strength);
        ClassicForceEvolutionEngine.AdvanceToDate(w,episode.EndDate);
        Assert.Equal(25,p.Strength);
        Assert.Equal(50,episode.InitialForce);
        Assert.Equal(25,w.PlayerStrengthHistory.Where(e=>e.PlayerId==p.Id).Sum(e=>e.DailyInjuryLoss));
        Assert.Contains(w.PlayerStrengthHistory,e=>e.PlayerId==p.Id&&e.ClubId==seller.Id);
        Assert.Contains(w.PlayerStrengthHistory,e=>e.PlayerId==p.Id&&e.ClubId==buyer.Id);
    }

    [Fact]
    public void CalendarPostponesWholeRoundRatherThanFieldingAnInjuredPlayer()
    {
        var w=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        w.RoundNumber=3;ClassicForceEvolutionEngine.PrepareRound(w);
        var club=w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao);
        var keep=w.Players.Where(p=>p.ClubId==club.Id&&p.Position!=PlayerPosition.G).Take(10).Select(p=>p.Id)
            .Append(w.Players.First(p=>p.ClubId==club.Id&&p.Position==PlayerPosition.G).Id).ToHashSet();
        w.Players.RemoveAll(p=>p.ClubId==club.Id&&!keep.Contains(p.Id));
        var injured=w.Players.First(p=>p.ClubId==club.Id&&p.Position!=PlayerPosition.G);
        var episode=Injury(w,injured,8,2);
        ClassicForceEvolutionEngine.PrepareRound(w);
        Assert.Equal(episode.EndDate,w.ClassicForce.GameDate);
        Assert.True(injured.IsAvailableForMatch);
        Assert.Single(w.ClassicForce.CalendarDelays);
        var date=w.ClassicForce.GameDate;
        ClassicForceEvolutionEngine.PrepareRound(w);
        Assert.Equal(date,w.ClassicForce.GameDate);
        Assert.Single(w.ClassicForce.CalendarDelays);
        var squad=w.Players.Where(p=>p.ClubId==club.Id).ToList();
        var (starters,bench)=AiManagerBrain.BuildStartersAndBench(squad,TacticalFormations.All["4-4-2"]);
        Assert.True(SquadValidator.ValidatePreMatchLineup("4-4-2",starters,bench,squad,true).IsValid);
    }

    [Fact]
    public void SameDateDailyLossPrecedesReserveGainAndReplayDoesNotRepeatEither()
    {
        bool found=false;
        for(int seed=1;seed<=100&&!found;seed++)
        {
            var (w,p,club)=World(50,DivisionTier.PrimeiraDivisao,1);w.Seed=seed;
            var episode=Injury(w,p,15,25);
            ClassicForceEvolutionEngine.AdvanceToDate(w,episode.StartDate.AddDays(1));
            var m=PolicyFixture(w,p,club,false,CompetitionType.League);
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
            if(w.PlayerStrengthHistory.All(e=>e.ReserveGrowth==0))continue;
            found=true;
            Assert.Equal(2,w.PlayerStrengthHistory.Count);
            Assert.Equal("INJURY_DAILY_LOSS",w.PlayerStrengthHistory[0].ChangeReason);
            Assert.Equal("RESERVE_PROGRESS",w.PlayerStrengthHistory[1].ChangeReason);
            Assert.Equal(w.PlayerStrengthHistory[0].GameDate,w.PlayerStrengthHistory[1].GameDate);
            Assert.Equal(w.PlayerStrengthHistory[0].StrengthAfter,w.PlayerStrengthHistory[1].StrengthBefore);
            string before=JsonSerializer.Serialize(w);
            var restored=JsonSerializer.Deserialize<GameWorld>(before)!;
            ClassicForceEvolutionEngine.AdvanceToDate(restored,restored.ClassicForce.GameDate);
            ClassicForceEvolutionEngine.ApplyFinishedRound(restored,[m]);
            Assert.Equal(before,JsonSerializer.Serialize(restored));
        }
        Assert.True(found);
    }

    [Fact]
    public void MissingModeOnOldSaveRemainsLegacyWithoutAutomaticConversion()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Players[0].Strength=1;w.Players[0].SeasonGoals=6;
        var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(w))!.AsObject();
        node.Remove("ForceEvolutionMode");node.Remove("ClassicForce");
        var loaded=JsonSerializer.Deserialize<GameWorld>(node.ToJsonString())!;
        Assert.Equal(ForceEvolutionMode.LegacySeasonal,loaded.ForceEvolutionMode);
        ClassicForceEvolutionEngine.PrepareRound(loaded);
        Assert.Equal(0,loaded.ClassicForce.CalendarSeason);
        PlayerStrengthEvolutionEngine.ApplySeasonEnd(loaded);
        Assert.Equal(2,loaded.Players[0].Strength);
        Assert.All(loaded.PlayerStrengthHistory,e=>Assert.Equal(PlayerStrengthEvolutionEngine.Rule,e.Rule));
    }

    [Fact]
    public void UnknownRulesVersionIsRejectedInsteadOfSilentlyUsingAnotherFormula()
    {
        var (w,p,_)=World(25,DivisionTier.PrimeiraDivisao,1);
        w.ClassicForce.Version=2;
        Assert.Throws<InvalidOperationException>(()=>ClassicForceEvolutionEngine.PrepareRound(w));
        Assert.Throws<InvalidOperationException>(()=>PlayerStrengthEvolutionEngine.ApplySeasonEnd(w));
        Assert.Equal(25,p.Strength);
        Assert.Empty(w.PlayerStrengthHistory);
    }

    internal static (GameWorld World,Player Player,Club Club) World(int strength,DivisionTier tier,int rank)
    {
        var club=new Club { Name="Controlled club",DivisionTier=tier,LeagueWins=8-rank };
        var p=new Player { Id=WorldSeedFactory.CreateDeterministicGuid(199801,4,1),ClubId=club.Id,Strength=strength,Position=PlayerPosition.A };
        var clubs=new List<Club>{club};
        for(int i=0;i<7;i++)clubs.Add(new Club { DivisionTier=tier,DeterministicIndex=i+1,LeagueWins=i<rank-1?20+i:0 });
        if(rank==8)club.LeagueGoalsAgainst=1;
        var w=new GameWorld { ForceEvolutionMode=ForceEvolutionMode.ClassicAdapted,Clubs=clubs,Players=[p],RoundNumber=1 };
        ClassicForceEvolutionEngine.InitializeSeason(w);
        w.ClassicForce.CompletedLeaguePositions[club.Id]=rank;
        return(w,p,club);
    }

    internal static ForceInjury Injury(GameWorld w,Player p,int duration,int total)
    {
        var injury=new ForceInjury { Id=Guid.NewGuid(),MatchId=Guid.NewGuid(),Season=w.SeasonNumber,Round=w.RoundNumber,
            StartDate=w.ClassicForce.GameDate,Duration=duration,InitialForce=p.Strength,TotalLoss=total };
        p.ForceInjuries.Add(injury);p.InjuryDaysRemaining=duration;return injury;
    }

    private static MatchFixture PolicyFixture(GameWorld w,Player p,Club club,bool played,CompetitionType competition)
        =>new() { GameWorldId=w.Id,SeasonNumber=w.SeasonNumber,RoundNumber=w.RoundNumber,HomeClubId=club.Id,AwayClubId=w.Clubs[1].Id,
            CompetitionType=competition,CurrentMinute=90,IsFinished=true,ForceRulesVersion=1,GameDate=w.ClassicForce.GameDate,
            ForceRoster=[new(p.Id,club.Id)],StatisticsLineupCaptured=true,
            StatisticsInitialLineup=played?[new(p.Id,club.Id,p.Position,p.Strength,true)]:[] };

    internal static void FinishFixture(GameWorld w,MatchFixture m)
    {
        var players=w.Players.ToDictionary(p=>p.Id);
        var (hs,hb)=AiManagerBrain.BuildStartersAndBench(w.Players.Where(p=>p.ClubId==m.HomeClubId).ToList(),TacticalFormations.All["4-4-2"]);
        var (aws,awb)=AiManagerBrain.BuildStartersAndBench(w.Players.Where(p=>p.ClubId==m.AwayClubId).ToList(),TacticalFormations.All["4-4-2"]);
        m.HomeActiveStarterIds=hs;m.HomeBenchIds=hb;m.AwayActiveStarterIds=aws;m.AwayBenchIds=awb;
        PlayerStatisticsEngine.CaptureLineup(m,players);m.CurrentMinute=90;m.IsFinished=true;
    }
}
