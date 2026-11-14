using System.Collections.Concurrent;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

/// <summary>
/// Server-authoritative multiplayer orchestrator for isolated GameWorld rooms.
/// Guarantees concurrency safety via per-world locks, idempotent command handling,
/// synchronized 0/N ready counters, shared match clock ticks, and global pause windows.
/// </summary>
public sealed partial class MultiplayerRoundOrchestrator
{
    private readonly IRoundTickTiming? _tickTiming;
    private readonly IWorldRepository _repository;
    private readonly RoomConnectionRegistry? _connections;
    private readonly IMatchRoomNotificationPublisher _publisher;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> WorldLocks = new();

    public MultiplayerRoundOrchestrator(
        IWorldRepository repository,
        IMatchRoomNotificationPublisher publisher, RoomConnectionRegistry? connections = null,
        IRoundTickTiming? tickTiming = null)
    {
        _repository = repository;
        _publisher = publisher;
        _tickTiming = tickTiming;
        _connections = connections;
    }

    public static SemaphoreSlim GetRoomLock(Guid worldId) => GetLock(worldId);

    internal static SemaphoreSlim GetLock(Guid worldId)
        => WorldLocks.GetOrAdd(worldId, _ => new SemaphoreSlim(1, 1));

    public async Task<GameWorld> CreateWorldAsync(CreateWorldRequest request, CancellationToken ct = default, Guid? ownerCoachId = null)
    {
        if (!Enum.IsDefined(request.ForceEvolutionMode)) throw new ArgumentException("Unsupported force evolution mode.");
        var world = WorldSeedFactory.CreateSeededWorld(
            worldName: string.IsNullOrWhiteSpace(request.WorldName) ? "Campeonato Brasileiro Clássico" : request.WorldName.Trim(),
            seed: request.Seed == 0 ? 199801 : request.Seed,
            totalClubs: request.TotalClubs <= 0 ? 128 : request.TotalClubs,
            maxHumans: Math.Clamp(request.MaxHumans, 1, 8),
            tickIntervalMs: Math.Clamp(request.TickIntervalMs, 10, 5000),
            pauseTimeoutSeconds: Math.Clamp(request.PauseTimeoutSeconds, 5, 3600),
            allowClassic505: request.AllowClassic505,
            inviteCode: request.InviteCode,
            forceEvolutionMode: request.ForceEvolutionMode,
            applyInitialSquadStrength: true);

        world.OwnerCoachId = ownerCoachId;
        await _repository.SaveWorldAsync(world, ct);
        return world;
    }

    public async Task<(bool Success, string? Error, HumanCoach? Coach, GameWorld? World)> JoinWorldAndAssignClubAsync(
        Guid worldId,
        Guid coachId,
        string nickname,
        string deviceId,
        Guid? preferredClubId = null,
        bool joinUnemployed = false,
        CancellationToken ct = default,
        bool allowDeviceReconnect = false,
        IReadOnlySet<Guid>? accountCoaches = null,
        string? inviteCode = null)
    {
        var sem = GetLock(worldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = accountCoaches == null
                ? await _repository.GetWorldAsync(worldId, ct)
                : await _repository.GetWorldForRoundCheckpointAsync(worldId, ct);
            if (world == null) return (false, "Sala/Mundo não encontrado.", null, null);

            var existingCoach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
            if (accountCoaches != null)
            {
                bool ownsAnotherSeat = existingCoach == null && world.HumanCoaches.Any(h => accountCoaches.Contains(h.Id));
                bool admitted = existingCoach != null || world.CreatorCoachId == coachId ||
                    (!string.IsNullOrWhiteSpace(inviteCode) && string.Equals(world.InviteCode, inviteCode.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!accountCoaches.Contains(coachId) || ownsAnotherSeat || !admitted)
                    return (false, "Use o código da sala ou retome com o perfil vinculado.", null, null);
                if (world.PendingWorldDataChange != null) throw new RoundCheckpointPendingException();
                if (world.RoundCheckpointNeedsSave) await PersistRoundCheckpointAsync(world);
            }
            var normalizedDevice = deviceId.Trim();
            // LocalDev device identity survives nickname edits; never guess by nickname or a shared legacy placeholder.
            if (allowDeviceReconnect && existingCoach == null && normalizedDevice.Length > 0 && normalizedDevice != "dev")
            {
                var matches = world.HumanCoaches.Where(h => string.Equals((h.DeviceId ?? string.Empty).Trim(), normalizedDevice, StringComparison.Ordinal)).ToList();
                if (matches.Count > 1)
                    return (false, "Mais de um treinador está associado a esse dispositivo. Retome a identidade original; nenhuma vaga foi alterada.", null, world);
                existingCoach = matches.SingleOrDefault();
            }
            if (existingCoach != null)
            {
                if (accountCoaches != null && (preferredClubId == null || existingCoach.CurrentClubId == preferredClubId))
                    return (true, null, existingCoach, world);
                if (preferredClubId != null && existingCoach.CurrentClubId != preferredClubId)
                {
                    if (existingCoach.CurrentClubId != null || existingCoach.Dismissals > 0 || existingCoach.EmploymentHistory.Count > 0)
                        return (false, "Mudanças de carreira são feitas por convites na aba RANKING & CARREIRA.", existingCoach, world);
                    if (world.InitialLobby is {StartedAt:null,DrawCompletedAt:not null})
                        return (false,"O sorteio já foi concluído; as escolhas iniciais estão bloqueadas.",existingCoach,world);
                    if (world.RoundState != RoundState.PREPARING || existingCoach.IsReadyForRound)
                        return (false, "Troque de clube somente entre rodadas e sem pronto.", existingCoach, world);
                    var assignRes = TryAssignClubInternal(world, existingCoach, preferredClubId.Value);
                    if (!assignRes.Success) return (false, assignRes.Error, existingCoach, world);
                    if(world.InitialLobby is {StartedAt:null} waiting) {waiting.Choices[existingCoach.Id]=new(InitialCareerChoice016B.MANUAL,preferredClubId);waiting.Version++;}
                }
                world.RoundVersion++;
                if (accountCoaches == null) await _repository.SaveWorldAsync(world, ct);
                else await PersistRoundCheckpointAsync(world);
                await _publisher.PublishReadyCounterUpdatedAsync(world.Id, BuildReadyCounterDto(world), ct);
                return (true, null, existingCoach, world);
            }

            if (world.CreatorCoachId is Guid creator && coachId != creator &&
                !world.HumanCoaches.Any(c => c.Id == creator) && world.HumanCoaches.Count >= world.MaxHumans - 1)
                return (false,"A última vaga está reservada ao criador desta sala.",null,world);
            if (world.HumanCoaches.Count >= world.MaxHumans)
                return (false, $"Sala lotada: as {world.MaxHumans} vagas estão ocupadas. Treinadores desconectados mantêm sua vaga e seu clube. Para retornar, use a mesma identidade e o mesmo ID de dispositivo; uma nova identidade precisa de uma vaga livre.", null, world);
            if (world.RoundState != RoundState.PREPARING)
                return (false, "Aguarde a próxima preparação para entrar na sala.", null, world);

            var coach = new HumanCoach
            {
                Id = coachId == Guid.Empty ? Guid.NewGuid() : coachId,
                GameWorldId = world.Id,
                Nickname = string.IsNullOrWhiteSpace(nickname) ? $"Técnico {world.HumanCoaches.Count + 1}" : nickname.Trim(),
                DeviceId = normalizedDevice,
                SessionToken = Guid.NewGuid().ToString("N"),
                IsHost = world.CreatorCoachId is Guid host ? host == coachId : world.HumanCoaches.Count == 0,
                IsConnected = false
            };

            if (!joinUnemployed)
            {
                Club? targetClub = null;
                if (preferredClubId != null)
                {
                    targetClub = world.Clubs.FirstOrDefault(c => c.Id == preferredClubId.Value);
                    if (targetClub == null) return (false, "Clube escolhido não existe nesta sala.", null, world);
                    if (targetClub.DivisionTier == DivisionTier.Distrital)
                        return (false, "Treinador humano nunca pode controlar um clube distrital.", null, world);
                    if (targetClub.HumanCoachId != null)
                        return (false, $"O clube {targetClub.Name} já é controlado por outro humano.", null, world);
                }
                else if(world.InitialLobby is not {StartedAt:null})
                {
                    // Auto-pick available division club (preferring 4th/3rd/2nd/1st available)
                    targetClub = world.Clubs
                        .Where(c => c.DivisionTier != DivisionTier.Distrital && c.HumanCoachId == null)
                        .OrderBy(c => c.DivisionTier)
                        .ThenBy(c => c.DeterministicIndex)
                        .FirstOrDefault();
                }

                if (targetClub != null)
                {
                    AssignCoachToClubObject(world, coach, targetClub);
                }
            }

            world.HumanCoaches.Add(coach);
            if(world.InitialLobby is {StartedAt:null} initialLobby) {
                initialLobby.Choices[coach.Id]=new(coach.CurrentClubId!=null?InitialCareerChoice016B.MANUAL:
                    joinUnemployed?InitialCareerChoice016B.UNEMPLOYED:InitialCareerChoice016B.PENDING,coach.CurrentClubId);
                initialLobby.Version++;
            }
            world.RoundVersion++;
            if (accountCoaches == null) await _repository.SaveWorldAsync(world, ct);
            else await PersistRoundCheckpointAsync(world);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, BuildReadyCounterDto(world), ct);
            return (true, null, coach, world);
        }
        finally
        {
            sem.Release();
        }
    }

    private static (bool Success, string? Error) TryAssignClubInternal(GameWorld world, HumanCoach coach, Guid clubId)
    {
        var club = world.Clubs.FirstOrDefault(c => c.Id == clubId);
        if (club == null) return (false, "Clube não encontrado.");
        if (club.DivisionTier == DivisionTier.Distrital)
            return (false, "Treinador humano nunca pode controlar um clube distrital.");
        if (club.HumanCoachId != null && club.HumanCoachId != coach.Id)
            return (false, "Este clube já pertence a outro treinador humano.");

        if (coach.CurrentClubId != null && coach.CurrentClubId != club.Id)
        {
            var prev = world.Clubs.FirstOrDefault(c => c.Id == coach.CurrentClubId.Value);
            if (prev != null)
            {
                prev.HumanCoachId = null;
                var record = coach.EmploymentHistory.LastOrDefault(e => e.EndSeason == null);
                if (record != null) { record.EndSeason = world.SeasonNumber; record.EndRound = world.RoundNumber; record.ReasonForLeaving = "CLUB_CHANGE"; }
                CoachCareerEngine.OpenVacancy(world, prev); CoachCareerEngine.EnsureProvisional(world, prev);
            }
        }

        AssignCoachToClubObject(world, coach, club);
        return (true, null);
    }

    private static void AssignCoachToClubObject(GameWorld world, HumanCoach coach, Club club)
    {
        if (club.AiManagerId is Guid aiId)
        {
            var ai = world.AiManagers.FirstOrDefault(a => a.Id == aiId);
            if (ai != null && ai.CurrentClubId == club.Id)
            {
                ai.CurrentClubId = null;
                var previousJob = ai.EmploymentHistory.LastOrDefault(e => e.EndSeason == null && e.ClubId == club.Id);
                if (previousJob != null) { previousJob.EndSeason = world.SeasonNumber; previousJob.EndRound = world.RoundNumber; previousJob.ReasonForLeaving = "HUMAN_INITIAL_ASSIGNMENT"; }
            }
            club.AiManagerId = null;
        }
        foreach (var pending in world.JobInvitations.Where(i => i.ClubId == club.Id && !i.IsAccepted)) pending.IsExpired = true;
        club.CareerVacancyId = null; club.HasProvisionalManager = false;
        club.HumanCoachId = coach.Id;
        coach.CurrentClubId = club.Id;
        coach.IsReadyForRound = false;
        coach.SubmittedFormation = null;
        coach.SubmittedStarterIds.Clear();
        coach.SubmittedBenchIds.Clear();
        coach.EmploymentHistory.Add(new CoachEmploymentRecord
        {
            ClubId = club.Id,
            ClubName = club.Name,
            StartSeason = world.SeasonNumber,
            StartRound = world.RoundNumber
        });

        // Pre-populate valid starter & bench selection if empty
        var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
        if (club.PreferredStarterIds.Count != 11)
        {
            var spec = TacticalFormations.Resolve(club.PreferredFormation, world.AllowClassic505);
            var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, spec, world, club.LineupSelectionMode, club.PreferredStarterIds);
            club.PreferredStarterIds = starters;
            club.PreferredBenchIds = bench;
        }
    }

    /// <summary>
    /// Handles tactic submission ("ENVIAR TÁTICA") for a human coach during RoundState.PREPARING.
    /// Increments the 0/N counter idempotently and automatically starts the round when N/N is reached.
    /// </summary>
    public async Task<(bool Success, string? Error, ReadyCounterDto? Counter, bool RoundStarted)> SubmitTacticsAsync(
        SubmitTacticsRequest request,
        CancellationToken ct = default)
    {
        var sem = GetLock(request.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await GetWorldForReadinessAsync(request.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null, false);

            var receipt = ReadReadinessReceipt(world, "tactics", request.CoachId, request.IdempotencyKey, request);
            if (receipt.HasValue)
                return (receipt.Value, receipt.Value ? null : "Chave de operação reutilizada com dados diferentes.", BuildReadyCounterDto(world), false);

            if(world.InitialLobby is {StartedAt:null})
                return (false,"Aguardando o criador iniciar o campeonato no lobby.",BuildReadyCounterDto(world),false);
            if (world.RoundState != RoundState.PREPARING)
                return (false, $"A rodada já não está em fase de preparação (estado atual: {world.RoundState}).", BuildReadyCounterDto(world), false);

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == request.CoachId);
            if (coach == null)
                return (false, "Treinador humano não pertence a esta sala.", BuildReadyCounterDto(world), false);

            if (coach.CurrentClubId == null)
                return (false, "Treinador está desempregado; use a ação 'Pronto para acompanhar'.", BuildReadyCounterDto(world), false);

            var club = world.Clubs.FirstOrDefault(c => c.Id == coach.CurrentClubId.Value);
            if (club == null || club.HumanCoachId != coach.Id)
                return (false, "Propriedade do clube inválida.", BuildReadyCounterDto(world), false);

            await RecoverSalaryDecisionsAsync(world, _repository);
            if (SalarySeasonTransitionEngine.BlockingReason(world, club.Id) is string salaryError)
                return (false, salaryError, BuildReadyCounterDto(world), false);

            var clubSquad = world.Players.Where(p => p.ClubId == club.Id).ToList();
            bool needsLineup = NeedsCurrentRoundLineup(world, club.Id);
            var (isValid, validationError) = needsLineup ? SquadValidator.ValidatePreMatchLineup(
                request.Formation,
                request.StarterPlayerIds,
                request.BenchPlayerIds,
                clubSquad,
                world.AllowClassic505) : TacticalFormations.TryGet(request.Formation, world.AllowClassic505, out _)
                    ? (true, (string?)null) : (false, "Formação tática inválida.");

            if (!isValid)
                return (false, validationError, BuildReadyCounterDto(world), false);

            coach.SubmittedFormation = request.Formation.Trim();
            coach.SubmittedStarterIds = needsLineup ? request.StarterPlayerIds.ToList() : new();
            coach.SubmittedBenchIds = needsLineup ? request.BenchPlayerIds.ToList() : new();
            coach.IsReadyForRound = true;
            coach.LastIdempotencyKey = request.IdempotencyKey;

            if (request.SaveAsPreferred && needsLineup)
            {
                club.PreferredFormation = coach.SubmittedFormation;
                club.PreferredStarterIds = coach.SubmittedStarterIds.ToList();
                club.PreferredBenchIds = coach.SubmittedBenchIds.ToList();
            }

            world.RoundVersion++;
            bool started = CheckAndStartRoundIfAllReadyInternal(world);

            RecordReadinessReceipt(world, "tactics", request.CoachId, request.IdempotencyKey, request);
            await PersistRoundCheckpointAsync(world);
            var counter = BuildReadyCounterDto(world);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, counter, ct);

            if (started)
            {
                var snapshot = BuildLiveRoundSnapshotDto(world);
                await _publisher.PublishRoundStateChangedAsync(world.Id, snapshot, ct);
            }

            return (true, null, counter, started);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Marks an unemployed human participant as ready ("PRONTO PARA ACOMPANHAR").
    /// Unemployed humans are included in the 0/N denominator and can trigger round start at N/N.
    /// </summary>
    public async Task<(bool Success, string? Error, ReadyCounterDto? Counter, bool RoundStarted)> SubmitUnemployedReadyAsync(
        UnemployedReadyRequest request,
        CancellationToken ct = default)
    {
        var sem = GetLock(request.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await GetWorldForReadinessAsync(request.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null, false);

            var receipt = ReadReadinessReceipt(world, "ready", request.CoachId, request.IdempotencyKey, request);
            if (receipt.HasValue)
                return (receipt.Value, receipt.Value ? null : "Chave de operação reutilizada com dados diferentes.", BuildReadyCounterDto(world), false);

            if(world.InitialLobby is {StartedAt:null})
                return (false,"Aguardando o criador iniciar o campeonato no lobby.",BuildReadyCounterDto(world),false);
            if (world.RoundState != RoundState.PREPARING)
                return (false, "A rodada já não está em fase de preparação.", BuildReadyCounterDto(world), false);

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == request.CoachId);
            if (coach == null)
                return (false, "Participante não encontrado na sala.", BuildReadyCounterDto(world), false);

            await RecoverSalaryDecisionsAsync(world, _repository);
            var club = world.Clubs.FirstOrDefault(c => c.Id == coach.CurrentClubId && c.HumanCoachId == coach.Id);
            if (coach.CurrentClubId != null && club == null)
                return (false, "Propriedade do clube inválida.", BuildReadyCounterDto(world), false);
            if (club != null && SalarySeasonTransitionEngine.BlockingReason(world, club.Id) is string salaryError)
                return (false, salaryError, BuildReadyCounterDto(world), false);

            // If coach actually has a club and auto-submits via PRONTO, validate/auto-build their preferred lineup
            if (club != null)
            {
                var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
                var spec = TacticalFormations.Resolve(club.PreferredFormation, world.AllowClassic505);
                var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, spec, world, club.LineupSelectionMode, club.PreferredStarterIds);
                coach.SubmittedFormation = spec.Code;
                coach.SubmittedStarterIds = starters;
                coach.SubmittedBenchIds = bench;
            }

            coach.IsReadyForRound = true;
            coach.LastIdempotencyKey = request.IdempotencyKey;
            world.RoundVersion++;
            bool started = CheckAndStartRoundIfAllReadyInternal(world);

            RecordReadinessReceipt(world, "ready", request.CoachId, request.IdempotencyKey, request);
            await PersistRoundCheckpointAsync(world);
            var counter = BuildReadyCounterDto(world);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, counter, ct);

            if (started)
            {
                await _publisher.PublishRoundStateChangedAsync(world.Id, BuildLiveRoundSnapshotDto(world), ct);
            }

            return (true, null, counter, started);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<(bool Success, string? Error, ReadyCounterDto? Counter)> WithdrawReadyAsync(
        WithdrawReadyRequest request,
        CancellationToken ct = default)
    {
        var sem = GetLock(request.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await GetWorldForReadinessAsync(request.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null);

            var receipt = ReadReadinessReceipt(world, "withdraw", request.CoachId, request.IdempotencyKey, request);
            if (receipt.HasValue)
                return (receipt.Value, receipt.Value ? null : "Chave de operação reutilizada com dados diferentes.", BuildReadyCounterDto(world));

            if(world.InitialLobby is {StartedAt:null})
                return (false,"Aguardando o criador iniciar o campeonato no lobby.",BuildReadyCounterDto(world));
            if (world.RoundState != RoundState.PREPARING)
                return (false, "Não é possível retirar o pronto após o início da rodada.", BuildReadyCounterDto(world));

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == request.CoachId);
            if (coach == null) return (false, "Treinador não encontrado.", BuildReadyCounterDto(world));

            coach.IsReadyForRound = false;
            world.RoundVersion++;
            RecordReadinessReceipt(world, "withdraw", request.CoachId, request.IdempotencyKey, request);
            await PersistRoundCheckpointAsync(world);

            var counter = BuildReadyCounterDto(world);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, counter, ct);
            return (true, null, counter);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<GameWorld?> GetWorldForReadinessAsync(Guid worldId, CancellationToken ct)
    {
        var world = await _repository.GetWorldForRoundCheckpointAsync(worldId, ct);
        if (world?.PendingWorldDataChange != null) throw new RoundCheckpointPendingException();
        if (world?.RoundCheckpointNeedsSave == true)
            await PersistRoundCheckpointAsync(world);
        return world;
    }

    private static bool? ReadReadinessReceipt<T>(GameWorld world, string action, Guid coachId, string key, T request)
    {
        var receiptKey = InteractionKey(action, coachId, key);
        return receiptKey != null && world.MatchInteractionReceipts.TryGetValue(receiptKey, out var receipt)
            ? receipt.Fingerprint == ReadinessFingerprint(request)
            : null;
    }

    private static void RecordReadinessReceipt<T>(GameWorld world, string action, Guid coachId, string key, T request)
        => RecordInteraction(world, InteractionKey(action, coachId, key), ReadinessFingerprint(request));

    private static string ReadinessFingerprint<T>(T request)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request)));

    private static bool NeedsCurrentRoundLineup(GameWorld world, Guid clubId)
        => !ClassicForceEvolutionEngine.IsActive(world) || GetCurrentRoundFixtures(world)
            .Any(m => m.HomeClubId == clubId || m.AwayClubId == clubId);

    // The caller holds the room lock. A club-scoped recovery prepares that club's inbox.
    public static async Task RecoverSalaryDecisionsAsync(GameWorld world, IWorldRepository repository, Guid? clubId = null)
    {
        if (!world.Economy.SalaryRequests.Any(r => r.IsUnresolved) && !world.Economy.SalaryRecoveryNeedsSave) return;
        bool changed = ClassicEconomyEngine.ReconcileSalaryRequests(world, clubId);
        if (clubId == null)
            changed |= ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world) > 0;
        if (changed)
        {
            world.RoundVersion++;
            world.Economy.SalaryRecoveryNeedsSave = true;
        }
        if (world.Economy.SalaryRecoveryNeedsSave)
        {
            await repository.SaveWorldAsync(world, CancellationToken.None);
            world.Economy.SalaryRecoveryNeedsSave = false;
        }
    }

    internal static bool CheckAndStartRoundIfAllReadyInternal(GameWorld world)
    {
        if (world.InitialLobby is {StartedAt:null} || world.InitialLobby!=null && world.HumanCoaches.Count!=world.MaxHumans) return false;
        if (world.RoundState != RoundState.PREPARING) return false;
        if (world.HumanCoaches.Count == 0) return false;
        if(SalarySeasonTransitionEngine.BlockingReason(world)!=null)return false;
        ClassicForceEvolutionEngine.PrepareRound(world);
        DisciplineEngine.EnsureMigrated(world);

        // Auto-ready any human coach explicitly placed in AutoMode by the host
        foreach (var autoHuman in world.HumanCoaches.Where(h => h.IsAutoMode && !h.IsReadyForRound))
        {
            if (autoHuman.CurrentClubId != null)
            {
                var club = world.Clubs.First(c => c.Id == autoHuman.CurrentClubId.Value);
                var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
                var spec = TacticalFormations.Resolve(club.PreferredFormation, world.AllowClassic505);
                var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, spec, world, club.LineupSelectionMode, club.PreferredStarterIds);
                autoHuman.SubmittedFormation = spec.Code;
                autoHuman.SubmittedStarterIds = starters;
                autoHuman.SubmittedBenchIds = bench;
            }
            autoHuman.IsReadyForRound = true;
        }

        bool allReady = world.HumanCoaches.All(h => h.IsReadyForRound);
        if (!allReady) return false;

        // A transfer or career move may have changed a squad after its tactics were submitted.
        // Never start using a player who has left the club; the affected human must submit again.
        foreach (var human in world.HumanCoaches.Where(h => h.CurrentClubId != null))
        {
            if (!NeedsCurrentRoundLineup(world, human.CurrentClubId!.Value)) continue;
            var squad = world.Players.Where(p => p.ClubId == human.CurrentClubId).ToList();
            var valid = SquadValidator.ValidatePreMatchLineup(human.SubmittedFormation ?? "4-4-2",
                human.SubmittedStarterIds, human.SubmittedBenchIds, squad, world.AllowClassic505);
            if (valid.IsValid) continue;
            if (human.IsAutoMode)
            {
                var club = world.Clubs.First(c => c.Id == human.CurrentClubId);
                var spec = TacticalFormations.Resolve(human.SubmittedFormation, world.AllowClassic505);
                var (starters, bench) = AiManagerBrain.BuildStartersAndBench(squad, spec, world, club.LineupSelectionMode, club.PreferredStarterIds);
                human.SubmittedFormation = spec.Code; human.SubmittedStarterIds = starters; human.SubmittedBenchIds = bench;
                if (SquadValidator.ValidatePreMatchLineup(spec.Code, starters, bench, squad, world.AllowClassic505).IsValid) continue;
            }
            human.IsReadyForRound = false;
            CoachCareerEngine.Log(world, "READY_LINEUP_INVALIDATED", human.Id, human.CurrentClubId, valid.ErrorMessage ?? "Elenco alterado.");
        }
        if (world.HumanCoaches.Any(h => !h.IsReadyForRound)) return false;

        // Transition atomically: PREPARING -> ALL_READY -> RUNNING_FIRST_HALF
        world.RoundState = RoundState.ALL_READY;

        CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, world.RoundNumber);
        var roundFixtures = world.Fixtures
            .Where(f => f.SeasonNumber == world.SeasonNumber && f.RoundNumber == world.RoundNumber)
            .OrderBy(f => f.DeterministicMatchIndex)
            .ToList();

        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var humanByClub = world.HumanCoaches
            .Where(h => h.CurrentClubId != null)
            .ToDictionary(h => h.CurrentClubId!.Value);
        var aiDict = world.AiManagers.ToDictionary(m => m.Id);
        var playersByClub = world.Players
            .Where(p => p.ClubId != null)
            .GroupBy(p => p.ClubId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var match in roundFixtures)
        {
            CoachCareerEngine.CaptureLeadership(world, match);
            var homeClub = clubDict[match.HomeClubId];
            var awayClub = clubDict[match.AwayClubId];
            var homeSquad = playersByClub.GetValueOrDefault(homeClub.Id) ?? new List<Player>();
            var awaySquad = playersByClub.GetValueOrDefault(awayClub.Id) ?? new List<Player>();

            // Lock Home lineup (Human submission or AI Manager selection)
            if (humanByClub.TryGetValue(homeClub.Id, out var hCoach) && hCoach.SubmittedStarterIds.Count > 0)
            {
                match.HomeFormation = hCoach.SubmittedFormation ?? homeClub.PreferredFormation;
                match.HomeActiveStarterIds = hCoach.SubmittedStarterIds.ToList();
                match.HomeBenchIds = hCoach.SubmittedBenchIds.ToList();
            }
            else
            {
                var mgr = homeClub.AiManagerId != null ? aiDict.GetValueOrDefault(homeClub.AiManagerId.Value) : null;
                if (mgr != null)
                {
                    var (form, starters, bench, _) = AiManagerBrain.PlanAndSelectLineup(
                        mgr, homeClub, homeSquad, awayClub, awaySquad, isHomeMatch: true,
                        world.Seed, world.SeasonNumber, world.RoundNumber, world.AllowClassic505, world);
                    match.HomeFormation = form;
                    match.HomeActiveStarterIds = starters;
                    match.HomeBenchIds = bench;
                }
                else
                {
                    var (starters, bench) = AiManagerBrain.BuildStartersAndBench(homeSquad, TacticalFormations.All["4-4-2"], world, LineupSelectionMode.Automatic, homeClub.PreferredStarterIds);
                    match.HomeFormation = "4-4-2";
                    match.HomeActiveStarterIds = starters;
                    match.HomeBenchIds = bench;
                }
            }

            // Lock Away lineup (Human submission or AI Manager selection)
            if (humanByClub.TryGetValue(awayClub.Id, out var aCoach) && aCoach.SubmittedStarterIds.Count > 0)
            {
                match.AwayFormation = aCoach.SubmittedFormation ?? awayClub.PreferredFormation;
                match.AwayActiveStarterIds = aCoach.SubmittedStarterIds.ToList();
                match.AwayBenchIds = aCoach.SubmittedBenchIds.ToList();
            }
            else
            {
                var mgr = awayClub.AiManagerId != null ? aiDict.GetValueOrDefault(awayClub.AiManagerId.Value) : null;
                if (mgr != null)
                {
                    var (form, starters, bench, _) = AiManagerBrain.PlanAndSelectLineup(
                        mgr, awayClub, awaySquad, homeClub, homeSquad, isHomeMatch: false,
                        world.Seed, world.SeasonNumber, world.RoundNumber, world.AllowClassic505, world);
                    match.AwayFormation = form;
                    match.AwayActiveStarterIds = starters;
                    match.AwayBenchIds = bench;
                }
                else
                {
                    var (starters, bench) = AiManagerBrain.BuildStartersAndBench(awaySquad, TacticalFormations.All["4-4-2"], world, LineupSelectionMode.Automatic, awayClub.PreferredStarterIds);
                    match.AwayFormation = "4-4-2";
                    match.AwayActiveStarterIds = starters;
                    match.AwayBenchIds = bench;
                }
            }

            // Increment appearance count for starting 11
            var playerMap = world.Players.ToDictionary(p => p.Id);
            foreach (var sId in match.HomeActiveStarterIds.Concat(match.AwayActiveStarterIds))
            {
                if (playerMap.TryGetValue(sId, out var p))
                {
                    p.SeasonMatches++;
                    p.CareerMatches++;
                }
            }

            match.CurrentMinute = 0;
            match.IsFinished = false;
        }

        world.CurrentMatchMinute = 0;
        world.RoundState = RoundState.RUNNING_FIRST_HALF;
        world.RoundVersion++;
        return true;
    }

    /// <summary>
    /// Requests a Global Pause ("PEDIR PARADA") during live match play.
    /// Freezes ALL matches in the room at the exact same minute.
    /// Merges simultaneous pause requests into a single shared PauseWindow.
    /// Enforces the 3-window quota in normal time (4 in extra time).
    /// </summary>
    public async Task<(bool Success, string? Error, PauseWindowStateDto? PauseWindow)> RequestGlobalPauseAsync(
        RequestPauseCommand command,
        CancellationToken ct = default)
    {
        var sem = GetLock(command.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(command.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null);

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == command.CoachId);
            if (coach == null) return (false, "Treinador não pertence à sala.", null);
            var receiptKey = InteractionKey("pause", command.CoachId, command.IdempotencyKey);
            var fingerprint = InteractionFingerprint(command);
            if (receiptKey != null && world.MatchInteractionReceipts.TryGetValue(receiptKey, out var receipt))
            {
                if (receipt.Fingerprint != fingerprint) return (false, "Chave de operação reutilizada com dados diferentes.", null);
                var previous = world.PauseWindows.FirstOrDefault(w => w.Id == receipt.PauseWindowId);
                if (world.RequiresDurableMatchInteractionSave)
                    await _repository.SaveWorldAsync(world, CancellationToken.None);
                return (true, null, previous == null ? null : BuildPauseWindowDto(world, previous) with
                {
                    Snapshot = BuildLiveRoundSnapshotDto(world)
                });
            }
            if (FirstPendingPenalty(world) != null) return (false, "Aguardando a escolha do cobrador do pênalti.", null);
            if (coach.CurrentClubId == null)
                return (false, "Treinador desempregado não pode solicitar parada tática.", null);
            var club = world.Clubs.FirstOrDefault(c => c.Id == coach.CurrentClubId && c.HumanCoachId == coach.Id);
            if (club == null) return (false, "Você não controla o clube desta partida.", null);

            var roundMatches = GetCurrentRoundFixtures(world);
            var coachMatch = roundMatches.FirstOrDefault(m => m.HomeClubId == coach.CurrentClubId || m.AwayClubId == coach.CurrentClubId);
            if (coachMatch == null || coachMatch.IsFinished)
                return (false, "Sua equipe não possui partida em andamento neste momento.", null);

            bool isHome = coachMatch.HomeClubId == coach.CurrentClubId;
            int usedWindows = isHome ? coachMatch.HomeUsedPauseWindows : coachMatch.AwayUsedPauseWindows;
            int maxWindows = isHome ? coachMatch.HomeMaxPauseWindows : coachMatch.AwayMaxPauseWindows;

            // A request can arrive just after another coach pauses or the clock reaches an interval.
            if ((world.RoundState == RoundState.GLOBAL_PAUSED || IsFreeInterval(world.RoundState))
                && world.ActivePauseWindowId != null)
            {
                var existingWindow = world.PauseWindows.First(w => w.Id == world.ActivePauseWindowId.Value);
                RecordInteraction(world, receiptKey, fingerprint, existingWindow.Id);
                world.RoundVersion++;
                await _repository.SaveWorldAsync(world, CancellationToken.None);
                var snapshot = BuildLiveRoundSnapshotDto(world);
                var dto = snapshot.ActivePauseWindow!;
                if (world.RoundState == RoundState.GLOBAL_PAUSED)
                    await _publisher.PublishGlobalPauseAsync(world.Id, dto, snapshot, ct);
                return (true, null, dto with { Snapshot = snapshot });
            }

            if (world.RoundState is not (RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME))
            {
                return (false, $"Não é possível solicitar parada no estado {world.RoundState}.", null);
            }

            if (usedWindows >= maxWindows)
            {
                return (false, $"Limite de oportunidades de parada ({usedWindows}/{maxWindows}) esgotado.", null);
            }

            // Create the single shared PauseWindow and freeze the entire room
            var window = new PauseWindow
            {
                Id = Guid.NewGuid(),
                GameWorldId = world.Id,
                SeasonNumber = world.SeasonNumber,
                RoundNumber = world.RoundNumber,
                Minute = world.CurrentMatchMinute,
                PauseType = RoundState.GLOBAL_PAUSED,
                RequestedByCoachId = coach.Id,
                RequestedByCoachName = coach.Nickname,
                OpenedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(world.PauseTimeoutSeconds)
            };
            RecordInteraction(world, receiptKey, fingerprint, window.Id);

            world.PauseWindows.Add(window);
            world.ActivePauseWindowId = window.Id;
            world.RoundState = RoundState.GLOBAL_PAUSED;
            world.RoundVersion++;

            coachMatch.Events.Add(new MatchEventRecord
            {
                Minute = world.CurrentMatchMinute,
                EventType = MatchEventType.GlobalPauseTriggered,
                ClubId = club.Id,
                ClubName = club.Name,
                Description = $"{world.CurrentMatchMinute}' PARADA GLOBAL solicitada por {coach.Nickname} ({club.ShortName})."
            });

            await _repository.SaveWorldAsync(world, CancellationToken.None);
            var snapDto = BuildLiveRoundSnapshotDto(world);
            var winDto = snapDto.ActivePauseWindow!;
            await _publisher.PublishGlobalPauseAsync(world.Id, winDto, snapDto, ct);
            return (true, null, winDto with { Snapshot = snapDto });
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Submits tactical/substitution adjustments during a Global Pause or Free Interval (Halftime 45', Extra Time 90', 105').
    /// If another coach edits during someone else's Global Pause, they consume 1 of their own pause opportunities.
    /// If they only observe/confirm, they consume 0.
    /// </summary>
    public async Task<(bool Success, string? Error, LiveRoundSnapshotDto? Snapshot)> SubmitInMatchAdjustmentAsync(
        SubmitInMatchAdjustmentCommand command,
        CancellationToken ct = default)
    {
        var sem = GetLock(command.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(command.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", null);

            var receiptKey = InteractionKey("adjust", command.CoachId, command.IdempotencyKey);
            var fingerprint = InteractionFingerprint(command);
            if (receiptKey != null && world.MatchInteractionReceipts.TryGetValue(receiptKey, out var receipt))
                return receipt.Fingerprint == fingerprint ? (true, null, BuildLiveRoundSnapshotDto(world)) : (false, "Chave de operação reutilizada com dados diferentes.", BuildLiveRoundSnapshotDto(world));
            if (FirstPendingPenalty(world) != null) return (false, "Aguardando a cobrança do pênalti.", BuildLiveRoundSnapshotDto(world));
            if (world.RoundState is not (RoundState.GLOBAL_PAUSED or RoundState.HALFTIME_PAUSED or RoundState.EXTRA_TIME_BREAK or RoundState.EXTRA_TIME_HALFTIME))
            {
                return (false, "Ajustes em partida só podem ser enviados durante pausa global ou intervalo.", BuildLiveRoundSnapshotDto(world));
            }

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == command.CoachId);
            if (coach == null || coach.CurrentClubId == null)
                return (false, "Treinador sem clube em partida.", BuildLiveRoundSnapshotDto(world));

            var match = world.Fixtures.FirstOrDefault(f =>
                f.Id == command.MatchId &&
                f.SeasonNumber == world.SeasonNumber &&
                f.RoundNumber == world.RoundNumber);

            if (match == null || match.IsFinished || (match.HomeClubId != coach.CurrentClubId && match.AwayClubId != coach.CurrentClubId))
                return (false, "Partida inválida ou não pertence ao seu clube.", BuildLiveRoundSnapshotDto(world));

            bool isHome = match.HomeClubId == coach.CurrentClubId;
            var club = world.Clubs.First(c => c.Id == coach.CurrentClubId.Value);
            var playerMap = world.Players.ToDictionary(p => p.Id);

            var (ok, err, newEvents) = MatchSimulationEngine.ApplyInMatchAdjustments(
                match,
                club,
                isHome,
                world.RoundState,
                world.ActivePauseWindowId,
                command.NewFormation,
                command.Substitutions,
                playerMap,
                world.AllowClassic505);

            if (!ok)
                return (false, err, BuildLiveRoundSnapshotDto(world));

            if (world.ActivePauseWindowId != null)
            {
                var win = world.PauseWindows.FirstOrDefault(w => w.Id == world.ActivePauseWindowId.Value);
                if (win != null && newEvents.Count > 0)
                {
                    win.EditedCoachIds.Add(coach.Id);
                    win.ConfirmedCoachIds.Remove(coach.Id);
                }
            }

            RecordInteraction(world, receiptKey, fingerprint, world.ActivePauseWindowId);

            world.RoundVersion++;
            await _repository.SaveWorldAsync(world, ct);
            var snapshot = BuildLiveRoundSnapshotDto(world);
            await _publisher.PublishRoundStateChangedAsync(world.Id, snapshot, ct);
            return (true, null, snapshot);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Confirms readiness to resume play ("CONTINUAR" / "PRONTO PARA O 2º TEMPO").
    /// Coaches who did not edit during a Global Pause do NOT spend a pause opportunity.
    /// When all humans confirm, the room resumes play automatically.
    /// </summary>
    public async Task<(bool Success, string? Error, bool Resumed, LiveRoundSnapshotDto? Snapshot)> ConfirmContinueMatchAsync(
        ConfirmContinueMatchCommand command,
        CancellationToken ct = default)
    {
        var sem = GetLock(command.GameWorldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(command.GameWorldId, ct);
            if (world == null) return (false, "Sala não encontrada.", false, null);

            var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == command.CoachId);
            if (coach == null)
                return (false, "Participante não encontrado na sala.", false, BuildLiveRoundSnapshotDto(world));

            var receiptKey = InteractionKey("continue", command.CoachId, command.IdempotencyKey);
            if (receiptKey == null)
                return (false, "Informe a chave da operação.", false, BuildLiveRoundSnapshotDto(world));
            var fingerprint = InteractionFingerprint(command);
            if (world.MatchInteractionReceipts.TryGetValue(receiptKey, out var receipt))
            {
                var current = BuildLiveRoundSnapshotDto(world);
                if (receipt.Fingerprint != fingerprint)
                    return (false, "Chave de operação reutilizada com dados diferentes.", false, current);
                // Also deliver a snapshot if the original request lost its response after commit.
                await _publisher.PublishRoundStateChangedAsync(world.Id, current, CancellationToken.None);
                return (true, null, world.ActivePauseWindowId != receipt.PauseWindowId, current);
            }
            if (world.ActivePauseWindowId == null)
                return (false, "Não há janela de pausa ou intervalo ativa.", false, BuildLiveRoundSnapshotDto(world));
            if (command.PauseWindowId is Guid expectedWindow && world.ActivePauseWindowId != expectedWindow)
                return (false, "Esta pausa já terminou. Atualize a partida para continuar.", false, BuildLiveRoundSnapshotDto(world));

            var window = world.PauseWindows.First(w => w.Id == world.ActivePauseWindowId.Value);
            if (window.ConfirmedCoachIds.Contains(coach.Id))
                return (true, null, false, BuildLiveRoundSnapshotDto(world));

            bool resumed = await SavePauseTransitionAsync(world, window, receiptKey, () =>
            {
                window.ConfirmedCoachIds.Add(coach.Id);
                RecordInteraction(world, receiptKey, fingerprint, window.Id);
                AutoConfirmPauseObservers(world, window);
                if (world.HumanCoaches.All(h => window.ConfirmedCoachIds.Contains(h.Id)))
                    ResumeFromPauseWindowInternal(world, window);
            }, ct);
            var snapshot = BuildLiveRoundSnapshotDto(world);
            await _publisher.PublishRoundStateChangedAsync(world.Id, snapshot, CancellationToken.None);
            return (true, null, resumed, snapshot);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task<bool> ForceExpirePauseWindowIfTimedOutAsync(Guid worldId, bool ignoreClockForTest = false, CancellationToken ct = default)
    {
        var sem = GetLock(worldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(worldId, ct);
            if (world?.ActivePauseWindowId == null) return false;

            var window = world.PauseWindows.FirstOrDefault(w => w.Id == world.ActivePauseWindowId.Value);
            if (window == null || window.IsClosed) return false;

            bool intervalReady = IsFreeInterval(window.PauseType) && IntervalDecisionCoaches(world)
                .All(h => window.ConfirmedCoachIds.Contains(h.Id));
            if (!ignoreClockForTest && DateTime.UtcNow < window.ExpiresAtUtc && !intervalReady)
                return false;

            await SavePauseTransitionAsync(world, window, null, () =>
            {
                AutoConfirmPauseObservers(world, window);
                ResumeFromPauseWindowInternal(world, window);
            }, ct);
            await _publisher.PublishRoundStateChangedAsync(world.Id, BuildLiveRoundSnapshotDto(world), CancellationToken.None);
            return true;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<bool> SavePauseTransitionAsync(
        GameWorld world, PauseWindow window, string? receiptKey, Action apply, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var confirmed = window.ConfirmedCoachIds.ToHashSet();
        var state = world.RoundState;
        var wasClosed = window.IsClosed;
        var version = world.RoundVersion;
        var updated = world.UpdatedAtUtc;
        var needsSave = world.RequiresDurableMatchInteractionSave;
        try
        {
            apply();
            world.RoundVersion++;
            await _repository.SavePauseTransitionAsync(world, version);
            return window.IsClosed;
        }
        catch
        {
            window.ConfirmedCoachIds = confirmed;
            window.IsClosed = wasClosed;
            world.ActivePauseWindowId = window.Id;
            world.RoundState = state;
            world.RoundVersion = version;
            world.UpdatedAtUtc = updated;
            world.RequiresDurableMatchInteractionSave = needsSave;
            if (receiptKey != null) world.MatchInteractionReceipts.Remove(receiptKey);
            throw;
        }
    }

    private static void ResumeFromPauseWindowInternal(GameWorld world, PauseWindow window)
    {
        window.IsClosed = true;
        world.ActivePauseWindowId = null;
        world.RequiresDurableMatchInteractionSave = true;

        int m = world.CurrentMatchMinute;
        if (window.PauseType == RoundState.HALFTIME_PAUSED || m == 45)
        {
            world.RoundState = RoundState.RUNNING_SECOND_HALF;
        }
        else if (window.PauseType == RoundState.EXTRA_TIME_BREAK || m == 90)
        {
            world.RoundState = RoundState.RUNNING_EXTRA_TIME;
        }
        else if (window.PauseType == RoundState.EXTRA_TIME_HALFTIME || m == 105)
        {
            world.RoundState = RoundState.RUNNING_EXTRA_TIME;
        }
        else if (m < 45)
        {
            world.RoundState = RoundState.RUNNING_FIRST_HALF;
        }
        else if (m < 90)
        {
            world.RoundState = RoundState.RUNNING_SECOND_HALF;
        }
        else
        {
            world.RoundState = RoundState.RUNNING_EXTRA_TIME;
        }
    }

    /// <summary>
    /// Advances the synchronized match clock by 1 virtual minute across ALL active matches of the current round.
    /// Stops automatically at 45' (Halftime), 90' (End of normal time / Extra Time Break), 105' (Extra Time Halftime),
    /// and 120' (Penalty Shootout & End of Extra Time), or immediately if a Global Pause is active.
    /// </summary>
    public async Task<LiveRoundSnapshotDto?> AdvanceClockTickAsync(Guid worldId, CancellationToken ct = default)
    {
        var sem = GetLock(worldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldForRoundCheckpointAsync(worldId, ct);
            if (world == null) return null;

            if (world.PendingWorldDataChange != null)
            {
                await ClubAndMarketApplicationService.ConfirmWorldDataChangeAsync(world, _repository, _publisher);
                return BuildLiveRoundSnapshotDto(world);
            }

            if (world.RoundCheckpointNeedsSave)
            {
                await PersistRoundCheckpointAsync(world);
                var recovered = BuildLiveRoundSnapshotDto(world);
                await _publisher.PublishRoundStateChangedAsync(world.Id, recovered, ct);
                return recovered;
            }

            if (world.RoundState is not (RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME))
            {
                return BuildLiveRoundSnapshotDto(world);
            }

            if (FirstPendingPenalty(world) != null) return BuildLiveRoundSnapshotDto(world);
            var simulationStart = System.Diagnostics.Stopwatch.GetTimestamp();
            world.CurrentMatchMinute++;
            int minute = world.CurrentMatchMinute;

            var roundMatches = GetCurrentRoundFixtures(world);
            var clubDict = world.Clubs.ToDictionary(c => c.Id);
            var aiDict = world.AiManagers.ToDictionary(m => m.Id);
            var playerMap = world.Players.ToDictionary(p => p.Id);

            foreach (var match in roundMatches.Where(m => !m.IsFinished))
            {
                match.CurrentMinute = minute;
                var homeClub = clubDict[match.HomeClubId];
                var awayClub = clubDict[match.AwayClubId];
                var homeMgr = homeClub.AiManagerId != null ? aiDict.GetValueOrDefault(homeClub.AiManagerId.Value) : null;
                var awayMgr = awayClub.AiManagerId != null ? aiDict.GetValueOrDefault(awayClub.AiManagerId.Value) : null;

                var homeIdentity = homeMgr?.Identity ?? ManagerTacticalIdentity.Equilibrado;
                var awayIdentity = awayMgr?.Identity ?? ManagerTacticalIdentity.Equilibrado;

                MatchSimulationEngine.SimulateSingleMinute(
                    match, homeClub, awayClub, playerMap, homeIdentity, awayIdentity, world.Seed, world.AllowClassic505,
                    homeHumanPenaltyChoice: RequiresHumanPenaltyChoice(world, homeClub),
                    awayHumanPenaltyChoice: RequiresHumanPenaltyChoice(world, awayClub));

                if (match.PendingPenalties.Count > 0) continue;
                // AI in-match reactive adjustments (never opens a global pause window!)
                if (homeClub.HumanCoachId == null)
                {
                    AiManagerBrain.EvaluateAiInMatchReaction(match, homeClub, homeMgr, isHomeTeam: true, world.RoundState, playerMap, world.AllowClassic505, world);
                }
                if (awayClub.HumanCoachId == null)
                {
                    AiManagerBrain.EvaluateAiInMatchReaction(match, awayClub, awayMgr, isHomeTeam: false, world.RoundState, playerMap, world.AllowClassic505, world);
                }
            }

            if (FirstPendingPenalty(world) == null) ApplyCurrentMinuteBoundary(world);

            var persistenceStart = System.Diagnostics.Stopwatch.GetTimestamp();
            world.RoundVersion++;
            if (world.RoundState == RoundState.RESULTS)
                await PersistRoundCheckpointAsync(world);
            else
            {
                world.RoundCheckpointId = Guid.NewGuid();
                try
                {
                    await _repository.SaveWorldAsync(world, ct);
                }
                catch
                {
                    // The minute has already mutated the aggregate. Retry its exact state
                    // before a subsequent tick can simulate another minute.
                    world.RequiresDurableMatchInteractionSave = true;
                    world.RoundCheckpointNeedsSave = true;
                    throw;
                }
            }
            var synchronizationStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var snapshot = BuildLiveRoundSnapshotDto(world);
            await _publisher.PublishClockTickAsync(world.Id, snapshot, ct);
            _tickTiming?.Record(
                System.Diagnostics.Stopwatch.GetElapsedTime(simulationStart, persistenceStart).TotalMilliseconds,
                System.Diagnostics.Stopwatch.GetElapsedTime(persistenceStart, synchronizationStart).TotalMilliseconds,
                System.Diagnostics.Stopwatch.GetElapsedTime(synchronizationStart).TotalMilliseconds);
            return snapshot;
        }
        finally
        {
            sem.Release();
        }
    }

    private static void EvaluateAllAiTeamsAtFreeInterval(
        GameWorld world,
        List<MatchFixture> matches,
        Dictionary<Guid, Club> clubDict,
        Dictionary<Guid, AiManager> aiDict,
        Dictionary<Guid, Player> playerMap)
    {
        foreach (var match in matches.Where(m => !m.IsFinished))
        {
            var home = clubDict[match.HomeClubId];
            var away = clubDict[match.AwayClubId];
            if (home.HumanCoachId == null)
            {
                var mgr = home.AiManagerId != null ? aiDict.GetValueOrDefault(home.AiManagerId.Value) : null;
                AiManagerBrain.EvaluateAiInMatchReaction(match, home, mgr, true, world.RoundState, playerMap, world.AllowClassic505, world);
            }
            if (away.HumanCoachId == null)
            {
                var mgr = away.AiManagerId != null ? aiDict.GetValueOrDefault(away.AiManagerId.Value) : null;
                AiManagerBrain.EvaluateAiInMatchReaction(match, away, mgr, false, world.RoundState, playerMap, world.AllowClassic505, world);
            }
        }
    }

    private static bool IsFreeInterval(RoundState state) => state is
        RoundState.HALFTIME_PAUSED or RoundState.EXTRA_TIME_BREAK or RoundState.EXTRA_TIME_HALFTIME;

    private static List<HumanCoach> IntervalDecisionCoaches(GameWorld world)
    {
        var activeClubs = GetCurrentRoundFixtures(world).Where(m => !m.IsFinished)
            .SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).ToHashSet();
        return world.HumanCoaches.Where(h => !h.IsAutoMode && h.CurrentClubId is Guid clubId && activeClubs.Contains(clubId)).ToList();
    }

    private static void AutoConfirmPauseObservers(GameWorld world, PauseWindow window)
    {
        // Spectators cannot edit an interval lineup and must not hold up those who can.
        var decisionCoaches = IsFreeInterval(window.PauseType)
            ? IntervalDecisionCoaches(world).Select(h => h.Id).ToHashSet()
            : world.HumanCoaches.Where(h => !h.IsAutoMode).Select(h => h.Id).ToHashSet();
        foreach (var coach in world.HumanCoaches.Where(h => !decisionCoaches.Contains(h.Id)))
            window.ConfirmedCoachIds.Add(coach.Id);
    }

    private static void OpenFreeIntervalPauseWindow(GameWorld world, RoundState pauseState, int minute)
    {
        var window = new PauseWindow
        {
            Id = Guid.NewGuid(),
            GameWorldId = world.Id,
            SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber,
            Minute = minute,
            PauseType = pauseState,
            RequestedByCoachId = null,
            RequestedByCoachName = pauseState == RoundState.HALFTIME_PAUSED ? "Intervalo (45')" : $"Intervalo Prorrogação ({minute}')",
            OpenedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(world.PauseTimeoutSeconds)
        };

        AutoConfirmPauseObservers(world, window);

        world.PauseWindows.Add(window);
        world.ActivePauseWindowId = window.Id;
        world.RoundState = pauseState;

        if (world.HumanCoaches.All(h => window.ConfirmedCoachIds.Contains(h.Id)))
        {
            ResumeFromPauseWindowInternal(world, window);
        }
    }

    private static void CompleteAllMatchesAndTransitionToResults(GameWorld world, List<MatchFixture> roundMatches)
    {
        world.RoundState = RoundState.RESULTS;
        world.ActivePauseWindowId = null;

        // 1. Settle gate receipts, stadium costs, salaries, contract renewals/expirations
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, roundMatches);

        // 2. Update standings, coach career points, and job invitations
        CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, roundMatches);
        ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world);

        // 3. Process weekly AI market batch (listings, transfers, auctions)
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
    }

    private async Task PersistRoundCheckpointAsync(GameWorld world)
    {
        bool retry = world.RoundCheckpointNeedsSave;
        if (!retry) world.RoundCheckpointId = Guid.NewGuid();
        world.RoundCheckpointNeedsSave = true;
        if (!retry && world.RoundState == RoundState.RESULTS)
        {
            try
            {
                await _publisher.PublishRoundCheckpointPendingAsync(world.Id, world.RoundVersion, CancellationToken.None);
            }
            catch
            {
                // Reconciliation can observe the pending checkpoint even if SignalR is unavailable.
            }
        }
        await _repository.SaveRoundCheckpointAsync(world, retry);
        world.RoundCheckpointNeedsSave = false;
    }

    /// <summary>
    /// Advances the room from RoundState.RESULTS to the next round's RoundState.PREPARING
    /// (or concludes the season and starts Season + 1 if it was the final round).
    /// Resets the human ReadyCounter back to 0/N.
    /// </summary>
    public async Task<LiveRoundSnapshotDto?> AdvanceToNextRoundPreparationAsync(Guid worldId, CancellationToken ct = default, int? expectedSeasonNumber = null, int? expectedRoundNumber = null)
    {
        var sem = GetLock(worldId);
        await sem.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldForRoundCheckpointAsync(worldId, ct);
            if (world == null) return null;
            if (world.PendingWorldDataChange != null) throw new RoundCheckpointPendingException();

            if (world.RoundCheckpointNeedsSave)
            {
                await PersistRoundCheckpointAsync(world);
                var recovered = BuildLiveRoundSnapshotDto(world);
                await _publisher.PublishRoundStateChangedAsync(world.Id, recovered, ct);
                return recovered;
            }

            // A delayed automatic request must never advance a later completed round.
            if ((expectedSeasonNumber.HasValue && world.SeasonNumber != expectedSeasonNumber) ||
                (expectedRoundNumber.HasValue && world.RoundNumber != expectedRoundNumber))
            {
                return BuildLiveRoundSnapshotDto(world);
            }

            if (world.RoundState is not (RoundState.RESULTS or RoundState.FINISHED))
            {
                return BuildLiveRoundSnapshotDto(world);
            }

            await RecoverSalaryDecisionsAsync(world, _repository);
            SalarySeasonTransitionEngine.RequireResolved(world);

            foreach (var h in world.HumanCoaches)
            {
                h.IsReadyForRound = false;
                h.SubmittedFormation = null;
                h.SubmittedStarterIds.Clear();
                h.SubmittedBenchIds.Clear();
            }

            if (world.RoundNumber >= world.TotalRoundsInSeason)
            {
                CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(world,world.SeasonNumber);
            }
            else
            {
                world.RoundNumber++;
                world.CurrentMatchMinute = 0;
                world.RoundState = RoundState.PREPARING;
                CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, world.RoundNumber);
            }

            ClassicForceEvolutionEngine.PrepareRound(world);
            // Refresh AI squad plans and ensure human-controlled clubs have healthy default starters/bench for the new round
            var aiDict = world.AiManagers.ToDictionary(m => m.Id);
            var squads = world.Players.ToLookup(player => player.ClubId);
            foreach (var club in world.Clubs.Where(c => c.DivisionTier != DivisionTier.Distrital))
            {
                var squad = squads[club.Id].ToList();
                if (club.HumanCoachId == null && club.AiManagerId != null && aiDict.TryGetValue(club.AiManagerId.Value, out var mgr))
                {
                    AiManagerBrain.PlanAndSelectLineup(mgr, club, squad, null, null, true, world.Seed, world.SeasonNumber, world.RoundNumber, world.AllowClassic505, world);
                }
                else if (club.HumanCoachId != null)
                {
                    TacticalFormations.TryGet(club.PreferredFormation, world.AllowClassic505, out var spec);
                    var (s, b) = AiManagerBrain.BuildStartersAndBench(squad, spec ?? TacticalFormations.All["4-4-2"], world, club.LineupSelectionMode, club.PreferredStarterIds);
                    club.PreferredStarterIds = s;
                    club.PreferredBenchIds = b;
                }
            }

            world.RoundVersion++;
            await PersistRoundCheckpointAsync(world);
            var snapshot = BuildLiveRoundSnapshotDto(world);
            await _publisher.PublishRoundStateChangedAsync(world.Id, snapshot, CancellationToken.None);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, snapshot.ReadyCounter, CancellationToken.None);
            return snapshot;
        }
        finally
        {
            sem.Release();
        }
    }

    public static List<MatchFixture> GetCurrentRoundFixtures(GameWorld world)
        => world.Fixtures
            .Where(f => f.SeasonNumber == world.SeasonNumber && f.RoundNumber == world.RoundNumber)
            .OrderBy(f => f.DeterministicMatchIndex)
            .ToList();

    public static ReadyCounterDto BuildReadyCounterDto(GameWorld world)
    {
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var roundMatches = GetCurrentRoundFixtures(world);
        int totalHumans = world.HumanCoaches.Count;
        int readyCount = world.HumanCoaches.Count(h => h.IsReadyForRound);

        string prefix = world.RoundState switch
        {
            RoundState.HALFTIME_PAUSED => "INTERVALO",
            RoundState.GLOBAL_PAUSED => "AJUSTES",
            _ => "TÁTICAS ENVIADAS"
        };

        var items = new List<ParticipantReadyItemDto>(totalHumans);
        foreach (var h in world.HumanCoaches)
        {
            Club? club = h.CurrentClubId != null ? clubDict.GetValueOrDefault(h.CurrentClubId.Value) : null;
            var match = club != null
                ? roundMatches.FirstOrDefault(m => m.HomeClubId == club.Id || m.AwayClubId == club.Id)
                : null;
            bool isHome = match != null && match.HomeClubId == club?.Id;

            int usedSubs = match == null ? 0 : (isHome ? match.HomeUsedSubstitutions : match.AwayUsedSubstitutions);
            int maxSubs = match == null ? 5 : (isHome ? match.HomeMaxSubstitutions : match.AwayMaxSubstitutions);
            int usedWins = match == null ? 0 : (isHome ? match.HomeUsedPauseWindows : match.AwayUsedPauseWindows);
            int maxWins = match == null ? 3 : (isHome ? match.HomeMaxPauseWindows : match.AwayMaxPauseWindows);

            items.Add(new ParticipantReadyItemDto(
                CoachId: h.Id,
                Nickname: h.Nickname,
                ClubId: club?.Id,
                ClubName: club?.Name ?? "Desempregado",
                IsUnemployed: h.IsUnemployed,
                IsReady: h.IsReadyForRound,
                IsConnected: h.IsConnected,
                IsAutoMode: h.IsAutoMode,
                UsedSubstitutions: usedSubs,
                MaxSubstitutions: maxSubs,
                UsedPauseWindows: usedWins,
                MaxPauseWindows: maxWins,
                Morale: h.Career.Morale,
                MoraleBand: world.CoachMoraleRules.Band(h.Career.Morale)));
        }

        return new ReadyCounterDto(
            GameWorldId: world.Id,
            SeasonNumber: world.SeasonNumber,
            RoundNumber: world.RoundNumber,
            RoundState: world.RoundState,
            RoundVersion: world.RoundVersion,
            ReadyCount: readyCount,
            TotalHumans: totalHumans,
            CounterLabel: $"{prefix}: {readyCount}/{totalHumans}",
            Participants: items);
    }

    public static PauseWindowStateDto BuildPauseWindowDto(GameWorld world, PauseWindow window)
    {
        int remainingSec = Math.Max(0, (int)Math.Ceiling((window.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds));
        return new PauseWindowStateDto(
            PauseWindowId: window.Id,
            GameWorldId: world.Id,
            Minute: window.Minute,
            PauseType: window.PauseType,
            RequestedByCoachId: window.RequestedByCoachId,
            RequestedByCoachName: window.RequestedByCoachName,
            ConfirmedCount: window.ConfirmedCoachIds.Count,
            TotalHumans: world.HumanCoaches.Count,
            RemainingSeconds: remainingSec,
            ConfirmedCoachIds: window.ConfirmedCoachIds.ToList(),
            EditedCoachIds: window.EditedCoachIds.ToList());
    }

    public static LiveRoundSnapshotDto BuildLiveRoundSnapshotDto(GameWorld world)
    {
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var playerDict = world.Players.ToDictionary(p => p.Id);
        var roundMatches = GetCurrentRoundFixtures(world);
        var compType = CompetitionAndSeasonEngine.GetCompetitionTypeForRound(world, world.RoundNumber);

        PauseWindowStateDto? pauseDto = null;
        if (world.ActivePauseWindowId != null)
        {
            var win = world.PauseWindows.FirstOrDefault(w => w.Id == world.ActivePauseWindowId.Value);
            if (win != null) pauseDto = BuildPauseWindowDto(world, win);
        }

        // For 128+ clubs, during preliminary district rounds, include human matches + top 32 matches so client stays fast
        var matchesToProject = roundMatches
            .OrderByDescending(m => clubDict[m.HomeClubId].HumanCoachId != null || clubDict[m.AwayClubId].HumanCoachId != null)
            .ThenBy(m => m.DivisionTier ?? DivisionTier.Distrital)
            .ThenBy(m => m.DeterministicMatchIndex)
            .Take(32)
            .ToList();

        var matchDtos = new List<LiveMatchSummaryDto>(matchesToProject.Count);
        foreach (var m in matchesToProject)
        {
            var home = clubDict[m.HomeClubId];
            var away = clubDict[m.AwayClubId];

            List<MatchLineupPlayerDto> MapLineup(
                List<Guid> ids,
                Dictionary<Guid, int> yellows,
                List<Guid> sentOff,
                List<Guid> injured,
                List<Guid> subOut,
                List<Guid> subIn)
            {
                return ids.Where(playerDict.ContainsKey).Select(id =>
                {
                    var p = playerDict[id];
                    return new MatchLineupPlayerDto(
                        p.Id,
                        p.DisplayName,
                        p.Position,
                        p.Strength,
                        p.Behavior.ToDisplayPt(),
                        p.IsStar,
                        yellows.GetValueOrDefault(p.Id, 0),
                        sentOff.Contains(p.Id),
                        injured.Contains(p.Id),
                        subOut.Contains(p.Id),
                        subIn.Contains(p.Id))
                    {
                        IsOnPitch = (m.HomeActiveStarterIds.Contains(id)||m.AwayActiveStarterIds.Contains(id))
                            && !sentOff.Contains(id)&&!injured.Contains(id)&&!subOut.Contains(id)
                    };
                }).ToList();
            }

            bool started=m.CurrentMinute>0||m.StatisticsLineupCaptured;
            var homeStarterSource = started ? m.HomeActiveStarterIds.Concat(m.HomeInjuryVacancies).Concat(m.HomeSentOffIds).Distinct().ToList() : home.PreferredStarterIds;
            var homeBenchSource = started ? m.HomeBenchIds : home.PreferredBenchIds;
            var awayStarterSource = started ? m.AwayActiveStarterIds.Concat(m.AwayInjuryVacancies).Concat(m.AwaySentOffIds).Distinct().ToList() : away.PreferredStarterIds;
            var awayBenchSource = started ? m.AwayBenchIds : away.PreferredBenchIds;

            matchDtos.Add(new LiveMatchSummaryDto(
                MatchId: m.Id,
                CompetitionType: m.CompetitionType,
                DivisionTier: m.DivisionTier,
                HomeClubId: home.Id,
                HomeClubName: home.Name,
                HomePrimaryColor: home.PrimaryColorHex,
                HomeSecondaryColor: home.SecondaryColorHex,
                AwayClubId: away.Id,
                AwayClubName: away.Name,
                AwayPrimaryColor: away.PrimaryColorHex,
                AwaySecondaryColor: away.SecondaryColorHex,
                HomeGoals: m.HomeGoals,
                AwayGoals: m.AwayGoals,
                HomePenaltyGoals: m.HomePenaltyGoals,
                AwayPenaltyGoals: m.AwayPenaltyGoals,
                Minute: m.CurrentMinute,
                IsFinished: m.IsFinished,
                IsExtraTime: m.EnteredExtraTime,
                HomeFormation: m.HomeFormation,
                AwayFormation: m.AwayFormation,
                HomeSubCount: m.HomeUsedSubstitutions,
                HomeMaxSubs: m.HomeMaxSubstitutions,
                HomePauseWindowsUsed: m.HomeUsedPauseWindows,
                HomeMaxPauseWindows: m.HomeMaxPauseWindows,
                AwaySubCount: m.AwayUsedSubstitutions,
                AwayMaxSubs: m.AwayMaxSubstitutions,
                AwayPauseWindowsUsed: m.AwayUsedPauseWindows,
                AwayMaxPauseWindows: m.AwayMaxPauseWindows,
                Attendance: m.Attendance,
                TicketPrice: m.TicketPrice,
                TicketRevenue: m.TicketRevenue,
                Events: m.Events.Select(e => new MatchEventDto(
                    e.Minute, e.EventType, e.ClubId, e.ClubName,
                    e.PrimaryPlayerId, e.PrimaryPlayerDisplayName,
                    e.SecondaryPlayerId, e.SecondaryPlayerDisplayName,
                    e.Description) { PenaltyKick = e.PenaltyKick, IsOwnGoal = e.IsOwnGoal }).ToList(),
                HomeStarters: MapLineup(homeStarterSource, m.HomeYellowCardsInMatch, m.HomeSentOffIds, m.HomeInjuredInMatchIds, m.HomeSubstitutedOutIds, m.HomeSubstitutedInIds),
                HomeBench: MapLineup(homeBenchSource, m.HomeYellowCardsInMatch, m.HomeSentOffIds, m.HomeInjuredInMatchIds, m.HomeSubstitutedOutIds, m.HomeSubstitutedInIds),
                AwayStarters: MapLineup(awayStarterSource, m.AwayYellowCardsInMatch, m.AwaySentOffIds, m.AwayInjuredInMatchIds, m.AwaySubstitutedOutIds, m.AwaySubstitutedInIds),
                AwayBench: MapLineup(awayBenchSource, m.AwayYellowCardsInMatch, m.AwaySentOffIds, m.AwayInjuredInMatchIds, m.AwaySubstitutedOutIds, m.AwaySubstitutedInIds))
            {
                HomeColorPalette = home.ColorPalette,
                AwayColorPalette = away.ColorPalette,
                HomePlayersOnPitch = started?m.HomeActiveStarterIds.Count:homeStarterSource.Count,
                AwayPlayersOnPitch = started?m.AwayActiveStarterIds.Count:awayStarterSource.Count
            });
        }

        return new LiveRoundSnapshotDto(
            GameWorldId: world.Id,
            WorldName: world.Name,
            SeasonNumber: world.SeasonNumber,
            RoundNumber: world.RoundNumber,
            CompetitionType: compType,
            CompetitionLabel: compType.ToDisplayPt(),
            RoundState: world.RoundState,
            RoundVersion: world.RoundVersion,
            CurrentMinute: world.CurrentMatchMinute,
            ReadyCounter: BuildReadyCounterDto(world),
            ActivePauseWindow: pauseDto,
            Matches: matchDtos)
        {
            HasInitialLobby=world.InitialLobby!=null,
            LobbyStarted=world.InitialLobby?.StartedAt!=null,
            GameModeId=world.GameModeId,
            PendingPenalty = BuildPendingPenaltyDto(world),
            TotalRoundsInSeason = world.TotalRoundsInSeason,
            ForceEvolutionMode = world.ForceEvolutionMode,
            DisciplineRulesVersion = world.DisciplineRulesVersion,
            ForceRulesVersion = ClassicForceEvolutionEngine.IsActive(world) ? world.ClassicForce.Version : 0,
            PersistedRoundVersion = world.PersistedRoundVersion,
            GameDate = ClassicForceEvolutionEngine.IsActive(world) ? world.ClassicForce.GameDate : null
        };
    }
}
