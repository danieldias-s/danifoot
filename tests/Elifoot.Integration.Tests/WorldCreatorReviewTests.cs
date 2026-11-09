using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Api;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elifoot.Integration.Tests;

public sealed class WorldCreatorReviewTests
{
    private static WorldCreatorRequest Request() => new("Revisão QA", FootballGender.Male, "2026", ["ES"],
        WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, 71197, [], AllowFictionalTeams: true,
        UseSavedData: true, MaxHumans: 2, AllowFictionalPlayers: true, DataMode: WorldDataMode.FREE_WORLD,
        UseWorldCatalogRules: true, FictionalOnly: true, RequireHostStart: true, GenerateAllRosterPlayers: true);

    private sealed class Harness : IAsyncDisposable
    {
        private readonly DirectoryInfo files = Directory.CreateTempSubdirectory("elifoot-world-review-");
        public ServiceProvider Services { get; }
        public WorldCreatorService Creator => Services.GetRequiredService<WorldCreatorService>();
        public IWorldRepository Worlds => Services.GetRequiredService<IWorldRepository>();
        public Harness()
        {
            Services = new ServiceCollection()
                .AddDbContext<CatalogDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(files.FullName, "catalog.db")};Pooling=False;Default Timeout=15"))
                .AddDbContext<ElifootDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(files.FullName, "worlds.db")};Pooling=False;Default Timeout=15"))
                .AddSingleton<IWorldRepository, EfWorldRepository>()
                .AddSingleton(new CatalogSyncOptions())
                .AddSingleton<CatalogSyncService>()
                .AddSingleton<WorldCreatorService>()
                .BuildServiceProvider();
        }
        public async Task Initialize()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        }
        public async Task<T> Catalog<T>(Func<CatalogDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
        }
        public async Task<T> WorldDb<T>(Func<ElifootDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<ElifootDbContext>());
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            files.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task HttpReassignIsOwnedImmutableAndKeepsTheExactFrozenRosterBeforeConfirmation()
    {
        await using var h = new Harness(); await h.Initialize();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration["Elifoot:AuthMode"] = "LocalDev";
        builder.Services.AddSingleton(h.Creator);
        builder.Services.AddSingleton(h.Services.GetRequiredService<CatalogSyncService>());
        await using var app = builder.Build(); app.MapWorldCreator013A(); await app.StartAsync();
        using var client = app.GetTestClient();
        var owner = Guid.NewGuid();
        const string route = "/api/world-creator";
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{owner:N}");
        var response = await client.PostAsJsonAsync(route + "/preview", Request()); response.EnsureSuccessStatusCode();
        var preview = (await response.Content.ReadFromJsonAsync<WorldCreatorPreview>())!;
        Assert.Empty(preview.Errors);
        var original = await h.Catalog(db => db.Drafts.AsNoTracking().SingleAsync(draft => draft.Id == preview.DraftId));
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        (assignments[0], assignments[40]) =
            (assignments[40] with { Division = 1 }, assignments[0] with { Division = 5 });
        var command = new WorldCreatorReassignRequest(assignments, "Nome revisado", 3);
        var path = $"{route}/preview/{preview.DraftId}/reassign";
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, command)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path, command)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"localdev-{owner:N}");
        var updatedResponse = await client.PostAsJsonAsync(path, command); updatedResponse.EnsureSuccessStatusCode();
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<WorldCreatorPreview>())!;
        Assert.NotEqual(preview.DraftId, updated.DraftId);
        Assert.Equal("Nome revisado", updated.Name);
        Assert.Equal(preview.Clubs[40].Club.Key, updated.Clubs[0].Club.Key);
        Assert.Equal(original.PreviewJson, await h.Catalog(db => db.Drafts.Where(draft => draft.Id == preview.DraftId).Select(draft => draft.PreviewJson).SingleAsync()));
        Assert.Empty(await h.Worlds.ListWorldsAsync());
        foreach (var club in updated.Clubs)
            Assert.Equal(JsonSerializer.Serialize(preview.Clubs.Single(entry => entry.Club.Key == club.Club.Key).Club), JsonSerializer.Serialize(club.Club));

        var invalid = assignments.ToList(); invalid[0] = invalid[0] with { Division = 5 };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new WorldCreatorReassignRequest(invalid))).StatusCode);
        Assert.Equal(2, await h.Catalog(db => db.Drafts.CountAsync()));
        var world = await h.Creator.Confirm(owner, updated.DraftId);
        Assert.Equal(3, world.MaxHumans); Assert.Equal("Nome revisado", world.Name);
        Assert.NotNull(world.InitialLobby); Assert.Equal(owner, world.InitialLobby.HostCoachId);
        Assert.Equal(96, world.Clubs.Count(club => club.DivisionTier == DivisionTier.Distrital));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{route}/preview/{updated.DraftId}/reassign", command)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentConfirmationAcrossServiceInstancesCreatesOneDurableWorld()
    {
        await using var h = new Harness(); await h.Initialize();
        var owner = Guid.NewGuid(); var preview = await h.Creator.Preview(owner, Request());
        var other = new WorldCreatorService(h.Services.GetRequiredService<IServiceScopeFactory>(),
            h.Services.GetRequiredService<CatalogSyncService>(), h.Worlds);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            (index % 2 == 0 ? h.Creator : other).Confirm(owner, preview.DraftId)));
        var id = Assert.Single(results.Select(world => world.Id).Distinct());
        Assert.Equal(1, await h.WorldDb(db => db.Worlds.CountAsync()));
        Assert.Equal(1, await h.Catalog(db => db.Presets.CountAsync()));
        Assert.Equal(id, (await h.Catalog(db => db.Drafts.SingleAsync(draft => draft.Id == preview.DraftId))).WorldId);
        EfWorldRepository.ClearCacheForTesting();
        var restored = (await h.Worlds.GetWorldAsync(id))!;
        Assert.Equal(128, restored.Clubs.Count);
        for (int division = 1; division <= 5; division++)
            Assert.Equal(division == 5 ? 96 : 8, restored.Clubs.Count(club => (int)club.DivisionTier == division));
    }

    [Fact]
    public async Task FailedFirstWorldSaveLeavesNoPartialOrCachedWorldAndRetryUsesTheSameReceipt()
    {
        await using var h = new Harness(); await h.Initialize();
        var owner = Guid.NewGuid(); var preview = await h.Creator.Preview(owner, Request());
        await h.WorldDb(db => db.Database.ExecuteSqlRawAsync("CREATE TRIGGER qa_review_save_failure BEFORE INSERT ON ef_worlds BEGIN SELECT RAISE(ABORT, 'QA save failure'); END;"));
        await Assert.ThrowsAsync<DbUpdateException>(() => h.Creator.Confirm(owner, preview.DraftId));
        var receipt = await h.Catalog(db => db.Drafts.SingleAsync(draft => draft.Id == preview.DraftId));
        Assert.NotNull(receipt.WorldId); Assert.NotNull(receipt.WorldJson);
        Assert.Equal(0, await h.WorldDb(db => db.Worlds.CountAsync()));
        Assert.Equal(0, await h.WorldDb(db => db.FinancialLedger.CountAsync()));
        Assert.Equal(0, await h.WorldDb(db => db.SeasonHistories.CountAsync()));
        Assert.Null(await h.Worlds.GetWorldAsync(receipt.WorldId!.Value));
        Assert.Equal(0, await h.Catalog(db => db.Presets.CountAsync()));

        await h.WorldDb(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER qa_review_save_failure;"));
        var retry = await h.Creator.Confirm(owner, preview.DraftId);
        Assert.Equal(receipt.WorldId, retry.Id);
        Assert.Equal(1, await h.WorldDb(db => db.Worlds.CountAsync()));
        Assert.Equal(retry.Id, (await h.Creator.Confirm(owner, preview.DraftId)).Id);
    }

    [Fact]
    public async Task InvalidStoredDistributionAndExpiredDraftCannotBePublishedOrReassigned()
    {
        await using var h = new Harness(); await h.Initialize();
        var owner = Guid.NewGuid(); var preview = await h.Creator.Preview(owner, Request());
        var broken = preview.Clubs.ToList(); broken[0] = broken[0] with { Division = 5 };
        await h.Catalog(db => db.Drafts.Where(draft => draft.Id == preview.DraftId).ExecuteUpdateAsync(update => update
            .SetProperty(draft => draft.PreviewJson, JsonSerializer.Serialize(preview with { Clubs = broken }, CatalogImportCodec.Json))));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Creator.Confirm(owner, preview.DraftId));
        Assert.Empty(await h.Worlds.ListWorldsAsync());
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => h.Creator.Reassign(owner, preview.DraftId, new(assignments)));
        await h.Catalog(db => db.Drafts.Where(draft => draft.Id == preview.DraftId).ExecuteUpdateAsync(update => update
            .SetProperty(draft => draft.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Creator.Confirm(owner, preview.DraftId));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Creator.Reassign(owner, preview.DraftId, new(assignments)));
        Assert.Equal(1, await h.Catalog(db => db.Drafts.CountAsync()));
    }

    [LocalHistoricalCatalogIndexTests.LocalPostgresFact]
    public async Task PostgresHoldsTheDraftLockUntilTheWorldIsDurablyPublished()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ELIFOOT_LOCAL_CATALOG_TEST_PG"));
        Assert.Contains(settings.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.True(settings.Database?.Contains("_qa_", StringComparison.Ordinal) == true ||
            settings.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) == true,
            "Use um banco local exclusivo de QA/teste.");
        settings.Pooling = false;
        string schema = $"world_review_test_{Guid.NewGuid():N}";
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await using (var create = new NpgsqlCommand($"""
            CREATE SCHEMA "{schema}";
            CREATE TABLE "{schema}"."wc013a_WorldCreationDraft" (
                "Id" uuid PRIMARY KEY, "OwnerId" uuid NOT NULL, "RequestJson" text NOT NULL,
                "PreviewJson" text NOT NULL, "ExpiresAt" timestamp with time zone NOT NULL,
                "WorldId" uuid NULL, "WorldJson" text NULL);
            CREATE TABLE "{schema}"."wc013a_WorldCreationPreset" (
                "OwnerId" uuid PRIMARY KEY, "RequestJson" text NOT NULL, "UpdatedAt" timestamp with time zone NOT NULL);
            """, control))
            await create.ExecuteNonQueryAsync();
        var blockedRepository = new BlockingRepository();
        Task<GameWorld>? confirmation = null;
        try
        {
            settings.SearchPath = schema;
            await using var services = new ServiceCollection()
                .AddDbContext<CatalogDbContext>(options => options.UseNpgsql(settings.ConnectionString))
                .AddSingleton<IWorldRepository>(blockedRepository)
                .AddSingleton(new CatalogSyncOptions()).AddSingleton<CatalogSyncService>()
                .AddSingleton<WorldCreatorService>().BuildServiceProvider();
            var owner = Guid.NewGuid(); var request = Request();
            var preview = WorldCreatorEngine.Preview(request, new(request.Gender, request.Season, request.Countries, [], [], []));
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                db.Drafts.Add(new()
                {
                    Id = preview.DraftId, OwnerId = owner, ExpiresAt = DateTime.UtcNow.AddHours(1),
                    RequestJson = JsonSerializer.Serialize(request, CatalogImportCodec.Json),
                    PreviewJson = JsonSerializer.Serialize(preview, CatalogImportCodec.Json)
                });
                await db.SaveChangesAsync();
            }
            confirmation = services.GetRequiredService<WorldCreatorService>().Confirm(owner, preview.DraftId);
            await blockedRepository.Started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            long key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"world-creator-confirm:{preview.DraftId:N}")));
            await using var competingSession = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", control);
            competingSession.Parameters.AddWithValue("key", key);
            Assert.False(Assert.IsType<bool>(await competingSession.ExecuteScalarAsync()));

            blockedRepository.Continue.TrySetResult();
            var world = await confirmation;
            Assert.Equal(128, world.Clubs.Count);
            Assert.Equal(1, blockedRepository.Saves);
            Assert.True(Assert.IsType<bool>(await competingSession.ExecuteScalarAsync()));
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", control);
            release.Parameters.AddWithValue("key", key);
            Assert.True(Assert.IsType<bool>(await release.ExecuteScalarAsync()));
        }
        finally
        {
            blockedRepository.Continue.TrySetResult();
            try { if (confirmation != null) await confirmation; }
            finally
            {
                // The schema name is generated above and always belongs exclusively to this test.
                await using var cleanup = new NpgsqlCommand($"""DROP SCHEMA "{schema}" CASCADE;""", control);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    private sealed class BlockingRepository : IWorldRepository
    {
        private readonly InMemoryWorldRepository inner = new();
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Saves;
        public Task<GameWorld?> GetWorldAsync(Guid id, CancellationToken ct = default) => inner.GetWorldAsync(id, ct);
        public Task<GameWorld?> GetWorldByInviteCodeAsync(string code, CancellationToken ct = default) => inner.GetWorldByInviteCodeAsync(code, ct);
        public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct = default) => inner.ListWorldsAsync(ct);
        public async Task SaveWorldAsync(GameWorld world, CancellationToken ct = default)
        {
            Started.TrySetResult(); await Continue.Task.WaitAsync(ct);
            await inner.SaveWorldAsync(world, ct); Interlocked.Increment(ref Saves);
        }
    }
}
