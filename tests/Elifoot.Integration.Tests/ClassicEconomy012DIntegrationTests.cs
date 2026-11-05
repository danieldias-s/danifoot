using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class ClassicEconomy012DIntegrationTests
{
    [Fact]
    public async Task AiAndConcurrentHumanBiddersCannotSellTheSameDebtPlayerTwice()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Auctions.Clear();var humans=new List<HumanCoach>();
        foreach(var c in w.Clubs.Take(4)){var h=new HumanCoach{CurrentClubId=c.Id};c.HumanCoachId=h.Id;w.HumanCoaches.Add(h);humans.Add(h);c.CashBalance=20000000;}
        var seller=w.Clubs[0];seller.CashBalance=-100000;var p=w.Players.Last(p=>p.ClubId==seller.Id);p.Strength=50;
        foreach(var ai in w.Clubs.Where(c=>c.HumanCoachId==null))ai.CashBalance=20000000;
        ClassicEconomyEngine.EvaluateInsolvency(w);var a=Assert.Single(w.Auctions);
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        var gate=MultiplayerRoundOrchestrator.GetRoomLock(w.Id);await gate.WaitAsync();
        try{MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);}finally{gate.Release();}
        Assert.NotEmpty(a.PrivateAiMaxBids);Assert.NotNull(a.HighestBidderClubId);
        var results=await Task.WhenAll(humans.Skip(1).Select((h,i)=>service.PlaceAuctionBidAsync(w.Id,h.Id,a.Id,5000000+i*1000000,idempotencyKey:"human-bid-"+i)));
        Assert.Contains(results,r=>r.Success);Assert.Equal(7000000,a.CurrentBid);Assert.Equal(w.Clubs[3].Id,a.HighestBidderClubId);
        await gate.WaitAsync();
        try
        {
            w.RoundState=RoundState.RESULTS;
            w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");
            MarketAndEconomyEngine.SettleAuction(w,a);
            MarketAndEconomyEngine.SettleAuction(w,a);
        }
        finally{gate.Release();}
        Assert.Equal(w.Clubs[3].Id,p.ClubId);Assert.Equal(6900000,seller.CashBalance);
        Assert.Single(w.Economy.AuctionHistory,r=>r.AuctionId==a.Id&&r.Outcome=="SOLD");
        var tx=w.Transactions.Where(t=>t.PlayerId==p.Id&&t.TransactionType=="FinancialInsolvency").ToList();Assert.Equal(2,tx.Count);Assert.Equal(0,tx.Sum(t=>t.Amount));
    }
    [Theory][InlineData(2)][InlineData(4)][InlineData(6)][InlineData(8)]
    public async Task HttpAndSignalRFinanceShareRoomLockAndIdempotency(int humans)
    {
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
            b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider","InMemory");b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");});
        using var client=factory.CreateClient();var repo=factory.Services.GetRequiredService<IWorldRepository>();
        var w=WorldSeedFactory.CreateSeededWorld();w.Auctions.Clear();var coaches=new List<HumanCoach>();
        foreach(var c in w.Clubs.Take(humans))
        {var h=new HumanCoach{CurrentClubId=c.Id};c.HumanCoachId=h.Id;w.HumanCoaches.Add(h);coaches.Add(h);}
        await repo.SaveWorldAsync(w);var club=w.Clubs[0];club.Stadium.UsableCapacity=40000;
        await using var hub=new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",o=>o.HttpMessageHandlerFactory=_=>factory.Server.CreateHandler()).Build();
        var notification=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>("MarketUpdated",_=>notification.TrySetResult());
        await hub.StartAsync();await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom",w.Id,coaches[0].Id);
        decimal before=club.CashBalance;
        string url=$"/api/worlds/{w.Id}/clubs/{club.Id}/loans?coachId={coaches[0].Id}&amount=500000&rounds=0&idempotencyKey=repeat&expectedRoundVersion={w.RoundVersion}";
        var responses=await Task.WhenAll(Enumerable.Range(0,humans).Select(_=>client.PostAsync(url,null)));
        foreach(var response in responses)Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        Assert.Single(w.BankLoans);Assert.Equal(before+500000,club.CashBalance);
        await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
        w.RoundState=RoundState.RUNNING_FIRST_HALF;
        var replay=await client.PostAsync(url,null);Assert.True(replay.IsSuccessStatusCode);
        var during=await client.PostAsync(url.Replace("idempotencyKey=repeat","idempotencyKey=new"),null);Assert.False(during.IsSuccessStatusCode);
        w.RoundState=RoundState.PREPARING;
        var wrongClub=await client.PostAsync(url.Replace($"clubs/{club.Id}",$"clubs/{w.Clubs[1].Id}").Replace("idempotencyKey=repeat","idempotencyKey=wrong"),null);
        Assert.False(wrongClub.IsSuccessStatusCode);
        var bids=new List<AuctionItem>();
        foreach(var p in w.Players.Where(p=>p.ClubId==w.Clubs.Last().Id).Take(humans).ToList())
        {p.ClubId=null;var a=new AuctionItem{PlayerId=p.Id,Reason=AuctionReason.SalaryRenewalRefused};w.Auctions.Add(a);bids.Add(a);}
        club.CashBalance=100000;
        var bidResponses=await Task.WhenAll(bids.Select((a,i)=>client.PostAsync($"/api/worlds/{w.Id}/market/auction/{a.Id}/bid?coachId={coaches[0].Id}&bidAmount=60000&idempotencyKey=bid{i}",null)));
        Assert.Single(bidResponses,r=>r.IsSuccessStatusCode);Assert.Equal(60000,ClassicEconomyEngine.ReservedAuctionCash(w,club.Id));
        var player=w.Players.First(p=>p.ClubId==club.Id);player.ContractRoundsRemaining=0;var request=new SalaryRaiseRequest{Id=Guid.NewGuid(),ClubId=club.Id,PlayerId=player.Id,
            CurrentSalary=player.SalaryPerRound,RequestedSalary=player.SalaryPerRound+10,RenewalMatches=20};w.Economy.SalaryRequests.Add(request);
        var decisions=await Task.WhenAll(Enumerable.Range(0,humans).Select(_=>client.PostAsJsonAsync($"/api/worlds/{w.Id}/clubs/{club.Id}/salary-requests/{request.Id}/respond",
            new SalaryDecisionCommandDto(coaches[0].Id,true,"salary-repeat",w.RoundVersion))));
        Assert.All(decisions,r=>Assert.True(r.IsSuccessStatusCode));Assert.Equal(SalaryRequestStatus.ACCEPTED,request.Status);
        Assert.Single(w.Economy.ContractHistory,r=>r.Key==$"salary:{request.Id}:accepted");
        var finance=await client.GetFromJsonAsync<JsonElement>($"/api/worlds/{w.Id}/clubs/{club.Id}/finances");
        Assert.Equal(500000,finance.GetProperty("credit").GetProperty("outstandingDebt").GetDecimal());
        Assert.Equal("ClassicFixed",finance.GetProperty("loans")[0].GetProperty("mode").GetString());
        await hub.StopAsync();
    }

    [Fact]
    public async Task SqliteRestartRetainsDebtModeSalaryRequestsAndCommandReceipts()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var services=new ServiceCollection();services.AddDbContext<ElifootDbContext>(o=>o.UseSqlite(connection));
        await using var provider=services.BuildServiceProvider();using(var scope=provider.CreateScope())await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repo=new EfWorldRepository(provider.GetRequiredService<IServiceScopeFactory>());var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];
        var h=new HumanCoach{CurrentClubId=c.Id};c.HumanCoachId=h.Id;w.HumanCoaches.Add(h);await repo.SaveWorldAsync(w);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await service.TakeBankLoanAsync(w.Id,h.Id,500000,0,idempotencyKey:"persist")).Success);
        var legacy=new BankLoan{ClubId=c.Id,RemainingBalance=8654,RemainingRounds=0,InterestRatePerRound=.015m,InstallmentPerRound=28750,LegacyOpeningBalance=8654};w.BankLoans.Add(legacy);
        var p=w.Players.First(p=>p.ClubId==c.Id);p.ContractRoundsRemaining=0;decimal salary=p.SalaryPerRound;
        var request=new SalaryRaiseRequest{Id=Guid.NewGuid(),ClubId=c.Id,PlayerId=p.Id,CurrentSalary=salary,RequestedSalary=salary+100,RenewalMatches=20};w.Economy.SalaryRequests.Add(request);
        await repo.SaveWorldAsync(w);decimal cash=c.CashBalance;EfWorldRepository.ClearCacheForTesting();
        var restored=(await repo.GetWorldAsync(w.Id))!;Assert.Equal(2,restored.BankLoans.Count);Assert.False(restored.BankLoans.Single(l=>l.Id==legacy.Id).IsPaidOff);
        Assert.Equal(BankLoanMode.LegacyInstallment,restored.BankLoans.Single(l=>l.Id==legacy.Id).Mode);Assert.Equal(SalaryRequestStatus.PENDING,restored.Economy.SalaryRequests[0].Status);
        Assert.True((await service.TakeBankLoanAsync(w.Id,h.Id,500000,0,idempotencyKey:"persist",expectedRoundVersion:-1)).Success);
        Assert.Equal(cash,restored.Clubs[0].CashBalance);Assert.Equal(2,restored.BankLoans.Count);
        Assert.True((await service.RespondSalaryAsync(w.Id,h.Id,c.Id,request.Id,true,"persist-salary")).Success);
        EfWorldRepository.ClearCacheForTesting();var twice=(await repo.GetWorldAsync(w.Id))!;
        Assert.Equal(SalaryRequestStatus.ACCEPTED,twice.Economy.SalaryRequests[0].Status);Assert.Equal(salary+100,twice.Players.First(p=>p.Id==request.PlayerId).SalaryPerRound);
        Assert.Single(twice.Economy.ContractHistory,r=>r.Key==$"salary:{request.Id}:accepted");
    }
}
