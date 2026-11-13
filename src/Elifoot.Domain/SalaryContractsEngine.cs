using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    public static bool IsAcceptedSalaryRaise(PlayerContractRecord record, int season) =>
        record.Season == season && record.Reason is "SPONTANEOUS_HUMAN_ACCEPTED" or "SPONTANEOUS_AI_ACCEPTED";

    private static bool HasAcceptedSalaryRaise(GameWorld world, Player player) =>
        player.ClubId.HasValue && world.Economy.ContractHistory.Any(record =>
            record.PlayerId == player.Id && record.ClubId == player.ClubId &&
            IsAcceptedSalaryRaise(record, world.SeasonNumber));

    public static string? RenewalSeasonRestriction(GameWorld world, Player player)
    {
        if (IsSeasonRenewed(world, player)) return "Contrato já renovado até o fim desta temporada.";
        return HasAcceptedSalaryRaise(world, player)
            ? "Jogador já teve um aumento salarial aceito nesta temporada."
            : null;
    }

    private static IEnumerable<SalaryRaiseRequest> PendingForPlayer(GameWorld w,Guid playerId)
    {
        if(w.Economy.PendingCacheCount!=w.Economy.SalaryRequests.Count)
        {
            w.Economy.PendingByPlayer=w.Economy.SalaryRequests.Where(r=>r.IsUnresolved)
                .GroupBy(r=>r.PlayerId).ToDictionary(g=>g.Key,g=>g.ToList());
            w.Economy.PendingCacheCount=w.Economy.SalaryRequests.Count;
        }
        return w.Economy.PendingByPlayer.GetValueOrDefault(playerId)?.Where(r=>r.IsUnresolved)??[];
    }
    public static void RecordContract(GameWorld w,Player p,decimal previousSalary,string reason,string key,decimal signingFee=0)
    {
        p.NegotiatedContractProtected=true;
        if(w.Economy.ContractKeys.Count==0)foreach(var h in w.Economy.ContractHistory)w.Economy.ContractKeys.Add(h.Key);
        if(!w.Economy.ContractKeys.Add(key))return;
        w.Economy.ContractHistory.Add(new(key,p.Id,p.ClubId,w.SeasonNumber,w.RoundNumber,previousSalary,p.SalaryPerRound,
            p.ContractRoundsRemaining,reason,signingFee,p.SalaryPerRound*Math.Max(0,p.ContractRoundsRemaining)));
        w.Economy.SalaryEligibility.Remove(p.Id);
        foreach(var pending in PendingForPlayer(w,p.Id).Where(r=>r.Status==SalaryRequestStatus.PENDING))
        {
            pending.Status=pending.ClubId==p.ClubId?SalaryRequestStatus.ACCEPTED:SalaryRequestStatus.REFUSED;
            pending.Resolution=pending.ClubId==p.ClubId?"SUPERSEDED_BY_NEGOTIATED_CONTRACT":"PLAYER_TRANSFERRED";
            pending.ResolvedAtUtc=DateTime.UtcNow;pending.ResolvedBy="CONTRACT";pending.DecisionVersion++;
        }
    }
    public static (bool Success,string? Error) RenewContract(GameWorld w,Club c,Player p,int additionalMatches,string key,decimal? salary=null,string? termsToken=null)
    {
        if(!w.Clubs.Contains(c)||!w.Players.Contains(p))return(false,"CONTRACT_AUTHORITY: Clube ou jogador não pertence a este mundo.");
        EnsureMigrated(w);string payload=$"{p.Id}:{additionalMatches}:{salary?.ToString(System.Globalization.CultureInfo.InvariantCulture)}"+(termsToken==null?"":$":terms:{termsToken}");
        var receipt=Receipt(w,c,key,"RENEW_CONTRACT",payload);
        if(receipt.Found)return(receipt.Valid,receipt.Valid?null:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key)||!Safe(w)||!w.Clubs.Contains(c)||!w.Players.Contains(p)||p.ClubId!=c.Id)return(false,"Renovação inválida ou indisponível durante a partida.");
        if(AuctionReserved(w,p.Id))return(false,"Jogador está reservado para um leilão em andamento.");
        if(RenewalSeasonRestriction(w,p) is string restriction)return(false,restriction);
        if(PendingForPlayer(w,p.Id).Any())return(false,"SALARY_PENDING_DECISION: Resolva o pedido espontâneo antes de tentar renovar este jogador.");
        if(p.SalaryPerRound<0||p.SalaryPerRound!=Math.Round(p.SalaryPerRound,2))return(false,"CONTRACT_VALUE_INVALID: Salário vigente inválido.");
        if(w.Economy.Rules.SeasonContractsEnabled&&salary==null)
        {
            RenewalContractPreviewDto preview;
            try{preview=PreviewRenewal(w,c,p);}
            catch(OverflowException){return(false,"CONTRACT_VALUE_INVALID: Salário excede o limite financeiro.");}
            if(termsToken!=null&&termsToken!=preview.TermsToken)return(false,"RENEWAL_TERMS_CHANGED: A exigência mudou. Consulte e confirme os valores atuais.");
            if(preview.RequiresRaise&&termsToken==null)return(false,"RENEWAL_CONFIRMATION_REQUIRED: O jogador exige salário maior. Confirme a prévia ou desista sem consequências.");
            salary=preview.RequiredSalary;
        }
        additionalMatches=w.Economy.Rules.SeasonContractsEnabled?SeasonContractRounds(w):Math.Clamp(additionalMatches,5,30);
        decimal previous=p.SalaryPerRound,negotiated=salary??previous;
        if(negotiated<0||negotiated!=Math.Round(negotiated,2))return(false,"CONTRACT_VALUE_INVALID: Salário inválido.");
        decimal fee;int contractRounds;
        try { contractRounds=w.Economy.Rules.SeasonContractsEnabled?additionalMatches:checked(p.ContractRoundsRemaining+additionalMatches);
            fee=Math.Round(negotiated*(additionalMatches/10m),2);_ = negotiated*Math.Max(0,contractRounds); }
        catch(OverflowException){return(false,"CONTRACT_VALUE_INVALID: Salário excede o limite financeiro.");}
        if(!w.Economy.Rules.SeasonContractsEnabled&&AvailableCash(w,c)<fee)return(false,$"Saldo insuficiente para luvas de renovação (R$ {fee:N0}).");
        string contractKey=$"{w.Id}:{c.Id}:{key}",feeKey=$"contract:{p.Id}:{key}:fee";
        using var mutation=new MarketTransferMutation(w,p,c,c,contractKey,$"{c.Id}:{feeKey}");
        mutation.TrackCommand(c,key);
        try
        {
        if(!MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.ContractRenewal,-fee,
            $"Renovação negociada de {p.DisplayName}",feeKey))
            return(false,"CONTRACT_OPERATION_FAILED: Luvas não foram concluídas.");
        p.SalaryPerRound=negotiated;
        if(w.Economy.Rules.SeasonContractsEnabled)ProtectSeasonContract(w,p);
        else p.ContractRoundsRemaining=contractRounds;
        RecordContract(w,p,previous,"PREVENTIVE_RENEWAL",contractKey,fee);
        Remember(w,c,key,"RENEW_CONTRACT",payload,p.Id);mutation.Commit();return(true,null);
        }
        catch(Exception error) when(error is ArithmeticException or InvalidOperationException or ArgumentException or NullReferenceException)
        {return(false,$"CONTRACT_OPERATION_FAILED: Renovação não concluída ({error.GetType().Name}).");}
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
        if(matches.Count==0||matches.Any(m=>!m.IsFinished)||!matches.Any(Played))return;
        foreach(var e in w.PlayerStrengthHistory.Skip(w.Economy.StrengthHistoryCursor))
        {
            if(e.StrengthAfter<=e.StrengthBefore)continue;
            var p=w.Players.FirstOrDefault(p=>p.Id==e.PlayerId);
            if(p?.ClubId is not Guid clubId||p.ClubId!=e.ClubId||p.NegotiatedContractProtected&&p.ContractRoundsRemaining>0)continue;
            w.Economy.SalaryEligibility[p.Id]=new(e.IdempotencyKey,"FORCE_PROGRESS",e.StrengthBefore,e.StrengthAfter,clubId,e.Season);
        }
        w.Economy.StrengthHistoryCursor=w.PlayerStrengthHistory.Count;
        var clubsPlaying=matches.Where(Played).SelectMany(m=>new[]{m.HomeClubId,m.AwayClubId}).ToHashSet();
        var clubsWithPending=w.Economy.SalaryRequests.Where(r=>r.IsUnresolved).Select(r=>r.ClubId).ToHashSet();
        var clubsWithRequestThisRound=w.Economy.SalaryRequests
            .Where(r=>r.Season==w.SeasonNumber&&r.Round==w.RoundNumber).Select(r=>r.ClubId).ToHashSet();
        var adjustedPlayers = w.Economy.ContractHistory.Where(r => IsAcceptedSalaryRaise(r, w.SeasonNumber))
            .Select(r => (r.PlayerId, r.ClubId)).ToHashSet();
        foreach(var c in w.Clubs.Where(c=>clubsPlaying.Contains(c.Id)).OrderBy(c=>c.Id))
        {
            string roundKey=$"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}";
            if(!w.Economy.SalaryRequestCheckedRounds.Add(roundKey))continue;
            if(w.Economy.SalaryRequestRounds.Contains(roundKey)||clubsWithRequestThisRound.Contains(c.Id))continue;
            if(clubsWithPending.Contains(c.Id))continue;
            var candidates=w.Players.Where(p=>p.ClubId==c.Id&&p.ContractRoundsRemaining<=0&&!IsSeasonRenewed(w,p)
                    &&!adjustedPlayers.Contains((p.Id,p.ClubId))
                    &&!p.IsListedForTransfer&&!AuctionReserved(w,p.Id)&&w.Economy.SalaryEligibility.TryGetValue(p.Id,out var e)&&e.ClubId==c.Id&&e.FromSeason<=w.SeasonNumber)
                .Select(p=>new {Player=p,Requested=RequestedSalary(w,c,p),Trigger=w.Economy.SalaryEligibility[p.Id]})
                .Where(x=>x.Requested>x.Player.SalaryPerRound)
                .OrderByDescending(x=>(x.Requested-x.Player.SalaryPerRound)/Math.Max(1,x.Player.SalaryPerRound))
                .ThenByDescending(x=>x.Trigger.StrengthAfter-x.Trigger.StrengthBefore).ThenBy(x=>x.Player.Id).ToList();
            if(candidates.Count==0)continue;
            var chosen=candidates[0];var player=chosen.Player;
            var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
            w.Economy.SalaryRequests.Add(new(){Id=CoachCareerEngine.StableId($"salary:{w.Id}:{roundKey}"),WorldId=w.Id,ClubId=c.Id,PlayerId=player.Id,
                Season=w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=player.SalaryPerRound,RequestedSalary=chosen.Requested,
                ReferenceSalary=MarketAndEconomyEngine.CalculateFairSalaryPerRound(w,player,c),RenewalMatches=w.Economy.Rules.RenewalMatches,
                CreatedAtUtc=DateTime.UtcNow,Reason=chosen.Trigger.Reason,TriggerKey=chosen.Trigger.Key,Mandatory=!SquadValidator.CanRemovePlayerFromSquad(squad,player.Id).CanRemove});
            w.Economy.SalaryRequestRounds.Add(roundKey);w.Economy.SalaryEligibility.Remove(player.Id);
        }
    }
    private static decimal RequestedSalary(GameWorld w,Club c,Player p)
    {
        decimal division=c.DivisionTier switch{DivisionTier.PrimeiraDivisao=>1.10m,DivisionTier.SegundaDivisao=>1.05m,DivisionTier.TerceiraDivisao=>1m,DivisionTier.QuartaDivisao=>.95m,_=>.9m};
        decimal reference=MarketAndEconomyEngine.CalculateFairSalaryPerRound(w,p,c)*division;
        decimal cap=w.Economy.Rules.MaximumSalaryRaiseFraction*(c.CashBalance<0?.5m:1m);
        return Math.Round(Math.Min(reference,Math.Max(p.SalaryPerRound+1,p.SalaryPerRound*(1+cap))),0);
    }
    public static bool CanRespondSalary(GameWorld world) => Safe(world) || world.RoundState == RoundState.FINISHED;

    private static (string Code, string Message)? ValidateSalaryRequest(GameWorld w, SalaryRaiseRequest request, Player? player)
    {
        if (player == null || player.ClubId != request.ClubId)
            return ("PLAYER_LEFT_CLUB", "O jogador já não pertence a este clube.");
        if (request.WorldId != Guid.Empty && request.WorldId != w.Id)
            return ("SALARY_AUTHORITY", "SALARY_AUTHORITY: Pedido pertence a outro mundo.");
        if (request.Season > w.SeasonNumber)
            return ("SALARY_SEASON_INVALID", "SALARY_SEASON_INVALID: Pedido pertence a uma temporada futura.");
        if ((!w.Economy.Rules.SeasonContractsEnabled && player.ContractRoundsRemaining > 0) ||
            RenewalSeasonRestriction(w, player) != null)
            return ("CONTRACT_IN_FORCE", "Jogador já possui contrato vigente.");
        if (AuctionReserved(w, player.Id))
            return ("PLAYER_IN_AUCTION", "Jogador já está em leilão.");
        if (request.CurrentSalary < 0 || request.RequestedSalary <= request.CurrentSalary
            || request.RequestedSalary != Math.Round(request.RequestedSalary, 2))
            return ("SALARY_VALUE_INVALID", "SALARY_VALUE_INVALID: Salário solicitado inválido.");
        if (player.SalaryPerRound != request.CurrentSalary)
            return ("SALARY_TERMS_CHANGED", "SALARY_TERMS_CHANGED: O salário vigente mudou; o pedido foi preservado para revisão.");
        try { _ = request.RequestedSalary * SeasonContractRounds(w); }
        catch (OverflowException) { return ("SALARY_VALUE_INVALID", "SALARY_VALUE_INVALID: Salário excede o limite financeiro."); }
        return null;
    }

    // Older saves can contain overlapping requests. Keep valid requests from distinct rounds in order.
    public static bool ReconcileSalaryRequests(GameWorld world, Guid? clubId = null)
    {
        if (!CanRespondSalary(world)) return false;
        var requests = world.Economy.SalaryRequests;
        var unresolved = requests.Where(r => r.IsUnresolved && (clubId == null || r.ClubId == clubId))
            .OrderBy(r => r.Season).ThenBy(r => r.Round).ThenBy(r => r.CreatedAtUtc).ThenBy(r => r.Id).ToList();
        if (unresolved.Count == 0) return false;

        var decided = requests.Where(r => r.Status is SalaryRequestStatus.ACCEPTED or SalaryRequestStatus.REFUSED).ToList();
        var usedRounds = decided.Select(r => (r.ClubId, r.Season, r.Round)).ToHashSet();
        var usedIds = decided.Select(r => (r.ClubId, r.Id)).ToHashSet();
        var clubs = world.Clubs.Select(c => c.Id).ToHashSet();
        var activeClubs = new HashSet<Guid>();
        bool changed = false;
        foreach (var request in unresolved)
        {
            var round = (request.ClubId, request.Season, request.Round);
            var id = (request.ClubId, request.Id);
            bool knownRound = request.Season > 0 && request.Round > 0;
            string? cancellation;
            if (!clubs.Contains(request.ClubId))
                cancellation = "CLUB_NOT_FOUND";
            else if ((knownRound && usedRounds.Contains(round)) || usedIds.Contains(id))
                cancellation = "DUPLICATE_SALARY_REQUEST";
            else
                cancellation = ValidateSalaryRequest(world, request, world.Players.FirstOrDefault(p => p.Id == request.PlayerId))?.Code;
            if (cancellation != null)
            {
                request.Status = SalaryRequestStatus.CANCELLED;
                request.Resolution = cancellation;
                request.ResolvedBy = "SYSTEM";
                request.ResolvedAtUtc = DateTime.UtcNow;
                changed = true;
                continue;
            }

            if (knownRound) usedRounds.Add(round);
            usedIds.Add(id);
            var status = activeClubs.Add(request.ClubId) ? SalaryRequestStatus.PENDING : SalaryRequestStatus.QUEUED;
            if (request.Status == status) continue;
            request.Status = status;
            changed = true;
        }
        return changed;
    }

    public static (bool Success,string? Error) RespondSalary(GameWorld w,Club c,Guid requestId,bool accept,string key,string actor="HUMAN")
    {
        if(!w.Clubs.Contains(c)||actor=="AI"&&!MarketValidationEngine.CanActAsAi(w,c))return(false,"SALARY_AUTHORITY: Responsável não controla este clube.");
        EnsureMigrated(w);string payload=$"{requestId}:{accept}";var receipt=Receipt(w,c,key,"SALARY_DECISION",payload);
        if(receipt.Found)return(receipt.Valid,receipt.Valid?receipt.Receipt?.Message:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key)||!CanRespondSalary(w))return(false,"Resposta salarial disponível somente entre partidas.");
        var request=w.Economy.SalaryRequests.FirstOrDefault(r=>r.Id==requestId&&r.ClubId==c.Id&&r.Status!=SalaryRequestStatus.CANCELLED);
        if(request==null)return(false,"Pedido não pertence ao clube.");
        if(request.Status==SalaryRequestStatus.QUEUED)return(false,"Resolva primeiro o pedido salarial pendente do seu clube.");
        if(request.Status!=SalaryRequestStatus.PENDING)
        {
            bool same=accept?request.Status==SalaryRequestStatus.ACCEPTED:request.Status==SalaryRequestStatus.REFUSED;
            if(same)Remember(w,c,key,"SALARY_DECISION",payload,request.Id);
            return(same,same?null:"Pedido já resolvido.");
        }
        var player=w.Players.FirstOrDefault(p=>p.Id==request.PlayerId&&p.ClubId==c.Id);
        if (ValidateSalaryRequest(w, request, player) is { } validation) return (false, validation.Message);
        ReconcileSalaryRequests(w, c.Id);
        if (request.Status != SalaryRequestStatus.PENDING)
            return (false, "Resolva primeiro o pedido salarial pendente do seu clube.");
        var p=player!;
        var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
        bool mandatory=!SquadValidator.CanRemovePlayerFromSquad(squad,p.Id).CanRemove;
        string contractKey=$"salary:{request.Id}:accepted";
        using var mutation=new MarketTransferMutation(w,p,c,c,contractKey);
        mutation.TrackCommand(c,key);
        try
        {
            request.Mandatory = mandatory;
            if (accept)
            {
                decimal previous = p.SalaryPerRound;
                p.SalaryPerRound = request.RequestedSalary;
                request.Status = SalaryRequestStatus.ACCEPTED;
                request.Resolution = "SPONTANEOUS_" + actor + "_ACCEPTED";
                RecordContract(w, p, previous, request.Resolution, contractKey);
            }
            else
            {
                if (mandatory) return (false, "Elenco mínimo: recomponha o elenco antes de recusar este pedido.");
                var auction = NewSpecialAuction(w, p, c, AuctionReason.SalaryRenewalRefused, $"salary:{request.Id}:refused");
                request.Status = SalaryRequestStatus.REFUSED;
                request.Resolution = actor + "_REFUSED";
                c.PreferredStarterIds.Remove(p.Id);
                c.PreferredBenchIds.Remove(p.Id);
                p.ClubId = null;
                p.ContractRoundsRemaining = 0;
                p.NegotiatedContractProtected = false;
                p.IsListedForTransfer = false;
                p.AskingPrice = null;
                request.AuctionId = auction.Id;
            }
            request.ResolvedAtUtc = DateTime.UtcNow;
            request.ResolvedBy = actor;
            request.DecisionVersion++;
            Remember(w, c, key, "SALARY_DECISION", payload, request.Id);
            mutation.Commit();
        }
        catch(Exception error) when(error is ArithmeticException or InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
        {return(false,$"SALARY_OPERATION_FAILED: Decisão não concluída ({error.GetType().Name}).");}
        ReconcileSalaryRequests(w, c.Id);
        return (true, null);
    }
    public static void AfterSportingRound(GameWorld w,IReadOnlyList<MatchFixture> matches)
    {
        GenerateSalaryRequests(w,matches);ProcessAiFinances(w);SettleNegativeCashInterest(w);EvaluateInsolvency(w);
    }
    public static int ResolvePendingAiSalaryRequests(GameWorld w)
    {
        if(!CanRespondSalary(w))return 0;
        int resolved=0;
        var pendingClubs = w.Economy.SalaryRequests.Where(r => r.IsUnresolved).Select(r => r.ClubId).ToHashSet();
        foreach(var c in w.Clubs.Where(c=>pendingClubs.Contains(c.Id)&&MarketValidationEngine.CanActAsAi(w,c)).OrderBy(c=>c.Id))
        {
            ReconcileSalaryRequests(w, c.Id);
            decimal payroll=w.Players.Where(p=>p.ClubId==c.Id).Sum(p=>p.SalaryPerRound);
            var credit=AssessCredit(w,c);decimal reserve=payroll*3+credit.InterestPerMatch*2+c.Stadium.BuiltCapacity*.7m;
            while (w.Economy.SalaryRequests.FirstOrDefault(r => r.ClubId == c.Id && r.Status == SalaryRequestStatus.PENDING) is { } r)
            {
                bool afford=c.CashBalance>=reserve+(r.RequestedSalary-r.CurrentSalary)*r.RenewalMatches;
                bool canRemove=SquadValidator.CanRemovePlayerFromSquad(w.Players.Where(p=>p.ClubId==c.Id).ToList(),r.PlayerId).CanRemove;
                if(!RespondSalary(w,c,r.Id,afford||!canRemove,"ai-salary:"+r.Id,"AI").Success || r.IsUnresolved)break;
                resolved++;
            }
        }
        return resolved;
    }
    public static void ProcessAiFinances(GameWorld w)
    {
        EnsureMigrated(w);
        if(!Safe(w))return;
        var pendingByClub=w.Economy.SalaryRequests.Where(r=>r.Status==SalaryRequestStatus.PENDING).GroupBy(r=>r.ClubId).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var c in w.Clubs.Where(c=>MarketValidationEngine.CanActAsAi(w,c)).OrderBy(c=>c.Id))
        {
            string key=$"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}";
            bool firstFinancialPass=w.Economy.AiFinancialRounds.Add(key);
            decimal payroll=w.Players.Where(p=>p.ClubId==c.Id).Sum(p=>p.SalaryPerRound);
            var credit=AssessCredit(w,c);decimal reserve=payroll*3+credit.InterestPerMatch*2+c.Stadium.BuiltCapacity*.7m;
            if(firstFinancialPass&&c.CashBalance<Math.Max(0,payroll)&&credit.AvailableLoans>0&&OperatingEconomyEngine.AiCanServiceNewLoan(w,c))TakeClassicLoan(w,c,"ai-loan:"+key);
            foreach(var r in pendingByClub.GetValueOrDefault(c.Id)??[])
            {
                decimal increase=r.RequestedSalary-r.CurrentSalary;
                bool afford=c.CashBalance>=reserve+increase*r.RenewalMatches;
                bool canRemove=SquadValidator.CanRemovePlayerFromSquad(w.Players.Where(p=>p.ClubId==c.Id).ToList(),r.PlayerId).CanRemove;
                RespondSalary(w,c,r.Id,afford||!canRemove,"ai-salary:"+r.Id,"AI");
            }
            if(firstFinancialPass)
                foreach(var loan in w.BankLoans.Where(l=>l.ClubId==c.Id&&!l.IsPaidOff).OrderBy(l=>l.Id).ToList())
                    if(AvailableCash(w,c)>=loan.RemainingBalance+reserve)RepayLoan(w,c,loan.Id,"ai-repay:"+key+":"+loan.Id);
        }
    }
}
