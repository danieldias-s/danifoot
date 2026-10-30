using Elifoot.Contracts;

namespace Elifoot.Infrastructure.Catalog;

/// <summary>Invented names and rankings only. Never represents real league coverage.</summary>
public static class CatalogFixtureFactory
{
    public static IEnumerable<CatalogImportPackage> Packages(int clubsPerCountry = 20)
    {
        foreach (var gender in Enum.GetValues<FootballGender>())
        for (int countryIndex = 0; countryIndex < WorldCreatorService.PriorityCountries.Length; countryIndex++)
        {
            var country = WorldCreatorService.PriorityCountries[countryIndex];
            string prefix = $"{country.Code}-{gender}";
            var date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var clubs = Enumerable.Range(1, clubsPerCountry).Select(i => new CatalogTeam($"{prefix}-{i}",
                $"Clube fictício {country.Code} {(gender == FootballGender.Female ? "F" : "M")} {i:00}", $"{country.Code}{i:00}",
                $"league-{prefix}", $"Liga fictícia {country.Name} ({gender})")).ToList();
            var positions = new[] { "G", "G", "D", "D", "D", "D", "D", "D", "M", "M", "M", "M", "M", "M", "A", "A", "A", "A" };
            var players = clubs.SelectMany((club, i) => positions.Select((position, j) => new CatalogAthlete(
                $"{club.ExternalId}-p{j}", club.ExternalId, "fixture013a", gender, "2026",
                $"{(gender == FootballGender.Female ? "Atleta fictícia" : "Atleta fictício")} {prefix}-{i + 1}-{j + 1}", position,
                15 + (i + j) % 30, "fixture-squads013a", date))).ToList();
            var ranking = clubs.Select((club, i) => new CatalogRank(club.ExternalId, "fixture013a", gender, "2026",
                countryIndex * clubsPerCountry + i + 1, "fixture-ranking013a", date)).ToList();
            CatalogImportPackage Package(CatalogDataType type, string provider, int count) => new(
                new(country.Code, gender, "*", "2026", type, provider), country.Name, "fixture-v1", date,
                true, 1, 1, count, true, true, "Invented test data; repository fixture", true, [], [], []);
            yield return Package(CatalogDataType.Clubs, "fixture013a", clubs.Count) with { Clubs = clubs };
            yield return Package(CatalogDataType.Squads, "fixture-squads013a", players.Count) with { Players = players };
            yield return Package(CatalogDataType.Rankings, "fixture-ranking013a", ranking.Count) with { Rankings = ranking };
        }
    }
}
