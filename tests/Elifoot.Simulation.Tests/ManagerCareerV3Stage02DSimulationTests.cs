using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Simulation.Tests;

/// <summary>
/// V3 — Etapa 02D: Reproducible multi-season manager career simulations (1, 3, 5, 10, and 20 seasons)
/// across 128 AI clubs and 8 simulated humans + 120 AI clubs under both Classical Economy (02C)
/// and Calibrated Multiplayer Economy.
/// </summary>
public partial class MultiSeasonAndBenchmarkTests
{
    public sealed record CareerStage02DSimulationMetrics(
        string ScenarioName,
        string EconomyMode,
        int Seed,
        int Seasons,
        int TotalClubs,
        int HumanCoachesCount,
        int TotalMatchesSimulated,
        long TotalMinutesSimulated,
        int HumanInvitationsAccepted,
        int HumanInvitationsRejected,
        int AiInvitationsAccepted,
        int AiInvitationsRejected,
        int RedMoraleDismissals,
        int DistrictRelegationHumanDismissals,
        int AiRelegationDismissals,
        int TotalDismissals,
        int UnemployedRecoveriesCount,
        int TotalLeagueTitlesAwarded,
        int TotalCupTitlesAwarded,
        int ClubOwnershipViolations,
        int CoachExclusivityViolations,
        int EmploymentHistoryViolations,
        int HumanInDistrictViolations,
        string DeterministicSignature);

    [Theory]
    [InlineData(1, true, 8)]
    [InlineData(3, true, 8)]
    [InlineData(5, true, 8)]
    [InlineData(10, true, 8)]
    [InlineData(20, true, 8)]
    [InlineData(20, true, 0)]
    [InlineData(20, false, 8)]
    public async Task Stage02D_ManagerCareerMultiSeasonSimulations_ZeroCareerOrOwnershipViolations(
        int seasons,
        bool classicalEconomy,
        int humanCount)
    {
        var metrics = await RunStage02DCareerSimulationAsync(seasons, classicalEconomy, humanCount, seed: 199801);

        Assert.Equal(0, metrics.ClubOwnershipViolations);
        Assert.Equal(0, metrics.CoachExclusivityViolations);
        Assert.Equal(0, metrics.EmploymentHistoryViolations);
        Assert.Equal(0, metrics.HumanInDistrictViolations);
        Assert.Equal(seasons * 4, metrics.TotalLeagueTitlesAwarded);
        Assert.Equal(seasons, metrics.TotalCupTitlesAwarded);

        if (seasons == 1 && classicalEconomy && humanCount == 8)
        {
            var replay = await RunStage02DCareerSimulationAsync(1, true, 8, seed: 199801);
            Assert.Equal(metrics.DeterministicSignature, replay.DeterministicSignature);
        }

        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string outDir = Path.Combine(repoRoot, "artifacts", "v3-stage02d");
        Directory.CreateDirectory(outDir);
        string perRunFile = Path.Combine(outDir, $"manager-career-{seasons}s-{(classicalEconomy ? "classical" : "calibrated")}-{humanCount}humans.json");
        File.WriteAllText(perRunFile, JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));

        if (seasons == 20 && classicalEconomy && humanCount == 8)
        {
            Assert.True(metrics.HumanInvitationsAccepted > 0, "Expected human club switches over 20 seasons.");
            Assert.True(metrics.AiInvitationsAccepted > 0, "Expected AI club switches/hirings over 20 seasons.");
            Assert.True(metrics.TotalDismissals > 0, "Expected dismissals over 20 seasons.");

            string reportPath = Path.Combine(outDir, "manager-career-20s-classical-8humans.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static async Task<CareerStage02DSimulationMetrics> RunStage02DCareerSimulationAsync(
        int seasons,
        bool classicalEconomy,
        int humanCount,
        int seed)
    {
        var world = WorldSeedFactory.CreateSeededWorld($"V3_02D_{seasons}S_{(classicalEconomy ? "CLASSIC" : "CALIB")}_{humanCount}H", seed);
        if (classicalEconomy)
        {
            ClassicPlayerEconomyEngine.EnableClassicalEconomy(world);
        }

        var repo = new InMemoryWorldRepository();
        await repo.SaveWorldAsync(world);
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        var service = new ClubAndMarketApplicationService(repo, NullMatchRoomNotificationPublisher.Instance);

        for (int i = 0; i < humanCount; i++)
        {
            // Place humans across Divisions 1..4 (including 4th division so relegation to Distrital is exercised)
            var club = world.Clubs[i * 4];
            var join = await orchestrator.JoinWorldAndAssignClubAsync(
                world.Id,
                CoachCareerEngine.StableId($"02d-human-{i}"),
                $"Carreira02D {i + 1}",
                $"account:02d-acc-{i}",
                club.Id);
            Assert.True(join.Success, join.Error);
        }

        int matches = 0, humanAccepted = 0, humanRejected = 0;
        long minutes = 0;
        int clubOwnershipViolations = 0, coachExclusivityViolations = 0, employmentHistoryViolations = 0, humanInDistrictViolations = 0;
        var sigBuilder = new StringBuilder();

        for (int season = 1; season <= seasons; season++)
        {
            for (int round = 1; round <= world.TotalRoundsInSeason; round++)
            {
                world.RoundNumber = round;
                world.RoundState = RoundState.PREPARING;

                CoachCareerEngine.ProcessVacancies(world);

                foreach (var h in world.HumanCoaches)
                {
                    var offer = world.JobInvitations.FirstOrDefault(i => i.CoachId == h.Id && !i.IsAccepted && !i.IsRejected && !i.IsExpired);
                    if (offer == null) continue;
                    bool accept = (CoachCareerEngine.Ordinal(world) + world.HumanCoaches.IndexOf(h)) % 4 != 0;
                    string idemKey = $"sim02d:{season}:{round}:{h.Id}:{offer.Id}:{accept}";
                    var res = await service.RespondJobInvitationAsync(world.Id, h.Id, offer.Id, accept, default, idemKey);
                    if (res.Success)
                    {
                        if (accept) humanAccepted++;
                        else humanRejected++;
                    }
                }

                ClassicForceEvolutionEngine.PrepareRound(world);
                CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, round);
                var fixtures = world.Fixtures
                    .Where(f => f.SeasonNumber == season && f.RoundNumber == round)
                    .OrderBy(f => f.DeterministicMatchIndex)
                    .ToList();

                foreach (var club in world.Clubs.Where(c => c.HumanCoachId != null))
                {
                    var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
                    var spec = TacticalFormations.Resolve(club.PreferredFormation, world.AllowClassic505);
                    var (s, b) = AiManagerBrain.BuildStartersAndBench(squad, spec, world);
                    club.PreferredFormation = spec.Code;
                    club.PreferredStarterIds = s;
                    club.PreferredBenchIds = b;
                }

                var roundMetrics = new ExperimentResult();
                SimulateRoundWithRealMatchEngine(world, fixtures, roundMetrics);
                matches += fixtures.Count;
                minutes += roundMetrics.RealMinutesSimulated;

                foreach (var f in fixtures)
                {
                    sigBuilder.Append($"{season}:{round}:{f.DeterministicMatchIndex}:{f.HomeGoals}:{f.AwayGoals}:{f.HomePenaltyGoals}:{f.AwayPenaltyGoals};");
                }

                world.RoundState = RoundState.RESULTS;
                MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, fixtures);
                ResolveAutomatedHumanSalaryRequests(world);
                CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, fixtures);

                if (round % 2 == 0)
                {
                    MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
                }

                ResolveAutomatedHumanSalaryRequests(world);
                ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world);
                Assert.Null(SalarySeasonTransitionEngine.BlockingReason(world));

                VerifyStage02DInvariants(
                    world,
                    ref clubOwnershipViolations,
                    ref coachExclusivityViolations,
                    ref employmentHistoryViolations,
                    ref humanInDistrictViolations);
            }

            CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(world, expectedSeasonNumber: season);
            VerifyStage02DInvariants(
                world,
                ref clubOwnershipViolations,
                ref coachExclusivityViolations,
                ref employmentHistoryViolations,
                ref humanInDistrictViolations);
        }

        sigBuilder.Append(CareerSignature(world));
        string signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sigBuilder.ToString())));

        int aiAccepted = world.JobInvitations.Count(i => i.IsAiCandidate && i.IsAccepted);
        int aiRejected = world.JobInvitations.Count(i => i.IsAiCandidate && i.IsRejected);
        int redDismissals = world.AuditLogs.Count(a => a.ActionType == "COACH_DISMISSED" && a.Description == "RED_MORALE_LOSS");
        int distDismissals = world.AuditLogs.Count(a => a.ActionType == "COACH_DISMISSED" && a.Description.Contains("Distrital", StringComparison.OrdinalIgnoreCase));
        int aiRelDismissals = world.AuditLogs.Count(a => a.ActionType == "COACH_DISMISSED" && a.Description == "AI_RELEGATION");
        int totalDismissals = CoachCareerEngine.Coaches(world).Sum(c => c.Human?.Dismissals ?? c.Ai!.Dismissals);
        int unemployedRecoveries = CoachCareerEngine.Coaches(world).Sum(c => c.State.MoraleHistory.Count(m => m.Reason == "UNEMPLOYED_RECOVERY"));
        int leagueTitles = world.CoachTitles.Count(t => t.TitleType == "LEAGUE");
        int cupTitles = world.CoachTitles.Count(t => t.TitleType == "CUP");

        return new CareerStage02DSimulationMetrics(
            ScenarioName: humanCount == 0 ? "128_AI_CLUBS" : $"{humanCount}_HUMANS_{128 - humanCount}_AI",
            EconomyMode: world.Economy.PlayerEconomyRules.Policy,
            Seed: seed,
            Seasons: seasons,
            TotalClubs: world.Clubs.Count,
            HumanCoachesCount: world.HumanCoaches.Count,
            TotalMatchesSimulated: matches,
            TotalMinutesSimulated: minutes,
            HumanInvitationsAccepted: humanAccepted,
            HumanInvitationsRejected: humanRejected,
            AiInvitationsAccepted: aiAccepted,
            AiInvitationsRejected: aiRejected,
            RedMoraleDismissals: redDismissals,
            DistrictRelegationHumanDismissals: distDismissals,
            AiRelegationDismissals: aiRelDismissals,
            TotalDismissals: totalDismissals,
            UnemployedRecoveriesCount: unemployedRecoveries,
            TotalLeagueTitlesAwarded: leagueTitles,
            TotalCupTitlesAwarded: cupTitles,
            ClubOwnershipViolations: clubOwnershipViolations,
            CoachExclusivityViolations: coachExclusivityViolations,
            EmploymentHistoryViolations: employmentHistoryViolations,
            HumanInDistrictViolations: humanInDistrictViolations,
            DeterministicSignature: signature);
    }

    private static void VerifyStage02DInvariants(
        GameWorld world,
        ref int clubOwnershipViolations,
        ref int coachExclusivityViolations,
        ref int employmentHistoryViolations,
        ref int humanInDistrictViolations)
    {
        foreach (var club in world.Clubs)
        {
            // Exactly 1 coach (either Human XOR AI)
            if (!(club.HumanCoachId.HasValue ^ club.AiManagerId.HasValue))
            {
                clubOwnershipViolations++;
            }

            if (club.HumanCoachId is Guid hId)
            {
                if (club.DivisionTier == DivisionTier.Distrital)
                {
                    humanInDistrictViolations++;
                }
                var h = world.HumanCoaches.SingleOrDefault(c => c.Id == hId);
                if (h == null || h.CurrentClubId != club.Id)
                {
                    clubOwnershipViolations++;
                }
            }
            else if (club.AiManagerId is Guid aiId)
            {
                var ai = world.AiManagers.SingleOrDefault(m => m.Id == aiId);
                if (ai == null || ai.CurrentClubId != club.Id)
                {
                    clubOwnershipViolations++;
                }
            }
        }

        foreach (var c in CoachCareerEngine.Coaches(world))
        {
            if (c.ClubId is Guid clubId)
            {
                int matchingClubs = world.Clubs.Count(x => (x.HumanCoachId ?? x.AiManagerId) == c.Id);
                if (matchingClubs != 1)
                {
                    coachExclusivityViolations++;
                }
                int openJobs = c.Employment.Count(e => e.EndSeason == null);
                var activeJob = c.Employment.LastOrDefault(e => e.EndSeason == null);
                if (openJobs != 1 || activeJob == null || activeJob.ClubId != clubId)
                {
                    employmentHistoryViolations++;
                }
            }
            else
            {
                if (world.Clubs.Any(x => (x.HumanCoachId ?? x.AiManagerId) == c.Id))
                {
                    coachExclusivityViolations++;
                }
                if (c.Employment.Any(e => e.EndSeason == null))
                {
                    employmentHistoryViolations++;
                }
            }
        }
    }
}
