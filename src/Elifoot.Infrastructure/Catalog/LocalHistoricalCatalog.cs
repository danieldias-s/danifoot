using System.Globalization;
using System.Text.Json;
using Elifoot.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure.Catalog;

public sealed record LocalHistoricalCatalogIndex(string? Version, string? Hash, DateTime? IndexedAt, int Files,
    List<EligibleCatalogClub> Clubs)
{
    public static LocalHistoricalCatalogIndex Empty => new(null, null, null, 0, []);
}

public sealed record LocalHistoricalCatalogReport(bool Valid, bool Applied, bool Changed, int Files, int Countries,
    List<string> AvailableYears, int ClubVariants, int PlayersInFiles, int PlayersImported,
    string? Version, string? Hash, List<string> Errors);

internal sealed class LocalHistoricalCatalogRow
{
    public int Id { get; set; }
    public string Hash { get; set; } = "";
    public string PayloadJson { get; set; } = "";
}

internal sealed class LocalHistoricalCatalogAudit
{
    public Guid Id { get; set; }
    public DateTime IndexedAt { get; set; }
    public string ReportJson { get; set; } = "";
}

public sealed class LocalHistoricalCatalog(IServiceScopeFactory scopes)
{
    private const int ActiveIndexId = 1;
    private readonly SemaphoreSlim reindexGate = new(1, 1);

    public static Task EnsureSchemaAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        bool postgres = db.Database.IsNpgsql();
        string guid = postgres ? "uuid" : "TEXT";
        string timestamp = postgres ? "timestamp with time zone" : "TEXT";
        // Only fixed provider-specific types are interpolated here.
#pragma warning disable EF1002
        return db.Database.ExecuteSqlRawAsync($"""
            CREATE TABLE IF NOT EXISTS "wc013a_LocalHistoricalCatalogRow" (
                "Id" INTEGER PRIMARY KEY, "Hash" TEXT NOT NULL, "PayloadJson" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "wc013a_LocalHistoricalCatalogAudit" (
                "Id" {guid} PRIMARY KEY, "IndexedAt" {timestamp} NOT NULL, "ReportJson" TEXT NOT NULL
            );
            """, ct);
#pragma warning restore EF1002
    }

    public async Task<LocalHistoricalCatalogIndex> GetAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        string? payload = await db.Set<LocalHistoricalCatalogRow>().AsNoTracking()
            .Where(row => row.Id == ActiveIndexId)
            .Select(row => row.PayloadJson)
            .SingleOrDefaultAsync(ct);
        return payload is null
            ? LocalHistoricalCatalogIndex.Empty
            : JsonSerializer.Deserialize<LocalHistoricalCatalogIndex>(payload, CatalogImportCodec.Json)
                ?? throw new InvalidOperationException("O índice ativo do catálogo local está inválido.");
    }

    public static async Task<LocalHistoricalCatalogReport> ValidateAsync(string root, CancellationToken ct = default)
    {
        var scan = await LocalHistoricalCatalogFiles.ReadAsync(root, ct);
        return Report(scan, applied: false, changed: false);
    }

    public async Task<LocalHistoricalCatalogReport> ReindexAsync(string root, CancellationToken ct = default)
    {
        await reindexGate.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(83465943722312);", ct);

            var scan = await LocalHistoricalCatalogFiles.ReadAsync(root, ct);
            string? activeHash = await db.Set<LocalHistoricalCatalogRow>().AsNoTracking()
                .Where(row => row.Id == ActiveIndexId)
                .Select(row => row.Hash)
                .SingleOrDefaultAsync(ct);
            bool valid = scan.Errors.Count == 0;
            bool changed = valid && scan.Index.Hash != activeHash;
            if (changed)
                await PublishAsync(db, scan.Index, ct);

            var report = Report(scan, applied: valid, changed);
            db.Set<LocalHistoricalCatalogAudit>().Add(new()
            {
                Id = Guid.NewGuid(),
                IndexedAt = DateTime.UtcNow,
                ReportJson = JsonSerializer.Serialize(report, CatalogImportCodec.Json)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return report;
        }
        finally
        {
            reindexGate.Release();
        }
    }

    private static async Task PublishAsync(CatalogDbContext db, LocalHistoricalCatalogIndex index, CancellationToken ct)
    {
        string payload = JsonSerializer.Serialize(index, CatalogImportCodec.Json);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "wc013a_LocalHistoricalCatalogRow" ("Id", "Hash", "PayloadJson")
            VALUES ({ActiveIndexId}, {index.Hash}, {payload})
            ON CONFLICT ("Id") DO UPDATE SET "Hash" = excluded."Hash", "PayloadJson" = excluded."PayloadJson";
            """, ct);

        foreach (var country in index.Clubs.DistinctBy(club => club.CountryCode))
            // Preserve the country labels referenced by previously saved custom assemblies.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "wc013a_CatalogCountry" ("Code", "Name")
                VALUES ({country.CountryCode}, {country.CountryName})
                ON CONFLICT ("Code") DO NOTHING;
                """, ct);
    }

    private static LocalHistoricalCatalogReport Report(LocalHistoricalCatalogScan scan, bool applied, bool changed)
    {
        var clubs = scan.Index.Clubs;
        int players = clubs.Sum(club => club.Players.Count);
        return new(scan.Errors.Count == 0, applied, changed, scan.Index.Files,
            clubs.Select(club => club.CountryCode).Distinct().Count(),
            clubs.Select(club => club.Season).Distinct()
                .OrderByDescending(year => int.Parse(year, CultureInfo.InvariantCulture)).ToList(),
            clubs.Count, players, applied ? players : 0, scan.Index.Version, scan.Index.Hash, scan.Errors);
    }
}
