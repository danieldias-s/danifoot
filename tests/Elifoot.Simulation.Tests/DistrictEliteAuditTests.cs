using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public class DistrictEliteAuditTests
{
    private const int Seed = 199801;
    private const int Seasons = 100;
    private const int ClubCount = 128;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DistrictVsElite_100RealSeasons()
    {
        string directory = Environment.GetEnvironmentVariable("ELIFOOT_DISTRICT_AUDIT_OUTPUT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/district-vs-elite"));
        Directory.CreateDirectory(directory);
        using var fixturesFile = File.CreateText(Path.Combine(directory, "persisted-fixtures.jsonl"));
        using var confrontationsFile = File.CreateText(Path.Combine(directory, "confrontations.jsonl"));
        var world = WorldSeedFactory.CreateSeededWorld(
            worldName: "Auditoria Distrital x Elite", seed: Seed, totalClubs: ClubCount,
            forceEvolutionMode: ForceEvolutionMode.ClassicAdapted, applyInitialSquadStrength: true);

        var fixtureIds = new HashSet<Guid>();
        int confrontations = 0, playedMinutes = 0, league = 0, cup = 0;
        for (int season = 1; season <= Seasons; season++)
        {
            Assert.Equal(season, world.SeasonNumber);
            Assert.Empty(world.HumanCoaches);
            var clubs = world.Clubs.ToDictionary(c => c.Id);
            Assert.Equal(ClubCount, clubs.Count);
            foreach (var division in Enum.GetValues<DivisionTier>())
                Assert.Equal(division == DivisionTier.Distrital ? 96 : 8, clubs.Values.Count(c => c.DivisionTier == division));

            var schedule = CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(world);
            Assert.Equal(CompetitionType.League, schedule[0]);
            Assert.Equal(CompetitionType.CupFinal, schedule[^1]);
            Assert.Equal(world.TotalRoundsInSeason, schedule.Count);
            int seasonLeague = 0, seasonCup = 0;
            for (int round = 1; round <= schedule.Count; round++)
            {
                world.RoundNumber = round;
                world.RoundState = RoundState.PREPARING;
                world.CurrentMatchMinute = 0;
                CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, round);
                ClassicForceEvolutionEngine.PrepareRound(world);
                var matches = world.Fixtures.Where(f => f.SeasonNumber == season && f.RoundNumber == round)
                    .OrderBy(f => f.DeterministicMatchIndex).ToList();
                var players = world.Players.ToDictionary(p => p.Id);
                var managers = world.AiManagers.ToDictionary(m => m.Id);
                var squads = world.Players.Where(p => p.ClubId.HasValue).ToLookup(p => p.ClubId!.Value);
                var captured = new Dictionary<Guid, Kickoff>();

                // The application refreshes division-club plans when preparing the next
                // round, including clubs idle in a cup round. The market consumes those plans.
                if (season != 1 || round != 1)
                {
                    foreach (var club in clubs.Values.Where(c => c.DivisionTier != DivisionTier.Distrital))
                        AiManagerBrain.PlanAndSelectLineup(managers[club.AiManagerId!.Value], club,
                            squads[club.Id].ToList(), null, null, true, Seed, season, round, world.AllowClassic505);
                }

                foreach (var match in matches)
                {
                    Assert.Equal(schedule[round - 1], match.CompetitionType);
                    CoachCareerEngine.CaptureLeadership(world, match);
                    var home = clubs[match.HomeClubId];
                    var away = clubs[match.AwayClubId];
                    var homeSquad = squads[home.Id].ToList();
                    var awaySquad = squads[away.Id].ToList();
                    var homeManager = managers[home.AiManagerId!.Value];
                    var awayManager = managers[away.AiManagerId!.Value];
                    var homePlan = AiManagerBrain.PlanAndSelectLineup(homeManager, home, homeSquad, away, awaySquad,
                        true, Seed, season, round, world.AllowClassic505);
                    var awayPlan = AiManagerBrain.PlanAndSelectLineup(awayManager, away, awaySquad, home, homeSquad,
                        false, Seed, season, round, world.AllowClassic505);
                    (match.HomeFormation, match.HomeActiveStarterIds, match.HomeBenchIds) =
                        (homePlan.Formation, homePlan.Starters, homePlan.Bench);
                    (match.AwayFormation, match.AwayActiveStarterIds, match.AwayBenchIds) =
                        (awayPlan.Formation, awayPlan.Starters, awayPlan.Bench);

                    if (IsConfrontation(home.DivisionTier, away.DivisionTier))
                    {
                        captured.Add(match.Id, new(
                            CaptureSide(homeSquad, match.HomeActiveStarterIds, match.HomeFormation, homeManager,
                                match.HomeMoraleAtKickoff, true, world.AllowClassic505, players),
                            CaptureSide(awaySquad, match.AwayActiveStarterIds, match.AwayFormation, awayManager,
                                match.AwayMoraleAtKickoff, false, world.AllowClassic505, players)));
                    }

                    int minutes = MatchSimulationEngine.SimulateFullMatchReal(
                        match, home, away, homeManager, awayManager, homeSquad, awaySquad, players, Seed, world.AllowClassic505);
                    if (!ClassicEconomyEngine.Played(match)) continue;
                    Assert.Equal(match.CurrentMinute, minutes);
                    Assert.Equal(match.LastSimulatedMinute, minutes);
                    Assert.True(minutes is 90 or 120);
                    playedMinutes += minutes;
                    if (captured.TryGetValue(match.Id, out var kickoff))
                        ValidateKickoff(match, kickoff, players);
                }

                world.RoundState = RoundState.RESULTS;
                MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, matches);
                CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, matches);
                MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);

                foreach (var match in matches.Where(ClassicEconomyEngine.Played))
                {
                    Assert.True(fixtureIds.Add(match.Id), $"Partida repetida: {match.Id}");
                    var persisted = world.History.Matches[match.Id].Match;
                    Assert.Equal((match.HomeGoals, match.AwayGoals, match.WinnerClubId),
                        (persisted.HomeGoals, persisted.AwayGoals, persisted.WinnerClubId));
                    var source = new FixtureResult(persisted, clubs[match.HomeClubId].DivisionTier,
                        clubs[match.AwayClubId].DivisionTier, match.HomePenaltyGoals, match.AwayPenaltyGoals, match.CurrentMinute);
                    if (match.CompetitionType == CompetitionType.League) seasonLeague++;
                    else seasonCup++;
                    fixturesFile.WriteLine(JsonSerializer.Serialize(source, Json));
                    if (captured.TryGetValue(match.Id, out var kickoff))
                    {
                        Assert.True(match.StatisticsLineupCaptured);
                        var events = match.Events.Where(e => e.EventType is MatchEventType.Substitution or MatchEventType.Injury
                            or MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard or MatchEventType.TacticChange).ToArray();
                        Assert.Equal(match.HomeUsedSubstitutions + match.AwayUsedSubstitutions,
                            events.Count(e => e.EventType == MatchEventType.Substitution));
                        confrontationsFile.WriteLine(JsonSerializer.Serialize(new Confrontation(
                            source, kickoff, match.HomeFormation, match.AwayFormation, events), Json));
                        confrontations++;
                    }
                }
            }

            Assert.Equal(224, seasonLeague);
            Assert.Equal(127, seasonCup);
            league += seasonLeague;
            cup += seasonCup;
            var finished = CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(world);
            Assert.Equal(season, finished.SeasonNumber);
            Assert.Contains(season, world.History.ClosedSeasons);
            fixturesFile.Flush();
            confrontationsFile.Flush();
            File.WriteAllText(Path.Combine(directory, "progress.json"), JsonSerializer.Serialize(new
            {
                seasons = season, matches = fixtureIds.Count, confrontations, playedMinutes
            }, Json));
        }

        Assert.Equal(Seasons + 1, world.SeasonNumber);
        Assert.Equal(Enumerable.Range(1, Seasons), world.History.ClosedSeasons.Order());
        Assert.True(fixtureIds.SetEquals(world.History.Matches.Keys));
        File.WriteAllText(Path.Combine(directory, "run.json"), JsonSerializer.Serialize(new
        {
            checkpoint = "cae3a65", seed = Seed, worldId = world.Id, clubCount = ClubCount, seasons = Seasons,
            league, cup, matches = fixtureIds.Count, confrontations, playedMinutes,
            forceMode = world.ForceEvolutionMode.ToString(), forceRulesVersion = ClassicForceEvolutionEngine.Version,
            disciplineRulesVersion = world.DisciplineRulesVersion,
            homeMultiplier = MatchSimulationEngine.HomeAdvantageMultiplier
        }, Json));
    }

    private static bool IsConfrontation(DivisionTier home, DivisionTier away)
        => home == DivisionTier.Distrital && IsElite(away) || away == DivisionTier.Distrital && IsElite(home);

    private static bool IsElite(DivisionTier tier) => tier is DivisionTier.PrimeiraDivisao or DivisionTier.SegundaDivisao;

    private static Side CaptureSide(IReadOnlyList<Player> squad, IReadOnlyList<Guid> starters, string formation,
        AiManager manager, int morale, bool home, bool allowClassic505, IReadOnlyDictionary<Guid, Player> players)
    {
        var snapshot = squad.Select(p => new PlayerAtKickoff(p.Id, p.DisplayName, p.Position, p.Strength,
            p.IsStar, p.InjuryDaysRemaining, p.InjuryRoundsRemaining, p.SuspensionRoundsRemaining)).ToArray();
        var sectors = MatchSimulationEngine.ComputeSectorBreakdown(
            formation, starters.Select(id => players[id]).ToArray(), home, manager.Identity, allowClassic505);
        return new(formation, manager.Identity.ToString(), morale, home ? MatchSimulationEngine.HomeAdvantageMultiplier : 1,
            starters.ToArray(), snapshot, sectors);
    }

    private static void ValidateKickoff(MatchFixture match, Kickoff kickoff, IReadOnlyDictionary<Guid, Player> players)
    {
        Assert.True(match.StatisticsLineupCaptured);
        void ValidateSide(Guid clubId, Side side)
        {
            var official = match.StatisticsInitialLineup.Where(p => p.ClubId == clubId).ToArray();
            Assert.Equal(side.Starters, official.Where(p => p.IsStarter).Select(p => p.PlayerId));
            var kickoffRoster = side.Squad.ToDictionary(p => p.Id);
            foreach (var p in official)
            {
                Assert.Equal(kickoffRoster[p.PlayerId].Strength, p.Strength);
                Assert.Equal(kickoffRoster[p.PlayerId].Position, p.Position);
            }
            foreach (var entry in match.Events.Where(e => e.ClubId == clubId && e.EventType == MatchEventType.Substitution))
            {
                var id = entry.PrimaryPlayerId!.Value;
                Assert.Contains(official, p => !p.IsStarter && p.PlayerId == id);
                // No strength growth or injury-day processing runs during the match.
                Assert.Equal(kickoffRoster[id].Strength, players[id].Strength);
            }
        }

        ValidateSide(match.HomeClubId, kickoff.Home);
        ValidateSide(match.AwayClubId, kickoff.Away);
    }

    private sealed record PlayerAtKickoff(Guid Id, string Name, PlayerPosition Position, int Strength,
        bool Star, int InjuryDays, int InjuryRounds, int SuspensionMatches);
    private sealed record Side(string Formation, string TacticalIdentity, int Morale, double HomeMultiplier,
        Guid[] Starters, PlayerAtKickoff[] Squad, SectorBreakdown Sectors);
    private sealed record Kickoff(Side Home, Side Away);
    private sealed record FixtureResult(HistoryMatchDto Match, DivisionTier HomeDivision, DivisionTier AwayDivision,
        int? HomePenaltyGoals, int? AwayPenaltyGoals, int Minutes);
    private sealed record Confrontation(FixtureResult Result, Kickoff Kickoff, string HomeFinalFormation,
        string AwayFinalFormation, MatchEventRecord[] Events);
}
