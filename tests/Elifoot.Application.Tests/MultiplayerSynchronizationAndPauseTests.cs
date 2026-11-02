using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class MultiplayerSynchronizationAndPauseTests
{
    [Fact]
    public async Task HumanSynchronization_0To6Counter_Idempotency_UnemployedIncluded_AndNoRaceConditions()
    {
        var repo = new InMemoryWorldRepository();
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

        var world = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala 6 Humanos", MaxHumans: 8, TotalClubs: 128, Seed: 199801));

        // Advance to Round 3 (League Round 1 where all 32 division clubs play) so all 5 club coaches have active league fixtures
        var divisionClubs = world.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital).Take(5).ToList();
        var coaches = new List<HumanCoach>();

        // 5 employed humans + 1 unemployed human = 6 humans total (122 AI clubs do NOT increment denominator!)
        for (int i = 0; i < 5; i++)
        {
            var (_, _, coach, _) = await orchestrator.JoinWorldAndAssignClubAsync(
                world.Id, Guid.NewGuid(), $"Humano {i + 1}", $"dev-{i + 1}", divisionClubs[i].Id);
            coaches.Add(coach!);
        }

        var (_, _, unemployedCoach, updatedWorld) = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, Guid.NewGuid(), "Humano Desempregado 6", "dev-6", preferredClubId: null, joinUnemployed: true);
        coaches.Add(unemployedCoach!);

        // 1. Room with 6 humans starts at 0/6 (AI clubs do not affect denominator)
        var initialCounter = MultiplayerRoundOrchestrator.BuildReadyCounterDto(updatedWorld!);
        Assert.Equal(0, initialCounter.ReadyCount);
        Assert.Equal(6, initialCounter.TotalHumans);
        Assert.Equal("TÁTICAS ENVIADAS: 0/6", initialCounter.CounterLabel);

        // 2. First human submits tactics -> 1/6 with name identified
        var club0 = updatedWorld!.Clubs.First(c => c.Id == coaches[0].CurrentClubId);
        var (ok1, err1, counter1, started1) = await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id,
            coaches[0].Id,
            club0.PreferredFormation,
            club0.PreferredStarterIds,
            club0.PreferredBenchIds,
            "idem-coach-0",
            updatedWorld.RoundVersion));

        Assert.True(ok1, err1);
        Assert.False(started1);
        Assert.Equal(1, counter1!.ReadyCount);
        Assert.Equal(6, counter1.TotalHumans);
        Assert.True(counter1.Participants.Single(p => p.CoachId == coaches[0].Id).IsReady);

        // 7. Same user submitting a second time (same or new key) does NOT increase counter beyond 1/6
        var (_, _, counter1Dup, _) = await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id,
            coaches[0].Id,
            club0.PreferredFormation,
            club0.PreferredStarterIds,
            club0.PreferredBenchIds,
            "idem-coach-0-dup",
            counter1.RoundVersion));
        Assert.Equal(1, counter1Dup!.ReadyCount);

        // Optional withdraw ready ("Retirar pronto") before 6/6 decrements back to 0/6, then re-submit to 1/6
        var (wOk, _, counterWithdrawn) = await orchestrator.WithdrawReadyAsync(
            new WithdrawReadyRequest(world.Id, coaches[0].Id, "withdraw-0", counter1Dup.RoundVersion));
        Assert.True(wOk);
        Assert.Equal(0, counterWithdrawn!.ReadyCount);

        await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id, coaches[0].Id, club0.PreferredFormation, club0.PreferredStarterIds, club0.PreferredBenchIds, "idem-coach-0-resubmit", counterWithdrawn.RoundVersion));

        // 3. Next 4 employed humans submit concurrently -> 5/6; match still does NOT start
        var concurrentTasks = Enumerable.Range(1, 4).Select(idx =>
        {
            var c = updatedWorld.Clubs.First(x => x.Id == coaches[idx].CurrentClubId);
            return orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
                world.Id, coaches[idx].Id, c.PreferredFormation, c.PreferredStarterIds, c.PreferredBenchIds, $"idem-{idx}", 1));
        });
        var results = await Task.WhenAll(concurrentTasks);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.False(r.RoundStarted));

        var snapAt5 = await repo.GetWorldAsync(world.Id);
        var counterAt5 = MultiplayerRoundOrchestrator.BuildReadyCounterDto(snapAt5!);
        Assert.Equal(5, counterAt5.ReadyCount);
        Assert.Equal(6, counterAt5.TotalHumans);
        Assert.Equal(RoundState.PREPARING, snapAt5!.RoundState);

        // 4 & 6. Last human (Unemployed) submits PRONTO -> 6/6; round starts automatically and exactly once!
        var (ok6, err6, counter6, started6) = await orchestrator.SubmitUnemployedReadyAsync(
            new UnemployedReadyRequest(world.Id, unemployedCoach!.Id, "idem-unemployed-6", snapAt5.RoundVersion));

        Assert.True(ok6, err6);
        Assert.True(started6);
        Assert.Equal(6, counter6!.ReadyCount);
        Assert.Equal(6, counter6.TotalHumans);
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, counter6.RoundState);
    }

    [Fact]
    public async Task GlobalPause_8Humans_Minute37Freeze_Quotas5And3_ExtraTime6And4_AndRoomIsolation()
    {
        var repo = new InMemoryWorldRepository();
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

        var worldA = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala A (8 Humanos)", MaxHumans: 8, TotalClubs: 128, Seed: 199801));
        var worldB = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala B Independente", MaxHumans: 2, TotalClubs: 128, Seed: 199802));

        // Move World A to Round 3 (League Round 1 where all 1st division clubs play 4 simultaneous matches)
        worldA.RoundNumber = 3;
        await repo.SaveWorldAsync(worldA);

        var round3Fixtures = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(worldA);
        var fourMatches = round3Fixtures.Take(4).ToList();
        var clubIdsFor8Humans = fourMatches.SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).ToList();

        var coachesA = new List<HumanCoach>();
        for (int i = 0; i < 8; i++)
        {
            var (_, _, c, _) = await orchestrator.JoinWorldAndAssignClubAsync(
                worldA.Id, Guid.NewGuid(), $"TreinadorA{i + 1}", $"devA-{i + 1}", clubIdsFor8Humans[i]);
            coachesA.Add(c!);
        }

        // Also join 1 human in World B and start World B's round
        var (_, _, coachB1, _) = await orchestrator.JoinWorldAndAssignClubAsync(worldB.Id, Guid.NewGuid(), "TreinadorB1", "devB-1");
        await orchestrator.SubmitUnemployedReadyAsync(new UnemployedReadyRequest(worldB.Id, coachB1!.Id, "b-ready", 1));

        // All 8 humans in World A submit tactics -> 8/8 starts round
        foreach (var h in coachesA)
        {
            var club = worldA.Clubs.First(c => c.Id == h.CurrentClubId);
            await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
                worldA.Id, h.Id, club.PreferredFormation, club.PreferredStarterIds, club.PreferredBenchIds, $"ready-{h.Id}", 1));
        }

        // Advance World A to minute 37
        LiveRoundSnapshotDto? snap37 = null;
        for (int m = 1; m <= 37; m++)
        {
            snap37 = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id);
        }

        Assert.Equal(37, snap37!.CurrentMinute);
        Assert.All(snap37.Matches, match => Assert.Equal(37, match.Minute));

        // 1 & 2. At minute 37, Coach A (coachesA[0]) requests pause -> ALL matches stop at 37'
        var coachA = coachesA[0];
        var coachOpponentB = coachesA[1]; // Playing against Coach A in the same match!
        var coachObserverC = coachesA[2]; // In another match

        var (pauseOk, pauseErr, pauseWin) = await orchestrator.RequestGlobalPauseAsync(
            new RequestPauseCommand(worldA.Id, coachA.Id, "pause-37-A"));

        Assert.True(pauseOk, pauseErr);
        Assert.Equal(37, pauseWin!.Minute);

        // Attempting to advance clock while GLOBAL_PAUSED does NOT advance minute 37!
        var frozenSnap = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id);
        Assert.Equal(37, frozenSnap!.CurrentMinute);
        Assert.Equal(RoundState.GLOBAL_PAUSED, frozenSnap.RoundState);

        // 12. Room B is completely unaffected by Room A's pause!
        var snapRoomB = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldB.Id);
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, snapRoomB!.RoundState);
        Assert.Equal(1, snapRoomB.CurrentMinute);

        // 3. Opening/observing spends no pause: Coach A has PARADAS 0/3; Coach B (opponent) and Coach C (observer) have 0/3
        var match0 = frozenSnap.Matches.First(m => m.HomeClubId == coachA.CurrentClubId);
        Assert.Equal(0, match0.HomePauseWindowsUsed);
        Assert.Equal(0, match0.AwayPauseWindowsUsed);

        // 5. Coach A makes TWO substitutions in the same pause window -> SUBS 2/5, still only 1/3 pause window used!
        var twoSubsForA = new List<InMatchSubstitutionDto>
        {
            new(match0.HomeStarters[1].PlayerId, match0.HomeBench[1].PlayerId),
            new(match0.HomeStarters[2].PlayerId, match0.HomeBench[2].PlayerId)
        };
        var (adjAOk, adjAErr, snapAfterA) = await orchestrator.SubmitInMatchAdjustmentAsync(
            new SubmitInMatchAdjustmentCommand(worldA.Id, coachA.Id, match0.MatchId, "4-3-3", twoSubsForA, "adj-A-37"));

        Assert.True(adjAOk, adjAErr);
        var updatedMatch0 = snapAfterA!.Matches.First(m => m.MatchId == match0.MatchId);
        Assert.Equal(2, updatedMatch0.HomeSubCount);
        Assert.Equal(5, updatedMatch0.HomeMaxSubs);
        Assert.Equal(1, updatedMatch0.HomePauseWindowsUsed);
        Assert.Equal(3, updatedMatch0.HomeMaxPauseWindows);

        // 4 & 11. Coach B (opponent in same match) decides to edit lineup during Coach A's window -> consumes 1 of B's own windows (1/3)
        var oneSubForB = new List<InMatchSubstitutionDto>
        {
            new(updatedMatch0.AwayStarters[1].PlayerId, updatedMatch0.AwayBench[1].PlayerId)
        };
        var (adjBOk, adjBErr, snapAfterB) = await orchestrator.SubmitInMatchAdjustmentAsync(
            new SubmitInMatchAdjustmentCommand(worldA.Id, coachOpponentB.Id, match0.MatchId, "3-5-2", oneSubForB, "adj-B-37"));

        Assert.True(adjBOk, adjBErr);
        updatedMatch0 = snapAfterB!.Matches.First(m => m.MatchId == match0.MatchId);
        Assert.Equal(1, updatedMatch0.AwaySubCount);
        Assert.Equal(1, updatedMatch0.AwayPauseWindowsUsed);

        // 6. Simultaneous pause request while already in GLOBAL_PAUSED returns the SAME shared PauseWindowId
        var (simOk, _, simWin) = await orchestrator.RequestGlobalPauseAsync(
            new RequestPauseCommand(worldA.Id, coachOpponentB.Id, "sim-pause-B"));
        Assert.True(simOk);
        Assert.Equal(pauseWin.PauseWindowId, simWin!.PauseWindowId);

        // 7. All 8 coaches confirm CONTINUAR -> Coach C (who only observed) stays at 0/3 pause windows used, and clock resumes to minute 38!
        foreach (var h in coachesA)
        {
            await orchestrator.ConfirmContinueMatchAsync(new ConfirmContinueMatchCommand(worldA.Id, h.Id, $"cont-{h.Id}"));
        }

        var snap38 = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id);
        Assert.Equal(38, snap38!.CurrentMinute);
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, snap38.RoundState);
        var matchOfC = snap38.Matches.First(m => m.HomeClubId == coachObserverC.CurrentClubId || m.AwayClubId == coachObserverC.CurrentClubId);
        Assert.Equal(0, matchOfC.HomePauseWindowsUsed);

        // 9. Advance to Halftime (45') -> HALFTIME_PAUSED is FREE (0 pause windows consumed, but subs count toward 5)
        LiveRoundSnapshotDto? snap45 = null;
        while ((snap45 = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id))!.CurrentMinute < 45) { }
        Assert.Equal(45, snap45.CurrentMinute);
        Assert.Equal(RoundState.HALFTIME_PAUSED, snap45.RoundState);

        var match0At45 = snap45.Matches.First(m => m.MatchId == match0.MatchId);
        var htSubA = new List<InMatchSubstitutionDto>
        {
            new(match0At45.HomeStarters[3].PlayerId, match0At45.HomeBench[0].PlayerId)
        };
        var (htOk, htErr, snapAfterHt) = await orchestrator.SubmitInMatchAdjustmentAsync(
            new SubmitInMatchAdjustmentCommand(worldA.Id, coachA.Id, match0.MatchId, "4-4-2", htSubA, "ht-sub-A"));
        Assert.True(htOk, htErr);
        match0At45 = snapAfterHt!.Matches.First(m => m.MatchId == match0.MatchId);
        Assert.Equal(3, match0At45.HomeSubCount); // 2 + 1 = 3/5
        Assert.Equal(1, match0At45.HomePauseWindowsUsed); // Still 1/3! Halftime was free!

        // Resume second half
        foreach (var h in coachesA)
        {
            await orchestrator.ConfirmContinueMatchAsync(new ConfirmContinueMatchCommand(worldA.Id, h.Id, $"ht-cont-{h.Id}"));
        }

        // 8. Coach A uses 2nd pause at 50' and 3rd pause at 60'; 4th pause in normal time is REJECTED!
        await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id); // 46'
        var (p2Ok, _, _) = await orchestrator.RequestGlobalPauseAsync(new RequestPauseCommand(worldA.Id, coachA.Id, "p2"));
        Assert.True(p2Ok);
        Assert.True((await orchestrator.SubmitInMatchAdjustmentAsync(new(worldA.Id,coachA.Id,match0.MatchId,"4-3-3",new(),"p2-edit"))).Success);
        await orchestrator.ForceExpirePauseWindowIfTimedOutAsync(worldA.Id, ignoreClockForTest: true);

        await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id); // 47'
        var (p3Ok, _, _) = await orchestrator.RequestGlobalPauseAsync(new RequestPauseCommand(worldA.Id, coachA.Id, "p3"));
        Assert.True(p3Ok);
        Assert.True((await orchestrator.SubmitInMatchAdjustmentAsync(new(worldA.Id,coachA.Id,match0.MatchId,"4-4-2",new(),"p3-edit"))).Success);
        await orchestrator.ForceExpirePauseWindowIfTimedOutAsync(worldA.Id, ignoreClockForTest: true);

        await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id); // 48'
        var (p4Ok, p4Err, _) = await orchestrator.RequestGlobalPauseAsync(new RequestPauseCommand(worldA.Id, coachA.Id, "p4"));
        Assert.False(p4Ok);
        Assert.Contains("3/3", p4Err!);
    }

    [Fact]
    public async Task CupExtraTime_Grants6SubstitutionsAnd4PauseWindows_WithFreeIntervalsAt90And105()
    {
        var repo = new InMemoryWorldRepository();
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

        var world = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala Taça Prorrogação", MaxHumans: 2, TotalClubs: 128, Seed: 199801));

        // Round 5 is CupRoundOf64 (knockout with extra time)
        world.RoundNumber = 5;
        CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, 5);
        var cupFixture = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world).First();

        var (_, _, coachHome, _) = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, Guid.NewGuid(), "Técnico Copa", "dev-cup", cupFixture.HomeClubId);

        var homeClub = world.Clubs.First(c => c.Id == cupFixture.HomeClubId);
        await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id, coachHome!.Id, homeClub.PreferredFormation, homeClub.PreferredStarterIds, homeClub.PreferredBenchIds, "cup-ready", 1));

        // Simulate to minute 89, force a tie (1x1) at minute 89, and tick to 90'
        for (int m = 1; m <= 89; m++)
        {
            await TickWithAutomaticPenaltyChoiceAsync(orchestrator, world.Id);
            if (m == 45)
            {
                await orchestrator.ConfirmContinueMatchAsync(new ConfirmContinueMatchCommand(world.Id, coachHome.Id, "cup-ht"));
            }
        }

        cupFixture.HomeGoals = 1;
        cupFixture.AwayGoals = 1;
        cupFixture.HomeUsedPauseWindows = 3; // Used all 3 normal-time pause windows
        cupFixture.HomeUsedSubstitutions = 5; // Used all 5 normal-time substitutions

        var snap90 = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, world.Id); // 90' -> tied knockout match enters Extra Time!
        Assert.Equal(90, snap90!.CurrentMinute);
        Assert.Equal(RoundState.EXTRA_TIME_BREAK, snap90.RoundState);

        var liveCupMatch = snap90.Matches.First(m => m.MatchId == cupFixture.Id);
        Assert.True(liveCupMatch.IsExtraTime);
        Assert.Equal(6, liveCupMatch.HomeMaxSubs); // +1 sub (total 6)
        Assert.Equal(4, liveCupMatch.HomeMaxPauseWindows); // +1 pause window (total 4)

        // Make the 6th substitution during the free 90' Extra Time break -> uses 6th sub, 0 extra pause window!
        var sixthSub = new List<InMatchSubstitutionDto>
        {
            new(liveCupMatch.HomeStarters[1].PlayerId, liveCupMatch.HomeBench[0].PlayerId)
        };
        var (sub6Ok, sub6Err, snapAfterSub6) = await orchestrator.SubmitInMatchAdjustmentAsync(
            new SubmitInMatchAdjustmentCommand(world.Id, coachHome.Id, cupFixture.Id, null, sixthSub, "sub-6-at-90"));

        Assert.True(sub6Ok, sub6Err);
        liveCupMatch = snapAfterSub6!.Matches.First(m => m.MatchId == cupFixture.Id);
        Assert.Equal(6, liveCupMatch.HomeSubCount);
        Assert.Equal(3, liveCupMatch.HomePauseWindowsUsed); // Break at 90' was free!

        // Confirm break at 90', advance to 95', and use the 4th pause window in live extra time!
        await orchestrator.ConfirmContinueMatchAsync(new ConfirmContinueMatchCommand(world.Id, coachHome.Id, "et-start"));
        for (int m = 91; m <= 95; m++)
        {
            await TickWithAutomaticPenaltyChoiceAsync(orchestrator, world.Id);
        }

        var (p4Ok, p4Err, _) = await orchestrator.RequestGlobalPauseAsync(new RequestPauseCommand(world.Id, coachHome.Id, "pause-4-et"));
        Assert.True(p4Ok, p4Err);
        Assert.Equal(3, cupFixture.HomeUsedPauseWindows);
        Assert.True((await orchestrator.SubmitInMatchAdjustmentAsync(new(world.Id,coachHome.Id,cupFixture.Id,
            cupFixture.HomeFormation=="4-3-3"?"4-4-2":"4-3-3",new(),"extra-time-edit"))).Success);
        Assert.Equal(4, cupFixture.HomeUsedPauseWindows);
    }

    [Fact]
    public async Task Multiplayer_100ConsecutiveRounds_8Coaches_SyncCounter_Pauses_Substitutions_Reconnection_AndRoomIsolation()
    {
        var repo = new InMemoryWorldRepository();
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

        var worldA = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala Stress 100 Rodadas (8 Técnicos)", MaxHumans: 8, TotalClubs: 128, Seed: 199801));
        var worldB = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Sala Paralela Isolada", MaxHumans: 2, TotalClubs: 128, Seed: 199802));

        var div1Clubs = worldA.Clubs.Where(c => c.DivisionTier == DivisionTier.PrimeiraDivisao).Take(8).ToList();
        var coachesA = new List<HumanCoach>();
        for (int i = 0; i < 8; i++)
        {
            var (_, _, c, _) = await orchestrator.JoinWorldAndAssignClubAsync(
                worldA.Id, Guid.NewGuid(), $"CoachStress{i + 1}", $"dev-stress-{i + 1}", div1Clubs[i].Id);
            coachesA.Add(c!);
        }

        var (_, _, coachB, _) = await orchestrator.JoinWorldAndAssignClubAsync(
            worldB.Id, Guid.NewGuid(), "CoachRoomB", "dev-room-b");

        int totalGlobalPausesExecuted = 0;
        int totalInMatchSubstitutionsExecuted = 0;
        int totalExtraTimeRounds = 0;

        for (int roundStep = 1; roundStep <= 100; roundStep++)
        {
            var currentWorld = (await repo.GetWorldAsync(worldA.Id))!;

            // Verify 0/8 ready counter at start of round preparation
            var readyDto0 = MultiplayerRoundOrchestrator.BuildReadyCounterDto(currentWorld);
            Assert.Equal(0, readyDto0.ReadyCount);
            Assert.Equal(8, readyDto0.TotalHumans);

            // Simulate temporary disconnection/auto-mode on round 15, then reconnection on round 16
            if (roundStep == 15)
            {
                coachesA[7].IsConnected = false;
                coachesA[7].IsAutoMode = true;
                await repo.SaveWorldAsync(currentWorld);
            }
            else if (roundStep == 16)
            {
                coachesA[7].IsConnected = true;
                coachesA[7].IsAutoMode = false;
                await repo.SaveWorldAsync(currentWorld);
            }

            // All human coaches submit tactics or unemployed PRONTO
            bool roundStarted = false;
            for (int i = 0; i < coachesA.Count; i++)
            {
                currentWorld = (await repo.GetWorldAsync(worldA.Id))!;
                var h = currentWorld.HumanCoaches.First(x => x.Id == coachesA[i].Id);
                if (h.IsReadyForRound) continue;

                if (h.CurrentClubId != null)
                {
                    var club = currentWorld.Clubs.First(c => c.Id == h.CurrentClubId);
                    var (ok, err, _, started) = await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
                        worldA.Id, h.Id, club.PreferredFormation, club.PreferredStarterIds, club.PreferredBenchIds,
                        $"r{roundStep}-h{i}", currentWorld.RoundVersion));
                    Assert.True(ok, err);
                    if (started) roundStarted = true;
                }
                else
                {
                    var (ok, err, _, started) = await orchestrator.SubmitUnemployedReadyAsync(
                        new UnemployedReadyRequest(worldA.Id, h.Id, $"r{roundStep}-h{i}-unemp", currentWorld.RoundVersion));
                    Assert.True(ok, err);
                    if (started) roundStarted = true;
                }
            }

            Assert.True(roundStarted, $"Rodada {roundStep} deveria iniciar automaticamente ao atingir 8/8.");

            // Tick minute by minute until the round finishes (RoundState.RESULTS)
            int safetyTicks = 0;
            while (safetyTicks++ < 150)
            {
                var snap = await TickWithAutomaticPenaltyChoiceAsync(orchestrator, worldA.Id);
                Assert.NotNull(snap);

                // Every 3rd round at minute 33, an active coach requests a global pause and makes 1 substitution
                if (roundStep % 3 == 0 && snap.CurrentMinute == 33 && snap.RoundState == RoundState.RUNNING_FIRST_HALF)
                {
                    currentWorld = (await repo.GetWorldAsync(worldA.Id))!;
                    var activeMatch = snap.Matches.FirstOrDefault(m =>
                        currentWorld.HumanCoaches.Any(hc => hc.CurrentClubId == m.HomeClubId || hc.CurrentClubId == m.AwayClubId));
                    if (activeMatch != null)
                    {
                        bool isHomeCoach = currentWorld.HumanCoaches.Any(hc => hc.CurrentClubId == activeMatch.HomeClubId);
                        var pausingCoach = currentWorld.HumanCoaches.First(hc =>
                            hc.CurrentClubId == (isHomeCoach ? activeMatch.HomeClubId : activeMatch.AwayClubId));

                        var (pOk, _, _) = await orchestrator.RequestGlobalPauseAsync(
                            new RequestPauseCommand(worldA.Id, pausingCoach.Id, $"pause-r{roundStep}-m33"));
                        if (pOk)
                        {
                            totalGlobalPausesExecuted++;
                            var startersList = isHomeCoach ? activeMatch.HomeStarters : activeMatch.AwayStarters;
                            var benchList = isHomeCoach ? activeMatch.HomeBench : activeMatch.AwayBench;
                            var outCand = startersList.FirstOrDefault(p => p.Position != PlayerPosition.G && !p.IsSentOff && !p.WasSubstitutedOut);
                            var inCand = benchList.FirstOrDefault(p => p.Position != PlayerPosition.G && !p.WasSubstitutedIn && !p.WasSubstitutedOut);

                            if (outCand != null && inCand != null)
                            {
                                var sub = new List<InMatchSubstitutionDto>
                                {
                                    new(outCand.PlayerId, inCand.PlayerId)
                                };
                                var (subOk, _, _) = await orchestrator.SubmitInMatchAdjustmentAsync(
                                    new SubmitInMatchAdjustmentCommand(worldA.Id, pausingCoach.Id, activeMatch.MatchId, null, sub, $"sub-r{roundStep}-m33"));
                                if (subOk) totalInMatchSubstitutionsExecuted++;
                            }
                            foreach (var hc in currentWorld.HumanCoaches)
                            {
                                await orchestrator.ConfirmContinueMatchAsync(
                                    new ConfirmContinueMatchCommand(worldA.Id, hc.Id, $"cont-p-{roundStep}-{hc.Id}"));
                            }
                        }
                    }
                }

                if (snap.RoundState == RoundState.EXTRA_TIME_BREAK)
                {
                    totalExtraTimeRounds++;
                }

                if (snap.ActivePauseWindow != null)
                {
                    currentWorld = (await repo.GetWorldAsync(worldA.Id))!;
                    foreach (var hc in currentWorld.HumanCoaches)
                    {
                        await orchestrator.ConfirmContinueMatchAsync(
                            new ConfirmContinueMatchCommand(worldA.Id, hc.Id, $"cont-brk-{roundStep}-{snap.CurrentMinute}-{hc.Id}"));
                    }
                }

                if (snap.RoundState == RoundState.RESULTS)
                {
                    await orchestrator.AdvanceToNextRoundPreparationAsync(worldA.Id);
                    break;
                }
            }

            Assert.True(safetyTicks < 150, $"Rodada {roundStep} excedeu limite de ticks.");
        }

        // Verify Room B remained completely untouched at Season 1, Round 1, PREPARING
        var finalWorldB = (await repo.GetWorldAsync(worldB.Id))!;
        Assert.Equal(1, finalWorldB.SeasonNumber);
        Assert.Equal(1, finalWorldB.RoundNumber);
        Assert.Equal(RoundState.PREPARING, finalWorldB.RoundState);
        Assert.Single(finalWorldB.HumanCoaches);
        Assert.Equal(coachB!.Id, finalWorldB.HumanCoaches[0].Id);

        Assert.True(totalGlobalPausesExecuted >= 10, $"Esperava >= 10 pausas globais nas 100 rodadas, obteve {totalGlobalPausesExecuted}.");
        Assert.True(totalInMatchSubstitutionsExecuted >= 10, $"Esperava >= 10 substituições em partida nas 100 rodadas, obteve {totalInMatchSubstitutionsExecuted}.");
    }
    // Legacy match drivers explicitly play the human decisions introduced by this task.
    private static async Task<LiveRoundSnapshotDto?> TickWithAutomaticPenaltyChoiceAsync(MultiplayerRoundOrchestrator orchestrator, Guid worldId)
    {
        var snap=await orchestrator.AdvanceClockTickAsync(worldId);
        while(snap?.PendingPenalty is { } penalty) {
            var choice=await orchestrator.ChoosePenaltyTakerAsync(new(worldId,penalty.CoachId,penalty.DecisionId,
                penalty.EligiblePlayers.OrderByDescending(p=>p.IsStar).ThenByDescending(p=>p.Position==PlayerPosition.A).ThenByDescending(p=>p.Strength).First().PlayerId,$"legacy-driver-{penalty.DecisionId}"));
            Assert.True(choice.Success,choice.Error);snap=choice.Snapshot;
        }
        return snap;
    }
}
