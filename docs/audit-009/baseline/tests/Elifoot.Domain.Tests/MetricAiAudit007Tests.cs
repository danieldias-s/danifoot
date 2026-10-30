using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class MetricAiAudit007Tests
{
    private static readonly string Output = Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/audit-007/controlled"));
    private static void Write(string name, object data) { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, name), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); }
    internal static (GameWorld World, Club Club, AiManager Manager, MatchFixture Match) ReactionFixture(int adaptability = 50)
    {
        var w = WorldSeedFactory.CreateSeededWorld();
        var club = w.Clubs[0];
        var manager = w.AiManagers.Single(m => m.Id == club.AiManagerId);
        manager.Identity = ManagerTacticalIdentity.Equilibrado;
        manager.Adaptability = adaptability;
        manager.PreferredFormations = [new("4-4-2", 60), new("5-3-2", 25), new("4-3-3", 15)];
        var squad = w.Players.Where(p => p.ClubId == club.Id).ToList();
        foreach (var p in squad) { p.Strength = 30; p.InjuryRoundsRemaining = 0; p.SuspensionRoundsRemaining = 0; }
        var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, TacticalFormations.All["4-4-2"]);
        var match = new MatchFixture { HomeClubId = club.Id, AwayClubId = w.Clubs[1].Id,
            CurrentMinute = 83, HomeFormation = "4-4-2", HomeActiveStarterIds = starters,
            HomeBenchIds = bench, AwayGoals = 3 };
        return (w, club, manager, match);
    }

    [Theory]
    [InlineData(0)] [InlineData(20)] [InlineData(40)] [InlineData(50)] [InlineData(60)]
    [InlineData(64)] [InlineData(65)] [InlineData(70)] [InlineData(80)] [InlineData(90)] [InlineData(100)]
    public void ExtremeDeficit_ReactsWithSuitablePreferredFormation_AtEveryAdaptability(int adaptability)
    {
        var (w, club, manager, match) = ReactionFixture(adaptability);
        // Ensure the third preference can be staffed with available players.
        var attacker = w.Players.First(p => p.ClubId == club.Id && p.Position == PlayerPosition.A && !match.HomeActiveStarterIds.Contains(p.Id));
        if (!match.HomeBenchIds.Contains(attacker.Id)) match.HomeBenchIds.Add(attacker.Id);
        AiManagerBrain.EvaluateAiInMatchReaction(match, club, manager, true, RoundState.RUNNING_SECOND_HALF, w.Players.ToDictionary(p => p.Id));
        Assert.Equal("4-3-3", match.HomeFormation);
        Assert.True(match.HomeUsedSubstitutions <= 5);
        Assert.True(match.HomeUsedPauseWindows <= 3);
        var events = match.Events.Count;
        AiManagerBrain.EvaluateAiInMatchReaction(match, club, manager, true, RoundState.RUNNING_SECOND_HALF, w.Players.ToDictionary(p => p.Id));
        Assert.Equal(events, match.Events.Count); // A repeated clock notification is not another decision.
        Assert.Equal("4-4-2", manager.PreferredFormations[0].Formation);
        Assert.Equal(ManagerTacticalIdentity.Equilibrado, manager.Identity);
    }

    [Fact]
    public void ReloadedRoundSettlement_DoesNotAgePlayersLoansOrWorksAgain()
    {
        var (w, club, _, match) = ReactionFixture();
        match.IsFinished = true; w.Fixtures = [match];
        var player = w.Players.First(p => p.ClubId == club.Id);
        player.ContractRoundsRemaining = 8; player.InjuryRoundsRemaining = 4; player.SuspensionRoundsRemaining = 3;
        club.Stadium.RenovationRoundsRemaining = 3;
        club.Stadium.ExpansionRoundsRemaining = 3; club.Stadium.ExpansionTargetSeats = 1000;
        w.BankLoans.Add(new BankLoan { ClubId = club.Id, RemainingBalance = 100_000, RemainingRounds = 10, InstallmentPerRound = 12_000 });
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [match]);
        var after = JsonSerializer.Serialize(w);
        var loaded = JsonSerializer.Deserialize<GameWorld>(after)!;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(loaded, loaded.Fixtures);
        Assert.Equal(after, JsonSerializer.Serialize(loaded));
    }

    [Fact]
    public void Legacy006Snapshot_ReconstructsRoundGuardFromCompleteSponsorshipPass()
    {
        var (w, _, _, match) = ReactionFixture(); match.IsFinished = true; w.Fixtures = [match];
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, [match]);
        w.SettledFinancialRounds.Clear();
        var loaded = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var playersBefore = JsonSerializer.Serialize(loaded.Players);
        var moneyBefore = loaded.Clubs.Sum(c => c.CashBalance); var txBefore = loaded.Transactions.Count;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(loaded, loaded.Fixtures);
        Assert.Equal(playersBefore, JsonSerializer.Serialize(loaded.Players));
        Assert.Equal(moneyBefore, loaded.Clubs.Sum(c => c.CashBalance)); Assert.Equal(txBefore, loaded.Transactions.Count);
    }

    [Fact]
    public void Transfers_ReconcileOnce_AndRetainResponsibleManagerAfterClubChangeAndReload()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); var buyer = w.Clubs[0]; var seller = w.Clubs[1];
        var originalManager = buyer.AiManagerId;
        var player = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        var total = w.Clubs.Sum(c => c.CashBalance);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, player.Id, 100_000, "Reserva tático", "007-a").Success);
        var purchase = w.Transactions.Single(t => t.Category == FinancialTransactionCategory.TransferPurchase);
        buyer.AiManagerId = seller.AiManagerId; seller.AiManagerId = originalManager;
        var next = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, next.Id, 100_000, "Titular urgente", "007-b", "AcceptedOffer").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, next.Id, 100_000, "duplicate", "007-b").Success);
        var loaded = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        Assert.Equal(originalManager, loaded.Transactions.Single(t => t.Id == purchase.Id).ManagerId);
        Assert.Equal(buyer.AiManagerId, loaded.Transactions.Last(t => t.Category == FinancialTransactionCategory.TransferPurchase).ManagerId);
        Assert.Equal(total, loaded.Clubs.Sum(c => c.CashBalance));
        Assert.Equal(2, loaded.Transactions.Count(t => t.Category == FinancialTransactionCategory.TransferPurchase));
        foreach (var buy in loaded.Transactions.Where(t => t.Category == FinancialTransactionCategory.TransferPurchase))
        {
            Assert.Equal(buy.Id, buy.TransferTransactionId); Assert.NotNull(buy.PlayerId); Assert.Equal(buyer.Id, buy.BuyerClubId);
            var sell = loaded.Transactions.Single(t => t.Category == FinancialTransactionCategory.TransferSale && t.TransferTransactionId == buy.Id);
            Assert.Equal(0m, buy.Amount + sell.Amount); Assert.Equal(seller.Id, buy.SellerClubId);
            Assert.NotNull(buy.SigningDecision); Assert.NotEmpty(buy.SigningDecision!.Reason);
        }
        var oldMetrics = AiMarketMetrics.FromRecords(originalManager!.Value, loaded.Transactions, loaded.AuditLogs, loaded.TransferOffers);
        var newMetrics = AiMarketMetrics.FromRecords(buyer.AiManagerId!.Value, loaded.Transactions, loaded.AuditLogs, loaded.TransferOffers);
        Assert.Equal(1,oldMetrics.AiPurchasesCompleted); Assert.Equal(1,newMetrics.AiPurchasesCompleted);
        Assert.Equal(0,oldMetrics.AiOffersAccepted); Assert.Equal(1,newMetrics.AiOffersAccepted);
    }

    [Fact]
    public void TransferWithExistingSellerLeg_DoesNotCreateAnUnpairedDebit()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); var buyer = w.Clubs[0]; var seller = w.Clubs[1];
        var player = w.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        MarketAndEconomyEngine.TryRecordTransaction(w, seller, FinancialTransactionCategory.TransferSale, 100_000, "existing", "partial:sell");
        var money = buyer.CashBalance;
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w, buyer.Id, seller.Id, player.Id, 100_000, "test", "partial").Success);
        Assert.Equal(money, buyer.CashBalance); Assert.Equal(seller.Id, player.ClubId);
    }

    [Fact]
    public void AuctionBids_AreNotPurchases_AndExpiredContractHasNoSellerCredit()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); var buyer = w.Clubs[0];
        foreach (var c in w.Clubs.Where(c => c.Id != buyer.Id)) c.HumanCoachId = Guid.NewGuid();
        var p = w.Players.First(p => p.ClubId == w.Clubs[1].Id && p.Position == PlayerPosition.A);
        p.ClubId = null; p.Strength = 45; buyer.CashBalance = 20_000_000;
        var auction = new AuctionItem { PlayerId = p.Id, FormerClubId = w.Clubs[1].Id, MinimumBid = 100_000, ExpiresAtRound = 2, CreatedAtSeason = 1 };
        w.Auctions.Add(auction); MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.Contains(w.AuditLogs, a => a.ActionType == "AiAuctionBid");
        Assert.DoesNotContain(w.Transactions, t => t.Category == FinancialTransactionCategory.AuctionPurchase);
        MarketAndEconomyEngine.SettleAuction(w, auction); MarketAndEconomyEngine.SettleAuction(w, auction);
        var win = Assert.Single(w.Transactions, t => t.Category == FinancialTransactionCategory.AuctionPurchase);
        Assert.Equal("Auction", win.TransactionType); Assert.Null(win.SellerClubId); Assert.Equal(buyer.AiManagerId, win.ManagerId);
        Assert.DoesNotContain(w.Transactions, t => t.Category == FinancialTransactionCategory.AuctionSale);
    }

    [Fact]
    public void TiedScore_DoesNotCauseRandomFormationChanges_AndExhaustedSubsAllowFreeTacticalChange()
    {
        var (w, club, manager, match) = ReactionFixture(100); match.AwayGoals = 0;
        AiManagerBrain.EvaluateAiInMatchReaction(match, club, manager, true, RoundState.RUNNING_SECOND_HALF, w.Players.ToDictionary(p => p.Id));
        Assert.Equal("4-4-2", match.HomeFormation);
        // Formation-only adjustment uses existing players; the full substitution quota cannot veto it at a free interval.
        match.Events.Clear(); match.CurrentMinute = 90; match.AwayGoals = 3; match.HomeUsedSubstitutions = 5;
        manager.PreferredFormations = [new("4-4-2", 60), new("3-5-2", 40)];
        var defender = w.Players[match.HomeActiveStarterIds.Select(id => w.Players.FindIndex(p => p.Id == id)).First(i => w.Players[i].Position == PlayerPosition.D)];
        defender.Position = PlayerPosition.M;
        AiManagerBrain.EvaluateAiInMatchReaction(match, club, manager, true, RoundState.EXTRA_TIME_BREAK, w.Players.ToDictionary(p => p.Id));
        Assert.Equal("3-5-2", match.HomeFormation); Assert.Equal(5, match.HomeUsedSubstitutions);
    }

    [Fact]
    public void CrisisClub_RecoversThroughRecordedRevenue_AndCannotFundStadiumExpansion()
    {
        var w = WorldSeedFactory.CreateSeededWorld(); var club = w.Clubs[0]; club.CashBalance = 10_000;
        Assert.False(MarketAndEconomyEngine.StartStadiumExpansion(w, club, 5000).Success);
        var built = club.Stadium.BuiltCapacity; MarketAndEconomyEngine.ProcessSeasonalStadiumWearAndAiProjects(w);
        Assert.Equal(0, club.Stadium.ExpansionRoundsRemaining); Assert.Equal(built, club.Stadium.BuiltCapacity);
        var before = club.CashBalance;
        for (int r = 1; r <= 3; r++) { w.RoundNumber = r; MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w, []); }
        Assert.True(club.CashBalance >= 350_000);
        Assert.Equal(before + w.Transactions.Where(t => t.ClubId == club.Id && t.Category == FinancialTransactionCategory.Sponsorship).Sum(t => t.Amount), club.CashBalance);
        Assert.DoesNotContain(w.Transactions, t => t.ClubId == club.Id && t.Category == FinancialTransactionCategory.LoanDisbursement);
    }

    [Fact]
    public void AdaptabilityMatrix_UsesIdenticalSquadsSeedsAndScoreContexts()
    {
        var data = new List<object>();
        foreach (int a in new[] { 0,20,40,50,60,64,65,70,80,90,100 })
        foreach (int deficit in new[] { 0,1,3 })
        {
            var (w,c,m,match) = ReactionFixture(a); match.AwayGoals = deficit;
            var decisions = new List<object>();
            foreach (int minute in new[] {62,74,83})
            {
                match.CurrentMinute = minute;
                AiManagerBrain.EvaluateAiInMatchReaction(match,c,m,true,RoundState.RUNNING_SECOND_HALF,w.Players.ToDictionary(p => p.Id));
                decisions.Add(new { Minute = minute, Formation = match.HomeFormation, Subs = match.HomeUsedSubstitutions, Windows = match.HomeUsedPauseWindows });
            }
            data.Add(new { Adaptability = a, Deficit = deficit, Decisions = decisions, Events = match.Events.Select(e => new {e.Minute,e.EventType,e.Description}) });
        }
        Write("adaptability-controlled.json", data);
    }

    [Fact]
    public void HomogeneousBehaviorExposure_RecordsRealMinutesFoulsCardsAndSuspensions()
    {
        var rows = new List<object>();
        foreach (var behavior in Enum.GetValues<PlayerBehavior>())
        {
            long minutes = 0; int appearances = 0, fouls = 0, yellows = 0, direct = 0, second = 0, suspensions = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var (w,c,_,match) = ReactionFixture(); match.AwayGoals = 0;
                match.HomeBenchIds.Clear();
                var awaySquad = w.Players.Where(p => p.ClubId == match.AwayClubId).ToList();
                var (away, _) = AiManagerBrain.BuildStartersAndBench(awaySquad,TacticalFormations.All["4-4-2"]);
                match.AwayActiveStarterIds = away;
                foreach (var p in w.Players) { p.Strength=30; p.Behavior = p.ClubId == c.Id ? behavior : PlayerBehavior.FairPlay; }
                var map=w.Players.ToDictionary(p=>p.Id); appearances += match.HomeActiveStarterIds.Count;
                for (int minute=1;minute<=90;minute++)
                {
                    minutes += match.HomeActiveStarterIds.Count; match.CurrentMinute=minute;
                    MatchSimulationEngine.SimulateSingleMinute(match,c,w.Clubs[1],map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,7000+seed);
                }
                fouls+=match.HomeFouls; yellows+=match.Events.Count(e=>e.ClubId==c.Id && e.EventType==MatchEventType.YellowCard);
                direct+=match.Events.Count(e=>e.ClubId==c.Id && e.EventType==MatchEventType.DirectRedCard);
                second+=match.Events.Count(e=>e.ClubId==c.Id && e.EventType==MatchEventType.SecondYellowRedCard);
                suspensions+=match.HomeSentOffIds.Count+match.HomeSuspendedByYellowInMatchIds.Count;
                Assert.All(match.HomeSentOffIds,id=>Assert.True(map[id].SuspensionRoundsRemaining>=1));
                foreach(var e in match.Events.Where(e=>e.EventType==MatchEventType.SecondYellowRedCard))
                    Assert.Contains(match.Events,x=>x.PrimaryPlayerId==e.PrimaryPlayerId && x.EventType==MatchEventType.YellowCard && x.Minute<=e.Minute);
            }
            if(behavior==PlayerBehavior.FairPlay) { Assert.Equal(0,fouls); Assert.Equal(0,yellows+direct+second); }
            rows.Add(new { Behavior=behavior,Matches=400,Minutes=minutes,Appearances=appearances,Fouls=fouls,Yellows=yellows,DirectReds=direct,SecondYellowReds=second,SuspensionAwards=suspensions,RedsPer10000Minutes=(direct+second)*10000.0/minutes });
        }
        Write("behavior-controlled.json",rows);
    }

    [Fact]
    public void AiMarket_DoesNotBuyStrength11ForTwoMillion()
    {
        var w=WorldSeedFactory.CreateSeededWorld(); var buyer=w.Clubs[0]; buyer.CashBalance=20_000_000;
        foreach(var p in w.Players.Where(p=>p.ClubId==buyer.Id))p.Strength=15;
        w.Players.Last(p=>p.ClubId==buyer.Id && p.Position!=PlayerPosition.G).ClubId=null;
        foreach(var c in w.Clubs.Where(c=>c.Id!=buyer.Id)) c.HumanCoachId=Guid.NewGuid();
        var weak=w.Players.First(p=>p.ClubId==w.Clubs[1].Id && p.Position==PlayerPosition.A);
        weak.Strength=11;weak.IsListedForTransfer=true;weak.AskingPrice=2_000_000;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.Contains(w.AuditLogs,a=>a.ActionType=="AiPurchaseAttempt");
        Assert.NotEqual(buyer.Id,weak.ClubId);
        Assert.DoesNotContain(w.Transactions,t=>t.PlayerId==weak.Id && t.Category==FinancialTransactionCategory.TransferPurchase);
        Assert.True(weak.AskingPrice>MarketAndEconomyEngine.CalculateFairValue(weak)*1.25m);
    }

    [Fact]
    public void InjuryAndRedCardReactions_RespectQuotasAndNeverReplaceSentOffPlayer()
    {
        var (w,c,m,match)=ReactionFixture(0); var map=w.Players.ToDictionary(p=>p.Id);
        var injured=match.HomeActiveStarterIds.First(id=>map[id].Position==PlayerPosition.M);
        match.HomeInjuredInMatchIds.Add(injured); map[injured].InjuryRoundsRemaining=3;
        match.CurrentMinute=30;
        AiManagerBrain.EvaluateAiInMatchReaction(match,c,m,true,RoundState.RUNNING_FIRST_HALF,map);
        Assert.DoesNotContain(injured,match.HomeActiveStarterIds);Assert.Equal(1,match.HomeUsedSubstitutions);
        var sentOff=match.HomeActiveStarterIds.First(id=>map[id].Position==PlayerPosition.D);
        match.HomeActiveStarterIds.Remove(sentOff); match.HomeSentOffIds.Add(sentOff);
        match.HomeUsedPauseWindows=3;match.CurrentMinute=83;
        var count=match.Events.Count;
        AiManagerBrain.EvaluateAiInMatchReaction(match,c,m,true,RoundState.RUNNING_SECOND_HALF,map);
        Assert.Equal(count,match.Events.Count);Assert.DoesNotContain(sentOff,match.HomeActiveStarterIds);
    }
}
