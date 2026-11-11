using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Application;

public sealed partial class ClubAndMarketApplicationService
{
    public async Task<object?> GetMarketAsync(Guid worldId,CancellationToken ct=default)
    {
        var gate=MultiplayerRoundOrchestrator.GetLock(worldId);
        await gate.WaitAsync(ct);
        try {
    var world = await _repository.GetWorldAsync(worldId, ct);
    if (world == null) return null;

    var clubDict = world.Clubs.ToDictionary(c => c.Id);
    var playerDict = world.Players.ToDictionary(p => p.Id);

    var listings = world.Players
        .Where(p => p.IsListedForTransfer && p.ClubId != null && p.AskingPrice.HasValue)
        .OrderByDescending(p => p.Strength)
        .Select(p => new TransferListingDto(
            p.Id, p.DisplayName, p.Position, p.Strength, p.Behavior.ToDisplayPt(), p.IsStar,
            p.ClubId, clubDict[p.ClubId!.Value].Name, p.AskingPrice!.Value,
            MarketAndEconomyEngine.CalculateFairValue(world, p), p.SalaryPerRound, p.ContractRoundsRemaining, p.ListedAtRound) { ListingVersion=p.TransferListingVersion })
        .ToList();

    var auctions = world.Auctions
        .OrderBy(a => a.IsClosed)
        .ThenBy(a => a.ExpiresAtRound)
        .Where(a => playerDict.ContainsKey(a.PlayerId))
        .Select(a =>
        {
            var p = playerDict[a.PlayerId];
            string? bidderName = a.HighestBidderClubId != null && clubDict.TryGetValue(a.HighestBidderClubId.Value, out var bc) ? bc.Name : null;
            return new AuctionItemDto(
                a.Id, p.Id, p.DisplayName, p.Position, p.Strength, p.Behavior.ToDisplayPt(), p.IsStar,
                a.MinimumBid, a.CurrentBid, a.HighestBidderClubId, bidderName, a.ExpiresAtRound, a.IsClosed) { Reason=a.Reason,ClosureReason=a.ClosureReason,SalaryPerRound=p.SalaryPerRound,DurationRounds=a.DurationRounds,ExpiresAtSeason=a.ExpiresAtSeason,
                    SellerClubId=a.FormerClubId,SellerClubName=a.FormerClubId is Guid sellerId?clubDict.GetValueOrDefault(sellerId)?.Name:null,
                    RoundsRemaining=a.IsClosed?0:Math.Max(0,((a.ExpiresAtSeason??world.SeasonNumber)-world.SeasonNumber)*world.TotalRoundsInSeason+a.ExpiresAtRound-world.RoundNumber+(world.RoundState==RoundState.RESULTS?0:1)) };
        })
        .ToList();

    var offers = world.TransferOffers
        .OrderByDescending(o => o.CreatedAtRound)
        .Take(50)
        .Where(o => playerDict.ContainsKey(o.PlayerId))
        .Select(o =>
        {
            var p = playerDict[o.PlayerId];
            return new TransferOfferDto(
                o.Id, p.Id, p.DisplayName, p.Position, p.Strength, p.IsStar,
                o.BuyerClubId, clubDict.GetValueOrDefault(o.BuyerClubId)?.Name ?? "",
                o.SellerClubId, clubDict.GetValueOrDefault(o.SellerClubId)?.Name ?? "",
                o.OfferedAmount, o.CounterAmount, o.Status, o.Justification, o.CreatedAtRound);
        })
        .ToList();

    return new { listings, auctions, offers, world.RoundVersion, world.SeasonNumber, world.RoundNumber, totalRoundsInSeason=world.TotalRoundsInSeason, inflationIndex=world.Economy.InflationIndex, inflationDisplay=ClassicPlayerEconomyEngine.CalculateInflationDisplay(world.Economy.InflationIndex) };
        } finally {gate.Release();}
    }
}
