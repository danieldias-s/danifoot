using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Theory]
    [InlineData(199801)]
    [InlineData(199802)]
    public void AiAuctionMarketOverOneOfficialSeason(int seed)
    {
        var world = WorldSeedFactory.CreateSeededWorld(seed: seed, totalClubs: 128,
            forceEvolutionMode: ForceEvolutionMode.ClassicAdapted);
        var result = new ExperimentResult();
        int fixedListings = world.Players.Count(p => p.IsListedForTransfer);
        int matches = 0;
        for (int round = 1; round <= world.TotalRoundsInSeason; round++)
        {
            world.RoundNumber = round;
            world.RoundState = RoundState.PREPARING;
            CompetitionAndSeasonEngine.EnsureCupFixturesForRound(world, round);
            var fixtures = world.Fixtures.Where(f => f.SeasonNumber == 1 && f.RoundNumber == round)
                .OrderBy(f => f.DeterministicMatchIndex).ToList();
            SimulateRoundWithRealMatchEngine(world, fixtures, result);
            Assert.All(fixtures, fixture => Assert.True(fixture.IsFinished));
            matches += fixtures.Count;

            world.RoundState = RoundState.RESULTS;
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, fixtures);
            CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(world, fixtures);
            ClassicEconomyEngine.ResolvePendingAiSalaryRequests(world);

            var previouslyListed = world.Players.Where(p => p.IsListedForTransfer).Select(p => p.Id).ToHashSet();
            int beforeMarket = world.Transactions.Count;
            MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(world);
            var newListings = world.Players.Where(p => p.IsListedForTransfer).Select(p => p.Id)
                .Concat(world.Transactions.Skip(beforeMarket)
                    .Where(t => t.Category == FinancialTransactionCategory.TransferSale && t.TransactionType == "DirectPurchase")
                    .Select(t => t.PlayerId!.Value));
            fixedListings += newListings.Except(previouslyListed).Count();
            Assert.All(world.Auctions.Where(a => !a.IsClosed && a.Reason == AuctionReason.Voluntary),
                a => Assert.False(world.Players.Single(p => p.Id == a.PlayerId).IsListedForTransfer));
            Assert.All(world.Auctions.Where(a => !a.IsClosed && a.AiOpenedAfterCompletedRounds != null)
                .GroupBy(a => a.FormerClubId), group => Assert.Single(group));
        }

        var voluntary = world.Auctions.Where(a => a.Reason == AuctionReason.Voluntary).ToList();
        foreach (var seller in voluntary.GroupBy(a => a.FormerClubId))
        {
            var rounds = seller.Select(a => a.AiOpenedAfterCompletedRounds!.Value).Order().ToArray();
            Assert.All(rounds.Zip(rounds.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 2));
        }
        var auctionSales = world.Transactions.Where(t => t.TransactionType == "VoluntaryAuction"
            && t.Category == FinancialTransactionCategory.AuctionSale).ToList();
        Assert.Equal(voluntary.Count(a => a.ClosureReason == "SOLD"), auctionSales.Count);
        foreach (var sale in auctionSales)
        {
            var purchase = Assert.Single(world.Transactions, t => t.Id == sale.TransferTransactionId);
            Assert.Equal(0, sale.Amount + purchase.Amount);
            Assert.Equal(sale.PlayerId, purchase.PlayerId);
        }

        var statistics = new
        {
            Seed = seed, Rounds = world.TotalRoundsInSeason, Matches = matches, FixedListings = fixedListings,
            FixedSales = world.Transactions.Count(t => t.Category == FinancialTransactionCategory.TransferSale && t.TransactionType == "DirectPurchase"),
            DirectNegotiations = world.Transactions.Count(t => t.Category == FinancialTransactionCategory.TransferSale && t.TransactionType == "NegotiatedTransfer"),
            VoluntaryOpened = voluntary.Count,
            WithBids = voluntary.Count(a => a.HighestBidderClubId != null),
            Sold = auctionSales.Count,
            WithoutBuyer = voluntary.Count(a => a.ClosureReason == "AUCTION_UNSOLD"),
            OpenAtEnd = voluntary.Count(a => !a.IsClosed),
            Revenue = auctionSales.Sum(t => t.Amount),
            EligibilityErrors = world.Clubs.Count(c => !SquadValidator.ValidateClubSquadLimits(
                world.Players.Where(p => p.ClubId == c.Id).ToList()).IsValid)
        };
        Assert.Equal(0, statistics.EligibilityErrors);
        Assert.True(statistics.FixedSales > 0);
        Assert.True(statistics.DirectNegotiations > 0);
        Assert.True(statistics.VoluntaryOpened > 0);
        Assert.True(statistics.Sold > 0);
        if (Environment.GetEnvironmentVariable("ELIFOOT_AI_AUCTION_REPORT_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"market-{seed}.json"),
                JsonSerializer.Serialize(statistics, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
