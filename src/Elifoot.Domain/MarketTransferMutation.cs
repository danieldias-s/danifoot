using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Small undo journal for a single transfer, under the caller's room lock.
/// Does not copy the world or change snapshot/schema. The repository persists snapshot and ledger together.</summary>
internal sealed class MarketTransferMutation : IDisposable
{
    private readonly GameWorld world;
    private readonly Player player;
    private readonly Club buyer;
    private readonly Club? seller;
    private readonly decimal buyerCash, sellerCash;
    private readonly Guid? owner;
    private readonly bool listed, protectedContract;
    private readonly decimal? asking;
    private readonly decimal salary, marketPrice;
    private readonly int rounds;
    private readonly int? renewed;
    private readonly Guid[] starters, bench;
    private readonly int transactions, contracts, audits;
    private readonly (TransferOffer Offer, TransferOfferStatus Status)[] offers;
    private readonly (SalaryRaiseRequest Request, SalaryRequestStatus Status, string? Resolution)[] requests;
    private readonly SalaryEligibility? eligibility;
    private readonly string[] transactionKeys;
    private readonly string contractKey;
    private readonly bool hadContractKey;
    private bool committed;

    public MarketTransferMutation(GameWorld world, Player player, Club buyer, Club? seller,
        string contractKey, params string[] transactionKeys)
    {
        this.world = world; this.player = player; this.buyer = buyer; this.seller = seller;
        this.contractKey = contractKey; this.transactionKeys = transactionKeys;
        buyerCash = buyer.CashBalance; sellerCash = seller?.CashBalance ?? 0;
        owner = player.ClubId; listed = player.IsListedForTransfer; asking = player.AskingPrice;
        protectedContract = player.NegotiatedContractProtected; salary = player.SalaryPerRound;
        marketPrice = player.MarketPrice; rounds = player.ContractRoundsRemaining; renewed = player.RenewedForSeason;
        starters = seller?.PreferredStarterIds.ToArray() ?? []; bench = seller?.PreferredBenchIds.ToArray() ?? [];
        transactions = world.Transactions.Count; contracts = world.Economy.ContractHistory.Count; audits = world.AuditLogs.Count;
        offers = world.TransferOffers.Where(o => o.PlayerId == player.Id).Select(o => (o, o.Status)).ToArray();
        requests = world.Economy.SalaryRequests.Where(r => r.PlayerId == player.Id)
            .Select(r => (r, r.Status, r.Resolution)).ToArray();
        eligibility = world.Economy.SalaryEligibility.GetValueOrDefault(player.Id);
        hadContractKey = world.Economy.ContractKeys.Contains(contractKey)
            || world.Economy.ContractKeys.Count == 0 && world.Economy.ContractHistory.Any(h => h.Key == contractKey);
    }

    public void Commit() => committed = true;

    public void Dispose()
    {
        if (committed) return;
        buyer.CashBalance = buyerCash;
        if (seller != null)
        {
            seller.CashBalance = sellerCash;
            seller.PreferredStarterIds.Clear(); seller.PreferredStarterIds.AddRange(starters);
            seller.PreferredBenchIds.Clear(); seller.PreferredBenchIds.AddRange(bench);
        }
        player.ClubId = owner; player.IsListedForTransfer = listed; player.AskingPrice = asking;
        player.NegotiatedContractProtected = protectedContract; player.SalaryPerRound = salary;
        player.MarketPrice = marketPrice; player.ContractRoundsRemaining = rounds; player.RenewedForSeason = renewed;
        world.Transactions.RemoveRange(transactions, world.Transactions.Count - transactions);
        world.Economy.ContractHistory.RemoveRange(contracts, world.Economy.ContractHistory.Count - contracts);
        world.AuditLogs.RemoveRange(audits, world.AuditLogs.Count - audits);
        foreach (var key in transactionKeys) world.AppliedTransactionKeys.Remove(key);
        if (!hadContractKey) world.Economy.ContractKeys.Remove(contractKey);
        if (eligibility != null) world.Economy.SalaryEligibility[player.Id] = eligibility;
        else world.Economy.SalaryEligibility.Remove(player.Id);
        foreach (var (offer, status) in offers) offer.Status = status;
        foreach (var (request, status, resolution) in requests) { request.Status = status; request.Resolution = resolution; }
    }
}
