using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    private static AuctionItem NewSpecialAuction(GameWorld w,Player p,Club former,AuctionReason reason,string key)
    {
        var id=CoachCareerEngine.StableId($"auction:{w.Id}:{key}");
        var existing=w.Auctions.FirstOrDefault(a=>a.Id==id);
        if(existing!=null)return existing;
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
            // Same permitted durations as voluntary auctions; independent of sporting RNG.
            var rng=DeterministicRng.FromContext(w.Seed,w.SeasonNumber,w.RoundNumber,
                BitConverter.ToInt32(id.ToByteArray(),0),subStep:0x53414C);
            int duration=rng.NextInt(1,4),total=Math.Max(1,w.TotalRoundsInSeason);
            int target=w.RoundNumber+duration-1+(w.RoundState==RoundState.RESULTS?1:0);
            auction.DurationRounds=duration;auction.ExpiresAtSeason=w.SeasonNumber+(target-1)/total;
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
            var p=squad.Where(p=>!IsSeasonRenewed(w,p)&&!AuctionReserved(w,p.Id)&&SquadValidator.CanRemovePlayerFromSquad(squad,p.Id).CanRemove)
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
        if(auction.IsClosed)return;
        if(auction.ExpiresAtSeason.HasValue&&!IsSpecialAuctionDue(w,auction))return;
        var p=w.Players.FirstOrDefault(p=>p.Id==auction.PlayerId);
        var buyer=w.Clubs.FirstOrDefault(c=>c.Id==auction.HighestBidderClubId);
        bool sold=false;
        if(p!=null&&buyer!=null&&auction.CurrentBid>=auction.MinimumBid&&auction.CurrentBid>=0
            &&AvailableCash(w,buyer,auction.Id)>=auction.CurrentBid&&w.Players.Count(p=>p.ClubId==buyer.Id)<SquadValidator.MaxSquadSize)
        {
            if(auction.Reason==AuctionReason.FinancialInsolvency&&p.ClubId==auction.FormerClubId&&p.ClubId is Guid sellerId)
                sold=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,sellerId,p.Id,auction.CurrentBid,"Leilão por insolvência",
                    $"auction:{auction.Id}","FinancialInsolvency",auction.Id).Success;
            else if(auction.Reason==AuctionReason.SalaryRenewalRefused&&p.ClubId==null)
            {
                sold=MarketAndEconomyEngine.TryRecordTransaction(w,buyer,FinancialTransactionCategory.AuctionPurchase,-auction.CurrentBid,
                    $"Leilão de atleta livre: {p.DisplayName}",$"auction:{auction.Id}:win");
                if(sold)
                {
                    var tx=w.Transactions[^1];tx.PlayerId=p.Id;tx.BuyerClubId=buyer.Id;tx.TransferTransactionId=tx.Id;
                    tx.TransactionType="SalaryRefusalFreeAgent";
                    decimal previous=p.SalaryPerRound;p.ClubId=buyer.Id;p.ContractRoundsRemaining=20;
                    p.SalaryPerRound=MarketAndEconomyEngine.CalculateFairSalaryPerRound(p);
                    RecordContract(w,p,previous,"FREE_AGENT_AUCTION",$"auction:{auction.Id}:contract");
                }
            }
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
        if(string.IsNullOrWhiteSpace(key)||!Safe(w))return(false,"Lances disponíveis somente entre partidas.");
        var a=w.Auctions.FirstOrDefault(a=>a.Id==auctionId&&!a.IsClosed);
        if(a==null)return(false,"Leilão encerrado ou inexistente.");
        if(w.Players.Any(p=>p.Id==a.PlayerId&&p.ClubId==c.Id))return(false,"O clube já possui este jogador.");
        if(w.Players.Count(p=>p.ClubId==c.Id)+w.Auctions.Count(a=>!a.IsClosed&&a.Id!=auctionId&&a.HighestBidderClubId==c.Id)>=SquadValidator.MaxSquadSize)
            return(false,"Limite de elenco reservado por contratações em andamento.");
        if(amount!=Math.Round(amount,a.Reason==AuctionReason.Voluntary?2:0)||amount<NextBid(a))return(false,"LANCE INSUFICIENTE. Respeite o mínimo, o incremento e a precisão do leilão.");
        if(AvailableCash(w,c,a.Id)<amount)return(false,"SALDO INSUFICIENTE.");
        a.CurrentBid=amount;a.HighestBidderClubId=c.Id;Remember(w,c,key,"AUCTION_BID",payload,a.Id);return(true,null);
    }
}
