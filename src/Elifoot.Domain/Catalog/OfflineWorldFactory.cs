using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Imports explicit values without passing through 017B or synthetic roster completion.</summary>
public static class OfflineWorldFactory
{
    public static EligibleCatalogClub Eligible(OfflineClubSource source, FootballGender gender, bool planningOnly = false)
    {
        var c = source.Definition;
        if (c.SchemaVersion == "1.1" && source.V11 == null)
            throw new ArgumentException("Catálogo 1.1 sem identidade e metadados resolvidos.");
        string key = $"offline:{source.CountryCode}:{source.Year}:{c.CatalogClubId}";
        return new(key, c.CatalogClubId!, "offline-v1", c.Name!, c.ShortName!, source.CountryCode,
            source.CountryName, gender, "Catálogo offline", source.Year.ToString(), c.Colors![0], c.Colors[1], null,
            planningOnly || source.V11 != null ? [] : (c.Players ?? []).Select(p => new CatalogAthlete(p.Id!, c.CatalogClubId!, "offline-v1", gender,
                source.Year.ToString(), p.Name!, p.Position!, p.Strength!.Value, "Arquivo local fornecido pelo proprietário",
                DateTime.UnixEpoch, CanonicalPlayerId: $"offline:{source.Year}:{p.Id}",
                IdentityOrigin: CatalogDataOrigin.Real, PlayerOrigin: WorldPlayerOrigin.REAL_HISTORICAL_CATALOG)).ToList(), [], OfflineSource: source);
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
        var clubs = OfflineCatalogRanking.Order(available);
        var rankedCandidates = clubs;
        if (request.IncludeSurplusClub && !request.ApplyV16RosterPolicy)
            throw new ArgumentException("Clube de excedentes exige corte de elenco V1.6.");
        var warnings = OfflineCatalogRanking.Warnings(clubs);
        if (request.ClubKeys != null)
        {
            var keys = request.ClubKeys.ToHashSet(StringComparer.Ordinal);
            if (keys.Count != request.ClubKeys.Count || keys.Any(k => clubs.All(c => c.Key != k)))
                throw new ArgumentException("Seleção de clubes repetida ou fora do catálogo.");
            clubs = clubs.Where(c => keys.Contains(c.Key)).ToList();
        }
        bool isV11 = clubs.Any(OfflineCatalogRanking.IsV11);
        if (isV11)
        {
            // Preserve the same calibration cohort as 128-real selection whenever the next real club exists.
            var chosen = clubs.Select(c => c.Key).ToHashSet();
            if (request.IncludeSurplusClub && clubs.Count == 127)
            {
                var calibration = rankedCandidates.FirstOrDefault(c => !chosen.Contains(c.Key));
                if (calibration != null) clubs = clubs.Append(calibration).ToList();
            }
            clubs = InitialPlayerStrength.Apply(clubs.Select((c, i) =>
            {
                var source = c.OfflineSource!;
                var def = source.Definition;
                var v11 = source.V11!;
                int rank = i + 1;
                return c with
                {
                    Rank = rank,
                    InitialProfile = new(v11.TeamStrength!.Value, rank, v11.HistoricalRanking?.Rating,
                        v11.HistoricalRanking?.Source ?? "offline-v1.1", DateTime.UnixEpoch, 1.0,
                        TargetInitialSquadSize: def.Players?.Count ?? 0, SquadSelectionMethod: "offline-v1.1"),
                    Players = (def.Players ?? []).Select(p => new CatalogAthlete(p.Id!, def.CatalogClubId!, "offline-v1.1", c.Gender,
                        source.Year.ToString(), p.Name!, p.Position!, 1, "Arquivo local fornecido pelo proprietário",
                        DateTime.UnixEpoch, CanonicalPlayerId: $"offline:{source.CountryCode}:{source.Year}:{def.CatalogClubId}:{p.Id}",
                        IdentityOrigin: CatalogDataOrigin.Real,
                        StrengthEstimate: new(InitialPlayerStrength.Version, CatalogDataOrigin.Estimated, 0, "", .5, null),
                        PlayerOrigin: WorldPlayerOrigin.REAL_HISTORICAL_CATALOG)).ToList()
                };
            }).ToList(), request.Seed);
            if (request.IncludeSurplusClub) clubs = clubs.Where(c => chosen.Contains(c.Key)).ToList();
        }
        int realCount = request.IncludeSurplusClub ? 127 : 128;
        if (clubs.Count != realCount) errors.Add($"Selecione exatamente {realCount} clubes reais; há {clubs.Count}. Não serão gerados clubes ou jogadores.");
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
            if (assigned.Count(a => a.Division == d) != (d == 5 ? 96 - (request.IncludeSurplusClub ? 1 : 0) : 8))
                errors.Add(d == 5 ? "Distritais exigem 96 clubes." : $"Divisão {d} exige 8 clubes.");
        errors.AddRange(OfflineCatalogRanking.BoundaryErrors(clubs, assigned, explicitAssignments));
        var order = clubs.Select((c, i) => (c.Key, i)).ToDictionary(x => x.Key, x => x.i);
        assigned = assigned.OrderBy(a => a.Division).ThenBy(a => order[a.Club.Key]).ToList();
        if (request.ApplyV16RosterPolicy)
        {
            if ((request.RetentionEvidence ?? []).Any(e => clubs.All(c => c.Key != e.ClubKey)))
                throw new ArgumentException("Pesquisa de retenção aponta clube fora da seleção.");
            assigned = assigned.Select(entry =>
            {
                try { return OfflineRosterV16.Select(entry, request.Seed, request.RetentionEvidence); }
                catch (ArgumentException error) { errors.Add(error.Message); return entry; }
            }).ToList();
        }
        if (request.IncludeSurplusClub && errors.Count == 0)
        {
            try { assigned.Add(OfflineSurplusV16.Build(assigned, request.Seed, request.RetentionEvidence)); }
            catch (ArgumentException error) { errors.Add(error.Message); }
        }
        if (isV11) assigned = InitialSquadStrength017B.Apply(assigned, request.Seed);
        if (request.UseDefaultBehavior)
        {
            var behaviors = DrawMissingBehaviors(assigned, request.Seed);
            assigned = assigned.Select(entry => entry with
            {
                Club = entry.Club with
                {
                    Players = entry.Club.Players.Select(player =>
                        behaviors.TryGetValue((entry.Club.Key, player.PlayerExternalId), out var behavior)
                            ? player with { InitialBehavior = behavior } : player).ToList()
                }
            }).ToList();
            if (behaviors.Count > 0)
                warnings.Add($"{behaviors.Count} jogadores sem classificação receberam comportamentos sorteados. Os seis tipos foram distribuídos em partes iguais, com diferença de até um jogador.");
        }
        string hash = Hash(request, assigned);
        return new(Guid.NewGuid(), request.Name, available[0].Gender, request.Seed, assigned,
            Math.Max(0, 128 - assigned.Count), errors.Distinct().ToList(), warnings, [], DateTime.UtcNow, hash);
    }

    private static Dictionary<(string Club, string Player), PlayerBehavior> DrawMissingBehaviors(
        List<WorldPreviewClub> clubs, int seed)
    {
        var players = clubs.Where(entry => entry.Club.OfflineSource?.V11 != null && !entry.Club.IsFictional)
            .SelectMany(entry =>
            {
                var missing = (entry.Club.OfflineSource!.Definition.Players ?? [])
                    .Where(player => player.Behavior == null).Select(player => player.Id).ToHashSet();
                return entry.Club.Players.Where(player => missing.Contains(player.PlayerExternalId))
                    .Select(player => (Club: entry.Club.Key, Player: player.PlayerExternalId));
            })
            .OrderBy(key => OfflineRosterV16.Draw(seed, key.Club, $"behavior:{key.Player}"))
            .ThenBy(key => key.Club, StringComparer.Ordinal).ThenBy(key => key.Player, StringComparer.Ordinal);
        var types = Enum.GetValues<PlayerBehavior>()
            .OrderBy(behavior => OfflineRosterV16.Draw(seed, "behavior-types", ((int)behavior).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ThenBy(behavior => behavior).ToArray();
        return players.Select((key, index) => (key, behavior: types[index % types.Length]))
            .ToDictionary(item => item.key, item => item.behavior);
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
            var useDefaults = request.UseDefaultBehavior && source.V11 != null;
            if ((source.Definition.Status != "READY" && !(useDefaults && source.Definition.Status == "DRAFT")) ||
                OfflineCatalogRules.ValidateClub(source.Definition, entry.Club.Key).Any(i => i.IsError || !useDefaults || i.Code != "BEHAVIOR"))
                throw new ArgumentException("Clube incompleto na prévia.");
            if ((!request.ApplyV16RosterPolicy && entry.Club.Players.Count != source.Definition.Players!.Count) ||
                entry.Club.Players.Any(p => p.Strength is < 1 or > 50 || source.Definition.Players!.All(s => s.Id != p.PlayerExternalId)) ||
                entry.Club.Players.Select(p => p.PlayerExternalId).Distinct().Count() != entry.Club.Players.Count)
                throw new ArgumentException("Forças individuais inválidas ou ausentes na prévia.");
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
            if (c.Stadium != null)
            {
                stadium.Name = c.Stadium.Name!; stadium.BuiltCapacity = c.Stadium.Capacity!.Value;
                stadium.UsableCapacity = (int)Math.Round(stadium.BuiltCapacity * stadium.ConditionPercent / 100);
                stadium.MaintenanceReservePerSeason = Math.Round(stadium.BuiltCapacity * 4.5m, 0);
                stadium.LastAttendance = stadium.UsableCapacity / 2;
            }
            else
            {
                stadium.Name = $"Estádio de {c.Name}";
            }
            var club = new Club
            {
                Id = Guid.NewGuid(), GameWorldId = world.Id, DeterministicIndex = i + 1,
                Name = c.Name!, ShortName = c.ShortName!, Country = source.CountryCode,
                PrimaryColorHex = c.Colors![0], SecondaryColorHex = c.Colors[1],
                DivisionTier = (DivisionTier)entry.Division, Stadium = stadium,
                CashBalance = template.CashBalance, Reputation = template.Reputation, InitialSquadSize = entry.Club.Players.Count,
                ClubInitialPower = source.V11?.TeamStrength ?? entry.Club.InitialProfile?.ClubInitialPower,
                InitialSquadReference = entry.InitialSquadReference,
                OfflineIdentity = new(source.CatalogId, c.CatalogClubId!, source.CountryCode, source.Year,
                    c.ShortName!, c.Abbreviation!, source.SourceHash, source.CrestDataUrl)
            };
            var coach = c.Coach!; var profile = foundation.AiManagers.Single(m => m.Id == template.AiManagerId);
            var manager = new AiManager
            {
                Id = Guid.NewGuid(), GameWorldId = world.Id, CurrentClubId = club.Id, Name = coach.Name!,
                Identity = profile.Identity, Pragmatism = profile.Pragmatism, Adaptability = profile.Adaptability,
                FinancialAggressiveness = profile.FinancialAggressiveness, YouthPatience = profile.YouthPatience,
                PreferredFormations = (coach.PreferredTactics!.Home!.Count > 0 ? coach.PreferredTactics.Home : coach.PreferredTactics.Away!).Select(f => new FormationPreferenceWeightDto(f, 50)).ToList(),
                OfflineIdentity = new(source.CatalogId, coach.Id!, source.CountryCode, source.Year, coach.Nationality!,
                    coach.PreferredTactics.Home!.ToList(), coach.PreferredTactics.Away!.ToList())
            };
            club.AiManagerId = manager.Id; world.Clubs.Add(club); world.AiManagers.Add(manager);
            var contractDefaults = foundation.Players.Where(p => p.ClubId == template.Id).ToList();
            for (int j = 0; j < entry.Club.Players.Count; j++)
            {
                var selected = entry.Club.Players[j];
                var originalSource = request.IncludeSurplusClub && entry.Club.IsFictional
                    ? preview.Clubs.Where(e => !e.Club.IsFictional).Select(e => e.Club.OfflineSource!).Single(s => s.Definition.Players!.Any(p => p.Id == selected.PlayerExternalId))
                    : source;
                var p = originalSource.Definition.Players!.Single(p => p.Id == selected.PlayerExternalId);
                int strength = p.Strength ?? entry.Club.Players[j].Strength;
                var player = new Player
                {
                    Id = Guid.NewGuid(), GameWorldId = world.Id, ClubId = club.Id, Name = p.Name!,
                    Position = Enum.Parse<PlayerPosition>(p.Position!), Strength = strength,
                    Behavior = p.Behavior is int behavior ? (PlayerBehavior)behavior :
                        selected.InitialBehavior ?? throw new ArgumentException("Comportamento sorteado ausente. Atualize a prévia."),
                    IsStar = p.IsStar!.Value, Nationality = p.Nationality,
                    ContractRoundsRemaining = contractDefaults[j % contractDefaults.Count].ContractRoundsRemaining,
                    OfflineIdentity = new(originalSource.CatalogId, p.Id!, originalSource.CountryCode, originalSource.Year)
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
        string schemaVersion = preview.Clubs.Any(c => OfflineCatalogRanking.IsV11(c.Club)) ? "1.1" : OfflineCatalogRules.Version;
        var origin = new OfflineWorldOrigin(schemaVersion, preview.DataHash, preview.CreatedAt, preview.Clubs);
        world.OfflineCatalogOrigin = JsonSerializer.Deserialize<OfflineWorldOrigin>(JsonSerializer.Serialize(origin));
        world.History = new(); HistoryEngine.Observe(world, beginningSeason: true);
        return world;
    }
}
