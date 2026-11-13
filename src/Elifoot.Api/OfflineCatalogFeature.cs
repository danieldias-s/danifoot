using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;

namespace Elifoot.Api;

internal static class OfflineCatalogFeature
{
    public static string Root(IConfiguration config, IHostEnvironment environment)
    {
        var directory = new DirectoryInfo(environment.ContentRootPath);
        while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName, "Elifoot.slnx"))) directory = directory.Parent;
        return Path.GetFullPath(config["CatalogOffline:Root"] ?? "catalogos",
            File.Exists(Path.Combine(directory.FullName, "Elifoot.slnx")) ? directory.FullName : environment.ContentRootPath);
    }
    public static async Task<bool> TryRun(string[] args)
    {
        if (args.FirstOrDefault() is not ("offline-catalog-validate" or "offline-catalog-list")) return false;
        var builder = WebApplication.CreateBuilder(args[1..]); builder.Logging.ClearProviders();
        var report = await new OfflineCatalogLoader(Root(builder.Configuration, builder.Environment)).LoadAsync();
        Console.WriteLine(JsonSerializer.Serialize(report, OfflineCatalogLoader.Json));
        if (!report.Valid) Environment.ExitCode = 2;
        return true; // No app, database, HTTP client, migration, or host is constructed.
    }
    public static void MapOffline(this RouteGroupBuilder parent)
    {
        var group = parent.MapGroup("/offline");
        Guid Owner(HttpContext c) => (Guid)c.Items["creatorOwner"]!;
        group.MapGet("/catalogs", async (OfflineWorldCreatorService service, CancellationToken ct) => Results.Ok(await service.Catalogs(ct)));
        group.MapGet("/catalogs/summary", async (FootballGender gender, OfflineWorldCreatorService service, CancellationToken ct) =>
        {
            var report = await service.Catalogs(ct);
            var rootValid = report.Issues.All(i => !i.IsError);
            return Results.Ok(report.Catalogs
                .Where(c => c.Manifest is { Country: not null } manifest && manifest.Gender == gender && c.Clubs.Count > 0)
                .Select(c => new
                {
                    c.Manifest!.CatalogId,
                    c.Manifest.Country,
                    c.Manifest.Year,
                    ClubCount = c.Clubs.Count,
                    Valid = c.Valid && rootValid,
                    Ready = c.Ready && rootValid
                }));
        });
        group.MapPost("/clubs", async (OfflineWorldRequest request, OfflineWorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.Select(request.CatalogIds, ct)));
        group.MapPost("/planning-clubs", async (OfflineWorldRequest request, OfflineWorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.SelectForPlanning(request.CatalogIds, ct)));
        group.MapPost("/planning-preview", async (OfflineWorldRequest request, OfflineWorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewForPlanning(request, ct)));
        group.MapPost("/preview", async (OfflineWorldRequest request, HttpContext c, OfflineWorldCreatorService service, CancellationToken ct) =>
            Results.Ok(await service.Preview(Owner(c), request, ct)));
        group.MapPost("/confirm/{id:guid}", async (Guid id, HttpContext c, OfflineWorldCreatorService service, CancellationToken ct) =>
        {
            var world = await service.Confirm(Owner(c), id, ct);
            return Results.Ok(new { world.Id, world.Name, world.InviteCode, world.Gender });
        });
    }
}
