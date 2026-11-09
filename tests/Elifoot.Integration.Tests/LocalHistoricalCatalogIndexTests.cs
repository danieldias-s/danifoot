using System.Text.Json;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elifoot.Integration.Tests;

public sealed class LocalHistoricalCatalogIndexTests
{
    private const string PostgresConnectionVariable = "ELIFOOT_LOCAL_CATALOG_TEST_PG";

    public sealed class LocalPostgresFactAttribute : FactAttribute
    {
        public LocalPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresConnectionVariable)))
                Skip = $"Configure {PostgresConnectionVariable} para PostgreSQL local de teste.";
        }
    }

    [Fact]
    public async Task EmptyDirectoriesDoNotAdvertiseCountriesOrYears()
    {
        using var files = new CatalogFiles();
        Directory.CreateDirectory(Path.Combine(files.Root, "paises", "PAIS_DE_TESTE", "1985", "times"));

        var report = await LocalHistoricalCatalog.ValidateAsync(files.Root);

        Assert.True(report.Valid);
        Assert.Equal(0, report.Files);
        Assert.Equal(0, report.Countries);
        Assert.Empty(report.AvailableYears);
        Assert.Equal(0, report.ClubVariants);
    }

    [Fact]
    public async Task RejectsMalformedRecordsAndDuplicatePlayersOrClubVariants()
    {
        using var files = new CatalogFiles();
        files.Club(1985);
        files.Club(1985, fileName: "duplicado.json");
        files.Club(1998, players:
            """
            [
              {"PlayerId":"ID_A","Name":"Atleta de teste","Position":"Z"},
              {"PlayerId":"id_a","Name":"Atleta de teste","Position":"M"}
            ]
            """);
        files.Write("PAIS_DE_TESTE/2025/times/invalido.json", "{ ClubId");
        files.Club(2025, folderYear: "2024", primaryColor: "verde");

        var report = await LocalHistoricalCatalog.ValidateAsync(files.Root);

        Assert.False(report.Valid);
        Assert.False(report.Applied);
        Assert.Contains(report.Errors, error => error.Contains("versão de clube duplicada"));
        Assert.Contains(report.Errors, error => error.Contains(".PlayerId duplicado"));
        Assert.Contains(report.Errors, error => error.Contains(".Position deve ser G, D, M ou A"));
        Assert.Contains(report.Errors, error => error.Contains("JSON inválido"));
        Assert.Contains(report.Errors, error => error.Contains("CatalogYear precisa ser"));
        Assert.Contains(report.Errors, error => error.Contains("PrimaryColor deve usar"));
    }

    [Fact]
    public async Task ReindexIsStableAndInvalidScanPreservesActiveIndex()
    {
        using var files = new CatalogFiles();
        await using var services = Services(options =>
            options.UseSqlite($"Data Source={Path.Combine(files.Root, "index.db")};Pooling=False"));
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.EnsureCreatedAsync();
            await LocalHistoricalCatalog.EnsureSchemaAsync(db);
        }
        await VerifyPublishedVersionsAsync(files, services);
    }

    [LocalPostgresFact]
    public async Task ReindexPublishesAndAuditsInPostgres()
    {
        var settings = Environment.GetEnvironmentVariable(PostgresConnectionVariable)!;
        var connectionString = new NpgsqlConnectionStringBuilder(settings);
        if (connectionString.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("O teste exige um PostgreSQL local.");

        using var files = new CatalogFiles();
        string schema = $"local_catalog_test_{Guid.NewGuid():N}";
        connectionString.Pooling = false;
        await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"""
            CREATE SCHEMA "{schema}";
            CREATE TABLE "{schema}"."wc013a_CatalogCountry" ("Code" TEXT PRIMARY KEY, "Name" TEXT NOT NULL);
            CREATE TABLE "{schema}"."wc013a_CatalogSnapshot" ("PayloadJson" TEXT NOT NULL);
            INSERT INTO "{schema}"."wc013a_CatalogSnapshot" ("PayloadJson") VALUES ('snapshot legado de teste');
            """, connection))
            await create.ExecuteNonQueryAsync();
        try
        {
            connectionString.SearchPath = schema;
            await using var services = Services(options => options.UseNpgsql(connectionString.ConnectionString));
            using (var scope = services.CreateScope())
                await LocalHistoricalCatalog.EnsureSchemaAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
            await VerifyPublishedVersionsAsync(files, services);

            await using var audit = new NpgsqlCommand($"""
                SELECT count(*) FROM "{schema}"."wc013a_LocalHistoricalCatalogAudit";
                """, connection);
            Assert.Equal(4L, Assert.IsType<long>(await audit.ExecuteScalarAsync()));
            await using var legacySnapshot = new NpgsqlCommand($"""
                SELECT "PayloadJson" FROM "{schema}"."wc013a_CatalogSnapshot";
                """, connection);
            Assert.Equal("snapshot legado de teste", Assert.IsType<string>(await legacySnapshot.ExecuteScalarAsync()));
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"""DROP SCHEMA "{schema}" CASCADE;""", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static ServiceProvider Services(Action<DbContextOptionsBuilder> configure) =>
        new ServiceCollection().AddDbContext<CatalogDbContext>(configure)
            .AddSingleton<LocalHistoricalCatalog>().BuildServiceProvider();

    private static async Task VerifyPublishedVersionsAsync(CatalogFiles files, ServiceProvider services)
    {
        var catalog = services.GetRequiredService<LocalHistoricalCatalog>();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Countries.Add(new CatalogCountry { Code = "ZZ", Name = "Nome legado" });
            await db.SaveChangesAsync();
        }
        Assert.Empty((await catalog.GetAsync()).Clubs);
        files.Club(1985);

        var first = await catalog.ReindexAsync(files.Root);
        var firstIndex = await catalog.GetAsync();
        Assert.True(first.Applied);
        Assert.True(first.Changed);
        Assert.Equal(["1985"], first.AvailableYears);
        Assert.Equal("ZZ:1985:CLUBE_TESTE:Male", Assert.Single(firstIndex.Clubs).Key);

        var same = await catalog.ReindexAsync(files.Root);
        Assert.True(same.Applied);
        Assert.False(same.Changed);
        Assert.Equal(first.Hash, same.Hash);
        Assert.Equal(firstIndex.IndexedAt, (await catalog.GetAsync()).IndexedAt);

        files.Club(2025, playerName: "Atleta de teste de 2025");
        files.Club(2025, country: "Outra Região", code: "XY");
        var second = await catalog.ReindexAsync(files.Root);
        Assert.True(second.Changed);
        Assert.Equal(["2025", "1985"], second.AvailableYears);
        Assert.Equal(2, second.Countries);
        Assert.Equal(3, second.ClubVariants);
        Assert.Equal(3, second.PlayersImported);
        Assert.NotEqual(first.Hash, second.Hash);

        var index = await catalog.GetAsync();
        var clubVersions = index.Clubs.Where(club => club.CountryCode == "ZZ").ToArray();
        Assert.Equal(2, clubVersions.Length);
        var oldPlayer = Assert.Single(Assert.Single(clubVersions, club => club.Season == "1985").Players);
        var newPlayer = Assert.Single(Assert.Single(clubVersions, club => club.Season == "2025").Players);
        Assert.NotEqual(oldPlayer.CanonicalPlayerId, newPlayer.CanonicalPlayerId);
        Assert.Equal("Atleta de teste de 2025", newPlayer.PlayerName);
        Assert.Equal(4, newPlayer.Number);
        Assert.Equal("Documento de teste", newPlayer.Source);
        Assert.Null(newPlayer.MembershipStart);
        Assert.Null(newPlayer.MembershipEnd);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var countryNames = await db.Countries.ToDictionaryAsync(country => country.Code, country => country.Name);
            Assert.Equal("Nome legado", countryNames["ZZ"]);
            Assert.Equal("Outra Região", countryNames["XY"]);
        }

        files.Write("PAIS_DE_TESTE/2025/times/invalido.json", "null");
        var invalid = await catalog.ReindexAsync(files.Root);
        Assert.False(invalid.Applied);
        Assert.False(invalid.Changed);
        Assert.Equal(0, invalid.PlayersImported);
        Assert.Equal(second.Hash, (await catalog.GetAsync()).Hash);
        Assert.Equal(3, (await catalog.GetAsync()).Clubs.Count);
    }

    private sealed class CatalogFiles : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("elifoot-local-catalog-").FullName;

        public void Club(int year, string? folderYear = null, string fileName = "teste.json", string? players = null,
            string country = "País de Teste", string code = "ZZ", string primaryColor = "#005033",
            string playerName = "Atleta de teste")
        {
            string folder = country == "País de Teste" ? "PAIS_DE_TESTE" : "OUTRA_REGIAO";
            players ??= JsonSerializer.Serialize(new[]
            {
                new { PlayerId = "ATLETA_TESTE", Name = playerName, Position = "G", Number = 4, Source = "Documento de teste" }
            });
            Write($"{folder}/{folderYear ?? year.ToString()}/times/{fileName}", $$"""
                {
                  "ClubId": "CLUBE_TESTE", "ClubName": "Clube de teste", "Country": "{{country}}", "CountryCode": "{{code}}",
                  "CatalogYear": {{year}}, "Category": "Masculino", "InitialRanking": 1, "RankingScore": 950,
                  "PrimaryColor": "{{primaryColor}}", "SecondaryColor": "#FFFFFF", "Players": {{players}}
                }
                """);
        }

        public void Write(string relative, string json)
        {
            string file = Path.Combine(Root, "paises", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, json);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
