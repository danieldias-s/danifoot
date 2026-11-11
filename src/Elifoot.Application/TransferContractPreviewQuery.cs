using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    public async Task<(RenewalContractPreviewDto? Preview,string? Error)> GetRenewalContractPreviewAsync(
        Guid worldId,Guid coachId,Guid playerId,CancellationToken ct=default)
    {
        var gate=MultiplayerRoundOrchestrator.GetLock(worldId);await gate.WaitAsync(ct);
        try
        {
            var w=await _repository.GetWorldAsync(worldId,ct);
            if(w==null)return(null,"Sala não encontrada.");
            var coach=w.HumanCoaches.FirstOrDefault(h=>h.Id==coachId);
            var c=w.Clubs.FirstOrDefault(c=>c.Id==coach?.CurrentClubId&&c.HumanCoachId==coachId);
            var p=w.Players.FirstOrDefault(p=>p.Id==playerId&&p.ClubId==c?.Id);
            if(c==null||p==null)return(null,"Jogador não pertence ao seu clube.");
            if(!ClassicEconomyEngine.Safe(w)||coach!.IsReadyForRound)return(null,"Renovação disponível entre partidas e sem pronto.");
            if(ClassicEconomyEngine.IsSeasonRenewed(w,p))return(null,"Jogador já renovado nesta temporada.");
            if(w.Auctions.Any(a=>!a.IsClosed&&a.PlayerId==p.Id))return(null,"Jogador reservado para leilão.");
            if(w.Economy.SalaryRequests.Any(r=>r.PlayerId==p.Id&&r.Status==SalaryRequestStatus.PENDING))
                return(null,"SALARY_PENDING_DECISION: Resolva primeiro o pedido espontâneo.");
            if(p.SalaryPerRound<0||p.SalaryPerRound!=Math.Round(p.SalaryPerRound,2))return(null,"CONTRACT_VALUE_INVALID: Salário vigente inválido.");
            try{return(ClassicEconomyEngine.PreviewRenewal(w,c,p),null);}
            catch(OverflowException){return(null,"CONTRACT_VALUE_INVALID: Salário excede o limite financeiro.");}
        }
        finally{gate.Release();}
    }

    public async Task<(TransferContractPreviewDto? Preview,string? Error)> GetTransferContractPreviewAsync(
        Guid worldId,Guid coachId,Guid playerId,Guid? offerId=null,Guid? auctionId=null,CancellationToken ct=default)
    {
        var gate=MultiplayerRoundOrchestrator.GetLock(worldId);await gate.WaitAsync(ct);
        try
        {
            var world=await _repository.GetWorldAsync(worldId,ct);
            if(world==null)return(null,"Sala não encontrada.");
            var coach=world.HumanCoaches.FirstOrDefault(h=>h.Id==coachId);
            var buyer=world.Clubs.FirstOrDefault(c=>c.Id==coach?.CurrentClubId&&c.HumanCoachId==coachId);
            if(buyer==null)return(null,"Você não controla este clube.");
            var player=world.Players.FirstOrDefault(p=>p.Id==playerId);
            if(player==null)return(null,"Jogador não encontrado.");
            Guid? sellerId=player.ClubId;
            decimal? price=player.IsListedForTransfer?player.AskingPrice:null;
            if(offerId is Guid oid)
            {
                var offer=world.TransferOffers.FirstOrDefault(o=>o.Id==oid&&o.PlayerId==playerId);
                if(offer==null||offer.BuyerClubId!=buyer.Id&&offer.SellerClubId!=buyer.Id)return(null,"Proposta não pertence ao clube.");
                buyer=world.Clubs.FirstOrDefault(c=>c.Id==offer.BuyerClubId);
                if(buyer==null)return(null,"Clube comprador não encontrado.");
                sellerId=offer.SellerClubId;price=offer.CounterAmount??offer.OfferedAmount;
            }
            if(auctionId is Guid aid)
            {
                var auction=world.Auctions.FirstOrDefault(a=>a.Id==aid&&a.PlayerId==playerId&&!a.IsClosed);
                if(auction==null)return(null,"Leilão encerrado ou inexistente.");
                sellerId=auction.FormerClubId;price=ClassicEconomyEngine.NextBid(auction);
            }
            var sellerClub=sellerId is Guid sid?world.Clubs.FirstOrDefault(c=>c.Id==sid):null;
            var breakdown=ClassicPlayerEconomyEngine.EvaluatePlayerEconomy(world,player,buyer,sellerClub,price);
            return(new(player.Id,player.DisplayName,buyer.Id,buyer.Name,sellerId,
                sellerClub?.Name,price,player.SalaryPerRound,
                MarketContractTermsEngine.SigningSalary(world,player,buyer),0,MarketContractTermsEngine.SalaryTermsToken(world,player),
                player.TransferListingVersion,auctionId.HasValue?20:Math.Max(16,player.ContractRoundsRemaining),
                auctionId.HasValue,world.RoundVersion)
            {
                CalculatedBaseSalary=breakdown.CalculatedBaseSalary,
                MarketValue=breakdown.IndividualMarketValue,
                CandidateSigningPrice=breakdown.CandidateSigningPrice,
                NationalityClass=breakdown.NationalityClass switch
                {
                    ClassicalNationalityClass.SameCountry=>"SAME_COUNTRY",
                    ClassicalNationalityClass.EquivalentCountry=>"EQUIVALENT_COUNTRY",
                    _=>"FOREIGNER"
                },
                AiHiringInterestFactor=breakdown.AiHiringInterestFactor,
                InflationIndex=breakdown.InflationIndex,
                InflationDisplay=breakdown.InflationDisplayIndex
            },null);
        }
        finally{gate.Release();}
    }
}
