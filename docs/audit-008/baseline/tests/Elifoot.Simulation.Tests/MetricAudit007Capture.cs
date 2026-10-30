using System.IO.Compression;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void Audit007_Reconstruct100Seasons_WithTransactionAndCrisisEvidence()
    {
        Directory.CreateDirectory(DocsDir);
        using var capture = new Audit007Capture(Path.Combine(DocsDir, "capture-007"));
        var result = RunMultiSeasonExperiment("EXP_007_100_SEASONS_199801", 199801, 100, ScenarioMode.ScenarioA_AllAi, capture);
        Assert.Equal(35100, result.TotalMatchesCompleted);
        Assert.Equal(0, result.InvalidLineupSelectionViolations);
        Assert.Equal(0, result.DoubleSettledMatchFinancialViolations);
        Assert.Equal(result.BilateralPurchaseDebitsTotal, result.BilateralSaleCreditsTotal);
        capture.Complete(result);
    }

    private sealed class DisciplineExposure
    {
        public long Minutes { get; set; }
        public int Appearances { get; set; }
        public int Yellows { get; set; }
        public int DirectReds { get; set; }
        public int SecondYellowReds { get; set; }
        public int SuspensionAwards { get; set; }
    }

    private sealed class Audit007Capture : IDisposable
    {
        private readonly string directory;
        private readonly StreamWriter ledger;
        private readonly StreamWriter finances;
        private readonly StreamWriter actions;
        private readonly HashSet<Guid> seenActions = new();
        private readonly Dictionary<Guid, TransferOfferStatus> seenOffers = new();
        private readonly HashSet<Guid> seen = new();
        private readonly Dictionary<Guid, decimal> lastCash = new();
        private readonly Dictionary<string, DisciplineExposure> behaviors = new();
        private readonly Dictionary<Guid, DisciplineExposure> players = new();
        private readonly List<object> redEvents = new();
        private readonly Dictionary<string, int> categories = new();
        private decimal initialCash;
        private decimal flows;
        private int unclassifiedFouls;
        private int matches;
        private int attributionsMissing;

        public Audit007Capture(string directory)
        {
            this.directory = directory;
            Directory.CreateDirectory(directory);
            ledger = Open("ledger.jsonl.gz");
            finances = Open("club-rounds.jsonl.gz");
            actions = Open("actions.jsonl.gz");
        }

        private StreamWriter Open(string file) => new(new GZipStream(File.Create(Path.Combine(directory, file)), CompressionLevel.Fastest));
        private void Write(string name, object data) => File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

        public void Begin(GameWorld w)
        {
            initialCash = w.Clubs.Sum(c => c.CashBalance);
            foreach (var tx in w.Transactions)
            {
                seen.Add(tx.Id);
                categories[tx.Category.ToString()] = categories.GetValueOrDefault(tx.Category.ToString()) + 1;
                ledger.WriteLine(JsonSerializer.Serialize(new { Transaction = tx, ObservedManagerId = (Guid?)null, Stage = "initial" }));
            }
            foreach (var c in w.Clubs) lastCash[c.Id] = c.CashBalance;
            Write("population.json", new { w.Seed, Seasons = 100, w.Clubs, w.AiManagers,
                Players = w.Players.Select(p => new { p.Id, p.Name, p.Position, p.Behavior, p.Strength, p.IsStar }) });
        }

        public void Observe(GameWorld w, string stage, int season, int round)
        {
            foreach (var action in w.AuditLogs.Where(a => seenActions.Add(a.Id)))
                actions.WriteLine(JsonSerializer.Serialize(new { Kind = "action", Action = action }));
            foreach (var offer in w.TransferOffers)
                if (!seenOffers.TryGetValue(offer.Id, out var status) || status != offer.Status)
                {
                    seenOffers[offer.Id] = offer.Status;
                    actions.WriteLine(JsonSerializer.Serialize(new { Kind = "offer", Offer = offer }));
                }
            var newTx = w.Transactions.Where(t => seen.Add(t.Id)).ToList();
            foreach (var t in newTx)
            {
                flows += t.Amount;
                categories[t.Category.ToString()] = categories.GetValueOrDefault(t.Category.ToString()) + 1;
                var c = w.Clubs.First(c => c.Id == t.ClubId);
                // Captured each round before manager rotation. Season-finalization contains no purchases.
                // Persisted ManagerId, once introduced, is authoritative; this is an explicit baseline observation.
                ledger.WriteLine(JsonSerializer.Serialize(new { Transaction = t, ObservedManagerId = c.HumanCoachId ?? c.AiManagerId, Stage = stage }));
            }
            var byClub = newTx.GroupBy(t => t.ClubId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var c in w.Clubs)
            {
                var tx = byClub.GetValueOrDefault(c.Id) ?? new List<FinancialTransaction>();
                var delta = c.CashBalance - lastCash[c.Id];
                if (delta != tx.Sum(t => t.Amount)) attributionsMissing++;
                finances.WriteLine(JsonSerializer.Serialize(new { ClubId = c.Id, c.Name, Season = season, Round = round,
                    Stage = stage, c.DivisionTier, Cash = c.CashBalance, CashBefore = lastCash[c.Id],
                    Flows = tx.GroupBy(t => t.Category.ToString()).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount)),
                    Sales = tx.Where(t => t.Category == FinancialTransactionCategory.TransferSale).Select(t => new { t.Id, t.Description, t.Amount }),
                    SalaryBill = w.Players.Where(p => p.ClubId == c.Id).Sum(p => p.SalaryPerRound),
                    c.Stadium.BuiltCapacity, c.Stadium.UsableCapacity, c.Stadium.ConditionPercent,
                    c.Stadium.ExpansionRoundsRemaining, c.Stadium.RenovationRoundsRemaining,
                    c.Stadium.LastAttendance, c.Stadium.LastTicketPrice }));
                lastCash[c.Id] = c.CashBalance;
            }
        }

        public void ObserveMatch(MatchFixture m, Club home, Club away, Dictionary<Guid, Player> map,
            Guid[] homeStart, Guid[] awayStart, string homeForm, string awayForm, int duration)
        {
            matches++;
            unclassifiedFouls += m.HomeFouls + m.AwayFouls;
            ObserveSide(home, homeStart, homeForm, true);
            ObserveSide(away, awayStart, awayForm, false);

            void ObserveSide(Club club, Guid[] initial, string formation, bool isHome)
            {
                var entered = initial.ToDictionary(id => id, _ => 1);
                var exposure = new Dictionary<Guid, int>();
                var sideEvents = m.Events.Where(e => e.ClubId == club.Id).ToList();
                foreach (var e in sideEvents)
                {
                    if (e.EventType == MatchEventType.Substitution)
                    {
                        Exit(e.SecondaryPlayerId, e.Minute);
                        if (e.PrimaryPlayerId is Guid incoming) entered[incoming] = e.Minute + 1;
                    }
                    if (e.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard)
                        Exit(e.PrimaryPlayerId, e.Minute);
                }
                foreach (var id in entered.Keys.ToList()) Exit(id, duration);
                foreach (var (id, minutes) in exposure)
                {
                    var p = map[id];
                    var b = Get(behaviors, p.Behavior.ToString());
                    var a = Get(players, id);
                    b.Minutes += minutes; a.Minutes += minutes;
                    if (minutes > 0) { b.Appearances++; a.Appearances++; }
                }
                var yellowSuspensions = isHome ? m.HomeSuspendedByYellowInMatchIds : m.AwaySuspendedByYellowInMatchIds;
                foreach (var id in yellowSuspensions)
                { Get(behaviors, map[id].Behavior.ToString()).SuspensionAwards++; Get(players, id).SuspensionAwards++; }
                foreach (var e in sideEvents.Where(e => e.EventType is MatchEventType.YellowCard or MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard))
                {
                    var p = map[e.PrimaryPlayerId!.Value];
                    var b = Get(behaviors, p.Behavior.ToString()); var a = Get(players, p.Id);
                    if (e.EventType == MatchEventType.YellowCard) { b.Yellows++; a.Yellows++; }
                    else
                    {
                        if (e.EventType == MatchEventType.DirectRedCard) { b.DirectReds++; a.DirectReds++; }
                        else { b.SecondYellowReds++; a.SecondYellowReds++; }
                        b.SuspensionAwards++; a.SuspensionAwards++;
                        string eventFormation = sideEvents.TakeWhile(x => x != e).LastOrDefault(x => x.EventType == MatchEventType.TacticChange)?.Description.Split(": ").Last().TrimEnd('.') ?? formation;
                        redEvents.Add(new { m.Id, m.SeasonNumber, m.RoundNumber, m.DeterministicMatchIndex,
                            e.Minute, e.EventType, PlayerId = p.Id, p.Name, p.Behavior, p.Strength,
                            ClubId = club.Id, club.DivisionTier, IsHome = isHome, Formation = eventFormation });
                    }
                }
                void Exit(Guid? id, int minute)
                {
                    if (id.HasValue && entered.Remove(id.Value, out int start))
                        exposure[id.Value] = exposure.GetValueOrDefault(id.Value) + Math.Max(0, minute - start + 1);
                }
            }
        }

        private static DisciplineExposure Get<T>(Dictionary<T, DisciplineExposure> map, T key) where T : notnull
        {
            if (!map.TryGetValue(key, out var value)) map[key] = value = new();
            return value;
        }

        public void Complete(ExperimentResult r)
        {
            ledger.Flush(); finances.Flush(); actions.Flush();
            Write("summary.json", new { r.WorldSeed, r.SeasonsCompleted, r.TotalMatchesCompleted, r.RealMinutesSimulated,
                r.TotalPurchases, r.AuctionsWon, r.BilateralPurchaseDebitsTotal, r.BilateralSaleCreditsTotal,
                r.ClubsEverInCrisis, r.ClubsRecoveredFromCrisis, r.FinalStateSignature,
                InitialCash = initialCash, RecordedNetFlows = flows, FinalCash = lastCash.Values.Sum(),
                CashLedgerMismatches = attributionsMissing, Categories = categories,
                Managers = r.ManagerTrackers.Values.Select(m => new { m.ManagerId, m.Name, m.Identity, m.PurchasesMade, m.SalesMade, m.InMatchTacticalShifts }),
                Discipline = new { Matches = matches, UnattributedFouls = unclassifiedFouls,
                    Note = "Unbooked fouls and penalty fouls have no offender in the engine. Do not invent attribution.",
                    ByBehavior = behaviors, ByPlayer = players, RedEvents = redEvents } });
            Assert.Equal(0, attributionsMissing);
            Assert.Equal(initialCash + flows, lastCash.Values.Sum());
        }

        public void Dispose() { ledger.Dispose(); finances.Dispose(); actions.Dispose(); }
    }
}
