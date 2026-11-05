using Elifoot.Contracts;

namespace Elifoot.Domain;

public record SectorBreakdown(
    double GoalkeeperPower,
    double DefenseSectorPower,
    double MidfieldSectorPower,
    double AttackSectorPower,
    double EffectiveDefenseTotal,
    double EffectiveAttackTotal,
    int ActivePlayersCount);

public static partial class MatchSimulationEngine
{
    public const double StarBonusAttacker = 1.18;
    public const double StarBonusMidfielder = 1.15;
    public const double StarBonusDefenderOrGk = 1.10;
    public const double HomeAdvantageMultiplier = 1.06;
    public const double Classic505TransitionFactor = 0.82;

    /// <summary>
    /// Computes auditable sector power breakdown for a side at the current minute.
    /// Does NOT grant any hidden tackle or defensive bonus to aggressive behaviors (Caceteiro/Sarrafeiro).
    /// </summary>
    public static SectorBreakdown ComputeSectorBreakdown(
        string formationCode,
        IReadOnlyList<Player> activePlayers,
        bool isHomeTeam,
        ManagerTacticalIdentity tacticalIdentity,
        bool allowClassic505 = true)
    {
        if (!TacticalFormations.TryGet(formationCode, allowClassic505, out var spec))
        {
            spec = TacticalFormations.All["4-4-2"];
        }

        if (activePlayers.Count == 0)
        {
            return new SectorBreakdown(1, 1, 1, 1, 1, 1, 0);
        }

        // Identify active Goalkeeper
        var gk = activePlayers.FirstOrDefault(p => p.Position == PlayerPosition.G)
                 ?? activePlayers.OrderByDescending(p => p.Strength).First();

        double gkBase = gk.Strength * (gk.IsStar ? StarBonusDefenderOrGk : 1.0);
        if (gk.Position != PlayerPosition.G)
        {
            gkBase *= 0.35; // Outfield player improvised in goal
        }

        var outfield = activePlayers.Where(p => p.Id != gk.Id).ToList();

        // Allocate outfield players to D, M, A slots according to the formation spec.
        // If team has fewer than 10 outfield players due to red cards/injuries, reduce slots proportionally (A first, then M, then D).
        int targetD = spec.Defenders;
        int targetM = spec.Midfielders;
        int targetA = spec.Attackers;
        int missingOutfield = Math.Max(0, 10 - outfield.Count);
        for (int i = 0; i < missingOutfield; i++)
        {
            if (targetA > 1) targetA--;
            else if (targetM > 1) targetM--;
            else if (targetD > 1) targetD--;
            else if (targetA > 0) targetA--;
            else if (targetM > 0) targetM--;
            else if (targetD > 0) targetD--;
        }

        var remaining = new List<Player>(outfield);

        List<Player> PickForSector(PlayerPosition preferredPos, int count)
        {
            var picked = new List<Player>(count);
            var exact = remaining
                .Where(p => p.Position == preferredPos)
                .OrderByDescending(p => GetEffectivePlayerRating(p, preferredPos))
                .Take(count)
                .ToList();
            foreach (var p in exact)
            {
                picked.Add(p);
                remaining.Remove(p);
            }

            if (picked.Count < count && remaining.Count > 0)
            {
                var improvised = remaining
                    .OrderByDescending(p => GetEffectivePlayerRating(p, preferredPos))
                    .Take(count - picked.Count)
                    .ToList();
                foreach (var p in improvised)
                {
                    picked.Add(p);
                    remaining.Remove(p);
                }
            }
            return picked;
        }

        var assignedD = PickForSector(PlayerPosition.D, targetD);
        var assignedM = PickForSector(PlayerPosition.M, targetM);
        var assignedA = PickForSector(PlayerPosition.A, targetA);

        double defPower = assignedD.Sum(p => GetEffectivePlayerRating(p, PlayerPosition.D));
        double midPower = assignedM.Sum(p => GetEffectivePlayerRating(p, PlayerPosition.M));
        double attPower = assignedA.Sum(p => GetEffectivePlayerRating(p, PlayerPosition.A));

        double homeMod = isHomeTeam ? HomeAdvantageMultiplier : 1.0;
        double identityDefMod = tacticalIdentity switch
        {
            ManagerTacticalIdentity.Defensivo => 1.06,
            ManagerTacticalIdentity.Ofensivo => 0.97,
            _ => 1.0
        };
        double identityAttMod = tacticalIdentity switch
        {
            ManagerTacticalIdentity.Ofensivo => 1.06,
            ManagerTacticalIdentity.Defensivo => 0.97,
            _ => 1.0
        };

        // Short-handed collective penalty (7% per missing player below 11)
        int missingTotal = Math.Max(0, 11 - activePlayers.Count);
        double shortHandedFactor = Math.Pow(0.93, missingTotal);

        double transitionMod = spec.IsClassic505 ? Classic505TransitionFactor : 1.0;

        // Midfielders collaborate in both defense (45%) and attack (50%)
        double effectiveDefense = (defPower + 0.45 * midPower) * homeMod * identityDefMod * shortHandedFactor;
        double effectiveAttack = (attPower + 0.50 * midPower) * homeMod * identityAttMod * shortHandedFactor * transitionMod;

        return new SectorBreakdown(
            GoalkeeperPower: Math.Max(1.0, gkBase * shortHandedFactor),
            DefenseSectorPower: defPower,
            MidfieldSectorPower: midPower,
            AttackSectorPower: attPower,
            EffectiveDefenseTotal: Math.Max(5.0, effectiveDefense),
            EffectiveAttackTotal: Math.Max(5.0, effectiveAttack),
            ActivePlayersCount: activePlayers.Count);
    }

    public static double GetEffectivePlayerRating(Player player, PlayerPosition assignedSector)
    {
        double baseVal = Math.Clamp(player.Strength, 1, 50);
        double starMult = player.IsStar
            ? assignedSector switch
            {
                PlayerPosition.A => StarBonusAttacker,
                PlayerPosition.M => StarBonusMidfielder,
                _ => StarBonusDefenderOrGk
            }
            : 1.0;

        double posMult = player.Position == assignedSector
            ? 1.0
            : (assignedSector == PlayerPosition.G || player.Position == PlayerPosition.G ? 0.35 : 0.65);

        return baseVal * starMult * posMult;
    }

    /// <summary>
    /// Relative foul propensity weight by behavior.
    /// Strictly ordered: FairPlay (0) < Cordeirinho < Cavalheiro < Caneleiro < Caceteiro < Sarrafeiro.
    /// FairPlay has 0 intentional foul propensity and 0 aggressive red card propensity.
    /// </summary>
    public static double GetBehaviorFoulWeight(PlayerBehavior behavior) => behavior switch
    {
        PlayerBehavior.FairPlay => 0.0,
        PlayerBehavior.Cordeirinho => 0.35,
        PlayerBehavior.Cavalheiro => 0.75,
        PlayerBehavior.Caneleiro => 1.45,
        PlayerBehavior.Caceteiro => 2.35,
        PlayerBehavior.Sarrafeiro => 3.60,
        _ => 0.75
    };

    /// <summary>
    /// Computes a player's effective foul weight at the current moment of the match,
    /// applying natural caution when the player already carries 1 yellow card in the match.
    /// </summary>
    public static double GetEffectivePlayerFoulWeight(Player player, bool hasYellowInMatch)
    {
        double baseWeight = GetBehaviorFoulWeight(player.Behavior);
        if (baseWeight <= 0.0 || !hasYellowInMatch) return baseWeight;

        double cautionFactor = player.Behavior is PlayerBehavior.Caceteiro or PlayerBehavior.Sarrafeiro
            ? 0.48
            : 0.32;
        return baseWeight * cautionFactor;
    }

    /// <summary>
    /// Simulates a single virtual minute (tick) for one match fixture.
    /// Does NOT pre-calculate future minutes, so any pause at minute M immediately affects minute M+1.
    /// </summary>
    public static List<MatchEventRecord> SimulateSingleMinute(
        MatchFixture match,
        Club homeClub,
        Club awayClub,
        IReadOnlyDictionary<Guid, Player> worldPlayers,
        ManagerTacticalIdentity homeIdentity,
        ManagerTacticalIdentity awayIdentity,
        int worldSeed,
        bool allowClassic505 = true,
        bool homeHumanPenaltyChoice = false,
        bool awayHumanPenaltyChoice = false)
    {
        var events = new List<MatchEventRecord>();
        if (match.IsFinished || match.IsCancelled || match.IsPostponed || match.CurrentMinute <= match.LastSimulatedMinute) return events;
        match.LastSimulatedMinute = match.CurrentMinute;
        DisciplineEngine.CaptureKickoff(match, worldPlayers);
        PlayerStatisticsEngine.CaptureLineup(match, worldPlayers);
        foreach(bool home in new[]{true,false})
        {
            var active=home?match.HomeActiveStarterIds:match.AwayActiveStarterIds;
            var injured=home?match.HomeInjuredInMatchIds:match.AwayInjuredInMatchIds;
            var sentOff=home?match.HomeSentOffIds:match.AwaySentOffIds;
            var vacancies=home?match.HomeInjuryVacancies:match.AwayInjuryVacancies;
            foreach(var id in active.Where(injured.Contains))if(!sentOff.Contains(id))vacancies.Add(id);
            active.RemoveAll(id=>injured.Contains(id)||sentOff.Contains(id));
        }

        int minute = match.CurrentMinute;
        var rng = DeterministicRng.FromContext(worldSeed, match.SeasonNumber, match.RoundNumber, match.DeterministicMatchIndex, minute);

        var homeActive = match.HomeActiveStarterIds
            .Where(worldPlayers.ContainsKey)
            .Select(id => worldPlayers[id])
            .Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0)
            .ToList();
        var awayActive = match.AwayActiveStarterIds
            .Where(worldPlayers.ContainsKey)
            .Select(id => worldPlayers[id])
            .Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0)
            .ToList();

        // 1. Discipline / Foul & Card Check
        int prevEvents = events.Count;
        ProcessTeamDisciplineTick(match, homeClub, homeActive, isHome: true, minute, rng, events, worldSeed);
        ProcessTeamDisciplineTick(match, awayClub, awayActive, isHome: false, minute, rng, events, worldSeed);

        if (events.Count > prevEvents)
        {
            homeActive = match.HomeActiveStarterIds.Where(worldPlayers.ContainsKey).Select(id => worldPlayers[id]).Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0).ToList();
            awayActive = match.AwayActiveStarterIds.Where(worldPlayers.ContainsKey).Select(id => worldPlayers[id]).Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0).ToList();
        }

        // 2. Injury Check (low deterministic chance per minute)
        prevEvents = events.Count;
        ProcessInjuryTick(match, homeClub, homeActive, isHome: true, minute, rng, events, worldSeed);
        ProcessInjuryTick(match, awayClub, awayActive, isHome: false, minute, rng, events, worldSeed);

        if (events.Count > prevEvents)
        {
            homeActive = match.HomeActiveStarterIds.Where(worldPlayers.ContainsKey).Select(id => worldPlayers[id]).Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0).ToList();
            awayActive = match.AwayActiveStarterIds.Where(worldPlayers.ContainsKey).Select(id => worldPlayers[id]).Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0).ToList();
        }

        // 3. Sector Computation & Attack vs Defense Resolution
        var homeSectors = ComputeSectorBreakdown(match.HomeFormation, homeActive, isHomeTeam: true, homeIdentity, allowClassic505);
        var awaySectors = ComputeSectorBreakdown(match.AwayFormation, awayActive, isHomeTeam: false, awayIdentity, allowClassic505);

        // Home attack chance
        TryResolveAttackAttempt(
            match,
            attackingClub: homeClub,
            attackingPlayers: homeActive,
            defendingPlayers: awayActive,
            attBreakdown: homeSectors,
            defBreakdown: awaySectors,
            isHomeAttacking: true,
            minute: minute,
            rng: rng,
            events: events,
            deferPenaltyChoice: homeHumanPenaltyChoice,
            assistSeed: worldSeed);

        // Away attack chance
        TryResolveAttackAttempt(
            match,
            attackingClub: awayClub,
            attackingPlayers: awayActive,
            defendingPlayers: homeActive,
            attBreakdown: awaySectors,
            defBreakdown: homeSectors,
            isHomeAttacking: false,
            minute: minute,
            rng: rng,
            events: events,
            deferPenaltyChoice: awayHumanPenaltyChoice,
            assistSeed: worldSeed);

        return events;
    }

    private static void ProcessTeamDisciplineTick(
        MatchFixture match,
        Club club,
        List<Player> activePlayers,
        bool isHome,
        int minute,
        DeterministicRng rng,
        List<MatchEventRecord> events,
        int worldSeed)
    {
        // Only players with Behavior != FairPlay commit intentional/aggressive fouls
        var foulCandidates = activePlayers.Where(p => p.Behavior != PlayerBehavior.FairPlay).ToList();
        if (foulCandidates.Count == 0) return;

        var yellowDict = isHome ? match.HomeYellowCardsInMatch : match.AwayYellowCardsInMatch;
        var sentOffList = isHome ? match.HomeSentOffIds : match.AwaySentOffIds;
        var activeIds = isHome ? match.HomeActiveStarterIds : match.AwayActiveStarterIds;

        double totalFoulWeight = foulCandidates.Sum(p => GetEffectivePlayerFoulWeight(p, yellowDict.ContainsKey(p.Id)));
        if (totalFoulWeight <= 0.0) return;

        double avgWeight = totalFoulWeight / Math.Max(1, activePlayers.Count);

        // 1. Foul generation: ~0.095 * avgWeight per minute (~18-20 fouls per match total).
        // A subset of fouls (cardTriggerProb = 0.0128 * avgWeight) warrants a referee card.
        double foulProb = Math.Clamp(0.095 * avgWeight, 0.0, 0.35);
        double cardTriggerProb = 0.0128 * avgWeight;
        double foulRoll = rng.NextDouble();
        if (foulRoll >= foulProb) return;

        if (isHome) match.HomeFouls++;
        else match.AwayFouls++;

        if (foulRoll >= cardTriggerProb) return;

        // 2. Weighted pick of offending player by effective foul weight (includes post-1st-yellow caution)
        double pickRoll = rng.NextDouble() * totalFoulWeight;
        Player offender = foulCandidates[0];
        double cumulative = 0;
        foreach (var candidate in foulCandidates)
        {
            cumulative += GetEffectivePlayerFoulWeight(candidate, yellowDict.ContainsKey(candidate.Id));
            if (pickRoll <= cumulative)
            {
                offender = candidate;
                break;
            }
        }

        // 3. Determine Direct Red vs Yellow Card
        // FairPlay cannot reach here (weight 0). Direct red probability scales monotonically with aggressiveness.
        double directRedChance = offender.Behavior switch
        {
            PlayerBehavior.Cordeirinho => 0.005,
            PlayerBehavior.Cavalheiro => 0.012,
            PlayerBehavior.Caneleiro => 0.025,
            PlayerBehavior.Caceteiro => 0.048,
            PlayerBehavior.Sarrafeiro => 0.085,
            _ => 0.0
        };

        if (rng.NextDouble() < directRedChance)
        {
            activeIds.Remove(offender.Id);
            if (!sentOffList.Contains(offender.Id)) sentOffList.Add(offender.Id);
            offender.SeasonReds++;


            var evt = new MatchEventRecord
            {
                Minute = minute,
                EventType = MatchEventType.DirectRedCard,
                ClubId = club.Id,
                ClubName = club.Name,
                PrimaryPlayerId = offender.Id,
                PrimaryPlayerDisplayName = offender.DisplayName,
                Description = $"{minute}' Cartão VERMELHO direto para {offender.DisplayName} ({club.ShortName})!"
            };
            DisciplineEngine.RecordCard(match,offender,evt,worldSeed);
            match.Events.Add(evt);
            events.Add(evt);
        }
        else
        {
            yellowDict.TryGetValue(offender.Id, out int currentYellows);
            currentYellows++;
            yellowDict[offender.Id] = currentYellows;
            offender.SeasonYellows++;



            if (currentYellows >= 2)
            {
                activeIds.Remove(offender.Id);
                if (!sentOffList.Contains(offender.Id)) sentOffList.Add(offender.Id);
                offender.SeasonReds++;


                var evt = new MatchEventRecord
                {
                    Minute = minute,
                    EventType = MatchEventType.SecondYellowRedCard,
                    ClubId = club.Id,
                    ClubName = club.Name,
                    PrimaryPlayerId = offender.Id,
                    PrimaryPlayerDisplayName = offender.DisplayName,
                    Description = $"{minute}' 2º Amarelo e VERMELHO para {offender.DisplayName} ({club.ShortName})!"
                };
                DisciplineEngine.RecordCard(match,offender,evt,worldSeed);
                match.Events.Add(evt);
                events.Add(evt);
            }
            else
            {
                var evt = new MatchEventRecord
                {
                    Minute = minute,
                    EventType = MatchEventType.YellowCard,
                    ClubId = club.Id,
                    ClubName = club.Name,
                    PrimaryPlayerId = offender.Id,
                    PrimaryPlayerDisplayName = offender.DisplayName,
                    Description = $"{minute}' Cartão amarelo para {offender.DisplayName} ({club.ShortName})."
                };
                DisciplineEngine.RecordCard(match,offender,evt,worldSeed);
                match.Events.Add(evt);
                events.Add(evt);
            }
        }
    }

    private static void ProcessInjuryTick(
        MatchFixture match,
        Club club,
        List<Player> activePlayers,
        bool isHome,
        int minute,
        DeterministicRng rng,
        List<MatchEventRecord> events,
        int worldSeed)
    {
        if (activePlayers.Count == 0) return;

        // ~0.18% chance per team per minute (~16% chance per match of an injury)
        if (rng.NextDouble() >= 0.0018) return;

        var injured = rng.PickOne(activePlayers);
        var injuredList = isHome ? match.HomeInjuredInMatchIds : match.AwayInjuredInMatchIds;
        if (injuredList.Contains(injured.Id)) return;

        injuredList.Add(injured.Id);
        (isHome ? match.HomeActiveStarterIds : match.AwayActiveStarterIds).Remove(injured.Id);
        (isHome ? match.HomeInjuryVacancies : match.AwayInjuryVacancies).Add(injured.Id);
        int legacyDuration = rng.NextInt(1, 4); // Preserve the match RNG stream and occurrence probability.
        if (match.ForceRulesVersion == ClassicForceEvolutionEngine.Version)
            ClassicForceEvolutionEngine.RegisterInjury(match, club, injured, worldSeed);
        else injured.InjuryRoundsRemaining = legacyDuration;

        // Keep the slot available for an injury substitution. Classic day-based injuries are
        // excluded from subsequent active-player projections, even when no substitution remains.
        var evt = new MatchEventRecord
        {
            Minute = minute,
            EventType = MatchEventType.Injury,
            ClubId = club.Id,
            ClubName = club.Name,
            PrimaryPlayerId = injured.Id,
            PrimaryPlayerDisplayName = injured.DisplayName,
            Description = $"{minute}' {injured.DisplayName} ({club.ShortName}) sofreu uma contusão!"
        };
        match.Events.Add(evt);
        events.Add(evt);
    }

    private static void TryResolveAttackAttempt(
        MatchFixture match,
        Club attackingClub,
        List<Player> attackingPlayers,
        List<Player> defendingPlayers,
        SectorBreakdown attBreakdown,
        SectorBreakdown defBreakdown,
        bool isHomeAttacking,
        int minute,
        DeterministicRng rng,
        List<MatchEventRecord> events,
        bool deferPenaltyChoice = false,
        int assistSeed = 0)
    {
        if (attackingPlayers.Count == 0) return;

        // 1. Compare Attack vs Defense to determine if a dangerous chance is created this minute
        double ratio = attBreakdown.EffectiveAttackTotal / Math.Max(10.0, defBreakdown.EffectiveDefenseTotal);
        // Smooth monotonic opportunity probability:
        // When Att == Def (ratio = 1.0), chance is ~4.4% per minute (~4.0 dangerous shots per 90m).
        // Stronger attack or weaker defense increases chance monotonically.
        double chanceProb = 0.044 * Math.Pow(ratio, 0.85);
        chanceProb = Math.Clamp(chanceProb, 0.008, 0.14);

        if (rng.NextDouble() >= chanceProb) return;

        // Small offside chance if opponent defense is well organized
        if (rng.NextDouble() < 0.09)
        {
            var offsidePlayer = PickShooter(attackingPlayers, rng);
            var offEvt = new MatchEventRecord
            {
                Minute = minute,
                EventType = MatchEventType.Offside,
                ClubId = attackingClub.Id,
                ClubName = attackingClub.Name,
                PrimaryPlayerId = offsidePlayer.Id,
                PrimaryPlayerDisplayName = offsidePlayer.DisplayName,
                Description = $"{minute}' Impedimento marcado contra {offsidePlayer.DisplayName} ({attackingClub.ShortName})."
            };
            match.Events.Add(offEvt);
            events.Add(offEvt);
            return;
        }

        // Check for in-box foul resulting in a Penalty Kick (weighted by defending box players' effective foul weight; FairPlay = 0)
        var defendingYellowMap = isHomeAttacking ? match.AwayYellowCardsInMatch : match.HomeYellowCardsInMatch;
        var boxDefenders = defendingPlayers.Where(p => p.Position is PlayerPosition.D or PlayerPosition.G).ToList();
        if (boxDefenders.Count == 0) boxDefenders = defendingPlayers;
        double avgBoxFoulWeight = boxDefenders.Count > 0
            ? boxDefenders.Average(p => GetEffectivePlayerFoulWeight(p, defendingYellowMap.ContainsKey(p.Id)))
            : 0.0;
        double penaltyFoulProb = avgBoxFoulWeight > 0.0
            ? Math.Clamp(0.031 * avgBoxFoulWeight, 0.0, 0.085)
            : 0.0;

        if (penaltyFoulProb > 0.0 && rng.NextDouble() < penaltyFoulProb)
        {
            if (isHomeAttacking) match.AwayFouls++;
            else match.HomeFouls++;

            var penTaker = attackingPlayers
                .OrderByDescending(p => p.IsStar)
                .ThenByDescending(p => p.Position == PlayerPosition.A)
                .ThenByDescending(p => p.Strength)
                .First();

            double convRoll = rng.NextDouble();
            var defendingGk = defendingPlayers.FirstOrDefault(p => p.Position == PlayerPosition.G) ?? defendingPlayers.FirstOrDefault();
            if (deferPenaltyChoice && attackingClub.HumanCoachId is Guid coachId)
            {
                match.PendingPenalties.Add(new PendingPenaltyKick {
                    CoachId = coachId, ClubId = attackingClub.Id, Minute = minute,
                    ConversionRoll = convRoll, GoalkeeperPower = defBreakdown.GoalkeeperPower,
                    GoalkeeperId = defendingGk?.Id, EligiblePlayerIds = attackingPlayers.Select(p => p.Id).ToList()
                });
                var awarded = new MatchEventRecord { Minute = minute, EventType = MatchEventType.PenaltyAwarded,
                    ClubId = attackingClub.Id, ClubName = attackingClub.Name,
                    Description = $"{minute}' PÊNALTI para {attackingClub.Name}. Aguardando escolha do cobrador." };
                match.Events.Add(awarded); events.Add(awarded);
            }
            else ResolvePenaltyKick(match, attackingClub, penTaker, defendingGk, defBreakdown.GoalkeeperPower, isHomeAttacking, minute, convRoll, events);
            return;
        }

        // 2. Shot vs Goalkeeper resolution
        var shooter = PickShooter(attackingPlayers, rng);
        double shooterFinishing = shooter.Strength * (shooter.IsStar ? 1.20 : 1.0);
        if (shooter.Position == PlayerPosition.A) shooterFinishing *= 1.12;
        else if (shooter.Position == PlayerPosition.M) shooterFinishing *= 1.02;
        else shooterFinishing *= 0.82;

        // Injured on pitch penalty
        var injuredIds = isHomeAttacking ? match.HomeInjuredInMatchIds : match.AwayInjuredInMatchIds;
        if (injuredIds.Contains(shooter.Id))
        {
            shooterFinishing *= 0.60;
        }

        // Compare shooter finishing + team attack support vs goalkeeper power + defensive pressure
        double shotPower = shooterFinishing + 0.08 * attBreakdown.EffectiveAttackTotal;
        double gkResistance = defBreakdown.GoalkeeperPower * 1.18 + 0.06 * defBreakdown.EffectiveDefenseTotal;

        // Monotonic conversion probability: higher shooter/attack increases P(goal); higher GK/defense reduces P(goal)
        double conversionRatio = shotPower / Math.Max(8.0, gkResistance);
        double goalProb = 0.34 * Math.Pow(conversionRatio, 0.90);
        goalProb = Math.Clamp(goalProb, 0.06, 0.72);

        if (rng.NextDouble() < goalProb)
        {
            // Describe the final pass with an independent stream: sporting RNG and outcomes are unchanged.
            // Selecting the shooter represents a solo finish; every position may supply the pass.
            var passRng = DeterministicRng.FromContext(assistSeed ^ 0x41535349, match.SeasonNumber,
                match.RoundNumber, match.DeterministicMatchIndex * 2 + (isHomeAttacking ? 0 : 1), minute);
            var passer = attackingPlayers[(int)(passRng.NextDouble() * attackingPlayers.Count)];
            RecordGoalScored(match, attackingClub, shooter, isHomeAttacking, minute, isPenalty: false, events,
                passer.Id == shooter.Id ? null : passer);
        }
    }

    private static void RecordGoalScored(
        MatchFixture match,
        Club attackingClub,
        Player scorer,
        bool isHomeAttacking,
        int minute,
        bool isPenalty,
        List<MatchEventRecord> events,
        Player? assister = null)
    {
        if (isHomeAttacking)
        {
            match.HomeGoals++;
            if (minute <= 45) match.HomeFirstHalfGoals++;
        }
        else
        {
            match.AwayGoals++;
            if (minute <= 45) match.AwayFirstHalfGoals++;
        }

        if (match.HomeGoals > match.AwayGoals) match.HomeLedDuringMatch = true;
        else if (match.AwayGoals > match.HomeGoals) match.AwayLedDuringMatch = true;

        scorer.SeasonGoals++;
        scorer.CareerGoals++;
        if (match.CompetitionType != CompetitionType.League)
        {
            scorer.CupGoals++;
        }

        string penSuffix = isPenalty ? " (Pênalti)" : "";
        var goalEvt = new MatchEventRecord
        {
            Minute = minute,
            EventType = MatchEventType.Goal,
            ClubId = attackingClub.Id,
            ClubName = attackingClub.Name,
            PrimaryPlayerId = scorer.Id,
            PrimaryPlayerDisplayName = scorer.DisplayName,
            AssistPlayerId = !isPenalty ? assister?.Id : null,
            AssistPlayerDisplayName = !isPenalty ? assister?.DisplayName : null,
            HasAssistCoverage = true,
            Description = $"{minute}' GOOOL! {scorer.DisplayName}{penSuffix} marca para {attackingClub.Name}! ({match.HomeGoals}x{match.AwayGoals})"
        };
        match.Events.Add(goalEvt);
        events.Add(goalEvt);
    }

    private static Player PickShooter(List<Player> activePlayers, DeterministicRng rng)
    {
        var weights = new double[activePlayers.Count];
        double total = 0;
        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            double w = p.Position switch
            {
                PlayerPosition.A => 5.5,
                PlayerPosition.M => 2.8,
                PlayerPosition.D => 0.7,
                PlayerPosition.G => 0.02,
                _ => 1.0
            };
            if (p.IsStar) w *= 1.35; // Star (*) players get into scoring positions more often
            w *= (0.7 + p.Strength / 50.0);
            weights[i] = w;
            total += w;
        }

        double roll = rng.NextDouble() * total;
        double acc = 0;
        for (int i = 0; i < activePlayers.Count; i++)
        {
            acc += weights[i];
            if (roll <= acc) return activePlayers[i];
        }
        return activePlayers[^1];
    }

    /// <summary>
    /// Validates and applies in-match substitutions and/or tactical formation changes for a team.
    /// Enforces:
    /// - 5 substitutions in 90m (6 in extra time)
    /// - 3 pause windows in 90m (4 in extra time)
    /// - Free intervals (Halftime 45', Extra Time Break 90', Extra Time Halftime 105') cost 0 pause windows
    /// - Multiple changes inside the same PauseWindowId cost only 1 pause window for that coach
    /// - Sent-off players cannot be replaced
    /// - Substituted-out players cannot return to the pitch
    /// </summary>
    public static (bool Success, string? ErrorMessage, List<MatchEventRecord> NewEvents) ApplyInMatchAdjustments(
        MatchFixture match,
        Club club,
        bool isHomeTeam,
        RoundState currentRoundState,
        Guid? activePauseWindowId,
        string? newFormation,
        IReadOnlyList<InMatchSubstitutionDto> substitutions,
        IReadOnlyDictionary<Guid, Player> worldPlayers,
        bool allowClassic505 = true)
    {
        var createdEvents = new List<MatchEventRecord>();
        PlayerStatisticsEngine.CaptureLineup(match, worldPlayers);
        bool hasFormationChange = !string.IsNullOrWhiteSpace(newFormation) &&
            !string.Equals(newFormation.Trim(), isHomeTeam ? match.HomeFormation : match.AwayFormation, StringComparison.OrdinalIgnoreCase);
        bool hasSubs = substitutions.Count > 0;

        if (!hasFormationChange && !hasSubs)
        {
            return (true, null, createdEvents);
        }

        if (hasFormationChange && !TacticalFormations.TryGet(newFormation!, allowClassic505, out _))
        {
            return (false, $"Formação '{newFormation}' inválida.", createdEvents);
        }

        bool isFreeInterval = SquadValidator.IsFreeIntervalState(currentRoundState);
        int usedSubs = isHomeTeam ? match.HomeUsedSubstitutions : match.AwayUsedSubstitutions;
        int maxSubs = isHomeTeam ? match.HomeMaxSubstitutions : match.AwayMaxSubstitutions;
        int usedWindows = isHomeTeam ? match.HomeUsedPauseWindows : match.AwayUsedPauseWindows;
        int maxWindows = isHomeTeam ? match.HomeMaxPauseWindows : match.AwayMaxPauseWindows;
        var consumedWindowSet = isHomeTeam ? match.HomeConsumedPauseWindowIds : match.AwayConsumedPauseWindowIds;

        // Check Pause Window opportunity quota if not a free interval
        bool needsNewWindowQuota = !isFreeInterval &&
            (activePauseWindowId == null || !consumedWindowSet.Contains(activePauseWindowId.Value));

        if (needsNewWindowQuota && usedWindows >= maxWindows)
        {
            return (false, $"Limite de oportunidades de parada ({usedWindows}/{maxWindows}) esgotado para {club.Name}.", createdEvents);
        }

        if (usedSubs + substitutions.Count > maxSubs)
        {
            return (false, $"Limite de substituições ({usedSubs}/{maxSubs}) seria excedido ao tentar {substitutions.Count} troca(s).", createdEvents);
        }

        var activeStarters = isHomeTeam ? match.HomeActiveStarterIds : match.AwayActiveStarterIds;
        var bench = isHomeTeam ? match.HomeBenchIds : match.AwayBenchIds;
        var subbedOut = isHomeTeam ? match.HomeSubstitutedOutIds : match.AwaySubstitutedOutIds;
        var subbedIn = isHomeTeam ? match.HomeSubstitutedInIds : match.AwaySubstitutedInIds;
        var sentOff = isHomeTeam ? match.HomeSentOffIds : match.AwaySentOffIds;
        var vacancies = isHomeTeam ? match.HomeInjuryVacancies : match.AwayInjuryVacancies;

        // Validate each substitution before mutating state
        var simActive = new List<Guid>(activeStarters);
        var simBench = new List<Guid>(bench);
        var simSubbedOut = new List<Guid>(subbedOut);
        var simVacancies = new HashSet<Guid>(vacancies);

        foreach (var sub in substitutions)
        {
            if (sentOff.Contains(sub.PlayerOutId))
                return (false, "Jogador expulso não pode ser substituído.", createdEvents);

            if (!simActive.Contains(sub.PlayerOutId) && !simVacancies.Contains(sub.PlayerOutId))
                return (false, "O jogador que sai não está em campo.", createdEvents);

            if (simSubbedOut.Contains(sub.PlayerInId))
                return (false, "Um jogador já substituído não pode retornar ao gramado.", createdEvents);

            if (!simBench.Contains(sub.PlayerInId) || sentOff.Contains(sub.PlayerInId))
                return (false, "O jogador que entra não está disponível no banco de reservas.", createdEvents);

            if (!worldPlayers.TryGetValue(sub.PlayerInId, out var pIn) || !pIn.IsAvailableForMatch)
                return (false, "O jogador que entra não está apto para atuar.", createdEvents);

            simActive.Remove(sub.PlayerOutId);
            simVacancies.Remove(sub.PlayerOutId);
            simActive.Add(sub.PlayerInId);
            simBench.Remove(sub.PlayerInId);
            simSubbedOut.Add(sub.PlayerOutId);
        }

        // Verify that at least 1 Goalkeeper remains on pitch (unless goalkeeper was sent off and no G exists, or a player is assigned)
        int gkAfter = simActive.Where(worldPlayers.ContainsKey).Count(id => worldPlayers[id].Position == PlayerPosition.G);
        if (gkAfter == 0 && activeStarters.Where(worldPlayers.ContainsKey).Any(id => worldPlayers[id].Position == PlayerPosition.G))
        {
            return (false, "A equipe não pode retirar seu goleiro sem colocar outro goleiro em campo.", createdEvents);
        }

        // Commit changes
        if (needsNewWindowQuota)
        {
            if (isHomeTeam) match.HomeUsedPauseWindows++;
            else match.AwayUsedPauseWindows++;

            if (activePauseWindowId != null)
            {
                consumedWindowSet.Add(activePauseWindowId.Value);
            }
        }

        foreach (var sub in substitutions)
        {
            activeStarters.Remove(sub.PlayerOutId);
            vacancies.Remove(sub.PlayerOutId);
            activeStarters.Add(sub.PlayerInId);
            bench.Remove(sub.PlayerInId);
            subbedOut.Add(sub.PlayerOutId);
            subbedIn.Add(sub.PlayerInId);

            if (isHomeTeam) match.HomeUsedSubstitutions++;
            else match.AwayUsedSubstitutions++;

            var pOut = worldPlayers[sub.PlayerOutId];
            var pIn = worldPlayers[sub.PlayerInId];
            pIn.SeasonMatches++;
            pIn.CareerMatches++;

            var evt = new MatchEventRecord
            {
                Minute = match.CurrentMinute,
                EventType = MatchEventType.Substitution,
                ClubId = club.Id,
                ClubName = club.Name,
                PrimaryPlayerId = pIn.Id,
                PrimaryPlayerDisplayName = pIn.DisplayName,
                SecondaryPlayerId = pOut.Id,
                SecondaryPlayerDisplayName = pOut.DisplayName,
                Description = $"{match.CurrentMinute}' Substituição ({club.ShortName}): entra {pIn.DisplayName}, sai {pOut.DisplayName}."
            };
            match.Events.Add(evt);
            createdEvents.Add(evt);
        }

        if (hasFormationChange)
        {
            string normalized = newFormation!.Trim();
            if (isHomeTeam) match.HomeFormation = normalized;
            else match.AwayFormation = normalized;

            var evt = new MatchEventRecord
            {
                Minute = match.CurrentMinute,
                EventType = MatchEventType.TacticChange,
                ClubId = club.Id,
                ClubName = club.Name,
                Description = $"{match.CurrentMinute}' Alteração tática ({club.ShortName}): {normalized}."
            };
            match.Events.Add(evt);
            createdEvents.Add(evt);
        }

        return (true, null, createdEvents);
    }

    /// <summary>
    /// Resolves a deterministic penalty shootout (5 initial kicks + sudden death) for knockout matches tied at 120'.
    /// </summary>
    public static void ResolvePenaltyShootout(
        MatchFixture match,
        Club homeClub,
        Club awayClub,
        IReadOnlyDictionary<Guid, Player> worldPlayers,
        int worldSeed)
    {
        var rng = DeterministicRng.FromContext(worldSeed, match.SeasonNumber, match.RoundNumber, match.DeterministicMatchIndex, 121);
        int homePens = 0;
        int awayPens = 0;

        var homeTakers = match.HomeActiveStarterIds
            .Where(worldPlayers.ContainsKey)
            .Select(id => worldPlayers[id])
            .Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0)
            .OrderByDescending(p => p.IsStar)
            .ThenByDescending(p => p.Position == PlayerPosition.A)
            .ThenByDescending(p => p.Strength)
            .ToList();

        var awayTakers = match.AwayActiveStarterIds
            .Where(worldPlayers.ContainsKey)
            .Select(id => worldPlayers[id])
            .Where(p => match.ForceRulesVersion != ClassicForceEvolutionEngine.Version || p.InjuryDaysRemaining == 0)
            .OrderByDescending(p => p.IsStar)
            .ThenByDescending(p => p.Position == PlayerPosition.A)
            .ThenByDescending(p => p.Strength)
            .ToList();

        var homeGk = homeTakers.FirstOrDefault(p => p.Position == PlayerPosition.G);
        var awayGk = awayTakers.FirstOrDefault(p => p.Position == PlayerPosition.G);

        double HomeConversionProb(Player taker)
        {
            double baseProb = 0.74 + (taker.Strength - 25) * 0.004 + (taker.IsStar ? 0.06 : 0.0);
            double gkFactor = (awayGk?.Strength ?? 20) * 0.003;
            return Math.Clamp(baseProb - gkFactor, 0.55, 0.90);
        }

        double AwayConversionProb(Player taker)
        {
            double baseProb = 0.74 + (taker.Strength - 25) * 0.004 + (taker.IsStar ? 0.06 : 0.0);
            double gkFactor = (homeGk?.Strength ?? 20) * 0.003;
            return Math.Clamp(baseProb - gkFactor, 0.55, 0.90);
        }

        int homeAttempts = 0;
        int awayAttempts = 0;

        for (int kick = 0; kick < 5; kick++)
        {
            if (homeTakers.Count > 0)
            {
                homeAttempts++;
                var hTaker = homeTakers[kick % homeTakers.Count];
                if (rng.NextDouble() < HomeConversionProb(hTaker)) homePens++;
            }
            if (awayTakers.Count > 0)
            {
                int remainingForAway = 4 - kick;
                if (Math.Abs(homePens - awayPens) <= remainingForAway + 1)
                {
                    awayAttempts++;
                    var aTaker = awayTakers[kick % awayTakers.Count];
                    if (rng.NextDouble() < AwayConversionProb(aTaker)) awayPens++;
                }
            }

            int remaining = 4 - kick;
            if (Math.Abs(homePens - awayPens) > remaining) break;
        }

        int suddenDeathRound = 5;
        while (homePens == awayPens && suddenDeathRound < 25)
        {
            var hTaker = homeTakers.Count > 0 ? homeTakers[suddenDeathRound % homeTakers.Count] : null;
            var aTaker = awayTakers.Count > 0 ? awayTakers[suddenDeathRound % awayTakers.Count] : null;
            if (hTaker != null) homeAttempts++;
            if (aTaker != null) awayAttempts++;
            bool hScored = hTaker != null && rng.NextDouble() < HomeConversionProb(hTaker);
            bool aScored = aTaker != null && rng.NextDouble() < AwayConversionProb(aTaker);
            if (hScored) homePens++;
            if (aScored) awayPens++;
            suddenDeathRound++;
        }

        if (homePens == awayPens)
        {
            homeAttempts++;
            awayAttempts++;
            if (rng.NextDouble() < 0.5) homePens++;
            else awayPens++;
        }

        match.HomeShootoutAttempts = homeAttempts;
        match.AwayShootoutAttempts = awayAttempts;
        match.HomePenaltyGoals = homePens;
        match.AwayPenaltyGoals = awayPens;
        match.WinnerClubId = homePens > awayPens ? homeClub.Id : awayClub.Id;

        match.Events.Add(new MatchEventRecord
        {
            Minute = 120,
            EventType = MatchEventType.PenaltyShootoutGoal,
            ClubId = match.WinnerClubId.Value,
            ClubName = homePens > awayPens ? homeClub.Name : awayClub.Name,
            Description = $"Disputa de pênaltis encerrada: {homeClub.ShortName} {homePens} x {awayPens} {awayClub.ShortName}."
        });
    }

    /// <summary>
    /// Executes a complete match using the real MatchSimulationEngine minute by minute (1..90, halftime at 45',
    /// extra time 91..120 with intervals at 90' and 105' when tied in knockout rounds, and penalty shootouts at 120').
    /// Never uses aggregate scores or shortcuts. Returns the total simulated match minutes (90 or 120).
    /// </summary>
    public static int SimulateFullMatchReal(
        MatchFixture match,
        Club homeClub,
        Club awayClub,
        AiManager? homeMgr,
        AiManager? awayMgr,
        IReadOnlyList<Player> homeSquad,
        IReadOnlyList<Player> awaySquad,
        IReadOnlyDictionary<Guid, Player> worldPlayers,
        int worldSeed,
        bool allowClassic505 = true)
    {
        if(match.IsFinished||match.IsCancelled||match.IsPostponed)return 0;
        // 1. Prepare initial lineups via AI Manager Brain if not already locked by a Human Coach
        if (match.CurrentMinute==0&&!SquadValidator.ValidatePreMatchLineup(match.HomeFormation,match.HomeActiveStarterIds,match.HomeBenchIds,homeSquad,allowClassic505).IsValid)
        {
            if (homeMgr != null)
            {
                var (form, starters, bench, _) = AiManagerBrain.PlanAndSelectLineup(
                    homeMgr, homeClub, homeSquad, awayClub, awaySquad, isHomeMatch: true,
                    worldSeed, match.SeasonNumber, match.RoundNumber, allowClassic505);
                match.HomeFormation = form;
                match.HomeActiveStarterIds = starters;
                match.HomeBenchIds = bench;
            }
            else
            {
                var (starters, bench) = AiManagerBrain.BuildStartersAndBench(homeSquad, TacticalFormations.All["4-4-2"]);
                match.HomeFormation = "4-4-2";
                match.HomeActiveStarterIds = starters;
                match.HomeBenchIds = bench;
            }
        }

        if (match.CurrentMinute==0&&!SquadValidator.ValidatePreMatchLineup(match.AwayFormation,match.AwayActiveStarterIds,match.AwayBenchIds,awaySquad,allowClassic505).IsValid)
        {
            if (awayMgr != null)
            {
                var (form, starters, bench, _) = AiManagerBrain.PlanAndSelectLineup(
                    awayMgr, awayClub, awaySquad, homeClub, homeSquad, isHomeMatch: false,
                    worldSeed, match.SeasonNumber, match.RoundNumber, allowClassic505);
                match.AwayFormation = form;
                match.AwayActiveStarterIds = starters;
                match.AwayBenchIds = bench;
            }
            else
            {
                var (starters, bench) = AiManagerBrain.BuildStartersAndBench(awaySquad, TacticalFormations.All["4-4-2"]);
                match.AwayFormation = "4-4-2";
                match.AwayActiveStarterIds = starters;
                match.AwayBenchIds = bench;
            }
        }

        // Count appearance for starting 11
        foreach (var sId in match.HomeActiveStarterIds)
        {
            if (worldPlayers.TryGetValue(sId, out var p)) { p.SeasonMatches++; p.CareerMatches++; }
        }
        foreach (var sId in match.AwayActiveStarterIds)
        {
            if (worldPlayers.TryGetValue(sId, out var p)) { p.SeasonMatches++; p.CareerMatches++; }
        }

        var homeIdentity = homeMgr?.Identity ?? ManagerTacticalIdentity.Equilibrado;
        var awayIdentity = awayMgr?.Identity ?? ManagerTacticalIdentity.Equilibrado;

        // 2. First Half (Minutes 1..45)
        for (int m = 1; m <= 45; m++)
        {
            match.CurrentMinute = m;
            SimulateSingleMinute(match, homeClub, awayClub, worldPlayers, homeIdentity, awayIdentity, worldSeed, allowClassic505);
            if (homeClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.RUNNING_FIRST_HALF, worldPlayers, allowClassic505);
            if (awayClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.RUNNING_FIRST_HALF, worldPlayers, allowClassic505);
        }

        // 3. Free Halftime Interval at 45'
        if (homeClub.HumanCoachId == null)
            AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.HALFTIME_PAUSED, worldPlayers, allowClassic505);
        if (awayClub.HumanCoachId == null)
            AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.HALFTIME_PAUSED, worldPlayers, allowClassic505);

        // 4. Second Half (Minutes 46..90)
        for (int m = 46; m <= 90; m++)
        {
            match.CurrentMinute = m;
            SimulateSingleMinute(match, homeClub, awayClub, worldPlayers, homeIdentity, awayIdentity, worldSeed, allowClassic505);
            if (homeClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.RUNNING_SECOND_HALF, worldPlayers, allowClassic505);
            if (awayClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.RUNNING_SECOND_HALF, worldPlayers, allowClassic505);
        }

        int simulatedMinutes = 90;

        // 5. Extra Time (Minutes 91..120) & Penalty Shootout if Knockout Match is tied at 90'
        if (match.RequiresKnockoutResolution && match.HomeGoals == match.AwayGoals)
        {
            match.EnteredExtraTime = true; // Unlocks 6th substitution & 4th pause window
            match.Events.Add(new MatchEventRecord
            {
                Minute = 90,
                EventType = MatchEventType.ExtraTimeStarted,
                ClubId = homeClub.Id,
                ClubName = homeClub.Name,
                Description = "90' Empate no tempo normal! A partida vai para a PRORROGAÇÃO (+1 substituição e +1 parada)."
            });

            // Free break before Extra Time (90')
            if (homeClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.EXTRA_TIME_BREAK, worldPlayers, allowClassic505);
            if (awayClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.EXTRA_TIME_BREAK, worldPlayers, allowClassic505);

            // First Half of Extra Time (91..105)
            for (int m = 91; m <= 105; m++)
            {
                match.CurrentMinute = m;
                SimulateSingleMinute(match, homeClub, awayClub, worldPlayers, homeIdentity, awayIdentity, worldSeed, allowClassic505);
                if (homeClub.HumanCoachId == null)
                    AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.RUNNING_EXTRA_TIME, worldPlayers, allowClassic505);
                if (awayClub.HumanCoachId == null)
                    AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.RUNNING_EXTRA_TIME, worldPlayers, allowClassic505);
            }

            // Free Halftime of Extra Time (105')
            if (homeClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.EXTRA_TIME_HALFTIME, worldPlayers, allowClassic505);
            if (awayClub.HumanCoachId == null)
                AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.EXTRA_TIME_HALFTIME, worldPlayers, allowClassic505);

            // Second Half of Extra Time (106..120)
            for (int m = 106; m <= 120; m++)
            {
                match.CurrentMinute = m;
                SimulateSingleMinute(match, homeClub, awayClub, worldPlayers, homeIdentity, awayIdentity, worldSeed, allowClassic505);
                if (homeClub.HumanCoachId == null)
                    AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, true, RoundState.RUNNING_EXTRA_TIME, worldPlayers, allowClassic505);
                if (awayClub.HumanCoachId == null)
                    AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, false, RoundState.RUNNING_EXTRA_TIME, worldPlayers, allowClassic505);
            }

            simulatedMinutes = 120;

            if (match.HomeGoals == match.AwayGoals)
            {
                ResolvePenaltyShootout(match, homeClub, awayClub, worldPlayers, worldSeed);
            }
            else
            {
                match.WinnerClubId = match.HomeGoals > match.AwayGoals ? homeClub.Id : awayClub.Id;
            }
        }
        else
        {
            match.WinnerClubId = match.HomeGoals > match.AwayGoals
                ? homeClub.Id
                : (match.AwayGoals > match.HomeGoals ? awayClub.Id : null);
        }

        match.IsFinished = true;
        return simulatedMinutes;
    }
}
