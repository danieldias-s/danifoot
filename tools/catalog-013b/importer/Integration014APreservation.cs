using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.RealDataImporter;

internal static class Integration014APreservation
{
    private static string Financial(GameWorld world) => JsonSerializer.Serialize(new {
        Clubs = world.Clubs.OrderBy(c => c.Id).Select(c => new {c.Id,c.CashBalance,c.DivisionTier}),
        Players = world.Players.OrderBy(p => p.Id).Select(p => new {p.Id,p.ClubId,p.Strength,p.SalaryPerRound,p.ContractRoundsRemaining}),
        Loans = world.BankLoans.OrderBy(l => l.Id).Select(l => new {l.Id,l.ClubId,l.Mode,l.RemainingBalance,l.RemainingRounds,l.InterestCharged,l.InterestPaid,l.PrincipalPaid}),
        Ledger = world.Transactions.Select(t => new {t.Id,t.Amount,t.ClubId,t.Category})
    }, CatalogImportCodec.Json);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    public static async Task Run(string output, bool verify, string[] files)
    {
        string connection = Environment.GetEnvironmentVariable("ELIFOOT_014A_REAL_WORLD_PG") ?? throw new ArgumentException("Conexão 014A ausente.");
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Port != 55435 || parsed.Database != "elifoot_014a_013b_worlds_real")
            throw new ArgumentException("Somente banco de mundos 014A próprio é aceito.");
        ServiceProvider Provider(string database)
        {
            if (!database.StartsWith("elifoot_014a_old_", StringComparison.Ordinal)) throw new ArgumentException("Banco histórico fora da 014A.");
            var selected = new Npgsql.NpgsqlConnectionStringBuilder(connection) { Database = database };
            var services = new ServiceCollection(); services.AddDbContext<ElifootDbContext>(o => o.UseNpgsql(selected.ConnectionString));
            services.AddSingleton<IWorldRepository, EfWorldRepository>(); return services.BuildServiceProvider();
        }
        Directory.CreateDirectory(output); string manifest = Path.Combine(output,"old-worlds.json");
        if (verify)
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
            foreach (var item in document.RootElement.EnumerateArray())
            {
                await using var provider = Provider(item.GetProperty("Database").GetString()!);
                var world = await provider.GetRequiredService<IWorldRepository>().GetWorldAsync(item.GetProperty("Id").GetGuid()) ?? throw new ArgumentException("Save perdido após reinício.");
                if (Hash(Financial(world)) != item.GetProperty("FinancialHash").GetString()) throw new ArgumentException("Finanças antigas mudaram após reinício.");
            }
            await File.WriteAllTextAsync(Path.Combine(output,"old-worlds-restart.json"),JsonSerializer.Serialize(new {Status="PASS",OldFinancialStateUnchanged=true}));
            Console.WriteLine("PASS: historical saves retain cash, debt, salaries, contracts, players and ledger after restart."); return;
        }
        var evidence = new List<object>();
        foreach (string file in files)
        {
            string raw = await File.ReadAllTextAsync(file);
            var world = JsonSerializer.Deserialize<GameWorld>(raw, CatalogImportCodec.Json) ?? throw new ArgumentException("Save inválido.");
            // Historical fixtures reuse an invite code; distinct disposable databases preserve that code unchanged.
            string database = "elifoot_014a_old_" + Hash(raw)[..12].ToLowerInvariant();
            await using var provider = Provider(database);
            using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
            var repo = provider.GetRequiredService<IWorldRepository>();
            string financial = Financial(world); await repo.SaveWorldAsync(world); EfWorldRepository.ClearCacheForTesting();
            var loaded = await repo.GetWorldAsync(world.Id) ?? throw new ArgumentException("Save não recarregado.");
            if (Financial(loaded) != financial) throw new ArgumentException("Migração alterou dados financeiros existentes.");
            if (loaded.CatalogOrigin != null) throw new ArgumentException("Origem inventada em mundo antigo.");
            await repo.SaveWorldAsync(loaded);
            evidence.Add(new { world.Id, Database = database, Source = Path.GetFullPath(file), SourceSha256 = Hash(raw), FinancialHash = Hash(financial),
                world.SeasonNumber, world.RoundNumber, Clubs = world.Clubs.Count, Players = world.Players.Count,
                Cash = world.Clubs.Sum(c=>c.CashBalance), Debt = world.BankLoans.Sum(l=>l.RemainingBalance),
                Contracts = world.Players.Count(p=>p.ContractRoundsRemaining>0), CatalogOriginAbsent = true,
                FinancialStateUnchanged = true, OriginalFileWritten = false, loaded.Economy.OperatingUpgradeSeason });
        }
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(evidence,new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"PASS: {evidence.Count} original historical saves loaded and persisted only in 014A.");
    }
}
