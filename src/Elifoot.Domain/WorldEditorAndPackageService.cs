using System.Globalization;
using System.Text;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class WorldEditorAndPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static WorldPackageExportDto ExportWorldToPackage(GameWorld world, string packageName = "Elifoot98-WorldPackage")
    {
        return new WorldPackageExportDto(
            FormatVersion: "1.0",
            PackageName: packageName,
            Seed: world.Seed,
            ExportedAtUtc: DateTime.UtcNow,
            AllowClassic505: world.AllowClassic505,
            DefaultTickIntervalMs: world.TickIntervalMs,
            Clubs: world.Clubs.OrderBy(c => c.DeterministicIndex).Select(c => new ClubExportDto(
                c.Id,
                c.Name,
                c.ShortName,
                c.Country,
                c.PrimaryColorHex,
                c.SecondaryColorHex,
                c.DivisionTier,
                c.CashBalance,
                c.Reputation,
                c.Stadium.Name,
                c.Stadium.BuiltCapacity,
                c.Stadium.UsableCapacity,
                c.Stadium.ConditionPercent,
                c.Stadium.MaintenanceReservePerSeason)).ToList(),
            Players: world.Players.Select(p => new PlayerExportDto(
                p.Id,
                p.ClubId,
                p.Name,
                p.Position,
                p.Strength,
                p.Behavior,
                p.IsStar,
                p.ContractRoundsRemaining,
                p.SalaryPerRound,
                p.MarketPrice,
                p.PhotoUrl,
                p.PhotoLicense,
                p.PhotoCredit,
                p.NegotiatedContractProtected)).ToList(),
            AiManagers: world.AiManagers.Select(m => new AiManagerExportDto(
                m.Id,
                m.CurrentClubId,
                m.Name,
                m.Identity,
                m.Pragmatism,
                m.Adaptability,
                m.FinancialAggressiveness,
                m.YouthPatience,
                m.PreferredFormations)).ToList(),
            Gender: world.Gender,
            CatalogOrigin: world.CatalogOrigin,
            ForceEvolutionMode: world.ForceEvolutionMode);
    }

    public static string SerializePackageToJson(WorldPackageExportDto package)
        => JsonSerializer.Serialize(package, JsonOptions);

    public static WorldPackageExportDto? DeserializePackageFromJson(string json)
        => JsonSerializer.Deserialize<WorldPackageExportDto>(json, JsonOptions);

    /// <summary>
    /// Exports players to CSV for external spreadsheet editing.
    /// Columns: Id,ClubId,Name,Position,Strength,Behavior,IsStar,ContractRounds,SalaryPerRound,MarketPrice,PhotoUrl,PhotoLicense,PhotoCredit
    /// </summary>
    public static string ExportPlayersToCsv(GameWorld world)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,ClubId,Name,Position,Strength,Behavior,IsStar,ContractRounds,SalaryPerRound,MarketPrice,PhotoUrl,PhotoLicense,PhotoCredit");
        foreach (var p in world.Players)
        {
            sb.AppendLine(string.Join(",",
                p.Id,
                p.ClubId?.ToString() ?? "",
                EscapeCsv(p.Name),
                p.Position,
                p.Strength,
                p.Behavior,
                p.IsStar ? "true" : "false",
                p.ContractRoundsRemaining,
                p.SalaryPerRound.ToString(CultureInfo.InvariantCulture),
                p.MarketPrice.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(p.PhotoUrl ?? ""),
                EscapeCsv(p.PhotoLicense ?? ""),
                EscapeCsv(p.PhotoCredit ?? "")));
        }
        return sb.ToString();
    }

    public static (List<PlayerExportDto> Players, List<string> ParseErrors) ParsePlayersFromCsv(string csvContent)
    {
        var players = new List<PlayerExportDto>();
        var errors = new List<string>();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            errors.Add("CSV vazio ou sem linhas de dados.");
            return (players, errors);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 10)
            {
                errors.Add($"Linha {i + 1}: número insuficiente de colunas ({cols.Count}).");
                continue;
            }

            if (!Guid.TryParse(cols[0], out var id)) id = Guid.NewGuid();
            Guid? clubId = Guid.TryParse(cols[1], out var cid) ? cid : null;
            string name = cols[2].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Linha {i + 1}: nome do jogador vazio.");
                continue;
            }
            if (!Enum.TryParse<PlayerPosition>(cols[3], true, out var pos))
            {
                errors.Add($"Linha {i + 1}: posição inválida '{cols[3]}' (use G, D, M, A).");
                continue;
            }
            if (!int.TryParse(cols[4], out int strength) || strength < 1 || strength > 50)
            {
                errors.Add($"Linha {i + 1}: força '{cols[4]}' fora da faixa 1..50.");
                continue;
            }
            if (!Enum.TryParse<PlayerBehavior>(cols[5], true, out var behavior))
            {
                behavior = PlayerBehavior.Cavalheiro;
            }
            bool isStar = cols[6].Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || cols[6].Trim() == "1" || cols[6].Trim() == "*";
            int.TryParse(cols[7], out int contractRounds);
            decimal.TryParse(cols[8], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal salary);
            decimal.TryParse(cols[9], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price);

            string? photoUrl = cols.Count > 10 && !string.IsNullOrWhiteSpace(cols[10]) ? cols[10].Trim() : null;
            string? photoLic = cols.Count > 11 && !string.IsNullOrWhiteSpace(cols[11]) ? cols[11].Trim() : null;
            string? photoCred = cols.Count > 12 && !string.IsNullOrWhiteSpace(cols[12]) ? cols[12].Trim() : null;

            players.Add(new PlayerExportDto(id, clubId, name, pos, strength, behavior, isStar,
                Math.Max(1, contractRounds), Math.Max(0m, salary), Math.Max(0m, price), photoUrl, photoLic, photoCred));
        }

        return (players, errors);
    }

    /// <summary>
    /// Validates a world package against all domain rules:
    /// - Unique IDs for clubs, players, managers
    /// - Player strength 1..50, non-negative values
    /// - Every club has 11..24 players, >= 1 Goalkeeper (G), >= 10 outfield players
    /// - Every AI manager's preferred formation weights sum to 100%
    /// - Generates a diff preview if compared against an existing GameWorld
    /// </summary>
    public static PackageValidationResultDto ValidatePackage(WorldPackageExportDto package, GameWorld? baselineWorld = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var diffs = new List<string>();

        if (package.Clubs.Count < 32)
            errors.Add($"O pacote precisa conter pelo menos 32 clubes de divisão (encontrado: {package.Clubs.Count}).");

        if (package.Clubs.Select(c => c.Id).Distinct().Count() != package.Clubs.Count)
            errors.Add("Existem IDs de clubes duplicados no pacote.");

        if (package.Players.Select(p => p.Id).Distinct().Count() != package.Players.Count)
            errors.Add("Existem IDs de jogadores duplicados no pacote.");

        foreach (var p in package.Players)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                errors.Add($"Jogador {p.Id} está sem nome.");
            if (p.Strength < 1 || p.Strength > 50)
                errors.Add($"Jogador {p.Name} possui força {p.Strength} fora do intervalo permitido 1..50.");
            if (p.SalaryPerRound < 0m || p.MarketPrice < 0m || p.ContractRoundsRemaining < 0)
                errors.Add($"Jogador {p.Name} possui valores negativos de salário, preço ou contrato.");
            if (!string.IsNullOrWhiteSpace(p.PhotoUrl) && string.IsNullOrWhiteSpace(p.PhotoLicense))
                warnings.Add($"Jogador {p.Name} possui PhotoUrl sem licença explícita (PhotoLicense recomendado).");
        }

        var playersByClub = package.Players
            .Where(p => p.ClubId != null)
            .GroupBy(p => p.ClubId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var c in package.Clubs)
        {
            if (c.CashBalance < 0m)
                errors.Add($"Clube {c.Name} possui caixa negativo ({c.CashBalance}).");
            if (c.BuiltCapacity <= 0 || c.UsableCapacity <= 0 || c.UsableCapacity > c.BuiltCapacity)
                errors.Add($"Clube {c.Name} possui capacidades de estádio inválidas ({c.UsableCapacity}/{c.BuiltCapacity}).");

            var squad = playersByClub.GetValueOrDefault(c.Id) ?? new List<PlayerExportDto>();
            if (squad.Count < SquadValidator.MinSquadSize || squad.Count > SquadValidator.MaxSquadSize)
                errors.Add($"Clube {c.Name} possui {squad.Count} atletas (deve ter entre {SquadValidator.MinSquadSize} e {SquadValidator.MaxSquadSize}).");

            int gks = squad.Count(p => p.Position == PlayerPosition.G);
            int outfield = squad.Count(p => p.Position != PlayerPosition.G);
            if (gks < 1)
                errors.Add($"Clube {c.Name} não possui nenhum goleiro (G).");
            if (outfield < 10)
                errors.Add($"Clube {c.Name} possui apenas {outfield} jogadores de linha (mínimo 10).");
        }

        foreach (var m in package.AiManagers)
        {
            if (m.PreferredFormations.Count is < 1 or > 3)
                errors.Add($"Técnico IA {m.Name} deve possuir entre 1 e 3 táticas preferidas.");
            int sumWeights = m.PreferredFormations.Sum(f => f.WeightPercent);
            if (sumWeights != 100)
                errors.Add($"Técnico IA {m.Name} possui soma de pesos táticos igual a {sumWeights}% (deve ser exatamente 100%).");

            foreach (var f in m.PreferredFormations)
            {
                if (!TacticalFormations.TryGet(f.Formation, package.AllowClassic505, out _))
                    errors.Add($"Técnico IA {m.Name} referencia formação inválida '{f.Formation}'.");
            }
        }

        if (baselineWorld != null)
        {
            var baseClubDict = baselineWorld.Clubs.ToDictionary(c => c.Id);
            foreach (var c in package.Clubs)
            {
                if (!baseClubDict.TryGetValue(c.Id, out var oldClub))
                    diffs.Add($"+ Novo clube: {c.Name} ({c.DivisionTier})");
                else if (oldClub.Name != c.Name || oldClub.CashBalance != c.CashBalance || oldClub.DivisionTier != c.DivisionTier)
                    diffs.Add($"~ Clube alterado: {oldClub.Name} -> {c.Name} (Caixa: {oldClub.CashBalance:N0} -> {c.CashBalance:N0})");
            }

            var basePlayerDict = baselineWorld.Players.ToDictionary(p => p.Id);
            foreach (var p in package.Players)
            {
                if (!basePlayerDict.TryGetValue(p.Id, out var oldP))
                    diffs.Add($"+ Novo jogador: {p.Name}{(p.IsStar ? "*" : "")} ({p.Position}, F:{p.Strength})");
                else if (oldP.Name != p.Name || oldP.Strength != p.Strength || oldP.IsStar != p.IsStar || oldP.Behavior != p.Behavior)
                    diffs.Add($"~ Jogador alterado: {oldP.DisplayName} (F:{oldP.Strength}) -> {p.Name}{(p.IsStar ? "*" : "")} (F:{p.Strength}, {p.Behavior})");
            }
        }

        return new PackageValidationResultDto(
            IsValid: errors.Count == 0,
            ClubCount: package.Clubs.Count,
            PlayerCount: package.Players.Count,
            ManagerCount: package.AiManagers.Count,
            Errors: errors,
            Warnings: warnings,
            DiffPreview: diffs.Take(50).ToList());
    }

    /// <summary>
    /// Creates a brand new GameWorld (or versioned save clone) from a validated WorldPackageExportDto.
    /// Never mutates a live mid-match round arbitrarily.
    /// </summary>
    public static (GameWorld? World, PackageValidationResultDto Validation) CreateWorldFromPackage(
        WorldPackageExportDto package,
        string newWorldName)
    {
        var validation = ValidatePackage(package);
        if (!validation.IsValid) return (null, validation);

        // Old catalog packages predate the mode field, but their creator always used ClassicAdapted.
        var forceMode = package.ForceEvolutionMode ?? (package.CatalogOrigin != null
            ? ForceEvolutionMode.ClassicAdapted : ForceEvolutionMode.LegacySeasonal);
        if (!Enum.IsDefined(forceMode)) throw new ArgumentException("Modo de evolução inválido no pacote.");
        var world = new GameWorld
        {
            ForceEvolutionMode = forceMode,
            Id = Guid.NewGuid(),
            Name = newWorldName,
            InviteCode = $"ED{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Seed = package.Seed,
            TotalClubs = package.Clubs.Count,
            AllowClassic505 = package.AllowClassic505,
            TickIntervalMs = package.DefaultTickIntervalMs,
            SeasonNumber = 1,
            RoundNumber = 1,
            RoundState = RoundState.PREPARING
        };

        int idx = 1;
        foreach (var cDto in package.Clubs)
        {
            world.Clubs.Add(new Club
            {
                Id = cDto.Id,
                GameWorldId = world.Id,
                DeterministicIndex = idx++,
                Name = cDto.Name,
                ShortName = cDto.ShortName,
                Country = cDto.Country,
                PrimaryColorHex = cDto.PrimaryColorHex,
                SecondaryColorHex = cDto.SecondaryColorHex,
                DivisionTier = cDto.DivisionTier,
                CashBalance = cDto.CashBalance,
                Reputation = cDto.Reputation,
                Stadium = new Stadium
                {
                    Name = cDto.StadiumName,
                    BuiltCapacity = cDto.BuiltCapacity,
                    UsableCapacity = cDto.UsableCapacity,
                    ConditionPercent = cDto.ConditionPercent,
                    MaintenanceReservePerSeason = cDto.MaintenanceReserve
                }
            });
        }

        foreach (var pDto in package.Players)
        {
            world.Players.Add(new Player
            {
                Id = pDto.Id,
                GameWorldId = world.Id,
                ClubId = pDto.ClubId,
                Name = pDto.Name,
                Position = pDto.Position,
                Strength = pDto.Strength,
                Behavior = pDto.Behavior,
                IsStar = pDto.IsStar,
                ContractRoundsRemaining = pDto.ContractRoundsRemaining,
                SalaryPerRound = pDto.SalaryPerRound,
                NegotiatedContractProtected = pDto.NegotiatedContractProtected ?? (pDto.ContractRoundsRemaining > 0),
                MarketPrice = pDto.MarketPrice,
                PhotoUrl = pDto.PhotoUrl,
                PhotoLicense = pDto.PhotoLicense,
                PhotoCredit = pDto.PhotoCredit
            });
        }

        var clubById = world.Clubs.ToDictionary(c => c.Id);
        foreach (var mDto in package.AiManagers)
        {
            var mgr = new AiManager
            {
                Id = mDto.Id,
                GameWorldId = world.Id,
                CurrentClubId = mDto.AssignedClubId,
                Name = mDto.Name,
                Identity = mDto.Identity,
                Pragmatism = mDto.Pragmatism,
                Adaptability = mDto.Adaptability,
                FinancialAggressiveness = mDto.FinancialAggressiveness,
                YouthPatience = mDto.YouthPatience,
                PreferredFormations = mDto.PreferredFormations
            };
            world.AiManagers.Add(mgr);

            if (mDto.AssignedClubId != null && clubById.TryGetValue(mDto.AssignedClubId.Value, out var club))
            {
                club.AiManagerId = mgr.Id;
                var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
                AiManagerBrain.PlanAndSelectLineup(mgr, club, squad, null, null, true, world.Seed, 1, 1, world.AllowClassic505);
            }
        }

        if (!Enum.IsDefined(package.Gender)) throw new ArgumentException("Categoria inválida no pacote.");
        world.SetImportedCategory(package.Gender, package.CatalogOrigin);
        world.Economy.OperatingRules = OperatingEconomyEngine.CurrentRules();
        ClassicEconomyEngine.EnsureMigrated(world, isNewWorld: true);
        foreach (var club in world.Clubs)
            world.Transactions.Add(new FinancialTransaction { GameWorldId = world.Id, ClubId = club.Id,
                SeasonNumber = 1, RoundNumber = 1, Category = FinancialTransactionCategory.InitialBalance,
                Amount = club.CashBalance, BalanceAfter = club.CashBalance,
                Description = "Saldo inicial do mundo criado por pacote", IdempotencyKey = $"init:{club.Id}" });
        CompetitionAndSeasonEngine.InitializeSeasonFixtures(world);
        return (world, validation);
    }

    private static string EscapeCsv(string val)
    {
        if (val.Contains(',') || val.Contains('"'))
            return $"\"{val.Replace("\"", "\"\"")}\"";
        return val;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
