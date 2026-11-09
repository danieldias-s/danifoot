using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class LocalHistoricalWorldTests
{
    [Fact]
    public async Task LocalApiDiscoversCoverageAndConfirmsAnIndependentMultiYearSnapshot()
    {
        var sandbox = Directory.CreateTempSubdirectory("elifoot-local-catalog-");
        try
        {
            await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Elifoot:AuthMode", "LocalDev");
                builder.UseSetting("Elifoot:DatabaseProvider", "InMemory");
                builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
                builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
                builder.UseSetting("Catalog013A:Enabled", "true");
                builder.UseSetting("Catalog013A:DatabaseProvider", "Sqlite");
                builder.UseSetting("ConnectionStrings:Catalog013A", $"Data Source={Path.Combine(sandbox.FullName, "catalog.db")};Pooling=False");
                builder.UseSetting("Catalog016A:Enabled", "true");
                builder.UseSetting("Catalog016B:Enabled", "true");
            });
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", $"localdev-{Guid.NewGuid():N}");
            const string route = "/api/world-creator";
            const string countryRoute = route + "/historical-countries?gender=Male";
            const string yearRoute = route + "/historical-years?gender=Male&countries=PT,ES";

            Assert.Empty((await client.GetFromJsonAsync<List<CatalogCountryOption>>(countryRoute))!);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/catalog-016a/account")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/world-catalog-016b/refresh", null)).StatusCode);

            await WriteClub(sandbox.FullName, "Portugal", "PT", 1985, 0);
            var index = app.Services.GetRequiredService<LocalHistoricalCatalog>();
            Assert.True((await index.ReindexAsync(sandbox.FullName)).Applied);
            Assert.Equal("PT", Assert.Single((await client.GetFromJsonAsync<List<CatalogCountryOption>>(countryRoute))!).Code);
            Assert.Equal("1985", Assert.Single((await client.GetFromJsonAsync<List<HistoricalYearOption>>(yearRoute))!).Year);

            await WriteClub(sandbox.FullName, "Portugal", "PT", 2025, 0);
            for (var club = 1; club <= 126; club++)
                await WriteClub(sandbox.FullName, "Espanha", "ES", 2025, club);
            Assert.True((await index.ReindexAsync(sandbox.FullName)).Applied);
            Assert.Equal(2, (await client.GetFromJsonAsync<List<CatalogCountryOption>>(countryRoute))!.Count);
            Assert.Empty((await client.GetFromJsonAsync<List<CatalogCountryOption>>(route + "/historical-countries?gender=Female"))!);
            var years = (await client.GetFromJsonAsync<List<HistoricalYearOption>>(yearRoute))!;
            Assert.Equal(["2025", "1985"], years.Select(year => year.Year));
            Assert.Equal(127, years[0].ClubCount);
            Assert.Equal("PT", Assert.Single(years[1].Countries).Code);
            Assert.Empty((await client.GetFromJsonAsync<List<HistoricalYearOption>>(route + "/historical-years?gender=Male&countries=XX"))!);

            var request = new WorldCreatorRequest("QA do catálogo histórico", FootballGender.Male, "2025", ["PT", "ES"],
                WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, 217, [], MaxHumans: 2,
                DataMode: WorldDataMode.REAL_COMPLETE, UseWorldCatalogRules: true, CatalogYears: ["1985", "2025"]);
            using var previewResponse = await client.PostAsJsonAsync(route + "/preview", request);
            previewResponse.EnsureSuccessStatusCode();
            var preview = (await previewResponse.Content.ReadFromJsonAsync<WorldCreatorPreview>())!;
            Assert.Empty(preview.Errors);
            Assert.Equal(0, preview.PendingSlots);
            Assert.Equal(128, preview.Clubs.Select(club => club.Club.Key).Distinct().Count());
            Assert.Equal(96, preview.Clubs.Count(club => club.Division == 5));
            var originalVersions = preview.Clubs.Where(club => club.Club.CountryCode == "PT").ToList();
            Assert.Equal(2, originalVersions.Count);
            Assert.All(originalVersions, club => Assert.Equal("Clube QA 0", club.Club.Name));

            // An older catalog country label must not rewrite the new snapshot's country identity.
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var country = await db.Countries.SingleAsync(country => country.Code == "ES");
                country.Name = "España";
                await db.SaveChangesAsync();
            }
            using var assemblyResponse = await client.PostAsJsonAsync(route + "/assemblies",
                new CreateWorldAssemblyRequest("Montagem histórica", FootballGender.Male, ["PT", "ES"], request.Seed,
                    SourcePreviewId: preview.DraftId));
            assemblyResponse.EnsureSuccessStatusCode();
            var assembly = (await assemblyResponse.Content.ReadFromJsonAsync<WorldAssemblyDto>())!;
            Assert.True(assembly.Validation.IsValid, string.Join(Environment.NewLine, assembly.Validation.Errors));
            var loaded = (await client.GetFromJsonAsync<WorldAssemblyDto>($"{route}/assemblies/{assembly.Id}"))!;
            Assert.True(loaded.Validation.IsValid, string.Join(Environment.NewLine, loaded.Validation.Errors));
            using var updateResponse = await client.PostAsJsonAsync($"{route}/assemblies/{assembly.Id}",
                new UpdateWorldAssemblyRequest(loaded.Name, loaded.Countries, loaded.Package, loaded.Version,
                    SavePreset: true));
            updateResponse.EnsureSuccessStatusCode();
            var saved = (await updateResponse.Content.ReadFromJsonAsync<WorldAssemblyDto>())!;
            Assert.True(saved.Validation.IsValid, string.Join(Environment.NewLine, saved.Validation.Errors));
            using var assemblyConfirmation = await client.PostAsJsonAsync($"{route}/assemblies/{assembly.Id}/confirm",
                new ConfirmWorldAssemblyRequest("qa-country-label"));
            assemblyConfirmation.EnsureSuccessStatusCode();
            Assert.All(saved.Package.CatalogOrigin!.Clubs.Where(entry => entry.Club.CountryCode == "ES"),
                entry => Assert.Equal("Espanha", entry.Club.CountryName));
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                Assert.Equal("España", (await db.Countries.SingleAsync(country => country.Code == "ES")).Name);
            }

            using var confirmation = await client.PostAsync($"{route}/confirm/{preview.DraftId}", null);
            confirmation.EnsureSuccessStatusCode();
            using var confirmationJson = JsonDocument.Parse(await confirmation.Content.ReadAsStringAsync());
            var id = confirmationJson.RootElement.GetProperty("id").GetGuid();
            var repo = app.Services.GetRequiredService<IWorldRepository>();
            var world = (await repo.GetWorldAsync(id))!;
            Assert.Equal(["2025", "1985"], world.CatalogOrigin!.CatalogYears);
            var variants = world.Clubs.Where(club => club.Name.StartsWith("Clube QA 0 (", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, variants.Count);
            Assert.Equal(2, variants.Select(club => club.Id).Distinct().Count());
            Assert.All(variants, club => Assert.Equal(11, world.Players.Count(player => player.ClubId == club.Id)));
            Assert.All(world.Clubs, club => Assert.Matches(@" \((1985|2025)\)$", club.Name));
            (await client.GetAsync($"/api/world-lobby-016b/{id}")).EnsureSuccessStatusCode();

            await WriteClub(sandbox.FullName, "Portugal", "PT", 1985, 0, "Clube QA atualizado");
            Assert.True((await index.ReindexAsync(sandbox.FullName)).Applied);
            var existing = (await repo.GetWorldAsync(id))!;
            Assert.Contains(existing.Clubs, club => club.Name == "Clube QA 0 (1985)");
            Assert.DoesNotContain(existing.Clubs, club => club.Name == "Clube QA atualizado (1985)");
        }
        finally
        {
            sandbox.Delete(recursive: true);
        }
    }

    private static Task WriteClub(string root, string country, string code, int year, int slot, string? name = null)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, "paises", country.ToUpperInvariant(), year.ToString(), "times"));
        string[] positions = ["G", "D", "D", "D", "D", "M", "M", "M", "M", "A", "A"];
        var club = new
        {
            ClubId = $"qa-{slot}",
            ClubName = name ?? $"Clube QA {slot}",
            Country = country,
            CountryCode = code,
            CatalogYear = year,
            Category = "Masculino",
            InitialRanking = slot + 1,
            RankingScore = 200.0 - slot,
            PrimaryColor = "#004733",
            SecondaryColor = "#FFFFFF",
            Players = positions.Select((position, index) => new
            {
                PlayerId = $"qa-{index}",
                Name = $"Atleta de teste {year}-{slot}-{index}",
                Position = position,
                Number = index + 1
            })
        };
        return File.WriteAllTextAsync(Path.Combine(directory.FullName, $"qa-{slot}.json"), JsonSerializer.Serialize(club));
    }
}
