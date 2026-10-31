using System.IO.Compression;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void EconomyBalance012DFix_Experiment()
    {
        string name=Environment.GetEnvironmentVariable("ECONOMY_FIX_CASE")??"final";
        int seed=int.Parse(Environment.GetEnvironmentVariable("ECONOMY_FIX_SEED")??"199801");
        int seasons=int.Parse(Environment.GetEnvironmentVariable("ECONOMY_FIX_SEASONS")??"100");
        RunEconomyBalanceCase(name,seed,seasons);
    }
    private sealed class BalanceYear
    {
        public Dictionary<string,decimal> Categories {get;}=new();
        public decimal InternalNet,LegacyInterest,InterestCharged,InterestPaid,PrincipalPaid;
        public int Transactions,Games,LoanDisbursements;
        public Dictionary<Guid,Dictionary<string,decimal>> ClubCategories {get;}=new();
    }
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void Integration014A_CreatedWorld100SeasonsReconcilesCashDebtAndTransfers(FootballGender gender)
    {
        RunEconomyBalanceCase("final",199801,100,
            () => Elifoot.Testing.Imported014AWorlds.Create(gender),
            Path.Combine(DocsDir,"integration-014a",gender.ToString()));
    }
    private static void RunEconomyBalanceCase(string name,int seed,int seasonCount,Func<GameWorld>? worldFactory=null,string? reportFolder=null)
    {
        string dir=reportFolder??Path.Combine(DocsDir,"balance-012d-fix",name+"-"+seed);Directory.CreateDirectory(dir);
        var books=new Dictionary<int,BalanceYear>();var seen=new HashSet<Guid>();var keys=new HashSet<string>();
        var pairs=new Dictionary<Guid,(int Count,decimal Net)>();var loanOffsets=new Dictionary<Guid,int>();
        var years=new List<object>();var clubSeries=new List<object>();var negativeStreak=new Dictionary<Guid,int>();
        var previousCash=new Dictionary<Guid,decimal>();var divisions=new Dictionary<Guid,DivisionTier>();
        var initialCashByClub=new Dictionary<Guid,decimal>();
        decimal opening=0,openingDebt=0,totalFlow=0;int ledgerCount=0;GameWorld? final=null;
        using var ledger=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,"ledger.jsonl.gz")),CompressionLevel.Fastest));
        BalanceYear Book(int season){if(!books.TryGetValue(season,out var b))books[season]=b=new();return b;}
        var result=RunMultiSeasonExperiment("ECONOMY_BALANCE_012D_FIX_"+name,seed,seasonCount,ScenarioMode.ScenarioA_AllAi,
            observeForce012:(w,phase)=>
            {
                if(phase=="initial")
                {
                    ConfigureOperatingExperiment(w,name);
                    opening=w.Clubs.Sum(c=>c.CashBalance);previousCash=w.Clubs.ToDictionary(c=>c.Id,c=>c.CashBalance);
                    initialCashByClub=new(previousCash);
                    divisions=w.Clubs.ToDictionary(c=>c.Id,c=>c.DivisionTier);openingDebt=w.BankLoans.Sum(l=>l.RemainingBalance);
                    var fourth=w.Clubs.Where(c=>c.DivisionTier==DivisionTier.QuartaDivisao).ToList();
                    foreach(var c in fourth.Take(2))
                    {
                        Assert.True(ClassicEconomyEngine.TakeClassicLoan(w,c,"012d-probe").Success);
                        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.StadiumRenovation,120000-c.CashBalance,
                            "Controlled working capital expense after borrowing","012d-loan-probe-expense");
                    }
                    var legacy=fourth[2];w.BankLoans.Add(new(){ClubId=legacy.Id,GameWorldId=w.Id,PrincipalAmount=500000,RemainingBalance=500000,
                        RemainingRounds=20,InstallmentPerRound=28750,LegacyOpeningBalance=500000});
                    MarketAndEconomyEngine.TryRecordTransaction(w,legacy,FinancialTransactionCategory.LoanDisbursement,500000,"Legacy probe","012d-legacy");
                    var stressed=fourth[3];MarketAndEconomyEngine.TryRecordTransaction(w,stressed,FinancialTransactionCategory.StadiumRenovation,
                        -stressed.CashBalance-1200000,"Controlled insolvency probe","012d-crisis");
                    File.WriteAllText(Path.Combine(dir,"initial-clubs.json"),JsonSerializer.Serialize(w.Clubs.Select(c=>new{c.Id,c.Name,c.DivisionTier,OpeningCash=previousCash[c.Id]})));
                }
                foreach(var tx in w.Transactions.Where(t=>seen.Add(t.Id)))
                {
                    Assert.True(keys.Add($"{tx.ClubId}:{tx.IdempotencyKey}"));ledger.WriteLine(JsonSerializer.Serialize(tx));ledgerCount++;
                    if(tx.Category==FinancialTransactionCategory.InitialBalance)continue;
                    var b=Book(tx.SeasonNumber);string category=tx.Category.ToString();b.Categories[category]=b.Categories.GetValueOrDefault(category)+tx.Amount;b.Transactions++;totalFlow+=tx.Amount;
                    if(!b.ClubCategories.TryGetValue(tx.ClubId,out var clubBook))b.ClubCategories[tx.ClubId]=clubBook=new();
                    clubBook[category]=clubBook.GetValueOrDefault(category)+tx.Amount;
                    if(tx.Category==FinancialTransactionCategory.LoanDisbursement)b.LoanDisbursements++;
                    if(tx.SellerClubId!=null&&tx.TransferTransactionId is Guid id)
                    {b.InternalNet+=tx.Amount;var pair=pairs.GetValueOrDefault(id);pairs[id]=(pair.Count+1,pair.Net+tx.Amount);}
                }
                Assert.Equal(opening+totalFlow,w.Clubs.Sum(c=>c.CashBalance));
                foreach(var l in w.BankLoans)
                {
                    foreach(var e in l.History.Skip(loanOffsets.GetValueOrDefault(l.Id)))
                    {var b=Book(e.Season);b.InterestCharged+=e.InterestCharged;b.InterestPaid+=e.InterestPaid;b.PrincipalPaid+=e.PrincipalPaid;if(l.Mode==BankLoanMode.LegacyInstallment)b.LegacyInterest+=e.InterestPaid;}
                    loanOffsets[l.Id]=l.History.Count;
                    Assert.Equal((l.Mode==BankLoanMode.ClassicFixed?l.PrincipalAmount:l.LegacyOpeningBalance??l.PrincipalAmount)+l.InterestCharged-l.InterestPaid-l.PrincipalPaid,l.RemainingBalance);
                }
                if(phase=="round")Book(w.SeasonNumber).Games+=w.Fixtures.Count(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber&&ClassicEconomyEngine.Played(m));
                if(phase!="seasonFinalized")return;
                int season=w.SeasonNumber-1;var book=Book(season);var squads=w.Players.Where(p=>p.ClubId!=null).GroupBy(p=>p.ClubId!.Value).ToDictionary(g=>g.Key,g=>g.ToList());
                var clubs=w.Clubs.OrderBy(c=>c.Id).Select(c=>{
                    var squad=squads[c.Id];Assert.InRange(squad.Count,11,24);Assert.True(squad.Count(p=>p.Position==PlayerPosition.G)>=1);Assert.True(squad.Count(p=>p.Position!=PlayerPosition.G)>=10);
                    decimal debt=w.BankLoans.Where(l=>l.ClubId==c.Id).Sum(l=>l.RemainingBalance);
                    bool insolvent=c.CashBalance<0||w.BankLoans.Any(l=>l.ClubId==c.Id&&l.UnpaidInterest>0);
                    negativeStreak[c.Id]=insolvent?negativeStreak.GetValueOrDefault(c.Id)+1:0;
                    return new{c.Id,c.Name,Division=divisions[c.Id].ToString(),NextDivision=c.DivisionTier.ToString(),OpeningCash=previousCash[c.Id],Cash=c.CashBalance,
                        InitialCash=initialCashByClub[c.Id],Accumulation=c.CashBalance-initialCashByClub[c.Id],
                        Debt=debt,SquadCount=squad.Count,NominalStrength=squad.Average(p=>p.Strength),WageBillPerGame=squad.Sum(p=>p.SalaryPerRound),
                        RosterMarketValue=squad.Sum(MarketAndEconomyEngine.CalculateFairValue),StadiumAssetProxy=c.Stadium.UsableCapacity*w.Economy.Rules.StadiumAssetPerUsableSeat,
                        Insolvent=insolvent,ConsecutiveInsolventSeasons=negativeStreak[c.Id],c.Stadium.BuiltCapacity,c.Stadium.UsableCapacity};
                }).ToList();
                decimal cash=clubs.Sum(c=>c.Cash),debtTotal=clubs.Sum(c=>c.Debt),start=previousCash.Values.Sum();
                decimal credit=book.Categories.GetValueOrDefault("LoanDisbursement")+book.Categories.GetValueOrDefault("LoanPrincipalRepayment")+book.Categories.GetValueOrDefault("LoanRepayment")+book.LegacyInterest;
                decimal externalRevenue=book.Categories.Where(p=>p.Value>0&&p.Key!="LoanDisbursement"&&p.Key!="TransferSale"&&p.Key!="AuctionSale").Sum(p=>p.Value);
                decimal externalNet=book.Categories.Values.Sum()-book.InternalNet-credit,externalExpenses=externalRevenue-externalNet;
                Assert.Equal(0,book.InternalNet);Assert.Equal(cash,start+externalRevenue-externalExpenses+book.InternalNet+credit);
                Assert.Equal(debtTotal,openingDebt+book.Categories.GetValueOrDefault("LoanDisbursement")+book.InterestCharged-book.InterestPaid-book.PrincipalPaid);
                var ordered=clubs.Select(c=>c.Cash).Order().ToList();decimal median=(ordered[63]+ordered[64])/2;
                var snapshot=new{Season=season,OpeningCash=start,TotalCash=cash,MeanCash=cash/128,MedianCash=median,MinimumCash=ordered[0],MaximumCash=ordered[^1],
                    Debt=debtTotal,OpeningDebt=openingDebt,AssetsRoster=clubs.Sum(c=>c.RosterMarketValue),AssetsStadiumProxy=clubs.Sum(c=>c.StadiumAssetProxy),
                    NetWorthProxy=cash+clubs.Sum(c=>c.RosterMarketValue+c.StadiumAssetProxy)-debtTotal,
                    ExternalRevenue=externalRevenue,ExternalExpenses=externalExpenses,InternalTransfersNet=book.InternalNet,CreditNet=credit,ReconciliationDifference=0,
                    Categories=book.Categories,book.Transactions,book.Games,book.LoanDisbursements,book.InterestCharged,book.InterestPaid,book.PrincipalPaid,
                    InsolventClubs=clubs.Count(c=>c.Insolvent),NegativeCashClubs=clubs.Count(c=>c.Cash<0),MinimumSquadClubs=clubs.Count(c=>c.SquadCount==11),
                    PersistentInsolventClubs=clubs.Count(c=>c.ConsecutiveInsolventSeasons>=5),Recovered=w.Economy.CrisisHistory.Count(e=>e.Season==season&&e.Kind=="RECOVERED"),
                    ByDivision=clubs.GroupBy(c=>c.Division).Select(g=>new{Division=g.Key,Clubs=g.Count(),Cash=g.Sum(c=>c.Cash),MeanCash=g.Average(c=>c.Cash),
                        Debt=g.Sum(c=>c.Debt),AssetsRoster=g.Sum(c=>c.RosterMarketValue),AssetsStadiumProxy=g.Sum(c=>c.StadiumAssetProxy),Insolvent=g.Count(c=>c.Insolvent),
                        WageBillPerGame=g.Sum(c=>c.WageBillPerGame),NominalStrength=g.Average(c=>c.NominalStrength),
                        CashFlow=g.SelectMany(c=>book.ClubCategories.GetValueOrDefault(c.Id)??new()).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Sum(p=>p.Value))}).ToList(),
                    TopTen=clubs.OrderByDescending(c=>c.Cash).Take(10).ToList(),TopTenShare=cash>0?clubs.OrderByDescending(c=>c.Cash).Take(10).Sum(c=>c.Cash)/cash:0};
                years.Add(snapshot);clubSeries.Add(new{Season=season,Clubs=clubs});previousCash=clubs.ToDictionary(c=>c.Id,c=>c.Cash);openingDebt=debtTotal;
                divisions=w.Clubs.ToDictionary(c=>c.Id,c=>c.DivisionTier);final=w;
                File.WriteAllText(Path.Combine(dir,"progress.json"),JsonSerializer.Serialize(new{Season=season,Cash=cash,Median=median,Debt=debtTotal,Insolvent=clubs.Count(c=>c.Insolvent)}));
            },forceEvolutionMode:ForceEvolutionMode.ClassicAdapted,worldFactory:worldFactory);
        Assert.All(pairs.Values,p=>{Assert.Equal(2,p.Count);Assert.Equal(0,p.Net);});
        Assert.Equal(seasonCount*351,result.TotalMatchesCompleted);Assert.Equal(0,result.InvalidSquadLimitViolations);Assert.Equal(0,result.DoubleSettledMatchFinancialViolations);
        if(name=="baseline"&&seed==199801&&seasonCount==100)Assert.Equal(18_107_128_451.84m,final!.Clubs.Sum(c=>c.CashBalance));
        File.WriteAllText(Path.Combine(dir,"seasons.json"),JsonSerializer.Serialize(years,new JsonSerializerOptions{WriteIndented=true}));
        using(var stream=new StreamWriter(new GZipStream(File.Create(Path.Combine(dir,"clubs.json.gz")),CompressionLevel.Fastest)))stream.Write(JsonSerializer.Serialize(clubSeries));
        File.WriteAllText(Path.Combine(dir,"summary.json"),JsonSerializer.Serialize(new{Case=name,Seed=seed,Seasons=seasonCount,SourceCheckpoint=worldFactory==null?"7529e69":"7a00bdd+47608e9",Gender=final!.Gender.ToString(),WorldSnapshot=final.CatalogOrigin?.DataHash,FixtureWorld=worldFactory!=null,InitialCash=opening,
            FinalCash=final!.Clubs.Sum(c=>c.CashBalance),FinalDebt=final.BankLoans.Sum(l=>l.RemainingBalance),Transactions=ledgerCount,TransferPairs=pairs.Count,
            result.TotalMatchesCompleted,result.RealMinutesSimulated,result.InvalidLineupSelectionViolations,result.InvalidSquadLimitViolations,result.DoubleSettledMatchFinancialViolations,
            OperatingRules=final.Economy.OperatingRules,SalaryRequests=final.Economy.SalaryRequests.Count,ForcedAuctions=final.Economy.CrisisHistory.Count(e=>e.Kind=="FORCED_AUCTION"),
            Unsold=final.Economy.AuctionHistory.Count(a=>a.Outcome=="AUCTION_UNSOLD"),Crises=final.Economy.CrisisHistory.Count(e=>e.Kind=="INSOLVENT"),Recovered=final.Economy.CrisisHistory.Count(e=>e.Kind=="RECOVERED")},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void ConfigureOperatingExperiment(GameWorld w,string name)
    {
        w.Economy.OperatingRules=name switch
        {
            "baseline"=>new(){Name="AUDIT_LEGACY_012D"},
            "h1-annual-sponsor"=>new(){Name=name,AnnualSponsorship=true},
            "h2-quality-linear"=>new(){Name=name,AnnualSponsorship=true,GateQualityExponent=1},
            "h3-quality-square"=>new(){Name=name,AnnualSponsorship=true,GateQualityExponent=2},
            "h4-debt-reserve"=>new(){Name=name,AnnualSponsorship=true,GateQualityExponent=2,ReserveBorrowedCapitalForAi=true},
            "h5-credit-surplus"=>new(){Name=name,AnnualSponsorship=true,GateQualityExponent=2,ReserveBorrowedCapitalForAi=true,AiCreditRequiresOperatingSurplus=true},
            "final"=>OperatingEconomyEngine.CurrentRules(),
            "h6-quality-2p2"=>new(){Name=name,AnnualSponsorship=true,GateQualityExponent=2.2,ReserveBorrowedCapitalForAi=true,AiCreditRequiresOperatingSurplus=true},
            _=>throw new InvalidOperationException("Unregistered operating economy experiment: "+name)
        };
    }
}
