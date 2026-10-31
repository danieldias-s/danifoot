using System.Net;
using System.Net.Http.Json;
using Elifoot.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class WorldCreator013ATests
{
    private static readonly List<string> Countries = WorldCreatorService.PriorityCountries.Select(c => c.Code).ToList();
    private static WorldCreatorRequest Request(FootballGender gender = FootballGender.Female) => new("Mundo 013A", gender, "2026", Countries,
        WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, 199801, [], UseSavedData: true);
    private sealed class Source(CatalogScopeKey key, Func<CancellationToken, Task<CatalogImportPackage>> fetch) : ICatalogSource
    {
        public CatalogScopeKey Scope => key;
        public Task<CatalogImportPackage> FetchAsync(CancellationToken ct) => fetch(ct);
    }
    private sealed class Harness : IAsyncDisposable
    {
        public ServiceProvider Provider { get; }
        public CatalogSyncService Sync => Provider.GetRequiredService<CatalogSyncService>();
        public WorldCreatorService Creator => Provider.GetRequiredService<WorldCreatorService>();
        public IWorldRepository Worlds => Provider.GetRequiredService<IWorldRepository>();
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"elifoot-013a-{Guid.NewGuid():N}.db");
        public Harness(IEnumerable<ICatalogSource>? sources = null, bool postgres = false)
        {
            var services = new ServiceCollection();
            services.AddDbContext<CatalogDbContext>(o => {
                if (postgres) o.UseNpgsql(Environment.GetEnvironmentVariable("ELIFOOT_013A_TEST_PG") ?? throw new InvalidOperationException("Set ELIFOOT_013A_TEST_PG to the isolated 013A test database."));
                else o.UseSqlite($"Data Source={_file};Default Timeout=15;Pooling=False");
            });
            services.AddSingleton<IWorldRepository, InMemoryWorldRepository>();
            services.AddSingleton(new CatalogSyncOptions { Timeout = TimeSpan.FromMilliseconds(60), Retries = 1 });
            foreach (var source in sources ?? []) services.AddSingleton(source);
            services.AddSingleton<CatalogSyncService>(); services.AddSingleton<WorldCreatorService>();
            Provider = services.BuildServiceProvider();
        }
        public async Task Initialize(bool fixtures = true)
        {
            using var scope = Provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreatedAsync();
            if (fixtures) foreach (var package in CatalogFixtureFactory.Packages()) await Sync.ImportAsync(package);
        }
        public async Task<T> Read<T>(Func<CatalogDbContext, Task<T>> action)
        { using var scope = Provider.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<CatalogDbContext>()); }
        public async ValueTask DisposeAsync() { await Provider.DisposeAsync(); if (File.Exists(_file)) File.Delete(_file); }
    }
    [Theory]
    [InlineData(FootballGender.Male)] [InlineData(FootballGender.Female)]
    public async Task EightCountriesAutomatic128UniqueAndImmutableIndependentSnapshot(FootballGender gender)
    {
        await using var h = new Harness(); await h.Initialize(); var request = Request(gender); var owner = Guid.NewGuid();
        var countries = await h.Creator.Countries("2026");
        Assert.Equal(8, countries.Count); Assert.All(countries, c => { Assert.Equal(20, c.MaleClubs); Assert.Equal(20, c.FemaleClubs); });
        var preview = await h.Creator.Preview(owner, request);
        Assert.Empty(preview.Errors); Assert.Equal(0, preview.PendingSlots); Assert.Equal(128, preview.Clubs.Select(c => c.Club.Key).Distinct().Count());
        for (int d = 1; d <= 5; d++) Assert.Equal(d == 5 ? 96 : 8, preview.Clubs.Count(c => c.Division == d));
        Assert.Equal(Enumerable.Range(1, 32), preview.Clubs.Where(c => c.Division < 5).Select(c => c.Club.Rank!.Value));
        Assert.All(preview.Clubs.SelectMany(c => c.Club.Players), p => Assert.Equal(gender, p.Gender));
        Assert.Contains(preview.Warnings, x => x.Contains("FIXTURES"));
        var world = await h.Creator.Confirm(owner, preview.DraftId);
        Assert.Equal(gender, world.Gender); Assert.Equal(ForceEvolutionMode.ClassicAdapted, world.ForceEvolutionMode);
        Assert.Equal(2304, world.Players.Count); Assert.Equal(2304, world.ClassicForce.SeasonStartingStrengths.Count);
        Assert.All(world.Clubs, club => Assert.True(SquadValidator.ValidateClubSquadLimits(world.Players.Where(p => p.ClubId == club.Id).ToList()).IsValid));
        Assert.Throws<InvalidOperationException>(() => world.SetCatalogOrigin(world.CatalogOrigin!));
        var reloaded = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world, CatalogImportCodec.Json), CatalogImportCodec.Json)!;
        var packageBackup = WorldEditorAndPackageService.ExportWorldToPackage(world);
        var clone = WorldEditorAndPackageService.CreateWorldFromPackage(packageBackup, "Clone offline");
        Assert.True(clone.Validation.IsValid); Assert.Equal(gender, clone.World!.Gender);
        Assert.Equal(preview.DataHash, clone.World.CatalogOrigin!.DataHash);
        Assert.Equal(gender, reloaded.Gender); Assert.Equal(preview.DataHash, reloaded.CatalogOrigin!.DataHash);
        string before = JsonSerializer.Serialize(world, CatalogImportCodec.Json);
        var package = CatalogFixtureFactory.Packages().First(p => p.Scope.Gender == gender && p.Scope.DataType == CatalogDataType.Clubs);
        await h.Sync.ImportAsync(package with { Version = "v2", UpdatedAt = package.UpdatedAt.AddDays(1), Clubs = package.Clubs.Select(c => c with { Name = c.Name + " atualizado" }).ToList() });
        Assert.Equal(before, JsonSerializer.Serialize(world, CatalogImportCodec.Json));
        Assert.Equal(world.Id, (await h.Creator.Confirm(owner, preview.DraftId)).Id);
        Assert.NotNull(await h.Creator.LastConfiguration(owner)); Assert.Null(await h.Creator.LastConfiguration(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Creator.Confirm(Guid.NewGuid(), preview.DraftId));
    }
    [Fact]
    public async Task CustomManualAssignmentsOverrideRankingAndManualDistrictsAreExact()
    {
        await using var h = new Harness(); await h.Initialize(); var selection = await h.Creator.Select(Countries, FootballGender.Female, "2026", true);
        var ordered = selection.Clubs.OrderBy(c => c.Rank).ToList();
        var assignments = ordered.Take(128).Select((c, i) => new ClubAssignment(c.Key, i < 32 ? i / 8 + 1 : 5)).ToList();
        (assignments[0], assignments[24]) = (assignments[0] with { Division = 4 }, assignments[24] with { Division = 1 });
        var request = Request() with { Mode = WorldAssemblyMode.Custom, DistrictMode = DistrictFillMode.Manual, Assignments = assignments, FillRemainingDivisions = false };
        var preview = WorldCreatorEngine.Preview(request, selection); Assert.Empty(preview.Errors);
        Assert.Equal(4, preview.Clubs.Single(c => c.Club.Key == ordered[0].Key).Division);
        Assert.Equal(1, preview.Clubs.Single(c => c.Club.Key == ordered[24].Key).Division);
        var duplicate = WorldCreatorEngine.Preview(request with { Assignments = [.. assignments, assignments[0] with { Division = 2 }] }, selection);
        Assert.Contains(duplicate.Errors, e => e.Contains("repetido"));
        var pending = WorldCreatorEngine.Preview(request with { Assignments = assignments.Take(31).ToList() }, selection);
        Assert.True(pending.PendingSlots > 0); Assert.NotEmpty(pending.Errors);
    }
    [Fact]
    public async Task RandomDistrictsAreSeededAndRankingAbsenceHasExplicitStableFallback()
    {
        await using var h = new Harness(); await h.Initialize(); var s = await h.Creator.Select(Countries, FootballGender.Female, "2026", true);
        var request = Request() with { DistrictMode = DistrictFillMode.Random };
        var reserved = s.Clubs.OrderBy(c => c.Rank).First();
        var forbidden = WorldCreatorEngine.Preview(request with { Assignments = [new(reserved.Key, 5)] }, s);
        Assert.Contains(forbidden.Errors, e => e.Contains("reservado"));
        var a = WorldCreatorEngine.Preview(request, s); var b = WorldCreatorEngine.Preview(request, s);
        var c = WorldCreatorEngine.Preview(request with { Seed = 33 }, s);
        Assert.Equal(a.Clubs.Select(x => x.Club.Key), b.Clubs.Select(x => x.Club.Key));
        Assert.NotEqual(string.Join(",", a.Clubs.Skip(32).Select(x => x.Club.Key)), string.Join(",", c.Clubs.Skip(32).Select(x => x.Club.Key)));
        var noRank = WorldCreatorEngine.Preview(request, s with { Clubs = s.Clubs.Select(c => c with { Rank = null }).ToList() });
        Assert.Empty(noRank.Errors); Assert.Contains(noRank.Warnings, w => w.Contains("Ranking ausente"));
    }
    [Fact]
    public async Task ShortageRequiresExplicitFictionalConsentAndDoesNotMixCategories()
    {
        await using var h = new Harness(); await h.Initialize(); var s = await h.Creator.Select(["BR"], FootballGender.Female, "2026", true);
        var request = Request() with { Countries = ["BR"] };
        var missing = WorldCreatorEngine.Preview(request, s); Assert.Equal(108, missing.PendingSlots); Assert.NotEmpty(missing.Errors);
        var completed = WorldCreatorEngine.Preview(request with { AllowFictionalTeams = true }, s);
        Assert.Empty(completed.Errors); Assert.Equal(108, completed.Clubs.Count(c => c.Club.IsFictional));
        Assert.All(completed.Clubs, c => Assert.Equal(FootballGender.Female, c.Club.Gender));
        var noData = await h.Creator.Select(["XX"], FootballGender.Male, "2026", true); Assert.Empty(noData.Clubs); Assert.Contains(noData.Warnings, w => w.Contains("sem cobertura"));
        var otherSeason = await h.Creator.Select(["BR"], FootballGender.Female, "2027", true); Assert.Empty(otherSeason.Clubs);
    }
    public static IEnumerable<object[]> InvalidPackages() => Enumerable.Range(0, 14).Select(i => new object[] { i });
    [Theory] [MemberData(nameof(InvalidPackages))]
    public async Task InvalidUpdatePreservesLastValidSnapshot(int issue)
    {
        await using var h = new Harness(); await h.Initialize(false);
        var good = CatalogFixtureFactory.Packages().First(p => p.Scope.DataType == CatalogDataType.Squads);
        await h.Sync.ImportAsync(CatalogFixtureFactory.Packages().First(p => p.Scope.CountryCode == good.Scope.CountryCode && p.Scope.Gender == good.Scope.Gender && p.Scope.DataType == CatalogDataType.Clubs));
        var id = await h.Sync.ImportAsync(good);
        var bad = good with { Version = "bad", UpdatedAt = good.UpdatedAt.AddDays(1) };
        bad = issue switch {
            0 => bad with { Complete = false }, 1 => bad with { ReceivedPages = 0 },
            2 => bad with { Players = [] }, 3 => bad with { Players = [.. good.Players.Skip(1), good.Players[1]] },
            4 => bad with { Players = good.Players.Select(p => p with { Gender = FootballGender.Female }).ToList() },
            5 => bad with { Players = good.Players.Select(p => p with { Season = "1998" }).ToList() },
            6 => bad with { Players = good.Players.Select(p => p with { Strength = 51 }).ToList() },
            7 => bad with { Players = good.Players.Select(p => p with { Position = "?" }).ToList() },
            8 => bad with { StorageAuthorized = false }, 9 => bad with { ExpectedItems = 1 },
            10 => bad with { Players = good.Players.Select(p => p with { Position = p.Position == "G" ? "D" : p.Position }).ToList() },
            11 => bad with { Players = good.Players.Take(10).ToList(), ExpectedItems = 10 },
            12 => bad with { UpdatedAt = good.UpdatedAt.AddDays(-1) },
            _ => bad with { Players = good.Players.Select(p => p with { ClubExternalId = "unknown" }).ToList() }
        };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => h.Sync.ImportAsync(bad));
        Assert.Equal(id, await h.Read(db => db.Scopes.Where(s => s.Key == good.Scope.Key).Select(s => s.ActiveSnapshotId).SingleAsync()));
        Assert.Equal(2, await h.Read(db => db.Snapshots.CountAsync()));
    }
    [Theory] [InlineData(FootballGender.Male)] [InlineData(FootballGender.Female)]
    public async Task PartialCountryFallbackWithApiFailureAndSuccessfulCountryUsesIndependentDates(FootballGender gender)
    {
        var packages = CatalogFixtureFactory.Packages().Where(p => p.Scope.Gender == gender && p.Scope.DataType == CatalogDataType.Clubs).ToList();
        var brazil = packages.Single(p => p.Scope.CountryCode == "BR"); var argentina = packages.Single(p => p.Scope.CountryCode == "AR");
        var fresh = brazil with { Version = "new", UpdatedAt = DateTime.UtcNow.AddMinutes(-1) };
        ICatalogSource[] sources = [new Source(brazil.Scope, _ => Task.FromResult(fresh)),
            new Source(argentina.Scope, _ => throw new HttpRequestException("offline"))];
        await using var h = new Harness(sources); await h.Initialize();
        await h.Read(async db => { await db.Scopes.ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttempt, DateTime.UnixEpoch)); return true; });
        var selection = await h.Creator.Select(["BR", "AR"], gender, "2026", false);
        Assert.Equal(40, selection.Clubs.Count);
        Assert.Contains(selection.References, r => r.Scope.CountryCode == "BR" && r.Scope.DataType == CatalogDataType.Clubs && r.Version == "new");
        Assert.Contains(selection.References, r => r.Scope.CountryCode == "AR" && r.Scope.DataType == CatalogDataType.Clubs && r.Status == "Fallback" && r.Version == "fixture-v1");
        Assert.All(selection.Clubs.SelectMany(c => c.Players), p => Assert.Equal(gender, p.Gender));
        var dates = selection.Clubs.First(c => c.CountryCode == "BR").References.Select(r => r.UpdatedAt).Distinct().ToList(); Assert.True(dates.Count >= 2);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task TimeoutAndRateLimitHaveBoundedRetriesAndFallback(bool rateLimit)
    {
        var package = CatalogFixtureFactory.Packages().First(); int calls = 0;
        var source = new Source(package.Scope, async ct => { Interlocked.Increment(ref calls);
            if (rateLimit) throw new HttpRequestException("429", null, HttpStatusCode.TooManyRequests);
            await Task.Delay(5000, ct); return package; });
        await using var h = new Harness([source]); await h.Initialize(false); var id = await h.Sync.ImportAsync(package);
        await h.Read(async db => { await db.Scopes.ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttempt, DateTime.UnixEpoch)); return true; });
        await h.Sync.RunDueAsync(); Assert.Equal(rateLimit ? 1 : 2, calls);
        Assert.Equal(id, await h.Read(db => db.Scopes.Select(s => s.ActiveSnapshotId).SingleAsync()));
        var failure = await h.Read(db => db.SyncJobs.SingleAsync(j => j.Status == "Failed")); Assert.Equal(rateLimit ? "RateLimit" : "Timeout/cancelamento", failure.Error);
        await h.Sync.RunDueAsync(); Assert.Equal(rateLimit ? 1 : 2, calls);
    }
    [Fact]
    public async Task SharedSyncLeaseAcrossServiceInstancesCoalescesConcurrentFetchAndImportIsIdempotent()
    {
        var package = CatalogFixtureFactory.Packages().First(); int calls = 0;
        var source = new Source(package.Scope, async ct => { Interlocked.Increment(ref calls); await Task.Delay(20, ct); return package; });
        await using var h = new Harness([source]); await h.Initialize(false); await h.Sync.EnsureScopeAsync(package.Scope);
        var other = new CatalogSyncService(h.Provider.GetRequiredService<IServiceScopeFactory>(), [source], h.Provider.GetRequiredService<CatalogSyncOptions>());
        await Task.WhenAll(h.Sync.RunDueAsync(), other.RunDueAsync(), h.Sync.RunDueAsync()); Assert.Equal(1, calls);
        var id = await h.Sync.ImportAsync(package); Assert.Equal(id, await h.Sync.ImportAsync(package));
        Assert.Equal(1, await h.Read(db => db.Snapshots.CountAsync()));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(package with { CountryName = "different" }));
    }
    [Fact]
    public async Task JsonCsvImportChecksRightsQuotesAndPositionsAndPreservesLastGoalkeeper()
    {
        var package = CatalogFixtureFactory.Packages().First();
        var csv = "ClubExternalId,ClubName,ShortName,LeagueExternalId,LeagueName\r\n1,\"Clube, Autorizado\",AUT,L1,Liga\r\n";
        var decoded = CatalogImportCodec.ReadCsv(package with { ExpectedItems = 1 }, csv);
        Assert.Equal("Clube, Autorizado", decoded.Clubs.Single().Name);
        Assert.Equal(decoded, CatalogImportCodec.ReadJson(JsonSerializer.Serialize(decoded, CatalogImportCodec.Json)) with { Clubs = decoded.Clubs, Players = decoded.Players, Rankings = decoded.Rankings });
        Assert.Equal("G", CatalogImportCodec.Position("Goalkeeper")); Assert.Equal("A", CatalogImportCodec.Position("Forward"));
        Assert.Throws<ArgumentException>(() => CatalogSyncService.Validate(decoded with { CommercialUseAuthorized = false }));
        var world = WorldSeedFactory.CreateSeededWorld(); var squad = world.Players.Where(p => p.ClubId == world.Clubs[0].Id).ToList();
        var keepers = squad.Where(p => p.Position == PlayerPosition.G).ToList(); squad.Remove(keepers[0]);
        Assert.False(SquadValidator.CanRemovePlayerFromSquad(squad, keepers[1].Id).CanRemove);
    }
    [Fact]
    public async Task PostgreSql013A_PersistenceRestartOfflineWorldAndCatalogSnapshots()
    {
        await using var h = new Harness(postgres: true); await h.Initialize(); var owner = Guid.NewGuid();
        var preview = await h.Creator.Preview(owner, Request()); var world = await h.Creator.Confirm(owner, preview.DraftId);
        var connection = Environment.GetEnvironmentVariable("ELIFOOT_013A_WORLD_PG")!;
        var isolated = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        Assert.True(isolated.Database!.Contains("013a") && isolated.Port == 55433 || isolated.Database.Contains("013b") && isolated.Port == 55434
            || isolated.Host == "127.0.0.1" && isolated.Database.StartsWith("elifoot_014a_", StringComparison.Ordinal) && isolated.Port == 55435
            || isolated.Host == "127.0.0.1" && isolated.Database.StartsWith("elifoot_013c_", StringComparison.Ordinal) && isolated.Port == 55436);
        var services = new ServiceCollection(); services.AddDbContext<ElifootDbContext>(o => o.UseNpgsql(connection)); services.AddSingleton<IWorldRepository, EfWorldRepository>();
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repo = provider.GetRequiredService<IWorldRepository>(); await repo.SaveWorldAsync(world); EfWorldRepository.ClearCacheForTesting();
        var loaded = await repo.GetWorldAsync(world.Id); Assert.NotNull(loaded); Assert.Equal(FootballGender.Female, loaded.Gender);
        Assert.Equal(world.CatalogOrigin!.DataHash, loaded.CatalogOrigin!.DataHash); Assert.Equal(2304, loaded.Players.Count);
        var restarted = new WorldCreatorService(h.Provider.GetRequiredService<IServiceScopeFactory>(), h.Sync, repo);
        var selection = await restarted.Select(Countries, FootballGender.Female, "2026", true); Assert.Equal(160, selection.Clubs.Count);
        Assert.NotNull(await restarted.LastConfiguration(owner)); Assert.Equal(world.Id, (await restarted.Confirm(owner, preview.DraftId)).Id);
        var existing = WorldSeedFactory.CreateSeededWorld("Existing 012C", forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        await repo.SaveWorldAsync(existing); EfWorldRepository.ClearCacheForTesting();
        Assert.Null((await repo.GetWorldAsync(existing.Id))!.CatalogOrigin);
    }
    [Fact]
    public async Task HttpPreviewConfirmationIsOwnedAndDoesNotRequeryChangedCatalog()
    {
        await using var h = new Harness(); await h.Initialize();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration["Elifoot:AuthMode"] = "LocalDev";
        builder.Services.AddSingleton(h.Creator); builder.Services.AddSingleton(h.Sync);
        await using var app = builder.Build(); app.MapWorldCreator013A(); await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/world-creator/countries?season=2026")).StatusCode);
        var owner = Guid.NewGuid(); client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{owner:N}");
        var response = await client.PostAsJsonAsync("/api/world-creator/preview", Request()); response.EnsureSuccessStatusCode();
        var preview = (await response.Content.ReadFromJsonAsync<WorldCreatorPreview>())!; Assert.Empty(preview.Errors);
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/world-creator/confirm/{preview.DraftId}", new { })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{owner:N}");
        var confirmed = await client.PostAsJsonAsync($"/api/world-creator/confirm/{preview.DraftId}", new { }); confirmed.EnsureSuccessStatusCode();
        var body = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(preview.DataHash, body.GetProperty("catalogOrigin").GetProperty("dataHash").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/catalog-013a/run-due", new { })).StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/world-creator/preview", Request() with { Countries = [] });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [Fact]
    public async Task DatabaseFailureRollsBackStagedVersionWithoutDestroyingActiveData()
    {
        await using var h = new Harness(); await h.Initialize(false); var package = CatalogFixtureFactory.Packages().First();
        var id = await h.Sync.ImportAsync(package);
        await h.Read(async db => { await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER wc013a_test_failure BEFORE INSERT ON wc013a_CatalogClub BEGIN SELECT RAISE(ABORT, 'test database failure'); END;"); return true; });
        await Assert.ThrowsAsync<DbUpdateException>(() => h.Sync.ImportAsync(package with { Version = "db-failure", UpdatedAt = package.UpdatedAt.AddDays(1) }));
        Assert.Equal(id, await h.Read(db => db.Scopes.Select(s => s.ActiveSnapshotId).SingleAsync()));
        Assert.Equal(1, await h.Read(db => db.Snapshots.CountAsync()));
        Assert.Equal(20, await h.Read(db => db.Set<CatalogClub>().CountAsync()));
        Assert.Equal("Banco", await h.Read(db => db.SyncJobs.Where(j => j.Status == "Failed").Select(j => j.Error).SingleAsync()));
    }    [Fact]
    public async Task CountriesAndLeaguesHaveIndependentSnapshotsCadenceAndJsonCsvImport()
    {
        await using var h = new Harness(); await h.Initialize(false);
        var template = CatalogFixtureFactory.Packages().First();
        var country = template with { Scope = template.Scope with { DataType = CatalogDataType.Countries }, Clubs = [], ExpectedItems = 1,
            CountryData = [new("BR", "Brasil")] };
        var leagues = template with { Scope = template.Scope with { DataType = CatalogDataType.Leagues }, Clubs = [], ExpectedItems = 1,
            LeagueData = [new("BR-M-2026", "Liga autorizada", FootballGender.Male, "2026")] };
        await h.Sync.ImportAsync(country); await h.Sync.ImportAsync(leagues);
        var states = await h.Read(db => db.Scopes.OrderBy(s => s.DataType).ToListAsync());
        Assert.Equal(2, states.Count); Assert.NotEqual(states[0].NextAttempt.Date, states[1].NextAttempt.Date);
        Assert.Equal("Liga autorizada", await h.Read(db => db.Set<CatalogLeague>().Select(x => x.Name).SingleAsync()));
        var csv = "LeagueExternalId,LeagueName,Gender,Season\nBR-M-2026,Liga autorizada,Male,2026\n";
        Assert.Equal(leagues.LeagueData!.Single(), CatalogImportCodec.ReadCsv(leagues, csv).LeagueData!.Single());
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(leagues with { Version = "wrong", LeagueData = [leagues.LeagueData[0] with { Gender = FootballGender.Female }] }));
    }    [Fact]
    public async Task FirstNetworkLoadOrdersClubsBeforeSquadsAndRankingEvenWithReversedRegistration()
    {
        var packages = CatalogFixtureFactory.Packages().Take(3).Reverse().ToList();
        var sources = packages.Select(p => (ICatalogSource)new Source(p.Scope, _ => Task.FromResult(p))).ToList();
        await using var h = new Harness(sources); await h.Initialize(false);
        var selection = await h.Creator.Select(["BR"], FootballGender.Male, "2026", false);
        Assert.Equal(20, selection.Clubs.Count);
        Assert.All(selection.Clubs, c => { Assert.Equal(18, c.Players.Count); Assert.NotNull(c.Rank); });
        Assert.Empty(await h.Read(db => db.SyncJobs.Where(j => j.Status == "Failed").ToListAsync()));
    }}
