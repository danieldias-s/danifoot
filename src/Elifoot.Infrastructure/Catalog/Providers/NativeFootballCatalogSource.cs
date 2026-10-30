using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Infrastructure.Catalog.Providers;

/// <summary>One ICatalogSource per country/category/league/season/data type, on the existing sync pipeline.</summary>
public sealed class NativeFootballCatalogSource(ProviderHttp http, NativeProviderOptions options, ProviderCompetition competition,
    CatalogDataType dataType) : ICatalogSource
{
    public string ProviderId => options.Kind switch { FootballProviderKind.ApiFootball => "api-football-v3", FootballProviderKind.Sportmonks => "sportmonks-v3", _ => "football-data-v4" };
    public TimeSpan? FetchTimeout => TimeSpan.FromSeconds(Math.Clamp(options.MaxFetchSeconds, 5, 120));
    public CatalogScopeKey Scope => new(competition.CountryCode, competition.Gender, competition.LeagueId, competition.Season, dataType, ProviderId);
    private DateTime _fetched;
    public async Task<CatalogImportPackage> FetchAsync(CancellationToken ct)
    {
        if (options.Kind == FootballProviderKind.Wikidata) throw new ArgumentException("Utilize o adapter Wikidata.");
        if (string.IsNullOrWhiteSpace(competition.ReviewEvidence) || string.IsNullOrWhiteSpace(competition.LeagueId) ||
            string.IsNullOrWhiteSpace(competition.ProviderSeasonId)) throw new ArgumentException("Revise o vínculo de competição, país, categoria e temporada.");
        if (!options.FixtureData && options.Rights is not { AccessAllowed: true, StorageAllowed: true }) throw new ArgumentException("Direitos do fornecedor não configurados.");
        _fetched = DateTime.UtcNow;
        var budget = new ProviderRequestBudget(Math.Clamp(options.MaxRequestsPerFetch, 1, 512));
        var teams = await Teams(budget, ct);
        List<CatalogTeam> clubs = []; List<CatalogAthlete> players = []; List<CatalogRank> ranks = []; List<CatalogRosterReport> reports = [];
        if (dataType == CatalogDataType.Clubs)
            clubs = teams.Select(t => new CatalogTeam(t.Required("id"), t.Required("name"),
                t.Text(options.Kind == FootballProviderKind.Sportmonks ? "short_code" : "tla") is { Length: > 0 } shortName ? shortName : t.Required("name")[..Math.Min(5, t.Required("name").Length)], competition.LeagueId, competition.LeagueName)).ToList();
        else if (dataType == CatalogDataType.Squads)
        {
            foreach (var team in teams)
            {
                var teamPlayers = await Squad(team.Required("id"), budget, ct);
                var issues = new List<string>(); var usable = new List<CatalogAthlete>();
                foreach (var raw in teamPlayers)
                {
                    try { usable.Add(MapPlayer(raw, team.Required("id"))); }
                    catch (ArgumentException e) { issues.Add(e.Message); }
                }
                if (usable.Select(p => p.PlayerExternalId).Distinct().Count() != usable.Count) throw new ArgumentException("Jogador repetido na resposta do fornecedor.");
                players.AddRange(usable);
                bool complete = usable.Count >= 11 && usable.Any(p => p.Position == "G") && usable.Count(p => p.Position != "G") >= 10 && usable.Any(p => p.Position == "D");
                if (!complete) issues.Add("Elenco parcial; completar exige autorização explícita do jogador.");
                reports.Add(new(team.Required("id"), ProviderId, teamPlayers.Count, usable.Count, complete, issues.Distinct().ToList()));
            }
        }
        else if (dataType == CatalogDataType.Rankings) ranks = await DomesticStandings(budget, ct);
        else throw new ArgumentException("Adapter nativo suporta clubes, elencos e classificações domésticas.");
        int count = dataType == CatalogDataType.Clubs ? clubs.Count : dataType == CatalogDataType.Squads ? players.Count : ranks.Count;
        var rawPackage = new CatalogImportPackage(Scope, competition.CountryName, "pending", _fetched, true, 1, 1, count,
            options.FixtureData || options.Rights!.StorageAllowed, options.FixtureData || options.Rights!.CommercialUseAllowed,
            options.Rights?.Evidence ?? "Synthetic provider protocol fixture", options.FixtureData, clubs, players, ranks,
            Rights: options.Rights, AllowPartialSquads: dataType == CatalogDataType.Squads, RosterReports: reports.Count == 0 ? null : reports,
            RosterAsOf: competition.AsOf ?? DateOnly.FromDateTime(_fetched));
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rawPackage, CatalogImportCodec.Json)));
        return rawPackage with { Version = ProviderId + ":" + hash };
    }
    private async Task<List<JsonElement>> ApiRows(string path, ProviderRequestBudget budget, CancellationToken ct)
    {
        var rows = new List<JsonElement>(); int total = 1;
        for (int page = 1; page <= total; page++)
        {
            if (page > Math.Clamp(options.MaxPages, 1, 128)) throw new ArgumentException("Paginação excedeu limite configurado.");
            var root = await http.Get(path + (path.Contains('?') ? "&" : "?") + "page=" + page, budget, ct);
            var current = root.Child("paging").Child("current").Integer(); var pages = root.Child("paging").Child("total").Integer();
            if (current != page || pages is null or < 1 || page > 1 && pages != total) throw new ArgumentException("Paginação incompleta/inconsistente.");
            total = pages.Value; var batch = root.Child("response").Rows();
            if (root.Child("results").Integer() != batch.Count || total > 1 && batch.Count == 0) throw new ArgumentException("Contagem de resposta inconsistente.");
            rows.AddRange(batch);
        }
        return rows;
    }
    private async Task<List<JsonElement>> SportRows(string path, ProviderRequestBudget budget, CancellationToken ct)
    {
        var rows = new List<JsonElement>();
        for (int page = 1; ; page++)
        {
            if (page > Math.Clamp(options.MaxPages, 1, 128)) throw new ArgumentException("Paginação excedeu limite configurado.");
            var root = await http.Get(path + (path.Contains('?') ? "&" : "?") + "page=" + page, budget, ct);
            if (root.Child("data").ValueKind != JsonValueKind.Array) throw new ArgumentException("Resposta Sportmonks sem data[].");
            var batch = root.Child("data").Rows(); rows.AddRange(batch);
            var pagination = root.Child("pagination");
            if (pagination.ValueKind == JsonValueKind.Undefined) break;
            if (pagination.Child("current_page").Integer() != page || batch.Count == 0) throw new ArgumentException("Página Sportmonks incompleta.");
            if (pagination.Child("has_more").ValueKind == JsonValueKind.False) break;
            if (pagination.Child("has_more").ValueKind != JsonValueKind.True) throw new ArgumentException("Fim da paginação não informado.");
        }
        return rows;
    }
    private async Task<List<JsonElement>> Teams(ProviderRequestBudget budget, CancellationToken ct)
    {
        List<JsonElement> result;
        if (options.Kind == FootballProviderKind.ApiFootball)
        {
            var leagues = await ApiRows($"leagues?id={Uri.EscapeDataString(competition.LeagueId)}&season={Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct);
            if (leagues.Count != 1 || leagues[0].Child("league").Text("id") != competition.LeagueId ||
                leagues[0].Child("league").Text("name") != competition.LeagueName ||
                leagues[0].Child("country").Text("name") != competition.ProviderCountryName ||
                !leagues[0].Child("seasons").Rows().Any(s => s.Text("year") == competition.ProviderSeasonId)) throw new ArgumentException("Competição/categoria revisada não corresponde ao fornecedor.");
            result = (await ApiRows($"teams?league={Uri.EscapeDataString(competition.LeagueId)}&season={Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct)).Select(r => r.Child("team")).ToList();
            if (result.Any(t => t.Child("national").ValueKind == JsonValueKind.True)) throw new ArgumentException("Seleções nacionais não são clubes.");
        }
        else if (options.Kind == FootballProviderKind.Sportmonks)
        {
            result = await SportRows($"teams/seasons/{Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct);
            string expectedGender = competition.Gender == FootballGender.Male ? "male" : "female";
            if (result.Any(t => t.Text("gender") != expectedGender || t.Text("country_id") != competition.ProviderCountryId)) throw new ArgumentException("Sportmonks retornou país/categoria diferente.");
        }
        else
        {
            var root = await http.Get($"competitions/{Uri.EscapeDataString(competition.LeagueId)}/teams?season={Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct);
            if (root.Child("competition").Text("id") != competition.LeagueId || !root.Child("season").Text("startDate").StartsWith(competition.ProviderSeasonId + "-")) throw new ArgumentException("Temporada/competição divergente.");
            result = root.Child("teams").Rows();
            if (root.Child("count").Integer() != result.Count) throw new ArgumentException("Lista de equipes incompleta.");
        }
        if (result.Count == 0 || result.Select(t => t.Required("id")).Distinct().Count() != result.Count) throw new ArgumentException("Lista de clubes vazia ou duplicada.");
        return result;
    }
    private async Task<List<JsonElement>> Squad(string team, ProviderRequestBudget budget, CancellationToken ct)
    {
        if (options.Kind == FootballProviderKind.ApiFootball)
            return await ApiRows($"players?team={team}&league={Uri.EscapeDataString(competition.LeagueId)}&season={Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct);
        if (options.Kind == FootballProviderKind.Sportmonks)
            return await SportRows($"squads/seasons/{Uri.EscapeDataString(competition.ProviderSeasonId)}/teams/{team}?include=player;position", budget, ct);
        // football-data's Team squad is current-only. Never label it as a historical squad.
        var metadata = await http.Get($"competitions/{Uri.EscapeDataString(competition.LeagueId)}", budget, ct);
        if (!metadata.Child("currentSeason").Text("startDate").StartsWith(competition.ProviderSeasonId + "-")) throw new ArgumentException("football-data.org só disponibiliza o elenco atual nesta rota.");
        var root = await http.Get($"teams/{team}", budget, ct);
        if (root.Text("id") != team) throw new ArgumentException("Elenco de outro clube.");
        return root.Child("squad").Rows();
    }
    private CatalogAthlete MapPlayer(JsonElement raw, string team)
    {
        JsonElement person = raw, statistics = default; string position;
        if (options.Kind == FootballProviderKind.ApiFootball)
        {
            person = raw.Child("player");
            var relevant = raw.Child("statistics").Rows().Where(s => s.Child("team").Text("id") == team &&
                s.Child("league").Text("id") == competition.LeagueId && s.Child("league").Text("season") == competition.ProviderSeasonId).ToList();
            if (relevant.Count != 1) throw new ArgumentException("Estatística do atleta não corresponde univocamente ao clube/liga/temporada.");
            statistics = relevant[0]; position = statistics.Child("games").Text("position");
        }
        else if (options.Kind == FootballProviderKind.Sportmonks)
        {
            if (raw.Text("team_id") != team || raw.Text("season_id") != competition.ProviderSeasonId) throw new ArgumentException("Vínculo de clube/temporada divergente.");
            person = raw.Child("player"); position = raw.Child("position").Text("name");
        }
        else position = person.Text("position");
        string name = person.Text("name"); if (name.Length == 0) name = person.Required("display_name");
        var performance = options.Kind == FootballProviderKind.ApiFootball ? new CatalogPlayerPerformance(
            statistics.Child("games").Child("rating").Number(), statistics.Child("games").Child("minutes").Integer(),
            statistics.Child("games").Child("appearences").Integer(), statistics.Child("goals").Child("total").Integer(),
            statistics.Child("goals").Child("assists").Integer(), "api-football-rating") : null;
        return new(person.Required("id"), team, ProviderId, competition.Gender, competition.Season, name,
            CatalogImportCodec.Position(position), 25, ProviderId, _fetched, IdentityOrigin: options.FixtureData ? CatalogDataOrigin.Fixture : CatalogDataOrigin.Real,
            StrengthEstimate: new(InitialPlayerStrength.Version + ":pending", CatalogDataOrigin.Estimated, .2, "pending-preview-calibration", .5, null),
            Performance: performance, OriginalPosition: position);
    }
    private async Task<List<CatalogRank>> DomesticStandings(ProviderRequestBudget budget, CancellationToken ct)
    {
        List<JsonElement> rows;
        if (options.Kind == FootballProviderKind.ApiFootball)
        {
            var data = await ApiRows($"standings?league={Uri.EscapeDataString(competition.LeagueId)}&season={Uri.EscapeDataString(competition.ProviderSeasonId)}", budget, ct);
            if (data.Count != 1) throw new ArgumentException("Classificação sem competição única.");
            var groups = data[0].Child("league").Child("standings").Rows();
            if (groups.Count != 1) throw new ArgumentException("Fases/grupos múltiplos precisam de seleção revisada; não são ranking mundial.");
            rows = groups[0].Rows();
        }
        else if (options.Kind == FootballProviderKind.FootballData)
        {
            var data = await http.Get($"competitions/{competition.LeagueId}/standings?season={competition.ProviderSeasonId}", budget, ct);
            var total = data.Child("standings").Rows().Where(x => x.Text("type") == "TOTAL").ToList();
            if (total.Count != 1) throw new ArgumentException("Tabela doméstica ambígua."); rows = total[0].Child("table").Rows();
        }
        else throw new ArgumentException("Ranking Sportmonks não homologado; use importação licenciada de ranking ou ausência explícita.");
        return rows.Select(r => new CatalogRank(r.Child("team").Required("id"), ProviderId, competition.Gender, competition.Season,
            r.Child(options.Kind == FootballProviderKind.ApiFootball ? "rank" : "position").Integer() ?? throw new ArgumentException("Posição ausente."),
            ProviderId, _fetched, r.Child("points").Number(), "DomesticStanding-v1", 1,
            $"domestic:{ProviderId}:{competition.LeagueId}:{competition.Season}:{competition.Gender}", options.FixtureData ? CatalogDataOrigin.Fixture : CatalogDataOrigin.Real)).ToList();
    }
}
