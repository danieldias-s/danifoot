using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void ClassicForce012C_100RealSeasonsAndPopulationBalance()
    {
        string directory = Path.Combine(DocsDir, "classic-force-012c");
        Directory.CreateDirectory(directory);
        using var history = new StreamWriter(new GZipStream(File.Create(Path.Combine(directory, "force-events.jsonl.gz")), CompressionLevel.Fastest));
        using var injuries = new StreamWriter(new GZipStream(File.Create(Path.Combine(directory, "injuries.jsonl.gz")), CompressionLevel.Fastest));
        using var probes = new StreamWriter(Path.Combine(directory, "force-one-probes.jsonl"));
        using var cards = new StreamWriter(new GZipStream(File.Create(Path.Combine(directory,"discipline-cards.jsonl.gz")),CompressionLevel.Fastest));
        using var bans = new StreamWriter(new GZipStream(File.Create(Path.Combine(directory,"discipline-suspensions.jsonl.gz")),CompressionLevel.Fastest));
        var cardOffsets=new Dictionary<Guid,int>();var banOffsets=new Dictionary<Guid,int>();
        int shortHandedMatches=0,unreplacedInjuries=0;
        var distributions = new List<object>();
        var usageDistributions = new List<object>();
        var leagueAppearances = new Dictionary<Guid,int>();
        int usageSeason = 0;
        var probeIds = new HashSet<Guid>();
        var injuryCounts = new Dictionary<Guid, int>();
        int lastEvent = 0, reserveEvents = 0, playedLosses = 0, dailyLosses = 0, cupGrowth = 0;
        int minimum = 50, maximum = 1, snapshots = 0;
        var market = new List<object>();
        var delays = new List<ForceCalendarDelay>();
        var seenTransfers = new HashSet<Guid>();
        int aiPurchases = 0, strongPurchases = 0;
        var result = RunMultiSeasonExperiment("CLASSIC_ADAPTED_FORCE_EVOLUTION_012C", 199801, 100,
            ScenarioMode.ScenarioA_AllAi, observeForce012: (w, phase) =>
            {
                if (usageSeason != w.SeasonNumber) { usageSeason=w.SeasonNumber; leagueAppearances.Clear(); }
                if (phase == "initial")
                    foreach (var tier in new[] { DivisionTier.PrimeiraDivisao, DivisionTier.SegundaDivisao, DivisionTier.TerceiraDivisao, DivisionTier.QuartaDivisao })
                    foreach (var role in new[] { PlayerPosition.G, PlayerPosition.A })
                    {
                        var club = w.Clubs.First(c => c.DivisionTier == tier);
                        var p = w.Players.First(p => p.ClubId == club.Id && p.Position == role);
                        p.Strength = 1;
                        p.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(p);
                        p.SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(p);
                        w.ClassicForce.SeasonStartingStrengths[p.Id] = 1;
                        probeIds.Add(p.Id);
                    }
                foreach (var e in w.PlayerStrengthHistory.Skip(lastEvent))
                {
                    history.WriteLine(JsonSerializer.Serialize(e));
                    if (e.ChangeReason == "RESERVE_PROGRESS") reserveEvents++;
                    if (e.ChangeReason == "PLAYED_MATCH_DECLINE") playedLosses++;
                    if (e.ChangeReason == "INJURY_DAILY_LOSS") dailyLosses++;
                    if (e.ReserveGrowth > 0 && e.GameDate == w.ClassicForce.GameDate
                        && phase == "round" && CompetitionAndSeasonEngine.GetCompetitionTypeForRound(w, w.RoundNumber) != CompetitionType.League) cupGrowth++;
                    Assert.InRange(e.StrengthAfter, 1, 50);
                    Assert.NotEqual(e.StrengthBefore, e.StrengthAfter);
                    if (e.ReserveGrowth > 0) Assert.True(e.StrengthAfter <= e.GrowthCeiling);
                }
                lastEvent = w.PlayerStrengthHistory.Count;
                foreach (var p in w.Players)
                {
                    foreach(var c in p.CardHistory.Skip(cardOffsets.GetValueOrDefault(p.Id)))cards.WriteLine(JsonSerializer.Serialize(new{p.Id,Card=c}));
                    foreach(var b in p.SuspensionHistory.Skip(banOffsets.GetValueOrDefault(p.Id)))bans.WriteLine(JsonSerializer.Serialize(new{p.Id,Suspension=b}));
                    cardOffsets[p.Id]=p.CardHistory.Count;banOffsets[p.Id]=p.SuspensionHistory.Count;
                    Assert.InRange(p.YellowCardsInCurrentCycle,0,2);
                    Assert.InRange(p.SuspensionMatchesRemaining,0,4);
                    foreach (var injury in p.ForceInjuries.Skip(injuryCounts.GetValueOrDefault(p.Id)))
                        injuries.WriteLine(JsonSerializer.Serialize(new { PlayerId = p.Id, p.Position, p.ClubId, Injury = injury }));
                    injuryCounts[p.Id] = p.ForceInjuries.Count;
                }
                var clubs = w.Clubs.ToDictionary(c => c.Id);
                if(phase=="round")
                    foreach(var m in w.Fixtures.Where(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber))
                    {
                        if(m.HomeActiveStarterIds.Count<11||m.AwayActiveStarterIds.Count<11)shortHandedMatches++;
                        unreplacedInjuries+=m.HomeInjuryVacancies.Count+m.AwayInjuryVacancies.Count;
                    }
                if (phase == "round")
                    foreach (var m in w.Fixtures.Where(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber&&m.CompetitionType==CompetitionType.League))
                    foreach (var (id,minutes) in ClassicForceEvolutionEngine.ParticipationMinutes(m))
                        if (minutes>0) leagueAppearances[id]=leagueAppearances.GetValueOrDefault(id)+1;
                if (phase == "round" && w.RoundNumber == w.TotalRoundsInSeason)
                    foreach (var group in w.Players.Where(p=>p.ClubId is Guid id&&clubs[id].DivisionTier!=DivisionTier.Distrital).GroupBy(p=>(
                        Division:clubs[p.ClubId!.Value].DivisionTier.ToString(), Position:p.Position.ToString(),
                        Usage:leagueAppearances.GetValueOrDefault(p.Id)==0?"Zero league minutes":leagueAppearances[p.Id]>=7?"Played at least 7 league games":"Played 1 to 6 league games",
                        InjuredThisSeason:p.ForceInjuries.Any(i=>i.Season==w.SeasonNumber))))
                        usageDistributions.Add(new {Season=w.SeasonNumber,group.Key.Division,group.Key.Position,group.Key.Usage,group.Key.InjuredThisSeason,
                            Count=group.Count(),Mean=group.Average(p=>p.Strength),At1=group.Count(p=>p.Strength==1),At50=group.Count(p=>p.Strength==50)});
                foreach (var transfer in w.Transactions.Where(t => t.ManagerId != null && t.SigningDecision != null))
                    if (seenTransfers.Add(transfer.Id))
                    {
                        aiPurchases++;
                        if (transfer.SigningDecision!.Strength >= 40) strongPurchases++;
                    }
                foreach (var p in w.Players.Where(p => probeIds.Contains(p.Id)))
                    probes.WriteLine(JsonSerializer.Serialize(new { Phase = phase, Season = w.SeasonNumber, Round = w.RoundNumber,
                        Date = w.ClassicForce.GameDate, p.Id, p.Position, p.Strength, p.ClubId, p.InjuryDaysRemaining,
                        Division = p.ClubId is Guid id ? clubs[id].DivisionTier.ToString() : "FreeAgent",
                        Ceiling = ClassicForceEvolutionEngine.ClubContext(w,p.ClubId is Guid cid ? clubs[cid] : null).Ceiling }));
                minimum = Math.Min(minimum, w.Players.Min(p => p.Strength));
                maximum = Math.Max(maximum, w.Players.Max(p => p.Strength));
                if (phase == "initial" || phase == "seasonFinalized" || (phase == "round" && w.RoundNumber == w.TotalRoundsInSeason))
                {
                    market.Add(new { Season = w.SeasonNumber, Phase = phase,
                        PlayersAtLeast40 = w.Players.Count(p => p.Strength >= 40),
                        ListedAtLeast40 = w.Players.Count(p => p.Strength >= 40 && p.IsListedForTransfer),
                        AverageFairValue = w.Players.Average(MarketAndEconomyEngine.CalculateFairValue),
                        Injured = w.Players.Count(p => p.InjuryDaysRemaining > 0) });
                    foreach (var group in w.Players.GroupBy(p => (
                        Division: p.ClubId is Guid id ? clubs[id].DivisionTier.ToString() : "FreeAgent",
                        Position: p.Position.ToString(),
                        ClubPosition: p.ClubId is Guid cid ? w.ClassicForce.CompletedLeaguePositions.GetValueOrDefault(cid, 0) : 0)))
                        distributions.Add(new { Season = w.SeasonNumber, Phase = phase, group.Key.Division, group.Key.Position, group.Key.ClubPosition,
                            Count = group.Count(), Minimum = group.Min(p => p.Strength), Maximum = group.Max(p => p.Strength),
                            Mean = group.Average(p => p.Strength), At1 = group.Count(p => p.Strength == 1), At50 = group.Count(p => p.Strength == 50),
                            Histogram = group.GroupBy(p => p.Strength).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()) });
                    Assert.True(w.Players.Count(p => p.Strength == 50) < w.Players.Count / 2);
                }
                if (phase == "round") snapshots = w.ClassicForce.TurnSnapshots.Count;
                delays = w.ClassicForce.CalendarDelays.ToList();
            }, forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        File.WriteAllText(Path.Combine(directory, "100-seasons.json"), JsonSerializer.Serialize(new
        {
            Mode = ClassicForceEvolutionEngine.Rule, Version = 1, Seed = 199801, result.SeasonsCompleted,
            result.TotalMatchesCompleted, result.RealMinutesSimulated, result.InvalidLineupSelectionViolations,
            result.DoubleSettledMatchFinancialViolations, result.TotalGoals, result.Injuries,
            result.TotalFouls,result.YellowCards,result.DirectRedCards,result.SecondYellowRedCards,result.RedCards,
            ShortHandedMatches=shortHandedMatches,UnreplacedInjuries=unreplacedInjuries,
            DisciplineCardRecords=cardOffsets.Values.Sum(),SuspensionHistoryRecords=banOffsets.Values.Sum(),
            ReserveEvents = reserveEvents, PlayedLosses = playedLosses, DailyInjuryLosses = dailyLosses,
            CupGrowth = cupGrowth, MinimumForce = minimum, MaximumForce = maximum, TurnSnapshots = snapshots,
            InjuryEpisodes = injuryCounts.Values.Sum(), Distributions = distributions,
            UsageDistributions = usageDistributions,
            Market = market, AiPurchases = aiPurchases, StrongAiPurchases = strongPurchases,
            result.TotalPurchases, result.AuctionsWon, result.BilateralPurchaseDebitsTotal, result.BilateralSaleCreditsTotal,
            CalendarDelays = delays,
            ProbeNote = "Eight force-1 players inserted initially (G/A in each division); other seed data and market unchanged"
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(35_100, result.TotalMatchesCompleted);
        Assert.Equal(0, result.InvalidLineupSelectionViolations);
        Assert.Equal(0, result.DoubleSettledMatchFinancialViolations);
        Assert.Equal(0, cupGrowth);
        Assert.True(reserveEvents > 0 && playedLosses > 0 && dailyLosses > 0);
        Assert.Equal(6_400, snapshots);
    }

    [Fact]
    public void ClassicForce012C_DeterministicReplayTwoRealSeasons()
    {
        string Replay()
        {
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var run=RunMultiSeasonExperiment("CLASSIC_REPLAY_012C",199813,2,ScenarioMode.ScenarioA_AllAi,
                observeForce012:(w,phase)=>
                {
                    string state=JsonSerializer.Serialize(new
                    {
                        Phase=phase,w.SeasonNumber,w.RoundNumber,w.ClassicForce.GameDate,
                        Players=w.Players.Select(p=>new {p.Id,p.ClubId,p.Strength,p.InjuryDaysRemaining,
                            Injuries=p.ForceInjuries.Select(i=>new{i.StartDate,i.Duration,i.InitialForce,i.TotalLoss,i.ProcessedDays,i.ActualLoss})}),
                        Events=w.PlayerStrengthHistory.Select(e=>new{e.PlayerId,e.Season,e.Round,e.GameDate,e.ClubId,e.ChangeReason,e.StrengthBefore,e.StrengthAfter})
                    });
                    hash.AppendData(Encoding.UTF8.GetBytes(state));
                },forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
            Assert.Equal(702,run.TotalMatchesCompleted);
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        string first=Replay(),second=Replay();
        Assert.Equal(first,second);
        string directory=Path.Combine(DocsDir,"classic-force-012c");Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"replay.json"),JsonSerializer.Serialize(new{Seed=199813,Seasons=2,First=first,Second=second,Identical=true}));
    }

    [Fact]
    public void ClassicForce012C_WeightedDuration100000Draws()
    {
        const int samples = 100_000;
        var rng = new DeterministicRng(120031);
        var counts = new int[14];
        for (int i = 0; i < samples; i++) counts[ClassicForceEvolutionEngine.DurationFromRoll(rng.NextInt(0, 200)) - 2]++;
        for (int i = 0; i < counts.Length; i++)
        {
            double p = ClassicForceEvolutionEngine.DurationWeights[i] / 200.0;
            double tolerance = 6 * Math.Sqrt(samples * p * (1 - p)) + 2;
            Assert.InRange((double)counts[i], samples * p - tolerance, samples * p + tolerance);
        }
        Assert.True(counts[13] > 0);
        string directory = Path.Combine(DocsDir, "classic-force-012c");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "weighted-duration-sample.json"), JsonSerializer.Serialize(new
        { Seed = 120031, Samples = samples, Counts = counts, Tolerance = "6 binomial standard deviations + 2 counts per duration" }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
