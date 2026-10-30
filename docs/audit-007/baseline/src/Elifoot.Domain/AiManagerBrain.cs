using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class AiManagerBrain
{
    /// <summary>
    /// Evaluates the AI Manager's squad, preferred formations, opponent strength, home/away context,
    /// injuries/suspensions, and club budget to produce a weekly ManagerSquadPlan and select the best
    /// formation, 11 starters, and up to 9 bench players.
    /// </summary>
    public static (string Formation, List<Guid> Starters, List<Guid> Bench, ManagerSquadPlan Plan) PlanAndSelectLineup(
        AiManager manager,
        Club club,
        IReadOnlyList<Player> clubSquad,
        Club? opponentClub,
        IReadOnlyList<Player>? opponentSquad,
        bool isHomeMatch,
        int worldSeed,
        int seasonNumber,
        int roundNumber,
        bool allowClassic505 = true)
    {
        var rng = DeterministicRng.FromContext(worldSeed, seasonNumber, roundNumber, club.DeterministicIndex, 0, 17);

        // Normalize preferred formations
        var prefs = manager.PreferredFormations.Count > 0
            ? manager.PreferredFormations
            : GetDefaultPreferencesForIdentity(manager.Identity);

        var primaryPref = prefs.OrderByDescending(p => p.WeightPercent).First();
        if (!TacticalFormations.TryGet(primaryPref.Formation, allowClassic505, out var primarySpec))
        {
            primarySpec = TacticalFormations.All["4-4-2"];
        }

        // Target starters and reserves driven by the manager's #1 preferred formation
        var targetStarters = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.G] = 1,
            [PlayerPosition.D] = primarySpec.Defenders,
            [PlayerPosition.M] = primarySpec.Midfielders,
            [PlayerPosition.A] = primarySpec.Attackers
        };

        var targetReserves = new Dictionary<PlayerPosition, int>
        {
            [PlayerPosition.G] = 1,
            [PlayerPosition.D] = Math.Max(2, (int)Math.Ceiling(primarySpec.Defenders * 0.5)),
            [PlayerPosition.M] = Math.Max(2, (int)Math.Ceiling(primarySpec.Midfielders * 0.5)),
            [PlayerPosition.A] = Math.Max(1, (int)Math.Ceiling(primarySpec.Attackers * 0.5))
        };

        var available = clubSquad.Where(p => p.IsAvailableForMatch).ToList();
        var availByPos = available
            .GroupBy(p => p.Position)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => GetSelectionScore(p)).ToList());

        int AvailCount(PlayerPosition pos) => availByPos.TryGetValue(pos, out var list) ? list.Count : 0;

        double myAvgStrength = available.Count > 0 ? available.Average(p => p.Strength) : 20.0;
        double oppAvgStrength = (opponentSquad != null && opponentSquad.Count > 0)
            ? opponentSquad.Where(p => p.IsAvailableForMatch).DefaultIfEmpty().Average(p => p?.Strength ?? 20)
            : myAvgStrength;

        // Candidate formations: manager's preferred formations + fallback formations (4-4-2, 4-5-1, 5-3-2)
        var candidateCodes = prefs.Select(p => p.Formation)
            .Concat(new[] { "4-4-2", "4-5-1", "5-3-2", "4-3-3", "3-5-2" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => TacticalFormations.TryGet(c, allowClassic505, out _))
            .ToList();

        string bestFormation = primarySpec.Code;
        double bestTotalScore = double.MinValue;
        string bestReason = string.Empty;

        decimal roundSalaryBill = clubSquad.Sum(p => p.SalaryPerRound);
        decimal emergencyReserve = Math.Max(180_000m, roundSalaryBill * 5m + club.Stadium.MaintenanceReservePerSeason * 0.35m);
        bool tightBudget = club.CashBalance <= emergencyReserve * 1.15m;

        foreach (var code in candidateCodes)
        {
            var spec = TacticalFormations.All[code];
            var prefEntry = prefs.FirstOrDefault(p => string.Equals(p.Formation, code, StringComparison.OrdinalIgnoreCase));
            double preferencePrior = (prefEntry?.WeightPercent ?? 5) * 0.45; // 0..45 pts

            // FitToSquad: do we have enough available players in D, M, A?
            int defDiff = AvailCount(PlayerPosition.D) - spec.Defenders;
            int midDiff = AvailCount(PlayerPosition.M) - spec.Midfielders;
            int attDiff = AvailCount(PlayerPosition.A) - spec.Attackers;

            double fitToSquad = 0;
            if (defDiff >= 0) fitToSquad += 10 + Math.Min(6, defDiff * 2);
            else fitToSquad += defDiff * 24; // Heavy penalty for missing starters in sector

            if (midDiff >= 0) fitToSquad += 10 + Math.Min(6, midDiff * 2);
            else fitToSquad += midDiff * 22;

            if (attDiff >= 0) fitToSquad += 12 + Math.Min(8, attDiff * 3);
            else fitToSquad += attDiff * 28; // If prefers 4-3-3 (3 A) but only has 2 A, strong negative fit

            // If tight budget and zero reserve in a demanding sector (e.g., 4-3-3 with 0 reserve attackers and tight cash), pragmatic coaches adapt to 4-4-2
            double financialConstraintMod = 0;
            if (tightBudget && attDiff == 0 && spec.Attackers >= 3 && manager.Pragmatism >= 45)
            {
                financialConstraintMod -= 14.0;
            }

            // OpponentMatchup & HomeAway
            double matchupDiff = myAvgStrength - oppAvgStrength + (isHomeMatch ? 2.0 : -2.0);
            double opponentMatchup = 0;
            if (matchupDiff < -3.0)
            {
                // Stronger opponent / away game: defensive/balanced formations gain points
                opponentMatchup += (spec.Defenders >= 4 ? 8.0 : -5.0) + (spec.Attackers <= 2 ? 6.0 : -4.0);
            }
            else if (matchupDiff > 3.0)
            {
                // Weaker opponent / home game: offensive formations gain points
                opponentMatchup += (spec.Attackers >= 3 ? 8.0 : 0.0) + (spec.Defenders <= 4 ? 4.0 : -3.0);
            }

            double homeAwayMod = isHomeMatch
                ? (spec.Attackers >= 3 ? 3.5 : 1.0)
                : (spec.Defenders >= 4 ? 3.5 : 0.0);

            // CurrentForm: winless streak prompts adaptation
            double currentFormMod = 0;
            if (club.ConsecutiveWinlessRounds >= 3 && !string.Equals(code, club.PreferredFormation, StringComparison.OrdinalIgnoreCase))
            {
                currentFormMod += manager.Adaptability * 0.08;
            }

            // InjuriesSuspensions adjustment
            int unavailableCount = clubSquad.Count(p => !p.IsAvailableForMatch);
            double injuriesMod = unavailableCount > 0 && defDiff >= 0 && midDiff >= 0 && attDiff >= 0 ? 5.0 : 0.0;

            // StrategicVariation (small deterministic variation scaled by 100 - Pragmatism)
            double strategicVariation = rng.NextDouble(-2.5, 2.5) * ((110 - manager.Pragmatism) / 100.0);

            double totalScore = preferencePrior
                                + fitToSquad
                                + opponentMatchup
                                + homeAwayMod
                                + currentFormMod
                                + injuriesMod
                                + financialConstraintMod
                                + strategicVariation;

            if (totalScore > bestTotalScore)
            {
                bestTotalScore = totalScore;
                bestFormation = code;

                var reasons = new List<string>();
                if (!string.Equals(code, primarySpec.Code, StringComparison.OrdinalIgnoreCase))
                {
                    if (AvailCount(PlayerPosition.A) < primarySpec.Attackers)
                        reasons.Add($"faltam atacantes aptos para {primarySpec.Code} ({AvailCount(PlayerPosition.A)}/{primarySpec.Attackers})");
                    else if (tightBudget && AvailCount(PlayerPosition.A) == primarySpec.Attackers)
                        reasons.Add($"faltam reservas atacantes para {primarySpec.Code} e orçamento curto");
                    if (AvailCount(PlayerPosition.D) < primarySpec.Defenders)
                        reasons.Add($"faltam defensores aptos para {primarySpec.Code}");
                }
                else
                {
                    reasons.Add($"esquema favorito ({primaryPref.WeightPercent}%)");
                }

                if (matchupDiff < -3.0) reasons.Add("adversário forte");
                else if (matchupDiff > 3.0) reasons.Add("favoritismo técnico");
                if (!isHomeMatch) reasons.Add("jogo fora de casa");
                if (tightBudget) reasons.Add("orçamento curto");

                bestReason = $"Formação {code} escolhida: {string.Join(", ", reasons)}.";
            }
        }

        // Build 11 starters and up to 9 bench players for bestFormation
        var chosenSpec = TacticalFormations.All[bestFormation];
        var (starters, bench) = BuildStartersAndBench(clubSquad, chosenSpec);

        // Identify squad gaps & surplus for the ManagerSquadPlan
        var priorityGaps = new List<PlayerPosition>();
        var gapDescriptions = new List<string>();

        var allByPos = clubSquad
            .GroupBy(p => p.Position)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => GetSelectionScore(p)).ToList());

        foreach (var pos in new[] { PlayerPosition.G, PlayerPosition.A, PlayerPosition.D, PlayerPosition.M })
        {
            int have = allByPos.TryGetValue(pos, out var list) ? list.Count : 0;
            int wantStarter = targetStarters[pos];
            int wantTotal = wantStarter + targetReserves[pos];

            if (have < wantStarter)
            {
                priorityGaps.Add(pos);
                gapDescriptions.Add($"Urgente: titular na posição {pos} ({have}/{wantStarter} para {primarySpec.Code})");
            }
            else if (have < wantTotal)
            {
                priorityGaps.Add(pos);
                gapDescriptions.Add($"Cobertura: reserva na posição {pos} ({have}/{wantTotal} para {primarySpec.Code})");
            }
            else if (list != null && list.Take(wantStarter).Any(p => p.Strength < myAvgStrength - 4))
            {
                priorityGaps.Add(pos);
                gapDescriptions.Add($"Qualidade: elevar força titular do setor {pos} para {primarySpec.Code}");
            }
        }

        // Surplus players: players beyond wantTotal+1 in a sector, sorted by lowest selection score, keeping squad >= 14 and >= 2 G
        var surplusIds = new List<Guid>();
        foreach (var kvp in allByPos)
        {
            int keepCount = targetStarters[kvp.Key] + targetReserves[kvp.Key];
            if (kvp.Key == PlayerPosition.G) keepCount = Math.Max(2, keepCount);
            if (kvp.Value.Count > keepCount)
            {
                foreach (var extra in kvp.Value.Skip(keepCount))
                {
                    surplusIds.Add(extra.Id);
                }
            }
        }

        decimal disposable = Math.Max(0m, club.CashBalance - emergencyReserve);
        decimal maxSingleSigning = Math.Round(disposable * (0.35m + (manager.FinancialAggressiveness / 200m)), 2);

        var plan = new ManagerSquadPlan
        {
            ManagerId = manager.Id,
            ManagerName = manager.Name,
            Identity = manager.Identity,
            TargetPrimaryFormation = primarySpec.Code,
            SelectedMatchFormation = bestFormation,
            SelectionReasoning = bestReason,
            TargetStarters = targetStarters,
            TargetReserves = targetReserves,
            PriorityGapPositions = priorityGaps,
            PriorityGapDescriptions = gapDescriptions,
            MaxSingleSigningBudget = maxSingleSigning,
            EmergencyCashReserve = emergencyReserve,
            SurplusPlayerIds = surplusIds,
            EvaluatedAtRound = roundNumber
        };

        manager.LastDecisionLog = bestReason;
        club.LatestSquadPlan = plan;
        club.PreferredFormation = bestFormation;
        club.PreferredStarterIds = starters;
        club.PreferredBenchIds = bench;

        return (bestFormation, starters, bench, plan);
    }

    public static (List<Guid> Starters, List<Guid> Bench) BuildStartersAndBench(
        IReadOnlyCollection<Player> clubSquad,
        FormationSpec spec)
    {
        var available = clubSquad.Where(p => p.IsAvailableForMatch).ToList();
        var starters = new List<Player>(11);

        // 1. Pick best Goalkeeper (G): prefer available G, otherwise least-unavailable G in squad
        var gk = available
            .Where(p => p.Position == PlayerPosition.G)
            .OrderByDescending(GetSelectionScore)
            .FirstOrDefault()
            ?? clubSquad
                .Where(p => p.Position == PlayerPosition.G)
                .OrderBy(p => p.InjuryRoundsRemaining + p.SuspensionRoundsRemaining)
                .ThenByDescending(GetSelectionScore)
                .FirstOrDefault()
            ?? available.OrderByDescending(GetSelectionScore).FirstOrDefault();

        if (gk != null)
        {
            starters.Add(gk);
        }

        var remaining = available.Where(p => gk == null || p.Id != gk.Id).ToList();

        void PickSector(PlayerPosition pos, int needed)
        {
            var exact = remaining
                .Where(p => p.Position == pos)
                .OrderByDescending(GetSelectionScore)
                .Take(needed)
                .ToList();
            foreach (var p in exact)
            {
                starters.Add(p);
                remaining.Remove(p);
            }
        }

        PickSector(PlayerPosition.D, spec.Defenders);
        PickSector(PlayerPosition.M, spec.Midfielders);
        PickSector(PlayerPosition.A, spec.Attackers);

        // Fill any missing outfield slots up to 11 with best remaining available outfield players (never adding a 2nd goalkeeper)
        foreach (var nextOutfield in remaining.Where(p => p.Position != PlayerPosition.G).OrderByDescending(GetSelectionScore).ToList())
        {
            if (starters.Count >= 11) break;
            starters.Add(nextOutfield);
            remaining.Remove(nextOutfield);
        }

        // If extreme injuries/suspensions left fewer than 10 available outfield players, fallback to least-unavailable outfield players
        if (starters.Count < 11)
        {
            var emergencyPool = clubSquad
                .Where(p => p.Position != PlayerPosition.G && starters.All(s => s.Id != p.Id))
                .OrderBy(p => p.InjuryRoundsRemaining + p.SuspensionRoundsRemaining)
                .ThenByDescending(GetSelectionScore)
                .Take(11 - starters.Count);
            starters.AddRange(emergencyPool);
        }

        // Build balanced bench (up to 9: 1 G, 2-3 D, 2-3 M, 2-3 A)
        var bench = new List<Player>(9);
        var backupGk = remaining
            .Where(p => p.Position == PlayerPosition.G)
            .OrderByDescending(GetSelectionScore)
            .FirstOrDefault();
        if (backupGk != null)
        {
            bench.Add(backupGk);
            remaining.Remove(backupGk);
        }

        foreach (var p in remaining.OrderByDescending(GetSelectionScore).Take(SquadValidator.MaxBenchSize - bench.Count))
        {
            bench.Add(p);
        }

        return (starters.Select(p => p.Id).ToList(), bench.Select(p => p.Id).ToList());
    }

    public static double GetSelectionScore(Player p)
    {
        double score = p.Strength;
        if (p.IsStar)
        {
            score += p.Position is PlayerPosition.A or PlayerPosition.M ? 5.5 : 3.5;
        }
        // Slight discipline preference against Sarrafeiro when strength is equal
        score -= (int)p.Behavior * 0.25;
        return score;
    }

    /// <summary>
    /// Evaluates whether an AI-controlled team should make tactical or substitution adjustments at the current minute
    /// without opening a global pause window. Respects 5/3 (or 6/4 in extra time) quotas strictly.
    /// </summary>
    public static void EvaluateAiInMatchReaction(
        MatchFixture match,
        Club aiClub,
        AiManager? manager,
        bool isHomeTeam,
        RoundState currentRoundState,
        IReadOnlyDictionary<Guid, Player> worldPlayers,
        bool allowClassic505 = true)
    {
        int minute = match.CurrentMinute;
        bool isFreeInterval = SquadValidator.IsFreeIntervalState(currentRoundState);

        // AI only checks at free intervals (45, 90, 105) or at tactical checkpoints (62, 74, 83, 112) or if an on-pitch player is injured
        var injuredList = isHomeTeam ? match.HomeInjuredInMatchIds : match.AwayInjuredInMatchIds;
        var activeIds = isHomeTeam ? match.HomeActiveStarterIds : match.AwayActiveStarterIds;
        var benchIds = isHomeTeam ? match.HomeBenchIds : match.AwayBenchIds;

        bool hasInjuredOnPitch = activeIds.Any(id => injuredList.Contains(id));
        bool isCheckpointMinute = isFreeInterval || minute is 62 or 74 or 83 or 112;
        if (!hasInjuredOnPitch && !isCheckpointMinute) return;

        int usedSubs = isHomeTeam ? match.HomeUsedSubstitutions : match.AwayUsedSubstitutions;
        int maxSubs = isHomeTeam ? match.HomeMaxSubstitutions : match.AwayMaxSubstitutions;
        int usedWindows = isHomeTeam ? match.HomeUsedPauseWindows : match.AwayUsedPauseWindows;
        int maxWindows = isHomeTeam ? match.HomeMaxPauseWindows : match.AwayMaxPauseWindows;

        if (!isFreeInterval && usedWindows >= maxWindows) return;
        if (usedSubs >= maxSubs && isFreeInterval) return;

        int myGoals = isHomeTeam ? match.HomeGoals : match.AwayGoals;
        int oppGoals = isHomeTeam ? match.AwayGoals : match.HomeGoals;
        int scoreDiff = myGoals - oppGoals;

        var subsToMake = new List<InMatchSubstitutionDto>();
        var availableBench = benchIds
            .Where(worldPlayers.ContainsKey)
            .Select(id => worldPlayers[id])
            .Where(p => p.IsAvailableForMatch)
            .ToList();

        // 1. Replace any injured player on pitch if substitution quota allows
        foreach (var injuredId in activeIds.Where(id => injuredList.Contains(id)).ToList())
        {
            if (usedSubs + subsToMake.Count >= maxSubs) break;
            if (!worldPlayers.TryGetValue(injuredId, out var injuredPlayer)) continue;

            var replacement = availableBench
                .OrderByDescending(p => p.Position == injuredPlayer.Position)
                .ThenByDescending(GetSelectionScore)
                .FirstOrDefault(p => injuredPlayer.Position != PlayerPosition.G || p.Position == PlayerPosition.G);

            if (replacement != null)
            {
                subsToMake.Add(new InMatchSubstitutionDto(injuredId, replacement.Id));
                availableBench.Remove(replacement);
            }
        }

        // 2. Tactical shift & performance substitutions at checkpoints (45, 62, 74, 83, 90, 105)
        string currentFormation = isHomeTeam ? match.HomeFormation : match.AwayFormation;
        string? newFormation = null;
        var identity = manager?.Identity ?? ManagerTacticalIdentity.Equilibrado;
        int adaptability = manager?.Adaptability ?? 50;

        if (adaptability >= 65 && manager != null && manager.PreferredFormations.Count >= 2)
        {
            string fallbackForm = manager.PreferredFormations[1].Formation;
            if (minute >= 62 && scoreDiff < 0 && identity != ManagerTacticalIdentity.Defensivo)
            {
                if (!string.Equals(currentFormation, fallbackForm, StringComparison.OrdinalIgnoreCase))
                {
                    newFormation = fallbackForm;
                }
            }
            else if (minute >= 74 && scoreDiff == 1 && identity == ManagerTacticalIdentity.Defensivo)
            {
                if (!string.Equals(currentFormation, fallbackForm, StringComparison.OrdinalIgnoreCase))
                {
                    newFormation = fallbackForm;
                }
            }
        }

        // Upgrade weakest or yellow-carded outfield player on pitch if bench has a strong replacement
        if (isCheckpointMinute && usedSubs + subsToMake.Count < maxSubs && availableBench.Count > 0)
        {
            var yellowMap = isHomeTeam ? match.HomeYellowCardsInMatch : match.AwayYellowCardsInMatch;
            var activeOutfield = activeIds
                .Where(id => subsToMake.All(s => s.PlayerOutId != id))
                .Where(worldPlayers.ContainsKey)
                .Select(id => worldPlayers[id])
                .Where(p => p.Position != PlayerPosition.G)
                .OrderByDescending(p => yellowMap.ContainsKey(p.Id) && (int)p.Behavior >= (int)PlayerBehavior.Caneleiro ? 1 : 0)
                .ThenBy(p => p.Strength)
                .ToList();

            foreach (var candidateOut in activeOutfield.Take(2))
            {
                if (usedSubs + subsToMake.Count >= maxSubs) break;
                bool hasYellowRisk = yellowMap.ContainsKey(candidateOut.Id);
                var replacement = availableBench
                    .Where(p => p.Position == candidateOut.Position)
                    .OrderByDescending(GetSelectionScore)
                    .FirstOrDefault(p => p.Strength >= candidateOut.Strength - (hasYellowRisk ? 4 : 1));

                if (replacement != null)
                {
                    subsToMake.Add(new InMatchSubstitutionDto(candidateOut.Id, replacement.Id));
                    availableBench.Remove(replacement);
                }
            }
        }

        if (subsToMake.Count > 0 || newFormation != null)
        {
            MatchSimulationEngine.ApplyInMatchAdjustments(
                match,
                aiClub,
                isHomeTeam,
                currentRoundState,
                activePauseWindowId: null,
                newFormation: newFormation,
                substitutions: subsToMake,
                worldPlayers: worldPlayers,
                allowClassic505: allowClassic505);
        }
    }

    public static List<FormationPreferenceWeightDto> GetDefaultPreferencesForIdentity(ManagerTacticalIdentity identity)
        => identity switch
        {
            ManagerTacticalIdentity.Ofensivo => new List<FormationPreferenceWeightDto>
            {
                new("4-3-3", 60),
                new("3-4-3", 25),
                new("4-4-2", 15)
            },
            ManagerTacticalIdentity.Defensivo => new List<FormationPreferenceWeightDto>
            {
                new("5-3-2", 55),
                new("5-4-1", 25),
                new("4-5-1", 20)
            },
            _ => new List<FormationPreferenceWeightDto>
            {
                new("4-4-2", 55),
                new("4-3-3", 25),
                new("3-5-2", 20)
            }
        };
}
