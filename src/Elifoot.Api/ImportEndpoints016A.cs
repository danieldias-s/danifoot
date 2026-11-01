using System.Net;
using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Import016A;
using Microsoft.EntityFrameworkCore;
namespace Elifoot.Api;

public static class ImportEndpoints016A
{
    public static void AddImport016A(this WebApplicationBuilder builder) {
        if(!builder.Configuration.GetValue<bool>("Catalog016A:Enabled"))return;
        if(!builder.Configuration.GetValue<bool>("Catalog013A:Enabled"))throw new InvalidOperationException("016A requer catálogo isolado.");
        if(builder.Environment.IsProduction())throw new InvalidOperationException("016A habilitada somente em DEV.");
        var options=builder.Configuration.GetSection("Catalog016A").Get<Import016AOptions>()??new();
        options.ApiFootballKey=Environment.GetEnvironmentVariable("APIFOOTBALL_API_KEY"); // Never a browser setting or a value in appsettings/Git.
        options.AdminToken=builder.Configuration["Catalog016A:AdminToken"];
        builder.Services.AddSingleton(options);
        builder.Services.AddHttpClient("016A.ApiFootball",c=>{c.BaseAddress=new("https://v3.football.api-sports.io/");c.Timeout=Timeout.InfiniteTimeSpan;});
        builder.Services.AddHttpClient("016A.Wikidata",c=>{c.BaseAddress=new("https://www.wikidata.org/");c.Timeout=Timeout.InfiniteTimeSpan;});
        builder.Services.AddHttpClient("016A.WikidataQuery",c=>{c.BaseAddress=new("https://query.wikidata.org/");c.Timeout=Timeout.InfiniteTimeSpan;});
        builder.Services.AddSingleton(sp=>new ImportHttp016A(sp.GetRequiredService<IServiceScopeFactory>(),name=>sp.GetRequiredService<IHttpClientFactory>().CreateClient(name),options));
        builder.Services.AddSingleton<PilotData016A>();builder.Services.AddSingleton<RealImportService016A>();
    }
    public static async Task InitializeImport016A(this WebApplication app) {
        using var scope=app.Services.CreateScope();await Import016ASchema.Ensure(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
    }
    public static void MapImport016A(this WebApplication app) {
        var group=app.MapGroup("/api/catalog-016a");
        group.AddEndpointFilter(async (context,next)=> {
            var request=context.HttpContext;string? expected=app.Configuration["Catalog016A:AdminToken"];
            string supplied=request.Request.Headers["X-Catalog-Admin-Token"].ToString();
            if(string.IsNullOrWhiteSpace(expected)||string.IsNullOrEmpty(supplied)||!IPAddress.IsLoopback(request.Connection.RemoteIpAddress??IPAddress.None)||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))return Results.NotFound();
            try{return await next(context);}
            catch(ImportProviderException e){return Results.Json(new{error=e.Code},statusCode:503);}
            catch(ArgumentException e){return Results.BadRequest(new{error=e.Message});}
            catch(KeyNotFoundException e){return Results.NotFound(new{error=e.Message});}
            catch(CatalogBusyException e){return Results.Conflict(new{error=e.Message});}
        });
        group.MapGet("/pilot",()=>PilotClubs016A.All);
        group.MapGet("/coverage",async(RealImportService016A service,CancellationToken ct)=>await service.Coverage(ct));
        group.MapPost("/dry-run",async(RealImport016ARequest command,RealImportService016A service,CancellationToken ct)=>await service.DryRun(command,ct));
        group.MapGet("/reports/{id:guid}",async(Guid id,RealImportService016A service,CancellationToken ct)=>await service.Report(id,ct));
        group.MapPost("/apply/{id:guid}",async(Guid id,RealImportService016A service,CancellationToken ct)=>await service.Apply(id,ct));
        group.MapPost("/map-team",async(ConfirmClub016ARequest command,RealImportService016A service,CancellationToken ct)=>{await service.Map(command,ct);return Results.Ok(new{status="REVIEWED"});});
        group.MapPost("/map-existing-club",async(CatalogIdentityMappingRequest command,CatalogDbContext db,CancellationToken ct)=>{await CatalogIdentityService.MapAsync(db,command,ct);return Results.Ok(new{status="REVIEWED"});});
        group.MapGet("/audit",async(CatalogDbContext db,CancellationToken ct)=>await db.Set<Import016AAudit>().AsNoTracking().OrderByDescending(a=>a.At).Take(100).ToListAsync(ct));
        group.MapGet("/account",async(ImportHttp016A http,CancellationToken ct)=> {
            var account=await http.Get("api-football","status",true,ct);
            // Return quota/coverage fields only. The provider response may contain account/person data.
            var root=account.Data.GetProperty("response");
            return Results.Ok(new{subscription=root.TryGetProperty("subscription",out var s)?s:default,requests=root.TryGetProperty("requests",out var r)?r:default});
        });
    }
}
