using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Infrastructure.Catalog;

/// <summary>File-only reader. Never opens a database, indexes the QA catalog, or performs network requests.</summary>
public sealed class OfflineCatalogLoader
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false, NumberHandling = JsonNumberHandling.Strict, WriteIndented = true
    };
    public string Root { get; }
    public OfflineCatalogLoader(string root) => Root = Path.GetFullPath(root);
    public static string ClubKey(OfflineClubSource source) => $"offline:{source.CountryCode}:{source.Year}:{source.Definition.CatalogClubId}";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public async Task<OfflineCatalogReport> LoadAsync(CancellationToken ct = default)
    {
        var catalogs = new List<OfflineLoadedCatalog>();
        var issues = new List<OfflineCatalogIssue>();
        try
        {
            CheckTree(Root);
            if (!Directory.Exists(Root)) return new([], [new("ROOT", ".", "Pasta do catálogo não encontrada.")]);
            foreach (var country in Directory.EnumerateDirectories(Root).Order(StringComparer.Ordinal))
            {
                string slug = Path.GetFileName(country);
                if (slug.StartsWith('_') || slug == "modelos") continue;
                if ((File.GetAttributes(country) & FileAttributes.ReparsePoint) != 0)
                { issues.Add(new("PATH", slug, "Links de diretório não são permitidos.")); continue; }
                foreach (var year in Directory.EnumerateDirectories(country).Order(StringComparer.Ordinal))
                {
                    ct.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(Root, Path.Combine(year, "manifest.json")).Replace('\\', '/');
                    catalogs.Add(await LoadManifest(relative, ct));
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { issues.Add(new("ROOT", ".", e.Message)); }

        void Duplicates<T>(Func<OfflineClubSource, IEnumerable<T>> items, Func<T, OfflineClubSource, string?> key, string code)
        {
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var marked = new HashSet<int>();
            for (int i = 0; i < catalogs.Count; i++)
                foreach (var club in catalogs[i].Clubs)
                    foreach (var item in items(club))
                        if (key(item, club) is { } id)
                        {
                            if (seen.TryGetValue(id, out var first)) { marked.Add(first); marked.Add(i); }
                            else seen[id] = i;
                        }
            foreach (int i in marked) catalogs[i].Issues.Add(new(code, catalogs[i].Path, "Identidade duplicada no mesmo ano; revise os arquivos."));
        }
        Duplicates(s => new[] { s.Definition }, (c, s) => $"{s.CountryCode}:{s.Year}:{c.CatalogClubId}", "DUPLICATE_CLUB");
        Duplicates(s => s.Definition.Players?.Where(p => p != null) ?? [], (p, s) => p.Id == null ? null : $"{s.Year}:{p.Id}", "DUPLICATE_PLAYER");
        Duplicates(s => s.Definition.Coach == null ? [] : new[] { s.Definition.Coach }, (c, s) => c.Id == null ? null : $"{s.Year}:{c.Id}", "DUPLICATE_COACH");
        foreach (var group in catalogs.Where(c => c.Manifest?.CatalogId != null).GroupBy(c => c.Manifest!.CatalogId!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            foreach (var c in group) c.Issues.Add(new("DUPLICATE_CATALOG", c.Path, "catalogId repetido."));
        foreach (var group in catalogs.Where(c => c.Manifest?.Country != null).GroupBy(c => c.Manifest!.Country!.Code, StringComparer.OrdinalIgnoreCase))
            if (group.Select(c => c.Manifest!.Country!.Slug).Distinct(StringComparer.Ordinal).Count() > 1)
                foreach (var c in group) c.Issues.Add(new("COUNTRY_SLUG", c.Path, "O país deve usar uma única pasta estável."));
        return new(catalogs, issues);
    }

    private async Task<OfflineLoadedCatalog> LoadManifest(string relative, CancellationToken ct)
    {
        OfflineManifest? manifest = null;
        var clubs = new List<OfflineClubSource>();
        var issues = new List<OfflineCatalogIssue>();
        string hash = "";
        try
        {
            byte[] bytes = await Read(SafePath(Root, relative), 1_000_000, ct);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("gender", out var gender)
                && (gender.ValueKind != JsonValueKind.String || gender.GetString() is not ("Male" or "Female")))
                throw new JsonException("gender deve ser Male ou Female, conforme o schema 1.0.");
            manifest = JsonSerializer.Deserialize<OfflineManifest>(bytes, Json) ?? throw new JsonException("Manifest vazio.");
            hash = Hash(bytes);
            bool ready = manifest.Status == "READY";
            void Check(bool ok, string code, string message, bool structural = true)
            { if (!ok) issues.Add(new(code, relative, message, structural || ready)); }
            Check(manifest.SchemaVersion == OfflineCatalogRules.Version, "SCHEMA_VERSION", "Versão suportada: 1.0.");
            Check(OfflineCatalogRules.Id(manifest.CatalogId), "CATALOG_ID", "catalogId estável obrigatório.");
            Check(manifest.Status is "READY" or "DRAFT", "STATUS", "Use READY ou DRAFT.");
            Check(OfflineCatalogRules.Country(manifest.Country?.Code) && OfflineCatalogRules.Text(manifest.Country?.Name), "COUNTRY", "País ISO e nome obrigatórios.");
            var pieces = relative.Split('/');
            Check(manifest.Country?.Slug == pieces[0] && Regex.IsMatch(pieces[0], @"\A[a-z][a-z0-9-]{0,79}\z"), "COUNTRY_FOLDER", "Slug deve corresponder à pasta do país.");
            Check(manifest.Year is >= 1 and <= 9999 && manifest.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) == pieces[1], "YEAR", "Ano deve corresponder à pasta (1..9999).");
            Check(OfflineCatalogRules.Text(manifest.Name), "NAME", "Nome do catálogo obrigatório.", false);
            Check(Enum.IsDefined(manifest.Gender), "GENDER", "Categoria inválida.");
            Check(manifest.Clubs != null, "REFERENCES", "Informe a lista de arquivos de clubes.");
            Check(manifest.Clubs is { Count: > 0 }, "EMPTY", "Rascunho sem clubes; não está pronto para criar mundo.", false);
            if (manifest.Clubs is { Count: > 4096 }) throw new ArgumentException("Máximo 4096 clubes por catálogo.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string yearRoot = Path.GetDirectoryName(SafePath(Root, relative))!;
            foreach (string reference in manifest.Clubs ?? [])
            {
                try
                {
                    if (reference == null || !Regex.IsMatch(reference, @"\Aclubes/[A-Za-z0-9][A-Za-z0-9_-]{0,100}\.json\z"))
                        throw new ArgumentException("Referência deve ser clubes/arquivo.json, dentro do ano.");
                    if (!seen.Add(reference)) { issues.Add(new("DUPLICATE_REFERENCE", relative, "Arquivo de clube repetido.")); continue; }
                    byte[] clubBytes = await Read(SafePath(yearRoot, reference), 10_000_000, ct);
                    var club = JsonSerializer.Deserialize<OfflineClub>(clubBytes, Json) ?? throw new JsonException("Clube vazio.");
                    string path = relative[..relative.LastIndexOf('/')] + "/" + reference;
                    issues.AddRange(OfflineCatalogRules.ValidateClub(club, path));
                    if (club.Country != manifest.Country?.Code || club.Year != manifest.Year)
                        issues.Add(new("SCOPE", path, "País/ano do clube diverge do manifest."));
                    if (ready && club.Status != "READY") issues.Add(new("DRAFT_CLUB", path, "Manifest READY contém clube em rascunho."));
                    string? crest = null; byte[] crestBytes = [];
                    if (club.CrestFile != null)
                    {
                        crestBytes = await Read(SafePath(yearRoot, club.CrestFile), 2_000_000, ct);
                        string extension = Path.GetExtension(club.CrestFile).ToLowerInvariant();
                        bool png = extension == ".png" && crestBytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                        bool jpeg = extension is ".jpg" or ".jpeg" && crestBytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
                        if (!png && !jpeg) throw new ArgumentException("Escudo deve ser PNG ou JPEG local válido.");
                        crest = $"data:image/{(png ? "png" : "jpeg")};base64,{Convert.ToBase64String(crestBytes)}";
                    }
                    clubs.Add(new(manifest.CatalogId ?? "", club.Country ?? "", manifest.Country?.Name ?? "", club.Year ?? 0,
                        Hash(clubBytes.Concat(crestBytes).ToArray()), club, crest));
                }
                catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
                { issues.Add(new("CLUB_FILE", relative + ":" + reference, e.Message)); }
            }
            hash = Hash(Encoding.UTF8.GetBytes(hash + string.Join("|", clubs.Select(c => c.SourceHash))));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        { issues.Add(new("MANIFEST_FILE", relative, e.Message)); }
        return new(relative, manifest, clubs, issues, hash);
    }

    private static async Task<byte[]> Read(string path, long limit, CancellationToken ct)
    {
        if (new FileInfo(path).Length > limit) throw new ArgumentException("Arquivo excede o limite de tamanho.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (bytes.LongLength > limit) throw new ArgumentException("Arquivo excede o limite de tamanho.");
        return bytes;
    }
    private static string SafePath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\\') ||
            relative.Split('/').Any(p => p is ".." or "." or "")) throw new ArgumentException("Caminho relativo inválido.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Caminho fora da pasta do catálogo.");
        CheckTree(path);
        return path;
    }
    private static void CheckTree(string path)
    {
        for (string? part = Path.GetFullPath(path); part != null; part = Path.GetDirectoryName(part))
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Links simbólicos/reparse points não são permitidos no caminho do catálogo.");
    }
}
