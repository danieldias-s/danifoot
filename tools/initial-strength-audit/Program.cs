using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

using var output = new StreamWriter(args.Single());
List<string> eightCountries = ["AR", "BR", "DE", "ES", "FR", "GB", "IT", "PT"];
Capture(WorldSeedFactory.CreateSeededWorld(seed: 199801), "traditional-seed", ["BR"]);
Generate("pt032", 199801, eightCountries, FootballGender.Male, "fictional");
Generate("fictional-jp", 199802, ["JP"], FootballGender.Female, "fictional");
Generate("fictional-legacy", 199803, ["BR", "PT"], FootballGender.Male, "fictional", false);
Generate("fictional-013a", 199807, ["BR"], FootballGender.Male, "fictional", false, legacyRequest: true);
Generate("elo", 199803, ["BR", "PT", "AR"], FootballGender.Male, "elo");
Generate("ordinal", 199804, ["ES", "DE", "FR"], FootballGender.Female, "ordinal");
Generate("unranked", 199805, ["IT", "GB"], FootballGender.Male, "unranked");
Generate("mixed", 199806, ["BR", "JP", "PT"], FootballGender.Female, "elo", false, 16);

void Generate(string scenario, int seed, List<string> countries, FootballGender gender, string ranking,
    bool catalogRules = true, int clubCount = 128, bool legacyRequest = false)
{
    bool fictional = ranking == "fictional";
    bool mixed = clubCount < 128;
    const string season = "2026";
    var observed = DateTime.UnixEpoch;
    var clubs = fictional ? [] : WorldCatalogRules016B.Classify(Enumerable.Range(0, clubCount).Select(i =>
    {
        string id = $"audit-{i + 1:000}";
        string country = countries[i % countries.Count];
        CatalogRank? rank = ranking == "unranked" ? null : new(id, "audit", gender, season, i + 1, "Synthetic audit fixture",
            observed, ranking == "elo" ? 2100 - i * 15 : null, ranking == "elo" ? "audit-elo" : "audit-ordinal",
            .8, "audit-comparable", CatalogDataOrigin.Estimated);
        return new EligibleCatalogClub(WorldCreatorEngine.ClubKey(country, gender, "audit", id), id, "audit",
            id, id, country, country, gender, "Audit fixture", season, "#006400", "#FFFFFF", rank?.Rank, [], [],
            RankingInfo: rank, Verification: new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED, "Synthetic audit fixture", observed));
    }));
    var selection = new CatalogSelection(gender, season, countries, clubs, [], []);
    var request = new WorldCreatorRequest(scenario, gender, season, countries, WorldAssemblyMode.Automatic,
        DistrictFillMode.Ranking, seed, [], AllowFictionalTeams: fictional || mixed, AllowFictionalPlayers: true,
        DataMode: legacyRequest ? null : fictional || mixed ? WorldDataMode.FREE_WORLD : WorldDataMode.REAL_CLUBS_SIMULATED,
        UseWorldCatalogRules: catalogRules, FictionalOnly: fictional && catalogRules);
    var preview = WorldCreatorEngine.Preview(request, selection);
    Capture(WorldCreatorEngine.Create(request, preview), scenario, countries);
    if (scenario != "elo") return;
    var assignments = preview.Clubs.Select((c, i) => new ClubAssignment(c.Club.Key, i switch { 0 => 4, 24 => 1, _ => c.Division })).ToList();
    var manual = request with { Mode = WorldAssemblyMode.Custom, DistrictMode = DistrictFillMode.Manual,
        Assignments = assignments, FillRemainingDivisions = false };
    Capture(WorldCreatorEngine.Create(manual, WorldCreatorEngine.Preview(manual, selection)), "elo-manual", countries);
}

void Capture(GameWorld world, string scenario, List<string> countries)
{
    var byClub = world.Players.ToLookup(p => p.ClubId);
    var origins = world.CatalogOrigin?.Clubs.ToDictionary(c => c.Club.ShortName);
    foreach (var club in world.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital))
    {
        var players = byClub[club.Id].ToList();
        if (club.PreferredStarterIds.Count != 11) throw new InvalidOperationException(club.Name);
        var profile = origins?.GetValueOrDefault(club.ShortName)?.Club.InitialProfile;
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Scenario = scenario, world.Seed, Countries = countries, Division = (int)club.DivisionTier,
            club.Name, club.ShortName, club.Country, club.ClubInitialPower, Rank = profile?.WorldRank,
            RankingConfidence = profile?.RankingConfidence, Formation = club.PreferredFormation,
            Players = players.Select(p => new
            {
                p.Name, Position = p.Position.ToString(), p.Strength, p.IsStar,
                Role = club.PreferredStarterIds.Contains(p.Id) ? "XI" : club.PreferredBenchIds.Contains(p.Id) ? "bench" : "outside",
                Method = p.CatalogIdentity?.StrengthEstimate?.Method
            })
        }));
    }
}
