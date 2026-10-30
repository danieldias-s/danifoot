using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void Force012_ControlledForceOneParticipationAndTableMatrix()
    {
        string directory = Path.Combine(DocsDir, "force-012");
        Directory.CreateDirectory(directory);
        using var writer = new StreamWriter(Path.Combine(directory, "force-one-rounds.jsonl"));
        var summary = new List<object>();
        foreach (int position in new[] { 1, 4, 8 })
        foreach (int entryMinute in new[] { 0, -1, -2, 45, 60, 80 })
        {
            var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
            var club = w.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
            var away = w.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao && c.Id != club.Id);
            var seller = w.Clubs.First(c => c.DivisionTier == DivisionTier.Distrital);
            var p = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
            p.Strength = 1;
            Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w, club.Id, seller.Id, p.Id,
                10_000m, "Controlled force one", $"force012:{position}:{entryMinute}").Success);
            var players = w.Players.ToDictionary(p => p.Id);
            int games = 0, entered = 0;
            string participation = entryMinute switch
            {
                0 => "STARTER", -1 => "BENCH_UNUSED", -2 => "NOT_SELECTED",
                45 => "HALFTIME", 60 => "MINUTE_60", _ => "MINUTE_80"
            };
            var seasonStrengths = new List<int>();
            // A controlled 30-year player probe, not 30 complete 128-club competitions.
            // Division, table position and market isolation are intentional experimental controls.
            for (int season = 1; season <= 30; season++)
            {
                w.SeasonNumber = season;
                p.SeasonGoals = p.CupGoals = p.SeasonMatches = 0;
                for (int round = 1; round <= w.TotalRoundsInSeason; round++)
                {
                    w.RoundNumber = round;
                    SetRank(w, club, position);
                    int before = p.Strength;
                    bool played = false;
                    bool wasInjured = p.InjuryRoundsRemaining > 0;
                    if (CompetitionAndSeasonEngine.GetCompetitionTypeForRound(w, round) == CompetitionType.League)
                    {
                        var hsquad = w.Players.Where(x => x.ClubId == club.Id).ToList();
                        var asquad = w.Players.Where(x => x.ClubId == away.Id).ToList();
                        var (hs, hb) = AiManagerBrain.BuildStartersAndBench(hsquad.Where(x => x.Id != p.Id).ToList(), TacticalFormations.All["4-4-2"]);
                        var (aws, awb) = AiManagerBrain.BuildStartersAndBench(asquad, TacticalFormations.All["4-4-2"]);
                        if (p.IsAvailableForMatch && entryMinute == 0)
                        {
                            int index = hs.FindIndex(id => players[id].Position == PlayerPosition.A);
                            hb.Remove(p.Id); hb.Remove(hs[index]); hs[index] = p.Id;
                            p.SeasonMatches++;
                            played = true;
                        }
                        else if (p.IsAvailableForMatch && entryMinute != -2)
                        {
                            hb.Remove(p.Id);
                            if (hb.Count >= SquadValidator.MaxBenchSize) hb.RemoveAt(hb.Count - 1);
                            hb.Add(p.Id);
                        }
                        var m = new MatchFixture { GameWorldId = w.Id, SeasonNumber = season, RoundNumber = round,
                            HomeClubId = club.Id, AwayClubId = away.Id, DeterministicMatchIndex = 1,
                            HomeFormation = "4-4-2", AwayFormation = "4-4-2", HomeActiveStarterIds = hs, HomeBenchIds = hb,
                            AwayActiveStarterIds = aws, AwayBenchIds = awb, DivisionTier = DivisionTier.QuartaDivisao };
                        Assert.True(SquadValidator.ValidatePreMatchLineup("4-4-2", hs, hb, hsquad, true).IsValid);
                        PlayerStatisticsEngine.CaptureLineup(m, players);
                        for (int minute = 1; minute <= 90; minute++)
                        {
                            m.CurrentMinute = minute;
                            if (minute == entryMinute && hb.Contains(p.Id) && p.IsAvailableForMatch)
                            {
                                var outgoing = m.HomeActiveStarterIds.FirstOrDefault(id => players[id].Position == PlayerPosition.A);
                                if (outgoing != Guid.Empty)
                                {
                                    var result = MatchSimulationEngine.ApplyInMatchAdjustments(m, club, true,
                                        minute == 45 ? RoundState.HALFTIME_PAUSED : RoundState.GLOBAL_PAUSED,
                                        club.Id, null, [new(outgoing, p.Id)], players, true);
                                    Assert.True(result.Success, result.ErrorMessage);
                                    entered++; played = true;
                                }
                            }
                            MatchSimulationEngine.SimulateSingleMinute(m, club, away, players,
                                ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, w.Seed);
                            Assert.Equal(before, p.Strength);
                        }
                        m.IsFinished = true;
                        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [m]);
                        games++;
                    }
                    if (round == w.TotalRoundsInSeason) PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
                    var change = w.PlayerStrengthHistory.LastOrDefault(e => e.PlayerId == p.Id && e.Season == season && e.Round == round);
                    writer.WriteLine(JsonSerializer.Serialize(new
                    {
                        Seed = w.Seed, PlayerId = p.Id, Season = season, Round = round, ClubId = club.Id,
                        Division = club.DivisionTier.ToString(), ClubPosition = position, Participation = participation,
                        Played = played, WasInjured = wasInjured, StrengthBefore = before, StrengthAfter = p.Strength,
                        ChangeReason = change?.ChangeReason, Detail = change?.Detail,
                        ExperimentalControls = "Fixed division/table; no AI market; only this club fixture simulated"
                    }));
                    Assert.InRange(p.Strength - before, -1, 2);
                }
                seasonStrengths.Add(p.Strength);
                // Isolate the contracted player from contract expiry in this controlled experiment.
                p.ClubId = club.Id;
                foreach (var x in w.Players) x.ContractRoundsRemaining = 10_000;
            }
            Assert.True(p.Strength > 1);
            Assert.InRange(p.Strength, 2, 49);
            if (entryMinute < 0) Assert.Equal(0, entered);
            summary.Add(new { Position = position, Participation = participation, Seasons = 30, Games = games,
                Substitutions = entered, StrengthBySeason = seasonStrengths, FinalStrength = p.Strength });
        }
        File.WriteAllText(Path.Combine(directory, "force-one-summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetRank(GameWorld w, Club club, int position)
    {
        var others = w.Clubs.Where(c => c.DivisionTier == club.DivisionTier && c.Id != club.Id).ToList();
        club.LeagueWins = 8 - position;
        club.LeagueDraws = club.LeagueGoalsFor = club.LeagueGoalsAgainst = 0;
        for (int i = 0; i < others.Count; i++)
        {
            others[i].LeagueWins = i < position - 1 ? 20 + i : 0;
            others[i].LeagueDraws = others[i].LeagueGoalsFor = others[i].LeagueGoalsAgainst = 0;
        }
        // Use a negative goal difference solely to break the last-place zero-point tie.
        if (position == 8) club.LeagueGoalsAgainst = 1;
        Assert.Equal(position, CompetitionAndSeasonEngine.GetDivisionStandings(w, club.DivisionTier)
            .Single(row => row.ClubId == club.Id).Position);
    }
}
