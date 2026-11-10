using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Voluntary sales retain ownership until actual settlement at an official global round end.</summary>
public static class VoluntaryAuctionEngine
{
    public static (bool Success,string? Error,Guid? AuctionId) Create(GameWorld world,Club seller,Guid playerId,string key,
        decimal minimumBid=VoluntaryAuctionDefaults.MinimumBid,int durationRounds=VoluntaryAuctionDefaults.DurationRounds)
    {
        if(string.IsNullOrWhiteSpace(key))return(false,"Identificador da operação é obrigatório.",null);
        if(!world.Clubs.Contains(seller))return(false,"MARKET_AUTHORITY: Clube não pertence a esta sala.",null);
        var id=CoachCareerEngine.StableId($"voluntary-auction:{world.Id}:{seller.Id}:{key}");
        var previous=world.Auctions.FirstOrDefault(a=>a.Id==id);
        if(previous!=null){bool same=previous.PlayerId==playerId&&previous.MinimumBid==minimumBid&&previous.DurationRounds==durationRounds;return(same,same?null:"Chave usada por outra operação.",same?id:null);}
        if(!ClassicEconomyEngine.Safe(world))return(false,"Leilões disponíveis somente entre partidas.",null);
        if(minimumBid<0||minimumBid!=Math.Round(minimumBid,2))return(false,"Lance mínimo deve ser um valor não negativo com até duas casas decimais.",null);
        if(durationRounds is <1 or >3)return(false,"Duração deve ser de 1, 2 ou 3 rodadas globais concluídas.",null);
        var player=world.Players.FirstOrDefault(p=>p.Id==playerId&&p.ClubId==seller.Id);
        if(player==null)return(false,"Jogador não pertence ao seu clube.",null);
        var sale=MarketValidationEngine.ValidateSale(world,seller.Id,player.Id);
        if(!sale.Success)return(false,sale.Error,null);
        // Preserve the existing, stricter outgoing reservation budget specifically at voluntary creation.
        var available=world.Players.Where(p=>p.ClubId==seller.Id&&!ClassicEconomyEngine.AuctionReserved(world,p.Id)).ToList();
        var removal=SquadValidator.CanRemovePlayerFromSquad(available,player.Id);
        if(!removal.CanRemove)return(false,removal.ErrorMessage,null);
        int total=world.TotalRoundsInSeason;
        if(total<=0||world.RoundNumber<1||world.RoundNumber>total)return(false,"Calendário da sala inválido.",null);
        int target=world.RoundNumber+durationRounds-1+(world.RoundState==RoundState.RESULTS?1:0);
        var auction=new AuctionItem{Id=id,GameWorldId=world.Id,PlayerId=player.Id,FormerClubId=seller.Id,Reason=AuctionReason.Voluntary,
            MinimumBid=minimumBid,DurationRounds=durationRounds,CreatedAtSeason=world.SeasonNumber,CreatedAtRound=world.RoundNumber,
            ExpiresAtSeason=world.SeasonNumber+(target-1)/total,ExpiresAtRound=(target-1)%total+1};
        player.IsListedForTransfer=false;player.AskingPrice=null;
        world.Auctions.Add(auction);
        return(true,null,id);
    }
    public static bool IsDue(GameWorld world,AuctionItem auction)
        =>auction.Reason==AuctionReason.Voluntary&&!auction.IsClosed&&auction.ExpiresAtSeason is int season
        &&world.RoundState==RoundState.RESULTS
        &&world.SettledFinancialRounds.Contains($"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}")
        &&(world.SeasonNumber>season||world.SeasonNumber==season&&world.RoundNumber>=auction.ExpiresAtRound);

    public static void Settle(GameWorld world,AuctionItem auction,List<string>? logs=null)
    {
        if(!IsDue(world,auction))return;
        var player=world.Players.FirstOrDefault(p=>p.Id==auction.PlayerId);
        var buyer=world.Clubs.FirstOrDefault(c=>c.Id==auction.HighestBidderClubId);
        bool sold=false;
        if(player?.ClubId==auction.FormerClubId&&player?.ClubId is Guid seller&&buyer!=null&&buyer.Id!=seller
            &&MarketValidationEngine.ValidateAuctionWinner(world,auction).Success)
        {
            var result=MarketAndEconomyEngine.ExecuteTransfer(world,buyer.Id,seller,player.Id,auction.CurrentBid,
                "Leilão voluntário",$"auction:{auction.Id}","VoluntaryAuction",auction.Id);
            if(result.ErrorMessage?.StartsWith("MARKET_TRANSFER_FAILED",StringComparison.Ordinal)==true)
            {logs?.Add(result.ErrorMessage);return;}
            sold=result.Success;
        }
        auction.IsClosed=true;auction.ClosureReason=sold?"SOLD":"AUCTION_UNSOLD";
        world.Economy.AuctionHistory.Add(new(auction.Id,auction.PlayerId,auction.Reason,auction.FormerClubId,
            sold?buyer!.Id:null,world.SeasonNumber,world.RoundNumber,sold?auction.CurrentBid:0,auction.ClosureReason));
        logs?.Add(sold?$"Leilão voluntário vendido: {player?.DisplayName}, R$ {auction.CurrentBid:N2}.":$"Leilão voluntário sem comprador: {player?.DisplayName}.");
    }
}
