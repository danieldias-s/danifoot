using Elifoot.Contracts;

namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    /// <summary>Only an explicit decision mutates the contract or releases the player.</summary>
    public static (bool Success, string? Error, Guid? AuctionId) DecideRenewalOffer(
        GameWorld world, Club club, Player player, decimal offeredSalary, bool accept,
        string termsToken, string key)
    {
        if (!world.Clubs.Contains(club) || !world.Players.Contains(player))
            return (false, "CONTRACT_AUTHORITY: Clube ou jogador não pertence a esta sala.", null);
        EnsureMigrated(world);
        string payload = FormattableString.Invariant($"{player.Id}:{offeredSalary}:{accept}:{termsToken}");
        var receipt = Receipt(world, club, key, "RENEWAL_NEGOTIATION", payload);
        if (receipt.Found)
            return (receipt.Valid, receipt.Valid ? null : "Chave usada por outra operação.",
                receipt.Valid && !accept ? receipt.Receipt?.EntityId : null);
        if (string.IsNullOrWhiteSpace(key) || !Safe(world) || player.ClubId != club.Id)
            return (false, "Renovação disponível apenas para seu jogador entre partidas.", null);
        if (!world.Economy.Rules.SeasonContractsEnabled)
            return (false, "Negociação anual indisponível nesta sala.", null);
        if (IsSeasonRenewed(world, player))
            return (false, "Contrato já renovado até o fim desta temporada.", null);
        if (AuctionReserved(world, player.Id))
            return (false, "Jogador está reservado para um leilão em andamento.", null);
        if (PendingForPlayer(world, player.Id).Any())
            return (false, "SALARY_PENDING_DECISION: Resolva primeiro o pedido espontâneo.", null);
        if (offeredSalary < 0 || offeredSalary != Math.Round(offeredSalary, 2)
            || player.SalaryPerRound < 0 || player.SalaryPerRound != Math.Round(player.SalaryPerRound, 2))
            return (false, "CONTRACT_VALUE_INVALID: Informe salário válido com até duas casas decimais.", null);
        RenewalContractPreviewDto preview;
        try { preview = PreviewRenewal(world, club, player); }
        catch (OverflowException) { return (false, "CONTRACT_VALUE_INVALID: Salário excede o limite financeiro.", null); }
        if (string.IsNullOrEmpty(termsToken) || termsToken != preview.TermsToken)
            return (false, "RENEWAL_TERMS_CHANGED: A exigência mudou. Consulte novamente os valores.", null);
        if (accept)
        {
            if (offeredSalary < preview.RequiredSalary)
                return (false, "RENEWAL_COUNTER_OFFER: A oferta não atende ao salário exigido pelo jogador.", null);
            var renewed = RenewContract(world, club, player, 0, key, offeredSalary, termsToken);
            if (!renewed.Success) return (false, renewed.Error, null);
            Remember(world, club, key, "RENEWAL_NEGOTIATION", payload, player.Id);
            return (true, null, null);
        }
        if (offeredSalary >= preview.RequiredSalary)
            return (false, "RENEWAL_COUNTER_OFFER: Não há contraproposta recusada para esta oferta.", null);
        var available = world.Players.Where(p => p.ClubId == club.Id && !AuctionReserved(world, p.Id)).ToList();
        var removal = SquadValidator.CanRemovePlayerFromSquad(available, player.Id);
        if (!removal.CanRemove)
            return (false, "Elenco mínimo: recomponha o elenco antes de recusar a contraproposta.", null);
        using var mutation = new MarketTransferMutation(world, player, club, club, $"renewal-refusal:{club.Id}:{key}");
        mutation.TrackCommand(club, key);
        var auction = NewSpecialAuction(world, player, club, AuctionReason.SalaryRenewalRefused,
            $"renewal-counter-refused:{club.Id}:{key}");
        club.PreferredStarterIds.RemoveAll(id => id == player.Id);
        club.PreferredBenchIds.RemoveAll(id => id == player.Id);
        player.ClubId = null;
        player.ContractRoundsRemaining = 0;
        player.NegotiatedContractProtected = false;
        player.IsListedForTransfer = false;
        player.AskingPrice = null;
        world.Economy.SalaryEligibility.Remove(player.Id);
        Remember(world, club, key, "RENEWAL_NEGOTIATION", payload, auction.Id);
        mutation.Commit();
        return (true, null, auction.Id);
    }
}
