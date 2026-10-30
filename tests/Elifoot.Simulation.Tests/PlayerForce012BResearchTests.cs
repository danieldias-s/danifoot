using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

/// <summary>Characterizes the existing clone. Original observations remain explicitly absent.</summary>
public class PlayerForce012BResearchTests
{
    private static readonly string Output = Path.Combine(
        Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/audit-012b")), "force-012b");

    [Fact]
    public void RealInjuryAndReinjuryAtEveryRequestedStrengthForKeepersAndForwards()
    {
        Directory.CreateDirectory(Output);
        using var raw = new StreamWriter(Path.Combine(Output, "injury-probe-events.jsonl"));
        var summary = new List<object>();
        foreach (var tier in new[] { DivisionTier.PrimeiraDivisao, DivisionTier.SegundaDivisao,
            DivisionTier.TerceiraDivisao, DivisionTier.QuartaDivisao })
        foreach (var role in new[] { PlayerPosition.A, PlayerPosition.G })
        foreach (int strength in new[] { 1, 10, 25, 40, 50 })
        {
            var generated = WorldSeedFactory.CreateSeededWorld(seed: 199812);
            var clubs = generated.Clubs.Where(c => c.DivisionTier == tier).Take(2).ToList();
            var clubIds = clubs.Select(c => c.Id).ToHashSet();
            var world = new GameWorld { Seed = 199812, RoundNumber = 3, Clubs = clubs,
                Players = generated.Players.Where(p => p.ClubId is Guid id && clubIds.Contains(id)).ToList(), PlayerStatisticsVersion = 9 };
            var id = world.Players.First(p => p.ClubId == clubs[0].Id && p.Position == role).Id;
            world.Players.Single(p => p.Id == id).Strength = strength;
            foreach (var p in world.Players) { p.Behavior = PlayerBehavior.FairPlay; p.ContractRoundsRemaining = 10_000; }
            for (int episode = 1; episode <= 2; episode++)
            {
                string beforeEpisode = JsonSerializer.Serialize(world);
                bool observed = false;
                for (int attempt = 0; attempt < 512 && !observed; attempt++)
                {
                    // Rejected candidate trials start from the same snapshot. This is selected-event
                    // characterization, not a sample from which to estimate an injury probability.
                    var candidate = JsonSerializer.Deserialize<GameWorld>(beforeEpisode)!;
                    candidate.Seed = 199812 + attempt;
                    var home = candidate.Clubs[0]; var away = candidate.Clubs[1];
                    var player = candidate.Players.Single(p => p.Id == id);
                    Assert.True(player.IsAvailableForMatch);
                    var players = candidate.Players.ToDictionary(p => p.Id);
                    var m = Prepare(candidate, home, away, player, "STARTER", players);
                    string scenario = $"INJURY_PROBE:D{(int)tier}:{role}:S{strength}:E{episode}";
                    var outcome = Play(candidate, m, home, away, player, "STARTER", players, raw, scenario);
                    if (outcome.Injuries == 0) continue;
                    observed = true;
                    var injury = Assert.Single(m.Events, e => e.EventType == MatchEventType.Injury && e.PrimaryPlayerId == id);
                    int duration = player.InjuryRoundsRemaining;
                    Assert.InRange(duration, 1, 3);
                    Assert.Equal(strength, player.Strength);
                    MarketAndEconomyEngine.SettleMatchAndRoundFinancials(candidate, [m]);
                    Assert.Equal(duration, player.InjuryRoundsRemaining);
                    for (int step = 1; step <= duration; step++)
                    {
                        candidate.RoundNumber++;
                        var recovery = Prepare(candidate, home, away, player, "BENCH_UNUSED", players);
                        Play(candidate, recovery, home, away, player, "BENCH_UNUSED", players, raw, scenario);
                        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(candidate, [recovery]);
                        Assert.Equal(duration - step, player.InjuryRoundsRemaining);
                        Assert.Equal(strength, player.Strength);
                    }
                    Assert.True(player.IsAvailableForMatch);
                    summary.Add(new { Scenario = scenario, Scope = "PROJECT_012_SELECTED_EVENT_PROBE", PlayerId = id,
                        Division = tier.ToString(), Position = role.ToString(), InitialStrength = strength,
                        Episode = episode, SelectedSeed = candidate.Seed, SearchAttempts = attempt + 1,
                        InjuryMinute = injury.Minute, Duration = duration, StrengthAfterInjury = strength,
                        StrengthAfterRecovery = player.Strength, OriginalObserved = false });
                    candidate.RoundNumber++;
                    world = candidate;
                }
                Assert.True(observed, $"No real injury found within the bounded search: {tier}/{role}/{strength}/{episode}");
            }
        }
        Assert.Equal(80, summary.Count);
        File.WriteAllText(Path.Combine(Output, "injury-probe-summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [InlineData(DivisionTier.PrimeiraDivisao)]
    [InlineData(DivisionTier.SegundaDivisao)]
    [InlineData(DivisionTier.TerceiraDivisao)]
    [InlineData(DivisionTier.QuartaDivisao)]
    public void AllRequestedStrengthParticipationAndTableCombinations(DivisionTier tier)
    {
        Directory.CreateDirectory(Output);
        using var rounds = new StreamWriter(Path.Combine(Output, $"rounds-division-{(int)tier}.jsonl"));
        using var injuries = new StreamWriter(Path.Combine(Output, $"injuries-division-{(int)tier}.jsonl"));
        var summaries = new List<object>();
        foreach (var role in new[] { PlayerPosition.A, PlayerPosition.G })
        foreach (int initialStrength in new[] { 1, 10, 25, 40, 50 })
        foreach (var mode in new[] { "STARTER", "BENCH_UNUSED", "HALFTIME", "LATE_80", "INJURED", "RECOVERED" })
        foreach (int rank in new[] { 1, 4, 8 })
        {
            var w = WorldSeedFactory.CreateSeededWorld(seed: 199812);
            var club = w.Clubs.First(c => c.DivisionTier == tier);
            var opponent = w.Clubs.First(c => c.DivisionTier == tier && c.Id != club.Id);
            var p = w.Players.First(x => x.ClubId == club.Id && x.Position == role);
            p.Strength = initialStrength;
            // Extra reserve keepers are fixture controls: a planned bench keeper must never force
            // an injured keeper to start merely because the other original keeper is unavailable.
            foreach (var fixtureClub in new[] { club, opponent })
            for (int extra = 0; extra < 2; extra++)
                w.Players.Add(new Player { Id = CoachCareerEngine.StableId($"012B:{fixtureClub.DeterministicIndex}:{extra}"),
                    GameWorldId = w.Id, ClubId = fixtureClub.Id, Name = $"Controle G {extra}",
                    Position = PlayerPosition.G, Strength = 25 });
            foreach (var x in w.Players)
            {
                x.ContractRoundsRemaining = 10_000;
                x.Behavior = PlayerBehavior.FairPlay;
            }
            var players = w.Players.ToDictionary(x => x.Id);
            string scenario = $"D{(int)tier}:{role}:S{initialStrength}:{mode}:P{rank}";
            if (mode == "INJURED") p.InjuryRoundsRemaining = 3;
            if (mode == "RECOVERED")
            {
                p.InjuryRoundsRemaining = 1;
                w.RoundNumber = 1;
                var warmup = Prepare(w, club, opponent, p, "BENCH_UNUSED", players);
                Play(w, warmup, club, opponent, p, "BENCH_UNUSED", players, injuries, scenario);
                MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [warmup]);
                Assert.Equal(0, p.InjuryRoundsRemaining);
                Assert.Equal(initialStrength, p.Strength);
            }
            int matches = 0, appearances = 0, injuryCount = 0, roundCount = 0;
            var seasonStrengths = new List<int>();
            for (int season = 1; season <= 2; season++)
            {
                w.SeasonNumber = season;
                foreach (var x in w.Players) x.SeasonGoals = x.CupGoals = x.SeasonMatches = 0;
                for (int round = 1; round <= w.TotalRoundsInSeason; round++)
                {
                    w.RoundNumber = round;
                    SetRank(w, club, rank);
                    int before = p.Strength, injuryBefore = p.InjuryRoundsRemaining;
                    int? afterEntry = null;
                    int afterWhistle = before, beforeRecovery = injuryBefore;
                    bool played = false;
                    int? entry = null;
                    if (CompetitionAndSeasonEngine.GetCompetitionTypeForRound(w, round) == CompetitionType.League)
                    {
                        var m = Prepare(w, club, opponent, p, mode, players);
                        if (m.HomeActiveStarterIds.Contains(p.Id))
                        {
                            p.SeasonMatches++;
                            p.CareerMatches++;
                            played = true;
                            entry = 0;
                            afterEntry = p.Strength;
                        }
                        var outcome = Play(w, m, club, opponent, p, mode, players, injuries, scenario);
                        entry ??= outcome.EntryMinute;
                        afterEntry = outcome.StrengthAfterEntry ?? afterEntry;
                        played |= outcome.EntryMinute.HasValue;
                        injuryCount += outcome.Injuries;
                        afterWhistle = p.Strength;
                        beforeRecovery = p.InjuryRoundsRemaining;
                        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [m]);
                        matches++;
                        if (played) appearances++;
                    }
                    int afterSettlement = p.Strength, injuryAfter = p.InjuryRoundsRemaining;
                    if (round == w.TotalRoundsInSeason)
                    {
                        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
                        string journal = JsonSerializer.Serialize(w.PlayerStrengthHistory);
                        PlayerStrengthEvolutionEngine.ApplySeasonEnd(w);
                        Assert.Equal(journal, JsonSerializer.Serialize(w.PlayerStrengthHistory));
                    }
                    var change = w.PlayerStrengthHistory.LastOrDefault(e => e.PlayerId == p.Id && e.Season == season && e.Round == round);
                    rounds.WriteLine(JsonSerializer.Serialize(new
                    {
                        Scope = "PROJECT_012", Scenario = scenario, Seed = w.Seed, PlayerId = p.Id,
                        Season = season, Round = round, Division = tier.ToString(), ClubId = club.Id,
                        ClubPosition = rank, Position = role.ToString(), InitialStrength = initialStrength,
                        ParticipationPlan = mode, Played = played, ActualEntryMinute = entry,
                        StrengthBefore = before, StrengthAfterEntry = afterEntry, StrengthAfterWhistle = afterWhistle,
                        StrengthAfterSettlement = afterSettlement, StrengthAfter = p.Strength,
                        InjuryRoundsBefore = injuryBefore, InjuryRoundsBeforeSettlement = beforeRecovery, InjuryRoundsAfter = injuryAfter,
                        ChangeReason = change?.ChangeReason, ChangeDetail = change?.Detail,
                        OriginalObserved = false, OriginalStrengthBefore = (int?)null, OriginalStrengthAfter = (int?)null
                    }));
                    if (afterEntry.HasValue) Assert.Equal(before, afterEntry.Value);
                    Assert.Equal(before, afterWhistle);
                    Assert.Equal(before, afterSettlement);
                    Assert.InRange(p.Strength, 1, 50);
                    Assert.InRange(p.Strength - before, -1, 2);
                    Assert.Equal(change != null, p.Strength != before);
                    roundCount++;
                }
                seasonStrengths.Add(p.Strength);
            }
            summaries.Add(new { Scenario = scenario, Scope = "PROJECT_012", Seed = w.Seed, Division = tier.ToString(),
                Position = role.ToString(), InitialStrength = initialStrength, ParticipationPlan = mode, ClubPosition = rank,
                Seasons = 2, Rounds = roundCount, Matches = matches, Appearances = appearances, Injuries = injuryCount,
                FinalStrength = p.Strength, StrengthBySeason = seasonStrengths, OriginalObserved = false });
        }
        Assert.Equal(180, summaries.Count);
        File.WriteAllText(Path.Combine(Output, $"summary-division-{(int)tier}.json"),
            JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static MatchFixture Prepare(GameWorld w, Club home, Club away, Player p, string mode,
        IReadOnlyDictionary<Guid, Player> players)
    {
        var homeSquad = w.Players.Where(x => x.ClubId == home.Id).ToList();
        var awaySquad = w.Players.Where(x => x.ClubId == away.Id).ToList();
        var (hs, hb) = AiManagerBrain.BuildStartersAndBench(homeSquad.Where(x => x.Id != p.Id).ToList(), TacticalFormations.All["4-4-2"]);
        var (aws, awb) = AiManagerBrain.BuildStartersAndBench(awaySquad, TacticalFormations.All["4-4-2"]);
        if (p.IsAvailableForMatch)
        {
            if (mode is "STARTER" or "RECOVERED" or "INJURED")
            {
                int index = hs.FindIndex(id => players[id].Position == p.Position);
                Assert.True(index >= 0);
                hs[index] = p.Id;
            }
            else
            {
                if (hb.Count >= SquadValidator.MaxBenchSize) hb.RemoveAt(hb.Count - 1);
                hb.Add(p.Id);
            }
        }
        var homeValid = SquadValidator.ValidatePreMatchLineup("4-4-2", hs, hb, homeSquad, true);
        var awayValid = SquadValidator.ValidatePreMatchLineup("4-4-2", aws, awb, awaySquad, true);
        Assert.True(homeValid.IsValid, homeValid.ErrorMessage);
        Assert.True(awayValid.IsValid, awayValid.ErrorMessage);
        if (!p.IsAvailableForMatch) Assert.DoesNotContain(p.Id, hs.Concat(hb));
        var m = new MatchFixture { GameWorldId = w.Id, SeasonNumber = w.SeasonNumber, RoundNumber = w.RoundNumber,
            HomeClubId = home.Id, AwayClubId = away.Id, DivisionTier = home.DivisionTier, DeterministicMatchIndex = 12,
            HomeFormation = "4-4-2", AwayFormation = "4-4-2", HomeActiveStarterIds = hs, HomeBenchIds = hb,
            AwayActiveStarterIds = aws, AwayBenchIds = awb };
        PlayerStatisticsEngine.CaptureLineup(m, players);
        return m;
    }

    private static (int? EntryMinute, int? StrengthAfterEntry, int Injuries) Play(GameWorld w, MatchFixture m,
        Club home, Club away, Player p, string mode, IReadOnlyDictionary<Guid, Player> players,
        StreamWriter injuries, string scenario)
    {
        int? entry = null, afterEntry = null;
        int count = 0;
        int target = mode == "HALFTIME" ? 45 : mode == "LATE_80" ? 80 : -1;
        for (int minute = 1; minute <= 90; minute++)
        {
            m.CurrentMinute = minute;
            int before = p.Strength;
            var events = MatchSimulationEngine.SimulateSingleMinute(m, home, away, players,
                ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, w.Seed);
            foreach (var e in events.Where(e => e.EventType == MatchEventType.Injury && e.PrimaryPlayerId == p.Id))
            {
                count++;
                injuries.WriteLine(JsonSerializer.Serialize(new { Scope = "PROJECT_012", Scenario = scenario,
                    Season = w.SeasonNumber, Round = w.RoundNumber, Minute = minute, StrengthBefore = before,
                    StrengthAfter = p.Strength, Duration = p.InjuryRoundsRemaining, OriginalObserved = false }));
            }
            Assert.Equal(before, p.Strength);
            // Pause after the completed minute: the incoming player first participates in the next tick.
            if (minute == target && m.HomeBenchIds.Contains(p.Id) && p.IsAvailableForMatch)
            {
                var outgoing = m.HomeActiveStarterIds.FirstOrDefault(id => players[id].Position == p.Position);
                if (outgoing != Guid.Empty)
                {
                    var substitution = MatchSimulationEngine.ApplyInMatchAdjustments(m, home, true,
                        minute == 45 ? RoundState.HALFTIME_PAUSED : RoundState.GLOBAL_PAUSED,
                        home.Id, null, [new(outgoing, p.Id)], players, true);
                    Assert.True(substitution.Success, substitution.ErrorMessage);
                    entry = minute;
                    afterEntry = p.Strength;
                }
            }
        }
        m.IsFinished = true;
        return (entry, afterEntry, count);
    }

    private static void SetRank(GameWorld w, Club club, int rank)
    {
        var others = w.Clubs.Where(c => c.DivisionTier == club.DivisionTier && c.Id != club.Id).ToList();
        club.LeagueWins = 8 - rank;
        club.LeagueDraws = club.LeagueGoalsFor = club.LeagueGoalsAgainst = 0;
        for (int i = 0; i < others.Count; i++)
        {
            others[i].LeagueWins = i < rank - 1 ? 20 + i : 0;
            others[i].LeagueDraws = others[i].LeagueGoalsFor = others[i].LeagueGoalsAgainst = 0;
        }
        if (rank == 8) club.LeagueGoalsAgainst = 1;
        Assert.Equal(rank, CompetitionAndSeasonEngine.GetDivisionStandings(w, club.DivisionTier)
            .Single(row => row.ClubId == club.Id).Position);
    }
}
