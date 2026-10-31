using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class VoluntaryAuctionIntegrationTests
{
    private static WebApplicationFactory<Program> Factory(string? file=null)=>new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
        b.UseSetting("Elifoot:DatabaseProvider",file==null?"InMemory":"Sqlite");
        if(file!=null)b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });
    private static (GameWorld W,HumanCoach Owner,HumanCoach Bidder,Player P) Seed()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.Auctions.Clear();w.RoundNumber=7;
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid(); // No AI market mutation in this controlled acceptance case.
        var a=new HumanCoach{CurrentClubId=w.Clubs[0].Id,Nickname="Seller",IsConnected=true};var b=new HumanCoach{CurrentClubId=w.Clubs[1].Id,Nickname="Buyer",IsConnected=true};
        w.Clubs[0].HumanCoachId=a.Id;w.Clubs[1].HumanCoachId=b.Id;w.HumanCoaches.AddRange([a,b]);
        return(w,a,b,w.Players.Last(p=>p.ClubId==a.CurrentClubId));
    }
    [Fact]
    public async Task HttpDefaultsConcurrentReplaySignalRAndRealR7R8Settlement()
    {
        await using var factory=Factory();using var client=factory.CreateClient();var repo=factory.Services.GetRequiredService<IWorldRepository>();
        var(w,owner,bidder,p)=Seed();await repo.SaveWorldAsync(w);
        await using var hub=new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",o=>o.HttpMessageHandlerFactory=_=>factory.Server.CreateHandler()).Build();
        var update=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);hub.On<JsonElement>("MarketUpdated",_=>update.TrySetResult());
        await hub.StartAsync();await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom",w.Id,owner.Id);
        var command=new{coachId=owner.Id,playerId=p.Id,idempotencyKey="same-auction",expectedRoundVersion=w.RoundVersion};
        string url=$"/api/worlds/{w.Id}/market/auction";
        var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>client.PostAsJsonAsync(url,command)));
        var ids=new List<Guid>();foreach(var result in results){Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());ids.Add((await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("auctionId").GetGuid());}
        Assert.Single(ids.Distinct());await update.Task.WaitAsync(TimeSpan.FromSeconds(5));var a=Assert.Single(w.Auctions);
        Assert.Equal(500000,a.MinimumBid);Assert.Equal(2,a.DurationRounds);Assert.Equal(8,a.ExpiresAtRound);
        Assert.False((await client.PostAsJsonAsync(url,new{coachId=owner.Id,playerId=p.Id,idempotencyKey="same-auction",minimumBid=0,durationRounds=3})).IsSuccessStatusCode);
        Assert.True((await client.PostAsync($"/api/worlds/{w.Id}/market/auction/{a.Id}/bid?coachId={bidder.Id}&bidAmount=500000&idempotencyKey=bid",null)).IsSuccessStatusCode);
        for(int i=0;i<3;i++){
            var market=await client.GetFromJsonAsync<JsonElement>($"/api/worlds/{w.Id}/market");
            var actual=market.GetProperty("auctions").EnumerateArray().Single(x=>x.GetProperty("auctionId").GetGuid()==a.Id);
            Assert.Equal("Voluntary",actual.GetProperty("reason").GetString());Assert.Equal(8,actual.GetProperty("expiresAtRound").GetInt32());Assert.Equal(2,actual.GetProperty("durationRounds").GetInt32());Assert.False(actual.GetProperty("isClosed").GetBoolean());
        }
        foreach(int round in new[]{7,8}){
            foreach(var coach in new[]{owner,bidder}){
                var detail=(await client.GetFromJsonAsync<ClubDetailDto>($"/api/worlds/{w.Id}/clubs/{coach.CurrentClubId}"))!;
                var snap=(await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{w.Id}/snapshot"))!;
                var result=await client.PostAsJsonAsync($"/api/worlds/{w.Id}/tactics",new SubmitTacticsRequest(w.Id,coach.Id,detail.PreferredFormation,detail.PreferredStarterIds,detail.PreferredBenchIds,$"ready-{round}-{coach.Id}",snap.RoundVersion));
                Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());
            }
            for(int step=0;step<24;step++){
                var snap=(await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{w.Id}/snapshot"))!;if(snap.RoundState==RoundState.RESULTS)break;
                Assert.False(a.IsClosed); // No partial game completion can end the global auction.
                if(snap.ActivePauseWindow!=null)foreach(var coach in new[]{owner,bidder})Assert.True((await client.PostAsJsonAsync($"/api/worlds/{w.Id}/continue",new ConfirmContinueMatchCommand(w.Id,coach.Id,$"continue-{round}-{step}-{coach.Id}"))).IsSuccessStatusCode);
                else Assert.True((await client.PostAsync($"/api/worlds/{w.Id}/tick?ticks=125",null)).IsSuccessStatusCode);
            }
            Assert.Equal(RoundState.RESULTS,w.RoundState);Assert.Equal(round==8,a.IsClosed);
            if(round==7){Assert.Equal(owner.CurrentClubId,p.ClubId);Assert.True((await client.PostAsync($"/api/worlds/{w.Id}/next-round",null)).IsSuccessStatusCode);Assert.False(a.IsClosed);}
        }
        Assert.Equal(bidder.CurrentClubId,p.ClubId);Assert.Equal("SOLD",a.ClosureReason);
        var tx=w.Transactions.Where(t=>t.PlayerId==p.Id&&t.TransactionType=="VoluntaryAuction").ToList();Assert.Equal(2,tx.Count);Assert.Equal(0,tx.Sum(t=>t.Amount));
        Assert.Equal(500000,tx.Single(t=>t.Category==FinancialTransactionCategory.AuctionSale).Amount);
        MarketAndEconomyEngine.SettleAuction(w,a);Assert.Single(w.Economy.AuctionHistory,h=>h.AuctionId==a.Id);
        Assert.True((await client.PostAsJsonAsync(url,command)).IsSuccessStatusCode);Assert.Single(w.Auctions,x=>x.Reason==AuctionReason.Voluntary);
        await hub.StopAsync();
    }
    [Fact]
    public async Task SqliteReloadRetainsCustomZeroDurationThreeAndIdempotentReceipt()
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-voluntary-auction-{Guid.NewGuid():N}.db");
        try{
            var(w,owner,bidder,p)=Seed();Guid id;
            await using(var first=Factory(file)){
                using var client=first.CreateClient();await first.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var result=await client.PostAsJsonAsync($"/api/worlds/{w.Id}/market/auction",new{coachId=owner.Id,playerId=p.Id,minimumBid=0,durationRounds=3,idempotencyKey="persist"});Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());id=(await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("auctionId").GetGuid();
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var second=Factory(file)){
                using var client=second.CreateClient();var repo=second.Services.GetRequiredService<IWorldRepository>();var restored=(await repo.GetWorldAsync(w.Id))!;
                var a=restored.Auctions.Single(x=>x.Id==id);Assert.Equal(0,a.MinimumBid);Assert.Equal(3,a.DurationRounds);Assert.Equal(9,a.ExpiresAtRound);Assert.False(a.IsClosed);Assert.Equal(owner.CurrentClubId,restored.Players.Single(x=>x.Id==p.Id).ClubId);
                var result=await client.PostAsJsonAsync($"/api/worlds/{w.Id}/market/auction",new{coachId=owner.Id,playerId=p.Id,minimumBid=0,durationRounds=3,idempotencyKey="persist",expectedRoundVersion=-1});Assert.True(result.IsSuccessStatusCode);Assert.Single(restored.Auctions,x=>x.Reason==AuctionReason.Voluntary);
                Assert.False((await client.PostAsJsonAsync($"/api/worlds/{w.Id}/market/auction",new{coachId=bidder.Id,playerId=p.Id,idempotencyKey="other"})).IsSuccessStatusCode);
                foreach(int duration in new[]{0,4})Assert.False((await client.PostAsJsonAsync($"/api/worlds/{w.Id}/market/auction",new{coachId=owner.Id,playerId=restored.Players.First(x=>x.ClubId==owner.CurrentClubId&&x.Id!=p.Id).Id,durationRounds=duration,idempotencyKey="invalid-"+duration})).IsSuccessStatusCode);
            }
        }finally{EfWorldRepository.ClearCacheForTesting();if(File.Exists(file))File.Delete(file);}
    }
}
