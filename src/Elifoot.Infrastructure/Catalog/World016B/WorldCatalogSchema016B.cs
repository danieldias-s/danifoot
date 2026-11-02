using Elifoot.Contracts;
using Microsoft.EntityFrameworkCore;
namespace Elifoot.Infrastructure.Catalog.World016B;
public sealed class CountryCatalog016B
{
    public string Key {get;set;}=""; public string CountryCode {get;set;}=""; public FootballGender Gender {get;set;} public string Season {get;set;}="";
    public string Status {get;set;}="QUEUED"; public DateTime? LastAttemptAt {get;set;} public DateTime? LastSuccessfulUpdateAt {get;set;}
    public DateTime NextEligibleUpdateAt {get;set;} public DateTime NextAttemptAt {get;set;} public string? LastError {get;set;}
    public string CatalogVersion {get;set;}="";public string WorkJson {get;set;}="{}";public Guid? LeaseOwner {get;set;}public DateTime? LeaseUntil {get;set;}
}
public sealed class CountryLeague016B
{
    public string Key {get;set;}="";public string CountryCode {get;set;}="";public string ExternalId {get;set;}="";public string Name {get;set;}="";
    public string Season {get;set;}="";public FootballGender? Gender {get;set;} public string? CategoryEvidence {get;set;}
    public bool CurrentSeason {get;set;}public bool Standings {get;set;}public bool PlayerStatistics {get;set;}public DateTime ObservedAt {get;set;}
}
public sealed class CountryDirectory016B {public string Code {get;set;}="";public string ProviderName {get;set;}="";public DateTime ObservedAt {get;set;}}
public sealed class WorldClubProjection016B { public string Key {get;set;}="";public string ProfileJson {get;set;}="{}"; public DateTime UpdatedAt {get;set;} }
public sealed class WorldCatalogOptions016B {public bool Enabled {get;set;} public int EliteFullSquadRankCutoff {get;set;}=20; public int CountryCatalogRefreshDays {get;set;}=7;public bool FetchPlayerStatistics {get;set;}=true;}
public static class WorldCatalogSchema016B
{
    public static void Configure(ModelBuilder b) {
        b.Entity<CountryDirectory016B>().HasKey(x=>x.Code);b.Entity<CountryDirectory016B>().HasIndex(x=>x.ProviderName).IsUnique();
        b.Entity<CountryCatalog016B>().HasKey(x=>x.Key);b.Entity<CountryCatalog016B>().HasIndex(x=>new{x.Status,x.NextAttemptAt});
        b.Entity<CountryLeague016B>().HasKey(x=>x.Key);b.Entity<WorldClubProjection016B>().HasKey(x=>x.Key);
    }
    public static async Task Ensure(CatalogDbContext db,CancellationToken ct=default) {
        bool pg=db.Database.IsNpgsql();string date=pg?"timestamp with time zone":"TEXT",guid=pg?"uuid":"TEXT",flag=pg?"boolean":"INTEGER";
        string[] sql=[
            $"CREATE TABLE IF NOT EXISTS \"wc013a_CountryDirectory016B\" (\"Code\" TEXT PRIMARY KEY, \"ProviderName\" TEXT NOT NULL, \"ObservedAt\" {date} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_CountryCatalog016B\" (\"Key\" TEXT PRIMARY KEY, \"CountryCode\" TEXT NOT NULL, \"Gender\" INTEGER NOT NULL, \"Season\" TEXT NOT NULL, \"Status\" TEXT NOT NULL, \"LastAttemptAt\" {date} NULL, \"LastSuccessfulUpdateAt\" {date} NULL, \"NextEligibleUpdateAt\" {date} NOT NULL, \"NextAttemptAt\" {date} NOT NULL, \"LastError\" TEXT NULL, \"CatalogVersion\" TEXT NOT NULL, \"WorkJson\" TEXT NOT NULL, \"LeaseOwner\" {guid} NULL, \"LeaseUntil\" {date} NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_CountryLeague016B\" (\"Key\" TEXT PRIMARY KEY, \"CountryCode\" TEXT NOT NULL, \"ExternalId\" TEXT NOT NULL, \"Name\" TEXT NOT NULL, \"Season\" TEXT NOT NULL, \"Gender\" INTEGER NULL, \"CategoryEvidence\" TEXT NULL, \"CurrentSeason\" {flag} NOT NULL, \"Standings\" {flag} NOT NULL, \"PlayerStatistics\" {flag} NOT NULL, \"ObservedAt\" {date} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_WorldClubProjection016B\" (\"Key\" TEXT PRIMARY KEY, \"ProfileJson\" TEXT NOT NULL, \"UpdatedAt\" {date} NOT NULL)"
        ];
        foreach(string command in sql)await db.Database.ExecuteSqlRawAsync(command,ct);
    }
}
