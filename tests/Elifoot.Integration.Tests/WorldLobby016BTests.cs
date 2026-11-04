using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

public class WorldLobby016BTests
{
    private static WebApplicationFactory<Program> Factory() {
        string file=Path.Combine(Path.GetTempPath(),"elifoot-world-lobby-016b-"+Guid.NewGuid()+".db");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b=> {
            b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider","InMemory");b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
            b.UseSetting("Catalog013A:Enabled","true");b.UseSetting("Catalog013A:DatabaseProvider","Sqlite");b.UseSetting("ConnectionStrings:Catalog013A","Data Source="+file);
            b.UseSetting("Catalog013A:LoadFixtures","false");b.UseSetting("Catalog016A:Enabled","true");b.UseSetting("Catalog016B:Enabled","true");
        });
    }
    private static HttpClient Actor(WebApplicationFactory<Program> f,Guid id) {
        var c=f.CreateClient();c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","localdev-"+id.ToString("N"));return c;
    }
    private static HubConnection Hub(WebApplicationFactory<Program> f)=>new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",o=>{
        o.HttpMessageHandlerFactory=_=>f.Server.CreateHandler();o.Transports=HttpTransportType.LongPolling;
    }).Build();
    private static async Task<GameWorld> Seed(WebApplicationFactory<Program> f,Guid host,int n) {
        var world=WorldSeedFactory.CreateSeededWorld("Fixture lobby 016B",1998,128,n);world.InitialLobby=new(){HostCoachId=host};
        await f.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(world);return world;
    }
    private static async Task Join(HttpClient c,Guid world,Guid actor,bool unemployed=true)=>
        (await c.PostAsync($"/api/worlds/{world}/join?coachId={actor}&nickname=Fixture&deviceId={actor}&joinUnemployed={unemployed}",null)).EnsureSuccessStatusCode();
    private static async Task<WorldLobbyView016B> View(HttpClient c,Guid id)=>
        (await c.GetFromJsonAsync<WorldLobbyView016B>($"/api/world-lobby-016b/{id}"))!;
    private static async Task<WorldLobbyView016B> Command(HttpClient c,Guid id,string command)=>
        (await (await c.PostAsJsonAsync($"/api/world-lobby-016b/{id}/{command}",new LobbyCommand016B("fixture-"+command))).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<WorldLobbyView016B>())!;

    [Theory][InlineData(1)][InlineData(2)][InlineData(4)][InlineData(6)][InlineData(8)]
    public async Task Capacity_AllDraw_ConnectedHostStartAndReadyAreAuthoritative(int capacity) {
        await using var f=Factory();var ids=Enumerable.Range(0,capacity).Select(_=>Guid.NewGuid()).ToList();
        var world=await Seed(f,ids[0],capacity);var hubs=new List<HubConnection>();var clients=new List<HttpClient>();
        try {
            for(int i=0;i<capacity;i++) {
                var c=Actor(f,ids[i]);clients.Add(c);await Join(c,world.Id,ids[i]);
                (await c.PostAsJsonAsync($"/api/world-lobby-016b/{world.Id}/choice",new LobbyChoice016B(InitialCareerChoice016B.FOURTH_DIVISION_DRAW))).EnsureSuccessStatusCode();
                var hub=Hub(f);hubs.Add(hub);await hub.StartAsync();await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom",world.Id,ids[i]);
                if(i+1<capacity)Assert.False((await View(c,world.Id)).CanDraw);
            }
            var view=await View(clients[0],world.Id);Assert.True(view.CanDraw);Assert.False(view.CanStart);Assert.Equal(capacity,view.Connected);
            var before=await clients[0].PostAsJsonAsync($"/api/worlds/{world.Id}/ready-unemployed",new UnemployedReadyRequest(world.Id,ids[0],"before-start",world.RoundVersion));
            Assert.Equal(HttpStatusCode.BadRequest,before.StatusCode);
            await hubs[^1].StopAsync();
            // Long-polling transport closes before the server finishes OnDisconnectedAsync.
            for(int retry=0;retry<100&&(await View(clients[0],world.Id)).Connected==capacity;retry++)await Task.Delay(25);
            Assert.False((await View(clients[0],world.Id)).CanDraw);
            await hubs[^1].StartAsync();await hubs[^1].InvokeAsync<LiveRoundSnapshotDto>("JoinRoom",world.Id,ids[^1]);
            if(capacity>1)Assert.Equal(HttpStatusCode.Forbidden,(await clients[1].PostAsJsonAsync($"/api/world-lobby-016b/{world.Id}/draw",new LobbyCommand016B("not-host"))).StatusCode);
            var draws=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Command(clients[0],world.Id,"draw")));
            Assert.All(draws,d=>Assert.True(d.DrawCompleted));
            var stored=await f.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id);
            Assert.Equal(capacity,stored!.InitialLobby!.DrawAssignments.Values.Distinct().Count());
            Assert.All(stored.HumanCoaches,h=>Assert.Equal(DivisionTier.QuartaDivisao,stored.Clubs.Single(c=>c.Id==h.CurrentClubId).DivisionTier));
            Assert.Equal(RoundState.PREPARING,stored.RoundState);Assert.Equal(0,stored.CurrentMatchMinute);
            var original=stored.InitialLobby.DrawAssignments.ToDictionary(x=>x.Key,x=>x.Value);
            var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(stored,CatalogImportCodec.Json),CatalogImportCodec.Json)!;
            await f.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(restored);
            await Command(clients[0],world.Id,"draw");
            Assert.Equal(original.OrderBy(x=>x.Key),restored.InitialLobby!.DrawAssignments.OrderBy(x=>x.Key));
            var start=await Command(clients[0],world.Id,"start");Assert.Equal(WorldLobbyPhase016B.IN_GAME,start.State);
            Assert.Equal(start.Version,(await Command(clients[0],world.Id,"start")).Version);
            for(int i=0;i<capacity;i++) {
                stored=(await f.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id))!;
                var club=stored.Clubs.Single(c=>c.HumanCoachId==ids[i]);
                var response=await clients[i].PostAsJsonAsync($"/api/worlds/{world.Id}/tactics",new SubmitTacticsRequest(world.Id,ids[i],club.PreferredFormation,club.PreferredStarterIds,club.PreferredBenchIds,"ready-"+i,stored.RoundVersion));
                response.EnsureSuccessStatusCode();
                if(i+1<capacity)Assert.Equal(RoundState.PREPARING,(await f.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id))!.RoundState);
            }
            Assert.NotEqual(RoundState.PREPARING,(await f.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id))!.RoundState);
        } finally {foreach(var h in hubs)await h.DisposeAsync();foreach(var c in clients)c.Dispose();}
    }
    [Fact] public async Task MixedChoicesPreserveManualAndUnemployedAndRejectClubCollision() {
        await using var f=Factory();var ids=Enumerable.Range(0,4).Select(_=>Guid.NewGuid()).ToList();var w=await Seed(f,ids[0],4);
        var clients=ids.Select(id=>Actor(f,id)).ToList();var registry=f.Services.GetRequiredService<RoomConnectionRegistry>();
        for(int i=0;i<4;i++){await Join(clients[i],w.Id,ids[i]);registry.Bind("fixture-"+i,w.Id,ids[i]);}
        var manual=w.Clubs.First(c=>c.DivisionTier==DivisionTier.PrimeiraDivisao);
        (await clients[0].PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/choice",new LobbyChoice016B(InitialCareerChoice016B.MANUAL,manual.Id))).EnsureSuccessStatusCode();
        var collision=await clients[1].PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/choice",new LobbyChoice016B(InitialCareerChoice016B.MANUAL,manual.Id));Assert.Equal(HttpStatusCode.BadRequest,collision.StatusCode);
        for(int i=2;i<4;i++)(await clients[i].PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/choice",new LobbyChoice016B(InitialCareerChoice016B.FOURTH_DIVISION_DRAW))).EnsureSuccessStatusCode();
        var draw=await Command(clients[0],w.Id,"draw");Assert.Equal(manual.Id,draw.Participants.Single(p=>p.CoachId==ids[0]).ClubId);
        Assert.Null(draw.Participants.Single(p=>p.CoachId==ids[1]).ClubId);Assert.Equal(2,draw.Participants.Where(p=>p.Choice==InitialCareerChoice016B.FOURTH_DIVISION_DRAW).Select(p=>p.ClubId).Distinct().Count());
        foreach(var c in clients)c.Dispose();
    }
    [Fact] public async Task MissingSeatsAndHostReservationCannotBeBypassedAndCapacityIsExplicit() {
        await using var f=Factory();var host=Guid.NewGuid();var w=await Seed(f,host,2);using var c=Actor(f,host);await Join(c,w.Id,host);
        f.Services.GetRequiredService<RoomConnectionRegistry>().Bind("fixture-host",w.Id,host);
        Assert.False((await View(c,w.Id)).CanStart);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/start",new LobbyCommand016B("incomplete"))).StatusCode);
        (await c.PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/capacity",new LobbyCapacity016B(1,"reduce-explicitly"))).EnsureSuccessStatusCode();
        Assert.True((await View(c,w.Id)).CanStart);await Command(c,w.Id,"start");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync($"/api/world-lobby-016b/{w.Id}/capacity",new LobbyCapacity016B(2,"after-start"))).StatusCode);
        var next=await Seed(f,host,1);var stranger=Guid.NewGuid();using var outsider=Actor(f,stranger);
        Assert.Equal(HttpStatusCode.BadRequest,(await outsider.PostAsync($"/api/worlds/{next.Id}/join?coachId={stranger}&nickname=Fixture&deviceId=other",null)).StatusCode);
    }
}
