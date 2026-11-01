using Elifoot.Contracts;
using Microsoft.EntityFrameworkCore;
namespace Elifoot.Infrastructure.Catalog.Import016A;

public sealed class Import016AOptions
{
    public bool Enabled {get;set;}
    public int DailyRequestBudget {get;set;} = 100;
    public int MinimumIntervalMilliseconds {get;set;} = 6100;
    public int TimeoutSeconds {get;set;} = 20;
    public int Retries {get;set;} = 2;
    public int CacheHours {get;set;} = 168;
    public string? AdminToken {get;set;}
    public string? ApiFootballKey {get;set;}
    public bool FixtureData {get;set;}
    public CatalogUsageRights? ApiFootballRights {get;set;}
}
// Auxiliary import-control tables only. Clubs/players/squads remain in the existing wc013a catalog.
public sealed class Import016ACache {public string Key {get;set;}="";public string Provider {get;set;}="";public string Resource {get;set;}="";public string Payload {get;set;}="";public DateTime FetchedAt {get;set;} public DateTime ExpiresAt {get;set;} public int HttpStatus {get;set;}}
public sealed class Import016ABudget {public string Key {get;set;}=""; public int Used {get;set;} public int Limit {get;set;} public int? Remaining {get;set;} public DateTime BlockedUntil {get;set;} public DateTime NextRequestAt {get;set;}}
public sealed class Import016AAudit {public Guid Id {get;set;} public string Provider {get;set;}="";public string Resource {get;set;}="";public DateTime At {get;set;}public int? HttpStatus {get;set;}public int? Remaining {get;set;}public string Status {get;set;}="";public int Records {get;set;}}
public sealed class Import016ARun {public Guid Id {get;set;}public string ReportJson {get;set;}="";public string PackagesJson {get;set;}="[]";public DateTime CreatedAt {get;set;}public bool Applied {get;set;}}
public sealed class Import016AMapping {public string Key {get;set;}="";public string ClubKey {get;set;}="";public string Provider {get;set;}="";public string TeamId {get;set;}="";public string LeagueId {get;set;}="";public string LeagueName {get;set;}="";public string Evidence {get;set;}="";public DateTime ReviewedAt {get;set;}}
public static class Import016ASchema
{
    public static void Configure(ModelBuilder b) {
        b.Entity<Import016ACache>().HasKey(x=>x.Key);
        b.Entity<Import016ABudget>().HasKey(x=>x.Key);
        b.Entity<Import016AAudit>().HasKey(x=>x.Id);
        b.Entity<Import016ARun>().HasKey(x=>x.Id);
        b.Entity<Import016AMapping>().HasKey(x=>x.Key);
    }
    public static async Task Ensure(CatalogDbContext db, CancellationToken ct=default) {
        // Additive schema only, including when EnsureCreated found an older catalog. No drops or save migrations.
        bool pg=db.Database.IsNpgsql();string date=pg?"timestamp with time zone":"TEXT",flag=pg?"boolean":"INTEGER",guid=pg?"uuid":"TEXT";
        string[] sql=[
            $"CREATE TABLE IF NOT EXISTS \"wc013a_Import016ACache\" (\"Key\" TEXT PRIMARY KEY, \"Provider\" TEXT NOT NULL, \"Resource\" TEXT NOT NULL, \"Payload\" TEXT NOT NULL, \"FetchedAt\" {date} NOT NULL, \"ExpiresAt\" {date} NOT NULL, \"HttpStatus\" INTEGER NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_Import016ABudget\" (\"Key\" TEXT PRIMARY KEY, \"Used\" INTEGER NOT NULL, \"Limit\" INTEGER NOT NULL, \"Remaining\" INTEGER NULL, \"BlockedUntil\" {date} NOT NULL, \"NextRequestAt\" {date} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_Import016AAudit\" (\"Id\" {guid} PRIMARY KEY, \"Provider\" TEXT NOT NULL, \"Resource\" TEXT NOT NULL, \"At\" {date} NOT NULL, \"HttpStatus\" INTEGER NULL, \"Remaining\" INTEGER NULL, \"Status\" TEXT NOT NULL, \"Records\" INTEGER NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_Import016ARun\" (\"Id\" {guid} PRIMARY KEY, \"ReportJson\" TEXT NOT NULL, \"PackagesJson\" TEXT NOT NULL, \"CreatedAt\" {date} NOT NULL, \"Applied\" {flag} NOT NULL)",
            $"CREATE TABLE IF NOT EXISTS \"wc013a_Import016AMapping\" (\"Key\" TEXT PRIMARY KEY, \"ClubKey\" TEXT NOT NULL, \"Provider\" TEXT NOT NULL, \"TeamId\" TEXT NOT NULL, \"LeagueId\" TEXT NOT NULL, \"LeagueName\" TEXT NOT NULL, \"Evidence\" TEXT NOT NULL, \"ReviewedAt\" {date} NOT NULL)"
        ];
        foreach(var command in sql)await db.Database.ExecuteSqlRawAsync(command,ct);
    }
}
