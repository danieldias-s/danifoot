using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Infrastructure.Catalog.Providers;

/// <summary>Structured CC0 data only. No images, page scraping, inferred names or claims of complete official squads.</summary>
public sealed class WikidataCatalogSource(ProviderHttp entities, ProviderHttp queries, ProviderCompetition competition,
    CatalogDataType dataType, bool fixtureData = false) : ICatalogSource
{
    public TimeSpan? FetchTimeout => TimeSpan.FromSeconds(45);
    public CatalogScopeKey Scope => new(competition.CountryCode, competition.Gender, competition.LeagueId, competition.Season, dataType, "wikidata");
    public async Task<CatalogImportPackage> FetchAsync(CancellationToken ct)
    {
        if (competition.AsOf == null || competition.WikidataTeams.Count == 0 || string.IsNullOrWhiteSpace(competition.ReviewEvidence))
            throw new ArgumentException("Wikidata exige equipes/categoria revisadas e data de referência explícita.");
        var teamIds = competition.WikidataTeams.Select(t => t.Id).ToList();
        if (teamIds.Count > 20 || teamIds.Any(id => !System.Text.RegularExpressions.Regex.IsMatch(id, "^Q[1-9][0-9]*$"))) throw new ArgumentException("Lista Wikidata inválida; limite 20 equipes por escopo.");
        var budget = new ProviderRequestBudget(4); var fetched = DateTime.UtcNow;
        var root = await entities.Get("w/api.php?action=wbgetentities&ids=" + string.Join("%7C", teamIds) + "&props=labels%7Cclaims%7Cinfo&languages=en%7Cpt&format=json&maxlag=5", budget, ct);
        if (root.Child("error").ValueKind != JsonValueKind.Undefined) throw new ArgumentException("Wikidata API indisponível/maxlag; não ativar snapshot.");
        var teams = new List<CatalogTeam>(); var revisions = new List<string>();
        foreach (var configured in competition.WikidataTeams)
        {
            var entity = root.Child("entities").Child(configured.Id);
            bool Has(string property, string id) => entity.Child("claims").Child(property).Rows()
                .Any(c => c.Child("mainsnak").Child("datavalue").Child("value").Text("id") == id);
            string name = entity.Child("labels").Child("en").Text("value");
            if (name != configured.ExpectedName || !Has("P17", configured.CountryEntityId) || !Has("P31", configured.CategoryEntityId) || !Has("P118", competition.LeagueId))
                throw new ArgumentException("Identidade, país, categoria ou liga Wikidata divergiu do mapeamento revisado.");
            revisions.Add(configured.Id + ":" + entity.Text("lastrevid"));
            DateOnly? dissolved = DateTime.TryParse(entity.Child("claims").Child("P576").Rows().FirstOrDefault().Child("mainsnak").Child("datavalue").Child("value").Text("time").TrimStart('+'), out var dissolution) ? DateOnly.FromDateTime(dissolution) : null;
            teams.Add(new(configured.Id, name, configured.Id, competition.LeagueId, competition.LeagueName,
                Verification: fixtureData ? null : new(dissolved != null ? CatalogClubVerificationStatus.REAL_CLUB_HISTORICAL : CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,
                    $"Reviewed Wikidata {configured.Id}; country {configured.CountryEntityId}; category {configured.CategoryEntityId}; league {competition.LeagueId}; undated league association",
                    fetched, CurrentSeasonConfirmed: false, DissolvedAt: dissolved, WikidataId: configured.Id)));
        }
        List<CatalogAthlete> players = []; List<CatalogRosterReport> reports = [];
        if (dataType == CatalogDataType.Squads)
        {
            string asOf = competition.AsOf.Value.ToString("yyyy-MM-dd") + "T00:00:00Z";
            string query = "SELECT DISTINCT ?team ?player ?playerLabel ?positionLabel ?start ?end ?membership ?sex WHERE { VALUES ?team { " +
                string.Join(" ", teamIds.Select(id => "wd:" + id)) + " } ?player p:P54 ?membership . ?membership ps:P54 ?team; pq:P580 ?start . " +
                "OPTIONAL { ?membership pq:P582 ?end } FILTER(?start <= \"" + asOf + "\"^^xsd:dateTime && (!BOUND(?end) || ?end >= \"" + asOf + "\"^^xsd:dateTime)) " +
                "OPTIONAL { ?player wdt:P21 ?sex } OPTIONAL { ?player wdt:P413 ?position } SERVICE wikibase:label { bd:serviceParam wikibase:language \"en,pt\". } } ORDER BY ?team ?player LIMIT 500";
            var data = await queries.Get("sparql?format=json&query=" + Uri.EscapeDataString(query), budget, ct);
            if (data.Child("results").Child("bindings").ValueKind != JsonValueKind.Array) throw new ArgumentException("Wikidata SPARQL sem resultados válidos.");
            var rows = data.Child("results").Child("bindings").Rows();
            if (rows.Count >= 500) throw new ArgumentException("Consulta atingiu o limite; cobertura pode estar incompleta.");
            foreach (var team in teams)
            {
                var members = rows.Where(r => r.Child("team").Text("value").EndsWith("/" + team.ExternalId)).GroupBy(r => r.Child("player").Text("value")).ToList();
                var issues = new List<string>(); int usable = 0;
                foreach (var member in members)
                {
                    string id = member.Key.Split('/').Last(), name = member.First().Child("playerLabel").Text("value");
                    if (string.IsNullOrWhiteSpace(name) || name == id) { issues.Add($"{id}: nome indisponível; atleta não gerado."); continue; }
                    if (!fixtureData)
                    {
                        var sexes = member.Select(r => r.Child("sex").Text("value").Split('/').Last()).ToHashSet();
                        string[] expected = competition.Gender == FootballGender.Female ? ["Q6581072", "Q1052281"] : ["Q6581097", "Q2449503"];
                        string[] opposite = competition.Gender == FootballGender.Female ? ["Q6581097", "Q2449503"] : ["Q6581072", "Q1052281"];
                        if (!sexes.Overlaps(expected) || sexes.Overlaps(opposite)) { issues.Add($"{id}: categoria do atleta incompatível/não certificada; não importado."); continue; }
                    }
                    var positions = new HashSet<string>(); string original = string.Join(" / ", member.Select(r => r.Child("positionLabel").Text("value")).Distinct());
                    foreach (var row in member)
                    { try { positions.Add(CatalogImportCodec.Position(row.Child("positionLabel").Text("value"))); } catch (ArgumentException) { } }
                    if (positions.Count != 1) { issues.Add($"{id}: posição ausente/ambígua; revisão necessária."); continue; }
                    DateOnly? Date(string key) => DateTime.TryParse(member.First().Child(key).Text("value"), out var value) ? DateOnly.FromDateTime(value) : null;
                    players.Add(new(id, team.ExternalId, "wikidata", competition.Gender, competition.Season, name, positions.Single(), 25, "wikidata", fetched,
                        CanonicalPlayerId: "wikidata:" + id, IdentityOrigin: fixtureData ? CatalogDataOrigin.Fixture : CatalogDataOrigin.Real,
                        StrengthEstimate: new(InitialPlayerStrength.Version + ":pending", CatalogDataOrigin.Estimated, .2, "wikidata-no-individual-performance", .5, null),
                        MembershipStart: Date("start"), MembershipEnd: Date("end"), OriginalPosition: original,
                        MembershipEvidence: new(CatalogMembershipPolicy.Classify(Date("start"), Date("end"), competition.AsOf.Value),
                            member.First().Child("membership").Text("value"), "Wikidata P54 with start/end qualifiers", competition.AsOf.Value,
                            "Open end does not prove current membership"))); usable++;
                }
                var squad = players.Where(p => p.ClubExternalId == team.ExternalId && CatalogMembershipPolicy.Usable(p, competition.AsOf.Value)).ToList();
                bool complete = squad.Count >= 11 && squad.Any(p => p.Position == "G") && squad.Count(p => p.Position != "G") >= 10 && squad.Any(p => p.Position == "D");
                issues.Add("Vínculos comunitários do Wikidata não garantem o elenco oficial completo/atual.");
                reports.Add(new(team.ExternalId, "wikidata", members.Count, usable, complete, issues));
            }
        }
        else if (dataType != CatalogDataType.Clubs) throw new ArgumentException("Wikidata não fornece ranking oficial neste adapter.");
        var package = new CatalogImportPackage(Scope, competition.CountryName, "pending", fetched, true, 1, 1,
            dataType == CatalogDataType.Clubs ? teams.Count : players.Count, true, true, CatalogRightsPolicy.WikidataRights.Evidence,
            fixtureData, dataType == CatalogDataType.Clubs ? teams : [], players, [], Rights: CatalogRightsPolicy.WikidataRights,
            AllowPartialSquads: dataType == CatalogDataType.Squads, RosterReports: reports.Count > 0 ? reports : null, RosterAsOf: competition.AsOf);
        return package with { Version = "wikidata:" + string.Join(",", revisions) + ":" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(package, CatalogImportCodec.Json))) };
    }
}
