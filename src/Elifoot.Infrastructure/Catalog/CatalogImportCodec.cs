using System.Globalization;
using System.Text.Json;
using Elifoot.Contracts;
using Microsoft.VisualBasic.FileIO;

namespace Elifoot.Infrastructure.Catalog;

public static class CatalogImportCodec
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    public static string Position(string value) => value.Trim().ToUpperInvariant().Replace("-", " ").Replace("ASSOCIATION FOOTBALL ", "") switch {
        "G" or "GK" or "GOALKEEPER" or "FOOTBALL GOALKEEPER" => "G",
        "D" or "DEF" or "DEFENDER" or "DEFENCE" or "CENTRE BACK" or "CENTER BACK" or "CENTRAL DEFENDER" or "FULL BACK" or "FULLBACK" or "LEFT BACK" or "RIGHT BACK" or "WING BACK" => "D",
        "M" or "MID" or "MIDFIELDER" or "MIDFIELD" or "DEFENSIVE MIDFIELD" or "ATTACKING MIDFIELD" or "CENTRAL MIDFIELD" or "DEFENSIVE MIDFIELDER" or "CENTRAL MIDFIELDER" or "ATTACKING MIDFIELDER" => "M",
        "A" or "FW" or "ATT" or "ATTACKER" or "FORWARD" or "STRIKER" or "OFFENCE" or "CENTRE FORWARD" or "CENTER FORWARD" or "WINGER" or "LEFT WINGER" or "RIGHT WINGER" or "SECOND STRIKER" => "A",
        _ => throw new ArgumentException($"Posição desconhecida: {value}.") };
    public static CatalogImportPackage ReadJson(string json)
        => JsonSerializer.Deserialize<CatalogImportPackage>(json, Json) ?? throw new ArgumentException("Pacote vazio.");
    public static CatalogImportPackage ReadCsv(CatalogImportPackage manifest, string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv)) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new ArgumentException("CSV sem cabeçalho.");
        var countries = new List<CatalogCountryEntry>(); var leagues = new List<CatalogLeagueEntry>();
        var clubs = new List<CatalogTeam>(); var players = new List<CatalogAthlete>(); var ranks = new List<CatalogRank>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields()!;
            if (fields.Length != headers.Length) throw new ArgumentException("Linha CSV incompleta.");
            var row = headers.Select((h, i) => (h, fields[i])).ToDictionary(x => x.h.Trim('\uFEFF'), x => x.Item2, StringComparer.OrdinalIgnoreCase);
            string Get(string key) => row.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Coluna ausente: {key}.");
            string? Optional(string key) => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
            double? Number(string key) => Optional(key) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : null;
            switch (manifest.Scope.DataType)
            {
                case CatalogDataType.Countries:
                    countries.Add(new(Get("CountryCode"), Get("CountryName"))); break;
                case CatalogDataType.Leagues:
                    leagues.Add(new(Get("LeagueExternalId"), Get("LeagueName"), Enum.Parse<FootballGender>(Get("Gender"), true), Get("Season"))); break;
                case CatalogDataType.Clubs:
                    clubs.Add(new(Get("ClubExternalId"), Get("ClubName"), Get("ShortName"), Get("LeagueExternalId"), Get("LeagueName"))); break;
                case CatalogDataType.Squads:
                    players.Add(new(Get("PlayerExternalId"), Get("ClubExternalId"), Get("ClubProvider"),
                        Enum.Parse<FootballGender>(Get("Gender"), true), Get("Season"), Get("PlayerName"), Position(Get("Position")),
                        int.Parse(Get("Strength"), CultureInfo.InvariantCulture), manifest.Scope.Provider, manifest.UpdatedAt,
                        CanonicalPlayerId: Optional("CanonicalPlayerId"), IdentityOrigin: Optional("IdentityOrigin") is { } identityOrigin ? Enum.Parse<CatalogDataOrigin>(identityOrigin, true) : null,
                        StrengthEstimate: Optional("StrengthMethod") is { } method ? new(method, CatalogDataOrigin.Estimated, Number("StrengthConfidence") ?? .2,
                            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", fields)))), .5, null) : null,
                        Performance: Number("Rating") is { } rating ? new(rating, (int?)Number("Minutes"), (int?)Number("Appearances"), (int?)Number("Goals"), (int?)Number("Assists"), Optional("RatingScale")) : null,
                        MembershipStart: Optional("MembershipStart") is { } start ? DateOnly.Parse(start, CultureInfo.InvariantCulture) : null,
                        MembershipEnd: Optional("MembershipEnd") is { } end ? DateOnly.Parse(end, CultureInfo.InvariantCulture) : null,
                        OriginalPosition: Get("Position"))); break;
                case CatalogDataType.Rankings:
                    ranks.Add(new(Get("ClubExternalId"), Get("ClubProvider"), Enum.Parse<FootballGender>(Get("Gender"), true),
                        Get("Season"), int.Parse(Get("Rank"), CultureInfo.InvariantCulture), manifest.Scope.Provider, manifest.UpdatedAt, Number("Score"), Optional("Method"),
                        Number("Confidence"), Optional("ComparableGroup"), Optional("Origin") is { } origin ? Enum.Parse<CatalogDataOrigin>(origin, true) : null)); break;
            }
        }
        return manifest with { Clubs = clubs, Players = players, Rankings = ranks, CountryData = countries.Count > 0 ? countries : null, LeagueData = leagues.Count > 0 ? leagues : null };
    }
}
