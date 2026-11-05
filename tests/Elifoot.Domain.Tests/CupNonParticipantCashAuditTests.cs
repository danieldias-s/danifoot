using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class CupNonParticipantCashAuditTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClubOutsideCupReceivesOnlyItsContractedSponsorshipAndReplayDoesNotPayTwice(bool annual)
    {
        var world = WorldSeedFactory.CreateSeededWorld();
        world.Economy.OperatingRules = annual ? OperatingEconomyEngine.CurrentRules() : new();
        world.RoundNumber = 3;
        var observer = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var playing = world.Clubs.Where(c => c.Id != observer.Id).Take(2).ToArray();
        var fixture = new MatchFixture
        {
            GameWorldId = world.Id,
            SeasonNumber = world.SeasonNumber,
            RoundNumber = world.RoundNumber,
            CompetitionType = CompetitionType.CupRoundOf16,
            HomeClubId = playing[0].Id,
            AwayClubId = playing[1].Id,
            IsFinished = true,
            CurrentMinute = 90
        };
        decimal before = observer.CashBalance;
        var existing = world.Transactions.Select(t => t.Id).ToHashSet();
        decimal expected = annual
            ? Math.Round(72000m * 3 / world.TotalRoundsInSeason, 2) - Math.Round(72000m * 2 / world.TotalRoundsInSeason, 2)
            : 72000m;

        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, [fixture]);
        var entries = world.Transactions.Where(t => t.ClubId == observer.Id && !existing.Contains(t.Id)).ToList();
        var sponsorship = Assert.Single(entries);
        Assert.Equal(FinancialTransactionCategory.Sponsorship, sponsorship.Category);
        Assert.Equal(expected, sponsorship.Amount);
        Assert.Equal(before + expected, observer.CashBalance);
        Assert.True(fixture.TicketRevenue > 0);
        Assert.Contains(world.Transactions, t => t.ClubId == playing[0].Id && t.Category == FinancialTransactionCategory.TicketRevenue);
        Assert.Contains(world.Transactions, t => t.ClubId == playing[1].Id && t.Category == FinancialTransactionCategory.TicketRevenue);

        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(world, [fixture]);
        Assert.Equal(before + expected, observer.CashBalance);
        Assert.Single(world.Transactions, t => t.ClubId == observer.Id && !existing.Contains(t.Id));
    }
}
