using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elifoot.Infrastructure.Catalog;

public sealed class CatalogClubIdentity
{
    public Guid Id { get; set; }
    public string CountryCode { get; set; } = "";
    public FootballGender Gender { get; set; }
    public string CanonicalKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}
public sealed class CatalogClubIdentityAlias
{
    public string Key { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public FootballGender Gender { get; set; }
    public string Provider { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public Guid ClubId { get; set; }
    public string? ReviewEvidence { get; set; }
}
public sealed class CatalogIdentityConflict
{
    public Guid Id { get; set; }
    public Guid LeftClubId { get; set; }
    public Guid RightClubId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Open";
    public string? Evidence { get; set; }
}
public static class CatalogIdentityService
{
    public static string AliasKey(string country, FootballGender gender, string provider, string id) => $"{Uri.EscapeDataString(country)}|{gender}|{Uri.EscapeDataString(provider)}|{Uri.EscapeDataString(id)}";
    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    private static string Normalize(string name) => string.Concat(name.Normalize(NormalizationForm.FormD)
        .Where(c => char.IsLetterOrDigit(c))).ToUpperInvariant();
    public static async Task RegisterAsync(CatalogDbContext db, CatalogImportPackage package, CancellationToken ct)
    {
        foreach (var team in package.Clubs)
        {
            string alias = AliasKey(package.Scope.CountryCode, package.Scope.Gender, package.Scope.Provider, team.ExternalId);
            Guid id = StableId(alias); string normalized = Normalize(team.Name);
            string canonical = WorldCreatorEngine.ClubKey(package.Scope.CountryCode, package.Scope.Gender, package.Scope.Provider, team.ExternalId);
            if (!await db.Set<CatalogClubIdentityAlias>().AnyAsync(a => a.Key == alias, ct))
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogClubIdentity\" (\"Id\", \"CountryCode\", \"Gender\", \"CanonicalKey\", \"Name\", \"NormalizedName\") VALUES ({id}, {package.Scope.CountryCode}, {(int)package.Scope.Gender}, {canonical}, {team.Name}, {normalized}) ON CONFLICT (\"Id\") DO NOTHING", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogClubIdentityAlias\" (\"Key\", \"CountryCode\", \"Gender\", \"Provider\", \"ExternalId\", \"ClubId\", \"ReviewEvidence\") VALUES ({alias}, {package.Scope.CountryCode}, {(int)package.Scope.Gender}, {package.Scope.Provider}, {team.ExternalId}, {id}, { (string?)null }) ON CONFLICT (\"Key\") DO NOTHING", ct);
            var actualId = await db.Set<CatalogClubIdentityAlias>().Where(a => a.Key == alias).Select(a => a.ClubId).SingleAsync(ct);
            var peers = await db.Set<CatalogClubIdentity>().AsNoTracking().Where(c => c.Id != actualId && c.CountryCode == package.Scope.CountryCode &&
                c.Gender == package.Scope.Gender && c.NormalizedName == normalized && db.Set<CatalogClubIdentityAlias>().Any(a => a.ClubId == c.Id)).ToListAsync(ct);
            foreach (var peer in peers)
            {
                Guid left = actualId.CompareTo(peer.Id) < 0 ? actualId : peer.Id, right = actualId.CompareTo(peer.Id) < 0 ? peer.Id : actualId;
                var conflictId = StableId($"identity-conflict:{left}:{right}");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogIdentityConflict\" (\"Id\", \"LeftClubId\", \"RightClubId\", \"Reason\", \"Status\", \"Evidence\") VALUES ({conflictId}, {left}, {right}, {"Nomes semelhantes não confirmam identidade esportiva"}, {"Open"}, { (string?)null }) ON CONFLICT (\"Id\") DO NOTHING", ct);
            }
        }
    }
    public static async Task MapAsync(CatalogDbContext db, CatalogIdentityMappingRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence) || request.Evidence.Length > 1000) throw new ArgumentException("Registre evidência da revisão da identidade.");
        var key = AliasKey(request.CountryCode, request.Gender, request.Provider, request.ExternalId);
        var alias = await db.Set<CatalogClubIdentityAlias>().SingleOrDefaultAsync(a => a.Key == key, ct);
        var target = await db.Set<CatalogClubIdentity>().SingleOrDefaultAsync(c => c.Id == request.InternalClubId, ct) ?? throw new ArgumentException("Equipe interna não encontrada.");
        if (target.CountryCode != request.CountryCode || target.Gender != request.Gender) throw new ArgumentException("Não unir países ou categorias diferentes.");
        if (alias == null) { alias = new() { Key = key, CountryCode = request.CountryCode, Gender = request.Gender, Provider = request.Provider, ExternalId = request.ExternalId, ClubId = target.Id }; db.Add(alias); }
        Guid previous = alias.ClubId; alias.ClubId = target.Id; alias.ReviewEvidence = request.Evidence;
        var conflicts = await db.Set<CatalogIdentityConflict>().Where(c => c.LeftClubId == previous && c.RightClubId == target.Id || c.RightClubId == previous && c.LeftClubId == target.Id).ToListAsync(ct);
        foreach (var conflict in conflicts) { conflict.Status = "Mapped"; conflict.Evidence = request.Evidence; }
        await db.SaveChangesAsync(ct);
    }
}
