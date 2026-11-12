using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class RenewalNegotiationTests
{
    private static (GameWorld W, Club C, Player P) Setup()
    {
        var w = WorldSeedFactory.CreateSeededWorld(totalClubs: 32);
        w.Auctions.Clear(); w.Transactions.Clear(); w.AppliedTransactionKeys.Clear();
        var c = w.Clubs[0]; c.HumanCoachId = Guid.NewGuid();
        foreach (var p in w.Players.Where(p => p.ClubId == c.Id))
        { p.IsListedForTransfer = false; p.RenewedForSeason = null; p.ContractRoundsRemaining = 0; p.NegotiatedContractProtected = false; }
        var player = w.Players.Last(p => p.ClubId == c.Id && p.Position == PlayerPosition.A);
        player.SalaryPerRound = 100; player.Strength = 30;
        ClassicEconomyEngine.EnsureMigrated(w);
        return (w, c, player);
    }

    [Theory][InlineData(0)][InlineData(25)]
    public void AcceptedOfferPersistsExactSalaryOneFeeAndSeasonMark(int extra)
    {
        var (w, c, p) = Setup(); var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        decimal offer = quote.RequiredSalary + extra, cash = c.CashBalance;
        int strength = p.Strength;
        var preview = ClassicEconomyEngine.PreviewRenewal(w, c, p, offer);
        Assert.True(preview.OfferAccepted); Assert.Equal(offer, preview.OfferedSalary);
        Assert.True(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, offer, true, quote.TermsToken, "renew").Success);
        Assert.True(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, offer, true, quote.TermsToken, "renew").Success);
        Assert.Equal(offer, p.SalaryPerRound); Assert.Equal(strength, p.Strength);
        Assert.Equal(cash - preview.OfferedSigningFee, c.CashBalance);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(w, p)); Assert.Single(w.Transactions);
        Assert.Single(w.Economy.ContractHistory); Assert.Empty(w.Auctions);
        var saved = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var restored = saved.Players.Single(x => x.Id == p.Id); var owner = saved.Clubs.Single(x => x.Id == c.Id);
        Assert.True(ClassicEconomyEngine.DecideRenewalOffer(saved, owner, restored, offer, true, quote.TermsToken, "renew").Success);
        Assert.Single(saved.Transactions); Assert.True(ClassicEconomyEngine.IsSeasonRenewed(saved, restored));
    }

    [Fact] public void LowOfferAndPreviewNeverRenewOrAuctionWithoutExplicitDecision()
    {
        var (w, c, p) = Setup(); decimal cash = c.CashBalance;
        var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p, 100);
        Assert.False(quote.OfferAccepted); Assert.True(quote.RequiredSalary > 100);
        var denied = ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 100, true, quote.TermsToken, "low");
        Assert.False(denied.Success); Assert.Contains("RENEWAL_COUNTER_OFFER", denied.Error);
        Assert.Equal(cash, c.CashBalance); Assert.Equal(c.Id, p.ClubId);
        Assert.Empty(w.Auctions); Assert.Empty(w.Economy.ContractHistory); Assert.Equal(100, p.SalaryPerRound);
    }

    [Fact] public void ExplicitCounterRefusalCreatesOneExistingSalaryAuctionAndPreservesCash()
    {
        var (w, c, p) = Setup(); decimal cash = c.CashBalance;
        c.PreferredBenchIds.Add(p.Id);
        var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        var first = ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 100, false, quote.TermsToken, "refuse");
        Assert.True(first.Success); Assert.NotNull(first.AuctionId);
        var repeat = ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 100, false, quote.TermsToken, "refuse");
        Assert.True(repeat.Success); Assert.Equal(first.AuctionId, repeat.AuctionId);
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 110, true, quote.TermsToken, "refuse").Success);
        var auction = Assert.Single(w.Auctions);
        Assert.Equal(AuctionReason.SalaryRenewalRefused, auction.Reason);
        Assert.Equal(0, auction.MinimumBid); Assert.Equal(1, auction.DurationRounds);
        Assert.Equal(c.Id, auction.FormerClubId); Assert.Null(p.ClubId);
        Assert.DoesNotContain(p.Id, c.PreferredStarterIds); Assert.DoesNotContain(p.Id, c.PreferredBenchIds);
        Assert.Equal(cash, c.CashBalance); Assert.Equal(100, p.SalaryPerRound);
        Assert.Empty(w.Transactions); Assert.Empty(w.Economy.SalaryRequests);
    }

    [Fact] public void MinimumSquadBlocksRefusalWithoutReleasingThePlayer()
    {
        var (w, c, p) = Setup();
        var keep = w.Players.Where(x => x.ClubId == c.Id && x.Id != p.Id).Take(10).Select(x => x.Id).Append(p.Id).ToHashSet();
        w.Players.RemoveAll(x => x.ClubId == c.Id && !keep.Contains(x.Id));
        var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        var result = ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 100, false, quote.TermsToken, "minimum");
        Assert.False(result.Success); Assert.Contains("Elenco mínimo", result.Error);
        Assert.Equal(c.Id, p.ClubId); Assert.Empty(w.Auctions);
    }

    [Theory][InlineData(-1)][InlineData(100.001)]
    public void InvalidAmountsAreRejectedBeforeMutation(double value)
    {
        var (w, c, p) = Setup(); var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, (decimal)value, true, quote.TermsToken, "bad").Success);
        Assert.Equal(100, p.SalaryPerRound); Assert.Empty(w.Transactions); Assert.Empty(w.Auctions);
    }

    [Fact] public void StaleTermsPendingSalaryAndRenewedPlayerAreProtected()
    {
        var (w, c, p) = Setup(); var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        p.Strength++;
        p.SalaryPerRound++;
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 200, true, quote.TermsToken, "stale").Success);
        quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        w.Economy.SalaryRequests.Add(new SalaryRaiseRequest { PlayerId = p.Id, ClubId = c.Id });
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 200, true, quote.TermsToken, "pending").Success);
        w.Economy.SalaryRequests.Clear(); p.RenewedForSeason = w.SeasonNumber;
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, 100, false, quote.TermsToken, "protected").Success);
        Assert.Equal(c.Id, p.ClubId); Assert.Empty(w.Auctions); Assert.Empty(w.Transactions);
    }

    [Fact] public void OverflowAndFalseRefusalDoNotChangeState()
    {
        var (w, c, p) = Setup(); var quote = ClassicEconomyEngine.PreviewRenewal(w, c, p);
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, decimal.MaxValue, true, quote.TermsToken, "overflow").Success);
        Assert.False(ClassicEconomyEngine.DecideRenewalOffer(w, c, p, quote.RequiredSalary, false, quote.TermsToken, "false-refusal").Success);
        Assert.Equal(100, p.SalaryPerRound); Assert.Equal(c.Id, p.ClubId); Assert.Empty(w.Transactions); Assert.Empty(w.Auctions);
    }
}
