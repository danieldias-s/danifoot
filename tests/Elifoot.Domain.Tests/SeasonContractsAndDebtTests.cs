using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class SeasonContractsAndDebtTests
{
    [Fact]
    public void RenewalKeepsOwnershipAndWageAndBlocksSalesUntilSeasonChanges()
    {
        var world = WorldSeedFactory.CreateSeededWorld();
        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        var player = world.Players.Last(p => p.ClubId == seller.Id);
        world.Auctions.Clear();
        world.RoundNumber = 8;
        var wage = player.SalaryPerRound;
        player.IsListedForTransfer = true;
        player.AskingPrice = 50_000;
        seller.CashBalance = 0;
        Assert.True(ClassicEconomyEngine.RenewContract(world, seller, player, 200, "renew").Success);
        Assert.True(ClassicEconomyEngine.RenewContract(world, seller, player, 200, "renew").Success);
        Assert.False(ClassicEconomyEngine.RenewContract(world, seller, player, 20, "again").Success);
        Assert.Equal(world.TotalRoundsInSeason - 7, player.ContractRoundsRemaining);
        Assert.Equal(wage, player.SalaryPerRound);
        Assert.False(player.IsListedForTransfer);
        Assert.Single(world.Transactions, t => t.Category == FinancialTransactionCategory.ContractRenewal);
        Assert.False(VoluntaryAuctionEngine.Create(world, seller, player.Id, "voluntary").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(world, buyer.Id, seller.Id, player.Id, 50_000, "sale", "sale").Success);
        foreach (var p in world.Players.Where(p => p.ClubId == seller.Id))
            p.RenewedForSeason = world.SeasonNumber;
        ClassicEconomyEngine.EvaluateInsolvency(world);
        Assert.Empty(world.Auctions);
        Assert.True(seller.CashBalance < 0);

        world = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        seller = world.Clubs.Single(c => c.Id == seller.Id);
        player = world.Players.Single(p => p.Id == player.Id);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(world, player));
        world.SeasonNumber++;
        world.RoundNumber = 1;
        Assert.False(ClassicEconomyEngine.IsSeasonRenewed(world, player));
        Assert.Equal(seller.Id, player.ClubId);
        ClassicEconomyEngine.EvaluateInsolvency(world);
        Assert.Single(world.Auctions, a => a.FormerClubId == seller.Id);
    }

    [Fact]
    public void ForcedAuctionStartsAtZeroLastsOneGlobalRoundAndSelectsNextEligible()
    {
        var world = WorldSeedFactory.CreateSeededWorld();
        var seller = world.Clubs[0];
        var buyer = world.Clubs[1];
        var squad = world.Players.Where(p => p.ClubId == seller.Id).ToList();
        var outfield = squad.Where(p => p.Position != PlayerPosition.G).TakeLast(3).ToList();
        world.Auctions.Clear();
        foreach (var player in squad) player.Strength = 10;
        outfield[0].Strength = 50;
        outfield[0].RenewedForSeason = world.SeasonNumber;
        outfield[1].Strength = 49;
        outfield[2].Strength = 48;
        seller.CashBalance = -100_000;
        ClassicEconomyEngine.EvaluateInsolvency(world);
        var auction = Assert.Single(world.Auctions);
        Assert.Equal(outfield[1].Id, auction.PlayerId);
        Assert.Equal(0, auction.MinimumBid);
        Assert.Equal(1, auction.DurationRounds);
        Assert.True(ClassicEconomyEngine.PlaceBid(world, buyer, auction.Id, 0, "bid").Success);
        MarketAndEconomyEngine.SettleAuction(world, auction);
        Assert.False(auction.IsClosed);
        world.RoundState = RoundState.RESULTS;
        world.SettledFinancialRounds.Add($"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}");
        MarketAndEconomyEngine.SettleAuction(world, auction);
        Assert.True(auction.IsClosed);
        Assert.Equal(buyer.Id, outfield[1].ClubId);
        Assert.Equal(-100_000, seller.CashBalance);
        ClassicEconomyEngine.EvaluateInsolvency(world);
        Assert.Single(world.Auctions);
        world.RoundNumber++;
        world.RoundState = RoundState.PREPARING;
        ClassicEconomyEngine.EvaluateInsolvency(world);
        Assert.Equal(outfield[2].Id, world.Auctions.Single(a => !a.IsClosed).PlayerId);
    }

    [Theory]
    [InlineData(false, -510_000)]
    [InlineData(true, 75_000)]
    public void NegativeCashInterestAndBankInterestAreIndependent(bool borrow, int expectedCash)
    {
        var world = WorldSeedFactory.CreateSeededWorld();
        var club = world.Clubs[0];
        var opponent = world.Clubs[1];
        club.CashBalance = borrow ? -400_000 : -500_000;
        if (borrow) Assert.True(ClassicEconomyEngine.TakeClassicLoan(world, club, "loan").Success);
        var match = new MatchFixture { SeasonNumber = world.SeasonNumber, RoundNumber = world.RoundNumber,
            HomeClubId = club.Id, AwayClubId = opponent.Id, IsFinished = true, CurrentMinute = 90 };
        world.Fixtures = [match];
        world.RoundState = RoundState.RESULTS;
        ClassicEconomyEngine.SettleLoans(world, [match]);
        world.SettledFinancialRounds.Add($"round_settled:S{world.SeasonNumber}:R{world.RoundNumber}");
        ClassicEconomyEngine.SettleNegativeCashInterest(world);
        world = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        ClassicEconomyEngine.SettleNegativeCashInterest(world);
        Assert.Equal(expectedCash, world.Clubs.Single(c => c.Id == club.Id).CashBalance);
        Assert.Equal(borrow ? 0 : 1, world.Transactions.Count(t => t.Category == FinancialTransactionCategory.NegativeCashInterest));
        Assert.Equal(borrow ? 25_000 : 0, world.BankLoans.Sum(l => l.InterestCharged));
    }

    [Fact]
    public void TenDefendersUseTheCustomFormationAndStillRequireOneGoalkeeper()
    {
        var squad = new List<Player> { new() { Position = PlayerPosition.G } };
        squad.AddRange(Enumerable.Range(0, 10).Select(_ => new Player { Position = PlayerPosition.D }));
        var ids = squad.Select(p => p.Id).ToList();
        Assert.True(SquadValidator.ValidatePreMatchLineup("10-0-0", ids, [], squad, true).IsValid);
        Assert.False(SquadValidator.ValidatePreMatchLineup("11-0-0", ids, [], squad, true).IsValid);
        Assert.False(TacticalFormations.TryGet("5-0-5", false, out _));
        Assert.Equal(10, TacticalFormations.Resolve("10-0-0").Defenders);
    }
}
