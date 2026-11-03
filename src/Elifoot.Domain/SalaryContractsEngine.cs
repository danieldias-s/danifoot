using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    private static IEnumerable<SalaryRaiseRequest> PendingForPlayer(GameWorld w,Guid playerId)
    {
        if(w.Economy.PendingCacheCount!=w.Economy.SalaryRequests.Count)
        {
            w.Economy.PendingByPlayer=w.Economy.SalaryRequests.Where(r=>r.Status==SalaryRequestStatus.PENDING)
                .GroupBy(r=>r.PlayerId).ToDictionary(g=>g.Key,g=>g.ToList());
            w.Economy.PendingCacheCount=w.Economy.SalaryRequests.Count;
        }
        return w.Economy.PendingByPlayer.GetValueOrDefault(playerId)?.Where(r=>r.Status==SalaryRequestStatus.PENDING)??[];
    }
    public static void RecordContract(GameWorld w,Player p,decimal previousSalary,string reason,string key,decimal signingFee=0)
    {
        p.NegotiatedContractProtected=true;
        if(w.Economy.ContractKeys.Count==0)foreach(var h in w.Economy.ContractHistory)w.Economy.ContractKeys.Add(h.Key);
        if(!w.Economy.ContractKeys.Add(key))return;
        w.Economy.ContractHistory.Add(new(key,p.Id,p.ClubId,w.SeasonNumber,w.RoundNumber,previousSalary,p.SalaryPerRound,
            p.ContractRoundsRemaining,reason,signingFee,p.SalaryPerRound*Math.Max(0,p.ContractRoundsRemaining)));
        w.Economy.SalaryEligibility.Remove(p.Id);
        foreach(var pending in PendingForPlayer(w,p.Id))
        {
            pending.Status=pending.ClubId==p.ClubId?SalaryRequestStatus.ACCEPTED:SalaryRequestStatus.REFUSED;
            pending.Resolution=pending.ClubId==p.ClubId?"SUPERSEDED_BY_NEGOTIATED_CONTRACT":"PLAYER_TRANSFERRED";
        }
    }
    public static (bool Success,string? Error) RenewContract(GameWorld w,Club c,Player p,int additionalMatches,string key,decimal? salary=null)
    {
        EnsureMigrated(w);string payload=$"{p.Id}:{additionalMatches}:{salary?.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var receipt=Receipt(w,c,key,"RENEW_CONTRACT",payload);
        if(receipt.Found)return(receipt.Valid,receipt.Valid?null:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key)||!Safe(w)||p.ClubId!=c.Id)return(false,"Renovação inválida ou indisponível durante a partida.");
        if(AuctionReserved(w,p.Id))return(false,"Jogador está reservado para um leilão em andamento.");
        if(IsSeasonRenewed(w,p))return(false,"Contrato já renovado até o fim desta temporada.");
        additionalMatches=w.Economy.Rules.SeasonContractsEnabled?SeasonContractRounds(w):Math.Clamp(additionalMatches,5,30);
        decimal previous=p.SalaryPerRound,negotiated=salary??previous;
        if(negotiated<0)return(false,"Salário inválido.");
        decimal fee=Math.Round(negotiated*(additionalMatches/10m),2);
        if(!w.Economy.Rules.SeasonContractsEnabled&&AvailableCash(w,c)<fee)return(false,$"Saldo insuficiente para luvas de renovação (R$ {fee:N0}).");
        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.ContractRenewal,-fee,
            $"Renovação negociada de {p.DisplayName}",$"contract:{p.Id}:{key}:fee");
        p.SalaryPerRound=negotiated;
        if(w.Economy.Rules.SeasonContractsEnabled)ProtectSeasonContract(w,p);
        else p.ContractRoundsRemaining+=additionalMatches;
        RecordContract(w,p,previous,"PREVENTIVE_RENEWAL",$"{w.Id}:{c.Id}:{key}",fee);
        Remember(w,c,key,"RENEW_CONTRACT",payload,p.Id);return(true,null);
    }
    public static void RegisterPromotions(GameWorld w,IEnumerable<Guid> clubIds)
    {
        EnsureMigrated(w);
        var promoted=clubIds.ToHashSet();
        foreach(var p in w.Players.Where(p=>p.ClubId is Guid id&&promoted.Contains(id)))
        {
            if(p.NegotiatedContractProtected&&p.ContractRoundsRemaining>0)continue;
            string key=$"{w.Id}:promotion:S{w.SeasonNumber+1}:{p.ClubId}:{p.Id}";
            w.Economy.SalaryEligibility[p.Id]=new(key,"DIVISION_PROMOTION",p.Strength,p.Strength,p.ClubId!.Value,w.SeasonNumber+1);
        }
        foreach(var c in w.Clubs)AssessCredit(w,c);
    }
    public static void GenerateSalaryRequests(GameWorld w,IReadOnlyList<MatchFixture> matches)
    {
        EnsureMigrated(w);
        foreach(var e in w.PlayerStrengthHistory.Skip(w.Economy.StrengthHistoryCursor))
        {
            if(e.StrengthAfter<=e.StrengthBefore)continue;
            var p=w.Players.FirstOrDefault(p=>p.Id==e.PlayerId);
            if(p?.ClubId is not Guid clubId||p.ClubId!=e.ClubId||p.NegotiatedContractProtected&&p.ContractRoundsRemaining>0)continue;
            w.Economy.SalaryEligibility[p.Id]=new(e.IdempotencyKey,"FORCE_PROGRESS",e.StrengthBefore,e.StrengthAfter,clubId,e.Season);
        }
        w.Economy.StrengthHistoryCursor=w.PlayerStrengthHistory.Count;
        var clubsPlaying=matches.Where(Played).SelectMany(m=>new[]{m.HomeClubId,m.AwayClubId}).ToHashSet();
        var clubsWithPending=w.Economy.SalaryRequests.Where(r=>r.Status==SalaryRequestStatus.PENDING).Select(r=>r.ClubId).ToHashSet();
        foreach(var c in w.Clubs.Where(c=>clubsPlaying.Contains(c.Id)).OrderBy(c=>c.Id))
        {
            string roundKey=$"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}";
            if(w.Economy.SalaryRequestRounds.Contains(roundKey))continue;
            if(clubsWithPending.Contains(c.Id))continue;
            var candidates=w.Players.Where(p=>p.ClubId==c.Id&&(!p.NegotiatedContractProtected||p.ContractRoundsRemaining<=0)
                    &&!AuctionReserved(w,p.Id)&&w.Economy.SalaryEligibility.TryGetValue(p.Id,out var e)&&e.ClubId==c.Id&&e.FromSeason<=w.SeasonNumber)
                .Select(p=>new {Player=p,Requested=RequestedSalary(w,c,p),Trigger=w.Economy.SalaryEligibility[p.Id]})
                .Where(x=>x.Requested>x.Player.SalaryPerRound)
                .OrderByDescending(x=>(x.Requested-x.Player.SalaryPerRound)/Math.Max(1,x.Player.SalaryPerRound))
                .ThenByDescending(x=>x.Trigger.StrengthAfter-x.Trigger.StrengthBefore).ThenBy(x=>x.Player.Id).ToList();
            if(candidates.Count==0)continue;
            var chosen=candidates[0];var player=chosen.Player;
            var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
            w.Economy.SalaryRequests.Add(new(){Id=CoachCareerEngine.StableId($"salary:{w.Id}:{roundKey}"),WorldId=w.Id,ClubId=c.Id,PlayerId=player.Id,
                Season=w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=player.SalaryPerRound,RequestedSalary=chosen.Requested,
                ReferenceSalary=MarketAndEconomyEngine.CalculateFairSalaryPerRound(player),RenewalMatches=w.Economy.Rules.RenewalMatches,
                Reason=chosen.Trigger.Reason,TriggerKey=chosen.Trigger.Key,Mandatory=!SquadValidator.CanRemovePlayerFromSquad(squad,player.Id).CanRemove});
            w.Economy.SalaryRequestRounds.Add(roundKey);w.Economy.SalaryEligibility.Remove(player.Id);
        }
    }
    private static decimal RequestedSalary(GameWorld w,Club c,Player p)
    {
        decimal division=c.DivisionTier switch{DivisionTier.PrimeiraDivisao=>1.10m,DivisionTier.SegundaDivisao=>1.05m,DivisionTier.TerceiraDivisao=>1m,DivisionTier.QuartaDivisao=>.95m,_=>.9m};
        decimal reference=MarketAndEconomyEngine.CalculateFairSalaryPerRound(p)*division;
        decimal cap=w.Economy.Rules.MaximumSalaryRaiseFraction*(c.CashBalance<0?.5m:1m);
        return Math.Round(Math.Min(reference,Math.Max(p.SalaryPerRound+1,p.SalaryPerRound*(1+cap))),0);
    }
    public static (bool Success,string? Error) RespondSalary(GameWorld w,Club c,Guid requestId,bool accept,string key,string actor="HUMAN")
    {
        EnsureMigrated(w);string payload=$"{requestId}:{accept}";var receipt=Receipt(w,c,key,"SALARY_DECISION",payload);
        if(receipt.Found)return(receipt.Valid,receipt.Valid?receipt.Receipt?.Message:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key)||!Safe(w))return(false,"Resposta salarial disponível somente entre partidas.");
        var request=w.Economy.SalaryRequests.FirstOrDefault(r=>r.Id==requestId&&r.ClubId==c.Id);
        if(request==null)return(false,"Pedido não pertence ao clube.");
        if(request.Status!=SalaryRequestStatus.PENDING)
        {
            bool same=accept?request.Status==SalaryRequestStatus.ACCEPTED:request.Status==SalaryRequestStatus.REFUSED;
            if(same)Remember(w,c,key,"SALARY_DECISION",payload,request.Id);
            return(same,same?null:"Pedido já resolvido.");
        }
        var p=w.Players.FirstOrDefault(p=>p.Id==request.PlayerId&&p.ClubId==c.Id);
        if(p==null)return(false,"O jogador já não pertence a este clube.");
        if(AuctionReserved(w,p.Id))return(false,"Jogador já está em leilão.");
        var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
        bool mandatory=!SquadValidator.CanRemovePlayerFromSquad(squad,p.Id).CanRemove;
        request.Mandatory=mandatory;
        if(accept||mandatory)
        {
            decimal previous=p.SalaryPerRound;p.SalaryPerRound=request.RequestedSalary;
            p.ContractRoundsRemaining=Math.Max(p.ContractRoundsRemaining,request.RenewalMatches);
            if(w.Economy.Rules.SeasonContractsEnabled)ProtectSeasonContract(w,p);
            request.Status=SalaryRequestStatus.ACCEPTED;request.Resolution=mandatory&&!accept?"MANDATORY_MINIMUM_SQUAD":actor+"_ACCEPTED";
            RecordContract(w,p,previous,request.Resolution,$"salary:{request.Id}:accepted");
            string? message=mandatory&&!accept?"RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO.":null;
            Remember(w,c,key,"SALARY_DECISION",payload,request.Id,message);return(true,message);
        }
        request.Status=SalaryRequestStatus.REFUSED;request.Resolution=actor+"_REFUSED";
        c.PreferredStarterIds.Remove(p.Id);c.PreferredBenchIds.Remove(p.Id);
        p.ClubId=null;p.ContractRoundsRemaining=0;p.NegotiatedContractProtected=false;p.IsListedForTransfer=false;p.AskingPrice=null;
        var auction=NewSpecialAuction(w,p,c,AuctionReason.SalaryRenewalRefused,$"salary:{request.Id}:refused");
        request.AuctionId=auction.Id;Remember(w,c,key,"SALARY_DECISION",payload,request.Id);return(true,null);
    }
    public static void AfterSportingRound(GameWorld w,IReadOnlyList<MatchFixture> matches)
    {
        GenerateSalaryRequests(w,matches);ProcessAiFinances(w);SettleNegativeCashInterest(w);EvaluateInsolvency(w);
    }
    public static void ProcessAiFinances(GameWorld w)
    {
        EnsureMigrated(w);
        if(!Safe(w))return;
        var pendingByClub=w.Economy.SalaryRequests.Where(r=>r.Status==SalaryRequestStatus.PENDING).GroupBy(r=>r.ClubId).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var c in w.Clubs.Where(c=>c.HumanCoachId==null&&c.AiManagerId!=null).OrderBy(c=>c.Id))
        {
            string key=$"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}";
            if(!w.Economy.AiFinancialRounds.Add(key))continue;
            decimal payroll=w.Players.Where(p=>p.ClubId==c.Id).Sum(p=>p.SalaryPerRound);
            var credit=AssessCredit(w,c);decimal reserve=payroll*3+credit.InterestPerMatch*2+c.Stadium.BuiltCapacity*.7m;
            if(c.CashBalance<Math.Max(0,payroll)&&credit.AvailableLoans>0&&OperatingEconomyEngine.AiCanServiceNewLoan(w,c))TakeClassicLoan(w,c,"ai-loan:"+key);
            foreach(var r in pendingByClub.GetValueOrDefault(c.Id)??[])
            {
                decimal increase=r.RequestedSalary-r.CurrentSalary;
                bool afford=c.CashBalance>=reserve+increase*r.RenewalMatches;
                RespondSalary(w,c,r.Id,afford,"ai-salary:"+r.Id,"AI");
            }
            foreach(var loan in w.BankLoans.Where(l=>l.ClubId==c.Id&&!l.IsPaidOff).OrderBy(l=>l.Id).ToList())
                if(AvailableCash(w,c)>=loan.RemainingBalance+reserve)RepayLoan(w,c,loan.Id,"ai-repay:"+key+":"+loan.Id);
        }
    }
}
