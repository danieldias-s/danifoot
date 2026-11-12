using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Infrastructure.Catalog;

public sealed partial class OfflineCatalogLoader
{
    private sealed record IdentityAsset(OfflineIdentityDefinition Definition, byte[] Bytes, byte[] Image, string? DataUrl);

    // Scoped to one scan: shared between all years, never stale after an operator edits a file.
    private async Task<Dictionary<string, IdentityAsset>> LoadIdentities(List<OfflineCatalogIssue> issues, CancellationToken ct)
    {
        var cache = new Dictionary<string, IdentityAsset>(StringComparer.OrdinalIgnoreCase);
        string root = SafePath(Root, "identidades");
        if (!Directory.Exists(root)) return cache;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            string folder = Path.GetFileName(directory);
            try
            {
                byte[] bytes = await Read(SafePath(root, folder + "/identidade.json"), 1_000_000, ct);
                var identity = JsonSerializer.Deserialize<OfflineIdentityDefinition>(bytes, Json) ?? throw new JsonException("Identidade vazia.");
                if (!OfflineCatalogRules.Id(identity.CatalogClubId) || identity.CatalogClubId != folder || !seen.Add(identity.CatalogClubId))
                    throw new ArgumentException("catalogClubId deve ser único e igual à pasta estável da identidade.");
                if (identity.SchemaVersion != "1.1" || !OfflineCatalogRules.Text(identity.Name) ||
                    !OfflineCatalogRules.Text(identity.ShortName, 80) || !OfflineCatalogRules.Text(identity.Abbreviation, 12) ||
                    identity.Colors is not { Count: 2 } || identity.Colors.Any(c => c == null || !Regex.IsMatch(c, @"\A#[0-9A-Fa-f]{6}\z")))
                    throw new ArgumentException("Identidade 1.1 exige nomes, sigla e exatamente duas cores #RRGGBB.");
                byte[] image = []; string? url = null;
                if (identity.CrestFile != null)
                {
                    image = await Read(SafePath(directory, identity.CrestFile), 2_000_000, ct);
                    string extension = Path.GetExtension(identity.CrestFile).ToLowerInvariant();
                    bool png = extension == ".png" && image.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10});
                    bool jpeg = extension is ".jpg" or ".jpeg" && image.AsSpan().StartsWith(new byte[] {255,216,255});
                    if (!png && !jpeg) throw new ArgumentException("Escudo local deve ser PNG/JPEG válido.");
                    url = $"data:image/{(png ? "png" : "jpeg")};base64,{Convert.ToBase64String(image)}";
                }
                cache.Add(identity.CatalogClubId, new(identity, bytes, image, url));
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            { issues.Add(new("IDENTITY", "identidades/" + folder, e.Message)); }
        }
        return cache;
    }

    private static OfflineClubSource ResolveV11(OfflineClubV11 annual, OfflineManifest manifest, string path, byte[] bytes,
        Dictionary<string, IdentityAsset> identities, List<OfflineCatalogIssue> issues)
    {
        if (annual.CatalogClubId == null || !identities.TryGetValue(annual.CatalogClubId, out var asset))
            throw new ArgumentException("Identidade global ausente ou inválida: " + annual.CatalogClubId);
        if (annual.CatalogClubId != asset.Definition.CatalogClubId) throw new ArgumentException("Referência de identidade deve manter a grafia canônica.");
        var identity = asset.Definition;
        var club = new OfflineClub("1.1", annual.CatalogClubId, annual.Country, annual.Year, identity.Name,
            identity.ShortName, identity.Abbreviation, identity.Colors, annual.Stadium, annual.Coach,
            annual.Players?.Select(p => p == null ? null! : new OfflinePlayer(p.Id, p.Name, p.Position, null, p.Nationality, p.Behavior, p.IsStar)).ToList(), annual.Status);
        bool ready = annual.Status == "READY";
        void Check(bool valid, string code, string message, bool structural = false)
        { if (!valid) issues.Add(new(code, path, message, structural || ready)); }
        issues.AddRange(OfflineCatalogRules.ValidateClub(club, path));
        Check(annual.Country == manifest.Country?.Code && annual.Year == manifest.Year, "SCOPE", "País/ano do clube diverge do manifest.", true);
        Check(manifest.Status != "READY" || ready, "DRAFT_CLUB", "Manifest READY contém rascunho.", true);
        Check(annual.TeamStrength is >= 1 and <= 20, "TEAM_STRENGTH", "teamStrength obrigatório: 1..20.", annual.TeamStrength != null);
        Check(annual.TieBreakPriority == null || annual.TieBreakPriority >= 0, "PRIORITY", "tieBreakPriority deve ser inteiro não negativo.", true);
        var ranking = annual.HistoricalRanking;
        Check(ranking == null || ranking.Rating is double rating && double.IsFinite(rating) && OfflineCatalogRules.Text(ranking.Source) &&
            ranking.ReferenceDate is DateOnly date && date.Year == annual.Year,
            "RANKING", "Ranking informado exige rating, fonte/escala e data no ano correspondente.", true);
        Check(ranking != null || annual.TieBreakPriority != null, "RANKING_RESOLUTION", "Sem ranking confiável, informe prioridade manual; empates nas vagas ainda serão validados.");
        string hash = Hash(bytes.Concat(asset.Bytes).Concat(asset.Image).ToArray());
        return new(manifest.CatalogId!, annual.Country ?? "", manifest.Country!.Name!, annual.Year ?? 0, hash, club, asset.DataUrl,
            new(identity, annual.TeamStrength, ranking, annual.TieBreakPriority));
    }
}
