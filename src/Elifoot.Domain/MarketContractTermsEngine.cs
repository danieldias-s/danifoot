using System.Security.Cryptography;
using System.Text;

namespace Elifoot.Domain;

/// <summary>One signing calculation for preview, human confirmations, AI and auction settlement.</summary>
public static class MarketContractTermsEngine
{
    public static decimal SigningSalary(Player player)=>MarketAndEconomyEngine.CalculateFairSalaryPerRound(player);

    // Consent fingerprint, not an authorization credential. Ownership and finances are validated separately.
    public static string SalaryTermsToken(GameWorld world,Player player)
    {
        string terms=FormattableString.Invariant($"salary-v1:{world.Id}:{player.Id}:{player.ClubId}:{player.SalaryPerRound}:{SigningSalary(player)}:{player.ContractRoundsRemaining}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms)));
    }

    public static string? ValidateConfirmation(GameWorld world,Player player,string? token)
        =>string.IsNullOrWhiteSpace(token)?"CONTRACT_PREVIEW_REQUIRED: Consulte e confirme o salário da contratação."
        :token!=SalaryTermsToken(world,player)?"CONTRACT_TERMS_CHANGED: O salário ou contrato mudou. Consulte os valores atuais e confirme novamente.":null;
}
