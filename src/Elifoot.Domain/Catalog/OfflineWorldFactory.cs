using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Imports explicit values without passing through 017B or synthetic roster completion.</summary>
public static class OfflineWorldFactory
{
    public static EligibleCatalogClub Eligible(OfflineClubSource source, FootballGender gender)
    {
        var c = source.Definition;
        string key = $"offline:{source.CountryCode}:{source.Year}:{c.CatalogClubId}";
        return new(key, c.CatalogClubId!, "offline-v1", c.Name!, c.ShortName!, source.CountryCode,
            source.CountryName, gender, "Catálogo offline", source.Year.ToString(), c.Colors![0], c.Colors[1], null,
            (c.Players ?? []).Select(p => new CatalogAthlete(p.Id!, c.CatalogClubId!, "offline-v1", gender,
                source.Year.ToString(), p.Name!, p.Position!, p.Strength!.Value, "Arquivo local fornecido pelo proprietário",
                DateTime.UnixEpoch, CanonicalPlayerId: $"offline:{source.Year}:{p.Id}")).ToList(), [], OfflineSource: source);
    }

    public static WorldCreatorPreview Preview(OfflineWorldRequest request, List<EligibleCatalogClub> available)
    {
        if (!OfflineCatalogRules.Text(request.Name) || request.MaxHumans is < 1 or > 8)
            throw new ArgumentException("Informe nome de até 160 caracteres e 1..8 treinadores.");
        if (request.CatalogIds == null || request.CatalogIds.Count == 0 || request.CatalogIds.Distinct().Count() != request.CatalogIds.Count)
            throw new ArgumentException("Selecione catálogos sem repetição.");
        if (available.Select(c => c.Gender).Distinct().Count() != 1 || available.Select(c => c.CountryCode).Distinct().Count() > 8)
            throw new ArgumentException("Escolha uma categoria e até oito países.");
        var errors = new List<string>();
        var clubs = available.OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
        if (request.ClubKeys != null)
        {
            var keys = request.ClubKeys.ToHashSet(StringComparer.Ordinal);
            if (keys.Count != request.ClubKeys.Count || keys.Any(k => clubs.All(c => c.Key != k)))
                throw new ArgumentException("Seleção de clubes repetida ou fora do catálogo.");
            clubs = clubs.Where(c => keys.Contains(c.Key)).ToList();
        }
        if (clubs.Count != 128) errors.Add($"Selecione exatamente 128 clubes; há {clubs.Count}. Não serão gerados clubes ou jogadores.");
        var assigned = new List<WorldPreviewClub>();
        var explicitAssignments = request.Assignments ?? [];
        if (explicitAssignments.Select(a => a.ClubKey).Distinct().Count() != explicitAssignments.Count ||
            explicitAssignments.Any(a => a.Division is < 1 or > 5 || clubs.All(c => c.Key != a.ClubKey)))
            throw new ArgumentException("Distribuição contém divisão inválida, clube repetido ou fora da seleção.");
        foreach (var c in clubs)
        {
            int? division = explicitAssignments.FirstOrDefault(a => a.ClubKey == c.Key)?.Division ?? c.OfflineSource?.Definition.InitialDivision;
            if (division.HasValue) assigned.Add(new(division.Value, c));
        }
        foreach (var c in clubs.Where(c => assigned.All(a => a.Club.Key != c.Key)))
        {
            int division = Enumerable.Range(1, 5).FirstOrDefault(d => assigned.Count(a => a.Division == d) < (d == 5 ? 96 : 8));
            if (division == 0) { errors.Add("Não há vaga para todos os clubes selecionados."); break; }
            assigned.Add(new(division, c));
        }
        for (int d = 1; d <= 5; d++)
            if (assigned.Count(a => a.Division == d) != (d == 5 ? 96 : 8))
                errors.Add(d == 5 ? "Distritais exigem 96 clubes." : $"Divisão {d} exige 8 clubes.");
        assigned = assigned.OrderBy(a => a.Division).ThenBy(a => a.Club.Key, StringComparer.Ordinal).ToList();
        string hash = Hash(request, assigned);
        return new(Guid.NewGuid(), request.Name, available[0].Gender, request.Seed, assigned,
            Math.Max(0, 128 - assigned.Count), errors.Distinct().ToList(), [], [], DateTime.UtcNow, hash);
    }

    private static string Hash(OfflineWorldRequest request, List<WorldPreviewClub> clubs) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request, clubs })));

    public static GameWorld Create(OfflineWorldRequest request, WorldCreatorPreview preview)
    {
        if (preview.Errors.Count > 0 || preview.PendingSlots != 0 || preview.DataHash != Hash(request, preview.Clubs) ||
            preview.Clubs.Count != 128 || preview.Clubs.Select(c => c.Club.Key).Distinct().Count() != 128)
            throw new ArgumentException("Prévia incompleta ou modificada; valide novamente.");
        foreach (var entry in preview.Clubs)
        {
            var source = entry.Club.OfflineSource ?? throw new ArgumentException("Clube sem origem offline.");
            if (source.Definition.Status != "READY" || OfflineCatalogRules.ValidateClub(source.Definition, entry.Club.Key).Count > 0)
                throw new ArgumentException("Clube incompleto na prévia.");
        }
        for (int d = 1; d <= 5; d++)
            if (preview.Clubs.Count(c => c.Division == d) != (d == 5 ? 96 : 8)) throw new ArgumentException("Distribuição inválida.");

        // Existing foundation rules supply financial/personality defaults, never roster identities or strengths.
        var foundation = WorldSeedFactory.CreateSeededWorld(request.Name, request.Seed, 128, request.MaxHumans);
        var world = new GameWorld
        {
            Id = Guid.NewGuid(), Name = request.Name, Seed = request.Seed, MaxHumans = request.MaxHumans,
            InviteCode = $"ELI{Guid.NewGuid():N}"[..11].ToUpperInvariant(), TotalClubs = 128,
            ForceEvolutionMode = ForceEvolutionMode.ClassicAdapted, PlayerStatisticsVersion = 9
        };
        for (int i = 0; i < preview.Clubs.Count; i++)
        {
            var entry = preview.Clubs[i]; var source = entry.Club.OfflineSource!; var c = source.Definition;
            var template = foundation.Clubs[i];
            var stadium = template.Stadium;
            stadium.Name = c.Stadium!.Name!; stadium.BuiltCapacity = c.Stadium.Capacity!.Value;
            stadium.UsableCapacity = (int)Math.Round(stadium.BuiltCapacity * stadium.ConditionPercent / 100);
            stadium.MaintenanceReservePerSeason = Math.Round(stadium.BuiltCapacity * 4.5m, 0);
            stadium.LastAttendance = stadium.UsableCapacity / 2;
            var club = new Club
            {
                Id = Guid.NewGuid(), GameWorldId = world.Id, DeterministicIndex = i + 1,
                Name = c.Name!, ShortName = c.ShortName!, Country = source.CountryCode,
                PrimaryColorHex = c.Colors![0], SecondaryColorHex = c.Colors[1],
                DivisionTier = (DivisionTier)entry.Division, Stadium = stadium,
                CashBalance = template.CashBalance, Reputation = template.Reputation, InitialSquadSize = c.Players!.Count,
                OfflineIdentity = new(source.CatalogId, c.CatalogClubId!, source.CountryCode, source.Year,
                    c.ShortName!, c.Abbreviation!, source.SourceHash, source.CrestDataUrl)
            };
            var coach = c.Coach!; var profile = foundation.AiManagers.Single(m => m.Id == template.AiManagerId);
            var manager = new AiManager
            {
                Id = Guid.NewGuid(), GameWorldId = world.Id, CurrentClubId = club.Id, Name = coach.Name!,
                Identity = profile.Identity, Pragmatism = profile.Pragmatism, Adaptability = profile.Adaptability,
                FinancialAggressiveness = profile.FinancialAggressiveness, YouthPatience = profile.YouthPatience,
                PreferredFormations = coach.PreferredTactics!.Home!.Select(f => new FormationPreferenceWeightDto(f, 50)).ToList(),
                OfflineIdentity = new(source.CatalogId, coach.Id!, source.CountryCode, source.Year, coach.Nationality!,
                    coach.PreferredTactics.Home!.ToList(), coach.PreferredTactics.Away!.ToList())
            };
            club.AiManagerId = manager.Id; world.Clubs.Add(club); world.AiManagers.Add(manager);
            var contractDefaults = foundation.Players.Where(p => p.ClubId == template.Id).ToList();
            int j = 0;
            foreach (var p in c.Players)
            {
                var player = new Player
                {
                    Id = Guid.NewGuid(), GameWorldId = world.Id, ClubId = club.Id, Name = p.Name!,
                    Position = Enum.Parse<PlayerPosition>(p.Position!), Strength = p.Strength!.Value,
                    Behavior = (PlayerBehavior)p.Behavior!.Value, IsStar = p.IsStar!.Value, Nationality = p.Nationality,
                    ContractRoundsRemaining = contractDefaults[j++ % contractDefaults.Count].ContractRoundsRemaining,
                    OfflineIdentity = new(source.CatalogId, p.Id!, source.CountryCode, source.Year)
                };
                player.SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(player);
                player.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(player);
                world.Players.Add(player);
            }
            AiManagerBrain.PlanAndSelectLineup(manager, club, world.Players.Where(p => p.ClubId == club.Id).ToList(), null, null,
                true, world.Seed, 1, 1, world.AllowClassic505);
            world.Transactions.Add(new FinancialTransaction
            {
                GameWorldId = world.Id, ClubId = club.Id, SeasonNumber = 1, RoundNumber = 1,
                Category = FinancialTransactionCategory.InitialBalance, Amount = club.CashBalance,
                BalanceAfter = club.CashBalance, Description = "Saldo inicial de fundação da temporada", IdempotencyKey = $"init:{club.Id}"
            });
        }
        world.Economy.OperatingRules = OperatingEconomyEngine.CurrentRules();
        ClassicEconomyEngine.EnsureMigrated(world, isNewWorld: true);
        CompetitionAndSeasonEngine.InitializeSeasonFixtures(world);
        CoachCareerEngine.EnsureMigrated(world, isNewWorld: true);
        ClassicForceEvolutionEngine.InitializeSeason(world);
        world.SetCatalogOrigin(new(preview.Gender, preview.Clubs[0].Club.Season,
            preview.Clubs.Select(c => c.Club.CountryCode).Distinct().ToList(), request.Seed, preview.CreatedAt,
            preview.DataHash, preview.Clubs, [], CatalogYears: preview.Clubs.Select(c => c.Club.Season).Distinct().ToList()));
        var origin = new OfflineWorldOrigin(OfflineCatalogRules.Version, preview.DataHash, preview.CreatedAt, preview.Clubs);
        world.OfflineCatalogOrigin = JsonSerializer.Deserialize<OfflineWorldOrigin>(JsonSerializer.Serialize(origin));
        world.History = new(); HistoryEngine.Observe(world, beginningSeason: true);
        return world;
    }
}
