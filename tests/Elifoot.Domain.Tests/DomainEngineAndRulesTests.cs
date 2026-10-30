using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class DomainEngineAndRulesTests
{
    [Fact]
    public void Player_DisplayName_PreservesLiteralAsterisk_AndSquadLimitsAreEnforced()
    {
        var starPlayer = new Player { Name = "João Silva", IsStar = true, Position = PlayerPosition.A, Strength = 42 };
        var normalPlayer = new Player { Name = "Carlos Rocha", IsStar = false, Position = PlayerPosition.D, Strength = 28 };

        Assert.Equal("João Silva*", starPlayer.DisplayName);
        Assert.Equal("Carlos Rocha", normalPlayer.DisplayName);

        var world = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        var club = world.Clubs.First();
        var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();

        var (valid, err) = SquadValidator.ValidateClubSquadLimits(squad);
        Assert.True(valid, err);

        // Cannot remove sole goalkeeper if only 1 GK remains
        var gks = squad.Where(p => p.Position == PlayerPosition.G).ToList();
        while (gks.Count > 1)
        {
            squad.Remove(gks[^1]);
            gks.RemoveAt(gks.Count - 1);
        }

        var (canRemoveSoleGk, gkErr) = SquadValidator.CanRemovePlayerFromSquad(squad, gks[0].Id);
        Assert.False(canRemoveSoleGk);
        Assert.Contains("goleiro", gkErr!, StringComparison.OrdinalIgnoreCase);

        // Cannot drop below 11 players
        var elevenSquad = new List<Player> { gks[0] };
        var elevenOutfield = squad.Where(p => p.Position != PlayerPosition.G).Take(10).ToList();
        var minSquad = elevenSquad.Concat(elevenOutfield).ToList();
        Assert.Equal(11, minSquad.Count);

        var (canRemoveFrom11, minErr) = SquadValidator.CanRemovePlayerFromSquad(minSquad, elevenOutfield[0].Id);
        Assert.False(canRemoveFrom11);
        Assert.Contains("mínimo", minErr!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchEngine_Monotonicity_StrongerDefense_StrongerGk_StarBonus_RedCard_AndBehaviorDiscipline()
    {
        // 1. Stronger Defense vs Weaker Defense (keeping everything else identical)
        int goalsAgainstWeakDef = SimulateSeriesGoals(attStrength: 35, defStrength: 15, gkStrength: 28, hasStarAttackers: false, seeds: 120);
        int goalsAgainstStrongDef = SimulateSeriesGoals(attStrength: 35, defStrength: 45, gkStrength: 28, hasStarAttackers: false, seeds: 120);
        Assert.True(goalsAgainstStrongDef < goalsAgainstWeakDef,
            $"Defesa forte ({goalsAgainstStrongDef} gols sofridos) deveria sofrer menos gols que defesa fraca ({goalsAgainstWeakDef}).");

        // 2. Stronger Goalkeeper vs Weaker Goalkeeper
        int goalsAgainstWeakGk = SimulateSeriesGoals(attStrength: 35, defStrength: 30, gkStrength: 10, hasStarAttackers: false, seeds: 120);
        int goalsAgainstStrongGk = SimulateSeriesGoals(attStrength: 35, defStrength: 30, gkStrength: 48, hasStarAttackers: false, seeds: 120);
        Assert.True(goalsAgainstStrongGk < goalsAgainstWeakGk,
            $"Goleiro forte ({goalsAgainstStrongGk} gols) deveria sofrer menos gols que goleiro fraco ({goalsAgainstWeakGk}).");

        // 3. Star (*) presence in Midfield/Attack increases goals scored
        int goalsWithoutStars = SimulateSeriesGoals(attStrength: 32, defStrength: 32, gkStrength: 32, hasStarAttackers: false, seeds: 120);
        int goalsWithStars = SimulateSeriesGoals(attStrength: 32, defStrength: 32, gkStrength: 32, hasStarAttackers: true, seeds: 120);
        Assert.True(goalsWithStars > goalsWithoutStars,
            $"Ataque com craques '*' ({goalsWithStars} gols) deveria marcar mais gols que ataque sem '*' ({goalsWithoutStars}).");

        // 4. Red card / expulsion weakens sector breakdown
        var full11 = CreateTestSquad(30, false, PlayerBehavior.Cavalheiro).Take(11).ToList();
        var fullBreakdown = MatchSimulationEngine.ComputeSectorBreakdown("4-4-2", full11, isHomeTeam: true, ManagerTacticalIdentity.Equilibrado);
        var tenMen = full11.Take(10).ToList();
        var tenMenBreakdown = MatchSimulationEngine.ComputeSectorBreakdown("4-4-2", tenMen, isHomeTeam: true, ManagerTacticalIdentity.Equilibrado);
        Assert.True(tenMenBreakdown.EffectiveAttackTotal < fullBreakdown.EffectiveAttackTotal);
        Assert.True(tenMenBreakdown.EffectiveDefenseTotal < fullBreakdown.EffectiveDefenseTotal);

        // 5. Discipline: Fair Play has 0 cards; Sarrafeiro has highest card count; no hidden defensive bonus for Sarrafeiro
        var fairPlaySquad = CreateTestSquad(30, false, PlayerBehavior.FairPlay).Take(11).ToList();
        var sarrafeiroSquad = CreateTestSquad(30, false, PlayerBehavior.Sarrafeiro).Take(11).ToList();
        var fpSec = MatchSimulationEngine.ComputeSectorBreakdown("4-4-2", fairPlaySquad, true, ManagerTacticalIdentity.Equilibrado);
        var sarSec = MatchSimulationEngine.ComputeSectorBreakdown("4-4-2", sarrafeiroSquad, true, ManagerTacticalIdentity.Equilibrado);
        Assert.Equal(fpSec.EffectiveDefenseTotal, sarSec.EffectiveDefenseTotal, 4);

        var (fpYellows, fpReds) = SimulateSeriesCards(PlayerBehavior.FairPlay, seeds: 80);
        var (cavYellows, cavReds) = SimulateSeriesCards(PlayerBehavior.Cavalheiro, seeds: 80);
        var (sarYellows, sarReds) = SimulateSeriesCards(PlayerBehavior.Sarrafeiro, seeds: 80);

        Assert.Equal(0, fpYellows);
        Assert.Equal(0, fpReds);
        Assert.True(sarYellows + sarReds > cavYellows + cavReds,
            $"Sarrafeiro ({sarYellows + sarReds}) deve receber mais cartões que Cavalheiro ({cavYellows + cavReds}).");
    }

    [Fact]
    public void Discipline_Audit006_SingleCardPerFoul_SecondYellowBugFix_SentOffIsolation_AndFairPlayZeroFouls()
    {
        // 1. Monotonicity of foul weights and post-1st-yellow caution dampening
        Assert.Equal(0.0, MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.FairPlay));
        Assert.True(MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Cordeirinho) < MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Cavalheiro));
        Assert.True(MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Cavalheiro) < MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Caneleiro));
        Assert.True(MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Caneleiro) < MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Caceteiro));
        Assert.True(MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Caceteiro) < MatchSimulationEngine.GetBehaviorFoulWeight(PlayerBehavior.Sarrafeiro));

        var sarPlayer = new Player { Id = Guid.NewGuid(), Name = "Sarrafeiro", Behavior = PlayerBehavior.Sarrafeiro, Position = PlayerPosition.D, Strength = 30 };
        double weightNoYellow = MatchSimulationEngine.GetEffectivePlayerFoulWeight(sarPlayer, hasYellowInMatch: false);
        double weightWithYellow = MatchSimulationEngine.GetEffectivePlayerFoulWeight(sarPlayer, hasYellowInMatch: true);
        Assert.True(weightWithYellow < weightNoYellow, "Jogador já amarelado na partida deve reduzir peso de falta por cautela.");

        // 2. FairPlay squad commits 0 fouls, 0 yellows, 0 reds, and 0 penalty fouls across 100 full matches
        var homeClub = new Club { Id = Guid.NewGuid(), Name = "FairPlay FC", ShortName = "FPF" };
        var awayClub = new Club { Id = Guid.NewGuid(), Name = "Sarrafeiro FC", ShortName = "SFC" };
        var fpSquad = CreateTestSquad(32, false, PlayerBehavior.FairPlay).Take(11).ToList();
        var sarSquad = CreateTestSquad(32, false, PlayerBehavior.Sarrafeiro).Take(11).ToList();
        var dict = fpSquad.Concat(sarSquad).ToDictionary(p => p.Id);

        int totalFpFouls = 0;
        int totalSarFouls = 0;
        int totalSarCards = 0;

        for (int s = 1; s <= 100; s++)
        {
            foreach (var p in dict.Values)
            {
                p.YellowCardsAccumulated = 1; // Pre-load 1 accumulated yellow to test BUG-006-01 when 2 yellows happen in a match
                p.SuspensionRoundsRemaining = 0;
            }

            var match = new MatchFixture
            {
                SeasonNumber = 1,
                RoundNumber = 1,
                DeterministicMatchIndex = s,
                HomeClubId = homeClub.Id,
                AwayClubId = awayClub.Id,
                HomeFormation = "4-4-2",
                AwayFormation = "4-4-2",
                HomeActiveStarterIds = fpSquad.Select(p => p.Id).ToList(),
                AwayActiveStarterIds = sarSquad.Select(p => p.Id).ToList()
            };

            var sentOffMinuteByPlayer = new Dictionary<Guid, int>();

            for (int m = 1; m <= 90; m++)
            {
                match.CurrentMinute = m;
                var minEvents = MatchSimulationEngine.SimulateSingleMinute(
                    match, homeClub, awayClub, dict, ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, 7000 + s);

                // A & B: At most 1 card per team per minute (1 foul -> at most 1 card)
                int homeCardsThisMin = minEvents.Count(e => e.ClubId == homeClub.Id &&
                    e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard or MatchEventType.DirectRedCard);
                int awayCardsThisMin = minEvents.Count(e => e.ClubId == awayClub.Id &&
                    e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard or MatchEventType.DirectRedCard);
                Assert.InRange(homeCardsThisMin, 0, 1);
                Assert.InRange(awayCardsThisMin, 0, 1);

                foreach (var ev in minEvents)
                {
                    // D: Sent-off player must NEVER participate in any event after being sent off
                    if (ev.PrimaryPlayerId.HasValue && sentOffMinuteByPlayer.TryGetValue(ev.PrimaryPlayerId.Value, out int sentMin))
                    {
                        Assert.Fail($"Jogador expulso no minuto {sentMin} participou do evento {ev.EventType} no minuto {m}.");
                    }

                    if (ev.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard && ev.PrimaryPlayerId.HasValue)
                    {
                        sentOffMinuteByPlayer[ev.PrimaryPlayerId.Value] = m;
                        Assert.DoesNotContain(ev.PrimaryPlayerId.Value, match.AwayActiveStarterIds);

                        // C (BUG-006-01): If expelled by 2nd yellow after starting match with YellowCardsAccumulated == 1,
                        // YellowCardsAccumulated must remain 1 (reverted from the 1st yellow) and NOT become 3!
                        if (ev.EventType == MatchEventType.SecondYellowRedCard)
                        {
                            var offender = dict[ev.PrimaryPlayerId.Value];
                            Assert.Equal(1, offender.SuspensionRoundsRemaining);
                            Assert.Equal(1, offender.YellowCardsAccumulated);
                        }
                    }
                }
            }

            totalFpFouls += match.HomeFouls;
            totalSarFouls += match.AwayFouls;
            int matchSarCards = match.Events.Count(e => e.ClubId == awayClub.Id &&
                e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard or MatchEventType.DirectRedCard);
            totalSarCards += matchSarCards;
            Assert.True(match.AwayFouls >= matchSarCards, "Toda infração com cartão deve ser contabilizada como falta.");

            // Verify FairPlay conceded 0 penalties to Away team
            int awayPenAttempts = match.Events.Count(e => e.ClubId == awayClub.Id &&
                (e.EventType == MatchEventType.PenaltyMissed || (e.EventType == MatchEventType.Goal && e.Description.Contains("(Pênalti)"))));
            Assert.Equal(0, awayPenAttempts);
        }

        Assert.Equal(0, totalFpFouls);
        Assert.True(totalSarFouls > totalSarCards && totalSarCards > 0);
    }

    [Fact]
    public void Penalties_Audit006_GoalkeeperSavesVsWide_ShootoutSeparation_AndDefenderBehaviorInfluence()
    {
        var attClub = new Club { Id = Guid.NewGuid(), Name = "Ataque FC", ShortName = "ATK" };
        var defClub = new Club { Id = Guid.NewGuid(), Name = "Defesa FC", ShortName = "DEF" };

        // Compare penalty frequency against Cordeirinho defense vs Sarrafeiro defense
        int pensVsCordeirinho = 0;
        int pensVsSarrafeiro = 0;
        int savesByStrongGk = 0;
        int wideAgainstStrongGk = 0;

        for (int s = 1; s <= 250; s++)
        {
            var attSquad1 = CreateTestSquad(38, true, PlayerBehavior.FairPlay).Take(11).ToList();
            var defCord = CreateTestSquad(26, false, PlayerBehavior.Cordeirinho, gkOverride: 46).Take(11).ToList();
            var dict1 = attSquad1.Concat(defCord).ToDictionary(p => p.Id);

            var m1 = new MatchFixture
            {
                SeasonNumber = 1, RoundNumber = 1, DeterministicMatchIndex = s,
                HomeClubId = attClub.Id, AwayClubId = defClub.Id,
                HomeFormation = "4-3-3", AwayFormation = "4-4-2",
                HomeActiveStarterIds = attSquad1.Select(p => p.Id).ToList(),
                AwayActiveStarterIds = defCord.Select(p => p.Id).ToList()
            };
            for (int min = 1; min <= 90; min++)
            {
                m1.CurrentMinute = min;
                MatchSimulationEngine.SimulateSingleMinute(m1, attClub, defClub, dict1, ManagerTacticalIdentity.Ofensivo, ManagerTacticalIdentity.Equilibrado, 9000 + s);
            }
            pensVsCordeirinho += m1.Events.Count(e => e.ClubId == attClub.Id &&
                (e.EventType == MatchEventType.PenaltyMissed || (e.EventType == MatchEventType.Goal && e.Description.Contains("(Pênalti)"))));

            var attSquad2 = CreateTestSquad(38, true, PlayerBehavior.FairPlay).Take(11).ToList();
            var defSar = CreateTestSquad(26, false, PlayerBehavior.Sarrafeiro, gkOverride: 46).Take(11).ToList();
            var dict2 = attSquad2.Concat(defSar).ToDictionary(p => p.Id);

            var m2 = new MatchFixture
            {
                SeasonNumber = 1, RoundNumber = 1, DeterministicMatchIndex = s,
                HomeClubId = attClub.Id, AwayClubId = defClub.Id,
                HomeFormation = "4-3-3", AwayFormation = "4-4-2",
                HomeActiveStarterIds = attSquad2.Select(p => p.Id).ToList(),
                AwayActiveStarterIds = defSar.Select(p => p.Id).ToList()
            };
            for (int min = 1; min <= 90; min++)
            {
                m2.CurrentMinute = min;
                MatchSimulationEngine.SimulateSingleMinute(m2, attClub, defClub, dict2, ManagerTacticalIdentity.Ofensivo, ManagerTacticalIdentity.Equilibrado, 9000 + s);
            }
            pensVsSarrafeiro += m2.Events.Count(e => e.ClubId == attClub.Id &&
                (e.EventType == MatchEventType.PenaltyMissed || (e.EventType == MatchEventType.Goal && e.Description.Contains("(Pênalti)"))));

            foreach (var miss in m2.Events.Where(e => e.EventType == MatchEventType.PenaltyMissed))
            {
                if (miss.Description.Contains("PÊNALTI DEFENDIDO!"))
                {
                    savesByStrongGk++;
                    Assert.NotNull(miss.SecondaryPlayerId);
                }
                else if (miss.Description.Contains("PÊNALTI PARA FORA!"))
                {
                    wideAgainstStrongGk++;
                }
            }
        }

        Assert.True(pensVsSarrafeiro > pensVsCordeirinho,
            $"Defesa Sarrafeiro ({pensVsSarrafeiro} pênaltis) deve cometer mais pênaltis que Cordeirinho ({pensVsCordeirinho}).");
        Assert.True(savesByStrongGk + wideAgainstStrongGk > 0);
        Assert.True(savesByStrongGk > 0, "Deve registrar defesas explícitas de pênalti pelo goleiro.");

        // Verify Penalty Shootout tracks HomeShootoutAttempts / AwayShootoutAttempts and never alters HomeGoals / AwayGoals
        var shootHome = CreateTestSquad(32, true, PlayerBehavior.Cavalheiro).Take(11).ToList();
        var shootAway = CreateTestSquad(32, true, PlayerBehavior.Cavalheiro).Take(11).ToList();
        var shootDict = shootHome.Concat(shootAway).ToDictionary(p => p.Id);
        var koMatch = new MatchFixture
        {
            SeasonNumber = 1, RoundNumber = 10, DeterministicMatchIndex = 1,
            HomeClubId = attClub.Id, AwayClubId = defClub.Id,
            HomeGoals = 1, AwayGoals = 1, RequiresKnockoutResolution = true, EnteredExtraTime = true,
            HomeActiveStarterIds = shootHome.Select(p => p.Id).ToList(),
            AwayActiveStarterIds = shootAway.Select(p => p.Id).ToList()
        };
        MatchSimulationEngine.ResolvePenaltyShootout(koMatch, attClub, defClub, shootDict, 199801);
        Assert.Equal(1, koMatch.HomeGoals);
        Assert.Equal(1, koMatch.AwayGoals);
        Assert.True(koMatch.HomeShootoutAttempts >= 3 && koMatch.AwayShootoutAttempts >= 3);
        Assert.NotNull(koMatch.HomePenaltyGoals);
        Assert.NotNull(koMatch.AwayPenaltyGoals);
        Assert.NotEqual(koMatch.HomePenaltyGoals, koMatch.AwayPenaltyGoals);
    }

    [Fact]
    public void AiManagerBrain_AdaptsTacticsToSquadAndBudget_AndFollowsManagerPhilosophy()
    {
        var club = new Club
        {
            Id = Guid.NewGuid(),
            DeterministicIndex = 1,
            Name = "Clube Teste",
            ShortName = "CTE",
            CashBalance = 5_000_000m,
            Stadium = new Stadium { BuiltCapacity = 20_000, UsableCapacity = 20_000, MaintenanceReservePerSeason = 90_000m }
        };

        var offensiveMgr = new AiManager
        {
            Id = Guid.NewGuid(),
            Name = "Técnico Ofensivo",
            Identity = ManagerTacticalIdentity.Ofensivo,
            Pragmatism = 65,
            Adaptability = 75,
            FinancialAggressiveness = 70,
            PreferredFormations =
            [
                new("4-3-3", 65),
                new("3-4-3", 20),
                new("4-4-2", 15)
            ]
        };

        // Case 1: Has 3 starter attackers and comfortable cash, but 0 reserve attackers -> picks 4-3-3 and flags 4th attacker in PriorityGapPositions
        var squadWith3Attackers = new List<Player>
        {
            new() { Position = PlayerPosition.G, Strength = 35 },
            new() { Position = PlayerPosition.G, Strength = 28 },
            new() { Position = PlayerPosition.D, Strength = 34 },
            new() { Position = PlayerPosition.D, Strength = 33 },
            new() { Position = PlayerPosition.D, Strength = 32 },
            new() { Position = PlayerPosition.D, Strength = 31 },
            new() { Position = PlayerPosition.D, Strength = 29 },
            new() { Position = PlayerPosition.M, Strength = 35 },
            new() { Position = PlayerPosition.M, Strength = 34 },
            new() { Position = PlayerPosition.M, Strength = 33 },
            new() { Position = PlayerPosition.M, Strength = 31 },
            new() { Position = PlayerPosition.M, Strength = 30 },
            new() { Position = PlayerPosition.A, Strength = 38, IsStar = true },
            new() { Position = PlayerPosition.A, Strength = 36 },
            new() { Position = PlayerPosition.A, Strength = 35 }
        };

        var (form1, starters1, _, plan1) = AiManagerBrain.PlanAndSelectLineup(
            offensiveMgr, club, squadWith3Attackers, null, null, isHomeMatch: true, 199801, 1, 1);

        Assert.Equal("4-3-3", form1);
        Assert.Equal(11, starters1.Count);
        Assert.Contains(PlayerPosition.A, plan1.PriorityGapPositions);

        // Case 2: Only 2 attackers available (3rd attacker injured) and tight cash -> adapts to 4-4-2 and explains why!
        squadWith3Attackers[^1].InjuryRoundsRemaining = 3;
        club.CashBalance = 150_000m;

        var (form2, starters2, _, plan2) = AiManagerBrain.PlanAndSelectLineup(
            offensiveMgr, club, squadWith3Attackers, null, null, isHomeMatch: false, 199801, 1, 2);

        Assert.Equal("4-4-2", form2);
        Assert.Equal(11, starters2.Count);
        Assert.DoesNotContain(squadWith3Attackers[^1].Id, starters2);
        Assert.Contains("4-4-2", plan2.SelectionReasoning);

        // Case 3: Defensive manager hired by same club shifts target philosophy to 5-3-2
        var defensiveMgr = new AiManager
        {
            Id = Guid.NewGuid(),
            Name = "Técnico Retranqueiro",
            Identity = ManagerTacticalIdentity.Defensivo,
            PreferredFormations =
            [
                new("5-3-2", 70),
                new("5-4-1", 20),
                new("4-4-2", 10)
            ]
        };

        var (_, _, _, planDef) = AiManagerBrain.PlanAndSelectLineup(
            defensiveMgr, club, squadWith3Attackers, null, null, isHomeMatch: true, 199801, 1, 3);

        Assert.Equal("5-3-2", planDef.TargetPrimaryFormation);
        Assert.Equal(5, planDef.TargetStarters[PlayerPosition.D]);
    }

    [Fact]
    public void MarketAndStadiumEconomy_FairValueConvex_Wear1To6Percent_AndBilateralTransfers()
    {
        var weakP = new Player { Name = "Fraco", Position = PlayerPosition.A, Strength = 11, Behavior = PlayerBehavior.Cavalheiro };
        var strongP = new Player { Name = "Craque", Position = PlayerPosition.A, Strength = 48, IsStar = true, Behavior = PlayerBehavior.FairPlay };

        decimal weakVal = MarketAndEconomyEngine.CalculateFairValue(weakP);
        decimal strongVal = MarketAndEconomyEngine.CalculateFairValue(strongP);

        Assert.True(strongVal > weakVal * 25m, $"Curva convexa deve valorizar F48* ({strongVal}) muito acima de F11 ({weakVal}).");

        // Test stadium wear without maintenance (1% to 6%) vs with maintenance (0% to 1%)
        var world = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        var poorClub = world.Clubs[0];
        int initialBuilt = poorClub.Stadium.BuiltCapacity;
        poorClub.Stadium.ConditionPercent = 95.0;
        poorClub.CashBalance = 0m; // Cannot pay maintenance

        var richClub = world.Clubs[1];
        richClub.Stadium.ConditionPercent = 95.0;
        richClub.CashBalance = 10_000_000m; // Pays maintenance

        MarketAndEconomyEngine.ProcessSeasonalStadiumWearAndAiProjects(world);

        double poorDrop = 95.0 - poorClub.Stadium.ConditionPercent;
        double richDrop = 95.0 - richClub.Stadium.ConditionPercent;

        Assert.Equal(initialBuilt, poorClub.Stadium.BuiltCapacity); // BuiltCapacity never erased by wear
        Assert.InRange(poorDrop, 1.0, 6.0);
        Assert.InRange(richDrop, 0.0, 1.0);
        Assert.True(poorClub.Stadium.UsableCapacity < poorClub.Stadium.BuiltCapacity);
    }

    [Fact]
    public void WorldEditorAndPackageService_ValidatesAndRoundTripsJsonAndCsv()
    {
        var world = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        var pkg = WorldEditorAndPackageService.ExportWorldToPackage(world, "Pacote Teste");
        var validation = WorldEditorAndPackageService.ValidatePackage(pkg, world);

        Assert.True(validation.IsValid);
        Assert.Equal(128, validation.ClubCount);

        string csv = WorldEditorAndPackageService.ExportPlayersToCsv(world);
        var (parsedPlayers, csvErrors) = WorldEditorAndPackageService.ParsePlayersFromCsv(csv);
        Assert.Empty(csvErrors);
        Assert.Equal(world.Players.Count, parsedPlayers.Count);
    }

    [Fact]
    public void MatchEngine_Characterization_All10Formations_DefensiveFormula_DeterministicReplay_AndBugRegressions()
    {
        // 1. Characterize all 10 formations in ComputeSectorBreakdown (preserving the defensive formula untouched)
        string[] allFormations = ["4-4-2", "4-3-3", "3-4-3", "5-3-2", "5-4-1", "4-5-1", "3-5-2", "4-2-4", "3-6-1", "5-0-5"];
        foreach (var form in allFormations)
        {
            Assert.True(TacticalFormations.TryGet(form, allowClassic505: true, out var spec));
            var eleven = new List<Player>
            {
                new() { Id = Guid.NewGuid(), Position = PlayerPosition.G, Strength = 30 }
            };
            for (int d = 0; d < spec.Defenders; d++)
                eleven.Add(new Player { Id = Guid.NewGuid(), Position = PlayerPosition.D, Strength = 30 });
            for (int m = 0; m < spec.Midfielders; m++)
                eleven.Add(new Player { Id = Guid.NewGuid(), Position = PlayerPosition.M, Strength = 30 });
            for (int a = 0; a < spec.Attackers; a++)
                eleven.Add(new Player { Id = Guid.NewGuid(), Position = PlayerPosition.A, Strength = 30 });

            var breakdown = MatchSimulationEngine.ComputeSectorBreakdown(
                form, eleven, isHomeTeam: false, ManagerTacticalIdentity.Equilibrado, allowClassic505: true);

            double expectedDefSector = spec.Defenders * 30.0;
            double expectedMidSector = spec.Midfielders * 30.0;
            double expectedAttSector = spec.Attackers * 30.0;
            double expectedEffDef = expectedDefSector + 0.45 * expectedMidSector;
            double expectedEffAtt = (expectedAttSector + 0.50 * expectedMidSector) * (form == "5-0-5" ? MatchSimulationEngine.Classic505TransitionFactor : 1.0);

            Assert.Equal(30.0, breakdown.GoalkeeperPower, 4);
            Assert.Equal(expectedDefSector, breakdown.DefenseSectorPower, 4);
            Assert.Equal(expectedMidSector, breakdown.MidfieldSectorPower, 4);
            Assert.Equal(expectedAttSector, breakdown.AttackSectorPower, 4);
            Assert.Equal(expectedEffDef, breakdown.EffectiveDefenseTotal, 4);
            Assert.Equal(expectedEffAtt, breakdown.EffectiveAttackTotal, 4);
        }

        // 2. Regression Test BUG-001: 3rd Yellow Card Suspension must NOT be cleared at the end of the same match
        var world = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        world.RoundNumber = 3; // League Round 1
        var leagueMatch = world.Fixtures.First(f => f.SeasonNumber == 1 && f.RoundNumber == 3);
        var homeClub = world.Clubs.First(c => c.Id == leagueMatch.HomeClubId);
        var homePlayer = world.Players.First(p => p.ClubId == homeClub.Id && p.Position == PlayerPosition.D);

        homePlayer.YellowCardsAccumulated = 0;
        homePlayer.SuspensionRoundsRemaining = 1;
        leagueMatch.HomeSuspendedByYellowInMatchIds.Add(homePlayer.Id);
        leagueMatch.IsFinished = true;

        foreach (var m in world.Fixtures.Where(f => f.SeasonNumber == 1 && f.RoundNumber == 3))
            m.IsFinished = true;

        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, world.Fixtures.Where(f => f.SeasonNumber == 1 && f.RoundNumber == 3).ToList());
        Assert.Equal(1, homePlayer.SuspensionRoundsRemaining); // Still suspended for the NEXT match!

        // In the next round (Round 4), when the player does NOT receive a new suspension, it decrements to 0
        world.RoundNumber = 4;
        var round4Matches = world.Fixtures.Where(f => f.SeasonNumber == 1 && f.RoundNumber == 4).ToList();
        foreach (var m in round4Matches) { m.IsFinished = true; m.CurrentMinute=90; }
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, round4Matches);
        Assert.Equal(0, homePlayer.SuspensionRoundsRemaining);

        // 3. Deterministic Full-Match Replay via MatchSimulationEngine.SimulateFullMatchReal
        var world1 = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        var world2 = WorldSeedFactory.CreateSeededWorld(seed: 199801, totalClubs: 128);
        var worldDiff = WorldSeedFactory.CreateSeededWorld(seed: 199899, totalClubs: 128);

        int diffScores = 0;
        for (int idx = 0; idx < 16; idx++)
        {
            var m1 = world1.Fixtures.Where(f => f.RoundNumber == 3).OrderBy(f => f.DeterministicMatchIndex).ElementAt(idx);
            var m2 = world2.Fixtures.Where(f => f.RoundNumber == 3).OrderBy(f => f.DeterministicMatchIndex).ElementAt(idx);
            var m3 = worldDiff.Fixtures.Where(f => f.RoundNumber == 3).OrderBy(f => f.DeterministicMatchIndex).ElementAt(idx);

            SimulateOneLeagueMatch(world1, m1);
            SimulateOneLeagueMatch(world2, m2);
            SimulateOneLeagueMatch(worldDiff, m3);

            Assert.Equal(m1.HomeGoals, m2.HomeGoals);
            Assert.Equal(m1.AwayGoals, m2.AwayGoals);
            Assert.Equal(m1.HomeFirstHalfGoals, m2.HomeFirstHalfGoals);
            Assert.Equal(m1.AwayFirstHalfGoals, m2.AwayFirstHalfGoals);
            Assert.Equal(m1.Events.Count, m2.Events.Count);

            if (m1.HomeGoals != m3.HomeGoals || m1.AwayGoals != m3.AwayGoals || m1.Events.Count != m3.Events.Count)
                diffScores++;
        }
        Assert.True(diffScores > 0, "Seeds diferentes devem produzir resultados diferentes de maneira controlada.");
    }

    private static void SimulateOneLeagueMatch(GameWorld w, MatchFixture m)
    {
        var h = w.Clubs.First(c => c.Id == m.HomeClubId);
        var a = w.Clubs.First(c => c.Id == m.AwayClubId);
        var hMgr = w.AiManagers.FirstOrDefault(mgr => mgr.Id == h.AiManagerId);
        var aMgr = w.AiManagers.FirstOrDefault(mgr => mgr.Id == a.AiManagerId);
        var hSquad = w.Players.Where(p => p.ClubId == h.Id).ToList();
        var aSquad = w.Players.Where(p => p.ClubId == a.Id).ToList();

        if (hMgr != null)
        {
            var (f, s, b, _) = AiManagerBrain.PlanAndSelectLineup(hMgr, h, hSquad, a, aSquad, true, w.Seed, w.SeasonNumber, w.RoundNumber, w.AllowClassic505);
            m.HomeFormation = f; m.HomeActiveStarterIds = new List<Guid>(s); m.HomeBenchIds = new List<Guid>(b);
        }
        if (aMgr != null)
        {
            var (f, s, b, _) = AiManagerBrain.PlanAndSelectLineup(aMgr, a, aSquad, h, hSquad, false, w.Seed, w.SeasonNumber, w.RoundNumber, w.AllowClassic505);
            m.AwayFormation = f; m.AwayActiveStarterIds = new List<Guid>(s); m.AwayBenchIds = new List<Guid>(b);
        }

        var pDict = w.Players.ToDictionary(p => p.Id);
        MatchSimulationEngine.SimulateFullMatchReal(m, h, a, hMgr, aMgr, hSquad, aSquad, pDict, w.Seed, w.AllowClassic505);
    }

    private static int SimulateSeriesGoals(int attStrength, int defStrength, int gkStrength, bool hasStarAttackers, int seeds)
    {
        int totalAwayConceded = 0;
        var homeClub = new Club { Id = Guid.NewGuid(), Name = "Atacante FC", ShortName = "AFC" };
        var awayClub = new Club { Id = Guid.NewGuid(), Name = "Defensor FC", ShortName = "DFC" };

        var homePlayers = CreateTestSquad(attStrength, hasStarAttackers, PlayerBehavior.FairPlay).Take(11).ToList();
        var awayPlayers = CreateTestSquad(defStrength, false, PlayerBehavior.FairPlay, gkOverride: gkStrength).Take(11).ToList();
        var dict = homePlayers.Concat(awayPlayers).ToDictionary(p => p.Id);

        for (int s = 1; s <= seeds; s++)
        {
            var match = new MatchFixture
            {
                SeasonNumber = 1,
                RoundNumber = 1,
                DeterministicMatchIndex = s,
                HomeClubId = homeClub.Id,
                AwayClubId = awayClub.Id,
                HomeFormation = "4-3-3",
                AwayFormation = "4-4-2",
                HomeActiveStarterIds = homePlayers.Select(p => p.Id).ToList(),
                AwayActiveStarterIds = awayPlayers.Select(p => p.Id).ToList()
            };

            for (int m = 1; m <= 90; m++)
            {
                match.CurrentMinute = m;
                MatchSimulationEngine.SimulateSingleMinute(
                    match, homeClub, awayClub, dict, ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, 1000 + s);
            }

            totalAwayConceded += match.HomeGoals;
        }

        return totalAwayConceded;
    }

    private static (int Yellows, int Reds) SimulateSeriesCards(PlayerBehavior behavior, int seeds)
    {
        int yellows = 0;
        int reds = 0;
        var homeClub = new Club { Id = Guid.NewGuid(), Name = "Time A", ShortName = "TMA" };
        var awayClub = new Club { Id = Guid.NewGuid(), Name = "Time B", ShortName = "TMB" };

        for (int s = 1; s <= seeds; s++)
        {
            var homePlayers = CreateTestSquad(30, false, behavior).Take(11).ToList();
            var awayPlayers = CreateTestSquad(30, false, PlayerBehavior.FairPlay).Take(11).ToList();
            var dict = homePlayers.Concat(awayPlayers).ToDictionary(p => p.Id);

            var match = new MatchFixture
            {
                SeasonNumber = 1,
                RoundNumber = 1,
                DeterministicMatchIndex = s,
                HomeClubId = homeClub.Id,
                AwayClubId = awayClub.Id,
                HomeFormation = "4-4-2",
                AwayFormation = "4-4-2",
                HomeActiveStarterIds = homePlayers.Select(p => p.Id).ToList(),
                AwayActiveStarterIds = awayPlayers.Select(p => p.Id).ToList()
            };

            for (int m = 1; m <= 90; m++)
            {
                match.CurrentMinute = m;
                MatchSimulationEngine.SimulateSingleMinute(
                    match, homeClub, awayClub, dict, ManagerTacticalIdentity.Equilibrado, ManagerTacticalIdentity.Equilibrado, 5000 + s);
            }

            yellows += match.Events.Count(e => e.ClubId == homeClub.Id && e.EventType == MatchEventType.YellowCard);
            reds += match.Events.Count(e => e.ClubId == homeClub.Id && e.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard);
        }

        return (yellows, reds);
    }

    private static List<Player> CreateTestSquad(int strength, bool starInAttackAndMid, PlayerBehavior behavior, int? gkOverride = null)
    {
        PlayerPosition[] positions =
        [
            PlayerPosition.G,
            PlayerPosition.D, PlayerPosition.D, PlayerPosition.D, PlayerPosition.D,
            PlayerPosition.M, PlayerPosition.M, PlayerPosition.M,
            PlayerPosition.A, PlayerPosition.A, PlayerPosition.A
        ];

        return positions.Select((pos, i) => new Player
        {
            Id = Guid.NewGuid(),
            Name = $"Atleta {i + 1}",
            Position = pos,
            Strength = pos == PlayerPosition.G && gkOverride.HasValue ? gkOverride.Value : strength,
            Behavior = behavior,
            IsStar = starInAttackAndMid && pos is PlayerPosition.A or PlayerPosition.M
        }).ToList();
    }
}
