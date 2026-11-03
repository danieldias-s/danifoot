using Elifoot.Contracts;
namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    public static bool IsSeasonRenewed(GameWorld w, Player p)
        => w.Economy.Rules.SeasonContractsEnabled && p.ClubId.HasValue && p.RenewedForSeason==w.SeasonNumber;

    public static int SeasonContractRounds(GameWorld w)
        => Math.Max(1,w.TotalRoundsInSeason-w.RoundNumber+(w.RoundState==RoundState.RESULTS?0:1));

    public static decimal SeasonRenewalFee(GameWorld w,Player p)
        => Math.Round(p.SalaryPerRound*SeasonContractRounds(w)/10m,2);

    private static void ProtectSeasonContract(GameWorld world, Player player)
    {
        player.RenewedForSeason = world.SeasonNumber;
        player.ContractRoundsRemaining = SeasonContractRounds(world);
        if (player.IsListedForTransfer)
        {
            player.IsListedForTransfer = false;
            player.AskingPrice = null;
            player.TransferListingVersion++;
        }
        foreach (var offer in world.TransferOffers.Where(offer => offer.PlayerId == player.Id
            && offer.Status is TransferOfferStatus.Pending or TransferOfferStatus.Countered))
            offer.Status = TransferOfferStatus.Cancelled;
    }

    /// Negative cash only, once per completed global round; never bank principal.
    public static void SettleNegativeCashInterest(GameWorld w)
    {
        EnsureMigrated(w);
        string roundKey=$"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}";
        var matches=w.Fixtures.Where(m=>m.SeasonNumber==w.SeasonNumber&&m.RoundNumber==w.RoundNumber&&!m.IsCancelled&&!m.IsPostponed).ToList();
        if(w.RoundState!=RoundState.RESULTS||!w.SettledFinancialRounds.Contains(roundKey)
            ||matches.Count==0||matches.Any(m=>!m.IsFinished)||!matches.Any(Played))return;
        if(!w.Economy.OverdraftInterestRounds.Add(roundKey))return;
        foreach(var club in w.Clubs.Where(c=>c.CashBalance<0).OrderBy(c=>c.Id))
        {
            decimal negative=-club.CashBalance;
            decimal interest=Math.Round(negative*w.Economy.Rules.NegativeCashInterestPerRound,2,MidpointRounding.AwayFromZero);
            if(interest<=0)continue;
            MarketAndEconomyEngine.TryRecordTransaction(w,club,FinancialTransactionCategory.NegativeCashInterest,-interest,
                $"Juros de caixa negativo: 2% de R$ {negative:N2}; rodada global concluída",
                $"negative-cash:{club.Id}:S{w.SeasonNumber}:R{w.RoundNumber}");
        }
    }

    public static bool IsSpecialAuctionDue(GameWorld w,AuctionItem auction)
    {
        if(auction.ExpiresAtSeason is not int season)
            return w.SeasonNumber>auction.CreatedAtSeason||w.RoundNumber>=auction.ExpiresAtRound;
        return w.RoundState==RoundState.RESULTS
            &&w.SettledFinancialRounds.Contains($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}")
            &&(w.SeasonNumber>season||w.SeasonNumber==season&&w.RoundNumber>=auction.ExpiresAtRound);
    }
}
