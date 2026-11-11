using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class CompetitionAndSeasonEngine
{
    public static List<CompetitionType> BuildSeasonRoundSchedule(int totalClubs)
        => BuildSeasonRoundSchedule(new GameWorld
        {
            TotalClubs = totalClubs,
            LeagueFirstSchedule = totalClubs is 64 or 128 or 256 or 512 or 1024
        });

    public static List<CompetitionType> BuildSeasonRoundSchedule(GameWorld world)
    {
        List<CompetitionType> cupRounds =
        [
            CompetitionType.CupRoundOf64, CompetitionType.CupRoundOf32,
            CompetitionType.CupRoundOf16, CompetitionType.CupQuarterFinal,
            CompetitionType.CupSemiFinal, CompetitionType.CupFinal
        ];
        var schedule = new List<CompetitionType>();
        if (world.LeagueFirstSchedule)
        {
            if (world.TotalClubs >= 128) cupRounds.Insert(0, CompetitionType.CupRoundOf128);
            if (world.TotalClubs >= 256) cupRounds.Insert(0, CompetitionType.CupRoundOf256);
            if (world.TotalClubs >= 512) cupRounds.Insert(0, CompetitionType.CupRoundOf512);
            if (world.TotalClubs >= 1024) cupRounds.Insert(0, CompetitionType.CupRoundOf1024);
        }
        else if (world.TotalClubs - 32 > 32)
        {
            // A loaded season keeps the round numbers of its persisted fixtures.
            schedule.Add(CompetitionType.CupPreliminary1);
            schedule.Add(CompetitionType.CupPreliminary2);
        }

        int cupIndex = 0;
        int interleavedCupRounds = world.LeagueFirstSchedule ? cupRounds.Count - 1 : cupRounds.Count;
        for (int pair = 0; pair < 7; pair++)
        {
            schedule.Add(CompetitionType.League);
            schedule.Add(CompetitionType.League);
            if (cupIndex < interleavedCupRounds) schedule.Add(cupRounds[cupIndex++]);
        }
        if (world.LeagueFirstSchedule) schedule.AddRange(cupRounds.Skip(cupIndex));
        return schedule;
    }

    public static CompetitionType GetCompetitionTypeForRound(GameWorld world, int roundNumber)
    {
        var schedule = BuildSeasonRoundSchedule(world);
        int idx = Math.Clamp(roundNumber - 1, 0, schedule.Count - 1);
        return schedule[idx];
    }

    /// <summary>
    /// Generates all 14 double round-robin League rounds for the 4 divisions (8 clubs each = 4 matches per division per league round = 16 matches per league round)
    /// for the season.
    /// Cup knockout rounds are generated dynamically as previous Cup rounds conclude.
    /// </summary>
    public static void InitializeSeasonFixtures(GameWorld world)
    {
        DisciplineEngine.EnsureMigrated(world);
        world.Fixtures.RemoveAll(f => f.SeasonNumber == world.SeasonNumber);
        world.LeagueFirstSchedule = world.TotalClubs is 64 or 128 or 256 or 512 or 1024;

        var schedule = BuildSeasonRoundSchedule(world);
        world.TotalRoundsInSeason = schedule.Count;
        world.RoundNumber = 1;
        world.RoundState = RoundState.PREPARING;
        world.CurrentMatchMinute = 0;

        // Reset current season stats on clubs and players
        foreach (var club in world.Clubs)
        {
            club.LeaguePlayed = 0;
            club.LeagueWins = 0;
            club.LeagueDraws = 0;
            club.LeagueLosses = 0;
            club.LeagueGoalsFor = 0;
            club.LeagueGoalsAgainst = 0;
            club.ConsecutiveWinlessRounds = 0;
        }

        foreach (var player in world.Players)
        {
            player.SeasonGoals = 0;
            player.CupGoals = 0;
            player.SeasonMatches = 0;
            player.SeasonYellows = 0;
            player.SeasonReds = 0;
        }

        // Map which global round numbers correspond to the 14 League rounds
        var leagueRoundNumbers = new List<int>();
        for (int r = 1; r <= schedule.Count; r++)
        {
            if (schedule[r - 1] == CompetitionType.League)
            {
                leagueRoundNumbers.Add(r);
            }
        }

        int matchIndexCounter = 1;

        // Berger circle algorithm for 8 teams per division -> 7 rounds first half + 7 rounds second half (swapped home/away)
        foreach (var tier in new[] { DivisionTier.PrimeiraDivisao, DivisionTier.SegundaDivisao, DivisionTier.TerceiraDivisao, DivisionTier.QuartaDivisao })
        {
            var divClubs = world.Clubs
                .Where(c => c.DivisionTier == tier)
                .OrderBy(c => c.DeterministicIndex)
                .ToList();

            if (divClubs.Count != 8) continue;

            var pairsPerLeagueRound = GenerateRoundRobin8(divClubs);
            for (int lIdx = 0; lIdx < Math.Min(14, leagueRoundNumbers.Count); lIdx++)
            {
                int globalRound = leagueRoundNumbers[lIdx];
                foreach (var (home, away) in pairsPerLeagueRound[lIdx])
                {
                    world.Fixtures.Add(new MatchFixture
                    {
                        GameWorldId = world.Id,
                        SeasonNumber = world.SeasonNumber,
                        RoundNumber = globalRound,
                        DeterministicMatchIndex = matchIndexCounter++,
                        CompetitionType = CompetitionType.League,
                        DivisionTier = tier,
                        HomeClubId = home.Id,
                        AwayClubId = away.Id,
                        RequiresKnockoutResolution = false
                    });
                }
            }
        }

        EnsureCupFixturesForRound(world,1);
        ClassicForceEvolutionEngine.InitializeSeason(world);
        ClassicForceEvolutionEngine.PrepareRound(world);
        HistoryEngine.Observe(world, beginningSeason:true);
    }

    private static List<List<(Club Home, Club Away)>> GenerateRoundRobin8(List<Club> teams)
    {
        var allRounds = new List<List<(Club Home, Club Away)>>(14);
        int n = 8;
        var rotating = teams.Skip(1).ToList(); // 7 items

        for (int r = 0; r < 7; r++)
        {
            var roundPairs = new List<(Club Home, Club Away)>(4);
            var fixedTeam = teams[0];
            var opp0 = rotating[r % 7];
            if (r % 2 == 0) roundPairs.Add((fixedTeam, opp0));
            else roundPairs.Add((opp0, fixedTeam));

            for (int i = 1; i < n / 2; i++)
            {
                var t1 = rotating[(r + i) % 7];
                var t2 = rotating[(r + 7 - i) % 7];
                if ((r + i) % 2 == 0) roundPairs.Add((t1, t2));
                else roundPairs.Add((t2, t1));
            }
            allRounds.Add(roundPairs);
        }

        // Return leg (rounds 8..14): invert Home & Away
        for (int r = 0; r < 7; r++)
        {
            allRounds.Add(allRounds[r].Select(p => (Home: p.Away, Away: p.Home)).ToList());
        }

        return allRounds;
    }

    /// <summary>
    /// Ensures deterministic knockout fixtures exist for the current round if it is a Cup round.
    /// Guarantee: unique slots, zero duplicate clubs in the same round, deterministic draw by seed.
    /// </summary>
    public static void EnsureCupFixturesForRound(GameWorld world, int roundNumber)
    {
        var compType = GetCompetitionTypeForRound(world, roundNumber);
        if (compType == CompetitionType.League) return;

        // Already generated?
        if (world.Fixtures.Any(f => f.SeasonNumber == world.SeasonNumber && f.RoundNumber == roundNumber))
        {
            return;
        }

        var rng = DeterministicRng.FromContext(world.Seed, world.SeasonNumber, roundNumber, 0, 0, 777);
        var districtClubs = world.Clubs
            .Where(c => c.DivisionTier == DivisionTier.Distrital)
            .OrderBy(c => c.DeterministicIndex)
            .ToList();
        var divisionClubs = world.Clubs
            .Where(c => c.DivisionTier != DivisionTier.Distrital)
            .OrderBy(c => c.DeterministicIndex)
            .ToList();

        List<Club> participants = new();

        if (world.LeagueFirstSchedule)
        {
            var cupStages = BuildSeasonRoundSchedule(world).Where(c => c != CompetitionType.League).ToList();
            int stage = cupStages.IndexOf(compType);
            if (stage == 0)
            {
                participants = world.Clubs.OrderBy(c => c.DeterministicIndex).ToList();
                rng.Shuffle(participants);
            }
            else
            {
                var previous = world.Fixtures.Where(f => f.SeasonNumber == world.SeasonNumber
                    && f.CompetitionType == cupStages[stage - 1]).ToList();
                if (previous.Count == 0 || previous.Any(f => !f.IsFinished || !f.WinnerClubId.HasValue))
                    throw new InvalidOperationException("Conclua a eliminatória anterior antes de gerar a próxima fase da Taça.");
                participants = GetRoundWinners(world, cupStages[stage - 1]);
            }
            if (participants.Count % 2 != 0)
                throw new InvalidOperationException("A eliminatória da Taça precisa de um número par de clubes.");
        }
        else if (compType == CompetitionType.CupPreliminary1)
        {
            // For 96 district clubs (or general D > 64):
            // We want 64 clubs remaining after Preliminary 1 so Preliminary 2 is 64 -> 32.
            // If D > 96 (e.g. 256/512/1024 benchmark worlds), reduce district pool to 64 play + 32 byes deterministically
            var shuffledDistricts = new List<Club>(districtClubs);
            rng.Shuffle(shuffledDistricts);
            // First 64 play in Preliminary 1 (32 matches -> 32 winners); next 32 get bye to Preliminary 2!
            participants = shuffledDistricts.Take(Math.Min(64, shuffledDistricts.Count)).ToList();
        }
        else if (compType == CompetitionType.CupPreliminary2)
        {
            var p1Winners = GetRoundWinners(world, CompetitionType.CupPreliminary1);
            var p1PlayedIds = world.Fixtures
                .Where(f => f.SeasonNumber == world.SeasonNumber && f.CompetitionType == CompetitionType.CupPreliminary1)
                .SelectMany(f => new[] { f.HomeClubId, f.AwayClubId })
                .ToHashSet();

            // Byes: 32 district clubs that did not play in Preliminary 1
            var rngP1 = DeterministicRng.FromContext(world.Seed, world.SeasonNumber, 1, 0, 0, 777);
            var shuffledDistricts = new List<Club>(districtClubs);
            rngP1.Shuffle(shuffledDistricts);
            var byes = shuffledDistricts.Where(c => !p1PlayedIds.Contains(c.Id)).Take(64 - p1Winners.Count).ToList();

            participants = p1Winners.Concat(byes).DistinctBy(c => c.Id).Take(64).ToList();
            rng.Shuffle(participants);
        }
        else if (compType == CompetitionType.CupRoundOf64)
        {
            var qualifiedDistricts = GetRoundWinners(world, CompetitionType.CupPreliminary2);
            if (qualifiedDistricts.Count < 32)
            {
                var usedIds = qualifiedDistricts.Select(c => c.Id).ToHashSet();
                qualifiedDistricts.AddRange(districtClubs.Where(c => !usedIds.Contains(c.Id)).Take(32 - qualifiedDistricts.Count));
            }
            participants = divisionClubs.Concat(qualifiedDistricts.Take(32)).DistinctBy(c => c.Id).ToList();
            rng.Shuffle(participants);
        }
        else if (compType == CompetitionType.CupRoundOf32)
        {
            participants = GetRoundWinners(world, CompetitionType.CupRoundOf64);
        }
        else if (compType == CompetitionType.CupRoundOf16)
        {
            participants = GetRoundWinners(world, CompetitionType.CupRoundOf32);
        }
        else if (compType == CompetitionType.CupQuarterFinal)
        {
            participants = GetRoundWinners(world, CompetitionType.CupRoundOf16);
        }
        else if (compType == CompetitionType.CupSemiFinal)
        {
            participants = GetRoundWinners(world, CompetitionType.CupQuarterFinal);
        }
        else if (compType == CompetitionType.CupFinal)
        {
            participants = GetRoundWinners(world, CompetitionType.CupSemiFinal);
        }

        int matchIdx = 1000 + roundNumber * 100;
        for (int i = 0; i + 1 < participants.Count; i += 2)
        {
            var home = participants[i];
            var away = participants[i + 1];
            world.Fixtures.Add(new MatchFixture
            {
                GameWorldId = world.Id,
                SeasonNumber = world.SeasonNumber,
                RoundNumber = roundNumber,
                DeterministicMatchIndex = matchIdx++,
                CompetitionType = compType,
                DivisionTier = null,
                HomeClubId = home.Id,
                AwayClubId = away.Id,
                RequiresKnockoutResolution = true
            });
        }
    }

    private static List<Club> GetRoundWinners(GameWorld world, CompetitionType prevComp)
    {
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var fixtures = world.Fixtures
            .Where(f => f.SeasonNumber == world.SeasonNumber && f.CompetitionType == prevComp)
            .OrderBy(f => f.DeterministicMatchIndex)
            .ToList();

        var winners = new List<Club>();
        foreach (var f in fixtures)
        {
            Guid winId = f.WinnerClubId ?? (f.HomeGoals >= f.AwayGoals ? f.HomeClubId : f.AwayClubId);
            if (clubDict.TryGetValue(winId, out var c))
            {
                winners.Add(c);
            }
        }
        return winners.DistinctBy(c => c.Id).ToList();
    }

    /// <summary>
    /// Updates League table and008 career points/morale using the coach captured at kickoff,
    /// and mid-season dismissal checks when a round finishes.
    /// </summary>
    public static void ApplyFinishedRoundResultsToStandingsAndCareers(
        GameWorld world,
        IReadOnlyList<MatchFixture> roundMatches)
    {
        var official = world.Fixtures.Where(m => m.SeasonNumber == world.SeasonNumber && m.RoundNumber == world.RoundNumber).ToList();
        if (ClassicForceEvolutionEngine.IsActive(world))
        {
            if (official.Count == 0 || official.Any(m => !m.IsFinished)) return;
            if (official.Any(m => m.CurrentMinute > 0 && (!m.StatisticsLineupCaptured || m.ForceRoster.Count == 0)))
                throw new InvalidOperationException("Classic force requires captured kickoff lineups and rosters.");
            roundMatches = official;
        }
        CoachCareerEngine.EnsureMigrated(world);
        DisciplineEngine.SettleFinishedMatches(world,roundMatches);
        PlayerStatisticsEngine.ApplyFinishedMatches(world, roundMatches);
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        bool updated = false;
        var pending = roundMatches.Where(m => m.IsFinished && m.CurrentMinute > 0 && !m.CareerResultsApplied).ToList();
        foreach (var match in pending) CoachCareerEngine.CaptureLeadership(world, match);
        foreach (var match in pending)
        {
            if (!clubDict.TryGetValue(match.HomeClubId, out var home) ||
                !clubDict.TryGetValue(match.AwayClubId, out var away))
            {
                continue;
            }

            if (match.WinnerClubId == null)
            {
                if (match.HomeGoals > match.AwayGoals) match.WinnerClubId = home.Id;
                else if (match.AwayGoals > match.HomeGoals) match.WinnerClubId = away.Id;
            }

            // League standings update
            if (match.CompetitionType == CompetitionType.League)
            {
                home.LeaguePlayed++;
                away.LeaguePlayed++;
                home.LeagueGoalsFor += match.HomeGoals;
                home.LeagueGoalsAgainst += match.AwayGoals;
                away.LeagueGoalsFor += match.AwayGoals;
                away.LeagueGoalsAgainst += match.HomeGoals;

                if (match.HomeGoals > match.AwayGoals)
                {
                    home.LeagueWins++;
                    away.LeagueLosses++;
                    home.ConsecutiveWinlessRounds = 0;
                    away.ConsecutiveWinlessRounds++;
                }
                else if (match.AwayGoals > match.HomeGoals)
                {
                    away.LeagueWins++;
                    home.LeagueLosses++;
                    away.ConsecutiveWinlessRounds = 0;
                    home.ConsecutiveWinlessRounds++;
                }
                else
                {
                    home.LeagueDraws++;
                    away.LeagueDraws++;
                    home.ConsecutiveWinlessRounds++;
                    away.ConsecutiveWinlessRounds++;
                }
            }

        }

        // Morale must see the completed league table, independent of fixture ordering within this round.
        foreach (var match in pending)
        {
            if (!clubDict.TryGetValue(match.HomeClubId, out var home) || !clubDict.TryGetValue(match.AwayClubId, out var away)) continue;
            CoachCareerEngine.ApplyMatchCareer(world, match, home, away);
            match.CareerResultsApplied = true;
            updated = true;
        }

        // Already settled results from an older save are read as history and do not trigger the new rule.
        if (updated && official.Count > 0 && official.All(m => m.IsFinished && (m.CurrentMinute == 0 || m.CareerResultsApplied)))
            ClassicForceEvolutionEngine.ApplyFinishedRound(world, official);
        ClassicEconomyEngine.AfterSportingRound(world,roundMatches);
        HistoryEngine.Observe(world);
        if (!updated) return;
        CoachCareerEngine.AwardCompletedLeagues(world);
        CoachCareerEngine.RecoverUnemployed(world);
        GenerateJobInvitationsForUnemployedHumans(world);
    }

    public static List<DivisionStandingRowDto> GetDivisionStandings(GameWorld world, DivisionTier tier)
    {
        var humanDict = world.HumanCoaches.ToDictionary(h => h.Id);
        var aiDict = world.AiManagers.ToDictionary(m => m.Id);

        var sorted = world.Clubs
            .Where(c => c.DivisionTier == tier)
            .OrderByDescending(c => c.LeaguePoints)
            .ThenByDescending(c => c.LeagueWins)
            .ThenByDescending(c => c.LeagueGoalDifference)
            .ThenByDescending(c => c.LeagueGoalsFor)
            .ThenBy(c => c.DeterministicIndex)
            .ToList();

        var result = new List<DivisionStandingRowDto>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            var c = sorted[i];
            int pos = i + 1;
            string coachName = c.HumanCoachId != null && humanDict.TryGetValue(c.HumanCoachId.Value, out var h)
                ? $"{h.Nickname} (Humano)"
                : (c.AiManagerId != null && aiDict.TryGetValue(c.AiManagerId.Value, out var ai) ? ai.Name : "Técnico IA");

            string zone = "Permanência";
            if (tier == DivisionTier.PrimeiraDivisao && pos == 1) zone = "Campeão";
            else if (tier != DivisionTier.PrimeiraDivisao && pos <= 2) zone = "Promoção";
            else if (pos >= sorted.Count - 1) zone = "Rebaixamento";

            result.Add(new DivisionStandingRowDto(
                Position: pos,
                ClubId: c.Id,
                ClubName: c.Name,
                PrimaryColorHex: c.PrimaryColorHex,
                SecondaryColorHex: c.SecondaryColorHex,
                IsHumanControlled: c.HumanCoachId != null,
                CoachName: coachName,
                Played: c.LeaguePlayed,
                Wins: c.LeagueWins,
                Draws: c.LeagueDraws,
                Losses: c.LeagueLosses,
                GoalsFor: c.LeagueGoalsFor,
                GoalsAgainst: c.LeagueGoalsAgainst,
                GoalDifference: c.LeagueGoalDifference,
                Points: c.LeaguePoints,
                ZoneStatus: zone));
        }

        return result;
    }

    /// <summary>
    /// Concludes the current season:
    /// 1. Crowns 1st Division Champion and Cup Champion, awards titles and prize money.
    /// 2. Promotes top 2 and relegates bottom 2 across Divisions 1↔2↔3↔4↔Distrital.
    /// 3. Enforces the strict rule: if a Human Coach's club is relegated from 4th Division to Distrital,
    ///    the human coach is dismissed (becomes Unemployed), retains career ranking, and receives invitations.
    /// 4. Applies seasonal stadium wear (1%..6% without maintenance, 0%..1% with maintenance) and AI stadium projects.
    /// 5. Advances SeasonNumber and initializes the next season's fixtures.
    /// </summary>
    public static SeasonHistoryEntry FinalizeSeasonAndAdvance(GameWorld world,int? expectedSeasonNumber=null)
    {
        if(expectedSeasonNumber is int expected&&expected!=world.SeasonNumber)
            return world.SeasonHistories.FirstOrDefault(h=>h.SeasonNumber==expected)
                ?? throw new InvalidOperationException("SEASON_VERSION: Temporada não corresponde à transição solicitada.");
        SalarySeasonTransitionEngine.RequireResolved(world);
        HistoryEngine.Observe(world);
        OperatingEconomyEngine.EnsureScheduledRules(world);
        CoachCareerEngine.EnsureMigrated(world);
        var scorerAwards = PlayerStatisticsEngine.AwardSeason(world, world.SeasonNumber);
        var div1 = GetDivisionStandings(world, DivisionTier.PrimeiraDivisao);
        var div2 = GetDivisionStandings(world, DivisionTier.SegundaDivisao);
        var div3 = GetDivisionStandings(world, DivisionTier.TerceiraDivisao);
        var div4 = GetDivisionStandings(world, DivisionTier.QuartaDivisao);

        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var humanDict = world.HumanCoaches.ToDictionary(h => h.Id);
        var aiDict = world.AiManagers.ToDictionary(m => m.Id);

        var champRow = div1.First();
        var champClub = clubDict[champRow.ClubId];

        CoachCareerEngine.AwardCompletedLeagues(world);

        // Determine Cup Champion
        var cupFinal = world.Fixtures
            .FirstOrDefault(f => f.SeasonNumber == world.SeasonNumber && f.CompetitionType == CompetitionType.CupFinal);
        Guid cupChampId = cupFinal?.WinnerClubId ?? champClub.Id;
        var cupChampClub = clubDict.GetValueOrDefault(cupChampId, champClub);

        // Cup titles are awarded once by the played final, never by a fallback club.

        // Award prize money idempotently
        MarketAndEconomyEngine.TryRecordTransaction(world, champClub, FinancialTransactionCategory.PrizeMoney,
            1_200_000m, $"Prêmio de Campeão da 1ª Divisão (Temporada {world.SeasonNumber})", $"prize:div1:{world.SeasonNumber}");
        MarketAndEconomyEngine.TryRecordTransaction(world, cupChampClub, FinancialTransactionCategory.PrizeMoney,
            900_000m, $"Prêmio de Campeão da Taça Nacional (Temporada {world.SeasonNumber})", $"prize:cup:{world.SeasonNumber}");

        if (cupFinal != null)
        {
            Guid runnerUpId = cupFinal.HomeClubId == cupChampClub.Id ? cupFinal.AwayClubId : cupFinal.HomeClubId;
            if (clubDict.TryGetValue(runnerUpId, out var cupRunnerUp))
            {
                MarketAndEconomyEngine.TryRecordTransaction(world, cupRunnerUp, FinancialTransactionCategory.PrizeMoney,
                    450_000m, $"Prêmio de Vice-Campeão da Taça Nacional (Temporada {world.SeasonNumber})", $"prize:cup_runnerup:{world.SeasonNumber}");
            }
        }

        AwardDivisionPlacementQuotas(world, div1, clubDict, 700_000m, 360_000m, 190_000m, skipFirst: true);
        AwardDivisionPlacementQuotas(world, div2, clubDict, 650_000m, 290_000m, 150_000m, skipFirst: false);
        AwardDivisionPlacementQuotas(world, div3, clubDict, 460_000m, 220_000m, 120_000m, skipFirst: false);
        AwardDivisionPlacementQuotas(world, div4, clubDict, 360_000m, 180_000m, 100_000m, skipFirst: false);

        // Top scorer
        var topScorer = PlayerStatisticsEngine.GetRankings(world, "scorers", "general", "current").Rows.FirstOrDefault();

        // Promotion & Relegation lists
        var promotedIds = new List<Guid>();
        var relegatedIds = new List<Guid>();

        var rel1 = div1.TakeLast(2).Select(r => clubDict[r.ClubId]).ToList();
        var pro2 = div2.Take(2).Select(r => clubDict[r.ClubId]).ToList();
        var rel2 = div2.TakeLast(2).Select(r => clubDict[r.ClubId]).ToList();
        var pro3 = div3.Take(2).Select(r => clubDict[r.ClubId]).ToList();
        var rel3 = div3.TakeLast(2).Select(r => clubDict[r.ClubId]).ToList();
        var pro4 = div4.Take(2).Select(r => clubDict[r.ClubId]).ToList();
        var rel4 = div4.TakeLast(2).Select(r => clubDict[r.ClubId]).ToList();

        // Top 2 District clubs to promote to 4th Division (based on Cup wins this season, then reputation)
        var cupWinsByClub = world.Fixtures
            .Where(f => f.SeasonNumber == world.SeasonNumber && f.CompetitionType != CompetitionType.League && f.WinnerClubId != null)
            .GroupBy(f => f.WinnerClubId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var proDist = world.Clubs
            .Where(c => c.DivisionTier == DivisionTier.Distrital)
            .OrderByDescending(c => cupWinsByClub.GetValueOrDefault(c.Id, 0))
            .ThenByDescending(c => c.Reputation)
            .ThenBy(c => (c.DeterministicIndex + world.SeasonNumber * 7) % 96)
            .Take(2)
            .ToList();

        foreach (var c in rel1) { c.DivisionTier = DivisionTier.SegundaDivisao; c.Reputation = Math.Max(35, c.Reputation - 3); relegatedIds.Add(c.Id); }
        foreach (var c in pro2) { c.DivisionTier = DivisionTier.PrimeiraDivisao; c.Reputation = Math.Min(95, c.Reputation + 4); promotedIds.Add(c.Id); }
        foreach (var c in rel2) { c.DivisionTier = DivisionTier.TerceiraDivisao; c.Reputation = Math.Max(28, c.Reputation - 3); relegatedIds.Add(c.Id); }
        foreach (var c in pro3) { c.DivisionTier = DivisionTier.SegundaDivisao; c.Reputation = Math.Min(88, c.Reputation + 4); promotedIds.Add(c.Id); }
        foreach (var c in rel3) { c.DivisionTier = DivisionTier.QuartaDivisao; c.Reputation = Math.Max(22, c.Reputation - 3); relegatedIds.Add(c.Id); }
        foreach (var c in pro4) { c.DivisionTier = DivisionTier.TerceiraDivisao; c.Reputation = Math.Min(78, c.Reputation + 4); promotedIds.Add(c.Id); }

        // Relegation from 4th Division to Distrital: Humans NEVER control a District club!
        foreach (var c in rel4)
        {
            c.DivisionTier = DivisionTier.Distrital;
            c.Reputation = Math.Max(18, c.Reputation - 4);
            relegatedIds.Add(c.Id);

            if (c.HumanCoachId != null && humanDict.TryGetValue(c.HumanCoachId.Value, out var relegatedHuman))
            {
                DismissHumanCoach(world, relegatedHuman, c, "Demitido por rebaixamento da 4ª Divisão para Distrital");
            }
        }

        foreach (var c in proDist)
        {
            c.DivisionTier = DivisionTier.QuartaDivisao;
            c.Reputation = Math.Max(c.Reputation + 6, 38);
            promotedIds.Add(c.Id);
            MarketAndEconomyEngine.TryRecordTransaction(world, c, FinancialTransactionCategory.PrizeMoney,
                380_000m, $"Bônus de acesso do Distrital à 4ª Divisão (Temporada {world.SeasonNumber})",
                $"prize:pro_dist:{c.Id}:S{world.SeasonNumber}");
        }

        ClassicEconomyEngine.RegisterPromotions(world,promotedIds);
        // The table position from another division is no longer a valid growth reference,
        // including injury history produced during the transition days before season initialization.
        world.ClassicForce.CompletedLeaguePositions.Clear();

        // AI Manager evaluation & rotation for relegated/underperforming AI clubs
        foreach (var relegated in rel1.Concat(rel2).Concat(rel3).Concat(rel4))
            if (relegated.AiManagerId is Guid managerId && !relegated.HasProvisionalManager)
                CoachCareerEngine.Dismiss(world, managerId, relegated, "AI_RELEGATION");

        // Increment seasons managed
        foreach (var h in world.HumanCoaches) h.SeasonsManaged++;
        foreach (var ai in world.AiManagers.Where(m => m.CurrentClubId != null)) ai.SeasonsManaged++;

        // Process seasonal stadium structural wear & AI stadium projects
        MarketAndEconomyEngine.ProcessSeasonalStadiumWearAndAiProjects(world);
        ClassicEconomyEngine.EvaluateInsolvency(world);
        foreach(var club in world.Clubs)ClassicEconomyEngine.AssessCredit(world,club);

        var historyEntry = new SeasonHistoryEntry
        {
            GameWorldId = world.Id,
            SeasonNumber = world.SeasonNumber,
            FirstDivisionChampionClubId = champClub.Id,
            FirstDivisionChampionName = champClub.Name,
            CupChampionClubId = cupChampClub.Id,
            CupChampionName = cupChampClub.Name,
            TopScorerPlayerId = topScorer?.PlayerId ?? Guid.Empty,
            TopScorerPlayerName = scorerAwards.Count > 0 ? string.Join(", ", scorerAwards.Select(a => a.DisplayName)) : topScorer?.DisplayName ?? "Sem gols oficiais recuperáveis",
            TopScorerGoals = topScorer?.GoalsScored ?? 0,
            PromotedClubIds = promotedIds,
            RelegatedClubIds = relegatedIds
        };
        world.SeasonHistories.Add(historyEntry);

        // Prune transient records older than previous season so long multi-season runs stay memory-bounded
        int pruneBeforeSeason = world.SeasonNumber - 1;
        if (pruneBeforeSeason >= 1)
        {
            world.Fixtures.RemoveAll(f => f.SeasonNumber < pruneBeforeSeason);
            world.Transactions.RemoveAll(t => t.SeasonNumber < pruneBeforeSeason);
            world.Auctions.RemoveAll(a => a.IsClosed && a.CreatedAtSeason < pruneBeforeSeason);
            world.TransferOffers.RemoveAll(o => o.Status != TransferOfferStatus.Pending && o.CreatedAtSeason < pruneBeforeSeason);
            world.PauseWindows.RemoveAll(p => p.IsClosed && p.SeasonNumber < pruneBeforeSeason);
            world.AuditLogs.RemoveAll(a => a.SeasonNumber < pruneBeforeSeason
                && (a.ActionType.StartsWith("AiPurchase", StringComparison.Ordinal) || a.ActionType == "AiAuctionBid"));
        }

        // Advance to next season
        if (ClassicForceEvolutionEngine.IsActive(world))
            ClassicForceEvolutionEngine.AdvanceToDate(world, world.ClassicForce.GameDate.AddDays(21));
        HistoryEngine.CloseSeason(world,historyEntry,new[]{div1,div2,div3,div4});
        world.SeasonNumber++;
        if(world.Economy.Rules.SeasonContractsEnabled)
            foreach(var player in world.Players.Where(player=>player.ClubId.HasValue))
            {
                player.ContractRoundsRemaining=0;
                player.RenewedForSeason=null;
                player.NegotiatedContractProtected=false;
            }
        OperatingEconomyEngine.EnsureScheduledRules(world);
        ClassicPlayerEconomyEngine.AdvanceWorldInflationAtSeasonEnd(world, world.SeasonNumber - 1);
        InitializeSeasonFixtures(world);
        GenerateJobInvitationsForUnemployedHumans(world);
        if(world.Economy.Rules.SeasonContractsEnabled)ClassicEconomyEngine.EvaluateInsolvency(world);

        return historyEntry;
    }

    private static void AwardDivisionPlacementQuotas(
        GameWorld world,
        List<DivisionStandingRowDto> standings,
        Dictionary<Guid, Club> clubDict,
        decimal topTwoPrize,
        decimal midTablePrize,
        decimal bottomTwoPrize,
        bool skipFirst)
    {
        foreach (var row in standings)
        {
            if (skipFirst && row.Position == 1) continue;
            if (!clubDict.TryGetValue(row.ClubId, out var club)) continue;

            decimal prize = row.Position <= 2
                ? topTwoPrize
                : (row.Position <= 6 ? midTablePrize : bottomTwoPrize);

            MarketAndEconomyEngine.TryRecordTransaction(world, club, FinancialTransactionCategory.PrizeMoney,
                prize, $"Cota de classificação ({row.Position}º lugar na {club.DivisionTier} - T{world.SeasonNumber})",
                $"prize:pos:{club.Id}:S{world.SeasonNumber}");
        }
    }

    public static void DismissHumanCoach(GameWorld world, HumanCoach coach, Club club, string reason)
        => CoachCareerEngine.Dismiss(world, coach.Id, club, reason);

    public static void GenerateJobInvitationsForUnemployedHumans(GameWorld world)
        => CoachCareerEngine.ProcessVacancies(world);

    public static List<CoachRankingDto> GetClassicCoachRanking(GameWorld world)
    {
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var entries = new List<CoachRankingDto>();

        foreach (var h in world.HumanCoaches)
        {
            Club? club = h.CurrentClubId != null ? clubDict.GetValueOrDefault(h.CurrentClubId.Value) : null;
            entries.Add(new CoachRankingDto(
                Rank: 0,
                CoachId: h.Id,
                Name: h.Nickname,
                IsHuman: true,
                CurrentClubId: club?.Id,
                CurrentClubName: club?.Name ?? "Desempregado",
                CurrentDivision: club?.DivisionTier,
                LeagueTitles: h.LeagueTitles,
                CupTitles: h.CupTitles,
                CoachPoints: h.CoachPoints,
                TotalWins: h.TotalWins,
                TotalDraws: h.TotalDraws,
                TotalLosses: h.TotalLosses,
                SeasonsManaged: h.SeasonsManaged,
                Dismissals: h.Dismissals));
        }

        foreach (var ai in world.AiManagers)
        {
            Club? club = ai.CurrentClubId != null ? clubDict.GetValueOrDefault(ai.CurrentClubId.Value) : null;
            if (club?.DivisionTier == DivisionTier.Distrital && ai.CoachPoints == 0 && ai.CupTitles == 0 && ai.LeagueTitles == 0)
            {
                continue;
            }
            entries.Add(new CoachRankingDto(
                Rank: 0,
                CoachId: ai.Id,
                Name: ai.Name,
                IsHuman: false,
                CurrentClubId: club?.Id,
                CurrentClubName: club?.Name ?? "Sem Clube",
                CurrentDivision: club?.DivisionTier,
                LeagueTitles: ai.LeagueTitles,
                CupTitles: ai.CupTitles,
                CoachPoints: ai.CoachPoints,
                TotalWins: ai.TotalWins,
                TotalDraws: ai.TotalDraws,
                TotalLosses: ai.TotalLosses,
                SeasonsManaged: ai.SeasonsManaged,
                Dismissals: ai.Dismissals));
        }

        // Classic ordering: 1) LeagueTitles, 2) CupTitles, 3) CoachPoints, 4) TotalWins
        var ordered = entries
            .OrderByDescending(e => e.LeagueTitles)
            .ThenByDescending(e => e.CupTitles)
            .ThenByDescending(e => e.CoachPoints)
            .ThenBy(e => e.CoachId)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i] = ordered[i] with { Rank = i + 1 };
        }

        return ordered;
    }
}
