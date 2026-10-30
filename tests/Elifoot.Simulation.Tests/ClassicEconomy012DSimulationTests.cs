using System.IO.Compression;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void ClassicEconomy012D_100RealSeasons_ReconcilesMoneyDebtContractsAndInflation()
    {
        string dir=Path.Combine(DocsDir,"economy-012d");Directory.CreateDirectory(dir);
        var seasons=new List<object>();var checkpoints=new List<object>();var seenTx=new HashSet<Guid>();var auctionIds=new HashSet<Guid>();
        var transfers=new Dictionary<Guid,List<FinancialTransaction>>();var loanOffsets=new Dictionary<Guid,int>();
        var cashChanges=new Dictionary<string,decimal>();decimal? lastCash=null;int lastRequests=0,lastContracts=0,lastCrisis=0;
        decimal interest=0,paidInterest=0,principal=0,classicInterest=0;int auctions=0,unsold=0,closed=0;int contractsPreserved=0;
        var auctionReasons=new Dictionary<string,int>();
        var forcedByClub=new Dictionary<Guid,int>();var unsoldByClub=new Dictionary<Guid,int>();
        using var ledger=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,"ledger.jsonl.gz")),CompressionLevel.Fastest));
        using var loans=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,"bank.jsonl.gz")),CompressionLevel.Fastest));
        using var requests=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,"contracts-and-crises.jsonl.gz")),CompressionLevel.Fastest));
        var previousSalaries=new Dictionary<Guid,(decimal Salary,int Contracts,int Remaining)>();
        var closedAuctions=new HashSet<Guid>();
        var result=RunMultiSeasonExperiment("CLASSIC_ECONOMY_012D_100_REAL_SEASONS",199801,100,ScenarioMode.ScenarioA_AllAi,
            observeForce012:(w,phase)=>
            {
                if(phase=="initial")
                {
                    // Explicit bank/insolvency probes, using real rules and actual cash entries.
                    var fourth=w.Clubs.Where(c=>c.DivisionTier==DivisionTier.QuartaDivisao).ToList();
                    foreach(var c in fourth.Take(2))
                    {
                        Assert.True(ClassicEconomyEngine.TakeClassicLoan(w,c,"012d-probe").Success);
                        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.StadiumRenovation,
                            120000-c.CashBalance,"Controlled working capital expense after borrowing","012d-loan-probe-expense");
                    }
                    var legacyClub=fourth[2];w.BankLoans.Add(new(){ClubId=legacyClub.Id,GameWorldId=w.Id,PrincipalAmount=500000,
                        RemainingBalance=500000,RemainingRounds=20,InstallmentPerRound=28750,LegacyOpeningBalance=500000});
                    MarketAndEconomyEngine.TryRecordTransaction(w,legacyClub,FinancialTransactionCategory.LoanDisbursement,500000,"Legacy probe","012d-legacy");
                    var stressed=fourth[3];MarketAndEconomyEngine.TryRecordTransaction(w,stressed,FinancialTransactionCategory.StadiumRenovation,
                        -stressed.CashBalance-1200000,"Controlled insolvency probe","012d-crisis");
                }
                var newTx=w.Transactions.Where(t=>seenTx.Add(t.Id)).ToList();
                foreach(var t in newTx)
                {
                    ledger.WriteLine(JsonSerializer.Serialize(t));string category=t.Category.ToString();cashChanges[category]=cashChanges.GetValueOrDefault(category)+t.Amount;
                    if(t.TransferTransactionId is Guid id&&t.SellerClubId!=null)
                    {if(!transfers.TryGetValue(id,out var list))transfers[id]=list=new();list.Add(t);}
                }
                decimal cash=w.Clubs.Sum(c=>c.CashBalance);
                if(lastCash.HasValue)Assert.Equal(cash-lastCash.Value,newTx.Sum(t=>t.Amount));lastCash=cash;
                foreach(var loan in w.BankLoans)
                {
                    foreach(var e in loan.History.Skip(loanOffsets.GetValueOrDefault(loan.Id)))
                    {loans.WriteLine(JsonSerializer.Serialize(new{loan.Id,loan.ClubId,loan.Mode,Entry=e}));interest+=e.InterestCharged;paidInterest+=e.InterestPaid;principal+=e.PrincipalPaid;if(loan.Mode==BankLoanMode.ClassicFixed)classicInterest+=e.InterestCharged;}
                    loanOffsets[loan.Id]=loan.History.Count;
                    decimal opening=loan.Mode==BankLoanMode.ClassicFixed?loan.PrincipalAmount:loan.LegacyOpeningBalance??loan.PrincipalAmount;
                    Assert.Equal(opening+loan.InterestCharged-loan.InterestPaid-loan.PrincipalPaid,loan.RemainingBalance);
                    Assert.Equal(loan.ChargedMatches.Count,loan.History.Count(e=>e.MatchId!=null));
                    Assert.True(loan.RemainingBalance>=0);if(loan.RemainingBalance>0)Assert.False(loan.IsPaidOff);
                }
                foreach(var r in w.Economy.SalaryRequests.Skip(lastRequests))requests.WriteLine(JsonSerializer.Serialize(new{Kind="REQUEST",Data=(object)r}));lastRequests=w.Economy.SalaryRequests.Count;
                foreach(var c in w.Economy.ContractHistory.Skip(lastContracts))requests.WriteLine(JsonSerializer.Serialize(new{Kind="CONTRACT",Data=(object)c}));lastContracts=w.Economy.ContractHistory.Count;
                foreach(var c in w.Economy.CrisisHistory.Skip(lastCrisis))requests.WriteLine(JsonSerializer.Serialize(new{Kind="CRISIS",Data=(object)c}));lastCrisis=w.Economy.CrisisHistory.Count;
                foreach(var a in w.Auctions)
                {
                    if(auctionIds.Add(a.Id)){auctions++;string reason=a.Reason.ToString();auctionReasons[reason]=auctionReasons.GetValueOrDefault(reason)+1;
                        if(a.Reason==AuctionReason.FinancialInsolvency&&a.FormerClubId is Guid id)forcedByClub[id]=forcedByClub.GetValueOrDefault(id)+1;}
                    if(a.IsClosed&&closedAuctions.Add(a.Id)){closed++;if(a.ClosureReason=="AUCTION_UNSOLD"){unsold++;if(a.FormerClubId is Guid id)unsoldByClub[id]=unsoldByClub.GetValueOrDefault(id)+1;}}
                }
                var contractCounts=w.Economy.ContractHistory.GroupBy(c=>c.PlayerId).ToDictionary(g=>g.Key,g=>g.Count());
                foreach(var p in w.Players)
                {
                    int count=contractCounts.GetValueOrDefault(p.Id);
                    if(previousSalaries.TryGetValue(p.Id,out var previous)&&previous.Contracts==count&&previous.Remaining>1&&p.ContractRoundsRemaining>0)
                    {Assert.Equal(previous.Salary,p.SalaryPerRound);contractsPreserved++;}
                    previousSalaries[p.Id]=(p.SalaryPerRound,count,p.ContractRoundsRemaining);
                }
                foreach(var c in w.Clubs)
                {
                    var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();Assert.InRange(squad.Count,11,24);
                    Assert.True(squad.Count(p=>p.Position==PlayerPosition.G)>=1);Assert.True(squad.Count(p=>p.Position!=PlayerPosition.G)>=10);
                }
                if(phase=="seasonFinalized")
                {
                    int season=w.SeasonNumber-1;
                    var snapshot=new{Season=season,TotalCash=cash,TotalDebt=w.BankLoans.Sum(l=>l.RemainingBalance),InterestCharged=interest,
                        InterestPaid=paidInterest,PrincipalPaid=principal,Loans=w.BankLoans.Count,ActiveLoans=w.BankLoans.Count(l=>!l.IsPaidOff),
                        Requests=w.Economy.SalaryRequests.Count,Accepted=w.Economy.SalaryRequests.Count(r=>r.Status==SalaryRequestStatus.ACCEPTED),
                        Refused=w.Economy.SalaryRequests.Count(r=>r.Status==SalaryRequestStatus.REFUSED),Auctions=auctions,Closed=closed,Unsold=unsold,
                        Insolvent=w.Economy.Crises.Count(c=>c.Value.IsInsolvent),NegativeCash=w.Clubs.Count(c=>c.CashBalance<0),
                        Recovered=w.Economy.CrisisHistory.Count(c=>c.Kind=="RECOVERED"),
                        MinimumSquadClubs=w.Clubs.Count(c=>w.Players.Count(p=>p.ClubId==c.Id)==11),
                        ByDivision=w.Clubs.GroupBy(c=>c.DivisionTier).Select(g=>new{Division=g.Key.ToString(),Cash=g.Sum(c=>c.CashBalance),
                            Debt=w.BankLoans.Where(l=>g.Any(c=>c.Id==l.ClubId)).Sum(l=>l.RemainingBalance),
                            Loans=w.BankLoans.Count(l=>g.Any(c=>c.Id==l.ClubId)),PaidOffLoans=w.BankLoans.Count(l=>l.IsPaidOff&&g.Any(c=>c.Id==l.ClubId)),
                            InterestCharged=w.BankLoans.Where(l=>g.Any(c=>c.Id==l.ClubId)).Sum(l=>l.InterestCharged),
                            InterestPaid=w.BankLoans.Where(l=>g.Any(c=>c.Id==l.ClubId)).Sum(l=>l.InterestPaid),
                            Requests=w.Economy.SalaryRequests.Count(r=>g.Any(c=>c.Id==r.ClubId)),
                            Accepted=w.Economy.SalaryRequests.Count(r=>r.Status==SalaryRequestStatus.ACCEPTED&&g.Any(c=>c.Id==r.ClubId)),
                            Refused=w.Economy.SalaryRequests.Count(r=>r.Status==SalaryRequestStatus.REFUSED&&g.Any(c=>c.Id==r.ClubId)),
                            ForcedAuctions=g.Sum(c=>forcedByClub.GetValueOrDefault(c.Id)),Unsold=g.Sum(c=>unsoldByClub.GetValueOrDefault(c.Id)),
                            Insolvent=g.Count(c=>w.Economy.Crises.TryGetValue(c.Id,out var crisis)&&crisis.IsInsolvent),
                            NegativeCash=g.Count(c=>c.CashBalance<0),MinimumSquadClubs=g.Count(c=>w.Players.Count(p=>p.ClubId==c.Id)==11),
                            MeanSalary=w.Players.Where(p=>p.ClubId!=null&&g.Any(c=>c.Id==p.ClubId)).Average(p=>p.SalaryPerRound),
                            MeanMarketValue=w.Players.Where(p=>p.ClubId!=null&&g.Any(c=>c.Id==p.ClubId)).Average(MarketAndEconomyEngine.CalculateFairValue),
                            CreditLimits=g.GroupBy(c=>ClassicEconomyEngine.AssessCredit(w,c).LoanLimit).ToDictionary(x=>x.Key.ToString(),x=>x.Count())}).ToList(),
                        StrengthBins=new[]{1,10,20,30,40,50}.Select(strength=>{
                            var group=w.Players.Where(p=>p.Strength==strength).ToList();
                            var fixedPlayer=new Player{Strength=strength,Position=PlayerPosition.M,Behavior=PlayerBehavior.Cavalheiro,ContractRoundsRemaining=10};
                            return new{Strength=strength,Population=group.Count,MeanSalary=group.Count==0?(decimal?)null:group.Average(p=>p.SalaryPerRound),
                                MeanMarketValue=group.Count==0?(decimal?)null:group.Average(MarketAndEconomyEngine.CalculateFairValue),
                                FixedQualitySalary=MarketAndEconomyEngine.CalculateFairSalaryPerRound(fixedPlayer),FixedQualityMarketValue=MarketAndEconomyEngine.CalculateFairValue(fixedPlayer)};
                        }).ToList()};
                    seasons.Add(snapshot);if(new[]{1,5,10,25,50,75,100}.Contains(season))checkpoints.Add(snapshot);
                    File.WriteAllText(Path.Combine(dir,"progress.json"),JsonSerializer.Serialize(new{CompletedSeasons=season,Cash=cash,Debt=w.BankLoans.Sum(l=>l.RemainingBalance),Requests=w.Economy.SalaryRequests.Count}));
                    if(season==100)File.WriteAllText(Path.Combine(dir,"salary-requests-final.json"),JsonSerializer.Serialize(w.Economy.SalaryRequests));
                }
            },forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        foreach(var pair in transfers){Assert.Equal(2,pair.Value.Count);Assert.Equal(0,pair.Value.Sum(t=>t.Amount));}
        Assert.Equal(100,result.SeasonsCompleted);Assert.Equal(35100,result.TotalMatchesCompleted);
        Assert.True(classicInterest>0,"The bank probes must exercise actual fixed interest.");
        Assert.Equal(0,result.InvalidSquadLimitViolations);Assert.Equal(0,result.DoubleSettledMatchFinancialViolations);
        File.WriteAllText(Path.Combine(dir,"100-seasons.json"),JsonSerializer.Serialize(new{Status="PASS",Seed=199801,Scenario="All AI with explicit initial bank, legacy and insolvency probes",
            result.SeasonsCompleted,result.TotalMatchesCompleted,result.RealMinutesSimulated,result.InvalidLineupSelectionViolations,
            result.InvalidSquadLimitViolations,result.DoubleSettledMatchFinancialViolations,ContractsPreservedChecks=contractsPreserved,
            BilateralAndDebtAuctionPairs=transfers.Count,AuctionsByReason=auctionReasons,ReconciledCashByCategory=cashChanges,InterestCharged=interest,ClassicInterestCharged=classicInterest,InterestPaid=paidInterest,PrincipalPaid=principal,
            Seasons=seasons,Checkpoints=checkpoints},new JsonSerializerOptions{WriteIndented=true}));
    }
}
