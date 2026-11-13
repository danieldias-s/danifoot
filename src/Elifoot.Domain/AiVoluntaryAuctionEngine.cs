using Elifoot.Contracts;

namespace Elifoot.Domain;

internal static class AiVoluntaryAuctionEngine
{
    private const int ListingStaleAfterRounds = 3;
    private const int MinimumRoundsBetweenAuctions = 2;

    public static void OpenForCompletedRound(GameWorld world, IReadOnlyList<Club> clubs,
        IReadOnlyList<Club> auctionBuyers, List<string> logs)
    {
        string roundKey = $"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}";
        if (world.RoundState != RoundState.RESULTS || !world.SettledFinancialRounds.Contains(roundKey)
            || world.Economy.LastAiVoluntaryAuctionRound == roundKey) return;
        world.Economy.LastAiVoluntaryAuctionRound = roundKey;

        int completedRounds = world.SettledFinancialRounds.Count;
        var squads = world.Players.Where(p => p.ClubId != null).ToLookup(p => p.ClubId!.Value);
        var reservedSlots = world.Auctions.Where(a => !a.IsClosed && a.HighestBidderClubId != null)
            .ToLookup(a => a.HighestBidderClubId!.Value);
        var buyers = auctionBuyers
            .Where(c => c.LatestSquadPlan != null && squads[c.Id].Any()
                && squads[c.Id].Count() + reservedSlots[c.Id].Count() < SquadValidator.MaxSquadSize)
            .Select(c => new Buyer(c.Id, c.LatestSquadPlan!, squads[c.Id].Average(p => p.Strength),
                ClassicEconomyEngine.AvailableCash(world, c) - c.LatestSquadPlan!.EmergencyCashReserve))
            .Where(b => b.BidBudget > 0)
            .ToList();
        if (buyers.Count == 0) return;

        var blockedClubs = world.Auctions
            .Where(a => a.Reason == AuctionReason.Voluntary && a.AiOpenedAfterCompletedRounds is int opened
                && (!a.IsClosed || completedRounds - opened < MinimumRoundsBetweenAuctions))
            .Select(a => a.FormerClubId).ToHashSet();
        var negotiating = world.TransferOffers
            .Where(o => o.Status is TransferOfferStatus.Pending or TransferOfferStatus.Countered)
            .Select(o => o.PlayerId).ToHashSet();
        var minutesPlayed = world.PlayerStatistics.Where(s => s.Season == world.SeasonNumber)
            .ToLookup(s => (s.ClubId, s.PlayerId), s => s.MinutesPlayed);

        foreach (var club in clubs)
        {
            var plan = club.LatestSquadPlan;
            if (plan == null || blockedClubs.Contains(club.Id)) continue;
            var eligible = squads[club.Id].Where(p => plan.SurplusPlayerIds.Contains(p.Id)
                && !p.IsStar && !club.PreferredStarterIds.Contains(p.Id) && !negotiating.Contains(p.Id)
                && MarketValidationEngine.ValidateSale(world, club.Id, p.Id).Success).ToList();
            bool surplus = eligible.Count >= 3;
            bool urgent = ClassicEconomyEngine.AvailableCash(world, club) < plan.EmergencyCashReserve;
            var candidates = eligible
                .Select(p => Assess(world, club, plan, p, surplus, urgent, buyers))
                .OfType<Candidate>()
                .OrderBy(c => club.PreferredBenchIds.Contains(c.Player.Id))
                .ThenBy(c => minutesPlayed[(club.Id, c.Player.Id)].Sum())
                .ThenByDescending(c => c.ExpectedAdvantage)
                .ThenBy(c => c.Player.Id);

            foreach (var candidate in candidates)
            {
                var opened = VoluntaryAuctionEngine.CreateForAi(world, club, candidate.Player.Id,
                    candidate.MinimumBid, candidate.Duration);
                if (!opened.Success) continue;
                logs.Add($"Leilão voluntário da IA: {club.ShortName} anunciou {candidate.Player.DisplayName}.");
                break;
            }
        }
    }

    internal static int RoundsSinceListing(GameWorld world, Player player)
    {
        if (player.TransferListedAfterCompletedRounds is int started)
            return Math.Max(0, world.SettledFinancialRounds.Count - started);
        // Legacy listings store the last price update. Recover only the age known in this season.
        return player.ListedAtRound > 0 ? Math.Max(0, world.RoundNumber - player.ListedAtRound) : 0;
    }

    private static Candidate? Assess(GameWorld world, Club club, ManagerSquadPlan plan, Player player,
        bool surplus, bool urgent, List<Buyer> buyers)
    {
        bool stale = player.IsListedForTransfer && RoundsSinceListing(world, player) >= ListingStaleAfterRounds;
        if (!surplus && !urgent && !stale) return null;

        decimal fair = MarketAndEconomyEngine.CalculateFairValue(world, player);
        decimal fixedPrice = player.IsListedForTransfer && player.AskingPrice is > 0m
            ? player.AskingPrice.Value : MarketAndEconomyEngine.AiFixedAskingPrice(club, plan, fair);
        decimal floorRatio = urgent ? 0.92m : 0.96m;
        if (stale) floorRatio -= 0.06m;
        if (surplus) floorRatio -= 0.02m;
        if (club.PreferredBenchIds.Contains(player.Id)) floorRatio += 0.02m;
        decimal minimumBid = Math.Max(1m, Math.Round(Math.Max(fair * floorRatio, fixedPrice * 0.88m), 0));

        var bids = buyers.Where(b => b.ClubId != club.Id)
            .Select(b => b.MaximumBid(player, fair))
            .Where(bid => bid >= minimumBid)
            .OrderDescending().Take(2).ToList();
        if (bids.Count == 0) return null;

        // With a single prospect, only the opening bid is supported by the available evidence.
        decimal expected = bids.Count == 2 ? bids[1] : minimumBid;
        decimal directAlternative = fixedPrice * (urgent ? 0.90m : stale ? 0.94m : 0.98m);
        if (expected < directAlternative) return null;
        int duration = urgent ? 1 : bids.Count == 2 && expected > fixedPrice ? 3 : 2;
        return new Candidate(player, minimumBid, duration, expected - directAlternative);
    }

    private sealed record Buyer(Guid ClubId, ManagerSquadPlan Plan, double AverageStrength, decimal BidBudget)
    {
        public decimal MaximumBid(Player player, decimal fair)
        {
            bool needsPosition = Plan.PriorityGapPositions.Contains(player.Position);
            if (!needsPosition && player.Strength <= AverageStrength + 1) return 0m;
            return Math.Min(BidBudget, MarketAndEconomyEngine.AiAuctionValuation(Plan, needsPosition, fair));
        }
    }

    private sealed record Candidate(Player Player, decimal MinimumBid, int Duration, decimal ExpectedAdvantage);
}
