using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class LineupSelectionV3Tests
{
    private static Player PlayerAt(PlayerPosition position, int strength = 40) => new() { Position = position, Strength = strength };
    private static List<Player> Squad() => new[] { (PlayerPosition.G, 2), (PlayerPosition.D, 6), (PlayerPosition.M, 5), (PlayerPosition.A, 7) }
        .SelectMany(x => Enumerable.Range(0, x.Item2).Select(_ => PlayerAt(x.Item1))).ToList();

    [Fact]
    public void SevenAttackersDistinguishAutomaticFromBestWithoutChangingStrength()
    {
        var squad = Squad(); var attackers = squad.Where(p => p.Position == PlayerPosition.A).ToArray();
        foreach (var p in attackers) p.Strength = 50;
        attackers[0].Strength = 45; attackers[0].SeasonGoals = 7;
        attackers[1].Strength = 45; attackers[1].SeasonGoals = 2;
        var incumbents = attackers.Take(3).Select(p => p.Id).ToList();
        var before = JsonSerializer.Serialize(squad);
        var a = LineupSelectionEngine.Select(squad, TacticalFormations.All["4-3-3"], LineupSelectionMode.Automatic, incumbents);
        var m = LineupSelectionEngine.Select(squad, TacticalFormations.All["4-3-3"], LineupSelectionMode.Best, incumbents);
        Assert.Equal(52, LineupSelectionEngine.Score(attackers[0], LineupSelectionMode.Automatic));
        Assert.Equal(47, LineupSelectionEngine.Score(attackers[1], LineupSelectionMode.Automatic));
        Assert.Equal(50, LineupSelectionEngine.Score(attackers[2], LineupSelectionMode.Automatic));
        Assert.Contains(attackers[0].Id, a.Starters); Assert.DoesNotContain(attackers[1].Id, a.Starters);
        Assert.DoesNotContain(attackers[0].Id, m.Starters); Assert.DoesNotContain(attackers[1].Id, m.Starters);
        Assert.Contains(attackers[2].Id, a.Starters); Assert.Contains(attackers[2].Id, m.Starters);
        Assert.Equal(before, JsonSerializer.Serialize(squad));
    }

    [Theory]
    [InlineData(PlayerPosition.A, 45, 44, 41)]
    [InlineData(PlayerPosition.M, 44, 42, 42)]
    [InlineData(PlayerPosition.D, 43, 42, 41)]
    [InlineData(PlayerPosition.G, 40, 40, 40)]
    public void PositionWeightsUseGoalsAndAssistsAndIgnoreStarAndBehavior(PlayerPosition position, double expected, double goalsOnly, double assistsOnly)
    {
        var p = PlayerAt(position); p.SeasonGoals = 4; p.CupGoals = 2;
        Assert.Equal(expected, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, new(4, 2)));
        foreach (var behavior in Enum.GetValues<PlayerBehavior>())
        {
            p.Behavior = behavior; p.IsStar = !p.IsStar;
            Assert.Equal(expected, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, new(4, 2)));
            Assert.Equal(40, LineupSelectionEngine.Score(p, LineupSelectionMode.Best, new(100, 100)));
        }
        Assert.Equal(goalsOnly, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, new(4, 0)));
        p.SeasonGoals = 0; p.CupGoals = 0;
        Assert.Equal(assistsOnly, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, new(0, 2)));
        Assert.Equal(position == PlayerPosition.G ? 40 : 50,
            LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, new(100, 100)));
    }

    [Theory]
    [InlineData(LineupSelectionMode.Automatic)]
    [InlineData(LineupSelectionMode.Best)]
    public void FormationIncumbentsAvailabilityAndBenchAreRespected(LineupSelectionMode mode)
    {
        var squad = Squad(); var incumbent = squad.Last(); var injured = squad[2]; var suspended = squad[3];
        injured.Strength = suspended.Strength = 50;
        injured.InjuryDaysRemaining = 1; suspended.SuspensionRoundsRemaining = 1;
        var first = LineupSelectionEngine.Select(squad, TacticalFormations.All["4-4-2"], mode, [incumbent.Id]);
        Assert.Contains(incumbent.Id, first.Starters);
        Assert.Equal(11, first.Starters.Count); Assert.Equal(7, first.Bench.Count);
        Assert.Equal(new[] { 1, 4, 4, 2 }, Enum.GetValues<PlayerPosition>().Select(pos => first.Starters.Count(id => squad.Single(p => p.Id == id).Position == pos)));
        Assert.DoesNotContain(injured.Id, first.Starters.Concat(first.Bench));
        Assert.DoesNotContain(suspended.Id, first.Starters.Concat(first.Bench));
        Assert.Equal(18, first.Starters.Concat(first.Bench).Distinct().Count());
        Assert.Equal(PlayerPosition.G, squad.Single(p => p.Id == first.Bench[0]).Position);
        var again = LineupSelectionEngine.Select(squad.AsEnumerable().Reverse().ToList(), TacticalFormations.All["4-4-2"], mode, first.Starters);
        Assert.Equal(first.Starters, again.Starters); Assert.Equal(first.Bench, again.Bench);
    }

    [Fact]
    public void LeagueCupTransferAndSeasonResetPreserveCareerAndLegacySaveEvidence()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); w.PlayerStatisticsVersion = 9;
        var p = w.Players.First(p => p.Position == PlayerPosition.A); p.Strength = 40;
        p.SeasonGoals = 5; p.CupGoals = 2; p.CareerGoals = 19;
        var oldClub = p.ClubId!.Value; p.ClubId = w.Clubs.First(c => c.Id != oldClub).Id;
        w.PlayerStatistics.Add(new() { PlayerId = p.Id, ClubId = oldClub, Season = w.SeasonNumber, CompetitionType = CompetitionType.League, GoalsScored = 3, Assists = 2 });
        w.PlayerStatistics.Add(new() { PlayerId = p.Id, ClubId = oldClub, Season = w.SeasonNumber, CompetitionType = CompetitionType.CupFinal, GoalsScored = 2, Assists = 2 });
        w.PlayerStatistics.Add(new() { PlayerId = p.Id, ClubId = p.ClubId.Value, Season = w.SeasonNumber, CompetitionType = CompetitionType.League, Assists = 2 });
        Assert.Equal(48, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, LineupSelectionEngine.CurrentPerformance(w)[p.Id]));
        Assert.Equal(5, PlayerStatisticsEngine.GetPanelStatistics(w, p).SeasonGoals);
        var restored = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        Assert.Equal(48, LineupSelectionEngine.Score(restored.Players.Single(x => x.Id == p.Id), LineupSelectionMode.Automatic, LineupSelectionEngine.CurrentPerformance(restored)[p.Id]));
        w.SeasonNumber++; CompetitionAndSeasonEngine.InitializeSeasonFixtures(w);
        Assert.Equal(40, LineupSelectionEngine.Score(p, LineupSelectionMode.Automatic, LineupSelectionEngine.CurrentPerformance(w).GetValueOrDefault(p.Id)));
        Assert.Equal(19, p.CareerGoals); Assert.Equal(3, w.PlayerStatistics.Count);
        Assert.Equal(LineupSelectionMode.Automatic, JsonSerializer.Deserialize<Club>("{}")!.LineupSelectionMode);
        Assert.Equal(5, PlayerStatisticsEngine.GetRankings(w, "scorers", "general", "career").Rows.Single(r => r.PlayerId == p.Id).GoalsScored);
    }

    [Fact]
    public void RealCupAndLeagueGoalsAppearOnceAfterRepeatedProcessingAndReload()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); w.PlayerStatisticsVersion = 9; w.Fixtures.Clear();
        var home = w.Clubs[0]; var away = w.Clubs[1];
        var scorer = w.Players.First(p => p.ClubId == home.Id && p.Position == PlayerPosition.A);
        foreach (var type in new[] { CompetitionType.CupFinal, CompetitionType.League })
        {
            var match = new MatchFixture { GameWorldId = w.Id, SeasonNumber = w.SeasonNumber, RoundNumber = 1,
                HomeClubId = home.Id, AwayClubId = away.Id, CompetitionType = type, DivisionTier = home.DivisionTier,
                HomeActiveStarterIds = home.PreferredStarterIds.ToList(), AwayActiveStarterIds = away.PreferredStarterIds.ToList() };
            // Explicit inclusion makes this independent of the seeded team's preferred lineup.
            if (!match.HomeActiveStarterIds.Contains(scorer.Id)) match.HomeActiveStarterIds[1] = scorer.Id;
            PlayerStatisticsEngine.CaptureLineup(match, w.Players.ToDictionary(p => p.Id));
            MatchSimulationEngine.ResolvePenaltyKick(match, home, scorer, null, 25, true, 20, 0, new());
            match.CurrentMinute = 90; match.IsFinished = true; w.Fixtures.Add(match);
            PlayerStatisticsEngine.ApplyFinishedMatches(w, [match]); PlayerStatisticsEngine.ApplyFinishedMatches(w, [match]);
            Assert.Equal(scorer.SeasonGoals, PlayerStatisticsEngine.GetPanelStatistics(w, scorer).SeasonGoals);
        }
        Assert.Equal(2, scorer.SeasonGoals); Assert.Equal(1, scorer.CupGoals); Assert.Equal(2, scorer.CareerGoals);
        var json = JsonSerializer.Serialize(w); var restored = JsonSerializer.Deserialize<GameWorld>(json)!;
        PlayerStatisticsEngine.ApplyFinishedMatches(restored, restored.Fixtures.Where(f => f.IsFinished).ToList());
        var p = restored.Players.Single(p => p.Id == scorer.Id);
        Assert.Equal(2, PlayerStatisticsEngine.GetPanelStatistics(restored, p).SeasonGoals);
        Assert.Equal(2, PlayerStatisticsEngine.GetHistory(restored, p.Id)!.Statistics.Sum(s => s.GoalsScored));
        Assert.Equal(1, PlayerStatisticsEngine.GetRankings(restored, "scorers", "cup").Rows.Single(r => r.PlayerId == p.Id).GoalsScored);
        restored.RoundNumber = restored.TotalRoundsInSeason;
        var granted = Assert.Single(PlayerStatisticsEngine.AwardSeason(restored, restored.SeasonNumber));
        Assert.Equal(2, granted.TotalGoals);
        var awards = JsonSerializer.Serialize(restored.SeasonTopScorerAwards);
        Assert.Equal(awards, JsonSerializer.Serialize(PlayerStatisticsEngine.AwardSeason(restored, restored.SeasonNumber)));
    }
}
