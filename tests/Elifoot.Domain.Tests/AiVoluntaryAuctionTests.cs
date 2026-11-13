using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class AiVoluntaryAuctionTests
{
    [Fact]
    public void BalancedClubKeepsFixedListingWhileExtraDepthMakesAuctionAnOption()
    {
        var (balanced, seller, _) = World(extraAttackers: 0);
        CompleteRound(balanced, 1);
        Assert.Empty(OpenedByAi(balanced, seller));
        Assert.Contains(balanced.Transactions, t => t.ClubId == seller.Id && t.TransactionType == "DirectPurchase");

        var (deep, deepSeller, buyer) = World();
        CompleteRound(deep, 1);
        var auction = Assert.Single(OpenedByAi(deep, deepSeller));
        Assert.Equal(deepSeller.Id, auction.FormerClubId);
        Assert.Equal(3, auction.DurationRounds);
        Assert.NotNull(auction.HighestBidderClubId);
        Assert.Contains(deep.AuditLogs, a => a.ClubId == auction.HighestBidderClubId && a.RelatedEntityId == auction.Id
            && a.ActionType == "AiAuctionBid");
        var player = deep.Players.Single(p => p.Id == auction.PlayerId);
        Assert.Equal(deepSeller.Id, player.ClubId);
        Assert.False(player.IsListedForTransfer);
        Assert.Null(player.AskingPrice);
        Assert.DoesNotContain(player.Id, deepSeller.PreferredStarterIds);
        Assert.InRange(auction.MinimumBid, MarketAndEconomyEngine.CalculateFairValue(deep, player) * 0.9m,
            MarketAndEconomyEngine.CalculateFairValue(deep, player));
        Assert.False(ClassicEconomyEngine.PlaceBid(deep, deepSeller, auction.Id, auction.CurrentBid + 5_000m, "self").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(deep, buyer.Id, deepSeller.Id, player.Id, auction.CurrentBid,
            "concurrent direct sale", "direct").Success);
    }

    [Fact]
    public void FinancialPressureCanOpenShortAuctionAndCooldownSpansTheSeasonBoundary()
    {
        var (world, seller, _) = World(extraAttackers: 2);
        seller.CashBalance = 1m;
        CompleteRound(world, world.TotalRoundsInSeason);
        var auction = Assert.Single(OpenedByAi(world, seller));
        Assert.Equal(1, auction.DurationRounds);
        Assert.Equal(2, auction.ExpiresAtSeason);
        Assert.Equal(1, auction.ExpiresAtRound);
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
        Assert.Single(OpenedByAi(world, seller));

        // Isolate the seller's next-season decision from the squad changes of the first sale.
        var (nextSeason, nextSeller, _) = World(extraAttackers: 2);
        nextSeason.SeasonNumber = 2;
        nextSeason.SettledFinancialRounds.UnionWith(world.SettledFinancialRounds);
        nextSeller.CashBalance = 1m;
        var unrestricted = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(nextSeason))!;
        CompleteRound(unrestricted, 1);
        Assert.Single(OpenedByAi(unrestricted, nextSeller));

        auction.IsClosed = true;
        nextSeason.Auctions.Add(auction);
        var atBoundary = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(nextSeason))!;
        CompleteRound(nextSeason, 1);
        Assert.Equal(auction.Id, Assert.Single(OpenedByAi(nextSeason, nextSeller)).Id);

        atBoundary.SettledFinancialRounds.Add("round_settled:S2:R1");
        CompleteRound(atBoundary, 2);
        Assert.Equal(2, atBoundary.SettledFinancialRounds.Count - auction.AiOpenedAfterCompletedRounds);
        Assert.Contains(OpenedByAi(atBoundary, nextSeller), a => a.CreatedAtSeason == 2 && a.CreatedAtRound == 2);
    }

    [Fact]
    public void StalePriceListingCanSwitchToAuctionAndOpenAuctionBlocksAnotherUntilItEnds()
    {
        var (world, seller, buyer) = World(extraAttackers: 0);
        // Keep the AI interested in bids while preventing ordinary purchases before the listing ages.
        var stale = world.Players.Where(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A)
            .OrderBy(p => p.Strength).First();
        stale.IsListedForTransfer = true;
        stale.AskingPrice = Math.Round(MarketAndEconomyEngine.CalculateFairValue(world, stale) * 0.95m, 0);
        stale.TransferListedAfterCompletedRounds = 0;
        var aiBuyers = world.Clubs.Where(c => c.Id != seller.Id && c.HumanCoachId == null).ToList();
        foreach (var club in aiBuyers) club.HumanCoachId = Guid.NewGuid();
        CompleteRound(world, 1);
        CompleteRound(world, 2);
        Assert.Empty(OpenedByAi(world, seller));
        buyer.HumanCoachId = null;
        CompleteRound(world, 3);
        var auction = Assert.Single(OpenedByAi(world, seller));
        Assert.Equal(stale.Id, auction.PlayerId);
        Assert.Equal(2, auction.DurationRounds);
        Assert.False(stale.IsListedForTransfer);

        seller.CashBalance = 1m;
        CompleteRound(world, 4);
        CompleteRound(world, 5);
        Assert.Single(OpenedByAi(world, seller));
        Assert.True(auction.IsClosed);
    }

    [Theory]
    [InlineData(TransferOfferStatus.Pending)]
    [InlineData(TransferOfferStatus.Countered)]
    public void ProtectsEssentialAndCommittedPlayers(TransferOfferStatus status)
    {
        var (world, seller, buyer) = World();
        seller.CashBalance = 1m;
        var attackers = world.Players.Where(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A)
            .OrderByDescending(p => p.Strength).ToList();
        var essential = attackers[0];
        essential.IsStar = true;
        var committed = attackers[^1];
        world.TransferOffers.Add(new TransferOffer
        {
            PlayerId = committed.Id, SellerClubId = seller.Id, BuyerClubId = buyer.Id,
            Status = status, OfferedAmount = 1m
        });
        CompleteRound(world, 1);
        var auction = Assert.Single(OpenedByAi(world, seller));
        Assert.NotEqual(essential.Id, auction.PlayerId);
        Assert.NotEqual(committed.Id, auction.PlayerId);
    }

    [Fact]
    public void NoBuyerKeepsPlayerAndSellerReceivesNothing()
    {
        var (world, seller, _) = World();
        CompleteRound(world, 1);
        var auction = Assert.Single(OpenedByAi(world, seller));
        auction.HighestBidderClubId = null;
        auction.CurrentBid = 0m;
        foreach (var club in world.Clubs.Where(c => c.Id != seller.Id && c.HumanCoachId == null))
            club.HumanCoachId = Guid.NewGuid();
        decimal cash = seller.CashBalance;
        CompleteRound(world, auction.ExpiresAtRound);
        Assert.Equal("AUCTION_UNSOLD", auction.ClosureReason);
        Assert.Equal(seller.Id, world.Players.Single(p => p.Id == auction.PlayerId).ClubId);
        Assert.Equal(cash, seller.CashBalance);
    }

    [Fact]
    public void ForcedAuctionsKeepTheirRulesAndDoNotUseTheVoluntarySlot()
    {
        var (world, seller, _) = World();
        seller.CashBalance = -1m;
        ClassicEconomyEngine.EvaluateInsolvency(world);
        var forced = Assert.Single(world.Auctions, a => a.Reason == AuctionReason.FinancialInsolvency);
        CompleteRound(world, 1);
        var voluntary = Assert.Single(OpenedByAi(world, seller));
        Assert.NotEqual(forced.PlayerId, voluntary.PlayerId);
        Assert.Equal(0m, forced.MinimumBid);
        Assert.Null(forced.AiOpenedAfterCompletedRounds);
    }

    [Fact]
    public void HumanListingSurvivesHandOverAndAiReservationSurvivesReloadAndReplay()
    {
        var (world, seller, _) = World();
        seller.HumanCoachId = Guid.NewGuid();
        var player = world.Players.First(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A);
        Assert.True(VoluntaryAuctionEngine.Create(world, seller, player.Id, "human").Success);
        var humanAuction = Assert.Single(world.Auctions);
        seller.HumanCoachId = null;
        CompleteRound(world, 1);
        var aiAuction = Assert.Single(OpenedByAi(world, seller));
        Assert.Null(humanAuction.AiOpenedAfterCompletedRounds);
        Assert.NotEqual(humanAuction.PlayerId, aiAuction.PlayerId);

        var restored = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(restored);
        Assert.Equal(aiAuction.Id, Assert.Single(OpenedByAi(restored, seller)).Id);
        Assert.All(restored.Auctions.Where(a => !a.IsClosed).GroupBy(a => a.PlayerId), a => Assert.Single(a));
    }

    [Fact]
    public void UndersizedSquadCannotOpenAnAuctionOrAListing()
    {
        var (world, seller, _) = World();
        foreach (var player in world.Players.Where(p => p.ClubId == seller.Id).Skip(SquadValidator.MinSquadSize - 1))
            player.ClubId = null;
        seller.CashBalance = 1m;
        CompleteRound(world, 1);
        Assert.Empty(OpenedByAi(world, seller));
        Assert.DoesNotContain(world.Players, p => p.ClubId == seller.Id && p.IsListedForTransfer);
    }

    private static IEnumerable<AuctionItem> OpenedByAi(GameWorld world, Club seller)
        => world.Auctions.Where(a => a.FormerClubId == seller.Id && a.AiOpenedAfterCompletedRounds != null);

    private static void CompleteRound(GameWorld world, int round)
    {
        world.RoundNumber = round;
        world.RoundState = RoundState.RESULTS;
        world.SettledFinancialRounds.Add($"round_settled:S{world.SeasonNumber}:R{round}");
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
    }

    private static (GameWorld World, Club Seller, Club Buyer) World(int extraAttackers = 3)
    {
        var world = WorldSeedFactory.CreateSeededWorld(totalClubs: 32);
        world.Auctions.Clear();
        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        var otherBuyer = world.Clubs[2];
        foreach (var club in world.Clubs.Skip(3)) club.HumanCoachId = Guid.NewGuid();
        foreach (var manager in world.AiManagers.Where(m => m.Id == seller.AiManagerId || m.Id == buyer.AiManagerId
            || m.Id == otherBuyer.AiManagerId))
            manager.PreferredFormations = [new("4-4-2", 100)];
        foreach (var player in world.Players)
        {
            player.IsListedForTransfer = false;
            player.AskingPrice = null;
            player.IsStar = false;
            player.ContractRoundsRemaining = 20;
            player.SalaryPerRound = 500m;
            player.Strength = player.ClubId == seller.Id ? 40 : 20;
            if (player.ClubId != seller.Id) player.RenewedForSeason = world.SeasonNumber;
        }
        foreach (var bidder in new[] { buyer, otherBuyer })
        {
            foreach (var player in world.Players.Where(p => p.ClubId == bidder.Id && p.Position == PlayerPosition.A).Skip(1))
                player.ClubId = null;
            bidder.CashBalance = 50_000_000m;
        }
        var weakerAttackers = world.Players.Where(p => p.ClubId == seller.Id && p.Position == PlayerPosition.A).Skip(2).ToList();
        for (int i = 0; i < weakerAttackers.Count; i++) weakerAttackers[i].Strength = 34 - i;
        for (int i = 0; i < extraAttackers; i++)
            world.Players.Add(new Player { ClubId = seller.Id, Name = $"Reserva {i}", Position = PlayerPosition.A,
                Strength = 30 - i, SalaryPerRound = 500m, ContractRoundsRemaining = 20 });
        seller.CashBalance = 20_000_000m;
        return (world, seller, buyer);
    }
}
