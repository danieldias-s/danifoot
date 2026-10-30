using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class PlayerRankings009Tests
{
    private const decimal Prize = 3_000_000m;
    private static Guid Id(int value) => new(value, 0, 0, new byte[8]);

    private static GameWorld World(int clubs = 2)
    {
        var w = new GameWorld
        {
            Id = Id(1_000_000), PlayerStatisticsVersion = 9, TotalClubs = clubs,
            TotalRoundsInSeason = 1, RoundNumber = 1
        };
        for (var i = 0; i < clubs; i++)
        {
            var club = new Club
            {
                Id = Id(10_000 + i), GameWorldId = w.Id, Name = $"Clube {i}",
                DivisionTier = (DivisionTier)(i % 4 + 1), CashBalance = 10_000_000m
            };
            w.Clubs.Add(club);
            foreach (var position in new[]
            {
                PlayerPosition.G, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D,
                PlayerPosition.M, PlayerPosition.M, PlayerPosition.M, PlayerPosition.M, PlayerPosition.A,
                PlayerPosition.A, PlayerPosition.G, PlayerPosition.D, PlayerPosition.M, PlayerPosition.A
            })
                w.Players.Add(new Player
                {
                    Id = Id(w.Players.Count + 1), GameWorldId = w.Id, ClubId = club.Id,
                    Name = $"Jogador {w.Players.Count + 1}", Position = position, Strength = 30,
                    Behavior = PlayerBehavior.FairPlay
                });
        }
        return w;
    }

    private static List<Player> Roster(GameWorld w, int club = 0) =>
        w.Players.Where(p => p.ClubId == w.Clubs[club].Id).ToList();

    private static MatchFixture Fixture(GameWorld w, CompetitionType competition = CompetitionType.League,
        DivisionTier? division = DivisionTier.PrimeiraDivisao, int season = 1, int minute = 90)
    {
        var home = Roster(w); var away = Roster(w, 1);
        var match = new MatchFixture
        {
            Id = Id(20_000 + w.Fixtures.Count), GameWorldId = w.Id, SeasonNumber = season,
            RoundNumber = 1, HomeClubId = w.Clubs[0].Id, AwayClubId = w.Clubs[1].Id,
            CompetitionType = competition, DivisionTier = competition == CompetitionType.League ? division : null,
            HomeActiveStarterIds = home.Take(11).Select(p => p.Id).ToList(),
            AwayActiveStarterIds = away.Take(11).Select(p => p.Id).ToList(),
            HomeBenchIds = home.Skip(11).Select(p => p.Id).ToList(),
            AwayBenchIds = away.Skip(11).Select(p => p.Id).ToList(), CurrentMinute = 1
        };
        PlayerStatisticsEngine.CaptureLineup(match, w.Players.ToDictionary(p => p.Id));
        Assert.True(match.StatisticsLineupCaptured);
        match.CurrentMinute = minute; match.IsFinished = true;
        w.Fixtures.Add(match);
        return match;
    }

    private static void Event(MatchFixture m, int minute, MatchEventType type, Guid club,
        Guid? primary = null, Guid? secondary = null, bool ownGoal = false) => m.Events.Add(new()
        {
            Minute = minute, EventType = type, ClubId = club, PrimaryPlayerId = primary,
            SecondaryPlayerId = secondary, IsOwnGoal = ownGoal
        });

    private static void Goal(MatchFixture m, int minute, Guid club, Guid? scorer, bool ownGoal = false)
    {
        Event(m, minute, MatchEventType.Goal, club, scorer, ownGoal: ownGoal);
        if (club == m.HomeClubId) m.HomeGoals++; else m.AwayGoals++;
    }

    private static void Sub(MatchFixture m, int minute, Guid club, Player incoming, Player outgoing)
    {
        Event(m, minute, MatchEventType.Substitution, club, incoming.Id, outgoing.Id);
        var home = club == m.HomeClubId;
        var active = home ? m.HomeActiveStarterIds : m.AwayActiveStarterIds;
        active.Remove(outgoing.Id); active.Add(incoming.Id);
        (home ? m.HomeSubstitutedInIds : m.AwaySubstitutedInIds).Add(incoming.Id);
        (home ? m.HomeSubstitutedOutIds : m.AwaySubstitutedOutIds).Add(outgoing.Id);
    }

    private static PlayerCompetitionStatistics Aggregate(GameWorld w, Player p, int matches = 20, int goals = 0,
        int conceded = 0, int yellow = 0, int red = 0, int season = 1,
        DivisionTier? division = DivisionTier.PrimeiraDivisao, CompetitionType competition = CompetitionType.League,
        Guid? club = null, bool incomplete = false)
    {
        var statistics = new PlayerCompetitionStatistics
        {
            PlayerId = p.Id, ClubId = club ?? p.ClubId ?? w.Clubs[0].Id, Position = p.Position,
            Season = season, CompetitionType = competition,
            Division = competition == CompetitionType.League ? division : null,
            MatchesPlayed = matches, GoalsScored = goals, GoalsConceded = conceded,
            YellowCards = yellow, RedCards = red, MinutesPlayed = matches * 90,
            HasIncompleteAppearances = incomplete
        };
        w.PlayerStatistics.Add(statistics);
        return statistics;
    }

    // A finished bye permits award criterion tests without pretending aggregate fixtures were played.
    // Event consolidation and real-minute telemetry are exercised separately below.
    private static GameWorld AwardWorld()
    {
        var w = World();
        w.Fixtures.Add(new MatchFixture
        {
            Id = Id(20_000), GameWorldId = w.Id, SeasonNumber = 1, RoundNumber = 1,
            HomeClubId = w.Clubs[0].Id, AwayClubId = w.Clubs[1].Id,
            IsFinished = true, CurrentMinute = 0
        });
        return w;
    }

    private static GameWorld Clone(GameWorld w) => JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
    private static PlayerCompetitionStatistics Statistics(GameWorld w, Player p) =>
        Assert.Single(w.PlayerStatistics, s => s.PlayerId == p.Id);
    private static PlayerRankingRowDto Ranking(GameWorld w, Player p, string category = "scorers",
        string scope = "general", string period = "career", int? season = null) =>
        Assert.Single(PlayerStatisticsEngine.GetRankings(w, category, scope, period, season).Rows, r => r.PlayerId == p.Id);

    [Fact]
    public void Scorers_TotalGoalsPrecedeAverage_ThenAveragePrecedesStableId()
    {
        var w = World(); var players = Roster(w);
        Aggregate(w, players[9], 50, 12); // More goals wins despite a lower average.
        Aggregate(w, players[10], 1, 11);
        Aggregate(w, players[14], 2, 11);
        var ranking = PlayerStatisticsEngine.GetRankings(w, "scorers");
        Assert.Equal(new[] { players[9].Id, players[10].Id, players[14].Id }, ranking.Rows.Select(r => r.PlayerId));
        Assert.Equal(new[] { 1, 2, 3 }, ranking.Rows.Select(r => r.Rank));
        Assert.Equal(0, ranking.MinimumMatches); Assert.Empty(ranking.NotEligible);
    }

    [Theory]
    [InlineData("general", 5, 15)]
    [InlineData("division1", 1, 1)]
    [InlineData("division2", 1, 2)]
    [InlineData("division3", 1, 3)]
    [InlineData("division4", 1, 4)]
    [InlineData("cup", 1, 5)]
    public void Scopes_KeepEachDivisionAndCupIndependent(string scope, int matches, int goals)
    {
        var w = World(); var p = Roster(w)[9];
        for (var i = 1; i <= 4; i++) Aggregate(w, p, 1, i, division: (DivisionTier)i);
        Aggregate(w, p, 1, 5, competition: CompetitionType.CupFinal);
        var row = Ranking(w, p, scope: scope);
        Assert.Equal(matches, row.MatchesPlayed); Assert.Equal(goals, row.GoalsScored);
        Assert.Equal(goals / (decimal)matches, row.Average);
    }

    [Theory]
    [InlineData(CompetitionType.CupPreliminary1)]
    [InlineData(CompetitionType.CupPreliminary2)]
    [InlineData(CompetitionType.CupRoundOf64)]
    [InlineData(CompetitionType.CupRoundOf32)]
    [InlineData(CompetitionType.CupRoundOf16)]
    [InlineData(CompetitionType.CupQuarterFinal)]
    [InlineData(CompetitionType.CupSemiFinal)]
    [InlineData(CompetitionType.CupFinal)]
    public void EveryOfficialCupPhase_ContributesOnceToCupAndGeneral(CompetitionType phase)
    {
        var w = World(); var p = Roster(w)[9]; var m = Fixture(w, phase);
        Goal(m, 12, m.HomeClubId, p.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(1, Ranking(w, p, scope: "cup").GoalsScored);
        Assert.Equal(1, Ranking(w, p).GoalsScored);
        Assert.Empty(PlayerStatisticsEngine.GetRankings(w, scope: "division1").Rows);
        Assert.Null(Statistics(w, p).Division);
    }

    [Theory]
    [InlineData(90)] [InlineData(105)] [InlineData(120)]
    public void RegulationAndExtraTimeGoals_Count_ShootoutAttemptsNeverCount(int minute)
    {
        var w = World(); var p = Roster(w)[9]; var keeper = Roster(w, 1)[0];
        var m = Fixture(w, CompetitionType.CupFinal, minute: 120); m.EnteredExtraTime = true;
        Goal(m, minute, m.HomeClubId, p.Id);
        Event(m, 120, MatchEventType.PenaltyShootoutGoal, m.HomeClubId, p.Id);
        Event(m, 120, MatchEventType.PenaltyShootoutMiss, m.HomeClubId, p.Id);
        Event(m, 70, MatchEventType.PenaltyMissed, m.HomeClubId, p.Id);
        m.HomePenaltyGoals = 7; m.AwayPenaltyGoals = 6;
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(1, Statistics(w, p).GoalsScored); Assert.Equal(120, Statistics(w, p).MinutesPlayed);
        Assert.Equal(1, Statistics(w, keeper).GoalsConceded);
        Assert.Equal(1, Ranking(w, p, scope: "cup").GoalsScored);
    }

    [Theory]
    [InlineData(false, 90, false)] [InlineData(false, 0, false)]
    [InlineData(true, 0, false)] [InlineData(true, 90, true)]
    public void Consolidation_RequiresFinishedMatchAndPositiveMinute(bool finished, int minute, bool applied)
    {
        var w = World(); var m = Fixture(w); var p = Roster(w)[9];
        Goal(m, 10, m.HomeClubId, p.Id); m.IsFinished = finished; m.CurrentMinute = minute;
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(applied, w.AppliedPlayerStatisticsMatches.Contains(m.Id));
        Assert.Equal(applied ? 22 : 0, w.PlayerStatistics.Count);
    }

    [Fact]
    public void CaptureLineup_IsImmutableAndIncludesBenchWithoutGivingBenchAppearances()
    {
        var w = World(); var m = Fixture(w); var home = Roster(w);
        var captured = JsonSerializer.Serialize(m.StatisticsInitialLineup);
        home[0].Position = PlayerPosition.A; home[0].Strength = 1;
        home[0].ClubId = w.Clubs[1].Id;
        m.HomeActiveStarterIds.Clear(); m.HomeBenchIds.Clear();
        PlayerStatisticsEngine.CaptureLineup(m, w.Players.ToDictionary(p => p.Id));
        Assert.Equal(captured, JsonSerializer.Serialize(m.StatisticsInitialLineup));
        Assert.Equal(22, m.StatisticsInitialLineup.Count(s => s.IsStarter));
        Assert.Equal(8, m.StatisticsInitialLineup.Count(s => !s.IsStarter));
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        var keeper = Statistics(w, home[0]);
        Assert.Equal(PlayerPosition.G, keeper.Position); Assert.Equal(w.Clubs[0].Id, keeper.ClubId);
        Assert.Equal(90, keeper.MinutesPlayed);
        Assert.DoesNotContain(w.PlayerStatistics, s => home.Skip(11).Any(p => p.Id == s.PlayerId));
    }

    [Fact]
    public void RealMinuteEngine_CapturesKickoffBeforeLaterRosterAdjustments()
    {
        var w = World(); var m = Fixture(w); var home = Roster(w); var players = w.Players.ToDictionary(p => p.Id);
        m.StatisticsLineupCaptured = false; m.StatisticsInitialLineup.Clear(); m.IsFinished = false; m.CurrentMinute = 1;
        MatchSimulationEngine.SimulateSingleMinute(m, w.Clubs[0], w.Clubs[1], players,
            ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, 199801);
        Assert.True(m.StatisticsLineupCaptured);
        Sub(m, 60, m.HomeClubId, home[14], home[9]);
        m.CurrentMinute = 90; m.IsFinished = true;
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(60, Statistics(w, home[9]).MinutesPlayed);
        Assert.Equal(30, Statistics(w, home[14]).MinutesPlayed);
        Assert.Contains(m.StatisticsInitialLineup, s => s.PlayerId == home[9].Id && s.IsStarter);
        Assert.Contains(m.StatisticsInitialLineup, s => s.PlayerId == home[14].Id && !s.IsStarter);
    }

    [Fact]
    public void GoalkeeperSubstitution_AssignsOnlyGoalsConcededWhileEachKeeperWasActive()
    {
        var w = World(); var home = Roster(w); var scorer = Roster(w, 1)[9]; var m = Fixture(w);
        Goal(m, 20, m.AwayClubId, scorer.Id); Goal(m, 59, m.AwayClubId, scorer.Id);
        Sub(m, 60, m.HomeClubId, home[11], home[0]);
        Goal(m, 61, m.AwayClubId, scorer.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal((1, 2, 60), (Statistics(w, home[0]).MatchesPlayed, Statistics(w, home[0]).GoalsConceded, Statistics(w, home[0]).MinutesPlayed));
        Assert.Equal((1, 1, 30), (Statistics(w, home[11]).MatchesPlayed, Statistics(w, home[11]).GoalsConceded, Statistics(w, home[11]).MinutesPlayed));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void SameMinuteEvents_UseOriginalOrder_AndDifferentMinutesAreReplayedChronologically(bool goalBeforeSub)
    {
        var w = World(); var home = Roster(w); var scorer = Roster(w, 1)[9]; var m = Fixture(w);
        // Intentionally insert minute 80 before minute 60.
        Goal(m, 80, m.AwayClubId, scorer.Id);
        if (goalBeforeSub) Goal(m, 60, m.AwayClubId, scorer.Id);
        Sub(m, 60, m.HomeClubId, home[11], home[0]);
        if (!goalBeforeSub) Goal(m, 60, m.AwayClubId, scorer.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(goalBeforeSub ? 1 : 0, Statistics(w, home[0]).GoalsConceded);
        Assert.Equal(goalBeforeSub ? 1 : 2, Statistics(w, home[11]).GoalsConceded);
        Assert.Equal(60, Statistics(w, home[0]).MinutesPlayed); Assert.Equal(30, Statistics(w, home[11]).MinutesPlayed);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void OwnGoalAndUnknownScorer_ChargeActiveGoalkeeperWithoutInventingIndividualGoal(bool ownGoal)
    {
        var w = World(); var home = Roster(w); var m = Fixture(w);
        Goal(m, 25, m.AwayClubId, ownGoal ? home[1].Id : null, ownGoal);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(1, Statistics(w, home[0]).GoalsConceded);
        Assert.Equal(0, w.PlayerStatistics.Sum(s => s.GoalsScored));
        Assert.DoesNotContain(w.PlayerStatistics, s => s.PlayerId == home[11].Id);
        if (ownGoal)
        {
            Assert.Empty(w.PlayerStatisticsCoverageWarnings);
            Assert.All(w.PlayerStatistics, s => Assert.False(s.HasIncompleteAppearances));
        }
        else
        {
            Assert.NotEmpty(w.PlayerStatisticsCoverageWarnings);
            Assert.All(w.PlayerStatistics, s => Assert.False(s.HasIncompleteAppearances));
            Assert.All(w.PlayerStatistics, s => Assert.True(s.HasIncompleteGoals));
            Assert.Empty(PlayerStatisticsEngine.AwardSeason(w, 1));
        }
        Assert.Equal(1, m.AwayGoals);
        Assert.Equal(0, m.HomeGoals);
    }

    [Theory]
    [InlineData("missing-id")]
    [InlineData("unknown-id")]
    [InlineData("benched-player")]
    [InlineData("opponent-id")]
    public void UnattributableOfficialGoal_PreservesScoreAndKnownKeeperWithoutInventingScorer(string association)
    {
        var w=World();var m=Fixture(w);var home=Roster(w);var away=Roster(w,1);
        Guid? author=association switch {"missing-id"=>null,"unknown-id"=>Id(999999),"benched-player"=>away[14].Id,_=>home[1].Id};
        Goal(m,61,m.AwayClubId,author);
        PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);
        Assert.Equal((0,1),(m.HomeGoals,m.AwayGoals));Assert.Equal(0,w.PlayerStatistics.Sum(s=>s.GoalsScored));
        Assert.Equal(1,Statistics(w,home[0]).GoalsConceded);Assert.Equal(90,Statistics(w,home[0]).MinutesPlayed);
        Assert.All(w.PlayerStatistics,s=>Assert.False(s.HasIncompleteAppearances));
        Assert.All(w.PlayerStatistics,s=>Assert.True(s.HasIncompleteGoals));
        Assert.DoesNotContain(w.PlayerStatistics,s=>s.PlayerId==away[14].Id);
        Assert.Empty(PlayerStatisticsEngine.AwardSeason(w,1));
    }

    [Fact]
    public void UnknownScorer_DoesNotDisqualifyKeeperWithTwentyVerifiedAppearances()
    {
        var w=World();var goalkeeper=Roster(w)[0];
        for(int game=0;game<20;game++){var m=Fixture(w);Goal(m,30,m.AwayClubId,null);PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);}
        var row=Assert.Single(PlayerStatisticsEngine.GetRankings(w,"goalkeepers").Rows,r=>r.PlayerId==goalkeeper.Id);
        Assert.Equal(20,row.MatchesPlayed);Assert.Equal(20,row.GoalsConceded);Assert.Equal(1m,row.Average);
    }

    [Theory]
    [InlineData(20,"Gol de pênalti durante a partida")]
    [InlineData(117,"Gol na prorrogação")]
    public void OfficialGoalWithVerifiedAuthor_IncludingPenaltyAndExtraTime_IsCountedExactly(int minute,string description)
    {
        var w=World();var scorer=Roster(w,1)[9];var m=Fixture(w,CompetitionType.CupFinal,minute:120);
        Goal(m,minute,m.AwayClubId,scorer.Id);m.Events[^1].Description=description;
        Event(m,120,MatchEventType.PenaltyShootoutGoal,m.AwayClubId,scorer.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);
        Assert.Equal(1,Ranking(w,scorer).GoalsScored);Assert.Equal(1,m.AwayGoals);
        Assert.Empty(w.PlayerStatisticsCoverageWarnings);
    }

    [Fact]
    public void UnusedKeeper_NeverReceivesAppearanceOrGoalsConceded()
    {
        var w = World(); var m = Fixture(w); var home = Roster(w);
        Goal(m, 30, m.AwayClubId, Roster(w, 1)[9].Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.DoesNotContain(w.PlayerStatistics, s => s.PlayerId == home[11].Id);
        Assert.Equal(1, Statistics(w, home[0]).GoalsConceded);
    }

    [Theory]
    [InlineData(60, 30)] [InlineData(90, 0)]
    public void SubstituteAppearance_CountsEvenWhenEnteredAtMinute90(int minute, int played)
    {
        var w = World(); var home = Roster(w); var m = Fixture(w);
        Sub(m, minute, m.HomeClubId, home[14], home[9]);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(1, Statistics(w, home[14]).MatchesPlayed);
        Assert.Equal(played, Statistics(w, home[14]).MinutesPlayed);
        Assert.Equal(minute, Statistics(w, home[9]).MinutesPlayed);
    }

    [Theory]
    [InlineData(MatchEventType.DirectRedCard, 0, 1)]
    [InlineData(MatchEventType.SecondYellowRedCard, 2, 1)]
    public void Dismissals_CountRealCardsAndStopMinutes(MatchEventType dismissal, int yellow, int red)
    {
        var w = World(); var defender = Roster(w)[1]; var m = Fixture(w);
        if (dismissal == MatchEventType.SecondYellowRedCard)
            Event(m, 15, MatchEventType.YellowCard, m.HomeClubId, defender.Id);
        Event(m, 60, dismissal, m.HomeClubId, defender.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m, m]);
        var s = Statistics(w, defender);
        Assert.Equal(yellow, s.YellowCards); Assert.Equal(red, s.RedCards);
        Assert.Equal(60, s.MinutesPlayed); Assert.Equal(1, s.MatchesPlayed);
    }

    [Fact]
    public void SentOffGoalkeeper_DoesNotReceiveLaterGoals()
    {
        var w = World(); var home = Roster(w); var m = Fixture(w); var scorer = Roster(w, 1)[9];
        Goal(m, 20, m.AwayClubId, scorer.Id);
        Event(m, 40, MatchEventType.DirectRedCard, m.HomeClubId, home[0].Id);
        Goal(m, 50, m.AwayClubId, scorer.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(1, Statistics(w, home[0]).GoalsConceded);
        Assert.Equal(40, Statistics(w, home[0]).MinutesPlayed);
        Assert.DoesNotContain(w.PlayerStatistics, s => s.PlayerId == home[11].Id);
    }

    [Theory]
    [InlineData("goalkeepers", PlayerPosition.G, 1, false, 19)]
    [InlineData("goalkeepers", PlayerPosition.G, 19, false, 1)]
    [InlineData("goalkeepers", PlayerPosition.G, 20, true, 0)]
    [InlineData("goalkeepers", PlayerPosition.G, 21, true, 0)]
    [InlineData("defenders", PlayerPosition.D, 1, false, 19)]
    [InlineData("defenders", PlayerPosition.D, 19, false, 1)]
    [InlineData("defenders", PlayerPosition.D, 20, true, 0)]
    [InlineData("defenders", PlayerPosition.D, 21, true, 0)]
    public void Eligibility_ChangesExactlyAt20Matches(string category, PlayerPosition position, int matches, bool eligible, int remaining)
    {
        var w = World(); var p = Roster(w).First(p => p.Position == position); Aggregate(w, p, matches);
        var ranking = PlayerStatisticsEngine.GetRankings(w, category);
        Assert.Equal(20, ranking.MinimumMatches);
        var row = Assert.Single(eligible ? ranking.Rows : ranking.NotEligible);
        Assert.Empty(eligible ? ranking.NotEligible : ranking.Rows);
        Assert.Equal(p.Id, row.PlayerId); Assert.Equal(remaining, row.MatchesRemaining);
        Assert.Equal(eligible ? 1 : 0, row.Rank);
    }

    [Theory]
    [InlineData("goalkeepers", PlayerPosition.G)]
    [InlineData("defenders", PlayerPosition.D)]
    public void Eligibility_IsComputedWithinDivisionCupAndSeason(string category, PlayerPosition position)
    {
        var w = World(); w.SeasonNumber = 2; var p = Roster(w).First(p => p.Position == position);
        Aggregate(w, p, 19, season: 2, division: DivisionTier.PrimeiraDivisao);
        Aggregate(w, p, 20, season: 2, division: DivisionTier.SegundaDivisao);
        Aggregate(w, p, 19, season: 2, competition: CompetitionType.CupFinal);
        Aggregate(w, p, 1, season: 1, division: DivisionTier.PrimeiraDivisao);
        Assert.Equal(59, Ranking(w, p, category).MatchesPlayed);
        Assert.Equal(58, Ranking(w, p, category, period: "current").MatchesPlayed);
        Assert.Equal(20, Ranking(w, p, category, "division1").MatchesPlayed);
        var division = PlayerStatisticsEngine.GetRankings(w, category, "division1", "current");
        Assert.Empty(division.Rows); Assert.Equal(1, Assert.Single(division.NotEligible).MatchesRemaining);
        Assert.Equal(20, Ranking(w, p, category, "division2", "current").MatchesPlayed);
        var cup = PlayerStatisticsEngine.GetRankings(w, category, "cup", "current");
        Assert.Empty(cup.Rows); Assert.Equal(1, Assert.Single(cup.NotEligible).MatchesRemaining);
        var old = PlayerStatisticsEngine.GetRankings(w, category, "general", "season", 1);
        Assert.Empty(old.Rows); Assert.Equal(19, Assert.Single(old.NotEligible).MatchesRemaining);
    }

    [Theory]
    [InlineData("goalkeepers", PlayerPosition.G)]
    [InlineData("defenders", PlayerPosition.D)]
    public void Eligibility_Aggregates20AcrossClubsAndCupPhasesWithinSelectedScope(string category, PlayerPosition position)
    {
        var w = World(); var p = Roster(w).First(p => p.Position == position);
        Aggregate(w, p, 10, club: w.Clubs[0].Id);
        Aggregate(w, p, 10, club: w.Clubs[1].Id);
        Aggregate(w, p, 9, competition: CompetitionType.CupPreliminary1);
        Aggregate(w, p, 11, competition: CompetitionType.CupFinal);
        Assert.Equal(20, Ranking(w, p, category, "division1").MatchesPlayed);
        Assert.Equal(20, Ranking(w, p, category, "cup").MatchesPlayed);
        Assert.Equal(40, Ranking(w, p, category).MatchesPlayed);
    }

    public static IEnumerable<object[]> FinishedMatchEligibilityCases()
    {
        foreach (var (category, position) in new[]
                 { ("goalkeepers", PlayerPosition.G), ("defenders", PlayerPosition.D) })
        foreach (var scope in new[] { "general", "division1", "division2", "division3", "division4", "cup" })
            yield return [category, position, scope];
    }

    [Theory]
    [MemberData(nameof(FinishedMatchEligibilityCases))]
    public void TwentiethFinishedAppearance_QualifiesWithinEachScopeAcrossStatisticalBuckets(
        string category, PlayerPosition position, string scope)
    {
        var w = World(); var p = Roster(w).First(p => p.Position == position);
        var division = scope.StartsWith("division") ? (DivisionTier)int.Parse(scope[^1..]) : DivisionTier.PrimeiraDivisao;
        for (var i = 1; i <= 20; i++)
        {
            var competition = scope == "cup"
                ? (i % 2 == 0 ? CompetitionType.CupFinal : CompetitionType.CupPreliminary1)
                : CompetitionType.League;
            var m = Fixture(w, competition, division);
            PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
            if (i == 19)
            {
                var notYet = PlayerStatisticsEngine.GetRankings(w, category, scope);
                Assert.DoesNotContain(notYet.Rows, r => r.PlayerId == p.Id);
                var row = Assert.Single(notYet.NotEligible, r => r.PlayerId == p.Id);
                Assert.Equal(19, row.MatchesPlayed); Assert.Equal(1, row.MatchesRemaining);
            }
        }
        var eligible = Ranking(w, p, category, scope);
        Assert.Equal(20, eligible.MatchesPlayed); Assert.Equal(1_800, eligible.MinutesPlayed);
        Assert.Equal(0, eligible.MatchesRemaining); Assert.Equal(0m, eligible.Average);
        var buckets = w.PlayerStatistics.Where(s => s.PlayerId == p.Id).ToList();
        Assert.Equal(scope == "cup" ? 2 : 1, buckets.Count);
        Assert.Equal(20, buckets.Sum(s => s.AppliedMatchIds.Count));
    }

    [Theory]
    [InlineData("goalkeepers", PlayerPosition.G)]
    [InlineData("defenders", PlayerPosition.D)]
    public void OnlyMatchesPlayedInCategoryPosition_CountTowardItsMinimum(string category, PlayerPosition position)
    {
        var w = World(); var p = Roster(w).First(p => p.Position == position);
        Aggregate(w, p, 19);
        var other = Aggregate(w, p, 50, season: 2); other.Position = PlayerPosition.M;
        var ranking = PlayerStatisticsEngine.GetRankings(w, category);
        Assert.Empty(ranking.Rows);
        Assert.Equal(19, Assert.Single(ranking.NotEligible).MatchesPlayed);
    }

    [Fact]
    public void Goalkeepers_LowerConcededAverageWins_EqualAveragePrefersMoreMatches()
    {
        var w = World(); var a = Roster(w)[0]; var b = Roster(w)[11]; var c = Roster(w, 1)[0];
        Aggregate(w, a, 20, conceded: 10); Aggregate(w, b, 40, conceded: 20);
        Aggregate(w, c, 50, conceded: 30);
        var ranking = PlayerStatisticsEngine.GetRankings(w, "goalkeepers");
        Assert.Equal(new[] { b.Id, a.Id, c.Id }, ranking.Rows.Select(r => r.PlayerId));
        Assert.Equal(.5m, ranking.Rows[0].Average);
    }

    [Fact]
    public void Defenders_DisciplineScorePrecedesRedRate_AndUsesActualCardCounts()
    {
        var w = World(); var defenders = Roster(w).Where(p => p.Position == PlayerPosition.D).ToList();
        Aggregate(w, defenders[0], 20, yellow: 3, red: 1); // 6/20, red 1/20.
        Aggregate(w, defenders[1], 20, yellow: 6);         // 6/20, red 0/20 wins.
        Aggregate(w, defenders[2], 20, yellow: 2);         // 2/20 wins overall.
        var ranking = PlayerStatisticsEngine.GetRankings(w, "defenders");
        Assert.Equal(new[] { defenders[2].Id, defenders[1].Id, defenders[0].Id }, ranking.Rows.Select(r => r.PlayerId));
        Assert.Equal(.3m, ranking.Rows[1].Average); Assert.Equal(.3m, ranking.Rows[1].YellowRate);
        Assert.Equal(.05m, ranking.Rows[2].RedRate);
        Assert.Equal(3, ranking.Rows[2].YellowCards); Assert.Equal(1, ranking.Rows[2].RedCards);
    }

    [Fact]
    public void Defenders_EquivalentDisciplineRedAndYellowRates_PreferMoreMatches()
    {
        var w = World(); var a = Roster(w)[1]; var b = Roster(w)[2];
        Aggregate(w, a, 20, yellow: 2, red: 1); Aggregate(w, b, 40, yellow: 4, red: 2);
        var ranking = PlayerStatisticsEngine.GetRankings(w, "defenders");
        Assert.Equal(new[] { b.Id, a.Id }, ranking.Rows.Select(r => r.PlayerId));
        Assert.Equal(ranking.Rows[0].Average, ranking.Rows[1].Average);
        Assert.Equal(ranking.Rows[0].RedRate, ranking.Rows[1].RedRate);
        Assert.Equal(ranking.Rows[0].YellowRate, ranking.Rows[1].YellowRate);
    }

    [Theory]
    [InlineData("goalkeepers", PlayerPosition.G)]
    [InlineData("defenders", PlayerPosition.D)]
    public void RationalAverageComparison_DistinguishesValuesThatRoundToSameDisplay(string category, PlayerPosition position)
    {
        var w = World(); var candidates = w.Players.Where(p => p.Position == position).Take(2).ToArray();
        // Products exceed Int32. Premature rounding would incorrectly favor the larger match count.
        Aggregate(w, candidates[0], 1_000_000, conceded: 1_000_001, yellow: 1_000_001);
        Aggregate(w, candidates[1], 1_000_001, conceded: 1_000_003, yellow: 1_000_003);
        var ranking = PlayerStatisticsEngine.GetRankings(w, category);
        Assert.Equal(candidates[0].Id, ranking.Rows[0].PlayerId);
        Assert.True(ranking.Rows[0].Average < ranking.Rows[1].Average);
        Assert.Equal(Math.Round(ranking.Rows[0].Average!.Value, 4), Math.Round(ranking.Rows[1].Average!.Value, 4));
    }

    [Fact]
    public void Defenders_RedRateTieBreakUsesExactRationalComparison()
    {
        var w = World(); var a = Roster(w)[1]; var b = Roster(w)[2];
        // Both discipline indices are exactly 3. Lower red rate wins, although both round to 0.0000.
        Aggregate(w, a, 1_000_000, yellow: 2_999_997, red: 1);
        Aggregate(w, b, 1_000_001, yellow: 2_999_997, red: 2);
        var rows = PlayerStatisticsEngine.GetRankings(w, "defenders").Rows;
        Assert.Equal(a.Id, rows[0].PlayerId); Assert.Equal(rows[0].Average, rows[1].Average);
        Assert.True(rows[0].RedRate < rows[1].RedRate);
        Assert.Equal(Math.Round(rows[0].RedRate, 4), Math.Round(rows[1].RedRate, 4));
    }

    [Fact]
    public void Scorers_EqualGoalsCompareExactAverageBeforeGuid()
    {
        var w = World(); var a = Roster(w)[9]; var b = Roster(w)[10];
        Aggregate(w, a, 1_000_001, 1); Aggregate(w, b, 1_000_000, 1);
        var rows = PlayerStatisticsEngine.GetRankings(w).Rows;
        Assert.Equal(b.Id, rows[0].PlayerId); Assert.True(rows[0].Average > rows[1].Average);
        Assert.Equal(Math.Round(rows[0].Average!.Value, 4), Math.Round(rows[1].Average!.Value, 4));
    }

    [Theory]
    [InlineData("scorers", PlayerPosition.A)]
    [InlineData("goalkeepers", PlayerPosition.G)]
    [InlineData("defenders", PlayerPosition.D)]
    public void Ties_AreStableByGuidAcrossInsertionOrderAndJsonReload(string category, PlayerPosition position)
    {
        var w = World(); var candidates = w.Players.Where(p => p.Position == position).Take(3).ToList();
        foreach (var p in candidates.AsEnumerable().Reverse()) Aggregate(w, p, 20, goals: 5, conceded: 4, yellow: 2, red: 1);
        var expected = candidates.Select(p => p.Id).Order().ToArray();
        Assert.Equal(expected, PlayerStatisticsEngine.GetRankings(w, category).Rows.Select(r => r.PlayerId));
        var loaded = Clone(w); loaded.Players.Reverse(); loaded.PlayerStatistics.Reverse();
        Assert.Equal(expected, PlayerStatisticsEngine.GetRankings(loaded, category).Rows.Select(r => r.PlayerId));
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao, DivisionTier.SegundaDivisao)]
    [InlineData(DivisionTier.SegundaDivisao, DivisionTier.PrimeiraDivisao)]
    [InlineData(DivisionTier.TerceiraDivisao, DivisionTier.QuartaDivisao)]
    [InlineData(DivisionTier.QuartaDivisao, DivisionTier.TerceiraDivisao)]
    public void PromotionAndRelegation_NeverReclassifyHistoricalCompetition(DivisionTier original, DivisionTier current)
    {
        var w = World(); var p = Roster(w)[9]; var m = Fixture(w, division: original);
        Goal(m, 10, m.HomeClubId, p.Id); PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        w.Clubs[0].DivisionTier = current;
        Assert.Equal(1, Ranking(w, p, scope: $"division{(int)original}").GoalsScored);
        Assert.Empty(PlayerStatisticsEngine.GetRankings(w, scope: $"division{(int)current}").Rows);
        Assert.Equal(original, Statistics(w, p).Division);
    }

    [Fact]
    public void TransferAfterKickoffAndBetweenGames_PreservesOriginalClubAndDivisionBuckets()
    {
        var w = World(); var p = Roster(w)[9]; var oldClub = w.Clubs[0]; var newClub = w.Clubs[1];
        var first = Fixture(w, division: DivisionTier.QuartaDivisao);
        Goal(first, 12, oldClub.Id, p.Id);
        p.ClubId = newClub.Id; // Consolidation must use kickoff snapshots, not current ownership.
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [first]);
        var second = Fixture(w, division: DivisionTier.PrimeiraDivisao);
        Goal(second, 20, newClub.Id, p.Id); PlayerStatisticsEngine.ApplyFinishedMatches(w, [second]);
        Assert.Equal(2, Ranking(w, p).GoalsScored); Assert.Equal(2, Ranking(w, p).MatchesPlayed);
        Assert.Equal(newClub.Id, Ranking(w, p).CurrentClubId);
        Assert.Equal(1, Ranking(w, p, scope: "division4").GoalsScored);
        Assert.Equal(1, Ranking(w, p, scope: "division1").GoalsScored);
        var history = Assert.IsType<PlayerHistoryDto>(PlayerStatisticsEngine.GetHistory(w, p.Id));
        Assert.Contains(history.Statistics, s => s.ClubId == oldClub.Id && s.Division == DivisionTier.QuartaDivisao);
        Assert.Contains(history.Statistics, s => s.ClubId == newClub.Id && s.Division == DivisionTier.PrimeiraDivisao);
        p.ClubId = null;
        Assert.Null(Ranking(w, p).CurrentClubId); Assert.Equal("Sem clube", Ranking(w, p).CurrentClubName);
        Assert.Equal(2, Ranking(w, p).GoalsScored);
    }

    [Theory]
    [InlineData("career", null, 6, 3)]
    [InlineData("current", null, 3, 1)]
    [InlineData("season", 1, 1, 1)]
    [InlineData("season", 2, 2, 1)]
    [InlineData("season", 3, 3, 1)]
    public void SeasonFilters_PreserveCareerAndSelectOnlyRequestedSeason(string period, int? season, int goals, int matches)
    {
        var w = World(); w.SeasonNumber = 3; var p = Roster(w)[9];
        for (var s = 1; s <= 3; s++) Aggregate(w, p, 1, s, season: s);
        var row = Ranking(w, p, period: period, season: season);
        Assert.Equal(goals, row.GoalsScored); Assert.Equal(matches, row.MatchesPlayed);
        Assert.Equal(new[] { 3, 2, 1 }, PlayerStatisticsEngine.GetRankings(w).Seasons);
    }

    [Fact]
    public void NewSeasonInitialization_ResetsLegacyCountersButRetainsRankingHistory()
    {
        var w = World(); var p = Roster(w)[9]; Aggregate(w, p, 4, 8);
        p.CareerGoals = p.SeasonGoals = 8; p.SeasonMatches = 4;
        w.SeasonNumber = 2;
        CompetitionAndSeasonEngine.InitializeSeasonFixtures(w);
        Assert.Equal(0, p.SeasonGoals); Assert.Equal(0, p.SeasonMatches);
        Assert.Equal(8, Ranking(w, p).GoalsScored);
        Assert.Empty(PlayerStatisticsEngine.GetRankings(w, period: "current").Rows);
        Assert.Equal(8, Ranking(w, p, period: "season", season: 1).GoalsScored);
    }

    [Theory]
    [InlineData("reprocess")] [InlineData("replay-clone")] [InlineData("reconnect-json")]
    public void MatchAndPlayerGuards_PreventDuplicateStatisticsAcrossRetries(string retry)
    {
        var w = World(); var p = Roster(w)[9]; var m = Fixture(w);
        Goal(m, 10, m.HomeClubId, p.Id);
        Event(m, 25, MatchEventType.YellowCard, m.HomeClubId, p.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        if (retry == "reconnect-json")
        {
            w = Clone(w); m = w.Fixtures.Single();
            Assert.Empty(w.PlayerStatisticsIndex); // Transient index is reconstructed as needed.
        }
        if (retry == "replay-clone") m = JsonSerializer.Deserialize<MatchFixture>(JsonSerializer.Serialize(m))!;
        var before = JsonSerializer.Serialize(w.PlayerStatistics);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m, m]);
        Assert.Equal(before, JsonSerializer.Serialize(w.PlayerStatistics));
        Assert.Equal(m.Id, Assert.Single(w.AppliedPlayerStatisticsMatches));
        Assert.All(w.PlayerStatistics, s => Assert.Equal(m.Id, Assert.Single(s.AppliedMatchIds)));
    }

    [Fact]
    public void PersistedPerPlayerMatchGuard_ProtectsExistingBucketsEvenIfWorldMatchGuardNeedsRecovery()
    {
        var w = World(); var p = Roster(w)[9]; var m = Fixture(w); Goal(m, 10, m.HomeClubId, p.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]); w = Clone(w);
        w.AppliedPlayerStatisticsMatches.Clear();
        var before = JsonSerializer.Serialize(w.PlayerStatistics);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, w.Fixtures);
        Assert.Equal(before, JsonSerializer.Serialize(w.PlayerStatistics));
        Assert.Contains(m.Id, w.AppliedPlayerStatisticsMatches);
    }

    [Fact]
    public void TwoDifferentMatches_AccumulateIntoSameBucketAfterJsonReload()
    {
        var w = World(); var p = Roster(w)[9]; var first = Fixture(w); Goal(first, 10, first.HomeClubId, p.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [first]); w = Clone(w);
        var second = Fixture(w); Goal(second, 20, second.HomeClubId, p.Id);
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [second]);
        var statistics = Statistics(w, p);
        Assert.Equal(2, statistics.MatchesPlayed); Assert.Equal(2, statistics.GoalsScored);
        Assert.Equal(180, statistics.MinutesPlayed); Assert.Equal(2, statistics.AppliedMatchIds.Count);
    }

    [Fact]
    public void LegacyRecovery_ReconstructsStartingLineupAndSubstitutionTimingFromFinalLists()
    {
        var w = World(); var m = Fixture(w); var home = Roster(w); var scorer = Roster(w, 1)[9];
        Goal(m, 20, m.AwayClubId, scorer.Id); Sub(m, 60, m.HomeClubId, home[11], home[0]);
        Goal(m, 70, m.AwayClubId, scorer.Id);
        m.StatisticsInitialLineup.Clear(); m.StatisticsLineupCaptured = false; w.PlayerStatisticsVersion = 0;
        PlayerStatisticsEngine.EnsureMigrated(w);
        Assert.Equal(9, w.PlayerStatisticsVersion); Assert.True(m.StatisticsLineupCaptured);
        Assert.Equal((60, 1), (Statistics(w, home[0]).MinutesPlayed, Statistics(w, home[0]).GoalsConceded));
        Assert.Equal((30, 1), (Statistics(w, home[11]).MinutesPlayed, Statistics(w, home[11]).GoalsConceded));
        Assert.All(w.PlayerStatistics, s => Assert.False(s.HasIncompleteAppearances));
        var before = JsonSerializer.Serialize(w); PlayerStatisticsEngine.EnsureMigrated(w);
        Assert.Equal(before, JsonSerializer.Serialize(w));
    }

    [Fact]
    public void LegacyMissingLineup_RecoversOnlyIndividualFactsWithoutInventingAppearancesOrAverages()
    {
        var w = World(); var scorer = Roster(w)[9]; var defender = Roster(w)[1]; var keeper = Roster(w)[0];
        var m = Fixture(w); Goal(m, 20, m.HomeClubId, scorer.Id);
        Event(m, 30, MatchEventType.YellowCard, m.HomeClubId, defender.Id);
        Event(m, 35, MatchEventType.YellowCard, m.HomeClubId, keeper.Id);
        m.StatisticsLineupCaptured = false; m.StatisticsInitialLineup.Clear();
        m.HomeActiveStarterIds.Clear(); m.AwayActiveStarterIds.Clear(); w.PlayerStatisticsVersion = 0;
        PlayerStatisticsEngine.EnsureMigrated(w);
        Assert.Equal(3, w.PlayerStatistics.Count); Assert.Equal(1, Statistics(w, scorer).GoalsScored);
        Assert.All(w.PlayerStatistics, s => { Assert.True(s.HasIncompleteAppearances); Assert.Equal(0, s.MinutesPlayed); });
        Assert.Null(Ranking(w, scorer).Average); Assert.NotEmpty(PlayerStatisticsEngine.GetHistory(w, scorer.Id)!.CoverageWarnings);
        Statistics(w, defender).MatchesPlayed = 20; Statistics(w, keeper).MatchesPlayed = 20;
        foreach (var category in new[] { "goalkeepers", "defenders" })
        {
            var ranking = PlayerStatisticsEngine.GetRankings(w, category);
            Assert.Empty(ranking.Rows); Assert.Null(Assert.Single(ranking.NotEligible).Average);
        }
    }

    [Fact]
    public void LegacyWithoutEvents_DoesNotDistributeOldCountersAcrossCompetitionsOrAwardRetroactively()
    {
        var w = World(); w.SeasonNumber = 4; var p = Roster(w)[9];
        p.CareerGoals = 150; p.CareerMatches = 300; p.SeasonGoals = 12; p.CupGoals = 5;
        var legacy = JsonNode.Parse(JsonSerializer.Serialize(w))!.AsObject();
        foreach (var field in new[] { "PlayerStatisticsVersion", "PlayerStatistics", "AppliedPlayerStatisticsMatches",
                     "PlayerStatisticsCoverageWarnings", "SeasonTopScorerAwards", "ClosedPlayerAwardSeasons" })
            legacy.Remove(field);
        var loaded = JsonSerializer.Deserialize<GameWorld>(legacy.ToJsonString())!;
        PlayerStatisticsEngine.EnsureMigrated(loaded);
        Assert.Empty(loaded.PlayerStatistics); Assert.Empty(PlayerStatisticsEngine.GetRankings(loaded).Rows);
        Assert.Equal(150, loaded.Players.Single(x => x.Id == p.Id).CareerGoals);
        Assert.True(loaded.ClosedPlayerAwardSeasons.IsSupersetOf([1, 2, 3]));
        Assert.NotEmpty(loaded.PlayerStatisticsCoverageWarnings);
        Assert.Empty(PlayerStatisticsEngine.AwardSeason(loaded, 3)); Assert.Empty(loaded.Transactions);
    }

    [Fact]
    public void ScoreWithoutMatchingGoalEvents_MarksIncompleteInsteadOfInventingGoals()
    {
        var w = World(); var m = Fixture(w); m.HomeGoals = 3;
        PlayerStatisticsEngine.ApplyFinishedMatches(w, [m]);
        Assert.Equal(0, w.PlayerStatistics.Sum(s => s.GoalsScored));
        Assert.Equal(0, w.PlayerStatistics.Sum(s => s.GoalsConceded));
        Assert.All(w.PlayerStatistics, s => Assert.True(s.HasIncompleteAppearances));
        Assert.NotEmpty(w.PlayerStatisticsCoverageWarnings);
        Assert.Empty(PlayerStatisticsEngine.AwardSeason(w, 1));
    }

    [Theory]
    [InlineData(128)] [InlineData(1024)]
    public void RankingScalesToSupportedClubCounts_WithoutLosingRows(int clubs)
    {
        var w = World(clubs);
        foreach (var p in w.Players)
            Aggregate(w, p, 20, goals: p.Position == PlayerPosition.A ? 5 : 0, conceded: 10, yellow: 2);
        var watch = Stopwatch.StartNew();
        var scorers = PlayerStatisticsEngine.GetRankings(w);
        var keepers = PlayerStatisticsEngine.GetRankings(w, "goalkeepers");
        var defenders = PlayerStatisticsEngine.GetRankings(w, "defenders");
        watch.Stop();
        Assert.Equal(clubs * 3, scorers.Rows.Count); Assert.Equal(clubs * 2, keepers.Rows.Count);
        Assert.Equal(clubs * 5, defenders.Rows.Count);
        Assert.Equal(scorers.Rows.Count, scorers.Rows.Select(r => r.PlayerId).Distinct().Count());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"Three rankings for {clubs} clubs took {watch.Elapsed}.");
    }

    [Fact]
    public void History_ExposesRealIdentityAndSeasonDivisionCupStatistics()
    {
        var w = World(); var p = Roster(w)[9]; p.IsStar = true; p.Strength = 47; p.Behavior = PlayerBehavior.Caneleiro;
        Aggregate(w, p, 10, 12, season: 1, division: DivisionTier.QuartaDivisao);
        Aggregate(w, p, 5, 8, season: 2, division: DivisionTier.TerceiraDivisao);
        Aggregate(w, p, 3, 5, season: 2, competition: CompetitionType.CupFinal);
        var history = Assert.IsType<PlayerHistoryDto>(PlayerStatisticsEngine.GetHistory(w, p.Id));
        Assert.Equal(p.DisplayName, history.Player.DisplayName); Assert.True(history.Player.IsStar);
        Assert.Equal(p.Position, history.Player.Position); Assert.Equal(47, history.Player.Strength);
        Assert.Equal(p.Behavior.ToDisplayPt(), history.Player.BehaviorDisplay);
        Assert.Equal(p.ClubId, history.Player.CurrentClubId);
        Assert.Equal(18, history.Career.MatchesPlayed); Assert.Equal(25, history.Career.GoalsScored);
        Assert.Equal(3, history.Statistics.Count);
        Assert.Contains(history.Statistics, s => s.Season == 1 && s.Division == DivisionTier.QuartaDivisao && s.GoalsScored == 12);
        Assert.Contains(history.Statistics, s => s.Season == 2 && s.CompetitionType == CompetitionType.CupFinal && s.GoalsScored == 5);
        Assert.Null(PlayerStatisticsEngine.GetHistory(w, Id(999_999)));
    }

    [Fact]
    public void SeasonAwards_SumEveryDivisionAndCupAcrossClubChanges()
    {
        var w = AwardWorld(); var p = Roster(w)[9];
        for (var division = 1; division <= 4; division++)
            Aggregate(w, p, division, division, division: (DivisionTier)division);
        Aggregate(w, p, 2, 5, competition: CompetitionType.CupPreliminary1, club: w.Clubs[1].Id);
        Aggregate(w, p, 1, 2, competition: CompetitionType.CupFinal);
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 1));
        Assert.Equal(17, award.TotalGoals); Assert.Equal(13, award.TotalMatches);
        Assert.Equal(17, Ranking(w, p).GoalsScored); Assert.Equal(7, Ranking(w, p, scope: "cup").GoalsScored);
        Assert.Equal(4, Ranking(w, p, scope: "division4").GoalsScored);
        Assert.Equal(1, p.SeasonTopScorerTitles);
    }

    [Fact]
    public void SeasonAwards_ConsolidateFinishedMatchBeforeSelectingWinner_AndIgnoreShootout()
    {
        var w = World(); var home = Roster(w)[9]; var away = Roster(w, 1)[9];
        var m = Fixture(w, CompetitionType.CupFinal, minute: 120); m.EnteredExtraTime = true;
        Goal(m, 105, m.HomeClubId, home.Id);
        for (var i = 0; i < 5; i++) Event(m, 120, MatchEventType.PenaltyShootoutGoal, m.AwayClubId, away.Id);
        m.AwayPenaltyGoals = 5; m.HomePenaltyGoals = 4;
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 1));
        Assert.Equal(home.Id, award.PlayerId); Assert.Equal(1, award.TotalGoals); Assert.Equal(1, award.TotalMatches);
        Assert.Equal(0, away.SeasonTopScorerTitles); Assert.Equal(0, Statistics(w, away).GoalsScored);
    }

    [Fact]
    public void RealMinuteEngine_FullMatchFeedsOfficialEventsAndSeasonAwardWithoutDuplicateTelemetry()
    {
        var w = World(); var m = Fixture(w); var players = w.Players.ToDictionary(p => p.Id);
        m.IsFinished = false; m.StatisticsLineupCaptured = false; m.StatisticsInitialLineup.Clear();
        foreach (var p in w.Players) p.Strength = p.Position == PlayerPosition.A ? 50 : 10;
        for (var minute = 1; minute <= 90; minute++)
        {
            m.CurrentMinute = minute;
            MatchSimulationEngine.SimulateSingleMinute(m, w.Clubs[0], w.Clubs[1], players,
                ManagerTacticalIdentity.Ofensivo, ManagerTacticalIdentity.Ofensivo, 199801);
            Assert.Empty(w.PlayerStatistics);
        }
        m.IsFinished = true;
        var goals = m.Events.Where(e => e.EventType == MatchEventType.Goal).ToList();
        Assert.NotEmpty(goals); Assert.Equal(m.HomeGoals + m.AwayGoals, goals.Count);
        var expected = goals.GroupBy(e => e.PrimaryPlayerId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var maximum = expected.Values.Max();
        var awards = PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(goals.Count, w.PlayerStatistics.Sum(s => s.GoalsScored));
        Assert.Equal(expected.Where(kv => kv.Value == maximum).Select(kv => kv.Key).Order(), awards.Select(a => a.PlayerId).Order());
        Assert.All(awards, a => { Assert.Equal(maximum, a.TotalGoals); Assert.Equal(1, a.TotalMatches); });
        Assert.All(expected, kv => Assert.Equal(kv.Value, Statistics(w, players[kv.Key]).GoalsScored));
        Assert.Equal(22, w.PlayerStatistics.Count); Assert.Empty(w.PlayerStatisticsCoverageWarnings);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)]
    public void SeasonAwards_AllTiedGoalsWinRegardlessOfMatchesOrAverage(int winners)
    {
        var w = AwardWorld(); var players = Roster(w).Take(winners + 1).ToList();
        for (var i = 0; i < winners; i++) Aggregate(w, players[i], 10 + i * 20, 29);
        Aggregate(w, players[winners], 1, 28);
        var awards = PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(winners, awards.Count);
        Assert.Equal(players.Take(winners).Select(p => p.Id).Order(), awards.Select(a => a.PlayerId));
        Assert.All(awards, a => { Assert.Equal(29, a.TotalGoals); Assert.Equal(Prize, a.AwardAmount); });
        Assert.All(players.Take(winners), p => Assert.Equal(1, p.SeasonTopScorerTitles));
        Assert.Equal(0, players[winners].SeasonTopScorerTitles);
        Assert.Equal(winners, w.Transactions.Count); Assert.Equal(winners * Prize, w.Transactions.Sum(t => t.Amount));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ZeroGoalSeason_NeverAwardsTitlesOrMoney(bool withStatistics)
    {
        var w = AwardWorld(); if (withStatistics) Aggregate(w, Roster(w)[9]);
        Assert.Empty(PlayerStatisticsEngine.AwardSeason(w, 1));
        Assert.Empty(w.Transactions); Assert.Empty(w.SeasonTopScorerAwards);
        Assert.All(w.Players, p => Assert.Equal(0, p.SeasonTopScorerTitles));
        Assert.Contains(1, w.ClosedPlayerAwardSeasons);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void EachWinner_CreditsFullPrizeToClosingClub_AndClubTotalsAccumulate(bool sameClub)
    {
        var w = AwardWorld(); var a = Roster(w)[9]; var b = sameClub ? Roster(w)[10] : Roster(w, 1)[9];
        Aggregate(w, a, 20, 29); Aggregate(w, b, 40, 29);
        var balances = w.Clubs.ToDictionary(c => c.Id, c => c.CashBalance);
        PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(balances[w.Clubs[0].Id] + (sameClub ? 2 : 1) * Prize, w.Clubs[0].CashBalance);
        Assert.Equal(balances[w.Clubs[1].Id] + (sameClub ? 0 : 1) * Prize, w.Clubs[1].CashBalance);
        var totals = PlayerStatisticsEngine.GetSeasonAwards(w, 1).ClubTotals;
        Assert.Equal(sameClub ? 1 : 2, totals.Count); Assert.Equal(2 * Prize, totals.Sum(t => t.Amount));
        if (sameClub) Assert.Equal(6_000_000m, Assert.Single(totals).Amount);
        else Assert.All(totals, t => Assert.Equal(Prize, t.Amount));
    }

    [Fact]
    public void AwardPayment_UsesClubAtClose_InsteadOfClubWhereGoalsWereScored()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; var original = w.Clubs[0]; var closing = w.Clubs[1];
        Aggregate(w, p, 20, 30); p.ClubId = closing.Id;
        var originalBalance = original.CashBalance; var closingBalance = closing.CashBalance;
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 1));
        Assert.Equal(closing.Id, award.ClubId); Assert.Equal(closing.Name, award.ClubNameAtClose);
        Assert.Equal(originalBalance, original.CashBalance); Assert.Equal(closingBalance + Prize, closing.CashBalance);
        Assert.Equal(original.Id, Assert.Single(PlayerStatisticsEngine.GetHistory(w, p.Id)!.Statistics).ClubId);
    }

    [Fact]
    public void FreeAgentWinner_KeepsTitleWithZeroAwardWithoutFictitiousClubOrLedgerCredit()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30); p.ClubId = null;
        var balances = w.Clubs.Select(c => c.CashBalance).ToArray();
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 1));
        Assert.Null(award.ClubId); Assert.Equal(0m, award.AwardAmount); Assert.Equal(1, p.SeasonTopScorerTitles);
        Assert.Empty(w.Transactions); Assert.Equal(balances, w.Clubs.Select(c => c.CashBalance));
        Assert.Empty(PlayerStatisticsEngine.GetSeasonAwards(w, 1).ClubTotals);
        Assert.Single(PlayerStatisticsEngine.GetHistory(w, p.Id)!.Awards);
    }

    [Fact]
    public void AwardLedger_RecordsExactAmountBalanceSeasonAndWorldScopedIdempotencyKey()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        var club = w.Clubs[0]; var balance = club.CashBalance; var before = DateTime.UtcNow;
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 1));
        var transaction = Assert.Single(w.Transactions);
        var key = $"season_top_scorer:{w.Id}:1:{p.Id}";
        Assert.Equal(key, award.IdempotencyKey); Assert.Equal(key, transaction.IdempotencyKey);
        Assert.NotEqual(Guid.Empty, award.AwardId);
        Assert.InRange(award.AwardGrantedAt, before, DateTime.UtcNow);
        Assert.Equal(w.Id, transaction.GameWorldId); Assert.Equal(club.Id, transaction.ClubId);
        Assert.Equal(1, transaction.SeasonNumber); Assert.Equal(FinancialTransactionCategory.PrizeMoney, transaction.Category);
        Assert.Equal(Prize, transaction.Amount); Assert.Equal(balance + Prize, transaction.BalanceAfter);
        Assert.Equal(transaction.BalanceAfter, club.CashBalance); Assert.Contains(p.DisplayName, transaction.Description);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RepeatedSeasonClose_IsIdempotentAcrossJsonRestart(bool reload)
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        PlayerStatisticsEngine.AwardSeason(w, 1); if (reload) w = Clone(w);
        var before = JsonSerializer.Serialize(w);
        PlayerStatisticsEngine.AwardSeason(w, 1); PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(before, JsonSerializer.Serialize(w));
        Assert.Equal(1, w.Players.Single(x => x.Id == p.Id).SeasonTopScorerTitles);
        Assert.Single(w.Transactions); Assert.Single(w.SeasonTopScorerAwards);
    }

    [Fact]
    public void AwardRecordGuard_ProtectsTitleAndPaymentWhenSeasonCloseMarkerIsMissing()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        PlayerStatisticsEngine.AwardSeason(w, 1); w = Clone(w); w.ClosedPlayerAwardSeasons.Clear();
        var balances = w.Clubs.Select(c => c.CashBalance).ToArray();
        var award = JsonSerializer.Serialize(w.SeasonTopScorerAwards);
        PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(award, JsonSerializer.Serialize(w.SeasonTopScorerAwards));
        Assert.Single(w.Transactions); Assert.Equal(balances, w.Clubs.Select(c => c.CashBalance));
        Assert.Equal(1, w.Players.Single(x => x.Id == p.Id).SeasonTopScorerTitles);
    }

    [Fact]
    public void AwardLedgerGuard_PreventsDuplicateCashWhenAwardRecordNeedsRecovery()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        var key = $"season_top_scorer:{w.Id}:1:{p.Id}";
        Assert.True(MarketAndEconomyEngine.TryRecordTransaction(w, w.Clubs[0], FinancialTransactionCategory.PrizeMoney, Prize, "Recovered award ledger", key));
        w = Clone(w); var balance = w.Clubs[0].CashBalance;
        PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(balance, w.Clubs[0].CashBalance); Assert.Single(w.Transactions);
        Assert.Single(w.SeasonTopScorerAwards); Assert.Equal(1, w.Players.Single(x => x.Id == p.Id).SeasonTopScorerTitles);
    }

    [Theory]
    [InlineData("early-round")] [InlineData("unfinished")] [InlineData("no-fixtures")]
    [InlineData("incomplete")] [InlineData("past-season")] [InlineData("future-season")]
    public void SeasonAwards_RequireCurrentSeasonOfficialCloseAndCompleteStatistics(string obstacle)
    {
        var w = AwardWorld(); var p = Roster(w)[9]; var statistics = Aggregate(w, p, 20, 30); var season = 1;
        switch (obstacle)
        {
            case "early-round": w.TotalRoundsInSeason = 2; break;
            case "unfinished": w.Fixtures[0].IsFinished = false; break;
            case "no-fixtures": w.Fixtures.Clear(); break;
            case "incomplete": statistics.HasIncompleteAppearances = true; break;
            case "past-season": w.SeasonNumber = 2; break;
            case "future-season": season = 2; break;
        }
        var balance = w.Clubs[0].CashBalance;
        Assert.Empty(PlayerStatisticsEngine.AwardSeason(w, season));
        Assert.Empty(w.Transactions); Assert.Empty(w.SeasonTopScorerAwards);
        Assert.DoesNotContain(season, w.ClosedPlayerAwardSeasons);
        Assert.Equal(balance, w.Clubs[0].CashBalance); Assert.Equal(0, p.SeasonTopScorerTitles);
    }

    [Fact]
    public void IncompleteEarlierSeason_DoesNotBlockCompleteCurrentSeasonAwards()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; w.SeasonNumber = 2; w.Fixtures[0].SeasonNumber = 2;
        Aggregate(w, p, 20, 100, incomplete: true); Aggregate(w, p, 20, 25, season: 2);
        var award = Assert.Single(PlayerStatisticsEngine.AwardSeason(w, 2));
        Assert.Equal(25, award.TotalGoals); Assert.Equal(2, award.Season);
    }

    [Fact]
    public void AwardSnapshots_RemainImmutableAfterTransfersRenamingContractExpiryAndNewSeason()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        PlayerStatisticsEngine.AwardSeason(w, 1); var snapshot = JsonSerializer.Serialize(w.SeasonTopScorerAwards);
        p.Name = "Nome posterior"; p.IsStar = true; p.ClubId = w.Clubs[1].Id; p.ContractRoundsRemaining = 0;
        w.Clubs[0].Name = "Clube renomeado"; w.Clubs[0].DivisionTier = DivisionTier.Distrital;
        w.SeasonNumber = 2; w = Clone(w);
        Assert.Equal(snapshot, JsonSerializer.Serialize(w.SeasonTopScorerAwards));
        Assert.Equal(JsonSerializer.Deserialize<List<SeasonTopScorerAward>>(snapshot)![0].ClubNameAtClose,
            Assert.Single(PlayerStatisticsEngine.GetSeasonAwards(w, 1).Awards).ClubNameAtClose);
        var history = PlayerStatisticsEngine.GetHistory(w, p.Id)!;
        Assert.Equal(1, history.Player.SeasonTopScorerTitles); Assert.Single(history.Awards);
        Assert.Equal(w.Clubs[1].Id, history.Player.CurrentClubId);
    }

    [Fact]
    public void FiveDistinctSeasonTitles_PersistWithCorrectPerSeasonHistoryAndNoDuplicateTitles()
    {
        var w = AwardWorld(); var p = Roster(w)[9];
        for (var season = 1; season <= 5; season++)
        {
            w.SeasonNumber = season; w.Fixtures[0].SeasonNumber = season;
            Aggregate(w, p, 20 + season, 25 + season, season: season);
            PlayerStatisticsEngine.AwardSeason(w, season); PlayerStatisticsEngine.AwardSeason(w, season);
        }
        w = Clone(w); var history = PlayerStatisticsEngine.GetHistory(w, p.Id)!;
        Assert.Equal(5, history.Player.SeasonTopScorerTitles);
        Assert.Equal(5, history.Awards.Count); Assert.Equal(5, w.Transactions.Count);
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, history.Awards.Select(a => a.Season));
        Assert.All(history.Awards, a => { Assert.Equal(25 + a.Season, a.TotalGoals); Assert.Equal(20 + a.Season, a.TotalMatches); });
        Assert.Equal(5, history.Awards.Select(a => a.AwardId).Distinct().Count());
        Assert.Equal(5, Ranking(w, p).SeasonTopScorerTitles);
        Assert.Equal(25_000_000m, w.Clubs[0].CashBalance); // Initial 10m plus five full prizes.
        var last = PlayerStatisticsEngine.GetSeasonAwards(w);
        Assert.Equal(5, last.Season); Assert.Equal(5, Assert.Single(last.Awards).Season);
        Assert.Equal(2, Assert.Single(PlayerStatisticsEngine.GetSeasonAwards(w, 2).Awards).Season);
    }

    [Fact]
    public void AwardingSeason_ChangesOnlyPrizeMoneyAndTitles_PreservingEconomyAndMultiplayerState()
    {
        var w = AwardWorld(); var p = Roster(w)[9]; Aggregate(w, p, 20, 30);
        w.RoundState = RoundState.RESULTS; w.RoundVersion = 37; w.CurrentMatchMinute = 90;
        w.HumanCoaches.Add(new HumanCoach { Id = Id(50_000), CurrentClubId = w.Clubs[0].Id,
            IsHost = true, IsConnected = false, IsReadyForRound = true, LeagueTitles = 2, CupTitles = 3, CoachPoints = 123.5m });
        w.ActivePauseWindowId = Id(60_000); w.SettledFinancialRounds.Add("existing-round");
        var statistics = JsonSerializer.Serialize(w.PlayerStatistics);
        var coaches = JsonSerializer.Serialize(w.HumanCoaches);
        var stadiums = JsonSerializer.Serialize(w.Clubs.Select(c => c.Stadium));
        var contracts = JsonSerializer.Serialize(w.Players.Select(x => new { x.Id, x.ContractRoundsRemaining, x.SalaryPerRound, x.MarketPrice }));
        var cash = w.Clubs.Sum(c => c.CashBalance);
        PlayerStatisticsEngine.AwardSeason(w, 1);
        Assert.Equal(cash + Prize, w.Clubs.Sum(c => c.CashBalance));
        Assert.Equal(statistics, JsonSerializer.Serialize(w.PlayerStatistics));
        Assert.Equal(coaches, JsonSerializer.Serialize(w.HumanCoaches));
        Assert.Equal(stadiums, JsonSerializer.Serialize(w.Clubs.Select(c => c.Stadium)));
        Assert.Equal(contracts, JsonSerializer.Serialize(w.Players.Select(x => new { x.Id, x.ContractRoundsRemaining, x.SalaryPerRound, x.MarketPrice })));
        Assert.Equal(RoundState.RESULTS, w.RoundState); Assert.Equal(37, w.RoundVersion);
        Assert.Equal(90, w.CurrentMatchMinute); Assert.Equal(Id(60_000), w.ActivePauseWindowId);
        Assert.Equal("existing-round", Assert.Single(w.SettledFinancialRounds));
        Assert.Empty(w.TransferOffers); Assert.Empty(w.BankLoans);
    }
}
