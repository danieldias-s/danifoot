using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Legal market constraints shared by human commands, AI decisions and settlement.
/// Callers own the room lock; strategy and price/salary formulas are deliberately separate.</summary>
public static class MarketValidationEngine
{
    public static bool CanActAsAi(GameWorld world, Club club) => world.Clubs.Contains(club)
        && club.HumanCoachId == null && club.AiManagerId is Guid managerId
        && world.AiManagers.Any(m => m.Id == managerId && m.CurrentClubId == club.Id);

    public static (bool Success, string? Error) ValidateSale(GameWorld world, Guid sellerId, Guid playerId,
        Guid? settlingAuctionId = null)
    {
        if (!ClassicEconomyEngine.Safe(world)) return Fail("MARKET_PHASE", "Operações de mercado disponíveis somente entre partidas.");
        var seller = world.Clubs.FirstOrDefault(c => c.Id == sellerId);
        var player = world.Players.FirstOrDefault(p => p.Id == playerId);
        if (seller == null) return Fail("MARKET_CLUB_NOT_FOUND", "Clube vendedor não encontrado.");
        if (player == null) return Fail("MARKET_PLAYER_NOT_FOUND", "Jogador não encontrado.");
        if (player.ClubId != sellerId) return Fail("MARKET_OWNERSHIP", "O jogador não pertence mais ao clube vendedor.");
        if(world.Economy.SalaryRequests.Any(r=>r.PlayerId==playerId&&r.IsUnresolved))
            return Fail("SALARY_PENDING_DECISION", "Resolva o pedido espontâneo antes de vender este jogador.");
        if (ClassicEconomyEngine.IsSeasonRenewed(world, player))
            return Fail("MARKET_RENEWED", "Jogador renovado está protegido de qualquer venda ou leilão nesta temporada.");
        if (!world.Economy.Rules.SeasonContractsEnabled && player.ContractRoundsRemaining <= 0)
            return Fail("MARKET_CONTRACT_EXPIRED", "Contrato do jogador encerrado.");
        if (world.Auctions.Any(a => !a.IsClosed && a.PlayerId == playerId && a.Id != settlingAuctionId))
            return Fail("MARKET_AUCTION_RESERVED", "Jogador reservado para leilão.");
        // A completed sale must preserve the registered squad. Existing auction rules revalidate again at settlement.
        var squad = world.Players.Where(p => p.ClubId == sellerId).ToList();
        var removal=SquadValidator.CanRemovePlayerFromSquad(squad, playerId);
        return removal.CanRemove ? (true,null) : Fail("MARKET_SELLER_SQUAD",removal.ErrorMessage!);
    }

    public static (bool Success, string? Error) ValidateBuyer(GameWorld world, Guid buyerId, decimal amount,
        Guid? settlingAuctionId = null)
    {
        var buyer = world.Clubs.FirstOrDefault(c => c.Id == buyerId);
        if (buyer == null) return Fail("MARKET_CLUB_NOT_FOUND", "Clube comprador não encontrado.");
        if (amount < 0 || amount != Math.Round(amount, 2))
            return Fail("MARKET_AMOUNT", "Valor deve ser não negativo e ter até duas casas decimais.");
        if (ClassicEconomyEngine.AvailableCash(world, buyer, settlingAuctionId) < amount)
            return Fail("MARKET_FUNDS", "SALDO INSUFICIENTE no clube comprador.");
        int reserved = world.Auctions.Count(a => !a.IsClosed && a.Id != settlingAuctionId && a.HighestBidderClubId == buyerId);
        if (world.Players.Count(p => p.ClubId == buyerId) + reserved >= SquadValidator.MaxSquadSize)
            return Fail("MARKET_SQUAD_FULL", "Limite de elenco reservado por contratações em andamento.");
        return (true, null);
    }

    public static (bool Success, string? Error) ValidatePurchase(GameWorld world, Guid buyerId, Guid sellerId,
        Guid playerId, decimal amount, Guid? settlingAuctionId = null)
    {
        if (buyerId == sellerId) return Fail("MARKET_SELF_PURCHASE", "O clube comprador e vendedor são o mesmo.");
        var sale = ValidateSale(world, sellerId, playerId, settlingAuctionId);
        if (!sale.Success) return sale;
        if (settlingAuctionId is Guid id)
        {
            var auction = world.Auctions.FirstOrDefault(a => a.Id == id);
            if (auction == null || auction.IsClosed || auction.PlayerId != playerId || auction.FormerClubId != sellerId
                || auction.Reason is not (AuctionReason.Voluntary or AuctionReason.FinancialInsolvency)
                || auction.HighestBidderClubId != buyerId || auction.CurrentBid != amount || !IsDue(world, auction))
                return Fail("MARKET_AUCTION_INVALID", "Liquidação de leilão inválida ou antecipada.");
            if (amount < auction.MinimumBid) return Fail("MARKET_BID_LOW", "LANCE INSUFICIENTE.");
        }
        else if (amount <= 0) return Fail("MARKET_AMOUNT", "O preço deve ser maior que zero.");
        var funds = ValidateBuyer(world, buyerId, amount, settlingAuctionId);
        if (!funds.Success) return funds;
        var seller = world.Clubs.First(c => c.Id == sellerId);
        if (seller.CashBalance > decimal.MaxValue - amount)
            return Fail("MARKET_AMOUNT", "Valor excede o limite financeiro do vendedor.");
        return (true, null);
    }

    public static bool IsDue(GameWorld world, AuctionItem auction) => auction.Reason == AuctionReason.Voluntary
        ? VoluntaryAuctionEngine.IsDue(world, auction) : ClassicEconomyEngine.IsSpecialAuctionDue(world, auction);

    public static (bool Success, string? Error) ValidateAuctionPlayer(GameWorld world, AuctionItem auction)
    {
        if (!world.Auctions.Contains(auction) || auction.IsClosed)
            return Fail("MARKET_AUCTION_CLOSED", "Leilão encerrado ou inexistente.");
        if (auction.MinimumBid < 0 || auction.CurrentBid < 0
            || auction.HighestBidderClubId != null && auction.CurrentBid < auction.MinimumBid)
            return Fail("MARKET_AUCTION_INVALID", "Estado financeiro do leilão inválido.");
        var player = world.Players.FirstOrDefault(p => p.Id == auction.PlayerId);
        if (player == null) return Fail("MARKET_PLAYER_NOT_FOUND", "Jogador não encontrado.");
        if (auction.Reason is AuctionReason.Voluntary or AuctionReason.FinancialInsolvency)
            return auction.FormerClubId is Guid seller ? ValidateSale(world, seller, player.Id, auction.Id)
                : Fail("MARKET_OWNERSHIP", "Clube vendedor não encontrado.");
        if (auction.Reason is not (AuctionReason.SalaryRenewalRefused or AuctionReason.ContractExpiration)
            || player.ClubId != null || world.Auctions.Any(a => !a.IsClosed && a.Id != auction.Id && a.PlayerId == player.Id))
            return Fail("MARKET_AUCTION_INVALID", "Jogador indisponível para este leilão.");
        return (true, null);
    }

    public static (bool Success, string? Error) ValidateBid(GameWorld world, Guid buyerId, AuctionItem auction, decimal amount)
    {
        if (!ClassicEconomyEngine.Safe(world)) return Fail("MARKET_PHASE", "Lances disponíveis somente entre partidas.");
        var availability = ValidateAuctionPlayer(world, auction);
        if (!availability.Success) return availability;
        if (world.Players.Any(p => p.Id == auction.PlayerId && p.ClubId == buyerId))
            return Fail("MARKET_SELF_PURCHASE", "O clube já possui este jogador.");
        // The authoritative closing batch bids and settles under the same room lock on the deadline round.
        // This preserves the existing AI timing; no new bid can enter a later round or a closed auction.
        int season = auction.ExpiresAtSeason ?? (auction.CreatedAtSeason > 0 ? auction.CreatedAtSeason : world.SeasonNumber);
        if (world.SeasonNumber > season || world.SeasonNumber == season && world.RoundNumber > auction.ExpiresAtRound)
            return Fail("MARKET_AUCTION_EXPIRED", "Prazo do leilão encerrado.");
        if (auction.MinimumBid < 0 || amount != Math.Round(amount, auction.Reason == AuctionReason.Voluntary ? 2 : 0)
            || auction.HighestBidderClubId != null && auction.CurrentBid > decimal.MaxValue - 5_000m
            || amount < ClassicEconomyEngine.NextBid(auction))
            return Fail("MARKET_BID_LOW", "LANCE INSUFICIENTE. Respeite o mínimo, o incremento e a precisão do leilão.");
        return ValidateBuyer(world, buyerId, amount, auction.Id);
    }

    public static (bool Success, string? Error) ValidateAuctionWinner(GameWorld world, AuctionItem auction)
    {
        var availability = ValidateAuctionPlayer(world, auction);
        if (!availability.Success) return availability;
        if (!IsDue(world, auction)) return Fail("MARKET_AUCTION_EARLY", "Leilão ainda não pode ser liquidado.");
        if (auction.HighestBidderClubId is not Guid buyerId || auction.CurrentBid < auction.MinimumBid)
            return Fail("MARKET_NO_BID", "Leilão sem comprador válido.");
        if (auction.CurrentBid != Math.Round(auction.CurrentBid,auction.Reason == AuctionReason.Voluntary ? 2 : 0))
            return Fail("MARKET_AMOUNT", "Precisão do lance inválida.");
        return ValidateBuyer(world, buyerId, auction.CurrentBid, auction.Id);
    }

    private static (bool Success, string? Error) Fail(string code, string message) => (false, $"{code}: {message}");
}
