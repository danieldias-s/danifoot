using System.Security.Cryptography;
using System.Text;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static partial class ClassicEconomyEngine
{
    // A quote is read-only. Abandoning it has no persisted consequence.
    public static RenewalContractPreviewDto PreviewRenewal(GameWorld w,Club c,Player p,decimal? offeredSalary=null)
    {
        decimal required=Math.Max(p.SalaryPerRound,RequestedSalary(w,c,p));
        decimal fee=Math.Round(required*(SeasonContractRounds(w)/10m),2);
        string terms=FormattableString.Invariant($"renewal-v1:{w.Id}:{c.Id}:{p.Id}:{w.SeasonNumber}:{w.RoundNumber}:{p.SalaryPerRound}:{required}:{fee}:{p.RenewedForSeason}:{p.ContractRoundsRemaining}");
        string token=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms)));
        return new(p.Id,c.Id,w.SeasonNumber,w.RoundNumber,p.SalaryPerRound,required,fee,
            required>p.SalaryPerRound,token,w.RoundVersion,offeredSalary,
            offeredSalary is decimal offer?Math.Round(offer*(SeasonContractRounds(w)/10m),2):null,
            offeredSalary is decimal proposed?proposed>=required:null);
    }
}
