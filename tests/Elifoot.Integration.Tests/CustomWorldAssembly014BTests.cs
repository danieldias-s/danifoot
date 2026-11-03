using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class CustomWorldAssembly014BTests
{
    private sealed class Harness : IAsyncDisposable
    {
        public readonly string FilePath = Path.Combine(Path.GetTempPath(), $"elifoot-014b-assembly-{Guid.NewGuid():N}.db");
        public ServiceProvider Provider { get; }
        public WorldAssemblyService Service => Provider.GetRequiredService<WorldAssemblyService>();
        public IWorldRepository Worlds => Provider.GetRequiredService<IWorldRepository>();
        public Harness()
        {
            var services = new ServiceCollection();
            services.AddDbContext<CatalogDbContext>(o => o.UseSqlite($"Data Source={FilePath};Pooling=False"));
            services.AddSingleton<IWorldRepository, InMemoryWorldRepository>();
            services.AddSingleton<WorldAssemblyService>(); services.AddSingleton<WorldCreatorService>();
            services.AddSingleton<CatalogSyncService>(); services.AddSingleton(new CatalogSyncOptions());
            Provider = services.BuildServiceProvider();
        }
        public async Task Init()
        {
            using var scope = Provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await db.Database.EnsureCreatedAsync(); await WorldAssemblyService.EnsureSchema(db); await WorldAssemblyService.EnsureSchema(db);
            db.Countries.Add(new() { Code = "BR", Name = "Brasil" }); db.Countries.Add(new() { Code = "PT", Name = "Portugal" }); await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() { await Provider.DisposeAsync(); if (File.Exists(FilePath)) File.Delete(FilePath); }
    }
    private static CreateWorldAssemblyRequest Request(FootballGender gender = FootballGender.Female) =>
        new("Minha montagem", gender, ["BR", "PT"], 199801, AllowPersonalizedContent: true);
    private static UpdateWorldAssemblyRequest Update(WorldAssemblyDto dto, WorldPackageExportDto? package = null, bool saved = false) =>
        new(dto.Name, dto.Countries, package ?? dto.Package, dto.Version, saved, true);
    [Theory]
    [InlineData(FootballGender.Male)]
    [InlineData(FootballGender.Female)]
    public async Task SavedAssemblyCreatesIndependentWorldsAndPreservesNegotiatedSalary(FootballGender gender)
    {
        await using var h = new Harness(); await h.Init(); var owner = Guid.NewGuid(); var a = await h.Service.Create(owner, Request(gender));
        Assert.True(a.Validation.IsValid, string.Join(";", a.Validation.Errors)); Assert.Equal(128, a.Package.Clubs.Count);
        var player = a.Package.Players[0]; var club = a.Package.Clubs.First(c => c.Id == player.ClubId);
        var p = a.Package with
        {
            Players = a.Package.Players.Select(x => x.Id == player.Id ? x with { Name = "Atleta personalizado", Strength = 47, SalaryPerRound = 12345, ContractRoundsRemaining = 45, IsStar = true } : x).ToList(),
            Clubs = a.Package.Clubs.Select(c => c.Id == club.Id ? c with { Name = "Clube personalizado" } : c).ToList()
        };
        a = await h.Service.Update(owner, a.Id, Update(a, p, true)); Assert.True(a.Validation.IsValid, string.Join(";", a.Validation.Errors));
        Assert.True(a.IsSaved); Assert.Single(await h.Service.List(owner)); Assert.Empty(await h.Service.List(Guid.NewGuid()));
        var one = await h.Service.Confirm(owner, a.Id, new("first")); string before = JsonSerializer.Serialize(one, CatalogImportCodec.Json);
        var duplicate = await h.Service.Confirm(owner, a.Id, new("first")); Assert.Equal(one.Id, duplicate.Id);
        var two = await h.Service.Confirm(owner, a.Id, new("second")); Assert.NotEqual(one.Id, two.Id);
        Assert.Empty(one.Clubs.Select(c => c.Id).Intersect(two.Clubs.Select(c => c.Id)));
        Assert.Empty(one.Players.Select(c => c.Id).Intersect(two.Players.Select(c => c.Id)));
        Assert.Empty(one.AiManagers.Select(c => c.Id).Intersect(two.AiManagers.Select(c => c.Id)));
        Assert.All(two.Players, p => Assert.Equal(two.Id, p.GameWorldId));
        var created = Assert.Single(one.Players, p => p.Name == "Atleta personalizado");
        Assert.Equal(12345m, created.SalaryPerRound); Assert.Equal(45, created.ContractRoundsRemaining); Assert.True(created.NegotiatedContractProtected);
        Assert.Equal(gender, one.Gender); Assert.Equal(ForceEvolutionMode.ClassicAdapted, one.ForceEvolutionMode);
        Assert.Equal(4, one.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital).GroupBy(c => c.DivisionTier).Count());
        Assert.All(one.Clubs, c => Assert.True(SquadValidator.ValidateClubSquadLimits(one.Players.Where(p => p.ClubId == c.Id).ToList()).IsValid));
        a = await h.Service.Update(owner, a.Id, Update(a, a.Package with { Players = a.Package.Players.Select(p => p with { Strength = 1 }).ToList() }));
        Assert.Equal(before, JsonSerializer.Serialize(await h.Worlds.GetWorldAsync(one.Id), CatalogImportCodec.Json));
        Assert.Equal(one.Id, (await h.Service.Confirm(owner, a.Id, new("first"))).Id);
    }
    [Fact]
    public async Task ManualDivisionUses017BReferenceAndKeepsContractedSalary()
    {
        await using var h = new Harness();
        await h.Init();
        var owner = Guid.NewGuid();
        var draft = await h.Service.Create(owner, Request() with { UseWorldCatalogRules = true });
        var first = draft.Package.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        var fourth = draft.Package.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var player = draft.Package.Players.First(p => p.ClubId == first.Id);
        var edited = draft.Package with
        {
            Clubs = draft.Package.Clubs.Select(c => c.Id == first.Id ? c with { DivisionTier = DivisionTier.QuartaDivisao }
                : c.Id == fourth.Id ? c with { DivisionTier = DivisionTier.PrimeiraDivisao } : c).ToList(),
            Players = draft.Package.Players.Select(p => p.Id == player.Id
                ? p with { Strength = 50, SalaryPerRound = 12345, ContractRoundsRemaining = 45 } : p).ToList()
        };
        draft = await h.Service.Update(owner, draft.Id, Update(draft, edited));
        Assert.True(draft.Validation.IsValid, string.Join(";", draft.Validation.Errors));
        var world = await h.Service.Confirm(owner, draft.Id, new("manual-017b"));
        var club = Assert.Single(world.Clubs, c => c.Name == first.Name);
        Assert.Equal(DivisionTier.QuartaDivisao, club.DivisionTier);
        Assert.Equal(new InitialSquadReference017B(1, 12, 11, 12), club.InitialSquadReference);
        var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
        Assert.All(squad, p => Assert.InRange(p.Strength, 11, 12));
        var signed = Assert.Single(squad, p => p.Name == player.Name);
        Assert.Equal(12345m, signed.SalaryPerRound);
        Assert.Equal(45, signed.ContractRoundsRemaining);
        Assert.Equal(signed.Strength, world.CatalogOrigin!.Clubs.Single(c => c.Club.Name == club.Name).Club.Players.Single(p => p.PlayerName == signed.Name).Strength);
    }

    [Fact]
    public async Task OwnershipConsentAndOptimisticConcurrencyAreEnforced()
    {
        await using var h = new Harness(); await h.Init(); var owner = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.Create(owner, Request() with { AllowPersonalizedContent = false }));
        var a = await h.Service.Create(owner, Request()); var other = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.Get(other, a.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.Update(other, a.Id, Update(a)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.Confirm(other, a.Id, new("steal")));
        var updated = await h.Service.Update(owner, a.Id, Update(a, saved: true));
        await Assert.ThrowsAsync<AssemblyConflictException>(() => h.Service.Update(owner, a.Id, Update(a)));
        Assert.Equal(updated.Version, (await h.Service.Get(owner, a.Id)).Version);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public async Task InvalidDraftRemainsEditableButCannotCreateWorld(int issue)
    {
        await using var h = new Harness(); await h.Init(); var owner = Guid.NewGuid(); var a = await h.Service.Create(owner, Request()); var p = a.Package;
        var first = p.Players[0]; var cid = first.ClubId;
        p = issue switch
        {
            0 => p with { Clubs = p.Clubs.Skip(1).ToList() },
            1 => p with { Clubs = [.. p.Clubs.Skip(1), p.Clubs[1]] },
            2 => p with { Players = [.. p.Players.Skip(1), p.Players[1]] },
            3 => p with { Players = p.Players.Select(x => x.Id == first.Id ? x with { Strength = 51 } : x).ToList() },
            4 => p with { Players = p.Players.Where(x => x.ClubId != cid).Concat(p.Players.Where(x => x.ClubId == cid).Take(10)).ToList() },
            5 => p with { Players = p.Players.Select(x => x.ClubId == cid ? x with { Position = PlayerPosition.D } : x).ToList() },
            6 => p with { Players = p.Players.Select(x => x.Id == first.Id ? x with { SalaryPerRound = 0 } : x).ToList() },
            7 => p with { Players = p.Players.Select(x => x.Id == first.Id ? x with { ContractRoundsRemaining = 0 } : x).ToList() },
            8 => p with { Clubs = p.Clubs.Select((c, i) => i == 0 ? c with { Country = "País inexistente" } : c).ToList() },
            9 => p with { Clubs = p.Clubs.Select((c, i) => i == 0 ? c with { DivisionTier = DivisionTier.Distrital } : c).ToList() },
            10 => p with { Gender = (FootballGender)99 },
            11 => p with { Players = p.Players.Select(x => x.Id == first.Id ? x with { Behavior = (PlayerBehavior)99 } : x).ToList() },
            12 => p with { Players = p.Players.Select(x => x.Id == first.Id ? x with { Id = Guid.Empty } : x).ToList() },
            _ => p with { Players = [.. p.Players, .. Enumerable.Range(0, 7).Select(_ => first with { Id = Guid.NewGuid() })] }
        };
        var bad = await h.Service.Update(owner, a.Id, Update(a, p, true)); Assert.False(bad.Validation.IsValid); Assert.NotEmpty(bad.Validation.Errors);
        var restored = await h.Service.Get(owner, a.Id); Assert.Equal(bad.Package, restored.Package, new PackageComparer());
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.Confirm(owner, a.Id, new("invalid")));
        var repaired = await h.Service.Update(owner, a.Id, Update(bad, a.Package)); Assert.True(repaired.Validation.IsValid);
        Assert.NotNull(await h.Service.Confirm(owner, a.Id, new("repaired")));
    }
    private sealed class PackageComparer : IEqualityComparer<WorldPackageExportDto>
    { public bool Equals(WorldPackageExportDto? a, WorldPackageExportDto? b) => JsonSerializer.Serialize(a, CatalogImportCodec.Json) == JsonSerializer.Serialize(b, CatalogImportCodec.Json); public int GetHashCode(WorldPackageExportDto obj) => 0; }
    [Fact]
    public async Task ConcurrentConfirmationCreatesExactlyOneReceiptAndWorld()
    {
        await using var h = new Harness(); await h.Init(); var owner = Guid.NewGuid(); var a = await h.Service.Create(owner, Request());
        var worlds = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => h.Service.Confirm(owner, a.Id, new("retry"))));
        Assert.Single(worlds.Select(w => w.Id).Distinct());
        using var scope = h.Provider.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Drafts.CountAsync());
    }
}
