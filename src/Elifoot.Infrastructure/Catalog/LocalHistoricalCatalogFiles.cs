using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Elifoot.Contracts;

namespace Elifoot.Infrastructure.Catalog;

internal sealed record LocalHistoricalCatalogScan(LocalHistoricalCatalogIndex Index, List<string> Errors);

internal static class LocalHistoricalCatalogFiles
{
    private static readonly Regex IdPattern = new(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,79}\z", RegexOptions.CultureInvariant);
    private static readonly Regex CountryCodePattern = new(@"\A[A-Z]{2}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ColorPattern = new(@"\A#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions FileJson = new()
    {
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private sealed record ClubFile(string? ClubId, string? ClubName, string? Country, string? CountryCode,
        int? CatalogYear, string? Category, int? InitialRanking, double? RankingScore,
        string? PrimaryColor, string? SecondaryColor, List<PlayerFile?>? Players,
        string? ShortName = null, string? League = null);

    private sealed record PlayerFile(string? PlayerId, string? Name, string? Position, int? Number = null,
        string? Source = null);

    public static async Task<LocalHistoricalCatalogScan> ReadAsync(string root, CancellationToken ct)
    {
        var countriesRoot = Path.GetFullPath(Path.Combine(root, "paises"));
        if (!Directory.Exists(countriesRoot))
            return new(LocalHistoricalCatalogIndex.Empty, ["O catálogo precisa conter a pasta paises."]);

        var errors = new List<string>();
        List<(string Path, string Relative)> files;
        try
        {
            files = Directory.EnumerateFiles(countriesRoot, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = false
                })
                .Where(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .Select(path => (Path: path, Relative: Path.GetRelativePath(countriesRoot, path).Replace('\\', '/')))
                .OrderBy(file => file.Relative, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(LocalHistoricalCatalogIndex.Empty, ["Não foi possível listar os arquivos da pasta paises."]);
        }

        var clubs = new List<(ClubFile File, string Path, FootballGender Category, string Year)>();
        var clubKeys = new HashSet<string>(StringComparer.Ordinal);
        var countriesByCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var countryCodesByFolder = new Dictionary<string, string>(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(file.Path, ct);
                HashFile(hash, file.Relative, bytes);
                var club = ReadClub(bytes);
                if (!ValidateClub(club, file.Relative, errors, out var category, out string year))
                    continue;

                string countryFolder = NormalizeCountry(club.Country!);
                if (countriesByCode.TryGetValue(club.CountryCode!, out string? country) && country != club.Country)
                {
                    errors.Add($"{file.Relative}: CountryCode já pertence a {country}. Use o mesmo nome do país em todos os arquivos.");
                    continue;
                }
                if (countryCodesByFolder.TryGetValue(countryFolder, out string? code) && code != club.CountryCode)
                {
                    errors.Add($"{file.Relative}: a pasta do país já usa o código {code}.");
                    continue;
                }
                countriesByCode[club.CountryCode!] = club.Country!;
                countryCodesByFolder[countryFolder] = club.CountryCode!;

                string key = ClubKey(club, year, category);
                if (!clubKeys.Add(key))
                {
                    errors.Add($"{file.Relative}: versão de clube duplicada ({key}).");
                    continue;
                }
                clubs.Add((club, file.Relative, category, year));
            }
            catch (JsonException error)
            {
                errors.Add($"{file.Relative}: JSON inválido em {error.Path ?? "$"} (linha {(error.LineNumber ?? 0) + 1}).");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{file.Relative}: não foi possível ler o arquivo.");
            }
        }

        string digest = Convert.ToHexString(hash.GetHashAndReset());
        string version = $"local-historical-v1-{digest}";
        var indexedAt = DateTime.UtcNow;
        var index = new LocalHistoricalCatalogIndex(version, digest, indexedAt, files.Count,
            clubs.Select(c => Project(c.File, c.Path, c.Category, c.Year, version, digest, indexedAt)).ToList());
        return new(index, errors);
    }

    private static ClubFile ReadClub(byte[] bytes)
    {
        int bom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? Encoding.UTF8.Preamble.Length : 0;
        return JsonSerializer.Deserialize<ClubFile>(bytes.AsSpan(bom), FileJson) ?? throw new JsonException();
    }

    private static void HashFile(IncrementalHash hash, string path, byte[] bytes)
    {
        var pathBytes = Encoding.UTF8.GetBytes(path);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, pathBytes.Length);
        hash.AppendData(length);
        hash.AppendData(pathBytes);
        hash.AppendData(SHA256.HashData(bytes));
    }

    private static bool ValidateClub(ClubFile club, string path, List<string> errors,
        out FootballGender category, out string year)
    {
        int initialErrors = errors.Count;
        var parts = path.Split('/');
        var parsedCategory = club.Category?.Trim().ToUpperInvariant() switch
        {
            "MASCULINO" => FootballGender.Male,
            "FEMININO" => FootballGender.Female,
            _ => (FootballGender?)null
        };
        category = parsedCategory.GetValueOrDefault();
        year = club.CatalogYear?.ToString(CultureInfo.InvariantCulture) ?? "";

        void Add(string message) => errors.Add($"{path}: {message}");
        if (parts.Length != 4 || !parts[2].Equals("times", StringComparison.Ordinal))
            Add("use a estrutura PAÍS/ANO/times/arquivo.json.");
        else
        {
            if (club.Country is not { Length: > 0 and <= 80 } country || !ValidText(country, 80) ||
                country != country.Trim() || !NormalizeCountry(country).Equals(parts[0], StringComparison.OrdinalIgnoreCase))
                Add("Country precisa corresponder à pasta do país.");
            if (club.CatalogYear is not > 0 || year != parts[1])
                Add("CatalogYear precisa ser um inteiro positivo e corresponder à pasta do ano.");
        }
        if (club.CountryCode is null || !CountryCodePattern.IsMatch(club.CountryCode))
            Add("CountryCode deve conter duas letras maiúsculas.");
        if (club.ClubId is null || !IdPattern.IsMatch(club.ClubId))
            Add("ClubId inválido. Use até 80 letras ASCII, dígitos, hífens ou sublinhados.");
        if (!ValidText(club.ClubName, 160))
            Add("ClubName deve conter de 1 a 160 caracteres.");
        if (club.ShortName is not null && !ValidText(club.ShortName, 80))
            Add("ShortName deve conter de 1 a 80 caracteres.");
        if (club.League is not null && !ValidText(club.League, 160))
            Add("League deve conter de 1 a 160 caracteres.");
        if (parsedCategory is null)
            Add("Category deve ser Masculino ou Feminino.");
        if (club.InitialRanking is not > 0)
            Add("InitialRanking deve ser um inteiro positivo.");
        if (club.RankingScore is not double score || !double.IsFinite(score))
            Add("RankingScore deve ser um número finito.");
        if (club.PrimaryColor is null || !ColorPattern.IsMatch(club.PrimaryColor))
            Add("PrimaryColor deve usar o formato #RRGGBB.");
        if (club.SecondaryColor is null || !ColorPattern.IsMatch(club.SecondaryColor))
            Add("SecondaryColor deve usar o formato #RRGGBB.");
        if (club.Players is null)
            Add("Players deve ser uma lista de jogadores. Use [] quando o elenco estiver pendente.");
        else
            ValidatePlayers(club.Players, Add);

        return errors.Count == initialErrors;
    }

    private static void ValidatePlayers(List<PlayerFile?> players, Action<string> add)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            string label = $"Players[{i}]";
            if (player is null)
            {
                add($"{label} deve ser um objeto.");
                continue;
            }
            if (player.PlayerId is null || !IdPattern.IsMatch(player.PlayerId))
                add($"{label}.PlayerId inválido.");
            else if (!ids.Add(player.PlayerId))
                add($"{label}.PlayerId duplicado no elenco ({player.PlayerId}).");
            if (!ValidText(player.Name, 160))
                add($"{label}.Name deve conter de 1 a 160 caracteres.");
            if (player.Position?.Trim().ToUpperInvariant() is not ("G" or "D" or "M" or "A"))
                add($"{label}.Position deve ser G, D, M ou A.");
            if (player.Number is < 0 or > 999)
                add($"{label}.Number deve estar entre 0 e 999.");
            if (player.Source is not null && !ValidText(player.Source, 500))
                add($"{label}.Source deve conter de 1 a 500 caracteres.");
        }
    }

    private static bool ValidText(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max && !value.Any(char.IsControl);

    private static string NormalizeCountry(string value)
    {
        string withoutAccents = string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        return Regex.Replace(withoutAccents.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
    }

    private static string ClubKey(ClubFile club, string year, FootballGender category) =>
        LocalHistoricalCatalogSource.ClubKey(club.CountryCode!, year, club.ClubId!.ToUpperInvariant(), category);

    private static EligibleCatalogClub Project(ClubFile club, string file, FootballGender category, string year,
        string version, string digest, DateTime indexedAt)
    {
        const string provider = LocalHistoricalCatalogSource.Provider;
        string key = ClubKey(club, year, category);
        string league = club.League?.Trim() ?? "Catálogo local";
        var referenceId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{digest}:{key}")).AsSpan(0, 16));
        var scope = new CatalogScopeKey(club.CountryCode!, category, league, year, CatalogDataType.Squads, provider);
        string evidence = $"Cadastro local da época em paises/{file}";
        var reference = new CatalogReference(referenceId, scope, version, indexedAt, indexedAt, false, "Active", null);
        var rank = new CatalogRank(key, provider, category, year, club.InitialRanking!.Value,
            "Ranking inicial do catálogo local", indexedAt, club.RankingScore, "LocalHistoricalInitialRanking",
            1, provider, CatalogDataOrigin.Estimated);
        var players = club.Players!.Select(entry =>
        {
            var player = entry!;
            string playerId = player.PlayerId!.ToUpperInvariant();
            string playerKey = LocalHistoricalCatalogSource.PlayerKey(key, playerId);
            return new CatalogAthlete(playerId, key, provider, category, year, player.Name!.Trim(),
                player.Position!.Trim().ToUpperInvariant(), 1, player.Source?.Trim() ?? provider,
                indexedAt, playerKey, CatalogDataOrigin.Real, Number: player.Number);
        }).ToList();
        return new EligibleCatalogClub(key, key, provider, club.ClubName!.Trim(), club.ShortName?.Trim() ?? club.ClubName.Trim(),
            club.CountryCode!, club.Country!, category, league, year, club.PrimaryColor!.ToUpperInvariant(),
            club.SecondaryColor!.ToUpperInvariant(), club.InitialRanking, players, [reference], RankingInfo: rank,
            RawRosterCount: players.Count, Verification: new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED, evidence,
                indexedAt, year), DocumentedPlayers: players.Count, SourceRanking: rank);
    }
}
