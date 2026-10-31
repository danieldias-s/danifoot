using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    private readonly IWorldRepository _repository;
    private readonly IMatchRoomNotificationPublisher _publisher;

    public ClubAndMarketApplicationService(
        IWorldRepository repository,
        IMatchRoomNotificationPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<ClubDetailDto?> GetClubDetailAsync(Guid worldId, Guid clubId, CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return null;

        var club = world.Clubs.FirstOrDefault(c => c.Id == clubId);
        if (club == null) return null;

        var squad = world.Players
            .Where(p => p.ClubId == club.Id)
            .OrderBy(p => p.Position)
            .ThenByDescending(p => p.Strength)
            .ToList();

        var human = club.HumanCoachId != null ? world.HumanCoaches.FirstOrDefault(h => h.Id == club.HumanCoachId.Value) : null;
        var aiMgr = club.AiManagerId != null ? world.AiManagers.FirstOrDefault(m => m.Id == club.AiManagerId.Value) : null;

        AiSquadPlanDto? planDto = null;
        if (club.LatestSquadPlan != null && aiMgr != null)
        {
            var sp = club.LatestSquadPlan;
            planDto = new AiSquadPlanDto(
                ManagerId: aiMgr.Id,
                ManagerName: aiMgr.Name,
                TacticalIdentity: aiMgr.Identity.ToString(),
                PreferredFormations: aiMgr.PreferredFormations,
                SelectedFormationForNextMatch: sp.SelectedMatchFormation,
                SelectionReasoning: sp.SelectionReasoning,
                TargetStartersByPosition: sp.TargetStarters.ToDictionary(k => k.Key.ToString(), v => v.Value),
                TargetReservesByPosition: sp.TargetReserves.ToDictionary(k => k.Key.ToString(), v => v.Value),
                PriorityGaps: sp.PriorityGapDescriptions,
                MaxSingleSigningBudget: sp.MaxSingleSigningBudget,
                EmergencyCashReserve: sp.EmergencyCashReserve,
                SurplusPlayerIds: sp.SurplusPlayerIds);
        }

        return new ClubDetailDto(
            Id: club.Id,
            Name: club.Name,
            ShortName: club.ShortName,
            Country: club.Country,
            PrimaryColorHex: club.PrimaryColorHex,
            SecondaryColorHex: club.SecondaryColorHex,
            DivisionTier: club.DivisionTier,
            DivisionDisplay: club.DivisionTier.ToDisplayPt(),
            CashBalance: club.CashBalance,
            Reputation: club.Reputation,
            HumanCoachId: human?.Id,
            HumanCoachName: human?.Nickname,
            AiManagerId: aiMgr?.Id,
            AiManagerName: aiMgr?.Name,
            AiManagerIdentity: aiMgr?.Identity.ToString(),
            PreferredFormation: club.PreferredFormation,
            PreferredStarterIds: club.PreferredStarterIds,
            PreferredBenchIds: club.PreferredBenchIds,
            Stadium: new StadiumDto(
                ClubId: club.Id,
                StadiumName: club.Stadium.Name,
                BuiltCapacity: club.Stadium.BuiltCapacity,
                UsableCapacity: club.Stadium.UsableCapacity,
                ConditionPercent: club.Stadium.ConditionPercent,
                MaintenanceReservePerSeason: club.Stadium.MaintenanceReservePerSeason,
                FixedCostPerMatch: Math.Round(club.Stadium.BuiltCapacity * club.Stadium.FixedMatchCostRate, 2),
                VariableCostPerAttendee: club.Stadium.VariableAttendeeCostRate,
                LastCalculatedTicketPrice: club.Stadium.LastTicketPrice,
                LastAttendance: club.Stadium.LastAttendance,
                ExpansionTargetSeats: club.Stadium.ExpansionTargetSeats,
                ExpansionRoundsRemaining: club.Stadium.ExpansionRoundsRemaining,
                RenovationRoundsRemaining: club.Stadium.RenovationRoundsRemaining),
            Squad: squad.Select(p => MapPlayerDto(p, club.Name)).ToList(),
            AiSquadPlan: planDto)
        {
            GrowthCeiling = ClassicForceEvolutionEngine.IsActive(world) ? ClassicForceEvolutionEngine.ClubContext(world,club).Ceiling : null,
            GrowthLeaguePosition = ClassicForceEvolutionEngine.IsActive(world) ? ClassicForceEvolutionEngine.ClubContext(world,club).Position : null,
            GrowthPositionIsProvisional = ClassicForceEvolutionEngine.IsActive(world) && !world.ClassicForce.CompletedLeaguePositions.ContainsKey(club.Id),
            RequiredStarterCount = SquadValidator.RequiredStartersForSquad(squad)
        };
    }

    public static PlayerDto MapPlayerDto(Player p, string? clubName)
    {
        decimal fair = MarketAndEconomyEngine.CalculateFairValue(p);
        return new PlayerDto(
            Id: p.Id,
            ClubId: p.ClubId,
            ClubName: clubName,
            Name: p.Name,
            DisplayName: p.DisplayName,
            Position: p.Position,
            Strength: p.Strength,
            Behavior: p.Behavior,
            BehaviorDisplay: p.Behavior.ToDisplayPt(),
            IsStar: p.IsStar,
            ContractRoundsRemaining: p.ContractRoundsRemaining,
            SalaryPerRound: p.SalaryPerRound,
            MarketPrice: p.MarketPrice,
            FairValue: fair,
            IsListedForTransfer: p.IsListedForTransfer,
            AskingPrice: p.AskingPrice,
            InjuryRoundsRemaining: p.InjuryRoundsRemaining,
            SuspensionRoundsRemaining: p.SuspensionRoundsRemaining,
            YellowCardsAccumulated: p.YellowCardsAccumulated,
            SeasonGoals: p.SeasonGoals + p.CupGoals,
            SeasonMatches: p.SeasonMatches,
            CareerGoals: p.CareerGoals,
            CareerMatches: p.CareerMatches,
            PhotoUrl: p.PhotoUrl,
            PhotoLicense: p.PhotoLicense,
            PhotoCredit: p.PhotoCredit)
        {
            CatalogIdentity = p.CatalogIdentity,
            ReferenceSalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(p),
            NegotiatedContractProtected = p.NegotiatedContractProtected,
            InjuryDaysRemaining = p.InjuryDaysRemaining,
            TotalYellowCards = p.TotalYellowCards, YellowCardsInCurrentCycle = p.YellowCardsInCurrentCycle,
            TotalDirectRedCards = p.TotalDirectRedCards, TotalSecondYellowDismissals = p.TotalSecondYellowDismissals,
            SuspensionMatchesRemaining = p.SuspensionMatchesRemaining, SuspensionReason = p.SuspensionReason,
            InjuryRecoveryDate = p.InjuryDaysRemaining > 0 ? p.ForceInjuries.LastOrDefault()?.EndDate : null
        };
    }

    private async Task<(bool Success, string? Error)> ListPlayerForTransferCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid playerId,
        decimal askingPrice,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");
        if (world.RoundState is not (RoundState.PREPARING or RoundState.RESULTS))
            return (false, "Operações de mercado não são permitidas com bola rolando.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var player = world.Players.FirstOrDefault(p => p.Id == playerId && p.ClubId == coach.CurrentClubId);
        if (player == null) return (false, "Jogador não pertence ao seu clube.");

        var squad = world.Players.Where(p => p.ClubId == coach.CurrentClubId).ToList();
        var (canRemove, err) = SquadValidator.CanRemovePlayerFromSquad(squad, player.Id);
        if (!canRemove) return (false, err);

        if (askingPrice <= 0m) return (false, "O preço pedido deve ser maior que zero.");

        if(ClassicEconomyEngine.AuctionReserved(world,player.Id))return(false,"Jogador reservado para leilão.");
        player.IsListedForTransfer = true;
        player.AskingPrice = Math.Round(askingPrice, 0);
        player.ListedAtRound = world.RoundNumber;
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> BuyListedPlayerCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid playerId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");
        if (world.RoundState is not (RoundState.PREPARING or RoundState.RESULTS))
            return (false, "Operações de mercado só são permitidas fora da partida.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var player = world.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.ClubId == null || !player.IsListedForTransfer || !player.AskingPrice.HasValue)
            return (false, "Jogador não está disponível para compra direta na lista.");

        var (ok, err) = MarketAndEconomyEngine.ExecuteTransfer(
            world,
            coach.CurrentClubId.Value,
            player.ClubId.Value,
            player.Id,
            player.AskingPrice.Value,
            "Compra direta na lista de transferências",
            idempotencyKey);

        if (!ok) return (false, err);

        return (true, null);
    }

    private async Task<(bool Success, string? Error, TransferOfferDto? Offer)> CreateTransferOfferCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid playerId,
        decimal offeredAmount,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.", null);
        if (world.RoundState is not (RoundState.PREPARING or RoundState.RESULTS))
            return (false, "Negociações só são permitidas fora da partida.", null);

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.", null);

        var buyer = world.Clubs.First(c => c.Id == coach.CurrentClubId.Value);
        var player = world.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.ClubId == null || player.ClubId == buyer.Id)
            return (false, "Jogador inválido para proposta.", null);

        if (offeredAmount <= 0m || ClassicEconomyEngine.AvailableCash(world,buyer) < offeredAmount)
            return (false, "Saldo insuficiente para sustentar a proposta.", null);

        if(ClassicEconomyEngine.AuctionReserved(world,player.Id))return(false,"Jogador reservado para leilão.",null);
        var seller = world.Clubs.First(c => c.Id == player.ClubId.Value);
        var sellerSquad = world.Players.Where(p => p.ClubId == seller.Id).ToList();
        var (canRemove, remErr) = SquadValidator.CanRemovePlayerFromSquad(sellerSquad, player.Id);
        if (!canRemove) return (false, remErr, null);

        var offer = new TransferOffer
        {
            SubmittedByManagerId = coach.Id,
            GameWorldId = world.Id,
            PlayerId = player.Id,
            BuyerClubId = buyer.Id,
            SellerClubId = seller.Id,
            OfferedAmount = Math.Round(offeredAmount, 0),
            Status = TransferOfferStatus.Pending,
            Justification = $"Proposta de {coach.Nickname}",
            CreatedAtSeason = world.SeasonNumber,
            CreatedAtRound = world.RoundNumber,
            IdempotencyKey = idempotencyKey
        };

        // If Seller is AI, evaluate immediately so the user gets instant negotiation feedback (Accepted / Countered / Rejected)
        if (seller.HumanCoachId == null)
        {
            decimal fair = MarketAndEconomyEngine.CalculateFairValue(player);
            bool isSurplus = seller.LatestSquadPlan?.SurplusPlayerIds.Contains(player.Id) == true || player.IsListedForTransfer;
            decimal askRef = player.AskingPrice ?? (isSurplus ? fair * 0.92m : fair * 1.12m);

            if (offer.OfferedAmount >= askRef)
            {
                var (ok, err) = MarketAndEconomyEngine.ExecuteTransfer(
                    world, buyer.Id, seller.Id, player.Id, offer.OfferedAmount,
                    "Proposta aceita pelo clube de IA", idempotencyKey, "AcceptedOffer");
                if (!ok) return (false, err, null);
                offer.Status = TransferOfferStatus.Accepted;
            }
            else if (offer.OfferedAmount >= askRef * 0.80m)
            {
                offer.Status = TransferOfferStatus.Countered;
                offer.CounterAmount = Math.Round(askRef, 0);
            }
            else
            {
                offer.Status = TransferOfferStatus.Rejected;
            }
        }

        world.TransferOffers.Add(offer);

        var dto = new TransferOfferDto(
            offer.Id, player.Id, player.DisplayName, player.Position, player.Strength, player.IsStar,
            buyer.Id, buyer.Name, seller.Id, seller.Name, offer.OfferedAmount, offer.CounterAmount,
            offer.Status, offer.Justification, offer.CreatedAtRound);

        return (true, null, dto);
    }

    private async Task<(bool Success, string? Error)> RespondToTransferOfferCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid offerId,
        bool accept,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var offer = world.TransferOffers.FirstOrDefault(o => o.Id == offerId);
        if (offer == null) return (false, "Proposta não encontrada.");
        if (offer.BuyerClubId != coach.CurrentClubId && offer.SellerClubId != coach.CurrentClubId)
            return (false, "Esta proposta não envolve seu clube.");
        if (offer.Status is not (TransferOfferStatus.Pending or TransferOfferStatus.Countered))
            return (false, "A proposta já foi encerrada.");

        if (!accept)
        {
            offer.Status = TransferOfferStatus.Rejected;
                return (true, null);
        }

        // Can be accepted by Seller (if Pending) or by Buyer (if Countered)
        decimal finalPrice = offer.Status == TransferOfferStatus.Countered && offer.CounterAmount.HasValue
            ? offer.CounterAmount.Value
            : offer.OfferedAmount;

        if (offer.Status == TransferOfferStatus.Pending && offer.SellerClubId != coach.CurrentClubId)
            return (false, "Apenas o clube vendedor pode aceitar esta proposta pendente.");
        if (offer.Status == TransferOfferStatus.Countered && offer.BuyerClubId != coach.CurrentClubId)
            return (false, "Apenas o clube comprador pode aceitar esta contraproposta.");

        var (ok, err) = MarketAndEconomyEngine.ExecuteTransfer(
            world, offer.BuyerClubId, offer.SellerClubId, offer.PlayerId, finalPrice,
            "Negociação bilateral concluída", idempotencyKey, "AcceptedOffer");

        if (!ok) return (false, err);

        offer.Status = TransferOfferStatus.Accepted;
        return (true, null);
    }

    private async Task<(bool Success, string? Error)> PlaceAuctionBidCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid auctionId,
        decimal bidAmount,
        string commandKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");
        if (world.RoundState is not (RoundState.PREPARING or RoundState.RESULTS))
            return (false, "Lances em leilão só são permitidos fora da partida.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var club = world.Clubs.First(c => c.Id == coach.CurrentClubId.Value);
        return ClassicEconomyEngine.PlaceBid(world,club,auctionId,bidAmount,commandKey);
    }

    private async Task<(bool Success, string? Error)> RenewPlayerContractCoreAsync(
        Guid worldId,
        Guid coachId,
        Guid playerId,
        int additionalRounds,
        string commandKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var club = world.Clubs.First(c => c.Id == coach.CurrentClubId.Value);
        var player = world.Players.FirstOrDefault(p => p.Id == playerId && p.ClubId == club.Id);
        if (player == null) return (false, "Jogador não pertence ao seu clube.");

        var result=ClassicEconomyEngine.RenewContract(world,club,player,additionalRounds,commandKey);
        return result;
    }

    private async Task<(bool Success, string? Error)> TakeBankLoanCoreAsync(
        Guid worldId,
        Guid coachId,
        decimal amount,
        int rounds,
        string commandKey,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");

        var coach = world.HumanCoaches.FirstOrDefault(h => h.Id == coachId);
        if (coach?.CurrentClubId == null) return (false, "Você não controla um clube.");

        var club = world.Clubs.First(c => c.Id == coach.CurrentClubId.Value);
        if(amount!=500_000m)return(false,"O empréstimo clássico possui principal fixo de R$ 500.000.");
        var result=ClassicEconomyEngine.TakeClassicLoan(world,club,commandKey);
        return(result.Success,result.Error);
    }

    public async Task<(bool Success, string? Error)> AcceptJobInvitationAsync(
        Guid worldId,
        Guid coachId,
        Guid invitationId,
        CancellationToken ct = default)
        => await RespondJobInvitationAsync(worldId, coachId, invitationId, true, ct);

    public async Task<(bool Success, string? Error)> RespondJobInvitationAsync(Guid worldId, Guid coachId, Guid invitationId, bool accept, CancellationToken ct = default)
    {
        var gate = MultiplayerRoundOrchestrator.GetLock(worldId);
        await gate.WaitAsync(ct);
        try
        {
            var world = await _repository.GetWorldAsync(worldId, ct);
            if (world == null) return (false, "Sala não encontrada.");
            var coach = world.HumanCoaches.FirstOrDefault(c => c.Id == coachId);
            if (world.RoundState == RoundState.PREPARING && coach?.IsReadyForRound == true)
                return (false, "Retire o pronto antes de mudar de clube.");
            var result = CoachCareerEngine.Respond(world, coachId, invitationId, accept);
            if (!result.Success) return result;
            world.RoundVersion++;
            await _repository.SaveWorldAsync(world, ct);
            var snap = MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world);
            await _publisher.PublishReadyCounterUpdatedAsync(world.Id, snap.ReadyCounter, ct);
            await _publisher.PublishRoundStateChangedAsync(world.Id, snap, ct);
            return (true, null);
        }
        finally { gate.Release(); }
    }

    public async Task<object?> GetCareerAsync(Guid worldId, Guid coachId, CancellationToken ct = default)
    {
        var gate = MultiplayerRoundOrchestrator.GetLock(worldId); await gate.WaitAsync(ct);
        try
        {
            var w = await _repository.GetWorldAsync(worldId, ct); if (w == null) return null;
            CoachCareerEngine.EnsureMigrated(w); CoachCareerEngine.ProcessVacancies(w);
            await _repository.SaveWorldAsync(w, ct);
            var coach = CoachCareerEngine.Find(w, coachId);
            var list = w.JobInvitations.Where(i => i.CoachId == coachId && !i.IsExpired && !i.IsRejected && !i.IsAccepted)
                .Select(i => {
                    var c = w.Clubs.Single(c => c.Id == i.ClubId);
                    return new JobInvitationDto(i.Id, i.CoachId, c.Id, c.Name, c.DivisionTier, c.DivisionTier.ToDisplayPt(), c.CashBalance,
                        w.Players.Count(p => p.ClubId == c.Id), i.CreatedAtSeason, i.CreatedAtRound, i.IsAccepted, i.IsRejected,
                        i.Explanation, CoachCareerEngine.TablePosition(w,c), Math.Round(CoachCareerEngine.Strength(w,c),1), c.Stadium.BuiltCapacity,
                        c.Reputation >= 75 ? "Disputar títulos" : c.Reputation >= 45 ? "Buscar acesso e estabilidade" : "Permanência e desenvolvimento", i.ProjectImprovement);
                }).ToList();
            return new { invitations = list, employmentHistory = coach?.Employment ?? new(), morale = coach?.State.Morale ?? 70,
                moraleBand = w.CoachMoraleRules.Band(coach?.State.Morale ?? 70), moraleHistory = coach?.State.MoraleHistory ?? new(),
                currentClubId = coach?.ClubId, currentProjectScore = coach?.ClubId is Guid id ? CoachCareerEngine.ProjectScore(w,w.Clubs.Single(c=>c.Id==id)) : 0m,
                careerRules = w.CoachMoraleRules, titles = w.CoachTitles.Where(t=>t.CoachId==coachId).ToList() };
        }
        finally { gate.Release(); }
    }

    public async Task<(bool Success, string? Error)> UpdateMoraleRulesAsync(Guid worldId, Guid hostId, CoachMoraleRules rules, CancellationToken ct = default)
    {
        var gate=MultiplayerRoundOrchestrator.GetLock(worldId);await gate.WaitAsync(ct);
        try {
            var w=await _repository.GetWorldAsync(worldId,ct);if(w==null)return(false,"Sala não encontrada.");
            if(!w.HumanCoaches.Any(c=>c.Id==hostId && c.IsHost))return(false,"Apenas o anfitrião pode configurar as faixas.");
            if(!CoachCareerEngine.IsSafe(w))return(false,"Configuração somente entre partidas.");
            if(rules.YellowMinimum<1 || rules.GreenMinimum<=rules.YellowMinimum || rules.GreenMinimum>100 || rules.UnemployedRecoveryPerRound<1 || rules.UnemployedRecoveryPerRound>20)return(false,"Faixas inválidas.");
            w.CoachMoraleRules=rules;w.RoundVersion++;await _repository.SaveWorldAsync(w,ct);return(true,null);
        } finally {gate.Release();}
    }

    public async Task<(bool Success, string? Error)> SetCoachAutoModeByHostAsync(
        Guid worldId,
        Guid hostCoachId,
        Guid targetCoachId,
        bool autoMode,
        CancellationToken ct = default)
    {
        var world = await _repository.GetWorldAsync(worldId, ct);
        if (world == null) return (false, "Sala não encontrada.");

        var host = world.HumanCoaches.FirstOrDefault(h => h.Id == hostCoachId);
        if (host == null || !host.IsHost)
            return (false, "Apenas o host da sala pode alterar o modo automático de participantes.");

        var target = world.HumanCoaches.FirstOrDefault(h => h.Id == targetCoachId);
        if (target == null) return (false, "Participante não encontrado.");

        target.IsAutoMode = autoMode;
        world.AuditLogs.Add(new AuditLogEntry
        {
            GameWorldId = world.Id,
            SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber,
            ActionType = "HOST_SET_AUTO_MODE",
            ActorCoachId = host.Id,
            Description = $"Host {host.Nickname} alterou modo automático de {target.Nickname} para {autoMode}."
        });

        await _repository.SaveWorldAsync(world, ct);
        return (true, null);
    }
}