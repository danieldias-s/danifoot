using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;

namespace Elifoot.Testing;

// Only explicitly fictional fixtures. Never presented as complete real squads.
internal static class Imported014AWorlds
{
    public static WorldCreatorRequest Request(FootballGender gender, int seed = 199801) => new(
        "Integração 014A — fixtures", gender, "2026", WorldCreatorService.PriorityCountries.Select(c => c.Code).ToList(),
        WorldAssemblyMode.Automatic, DistrictFillMode.Random, seed, [], UseSavedData: true);

    public static CatalogSelection Selection(FootballGender gender)
    {
        var packages = CatalogFixtureFactory.Packages().Where(p => p.Scope.Gender == gender).ToList();
        var clubs = packages.Where(p => p.Scope.DataType == CatalogDataType.Clubs).SelectMany(package =>
            package.Clubs.Select(team => new EligibleCatalogClub(
                WorldCreatorEngine.ClubKey(package.Scope.CountryCode, gender, package.Scope.Provider, team.ExternalId),
                team.ExternalId, package.Scope.Provider, team.Name, team.ShortName, package.Scope.CountryCode,
                package.CountryName, gender, team.LeagueName, "2026", team.PrimaryColor, team.SecondaryColor,
                packages.SelectMany(p => p.Rankings).Single(r => r.ClubExternalId == team.ExternalId).Rank,
                packages.SelectMany(p => p.Players).Where(p => p.ClubExternalId == team.ExternalId).ToList(), [], true))).ToList();
        return new(gender, "2026", Request(gender).Countries, clubs, [], ["FIXTURES fictícias autorizadas da integração 014A."]);
    }

    public static GameWorld Create(FootballGender gender, int seed = 199801, bool manual = false)
    {
        var request = Request(gender, seed); var selection = Selection(gender);
        if (manual)
        {
            var ordered = selection.Clubs.OrderBy(c => c.Rank).Take(128).Reverse().ToList();
            request = request with { Mode = WorldAssemblyMode.Custom, DistrictMode = DistrictFillMode.Manual,
                FillRemainingDivisions = false,
                Assignments = ordered.Select((c, i) => new ClubAssignment(c.Key, i < 32 ? i / 8 + 1 : 5)).ToList() };
        }
        var preview = WorldCreatorEngine.Preview(request, selection);
        if (preview.Errors.Count > 0) throw new InvalidOperationException(string.Join("; ", preview.Errors));
        return WorldCreatorEngine.Create(request, preview);
    }
}
