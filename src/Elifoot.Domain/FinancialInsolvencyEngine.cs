using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    private static AuctionItem NewSpecialAuction(GameWorld w,Player p,Club former,AuctionReason reason,string key)
    {
        var id=CoachCareerEngine.StableId($"auction:{w.Id}:{key}");
        var existing=w.Auctions.FirstOrDefault(a=>a.Id==id);
        if(existing!=null)
        {
            if(existing.PlayerId!=p.Id||existing.FormerClubId!=former.Id||existing.Reason!=reason||existing.IsClosed)
                throw new InvalidOperationException("AUCTION_CONFLICT: A identidade do leilão já pertence a outra operação.");
            return existing;
        }
        var auction=new AuctionItem{Id=id,GameWorldId=w.Id,PlayerId=p.Id,FormerClubId=former.Id,Reason=reason,
            MinimumBid=0,CurrentBid=0,CreatedAtSeason=w.SeasonNumber,CreatedAtRound=w.RoundNumber,
            ExpiresAtRound=w.RoundNumber+w.Economy.Rules.AuctionDurationRounds};
        if(reason==AuctionReason.FinancialInsolvency&&w.Economy.Rules.SeasonContractsEnabled)
        {
            int total=Math.Max(1,w.TotalRoundsInSeason);
            int target=w.RoundNumber+(w.SettledFinancialRounds.Contains($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}")?1:0);
            auction.DurationRounds=1;auction.ExpiresAtSeason=w.SeasonNumber+(target-1)/total;
            auction.ExpiresAtRound=(target-1)%total+1;
        }
        if(reason==AuctionReason.SalaryRenewalRefused)
        {
            // Exactly one playable round, including a crossing into the next season.
            int total=Math.Max(1,w.TotalRoundsInSeason);
            int target=w.RoundNumber+(w.RoundState is RoundState.RESULTS or RoundState.FINISHED?1:0);
            auction.DurationRounds=1;auction.ExpiresAtSeason=w.SeasonNumber+(target-1)/total;
            auction.ExpiresAtRound=(target-1)%total+1;
        }
        w.Auctions.Add(auction);return auction;
    }
    public static void EvaluateInsolvency(GameWorld w)
    {
        EnsureMigrated(w);
        foreach(var c in w.Clubs.OrderBy(c=>c.Id))
        {
            var loans=w.BankLoans.Where(l=>l.ClubId==c.Id&&!l.IsPaidOff).ToList();
            decimal overdue=loans.Sum(l=>l.UnpaidInterest),debt=loans.Sum(l=>l.RemainingBalance);
            if(!w.Economy.Crises.TryGetValue(c.Id,out var crisis))w.Economy.Crises[c.Id]=crisis=new();
            bool insolvent=c.CashBalance<0||overdue>0;
            void Record(string kind,Guid? player=null,Guid? auction=null)
            {
                string key=$"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}:{kind}";
                if(!w.Economy.CrisisHistory.Any(h=>h.Key==key))w.Economy.CrisisHistory.Add(new(key,c.Id,w.SeasonNumber,w.RoundNumber,kind,c.CashBalance,overdue,debt,player,auction));
            }
            if(!insolvent){if(crisis.IsInsolvent)Record("RECOVERED");crisis.IsInsolvent=false;continue;}
            if(!crisis.IsInsolvent){Record("INSOLVENT");crisis.FirstCrisisSeason=w.SeasonNumber;}
            crisis.IsInsolvent=true;
            if(w.Auctions.Any(a=>!a.IsClosed&&a.FormerClubId==c.Id&&a.Reason==AuctionReason.FinancialInsolvency))continue;
            if(w.Auctions.Any(a=>a.FormerClubId==c.Id&&a.Reason==AuctionReason.FinancialInsolvency
                &&a.CreatedAtSeason==w.SeasonNumber&&a.CreatedAtRound==w.RoundNumber))continue;
            if(!w.Economy.Rules.SeasonContractsEnabled&&Ordinal(w)-crisis.LastAuctionOrdinal<w.Economy.Rules.InsolvencyAuctionCooldownRounds)continue;
            var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
            var p=squad.Where(p=>!IsSeasonRenewed(w,p)&&!AuctionReserved(w,p.Id)
                    &&!w.Economy.SalaryRequests.Any(r=>r.PlayerId==p.Id&&r.Status==SalaryRequestStatus.PENDING)
                    &&SquadValidator.CanRemovePlayerFromSquad(squad,p.Id).CanRemove)
                .OrderByDescending(p=>p.Strength).ThenByDescending(p=>p.SalaryPerRound).ThenBy(p=>p.Id).FirstOrDefault();
            if(p==null){Record("NO_ELIGIBLE_PLAYER");continue;}
            foreach(var r in w.Economy.SalaryRequests.Where(r=>r.PlayerId==p.Id&&r.Status==SalaryRequestStatus.PENDING))
            {r.Status=SalaryRequestStatus.REFUSED;r.Resolution="REPLACED_BY_FINANCIAL_AUCTION";}
            p.IsListedForTransfer=false;p.AskingPrice=null;
            var a=NewSpecialAuction(w,p,c,AuctionReason.FinancialInsolvency,$"debt:{c.Id}:{Ordinal(w)}");
            crisis.LastAuctionOrdinal=Ordinal(w);Record("FORCED_AUCTION",p.Id,a.Id);
        }
    }
    public static void SettleSpecialAuction(GameWorld w,AuctionItem auction,List<string>? logs=null)
    {
        if(auction.IsClosed||!MarketValidationEngine.IsDue(w,auction))return;
        var p=w.Players.FirstOrDefault(p=>p.Id==auction.PlayerId);
        var buyer=w.Clubs.FirstOrDefault(c=>c.Id==auction.HighestBidderClubId);
        bool sold=false;
        if(p!=null&&buyer!=null&&MarketValidationEngine.ValidateAuctionWinner(w,auction).Success)
        {
            (bool Success,string? Error) result=(false,null);
            if(auction.Reason==AuctionReason.FinancialInsolvency&&p.ClubId==auction.FormerClubId&&p.ClubId is Guid sellerId)
                result=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,sellerId,p.Id,auction.CurrentBid,"Leilão por insolvência",
                    $"auction:{auction.Id}","FinancialInsolvency",auction.Id);
            else if(auction.Reason==AuctionReason.SalaryRenewalRefused&&p.ClubId==null)
            {
                result=MarketAndEconomyEngine.SignAuctionFreeAgent(w,auction,buyer,p,20,"FREE_AGENT_AUCTION",
                    "SalaryRefusalFreeAgent",FinancialTransactionCategory.AuctionPurchase,auction.CurrentBid,
                    $"auction:{auction.Id}:win",$"Leilão de atleta livre: {p.DisplayName}",true);
            }
            if(result.Error?.StartsWith("MARKET_TRANSFER_FAILED",StringComparison.Ordinal)==true)
            {logs?.Add(result.Error);return;}
            sold=result.Success;
        }
        auction.IsClosed=true;auction.ClosureReason=sold?"SOLD":"AUCTION_UNSOLD";
        w.Economy.AuctionHistory.Add(new(auction.Id,auction.PlayerId,auction.Reason,auction.FormerClubId,
            sold?buyer!.Id:null,w.SeasonNumber,w.RoundNumber,sold?auction.CurrentBid:0,auction.ClosureReason));
        if(auction.Reason==AuctionReason.FinancialInsolvency&&auction.FormerClubId is Guid clubId&&w.Economy.Crises.TryGetValue(clubId,out var crisis))
            crisis.LastAuctionOrdinal=Ordinal(w);
        logs?.Add(sold?$"Leilão concluído: {p?.DisplayName}, R$ {auction.CurrentBid:N0}.":$"Leilão sem comprador: {p?.DisplayName}.");
    }
    public static (bool Success,string? Error) PlaceBid(GameWorld w,Club c,Guid auctionId,decimal amount,string key)
    {
        EnsureMigrated(w);string payload=$"{auctionId}:{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var receipt=Receipt(w,c,key,"AUCTION_BID",payload);
        if(receipt.Found)return(receipt.Valid,receipt.Valid?null:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key))return(false,"Identificador da operação é obrigatório.");
        var result=TryPlaceBid(w,c,auctionId,amount);
        if(result.Success)Remember(w,c,key,"AUCTION_BID",payload,auctionId);
        return result;
    }
    internal static (bool Success,string? Error) TryPlaceBid(GameWorld w,Club c,Guid auctionId,decimal amount)
    {
        if(!w.Clubs.Contains(c))return(false,"MARKET_AUTHORITY: Clube não pertence a esta sala.");
        var a=w.Auctions.FirstOrDefault(a=>a.Id==auctionId&&!a.IsClosed);
        if(a==null)return(false,"Leilão encerrado ou inexistente.");
        var validation=MarketValidationEngine.ValidateBid(w,c.Id,a,amount);
        if(!validation.Success)return validation;
        a.CurrentBid=amount;a.HighestBidderClubId=c.Id;return(true,null);
    }
}
