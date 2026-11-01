using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class MarketAndEconomyEngine
{
    /// <summary>
    /// Convex FairValue curve for strength 1..50, modulated by Star (*), discipline (Behavior),
    /// position demand, and remaining contract rounds. Calibrated to internal game economy.
    /// </summary>
    public static decimal CalculateFairValue(Player player)
    {
        int s = Math.Clamp(player.Strength, 1, 50);
        // Convex curve: strength 10 ~ 45k, strength 25 ~ 280k, strength 38 ~ 950k, strength 50 ~ 2.45M
        double baseVal = 12_000.0 + Math.Pow(s, 2.65) * 76.0;

        if (player.IsStar)
        {
            baseVal *= player.Position is PlayerPosition.A or PlayerPosition.M ? 1.32 : 1.22;
        }

        double posMod = player.Position switch
        {
            PlayerPosition.A => 1.12,
            PlayerPosition.M => 1.05,
            PlayerPosition.G => 1.03,
            PlayerPosition.D => 0.98,
            _ => 1.0
        };

        double behaviorMod = player.Behavior switch
        {
            PlayerBehavior.FairPlay => 1.08,
            PlayerBehavior.Cordeirinho => 1.04,
            PlayerBehavior.Cavalheiro => 1.00,
            PlayerBehavior.Caneleiro => 0.95,
            PlayerBehavior.Caceteiro => 0.89,
            PlayerBehavior.Sarrafeiro => 0.82,
            _ => 1.0
        };

        double contractMod = player.ContractRoundsRemaining switch
        {
            <= 2 => 0.75,
            <= 5 => 0.88,
            >= 18 => 1.06,
            _ => 1.0
        };

        decimal fair = (decimal)(baseVal * posMod * behaviorMod * contractMod);
        return Math.Round(Math.Max(15_000m, fair), 0);
    }

    public static decimal CalculateFairSalaryPerRound(Player player) =>
        CalculateFairSalaryPerRound(player.Strength, player.IsStar, player.Position);

    public static decimal CalculateFairSalaryPerRound(int strength, bool isStar, PlayerPosition position)
    {
        int s = Math.Clamp(strength, 1, 50);
        double baseSal = 220.0 + Math.Pow(s, 2.08) * 1.65;
        if (isStar) baseSal *= 1.20;
        if (position == PlayerPosition.A) baseSal *= 1.08;
        return Math.Round((decimal)baseSal, 0);
    }

    /// <summary>
    /// Automatically determines ticket price and attendance based on division, campaign, opponent strength,
    /// popularity/reputation, usable stadium capacity, and structural condition.
    /// </summary>
    public static (decimal TicketPrice, int Attendance, decimal Revenue, decimal FixedCost, decimal VariableCost)
        CalculateMatchGateEconomics(
            Club homeClub,
            Club awayClub,
            CompetitionType competitionType,
            int worldSeed,
            int seasonNumber,
            int roundNumber,
            int matchIndex,
            double demandMultiplier = 1.0)
    {
        var stadium = homeClub.Stadium;
        int usableCap = Math.Clamp(stadium.UsableCapacity, 1_000, stadium.BuiltCapacity);

        decimal baseTicket = homeClub.DivisionTier switch
        {
            DivisionTier.PrimeiraDivisao => 24m,
            DivisionTier.SegundaDivisao => 18m,
            DivisionTier.TerceiraDivisao => 14m,
            DivisionTier.QuartaDivisao => 11m,
            _ => 8m
        };

        if (competitionType >= CompetitionType.CupRoundOf16)
        {
            baseTicket *= 1.25m;
        }

        // Campaign & opponent attractiveness factor
        double campaignBoost = 1.0 + Math.Clamp((homeClub.LeagueWins - homeClub.LeagueLosses) * 0.03, -0.18, 0.25);
        double opponentBoost = 1.0 + Math.Clamp((awayClub.Reputation - 50) * 0.004, -0.12, 0.22);
        double conditionFactor = Math.Clamp(stadium.ConditionPercent / 100.0, 0.45, 1.0);

        // Automatic optimal ticket price balancing demand vs elasticity
        decimal ticketPrice = Math.Round(baseTicket * (decimal)(0.85 + 0.15 * campaignBoost * opponentBoost), 2);
        ticketPrice = Math.Clamp(ticketPrice, 5m, 65m);

        // Base demand in fans
        double baseDemandByTier = homeClub.DivisionTier switch
        {
            DivisionTier.PrimeiraDivisao => 26_000,
            DivisionTier.SegundaDivisao => 14_500,
            DivisionTier.TerceiraDivisao => 8_200,
            DivisionTier.QuartaDivisao => 4_800,
            _ => 2_200
        };

        var rng = DeterministicRng.FromContext(worldSeed, seasonNumber, roundNumber, matchIndex, 0, 99);
        double weatherOrDayMod = rng.NextDouble(0.90, 1.10);

        // Price elasticity: if ticketPrice exceeds expected tier price, attendance drops
        double priceRatio = (double)(ticketPrice / baseTicket);
        double priceElasticity = Math.Pow(Math.Max(0.5, priceRatio), -0.85);

        double rawDemand = baseDemandByTier
                           * (0.6 + homeClub.Reputation / 100.0)
                           * campaignBoost
                           * opponentBoost
                           * conditionFactor
                           * priceElasticity
                           * weatherOrDayMod
                           * demandMultiplier;

        int attendance = (int)Math.Round(Math.Clamp(rawDemand, usableCap * 0.05, usableCap));
        decimal revenue = Math.Round(attendance * ticketPrice, 2);

        // Costs: calibrated from prompt parameters (fixedCost = builtCapacity * 0.70, variableCost = attendance * 1.10, plus condition penalty)
        decimal deteriorationPenalty = stadium.ConditionPercent < 80.0
            ? (decimal)((80.0 - stadium.ConditionPercent) * 0.008) + 1.0m
            : 1.0m;

        decimal fixedCost = Math.Round(stadium.BuiltCapacity * stadium.FixedMatchCostRate * deteriorationPenalty, 2);
        decimal variableCost = Math.Round(attendance * stadium.VariableAttendeeCostRate, 2);

        return (ticketPrice, attendance, revenue, fixedCost, variableCost);
    }

    /// <summary>
    /// Records an idempotent financial transaction and updates the club's cash balance.
    /// Never applies the same idempotencyKey twice.
    /// </summary>
    public static bool TryRecordTransaction(
        GameWorld world,
        Club club,
        FinancialTransactionCategory category,
        decimal amount,
        string description,
        string idempotencyKey)
    {
        EnsureTransactionKeys(world);

        string compositeKey = $"{club.Id}:{idempotencyKey}";
        if (!world.AppliedTransactionKeys.Add(compositeKey))
        {
            return false;
        }

        club.CashBalance = Math.Round(club.CashBalance + amount, 2);
        world.Transactions.Add(new FinancialTransaction
        {
            GameWorldId = world.Id,
            ClubId = club.Id,
            ManagerId = club.HumanCoachId ?? club.AiManagerId,
            SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber,
            Category = category,
            Amount = amount,
            BalanceAfter = club.CashBalance,
            Description = description,
            IdempotencyKey = idempotencyKey
        });
        return true;
    }

    private static void EnsureTransactionKeys(GameWorld world)
    {
        if (world.AppliedTransactionKeys.Count != 0) return;
        foreach (var existing in world.Transactions)
            world.AppliedTransactionKeys.Add($"{existing.ClubId}:{existing.IdempotencyKey}");
    }

    /// <summary>
    /// Settles match gate revenues, stadium operating costs, player round salaries, contract countdowns,
    /// and stadium construction progress idempotently at the end of a round.
    /// </summary>
    public static void SettleMatchAndRoundFinancials(
        GameWorld world,
        IReadOnlyList<MatchFixture> roundMatches)
    {
        ClassicEconomyEngine.EnsureMigrated(world);
        OperatingEconomyEngine.EnsureScheduledRules(world);
        DisciplineEngine.SettleFinishedMatches(world,roundMatches);
        var clubDict = world.Clubs.ToDictionary(c => c.Id);

        // 1. Settle match gate for every finished match in this round
        foreach (var match in roundMatches.Where(m => ClassicEconomyEngine.Played(m) && !m.FinancialsSettled))
        {
            if (!clubDict.TryGetValue(match.HomeClubId, out var homeClub) ||
                !clubDict.TryGetValue(match.AwayClubId, out var awayClub))
            {
                continue;
            }

            var (ticketPrice, attendance, revenue, fixedCost, variableCost) = CalculateMatchGateEconomics(
                homeClub,
                awayClub,
                match.CompetitionType,
                world.Seed,
                match.SeasonNumber,
                match.RoundNumber,
                match.DeterministicMatchIndex,
                OperatingEconomyEngine.GateDemandFactor(world,homeClub,awayClub));

            match.TicketPrice = ticketPrice;
            match.Attendance = attendance;
            match.TicketRevenue = revenue;
            match.StadiumOperatingCost = fixedCost + variableCost;
            match.FinancialsSettled = true;

            homeClub.Stadium.LastTicketPrice = ticketPrice;
            homeClub.Stadium.LastAttendance = attendance;

            string baseKey = $"match:{match.Id}:S{match.SeasonNumber}:R{match.RoundNumber}";
            if (match.CompetitionType != CompetitionType.League)
            {
                decimal visitorShare = Math.Round(revenue * 0.25m, 2);
                decimal homeRevenue = revenue - visitorShare;

                TryRecordTransaction(world, homeClub, FinancialTransactionCategory.TicketRevenue,
                    homeRevenue, $"Bilheteria Taça 75% ({attendance} pagantes x R$ {ticketPrice:N2}) vs {awayClub.ShortName}", $"{baseKey}:rev");
                TryRecordTransaction(world, awayClub, FinancialTransactionCategory.TicketRevenue,
                    visitorShare, $"Cota visitante Taça 25% ({attendance} pagantes) vs {homeClub.ShortName}", $"{baseKey}:rev_away");
            }
            else
            {
                TryRecordTransaction(world, homeClub, FinancialTransactionCategory.TicketRevenue,
                    revenue, $"Bilheteria ({attendance} pagantes x R$ {ticketPrice:N2}) vs {awayClub.ShortName}", $"{baseKey}:rev");
            }

            TryRecordTransaction(world, homeClub, FinancialTransactionCategory.StadiumFixedCost,
                -fixedCost, $"Custo operacional fixo do estádio ({homeClub.Stadium.Name})", $"{baseKey}:fix");

            TryRecordTransaction(world, homeClub, FinancialTransactionCategory.StadiumVariableCost,
                -variableCost, $"Custo operacional variável ({attendance} presentes)", $"{baseKey}:var");
        }

        // 2. Settle salaries, contracts, injuries/suspensions recovery, and stadium works for clubs active in this round (idempotent per Season+Round)
        string roundSettlementKey = $"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}";
        // Compatibility with 006 snapshots: a completed sponsorship pass contains every funded club.
        // Reconstruct only this round, from its recorded transactions; do not age players on reload.
        if (!world.SettledFinancialRounds.Contains(roundSettlementKey))
        {
            var fundedClubIds = world.Players.Where(p => p.ClubId.HasValue).Select(p => p.ClubId!.Value).Distinct().ToHashSet();
            var sponsored = world.Transactions.Where(t => t.SeasonNumber == world.SeasonNumber && t.RoundNumber == world.RoundNumber
                && t.Category == FinancialTransactionCategory.Sponsorship).Select(t => t.ClubId).ToHashSet();
            if (fundedClubIds.Count > 0 && fundedClubIds.IsSubsetOf(sponsored))
                world.SettledFinancialRounds.Add(roundSettlementKey);
        }
        if (!world.SettledFinancialRounds.Add(roundSettlementKey))
        {
            ClassicEconomyEngine.SettleLoans(world,roundMatches);
            return;
        }

        var playersByClub = world.Players
            .Where(p => p.ClubId != null)
            .GroupBy(p => p.ClubId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var club in world.Clubs)
        {
            if (!playersByClub.TryGetValue(club.Id, out var squad)) continue;

            var (sponsorship,sponsorshipDescription)=OperatingEconomyEngine.SponsorshipForRound(world,club);
            TryRecordTransaction(world, club, FinancialTransactionCategory.Sponsorship,
                sponsorship, sponsorshipDescription,
                $"sponsor:{club.Id}:S{world.SeasonNumber}:R{world.RoundNumber}");

            bool playedThisRound = roundMatches.Any(m => ClassicEconomyEngine.Played(m) && (m.HomeClubId == club.Id || m.AwayClubId == club.Id));
            if (!playedThisRound)
            {
                continue;
            }

            decimal totalSalary = squad.Sum(p => p.SalaryPerRound);
            string salKey = $"salary:{club.Id}:S{world.SeasonNumber}:R{world.RoundNumber}";
            TryRecordTransaction(world, club, FinancialTransactionCategory.PlayerSalary,
                -totalSalary, $"Folha salarial da rodada ({squad.Count} atletas)", salKey);

            // Progress stadium works
            if (club.Stadium.ExpansionRoundsRemaining > 0)
            {
                club.Stadium.ExpansionRoundsRemaining--;
                if (club.Stadium.ExpansionRoundsRemaining == 0 && club.Stadium.ExpansionTargetSeats > 0)
                {
                    club.Stadium.BuiltCapacity += club.Stadium.ExpansionTargetSeats;
                    club.Stadium.UsableCapacity += club.Stadium.ExpansionTargetSeats;
                    club.Stadium.ExpansionTargetSeats = 0;
                }
            }

            if (club.Stadium.RenovationRoundsRemaining > 0)
            {
                club.Stadium.RenovationRoundsRemaining--;
                if (club.Stadium.RenovationRoundsRemaining == 0)
                {
                    club.Stadium.ConditionPercent = Math.Min(100.0, club.Stadium.ConditionPercent + 15.0);
                    club.Stadium.UsableCapacity = (int)Math.Round(club.Stadium.BuiltCapacity * (club.Stadium.ConditionPercent / 100.0));
                }
            }

            // Decrement player injury/suspension & contract rounds
            var matchForClub = roundMatches.First(m => m.HomeClubId == club.Id || m.AwayClubId == club.Id);
            foreach (var p in squad.ToList())
            {
                if (p.InjuryRoundsRemaining > 0)
                {
                    bool injuredToday = matchForClub.HomeInjuredInMatchIds.Contains(p.Id) || matchForClub.AwayInjuredInMatchIds.Contains(p.Id);
                    if (!injuredToday) p.InjuryRoundsRemaining--;
                }

                p.ContractRoundsRemaining--;
                if (p.ContractRoundsRemaining <= 0)
                {
                    var (canRelease, _) = SquadValidator.CanRemovePlayerFromSquad(squad, p.Id);
                    int samePosCount = squad.Count(x => x.Position == p.Position);
                    int minSafePos = p.Position == PlayerPosition.G ? 2 : 3;
                    bool shouldRenew = ClassicEconomyEngine.AuctionReserved(world,p.Id) || !canRelease
                                       || samePosCount <= minSafePos
                                       || (squad.Count <= 20 && club.CashBalance > p.SalaryPerRound * 8m && p.Strength >= squad.Average(x => x.Strength) - 3);

                    if (shouldRenew)
                    {
                        decimal previousSalary=p.SalaryPerRound;
                        p.ContractRoundsRemaining = 20;
                        p.SalaryPerRound = CalculateFairSalaryPerRound(p);
                        p.MarketPrice = CalculateFairValue(p);
                        decimal bonus = Math.Round(p.SalaryPerRound * 1.5m, 2);
                        TryRecordTransaction(world, club, FinancialTransactionCategory.ContractRenewal,
                            -bonus, $"Renovação contratual de {p.DisplayName}",
                            $"renew:{p.Id}:S{world.SeasonNumber}:R{world.RoundNumber}");
                        ClassicEconomyEngine.RecordContract(world,p,previousSalary,"CONTRACT_EXPIRATION_RENEWAL",$"renew:{p.Id}:S{world.SeasonNumber}:R{world.RoundNumber}",bonus);
                    }
                    else
                    {
                        // Player enters Auction upon contract expiration!
                        squad.Remove(p);
                        club.PreferredStarterIds.Remove(p.Id);
                        club.PreferredBenchIds.Remove(p.Id);
                        p.ClubId = null;
                        p.NegotiatedContractProtected=false;
                        p.IsListedForTransfer = false;
                        p.AskingPrice = null;
                        p.ContractRoundsRemaining = 20;
                        decimal fair = CalculateFairValue(p);
                        p.MarketPrice = fair;
                        p.SalaryPerRound = CalculateFairSalaryPerRound(p);
                        world.Auctions.Add(new AuctionItem
                        {
                            GameWorldId = world.Id,
                            PlayerId = p.Id,
                            FormerClubId = club.Id,
                            MinimumBid = Math.Round(fair * 0.65m, 0),
                            CurrentBid = 0m,
                            CreatedAtSeason = world.SeasonNumber,
                            CreatedAtRound = world.RoundNumber,
                            ExpiresAtRound = world.RoundNumber + 2
                        });
                    }
                }
            }
        }
        ClassicEconomyEngine.SettleLoans(world,roundMatches);
    }

    /// <summary>
    /// End-of-season stadium structural wear (1%..6% without maintenance; 0%..1% with maintenance),
    /// reducing UsableCapacity while keeping BuiltCapacity intact, plus AI ROI-driven maintenance/renovation/expansion.
    /// </summary>
    public static void ProcessSeasonalStadiumWearAndAiProjects(GameWorld world)
    {
        for (int i = 0; i < world.Clubs.Count; i++)
        {
            var club = world.Clubs[i];
            var stadium = club.Stadium;
            var rng = DeterministicRng.FromContext(world.Seed, world.SeasonNumber, 99, club.DeterministicIndex, 0, 501);

            decimal maintenanceCost = Math.Round(stadium.BuiltCapacity * 4.5m, 2);
            decimal operatingCash=club.CashBalance-OperatingEconomyEngine.AiDebtReserve(world,club);
            bool canAffordMaintenance = operatingCash > maintenanceCost * 2.2m;

            // AI estimates future loss from neglect vs maintenance cost
            double expectedWearWithoutMaint = 3.5; // avg of 1..6%
            decimal estimatedLossFromWear = (decimal)(stadium.BuiltCapacity * (expectedWearWithoutMaint / 100.0)) * stadium.LastTicketPrice * 7m;
            bool payMaintenance = canAffordMaintenance && (club.HumanCoachId != null || estimatedLossFromWear >= maintenanceCost * 0.75m);

            double wearPercent;
            if (payMaintenance)
            {
                TryRecordTransaction(world, club, FinancialTransactionCategory.StadiumMaintenance,
                    -maintenanceCost, $"Manutenção preventiva anual do estádio ({stadium.Name})",
                    $"maint:{club.Id}:S{world.SeasonNumber}");
                wearPercent = rng.NextDouble(0.0, 1.0); // 0% to 1% wear with maintenance
            }
            else
            {
                wearPercent = rng.NextDouble(1.0, 6.0); // 1% to 6% wear without maintenance
            }

            stadium.ConditionPercent = Math.Round(Math.Clamp(stadium.ConditionPercent - wearPercent, 35.0, 100.0), 2);
            stadium.UsableCapacity = Math.Clamp(
                (int)Math.Round(stadium.BuiltCapacity * (stadium.ConditionPercent / 100.0)),
                1_000,
                stadium.BuiltCapacity);

            // AI Renovation & ROI Expansion decisions for AI clubs
            if (club.HumanCoachId == null)
            {
                if (stadium.ConditionPercent < 82.0 && stadium.RenovationRoundsRemaining == 0)
                {
                    decimal renoCost = CalculateRenovationCost(stadium);
                    if (operatingCash > renoCost * 2.5m)
                    {
                        StartStadiumRenovation(world, club);
                    }
                }
                else if (stadium.ExpansionRoundsRemaining == 0
                         && stadium.LastAttendance >= stadium.UsableCapacity * 0.88
                         && stadium.BuiltCapacity < 65_000)
                {
                    int addSeats = 5_000;
                    decimal expCost = CalculateExpansionCost(stadium, addSeats);
                    // ROI check: will 5,000 extra seats pay back within ~4 seasons?
                    decimal annualNetGainPerSeat = (stadium.LastTicketPrice - stadium.VariableAttendeeCostRate - stadium.FixedMatchCostRate) * 7m;
                    decimal fourYearReturn = annualNetGainPerSeat * addSeats * 4m;
                    if (fourYearReturn > expCost && operatingCash > expCost * 2.8m)
                    {
                        StartStadiumExpansion(world, club, addSeats);
                    }
                }
            }
        }
    }

    public static decimal CalculateRenovationCost(Stadium stadium)
    {
        double damagePct = Math.Max(1.0, 100.0 - stadium.ConditionPercent);
        return Math.Round((decimal)(stadium.BuiltCapacity * (damagePct / 100.0) * 38.0), 0);
    }

    public static decimal CalculateExpansionCost(Stadium stadium, int additionalSeats)
    {
        return Math.Round(additionalSeats * 85m, 0);
    }

    public static (bool Success, string? ErrorMessage) StartStadiumRenovation(GameWorld world, Club club)
    {
        if (club.Stadium.RenovationRoundsRemaining > 0)
            return (false, "O estádio já está em reforma.");
        if (club.Stadium.ConditionPercent >= 99.5)
            return (false, "O estádio já está em perfeitas condições.");

        decimal cost = CalculateRenovationCost(club.Stadium);
        if (club.CashBalance < cost)
            return (false, $"Saldo insuficiente para a reforma (necessário: R$ {cost:N0}).");

        TryRecordTransaction(world, club, FinancialTransactionCategory.StadiumRenovation,
            -cost, $"Obra de reforma estrutural do estádio ({club.Stadium.Name})",
            $"reno:{club.Id}:S{world.SeasonNumber}:R{world.RoundNumber}");

        club.Stadium.RenovationRoundsRemaining = 3;
        return (true, null);
    }

    public static (bool Success, string? ErrorMessage) StartStadiumExpansion(GameWorld world, Club club, int additionalSeats)
    {
        if (additionalSeats <= 0 || additionalSeats > 30_000)
            return (false, "Ampliação deve ser entre 1.000 e 30.000 lugares.");
        if (club.Stadium.ExpansionRoundsRemaining > 0)
            return (false, "O estádio já possui obra de ampliação em andamento.");

        decimal cost = CalculateExpansionCost(club.Stadium, additionalSeats);
        if (club.CashBalance < cost)
            return (false, $"Saldo insuficiente para ampliação (necessário: R$ {cost:N0}).");

        TryRecordTransaction(world, club, FinancialTransactionCategory.StadiumExpansion,
            -cost, $"Ampliação do estádio em +{additionalSeats} lugares",
            $"exp:{club.Id}:S{world.SeasonNumber}:R{world.RoundNumber}");

        club.Stadium.ExpansionTargetSeats = additionalSeats;
        club.Stadium.ExpansionRoundsRemaining = 5;
        return (true, null);
    }

    /// <summary>
    /// Executes the weekly/round AI transfer market batch:
    /// 1. Updates transfer listings for surplus players and crisis sales.
    /// 2. Processes pending bilateral offers made to AI clubs.
    /// 3. AI clubs buy listed players or submit bilateral offers driven by their ManagerSquadPlan tactical needs.
    /// 4. AI clubs place rational bids in open auctions up to their private maxBid.
    /// 5. Settles expired auctions atomically.
    /// </summary>
    public static List<string> ProcessWeeklyAiMarketBatch(GameWorld world)
    {
        var logs = new List<string>();
        var clubDict = world.Clubs.ToDictionary(c => c.Id);
        var managerDict = world.AiManagers.ToDictionary(m => m.Id);
        var playerDict = world.Players.ToDictionary(p => p.Id);

        // Update all AI squad plans first
        foreach (var club in world.Clubs.Where(c => c.HumanCoachId == null && c.DivisionTier != DivisionTier.Distrital))
        {
            if (club.AiManagerId != null && managerDict.TryGetValue(club.AiManagerId.Value, out var mgr))
            {
                var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
                AiManagerBrain.PlanAndSelectLineup(mgr, club, squad, null, null, true, world.Seed, world.SeasonNumber, world.RoundNumber, world.AllowClassic505);
                OperatingEconomyEngine.ApplyAiCapitalBudget(world,club);
            }
        }

        // 1. AI clubs list surplus players or discount stale listings
        foreach (var club in world.Clubs.Where(c => c.HumanCoachId == null && c.DivisionTier != DivisionTier.Distrital))
        {
            var squad = world.Players.Where(p => p.ClubId == club.Id).ToList();
            var plan = club.LatestSquadPlan;
            if (plan == null) continue;

            // Discount listings that haven't sold after 3+ rounds
            foreach (var listed in squad.Where(p => p.IsListedForTransfer && world.RoundNumber - p.ListedAtRound >= 3))
            {
                decimal fair = CalculateFairValue(listed);
                listed.AskingPrice = Math.Round(Math.Max(fair * 0.78m, (listed.AskingPrice ?? fair) * 0.90m), 0);
                listed.ListedAtRound = world.RoundNumber;
            }

            // List surplus players if allowed by squad rules
            foreach (var surplusId in plan.SurplusPlayerIds.Take(2))
            {
                if (!playerDict.TryGetValue(surplusId, out var p) || p.IsListedForTransfer) continue;
                var (canRemove, _) = SquadValidator.CanRemovePlayerFromSquad(squad, p.Id);
                if (!canRemove) continue;

                decimal fair = CalculateFairValue(p);
                p.IsListedForTransfer = true;
                p.AskingPrice = Math.Round(fair * (club.CashBalance < plan.EmergencyCashReserve ? 0.88m : 1.04m), 0);
                p.ListedAtRound = world.RoundNumber;
            }
        }

        // 2. AI clubs respond to pending transfer offers where Seller is AI
        foreach (var offer in world.TransferOffers.Where(o => o.Status == TransferOfferStatus.Pending).ToList())
        {
            if (!clubDict.TryGetValue(offer.SellerClubId, out var seller) || seller.HumanCoachId != null)
                continue;

            if (!playerDict.TryGetValue(offer.PlayerId, out var player) || player.ClubId != seller.Id)
            {
                offer.Status = TransferOfferStatus.Cancelled;
                continue;
            }

            var sellerSquad = world.Players.Where(p => p.ClubId == seller.Id).ToList();
            var (canRemove, _) = SquadValidator.CanRemovePlayerFromSquad(sellerSquad, player.Id);
            if (!canRemove)
            {
                offer.Status = TransferOfferStatus.Rejected;
                continue;
            }

            decimal fair = CalculateFairValue(player);
            bool isSurplus = seller.LatestSquadPlan?.SurplusPlayerIds.Contains(player.Id) == true;
            decimal minAccept = isSurplus ? fair * 0.88m : fair * 1.12m;

            if (offer.OfferedAmount >= minAccept)
            {
                var transferResult = ExecuteTransfer(world, offer.BuyerClubId, seller.Id, player.Id, offer.OfferedAmount,
                    $"Oferta aceita ({offer.Justification})", $"offer:{offer.Id}", "AcceptedOffer");
                offer.Status = transferResult.Success ? TransferOfferStatus.Accepted : TransferOfferStatus.Cancelled;
                if (!transferResult.Success) continue;
                logs.Add($"Transferência: {player.DisplayName} de {seller.ShortName} para {clubDict[offer.BuyerClubId].ShortName} por R$ {offer.OfferedAmount:N0}.");
            }
            else if (offer.OfferedAmount >= minAccept * 0.82m)
            {
                offer.Status = TransferOfferStatus.Countered;
                offer.CounterAmount = Math.Round(minAccept, 0);
            }
            else
            {
                offer.Status = TransferOfferStatus.Rejected;
            }
        }

        // 3. AI clubs actively seek tactical reinforcements (up to max 1 signing per club per round)
        // Order by tactical need (PriorityGapPositions.Count) + deterministic round rotation so all divisions participate
        var buyerCandidates = world.Clubs
            .Where(c => c.HumanCoachId == null && c.DivisionTier != DivisionTier.Distrital)
            .OrderByDescending(c => c.LatestSquadPlan?.PriorityGapPositions.Count ?? 0)
            .ThenBy(c => (c.DeterministicIndex + world.RoundNumber * 5) % 32)
            .ToList();

        int transfersExecutedThisBatch = 0;
        foreach (var buyer in buyerCandidates)
        {
            if (transfersExecutedThisBatch >= 8) break;

            var buyerSquad = world.Players.Where(p => p.ClubId == buyer.Id).ToList();
            if (buyerSquad.Count >= SquadValidator.MaxSquadSize) continue;

            var plan = buyer.LatestSquadPlan;
            if (plan == null || plan.MaxSingleSigningBudget < 25_000m) continue;

            int divExpectedStr = buyer.DivisionTier switch
            {
                DivisionTier.PrimeiraDivisao => 36,
                DivisionTier.SegundaDivisao => 29,
                DivisionTier.TerceiraDivisao => 23,
                DivisionTier.QuartaDivisao => 18,
                _ => 14
            };

            // Can seek reinforcements even with 18..23 players if priority gap exists or a clear starter upgrade is affordable
            var targetPositions = plan.PriorityGapPositions.Count > 0
                ? plan.PriorityGapPositions
                : new List<PlayerPosition> { PlayerPosition.A, PlayerPosition.M, PlayerPosition.D, PlayerPosition.G };

            foreach (var pos in targetPositions)
            {
                var currentInPos = buyerSquad.Where(p => p.Position == pos).OrderByDescending(p => p.Strength).ToList();
                int targetStarterCount = plan.TargetStarters.GetValueOrDefault(pos, 2);
                int targetReserveCount = plan.TargetReserves.GetValueOrDefault(pos, 1);

                int minStrengthNeeded;
                string needCategory;
                if (currentInPos.Count < targetStarterCount)
                {
                    minStrengthNeeded = (int)Math.Max(12, Math.Min(divExpectedStr, buyerSquad.Average(p => p.Strength) - 2));
                    needCategory = "Titular urgente";
                }
                else if (currentInPos.Count < targetStarterCount + targetReserveCount)
                {
                    minStrengthNeeded = (int)Math.Max(10, Math.Min(divExpectedStr - 3, buyerSquad.Average(p => p.Strength) - 4));
                    needCategory = "Reserva tático";
                }
                else
                {
                    int weakestStarter = currentInPos.Take(targetStarterCount).LastOrDefault()?.Strength ?? 20;
                    minStrengthNeeded = weakestStarter + 2;
                    needCategory = "Melhoria titular";
                }

                // Look at listed players first (including human-listed players!)
                var candidates = world.Players
                    .Where(p => p.ClubId != null && p.ClubId != buyer.Id && p.Position == pos && p.Strength >= minStrengthNeeded && !ClassicEconomyEngine.AuctionReserved(world,p.Id))
                    .Select(p => new
                    {
                        Player = p,
                        Fair = CalculateFairValue(p),
                        Price = p.IsListedForTransfer && p.AskingPrice.HasValue
                            ? p.AskingPrice.Value
                            : Math.Round(CalculateFairValue(p) * 1.12m, 0)
                    })
                    .Where(x => x.Price <= plan.MaxSingleSigningBudget && x.Price <= x.Fair * 1.25m)
                    // Rational Utility: prefer listed players first, then maximize (Strength + StarBonus) / Price
                    .OrderByDescending(x => x.Player.IsListedForTransfer ? 1 : 0)
                    .ThenByDescending(x => (x.Player.Strength + (x.Player.IsStar ? 6 : 0)) / (double)Math.Max(10_000m, x.Price))
                    .Take(6)
                    .ToList();

                bool signedOrOffered = false;
                foreach (var bestTarget in candidates)
                {
                    RecordAiMarketAction(world, buyer, "AiPurchaseEvaluation", bestTarget.Player.Id, bestTarget.Price);
                    var seller = clubDict[bestTarget.Player.ClubId!.Value];
                    var sellerSquad = world.Players.Where(p => p.ClubId == seller.Id).ToList();
                    var (canSellerRemove, _) = SquadValidator.CanRemovePlayerFromSquad(sellerSquad, bestTarget.Player.Id);
                    if (!canSellerRemove) continue;

                    // Do not let seller drop below 2 goalkeepers if selling a goalkeeper
                    if (bestTarget.Player.Position == PlayerPosition.G && sellerSquad.Count(p => p.Position == PlayerPosition.G) < 2)
                        continue;

                    string justification = $"{needCategory} para {plan.TargetPrimaryFormation} na posição {pos} (força {bestTarget.Player.Strength})";

                    if (bestTarget.Player.IsListedForTransfer)
                    {
                        // Direct buy from transfer list (works for both AI and Human sellers who listed the player)
                        var (ok, _) = ExecuteTransfer(world, buyer.Id, seller.Id, bestTarget.Player.Id, bestTarget.Price,
                            justification, $"listbuy:{buyer.Id}:{bestTarget.Player.Id}:S{world.SeasonNumber}:R{world.RoundNumber}", "DirectPurchase");
                        if (ok)
                        {
                            transfersExecutedThisBatch++;
                            logs.Add($"Compra na lista: {buyer.ShortName} contratou {bestTarget.Player.DisplayName} de {seller.ShortName} por R$ {bestTarget.Price:N0} ({justification}).");
                            signedOrOffered = true;
                            break;
                        }
                    }
                    else if (seller.HumanCoachId != null)
                    {
                        // Submit formal bilateral offer to Human coach
                        if (!world.TransferOffers.Any(o => o.PlayerId == bestTarget.Player.Id && o.BuyerClubId == buyer.Id && o.Status == TransferOfferStatus.Pending))
                        {
                            world.TransferOffers.Add(new TransferOffer
                            {
                                SubmittedByManagerId = buyer.AiManagerId,
                                GameWorldId = world.Id,
                                PlayerId = bestTarget.Player.Id,
                                BuyerClubId = buyer.Id,
                                SellerClubId = seller.Id,
                                OfferedAmount = bestTarget.Price,
                                Status = TransferOfferStatus.Pending,
                                Justification = justification,
                                CreatedAtSeason = world.SeasonNumber,
                                CreatedAtRound = world.RoundNumber,
                                IdempotencyKey = $"aioffer:{buyer.Id}:{bestTarget.Player.Id}:S{world.SeasonNumber}:R{world.RoundNumber}"
                            });
                            RecordAiMarketAction(world, buyer, "AiPurchaseAttempt", bestTarget.Player.Id, bestTarget.Price);
                            logs.Add($"Proposta enviada: {buyer.ShortName} ofereceu R$ {bestTarget.Price:N0} a {seller.ShortName} por {bestTarget.Player.DisplayName}.");
                            signedOrOffered = true;
                            break;
                        }
                    }
                    else
                    {
                        // AI-to-AI direct negotiation
                        var (ok, _) = ExecuteTransfer(world, buyer.Id, seller.Id, bestTarget.Player.Id, bestTarget.Price,
                            justification, $"aidirect:{buyer.Id}:{bestTarget.Player.Id}:S{world.SeasonNumber}:R{world.RoundNumber}", "NegotiatedTransfer");
                        if (ok)
                        {
                            transfersExecutedThisBatch++;
                            logs.Add($"Negociação direta: {buyer.ShortName} contratou {bestTarget.Player.DisplayName} de {seller.ShortName} por R$ {bestTarget.Price:N0} ({justification}).");
                            signedOrOffered = true;
                            break;
                        }
                    }
                }

                if (signedOrOffered) break;
            }
        }

        // 4. AI participation in open Auctions with private maxBid (no fake inflation loops)
        foreach (var auction in world.Auctions.Where(a => !a.IsClosed).ToList())
        {
            if (!playerDict.TryGetValue(auction.PlayerId, out var auctionPlayer)) continue;
            decimal fair = CalculateFairValue(auctionPlayer);

            foreach (var aiClub in buyerCandidates.Take(10))
            {
                if(auctionPlayer.ClubId==aiClub.Id)continue;
                var squad = world.Players.Where(p => p.ClubId == aiClub.Id).ToList();
                if (squad.Count >= SquadValidator.MaxSquadSize) continue;

                var plan = aiClub.LatestSquadPlan;
                if (plan == null) continue;

                bool needsPos = plan.PriorityGapPositions.Contains(auctionPlayer.Position);
                double avgStr = squad.Average(p => p.Strength);
                if (!needsPos && auctionPlayer.Strength <= avgStr + 1) continue;

                if (!auction.PrivateAiMaxBids.TryGetValue(aiClub.Id, out decimal privateMax))
                {
                    decimal valuation = fair * (needsPos ? 1.05m : 0.88m);
                    privateMax = Math.Round(Math.Min(valuation, plan.MaxSingleSigningBudget), 0);
                    auction.PrivateAiMaxBids[aiClub.Id] = privateMax;
                }

                decimal nextRequiredBid = auction.Reason!=AuctionReason.ContractExpiration ? ClassicEconomyEngine.NextBid(auction) : auction.CurrentBid == 0m
                    ? auction.MinimumBid
                    : Math.Round(auction.CurrentBid * 1.06m, 0);

                if (auction.HighestBidderClubId != aiClub.Id
                    && nextRequiredBid <= privateMax
                    && ClassicEconomyEngine.AvailableCash(world,aiClub,auction.Id) >= nextRequiredBid + plan.EmergencyCashReserve)
                {
                    auction.CurrentBid = nextRequiredBid;
                    auction.HighestBidderClubId = aiClub.Id;
                    RecordAiMarketAction(world, aiClub, "AiAuctionBid", auction.PlayerId, nextRequiredBid, auction.Id);
                }
            }

            // Close auction if expired
            if (auction.Reason==AuctionReason.Voluntary ? VoluntaryAuctionEngine.IsDue(world,auction)
                : world.SeasonNumber > auction.CreatedAtSeason || world.RoundNumber >= auction.ExpiresAtRound)
            {
                SettleAuction(world, auction, logs);
            }
        }

        ClassicEconomyEngine.EvaluateInsolvency(world);
        return logs;
    }

    public static (bool Success, string? ErrorMessage) ExecuteTransfer(
        GameWorld world,
        Guid buyerClubId,
        Guid sellerClubId,
        Guid playerId,
        decimal price,
        string justification,
        string idempotencyKey,
        string transactionType = "DirectPurchase",
        Guid? settlingAuctionId = null)
    {
        if (buyerClubId == sellerClubId)
            return (false, "O clube comprador e vendedor são o mesmo.");

        var buyer = world.Clubs.FirstOrDefault(c => c.Id == buyerClubId);
        var seller = world.Clubs.FirstOrDefault(c => c.Id == sellerClubId);
        var player = world.Players.FirstOrDefault(p => p.Id == playerId);

        if (buyer == null || seller == null || player == null)
            return (false, "Clube ou jogador não encontrado.");

        EnsureTransactionKeys(world);
        if (world.AppliedTransactionKeys.Contains($"{buyer.Id}:{idempotencyKey}:buy")
            || world.AppliedTransactionKeys.Contains($"{seller.Id}:{idempotencyKey}:sell"))
            return (false, "Transação já processada anteriormente.");

        RecordAiMarketAction(world, buyer, "AiPurchaseAttempt", player.Id, price);

        if (player.ClubId != seller.Id)
            return (false, "O jogador não pertence mais ao clube vendedor.");

        bool auctionSale=settlingAuctionId is Guid aid&&world.Auctions.Any(a=>a.Id==aid&&!a.IsClosed&&a.PlayerId==playerId&&a.FormerClubId==sellerClubId&&a.Reason is (AuctionReason.FinancialInsolvency or AuctionReason.Voluntary)&&a.HighestBidderClubId==buyerClubId&&a.CurrentBid==price);
        if(ClassicEconomyEngine.AuctionReserved(world,playerId)&&!auctionSale)return(false,"Jogador reservado para leilão.");
        if (price < 0m || price == 0m&&!auctionSale || ClassicEconomyEngine.AvailableCash(world,buyer,settlingAuctionId) < price)
            return (false, "Saldo insuficiente no clube comprador.");
        if(OperatingEconomyEngine.AiDebtReserve(world,buyer)>0&&ClassicEconomyEngine.AvailableCash(world,buyer,settlingAuctionId)-OperatingEconomyEngine.AiDebtReserve(world,buyer)<price)
            return(false,"A IA preserva o saldo necessário à quitação antes de contratar reforços.");

        var buyerSquad = world.Players.Where(p => p.ClubId == buyer.Id).ToList();
        var (canAdd, addErr) = SquadValidator.CanAddPlayerToSquad(buyerSquad);
        if (!canAdd) return (false, addErr);

        var sellerSquad = world.Players.Where(p => p.ClubId == seller.Id).ToList();
        var (canRemove, remErr) = SquadValidator.CanRemovePlayerFromSquad(sellerSquad, player.Id);
        if (!canRemove) return (false, remErr);

        // Record financial transactions idempotently
        if (!TryRecordTransaction(world, buyer, auctionSale?FinancialTransactionCategory.AuctionPurchase:FinancialTransactionCategory.TransferPurchase,
                -price, $"Compra de {player.DisplayName} de {seller.Name} ({justification})", $"{idempotencyKey}:buy"))
        {
            return (false, "Transação já processada anteriormente.");
        }

        TryRecordTransaction(world, seller, auctionSale?FinancialTransactionCategory.AuctionSale:FinancialTransactionCategory.TransferSale,
            price, $"Venda de {player.DisplayName} para {buyer.Name}", $"{idempotencyKey}:sell");

        var buyTransaction = world.Transactions[^2];
        var sellTransaction = world.Transactions[^1];
        StampTransfer(world, buyTransaction, buyer, seller, player, transactionType, price, justification);
        sellTransaction.TransferTransactionId = buyTransaction.Id;
        sellTransaction.PlayerId = player.Id;
        sellTransaction.BuyerClubId = buyer.Id;
        sellTransaction.SellerClubId = seller.Id;
        sellTransaction.ManagerId = seller.HumanCoachId ?? seller.AiManagerId;
        sellTransaction.TransactionType = transactionType;

        // Remove from seller preferred lists
        seller.PreferredStarterIds.Remove(player.Id);
        seller.PreferredBenchIds.Remove(player.Id);

        // A completed transfer invalidates all competing bilateral negotiations atomically in the same snapshot/ledger save.
        foreach (var pending in world.TransferOffers.Where(o => o.PlayerId == player.Id &&
            o.Status is TransferOfferStatus.Pending or TransferOfferStatus.Countered))
            pending.Status = TransferOfferStatus.Cancelled;

        // Transfer ownership
        player.ClubId = buyer.Id;
        player.IsListedForTransfer = false;
        player.AskingPrice = null;
        player.ContractRoundsRemaining = Math.Max(player.ContractRoundsRemaining, 16);
        player.MarketPrice = CalculateFairValue(player);
        decimal previousSalary=player.SalaryPerRound;
        player.SalaryPerRound = CalculateFairSalaryPerRound(player);
        ClassicEconomyEngine.RecordContract(world,player,previousSalary,"TRANSFER_CONTRACT",$"transfer:{world.Id}:{buyer.Id}:{player.Id}:{idempotencyKey}:contract");

        return (true, null);
    }

    public static void SettleAuction(GameWorld world, AuctionItem auction, List<string>? logs = null)
    {
        if (auction.IsClosed) return;
        if(auction.Reason==AuctionReason.Voluntary){VoluntaryAuctionEngine.Settle(world,auction,logs);return;}
        if(auction.Reason!=AuctionReason.ContractExpiration){ClassicEconomyEngine.SettleSpecialAuction(world,auction,logs);return;}
        auction.IsClosed = true;

        var player = world.Players.FirstOrDefault(p => p.Id == auction.PlayerId);
        if (player == null || player.ClubId!=null) return;

        if (auction.HighestBidderClubId != null && auction.CurrentBid > 0m)
        {
            var winner = world.Clubs.FirstOrDefault(c => c.Id == auction.HighestBidderClubId.Value);
            var winnerSquad = winner != null ? world.Players.Where(p => p.ClubId == winner.Id).ToList() : new List<Player>();

            if (winner != null && ClassicEconomyEngine.AvailableCash(world,winner,auction.Id) >= auction.CurrentBid && winnerSquad.Count < SquadValidator.MaxSquadSize)
            {
                if (!TryRecordTransaction(world, winner, FinancialTransactionCategory.AuctionPurchase,
                    -auction.CurrentBid, $"Arremate em leilão de {player.DisplayName}", $"auction:{auction.Id}:win")) return;
                // Expired contracts are free agents. The former club is not a selling counterparty.
                StampTransfer(world, world.Transactions[^1], winner, null, player, "Auction", auction.CurrentBid, "Leilão");

                player.ClubId = winner.Id;
                player.ContractRoundsRemaining = 20;
                player.IsListedForTransfer = false;
                player.AskingPrice = null;
                player.MarketPrice = CalculateFairValue(player);
                player.SalaryPerRound = CalculateFairSalaryPerRound(player);
                ClassicEconomyEngine.RecordContract(world,player,player.SalaryPerRound,"EXPIRATION_AUCTION",$"auction:{auction.Id}:contract");
                logs?.Add($"Leilão encerrado: {winner.ShortName} arrematou {player.DisplayName} por R$ {auction.CurrentBid:N0}.");
                return;
            }
        }

        // If no valid bidder won, assign to a club needing depth in that position that can afford a minimum contract
        var fallbackClub = world.Clubs
            .Where(c => c.HumanCoachId == null && c.DivisionTier != DivisionTier.Distrital)
            .Select(c => new { Club = c, Squad = world.Players.Where(p => p.ClubId == c.Id).ToList() })
            .Where(x => (x.Squad.Count < 18 || x.Squad.Count(p => p.Position == player.Position) < 3)
                        && x.Squad.Count < SquadValidator.MaxSquadSize
                        && x.Club.CashBalance > (x.Squad.Sum(p => p.SalaryPerRound) + player.SalaryPerRound) * 6m)
            .OrderBy(x => x.Squad.Count(p => p.Position == player.Position))
            .ThenBy(x => x.Squad.Count)
            .FirstOrDefault()?.Club;

        if (fallbackClub != null)
        {
            if (!TryRecordTransaction(world, fallbackClub, FinancialTransactionCategory.FreeAgentSigning, 0m,
                $"Contrato sem lance de {player.DisplayName}", $"auction:{auction.Id}:free")) return;
            StampTransfer(world, world.Transactions[^1], fallbackClub, null, player, "FreeAgentAssignment", 0m, "Contrato sem lance");
            player.ClubId = fallbackClub.Id;
            player.ContractRoundsRemaining = 14;
            player.MarketPrice = CalculateFairValue(player);
            player.SalaryPerRound = CalculateFairSalaryPerRound(player);
            ClassicEconomyEngine.RecordContract(world,player,player.SalaryPerRound,"EXPIRATION_FREE_ASSIGNMENT",$"auction:{auction.Id}:contract");
        }
    }

    private static void RecordAiMarketAction(GameWorld world, Club club, string action, Guid playerId, decimal amount, Guid? relatedId = null)
    {
        if (club.HumanCoachId != null || club.AiManagerId == null) return;
        world.AuditLogs.Add(new AuditLogEntry { GameWorldId = world.Id, SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber, ActorCoachId = club.AiManagerId, ClubId = club.Id,
            PlayerId = playerId, Amount = amount, RelatedEntityId = relatedId, ActionType = action });
    }

    private static void StampTransfer(GameWorld world, FinancialTransaction tx, Club buyer, Club? seller,
        Player player, string type, decimal amount, string justification)
    {
        tx.TransferTransactionId = tx.Id;
        tx.PlayerId = player.Id;
        tx.BuyerClubId = buyer.Id;
        tx.SellerClubId = seller?.Id;
        tx.ManagerId = buyer.HumanCoachId ?? buyer.AiManagerId;
        tx.TransactionType = type;
        if (buyer.HumanCoachId != null || buyer.AiManagerId == null) return;
        var positionSquad = world.Players.Where(p => p.ClubId == buyer.Id && p.Position == player.Position).OrderByDescending(p => p.Strength).ToList();
        int starters = buyer.LatestSquadPlan?.TargetStarters.GetValueOrDefault(player.Position) ?? 1;
        string reason = justification.Contains("Titular urgente", StringComparison.Ordinal) ? "TACTICAL_REQUIREMENT"
            : justification.Contains("Melhoria titular", StringComparison.Ordinal) ? "REINFORCEMENT_STARTER"
            : justification.Contains("Reserva tático", StringComparison.Ordinal) ? "SQUAD_DEPTH"
            : positionSquad.Count < starters ? "TACTICAL_REQUIREMENT"
            : positionSquad.Count < starters + (buyer.LatestSquadPlan?.TargetReserves.GetValueOrDefault(player.Position) ?? 1) ? "SQUAD_DEPTH"
            : player.Strength > (positionSquad.Take(starters).LastOrDefault()?.Strength ?? 0) ? "REINFORCEMENT_STARTER" : "MARKET_OPPORTUNITY";
        tx.SigningDecision = new(reason, buyer.LatestSquadPlan?.TargetPrimaryFormation ?? buyer.PreferredFormation,
            player.Position, player.Strength, positionSquad.Take(starters).LastOrDefault()?.Strength,
            positionSquad.Skip(starters).FirstOrDefault()?.Strength, amount, CalculateFairValue(player),
            CalculateFairSalaryPerRound(player), player.Behavior, player.IsStar, buyer.CashBalance + amount, buyer.CashBalance);
    }
}
