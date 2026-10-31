using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class GameWorld { public CustomAssemblyWorldOrigin? CustomAssemblyOrigin { get; set; } }

/// <summary>Pre-save validation and cloning. Uses the approved factory/economy and editor package services.</summary>
public static class CustomAssemblyPolicy
{
    public static WorldPackageExportDto Prepare(WorldPackageExportDto edited, WorldPackageExportDto baseline,
        Guid assemblyId, List<CatalogCountryEntry> countries, bool trustedCatalog, bool personalized)
    {
        var originals = baseline.Players.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        var originalClubs = baseline.Clubs.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var sourceRows = baseline.CatalogOrigin?.Clubs ?? [];
        var sourceById = new Dictionary<Guid, EligibleCatalogClub>();
        if (trustedCatalog && sourceRows.Count == baseline.Clubs.Count)
            for (int i = 0; i < baseline.Clubs.Count; i++) sourceById[baseline.Clubs[i].Id] = sourceRows[i].Club;
        var players = edited.Players.Select(p =>
        {
            originals.TryGetValue(p.Id, out var old);
            bool same = old != null && old.Name == p.Name && old.Position == p.Position && old.ClubId == p.ClubId && baseline.Gender == edited.Gender;
            bool sameClub = p.ClubId is Guid clubId && originalClubs.TryGetValue(clubId, out var club) &&
                edited.Clubs.Any(c => c.Id == clubId && c.Name == club.Name && c.Country == club.Country);
            var identity = same && trustedCatalog ? old!.CatalogIdentity : null;
            if (identity?.Origin == WorldPlayerOrigin.REAL_CURRENT_VERIFIED && !sameClub)
                identity = identity with { Origin = WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE };
            if (identity == null)
            {
                if (!trustedCatalog && p.CatalogIdentity?.Origin is WorldPlayerOrigin.REAL_CURRENT_VERIFIED or WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE)
                    identity = p.CatalogIdentity with { Origin = WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE };
                else identity = new(WorldPlayerOrigin.FICTIONAL_GENERATED, "custom014b", p.Id.ToString("N"),
                    $"custom:{assemblyId:N}:{p.Id:N}", $"custom-club:{p.ClubId}", "XX", edited.Gender,
                    baseline.CatalogOrigin?.Season ?? "PERSONALIZADO", DateTime.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), [], GeneratedSeed: edited.Seed);
            }
            var reference = new Player { Position = p.Position, Strength = p.Strength, IsStar = p.IsStar, Behavior = p.Behavior, ContractRoundsRemaining = p.ContractRoundsRemaining };
            return p with { CatalogIdentity = identity, MarketPrice = MarketAndEconomyEngine.CalculateFairValue(reference), NegotiatedContractProtected = true };
        }).ToList();
        var ordered = edited.Clubs.OrderBy(c => (int)c.DivisionTier).ToList();
        var foundation = WorldSeedFactory.CreateSeededWorld(seed: edited.Seed, forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        var clubs = ordered.Select((c, i) => c with { ColorPalette=ClubColorPolicy.ForAssembly(c,originalClubs.GetValueOrDefault(c.Id),trustedCatalog,edited.Gender,countries.FirstOrDefault(x=>x.Code.Equals(c.Country,StringComparison.OrdinalIgnoreCase)||x.Name.Equals(c.Country,StringComparison.OrdinalIgnoreCase))?.Code), CashBalance = i < foundation.Clubs.Count ? foundation.Clubs[i].CashBalance : c.CashBalance }).ToList();
        var rows = clubs.Select((c, i) =>
        {
            var country = countries.FirstOrDefault(x => string.Equals(x.Name, c.Country, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Code, c.Country, StringComparison.OrdinalIgnoreCase));
            sourceById.TryGetValue(c.Id, out var previous);
            bool unchanged = previous != null && previous.Name == c.Name && previous.Gender == edited.Gender && previous.CountryCode == country?.Code;
            string key = unchanged ? previous!.Key : $"custom:{assemblyId:N}:{c.Id:N}";
            var athletes = players.Where(p => p.ClubId == c.Id).Select(p =>
            {
                var identity = p.CatalogIdentity!;
                var original = previous?.Players.FirstOrDefault(a => a.CanonicalPlayerId == identity.CanonicalIdentity);
                return original != null && identity.Origin != WorldPlayerOrigin.FICTIONAL_GENERATED
                    ? original with { PlayerName = p.Name, Position = p.Position.ToString(), Strength = p.Strength, PlayerOrigin = identity.Origin }
                    : new CatalogAthlete(p.Id.ToString("N"), c.Id.ToString("N"), "custom014b", edited.Gender,
                        baseline.CatalogOrigin?.Season ?? "PERSONALIZADO", p.Name, p.Position.ToString(), p.Strength, "custom014b", DateTime.UtcNow,
                        identity.CanonicalIdentity, identity.Origin == WorldPlayerOrigin.FICTIONAL_GENERATED ? CatalogDataOrigin.Generated : CatalogDataOrigin.Real,
                        PlayerOrigin: identity.Origin);
            }).ToList();
            var record = unchanged ? previous! with { Name = c.Name, ShortName = c.ShortName, Players = athletes,PrimaryColor=c.PrimaryColorHex,SecondaryColor=c.SecondaryColorHex,ColorPalette=c.ColorPalette } :
                new EligibleCatalogClub(key, c.Id.ToString("N"), "custom014b", c.Name, c.ShortName, country?.Code ?? "INVALID", country?.Name ?? c.Country,
                    edited.Gender, "Montagem personalizada", baseline.CatalogOrigin?.Season ?? "PERSONALIZADO", c.PrimaryColorHex, c.SecondaryColorHex, i + 1, athletes, [], true,
                    Verification: new(CatalogClubVerificationStatus.FICTIONAL_CLUB, "Clube personalizado pelo proprietário", DateTime.UtcNow),ColorPalette:c.ColorPalette);
            return new WorldPreviewClub((int)c.DivisionTier, record);
        }).ToList();
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { edited.Seed, edited.Gender, clubs, players })));
        var refs = trustedCatalog ? baseline.CatalogOrigin?.References ?? [] : [];
        var origin = new WorldCreationOrigin(edited.Gender, baseline.CatalogOrigin?.Season ?? "PERSONALIZADO", countries.Select(c => c.Code).ToList(), edited.Seed,
            DateTime.UtcNow, hash, rows, refs, WorldDataMode.FREE_WORLD, edited.Seed, DateOnly.FromDateTime(DateTime.UtcNow));
        return edited with { Clubs = clubs, Players = players, CatalogOrigin = origin, ForceEvolutionMode = ForceEvolutionMode.ClassicAdapted };
    }
    public static PackageValidationResultDto Validate(WorldPackageExportDto package, List<CatalogCountryEntry> countries,
        WorldPackageExportDto baseline, bool personalized)
    {
        var result = WorldEditorAndPackageService.ValidatePackage(package); var errors = result.Errors.ToList();
        if (package.Clubs.Count != 128) errors.Add("A montagem precisa de 128 clubes: quatro divisões de oito e 96 distritais.");
        for (int d = 1; d <= 5; d++) if (package.Clubs.Count(c => (int)c.DivisionTier == d) != (d == 5 ? 96 : 8)) errors.Add($"Divisão {d}: quantidade de clubes incorreta.");
        if (countries.Count == 0 || countries.Select(c => c.Code).Distinct().Count() != countries.Count) errors.Add("Selecione países válidos sem repetição.");
        foreach (var c in package.Clubs)
        {
            if(!ClubColorPolicy.Hex(c.PrimaryColorHex)||!ClubColorPolicy.Hex(c.SecondaryColorHex))errors.Add($"{c.Name}: cores devem ser #RRGGBB.");
            if (!countries.Any(x => string.Equals(x.Name, c.Country, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Code, c.Country, StringComparison.OrdinalIgnoreCase))) errors.Add($"{c.Name}: país fora da seleção.");
            if (!Enum.IsDefined(c.DivisionTier) || string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.ShortName)) errors.Add($"{c.Name}: divisão ou sigla inválida.");
        }
        foreach (var p in package.Players)
        {
            if (p.ClubId == null || !package.Clubs.Any(c => c.Id == p.ClubId)) errors.Add($"{p.Name}: clube inválido.");
            if (!Enum.IsDefined(p.Position) || !Enum.IsDefined(p.Behavior)) errors.Add($"{p.Name}: posição ou comportamento inválido.");
            if (p.SalaryPerRound <= 0 || p.ContractRoundsRemaining <= 0) errors.Add($"{p.Name}: salário e contrato inicial devem ser positivos.");
        }
        if (package.Clubs.Any(c => c.Id == Guid.Empty) || package.Players.Any(p => p.Id == Guid.Empty)) errors.Add("Identificadores vazios não são permitidos.");
        if (package.AiManagers.Select(m => m.Id).Distinct().Count() != package.AiManagers.Count) errors.Add("Técnicos IA duplicados.");
        foreach (var c in package.Clubs) if (package.AiManagers.Count(m => m.AssignedClubId == c.Id) != 1) errors.Add($"{c.Name}: deve ter um técnico IA inicial.");
        if (!Enum.IsDefined(package.Gender)) errors.Add("Categoria inválida.");
        if (!personalized)
        {
            var before = baseline.Clubs.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            if (package.Clubs.Any(c => !before.TryGetValue(c.Id, out var old) || c.Name != old.Name || c.Country != old.Country) || package.Gender != baseline.Gender)
                errors.Add("Autorize explicitamente clubes e jogadores personalizados para alterar identidades ou categoria.");
            var originals = baseline.Players.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            if (package.Players.Any(p => !originals.TryGetValue(p.Id, out var old) || p.Name != old.Name || p.Position != old.Position || p.ClubId != old.ClubId))
                errors.Add("Autorize explicitamente jogadores personalizados para alterar identidades e posições.");
        }
        return result with { IsValid = errors.Count == 0, Errors = errors.Distinct().ToList() };
    }
    public static GameWorld CreateIndependent(WorldPackageExportDto package, Guid assemblyId, int version, List<string> countries, string name, int maxHumans)
    {
        var clubIds = package.Clubs.ToDictionary(c => c.Id, _ => Guid.NewGuid());
        var players = package.Players.Select(p => p with { Id = Guid.NewGuid(), ClubId = p.ClubId is Guid id ? clubIds[id] : null }).ToList();
        var clubs = package.Clubs.Select(c => c with { Id = clubIds[c.Id] }).ToList();
        var managers = package.AiManagers.Select(m => m with { Id = Guid.NewGuid(), AssignedClubId = m.AssignedClubId is Guid id ? clubIds.GetValueOrDefault(id) : null }).ToList();
        var result = WorldEditorAndPackageService.CreateWorldFromPackage(package with { Clubs = clubs, Players = players, AiManagers = managers }, name);
        if (result.World == null) throw new ArgumentException(string.Join("; ", result.Validation.Errors));
        result.World.MaxHumans = Math.Clamp(maxHumans, 1, 8);
        result.World.CustomAssemblyOrigin = new(assemblyId, version, package.CatalogOrigin?.DataHash ?? "", countries.ToList(), DateTime.UtcNow);
        return result.World;
    }
}
