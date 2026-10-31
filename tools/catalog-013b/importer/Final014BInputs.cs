using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.RealDataImporter;

internal static class Final014BInputs
{
    public static async Task Run(string output)
    {
        string catalog=Environment.GetEnvironmentVariable("ELIFOOT_014B_REAL_CATALOG_PG")??throw new ArgumentException("Conexão 014B ausente.");
        string worlds=Environment.GetEnvironmentVariable("ELIFOOT_014B_REAL_WORLD_PG")??throw new ArgumentException("Conexão 014B de mundos ausente.");
        var c=new Npgsql.NpgsqlConnectionStringBuilder(catalog);var w=new Npgsql.NpgsqlConnectionStringBuilder(worlds);
        if(c.Port!=55437||!LocalCatalogTarget.Catalog(c)||!LocalCatalogTarget.Worlds(c,w))throw new ArgumentException("Use somente os bancos próprios 014B.");
        var services=new ServiceCollection();services.AddDbContext<CatalogDbContext>(o=>o.UseNpgsql(catalog));
        services.AddDbContext<ElifootDbContext>(o=>o.UseNpgsql(worlds));services.AddSingleton<IWorldRepository,EfWorldRepository>();
        services.AddSingleton(new CatalogSyncOptions());services.AddSingleton<CatalogSyncService>();services.AddSingleton<WorldCreatorService>();
        await using var provider=services.BuildServiceProvider();var creator=provider.GetRequiredService<WorldCreatorService>();
        Directory.CreateDirectory(output);var evidence=new List<object>();
        foreach(var gender in Enum.GetValues<FootballGender>())
        {
            var request=new WorldCreatorRequest("014B 100 temporadas "+gender,gender,"2026",WorldCreatorService.PriorityCountries.Select(x=>x.Code).ToList(),
                WorldAssemblyMode.Automatic,DistrictFillMode.Random,199801,[],UseSavedData:true,AllowFictionalPlayers:true,DataMode:WorldDataMode.REAL_CLUBS_SIMULATED);
            var selected=await creator.SelectForWorld(request);
            if(gender==FootballGender.Female)
            {
                var clubs=selected.Clubs.OrderByDescending(x=>x.Players.Count).ThenBy(x=>x.Rank).Take(128).ToList();
                request=request with{Mode=WorldAssemblyMode.Custom,DistrictMode=DistrictFillMode.Manual,FillRemainingDivisions=false,
                    Assignments=clubs.Select((x,i)=>new ClubAssignment(x.Key,i<32?i/8+1:5)).ToList()};
            }
            var preview=WorldCreatorEngine.Preview(request,await creator.Select(request.Countries,gender,"2026",true));
            if(preview.Errors.Count!=0)throw new ArgumentException(string.Join("; ",preview.Errors));
            var world=WorldCreatorEngine.Create(request,preview);
            if(world.Players.Count!=2304||world.Players.Any(x=>x.CatalogIdentity==null)||preview.Clubs.Any(x=>!PlayableWorldPolicy.Verified(x.Club)))throw new ArgumentException("Fundação inválida.");
            string json=JsonSerializer.Serialize(new{request,preview},CatalogImportCodec.Json);string path=Path.Combine(output,gender+".input.json");
            await File.WriteAllTextAsync(path,json);
            await File.WriteAllTextAsync(Path.Combine(output,gender+".birth-world.json"),JsonSerializer.Serialize(world,CatalogImportCodec.Json));
            evidence.Add(new{Gender=gender.ToString(),File=Path.GetFullPath(path),Sha256=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),
                Seed=request.Seed,Clubs=world.Clubs.Count,Players=world.Players.Count,request.DataMode,request.Mode,request.DistrictMode,
                preview.DataSummary,InitialCash=world.Clubs.Sum(x=>x.CashBalance),WagesPerOfficialMatch=world.Players.Sum(x=>x.SalaryPerRound),
                MeanStrength=world.Players.Average(x=>x.Strength),Snapshot=preview.DataHash});
        }
        await File.WriteAllTextAsync(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(evidence,CatalogImportCodec.Json));
        Console.WriteLine("PASS: own actual-catalog male/female frozen inputs, 128 verified real clubs each, explicitly authorized generated squads.");
    }
}
