using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class RealData013BTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => action(request, ct); }
    private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private static object Envelope(IEnumerable<object> rows, int page = 1, int pages = 1)
    { var data = rows.ToArray(); return new { errors = Array.Empty<string>(), results = data.Length, paging = new { current = page, total = pages }, response = data }; }
    private static ProviderCompetition Competition(FootballGender gender) => new() { CountryCode = "BR", CountryName = "Brasil", Gender = gender,
        Season = "2026", LeagueId = "201", LeagueName = "Fixture league 013B", ProviderSeasonId = "2026", ProviderCountryId = "32",
        ProviderCountryName = "Brazil", ReviewEvidence = "Synthetic protocol fixture; no live commercial data", AsOf = new(2026, 10, 1) };
    private static List<object> ApiPlayers(int count = 35) => Enumerable.Range(0, count).Select(i => (object)new {
        player = new { id = 1000 + i, name = $"Atleta fictício protocolo {i}" }, statistics = new[] { new {
            team = new { id = 101 }, league = new { id = 201, season = 2026 },
            games = new { position = i < 2 ? "Goalkeeper" : i < 12 ? "Defender" : i < 24 ? "Midfielder" : "Attacker", rating = (5 + (i % 10) * .3).ToString(System.Globalization.CultureInfo.InvariantCulture), minutes = 100 + i * 30, appearences = 10 },
            goals = new { total = i % 4, assists = i % 3 } } } }).ToList();
    [Theory]
    [InlineData(FootballProviderKind.ApiFootball, FootballGender.Male)] [InlineData(FootballProviderKind.ApiFootball, FootballGender.Female)]
    [InlineData(FootballProviderKind.Sportmonks, FootballGender.Male)] [InlineData(FootballProviderKind.Sportmonks, FootballGender.Female)]
    [InlineData(FootballProviderKind.FootballData, FootballGender.Male)] [InlineData(FootballProviderKind.FootballData, FootballGender.Female)]
    public async Task NativeProviderProtocolsImportGenderSeasonSquadsAndNoMedia(FootballProviderKind kind, FootballGender gender)
    {
        var config = Competition(gender); int requests = 0;
        using var client = new HttpClient(new Handler((request, _) => {
            requests++; string path = request.RequestUri!.AbsolutePath, query = request.RequestUri.Query;
            string authHeader = kind == FootballProviderKind.ApiFootball ? "x-apisports-key" : kind == FootballProviderKind.Sportmonks ? "Authorization" : "X-Auth-Token";
            Assert.Equal("fixture-secret013b", request.Headers.GetValues(authHeader).Single()); Assert.DoesNotContain("fixture-secret", query);
            if (kind == FootballProviderKind.ApiFootball)
            {
                if (path.EndsWith("/leagues")) return Task.FromResult(Json(Envelope([new { league = new { id = 201, name = config.LeagueName }, country = new { name = "Brazil" }, seasons = new[] { new { year = 2026 } } }])));
                if (path.EndsWith("/teams")) return Task.FromResult(Json(Envelope([new { team = new { id = 101, name = "Clube fictício protocolo", national = false, logo = "https://unlicensed.invalid/logo.png" } }])));
                int page = query.Contains("page=2") ? 2 : 1;
                return Task.FromResult(Json(Envelope(page == 1 ? ApiPlayers().Take(20) : ApiPlayers().Skip(20), page, 2)));
            }
            if (kind == FootballProviderKind.Sportmonks)
            {
                if (path.Contains("teams/seasons")) return Task.FromResult(Json(new { data = new[] { new { id = 101, name = "Clube fictício protocolo", country_id = 32, gender = gender == FootballGender.Male ? "male" : "female" } } }));
                return Task.FromResult(Json(new { data = Enumerable.Range(0, 18).Select(i => new { player_id = 1000 + i, team_id = 101, season_id = 2026,
                    player = new { id = 1000 + i, display_name = $"Atleta fictício {i}" }, position = new { name = i < 2 ? "Goalkeeper" : i < 8 ? "Defender" : "Midfielder" } }).ToList() }));
            }
            if (path.EndsWith("/competitions/201/teams")) return Task.FromResult(Json(new { competition = new { id = 201 }, season = new { startDate = "2026-01-01" }, count = 1,
                teams = new[] { new { id = 101, name = "Clube fictício protocolo", tla = "FIX" } } }));
            if (path.EndsWith("/competitions/201")) return Task.FromResult(Json(new { currentSeason = new { startDate = "2026-01-01" } }));
            return Task.FromResult(Json(new { id = 101, squad = Enumerable.Range(0, 18).Select(i => new { id = 1000 + i, name = $"Atleta fictício {i}", position = i < 2 ? "Goalkeeper" : i < 8 ? "Defence" : "Midfield" }).ToList() }));
        })) { BaseAddress = new("https://fixture.invalid/") };
        var transport = new ProviderHttp(client, kind, "fixture-secret013b", 0);
        var options = new NativeProviderOptions { Kind = kind, FixtureData = true, MinimumIntervalMilliseconds = 0 };
        var clubs = await new NativeFootballCatalogSource(transport, options, config, CatalogDataType.Clubs).FetchAsync(default);
        var squads = await new NativeFootballCatalogSource(transport, options, config, CatalogDataType.Squads).FetchAsync(default);
        CatalogSyncService.Validate(clubs); CatalogSyncService.Validate(squads);
        Assert.True(squads.IsFixture); Assert.Equal(gender, squads.Scope.Gender); Assert.All(squads.Players, p => { Assert.Equal(gender, p.Gender); Assert.Equal("2026", p.Season); Assert.Equal(CatalogDataOrigin.Fixture, p.IdentityOrigin); Assert.NotNull(p.StrengthEstimate); });
        Assert.DoesNotContain("unlicensed", JsonSerializer.Serialize(clubs)); Assert.True(requests >= 2);
        await using var h = new Harness(); await h.Initialize(); await h.Sync.ImportAsync(clubs); await h.Sync.ImportAsync(squads);
        var selection = await h.Creator.Select(["BR"], gender, "2026", true); var club = Assert.Single(selection.Clubs);
        Assert.NotNull(club.InternalCatalogClubId); Assert.Equal(squads.Players.Count, club.Players.Count);
        var roster = InitialRosterPolicy.Prepare(club, 12, false); Assert.Empty(roster.Issues); Assert.InRange(roster.Players.Count, 11, 24); Assert.Contains(roster.Players, p => p.Position == "G"); Assert.Contains(roster.Players, p => p.Position == "D");
    }
    [Theory] [InlineData(401)] [InlineData(403)] [InlineData(429)] [InlineData(500)]
    public async Task TransportRedactsHttpFailuresAndRateLimits(int status)
    {
        int calls = 0; using var client = new HttpClient(new Handler((_, _) => { calls++; var result = Json(new { error = "echo-of-fixture-secret" }, (HttpStatusCode)status);
            if (status == 429) result.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1)); return Task.FromResult(result); })) { BaseAddress = new("https://fixture.invalid/") };
        var transport = new ProviderHttp(client, FootballProviderKind.ApiFootball, "fixture-secret", 0);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => transport.Get("teams", new(10), default));
        Assert.Equal((HttpStatusCode)status, error.StatusCode); Assert.DoesNotContain("fixture-secret", error.Message);
        if (status == 429) { await Assert.ThrowsAsync<HttpRequestException>(() => transport.Get("teams", new(10), default)); Assert.Equal(1, calls); }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PaginationAndHttp200ErrorsNeverActivate(bool payloadError)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(payloadError ? new { errors = new { token = "fixture-secret" } } :
            (object)new { errors = Array.Empty<string>(), results = 1, paging = new { current = 2, total = 1 }, response = new[] { new { id = 1 } } })))) { BaseAddress = new("https://fixture.invalid/") };
        var source = new NativeFootballCatalogSource(new(client, FootballProviderKind.ApiFootball, "fixture-secret", 0),
            new() { Kind = FootballProviderKind.ApiFootball, FixtureData = true }, Competition(FootballGender.Male), CatalogDataType.Clubs);
        await Assert.ThrowsAsync<ArgumentException>(() => source.FetchAsync(default));
    }
    [Fact]
    public async Task MissingCredentialsCancellationAndWikimediaMaxlagRespectLimits()
    {
        using var slow = new HttpClient(new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(new { }); })) { BaseAddress = new("https://fixture.invalid/") };
        var noToken = new ProviderHttp(slow, FootballProviderKind.ApiFootball, null, 0);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Assert.ThrowsAsync<HttpRequestException>(() => noToken.Get("teams", new(1), default))).StatusCode);
        using var cancel = new CancellationTokenSource(25);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProviderHttp(slow, FootballProviderKind.ApiFootball, "fixture", 0).Get("teams", new(1), cancel.Token));
        int calls = 0; using var busy = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Json(new { error = new { code = "maxlag", info = "load" } })); })) { BaseAddress = new("https://fixture.invalid/") };
        var wiki = new ProviderHttp(busy, FootballProviderKind.Wikidata, null, 0);
        for (int i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Assert.ThrowsAsync<HttpRequestException>(() => wiki.Get("w/api.php", new(5), default))).StatusCode);
        Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("Centre Back", "D")] [InlineData("Full Back", "D")] [InlineData("Defensive Midfielder", "M")]
    [InlineData("Central Midfielder", "M")] [InlineData("Attacking Midfielder", "M")] [InlineData("Winger", "A")]
    [InlineData("Striker", "A")] [InlineData("association football midfielder", "M")] [InlineData("centre-forward", "A")]
    public void PositionsAreClassicAndUnknownIsRejected(string value, string expected)
    { Assert.Equal(expected, CatalogImportCodec.Position(value)); Assert.Throws<ArgumentException>(() => CatalogImportCodec.Position("UnmappedPosition")); }
    private sealed class Harness : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"elifoot-013b-{Guid.NewGuid():N}.db");
        public ServiceProvider Services { get; }
        public CatalogSyncService Sync => Services.GetRequiredService<CatalogSyncService>();
        public WorldCreatorService Creator => Services.GetRequiredService<WorldCreatorService>();
        public Harness()
        {
            var services = new ServiceCollection(); services.AddDbContext<CatalogDbContext>(o => o.UseSqlite($"Data Source={_path};Pooling=False"));
            services.AddSingleton(new CatalogSyncOptions()); services.AddSingleton<IWorldRepository, InMemoryWorldRepository>();
            services.AddSingleton<CatalogSyncService>(); services.AddSingleton<WorldCreatorService>(); Services = services.BuildServiceProvider();
        }
        public async Task Initialize() { using var scope = Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreatedAsync(); }
        public async Task<T> Read<T>(Func<CatalogDbContext, Task<T>> action) { using var scope = Services.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<CatalogDbContext>()); }
        public async ValueTask DisposeAsync() { await Services.DisposeAsync(); if (File.Exists(_path)) File.Delete(_path); }
    }
    private static CatalogImportPackage OneClub(string provider = "fixture-a", FootballGender gender = FootballGender.Male, string external = "one", string name = "Clube fictício homônimo")
    {
        var p = CatalogFixtureFactory.Packages().First();
        return p with { Scope = p.Scope with { Provider = provider, Gender = gender }, ExpectedItems = 1,
            Clubs = [new(external, name, "FIX", "league", "Liga de teste")] };
    }
    [Fact]
    public async Task NamesNeverMergeTeamsGenderAndMultipleLeaguesRemainDistinctUntilReviewed()
    {
        await using var h = new Harness(); await h.Initialize();
        var a = OneClub(); var b = OneClub("fixture-b", external: "other");
        await h.Sync.ImportAsync(a); await h.Sync.ImportAsync(b); await h.Sync.ImportAsync(OneClub(gender: FootballGender.Female));
        await h.Sync.ImportAsync(a with { Scope = a.Scope with { League = "second-league" }, Clubs = [a.Clubs[0] with { LeagueExternalId = "second-league" }] });
        var male = await h.Creator.Select(["BR"], FootballGender.Male, "2026", true); Assert.Equal(2, male.Clubs.Count);
        Assert.Single((await h.Creator.Select(["BR"], FootballGender.Female, "2026", true)).Clubs);
        Assert.Single(await h.Read(db => db.Set<CatalogIdentityConflict>().Where(c => c.Status == "Open").ToListAsync()));
        var target = male.Clubs.Single(c => c.Provider == "fixture-a").InternalCatalogClubId!.Value;
        await h.Read(async db => { await CatalogIdentityService.MapAsync(db, new("BR", FootballGender.Male, "fixture-b", "other", target, "Manual test review of external IDs"), default); return true; });
        Assert.Single((await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs);
        await Assert.ThrowsAsync<ArgumentException>(() => h.Read(async db => { await CatalogIdentityService.MapAsync(db, new("BR", FootballGender.Female, "fixture-a", "one", target, "Cross-gender forbidden"), default); return true; }));
        await h.Read(async db => { await CatalogIdentityService.MapAsync(db, new("BR", FootballGender.Male, "ranking-provider", "rank-one", target, "Explicit ranking ID mapping test"), default); return true; });
        var ranks = a with { Scope = a.Scope with { Provider = "ranking-provider", DataType = CatalogDataType.Rankings }, Clubs = [], Rankings = [new("rank-one", "ranking-provider", FootballGender.Male, "2026", 1, "ranking-provider", a.UpdatedAt)] };
        await h.Sync.ImportAsync(ranks);
        Assert.Equal(1, (await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs.Single().Rank);
    }
    [Fact]
    public async Task LicensesAreIndependentAndExpiredCacheIsNotShownAsUsable()
    {
        await using var h = new Harness(); await h.Initialize(); var a = OneClub() with { IsFixture = false, Rights = CatalogRightsPolicy.WikidataRights };
        await h.Sync.ImportAsync(a); Assert.Single((await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs);
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(a with { Version = "bad-license", Rights = a.Rights! with { StorageAllowed = false } }));
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(a with { Version = "expired", Rights = a.Rights! with { UseUntil = DateTime.UtcNow.AddDays(-1) } }));
        Assert.Single((await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs);
        await h.Sync.ImportAsync(a with { Version = "research-only", UpdatedAt = a.UpdatedAt.AddDays(1), CommercialUseAuthorized = false,
            Rights = a.Rights! with { CommercialUseAllowed = false, DisplayAllowed = false, OfflineWorldsAllowed = false } });
        var unavailable = await h.Creator.Select(["BR"], FootballGender.Male, "2026", true); Assert.Empty(unavailable.Clubs); Assert.Contains(unavailable.Warnings, w => w.Contains("restrição"));
    }
    [Fact]
    public async Task PartialRosterPersistsCoverageButDoesNotReplaceACompleteValidRoster()
    {
        await using var h = new Harness(); await h.Initialize(); var club = OneClub(); await h.Sync.ImportAsync(club);
        var source = CatalogFixtureFactory.Packages().First(p => p.Scope.DataType == CatalogDataType.Squads);
        var players = source.Players.Take(18).Select(p => p with { ClubExternalId = "one", ClubProvider = "fixture-a", Source = "roster013b", IdentityOrigin = CatalogDataOrigin.Fixture }).ToList();
        var packet = source with { Scope = source.Scope with { Provider = "roster013b" }, Players = players, ExpectedItems = 18, AllowPartialSquads = true,
            RosterAsOf = new(2026, 10, 1), RosterReports = [new("one", "fixture-a", 18, 18, true, [])] };
        var id = await h.Sync.ImportAsync(packet);
        var bad = packet with { Version = "partial-v2", UpdatedAt = packet.UpdatedAt.AddDays(1), Players = players.Take(5).ToList(), ExpectedItems = 5,
            AllowCountDecrease = true, RosterReports = [new("one", "fixture-a", 5, 5, false, ["missing coverage"])] };
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(bad));
        Assert.Equal(id, await h.Read(db => db.Scopes.Where(s => s.Key == packet.Scope.Key).Select(s => s.ActiveSnapshotId).SingleAsync()));
        var coverage = Assert.Single(await h.Creator.Coverage(["BR"], FootballGender.Male, "2026")); Assert.Equal(1, coverage.CompleteSquads);
    }

    [Fact]
    public async Task MembershipDatesPreventTransferredPlayerAppearingAtBothClubs()
    {
        await using var h = new Harness(); await h.Initialize();
        var clubs = OneClub() with { ExpectedItems = 2, Clubs = [new("old", "Clube fictício anterior", "OLD", "L", "Liga"), new("new", "Clube fictício novo", "NEW", "L", "Liga")] };
        await h.Sync.ImportAsync(clubs);
        var original = CatalogFixtureFactory.Packages().First(p => p.Scope.DataType == CatalogDataType.Squads);
        foreach (var club in new[] { "old", "new" })
        {
            var players = original.Players.Take(18).Select((p, i) => p with { PlayerExternalId = club + "-" + i, ClubExternalId = club, ClubProvider = "fixture-a", Source = "transfer013b",
                CanonicalPlayerId = i == 17 ? "transferred-player" : club + "-" + i, IdentityOrigin = CatalogDataOrigin.Fixture,
                MembershipStart = club == "new" && i == 17 ? new DateOnly(2026, 10, 1) : new DateOnly(2025, 1, 1),
                MembershipEnd = club == "old" && i == 17 ? new DateOnly(2026, 9, 30) : null }).ToList();
            await h.Sync.ImportAsync(original with { Scope = original.Scope with { Provider = "transfer013b", League = club }, Version = club, Players = players,
                ExpectedItems = 18, AllowPartialSquads = true, RosterAsOf = new(2026, 10, 1), RosterReports = [new(club, "fixture-a", 18, 18, true, [])] });
        }
        var selection = await h.Creator.Select(["BR"], FootballGender.Male, "2026", true);
        Assert.Single(selection.Clubs.SelectMany(c => c.Players), p => p.CanonicalPlayerId == "transferred-player");
        Assert.Equal("new", selection.Clubs.Single(c => c.Players.Any(p => p.CanonicalPlayerId == "transferred-player")).ExternalId);
        var reloaded = new WorldCreatorService(h.Services.GetRequiredService<IServiceScopeFactory>(), h.Sync, h.Services.GetRequiredService<IWorldRepository>());
        Assert.Equal(selection.Clubs.Sum(c => c.Players.Count), (await reloaded.Select(["BR"], FootballGender.Male, "2026", true)).Clubs.Sum(c => c.Players.Count));
    }
    [Fact]
    public async Task EstimatedRankingPreservesDateMethodAndRequiresRightsForPermanentStorage()
    {
        await using var h = new Harness(); await h.Initialize(); var club = OneClub() with { IsFixture = false, Rights = CatalogRightsPolicy.WikidataRights };
        await h.Sync.ImportAsync(club);
        var provisional = (await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs.Single();
        Assert.Null(provisional.Rank); Assert.Equal("ProvisionalIdentifierOrder-v1", provisional.RankingInfo!.Method);
        await Assert.ThrowsAsync<ArgumentException>(() => h.Sync.ImportAsync(club with { Version = "limited-retention", Rights = club.Rights! with { RetainUntil = DateTime.UtcNow.AddDays(5) } }));
        var rank = new CatalogRank("one", "fixture-a", FootballGender.Male, "2026", 1, "rank-derived-test", club.UpdatedAt, 1500,
            "derived-elo-v1", .2, "derived-elo:Male:2026", CatalogDataOrigin.Estimated);
        await h.Sync.ImportAsync(club with { Scope = club.Scope with { Provider = "rank-derived-test", DataType = CatalogDataType.Rankings }, Clubs = [], Rankings = [rank] });
        var selected = (await h.Creator.Select(["BR"], FootballGender.Male, "2026", true)).Clubs.Single();
        Assert.Equal(CatalogDataOrigin.Estimated, selected.RankingInfo!.Origin); Assert.Equal(club.UpdatedAt, selected.RankingInfo.UpdatedAt);
        Assert.Contains(selected.CoverageWarnings!, w => w.Contains("desatualizado"));
        var csv = "ClubExternalId,ClubProvider,Gender,Season,Rank,Score,Method,Confidence,ComparableGroup,Origin\none,fixture-a,Male,2026,1,1500,derived-elo-v1,0.2,derived-elo:Male:2026,Estimated\n";
        var decoded = CatalogImportCodec.ReadCsv(club with { Scope = club.Scope with { Provider = "rank-derived-test", DataType = CatalogDataType.Rankings }, Clubs = [] }, csv);
        Assert.Equal(CatalogDataOrigin.Estimated, decoded.Rankings.Single().Origin); Assert.Equal("derived-elo-v1", decoded.Rankings.Single().Method);
    }

    [Fact]
    public async Task ProviderQuotaHeadersStopNewCallsButCachedDataRemainsUsable()
    {
        int calls = 0;
        using var client = new HttpClient(new Handler((_, _) => { calls++; var response = Json(new { data = new[] { 1 } });
            response.Headers.Add("x-ratelimit-requests-remaining", "0"); return Task.FromResult(response); })) { BaseAddress = new("https://fixture.invalid/") };
        var transport = new ProviderHttp(client, FootballProviderKind.ApiFootball, "fixture-secret", 0);
        await transport.Get("teams", new(10), default); await transport.Get("teams", new(10), default);
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.Get("players", new(10), default)); Assert.Equal(1, calls);
    }
    [Theory] [InlineData(FootballGender.Male)] [InlineData(FootballGender.Female)]
    public async Task WikidataProtocolMapsReviewedTeamAndMembershipWithoutInventingMissingNames(FootballGender gender)
    {
        var competition = new ProviderCompetition { CountryCode = "ES", CountryName = "Espanha", Gender = gender, Season = "2026", LeagueId = "Q10",
            LeagueName = "Liga fictícia", AsOf = new(2026, 10, 1), ReviewEvidence = "Synthetic entity mapping", WikidataTeams = [new("Q100", "Equipe fictícia Wikidata", "Q29", "Q20")] };
        object Claim(string id) => new { mainsnak = new { datavalue = new { value = new { id } } } };
        using var entityClient = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { entities = new Dictionary<string, object> { ["Q100"] = new {
            labels = new { en = new { value = "Equipe fictícia Wikidata" } }, lastrevid = 123,
            claims = new Dictionary<string, object> { ["P17"] = new[] { Claim("Q29") }, ["P31"] = new[] { Claim("Q20") }, ["P118"] = new[] { Claim("Q10") } } } } })))) { BaseAddress = new("https://fixture.invalid/") };
        var rows = Enumerable.Range(0, 18).Select(n => new { team = new { value = "http://www.wikidata.org/entity/Q100" },
            player = new { value = "http://www.wikidata.org/entity/Q" + (1000 + n) }, playerLabel = new { value = n == 17 ? "Q1017" : "Atleta fictício " + n },
            positionLabel = new { value = n < 2 ? "goalkeeper" : n < 8 ? "defender" : n < 14 ? "midfielder" : "forward" }, start = new { value = "2025-01-01T00:00:00Z" } }).ToList();
        using var queryClient = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { results = new { bindings = rows } })))) { BaseAddress = new("https://fixture.invalid/") };
        var source = new WikidataCatalogSource(new(entityClient, FootballProviderKind.Wikidata, null, 0), new(queryClient, FootballProviderKind.Wikidata, null, 0), competition, CatalogDataType.Squads, true);
        var result = await source.FetchAsync(default); CatalogSyncService.Validate(result);
        Assert.Equal(17, result.Players.Count); Assert.True(result.IsFixture); Assert.DoesNotContain(result.Players, p => p.PlayerName == "Q1017");
        Assert.All(result.Players, p => { Assert.Equal(gender, p.Gender); Assert.Equal(CatalogDataOrigin.Fixture, p.IdentityOrigin); Assert.NotNull(p.MembershipStart); });
        Assert.Contains(result.RosterReports!.Single().Issues, issue => issue.Contains("nome indisponível"));
    }
}
