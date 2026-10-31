using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.Length >= 2 && args[0] is "014a-old-worlds" or "014a-verify-old-worlds") { await Elifoot.RealDataImporter.Integration014APreservation.Run(args[1], args[0] == "014a-verify-old-worlds", args.Skip(2).ToArray()); return; }

if (args.Length >= 2 && args[0] == "coverage-audit") { await Elifoot.RealDataImporter.CoverageAudit.Run(args[1], args.ElementAtOrDefault(2) ?? "ELIFOOT_013B_CATALOG_PG"); return; }

if (args.Length >= 2 && args[0] == "coverage") { await Elifoot.RealDataImporter.CoverageBundleImporter.Run(args[1], args.ElementAtOrDefault(2) ?? "ELIFOOT_013B_CATALOG_PG"); return; }

if (args.Length < 2 || args[0] is not ("inspect" or "sync")) throw new ArgumentException("Uso: inspect|sync manifest.json [connection-env-name] [--force]");
using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
var providers = JsonSerializer.Deserialize<List<NativeProviderOptions>>(manifest.RootElement.GetProperty("Catalog013B").GetProperty("Providers").GetRawText(),
    new JsonSerializerOptions(CatalogImportCodec.Json) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;
var sources = providers.SelectMany(o => NativeProviderFactory.Create(o)).ToList();
if (args[0] == "inspect")
{
    foreach (var source in sources)
    {
        using var timeout = new CancellationTokenSource(source.FetchTimeout ?? TimeSpan.FromSeconds(10));
        try { var package = await source.FetchAsync(timeout.Token); CatalogSyncService.Validate(package);
            Console.WriteLine(JsonSerializer.Serialize(new { source.Scope, status = "Validated", clubs = package.Clubs.Count, players = package.Players.Count, package.IsFixture, package.RosterReports }, CatalogImportCodec.Json)); }
        catch (ArgumentException e) { Console.WriteLine(JsonSerializer.Serialize(new { source.Scope, status = "Invalid", reason = e.Message }, CatalogImportCodec.Json)); }
        catch (HttpRequestException e) { Console.WriteLine(JsonSerializer.Serialize(new { source.Scope, status = "NetworkOrAuth", code = e.StatusCode?.ToString() }, CatalogImportCodec.Json)); }
        catch (OperationCanceledException) { Console.WriteLine(JsonSerializer.Serialize(new { source.Scope, status = "Timeout" }, CatalogImportCodec.Json)); }
    }
    return;
}
string connection = Environment.GetEnvironmentVariable(args.ElementAtOrDefault(2) ?? "ELIFOOT_013B_CATALOG_PG") ?? throw new ArgumentException("Configure a variável de conexão 013B, sem gravá-la em argumentos/logs.");
var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
if (!Elifoot.RealDataImporter.LocalCatalogTarget.Catalog(parsed))
    throw new ArgumentException("Este runner exige o catálogo DEV isolado 013B ou 014A.");
var services = new ServiceCollection(); services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(connection));
services.AddSingleton(new CatalogSyncOptions()); foreach (var source in sources) services.AddSingleton(source); services.AddSingleton<CatalogSyncService>();
await using var provider = services.BuildServiceProvider();
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>(); await db.Database.EnsureCreatedAsync();
    if (args.Contains("--force")) await db.Scopes.ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttempt, DateTime.UnixEpoch));
}
await provider.GetRequiredService<CatalogSyncService>().RunDueAsync();
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var jobs = await db.SyncJobs.AsNoTracking().OrderBy(j => j.StartedAt).Select(j => new { j.ScopeKey, j.Status, j.Attempts, j.Error }).ToListAsync();
    Console.WriteLine(JsonSerializer.Serialize(jobs, CatalogImportCodec.Json));
}
