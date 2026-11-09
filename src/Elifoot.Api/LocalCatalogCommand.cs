using System.Text.Json;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Api;

internal static class LocalCatalogCommand
{
    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (args.FirstOrDefault() is not ("catalog-validate" or "catalog-reindex"))
            return false;

        var builder = WebApplication.CreateBuilder(args[1..]);
        builder.Logging.ClearProviders();
        var root = Root(builder.Configuration, builder.Environment);
        LocalHistoricalCatalogReport result;
        if (args[0] == "catalog-validate")
        {
            result = await LocalHistoricalCatalog.ValidateAsync(root);
        }
        else
        {
            builder.Configuration["Catalog013A:Enabled"] = "true";
            if (!WorldCreatorEndpoints.UsesLocalHistoricalCatalog(builder.Configuration))
                throw new InvalidOperationException("Desative CatalogLocal:UseLegacyProviders antes de reindexar.");

            builder.AddCatalogDatabase();
            builder.Services.AddSingleton<LocalHistoricalCatalog>();
            await using var app = builder.Build();
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.EnsureCreatedAsync();
            await LocalHistoricalCatalog.EnsureSchemaAsync(db);
            result = await app.Services.GetRequiredService<LocalHistoricalCatalog>().ReindexAsync(root);
        }

        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        if (!result.Valid) Environment.ExitCode = 2;
        return true;
    }

    public static string Root(IConfiguration config, IHostEnvironment environment)
    {
        var directory = new DirectoryInfo(environment.ContentRootPath);
        while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName, "Elifoot.slnx")))
            directory = directory.Parent;
        var basePath = File.Exists(Path.Combine(directory.FullName, "Elifoot.slnx"))
            ? directory.FullName
            : environment.ContentRootPath;
        return Path.GetFullPath(config["CatalogLocal:Root"] ?? "catalogo", basePath);
    }
}
