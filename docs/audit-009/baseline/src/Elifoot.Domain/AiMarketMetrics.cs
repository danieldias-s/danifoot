using Elifoot.Contracts;

namespace Elifoot.Domain;

// A projection of recorded facts, never independently incremented counters.
// Callers must supply the complete records for the stated period (the live snapshot keeps a recent window).
public record AiMarketMetrics(int AiPurchaseEvaluations, int AiPurchaseAttempts, int AiOffersSubmitted,
    int AiOffersAccepted, int AiPurchasesCompleted, int AiAuctionBids, int AiAuctionsWon,
    int AiSalesCompleted, int AiContractRenewals)
{
    public static AiMarketMetrics FromRecords(Guid managerId, IEnumerable<FinancialTransaction> transactions,
        IEnumerable<AuditLogEntry> actions, IEnumerable<TransferOffer> offers)
    {
        var tx = transactions.Where(t => t.ManagerId == managerId).DistinctBy(t => t.Id).ToList();
        var log = actions.Where(a => a.ActorCoachId == managerId).DistinctBy(a => a.Id).ToList();
        int Count(FinancialTransactionCategory category) => tx.Count(t => t.Category == category);
        return new(log.Count(a => a.ActionType == "AiPurchaseEvaluation"), log.Count(a => a.ActionType == "AiPurchaseAttempt"),
            offers.Where(o => o.SubmittedByManagerId == managerId).DistinctBy(o => o.Id).Count(),
            tx.Count(t => t.Category == FinancialTransactionCategory.TransferPurchase && t.TransactionType == "AcceptedOffer"),
            Count(FinancialTransactionCategory.TransferPurchase) + Count(FinancialTransactionCategory.AuctionPurchase),
            log.Count(a => a.ActionType == "AiAuctionBid"), Count(FinancialTransactionCategory.AuctionPurchase),
            Count(FinancialTransactionCategory.TransferSale), Count(FinancialTransactionCategory.ContractRenewal));
    }
}
