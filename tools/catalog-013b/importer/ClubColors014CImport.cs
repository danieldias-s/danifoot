using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.RealDataImporter;
internal static class ClubColors014CImport
{
    public static async Task Run(string file,string output)
    {
        string connection=Environment.GetEnvironmentVariable("ELIFOOT_014C_REAL_CATALOG_PG")??throw new ArgumentException("Configure somente o banco 014C.");
        var parsed=new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if(parsed.Host!="127.0.0.1"||parsed.Port!=55438||parsed.Database!="elifoot_014c_013b_catalog_real")throw new ArgumentException("Importação restrita ao banco próprio 014C.");
        var services=new ServiceCollection();services.AddDbContext<CatalogDbContext>(o=>o.UseNpgsql(connection));
        services.AddSingleton<IWorldRepository,InMemoryWorldRepository>();services.AddSingleton(new CatalogSyncOptions());
        services.AddSingleton<CatalogSyncService>();services.AddSingleton<WorldCreatorService>();services.AddSingleton<ClubColorService>();
        services.AddSingleton<IClubColorSource>(new SavedClubColorSource(Path.GetFullPath(file)));
        await using var provider=services.BuildServiceProvider();
        using(var scope=provider.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.EnsureCreatedAsync();await ClubColorService.EnsureSchema(db);}
        var reports=new List<ClubColorCoverageDto>();
        foreach(var gender in Enum.GetValues<FootballGender>()){
            var selected=await provider.GetRequiredService<WorldCreatorService>().Select(WorldCreatorService.PriorityCountries.Select(c=>c.Code).ToList(),gender,"2026",true);
            reports.AddRange(await provider.GetRequiredService<ClubColorService>().Refresh(selected.Clubs));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{Palettes=reports.Count(c=>c.Palette!=null),Missing=reports.Count(c=>c.Palette==null),Clubs=reports},new JsonSerializerOptions(CatalogImportCodec.Json){WriteIndented=true}));
        Console.WriteLine($"014C color cache: {reports.Count(c=>c.Palette!=null)} palettes; {reports.Count(c=>c.Palette==null)} missing; no gameplay database writes.");
    }
}
