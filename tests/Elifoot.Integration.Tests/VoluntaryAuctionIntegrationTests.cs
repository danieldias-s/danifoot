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
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider",file==null?"InMemory":"Sqlite");
        if(file!=null)b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });
    private static (GameWorld W,HumanCoach Owner,HumanCoach Bidder,Player P) Seed()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.Auctions.Clear();w.RoundNumber=7;
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid(); // No AI market mutation in this controlled acceptance case.
        var a=new HumanCoach{CurrentClubId=w.Clubs[0].Id,Nickname="Seller",IsConnected=true};var b=new HumanCoach{CurrentClubId=w.Clubs[1].Id,Nickname="Buyer",IsConnected=true};
        w.Clubs[0].HumanCoachId=a.Id;w.Clubs[1].HumanCoachId=b.Id;w.HumanCoaches.AddRange([a,b]);
        // This two-client scenario starts in R7. Other squads have already renewed,
        // so the fixture's synthetic owners cannot leave unanswerable salary requests.
        foreach(var player in w.Players.Where(p=>p.ClubId!=a.CurrentClubId&&p.ClubId!=b.CurrentClubId))
        {
            player.RenewedForSeason=w.SeasonNumber;
            player.ContractRoundsRemaining=ClassicEconomyEngine.SeasonContractRounds(w);
            player.NegotiatedContractProtected=true;
        }
        return(w,a,b,w.Players.Last(p=>p.ClubId==a.CurrentClubId));
    }

    [Fact]
    public async Task AiCreatedAuctionIsSharedAcrossClientsAndSurvivesRestartThroughHumanWinningBid()
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-ai-auction-{Guid.NewGuid():N}.db");
        try
        {
            var (world, _, bidder, _) = Seed();
            var seller = world.Clubs[2];
            var interested = world.Clubs[3];
            seller.HumanCoachId = null;
            interested.HumanCoachId = null;
            world.AiManagers.Single(m => m.Id == seller.AiManagerId).PreferredFormations = [new("4-4-2", 100)];
            world.AiManagers.Single(m => m.Id == interested.AiManagerId).PreferredFormations = [new("4-4-2", 100)];
            var attackers = world.Players.Where(p => p.ClubId == interested.Id && p.Position == PlayerPosition.A).ToList();
            foreach (var player in attackers.Skip(1)) player.ClubId = null;
            foreach (var player in world.Players.Where(p => p.ClubId == seller.Id))
            {
                player.RenewedForSeason = null;
                player.Strength = 38;
                player.IsStar = false;
                player.SalaryPerRound = 50_000m;
            }
            for (int i = 0; i < 3; i++)
                world.Players.Add(new Player { Name = $"Reserva {i}", ClubId = seller.Id,
                    Position = PlayerPosition.A, Strength = 30 - i, ContractRoundsRemaining = 10, SalaryPerRound = 500m });
            interested.CashBalance = 50_000_000m;
            seller.CashBalance = -1_000_000m;
            world.Clubs.Single(c => c.Id == bidder.CurrentClubId).CashBalance = 100_000_000m;

            // Use the real room clock command at the last minute of R7 to publish the auction.
            world.RoundState = RoundState.RUNNING_SECOND_HALF;
            world.CurrentMatchMinute = 89;
            foreach (var match in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world))
            {
                match.CurrentMinute = 89;
                match.LastSimulatedMinute = 89;
            }
            var auctionRound = new TaskCompletionSource<LiveRoundSnapshotDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            AuctionItem auction;
            decimal winningBid;
            string marketPath = $"/api/worlds/{world.Id}/market";
            await using (var factory = Factory(file))
            {
                using var clientA = factory.CreateClient();
                using var clientB = factory.CreateClient();
                var repo = factory.Services.GetRequiredService<IWorldRepository>();
                await repo.SaveWorldAsync(world);
                await using var hub = new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",
                    o => o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()).Build();
                hub.On<LiveRoundSnapshotDto>("ClockTick", state =>
                {
                    if (state.RoundState == RoundState.RESULTS) auctionRound.TrySetResult(state);
                });
                await hub.StartAsync();
                await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", world.Id, bidder.Id);
                (await clientA.PostAsync($"/api/worlds/{world.Id}/tick", null)).EnsureSuccessStatusCode();
                await auctionRound.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var live = (await repo.GetWorldAsync(world.Id))!;
                auction = Assert.Single(live.Auctions, a => a.FormerClubId == seller.Id && a.AiOpenedAfterCompletedRounds != null);
                winningBid = Math.Max(auction.MinimumBid, auction.CurrentBid) + 50_000m;
                (await clientB.PostAsync($"{marketPath}/auction/{auction.Id}/bid?coachId={bidder.Id}&bidAmount={winningBid}&idempotencyKey=human-bid", null))
                    .EnsureSuccessStatusCode();
                var views = await Task.WhenAll(clientA.GetFromJsonAsync<JsonElement>(marketPath),
                    clientB.GetFromJsonAsync<JsonElement>(marketPath));
                foreach (var view in views)
                {
                    var visible = Assert.Single(view.GetProperty("auctions").EnumerateArray(),
                        a => a.GetProperty("auctionId").GetGuid() == auction.Id);
                    Assert.Equal(seller.Id, visible.GetProperty("sellerClubId").GetGuid());
                    Assert.Equal(winningBid, visible.GetProperty("currentBid").GetDecimal());
                    Assert.Equal(bidder.CurrentClubId, visible.GetProperty("highestBidderClubId").GetGuid());
                    Assert.DoesNotContain("privateAiMaxBids", visible.GetRawText(), StringComparison.OrdinalIgnoreCase);
                }
            }

            EfWorldRepository.ClearCacheForTesting();
            await using (var restarted = Factory(file))
            {
                using var clientA = restarted.CreateClient();
                using var clientB = restarted.CreateClient();
                var repo = restarted.Services.GetRequiredService<IWorldRepository>();
                var loaded = (await repo.GetWorldAsync(world.Id))!;
                var restored = Assert.Single(loaded.Auctions, a => a.Id == auction.Id);
                Assert.Equal(auction.AiOpenedAfterCompletedRounds, restored.AiOpenedAfterCompletedRounds);
                Assert.Equal(auction.ExpiresAtRound, restored.ExpiresAtRound);
                Assert.Equal(winningBid, restored.CurrentBid);
                Assert.Equal(seller.Id, loaded.Players.Single(p => p.Id == restored.PlayerId).ClubId);
                loaded.RoundNumber = restored.ExpiresAtRound;
                loaded.RoundState = RoundState.RESULTS;
                loaded.SettledFinancialRounds.Add($"round_settled:S{loaded.SeasonNumber}:R{loaded.RoundNumber}");
                var loadedSeller = loaded.Clubs.Single(c => c.Id == seller.Id);
                decimal sellerCash = loadedSeller.CashBalance;
                MarketAndEconomyEngine.SettleAuction(loaded, restored);
                MarketAndEconomyEngine.SettleAuction(loaded, restored);
                Assert.Equal("SOLD", restored.ClosureReason);
                Assert.Equal(bidder.CurrentClubId, loaded.Players.Single(p => p.Id == restored.PlayerId).ClubId);
                Assert.Equal(sellerCash + winningBid, loadedSeller.CashBalance);
                Assert.Single(loaded.Transactions, t => t.Category == FinancialTransactionCategory.AuctionSale
                    && t.PlayerId == restored.PlayerId && t.ClubId == seller.Id && t.Amount == winningBid);
                await repo.SaveWorldAsync(loaded);
                var views = await Task.WhenAll(clientA.GetFromJsonAsync<JsonElement>(marketPath),
                    clientB.GetFromJsonAsync<JsonElement>(marketPath));
                foreach (var view in views)
                {
                    var visible = Assert.Single(view.GetProperty("auctions").EnumerateArray(),
                        a => a.GetProperty("auctionId").GetGuid() == restored.Id);
                    Assert.True(visible.GetProperty("isClosed").GetBoolean());
                    Assert.Equal("SOLD", visible.GetProperty("closureReason").GetString());
                    Assert.Equal(bidder.CurrentClubId, visible.GetProperty("highestBidderClubId").GetGuid());
                }
            }
        }
        finally
        {
            EfWorldRepository.ClearCacheForTesting();
            if (File.Exists(file)) File.Delete(file);
        }
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
                else Assert.True((await ApiAndSignalREndToEndTests.TickWithAutomaticPenaltyChoices(client,w.Id,125)).IsSuccessStatusCode);
            }
            Assert.Equal(RoundState.RESULTS,w.RoundState);Assert.Equal(round==8,a.IsClosed);
            if(round==7){
                Assert.Equal(owner.CurrentClubId,p.ClubId);
                await ApiAndSignalREndToEndTests.ResolveHumanSalaryRequestsAsync(repo,w.Id,
                    new[]{owner,bidder}.ToDictionary(h=>h.Id,_=>client));
                var advance=await client.PostAsync($"/api/worlds/{w.Id}/next-round",null);
                Assert.True(advance.IsSuccessStatusCode,await advance.Content.ReadAsStringAsync());Assert.False(a.IsClosed);
            }
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
