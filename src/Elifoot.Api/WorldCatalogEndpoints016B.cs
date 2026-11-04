using System.Net;
using System.Security.Cryptography;
using System.Text;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.World016B;
using Microsoft.EntityFrameworkCore;
namespace Elifoot.Api;

public static class WorldCatalogEndpoints016B
{
    public static void AddWorldCatalog016B(this WebApplicationBuilder builder) {
        if(!builder.Configuration.GetValue<bool>("Catalog016B:Enabled"))return;
        if(builder.Environment.IsProduction() || !builder.Configuration.GetValue<bool>("Catalog013A:Enabled") || !builder.Configuration.GetValue<bool>("Catalog016A:Enabled"))
            throw new InvalidOperationException("Catálogo mundial 016B requer DEV e os catálogos 013A/016A habilitados.");
        var options=builder.Configuration.GetSection("Catalog016B").Get<WorldCatalogOptions016B>()??new();
        if(options.CountryCatalogRefreshDays!=7 || options.EliteFullSquadRankCutoff is <0 or >1000)throw new InvalidOperationException("Atualização por país deve respeitar sete dias e o limite de elite válido.");
        builder.Services.AddSingleton(options);builder.Services.AddSingleton<WorldCatalogService016B>();builder.Services.AddHostedService<CountryCatalogWorker016B>();
    }
    public static async Task InitializeWorldCatalog016B(this WebApplication app) {
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await WorldCatalogSchema016B.Ensure(db);
        foreach(var c in WorldCatalogService016B.Countries)
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogCountry\" (\"Code\",\"Name\") VALUES ({c.Key},{c.Value.Local}) ON CONFLICT (\"Code\") DO NOTHING");
    }
    private static RouteGroupBuilder AuthGroup(WebApplication app,string path) {
        var group=app.MapGroup(path);
        group.AddEndpointFilter(async(context,next)=> {
            if(app.Environment.IsProduction())return Results.StatusCode(503);
            if(!AccountLogin.TryOwner(context.HttpContext,app.Configuration,out var owner))return Results.Unauthorized();
            context.HttpContext.Items["owner016b"]=owner;
            try{return await next(context);}
            catch(ArgumentException e){return Results.BadRequest(new{error=e.Message});}
            catch(KeyNotFoundException e){return Results.NotFound(new{error=e.Message});}
            catch(UnauthorizedAccessException){return Results.Json(new{error="Somente o criador ou participante autorizado pode executar esta ação."},statusCode:403);}
            catch(CatalogBusyException e){return Results.Conflict(new{error=e.Message});}
        });return group;
    }
    private static Guid Owner(HttpContext c)=>(Guid)c.Items["owner016b"]!;
    public static void MapWorldCatalog016B(this WebApplication app) {
        var group=AuthGroup(app,"/api/world-catalog-016b");
        group.MapGet("/countries",async(FootballGender gender,string season,WorldCatalogService016B service,CancellationToken ct)=>
            Results.Ok(new{Countries=await service.Directory(ct),States=await service.Status(gender,season,ct)}));
        group.MapPost("/refresh",async(CountryRefreshRequest016B request,WorldCatalogService016B service,CancellationToken ct)=>
            Results.Ok(await service.Queue(request.CountryCode,request.Gender,request.Season,ct)));
        group.MapGet("/report",async(string country,FootballGender gender,string season,WorldCatalogService016B service,CancellationToken ct)=>
            Results.Ok(await service.Report(country,gender,season,ct)));
        var admin=group.MapGroup("/admin");
        admin.AddEndpointFilter(async(context,next)=> {
            var c=context.HttpContext;string? expected=app.Configuration["Catalog016A:AdminToken"];string supplied=c.Request.Headers["X-Catalog-Admin-Token"].ToString();
            if(string.IsNullOrWhiteSpace(expected)||string.IsNullOrWhiteSpace(supplied)||!IPAddress.IsLoopback(c.Connection.RemoteIpAddress??IPAddress.None)||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))return Results.NotFound();
            return await next(context);
        });
        admin.MapPost("/review-league",async(ReviewCountryLeague016B request,WorldCatalogService016B service,CancellationToken ct)=>{await service.ReviewLeague(request,ct);return Results.Ok(new{status="REVIEWED"});});
        admin.MapPost("/club-power",async(ManualClubPower016B command,WorldCatalogService016B service,CancellationToken ct)=>{await service.ManualPower(command,ct);return Results.Ok(new{status="REVIEWED"});});
        var lobby=AuthGroup(app,"/api/world-lobby-016b");
        lobby.MapGet("/{id:guid}",async(Guid id,MultiplayerRoundOrchestrator service,CancellationToken ct)=>Results.Ok(await service.InitialLobbyView(id,ct)));
        lobby.MapPost("/{id:guid}/choice",async(Guid id,LobbyChoice016B choice,HttpContext c,MultiplayerRoundOrchestrator service,CancellationToken ct)=>Results.Ok(await service.ChooseInitialCareer(id,Owner(c),choice,ct)));
        lobby.MapPost("/{id:guid}/draw",async(Guid id,LobbyCommand016B command,HttpContext c,MultiplayerRoundOrchestrator service,CancellationToken ct)=>Results.Ok(await service.DrawInitialClubs(id,Owner(c),command.IdempotencyKey,ct)));
        lobby.MapPost("/{id:guid}/start",async(Guid id,LobbyCommand016B command,HttpContext c,MultiplayerRoundOrchestrator service,CancellationToken ct)=>Results.Ok(await service.StartInitialChampionship(id,Owner(c),command.IdempotencyKey,ct)));
        lobby.MapPost("/{id:guid}/capacity",async(Guid id,LobbyCapacity016B command,HttpContext c,MultiplayerRoundOrchestrator service,CancellationToken ct)=>Results.Ok(await service.ConfigureInitialCapacity(id,Owner(c),command,ct)));
    }
}
public sealed class CountryCatalogWorker016B(WorldCatalogService016B service,ILogger<CountryCatalogWorker016B> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            try{if(await service.RunOneStep(ct))continue;}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e){log.LogWarning("Country catalogue worker recovered from {ErrorType}",e.GetType().Name);}
            try{await Task.Delay(1000,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
        }
    }
}
