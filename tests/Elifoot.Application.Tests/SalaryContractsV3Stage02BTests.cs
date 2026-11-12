using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class SalaryContractsV3Stage02BTests
{
    private static (GameWorld W,HumanCoach Buyer,HumanCoach Seller,Player P,SalaryRaiseRequest R) Setup()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:32);w.Auctions.Clear();
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid();
        var coaches=w.Clubs.Take(2).Select(c=>new HumanCoach{GameWorldId=w.Id,CurrentClubId=c.Id}).ToArray();
        foreach(var h in coaches){w.HumanCoaches.Add(h);w.Clubs.Single(c=>c.Id==h.CurrentClubId).HumanCoachId=h.Id;}
        var p=w.Players.Last(p=>p.ClubId==coaches[1].CurrentClubId&&p.Position==PlayerPosition.A);
        p.ContractRoundsRemaining=0;p.IsListedForTransfer=false;p.SalaryPerRound=100;p.Strength=30;
        var r=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=p.ClubId!.Value,PlayerId=p.Id,
            Season=w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=100,RequestedSalary=110,RenewalMatches=20};
        w.Economy.SalaryRequests.Add(r);return(w,coaches[0],coaches[1],p,r);
    }
    private static ClubAndMarketApplicationService Service(IWorldRepository repo)=>new(repo,NullMatchRoomNotificationPublisher.Instance);

    [Fact] public async Task ConcurrentAcceptAndRefuseHaveOneWinningDecision()
    {
        var(w,b,s,p,r)=Setup();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var service=Service(repo);
        long version=w.RoundVersion;decimal cash=w.Clubs[1].CashBalance;
        var decisions=await Task.WhenAll(service.RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,true,"accept",expectedRoundVersion:version),
            service.RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,false,"refuse",expectedRoundVersion:version));
        Assert.Single(decisions,d=>d.Success);Assert.Equal(1,r.DecisionVersion);
        Assert.Equal(r.Status==SalaryRequestStatus.ACCEPTED?0:1,w.Auctions.Count);
        Assert.Equal(cash,w.Clubs[1].CashBalance);
        Assert.False((await service.RespondSalaryAsync(w.Id,b.Id,r.ClubId,r.Id,true,"wrong-owner")).Success);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task FailedSalarySaveRetriesSameCompleteDecisionAndSurvivesRestart(bool accept)
    {
        var(w,b,s,p,r)=Setup();var repo=new FailsOnce(w);var service=Service(repo);long version=w.RoundVersion;
        await Assert.ThrowsAsync<IOException>(()=>service.RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,accept,"same",expectedRoundVersion:version));
        var before=JsonSerializer.Deserialize<GameWorld>(repo.Durable)!;Assert.Equal(SalaryRequestStatus.PENDING,before.Economy.SalaryRequests.Single().Status);
        Assert.True((await service.RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,accept,"same",expectedRoundVersion:version)).Success);
        var restored=JsonSerializer.Deserialize<GameWorld>(repo.Durable)!;var restart=new InMemoryWorldRepository();await restart.SaveWorldAsync(restored);
        Assert.True((await Service(restart).RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,accept,"same",expectedRoundVersion:version)).Success);
        Assert.Equal(1,restored.Economy.SalaryRequests.Single().DecisionVersion);
        Assert.Equal(accept?0:1,restored.Auctions.Count);Assert.Equal(accept?1:0,restored.Economy.ContractHistory.Count);
        Assert.Equal(accept?r.ClubId:(Guid?)null,restored.Players.Single(x=>x.Id==p.Id).ClubId);
    }

    [Fact] public async Task RenewalFailureRetriesOneFeeAndBackendBlocksSaleForProtectedSeason()
    {
        var(w,b,s,p,r)=Setup();w.Economy.SalaryRequests.Clear();var repo=new FailsOnce(w);var service=Service(repo);decimal cash=w.Clubs[1].CashBalance;
        var quote=ClassicEconomyEngine.PreviewRenewal(w,w.Clubs[1],p);
        await Assert.ThrowsAsync<IOException>(()=>service.RenewPlayerContractAsync(w.Id,s.Id,p.Id,200,idempotencyKey:"renew",renewalTermsToken:quote.TermsToken));
        Assert.True((await service.RenewPlayerContractAsync(w.Id,s.Id,p.Id,200,idempotencyKey:"renew",renewalTermsToken:quote.TermsToken)).Success);
        Assert.Equal(cash-ClassicEconomyEngine.SeasonRenewalFee(w,p),w.Clubs[1].CashBalance);
        Assert.Single(w.Transactions,t=>t.Category==FinancialTransactionCategory.ContractRenewal);
        Assert.False((await service.ListPlayerForTransferAsync(w.Id,s.Id,p.Id,100,idempotencyKey:"bypass-list")).Success);
        Assert.False((await service.CreateVoluntaryAuctionAsync(w.Id,s.Id,p.Id,idempotencyKey:"bypass-auction")).Success);
        Assert.False((await service.CreateTransferOfferAsync(w.Id,b.Id,p.Id,100,"bypass-offer",salaryTermsToken:MarketContractTermsEngine.SalaryTermsToken(w,p))).Success);
    }

    [Fact] public async Task PreviewIsReadOnlyAndStaleOrMissingConsentCannotBuy()
    {
        var(w,b,s,p,r)=Setup();w.Economy.SalaryRequests.Clear();p.IsListedForTransfer=true;p.AskingPrice=100_000;p.SalaryPerRound=5000;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var service=Service(repo);string before=JsonSerializer.Serialize(w);
        var preview=await service.GetTransferContractPreviewAsync(w.Id,b.Id,p.Id);
        Assert.NotNull(preview.Preview);Assert.Equal(before,JsonSerializer.Serialize(w));
        Assert.Equal(5000,preview.Preview.CurrentSalaryPerRound);Assert.Equal(MarketContractTermsEngine.SigningSalary(p),preview.Preview.SigningSalaryPerRound);
        Assert.Equal(0,preview.Preview.AdditionalCosts);Assert.Equal(b.CurrentClubId,preview.Preview.BuyerClubId);
        Assert.False((await service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"missing")).Success);
        p.Strength=50;var failed=await service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"stale",salaryTermsToken:preview.Preview.SalaryTermsToken);
        Assert.False(failed.Success);Assert.Contains("TERMS_CHANGED",failed.Error);Assert.Equal(s.CurrentClubId,p.ClubId);
        var current=(await service.GetTransferContractPreviewAsync(w.Id,b.Id,p.Id)).Preview!;
        var purchase=await service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"fresh",salaryTermsToken:current.SalaryTermsToken);
        Assert.True(purchase.Success,purchase.Error);Assert.Equal(current.SigningSalaryPerRound,p.SalaryPerRound);
    }

    [Fact] public async Task SellerCannotSilentlyAcceptChangedBuyerSalaryAndCounterRequiresFreshBuyerConsent()
    {
        var(w,b,s,p,r)=Setup();w.Economy.SalaryRequests.Clear();p.ContractRoundsRemaining=20;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var service=Service(repo);
        var token=MarketContractTermsEngine.SalaryTermsToken(w,p);
        var offered=await service.CreateTransferOfferAsync(w.Id,b.Id,p.Id,100_000,"offer",salaryTermsToken:token);Assert.True(offered.Success,offered.Error);
        p.Strength=50;var current=MarketContractTermsEngine.SalaryTermsToken(w,p);
        Assert.False((await service.RespondToTransferOfferAsync(w.Id,s.Id,offered.Offer!.OfferId,true,"seller",salaryTermsToken:current)).Success);
        Assert.Equal(s.CurrentClubId,p.ClubId);
        Assert.True((await service.CounterTransferOfferAsync(w.Id,s.Id,offered.Offer!.OfferId,120_000,"counter")).Success);
        Assert.False((await service.RespondToTransferOfferAsync(w.Id,b.Id,offered.Offer.OfferId,true,"wrong-price",salaryTermsToken:current,expectedPrice:100_000)).Success);
        Assert.True((await service.RespondToTransferOfferAsync(w.Id,b.Id,offered.Offer.OfferId,true,"buyer",salaryTermsToken:current,expectedPrice:120_000)).Success);
        Assert.Equal(b.CurrentClubId,p.ClubId);Assert.Equal(MarketContractTermsEngine.SigningSalary(p),p.SalaryPerRound);
    }

    [Theory][InlineData(RoundState.RESULTS)][InlineData(RoundState.FINISHED)]
    public async Task PendingSeasonCannotResetReadyAndResolutionUnlocksTransition(RoundState phase)
    {
        var(w,b,s,p,r)=Setup();w.RoundState=phase;w.RoundNumber=w.TotalRoundsInSeason;s.IsReadyForRound=true;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        await Assert.ThrowsAsync<SalarySeasonTransitionException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id,expectedSeasonNumber:1,expectedRoundNumber:w.RoundNumber));
        Assert.True(s.IsReadyForRound);Assert.Equal(1,w.SeasonNumber);Assert.Empty(w.SeasonHistories);
        Assert.True((await Service(repo).RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,true,"resolve")).Success);
        var oldRound=w.RoundNumber;
        await Task.WhenAll(Enumerable.Range(0,4).Select(_=>rounds.AdvanceToNextRoundPreparationAsync(w.Id,expectedSeasonNumber:1,expectedRoundNumber:oldRound)));
        Assert.Equal(2,w.SeasonNumber);Assert.Single(w.SeasonHistories);Assert.False(s.IsReadyForRound);
    }

    [Fact] public async Task FailedSeasonSaveThenOriginalRequestPersistsTransitionWithoutRepeatingIt()
    {
        var(w,b,s,p,r)=Setup();w.Economy.SalaryRequests.Clear();w.RoundState=RoundState.RESULTS;w.RoundNumber=w.TotalRoundsInSeason;
        int oldRound=w.RoundNumber;var repo=new FailsOnce(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        await Assert.ThrowsAsync<IOException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id,expectedSeasonNumber:1,expectedRoundNumber:oldRound));
        Assert.Equal(2,w.SeasonNumber);Assert.Equal(1,JsonSerializer.Deserialize<GameWorld>(repo.Durable)!.SeasonNumber);
        await rounds.AdvanceToNextRoundPreparationAsync(w.Id,expectedSeasonNumber:1,expectedRoundNumber:oldRound);
        var durable=JsonSerializer.Deserialize<GameWorld>(repo.Durable)!;
        Assert.Equal(2,durable.SeasonNumber);Assert.Single(durable.SeasonHistories);
        Assert.Equal(w.Transactions.Count,durable.Transactions.Count);
    }

    [Fact] public async Task VoluntaryQuoteIsReadOnlyAndConcurrentConfirmationRenewsOnce()
    {
        var(w,b,s,p,r)=Setup();w.Economy.SalaryRequests.Clear();
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var service=Service(repo);
        string before=JsonSerializer.Serialize(w);var quote=await service.GetRenewalContractPreviewAsync(w.Id,s.Id,p.Id);
        Assert.NotNull(quote.Preview);Assert.True(quote.Preview.RequiresRaise);
        Assert.Equal(before,JsonSerializer.Serialize(w));Assert.Empty(w.Auctions);
        Assert.Null((await service.GetRenewalContractPreviewAsync(w.Id,b.Id,p.Id)).Preview);
        Assert.False((await service.RenewPlayerContractAsync(w.Id,s.Id,p.Id,0,idempotencyKey:"no-consent")).Success);
        long version=w.RoundVersion;
        var results=await Task.WhenAll(
            service.RenewPlayerContractAsync(w.Id,s.Id,p.Id,0,idempotencyKey:"one",expectedRoundVersion:version,renewalTermsToken:quote.Preview.TermsToken),
            service.RenewPlayerContractAsync(w.Id,s.Id,p.Id,0,idempotencyKey:"two",expectedRoundVersion:version,renewalTermsToken:quote.Preview.TermsToken));
        Assert.Single(results,x=>x.Success);Assert.Single(w.Transactions,t=>t.Category==FinancialTransactionCategory.ContractRenewal);
        Assert.Single(w.Economy.ContractHistory);Assert.Empty(w.Economy.SalaryRequests);Assert.Empty(w.Auctions);
        Assert.Equal(quote.Preview.RequiredSalary,p.SalaryPerRound);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task EveryRoundWaitsForCurrentOrLegacyHumanRequestWithoutResettingReady(bool legacy)
    {
        var(w,b,s,p,r)=Setup();w.RoundState=RoundState.RESULTS;w.RoundNumber=2;s.IsReadyForRound=true;
        if(legacy){w.SeasonNumber=2;r.Season=1;}
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        string before=JsonSerializer.Serialize(w);
        await Assert.ThrowsAsync<SalarySeasonTransitionException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id));
        Assert.Equal(before,JsonSerializer.Serialize(w));Assert.True(s.IsReadyForRound);Assert.Equal(2,w.RoundNumber);
        Assert.True((await Service(repo).RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,true,"resolve")).Success);
        await rounds.AdvanceToNextRoundPreparationAsync(w.Id);
        Assert.Equal(3,w.RoundNumber);Assert.Equal(RoundState.PREPARING,w.RoundState);
        Assert.DoesNotContain(w.Economy.SalaryRequests,x=>x.Status==SalaryRequestStatus.PENDING);
    }

    [Fact] public async Task LegacyRequestBlocksReadyButAnAlreadyReadyHumanCanStillDecide()
    {
        var(w,b,s,p,r)=Setup();w.SeasonNumber=2;r.Season=1;s.IsReadyForRound=true;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var ready=await rounds.SubmitTacticsAsync(new(w.Id,s.Id,"4-4-2",[],[],"ready",w.RoundVersion));
        Assert.False(ready.Success);Assert.Contains("SALARY_PENDING_TRANSITION",ready.Error);
        Assert.Equal(RoundState.PREPARING,w.RoundState);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        Assert.True((await Service(repo).RespondSalaryAsync(w.Id,s.Id,r.ClubId,r.Id,true,"human")).Success);
        Assert.Equal(110,p.SalaryPerRound);Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,p));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherClubsRequestDoesNotPreventReadyButStillPreventsKickoff(bool withoutTactics)
    {
        var (world, otherCoach, owner, player, request) = Setup();
        var ownerClub = world.Clubs.Single(c => c.Id == owner.CurrentClubId);
        var otherClub = world.Clubs.Single(c => c.Id == otherCoach.CurrentClubId);
        var repo = new InMemoryWorldRepository();
        await repo.SaveWorldAsync(world);
        var rounds = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

        SubmitTacticsRequest Tactics(HumanCoach coach, Club club)
        {
            var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
            var formation = TacticalFormations.Resolve("4-4-2", world.AllowClassic505);
            var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, formation, world, club.LineupSelectionMode);
            return new(world.Id, coach.Id, formation.Code, starters, bench, $"ready:{club.Id}", world.RoundVersion);
        }

        var blocked = await rounds.SubmitTacticsAsync(Tactics(owner, ownerClub));
        Assert.False(blocked.Success);
        Assert.Contains("SALARY_PENDING_TRANSITION", blocked.Error);
        var ready = withoutTactics
            ? await rounds.SubmitUnemployedReadyAsync(new(world.Id, otherCoach.Id, "ready", world.RoundVersion))
            : await rounds.SubmitTacticsAsync(Tactics(otherCoach, otherClub));
        Assert.True(ready.Success, ready.Error);
        Assert.False(ready.RoundStarted);
        Assert.True(otherCoach.IsReadyForRound);
        Assert.Equal(RoundState.PREPARING, world.RoundState);

        // A legacy save can contain submitted tactics even though the owner's request is still pending.
        var ownerTactics = Tactics(owner, ownerClub);
        owner.IsReadyForRound = true;
        owner.SubmittedFormation = ownerTactics.Formation;
        owner.SubmittedStarterIds = ownerTactics.StarterPlayerIds;
        owner.SubmittedBenchIds = ownerTactics.BenchPlayerIds;
        Assert.True((await Service(repo).RespondSalaryAsync(world.Id, owner.Id, ownerClub.Id, request.Id, true, "decision")).Success);
        Assert.Equal(110, player.SalaryPerRound);
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, world.RoundState);
    }

    [Fact] public async Task AdvanceRecoversOnlyAiLegacyRequestsAndNeverChoosesForAHuman()
    {
        var(w,b,s,p,r)=Setup();w.SeasonNumber=2;r.Season=1;w.RoundState=RoundState.RESULTS;w.RoundNumber=2;
        var aiClub=w.Clubs[0];aiClub.HumanCoachId=null;w.HumanCoaches.Remove(b);
        var aiPlayer=w.Players.Last(x=>x.ClubId==aiClub.Id&&x.Position==PlayerPosition.A);
        aiPlayer.ContractRoundsRemaining=0;aiPlayer.SalaryPerRound=100;
        var aiRequest=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=aiClub.Id,PlayerId=aiPlayer.Id,
            Season=1,Round=1,CurrentSalary=100,RequestedSalary=110,RenewalMatches=20};w.Economy.SalaryRequests.Add(aiRequest);
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        await Assert.ThrowsAsync<SalarySeasonTransitionException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id));
        Assert.NotEqual(SalaryRequestStatus.PENDING,aiRequest.Status);Assert.Equal(1,aiRequest.DecisionVersion);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Equal(100,p.SalaryPerRound);Assert.Equal(2,w.RoundNumber);
        await Assert.ThrowsAsync<SalarySeasonTransitionException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id));
        Assert.Equal(1,aiRequest.DecisionVersion);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        Assert.Empty(w.BankLoans);
    }

    [Fact] public async Task AiRecoverySaveFailureRetriesTheSameDecisionBeforeBlockingOnHuman()
    {
        var(w,b,s,p,r)=Setup();w.SeasonNumber=2;r.Season=1;w.RoundState=RoundState.RESULTS;w.RoundNumber=2;
        var c=w.Clubs[0];c.HumanCoachId=null;w.HumanCoaches.Remove(b);
        var player=w.Players.Last(x=>x.ClubId==c.Id&&x.Position==PlayerPosition.A);
        player.SalaryPerRound=100;player.ContractRoundsRemaining=0;
        var ai=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=c.Id,PlayerId=player.Id,
            Season=1,Round=1,CurrentSalary=100,RequestedSalary=110,RenewalMatches=20};w.Economy.SalaryRequests.Add(ai);
        var repo=new FailsOnce(w);var rounds=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        await Assert.ThrowsAsync<IOException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id));
        Assert.Equal(1,ai.DecisionVersion);Assert.True(w.Economy.SalaryRecoveryNeedsSave);
        Assert.All(JsonSerializer.Deserialize<GameWorld>(repo.Durable)!.Economy.SalaryRequests,x=>Assert.Equal(SalaryRequestStatus.PENDING,x.Status));
        await Assert.ThrowsAsync<SalarySeasonTransitionException>(()=>rounds.AdvanceToNextRoundPreparationAsync(w.Id));
        var durable=JsonSerializer.Deserialize<GameWorld>(repo.Durable)!;
        Assert.Equal(ai.Status,durable.Economy.SalaryRequests.Single(x=>x.Id==ai.Id).Status);
        Assert.Equal(1,ai.DecisionVersion);Assert.False(w.Economy.SalaryRecoveryNeedsSave);
        Assert.Equal(SalaryRequestStatus.PENDING,durable.Economy.SalaryRequests.Single(x=>x.Id==r.Id).Status);
        Assert.Equal(2,w.RoundNumber);
    }

    private sealed class FailsOnce(GameWorld world):IWorldRepository
    {
        public string Durable {get;private set;}=JsonSerializer.Serialize(world);private int calls;
        public Task<GameWorld?> GetWorldAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(world);
        public Task<GameWorld?> GetWorldByInviteCodeAsync(string code,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(world);
        public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct=default)=>Task.FromResult(new List<GameWorld>{world});
        public Task SaveWorldAsync(GameWorld value,CancellationToken ct=default)
        {if(++calls==1)throw new IOException("02B controlled save failure");Durable=JsonSerializer.Serialize(value);return Task.CompletedTask;}
    }
}
