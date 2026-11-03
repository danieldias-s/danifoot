using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Owner-authorized classic economy; reuses the world's loans, cash ledger and auctions.</summary>
public static partial class ClassicEconomyEngine
{
    public const int Version=1;
    public static int Ordinal(GameWorld w)=>w.SeasonNumber*1000+w.RoundNumber;
    public static int Ordinal(MatchFixture m)=>m.SeasonNumber*1000+m.RoundNumber;
    public static bool Played(MatchFixture m)=>m.IsFinished&&m.CurrentMinute>0&&!m.IsCancelled&&!m.IsPostponed;
    public static bool Safe(GameWorld w)=>w.RoundState is RoundState.PREPARING or RoundState.RESULTS;
    public static void EnsureMigrated(GameWorld w,bool isNewWorld=false)
    {
        if(!w.Economy.SeasonalDebtRulesInitialized)
        {
            w.Economy.OverdraftInterestRounds.UnionWith(w.SettledFinancialRounds);
            if (w.Economy.Rules.SeasonContractsEnabled)
            {
                var latestAgreements = w.Economy.ContractHistory
                    .Where(entry => entry.Season == w.SeasonNumber)
                    .GroupBy(entry => entry.PlayerId)
                    .ToDictionary(group => group.Key, group => group.Last());
                foreach (var player in w.Players.Where(player => player.ClubId.HasValue))
                {
                    if (latestAgreements.TryGetValue(player.Id, out var entry)
                        && entry.ClubId == player.ClubId && player.ContractRoundsRemaining > 0
                        && (entry.Reason == "PREVENTIVE_RENEWAL" || entry.Reason.EndsWith("_ACCEPTED", StringComparison.Ordinal)
                            || entry.Reason == "MANDATORY_MINIMUM_SQUAD"))
                    {
                        ProtectSeasonContract(w, player);
                    }
                }
            }
            w.Economy.SeasonalDebtRulesInitialized=true;
        }
        if(w.Economy.Version==Version)return;
        if(w.Economy.Version!=0)throw new InvalidOperationException("Unsupported economy version.");
        foreach(var p in w.Players)
        {
            if(!isNewWorld&&p.ContractRoundsRemaining>0)p.NegotiatedContractProtected=true;
        }
        foreach(var loan in w.BankLoans.Where(l=>l.Mode==BankLoanMode.LegacyInstallment))
        {
            loan.LegacyOpeningBalance??=loan.RemainingBalance;
            foreach(var m in w.Fixtures.Where(m=>m.FinancialsSettled&&(m.HomeClubId==loan.ClubId||m.AwayClubId==loan.ClubId)))loan.ChargedMatches.Add(m.Id);
        }
        if(!isNewWorld&&w.BankLoans.Count>0)w.Economy.CoverageWarnings.Add("Legacy loans retain their original rate, installment and opening debt; past interest/principal splits are unknown.");
        w.Economy.StrengthHistoryCursor=w.PlayerStrengthHistory.Count;
        w.Economy.Version=Version;
        foreach(var c in w.Clubs)AssessCredit(w,c);
    }
    public static CreditAssessmentDto AssessCredit(GameWorld w,Club c)
    {
        int baseLimit=c.DivisionTier is DivisionTier.PrimeiraDivisao or DivisionTier.SegundaDivisao?2:1;
        decimal indicator=Math.Max(0,c.Stadium.UsableCapacity)*w.Economy.Rules.StadiumAssetPerUsableSeat;
        int limit=baseLimit+(w.Economy.Rules.ExtraCreditThresholds.GetValueOrDefault(c.DivisionTier)??[]).Count(t=>indicator>=t);
        var loans=w.BankLoans.Where(l=>l.ClubId==c.Id&&!l.IsPaidOff).ToList();
        var result=new CreditAssessmentDto(c.Id,c.DivisionTier,indicator,limit,loans.Count,Math.Max(0,limit-loans.Count),
            loans.Sum(l=>l.RemainingBalance),loans.Where(l=>l.Mode==BankLoanMode.ClassicFixed).Sum(l=>l.InterestPerMatch),"CLASSIC_ADAPTED_ASSET_CREDIT_V1");
        w.Economy.CreditAssessments[c.Id]=result;
        return result;
    }
    private static string CommandKey(Club c,string key)=>$"C{c.Id}:{key}";
    private static (bool Found,bool Valid,FinancialCommandReceipt? Receipt) Receipt(GameWorld w,Club c,string key,string operation,string payload)
    {
        if(w.Economy.Commands.TryGetValue(CommandKey(c,key),out var r))return(true,r.Operation==operation&&r.Payload==payload,r);
        return(false,true,null);
    }
    private static void Remember(GameWorld w,Club c,string key,string operation,string payload,Guid? id,string? message=null)
        =>w.Economy.Commands[CommandKey(c,key)]=new(operation,payload,id,message);

    public static (bool Success,string? Error,Guid? LoanId) TakeClassicLoan(GameWorld w,Club c,string key)
    {
        EnsureMigrated(w);
        if(string.IsNullOrWhiteSpace(key))return(false,"Identificador da operação é obrigatório.",null);
        var receipt=Receipt(w,c,key,"TAKE_LOAN","500000");
        if(receipt.Found)return(receipt.Valid,receipt.Valid?null:"Chave usada por outra operação.",receipt.Receipt?.EntityId);
        if(!Safe(w))return(false,"Banco disponível somente entre partidas.",null);
        var credit=AssessCredit(w,c);
        if(credit.AvailableLoans==0)return(false,$"Limite individual de {credit.LoanLimit} empréstimo(s) atingido.",null);
        bool afterGame=w.RoundState==RoundState.RESULTS||w.Fixtures.Any(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber&&Played(m)&&(m.HomeClubId==c.Id||m.AwayClubId==c.Id));
        var loan=new BankLoan {Id=CoachCareerEngine.StableId($"loan:{w.Id}:{c.Id}:{key}"),GameWorldId=w.Id,ClubId=c.Id,
            Mode=BankLoanMode.ClassicFixed,PrincipalAmount=500_000m,RemainingBalance=500_000m,InterestPerMatch=25_000m,
            InterestRatePerRound=0,InstallmentPerRound=0,RemainingRounds=0,CreatedSeason=w.SeasonNumber,CreatedRound=w.RoundNumber,
            CreatedVirtualDate=ClassicForceEvolutionEngine.IsActive(w)?w.ClassicForce.GameDate:null,
            FirstEligibleOrdinal=Ordinal(w)+(afterGame?1:0),ExcludedCompletedMatches=w.Fixtures.Where(Played).Select(m=>m.Id).ToHashSet()};
        if(!MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.LoanDisbursement,500_000m,"Empréstimo clássico: principal R$ 500.000",$"loan:{loan.Id}:disbursement"))
            return(false,"Operação bancária já registrada.",null);
        loan.History.Add(new($"loan:{loan.Id}:disbursement",w.SeasonNumber,w.RoundNumber,null,"DISBURSEMENT",0,0,0,500_000m,0,500_000m));
        w.BankLoans.Add(loan);Remember(w,c,key,"TAKE_LOAN","500000",loan.Id);AssessCredit(w,c);
        return(true,null,loan.Id);
    }

    public static void SettleLoans(GameWorld w,IEnumerable<MatchFixture> matches)
    {
        EnsureMigrated(w);
        var clubs=w.Clubs.ToDictionary(c=>c.Id);
        foreach(var m in matches.Where(Played).OrderBy(m=>m.SeasonNumber).ThenBy(m=>m.RoundNumber).ThenBy(m=>m.DeterministicMatchIndex))
        foreach(var loan in w.BankLoans.Where(l=>(l.ClubId==m.HomeClubId||l.ClubId==m.AwayClubId)&&!l.IsPaidOff).OrderBy(l=>l.Id))
        {
            if(Ordinal(m)<loan.FirstEligibleOrdinal||loan.ExcludedCompletedMatches.Contains(m.Id)||!loan.ChargedMatches.Add(m.Id))continue;
            if(!clubs.TryGetValue(loan.ClubId,out var club))continue;
            decimal before=loan.RemainingBalance,interest,paid,principal;
            string key=$"loan:{loan.Id}:match:{m.Id}";
            if(loan.Mode==BankLoanMode.ClassicFixed)
            {
                interest=loan.InterestPerMatch;loan.UnpaidInterest+=interest;loan.RemainingBalance+=interest;
                paid=Math.Min(Math.Max(0,club.CashBalance),loan.UnpaidInterest);principal=0;
                if(paid>0)MarketAndEconomyEngine.TryRecordTransaction(w,club,FinancialTransactionCategory.LoanInterestPayment,-paid,
                    $"Juros do empréstimo {loan.Id}: partida oficial concluída",key+":interest");
                loan.UnpaidInterest-=paid;loan.RemainingBalance-=paid;
            }
            else
            {
                loan.LegacyOpeningBalance??=before;
                interest=Math.Round(before*loan.InterestRatePerRound,2);
                decimal payment=Math.Min(before+interest,Math.Max(0,loan.InstallmentPerRound));
                paid=Math.Min(interest,payment);principal=Math.Max(0,payment-paid);
                loan.RemainingBalance=before+interest-payment;loan.RemainingRounds=Math.Max(0,loan.RemainingRounds-1);
                MarketAndEconomyEngine.TryRecordTransaction(w,club,FinancialTransactionCategory.LoanRepayment,-payment,
                    $"Prestação legada: juros {paid:N2}, principal {principal:N2}, saldo {loan.RemainingBalance:N2}",key+":legacy");
            }
            loan.InterestCharged+=interest;loan.InterestPaid+=paid;loan.PrincipalPaid+=principal;
            loan.History.Add(new(key,m.SeasonNumber,m.RoundNumber,m.Id,loan.Mode==BankLoanMode.ClassicFixed?"INTEREST":"LEGACY_INSTALLMENT",
                interest,paid,principal,-paid-principal,before,loan.RemainingBalance));
        }
        foreach(var c in w.Clubs)AssessCredit(w,c);
    }

    public static (bool Success,string? Error) RepayLoan(GameWorld w,Club c,Guid loanId,string key)
    {
        EnsureMigrated(w);var receipt=Receipt(w,c,key,"REPAY_LOAN",loanId.ToString());
        if(receipt.Found)return(receipt.Valid,receipt.Valid?null:"Chave usada por outra operação.");
        if(string.IsNullOrWhiteSpace(key)||!Safe(w))return(false,"Quitação disponível entre partidas, com identificador da operação.");
        var loan=w.BankLoans.FirstOrDefault(l=>l.Id==loanId&&l.ClubId==c.Id);
        if(loan==null)return(false,"Empréstimo não pertence ao clube.");
        SettleLoans(w,w.Fixtures);
        if(loan.IsPaidOff){Remember(w,c,key,"REPAY_LOAN",loanId.ToString(),loanId);return(true,null);}
        decimal due=loan.RemainingBalance;
        if(AvailableCash(w,c)<due)return(false,"Saldo insuficiente para quitar a dívida integral.");
        decimal interest=loan.Mode==BankLoanMode.ClassicFixed?loan.UnpaidInterest:0,principal=due-interest;
        if(interest>0)MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.LoanInterestPayment,-interest,"Juros pendentes na quitação",$"loan:{loan.Id}:repay:{key}:interest");
        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.LoanPrincipalRepayment,-principal,"Quitação integral do saldo bancário",$"loan:{loan.Id}:repay:{key}:principal");
        loan.InterestPaid+=interest;loan.PrincipalPaid+=principal;loan.RemainingBalance=0;loan.UnpaidInterest=0;
        loan.History.Add(new($"loan:{loan.Id}:repay:{key}",w.SeasonNumber,w.RoundNumber,null,"REPAYMENT",0,interest,principal,-due,due,0));
        Remember(w,c,key,"REPAY_LOAN",loanId.ToString(),loanId);AssessCredit(w,c);return(true,null);
    }

    public static decimal ReservedAuctionCash(GameWorld w,Guid clubId,Guid? except=null)
        =>w.Auctions.Where(a=>!a.IsClosed&&a.Id!=except&&a.HighestBidderClubId==clubId).Sum(a=>a.CurrentBid);
    public static decimal AvailableCash(GameWorld w,Club c,Guid? except=null)=>Math.Max(0,c.CashBalance-ReservedAuctionCash(w,c.Id,except));
    public static decimal NextBid(AuctionItem a)=>a.HighestBidderClubId==null?a.MinimumBid:a.CurrentBid+5_000m;
    public static bool AuctionReserved(GameWorld w,Guid playerId)=>w.Auctions.Any(a=>a.PlayerId==playerId&&!a.IsClosed);
}
