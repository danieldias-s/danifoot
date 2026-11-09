using Elifoot.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Infrastructure.Catalog;

public sealed class CatalogCountry { public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class CatalogProvider { public string Id { get; set; } = ""; public string LicenseReference { get; set; } = ""; public bool StorageAuthorized { get; set; } public bool CommercialUseAuthorized { get; set; } }
public sealed class CatalogSeason { public string Name { get; set; } = ""; }
public sealed class CatalogScope
{
    public string Key { get; set; } = ""; public string CountryCode { get; set; } = "";
    public FootballGender Gender { get; set; } public string Season { get; set; } = "";
    public string League { get; set; } = ""; public CatalogDataType DataType { get; set; }
    public string Provider { get; set; } = ""; public Guid? ActiveSnapshotId { get; set; }
    public DateTime? LastSuccess { get; set; } public DateTime NextAttempt { get; set; }
    public Guid? LeaseOwner { get; set; } public DateTime? LeaseUntil { get; set; }
    public CatalogScopeKey ToKey() => new(CountryCode, Gender, League, Season, DataType, Provider);
}
public sealed class CatalogSnapshot
{
    public Guid Id { get; set; } public string ScopeKey { get; set; } = "";
    public string Version { get; set; } = ""; public string Hash { get; set; } = "";
    public string PayloadJson { get; set; } = ""; public DateTime UpdatedAt { get; set; }
    public DateTime ValidatedAt { get; set; } public bool IsFixture { get; set; }
}
public sealed class CatalogLeague { public Guid SnapshotId { get; set; } public string ExternalId { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class CatalogClub { public Guid SnapshotId { get; set; } public string ExternalId { get; set; } = ""; public string Name { get; set; } = ""; public string LeagueExternalId { get; set; } = ""; }
public sealed class CatalogPlayer { public Guid SnapshotId { get; set; } public string ExternalId { get; set; } = ""; public string Name { get; set; } = ""; public string Position { get; set; } = ""; public int Strength { get; set; } }
public sealed class CatalogSquad { public Guid SnapshotId { get; set; } public string PlayerExternalId { get; set; } = ""; public string ClubKey { get; set; } = ""; }
public sealed class CatalogRanking { public Guid SnapshotId { get; set; } public string ClubKey { get; set; } = ""; public int Rank { get; set; } public DateTime UpdatedAt { get; set; } }
public sealed class CatalogSyncJob
{
    public Guid Id { get; set; } public string ScopeKey { get; set; } = ""; public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; } public DateTime? FinishedAt { get; set; }
    public int Attempts { get; set; } public string? Error { get; set; }
}
public sealed class WorldCreationPreset { public Guid OwnerId { get; set; } public string RequestJson { get; set; } = ""; public DateTime UpdatedAt { get; set; } }
public sealed class WorldCreationDraft
{
    public Guid Id { get; set; } public Guid OwnerId { get; set; } public string RequestJson { get; set; } = "";
    public string PreviewJson { get; set; } = ""; public DateTime ExpiresAt { get; set; }
    public Guid? WorldId { get; set; } public string? WorldJson { get; set; }
}

public sealed class CustomWorldAssembly
{
    public Guid Id {get;set;} public Guid OwnerId {get;set;} public string Name {get;set;}="";
    public string CountriesJson {get;set;}="[]"; public string SourceJson {get;set;}=""; public string PackageJson {get;set;}="";
    public int Version {get;set;} public bool IsSaved {get;set;} public bool TrustedCatalog {get;set;} public bool Personalized {get;set;}
    public DateTime CreatedAtUtc {get;set;} public DateTime UpdatedAtUtc {get;set;}
}

/// <summary>Separate database and lifecycle from gameplay persistence; never migrates an existing world database.</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<CatalogCountry> Countries => Set<CatalogCountry>();
    public DbSet<CatalogScope> Scopes => Set<CatalogScope>();
    public DbSet<CatalogSnapshot> Snapshots => Set<CatalogSnapshot>();
    public DbSet<CatalogSyncJob> SyncJobs => Set<CatalogSyncJob>();
    public DbSet<WorldCreationPreset> Presets => Set<WorldCreationPreset>();
    public DbSet<WorldCreationDraft> Drafts => Set<WorldCreationDraft>();
    public DbSet<CustomWorldAssembly> Assemblies => Set<CustomWorldAssembly>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        Import016A.Import016ASchema.Configure(b);
        World016B.WorldCatalogSchema016B.Configure(b);
        b.Entity<ClubColorCache>().HasKey(x=>x.ClubId);
        b.Entity<PlayerAppearance>().HasKey(x=>new{x.OwnerId,x.WorldId,x.ClubId});
        b.Entity<PlayerAppearance>().Property(x=>x.Version).IsConcurrencyToken();
        b.Entity<CustomWorldAssembly>().HasKey(x=>x.Id);
        b.Entity<CustomWorldAssembly>().HasIndex(x=>new{x.OwnerId,x.IsSaved});
        b.Entity<CustomWorldAssembly>().Property(x=>x.Version).IsConcurrencyToken();
        b.Entity<CatalogCountry>().HasKey(x => x.Code);
        b.Entity<CatalogProvider>().HasKey(x => x.Id);
        b.Entity<CatalogSeason>().HasKey(x => x.Name);
        b.Entity<CatalogScope>().HasKey(x => x.Key);
        b.Entity<CatalogScope>().HasIndex(x => new { x.CountryCode, x.Gender, x.Season, x.DataType });
        b.Entity<CatalogScope>().HasIndex(x => x.NextAttempt);
        b.Entity<CatalogSnapshot>().HasKey(x => x.Id);
        b.Entity<CatalogSnapshot>().HasIndex(x => new { x.ScopeKey, x.Version }).IsUnique();
        b.Entity<CatalogSnapshot>().HasIndex(x => new { x.ScopeKey, x.ValidatedAt });
        b.Entity<CatalogLeague>().HasKey(x => new { x.SnapshotId, x.ExternalId });
        b.Entity<CatalogClub>().HasKey(x => new { x.SnapshotId, x.ExternalId });
        b.Entity<CatalogClub>().HasIndex(x => new { x.SnapshotId, x.Name });
        b.Entity<CatalogPlayer>().HasKey(x => new { x.SnapshotId, x.ExternalId });
        b.Entity<CatalogSquad>().HasKey(x => new { x.SnapshotId, x.PlayerExternalId });
        b.Entity<CatalogSquad>().HasIndex(x => new { x.SnapshotId, x.ClubKey });
        b.Entity<CatalogRanking>().HasKey(x => new { x.SnapshotId, x.ClubKey });
        b.Entity<CatalogRanking>().HasIndex(x => new { x.SnapshotId, x.Rank });
        b.Entity<CatalogSyncJob>().HasKey(x => x.Id);
        b.Entity<CatalogSyncJob>().HasIndex(x => new { x.ScopeKey, x.StartedAt });
        b.Entity<WorldCreationPreset>().HasKey(x => x.OwnerId);
        b.Entity<WorldCreationDraft>().HasKey(x => x.Id);
        b.Entity<WorldCreationDraft>().HasIndex(x => new { x.OwnerId, x.ExpiresAt });
        b.Entity<LocalHistoricalCatalogRow>().HasKey(x => x.Id);
        b.Entity<LocalHistoricalCatalogAudit>().HasKey(x => x.Id);
        b.Entity<CatalogClubIdentity>().HasKey(x => x.Id);
        b.Entity<CatalogClubIdentity>().HasIndex(x => x.CanonicalKey).IsUnique();
        b.Entity<CatalogClubIdentity>().HasIndex(x => new { x.CountryCode, x.Gender, x.NormalizedName });
        b.Entity<CatalogClubIdentityAlias>().HasKey(x => x.Key);
        b.Entity<CatalogClubIdentityAlias>().HasIndex(x => x.ClubId);
        b.Entity<CatalogIdentityConflict>().HasKey(x => x.Id);
        b.Entity<CatalogIdentityConflict>().HasIndex(x => x.Status);
        foreach (var entity in b.Model.GetEntityTypes())
            entity.SetTableName("wc013a_" + entity.ClrType.Name);
    }
}
