using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

/// <summary>
/// V3 — Etapa 02D: Carreira dos Treinadores, Demissões, Contratações e Trocas de Clubes.
/// Covers mandatory items 01..50 and scenarios A..H at Application & Orchestrator levels.
/// </summary>
public class ManagerCareerV3Stage02DApplicationTests
{
    private static (GameWorld World, InMemoryWorldRepository Repo, MultiplayerRoundOrchestrator Orchestrator, ClubAndMarketApplicationService Service) CreateHarness(int seed = 199801)
    {
        var world = WorldSeedFactory.CreateSeededWorld("V3 Stage 02D World", seed);
        world.RoundNumber = 4;
        var repo = new InMemoryWorldRepository();
        repo.SaveWorldAsync(world).GetAwaiter().GetResult();
        var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);
        var service = new ClubAndMarketApplicationService(repo, NullMatchRoomNotificationPublisher.Instance);
        return (world, repo, orchestrator, service);
    }

    // =========================================================================
    // 1. IDENTIDADE DO TREINADOR (Testes 01..05)
    // =========================================================================

    [Fact]
    public async Task T01_T02_T03_Identity_AccountCoachIdPreservedAcrossReconnectAndDeviceChange_NoDuplicateCoachInWorld()
    {
        var (world, _, orchestrator, _) = CreateHarness();
        Guid persistentCoachId = CoachCareerEngine.StableId("account-primary-coach-01");
        Guid clubId = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao).Id;

        var join1 = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, persistentCoachId, "Treinador Conta 1", "account:acc01-deviceA", clubId);
        Assert.True(join1.Success, join1.Error);
        Assert.Single(world.HumanCoaches);
        Assert.Equal(persistentCoachId, join1.Coach!.Id);
        Assert.Equal(clubId, join1.Coach.CurrentClubId);

        join1.Coach.CoachPoints = 24.5m;
        join1.Coach.LeagueTitles = 1;
        join1.Coach.TotalWins = 6;

        // T01: Reconnect with same coachId
        var reconnect = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, persistentCoachId, "Treinador Conta 1", "account:acc01-deviceA", null);
        Assert.True(reconnect.Success, reconnect.Error);
        Assert.Single(world.HumanCoaches);
        Assert.Equal(persistentCoachId, reconnect.Coach!.Id);
        Assert.Equal(clubId, reconnect.Coach.CurrentClubId);
        Assert.Equal(24.5m, reconnect.Coach.CoachPoints);
        Assert.Equal(1, reconnect.Coach.LeagueTitles);

        // T02 & T03: Reconnect from another device with the same persistent account/coachId
        var deviceSwitch = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, persistentCoachId, "Treinador Conta 1", "account:acc01-deviceB", null);
        Assert.True(deviceSwitch.Success, deviceSwitch.Error);
        Assert.Single(world.HumanCoaches);
        Assert.Equal(persistentCoachId, deviceSwitch.Coach!.Id);
        Assert.Equal(clubId, deviceSwitch.Coach.CurrentClubId);
        Assert.Equal(24.5m, deviceSwitch.Coach.CoachPoints);
    }

    [Fact]
    public async Task T04_T05_Identity_ClubSwitchAndDismissalPreserveCoachIdentityAndAccountLink()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        Guid persistentCoachId = CoachCareerEngine.StableId("account-primary-coach-04");
        var clubA = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var clubB = world.Clubs.First(c => c.DivisionTier == DivisionTier.SegundaDivisao);

        var join = await orchestrator.JoinWorldAndAssignClubAsync(
            world.Id, persistentCoachId, "Tecnico Persistente", "account:acc04", clubA.Id);
        Assert.True(join.Success, join.Error);
        var coach = join.Coach!;
        coach.LeagueTitles = 1;
        coach.CoachPoints = 65m;
        coach.Career.Morale = 80;

        foreach (var ai in world.AiManagers) ai.Career.Morale = 20;
        CoachCareerEngine.OpenVacancy(world, clubB);
        CoachCareerEngine.ProcessVacancies(world);
        var invite = world.JobInvitations.Single(i => i.CoachId == persistentCoachId && i.ClubId == clubB.Id);

        var accept = await service.AcceptJobInvitationAsync(world.Id, persistentCoachId, invite.Id, default, "switch-04");
        Assert.True(accept.Success, accept.Error);
        Assert.Equal(persistentCoachId, coach.Id);
        Assert.Equal("account:acc04", coach.DeviceId);
        Assert.Equal(clubB.Id, coach.CurrentClubId);

        Assert.True(CoachCareerEngine.Dismiss(world, persistentCoachId, clubB, "RED_MORALE_LOSS"));
        Assert.Equal(persistentCoachId, coach.Id);
        Assert.Equal("account:acc04", coach.DeviceId);
        Assert.Null(coach.CurrentClubId);
        Assert.True(coach.IsUnemployed);
        Assert.Equal(1, coach.LeagueTitles);
        Assert.Equal(65m, coach.CoachPoints);
    }

    // =========================================================================
    // 2. VÍNCULO CLUBE / TREINADOR E EXCLUSIVIDADE (Testes 06..11)
    // =========================================================================

    [Fact]
    public async Task T06_T07_T08_T09_T10_T11_ClubCoachBinding_ExclusivityAndPermissions()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        var clubA = world.Clubs.First(c => c.DivisionTier == DivisionTier.TerceiraDivisao);
        var clubB = world.Clubs.Where(c => c.DivisionTier == DivisionTier.TerceiraDivisao).Skip(1).First();

        Assert.Equal(128, world.Clubs.Count);
        Assert.All(world.Clubs, c =>
        {
            Assert.Null(c.HumanCoachId);
            Assert.NotNull(c.AiManagerId);
            Assert.Equal(c.Id, world.AiManagers.Single(a => a.Id == c.AiManagerId.Value).CurrentClubId);
        });

        Guid coach1Id = CoachCareerEngine.StableId("coach-06-1");
        var join1 = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach1Id, "Humano 1", "dev-1", clubA.Id);
        Assert.True(join1.Success, join1.Error);
        Assert.Equal(coach1Id, clubA.HumanCoachId);
        Assert.Null(clubA.AiManagerId);
        Assert.Equal(clubA.Id, join1.Coach!.CurrentClubId);

        Guid coach2Id = CoachCareerEngine.StableId("coach-06-2");
        var join2Same = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach2Id, "Humano 2", "dev-2", clubA.Id);
        Assert.False(join2Same.Success);
        Assert.Equal(coach1Id, clubA.HumanCoachId);

        var join2 = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach2Id, "Humano 2", "dev-2", clubB.Id);
        Assert.True(join2.Success, join2.Error);

        var trySecondClub = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach1Id, "Humano 1", "dev-1", clubB.Id);
        Assert.False(trySecondClub.Success);
        Assert.Single(world.Clubs, c => c.HumanCoachId == coach1Id);

        var playerInClubB = world.Players.First(p => p.ClubId == clubB.Id);
        var listOtherClubPlayer = await service.ListPlayerForTransferAsync(world.Id, coach1Id, playerInClubB.Id, 200_000m);
        Assert.False(listOtherClubPlayer.Success);

        var loanOtherClub = await service.TakeBankLoanAsync(world.Id, coach1Id, 500_000m, 10, requiredClub: clubB.Id);
        Assert.False(loanOtherClub.Success);
    }

    // =========================================================================
    // 3. TROCA DE CLUBE E SEPARAÇÃO CLUBE X TREINADOR (Testes 12..18)
    // =========================================================================

    [Fact]
    public async Task T12_To_T18_ClubSwitch_UpdatesOwnershipRevokesOldPermissionsAndLeavesSquadCashContractsWithOldClub()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        var clubOld = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var clubNew = world.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        clubNew.CashBalance = 15_000_000m;
        clubNew.Reputation = 92;
        clubNew.Stadium.BuiltCapacity = 60_000;

        Guid coachId = CoachCareerEngine.StableId("coach-switch-12");
        var join = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coachId, "Tecnico Troca", "dev-12", clubOld.Id);
        Assert.True(join.Success, join.Error);
        var coach = join.Coach!;
        coach.Career.Morale = 85;
        coach.LeagueTitles = 2;
        coach.CoachPoints = 88m;

        var oldSquadIds = world.Players.Where(p => p.ClubId == clubOld.Id).Select(p => p.Id).OrderBy(x => x).ToList();
        var newSquadIds = world.Players.Where(p => p.ClubId == clubNew.Id).Select(p => p.Id).OrderBy(x => x).ToList();
        decimal oldCashBefore = clubOld.CashBalance;
        decimal newCashBefore = clubNew.CashBalance;

        var oldPlayer = world.Players.First(p => p.ClubId == clubOld.Id);
        oldPlayer.RenewedForSeason = world.SeasonNumber;
        oldPlayer.NegotiatedContractProtected = true;

        var pendingRaisePlayer = world.Players.Where(p => p.ClubId == clubOld.Id && p.Id != oldPlayer.Id).First();
        world.Economy.SalaryRequests.Add(new SalaryRaiseRequest
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            PlayerId = pendingRaisePlayer.Id,
            ClubId = clubOld.Id,
            Season = world.SeasonNumber,
            Round = world.RoundNumber,
            CurrentSalary = pendingRaisePlayer.SalaryPerRound,
            RequestedSalary = pendingRaisePlayer.SalaryPerRound + 100m,
            ReferenceSalary = pendingRaisePlayer.SalaryPerRound + 100m,
            RenewalMatches = world.Economy.Rules.RenewalMatches,
            Reason = "STRENGTH_INCREASE",
            Status = SalaryRequestStatus.PENDING
        });

        foreach (var ai in world.AiManagers) ai.Career.Morale = 20;
        CoachCareerEngine.OpenVacancy(world, clubNew);
        CoachCareerEngine.ProcessVacancies(world);
        var invitation = world.JobInvitations.Single(i => i.CoachId == coachId && i.ClubId == clubNew.Id);

        var switchRes = await service.AcceptJobInvitationAsync(world.Id, coachId, invitation.Id, default, "accept-switch-12");
        Assert.True(switchRes.Success, switchRes.Error);
        Assert.Equal(clubNew.Id, coach.CurrentClubId);
        Assert.Equal(coachId, clubNew.HumanCoachId);
        Assert.Null(clubNew.AiManagerId);

        Assert.Null(clubOld.HumanCoachId);
        Assert.NotNull(clubOld.AiManagerId);
        Assert.DoesNotContain(world.Economy.SalaryRequests, r => r.ClubId == clubOld.Id && r.Status == SalaryRequestStatus.PENDING);

        var oldListRes = await service.ListPlayerForTransferAsync(world.Id, coachId, oldPlayer.Id, 150_000m);
        Assert.False(oldListRes.Success);

        var oldRenewRes = await service.RenewPlayerContractAsync(world.Id, coachId, oldPlayer.Id, 10);
        Assert.False(oldRenewRes.Success);

        var oldLoanRes = await service.TakeBankLoanAsync(world.Id, coachId, 500_000m, 10, requiredClub: clubOld.Id);
        Assert.False(oldLoanRes.Success);

        var oldStadiumRes = await service.StadiumCommandAsync(world.Id, coachId, clubOld.Id, false, 0, "stad-old");
        Assert.False(oldStadiumRes.Success);

        var newPlayer = world.Players.First(p => p.ClubId == clubNew.Id && p.Position == PlayerPosition.A);
        var newListRes = await service.ListPlayerForTransferAsync(world.Id, coachId, newPlayer.Id, 500_000m);
        Assert.True(newListRes.Success, newListRes.Error);

        var newTacticsRes = await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id, coachId, clubNew.PreferredFormation, clubNew.PreferredStarterIds.ToList(), clubNew.PreferredBenchIds.ToList(), "tac-new-14", world.RoundVersion));
        Assert.True(newTacticsRes.Success, newTacticsRes.Error);

        Assert.Equal(oldSquadIds, world.Players.Where(p => p.ClubId == clubOld.Id).Select(p => p.Id).OrderBy(x => x).ToList());
        Assert.Equal(newSquadIds, world.Players.Where(p => p.ClubId == clubNew.Id).Select(p => p.Id).OrderBy(x => x).ToList());
        Assert.Equal(oldCashBefore, clubOld.CashBalance);
        Assert.Equal(newCashBefore, clubNew.CashBalance);
        Assert.Equal(clubOld.Id, oldPlayer.ClubId);
        Assert.Equal(world.SeasonNumber, oldPlayer.RenewedForSeason);
        Assert.True(oldPlayer.NegotiatedContractProtected);

        Assert.Equal(2, coach.EmploymentHistory.Count);
        Assert.Equal(clubOld.Id, coach.EmploymentHistory[0].ClubId);
        Assert.Equal(world.SeasonNumber, coach.EmploymentHistory[0].EndSeason);
        Assert.Equal("CLUB_PROJECT_UPGRADE", coach.EmploymentHistory[0].ReasonForLeaving);
        Assert.Equal(clubNew.Id, coach.EmploymentHistory[1].ClubId);
        Assert.Null(coach.EmploymentHistory[1].EndSeason);
    }

    // =========================================================================
    // 4. DEMISSÕES E TREINADOR SEM CLUBE (Testes 19..23)
    // =========================================================================

    [Fact]
    public async Task T19_To_T23_Dismissal_ValidTriggerUnemployedStateBlockedClubCommandsAiTakeoverAndHistoryPreserved()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        var club = world.Clubs.First(c => c.DivisionTier == DivisionTier.TerceiraDivisao);
        var opponent = world.Clubs.Skip(1).First(c => c.DivisionTier == DivisionTier.TerceiraDivisao);

        Guid coachId = CoachCareerEngine.StableId("coach-dismiss-19");
        var join = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coachId, "Tecnico Demitido", "dev-19", club.Id);
        Assert.True(join.Success, join.Error);
        var coach = join.Coach!;
        coach.LeagueTitles = 1;
        coach.CupTitles = 1;
        coach.CoachPoints = 44.5m;
        coach.Career.Morale = 22;

        var match = new MatchFixture
        {
            GameWorldId = world.Id,
            SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber,
            CompetitionType = CompetitionType.League,
            DivisionTier = DivisionTier.TerceiraDivisao,
            HomeClubId = club.Id,
            AwayClubId = opponent.Id,
            CurrentMinute = 90,
            IsFinished = true,
            HomeGoals = 0,
            AwayGoals = 2
        };
        world.Fixtures.Add(match);
        CoachCareerEngine.CaptureLeadership(world, match);

        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, [match]);

        Assert.True(coach.IsUnemployed);
        Assert.Null(coach.CurrentClubId);
        Assert.Equal(1, coach.Dismissals);
        Assert.Null(club.HumanCoachId);
        Assert.NotNull(club.AiManagerId);

        var playerInOldClub = world.Players.First(p => p.ClubId == club.Id);
        Assert.False((await service.ListPlayerForTransferAsync(world.Id, coachId, playerInOldClub.Id, 100_000m)).Success);

        var buyTarget = world.Players.First(p => p.ClubId == opponent.Id);
        buyTarget.IsListedForTransfer = true;
        buyTarget.AskingPrice = 100_000m;
        Assert.False((await service.BuyListedPlayerAsync(world.Id, coachId, buyTarget.Id, "buy-after-dismiss")).Success);

        Assert.False((await orchestrator.SubmitTacticsAsync(new SubmitTacticsRequest(
            world.Id, coachId, "4-4-2", club.PreferredStarterIds.ToList(), club.PreferredBenchIds.ToList(), "tac-dismissed", world.RoundVersion))).Success);

        var unempReady = await orchestrator.SubmitUnemployedReadyAsync(new UnemployedReadyRequest(world.Id, coachId, "unemp-ready-20", world.RoundVersion));
        Assert.True(unempReady.Success, unempReady.Error);

        Assert.Equal(coachId, coach.Id);
        Assert.Equal(1, coach.LeagueTitles);
        Assert.Equal(1, coach.CupTitles);
        Assert.Equal(46.5m, coach.CoachPoints);
        Assert.Single(coach.EmploymentHistory);
        Assert.Equal("RED_MORALE_LOSS", coach.EmploymentHistory[0].ReasonForLeaving);
    }

    // =========================================================================
    // 5. PROPOSTAS, CONCORRÊNCIA E IDEMPOTÊNCIA (Testes 24..28 & Cenários A..H)
    // =========================================================================

    [Fact]
    public async Task T24_To_T28_Proposals_AcceptRejectExpiredConcurrencyAndIdempotentReplay()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        var clubTarget1 = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var clubTarget2 = world.Clubs.Where(c => c.DivisionTier == DivisionTier.QuartaDivisao).Skip(1).First();

        Guid coach1Id = CoachCareerEngine.StableId("coach-prop-24-1");
        Guid coach2Id = CoachCareerEngine.StableId("coach-prop-24-2");
        await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach1Id, "Candidato 1", "dev-24-1", joinUnemployed: true);
        await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coach2Id, "Candidato 2", "dev-24-2", joinUnemployed: true);

        var coach1 = world.HumanCoaches.Single(h => h.Id == coach1Id);
        var coach2 = world.HumanCoaches.Single(h => h.Id == coach2Id);
        coach1.CoachPoints = 50m;
        coach2.CoachPoints = 30m;

        foreach (var ai in world.AiManagers) ai.Career.Morale = 20;
        CoachCareerEngine.OpenVacancy(world, clubTarget1);
        CoachCareerEngine.OpenVacancy(world, clubTarget2);

        var careerDto = await service.GetCareerAsync(world.Id, coach1Id);
        Assert.NotNull(careerDto);
        Assert.Equal(2, careerDto!.Invitations.Count);
        Assert.Equal("VERDE", careerDto.MoraleBand);

        var inv1ForTarget1 = careerDto.Invitations.Single(i => i.ClubId == clubTarget1.Id);
        var inv1ForTarget2 = careerDto.Invitations.Single(i => i.ClubId == clubTarget2.Id);

        var rejectRes = await service.RespondJobInvitationAsync(world.Id, coach1Id, inv1ForTarget1.InvitationId, false, default, "reject-key-26");
        Assert.True(rejectRes.Success, rejectRes.Error);
        Assert.True(coach1.IsUnemployed);
        Assert.Contains(world.JobInvitations, i => i.CoachId == coach2Id && i.ClubId == clubTarget1.Id && !i.IsRejected && !i.IsExpired);

        long versionAfterReject = world.RoundVersion;
        var rejectRepeat = await service.RespondJobInvitationAsync(world.Id, coach1Id, inv1ForTarget1.InvitationId, false, default, "reject-key-26");
        Assert.True(rejectRepeat.Success, rejectRepeat.Error);
        Assert.Equal(versionAfterReject, world.RoundVersion);

        var keyConflict = await service.RespondJobInvitationAsync(world.Id, coach1Id, inv1ForTarget1.InvitationId, true, default, "reject-key-26");
        Assert.False(keyConflict.Success);

        var rawInv2 = world.JobInvitations.Single(i => i.Id == inv1ForTarget2.InvitationId);
        rawInv2.ExpiresAtOrdinal = CoachCareerEngine.Ordinal(world);
        Assert.False((await service.AcceptJobInvitationAsync(world.Id, coach1Id, rawInv2.Id)).Success);

        var invCoach2Target1 = world.JobInvitations.Single(i => i.CoachId == coach2Id && i.ClubId == clubTarget1.Id && !i.IsRejected);
        var staleConcurrentInvite = new JobInvitation
        {
            Id = CoachCareerEngine.StableId("stale-concurrent-28"),
            GameWorldId = world.Id,
            ClubId = clubTarget1.Id,
            CoachId = coach1Id,
            VacancyId = invCoach2Target1.VacancyId,
            CreatedAtSeason = world.SeasonNumber,
            CreatedAtRound = world.RoundNumber,
            ExpiresAtOrdinal = CoachCareerEngine.Ordinal(world) + 3
        };
        world.JobInvitations.Add(staleConcurrentInvite);

        var concurrentResults = await Task.WhenAll(
            service.AcceptJobInvitationAsync(world.Id, coach1Id, staleConcurrentInvite.Id, default, "accept-c1"),
            service.AcceptJobInvitationAsync(world.Id, coach2Id, invCoach2Target1.Id, default, "accept-c2"));

        Assert.Single(concurrentResults, r => r.Success);
        Assert.Single(world.HumanCoaches, h => h.CurrentClubId == clubTarget1.Id);
        var winnerCoach = world.HumanCoaches.Single(h => h.CurrentClubId == clubTarget1.Id);
        var winnerKey = winnerCoach.Id == coach1Id ? "accept-c1" : "accept-c2";
        var winnerInviteId = winnerCoach.Id == coach1Id ? staleConcurrentInvite.Id : invCoach2Target1.Id;

        int employmentCountBeforeReplay = winnerCoach.EmploymentHistory.Count;
        long versionBeforeReplay = world.RoundVersion;
        var replayAccept = await service.AcceptJobInvitationAsync(world.Id, winnerCoach.Id, winnerInviteId, default, winnerKey);
        Assert.True(replayAccept.Success, replayAccept.Error);
        Assert.Equal(employmentCountBeforeReplay, winnerCoach.EmploymentHistory.Count);
        Assert.Equal(versionBeforeReplay, world.RoundVersion);
    }

    // =========================================================================
    // 6. IA E TRANSIÇÃO HUMANO <-> IA (Testes 29..32)
    // =========================================================================

    [Fact]
    public async Task T29_To_T32_AiManagers_ManageUnclaimedClubsAndNeverInterfereWithHumanClubs()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        var clubHuman = world.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        var clubAi = world.Clubs.Skip(1).First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        var clubDestination = world.Clubs.First(c => c.DivisionTier == DivisionTier.SegundaDivisao);

        Assert.True(MarketValidationEngine.CanActAsAi(world, clubHuman));
        Assert.True(MarketValidationEngine.CanActAsAi(world, clubAi));

        Guid coachId = CoachCareerEngine.StableId("coach-ai-30");
        var join = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coachId, "Humano Titular", "dev-30", clubHuman.Id);
        Assert.True(join.Success, join.Error);
        Assert.False(MarketValidationEngine.CanActAsAi(world, clubHuman));
        Assert.Null(clubHuman.AiManagerId);

        foreach (var p in world.Players.Where(p => p.ClubId == clubHuman.Id)) p.IsListedForTransfer = false;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
        Assert.All(world.Players.Where(p => p.ClubId == clubHuman.Id), p => Assert.False(p.IsListedForTransfer));

        var coach = join.Coach!;
        coach.Career.Morale = 85;
        clubHuman.Reputation = 40;
        clubHuman.CashBalance = 200_000m;
        clubHuman.Stadium.BuiltCapacity = 8_000;
        clubDestination.Reputation = 95;
        clubDestination.CashBalance = 25_000_000m;
        clubDestination.Stadium.BuiltCapacity = 70_000;
        foreach (var p in world.Players.Where(p => p.ClubId == clubHuman.Id)) p.Strength = 18;
        foreach (var p in world.Players.Where(p => p.ClubId == clubDestination.Id)) p.Strength = 46;
        foreach (var ai in world.AiManagers) ai.Career.Morale = 20;

        CoachCareerEngine.OpenVacancy(world, clubDestination);
        CoachCareerEngine.ProcessVacancies(world);
        var invite = world.JobInvitations.Single(i => i.CoachId == coachId && i.ClubId == clubDestination.Id);
        var accept = await service.AcceptJobInvitationAsync(world.Id, coachId, invite.Id, default, "ai-transition-31");
        Assert.True(accept.Success, accept.Error);

        Assert.True(MarketValidationEngine.CanActAsAi(world, clubHuman));
        Assert.NotNull(clubHuman.AiManagerId);
        Assert.False(MarketValidationEngine.CanActAsAi(world, clubDestination));
        Assert.Null(clubDestination.AiManagerId);
        Assert.Equal(coachId, clubDestination.HumanCoachId);
    }

    // =========================================================================
    // 7. TEMPORADA, SAVES E REGRESSÃO (Testes 33..50)
    // =========================================================================

    [Fact]
    public async Task T33_To_T50_SeasonTransitionSavesAndStages01To02CRegression()
    {
        var (world, _, orchestrator, service) = CreateHarness();
        ClassicPlayerEconomyEngine.EnableClassicalEconomy(world);

        var clubBuyer = world.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        var clubSeller = world.Clubs.Skip(1).First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
        clubBuyer.CashBalance = 15_000_000m;

        Guid coachBuyerId = CoachCareerEngine.StableId("reg-buyer-43");
        var join = await orchestrator.JoinWorldAndAssignClubAsync(world.Id, coachBuyerId, "Comprador", "dev-43", clubBuyer.Id);
        Assert.True(join.Success, join.Error);

        Assert.All(world.Players, p => Assert.InRange(p.Strength, 1, 50));
        ClassicForceEvolutionEngine.PrepareRound(world);
        var lineupRes = await orchestrator.SelectLineupAsync(world.Id, new SelectLineupRequest(
            coachBuyerId, "4-4-2", LineupSelectionMode.Automatic, clubBuyer.PreferredStarterIds, "lineup-44", world.RoundVersion));
        Assert.True(lineupRes.Success, lineupRes.Error);
        Assert.Equal(11, lineupRes.Selection!.StarterPlayerIds.Count);

        var testPlayer = world.Players.First(p => p.ClubId == clubSeller.Id && p.Position == PlayerPosition.A);
        testPlayer.Strength = 25;
        decimal baseSalary25 = ClassicPlayerEconomyEngine.CalculateClassicalBaseSalary(25, world.Economy.InflationIndex);
        decimal marketVal25 = ClassicPlayerEconomyEngine.CalculateClassicalMarketValue(testPlayer, world.Economy.InflationIndex);
        Assert.True(baseSalary25 > 0m);
        Assert.True(marketVal25 > baseSalary25);

        testPlayer.IsListedForTransfer = true;
        testPlayer.AskingPrice = marketVal25;
        var preview = await service.GetTransferContractPreviewAsync(world.Id, coachBuyerId, testPlayer.Id);
        Assert.NotNull(preview.Preview);
        var buyRes = await service.BuyListedPlayerAsync(
            world.Id, coachBuyerId, testPlayer.Id, "buy-47", default, world.RoundVersion,
            preview.Preview!.ListingVersion, clubSeller.Id, testPlayer.AskingPrice, preview.Preview.SalaryTermsToken);
        Assert.True(buyRes.Success, buyRes.Error);
        Assert.Equal(clubBuyer.Id, testPlayer.ClubId);

        var sellerCoachId = CoachCareerEngine.StableId("reg-seller-48");
        await orchestrator.JoinWorldAndAssignClubAsync(world.Id, sellerCoachId, "Vendedor", "dev-48", clubSeller.Id);
        var auctionPlayer = world.Players.First(p => p.ClubId == clubSeller.Id && p.Position == PlayerPosition.M);
        decimal minBid = VoluntaryAuctionDefaults.MinimumBid;
        var auctionCreate = await service.CreateVoluntaryAuctionAsync(world.Id, sellerCoachId, auctionPlayer.Id, minBid, 2, default, "auc-48");
        Assert.True(auctionCreate.Success, auctionCreate.Error);
        var bidRes = await service.PlaceAuctionBidAsync(world.Id, coachBuyerId, auctionCreate.AuctionId!.Value, minBid, default, "bid-48");
        Assert.True(bidRes.Success, bidRes.Error);

        var renewTarget = world.Players.First(p => p.ClubId == clubBuyer.Id && p.Id != testPlayer.Id && !ClassicEconomyEngine.IsSeasonRenewed(world, p));
        var renewPreview = await service.GetRenewalContractPreviewAsync(world.Id, coachBuyerId, renewTarget.Id);
        Assert.NotNull(renewPreview.Preview);
        var renewRes = await service.RenewPlayerContractAsync(
            world.Id, coachBuyerId, renewTarget.Id, 0, default, "renew-49", renewPreview.Preview!.RoundVersion, renewPreview.Preview.TermsToken);
        Assert.True(renewRes.Success, renewRes.Error);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(world, renewTarget));

        // Round-trip save & reload
        string json = JsonSerializer.Serialize(world);
        var loaded = JsonSerializer.Deserialize<GameWorld>(json)!;
        CoachCareerEngine.EnsureMigrated(loaded);
        Assert.Equal(clubBuyer.Id, loaded.HumanCoaches.Single(h => h.Id == coachBuyerId).CurrentClubId);
        var legacyNode = JsonNode.Parse(json)!.AsObject();
        legacyNode.Remove("CareerRulesVersion");
        var legacyLoaded = JsonSerializer.Deserialize<GameWorld>(legacyNode.ToJsonString())!;
        CoachCareerEngine.EnsureMigrated(legacyLoaded);
        Assert.Equal(8, legacyLoaded.CareerRulesVersion);
    }
}
