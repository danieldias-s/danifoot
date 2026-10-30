using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class WorldSeedFactory
{
    private static readonly (string Name, string Short, string Primary, string Secondary, string Stadium)[] DivisionTemplates =
    [
        // 1ª Divisão (8 clubes)
        ("Atlético Paulistano", "APA", "#C8102E", "#FFFFFF", "Arena Parque Sul"),
        ("Grêmio Guanabara", "GGU", "#000000", "#CC0000", "Estádio da Colina"),
        ("Palestra Verde", "PAL", "#006437", "#FFFFFF", "Jardim das Palmeiras"),
        ("Sport Club Atlântico", "SCA", "#0033A0", "#FFFFFF", "Arena Beira-Mar"),
        ("Real Pampulha", "RPA", "#002B7F", "#FCD116", "Gigante da Lagoa"),
        ("Internacional do Sul", "INS", "#D50000", "#FFFFFF", "Estádio dos Pampas"),
        ("Estrela Solitária", "EST", "#111111", "#FFFFFF", "Estádio Olímpico Norte"),
        ("Esporte Clube Litoral", "ECL", "#FFFFFF", "#000000", "Vila do Porto"),

        // 2ª Divisão (8 clubes)
        ("Coritiba Imperial", "CIM", "#005A32", "#FFFFFF", "Alto da Glória"),
        ("Fortaleza do Sol", "FOR", "#0038A8", "#CE1126", "Arena Castelinho"),
        ("Bahia de Todos", "BAH", "#0046AD", "#E4002B", "Fonte Imperial"),
        ("Goiás Esmeralda", "GOI", "#006837", "#FFFFFF", "Serra Dourada Sul"),
        ("Leão da Ilha", "LEI", "#C60C30", "#111111", "Ilha do Retiro Novo"),
        ("Ponte Alvinegra", "PON", "#000000", "#FFFFFF", "Moisés Central"),
        ("Guarani da Serra", "GUA", "#007A33", "#FFFFFF", "Brinco de Ouro"),
        ("Ceará Alvinegro", "CEA", "#1C1C1C", "#FFFFFF", "Vovozão Arena"),

        // 3ª Divisão (8 clubes)
        ("Juventude Serrana", "JUV", "#00843D", "#FFFFFF", "Alfredo da Serra"),
        ("Criciúma Carvoeiro", "CRI", "#FFD100", "#000000", "Majestoso do Sul"),
        ("Paysandu da Amazônia", "PAY", "#009CDE", "#FFFFFF", "Curuzu Real"),
        ("Remo Azulino", "REM", "#00205B", "#FFFFFF", "Baenão Norte"),
        ("Náutico Capibaribe", "NAU", "#E4002B", "#FFFFFF", "Aflitos Clássico"),
        ("Santa Cruz Coral", "STC", "#D50000", "#000000", "Arruda Popular"),
        ("Vila Nova Colorada", "VIL", "#C8102E", "#FFFFFF", "Oba Central"),
        ("Avaí da Ilha", "AVA", "#005596", "#FFFFFF", "Ressacada Sul"),

        // 4ª Divisão (8 clubes)
        ("Ferroviária Grená", "FER", "#6F1D1B", "#FFFFFF", "Fonte Luminosa"),
        ("Joinville Tricolor", "JOI", "#000000", "#E4002B", "Arena Joinville"),
        ("Brasil de Pelotas", "BPE", "#C8102E", "#000000", "Bento Freitas"),
        ("Sampaio Boliviano", "SAM", "#007A33", "#FFD100", "Castelão Maranhense"),
        ("ABC Potiguar", "ABC", "#111111", "#FFFFFF", "Frasqueirão"),
        ("América Mecão", "AME", "#DA291C", "#FFFFFF", "Arena das Dunas Sul"),
        ("Botafogo da Paraíba", "BPB", "#000000", "#E4002B", "Almeidão"),
        ("CSA Azulão", "CSA", "#0033A0", "#FFFFFF", "Rei Pelé")
    ];

    private static readonly string[] DistrictPrefixes =
    [
        "União", "Operário", "Comercial", "Ferroviário", "Independente", "Nacional",
        "Rio Branco", "São Bento", "XV de", "Atlético", "Esportivo", " Desportivo",
        "Associação", "Grêmio", "Real", "Cruzeiro", "Estrela", " Primavera"
    ];

    private static readonly string[] DistrictCities =
    [
        "Sorocaba", "Piracicaba", "Ribeirão", "Bauru", "Marília", "Londrina", "Maringá", "Cascavel",
        "Ponta Grossa", "Blumenau", "Chapecó", "Itajaí", "Caxias", "Passo Fundo", "Novo Hamburgo", "Bagé",
        "Uberlândia", "Juiz de Fora", "Ipatinga", "Montes Claros", "Varginha", "Governador", "Volta Redonda", "Campos",
        "Petrópolis", "Macaé", "Anápolis", "Rio Verde", "Itumbiara", "Cuiabá", "Rondonópolis", "Dourados",
        "Campo Grande", "Palmas", "Porto Velho", "Rio Branco", "Macapá", "Boa Vista", "Santarém", "Marabá",
        "Imperatriz", "Parnaíba", "Sobral", "Juazeiro", "Mossoró", "Campina Grande", "Caruaru", "Petrolina",
        "Arapiraca", "Itabuna", "Feira", "Vitória da Conquista", "Ilhéus", "Cachoeiro", "Colatina", "Linhares"
    ];

    private static readonly string[] FirstNames =
    [
        "João", "Pedro", "Lucas", "Mateus", "Gabriel", "Rafael", "Bruno", "Carlos", "Eduardo", "Felipe",
        "Gustavo", "Henrique", "Igor", "Júlio", "Leandro", "Marcelo", "Nílton", "Otávio", "Paulo", "Renato",
        "Sérgio", "Thiago", "Vítor", "Wagner", "Zé", "André", "Caio", "Daniel", "Fábio", "Alex"
    ];

    private static readonly string[] LastNames =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Pereira", "Ferreira", "Alves", "Lima", "Gomes", "Ribeiro",
        "Martins", "Carvalho", "Almeida", "Lopes", "Soares", "Fernandes", "Vieira", "Barbosa", "Rocha", "Dias",
        "Nascimento", "Andrade", "Moreira", "Nunes", "Marques", "Machado", "Mendes", "Freitas", "Cardoso", "Teixeira"
    ];

    private static readonly (ManagerTacticalIdentity Identity, string f1, int w1, string f2, int w2, string f3, int w3)[] TacticalProfiles =
    [
        (ManagerTacticalIdentity.Ofensivo, "4-3-3", 60, "3-4-3", 25, "4-4-2", 15),
        (ManagerTacticalIdentity.Ofensivo, "3-4-3", 55, "4-3-3", 30, "4-2-4", 15),
        (ManagerTacticalIdentity.Ofensivo, "4-3-3", 50, "4-4-2", 30, "3-5-2", 20),
        (ManagerTacticalIdentity.Equilibrado, "4-4-2", 60, "4-3-3", 25, "4-5-1", 15),
        (ManagerTacticalIdentity.Equilibrado, "3-5-2", 55, "4-4-2", 30, "5-3-2", 15),
        (ManagerTacticalIdentity.Equilibrado, "4-4-2", 50, "3-5-2", 30, "4-3-3", 20),
        (ManagerTacticalIdentity.Defensivo, "5-3-2", 60, "5-4-1", 25, "4-4-2", 15),
        (ManagerTacticalIdentity.Defensivo, "4-5-1", 55, "5-3-2", 30, "3-6-1", 15),
        (ManagerTacticalIdentity.Defensivo, "5-4-1", 50, "5-3-2", 35, "4-5-1", 15)
    ];

    public static GameWorld CreateSeededWorld(
        string worldName = "Campeonato Brasileiro Clássico",
        int seed = 199801,
        int totalClubs = 128,
        int maxHumans = 8,
        int tickIntervalMs = 200,
        int pauseTimeoutSeconds = 60,
        bool allowClassic505 = true,
        string? inviteCode = null)
    {
        totalClubs = Math.Max(64, totalClubs);
        var rng = new DeterministicRng(seed);

        var world = new GameWorld
        {
            Id = Guid.NewGuid(),
            Name = worldName,
            InviteCode = inviteCode ?? $"ELI{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Seed = seed,
            MaxHumans = Math.Clamp(maxHumans, 1, 8),
            TotalClubs = totalClubs,
            SeasonNumber = 1,
            RoundNumber = 1,
            RoundState = RoundState.PREPARING,
            RoundVersion = 1,
            TickIntervalMs = tickIntervalMs,
            PauseTimeoutSeconds = pauseTimeoutSeconds,
            AllowClassic505 = allowClassic505
        };

        // 1. Generate all clubs (32 Division clubs + (totalClubs - 32) District clubs)
        for (int i = 0; i < totalClubs; i++)
        {
            DivisionTier tier = i switch
            {
                < 8 => DivisionTier.PrimeiraDivisao,
                < 16 => DivisionTier.SegundaDivisao,
                < 24 => DivisionTier.TerceiraDivisao,
                < 32 => DivisionTier.QuartaDivisao,
                _ => DivisionTier.Distrital
            };

            string clubName;
            string shortName;
            string primaryColor;
            string secondaryColor;
            string stadiumName;

            if (i < DivisionTemplates.Length)
            {
                var tpl = DivisionTemplates[i];
                clubName = tpl.Name;
                shortName = tpl.Short;
                primaryColor = tpl.Primary;
                secondaryColor = tpl.Secondary;
                stadiumName = tpl.Stadium;
            }
            else
            {
                int distIdx = i - 32;
                string prefix = DistrictPrefixes[distIdx % DistrictPrefixes.Length].Trim();
                string city = DistrictCities[(distIdx * 7 + i) % DistrictCities.Length];
                clubName = $"{prefix} de {city} {(distIdx >= DistrictCities.Length ? (distIdx / DistrictCities.Length + 1).ToString() : "")}".Trim();
                shortName = $"{prefix[0]}{city[..Math.Min(2, city.Length)].ToUpperInvariant()}{(distIdx % 10)}";
                string[] palette = ["#006400", "#0033A0", "#C8102E", "#111111", "#6F1D1B", "#FF8C00", "#4B0082", "#008080"];
                primaryColor = palette[i % palette.Length];
                secondaryColor = "#FFFFFF";
                stadiumName = $"Municipal de {city}";
            }

            var (builtCap, cash, rep, minStr, maxStr) = tier switch
            {
                DivisionTier.PrimeiraDivisao => (rng.NextInt(32_000, 52_000), 4_200_000m, rng.NextInt(78, 95), 34, 48),
                DivisionTier.SegundaDivisao => (rng.NextInt(18_000, 30_000), 2_600_000m, rng.NextInt(62, 77), 27, 39),
                DivisionTier.TerceiraDivisao => (rng.NextInt(10_000, 18_000), 1_650_000m, rng.NextInt(48, 61), 21, 32),
                DivisionTier.QuartaDivisao => (rng.NextInt(6_000, 11_000), 1_100_000m, rng.NextInt(35, 47), 16, 26),
                _ => (rng.NextInt(3_000, 6_500), 650_000m, rng.NextInt(18, 34), 10, 21)
            };

            double cond = Math.Round(rng.NextDouble(88.0, 99.0), 1);
            int usableCap = (int)Math.Round(builtCap * (cond / 100.0));

            var club = new Club
            {
                Id = CreateDeterministicGuid(seed, 2, i + 1),
                GameWorldId = world.Id,
                DeterministicIndex = i + 1,
                Name = clubName,
                ShortName = shortName,
                Country = "Brasil",
                PrimaryColorHex = primaryColor,
                SecondaryColorHex = secondaryColor,
                DivisionTier = tier,
                CashBalance = cash,
                Reputation = rep,
                Stadium = new Stadium
                {
                    Name = stadiumName,
                    BuiltCapacity = builtCap,
                    UsableCapacity = usableCap,
                    ConditionPercent = cond,
                    MaintenanceReservePerSeason = Math.Round(builtCap * 4.5m, 0),
                    FixedMatchCostRate = 0.70m,
                    VariableAttendeeCostRate = 1.10m,
                    LastTicketPrice = tier == DivisionTier.PrimeiraDivisao ? 24m : 14m,
                    LastAttendance = usableCap / 2
                }
            };

            // Create AI Manager for the club
            var profile = TacticalProfiles[(i + seed) % TacticalProfiles.Length];
            string mgrName = $"{FirstNames[(i * 3 + 5) % FirstNames.Length]} {LastNames[(i * 5 + 11) % LastNames.Length]}";
            var aiMgr = new AiManager
            {
                Id = CreateDeterministicGuid(seed, 3, i + 1),
                GameWorldId = world.Id,
                CurrentClubId = club.Id,
                Name = mgrName,
                Identity = profile.Identity,
                Pragmatism = rng.NextInt(30, 85),
                Adaptability = rng.NextInt(35, 90),
                FinancialAggressiveness = rng.NextInt(25, 80),
                YouthPatience = rng.NextInt(30, 85),
                PreferredFormations =
                [
                    new FormationPreferenceWeightDto(profile.f1, profile.w1),
                    new FormationPreferenceWeightDto(profile.f2, profile.w2),
                    new FormationPreferenceWeightDto(profile.f3, profile.w3)
                ]
            };

            club.AiManagerId = aiMgr.Id;
            world.AiManagers.Add(aiMgr);

            // Generate 18 players per club: 2 G, 6 D, 6 M, 4 A (strictly within 11..24, >=1 G, >=10 outfield)
            PlayerPosition[] squadTemplate =
            [
                PlayerPosition.G, PlayerPosition.G,
                PlayerPosition.D, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D,
                PlayerPosition.M, PlayerPosition.M, PlayerPosition.M, PlayerPosition.M, PlayerPosition.M, PlayerPosition.M,
                PlayerPosition.A, PlayerPosition.A, PlayerPosition.A, PlayerPosition.A
            ];

            var clubSquad = new List<Player>(squadTemplate.Length);
            for (int pIdx = 0; pIdx < squadTemplate.Length; pIdx++)
            {
                var pos = squadTemplate[pIdx];
                int strength = rng.NextInt(minStr, maxStr + 1);
                bool isStar = (pIdx == 8 || pIdx == 14) && rng.NextDouble() < (tier == DivisionTier.PrimeiraDivisao ? 0.65 : 0.35);

                // Behavior distribution covering FairPlay..Sarrafeiro
                int behRoll = rng.NextInt(0, 100);
                PlayerBehavior behavior = behRoll switch
                {
                    < 14 => PlayerBehavior.FairPlay,
                    < 32 => PlayerBehavior.Cordeirinho,
                    < 62 => PlayerBehavior.Cavalheiro,
                    < 82 => PlayerBehavior.Caneleiro,
                    < 94 => PlayerBehavior.Caceteiro,
                    _ => PlayerBehavior.Sarrafeiro
                };

                string pName = $"{FirstNames[rng.NextInt(0, FirstNames.Length)]} {LastNames[rng.NextInt(0, LastNames.Length)]}";
                decimal salary = MarketAndEconomyEngine.CalculateFairSalaryPerRound(strength, isStar, pos);

                var player = new Player
                {
                    Id = CreateDeterministicGuid(seed, 4, i * 100 + pIdx + 1),
                    GameWorldId = world.Id,
                    ClubId = club.Id,
                    Name = pName,
                    Position = pos,
                    Strength = strength,
                    Behavior = behavior,
                    IsStar = isStar,
                    ContractRoundsRemaining = rng.NextInt(12, 28),
                    SalaryPerRound = salary
                };
                player.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(player);

                // List ~1 surplus reserve per division club so the transfer list is immediately populated from Round 1
                if (tier != DivisionTier.Distrital && pIdx == squadTemplate.Length - 1)
                {
                    player.IsListedForTransfer = true;
                    player.AskingPrice = Math.Round(player.MarketPrice * 0.95m, 0);
                    player.ListedAtRound = 1;
                }

                clubSquad.Add(player);
                world.Players.Add(player);
            }

            // Initialize initial AI lineup & squad plan
            AiManagerBrain.PlanAndSelectLineup(aiMgr, club, clubSquad, null, null, true, seed, 1, 1, allowClassic505);
            world.Clubs.Add(club);

            // Record initial cashbook entry
            world.Transactions.Add(new FinancialTransaction
            {
                GameWorldId = world.Id,
                ClubId = club.Id,
                SeasonNumber = 1,
                RoundNumber = 1,
                Category = FinancialTransactionCategory.InitialBalance,
                Amount = club.CashBalance,
                BalanceAfter = club.CashBalance,
                Description = "Saldo inicial de fundação da temporada",
                IdempotencyKey = $"init:{club.Id}"
            });
        }

        // Create 4 initial free-agent auction items so the Auction screen is immediately active on Round 1
        PlayerPosition[] auctionPositions = [PlayerPosition.A, PlayerPosition.M, PlayerPosition.D, PlayerPosition.G];
        for (int aIdx = 0; aIdx < auctionPositions.Length; aIdx++)
        {
            var pos = auctionPositions[aIdx];
            int str = rng.NextInt(28, 43);
            bool star = aIdx < 2;
            var freePlayer = new Player
            {
                Id = CreateDeterministicGuid(seed, 5, aIdx + 1),
                GameWorldId = world.Id,
                ClubId = null,
                Name = $"{FirstNames[(aIdx * 7 + 3) % FirstNames.Length]} {LastNames[(aIdx * 9 + 4) % LastNames.Length]}",
                Position = pos,
                Strength = str,
                Behavior = aIdx == 0 ? PlayerBehavior.FairPlay : PlayerBehavior.Cavalheiro,
                IsStar = star,
                ContractRoundsRemaining = 22,
                SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(str, star, pos)
            };
            freePlayer.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(freePlayer);
            world.Players.Add(freePlayer);

            world.Auctions.Add(new AuctionItem
            {
                Id = CreateDeterministicGuid(seed, 6, aIdx + 1),
                GameWorldId = world.Id,
                PlayerId = freePlayer.Id,
                MinimumBid = Math.Round(freePlayer.MarketPrice * 0.65m, 0),
                CurrentBid = 0m,
                CreatedAtSeason = 1,
                CreatedAtRound = 1,
                ExpiresAtRound = 3
            });
        }

        // Initialize Season 1 fixtures (14 League rounds + Cup Preliminary & Knockout schedule)
        CompetitionAndSeasonEngine.InitializeSeasonFixtures(world);

        return world;
    }

    public static Guid CreateDeterministicGuid(int seed, int category, int index)
    {
        var rng = new DeterministicRng(((long)seed << 32) ^ ((long)category << 20) ^ index);
        byte[] bytes = new byte[16];
        ulong part1 = rng.NextUInt64();
        ulong part2 = rng.NextUInt64();
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), part1);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 8), part2);
        // Set RFC 4122 version 4 and variant bits for clean standard GUID formatting
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
