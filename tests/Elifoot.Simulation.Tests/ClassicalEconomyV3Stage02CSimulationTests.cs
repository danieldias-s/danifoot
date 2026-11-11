using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void ClassicalEconomyV3Stage02C_MultiSeasonValidation_Horizons(int horizonSeasons)
    {
        string outDir = Path.Combine(DocsDir, "audit-v3-stage02c", $"horizon-{horizonSeasons}s");
        Directory.CreateDirectory(outDir);

        var report = ExecuteClassicalEconomyMultiSeasonValidation(
            experimentName: $"V3_STAGE02C_HORIZON_{horizonSeasons}S",
            seed: 199802,
            seasons: horizonSeasons,
            scenarioMode: ScenarioMode.ScenarioB_EightHumanAgents,
            outputDirectory: outDir);

        Assert.Equal(horizonSeasons, report.SeasonsCompleted);
        Assert.Equal(horizonSeasons * 351, report.TotalMatchesCompleted);
        Assert.Equal(0, report.InvalidSquadLimitViolations);
        Assert.Equal(0, report.InvalidLineupSelectionViolations);
        Assert.Equal(0, report.DoubleSettledMatchFinancialViolations);
        Assert.InRange(report.FinalInflationIndex, 0.5, 10.0);
        Assert.InRange(report.FinalInflationDisplay, 5, 100);
        Assert.True(report.TotalPurchases + report.TotalAuctionsOpened > 0, "Market must remain active.");
    }

    [Fact]
    public void ClassicalEconomyV3Stage02C_AllAi_20Seasons_FullAuditAndCohortBreakdown()
    {
        string outDir = Path.Combine(DocsDir, "audit-v3-stage02c", "all-ai-20s");
        Directory.CreateDirectory(outDir);

        var report = ExecuteClassicalEconomyMultiSeasonValidation(
            experimentName: "V3_STAGE02C_ALL_AI_20S",
            seed: 199801,
            seasons: 20,
            scenarioMode: ScenarioMode.ScenarioA_AllAi,
            outputDirectory: outDir);

        Assert.Equal(20, report.SeasonsCompleted);
        Assert.Equal(20 * 351, report.TotalMatchesCompleted);
        Assert.Equal(0, report.InvalidSquadLimitViolations);
        Assert.Equal(0, report.DoubleSettledMatchFinancialViolations);
        Assert.Equal(20, report.SeasonSnapshots.Count);
        Assert.True(report.TotalPurchases > 0, "AI clubs must execute transfers under classical economy.");
        Assert.True(report.TotalSalaryRequestsGenerated > 0, "Spontaneous salary requests must occur across 20 seasons.");
    }

    private sealed class ClassicalEconomySimulationReport
    {
        public string ExperimentName { get; set; } = string.Empty;
        public int Seed { get; set; }
        public int SeasonsCompleted { get; set; }
        public string ScenarioMode { get; set; } = string.Empty;
        public int TotalMatchesCompleted { get; set; }
        public int InvalidSquadLimitViolations { get; set; }
        public int InvalidLineupSelectionViolations { get; set; }
        public int DoubleSettledMatchFinancialViolations { get; set; }
        public double InitialInflationIndex { get; set; }
        public double FinalInflationIndex { get; set; }
        public int FinalInflationDisplay { get; set; }
        public int FinalEconomyYearCounter { get; set; }
        public decimal InitialTotalCash { get; set; }
        public decimal FinalTotalCash { get; set; }
        public decimal FinalTotalDebt { get; set; }
        public int TotalPurchases { get; set; }
        public int TotalSales { get; set; }
        public int AiToAiTransfers { get; set; }
        public int HumanInvolvedTransfers { get; set; }
        public decimal TotalTransferVolume { get; set; }
        public decimal MinTransferPrice { get; set; }
        public decimal MaxTransferPrice { get; set; }
        public int TotalAuctionsOpened { get; set; }
        public int TotalAuctionsWon { get; set; }
        public int VoluntaryAuctionsCount { get; set; }
        public int MandatoryInsolvencyAuctionsCount { get; set; }
        public int SalaryRefusalAuctionsCount { get; set; }
        public int UnsoldAuctionsCount { get; set; }
        public int TotalSalaryRequestsGenerated { get; set; }
        public int SalaryRequestsAccepted { get; set; }
        public int SalaryRequestsRefused { get; set; }
        public int VoluntaryContractRenewals { get; set; }
        public int TotalContractHistoryEvents { get; set; }
        public List<ClassicalSeasonSnapshot> SeasonSnapshots { get; set; } = new();
    }

    private sealed class ClassicalSeasonSnapshot
    {
        public int Season { get; set; }
        public double InflationIndex { get; set; }
        public int InflationDisplay { get; set; }
        public int EconomyYearCounter { get; set; }
        public decimal TotalCash { get; set; }
        public decimal MeanClubCash { get; set; }
        public decimal MedianClubCash { get; set; }
        public decimal MinClubCash { get; set; }
        public decimal MaxClubCash { get; set; }
        public decimal TotalDebt { get; set; }
        public int InsolventClubsCount { get; set; }
        public int NegativeCashClubsCount { get; set; }
        public decimal MeanPlayerSalary { get; set; }
        public decimal MinPlayerSalary { get; set; }
        public decimal MaxPlayerSalary { get; set; }
        public decimal MeanMarketValue { get; set; }
        public decimal MinMarketValue { get; set; }
        public decimal MaxMarketValue { get; set; }
        public decimal MeanCandidatePriceSameCountry { get; set; }
        public int CumulativePurchases { get; set; }
        public int CumulativeAuctionsOpened { get; set; }
        public int CumulativeAuctionsUnsold { get; set; }
        public int CumulativeSalaryRequests { get; set; }
        public int CumulativeVoluntaryRenewals { get; set; }
        public List<ClassicalCohortSummary> DivisionCohorts { get; set; } = new();
        public List<ClassicalCohortSummary> WealthAndStrengthCohorts { get; set; } = new();
        public List<ClassicalCohortSummary> ControlCohorts { get; set; } = new();
    }

    private sealed class ClassicalCohortSummary
    {
        public string CohortName { get; set; } = string.Empty;
        public int ClubCount { get; set; }
        public decimal MeanCash { get; set; }
        public decimal MedianCash { get; set; }
        public decimal MeanWageBillPerRound { get; set; }
        public decimal MeanSquadMarketValue { get; set; }
        public double MeanSquadStrength { get; set; }
        public double MinSquadStrength { get; set; }
        public double MaxSquadStrength { get; set; }
        public int InsolventCount { get; set; }
    }

    private static ClassicalEconomySimulationReport ExecuteClassicalEconomyMultiSeasonValidation(
        string experimentName,
        int seed,
        int seasons,
        ScenarioMode scenarioMode,
        string outputDirectory)
    {
        var seenTx = new HashSet<Guid>();
        var txKeySet = new HashSet<string>();
        var transferPairs = new Dictionary<Guid, (int Count, decimal Net)>();
        var seenAuctionIds = new HashSet<Guid>();
        var closedAuctionIds = new HashSet<Guid>();
        var snapshots = new List<ClassicalSeasonSnapshot>();

        decimal initialCash = 0m;
        decimal cumulativeTxFlow = 0m;
        double initialInflation = 2.0;
        int voluntaryRenewalsCount = 0;
        int voluntaryAuctionsOpened = 0;
        int mandatoryInsolvencyAuctions = 0;
        int salaryRefusalAuctions = 0;
        int unsoldAuctions = 0;
        GameWorld? finalWorld = null;

        var expResult = RunMultiSeasonExperiment(
            experimentId: experimentName,
            worldSeed: seed,
            seasonsToSimulate: seasons,
            scenarioMode: scenarioMode,
            observeForce012: (w, phase) =>
            {
                RuntimePlayerProvenance.EnsureGeneratedPlayers(w);
                if (phase == "initial")
                {
                    w.Economy.OperatingRules = OperatingEconomyEngine.CurrentRules();
                    ClassicPlayerEconomyEngine.EnableClassicalEconomy(w, 1, true);
                    initialCash = w.Clubs.Sum(c => c.CashBalance);
                    initialInflation = w.Economy.InflationIndex;
                }

                // Reconcile every new financial transaction
                foreach (var tx in w.Transactions.Where(t => seenTx.Add(t.Id)))
                {
                    Assert.True(txKeySet.Add($"{tx.ClubId}:{tx.IdempotencyKey}"));
                    if (tx.Category == FinancialTransactionCategory.InitialBalance) continue;
                    cumulativeTxFlow += tx.Amount;
                    if (tx.SellerClubId != null && tx.TransferTransactionId is Guid pairId)
                    {
                        var cur = transferPairs.GetValueOrDefault(pairId);
                        transferPairs[pairId] = (cur.Count + 1, cur.Net + tx.Amount);
                    }
                }
                Assert.Equal(initialCash + cumulativeTxFlow, w.Clubs.Sum(c => c.CashBalance));

                // Exercise human coach voluntary renewal & voluntary auction in round 3/7 for human clubs
                if (phase == "round" && scenarioMode == ScenarioMode.ScenarioB_EightHumanAgents)
                {
                    if (w.RoundNumber == 3)
                    {
                        foreach (var hc in w.HumanCoaches.Where(h => h.CurrentClubId != null).Take(2))
                        {
                            var club = w.Clubs.First(c => c.Id == hc.CurrentClubId!.Value);
                            var candidate = w.Players
                                .Where(p => p.ClubId == club.Id && p.ContractRoundsRemaining <= 6 && !ClassicEconomyEngine.IsSeasonRenewed(w, p))
                                .OrderByDescending(p => p.Strength)
                                .FirstOrDefault();
                            if (candidate != null && club.CashBalance > 250_000m)
                            {
                                var preview = ClassicEconomyEngine.PreviewRenewal(w, club, candidate);
                                var renewRes = ClassicEconomyEngine.RenewContract(
                                    w, club, candidate, 10, $"sim-vol-renew:{hc.Id}:S{w.SeasonNumber}:R{w.RoundNumber}",
                                    preview.RequiredSalary, preview.TermsToken);
                                if (renewRes.Success) voluntaryRenewalsCount++;
                            }
                        }
                    }
                    else if (w.RoundNumber == 7)
                    {
                        foreach (var hc in w.HumanCoaches.Where(h => h.CurrentClubId != null).Skip(2).Take(1))
                        {
                            var club = w.Clubs.First(c => c.Id == hc.CurrentClubId!.Value);
                            var squad = w.Players.Where(p => p.ClubId == club.Id).ToList();
                            if (squad.Count >= 18)
                            {
                                var benchPlayer = squad
                                    .Where(p => p.Position != PlayerPosition.G
                                        && !club.PreferredStarterIds.Contains(p.Id)
                                        && !ClassicEconomyEngine.IsSeasonRenewed(w, p)
                                        && squad.Count(x => x.Position == p.Position) >= 5)
                                    .OrderBy(p => p.Strength)
                                    .FirstOrDefault();
                                if (benchPlayer != null)
                                {
                                    _ = VoluntaryAuctionEngine.Create(
                                        w, club, benchPlayer.Id, $"sim-vol-auc:{hc.Id}:S{w.SeasonNumber}:R{w.RoundNumber}");
                                }
                            }
                        }
                    }
                }

                // Track auctions and validate all player salaries/values
                foreach (var a in w.Auctions)
                {
                    if (seenAuctionIds.Add(a.Id))
                    {
                        if (a.Reason == AuctionReason.Voluntary) voluntaryAuctionsOpened++;
                        else if (a.Reason == AuctionReason.FinancialInsolvency) mandatoryInsolvencyAuctions++;
                        else if (a.Reason == AuctionReason.SalaryRenewalRefused) salaryRefusalAuctions++;
                    }
                    if (a.IsClosed && closedAuctionIds.Add(a.Id) && a.ClosureReason == "AUCTION_UNSOLD")
                    {
                        unsoldAuctions++;
                    }
                }

                // Validate strict bounds on all players and clubs
                Assert.InRange(w.Economy.InflationIndex, 0.5, 10.0);
                Assert.InRange(w.Economy.InflationDisplayIndex, 5, 100);
                foreach (var p in w.Players)
                {
                    Assert.InRange(p.Strength, 1, 50);
                    Assert.True(p.SalaryPerRound > 0m && p.SalaryPerRound <= 250_000m, $"Invalid salary {p.SalaryPerRound} for player {p.DisplayName}");
                    decimal mv = MarketAndEconomyEngine.CalculateFairValue(w, p);
                    Assert.True(mv > 0m, $"Market value must be positive, got {mv}");
                }

                if (phase == "seasonFinalized")
                {
                    int completedSeason = w.SeasonNumber - 1;
                    var playersByClub = w.Players
                        .Where(p => p.ClubId != null)
                        .GroupBy(p => p.ClubId!.Value)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var c in w.Clubs)
                    {
                        var sq = playersByClub[c.Id];
                        Assert.InRange(sq.Count, 11, 24);
                        Assert.True(sq.Count(p => p.Position == PlayerPosition.G) >= 1);
                        Assert.True(sq.Count(p => p.Position != PlayerPosition.G) >= 10);
                    }

                    ClassicalCohortSummary SummarizeCohort(string name, List<Club> cohortClubs)
                    {
                        if (cohortClubs.Count == 0)
                        {
                            return new ClassicalCohortSummary { CohortName = name };
                        }
                        var sortedCash = cohortClubs.Select(c => c.CashBalance).OrderBy(x => x).ToList();
                        decimal medCash = sortedCash[sortedCash.Count / 2];
                        var squadStrengths = cohortClubs.Select(c => playersByClub[c.Id].Average(p => p.Strength)).ToList();
                        return new ClassicalCohortSummary
                        {
                            CohortName = name,
                            ClubCount = cohortClubs.Count,
                            MeanCash = Math.Round(cohortClubs.Average(c => c.CashBalance), 2),
                            MedianCash = Math.Round(medCash, 2),
                            MeanWageBillPerRound = Math.Round(cohortClubs.Average(c => playersByClub[c.Id].Sum(p => p.SalaryPerRound)), 2),
                            MeanSquadMarketValue = Math.Round(cohortClubs.Average(c => playersByClub[c.Id].Sum(p => MarketAndEconomyEngine.CalculateFairValue(w, p))), 2),
                            MeanSquadStrength = Math.Round(squadStrengths.Average(), 2),
                            MinSquadStrength = Math.Round(squadStrengths.Min(), 2),
                            MaxSquadStrength = Math.Round(squadStrengths.Max(), 2),
                            InsolventCount = cohortClubs.Count(c => c.CashBalance < 0m || (w.Economy.Crises.TryGetValue(c.Id, out var cr) && cr.IsInsolvent))
                        };
                    }

                    var divisionCohorts = new List<ClassicalCohortSummary>
                    {
                        SummarizeCohort("PrimeiraDivisao", w.Clubs.Where(c => c.DivisionTier == DivisionTier.PrimeiraDivisao).ToList()),
                        SummarizeCohort("SegundaDivisao", w.Clubs.Where(c => c.DivisionTier == DivisionTier.SegundaDivisao).ToList()),
                        SummarizeCohort("TerceiraDivisao", w.Clubs.Where(c => c.DivisionTier == DivisionTier.TerceiraDivisao).ToList()),
                        SummarizeCohort("QuartaDivisao", w.Clubs.Where(c => c.DivisionTier == DivisionTier.QuartaDivisao).ToList()),
                        SummarizeCohort("Distrital", w.Clubs.Where(c => c.DivisionTier == DivisionTier.Distrital).ToList())
                    };

                    var activeDivClubs = w.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital).ToList();
                    var byWealth = activeDivClubs.OrderByDescending(c => c.CashBalance).ToList();
                    var byStrength = activeDivClubs.OrderByDescending(c => playersByClub[c.Id].Average(p => p.Strength)).ToList();

                    var wealthAndStrengthCohorts = new List<ClassicalCohortSummary>
                    {
                        SummarizeCohort("ClubesRicos_Top8", byWealth.Take(8).ToList()),
                        SummarizeCohort("ClubesPobres_Bottom8", byWealth.TakeLast(8).ToList()),
                        SummarizeCohort("ElencosFortes_Top8", byStrength.Take(8).ToList()),
                        SummarizeCohort("ElencosFracos_Bottom8", byStrength.TakeLast(8).ToList())
                    };

                    var controlCohorts = new List<ClassicalCohortSummary>
                    {
                        SummarizeCohort("ClubesIA", w.Clubs.Where(c => c.HumanCoachId == null).ToList()),
                        SummarizeCohort("TecnicosHumanosSimulados", w.Clubs.Where(c => c.HumanCoachId != null).ToList())
                    };

                    var allCash = w.Clubs.Select(c => c.CashBalance).OrderBy(x => x).ToList();
                    var employedPlayers = w.Players.Where(p => p.ClubId != null).ToList();
                    var marketValues = employedPlayers.Select(p => MarketAndEconomyEngine.CalculateFairValue(w, p)).ToList();
                    var salaries = employedPlayers.Select(p => p.SalaryPerRound).ToList();
                    var sampleBuyer = w.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
                    var candidatePrices = employedPlayers.Take(64).Select(p => ClassicPlayerEconomyEngine.CalculateCandidateSigningPrice(w, p, sampleBuyer)).ToList();

                    snapshots.Add(new ClassicalSeasonSnapshot
                    {
                        Season = completedSeason,
                        InflationIndex = w.Economy.InflationIndex,
                        InflationDisplay = w.Economy.InflationDisplayIndex,
                        EconomyYearCounter = w.Economy.EconomyYearCounter,
                        TotalCash = w.Clubs.Sum(c => c.CashBalance),
                        MeanClubCash = Math.Round(w.Clubs.Average(c => c.CashBalance), 2),
                        MedianClubCash = (allCash[63] + allCash[64]) / 2m,
                        MinClubCash = allCash[0],
                        MaxClubCash = allCash[^1],
                        TotalDebt = w.BankLoans.Sum(l => l.RemainingBalance),
                        InsolventClubsCount = w.Clubs.Count(c => c.CashBalance < 0m || (w.Economy.Crises.TryGetValue(c.Id, out var cr) && cr.IsInsolvent)),
                        NegativeCashClubsCount = w.Clubs.Count(c => c.CashBalance < 0m),
                        MeanPlayerSalary = Math.Round(salaries.Average(), 2),
                        MinPlayerSalary = salaries.Min(),
                        MaxPlayerSalary = salaries.Max(),
                        MeanMarketValue = Math.Round(marketValues.Average(), 2),
                        MinMarketValue = marketValues.Min(),
                        MaxMarketValue = marketValues.Max(),
                        MeanCandidatePriceSameCountry = Math.Round(candidatePrices.Average(), 2),
                        CumulativePurchases = w.Transactions.Count(t => t.Category == FinancialTransactionCategory.TransferPurchase),
                        CumulativeAuctionsOpened = seenAuctionIds.Count,
                        CumulativeAuctionsUnsold = unsoldAuctions,
                        CumulativeSalaryRequests = w.Economy.SalaryRequests.Count,
                        CumulativeVoluntaryRenewals = voluntaryRenewalsCount,
                        DivisionCohorts = divisionCohorts,
                        WealthAndStrengthCohorts = wealthAndStrengthCohorts,
                        ControlCohorts = controlCohorts
                    });

                    finalWorld = w;
                }
            },
            forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);

        Assert.All(transferPairs.Values, p =>
        {
            Assert.Equal(2, p.Count);
            Assert.Equal(0m, p.Net);
        });

        var report = new ClassicalEconomySimulationReport
        {
            ExperimentName = experimentName,
            Seed = seed,
            SeasonsCompleted = expResult.SeasonsCompleted,
            ScenarioMode = scenarioMode.ToString(),
            TotalMatchesCompleted = expResult.TotalMatchesCompleted,
            InvalidSquadLimitViolations = expResult.InvalidSquadLimitViolations,
            InvalidLineupSelectionViolations = expResult.InvalidLineupSelectionViolations,
            DoubleSettledMatchFinancialViolations = expResult.DoubleSettledMatchFinancialViolations,
            InitialInflationIndex = initialInflation,
            FinalInflationIndex = finalWorld!.Economy.InflationIndex,
            FinalInflationDisplay = finalWorld.Economy.InflationDisplayIndex,
            FinalEconomyYearCounter = finalWorld.Economy.EconomyYearCounter,
            InitialTotalCash = initialCash,
            FinalTotalCash = finalWorld.Clubs.Sum(c => c.CashBalance),
            FinalTotalDebt = finalWorld.BankLoans.Sum(l => l.RemainingBalance),
            TotalPurchases = expResult.TotalPurchases,
            TotalSales = expResult.TotalSales,
            AiToAiTransfers = expResult.AiToAiTransfers,
            HumanInvolvedTransfers = expResult.HumanInvolvedTransfers,
            TotalTransferVolume = expResult.TotalTransferVolume,
            MinTransferPrice = expResult.MinTransferPrice,
            MaxTransferPrice = expResult.MaxTransferPrice,
            TotalAuctionsOpened = seenAuctionIds.Count,
            TotalAuctionsWon = expResult.AuctionsWon,
            VoluntaryAuctionsCount = voluntaryAuctionsOpened,
            MandatoryInsolvencyAuctionsCount = mandatoryInsolvencyAuctions,
            SalaryRefusalAuctionsCount = salaryRefusalAuctions,
            UnsoldAuctionsCount = unsoldAuctions,
            TotalSalaryRequestsGenerated = finalWorld.Economy.SalaryRequests.Count,
            SalaryRequestsAccepted = finalWorld.Economy.SalaryRequests.Count(r => r.Status == SalaryRequestStatus.ACCEPTED),
            SalaryRequestsRefused = finalWorld.Economy.SalaryRequests.Count(r => r.Status == SalaryRequestStatus.REFUSED),
            VoluntaryContractRenewals = voluntaryRenewalsCount,
            TotalContractHistoryEvents = finalWorld.Economy.ContractHistory.Count,
            SeasonSnapshots = snapshots
        };

        File.WriteAllText(
            Path.Combine(outputDirectory, "summary.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        return report;
    }
}
