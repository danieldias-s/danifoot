using Elifoot.Domain;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Api;

public static class WorldCreatorEndpoints
{
    public static bool AddWorldCreator013A(this WebApplicationBuilder builder)
    {
        var c = builder.Configuration.GetSection("Catalog013A");
        if (!c.GetValue<bool>("Enabled")) return false;
        string connection = builder.Configuration.GetConnectionString("Catalog013A")
            ?? throw new InvalidOperationException("Configure um banco exclusivo ConnectionStrings:Catalog013A.");
        string isolatedSuffix = builder.Configuration.GetValue<bool>("Catalog013B:Enabled") ? "013b" : "013a";
        string provider = c["DatabaseProvider"] ?? "PostgreSQL";
        if (provider != "Sqlite" && !new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.Contains(isolatedSuffix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A 013A exige nome de banco exclusivo contendo 013a.");
        if (provider is not ("Sqlite" or "PostgreSQL")) throw new InvalidOperationException("Provedor de banco do catálogo inválido.");
        if (provider == "PostgreSQL" && builder.Configuration["Elifoot:DatabaseProvider"] is not ("InMemory" or "Sqlite"))
        {
            var worldConnection = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Configure explicitamente o banco de mundos exclusivo da 013A.");
            var world = new Npgsql.NpgsqlConnectionStringBuilder(worldConnection);
            var catalog = new Npgsql.NpgsqlConnectionStringBuilder(connection);
            if (world.Database?.Contains(isolatedSuffix, StringComparison.OrdinalIgnoreCase) != true ||
                world.Host == catalog.Host && world.Port == catalog.Port && world.Database == catalog.Database)
                throw new InvalidOperationException("Use bancos de mundos e catálogo separados, ambos exclusivos da 013A.");
        }
        builder.Services.AddDbContext<CatalogDbContext>(o => { if (provider == "Sqlite") o.UseSqlite(connection); else o.UseNpgsql(connection); });
        var options = new CatalogSyncOptions();
        options.Timeout = TimeSpan.FromSeconds(Math.Clamp(c.GetValue("TimeoutSeconds", 2), 1, 5));
        foreach (var type in Enum.GetValues<CatalogDataType>())
            options.Frequencies[type] = TimeSpan.FromMinutes(Math.Max(1, c.GetValue($"FrequencyMinutes:{type}", options.Frequencies[type].TotalMinutes)));
        builder.Services.AddSingleton(options);
        foreach (var entry in c.GetSection("Sources").GetChildren())
        {
            var key = entry.GetSection("Scope").Get<CatalogScopeKey>() ?? throw new InvalidOperationException("Escopo ausente.");
            var endpoint = new Uri(entry["Endpoint"]!);
            if (endpoint.Scheme != "https") throw new InvalidOperationException("Fontes externas exigem HTTPS.");
            builder.Services.AddSingleton<ICatalogSource>(new AuthorizedJsonCatalogSource(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, key,
                endpoint, entry.GetValue<bool>("StorageAuthorized"), entry.GetValue<bool>("CommercialUseAuthorized"),
                entry["LicenseReference"] ?? "", entry["BearerToken"]));
        }
        builder.AddRealDataProviders013B();
        builder.Services.AddSingleton<CatalogSyncService>();
        builder.Services.AddSingleton<WorldCreatorService>();
        builder.Services.AddSingleton<WorldAssemblyService>();
        builder.Services.AddSingleton<ClubColorService>();
        if(builder.Configuration.GetValue<bool>("Catalog014C:OnlineColorResearch")) {
            var client=new HttpClient{BaseAddress=new Uri("https://www.wikidata.org/"),Timeout=TimeSpan.FromSeconds(12)};
            builder.Services.AddSingleton<IClubColorSource>(new Elifoot.Infrastructure.Catalog.Providers.WikidataClubColorSource(new(client,Elifoot.Infrastructure.Catalog.Providers.FootballProviderKind.Wikidata,null)));
        }
        if(builder.Configuration["Catalog014C:ColorFile"] is string colorFile)
            builder.Services.AddSingleton<IClubColorSource>(new Elifoot.Infrastructure.Catalog.Providers.SavedClubColorSource(Path.GetFullPath(colorFile)));
        return true;
    }
    public static async Task InitializeWorldCreator013A(this WebApplication app)
    {
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.EnsureCreatedAsync();
        await WorldAssemblyService.EnsureSchema(db);
        await ClubColorService.EnsureSchema(db);
        foreach (var country in WorldCreatorService.PriorityCountries)
            if (!await db.Countries.AnyAsync(c => c.Code == country.Code)) db.Countries.Add(new() { Code = country.Code, Name = country.Name });
        await db.SaveChangesAsync();
        if (app.Configuration.GetValue<bool>("Catalog013A:LoadFixtures"))
            foreach (var package in CatalogFixtureFactory.Packages()) await app.Services.GetRequiredService<CatalogSyncService>().ImportAsync(package);
    }
    public static void MapWorldCreator013A(this WebApplication app)
    {
        var group = app.MapGroup("/api/world-creator");
        // Follows the existing LocalDev identity contract. Production identity integration is deliberately gated.
        group.AddEndpointFilter(async (context, next) =>
        {
            if (!AccountLogin.TryOwner(context.HttpContext, app.Configuration, out var owner))
                return Results.Unauthorized();
            context.HttpContext.Items["creatorOwner"] = owner;
            try { return await next(context); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (AssemblyConflictException e) { return Results.Conflict(new { error = e.Message }); }
            catch (CatalogBusyException e) { return Results.Conflict(new { error = e.Message }); }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(new { error = "Conteúdo JSON inválido." }); }
            catch (FormatException) { return Results.BadRequest(new { error = "Formato JSON/CSV inválido." }); }
        });
        Guid Owner(HttpContext c) => (Guid)c.Items["creatorOwner"]!;
        group.MapGet("/seasons", async (WorldCreatorService service, CancellationToken ct) => Results.Ok(await service.Seasons(ct)));
        group.MapGet("/countries", async (string season, FootballGender? gender, WorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.Countries(season, gender, ct)));
        group.MapPost("/coverage", async (WorldCreatorRequest request, WorldCreatorService service, CancellationToken ct) => Results.Ok(await service.Coverage(request.Countries, request.Gender, request.Season, ct)));
        group.MapPost("/catalog", async (WorldCreatorRequest request, WorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.SelectForWorld(request, ct)));
        group.MapGet("/last-configuration", async (HttpContext c, WorldCreatorService service, CancellationToken ct) => Results.Ok(await service.LastConfiguration(Owner(c), ct)));
        group.MapPost("/preview", async (WorldCreatorRequest request, HttpContext c, WorldCreatorService service, CancellationToken ct) => Results.Ok(await service.Preview(Owner(c), request, ct)));
        group.MapPost("/confirm/{draftId:guid}", async (Guid draftId, HttpContext c, WorldCreatorService service, CancellationToken ct) =>
        {
            var world = await service.Confirm(Owner(c), draftId, ct);
            return Results.Ok(new { world.Id, world.Name, world.InviteCode, world.Gender, world.CatalogOrigin });
        });
        group.MapPost("/assemblies", async (CreateWorldAssemblyRequest request, HttpContext c, [Microsoft.AspNetCore.Mvc.FromServices] WorldAssemblyService service, CancellationToken ct) => Results.Ok(await service.Create(Owner(c), request, ct)));
        group.MapGet("/assemblies", async (HttpContext c, [Microsoft.AspNetCore.Mvc.FromServices] WorldAssemblyService service, CancellationToken ct) => Results.Ok(await service.List(Owner(c), ct)));
        group.MapGet("/assemblies/{id:guid}", async (Guid id, HttpContext c, [Microsoft.AspNetCore.Mvc.FromServices] WorldAssemblyService service, CancellationToken ct) => Results.Ok(await service.Get(Owner(c), id, ct)));
        group.MapPost("/assemblies/{id:guid}", async (Guid id, UpdateWorldAssemblyRequest request, HttpContext c, [Microsoft.AspNetCore.Mvc.FromServices] WorldAssemblyService service, CancellationToken ct) => Results.Ok(await service.Update(Owner(c), id, request, ct)));
        group.MapPost("/assemblies/{id:guid}/confirm", async (Guid id, ConfirmWorldAssemblyRequest request, HttpContext c, [Microsoft.AspNetCore.Mvc.FromServices] WorldAssemblyService service, CancellationToken ct) =>
        {
            var world = await service.Confirm(Owner(c), id, request, ct); return Results.Ok(new { world.Id, world.Name, world.InviteCode, world.CustomAssemblyOrigin });
        });
        group.MapPost("/assemblies/players/parse-csv", (AssemblyCsvRequest request) =>
        {
            var result = WorldEditorAndPackageService.ParsePlayersFromCsv(request.Csv);
            return Results.Ok(new { players = result.Players, errors = result.ParseErrors });
        });
        group.MapPost("/assemblies/players/export-csv", (AssemblyPackageRequest request) =>
        {
            var preview = new GameWorld();
            foreach (var p in request.Package.Players) preview.Players.Add(new Player
            {
                Id = p.Id,
                ClubId = p.ClubId,
                Name = p.Name,
                Position = p.Position,
                Strength = p.Strength,
                Behavior = p.Behavior,
                IsStar = p.IsStar,
                SalaryPerRound = p.SalaryPerRound,
                ContractRoundsRemaining = p.ContractRoundsRemaining,
                MarketPrice = p.MarketPrice,
                PhotoUrl = p.PhotoUrl,
                PhotoLicense = p.PhotoLicense,
                PhotoCredit = p.PhotoCredit,
                NegotiatedContractProtected = p.NegotiatedContractProtected ?? true,
                CatalogIdentity = p.CatalogIdentity
            });
            return Results.Ok(new { csv = WorldEditorAndPackageService.ExportPlayersToCsv(preview) });
        });
        group.MapGet("/appearance/{worldId:guid}/{clubId:guid}",async(Guid worldId,Guid clubId,HttpContext c,[Microsoft.AspNetCore.Mvc.FromServices] ClubColorService service,CancellationToken ct)=>Results.Ok(await service.Appearance(Owner(c),worldId,clubId,ct:ct)));
        group.MapPost("/appearance/{worldId:guid}/{clubId:guid}",async(Guid worldId,Guid clubId,SaveAppearanceRequest command,HttpContext c,[Microsoft.AspNetCore.Mvc.FromServices] ClubColorService service,CancellationToken ct)=>Results.Ok(await service.Appearance(Owner(c),worldId,clubId,command,ct)));
        var admin = app.MapGroup("/api/catalog-013a");
        admin.AddEndpointFilter(async (context, next) =>
        {
            string? token = app.Configuration["Catalog013A:ImportToken"];
            if (string.IsNullOrWhiteSpace(token) || context.HttpContext.Request.Headers["X-Catalog-Import-Token"].ToString() != token ||
                !System.Net.IPAddress.IsLoopback(context.HttpContext.Connection.RemoteIpAddress ?? System.Net.IPAddress.None)) return Results.NotFound();
            try { return await next(context); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (AssemblyConflictException e) { return Results.Conflict(new { error = e.Message }); }
            catch (CatalogBusyException e) { return Results.Conflict(new { error = e.Message }); }
        });
        admin.MapGet("/identities", async (string country, FootballGender gender, [Microsoft.AspNetCore.Mvc.FromServices] CatalogDbContext db, CancellationToken ct) => Results.Ok(await db.Set<CatalogClubIdentity>().Where(i => i.CountryCode == country && i.Gender == gender).ToListAsync(ct)));
        admin.MapGet("/identity-conflicts", async ([Microsoft.AspNetCore.Mvc.FromServices] CatalogDbContext db, CancellationToken ct) => Results.Ok(await db.Set<CatalogIdentityConflict>().Where(c => c.Status == "Open").ToListAsync(ct)));
        admin.MapPost("/map-club", async (CatalogIdentityMappingRequest request, [Microsoft.AspNetCore.Mvc.FromServices] CatalogDbContext db, CancellationToken ct) => { await CatalogIdentityService.MapAsync(db, request, ct); return Results.Ok(new { status = "Reviewed" }); });
        admin.MapPost("/club-colors/import",async(ImportClubColorRequest request,[Microsoft.AspNetCore.Mvc.FromServices] ClubColorService service,CancellationToken ct)=>{await service.Import(request,ct);return Results.Ok(new{status="Imported"});});
        admin.MapPost("/club-colors/refresh",async(WorldCreatorRequest request,WorldCreatorService creator,[Microsoft.AspNetCore.Mvc.FromServices] ClubColorService service,CancellationToken ct)=>{
            var selected=await creator.Select(request.Countries,request.Gender,request.Season,true,ct);return Results.Ok(await service.Refresh(selected.Clubs,ct:ct));});
        admin.MapPost("/import", async (CatalogImportPackage package, CatalogSyncService sync, CancellationToken ct) => Results.Ok(new { snapshotId = await sync.ImportAsync(package, ct) }));
        admin.MapPost("/import-csv", async (CatalogCsvRequest request, CatalogSyncService sync, CancellationToken ct) =>
            Results.Ok(new { snapshotId = await sync.ImportAsync(CatalogImportCodec.ReadCsv(request.Manifest, request.Csv), ct) }));
        admin.MapPost("/run-due", async (CatalogSyncService sync, CancellationToken ct) => { await sync.RunDueAsync(ct); return Results.Ok(new { status = "Completed" }); });
    }
}
public record CatalogCsvRequest(CatalogImportPackage Manifest, string Csv);
