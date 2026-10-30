using System.Text.Json;
using System.Runtime.CompilerServices;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    private static readonly JsonSerializerOptions CommandJson=new(){IncludeFields=true};
    private async Task<T> Finance<T>(Guid worldId,Guid coachId,string operation,string payload,string? key,long? version,
        Func<string,T> fail,Func<string,Task<T>> action,CancellationToken ct,Guid? requiredClub=null)
    {
        var gate=MultiplayerRoundOrchestrator.GetLock(worldId);await gate.WaitAsync(ct);
        try
        {
            var w=await _repository.GetWorldAsync(worldId,ct);
            if(w==null)return fail("Sala não encontrada.");
            ClassicEconomyEngine.EnsureMigrated(w);
            var coach=w.HumanCoaches.FirstOrDefault(h=>h.Id==coachId);
            var c=w.Clubs.FirstOrDefault(c=>c.Id==coach?.CurrentClubId&&c.HumanCoachId==coachId);
            if(c==null||requiredClub!=null&&requiredClub!=c.Id)return fail("Você não controla este clube.");
            key=string.IsNullOrWhiteSpace(key)?Guid.NewGuid().ToString("N"):key;
            string receiptKey=$"C{c.Id}:{key}";
            if(w.Economy.Commands.TryGetValue(receiptKey,out var receipt)&&receipt.ResultJson!=null)
            {
                if(receipt.Operation!="APP:"+operation||receipt.Payload!=payload)return fail("Chave usada por outra operação.");
                // Also retry durability if the original save failed after the in-memory mutation.
                await _repository.SaveWorldAsync(w,CancellationToken.None);
                return JsonSerializer.Deserialize<T>(receipt.ResultJson,CommandJson)!;
            }
            if(!ClassicEconomyEngine.Safe(w))return fail("Operações financeiras disponíveis somente entre partidas.");
            if(coach!.IsReadyForRound&&w.RoundState==RoundState.PREPARING)return fail("Retire o pronto antes de alterar as finanças.");
            if(version.HasValue&&version!=w.RoundVersion)return fail("A sala mudou. Atualize antes de confirmar a operação.");
            ct.ThrowIfCancellationRequested();
            var result=await action(key);
            if(result is ITuple tuple&&tuple[0] is true)
            {
                w.Economy.Commands[receiptKey]=new("APP:"+operation,payload,null,null,JsonSerializer.Serialize(result,CommandJson));
                w.RoundVersion++;
                // Once accepted under the room lock, a disconnected client cannot cancel durability.
                await _repository.SaveWorldAsync(w,CancellationToken.None);
                await _publisher.PublishMarketUpdatedAsync(w.Id,"Finanças atualizadas.",ct);
            }
            return result;
        }
        finally{gate.Release();}
    }
    public Task<(bool Success,string? Error)> ListPlayerForTransferAsync(Guid worldId,Guid coachId,Guid playerId,decimal askingPrice,CancellationToken ct=default,string? idempotencyKey=null,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"LIST",$"{playerId}:{askingPrice}",idempotencyKey,expectedRoundVersion,e=>(false,e),k=>ListPlayerForTransferCoreAsync(worldId,coachId,playerId,askingPrice,ct),ct);
    public Task<(bool Success,string? Error)> BuyListedPlayerAsync(Guid worldId,Guid coachId,Guid playerId,string idempotencyKey,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"BUY",playerId.ToString(),idempotencyKey,expectedRoundVersion,e=>(false,e),k=>BuyListedPlayerCoreAsync(worldId,coachId,playerId,k,ct),ct);
    public Task<(bool Success,string? Error,TransferOfferDto? Offer)> CreateTransferOfferAsync(Guid worldId,Guid coachId,Guid playerId,decimal offeredAmount,string idempotencyKey,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance<(bool,string?,TransferOfferDto?)>(worldId,coachId,"OFFER",$"{playerId}:{offeredAmount}",idempotencyKey,expectedRoundVersion,e=>(false,e,null),k=>CreateTransferOfferCoreAsync(worldId,coachId,playerId,offeredAmount,k,ct),ct);
    public Task<(bool Success,string? Error)> RespondToTransferOfferAsync(Guid worldId,Guid coachId,Guid offerId,bool accept,string idempotencyKey,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"OFFER_DECISION",$"{offerId}:{accept}",idempotencyKey,expectedRoundVersion,e=>(false,e),k=>RespondToTransferOfferCoreAsync(worldId,coachId,offerId,accept,k,ct),ct);
    public Task<(bool Success,string? Error)> PlaceAuctionBidAsync(Guid worldId,Guid coachId,Guid auctionId,decimal bidAmount,CancellationToken ct=default,string? idempotencyKey=null,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"BID",$"{auctionId}:{bidAmount}",idempotencyKey,expectedRoundVersion,e=>(false,e),k=>PlaceAuctionBidCoreAsync(worldId,coachId,auctionId,bidAmount,k,ct),ct);
    public Task<(bool Success,string? Error)> RenewPlayerContractAsync(Guid worldId,Guid coachId,Guid playerId,int additionalRounds,CancellationToken ct=default,string? idempotencyKey=null,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"RENEW",$"{playerId}:{additionalRounds}",idempotencyKey,expectedRoundVersion,e=>(false,e),k=>RenewPlayerContractCoreAsync(worldId,coachId,playerId,additionalRounds,k,ct),ct);
    public Task<(bool Success,string? Error)> TakeBankLoanAsync(Guid worldId,Guid coachId,decimal amount,int rounds,CancellationToken ct=default,string? idempotencyKey=null,long? expectedRoundVersion=null,Guid? requiredClub=null)
        =>Finance(worldId,coachId,"LOAN",$"{amount}:{rounds}",idempotencyKey,expectedRoundVersion,e=>(false,e),k=>TakeBankLoanCoreAsync(worldId,coachId,amount,rounds,k,ct),ct,requiredClub);
    public Task<(bool Success,string? Error)> RepayBankLoanAsync(Guid worldId,Guid coachId,Guid clubId,Guid loanId,string key,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"REPAY",loanId.ToString(),key,expectedRoundVersion,e=>(false,e),async k=>{
            var w=(await _repository.GetWorldAsync(worldId,ct))!;return ClassicEconomyEngine.RepayLoan(w,w.Clubs.First(c=>c.Id==clubId),loanId,k);},ct,clubId);
    public Task<(bool Success,string? Error)> RespondSalaryAsync(Guid worldId,Guid coachId,Guid clubId,Guid requestId,bool accept,string key,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"SALARY",$"{requestId}:{accept}",key,expectedRoundVersion,e=>(false,e),async k=>{
            var w=(await _repository.GetWorldAsync(worldId,ct))!;return ClassicEconomyEngine.RespondSalary(w,w.Clubs.First(c=>c.Id==clubId),requestId,accept,k);},ct,clubId);
    public Task<(bool Success,string? Error)> StadiumCommandAsync(Guid worldId,Guid coachId,Guid clubId,bool expand,int seats,string key,CancellationToken ct=default,long? expectedRoundVersion=null)
        =>Finance(worldId,coachId,"STADIUM",$"{expand}:{seats}",key,expectedRoundVersion,e=>(false,e),async k=>{
            var w=(await _repository.GetWorldAsync(worldId,ct))!;var c=w.Clubs.First(c=>c.Id==clubId);
            decimal cost=expand?MarketAndEconomyEngine.CalculateExpansionCost(c.Stadium,seats):MarketAndEconomyEngine.CalculateRenovationCost(c.Stadium);
            if(ClassicEconomyEngine.AvailableCash(w,c)<cost)return(false,"Saldo disponível insuficiente.");
            return expand?MarketAndEconomyEngine.StartStadiumExpansion(w,c,seats):MarketAndEconomyEngine.StartStadiumRenovation(w,c);},ct,clubId);
}

